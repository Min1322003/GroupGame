// PistolData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "PistolData", menuName = "Weapons/Pistol")]
public class PistolData : WeaponBase
{
    [Header("Pistol Stats")]
    public float projectileSpeed = 12f;
    public float projectileLifetime = 3f;
    public GameObject projectilePrefab; // assign in Inspector
}