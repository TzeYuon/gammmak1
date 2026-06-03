using JetBrains.Annotations;
using System.Runtime.CompilerServices;
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
    private NativeArray<byte> _costField;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (GridManager.Instance == null || !GridManager.Instance.nativeGrid.IsCreated) return;

        _timer -= SystemAPI.Time.DeltaTime;
        if (_timer > 0) return;
        _timer = 0.45f;

        int gridWidth = GridManager.Instance.width;
        int gridHeight = GridManager.Instance.height;
        int totalCells = gridWidth * gridHeight;

        if (!_integrationField.IsCreated || _integrationField.Length != totalCells)
        {
            if (_integrationField.IsCreated) _integrationField.Dispose();
            _integrationField = new NativeArray<int>(totalCells, Allocator.Persistent);
        }

        if (!_costField.IsCreated || _costField.Length != totalCells)
        {
            if (_costField.IsCreated) _costField.Dispose();
            _costField = new NativeArray<byte>(totalCells, Allocator.Persistent);
        }

        var noiseFieldHandle = new GenerateNoiseFieldJob
        {
            CostGrid = _costField,
            ObstacleGrid = GridManager.Instance.nativeGrid,
            GridWidth = gridWidth,
            CellSize = 1,
            ElapsedTime = (float)SystemAPI.Time.ElapsedTime,
            NoiseFrequency = 0.05f,
            MaxNoiseCost = 10
        }.Schedule(totalCells, 4096, state.Dependency);

        NativeList<int2> targetPositions = new NativeList<int2>(Allocator.TempJob);
        foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
        {
            int2 gridPos = new int2(
                (int)math.round(transform.ValueRO.Position.x),
                (int)math.round(transform.ValueRO.Position.y)
            ) - new int2(GridManager.Instance.worldOrigin.x, GridManager.Instance.worldOrigin.y);

            targetPositions.Add(gridPos);
        }

        var job = new GenerateFlowFieldJob
        {
            Grid = GridManager.Instance.nativeGrid,
            IntegrationField = _integrationField,
            CostGrid = _costField,
            FlowField = GridManager.Instance.nativeFlowField,
            TargetPositions = targetPositions,
            GridWidth = gridWidth,
            GridHeight = gridHeight,
            ElapsedTime = (float)SystemAPI.Time.ElapsedTime,
        };

        state.Dependency = job.Schedule(noiseFieldHandle);

        targetPositions.Dispose(state.Dependency);
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_integrationField.IsCreated) _integrationField.Dispose();
        if (_costField.IsCreated) _costField.Dispose();
    }
}

[BurstCompile]
public struct GenerateNoiseFieldJob : IJobParallelFor
{
    [WriteOnly] public NativeArray<byte> CostGrid;

    [ReadOnly] public NativeArray<int> ObstacleGrid;

    public int GridWidth;
    public float CellSize;

    // Tuning Variables
    public float ElapsedTime;
    public float NoiseFrequency;
    public byte MaxNoiseCost;

    public void Execute(int index)
    {
        if (ObstacleGrid[index] > 0)
        {
            CostGrid[index] = 255;
            return;
        }

        int x = index % GridWidth;
        int y = index / GridWidth;

        float noiseX = x * CellSize * NoiseFrequency;
        float noiseY = y * CellSize * NoiseFrequency;

        float noiseZ = ElapsedTime * 0.2f;
        float rawNoise = noise.snoise(new float3(noiseX, noiseY, noiseZ));
        float normalizedNoise = (rawNoise + 1.0f) * 0.5f;
        byte finalCost = (byte)math.clamp(math.lerp(1, MaxNoiseCost, normalizedNoise), 1, 254);
        
        CostGrid[index] = finalCost;
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
    public NativeArray<byte> CostGrid;
    public NativeArray<float2> FlowField;

    public float ElapsedTime;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetSafeIntegrationValue(int checkX, int checkY)
    {
        // If the neighbor is off-map, return a massive distance value.
        // This acts as an invisible, steep wall keeping the swarm inside!
        if (checkX < 0 || checkX >= GridWidth || checkY < 0 || checkY >= GridHeight)
        {
            return 999999;
        }

        return IntegrationField[checkX + (checkY * GridWidth)];
    }

    public void Execute()
    {
        // Reset Arrays
        for (int i = 0; i < IntegrationField.Length; i++)
        {
            IntegrationField[i] = 99999;
            FlowField[i] = float2.zero;
        }

        NativeQueue<int2> cellsToCheck = new NativeQueue<int2>(Allocator.Temp);

        // multi-target start
        for (int i = 0; i < TargetPositions.Length; i++)
        {
            int2 pos = TargetPositions[i];
            if (pos.x >= 0 && pos.x < GridWidth && pos.y >= 0 && pos.y < GridHeight)
            {
                if (Grid[pos.x + pos.y * GridWidth] != 1)
                {
                    IntegrationField[pos.x + pos.y * GridWidth] = 0;
                    cellsToCheck.Enqueue(pos);
                }
            }
        }

        // Dijkstra Flood Fill
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

                    int cost = (tileType == 2) ? 5 : (tileType == 3) ? 25 : 1;
                    int current1D = current.x + current.y * GridWidth;
                    int neighbor1D = neighbor.x + neighbor.y * GridWidth;

                    int newCost = IntegrationField[current1D] + cost;

                    newCost += CostGrid[neighbor1D];

                    if (newCost < IntegrationField[neighbor1D])
                    {
                        IntegrationField[neighbor1D] = newCost;
                        cellsToCheck.Enqueue(neighbor);
                    }
                }
            }
        }

        // Generate Flow Field Vectors
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                int idx = x + y * GridWidth;
                if (Grid[idx] == 1) continue; // wall

                int valTL = GetSafeIntegrationValue(x - 1, y + 1); // Top-Left
                int valT = GetSafeIntegrationValue(x, y + 1); // Top
                int valTR = GetSafeIntegrationValue(x + 1, y + 1); // Top-Right

                int valL = GetSafeIntegrationValue(x - 1, y); // Left
                int valR = GetSafeIntegrationValue(x + 1, y); // Right

                int valBL = GetSafeIntegrationValue(x - 1, y - 1); // Bottom-Left
                int valB = GetSafeIntegrationValue(x, y - 1); // Bottom
                int valBR = GetSafeIntegrationValue(x + 1, y - 1); // Bottom-Right

                float dirX = (valTL + (2 * valL) + valBL) - (valTR + (2 * valR) + valBR);
                float dirY = (valBL + (2 * valB) + valBR) - (valTL + (2 * valT) + valTR);

                float2 flowDir = new(dirX, dirY);

                if (math.lengthsq(flowDir) > 0.001f)
                {
                    FlowField[idx] = math.normalize(flowDir);
                }
                else
                {
                    FlowField[idx] = float2.zero;
                }
            }
        }

        cellsToCheck.Dispose();
        neighbors.Dispose();
    }
}