using UnityEngine;
using Unity.Netcode;

public class PlayerWeaponController : NetworkBehaviour
{
    [Header("Loadout")]
    public PistolData pistol;          // always equipped, assign in Inspector
    public SpellBase[] spells = new SpellBase[3]; // drag 3 spells in Inspector

    private float pistolCooldownTimer = 0f;
    private float[] spellCooldownTimers = new float[3];

    private PlayerController playerController;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    
    void Update()
    {
        if (!IsOwner) return;

        TickCooldowns();
        HandleWeaponInput();
    }

    void TickCooldowns()
    {
        if (pistolCooldownTimer > 0f)
            pistolCooldownTimer -= Time.deltaTime;

        for (int i = 0; i < spellCooldownTimers.Length; i++)
        {
            if (spellCooldownTimers[i] > 0f)
                spellCooldownTimers[i] -= Time.deltaTime;
        }
    }

    void HandleWeaponInput()
    {
        // Aim direction from mouse
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 aimDir = ((Vector2)mouseWorld - (Vector2)transform.position).normalized;

        // Pistol — left click
        if (Input.GetMouseButtonDown(0) && pistolCooldownTimer <= 0f)
        {
            FirePistolServerRpc(aimDir);
            pistolCooldownTimer = pistol.cooldown;
        }

        // Spells — Q, E, R
        KeyCode[] spellKeys = { KeyCode.Q, KeyCode.E, KeyCode.R };
        for (int i = 0; i < spellKeys.Length; i++)
        {
            if (spells[i] == null) continue;
            if (Input.GetKeyDown(spellKeys[i]) && spellCooldownTimers[i] <= 0f)
            {
                CastSpellServerRpc(i, aimDir);
                spellCooldownTimers[i] = spells[i].cooldown;
            }
        }
    }

    [ServerRpc]
    private void FirePistolServerRpc(Vector2 aimDir)
    {
        if (pistol == null || pistol.projectilePrefab == null) return;

        // Spawn projectile on server — NetworkObject auto-replicates to all clients
        GameObject obj = Instantiate(
            pistol.projectilePrefab,
            transform.position,
            Quaternion.identity
        );

        Projectile proj = obj.GetComponent<Projectile>();
        proj.direction = aimDir;
        proj.speed = pistol.projectileSpeed;
        proj.damage = pistol.damage;
        proj.lifetime = pistol.projectileLifetime;
        proj.ownerClientId = OwnerClientId;

        obj.GetComponent<NetworkObject>().Spawn();
    }

    [ServerRpc]
    private void CastSpellServerRpc(int spellIndex, Vector2 aimDir)
    {
        // Stub — spell implementations come next
        Debug.Log($"Spell {spellIndex} cast by {OwnerClientId} toward {aimDir}");
    }
}