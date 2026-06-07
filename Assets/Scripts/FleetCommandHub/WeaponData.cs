using UnityEngine;

public enum WeaponID : byte
{
    None = 0,
    PDC = 1,
    Laser = 2,
    Missile = 3,
}

[CreateAssetMenu(fileName = "NewWeapon", menuName = "FleetCommand/Weapon")]
public class WeaponData : ScriptableObject
{
    public string WeaponName;
    public WeaponID WeaponID;
    public int EnergyCost;
    public int UpgradeLevel;
    public int MaxUpgradeLevel { get { return UpgradeCosts.Length; } }
    public int[] UpgradeCosts;

    public int GetUpgradeCost()
    {
        if (UpgradeLevel >= MaxUpgradeLevel) return 0;
        return UpgradeCosts[UpgradeLevel];
    }

    public int GetRefundValue()
    {
        if (UpgradeLevel <= 0) return 0;
        return UpgradeCosts[UpgradeLevel - 1];
    }
}