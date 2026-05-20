using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

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

        var updateEnemyHeatmapJob = new UpdateEnemyHeatmapJob
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
        };

        state.Dependency = updateEnemyHeatmapJob.ScheduleParallel(clearHandles);
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
public partial struct UpdateEnemyHeatmapJob : IJobEntity
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