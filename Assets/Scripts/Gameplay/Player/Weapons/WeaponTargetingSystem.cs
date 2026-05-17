using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(PreCombatSystemGroup))]
partial struct WeaponTargetingSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CoarseEnemyHeatmapComponent>();
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
        state.RequireForUpdate<WeaponStats>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        CoarseEnemyHeatmapComponent coarseHeatmap = SystemAPI.GetSingletonRW<CoarseEnemyHeatmapComponent>().ValueRW;
        FineEnemyHeatmapComponent fineEntityMap = SystemAPI.GetSingletonRW<FineEnemyHeatmapComponent>().ValueRW;

        ClosestTargetingJob job = new()
        {
            CoarseCellSize = coarseHeatmap.CellSize,
            CoarseGridWidth = coarseHeatmap.GridWidth,
            CoarseGridHeight = coarseHeatmap.GridHeight,
            CoarseHeatmap = coarseHeatmap.Map,
            FineCellSize = fineEntityMap.CellSize,
            FineGridWidth = fineEntityMap.GridWidth,
            FineGridHeight = fineEntityMap.GridHeight,
            FineEntityMap = fineEntityMap.Map,
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
public partial struct ClosestTargetingJob : IJobEntity
{
    public float CoarseCellSize;
    public int CoarseGridWidth;
    public int CoarseGridHeight;
    [ReadOnly] public NativeArray<int> CoarseHeatmap;

    public float FineCellSize;
    public int FineGridWidth;
    public int FineGridHeight;
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> FineEntityMap;

    private void Execute(
        in LocalToWorld transform,
        in WeaponStats stats,
        ref WeaponState state
        )
    {
        if (!CoarseHeatmap.IsCreated || !FineEntityMap.IsCreated) return;

        float2 weaponPos = transform.Position.xy;
        float rangeSq = stats.Range * stats.Range;

        // Round to cell position
        int2 cell = GridUtility.WorldPosToCell(transform.Position, CoarseCellSize);
        int radiusCells = (int)math.ceil(stats.Range / CoarseCellSize);

        bool foundTarget = false;
        float2 bestTarget = float2.zero;
        float closestDistSq = float.MaxValue;

        // Loop through the square of cells around the weapon
        for (int x = -radiusCells; x <= radiusCells; x++)
        {
            for (int y = -radiusCells; y <= radiusCells; y++)
            {
                int2 curCell = cell + new int2(x, y);

                // Bounds check
                if (!GridUtility.IsWithinBounds(curCell, CoarseGridWidth, CoarseGridHeight)) continue;

                // Check if there are enemies in this cell
                int density = CoarseHeatmap[GridUtility.CellToIndex(curCell, CoarseGridWidth)];
                if (density <= 0) continue;

                float2 cellCorner = new float2(curCell.x, curCell.y) * CoarseCellSize;

                // Check fine map for specific target
                for (float i = cellCorner.x; i < cellCorner.x + CoarseCellSize; i += FineCellSize)
                {
                    for (float j = cellCorner.y; j < cellCorner.y + CoarseCellSize; j += FineCellSize)
                    {
                        float2 p = new(i, j);
                        int key = GridUtility.WorldPosToIndex(p, FineCellSize, FineGridWidth);
                        if (!FineEntityMap.ContainsKey(key)) continue; // no enemies

                        float distSq = math.distancesq(weaponPos, p);
                        if (distSq > rangeSq) continue; // range check

                        // Check best target
                        if (distSq < closestDistSq)
                        {
                            closestDistSq = distSq;
                            bestTarget = p;
                            foundTarget = true;
                        }
                    }
                }
            }
        }

        // Set the Target
        if (foundTarget)
        {
            state.HasTarget = true;
            state.TargetPos = bestTarget;
        } 
        else
        {
            state.HasTarget = false;
        }
    }
}