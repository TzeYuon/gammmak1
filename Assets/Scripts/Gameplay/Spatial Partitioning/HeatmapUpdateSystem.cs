using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SpatialGridSystemGroup))]
partial struct HeatmapUpdateSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CoarseEnemyHeatmapComponent>();
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        CoarseEnemyHeatmapComponent coarseEnemyHeatmap = SystemAPI.GetSingleton<CoarseEnemyHeatmapComponent>();
        FineEnemyHeatmapComponent fineEnemyHeatmap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        
        state.Dependency.Complete();
        fineEnemyHeatmap.Map.Clear();

        var clearCoarseEnemyJob = new ClearCoarseEnemyJob
        {
            Map = coarseEnemyHeatmap.Map,
        };
        JobHandle clearCoarseHandle = clearCoarseEnemyJob.Schedule(coarseEnemyHeatmap.Map.Length, 4096, state.Dependency);

        var clearFineEnemyJob = new ClearFineEnemyJob
        {
            Map = fineEnemyHeatmap.DensityMap,
        };
        JobHandle clearFineHandle = clearFineEnemyJob.Schedule(fineEnemyHeatmap.DensityMap.Length, 4096, state.Dependency);
        JobHandle clearHandles = JobHandle.CombineDependencies(clearCoarseHandle, clearFineHandle);

        var swarmHeatmapHandle = new SwarmHeatmapJob
        {
            CoarseMap = coarseEnemyHeatmap.Map,
            CoarseCellSize = coarseEnemyHeatmap.CellSize,
            CoarseGridWidth = coarseEnemyHeatmap.GridWidth,
            CoarseGridHeight = coarseEnemyHeatmap.GridHeight,

            FineMap = fineEnemyHeatmap.Map.AsParallelWriter(),
            FineDensityMap = fineEnemyHeatmap.DensityMap,
            FineCellSize = fineEnemyHeatmap.CellSize,
            FineGridWidth = fineEnemyHeatmap.GridWidth,
            FineGridHeight = fineEnemyHeatmap.GridHeight,
        }.ScheduleParallel(clearHandles);

        var hiveHeatmapHandle = new HiveHeatmapJob
        {
            CoarseMap = coarseEnemyHeatmap.Map,
            CoarseCellSize = coarseEnemyHeatmap.CellSize,
            CoarseGridWidth = coarseEnemyHeatmap.GridWidth,
            CoarseGridHeight = coarseEnemyHeatmap.GridHeight,

            FineMap = fineEnemyHeatmap.Map.AsParallelWriter(),
            FineDensityMap = fineEnemyHeatmap.DensityMap,
            FineCellSize = fineEnemyHeatmap.CellSize,
            FineGridWidth = fineEnemyHeatmap.GridWidth,
            FineGridHeight = fineEnemyHeatmap.GridHeight,
        }.ScheduleParallel(swarmHeatmapHandle);

        state.Dependency = hiveHeatmapHandle;
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
public struct ClearCoarseEnemyJob : IJobParallelFor
{
    [WriteOnly] public NativeArray<int> Map;

    public void Execute(int index)
    {
        Map[index] = 0;
    }
}

[BurstCompile]
public struct ClearFineEnemyJob : IJobParallelFor
{
    [WriteOnly] public NativeArray<int> Map;

    public void Execute(int index)
    {
        Map[index] = 0;
    }
}

[BurstCompile]
[WithAll(typeof(SwarmTag))]
[WithDisabled(typeof(DeadTag))]
public partial struct SwarmHeatmapJob : IJobEntity
{
    [NativeDisableParallelForRestriction] public NativeArray<int> CoarseMap;
    public float CoarseCellSize;
    public int CoarseGridWidth;
    public int CoarseGridHeight;

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
        int2 coarseCell = GridUtility.WorldPosToCell(pos, CoarseCellSize);
        if (GridUtility.IsWithinBounds(coarseCell, CoarseGridWidth, CoarseGridHeight)) 
        {
            int coarseIndex = GridUtility.CellToIndex(coarseCell, CoarseGridWidth);
            CoarseMap.InterlockedAdd(coarseIndex, 1);
        }

        int2 fineCell = GridUtility.WorldPosToCell(pos, FineCellSize);
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
    [NativeDisableParallelForRestriction] public NativeArray<int> CoarseMap;
    public float CoarseCellSize;
    public int CoarseGridWidth;
    public int CoarseGridHeight;

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
        // Update coarse heatmap
        int coarseRadiusCells = (int)math.ceil(hive.Size / CoarseCellSize);
        int2 coarseCenterCell = GridUtility.WorldPosToCell(transform.Position.xy, CoarseCellSize);
        float radiusSq = hive.Size * hive.Size;
        for (int x = -coarseRadiusCells; x <= coarseRadiusCells; x++)
        {
            for (int y = -coarseRadiusCells; y <= coarseRadiusCells; y++)
            {
                int2 checkCell = coarseCenterCell + new int2(x, y);
                float2 cellWorldPos = GridUtility.CellToWorldPos(checkCell, CoarseCellSize);

                if (math.distancesq(transform.Position.xy, cellWorldPos) <= radiusSq)
                {
                    if (GridUtility.IsWithinBounds(checkCell, CoarseGridWidth, CoarseGridHeight))
                    {
                        int coarseIndex = GridUtility.CellToIndex(checkCell, CoarseGridWidth);
                        CoarseMap.InterlockedAdd(coarseIndex, 1);
                    }
                }
            }
        }

        // Update fine heatmap
        int fineRadiusCells = (int)math.ceil(hive.Size / FineCellSize);
        int2 fineCenterCell = GridUtility.WorldPosToCell(transform.Position.xy, FineCellSize);
        for (int x = -fineRadiusCells; x <= fineRadiusCells; x++)
        {
            for (int y = -fineRadiusCells; y <= fineRadiusCells; y++)
            {
                int2 checkCell = fineCenterCell + new int2(x, y);
                float2 cellWorldPos = GridUtility.CellToWorldPos(checkCell, FineCellSize);

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