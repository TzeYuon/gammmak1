
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    public int width = 200;
    public int height = 200;
    public int safezoneRadius = 20;

    public Vector2Int worldOrigin = new Vector2Int(1000, 1000);

    // Noise settings
    public float scale = 0.15f;
    public float randomOffset = 0f;

    public int[,] grid; // 0 = Walkable, 1 = Wall, 2 = Mud, 3 = Poison
    public NativeArray<int> nativeGrid;
    public NativeArray<float2> nativeFlowField;

    // Terrain prefabs
    public GameObject wallPrefab;
    public GameObject mudPrefab;
    public GameObject poisonPrefab; 
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    void Start()
    {
        grid = new int[width, height];
        nativeGrid = new NativeArray<int>(width * height, Allocator.Persistent);
        nativeFlowField = new NativeArray<float2>(width * height, Allocator.Persistent);
        randomOffset = UnityEngine.Random.Range(0f, 9999f);
        GenerateTerrain();
    }

    bool IsInSafeZone(int x, int y)
    {
        if (x < safezoneRadius && y < safezoneRadius) return true;

        if (x > (width / 2) - safezoneRadius && x < (width / 2) + safezoneRadius &&
            y > (height / 2) - safezoneRadius && y < (height / 2) + safezoneRadius) return true;

        return false;
    }

    void GenerateTerrain()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (IsInSafeZone(x, y))
                {
                    grid[x, y] = 0;
                    continue;
                }

                float elevation = Mathf.PerlinNoise((x + randomOffset) * scale * 0.8f, (y + randomOffset) * scale * 0.8f);

                grid[x, y] = (elevation > 0.55f) ? 1 : 0;
            }
        }

        // Cellulat Automata
        for (int i = 0; i < 3; i++)
        {
            grid = SmoothAsteroidCaves(grid);
        }

        PurgeDisconnectedIslands();

        // Spawn mud and poison
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int currentTile = grid[x, y];

                if (currentTile == 0 && !IsInSafeZone(x, y))
                {
                    float poisonNoise = Mathf.PerlinNoise((x + randomOffset + 5000f) * scale, (y + randomOffset + 5000f) * scale);
                    float mudNoise = Mathf.PerlinNoise((x + randomOffset + 2000f) * scale, (y + randomOffset + 2000f) * scale);

                    if (poisonNoise > 0.75f)
                        grid[x, y] = 3; // Poison Patch
                    else if (mudNoise > 0.75f)
                        grid[x, y] = 2; // Mud Patch
                }

                int finalTile = grid[x, y];
                nativeGrid[x + y * width] = finalTile;

                Vector3 spawnPos = new Vector3(x + worldOrigin.x, y + worldOrigin.y, 0);

                if (finalTile == 1 && wallPrefab != null)
                    Instantiate(wallPrefab, spawnPos, Quaternion.identity, this.transform);
                else if (finalTile == 2 && mudPrefab != null)
                    Instantiate(mudPrefab, spawnPos, Quaternion.identity, this.transform);
                else if (finalTile == 3 && poisonPrefab != null)
                    Instantiate(poisonPrefab, spawnPos, Quaternion.identity, this.transform);
            }
        }
    }
    void PurgeDisconnectedIslands()
    {
        int[,] mapFlags = new int[width, height];
        List<List<Vector2Int>> allIslands = new List<List<Vector2Int>>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == 0 && mapFlags[x, y] == 0)
                {
                    List<Vector2Int> newIsland = GetIsland(x, y, mapFlags);
                    allIslands.Add(newIsland);
                }
            }
        }

        // Find the biggest island
        int biggestIslandIndex = 0;
        int maxTiles = 0;

        for (int i = 0; i < allIslands.Count; i++)
        {
            if (allIslands[i].Count > maxTiles)
            {
                maxTiles = allIslands[i].Count;
                biggestIslandIndex = i;
            }
        }

        // Turn every other island into walls
        for (int i = 0; i < allIslands.Count; i++)
        {
            if (i != biggestIslandIndex)
            {
                foreach (Vector2Int tile in allIslands[i])
                {
                    grid[tile.x, tile.y] = 1;
                }
            }
        }
    }

    List<Vector2Int> GetIsland(int startX, int startY, int[,] mapFlags)
    {
        List<Vector2Int> tiles = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        queue.Enqueue(new Vector2Int(startX, startY));
        mapFlags[startX, startY] = 1;

        while (queue.Count > 0)
        {
            Vector2Int tile = queue.Dequeue();
            tiles.Add(tile);

            Vector2Int[] neighbors = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
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

    // Cellular Automata Smoothing Function
    int[,] SmoothAsteroidCaves(int[,] oldGrid)
    {
        int[,] newGrid = new int[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (IsInSafeZone(x, y))
                {
                    newGrid[x, y] = 0;
                    continue;
                }

                int neighborWallCount = GetSurroundingWallCount(oldGrid, x, y);

                if (neighborWallCount > 4) newGrid[x, y] = 1;
                else if (neighborWallCount < 4) newGrid[x, y] = 0;
                else newGrid[x, y] = oldGrid[x, y];
            }
        }
        return newGrid;
    }

    int GetSurroundingWallCount(int[,] checkGrid, int gridX, int gridY)
    {
        int wallCount = 0;
        for (int neighborX = gridX - 1; neighborX <= gridX + 1; neighborX++)
        {
            for (int neighborY = gridY - 1; neighborY <= gridY + 1; neighborY++)
            {
                if (neighborX >= 0 && neighborX < width && neighborY >= 0 && neighborY < height)
                {
                    if (neighborX != gridX || neighborY != gridY)
                    {
                        if (checkGrid[neighborX, neighborY] == 1) wallCount += 1;
                    }
                }
                else
                {
                    wallCount++;
                }
            }
        }
        return wallCount;
    }

    private void OnDestroy()
    {
        if (nativeGrid.IsCreated) nativeGrid.Dispose();
        if (nativeFlowField.IsCreated) nativeFlowField.Dispose();
    }

    public void UpdateGridTile(Vector3 worldPosition, int newTileType)
    {   
        int gridX = Mathf.RoundToInt(worldPosition.x) - worldOrigin.x;
        int gridY = Mathf.RoundToInt(worldPosition.y) - worldOrigin.y;

        if (gridX >= 0 && gridX < width && gridY >= 0 && gridY < height)
        {
            grid[gridX, gridY] = newTileType;
            nativeGrid[gridX + gridY * width] = newTileType;
        }
    }
}