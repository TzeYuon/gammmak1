using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(CombatTargetingGroup))]
partial struct WeaponTargetingSystem : ISystem
{
    private uint frameCount;

    private EntityQuery coarseHeatmapQuery;
    private EntityQuery fineEntityMapQuery;
    private EntityQuery terrainGridComponentQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
        state.RequireForUpdate<WeaponState>();
        state.RequireForUpdate<TerrainGridComponent>();
        state.RequireForUpdate<FogOfWarState>();

        frameCount = 0;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        FineEnemyHeatmapComponent fineEntityMap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        TerrainGridComponent terrainGridComponent = SystemAPI.GetSingleton<TerrainGridComponent>();
        FogOfWarState fogOfWarState = SystemAPI.GetSingleton<FogOfWarState>();

        ComponentLookup<LocalToWorld> worldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<DeadTag> deadLookup = SystemAPI.GetComponentLookup<DeadTag>(true);
        ComponentLookup<EnemyValueComponent> enemyValueLookup = SystemAPI.GetComponentLookup<EnemyValueComponent>(true);

        NativeArray<int> obstacleGrid = terrainGridComponent.Grid;
        int2 obstacleWorldOrigin = terrainGridComponent.WorldOrigin;
        int obstacleGridWidth = terrainGridComponent.GridWidth;
        int obstacleGridHeight = terrainGridComponent.GridHeight;
        float obstacleGridCellSize = terrainGridComponent.CellSize;

        int numBuckets = 10; // Spreads the load over 10 frames
        int currentBucket = (int)(frameCount++ % numBuckets);

        HeatmapTargetingJob heatmapJob = new()
        {
            NumBuckets = numBuckets,
            CurrentBucket = currentBucket,
            DeadLookup = deadLookup,

            FogArray = fogOfWarState.FogArray,

            Grid = fineEntityMap.Grid,
            HeatmapWorldOrigin = fineEntityMap.WorldOrigin,
            GridWidth = fineEntityMap.GridWidth,
            GridHeight = fineEntityMap.GridHeight,
            CellSize = fineEntityMap.CellSize,

            SpatialMap = fineEntityMap.Map,
            EnemyValueLookup = enemyValueLookup,

            WorldLookup = worldLookup,

            ObstacleGrid = obstacleGrid,
            ObstacleWorldOrigin = obstacleWorldOrigin,
            ObstacleGridWidth = obstacleGridWidth,
            ObstacleGridHeight = obstacleGridHeight,
            ObstacleGridCellSize = obstacleGridCellSize
        };

        state.Dependency = heatmapJob.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
public partial struct HeatmapTargetingJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<DeadTag> DeadLookup;
    public int NumBuckets;
    public int CurrentBucket;

    [ReadOnly] public NativeArray<Color32> FogArray;

    [ReadOnly] public NativeArray<HeatmapData> Grid;
    public int2 HeatmapWorldOrigin;
    public int GridWidth;
    public int GridHeight;
    public float CellSize;

    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> SpatialMap;
    [ReadOnly] public ComponentLookup<EnemyValueComponent> EnemyValueLookup;

    [ReadOnly] public ComponentLookup<LocalToWorld> WorldLookup;

    [ReadOnly] public NativeArray<int> ObstacleGrid;
    public int2 ObstacleWorldOrigin;
    public int ObstacleGridWidth;
    public int ObstacleGridHeight;
    public float ObstacleGridCellSize;

    private void Execute(
        Entity entity,
        ref WeaponState state,
        in WeaponTargetingStats stats,
        in WeaponFiringArc arc,
        in LocalToWorld weaponWorld,
        in ShipReference ship
        ) 
    {
        if (ship.Value == Entity.Null) return; // ship ref not set up yet

        float2 weaponPos = weaponWorld.Position.xy;
        float rangeSq = stats.RangeSq;

        // Validate current target if exists, deletes existing target if it is not valid anymore, does not recalculate instantly if not valid
        // Assumes still in range since targets are only stored for 10 frames
        if (state.HasTarget)
        {
            bool isStillValid = true;

            if (stats.RequireTargetEntity)
            {
                // check if target is still alive
                if (!DeadLookup.EntityExists(state.TargetEntity) || DeadLookup.IsComponentEnabled(state.TargetEntity))
                {
                    isStillValid = false;
                }
            }
            else
            {
                // check if there are still enemies in target pos
                int targetIndex = GridUtility.WorldPosToIndex(state.TargetPos, HeatmapWorldOrigin, CellSize, GridWidth);
                if (Grid[targetIndex].Density <= 0)
                {
                    isStillValid = false;
                }
            }

            // Drop the target if it became invalid
            if (!isStillValid)
            {
                state.HasTarget = false;
                state.TargetEntity = Entity.Null;
            }
        }

        // used to stagger weapons targeting update
        if ((entity.Index % NumBuckets) != CurrentBucket)
        {
            return;
        }

        LocalToWorld shipWorld = WorldLookup[ship.Value];
        quaternion combinedWorldRot = math.mul(shipWorld.Rotation, arc.RestingRotation);
        float3 shipForwardWorld = math.rotate(combinedWorldRot, new float3(0, 1, 0));
        float3 currentBarrelForward = weaponWorld.Up;

        float bestScore = -1f;
        float2 bestTarget = float2.zero;

        int radiusCells = (int)math.ceil(stats.Range / CellSize);
        int2 centerCell = GridUtility.WorldPosToCell(weaponPos, HeatmapWorldOrigin, CellSize);

        float invRangeSq = 1.0f / rangeSq;

        // precalculate min arc cos to compare to dot product
        float minArcCosine = math.cos(math.radians(arc.MaxAngle));

        // pre clamp boundaries to avoid cells outside the map
        int minX = math.max(0, centerCell.x - radiusCells);
        int maxX = math.min(GridWidth - 1, centerCell.x + radiusCells);
        int minY = math.max(0, centerCell.y - radiusCells);
        int maxY = math.min(GridHeight - 1, centerCell.y + radiusCells);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                int2 checkCell = new(x, y);

                // check if there are any targets in this cell
                int heatmapIndex = GridUtility.CellToIndex(checkCell, GridWidth);
                int density = Grid[heatmapIndex].Density;
                if (density <= 0) continue;

                float2 cellWorldPos = GridUtility.CellToWorldPosCenter(checkCell, HeatmapWorldOrigin, CellSize);
                float2 vectorToCell = cellWorldPos - weaponPos;
                float distSq = math.lengthsq(vectorToCell);

                // check range
                if (distSq > rangeSq || distSq < 0.001f) continue;

                // check if cell is revealed
                int obstacleIndex = GridUtility.WorldPosToIndex(cellWorldPos, ObstacleWorldOrigin, ObstacleGridCellSize, ObstacleGridWidth);
                if (FogArray[obstacleIndex].a != 0) continue;

                // check if the cell is in the weapon firing arc
                float invDist = math.rsqrt(distSq);
                float2 dirToCell = vectorToCell * invDist;
                float arcDot = math.dot(shipForwardWorld.xy, dirToCell);
                if (arcDot < minArcCosine) continue;

                int threat = Grid[heatmapIndex].Threat;

                // Calculate alignment (how much the turret needs to turn)
                float alignment = math.dot(currentBarrelForward.xy, dirToCell);
                float angleFactor = (alignment + 1.0f) * 0.5f;

                // calculate distance factor
                float distanceFactor = math.clamp(1.0f - (distSq * invRangeSq), 0.0f, 1.0f);

                // calculate final score
                float score = (distanceFactor * stats.DistanceBias) +
                              (angleFactor * stats.AngleBias) +
                              (threat * stats.ThreatBias) +
                              (density * stats.DensityBias);

                if (score > bestScore)
                {
                    // check line of sight
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

                    bestScore = score;
                    bestTarget = cellWorldPos;
                }
            }
        }

        if (bestScore > 0)
        {
            if (stats.RequireTargetEntity)
            {
                Entity bestEntity = Entity.Null;
                float highestThreatScore = -1f;

                int bestFlatIndex = GridUtility.WorldPosToIndex(bestTarget, HeatmapWorldOrigin, CellSize, GridWidth);

                if (SpatialMap.TryGetFirstValue(bestFlatIndex, out Entity enemy, out var iterator))
                {
                    do
                    {
                        if (EnemyValueLookup.HasComponent(enemy))
                        {
                            float threat = EnemyValueLookup[enemy].Value;
                            if (threat > highestThreatScore)
                            {
                                highestThreatScore = threat;
                                bestEntity = enemy;
                            }
                        }
                    } while (SpatialMap.TryGetNextValue(out enemy, ref iterator));
                }

                if (bestEntity != Entity.Null)
                {
                    state.HasTarget = true;
                    state.TargetEntity = bestEntity;
                    state.TargetPos = bestTarget;
                }
                else
                {
                    state.HasTarget = false;
                    state.TargetEntity = Entity.Null;
                }
            }
            else
            {
                state.HasTarget = true;
                state.TargetPos = bestTarget;
            }
        }
        else
        {
            state.HasTarget = false;
            state.TargetEntity = Entity.Null;
        }
    }
}