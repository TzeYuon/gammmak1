using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerSave", menuName = "FleetCommand/Player Save Data")]
public class PlayerSaveData : ScriptableObject
{
    [HideInInspector] public CurrentSectorData ActiveSector;

    [SerializeField] private int startingUpgradeCores;
    [HideInInspector] public int UpgradeCoresRemaining;

    [SerializeField] private List<ShipBlueprintData> startingAvailableShips;
    [HideInInspector] public List<ShipBlueprintData> AvailableShips;

    [SerializeField] private List<WeaponData> startingAvailableWeapons;
    [HideInInspector] public List<WeaponData> AvailableWeapons;

    [SerializeField] private ShipBlueprintData[] startingActiveRoster;
    [HideInInspector] public ShipBlueprintData[] ActiveRoster = new ShipBlueprintData[4];
    public ShipBlueprintData Mothership;

    public void OnStartNewRun(Starmap map)
    {
        EnterNewSector(map);
        UpgradeCoresRemaining = startingUpgradeCores;
        AvailableShips = startingAvailableShips;
        AvailableWeapons = startingAvailableWeapons;
        ActiveRoster = startingActiveRoster;
    }

    public void EnterNewSector(Starmap map)
    {
        ActiveSector = new(map);
    }

    public void MoveToNode(LevelNode node)
    {
        ActiveSector.CurrentNode = node;
    }

    public bool IsRosterWeaponless()
    {
        for (int i = 0; i < ActiveRoster.Length; i++)
        {
            if (ActiveRoster[i] != null && !ActiveRoster[i].IsWeaponless())
            {
                return false;
            }
        }

        return true;
    }

    public bool IsRosterEmpty()
    {
        for (int i = 0; i < ActiveRoster.Length; i++)
        {
            if (ActiveRoster[i] != null)
            {
                return false;
            }
        }

        return true;
    }

    public bool IsRosterFull()
    {
        for (int i = 0; i < ActiveRoster.Length; i++)
        {
            if (ActiveRoster[i] == null)
            {
                return false;
            }
        }

        return true;
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

public class CurrentSectorData
{
    public Starmap SectorMap; // level nodes present in the current sector
    public LevelNode CurrentNode;

    public CurrentSectorData(Starmap map)
    {
        SectorMap = map;
    }
}