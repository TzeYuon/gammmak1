using UnityEngine;

public enum WeaponSize : byte { Small = 0, Medium = 1, Large = 2 }
public enum WeaponID : byte
{
    None = 0,

}

[CreateAssetMenu(fileName = "NewWeapon", menuName = "FleetCommand/Weapon")]
public class WeaponData : ScriptableObject
{
    public string WeaponName;
    public WeaponID WeaponID;
    public WeaponSize Size;
    public int UpgradeLevel;
    public int MaxUpgradeLevel = 5;
    public int BaseUpgradeCost = 100;

    public float BaseDamage;
    public float BaseFireRate;
}