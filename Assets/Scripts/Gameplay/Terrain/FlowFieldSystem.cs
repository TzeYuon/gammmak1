using JetBrains.Annotations;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(EnvironmentUpdateGroup))]
partial struct FlowFieldSystem : ISystem
{
    private float _timer;
    private NativeArray<int> _integrationField;
    private NativeArray<byte> _costField;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
        state.RequireForUpdate<TerrainGridComponent>();
        state.RequireForUpdate<EnemyFlowFieldComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var gamestate = SystemAPI.GetSingleton<GameStateSingleton>();
        if (gamestate.CurrentState != GameState.Gameplay) return;

        _timer -= SystemAPI.Time.DeltaTime;
        if (_timer > 0) return;
        _timer = 0.45f;

        var terrainGridComponent = SystemAPI.GetSingleton<TerrainGridComponent>();
        int gridWidth = terrainGridComponent.GridWidth;
        int gridHeight = terrainGridComponent.GridHeight;
        int totalCells = gridWidth * gridHeight;

        var flowFieldComponent = SystemAPI.GetSingletonRW<EnemyFlowFieldComponent>();

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
            ObstacleGrid = terrainGridComponent.Grid,
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
                (int)math.round(transform.ValueRO.Position.x) - terrainGridComponent.WorldOrigin.x,
                (int)math.round(transform.ValueRO.Position.y) - terrainGridComponent.WorldOrigin.y
            );
            targetPositions.Add(gridPos);
        }

        var job = new GenerateFlowFieldJob
        {
            Grid = terrainGridComponent.Grid,
            IntegrationField = _integrationField,
            CostGrid = _costField,
            FlowField = flowFieldComponent.ValueRW.FlowField,
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

    private const int MAX_COST = 9999999;

    public void Execute()
    {
        for (int i = 0; i < IntegrationField.Length; i++)
        {
            IntegrationField[i] = MAX_COST;
            FlowField[i] = float2.zero;
        }

        NativeQueue<int2> cellsToCheck = new NativeQueue<int2>(Allocator.Temp);

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

        NativeArray<int2> neighbors = new NativeArray<int2>(8, Allocator.Temp);
        neighbors[0] = new int2(0, 1);
        neighbors[1] = new int2(0, -1);
        neighbors[2] = new int2(-1, 0);
        neighbors[3] = new int2(1, 0);
        neighbors[4] = new int2(-1, 1);
        neighbors[5] = new int2(1, 1);
        neighbors[6] = new int2(-1, -1);
        neighbors[7] = new int2(1, -1);

        while (cellsToCheck.TryDequeue(out int2 current))
        {
            for (int i = 0; i < 8; i++)
            {
                int2 neighbor = current + neighbors[i];

                if (neighbor.x >= 0 && neighbor.x < GridWidth && neighbor.y >= 0 && neighbor.y < GridHeight)
                {
                    int tileType = Grid[neighbor.x + neighbor.y * GridWidth];
                    if (tileType == 1) continue;

                    int baseCost = (tileType == 2) ? 250 : (tileType == 3) ? 1000 : 10;
                    int cost = (i < 4) ? baseCost : (int)(baseCost * 1.414f);

                    int current1D = current.x + current.y * GridWidth;
                    int neighbor1D = neighbor.x + neighbor.y * GridWidth;

                    int newCost = IntegrationField[current1D] + cost + CostGrid[neighbor1D];

                    if (newCost < IntegrationField[neighbor1D])
                    {
                        IntegrationField[neighbor1D] = newCost;
                        cellsToCheck.Enqueue(neighbor);
                    }
                }
            }
        }

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                int idx = x + y * GridWidth;

                if (Grid[idx] == 1)
                {
                    int minWallCost = MAX_COST;
                    float2 escapeDir = float2.zero;

                    for (int i = 0; i < 8; i++)
                    {
                        int nx = x + neighbors[i].x;
                        int ny = y + neighbors[i].y;
                        if (nx >= 0 && nx < GridWidth && ny >= 0 && ny < GridHeight)
                        {
                            if (Grid[nx + ny * GridWidth] != 1)
                            {
                                int nCost = IntegrationField[nx + ny * GridWidth];
                                if (nCost < minWallCost)
                                {
                                    minWallCost = nCost;
                                    escapeDir = new float2(neighbors[i].x, neighbors[i].y);
                                }
                            }
                        }
                    }

                    if (math.lengthsq(escapeDir) > 0.01f) FlowField[idx] = math.normalize(escapeDir);
                    else FlowField[idx] = float2.zero;

                    continue;
                }

                int currentCost = IntegrationField[idx];
                int minCost = currentCost;
                float2 bestDir = float2.zero;

                for (int i = 0; i < 8; i++)
                {
                    int nx = x + neighbors[i].x;
                    int ny = y + neighbors[i].y;

                    if (nx >= 0 && nx < GridWidth && ny >= 0 && ny < GridHeight)
                    {
                        if (Grid[nx + ny * GridWidth] != 1) // Ignore walls
                        {
                            int neighborCost = IntegrationField[nx + ny * GridWidth];

                            if (neighborCost < minCost)
                            {
                                minCost = neighborCost;
                                bestDir = new float2(neighbors[i].x, neighbors[i].y);
                            }
                        }
                    }
                }

                if (math.lengthsq(bestDir) > 0.01f)
                {
                    FlowField[idx] = math.normalize(bestDir);
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