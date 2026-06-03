using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerSave", menuName = "FleetCommand/Player Save Data")]
public class PlayerSaveData : ScriptableObject
{
    public int TotalScrap;

    public List<ShipBlueprintData> AvailableShips;
    public List<WeaponData> AvailableWeapons;

    public ShipBlueprintData[] ActiveRoster = new ShipBlueprintData[4];
}