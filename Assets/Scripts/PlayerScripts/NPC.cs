using UnityEngine;
using Unity.Netcode;

public class NPC : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    
    [Header("Explosion")]
    public float explosionDistance = 1.5f;
    public int projectileCount = 8;
    public float projectileSpeed = 10f;
    public float projectileDamage = 5f;
    public float projectileLifetime = 3f;
    public GameObject projectilePrefab;
    
    private Rigidbody2D rb;
    private PlayerController targetPlayer;
    private NetworkObject targetPlayerNetObj;
    private bool hasExploded = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Find the player (in single player, this is the only other NetworkBehaviour)
            // In multiplayer, you may need a more robust way to find all players
            FindTargetPlayer();
        }
    }

    void FixedUpdate()
    {
        if (!IsServer || hasExploded) return;
        if (targetPlayer == null)
        {
            FindTargetPlayer();
            return;
        }

        // Move toward player
        Vector2 directionToPlayer = ((Vector2)targetPlayer.transform.position - rb.position).normalized;
        rb.linearVelocity = directionToPlayer * moveSpeed;

        // Check if close enough to explode
        float distanceToPlayer = Vector2.Distance(rb.position, targetPlayer.transform.position);
        if (distanceToPlayer <= explosionDistance)
        {
            TriggerExplosion();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer || hasExploded) return;

        // Check if hit by a projectile
        Projectile projectile = other.GetComponent<Projectile>();
        if (projectile != null)
        {
            TriggerExplosion();
        }
    }

    private void FindTargetPlayer()
    {
        // Find the first player in the scene
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        if (players.Length > 0)
        {
            targetPlayer = players[0];
            targetPlayerNetObj = targetPlayer.GetComponent<NetworkObject>();
        }
    }

    private void TriggerExplosion()
    {
        hasExploded = true;
        rb.linearVelocity = Vector2.zero;

        // Spawn 8 projectiles in a circle pattern
        for (int i = 0; i < projectileCount; i++)
        {
            float angle = (i / (float)projectileCount) * Mathf.PI * 2f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;

            SpawnExplosionProjectileServerRpc(direction);
        }

        // Despawn the NPC after explosion
        Invoke(nameof(DespawnNPC), 0.1f);
    }

    [ServerRpc]
    private void SpawnExplosionProjectileServerRpc(Vector2 direction)
    {
        if (projectilePrefab == null) return;

        GameObject obj = Instantiate(
            projectilePrefab,
            transform.position,
            Quaternion.identity
        );

        Projectile proj = obj.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.direction = direction;
            proj.speed = projectileSpeed;
            proj.damage = projectileDamage;
            proj.lifetime = projectileLifetime;
            proj.ownerClientId = ulong.MaxValue; // NPC-spawned projectile (neutral)
            proj.spellEffect = SpellProjectileEffect.DamageAndKnockback;
        }

        obj.GetComponent<NetworkObject>().Spawn();
    }

    private void DespawnNPC()
    {
        if (IsServer && GetComponent<NetworkObject>() != null)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }
}
