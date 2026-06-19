using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
partial struct LevelGen_TerrainSystem : ISystem
{
    private NativeArray<int> obstacleGrid;
    private NativeArray<float2> flowField;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameStateSingleton>();
        state.RequireForUpdate<LevelGenerationStatus>();

        state.RequireForUpdate<TerrainPrefabsComponent>();

        state.EntityManager.CreateSingleton(new TerrainGridComponent());
        state.EntityManager.CreateSingleton(new EnemyFlowFieldComponent());

        // TEMPORARY: Inject a default level blueprint until the UI is built
        state.EntityManager.CreateSingleton(new LevelBlueprintComponent
        {
            GridWidth = 200,
            GridHeight = 200,
            Shape = MapShape.Standard,
            NoiseScale = 0.15f,
            WallThreshold = 0.60f,
            PoisonThreshold = 0.75f,
            MudThreshold = 0.25f,
            Seed = 12345
        });
    }

    public void OnDestroy(ref SystemState state)
    {
        if (obstacleGrid.IsCreated) obstacleGrid.Dispose();
        if (flowField.IsCreated) flowField.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<GameStateSingleton>(out var gameState) ||
            gameState.CurrentState != GameState.Generating) return;

        if (!SystemAPI.TryGetSingletonRW<LevelGenerationStatus>(out var status)) return;
        if (status.ValueRO.TerrainGenerated) return;

        var blueprint = SystemAPI.GetSingleton<LevelBlueprintComponent>();
        var prefabs = SystemAPI.GetSingleton<TerrainPrefabsComponent>();
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        RefRW<TerrainGridComponent> terrainGridComponent = SystemAPI.GetSingletonRW<TerrainGridComponent>();
        RefRW<EnemyFlowFieldComponent> enemyFlowFieldComponent = SystemAPI.GetSingletonRW<EnemyFlowFieldComponent>();

        int width = blueprint.GridWidth;
        int height = blueprint.GridHeight;
        int safezoneRadius = 20;
        int2 worldOrigin = new int2(1000 - width / 2, 1000 - height / 2);

        var rng = Unity.Mathematics.Random.CreateFromIndex(blueprint.Seed);
        float randomOffset = rng.NextFloat(0f, 9999f);

        int totalCells = width * height;
        if (!obstacleGrid.IsCreated || obstacleGrid.Length != totalCells)
        {
            if (obstacleGrid.IsCreated) obstacleGrid.Dispose();
            if (flowField.IsCreated) flowField.Dispose();
            obstacleGrid = new NativeArray<int>(totalCells, Allocator.Persistent);
            flowField = new NativeArray<float2>(totalCells, Allocator.Persistent);
        }

        int[,] grid = new int[width, height];

        // Perlin noise
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (InSafeZone(x, y, width, height, safezoneRadius)) continue;
                float elevation = noise.snoise(new float2(x + randomOffset, y + randomOffset) * blueprint.NoiseScale * 0.8f) * 0.5f + 0.5f;
                grid[x, y] = (elevation > blueprint.WallThreshold) ? 1 : 0;
            }
        }

        // Cellular automata
        for (int i = 0; i < 3; i++) grid = SmoothAsteroidCaves(grid, width, height, safezoneRadius);

        // Merge disconnected islands
        ConnectDisconnectedIslands(grid, width, height);

        // Hazards and ECS spawning
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int currentTile = grid[x, y];

                if (currentTile == 0 && !InSafeZone(x, y, width, height, safezoneRadius))
                {
                    float poisonNoise = noise.snoise(new float2(x + randomOffset + 5000f, y + randomOffset + 5000f) * blueprint.NoiseScale) * 0.5f + 0.5f;
                    float mudNoise = noise.snoise(new float2(x + randomOffset + 2000f, y + randomOffset + 2000f) * blueprint.NoiseScale) * 0.5f + 0.5f;

                    if (poisonNoise > blueprint.PoisonThreshold) grid[x, y] = 3;
                    else if (mudNoise > 1.0f - blueprint.MudThreshold) grid[x, y] = 2;
                }

                // Copy to ECS Native Array
                int finalTile = grid[x, y];
                obstacleGrid[x + y * width] = finalTile;
                flowField[x + y * width] = float2.zero; // Reset flow field

                // Instantly spawn ECS terrain entities
                Entity prefabToSpawn = Entity.Null;
                if (finalTile == 1) prefabToSpawn = prefabs.WallEntity;
                else if (finalTile == 2) prefabToSpawn = prefabs.MudEntity;
                else if (finalTile == 3) prefabToSpawn = prefabs.PoisonEntity;

                if (prefabToSpawn != Entity.Null)
                {
                    Entity tileEntity = ecb.Instantiate(prefabToSpawn);
                    float3 spawnPos = new float3(x + worldOrigin.x, y + worldOrigin.y, 0);
                    ecb.SetComponent(tileEntity, LocalTransform.FromPosition(spawnPos));
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();

        // Update global singletons
        RefRW<TerrainGridComponent> terrainComp = SystemAPI.GetSingletonRW<TerrainGridComponent>();
        RefRW<EnemyFlowFieldComponent> flowComp = SystemAPI.GetSingletonRW<EnemyFlowFieldComponent>();

        terrainComp.ValueRW.Grid = obstacleGrid;
        terrainComp.ValueRW.WorldOrigin = worldOrigin;
        terrainComp.ValueRW.GridWidth = width;
        terrainComp.ValueRW.GridHeight = height;
        terrainComp.ValueRW.CellSize = 1;

        flowComp.ValueRW.FlowField = flowField;
        flowComp.ValueRW.WorldOrigin = worldOrigin;
        flowComp.ValueRW.GridWidth = width;
        flowComp.ValueRW.GridHeight = height;
        flowComp.ValueRW.CellSize = 1;

        status.ValueRW.TerrainGenerated = true;
    }

    // Helper Functions
    private bool InSafeZone(int x, int y, int width, int height, int radius)
    {
        bool corner = (x < radius || x >= width - radius) && (y < radius || y >= height - radius);
        bool center = math.abs(x - width / 2) <= radius && math.abs(y - height / 2) <= radius;
        return corner || center;
    }

    private int[,] SmoothAsteroidCaves(int[,] oldGrid, int width, int height, int radius)
    {
        int[,] newGrid = new int[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (InSafeZone(x, y, width, height, radius)) { newGrid[x, y] = 0; continue; }
                int walls = GetSurroundingWallCount(oldGrid, x, y, width, height);
                if (walls > 4) newGrid[x, y] = 1;
                else if (walls < 4) newGrid[x, y] = 0;
                else newGrid[x, y] = oldGrid[x, y];
            }
        }
        return newGrid;
    }

    private int GetSurroundingWallCount(int[,] checkGrid, int gridX, int gridY, int width, int height)
    {
        int wallCount = 0;
        for (int nx = gridX - 1; nx <= gridX + 1; nx++)
        {
            for (int ny = gridY - 1; ny <= gridY + 1; ny++)
            {
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (nx != gridX || ny != gridY) { if (checkGrid[nx, ny] == 1) wallCount++; }
                }
                else wallCount++;
            }
        }
        return wallCount;
    }

    private void ConnectDisconnectedIslands(int[,] grid, int width, int height)
    {
        int[,] mapFlags = new int[width, height];
        List<List<Vector2Int>> allIslands = new List<List<Vector2Int>>();
        int biggestIslandIndex = 0;
        int maxTiles = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == 0 && mapFlags[x, y] == 0)
                {
                    List<Vector2Int> newIsland = GetIsland(x, y, mapFlags, grid, width, height);
                    if (newIsland.Count > maxTiles)
                    {
                        maxTiles = newIsland.Count;
                        biggestIslandIndex = allIslands.Count;
                    }
                    allIslands.Add(newIsland);
                }
            }
        }

        if (allIslands.Count <= 1) return;

        List<Vector2Int> mainIsland = allIslands[biggestIslandIndex];

        for (int i = 0; i < allIslands.Count; i++)
        {
            if (i == biggestIslandIndex) continue;
            List<Vector2Int> smallIsland = allIslands[i];

            float bestDist = float.MaxValue;
            Vector2Int bestStart = Vector2Int.zero;
            Vector2Int bestEnd = Vector2Int.zero;

            foreach (Vector2Int tileA in smallIsland)
            {
                foreach (Vector2Int tileB in mainIsland)
                {
                    float dist = math.distancesq(new float2(tileA.x, tileA.y), new float2(tileB.x, tileB.y));
                    if (dist < bestDist) { bestDist = dist; bestStart = tileA; bestEnd = tileB; }
                }
            }
            CarveTunnel(bestStart, bestEnd, grid, width, height);
        }
    }

    private void CarveTunnel(Vector2Int start, Vector2Int end, int[,] grid, int width, int height)
    {
        int x = start.x, y = start.y;
        int dx = math.abs(end.x - start.x), dy = math.abs(end.y - start.y);
        int sx = start.x < end.x ? 1 : -1, sy = start.y < end.y ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            for (int tx = -1; tx <= 1; tx++)
            {
                for (int ty = -1; ty <= 1; ty++)
                {
                    if (x + tx >= 0 && x + tx < width && y + ty >= 0 && y + ty < height)
                        grid[x + tx, y + ty] = 0;
                }
            }
            if (x == end.x && y == end.y) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }

    private List<Vector2Int> GetIsland(int startX, int startY, int[,] mapFlags, int[,] grid, int width, int height)
    {
        List<Vector2Int> tiles = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        queue.Enqueue(new Vector2Int(startX, startY));
        mapFlags[startX, startY] = 1;
        Vector2Int[] neighbors = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int tile = queue.Dequeue();
            tiles.Add(tile);

            foreach (Vector2Int dir in neighbors)
            {
                Vector2Int n = tile + dir;
                if (n.x >= 0 && n.x < width && n.y >= 0 && n.y < height)
                {
                    if (grid[n.x, n.y] == 0 && mapFlags[n.x, n.y] == 0)
                    {
                        mapFlags[n.x, n.y] = 1;
                        queue.Enqueue(n);
                    }
                }
            }
        }
        return tiles;
    }
}