using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(EnvironmentUpdateGroup))]
partial struct HeatmapUpdateSystem : ISystem
{
    EntityQuery coarseHeatmapQuery;
    EntityQuery fineHeatmapQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FineEnemyHeatmapComponent>();

        fineHeatmapQuery = SystemAPI.QueryBuilder().WithAll<FineEnemyHeatmapComponent>().Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        fineHeatmapQuery.CompleteDependency();
        RefRW<FineEnemyHeatmapComponent> fineEnemyHeatmap = SystemAPI.GetSingletonRW<FineEnemyHeatmapComponent>();

        int livingEnemiesCount = SystemAPI.QueryBuilder().WithAll<SwarmTag>().Build().CalculateEntityCount();
        if (fineEnemyHeatmap.ValueRO.Map.Capacity <= livingEnemiesCount)
        {
            int newCapacity = (int)(livingEnemiesCount * 1.25f);
            fineEnemyHeatmap.ValueRO.Map.Dispose();
            fineEnemyHeatmap.ValueRW.Map = new NativeParallelMultiHashMap<int, Entity>(newCapacity, Allocator.Persistent);
        }

        fineEnemyHeatmap.ValueRO.Map.Clear();
        ClearArray(fineEnemyHeatmap.ValueRW.DensityMap);

        var swarmHeatmapHandle = new SwarmHeatmapJob
        {
            WorldOrigin = fineEnemyHeatmap.ValueRO.WorldOrigin,

            FineMap = fineEnemyHeatmap.ValueRO.Map.AsParallelWriter(),
            FineDensityMap = fineEnemyHeatmap.ValueRO.DensityMap,
            FineCellSize = fineEnemyHeatmap.ValueRO.CellSize,
            FineGridWidth = fineEnemyHeatmap.ValueRO.GridWidth,
            FineGridHeight = fineEnemyHeatmap.ValueRO.GridHeight,
        }.ScheduleParallel(state.Dependency);

        var hiveHeatmapHandle = new HiveHeatmapJob
        {
            WorldOrigin = fineEnemyHeatmap.ValueRO.WorldOrigin,

            FineMap = fineEnemyHeatmap.ValueRO.Map.AsParallelWriter(),
            FineDensityMap = fineEnemyHeatmap.ValueRO.DensityMap,
            FineCellSize = fineEnemyHeatmap.ValueRO.CellSize,
            FineGridWidth = fineEnemyHeatmap.ValueRO.GridWidth,
            FineGridHeight = fineEnemyHeatmap.ValueRO.GridHeight,
        }.ScheduleParallel(swarmHeatmapHandle);

        state.Dependency = hiveHeatmapHandle;
    }

    public void ClearArray(NativeArray<int> myArray)
    {
        unsafe
        {
            void* pointer = NativeArrayUnsafeUtility.GetUnsafePtr(myArray);
            long sizeInBytes = myArray.Length * sizeof(int);
            UnsafeUtility.MemClear(pointer, sizeInBytes);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
[WithAll(typeof(SwarmTag))]
[WithDisabled(typeof(DeadTag))]
public partial struct SwarmHeatmapJob : IJobEntity
{
    public int2 WorldOrigin;

    public NativeParallelMultiHashMap<int, Entity>.ParallelWriter FineMap;
    [NativeDisableParallelForRestriction] public NativeArray<int> FineDensityMap;
    public float FineCellSize;
    public int FineGridWidth;
    public int FineGridHeight;

    void Execute(
        Entity entity,
        in LocalTransform transform
        )
    {
        float2 pos = transform.Position.xy;

        int2 fineCell = GridUtility.WorldPosToCell(pos, WorldOrigin, FineCellSize);
        if (GridUtility.IsWithinBounds(fineCell, FineGridWidth, FineGridHeight))
        {
            int fineCellIndex = GridUtility.CellToIndex(fineCell, FineGridWidth);
            FineMap.Add(fineCellIndex, entity);
            FineDensityMap.InterlockedAdd(fineCellIndex, 1);
        }
    }
}

[BurstCompile]
[WithDisabled(typeof(DeadTag))]
public partial struct HiveHeatmapJob : IJobEntity
{
    public int2 WorldOrigin;

    public NativeParallelMultiHashMap<int, Entity>.ParallelWriter FineMap;
    [NativeDisableParallelForRestriction] public NativeArray<int> FineDensityMap;
    public float FineCellSize;
    public int FineGridWidth;
    public int FineGridHeight;

    void Execute(
        Entity entity,
        in HiveComponent hive,
        in LocalTransform transform
        )
    {
        float radiusSq = hive.Size * hive.Size;

        // Update fine heatmap
        int fineRadiusCells = (int)math.ceil(hive.Size / FineCellSize);
        int2 fineCenterCell = GridUtility.WorldPosToCell(transform.Position.xy, WorldOrigin, FineCellSize);
        for (int x = -fineRadiusCells; x <= fineRadiusCells; x++)
        {
            for (int y = -fineRadiusCells; y <= fineRadiusCells; y++)
            {
                int2 checkCell = fineCenterCell + new int2(x, y);
                float2 cellWorldPos = GridUtility.CellToWorldPosCenter(checkCell, WorldOrigin, FineCellSize);

                if (math.distancesq(transform.Position.xy, cellWorldPos) <= radiusSq)
                {
                    if (GridUtility.IsWithinBounds(checkCell, FineGridWidth, FineGridHeight))
                    {
                        int fineCellIndex = GridUtility.CellToIndex(checkCell, FineGridWidth);
                        FineMap.Add(fineCellIndex, entity);
                        FineDensityMap.InterlockedAdd(fineCellIndex, 1);
                    }
                }
            }
        }
    }
}