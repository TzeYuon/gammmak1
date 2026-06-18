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

public struct RosterShipBlueprint : IComponentData
{
    public int SlotIndex;
    public Entity ShipPrefab;
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