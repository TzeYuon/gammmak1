using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(CombatTargetingGroup))]
partial struct WeaponTargetingSystem : ISystem
{
    private EntityQuery coarseHeatmapQuery;
    private EntityQuery fineEntityMapQuery;
    private EntityQuery terrainGridComponentQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CoarseEnemyHeatmapComponent>();
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
        state.RequireForUpdate<WeaponState>();
        state.RequireForUpdate<TerrainGridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        CoarseEnemyHeatmapComponent coarseHeatmap = SystemAPI.GetSingleton<CoarseEnemyHeatmapComponent>();
        FineEnemyHeatmapComponent fineEntityMap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        TerrainGridComponent terrainGridComponent = SystemAPI.GetSingleton<TerrainGridComponent>();

        ComponentLookup<LocalToWorld> worldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);

        NativeArray<int> obstacleGrid = terrainGridComponent.Grid;
        int2 obstacleWorldOrigin = terrainGridComponent.WorldOrigin;
        int obstacleGridWidth = terrainGridComponent.GridWidth;
        int obstacleGridHeight = terrainGridComponent.GridHeight;
        float obstacleGridCellSize = terrainGridComponent.CellSize;

        PrecisionTargetingJob precisionJob = new()
        {
            HeatmapWorldOrigin = coarseHeatmap.WorldOrigin,
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
            ObstacleGridCellSize = obstacleGridCellSize
        };

        HeatmapTargetingJob heatmapJob = new()
        {
            FineDensityMap = fineEntityMap.DensityMap,
            HeatmapWorldOrigin = fineEntityMap.WorldOrigin,
            GridWidth = fineEntityMap.GridWidth,
            GridHeight = fineEntityMap.GridHeight,
            CellSize = fineEntityMap.CellSize,
            WorldLookup = worldLookup,
            ObstacleGrid = obstacleGrid,
            ObstacleWorldOrigin = obstacleWorldOrigin,
            ObstacleGridWidth = obstacleGridWidth,
            ObstacleGridHeight = obstacleGridHeight,
            ObstacleGridCellSize = obstacleGridCellSize
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
    public int2 HeatmapWorldOrigin;
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
    public float ObstacleGridCellSize;

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
        int2 cell = GridUtility.WorldPosToCell(transform.Position, HeatmapWorldOrigin, CoarseCellSize);
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

                float2 cellCorner = GridUtility.CellToWorldPosCorner(curCell, HeatmapWorldOrigin, CoarseCellSize);

                // Check fine map for specific target
                for (float i = cellCorner.x; i < cellCorner.x + CoarseCellSize; i += FineCellSize)
                {
                    for (float j = cellCorner.y; j < cellCorner.y + CoarseCellSize; j += FineCellSize)
                    {
                        float2 p = new(i, j);
                        int key = GridUtility.WorldPosToIndex(p, HeatmapWorldOrigin, FineCellSize, FineGridWidth);
                        if (!FineEntityMap.ContainsKey(key)) continue; // no enemies

                        float distSq = math.distancesq(weaponPos, p);
                        if (distSq > rangeSq) continue; // range check

                        if (!stats.IgnoreObstacles && !GridUtility.HasLineOfSight(
                            weaponPos,
                            p,
                            ObstacleGrid,
                            ObstacleWorldOrigin,
                            ObstacleGridWidth,
                            ObstacleGridHeight,
                            ObstacleGridCellSize))
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
    public int2 HeatmapWorldOrigin;
    public int GridWidth;
    public int GridHeight;
    public float CellSize;

    [ReadOnly] public ComponentLookup<LocalToWorld> WorldLookup;

    [ReadOnly] public NativeArray<int> ObstacleGrid;
    public int2 ObstacleWorldOrigin;
    public int ObstacleGridWidth;
    public int ObstacleGridHeight;
    public float ObstacleGridCellSize;

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
        int2 centerCell = GridUtility.WorldPosToCell(weaponPos, HeatmapWorldOrigin, CellSize);

        for (int x = -radiusCells; x <= radiusCells; x++)
        {
            for (int y = -radiusCells; y <= radiusCells; y++)
            {
                int2 checkCell = centerCell + new int2(x, y);
                if (!GridUtility.IsWithinBounds(checkCell, GridWidth, GridHeight)) continue;

                int density = FineDensityMap[GridUtility.CellToIndex(checkCell, GridWidth)];
                if (density <= 0) continue;

                float2 cellWorldPos = GridUtility.CellToWorldPosCenter(checkCell, HeatmapWorldOrigin, CellSize);
                float2 vectorToCell = cellWorldPos - weaponWorld.Position.xy;
                float distSq = math.lengthsq(vectorToCell);

                if (distSq > stats.Range * stats.Range) continue;

                if (!stats.IgnoreObstacles && !GridUtility.HasLineOfSight(
                    weaponPos, 
                    cellWorldPos, 
                    ObstacleGrid,
                    ObstacleWorldOrigin,
                    ObstacleGridWidth,
                    ObstacleGridHeight,
                    ObstacleGridCellSize))
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