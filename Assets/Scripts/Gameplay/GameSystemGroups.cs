using Unity.Entities;
using Unity.Transforms;

// InitializationSystemGroup
/*
    InputManagerSystem
    GameStateSystem
    LevelPurgeSystem
    LevelGenerationManagerSystem
    LevelGen_TerrainSystem
    LevelGen_HivesSystem
    LevelGen_PlayerFleetSystem
    HeatmapInitSystem
    VFXSystem
*/

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class GameplayMasterGroup : ComponentSystemGroup { }

// Subgroups
[UpdateInGroup(typeof(GameplayMasterGroup))]
public partial class EnvironmentUpdateGroup : ComponentSystemGroup { }
/*
    HeatmapUpdateSystem
    FlowFieldSystem 
    ShipReferencesInitSystem
*/

[UpdateInGroup(typeof(GameplayMasterGroup))]
[UpdateAfter(typeof(EnvironmentUpdateGroup))]
public partial class CommandAndSpawnGroup : ComponentSystemGroup { }
/*
    FleetCommandSystem
    ShipSpawnerSystem
    HiveSystem
*/

[UpdateInGroup(typeof(GameplayMasterGroup))]
[UpdateAfter(typeof(CommandAndSpawnGroup))]
public partial class CombatTargetingGroup : ComponentSystemGroup { }
/*
    WeaponTargetingSystem
    TurretAimingSystem
*/

[UpdateInGroup(typeof(GameplayMasterGroup))]
[UpdateAfter(typeof(CombatTargetingGroup))]
public partial class ActionAndMovementGroup : ComponentSystemGroup { }
/*
    WeaponFiringSystem
    ShipMovementSystem
    EnemySwarmMovementSystem
    ProjectileSystem
    PoisonDamageSystem
*/

[UpdateInGroup(typeof(GameplayMasterGroup))]
[UpdateAfter(typeof(ActionAndMovementGroup))]
public partial class CollisionAndDamageGroup : ComponentSystemGroup { }
/*
    ProcessAOESystem
    ShieldSystem
    PlayerShipCollisionSystem
    BiomassCollectionSystem
*/

[UpdateInGroup(typeof(GameplayMasterGroup))]
[UpdateAfter(typeof(CollisionAndDamageGroup))]
public partial class LateGameplayGroup : ComponentSystemGroup { }
/*
    GamePauseSystem
    ProcessDamageSystem
    LevelWinLossSystem
    EconomyEventSystem
    SwarmDeathEventSystem
    PlayerDeathEventSystem
    MothershipDeathEventSystem
    EmitTrailSystem
*/

[UpdateInGroup(typeof(GameplayMasterGroup))]
[UpdateAfter(typeof(LateGameplayGroup))]
public partial class PurgeGroup : ComponentSystemGroup { }
/*
    PurgeDeathSystem
*/