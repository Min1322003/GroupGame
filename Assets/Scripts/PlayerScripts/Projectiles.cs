using UnityEngine;
using Unity.Netcode;

public enum SpellProjectileEffect
{
    /// <summary>Deal damage, knockback, despawn (pistol and generic bolts).</summary>
    DamageAndKnockback = 0,
    /// <summary>Swap this projectile owner with the hit player (Q-style).</summary>
    SwapWithTarget = 1,
    /// <summary>Big knockback via knockbackBonus; still uses damage for force scaling.</summary>
    Gust = 2,
    /// <summary>Normal hit, then heals some of the owner's accumulated damage.</summary>
    Leech = 3,
    /// <summary>Deal damage and pull the target toward the shot direction (toward caster).</summary>
    DamageAndPull = 4,
    /// <summary>Short-lived bolt; when lifetime ends, owner teleports to the projectile (dash).</summary>
    DashToProjectileOnExpiry = 5
}

public class Projectile : NetworkBehaviour
{
    public AudioSource shoot;
    // Sync init data so clients receive it correctly on spawn
    private NetworkVariable<Vector2> netDirection = new NetworkVariable<Vector2>(
        Vector2.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private NetworkVariable<float> netSpeed = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [HideInInspector] public Vector2 direction;
    [HideInInspector] public float speed;
    [HideInInspector] public float damage;
    [HideInInspector] public float lifetime;
    [HideInInspector] public ulong ownerClientId;
    [HideInInspector] public SpellProjectileEffect spellEffect = SpellProjectileEffect.DamageAndKnockback;
    [HideInInspector] public float knockbackBonus;
    [HideInInspector] public float leechHealAmount = 5f;

    private Rigidbody2D rb;
    private float spawnTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        shoot = GetComponent<AudioSource>();
    }

    public override void OnNetworkSpawn()
    {
        shoot.Play();

        spawnTime = Time.time;

        if (IsServer)
        {
            // Server writes NetworkVariables AFTER spawn so clients receive them
            netDirection.Value = direction;
            netSpeed.Value = speed;
        }

        // Both server and clients set velocity from NetworkVariables
        rb.linearVelocity = netDirection.Value * netSpeed.Value;

        // Subscribe so if client receives the value slightly late, it still applies
        netDirection.OnValueChanged += OnDirectionChanged;
        netSpeed.OnValueChanged += OnSpeedChanged;
    }

    public override void OnNetworkDespawn()
    {
        netDirection.OnValueChanged -= OnDirectionChanged;
        netSpeed.OnValueChanged -= OnSpeedChanged;
    }

    private void OnDirectionChanged(Vector2 prev, Vector2 current)
    {
        rb.linearVelocity = current * netSpeed.Value ;
    }

    private void OnSpeedChanged(float prev, float current)
    {
        rb.linearVelocity = netDirection.Value * current;
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        if (Time.time - spawnTime >= lifetime)
        {
            if (spellEffect == SpellProjectileEffect.DashToProjectileOnExpiry)
                ServerTeleportOwnerToProjectile(rb.position);

            GetComponent<NetworkObject>().Despawn();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        PlayerCombatStats target = other.GetComponentInParent<PlayerCombatStats>();
        if (target == null) return;
        if (target.OwnerClientId == ownerClientId) return;

        switch (spellEffect)
        {
            case SpellProjectileEffect.SwapWithTarget:
                ServerApplySwap(target);
                break;
            case SpellProjectileEffect.Leech:
                ServerApplyDamageAndKnockback(target, applyDamage: true, pullTowardCaster: false);
                ServerApplyLeechHeal();
                break;
            case SpellProjectileEffect.DamageAndPull:
                ServerApplyDamageAndKnockback(target, applyDamage: true, pullTowardCaster: true);
                break;
            case SpellProjectileEffect.DashToProjectileOnExpiry:
                // Mobility spell: hitting a player ends the shot without a dash.
                break;
            default:
                // DamageAndKnockback + Gust (Gust uses knockbackBonus on the asset)
                ServerApplyDamageAndKnockback(target, applyDamage: true, pullTowardCaster: false);
                break;
        }

        GetComponent<NetworkObject>().Despawn();
    }

    private void ServerApplySwap(PlayerCombatStats target)
    {
        NetworkObject ownerObj = ResolveOwnerPlayerObject();
        if (ownerObj == null) return;

        Rigidbody2D ownerRb = ownerObj.GetComponent<Rigidbody2D>();
        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (ownerRb == null || targetRb == null) return;

        Vector2 ownerPos = ownerRb.position;
        Vector2 targetPos = targetRb.position;

        PlayerCombatStats ownerStats = ownerObj.GetComponent<PlayerCombatStats>();
        if (ownerStats == null) return;

        ownerStats.TeleportToPositionClientRpc(
            targetPos,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { ownerClientId } }
            }
        );
        target.TeleportToPositionClientRpc(
            ownerPos,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { target.OwnerClientId } }
            }
        );
    }

    private void ServerApplyDamageAndKnockback(PlayerCombatStats target, bool applyDamage, bool pullTowardCaster)
    {
        if (applyDamage && damage > 0f)
            target.ServerAddDamage(damage);

        float totalDamage = target.accumulatedDamage.Value + (applyDamage ? damage : 0f);
        float force = target.baseKnockback + (totalDamage * target.damageMultiplier) + knockbackBonus;

        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { target.OwnerClientId }
            }
        };

        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        if (pullTowardCaster)
            dir = -dir;

        target.ApplyKnockbackClientRpc(dir, force, rpcParams);
    }

    private void ServerTeleportOwnerToProjectile(Vector2 worldPosition)
    {
        NetworkObject ownerObj = ResolveOwnerPlayerObject();
        if (ownerObj == null) return;

        PlayerCombatStats ownerStats = ownerObj.GetComponent<PlayerCombatStats>();
        if (ownerStats == null) return;

        ownerStats.TeleportToPositionClientRpc(
            worldPosition,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { ownerClientId } }
            }
        );
    }

    private void ServerApplyLeechHeal()
    {
        NetworkObject ownerObj = ResolveOwnerPlayerObject();
        if (ownerObj == null) return;

        PlayerCombatStats ownerStats = ownerObj.GetComponent<PlayerCombatStats>();
        if (ownerStats == null || leechHealAmount <= 0f) return;

        ownerStats.ServerHealAccumulatedDamage(leechHealAmount);
    }

    private NetworkObject ResolveOwnerPlayerObject()
    {
        if (NetworkManager.Singleton == null) return null;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(ownerClientId, out var client))
            return null;
        return client.PlayerObject;
    }
}