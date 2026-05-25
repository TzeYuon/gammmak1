using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

partial struct FlowFieldSystem : ISystem
{
    private float _timer;
    private NativeArray<int> _integrationField;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (GridManager.Instance == null || !GridManager.Instance.nativeGrid.IsCreated) return;

        // Throttle updates to 4 times a second so the game doesn't lag
        _timer -= SystemAPI.Time.DeltaTime;
        if (_timer > 0) return;
        _timer = 0.25f;

        int gridWidth = GridManager.Instance.width;
        int gridHeight = GridManager.Instance.height;
        int totalCells = gridWidth * gridHeight;

        // Allocate the Integration Field memory once
        if (!_integrationField.IsCreated || _integrationField.Length != totalCells)
        {
            if (_integrationField.IsCreated) _integrationField.Dispose();
            _integrationField = new NativeArray<int>(totalCells, Allocator.Persistent);
        }

        // 1. Gather ALL Player Ship positions
        NativeList<int2> targetPositions = new NativeList<int2>(Allocator.TempJob);
        foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
        {
            int2 gridPos = new int2(
                (int)math.round(transform.ValueRO.Position.x),
                (int)math.round(transform.ValueRO.Position.y)
            ) - new int2(GridManager.Instance.worldOrigin.x, GridManager.Instance.worldOrigin.y);

            targetPositions.Add(gridPos);
        }

        // 2. Schedule the Burst Dijkstra Job
        var job = new GenerateFlowFieldJob
        {
            Grid = GridManager.Instance.nativeGrid,
            IntegrationField = _integrationField,
            FlowField = GridManager.Instance.nativeFlowField,
            TargetPositions = targetPositions, // Pass all ships in!
            GridWidth = gridWidth,
            GridHeight = gridHeight
        };

        // Notice we use Schedule() instead of ScheduleParallel() because Dijkstra must run sequentially
        state.Dependency = job.Schedule(state.Dependency);

        // Dispose the temporary list after the job finishes
        targetPositions.Dispose(state.Dependency);
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_integrationField.IsCreated) _integrationField.Dispose();
    }
}

[BurstCompile]
public struct GenerateFlowFieldJob : IJob
{
    [ReadOnly] public NativeArray<int> Grid;
    [ReadOnly] public NativeList<int2> TargetPositions;
    public int GridWidth;
    public int GridHeight;

    public NativeArray<int> IntegrationField;
    public NativeArray<float2> FlowField;

    public void Execute()
    {
        // 1. Reset Arrays
        for (int i = 0; i < IntegrationField.Length; i++)
        {
            IntegrationField[i] = 99999;
            FlowField[i] = float2.zero;
        }

        NativeQueue<int2> cellsToCheck = new NativeQueue<int2>(Allocator.Temp);

        // 2. MULTI-TARGET START: Drop all "pebbles" into the pond at once!
        for (int i = 0; i < TargetPositions.Length; i++)
        {
            int2 pos = TargetPositions[i];
            if (pos.x >= 0 && pos.x < GridWidth && pos.y >= 0 && pos.y < GridHeight)
            {
                if (Grid[pos.x + pos.y * GridWidth] != 1) // If ship isn't inside a wall
                {
                    IntegrationField[pos.x + pos.y * GridWidth] = 0;
                    cellsToCheck.Enqueue(pos);
                }
            }
        }

        // 3. Dijkstra Flood Fill
        NativeArray<int2> neighbors = new NativeArray<int2>(4, Allocator.Temp);
        neighbors[0] = new int2(0, 1);
        neighbors[1] = new int2(0, -1);
        neighbors[2] = new int2(-1, 0);
        neighbors[3] = new int2(1, 0);

        while (cellsToCheck.TryDequeue(out int2 current))
        {
            for (int i = 0; i < 4; i++)
            {
                int2 neighbor = current + neighbors[i];

                if (neighbor.x >= 0 && neighbor.x < GridWidth && neighbor.y >= 0 && neighbor.y < GridHeight)
                {
                    int tileType = Grid[neighbor.x + neighbor.y * GridWidth];
                    if (tileType == 1) continue; // Wall

                    int cost = (tileType == 2) ? 5 : (tileType == 3) ? 15 : 1;
                    int current1D = current.x + current.y * GridWidth;
                    int neighbor1D = neighbor.x + neighbor.y * GridWidth;

                    int newCost = IntegrationField[current1D] + cost;

                    if (newCost < IntegrationField[neighbor1D])
                    {
                        IntegrationField[neighbor1D] = newCost;
                        cellsToCheck.Enqueue(neighbor);
                    }
                }
            }
        }

        // 4. Generate Flow Field Vectors (8-way check)
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                int idx = x + y * GridWidth;
                if (Grid[idx] == 1) continue;

                int bestCost = IntegrationField[idx];
                int2 bestDir = int2.zero;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx >= 0 && nx < GridWidth && ny >= 0 && ny < GridHeight)
                        {
                            // Prevent corner cutting through walls
                            if (dx != 0 && dy != 0)
                            {
                                if (Grid[(x + dx) + y * GridWidth] == 1 || Grid[x + (y + dy) * GridWidth] == 1) continue;
                            }

                            int nIdx = nx + ny * GridWidth;
                            if (IntegrationField[nIdx] < bestCost)
                            {
                                bestCost = IntegrationField[nIdx];
                                bestDir = new int2(dx, dy);
                            }
                        }
                    }
                }

                float2 direction = new float2(bestDir.x, bestDir.y);
                if (math.lengthsq(direction) > 0.1f) direction = math.normalize(direction);

                FlowField[idx] = direction;
            }
        }

        cellsToCheck.Dispose();
        neighbors.Dispose();
    }
}