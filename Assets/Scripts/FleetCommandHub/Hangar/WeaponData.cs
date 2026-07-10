using System.Collections.Generic;
using UnityEngine;

public enum WeaponID : byte
{
    None = 0,
    PDC = 1,
    Laser = 2,
    Missile = 3,
    Railgun = 4,
    SuperHeavyGun = 5,
    FlakShotgun = 6,
}

public enum WeaponStat
{
    Damage = 0,
    FireRate = 1,
    Range = 2,
    Spread = 3,
    HitRadius = 4,
    ExplosionRadius = 5,
    ProjectileSpeed = 6,
    ProjectileTurnSpeed = 7,
    PierceCount = 8,
    TriggerTime = 9,
    ProjectileCount = 10,
}

public enum ModifierType : byte
{
    FlatAdd,
    PercentAdd,
    Unlock    
}

[System.Serializable]
public class WeaponUpgradeEffect
{
    public WeaponStat StatType;
    public ModifierType ModifierType;
    public float Value;

    public string GetDescription()
    {
        float displayValue = Value;
        string statName = EnumUtils.GetStatName(StatType);

        if (StatType == WeaponStat.Spread)
        {
            displayValue = -Value;
        }

        string sign = displayValue > 0 ? "+" : "";

        if (ModifierType == ModifierType.PercentAdd)
            return $"{sign}{Mathf.RoundToInt(displayValue * 100f)}% {statName}";
        else
            return $"{sign}{displayValue} {statName}";
    }
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
    public string AbbrWpnName;
    public WeaponID WeaponID;
    public GameObject WeaponPrefab;
    public int EquipCost;

    public int UpgradeLevel;
    public int MaxUpgradeLevel { get { return Upgrades.Count; } }
    public List<WeaponUpgrade> Upgrades = new();

    [Header("Base Targeting Stats")]
    public bool IgnoreObstacles = false;
    public bool RequireTargetEntity = false;
    public float BaseRange = 30f;
    /// <summary>
    /// Higher number prefers higher density
    /// </summary>
    public float DensityBias = 1.0f;
    /// <summary>
    /// Higher number prefers higher target value/threat level
    /// </summary>
    public float ThreatBias = 1.0f;
    /// <summary>
    /// Higher number prefers less rotation
    /// </summary>
    public float AngleBias = 2.0f;
    /// <summary>
    /// Higher number prefers closer targets, larger negative prefers far targets
    /// </summary>
    public float DistanceBias = 2.0f;

    [Header("Base Combat Stats")]
    public float BaseDamage = 10f;
    public float BaseFireRate = 0.5f;
    public float BaseSpreadDegrees = 5f;

    [Header("Projectile Stats (Ignore if Hitscan)")]
    public int BaseProjectileCount = 1;
    public float BaseProjectileSpeed = 50f;
    public float BaseProjectileTurnSpeed = 5;
    public float BaseHitRadius = 1.0f;
    public float BaseExplosionRadius = 0f;
    public int BasePierceCount = 0;
    public float BaseTriggerTime = 0;

    public void ResetToBase()
    {
        UpgradeLevel = 0;
    }

    public bool CanBeUpgraded()
    {
        return UpgradeLevel < MaxUpgradeLevel;
    }

    public bool CanBeRefunded()
    {
        return UpgradeLevel > 0;
    }

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

    public int GetStatInt(WeaponStat statType)
    {
        return Mathf.RoundToInt(GetStat(statType));
    }

    public float GetStat(WeaponStat statType)
    {
        // Set base values
        float baseValue = 0f;
        switch (statType)
        {
            case WeaponStat.Damage: baseValue = BaseDamage; break;
            case WeaponStat.FireRate: baseValue = BaseFireRate; break;
            case WeaponStat.Range: baseValue = BaseRange; break;
            case WeaponStat.Spread: baseValue = BaseSpreadDegrees; break;
            case WeaponStat.HitRadius: baseValue = BaseHitRadius; break;
            case WeaponStat.ExplosionRadius: baseValue = BaseExplosionRadius; break;
            case WeaponStat.ProjectileSpeed: baseValue = BaseProjectileSpeed; break;
            case WeaponStat.ProjectileTurnSpeed: baseValue = BaseProjectileTurnSpeed; break;
            case WeaponStat.PierceCount: baseValue = BasePierceCount; break;
            case WeaponStat.TriggerTime: baseValue = BaseTriggerTime; break;
            case WeaponStat.ProjectileCount: baseValue = BaseProjectileCount; break;
        }

        float flatBonus = 0f;
        float percentBonus = 0f;


        for (int i = 0; i < UpgradeLevel; i++)
        {
            if (i >= Upgrades.Count) break; // Safety check

            foreach (var effect in Upgrades[i].Effects)
            {
                if (effect.StatType == statType)
                {
                    if (effect.ModifierType == ModifierType.FlatAdd)
                        flatBonus += effect.Value;
                    else if (effect.ModifierType == ModifierType.PercentAdd)
                        percentBonus += effect.Value;
                }
            }
        }

        float finalValue = (baseValue + flatBonus) * (1.0f + percentBonus);
        return finalValue;
    }
}