using Map;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerSave", menuName = "FleetCommand/Player Save Data")]
public class PlayerSaveData : ScriptableObject
{
    public CurrentRunData ActiveRun;
    public int UpgradeCoresRemaining;

    public List<ShipBlueprintData> AvailableShips;
    public List<WeaponData> AvailableWeapons;

    public ShipBlueprintData[] ActiveRoster = new ShipBlueprintData[4];

    public void OnStartNewRun()
    {
        ActiveRun?.Clear();
        ActiveRun ??= new();
        UpgradeCoresRemaining = 0;
    }

    public bool IsShipInRoster(ShipBlueprintData data)
    {
        for (int i = 0; i < ActiveRoster.Length; i++)
        {
            if (ActiveRoster[i] == data) return true;
        }
        return false;
    }

    public void RemoveFromRoster(ShipBlueprintData data)
    {
        for (int i = 0; i < ActiveRoster.Length; i++)
        {
            if (ActiveRoster[i] == data)
            {
                ActiveRoster[i] = null;
            }
        }
    }

    public void AddToRoster(ShipBlueprintData data)
    {
        for (int i = 0; i < ActiveRoster.Length; i++)
        {
            if (ActiveRoster[i] == null)
            {
                ActiveRoster[i] = data;
                return;
            }
        }
    }
}

public class CurrentRunData
{
    public int CurrentNodeIndex = 0; // Current node player is at

    public List<LevelNode> SectorMap = new(); // level nodes present in the current sector

    public void Clear()
    {
        CurrentNodeIndex = 0;
        SectorMap.Clear();
    }
}