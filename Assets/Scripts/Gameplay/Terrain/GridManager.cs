
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    public int Width = 200;
    public int Height = 200;
    public int safezoneRadius = 20;

    [SerializeField] private Vector2Int worldCentre = new Vector2Int(1000, 1000);
    public int2 WorldOrigin;

    // Noise settings
    [SerializeField] private float scale = 0.15f;
    [SerializeField] private float randomOffset = 0f;

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
        }
        else Destroy(gameObject);
    }

    void Start()
    {
        grid = new int[Width, Height];
        nativeGrid = new NativeArray<int>(Width * Height, Allocator.Persistent);
        nativeFlowField = new NativeArray<float2>(Width * Height, Allocator.Persistent);
        randomOffset = UnityEngine.Random.Range(0f, 9999f);

        WorldOrigin = new int2(worldCentre.x - Width / 2, worldCentre.y - Height / 2);

        GenerateTerrain();
    }

    bool IsInSafeZone(int x, int y)
    {
        bool corner =
       (x < safezoneRadius || x >= Width - safezoneRadius) &&
       (y < safezoneRadius || y >= Height - safezoneRadius);

        bool center =
            Mathf.Abs(x - Width / 2) <= safezoneRadius &&
            Mathf.Abs(y - Height / 2) <= safezoneRadius;

        return corner || center;
    }

    void GenerateTerrain()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
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

        // Cellular Automata
        for (int i = 0; i < 3; i++)
        {
            grid = SmoothAsteroidCaves(grid);
        }

        ConnectDisconnectedIslands();

        // Spawn mud and poison
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                int currentTile = grid[x, y];

                if (currentTile == 0 && !IsInSafeZone(x, y))
                {
                    float poisonNoise = Mathf.PerlinNoise((x + randomOffset + 5000f) * scale, (y + randomOffset + 5000f) * scale);
                    float mudNoise = Mathf.PerlinNoise((x + randomOffset + 2000f) * scale, (y + randomOffset + 2000f) * scale);

                    if (poisonNoise > 0.75f)
                        grid[x, y] = 3;
                    else if (mudNoise > 0.75f)
                        grid[x, y] = 2;
                }

                int finalTile = grid[x, y];
                nativeGrid[x + y * Width] = finalTile;

                Vector3 spawnPos = new Vector3(x + WorldOrigin.x, y + WorldOrigin.y, 0);

                if (finalTile == 1 && wallPrefab != null)
                    Instantiate(wallPrefab, spawnPos, Quaternion.identity, this.transform);
                else if (finalTile == 2 && mudPrefab != null)
                    Instantiate(mudPrefab, spawnPos, Quaternion.identity, this.transform);
                else if (finalTile == 3 && poisonPrefab != null)
                    Instantiate(poisonPrefab, spawnPos, Quaternion.identity, this.transform);
            }
        }
    }
    void ConnectDisconnectedIslands()
    {
        int[,] mapFlags = new int[Width, Height];
        List<List<Vector2Int>> allIslands = new List<List<Vector2Int>>();
        int biggestIslandIndex = 0;
        int maxTiles = 0;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (grid[x, y] == 0 && mapFlags[x, y] == 0)
                {
                    List<Vector2Int> newIsland = GetIsland(x, y, mapFlags);
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

        // Carve a tunnel from every small island to the main island
        for (int i = 0; i < allIslands.Count; i++)
        {
            if (i == biggestIslandIndex) continue;

            List<Vector2Int> smallIsland = allIslands[i];

            // Find the two closest tiles between the small island and the main island
            float bestDist = float.MaxValue;
            Vector2Int bestStart = Vector2Int.zero;
            Vector2Int bestEnd = Vector2Int.zero;

            foreach (Vector2Int tileA in smallIsland)
            {
                foreach (Vector2Int tileB in mainIsland)
                {
                    float dist = math.distancesq(new float2(tileA.x, tileA.y), new float2(tileB.x, tileB.y));
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestStart = tileA;
                        bestEnd = tileB;
                    }
                }
            }

            CarveTunnel(bestStart, bestEnd);
        }
    }

    // A simple function to smash walls in a straight line between two points
    void CarveTunnel(Vector2Int start, Vector2Int end)
    {
        int x = start.x;
        int y = start.y;
        int dx = Mathf.Abs(end.x - start.x);
        int dy = Mathf.Abs(end.y - start.y);
        int sx = start.x < end.x ? 1 : -1;
        int sy = start.y < end.y ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            // Make a 3x3 tunnel so fat ships can fit through
            for (int tx = -1; tx <= 1; tx++)
            {
                for (int ty = -1; ty <= 1; ty++)
                {
                    if (IsValid(x + tx, y + ty))
                    {
                        grid[x + tx, y + ty] = 0;
                    }
                }
            }

            if (x == end.x && y == end.y) break;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }
    List<Vector2Int> GetIsland(int startX, int startY, int[,] mapFlags)
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
                if (IsValid(n.x, n.y))
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
        int[,] newGrid = new int[Width, Height];

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
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

    bool IsValid(int x, int y)
    {
        return (x >= 0 && x < Width && y >= 0 && y < Height) ? true : false;
    }
    int GetSurroundingWallCount(int[,] checkGrid, int gridX, int gridY)
    {
        int wallCount = 0;
        for (int neighborX = gridX - 1; neighborX <= gridX + 1; neighborX++)
        {
            for (int neighborY = gridY - 1; neighborY <= gridY + 1; neighborY++)
            {
                if (neighborX >= 0 && neighborX < Width && neighborY >= 0 && neighborY < Height)
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
        int gridX = Mathf.RoundToInt(worldPosition.x) - WorldOrigin.x;
        int gridY = Mathf.RoundToInt(worldPosition.y) - WorldOrigin.y;

        if (IsValid(gridX, gridY))
        {
            grid[gridX, gridY] = newTileType;
            nativeGrid[gridX + gridY * Width] = newTileType;
        }
    }
}