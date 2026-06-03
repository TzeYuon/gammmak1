using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(PostUpdateSystemGroup))]
public partial class ShipSpawnerSystem : SystemBase
{
    private NativeQueue<SpawnRequest> spawnQueue;

    protected override void OnCreate()
    {
        spawnQueue = new NativeQueue<SpawnRequest>(Allocator.Persistent);
        EntityManager.CreateSingleton(new SpawnQueueSingleton { Queue = spawnQueue });
    }

    protected override void OnDestroy()
    {
        if (spawnQueue.IsCreated) spawnQueue.Dispose();
    }

    protected override void OnUpdate()
    {
        if (spawnQueue.IsEmpty()) return;
        if (!SystemAPI.TryGetSingletonBuffer<ActiveRosterSlot>(out var roster)) return;
        if (!SystemAPI.TryGetSingletonRW<PlayerEconomy>(out var economy)) return;

        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(World.Unmanaged);

        while (spawnQueue.TryDequeue(out SpawnRequest request))
        {
            var ship = roster[request.SlotIndex];
            Entity prefabToSpawn = ship.PrefabEntity;
            float cost = ship.BiomassCost;

            if (economy.ValueRO.TotalBiomass < cost) return;
            else economy.ValueRW.TotalBiomass -= cost;

            if (prefabToSpawn != Entity.Null)
            {
                Entity newShip = ecb.Instantiate(prefabToSpawn);
                ecb.SetComponent(newShip, LocalTransform.FromPosition(new float3(request.Position, 0)));
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