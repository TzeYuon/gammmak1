using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSystemDefinition", menuName = "FleetCommand/Ship System Definition")]
public class ShipSystemDefinition : ScriptableObject
{
    public ShipSystemID SystemID;

    public List<ShipSystemUpgrade> Upgrades = new();

    public int MaxUpgradeLevel => Upgrades.Count;

    public int GetUpgradeCost(int currentLevel)
    {
        if (currentLevel >= MaxUpgradeLevel) return 0;
        return Upgrades[currentLevel].Cost;
    }

    public int GetRefundValue(int currentLevel)
    {
        if (currentLevel <= 0) return 0;
        return Upgrades[currentLevel - 1].Cost;
    }
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
