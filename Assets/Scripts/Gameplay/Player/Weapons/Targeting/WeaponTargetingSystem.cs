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

        NativeArray<int> obstacleGrid = terrainGridComponent.Grid;
        int2 obstacleWorldOrigin = terrainGridComponent.WorldOrigin;
        int obstacleGridWidth = terrainGridComponent.GridWidth;
        int obstacleGridHeight = terrainGridComponent.GridHeight;
        float obstacleGridCellSize = terrainGridComponent.CellSize;

        HeatmapTargetingJob heatmapJob = new()
        {
            FogArray = fogOfWarState.FogArray,
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
        quaternion combinedWorldRot = math.mul(shipWorld.Rotation, arc.RestingRotation);
        float3 shipForwardWorld = math.rotate(combinedWorldRot, new float3(0, 1, 0));
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

                // check if there are any targets in this cell
                int heatmapIndex = GridUtility.CellToIndex(checkCell, GridWidth);
                int density = FineDensityMap[heatmapIndex];
                if (density <= 0) continue;

                float2 cellWorldPos = GridUtility.CellToWorldPosCenter(checkCell, HeatmapWorldOrigin, CellSize);
                float2 vectorToCell = cellWorldPos - weaponWorld.Position.xy;
                float distSq = math.lengthsq(vectorToCell);

                // check range
                if (distSq > stats.Range * stats.Range) continue;

                // check if cell is revealed
                int obstacleIndex = GridUtility.WorldPosToIndex(cellWorldPos, ObstacleWorldOrigin, ObstacleGridCellSize, ObstacleGridWidth);
                if (FogArray[obstacleIndex].a != 0) continue;

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