using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(CommandAndSpawnGroup))]
public partial struct ShipSpawnerSystem : ISystem
{
    private NativeArray<Entity> mappedBlueprints;
    private GameState m_PreviousState;

    private NativeQueue<SpawnRequest> spawnQueue;

    private EntityQuery economyQuery;

    public void OnCreate(ref SystemState state)
    {
        mappedBlueprints = new NativeArray<Entity>(4, Allocator.Persistent);

        spawnQueue = new NativeQueue<SpawnRequest>(Allocator.Persistent);
        state.EntityManager.CreateSingleton(new SpawnQueueSingleton { Queue = spawnQueue });

        economyQuery = SystemAPI.QueryBuilder().WithAll<PlayerEconomy>().Build();

        state.RequireForUpdate<PlayerEconomy>();
        state.RequireForUpdate<RosterShipBlueprint>();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (mappedBlueprints.IsCreated) mappedBlueprints.Dispose();
        if (spawnQueue.IsCreated) spawnQueue.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var gameState = SystemAPI.GetSingleton<GameStateSingleton>().CurrentState;
        // Rebuild roster mapping
        if (gameState == GameState.Gameplay && m_PreviousState != GameState.Gameplay)
        {
            var shipBlueprintQuery = SystemAPI.QueryBuilder().WithAll<RosterShipBlueprint, RosterWeaponBlueprint>().Build();
            var shipBlueprintEntities = shipBlueprintQuery.ToEntityArray(Allocator.Temp);
            var shipBlueprintLookup = SystemAPI.GetComponentLookup<RosterShipBlueprint>(true);

            // map the slot allocated to the blueprint to the correct index in an array
            for (int i = 0; i < shipBlueprintEntities.Length; i++)
            {
                int slot = shipBlueprintLookup[shipBlueprintEntities[i]].SlotIndex;
                if (slot >= 0 && slot < 4)
                {
                    mappedBlueprints[slot] = shipBlueprintEntities[i];
                }
            }

            shipBlueprintEntities.Dispose();
        }

        m_PreviousState = gameState;
        // only spawn when in gameplay state
        if (gameState != GameState.Gameplay) return;

        // return if no requests
        if (spawnQueue.IsEmpty()) return;

        // Complete dependency for PlayerEconomy
        economyQuery.CompleteDependency();
        if (!SystemAPI.TryGetSingletonRW<PlayerEconomy>(out var economy)) return;

        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);

        if (!SystemAPI.TryGetSingletonEntity<MothershipTag>(out var mothershipEntity)) return;
        var mothershipPosition = SystemAPI.GetComponentRO<LocalTransform>(mothershipEntity).ValueRO.Position;
        
        var shipBlueprintLookupReadOnly = SystemAPI.GetComponentLookup<RosterShipBlueprint>(true);
        var weaponBufferLookup = SystemAPI.GetBufferLookup<RosterWeaponBlueprint>(true);

        // process all requests in current frame
        while (spawnQueue.TryDequeue(out SpawnRequest request))
        {
            var shipBlueprintEntity = mappedBlueprints[request.SlotIndex];

            // safety check for missing blueprint component
            if (!shipBlueprintLookupReadOnly.HasComponent(shipBlueprintEntity)) continue;
            RosterShipBlueprint shipBlueprint = shipBlueprintLookupReadOnly[shipBlueprintEntity];
            
            // safety check for no prefab
            Entity prefabToSpawn = shipBlueprint.ShipPrefab;
            if (prefabToSpawn == Entity.Null) continue;
            
            // check and subtract cost
            int cost = shipBlueprint.BiomassCost;
            if (economy.ValueRO.TotalBiomass < cost) continue;
            else economy.ValueRW.TotalBiomass -= cost;
            
            // Spawn ship
            Entity newShip = ecb.Instantiate(prefabToSpawn);
            ecb.SetComponent(newShip, LocalTransform.FromPosition(mothershipPosition));

            // Set ship stats
            ecb.SetComponent(newShip, shipBlueprint.ShipHealthComponent);
            ecb.SetComponent(newShip, shipBlueprint.ShipMovementStats);
            ecb.SetComponent(newShip, shipBlueprint.ShipRevealFogRadius);
            ecb.SetComponent(newShip, shipBlueprint.ShipCollectionRadiusStat);

            // set shield if present
            if (shipBlueprint.HasShield) 
            {
                var shieldEntity = ecb.Instantiate(shipBlueprint.ShieldPrefab);

                // parent to ship
                ecb.AddComponent(shieldEntity, new Parent { Value = newShip });

                // add shield components
                ecb.AddComponent(shieldEntity, shipBlueprint.ShieldComponent); 
                ecb.AddComponent(shieldEntity, new ShieldOpacityOverride{ Value = 1 });

                ecb.AppendToBuffer(newShip, new LinkedEntityGroup { Value = shieldEntity });
            }

            // spawn weapons
            if (!weaponBufferLookup.HasBuffer(shipBlueprintEntity)) continue;
            var weaponBuffer = weaponBufferLookup[shipBlueprintEntity];

            for (int w = 0; w < weaponBuffer.Length; w++)
            {
                var weaponBlueprint = weaponBuffer[w];

                Entity weaponPrefab = weaponBlueprint.WeaponPrefab;
                if (weaponPrefab == Entity.Null) continue;

                Entity weapon = ecb.Instantiate(weaponPrefab);
                ecb.AddComponent(weapon, new Parent { Value = newShip });
                ecb.SetComponent(weapon, LocalTransform.FromPositionRotation(new float3(weaponBlueprint.Position, 0), weaponBlueprint.OriginalRotation));

                // Set weapon stats
                ecb.SetComponent(weapon, weaponBlueprint.WeaponTargetingStats);
                ecb.SetComponent(weapon, weaponBlueprint.WeaponCombatStats);
                ecb.SetComponent(weapon, weaponBlueprint.WeaponFiringArc);

                ecb.AppendToBuffer(newShip, new LinkedEntityGroup { Value = weapon });
            }

            // Spawn ghosts for movement visuals
            var ghost1 = ecb.Instantiate(shipBlueprint.GhostEntity);
            ecb.AddComponent(ghost1, new ShipReference { Value = newShip });
            ecb.AddComponent(ghost1, new LevelEntityTag());
            ecb.AddComponent(ghost1, new MovementTargetVisualComponent());

            var ghost2 = ecb.Instantiate(shipBlueprint.GhostEntity);
            ecb.AddComponent(ghost2, new ShipReference { Value = newShip });
            ecb.AddComponent(ghost1, new LevelEntityTag());
            ecb.AddComponent(ghost2, new MovementTargetProjectionVisualComponent());
        }
    }
}

public struct SpawnRequest
{
    public int SlotIndex;
}

public struct SpawnQueueSingleton : IComponentData
{
    public NativeQueue<SpawnRequest> Queue;
}