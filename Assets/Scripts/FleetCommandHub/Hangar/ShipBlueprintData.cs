using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    Collector,
    Sensors
}

[System.Serializable]
public class WeaponMount
{
    public Vector2 Position;
    [HideInInspector] public Quaternion OriginalRotation;

    public float MaxAngle;
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
    RevealFogRadius
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
        string statName = EnumUtils.GetStatName(StatType);

        string sign = displayValue > 0 ? "+" : "";

        if (ModifierType == ModifierType.PercentAdd)
            return $"{sign}{Mathf.RoundToInt(displayValue * 100f)}% {statName}";
        else
            return $"{sign}{displayValue} {statName}";
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
    public Mesh ShipMesh;

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
    public float BaseRevealFogRadius = 25f;
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

    public bool IsWeaponless()
    {
        foreach (WeaponMount mount in Mounts)
        {
            if (mount.EquippedWeapon != null) return false;
        }

        return true;
    }

    public bool CanEquipWeaponInMount(WeaponData data, WeaponMount mount)
    {
        int remainingCap = RemainingWeaponCapacity;
        if (mount.EquippedWeapon != null) return remainingCap + mount.EquippedWeapon.EquipCost >= data.EquipCost;
        else return remainingCap >= data.EquipCost;
    }

    public bool CanRefundSystem(ShipSystem system, out string reason)
    {
        reason = "";
        if (!system.CanBeRefunded())
        {
            reason = "Upgrade level already at minimum";
            return false;
        }

        // simulate the refund
        system.UpgradeLevel--;

        // check if weapon cap will be exceeded
        if (RemainingWeaponCapacity < 0)
        {
            // undo the refund
            system.UpgradeLevel++;

            reason = "Weapon capacity will be exceeded if refunded. Remove some weapons first.";
            return false;
        }

        system.UpgradeLevel++;
        return true;
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
            case ShipStat.RevealFogRadius: baseValue = BaseRevealFogRadius; break;
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

#if UNITY_EDITOR
    // Adds a right-click option on the ScriptableObject in the top of the Inspector menu
    /// <summary>
    /// Syncs the weapon mounts on the prefab of the ship and clears all weapons
    /// </summary>
    [ContextMenu("Sync Weapon Mounts From Prefab")]
    public void SyncMounts()
    {
        if (ShipPrefab == null)
        {
            Debug.LogError($"No ShipVisualPrefab assigned to Ship Blueprint");
            return;
        }

        var visualizers = ShipPrefab.GetComponentsInChildren<WeaponMountVisualiser>(true);
        Mounts.Clear();

        foreach (var vis in visualizers)
        {
            Mounts.Add(new WeaponMount
            {
                Position = vis.transform.localPosition,
                OriginalRotation = vis.transform.localRotation,
                MaxAngle = vis.MaxAngleLimit,
                EquippedWeapon = null,
            });
        }

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }
#endif
}