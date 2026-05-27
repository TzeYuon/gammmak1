
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    public int width = 200;
    public int height = 200;

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

    void GenerateTerrain()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x < 10 && y < 10) 
                { 
                    grid[x, y] = 0;
                    nativeGrid[x + y * width] = 0;
                    continue;
                }

                float elevation = Mathf.PerlinNoise((x + randomOffset) * scale, (y + randomOffset) * scale);

                float biome = Mathf.PerlinNoise((x + randomOffset + 5000f) * scale, (y + randomOffset + 5000f) * scale);

                int tileType = 0;

                if (elevation > 0.70f)
                {
                    tileType = 1;
                }
                else
                {
                    if (biome > 0.75f) tileType = 3;
                    else if (biome < 0.25f) tileType = 2;
                }

                grid[x, y] = tileType;
                nativeGrid[x + y * width] = tileType;

                Vector3 spawnPos = new Vector3(x + worldOrigin.x, y + worldOrigin.y, 0);

                if (tileType == 1 && wallPrefab != null)
                    Instantiate(wallPrefab, spawnPos, Quaternion.identity, this.transform);
                else if (tileType == 2 && mudPrefab != null)
                    Instantiate(mudPrefab, spawnPos, Quaternion.identity, this.transform);
                else if (tileType == 3 && poisonPrefab != null)
                    Instantiate(poisonPrefab, spawnPos, Quaternion.identity, this.transform);
            }
        }
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