using UnityEngine;
using Unity.Netcode;

public class PlayerCombatStats : NetworkBehaviour
{
    [Header("Knockback Settings")]
    public float baseKnockback = 3f;
    public float damageMultiplier = 0.1f;
    public float knockbackStopThreshold = 0.1f; // velocity below this = movement unlocked

    // Accumulated damage — everyone can read, only server writes
    public NetworkVariable<float> accumulatedDamage = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Rigidbody2D rb;
    private PlayerController playerController;
    public bool isKnockedBack = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController>();
    }

    void FixedUpdate()
    {
        if (!isKnockedBack) return;

        rb.linearVelocity *= 0.9f;

        if (rb.linearVelocity.magnitude < knockbackStopThreshold)
        {
            isKnockedBack = false;
            playerController.SetInputLocked(false);
            rb.linearVelocity = Vector2.zero;
        }
    }

    [ClientRpc]
    public void ApplyKnockbackClientRpc(Vector2 direction, float force, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return; 

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * force, ForceMode2D.Impulse);

        isKnockedBack = true;
        playerController.SetInputLocked(true);
    }

    // Server adds damage to the NetworkVariable
    public void ServerAddDamage(float damage)
    {
        if (!IsServer) return;
        accumulatedDamage.Value += damage;
    }

    public void ResetDamage()
    {
        accumulatedDamage.Value = 0f;
    }

    /// <summary>Server-only: reduce buildup damage (used by leech spells).</summary>
    public void ServerHealAccumulatedDamage(float amount)
    {
        if (!IsServer) return;
        accumulatedDamage.Value = Mathf.Max(0f, accumulatedDamage.Value - amount);
    }

    /// <summary>Owner applies teleport — works with client-authoritative NetworkTransform.</summary>
    [ClientRpc]
    public void TeleportToPositionClientRpc(Vector2 worldPosition, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        rb.linearVelocity = Vector2.zero;
        rb.position = worldPosition;
        isKnockedBack = false;
        playerController.SetInputLocked(false);
    }
}