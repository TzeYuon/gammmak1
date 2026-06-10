using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(CollisionAndDamageGroup))]
public partial struct BiomassCollectionSystem : ISystem
{
    private NativeQueue<float> collectedBiomassQueue;
    private EntityQuery shipQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        collectedBiomassQueue = new NativeQueue<float>(Allocator.Persistent);
        state.RequireForUpdate<PlayerEconomy>();

        shipQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerTag, LocalTransform, ShipCollectionRadiusComponent>()
            .Build();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (collectedBiomassQueue.IsCreated) collectedBiomassQueue.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        int shipCount = shipQuery.CalculateEntityCount();
        if (shipCount == 0) return;

        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        // Get an array of collectors that store their position and radius
        var collectors = new NativeArray<CollectorData>(shipCount, Allocator.TempJob);
        var gatherJob = new BuildCollectorsJob
        {
            Collectors = collectors
        };
        var gatherHandle = gatherJob.ScheduleParallel(state.Dependency);

        // Update biomass timer and check if collectable by collectors
        // Enqueue collected biomass to collectedBiomassQueue
        var processJob = new ProcessBiomassJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Collectors = collectors,
            CollectionQueue = collectedBiomassQueue.AsParallelWriter(),
            ECB = ecb
        };
        var processHandle = processJob.ScheduleParallel(gatherHandle);

        // Process all biomass collected in the queue
        var collectJob = new CollectBiomassJob
        {
            CollectionQueue = collectedBiomassQueue
        };
        var consumeHandle = collectJob.Schedule(processHandle);

        state.Dependency = consumeHandle;
        collectors.Dispose(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(PlayerTag))]
public partial struct BuildCollectorsJob : IJobEntity
{
    public NativeArray<CollectorData> Collectors;

    private void Execute(
        [EntityIndexInQuery] int index, 
        in LocalTransform transform, 
        in ShipCollectionRadiusComponent radius
        )
    {
        Collectors[index] = new CollectorData
        {
            Position = transform.Position,
            RadiusSq = radius.RadiusSq
        };
    }
}

[BurstCompile]
public partial struct ProcessBiomassJob : IJobEntity
{
    public float DeltaTime;

    [ReadOnly] public NativeArray<CollectorData> Collectors;
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

        if (Collectors.Length == 0) return;

        float3 lootPos = transform.Position;
        bool isCollected = false;

        // Check distance to all collectors
        for (int i = 0; i < Collectors.Length; i++)
        {
            if (math.distancesq(lootPos, Collectors[i].Position) <= Collectors[i].RadiusSq)
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

[BurstCompile]
public partial struct CollectBiomassJob : IJobEntity
{
    public NativeQueue<float> CollectionQueue;

    private void Execute(
        ref PlayerEconomy economy
        )
    {
        float totalCollectedThisFrame = 0f;

        while (CollectionQueue.TryDequeue(out float value))
        {
            totalCollectedThisFrame += value;
        }

        economy.TotalBiomass += totalCollectedThisFrame;
    }
}

public struct CollectorData
{
    public float3 Position;
    public float RadiusSq;
}