using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unique ID for each ship blueprint.
/// </summary>
public enum ShipID : byte
{
    None = 0,
    Frigate,
    Destroyer,
    Cruiser
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
    Shields
}

[System.Serializable]
public class WeaponMount
{
    public Vector2 Position;
    public WeaponData EquippedWeapon;
}

[System.Serializable]
public class ShipSystem
{
    public ShipSystemID SystemID;
    public int UpgradeLevel;
    public int MaxUpgradeLevel = 5;
    public int BaseUpgradeCost = 100;
}

[CreateAssetMenu(fileName = "NewShipBlueprint", menuName = "FleetCommand/Ship Blueprint")]
public class ShipBlueprintData : ScriptableObject
{
    public string ShipName;
    public ShipClass ShipClass;
    public ShipID ShipID;
    public GameObject ShipPrefab;
    public int MaxSpawnsPerLevel = 10;
    public int CostToSpawn = 50;

    public List<ShipSystem> ShipSystems = new();
    public List<WeaponMount> Mounts = new();
}