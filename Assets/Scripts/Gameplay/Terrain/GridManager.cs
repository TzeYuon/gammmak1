
using Unity.Collections;
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
        randomOffset = Random.Range(0f, 9999f);
        GenerateTerrain();
    }

    void GenerateTerrain()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x < 3 && y < 3) 
                { 
                    grid[x, y] = 0;
                    nativeGrid[x + y * width] = 0;
                    continue;
                }
                
                float noise = Mathf.PerlinNoise((x + randomOffset) * scale, (y + randomOffset) * scale);
                int tileType = 0;
                if (noise > 0.75f) tileType = 1;
                else if (noise > 0.65f) tileType = 3;
                else if (noise < 0.25f) tileType = 2;

                grid[x, y] = tileType;
                nativeGrid[x + y * width] = tileType;
            }
        }
    }

    private void OnDestroy()
    {
        if (nativeGrid.IsCreated) nativeGrid.Dispose();
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
    private void OnDrawGizmos()
    {
        if (grid == null) return;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == 0) continue;

                if (grid[x, y] == 1) Gizmos.color = Color.black;
                else if (grid[x, y] == 2) Gizmos.color = new Color(0.5f, 0.25f, 0f);
                else if (grid[x, y] == 3) Gizmos.color = Color.green;

                Gizmos.DrawCube(new Vector3(x + worldOrigin.x, y + worldOrigin.y, -2f), Vector3.one * 0.9f);
            }
        }
    }
}