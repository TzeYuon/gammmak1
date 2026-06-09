using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(PostUpdateSystemGroup))]
public partial class ShipSpawnerSystem : SystemBase
{
    private NativeQueue<SpawnRequest> spawnQueue;

    protected override void OnCreate()
    {
        spawnQueue = new NativeQueue<SpawnRequest>(Allocator.Persistent);
        EntityManager.CreateSingleton(new SpawnQueueSingleton { Queue = spawnQueue });

        RequireForUpdate<PlayerEconomy>();
        RequireForUpdate<RosterShipBlueprint>();
    }

    protected override void OnDestroy()
    {
        if (spawnQueue.IsCreated) spawnQueue.Dispose();
    }

    protected override void OnUpdate()
    {
        if (spawnQueue.IsEmpty()) return;
        if (!SystemAPI.TryGetSingletonRW<PlayerEconomy>(out var economy)) return;

        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(World.Unmanaged);

        var shipBlueprintQuery = SystemAPI.QueryBuilder().WithAll<RosterShipBlueprint, RosterWeaponBlueprint>().Build();
        var shipBlueprintEntities = shipBlueprintQuery.ToEntityArray(Allocator.Temp);
        var shipBlueprintLookup = SystemAPI.GetComponentLookup<RosterShipBlueprint>(true);
        var weaponBufferLookup = SystemAPI.GetBufferLookup<RosterWeaponBlueprint>(true);

        // map the slot allocated to the blueprint to the correct index in an array
        var mappedBlueprints = new NativeArray<Entity>(4, Allocator.Temp);
        for (int i = 0; i < shipBlueprintEntities.Length; i++)
        {
            int slot = shipBlueprintLookup[shipBlueprintEntities[i]].SlotIndex;
            if (slot >= 0 && slot < 4)
            {
                mappedBlueprints[slot] = shipBlueprintEntities[i];
            }
        }

        while (spawnQueue.TryDequeue(out SpawnRequest request))
        {
            var shipBlueprintEntity = mappedBlueprints[request.SlotIndex];

            // safety check for missing blueprint component
            if (!shipBlueprintLookup.HasComponent(shipBlueprintEntity)) continue;
            RosterShipBlueprint shipBlueprint = shipBlueprintLookup[shipBlueprintEntity];

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

                //ecb.AppendToBuffer(ship, new LinkedEntityGroup { Value = weapon });
            }
        }

        shipBlueprintEntities.Dispose();
        mappedBlueprints.Dispose();
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