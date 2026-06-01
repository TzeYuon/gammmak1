using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
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
        state.RequireForUpdate<WeaponState>();
    }

    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        CoarseEnemyHeatmapComponent coarseHeatmap = SystemAPI.GetSingletonRW<CoarseEnemyHeatmapComponent>().ValueRW;
        FineEnemyHeatmapComponent fineEntityMap = SystemAPI.GetSingletonRW<FineEnemyHeatmapComponent>().ValueRW;

        ComponentLookup<LocalToWorld> worldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);

        if (GridManager.Instance == null || !GridManager.Instance.nativeGrid.IsCreated) return;
        NativeArray<int> obstacleGrid = GridManager.Instance.nativeGrid;
        int2 obstacleWorldOrigin = new int2(GridManager.Instance.worldOrigin.x, GridManager.Instance.worldOrigin.y);
        int obstacleGridWidth = GridManager.Instance.width;
        int obstacleGridHeight = GridManager.Instance.height;

        PrecisionTargetingJob precisionJob = new()
        {
            CoarseCellSize = coarseHeatmap.CellSize,
            CoarseGridWidth = coarseHeatmap.GridWidth,
            CoarseGridHeight = coarseHeatmap.GridHeight,
            CoarseHeatmap = coarseHeatmap.Map,
            FineCellSize = fineEntityMap.CellSize,
            FineGridWidth = fineEntityMap.GridWidth,
            FineGridHeight = fineEntityMap.GridHeight,
            FineEntityMap = fineEntityMap.Map,
            ObstacleGrid = obstacleGrid,
            ObstacleWorldOrigin = obstacleWorldOrigin,
            ObstacleGridWidth = obstacleGridWidth,
            ObstacleGridHeight = obstacleGridHeight,
        };

        HeatmapTargetingJob heatmapJob = new()
        {
            FineDensityMap = fineEntityMap.DensityMap,
            GridWidth = fineEntityMap.GridWidth,
            GridHeight = fineEntityMap.GridHeight,
            CellSize = fineEntityMap.CellSize,
            WorldLookup = worldLookup,
            ObstacleGrid = obstacleGrid,
            ObstacleWorldOrigin = obstacleWorldOrigin,
            ObstacleGridWidth = obstacleGridWidth,
            ObstacleGridHeight = obstacleGridHeight,
        };

        state.Dependency = JobHandle.CombineDependencies(
            precisionJob.ScheduleParallel(state.Dependency),
            heatmapJob.ScheduleParallel(state.Dependency)
        );
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
[WithAll(typeof(PrecisionTargetingComponent))]
public partial struct PrecisionTargetingJob : IJobEntity
{
    public float CoarseCellSize;
    public int CoarseGridWidth;
    public int CoarseGridHeight;
    [ReadOnly] public NativeArray<int> CoarseHeatmap;

    public float FineCellSize;
    public int FineGridWidth;
    public int FineGridHeight;
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> FineEntityMap;

    [ReadOnly] public NativeArray<int> ObstacleGrid;
    public int2 ObstacleWorldOrigin;
    public int ObstacleGridWidth;
    public int ObstacleGridHeight;

    private void Execute(
        in LocalToWorld transform,
        in WeaponTargetingStats stats,
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

                        if (!GridUtility.HasLineOfSight(
                            weaponPos,
                            p,
                            ObstacleGrid,
                            ObstacleWorldOrigin,
                            ObstacleGridWidth,
                            ObstacleGridHeight))
                        {
                            continue; // no line of sight
                        }

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

[BurstCompile]
[WithAll(typeof(HeatmapTargetingComponent))]
public partial struct HeatmapTargetingJob : IJobEntity
{
    [ReadOnly] public NativeArray<int> FineDensityMap;
    public int GridWidth;
    public int GridHeight;
    public float CellSize;

    [ReadOnly] public ComponentLookup<LocalToWorld> WorldLookup;

    [ReadOnly] public NativeArray<int> ObstacleGrid;
    public int2 ObstacleWorldOrigin;
    public int ObstacleGridWidth;
    public int ObstacleGridHeight;

    private void Execute(
        ref WeaponState state,
        in WeaponTargetingStats stats,
        in WeaponFiringArc arc,
        in LocalToWorld weaponWorld,
        in ShipReference ship
        ) 
    {
        if (ship.Value == Entity.Null) return; // ship ref not set up yet
        LocalToWorld shipWorld = WorldLookup[ship.Value];
        float3 shipForwardWorld = math.rotate(shipWorld.Rotation, arc.BaseDirection);
        float3 currentBarrelForward = weaponWorld.Up;

        float bestScore = -1f;
        float2 bestTarget = float2.zero;

        float2 weaponPos = weaponWorld.Position.xy;
        int radiusCells = (int)math.ceil(stats.Range / CellSize);
        int2 centerCell = (int2)math.floor(weaponPos / CellSize);

        for (int x = -radiusCells; x <= radiusCells; x++)
        {
            for (int y = -radiusCells; y <= radiusCells; y++)
            {
                int2 checkCell = centerCell + new int2(x, y);
                if (checkCell.x < 0 || checkCell.x >= GridWidth || checkCell.y < 0 || checkCell.y >= GridHeight) continue;

                int density = FineDensityMap[checkCell.x + (checkCell.y * GridWidth)];
                if (density <= 0) continue;

                float2 cellWorldPos = new(checkCell.x * CellSize + CellSize * 0.5f, checkCell.y * CellSize + CellSize * 0.5f);
                float2 vectorToCell = cellWorldPos - weaponWorld.Position.xy;
                float distSq = math.lengthsq(vectorToCell);

                if (distSq > stats.Range * stats.Range) continue;

                if (!GridUtility.HasLineOfSight(
                    weaponPos, 
                    cellWorldPos, 
                    ObstacleGrid,
                    ObstacleWorldOrigin,
                    ObstacleGridWidth,
                    ObstacleGridHeight))
                {
                    continue; // no line of sight
                }

                float2 dirToCell = math.normalize(vectorToCell);

                // check if the cell is in the weapon firing arc
                float angleToShipResting = math.degrees(math.acos(math.dot(shipForwardWorld.xy, dirToCell)));
                if (angleToShipResting > arc.MaxAngle) continue;

                // add bias based on how much the turret needs to rotate
                float alignment = math.dot(currentBarrelForward.xy, dirToCell);
                float angleWeight = math.pow(math.max(0.1f, (alignment + 1f) * 0.5f), stats.AngleBias);

                // add bias to prioritise targets by distance
                float normalizedDistSq = math.sqrt(distSq) / stats.RangeSq; 
                float distanceWeight = 1.0f;
                if (stats.DistanceBias > 0f) distanceWeight = math.pow(1.0f - normalizedDistSq, stats.DistanceBias);
                else if (stats.DistanceBias < 0f) distanceWeight = math.pow(normalizedDistSq, math.abs(stats.DistanceBias));

                float score;
                if (stats.DensityBias == 0) score = angleWeight * distanceWeight;
                else score = density * stats.DensityBias * angleWeight * distanceWeight;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = cellWorldPos;
                }
            }
        }

        if (bestScore > 0)
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