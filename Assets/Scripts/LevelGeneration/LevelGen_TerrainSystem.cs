using System;
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

        // mark as complete
        status.ValueRW.TerrainGenerated = true;
        var oldTerrainQuery = state.EntityManager.CreateEntityQuery(typeof(ProceduralTerrainTag));
        state.EntityManager.DestroyEntity(oldTerrainQuery);

        if (!SystemAPI.TryGetSingleton(out LevelBlueprint blueprint))
        {
            blueprint = new LevelBlueprint();
        }

        if (blueprint.GridWidth <= 0)
        {
            blueprint.GridWidth = 200;
            blueprint.GridHeight = 200;
            blueprint.NoiseScale = 0.15f;
            blueprint.WallThreshold = 0.75f;
            blueprint.PoisonThreshold = 0.78f;
            blueprint.MudThreshold = 0.15f;
            blueprint.Seed = (uint)UnityEngine.Random.Range(1, 99999);
        }
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        int width = blueprint.GridWidth;
        int height = blueprint.GridHeight;
        int safezoneRadius = 20;
        int2 worldOrigin = new int2(1000 - width / 2, 1000 - height / 2);
        LevelSpawnData spawnData = new LevelSpawnData();

        float Xtl = 1000 - width / 2 + safezoneRadius;
        float Ytl = 1000 + height / 2 - safezoneRadius;
        float Xtr = 1000 + width / 2 - safezoneRadius;
        float Ytr = 1000 + height / 2 - safezoneRadius;
        float Xbr = 1000 + width / 2 - safezoneRadius;
        float Ybr = 1000 - height / 2 + safezoneRadius;
        float Xbl = 1000 - width / 2 + safezoneRadius;
        float Ybl = 1000 - height / 2 + safezoneRadius;
        float Xt = 1000;
        float Yt = 1000 + height / 2 - safezoneRadius;
        float Xr = 1000 + width / 2 - safezoneRadius;
        float Yr = 1000;
        float Xl = 1000 - width / 2 + safezoneRadius;
        float Yl = 1000;
        float Xb = 1000;
        float Yb = 1000 - height / 2 + safezoneRadius;

        unsafe
        {
            switch (blueprint.TerrainType)
            {
                case TerrainType.Asteroids:
                    // Default
                    spawnData.MothershipSpawn = new float3(1000 - width / 2 + safezoneRadius, 1000 - height / 2 + safezoneRadius, 0);
                    spawnData.TotalHiveSpots = 1;
                    spawnData.ActiveHiveCount = 1;
                    spawnData.HiveSpawnsX[0] = Xtr; spawnData.HiveSpawnsY[0] = Ytr;
                    break;

                case TerrainType.Cavern:
                    // Maybe can spawn new hives periodically while starting with <8?
                    spawnData.MothershipSpawn = new float3(1000, 1000, 0);
                    spawnData.TotalHiveSpots = 8;
                    spawnData.ActiveHiveCount = 2;
                    spawnData.HiveSpawnsX[0] = Xtl; spawnData.HiveSpawnsY[0] = Ytl;
                    spawnData.HiveSpawnsX[1] = Xtr; spawnData.HiveSpawnsY[1] = Ytr;
                    spawnData.HiveSpawnsX[2] = Xbr; spawnData.HiveSpawnsY[2] = Ybr;
                    spawnData.HiveSpawnsX[3] = Xbl; spawnData.HiveSpawnsY[3] = Ybl;
                    spawnData.HiveSpawnsX[4] = Xt; spawnData.HiveSpawnsY[4] = Yt;
                    spawnData.HiveSpawnsX[5] = Xr; spawnData.HiveSpawnsY[5] = Yr;
                    spawnData.HiveSpawnsX[6] = Xl; spawnData.HiveSpawnsY[6] = Yl;
                    spawnData.HiveSpawnsX[7] = Xb; spawnData.HiveSpawnsY[7] = Yb;
                    break;
                case TerrainType.OneTunnel:
                    spawnData.MothershipSpawn = new float3(1000 - width / 2 + safezoneRadius, 1000 - height / 2 + safezoneRadius, 0);
                    spawnData.TotalHiveSpots = 1;
                    spawnData.ActiveHiveCount = 1;
                    spawnData.HiveSpawnsX[0] = Xtr; spawnData.HiveSpawnsY[0] = Ytr;
                    break;

                case TerrainType.Maze:
                    spawnData.MothershipSpawn = new float3(1000 - width / 2 + safezoneRadius, 1000 - height / 2 + safezoneRadius, 0);
                    spawnData.TotalHiveSpots = 4;
                    spawnData.ActiveHiveCount = 2;
                    spawnData.HiveSpawnsX[0] = Xtr; spawnData.HiveSpawnsY[0] = Ytr;
                    spawnData.HiveSpawnsX[1] = Xbr; spawnData.HiveSpawnsY[1] = Ybr;
                    spawnData.HiveSpawnsX[2] = Xtl; spawnData.HiveSpawnsY[2] = Ytl;
                    spawnData.HiveSpawnsX[3] = Xt; spawnData.HiveSpawnsY[3] = Yt;
                    break;

                default:
                    spawnData.MothershipSpawn = new float3(1000, 1000, 0);
                    spawnData.TotalHiveSpots = 4;
                    spawnData.ActiveHiveCount = 4;
                    spawnData.HiveSpawnsX[0] = Xtl; spawnData.HiveSpawnsY[0] = Ytl;
                    spawnData.HiveSpawnsX[1] = Xtr; spawnData.HiveSpawnsY[1] = Ytr;
                    spawnData.HiveSpawnsX[2] = Xbr; spawnData.HiveSpawnsY[2] = Ybr;
                    spawnData.HiveSpawnsX[3] = Xbl; spawnData.HiveSpawnsY[3] = Ybl;
                    break;
            }
        }

        NativeArray<float3> spawnPoints = new NativeArray<float3>(spawnData.TotalHiveSpots + 1, Allocator.Temp);
        spawnPoints[0] = spawnData.MothershipSpawn;
        unsafe
        {
            for (int i = 0; i < spawnData.TotalHiveSpots; i++)
            {
                spawnPoints[i + 1] = new float3(spawnData.HiveSpawnsX[i], spawnData.HiveSpawnsY[i], 0);
            }
        }

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

        if (blueprint.TerrainType == TerrainType.Maze)
        {
            CarveMaze(grid, width, height, rng);
        }
        else
        {
            // Perlin noise
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (InSafeZone(x, y, width, height, safezoneRadius, spawnPoints, worldOrigin)) continue;
                    // Base noise
                    float elevation = noise.snoise(new float2(x + randomOffset, y + randomOffset) * blueprint.NoiseScale * 0.8f) * 0.5f + 0.5f;
                    float distToCenter = math.distance(new float2(x, y), new float2(centerX, centerY));

                    // Generate Terrain based on enum
                    switch (blueprint.TerrainType)
                    {
                        case TerrainType.Asteroids:
                            break;

                        case TerrainType.Cavern:
                            // Carves a circular cavern in the center
                            float normalizedDist = distToCenter / maxDistFromCenter;
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
                            // Logic
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
            for (int i = 0; i < 3; i++) grid = SmoothAsteroidCaves(grid, width, height, safezoneRadius, spawnPoints, worldOrigin);
        }

        int borderThickness = 1;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x < borderThickness || x >= width - borderThickness ||
                    y < borderThickness || y >= height - borderThickness)
                {
                    grid[x, y] = 1; // Force Wall
                }
                else if (InSafeZone(x, y, width, height, safezoneRadius, spawnPoints, worldOrigin))
                {
                    grid[x, y] = 0;
                }
            }
        }

        // Merge disconnected islands
        ConnectDisconnectedIslands(grid, width, height);

        // Hazards and ECS spawning
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int currentTile = grid[x, y];

                if (currentTile == 0 && !InSafeZone(x, y, width, height, safezoneRadius, spawnPoints, worldOrigin))
                {
                    int wallNeighbors = GetSurroundingWallCount(grid, x, y, width, height);

                    if (wallNeighbors >= 3)
                    {
                        grid[x, y] = 2; // Mud
                    }
                    else if (wallNeighbors <= 1)
                    {
                        float poisonNoise = noise.snoise(new float2(x + randomOffset, y + randomOffset) * 0.01f); 
                        if (poisonNoise > blueprint.PoisonThreshold) grid[x, y] = 3; // Poison in open areas
                    }
                }

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

        var spawnerEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(spawnerEntity, spawnData);
        state.EntityManager.AddComponentData(spawnerEntity, new LevelEntityTag());
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

    // HELPER FUNCTIONS
    private bool InSafeZone(int x, int y, int width, int height, int radius, NativeArray<float3> spawns, int2 worldOrigin)
    {
        float2 currentWorldPos = new float2(x + worldOrigin.x, y + worldOrigin.y);
        for (int i = 0; i < spawns.Length; i++)
        {
            if (math.distance(currentWorldPos, spawns[i].xy) < radius)
            {
                return true;
            }
        }
        return false;
    }

    private int[,] SmoothAsteroidCaves(int[,] oldGrid, int width, int height, int radius, NativeArray<float3> spawns, int2 worldOrigin)
    {
        int[,] newGrid = new int[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (InSafeZone(x, y, width, height, radius, spawns, worldOrigin)) { newGrid[x, y] = 0; continue; }
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
            int tunnelRadius = 2;
            CarveTunnel(bestStart, bestEnd, grid, width, height, tunnelRadius);
        }
    }

    private void CarveTunnel(Vector2Int start, Vector2Int end, int[,] grid, int width, int height, int tunnelRadius)
    {
        int x = start.x, y = start.y;
        int dx = math.abs(end.x - start.x), dy = math.abs(end.y - start.y);
        int sx = start.x < end.x ? 1 : -1, sy = start.y < end.y ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            for (int tx = -tunnelRadius; tx <= tunnelRadius; tx++)
            {
                for (int ty = -tunnelRadius; ty <= tunnelRadius; ty++)
                {
                    if (tx * tx + ty * ty <= tunnelRadius * tunnelRadius)
                    {
                        if (x + tx >= 0 && x + tx < width && y + ty >= 0 && y + ty < height)
                        {
                            grid[x + tx, y + ty] = 0;
                        }
                    }
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

    private void CarveMaze(int[,] grid, int width, int height, Unity.Mathematics.Random rng)
    {
        // Fill the entire map with walls
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = 1;
            }
        }

        int numWorms = 50;
        int maxLifespan = 500; // Max length of tunnel

        int2[] directions = { new int2(0, 1), new int2(1, 0), new int2(0, -1), new int2(-1, 0) };

        for (int i = 0; i < numWorms; i++)
        {
            int2 wormPos = new int2(rng.NextInt(20, width - 20), rng.NextInt(20, height - 20));

            for (int life = 0; life < maxLifespan; life++)
            {
                grid[wormPos.x, wormPos.y] = 0;

                int2 dir = directions[rng.NextInt(0, 4)];

                int2 nextPos = wormPos + dir;
                if (nextPos.x > 3 && nextPos.x < width - 3 && nextPos.y > 3 && nextPos.y < height - 3)
                {
                    wormPos = nextPos;
                }
            }
        }
    }

    public struct ProceduralTerrainTag : IComponentData { }
}