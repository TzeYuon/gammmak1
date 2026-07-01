using Unity.Entities;
using Unity.Mathematics;

public struct BakedShipData : IBufferElementData
{
    public ShipID ShipID;
    public Entity PrefabEntity;
}

public struct BakedWeaponData : IBufferElementData
{
    public WeaponID WeaponID;
    public Entity PrefabEntity;
}

public struct MothershipBlueprint : IComponentData
{
    // Ship stats after upgrades
    public HealthComponent ShipHealthComponent;
    public ShipCollectionRadiusComponent ShipCollectionRadiusStat;
}

public struct MothershipWeaponBlueprint : IBufferElementData
{
    public Entity WeaponPrefab;
    public float2 Position;

    // Weapon stats after upgrades
    public WeaponTargetingStats WeaponTargetingStats;
    public WeaponCombatStats WeaponCombatStats;
}

public struct RosterShipBlueprint : IComponentData
{
    public int SlotIndex;
    public Entity ShipPrefab;
    public Entity GhostEntity;
    public int BiomassCost; // Cost after upgrades

    // Ship stats after upgrades
    public HealthComponent ShipHealthComponent;
    public ShipMovementStatsComponent ShipMovementStats;
    public ShipCollectionRadiusComponent ShipCollectionRadiusStat;
}

public struct RosterWeaponBlueprint : IBufferElementData
{
    public Entity WeaponPrefab;
    public float2 Position;

    // Weapon stats after upgrades
    public WeaponTargetingStats WeaponTargetingStats;
    public WeaponCombatStats WeaponCombatStats;
}