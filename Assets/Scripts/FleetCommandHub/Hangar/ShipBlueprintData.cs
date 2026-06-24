using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unique ID for each ship blueprint.
/// </summary>
public enum ShipID : byte
{
    None = 0,
    
    // Frigate class
    Rodent = 1,
    Seeker = 2,

    // Destroyer class
    Explorer = 51,
    Crab = 52,

    // Cruiser class
    Sword = 101,
    Shield = 102,

    // Battleship class
    Bastion = 151,
    Invincible = 152,

    // Dreadnought class
}

public enum ShipClass : byte
{
    None = 0,
    Frigate = 1,
    Destroyer = 2,
    Cruiser = 3,
    Battleship = 4,
    Dreadnought = 5,
}

/// <summary>
/// Upgradable systems that the ships can carry.
/// </summary>
public enum ShipSystemID : byte
{
    None = 0,
    Engine,
    Hull,
    Shields,
    Weapons,
    Collector
}

[System.Serializable]
public class WeaponMount
{
    public Vector2 Position;
    public WeaponData EquippedWeapon;
}

public enum ShipStat
{
    Cost,
    MaxHealth,
    Speed,
    Acceleration,
    TurnSpeed,
    CollectionRadius,
    WeaponCapacity,
}

[System.Serializable]
public class ShipSystemUpgradeEffect
{
    public ShipStat StatType;
    public ModifierType ModifierType;
    public float Value;

    public string GetDescription()
    {
        float displayValue = Value;
        string statName = GetStatName(StatType);

        string sign = displayValue > 0 ? "+" : "";

        if (ModifierType == ModifierType.PercentAdd)
            return $"{sign}{Mathf.RoundToInt(displayValue * 100f)}% {statName}";
        else
            return $"{sign}{displayValue} {statName}";
    }

    private string GetStatName(ShipStat stat)
    {
        switch (stat)
        {
            case ShipStat.Cost:
                return "Deployment Cost";
            case ShipStat.MaxHealth:
                return "Hull";
            case ShipStat.Speed:
                return "Max Speed";
            case ShipStat.Acceleration:
                return "Acceleration";
            case ShipStat.TurnSpeed:
                return "Turn Speed";
            case ShipStat.CollectionRadius:
                return "Collection Radius";
            case ShipStat.WeaponCapacity:
                return "Weapon Capacity";
            default:
                return stat.ToString();
        }
    }
}

[System.Serializable]
public class ShipSystemUpgrade
{
    public int Cost;
    public string Description;

    public List<ShipSystemUpgradeEffect> Effects = new();
}

[System.Serializable]
public class ShipSystem
{
    public ShipSystemID SystemID;

    public int UpgradeLevel;
    public int MaxUpgradeLevel { get { return Upgrades.Count; } }
    public List<ShipSystemUpgrade> Upgrades = new();

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
}

[CreateAssetMenu(fileName = "NewShipBlueprint", menuName = "FleetCommand/Ship Blueprint")]
public class ShipBlueprintData : ScriptableObject
{
    public string ShipName;
    public ShipClass ShipClass;
    public ShipID ShipID;
    public GameObject ShipPrefab;

    public List<ShipSystem> ShipSystems = new();

    public int BaseWeaponCapacity;
    public int WeaponCapacity { get { return Mathf.RoundToInt(GetStat(ShipStat.WeaponCapacity)); } }
    public int UsedWeaponCapacity { 
        get {
            int capUsed = 0;
            foreach (WeaponMount mount in Mounts)
            {
                if (mount.EquippedWeapon == null) continue;
                capUsed += mount.EquippedWeapon.EquipCost;
            }
            return capUsed;
        } 
    }
    public int RemainingWeaponCapacity { get { return WeaponCapacity - UsedWeaponCapacity; } }
    public List<WeaponMount> Mounts = new();

    [Header("Base Stats")]
    public float BaseCost = 5f;
    public float BaseMaxHealth = 10f;
    public float BaseSpeed = 15f;
    public float BaseAcceleration = 15f;
    public float BaseTurnSpeed = 15f;
    public float BaseCollectionRadius = 5f;

    public void ResetToBase()
    {
        foreach (ShipSystem system in ShipSystems)
        {
            system.UpgradeLevel = 0;
        }

        foreach (WeaponMount mount in Mounts)
        {
            mount.EquippedWeapon = null;
        }
    }

    public bool CanEquipWeaponInMount(WeaponData data, WeaponMount mount)
    {
        int remainingCap = RemainingWeaponCapacity;
        if (mount.EquippedWeapon != null) return remainingCap + mount.EquippedWeapon.EquipCost >= data.EquipCost;
        else return remainingCap >= data.EquipCost;
    }

    public float GetStat(ShipStat statType)
    {
        // Set base values
        float baseValue = 0f;
        switch (statType)
        {
            case ShipStat.Cost: baseValue = BaseCost; break;
            case ShipStat.MaxHealth: baseValue = BaseMaxHealth; break;
            case ShipStat.Speed: baseValue = BaseSpeed; break;
            case ShipStat.Acceleration: baseValue = BaseAcceleration; break;
            case ShipStat.TurnSpeed: baseValue = BaseTurnSpeed; break;
            case ShipStat.CollectionRadius: baseValue = BaseCollectionRadius; break;
            case ShipStat.WeaponCapacity: baseValue = BaseWeaponCapacity; break;
        }

        float flatBonus = 0f;
        float percentBonus = 0f;

        // add all bonuses from all ship systems
        foreach (ShipSystem system in ShipSystems)
        {
            for (int i = 0; i < system.UpgradeLevel; i++)
            {
                if (i >= system.Upgrades.Count) break; // Safety check

                foreach (var effect in system.Upgrades[i].Effects)
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
        }
        float finalValue = (baseValue + flatBonus) * (1.0f + percentBonus);
        return finalValue;
    }

    public int GetStatInt(ShipStat statType)
    {
        return Mathf.RoundToInt(GetStat(statType));
    }
}