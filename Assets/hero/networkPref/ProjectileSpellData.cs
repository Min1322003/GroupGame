using UnityEngine;

[CreateAssetMenu(fileName = "ProjectileSpell", menuName = "Weapons/Projectile Spell")]
public class ProjectileSpellData : SpellBase
{
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 12f;
    public float projectileLifetime = 3f;

    [Header("Spell behaviour")]
    public SpellProjectileEffect effect = SpellProjectileEffect.DamageAndKnockback;
    public float knockbackBonus;
    public float leechHealAmount = 5f;
}
