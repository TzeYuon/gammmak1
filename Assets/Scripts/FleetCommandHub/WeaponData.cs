using UnityEngine;

public enum WeaponID : byte
{
    None = 0,
    PDC = 1,
}

[CreateAssetMenu(fileName = "NewWeapon", menuName = "FleetCommand/Weapon")]
public class WeaponData : ScriptableObject
{
    public string WeaponName;
    public WeaponID WeaponID;
    public int EnergyCost;
    public int UpgradeLevel;
    public int MaxUpgradeLevel = 5;
    public int BaseUpgradeCost = 100;

    public float BaseDamage;
    public float BaseFireRate;
}