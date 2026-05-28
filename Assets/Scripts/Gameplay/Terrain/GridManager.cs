
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
        // 1. Bottom Left Spawn Zone
        if (x < safezoneRadius && y < safezoneRadius) return true;

        // 2. Center Arena Zone
        if (x > (width / 2) - safezoneRadius && x < (width / 2) + safezoneRadius &&
            y > (height / 2) - safezoneRadius && y < (height / 2) + safezoneRadius)
        {
            return true;
        }

        return false;
    }

    void GenerateTerrain()
    {
        // =========================================================
        // PASS 1: THE PERLIN NOISE FOUNDATION
        // We use Perlin noise to create the "blobs" of the galaxy.
        // =========================================================
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
        for (int i = 0; i < 3; i++) // Run the carver 3 times to smooth the walls
        {
            grid = SmoothAsteroidCaves(grid);
        }

        // Spawn mud and poison
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int currentTile = grid[x, y];

                if (currentTile == 0 && !IsInSafeZone(x, y))
                {
                    int wallNeighbors = GetSurroundingWallCount(grid, x, y);

                    // If surrounded by walls (a narrow canyon), fill it with Nebula Gas (Mud - 2)!
                    if (wallNeighbors >= 3)
                    {
                        grid[x, y] = 2;
                    }
                    else
                    {
                        float biome = Mathf.PerlinNoise((x + randomOffset + 5000f) * scale, (y + randomOffset + 5000f) * scale);
                        if (biome > 0.75f) grid[x, y] = 3;
                    }
                }

                // Sync the final data to the Native Grid for ECS
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