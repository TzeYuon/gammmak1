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
    public ShipRevealFogRadius ShipRevealFogRadius;
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
    public ShipIDComponent ShipIDComponent;
    public HealthComponent ShipHealthComponent;
    public ShipMovementStatsComponent ShipMovementStats;
    public ShipRevealFogRadius ShipRevealFogRadius;
    public ShipCollectionRadiusComponent ShipCollectionRadiusStat;

    // Shield
    public bool HasShield;
    public float2 ShieldOffset;
    public Entity ShieldPrefab;
    public ShieldComponent ShieldComponent;
}

public struct RosterWeaponBlueprint : IBufferElementData
{
    public Entity WeaponPrefab;
    public float2 Position;
    public quaternion OriginalRotation;

    // Weapon stats after upgrades
    public WeaponTargetingStats WeaponTargetingStats;
    public WeaponCombatStats WeaponCombatStats;
    public WeaponFiringArc WeaponFiringArc;

    // turret stats
    public bool HasTurret;
    public TurretComponent TurretComponent;
}