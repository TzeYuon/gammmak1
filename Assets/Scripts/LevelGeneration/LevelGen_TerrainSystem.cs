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

        state.EntityManager.CreateSingleton(new TerrainGridComponent());
        state.EntityManager.CreateSingleton(new EnemyFlowFieldComponent());

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

        status.ValueRW.TerrainGenerated = true;
        var oldTerrainQuery = state.EntityManager.CreateEntityQuery(typeof(ProceduralTerrainTag));
        state.EntityManager.DestroyEntity(oldTerrainQuery);
        
        LevelBlueprint blueprint;
        if (!SystemAPI.TryGetSingleton<LevelBlueprint>(out blueprint))
        {
            blueprint = new LevelBlueprint();
        }

        if (blueprint.GridWidth <= 0)
        {
            blueprint = new LevelBlueprint
            {
                GridWidth = 200,
                GridHeight = 200,
                TerrainType = TerrainType.TwoRing   ,
                NoiseScale = 0.15f,
                WallThreshold = 0.75f,
                PoisonThreshold = 0.85f,
                MudThreshold = 0.15f,
                Seed = (uint)UnityEngine.Random.Range(1, 99999)
            };
        }
        var ecb = new EntityCommandBuffer(Allocator.Temp);

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

        float centerX = width / 2f;
        float centerY = height / 2f;
        float maxDistFromCenter = math.sqrt(centerX * centerX + centerY * centerY);

        // Perlin noise
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (InSafeZone(x, y, width, height, safezoneRadius)) continue;

                // Base noise
                float elevation = noise.snoise(new float2(x + randomOffset, y + randomOffset) * blueprint.NoiseScale * 0.8f) * 0.5f + 0.5f;
                float distToCenter = math.distance(new float2(x, y), new float2(centerX, centerY));
                float normalizedDist = distToCenter / maxDistFromCenter;

                // Generate Terrain based on enum
                switch (blueprint.TerrainType)
                {
                    case TerrainType.Asteroids:
                        // Default
                        break;

                    case TerrainType.Cavern:
                        // Pushes the edges up into solid rock to form an enclosed arena
                        elevation += (normalizedDist * 1.5f) - 0.5f;
                        break;

                    case TerrainType.OneTunnel:
                        // Carves a single diagonal trench
                        float distToDiagonal = math.abs(x - y) * 0.7071f;
                        float tunnelMask = distToDiagonal / (width * 0.3f);
                        elevation += tunnelMask * 1.5f;
                        break;

                    case TerrainType.TwoTunnel:
                        // Carves a giant "X" connecting all 4 corners
                        float distToDiag1 = math.abs(x - y) * 0.7071f;
                        float distToDiag2 = math.abs(x - (height - y)) * 0.7071f;
                        float closestTunnelDist = math.min(distToDiag1, distToDiag2);
                        elevation += math.smoothstep(12f, 25f, closestTunnelDist) * 2.0f;
                        break;

                    case TerrainType.Ring:
                        // Creates a massive hollow ring of walls between dist 35 and 55
                        float ringThickness = math.abs(distToCenter - 45f);
                        if (ringThickness < 10f)
                        {
                            // Carves 4 Entrances (N,S,E,W)
                            bool isDoor = math.abs(x - centerX) < 12f || math.abs(y - centerY) < 12f;
                            if (!isDoor)
                            {
                                elevation += 2.0f;
                            }
                        }
                        break;

                    case TerrainType.TwoRing:
                        float dx = x - centerX;
                        float dy = y - centerY;
                        float angleDeg = 90f - math.degrees(math.atan2(dy, dx));
                        if (angleDeg < 0f) angleDeg += 360f; // Keep it between 0 and 360

                        float innerRingRadius = 35f;
                        float outerRingRadius = 70f;
                        float radiusThickness = 7f;

                        float distToInner = math.abs(distToCenter - innerRingRadius);
                        float distToOuter = math.abs(distToCenter - outerRingRadius);

                        // Inner Ring Logic (Entrances at 0, 120, 240)
                        if (distToInner < radiusThickness)
                        {
                            float modInner = angleDeg % 120f;
                            bool isInnerGap = (modInner < 12f || modInner > 108f);

                            if (isInnerGap) elevation -= 1.0f; 
                            else elevation += 2.0f;
                        }

                        // Outer Ring Logic (Entrances at 60, 180, 300)
                        if (distToOuter < radiusThickness)
                        {
                            float shiftedAngle = (angleDeg + 360f - 60f) % 360f;
                            float modOuter = shiftedAngle % 120f;
                            bool isOuterGap = (modOuter < 9f || modOuter > 111f);

                            if (isOuterGap) elevation -= 1.0f;
                            else elevation += 2.0f;
                        }
                        break;
                }
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
                flowField[x + y * width] = float2.zero;
            }
        }

        var meshGen = TerrainMeshGenerator.GetInstance();
        if (meshGen != null)
        {
            meshGen.BuildTerrainMesh(grid, width, height, worldOrigin);
        }
        else
        {
            Debug.LogError("ERROR: TerrainMeshGenerator was not found in the scene");
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

    public struct ProceduralTerrainTag : IComponentData { }
}