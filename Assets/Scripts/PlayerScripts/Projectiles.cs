using UnityEngine;
using Unity.Netcode;

public class Projectile : NetworkBehaviour
{
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

    private Rigidbody2D rb;
    private float spawnTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public override void OnNetworkSpawn()
    {
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
            GetComponent<NetworkObject>().Despawn();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        PlayerCombatStats target = other.GetComponentInParent<PlayerCombatStats>();
        if (target == null) return;
        if (target.OwnerClientId == ownerClientId) return;

        target.ServerAddDamage(damage);

        float totalDamage = target.accumulatedDamage.Value + damage;
        float force = target.baseKnockback + (totalDamage * target.damageMultiplier);

        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { target.OwnerClientId }
            }
        };

        target.ApplyKnockbackClientRpc(direction, force, rpcParams);

        GetComponent<NetworkObject>().Despawn();
    }
}