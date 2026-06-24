using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "LootDatabase", menuName = "FleetCommand/Loot Database")]
public class LootDatabase : ScriptableObject
{
    [SerializeField] List<WeaponData> allWeapons;
    [SerializeField] List<ShipBlueprintData> allShips;
    [HideInInspector] public List<WeaponData> DropableWeapons;
    [HideInInspector] public List<ShipBlueprintData> DropableShips;

    public void Init(PlayerSaveData save)
    {
        foreach (var weapon in allWeapons)
        {
            weapon.ResetToBase();
        }

        foreach (var ship in allShips)
        {
            ship.ResetToBase();
        }

        DropableWeapons = allWeapons.Where(x => !save.AvailableWeapons.Contains(x)).ToList();
        DropableShips = allShips.Where(x => !save.AvailableShips.Contains(x)).ToList();
    }
}