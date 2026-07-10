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
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        FineEnemyHeatmapComponent fineEntityMap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        TerrainGridComponent terrainGridComponent = SystemAPI.GetSingleton<TerrainGridComponent>();
        FogOfWarState fogOfWarState = SystemAPI.GetSingleton<FogOfWarState>();

        ComponentLookup<LocalToWorld> worldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<EnemyValueComponent> enemyValueLookup = SystemAPI.GetComponentLookup<EnemyValueComponent>(true);

        NativeArray<int> obstacleGrid = terrainGridComponent.Grid;
        int2 obstacleWorldOrigin = terrainGridComponent.WorldOrigin;
        int obstacleGridWidth = terrainGridComponent.GridWidth;
        int obstacleGridHeight = terrainGridComponent.GridHeight;
        float obstacleGridCellSize = terrainGridComponent.CellSize;

        HeatmapTargetingJob heatmapJob = new()
        {
            FogArray = fogOfWarState.FogArray,

            DensityGrid = fineEntityMap.DensityGrid,
            ThreatGrid = fineEntityMap.ThreatGrid,
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
    [ReadOnly] public NativeArray<Color32> FogArray;

    [ReadOnly] public NativeArray<int> DensityGrid;
    [ReadOnly] public NativeArray<int> ThreatGrid;
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
        ref WeaponState state,
        in WeaponTargetingStats stats,
        in WeaponFiringArc arc,
        in LocalToWorld weaponWorld,
        in ShipReference ship
        ) 
    {
        if (ship.Value == Entity.Null) return; // ship ref not set up yet
        LocalToWorld shipWorld = WorldLookup[ship.Value];
        quaternion combinedWorldRot = math.mul(shipWorld.Rotation, arc.RestingRotation);
        float3 shipForwardWorld = math.rotate(combinedWorldRot, new float3(0, 1, 0));
        float3 currentBarrelForward = weaponWorld.Up;

        float bestScore = -1f;
        float2 bestTarget = float2.zero;

        float2 weaponPos = weaponWorld.Position.xy;
        float rangeSq = stats.RangeSq;
        int radiusCells = (int)math.ceil(stats.Range / CellSize);
        int2 centerCell = GridUtility.WorldPosToCell(weaponPos, HeatmapWorldOrigin, CellSize);

        for (int x = -radiusCells; x <= radiusCells; x++)
        {
            for (int y = -radiusCells; y <= radiusCells; y++)
            {
                int2 checkCell = centerCell + new int2(x, y);
                if (!GridUtility.IsWithinBounds(checkCell, GridWidth, GridHeight)) continue;

                // check if there are any targets in this cell
                int heatmapIndex = GridUtility.CellToIndex(checkCell, GridWidth);
                int density = DensityGrid[heatmapIndex];
                if (density <= 0) continue;

                float2 cellWorldPos = GridUtility.CellToWorldPosCenter(checkCell, HeatmapWorldOrigin, CellSize);
                float2 vectorToCell = cellWorldPos - weaponPos;
                float distSq = math.lengthsq(vectorToCell);

                // check range
                if (distSq > rangeSq) continue;

                // check if cell is revealed
                int obstacleIndex = GridUtility.WorldPosToIndex(cellWorldPos, ObstacleWorldOrigin, ObstacleGridCellSize, ObstacleGridWidth);
                if (FogArray[obstacleIndex].a != 0) continue;

                // check if the cell is in the weapon firing arc
                float2 dirToCell = math.normalize(vectorToCell);
                float angleToShipResting = math.degrees(math.acos(math.dot(shipForwardWorld.xy, dirToCell)));
                if (angleToShipResting > arc.MaxAngle) continue;

                int threat = ThreatGrid[heatmapIndex];

                // Calculate alignment (how much the turret needs to turn)
                float alignment = math.dot(currentBarrelForward.xy, dirToCell);
                float angleFactor = (alignment + 1.0f) * 0.5f;

                // calculate distance factor
                float distanceFactor = math.clamp(1.0f - (distSq / rangeSq), 0.0f, 1.0f);

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