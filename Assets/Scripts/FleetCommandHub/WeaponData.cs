using System.Collections.Generic;
using UnityEngine;

public enum WeaponID : byte
{
    None = 0,
    PDC = 1,
    Laser = 2,
    Missile = 3,
}

public enum WeaponStat
{
    Damage,
    FireRate,
    Range,
    SpreadRadians,
    PierceCount,
    ExplosionRadius
}

public enum ModifierType : byte
{
    FlatAdd,
    PercentAdd
}

[System.Serializable]
public class WeaponUpgradeEffect
{
    public WeaponStat StatType;
    public ModifierType ModifierType;
    public float Value;
}

[System.Serializable]
public class WeaponUpgrade
{
    public int Cost;
    public string Description;

    public List<WeaponUpgradeEffect> Effects = new();
}

[CreateAssetMenu(fileName = "NewWeapon", menuName = "FleetCommand/Weapon")]
public class WeaponData : ScriptableObject
{
    public string WeaponName;
    public WeaponID WeaponID;
    public GameObject WeaponPrefab;
    public int EnergyCost;
    public int UpgradeLevel;
    public int MaxUpgradeLevel { get { return Upgrades.Count; } }
    public List<WeaponUpgrade> Upgrades = new();

    public int GetUpgradeCost()
    {
        if (UpgradeLevel >= MaxUpgradeLevel) return 0;
        return Upgrades[UpgradeLevel].Cost;
    }

    public int GetRefundValue()
    {
        if (UpgradeLevel <= 0) return 0;
        return Upgrades[UpgradeLevel - 1].Cost;
    }
}