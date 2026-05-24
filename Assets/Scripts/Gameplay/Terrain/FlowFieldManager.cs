using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class FlowFieldManager : MonoBehaviour
{
    public static FlowFieldManager Instance { get; private set; }

    [ReadOnly] public NativeArray<float2> flowField;
    [ReadOnly] public NativeArray<int> Grid;
    private Vector2Int lastTargetPos = new Vector2Int(-9999, -9999);
    private int width, height;
    private int[,] integrationField; // Stores the cost to reach the destination

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    private void Start()
    {
        width = GridManager.Instance.width;
        height = GridManager.Instance.height;

        integrationField = new int[width, height];
        flowField = new NativeArray<float2>(width * height, Allocator.Persistent);
    }

    private void Update()
    {
        if (Unity.Entities.World.DefaultGameObjectInjectionWorld == null) return;

        var entityManager = Unity.Entities.World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = entityManager.CreateEntityQuery(typeof(PlayerInputData));

        if (query.HasSingleton<PlayerInputData>())
        {
            var input = query.GetSingleton<PlayerInputData>();

            float2 mousePos = input.MouseWorldPosition;
            Vector2Int gridPos = new Vector2Int(Mathf.RoundToInt(mousePos.x), Mathf.RoundToInt(mousePos.y));

            if (lastTargetPos.x != gridPos.x || lastTargetPos.y != gridPos.y)
            {
                GenerateField(gridPos);
                lastTargetPos = gridPos;
            }
        }
    }


    private void OnDestroy()
    {
        if (flowField.IsCreated) flowField.Dispose();
    }

    public void GenerateField(Vector2Int targetPos)
    {
        Vector2Int gridTarget = targetPos - GridManager.Instance.worldOrigin;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                integrationField[x, y] = 99999;
                flowField[x + y * width] = float2.zero;
            }
        }

        if (gridTarget.x < 0 || gridTarget.x >= width || gridTarget.y < 0 || gridTarget.y >= height) return;
        if (GridManager.Instance.grid[gridTarget.x, gridTarget.y] == 1) return;

        Queue<Vector2Int> cellsToCheck = new Queue<Vector2Int>();
        cellsToCheck.Enqueue(gridTarget);
        integrationField[gridTarget.x, gridTarget.y] = 0;

        Vector2Int[] neighbors = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (cellsToCheck.Count > 0)
        {
            Vector2Int current = cellsToCheck.Dequeue();

            foreach (Vector2Int dir in neighbors)
            {
                Vector2Int neighbor = current + dir;

                if (neighbor.x >= 0 && neighbor.x < width && neighbor.y >= 0 && neighbor.y < height)
                {
                    int tileType = GridManager.Instance.grid[neighbor.x, neighbor.y];
                    if (tileType == 1) continue; // Wall

                    int cost = 1;
                    if (tileType == 2) cost = 5;
                    if (tileType == 3) cost = 15;

                    int newCost = integrationField[current.x, current.y] + cost;

                    if (newCost < integrationField[neighbor.x, neighbor.y])
                    {
                        integrationField[neighbor.x, neighbor.y] = newCost;
                        cellsToCheck.Enqueue(neighbor);
                    }
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (GridManager.Instance.grid[x, y] == 1) continue;

                int bestCost = integrationField[x, y];
                Vector2Int bestDir = Vector2Int.zero;

                if (bestCost >= 99999)
                {
                    Vector2Int directLine = gridTarget - new Vector2Int(x, y);
                    float2 fallbackDir = math.normalize(new float2(directLine.x, directLine.y));
                    flowField[x + y * width] = fallbackDir;
                    continue;
                }

                // Look at all 8 neighbors
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                        {
                            if (dx != 0 && dy != 0)
                            {
                                if (GridManager.Instance.grid[x + dx, y] == 1 ||
                                    GridManager.Instance.grid[x, y + dy] == 1)
                                {
                                    continue;
                                }
                            }
                            if (integrationField[nx, ny] < bestCost)
                            {
                                bestCost = integrationField[nx, ny];
                                bestDir = new Vector2Int(dx, dy);
                            }
                        }
                    }
                }

                float2 direction = new float2(bestDir.x, bestDir.y);
                if (math.lengthsq(direction) > 0.1f) direction = math.normalize(direction);

                flowField[x + y * width] = direction;
            }
        }
    }
}