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
    Engine = 1,
    Hull = 2,
    Shields = 3,
    Weapons = 4,
    Collector = 5,
    Sensors = 6,
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
    Cost = 0,
    MaxHealth = 1,
    Speed = 2,

    TurnSpeed = 4,
    CollectionRadius = 5,
    WeaponCapacity = 6,
    RevealFogRadius = 7,
    DamageReduction = 8,
    ShieldMaxCapacity = 9,
    ShieldRegenPerSecond = 10,
    ShieldDamage = 11,
    ShieldEfficiencyBonus = 12,
    ShieldRechargeDelay = 13,
    ShieldRadius = 14,
}

[System.Serializable]
public class ShipSystemState
{
    public ShipSystemDefinition Definition;
    [HideInInspector] public int UpgradeLevel;

    public ShipSystemID SystemID => Definition.SystemID;
    public int MaxUpgradeLevel => Definition != null ? Definition.MaxUpgradeLevel : 0;

    public bool CanBeUpgraded() => Definition != null && UpgradeLevel < MaxUpgradeLevel;
    public bool CanBeRefunded() => UpgradeLevel > 0;

    public int GetUpgradeCost() => Definition != null ? Definition.GetUpgradeCost(UpgradeLevel) : 0;
    public int GetRefundValue() => Definition != null ? Definition.GetRefundValue(UpgradeLevel) : 0;
}

[CreateAssetMenu(fileName = "NewShipBlueprint", menuName = "FleetCommand/Ship Blueprint")]
public class ShipBlueprintData : ScriptableObject
{
    public string ShipName;
    public ShipClass ShipClass;
    public ShipID ShipID;
    public GameObject ShipPrefab;
    public Mesh ShipMesh;

    public List<ShipSystemState> ShipSystems = new();

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

    [Header("Avoidance")]
    public float AvoidanceRadius;
    public Vector2 AvoidanceOffset;

    [Header("Base Stats")]
    public float BaseCost = 5f;
    public float BaseMaxHealth = 10f;
    public float BaseDamageReduction = 0f;
    public float BaseSpeed = 15f;
    public float BaseTurnSpeed = 15f;
    public float BaseRevealFogRadius = 25f;
    public float BaseCollectionRadius = 5f;

    [Header("Base Shield Stats")]
    public bool HasShield = true;
    public Vector2 ShieldOffset;
    public float ShieldRadius;
    public float ShieldRechargeDelay = 1;
    public float BaseShieldMaxCapacity;
    public float BaseShieldRegenPerSecond;
    public float ShieldDamage;
    [Tooltip("1 means shield takes no capacity to deal damage, 0 means shield takes capacity equals to damage dealt")]
    public float ShieldEfficiencyBonus = 0;

    public void ResetToBase()
    {
        foreach (ShipSystemState system in ShipSystems)
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

    public bool CanRefundSystem(ShipSystemState system, out string reason)
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
            case ShipStat.DamageReduction: baseValue = BaseDamageReduction; break;
            case ShipStat.Speed: baseValue = BaseSpeed; break;
            case ShipStat.TurnSpeed: baseValue = BaseTurnSpeed; break;
            case ShipStat.RevealFogRadius: baseValue = BaseRevealFogRadius; break;
            case ShipStat.CollectionRadius: baseValue = BaseCollectionRadius; break;
            case ShipStat.WeaponCapacity: baseValue = BaseWeaponCapacity; break;
            case ShipStat.ShieldMaxCapacity: baseValue = BaseShieldMaxCapacity; break;
            case ShipStat.ShieldRegenPerSecond: baseValue = BaseShieldRegenPerSecond; break;
            case ShipStat.ShieldDamage: return ShieldDamage;
            case ShipStat.ShieldEfficiencyBonus: return ShieldEfficiencyBonus;
            case ShipStat.ShieldRechargeDelay: return ShieldRechargeDelay;
            case ShipStat.ShieldRadius: return ShieldRadius;
        }

        float flatBonus = 0f;
        float percentBonus = 0f;

        // add all bonuses from all ship systems
        foreach (ShipSystemState system in ShipSystems)
        {
            for (int i = 0; i < system.UpgradeLevel; i++)
            {
                if (i >= system.Definition.Upgrades.Count) break; // Safety check

                foreach (var effect in system.Definition.Upgrades[i].Effects)
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
    [ContextMenu("Sync Variables From Prefab")]
    public void SyncMounts()
    {
        if (ShipPrefab == null)
        {
            Debug.LogError($"No ShipVisualPrefab assigned to Ship Blueprint");
            return;
        }

        RadiusVisualiser radius = ShipPrefab.GetComponentInChildren<RadiusVisualiser>(true);
        if (radius != null)
        {
            AvoidanceRadius = radius.VisualRadius;
            AvoidanceOffset = radius.transform.localPosition;
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

        ShieldVisualiser shield = ShipPrefab.GetComponentInChildren<ShieldVisualiser>(true);
        if (shield != null)
        {
            HasShield = true;
            ShieldRadius = shield.VisualRadius;
            ShieldOffset = shield.transform.localPosition;
        }
        else
        {
            HasShield = false;
        }

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }
#endif
}