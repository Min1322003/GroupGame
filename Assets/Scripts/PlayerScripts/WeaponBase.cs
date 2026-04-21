// WeaponBase.cs
using UnityEngine;

public abstract class WeaponBase : ScriptableObject
{
    [Header("Base Stats")]
    public string weaponName;
    public float damage = 10f;
    public float cooldown = 0.5f;
    public KeyCode inputKey;
}