using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct LevelGen_HiveSiegePathSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameStateSingleton>();
        state.RequireForUpdate<LevelGenerationStatus>();
        state.RequireForUpdate<TerrainGridComponent>();
        state.RequireForUpdate<MothershipTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<GameStateSingleton>(out var gameState)
            || !SystemAPI.TryGetSingletonRW<LevelGenerationStatus>(out var status)
            || gameState.CurrentState != GameState.Generating
            || status.ValueRO.HiveSiegePathsGenerated
            || !status.ValueRO.PlayerFleetSpawned
            || !status.ValueRO.HivesGenerated
            || !status.ValueRO.TerrainGenerated
            || !SystemAPI.TryGetSingleton<TerrainGridComponent>(out var terrainGridComponent))
            return;

        // mark as complete
        status.ValueRW.HiveSiegePathsGenerated = true;

        // get variables 
        NativeArray<int> grid = terrainGridComponent.Grid;
        int gridWidth = terrainGridComponent.GridWidth;
        int gridHeight = terrainGridComponent.GridHeight;
        int2 worldOrigin = terrainGridComponent.WorldOrigin;
        float cellSize = terrainGridComponent.CellSize;

        var mothershipEntity = SystemAPI.GetSingletonEntity<MothershipTag>();
        var mothershipPos = SystemAPI.GetComponentRO<LocalTransform>(mothershipEntity).ValueRO.Position;
        var mothershipGridPos = GridUtility.WorldPosToCell(mothershipPos, worldOrigin, cellSize);

        // create a siege path for each hive present
        foreach (var (_, transform, hiveEntity) in SystemAPI.Query<RefRO<HiveComponent>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            var hivePos = transform.ValueRO.Position;
            var hiveGridPos = GridUtility.WorldPosToCell(hivePos, worldOrigin, cellSize);

            // get path to mothership
            NativeList<float2> path = new NativeList<float2>(Allocator.Temp);
            BurstPathfinder.CalculateAStar(hiveGridPos, mothershipGridPos, grid, gridWidth, gridHeight, worldOrigin, false, ref path);

            // Copy path to buffer on entity
            var pathBuffer = state.EntityManager.GetBuffer<SiegePathWaypointElement>(hiveEntity);
   
            float maxVariance = 5.0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                float2 p0 = path[i];
                float2 p1 = path[i + 1];

                float2 forward = math.normalizesafe(p1 - p0);
                float2 right = new float2(-forward.y, forward.x);

                float noiseVal = noise.snoise(new float2(i * 0.1f, hiveEntity.Index * 13.37f));

                float t = (float)i / path.Length;
                float envelope = t * (1.0f - t) * 4.0f;

                float2 offset = right * noiseVal * maxVariance * envelope;
                float2 finalPos = p0 + offset;

                int maxSafetyChecks = 5;
                for (int step = 0; step < maxSafetyChecks; step++)
                {
                    int2 cell = GridUtility.WorldPosToCell(new float3(finalPos, 0), worldOrigin, cellSize);

                    if (cell.x >= 0 && cell.x < gridWidth && cell.y >= 0 && cell.y < gridHeight)
                    {
                        int gridIndex = cell.x + (cell.y * gridWidth);

                        if (grid[gridIndex] > 0)
                        {
                            float tBack = (float)(step + 1) / maxSafetyChecks;
                            finalPos = math.lerp(p0 + offset, p0, tBack);
                            continue;
                        }
                    }
                    break; 
                }

                pathBuffer.Add(new SiegePathWaypointElement
                {
                    Position = finalPos,
                });
            }

            if (path.Length > 0)
            {
                pathBuffer.Add(new SiegePathWaypointElement
                {
                    Position = path[path.Length - 1] 
                });
            }

            path.Dispose();
        }
    }
}
