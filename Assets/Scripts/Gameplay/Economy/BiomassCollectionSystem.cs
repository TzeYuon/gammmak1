using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(PostMovementSystemGroup))]
public partial struct BiomassCollectionSystem : ISystem
{
    private NativeQueue<float> collectedBiomassQueue;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        collectedBiomassQueue = new NativeQueue<float>(Allocator.Persistent);
        state.RequireForUpdate<PlayerEconomy>();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (collectedBiomassQueue.IsCreated) collectedBiomassQueue.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Find ships
        var shipTransforms = SystemAPI.QueryBuilder()
            .WithAll<PlayerTag, LocalTransform, ShipCollectionRadiusComponent>()
            .Build();

        int length = shipTransforms.CalculateEntityCount();
        var collectors = new NativeArray<CollectorData>(length, Allocator.TempJob);
        int i = 0;
        foreach (var (transform, radius) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<ShipCollectionRadiusComponent>>()
            .WithAll<PlayerTag>())
        {
            collectors[i++] = new CollectorData
            {
                Position = transform.ValueRO.Position,
                RadiusSq = radius.ValueRO.RadiusSq,
            };
        }

        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var job = new ProcessBiomassJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            StationaryShips = collectors,
            CollectionQueue = collectedBiomassQueue.AsParallelWriter(),
            ECB = ecb
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
        collectors.Dispose(state.Dependency);
        state.Dependency.Complete();

        float totalCollectedThisFrame = 0f;
        while (collectedBiomassQueue.TryDequeue(out float value))
        {
            totalCollectedThisFrame += value;
        }

        if (totalCollectedThisFrame > 0)
        {
            var economy = SystemAPI.GetSingletonRW<PlayerEconomy>();
            economy.ValueRW.TotalBiomass += totalCollectedThisFrame;
        }
    }
}

[BurstCompile]
public partial struct ProcessBiomassJob : IJobEntity
{
    public float DeltaTime;

    [ReadOnly] public NativeArray<CollectorData> StationaryShips;
    public NativeQueue<float>.ParallelWriter CollectionQueue;
    public EntityCommandBuffer.ParallelWriter ECB;

    private void Execute(
        Entity entity, 
        [ChunkIndexInQuery] int chunkIndex, 
        ref BiomassLoot loot, 
        in LocalTransform transform
        )
    {
        // Check timer to despawn
        loot.TimeRemaining -= DeltaTime;
        if (loot.TimeRemaining <= 0)
        {
            ECB.DestroyEntity(chunkIndex, entity);
            return;
        }

        if (StationaryShips.Length == 0) return;

        float3 lootPos = transform.Position;
        bool isCollected = false;

        // Check distance to all stationary ships
        for (int i = 0; i < StationaryShips.Length; i++)
        {
            if (math.distancesq(lootPos, StationaryShips[i].Position) <= StationaryShips[i].RadiusSq)
            {
                isCollected = true;
                break;
            }
        }

        if (isCollected)
        {
            CollectionQueue.Enqueue(loot.Value);
            ECB.DestroyEntity(chunkIndex, entity);
        }
    }
}

public struct CollectorData
{
    public float3 Position;
    public float RadiusSq;
}