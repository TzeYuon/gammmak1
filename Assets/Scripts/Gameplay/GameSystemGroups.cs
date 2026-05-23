using Unity.Entities;
using Unity.Transforms;

/*
    System Groups update order:

    1. Spatial Grid Phase: Build spatial grid and update grid values
    2. Player Input Phase: Process player inputs
    3. PreCombat Phase: Enemy AI decisions, weapon targeting
    4. Combat Phase: Ships aim, fire hitscans
    5. Movement Phase: Projectiles fly, Swarm moves, Ships move
    6. Post Movement Phase: Apply collision damage, Collect biomass
    7. Process Damage Phase: Apply damage, add dead tag
    8. Event Phase: Dead entities trigger events
    9. Purge Phase: Dead entities are wiped from memory
*/

// Spatial Grid Phase: Build spatial grid and update grid values
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SpatialGridSystemGroup : ComponentSystemGroup { }

// Player Input Phase: Process player inputs
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SpatialGridSystemGroup))]
public partial class PlayerInputSystemGroup : ComponentSystemGroup { }

// PreCombat Phase: Enemy AI decisions, weapon targeting
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PlayerInputSystemGroup))]
public partial class PreCombatSystemGroup : ComponentSystemGroup { }

// Combat Phase: Ships aim, fire hitscans
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PreCombatSystemGroup))]
public partial class CombatSystemGroup : ComponentSystemGroup { }

// Movement Phase: Projectiles fly, swarm moves, ships move
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CombatSystemGroup))] 
[UpdateBefore(typeof(TransformSystemGroup))] 
public partial class MovementSystemGroup : ComponentSystemGroup { }

// Post Movement Phase: Apply collision damage
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MovementSystemGroup))]
public partial class PostMovementSystemGroup : ComponentSystemGroup { }

// Process Damage Phase: Apply damage, add dead tag
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PostMovementSystemGroup))]
public partial class ProcessDamageSystemGroup : ComponentSystemGroup { }

// Event Phase: Dead entities trigger events, update swarm entity rank
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ProcessDamageSystemGroup))]
public partial class PostUpdateSystemGroup : ComponentSystemGroup { }

// Purge Phase: Dead entities are wiped from memory
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PostUpdateSystemGroup))]
public partial class PurgeSystemGroup : ComponentSystemGroup { }