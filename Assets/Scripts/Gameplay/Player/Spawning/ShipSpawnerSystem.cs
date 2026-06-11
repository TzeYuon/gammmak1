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
            ecb.SetComponent(newShip, LocalTransform.FromPosition(new float3(request.Position, 0)));

            // Set ship stats
            //ecb.SetComponent(newShip, new HealthComponent { Value = shipBlueprint.MaxHealth });
            //ecb.SetComponent(newShip, new ShipMovementStatsComponent { MaxSpeed = shipBlueprint.MaxSpeed });

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
                ecb.SetComponent(weapon, LocalTransform.FromPosition(new float3(weaponBlueprint.Position, 0)));

                // Set weapon stats
                //// ecb.SetComponent(weapon, new HitscanWeapon { Damage = wpnRecipe.Damage });

                ecb.AppendToBuffer(newShip, new LinkedEntityGroup { Value = weapon });
            }
        }
    }
}

public struct SpawnRequest
{
    public int SlotIndex;
    public float2 Position;
}

public struct SpawnQueueSingleton : IComponentData
{
    public NativeQueue<SpawnRequest> Queue;
}