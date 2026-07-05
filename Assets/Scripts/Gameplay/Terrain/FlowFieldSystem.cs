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
    private int _genState; // 0=Idle, 1=Setup, 2=Dijkstra, 3=Vectors, 4=Apply

    
    private NativeArray<int> _integrationField;
    private NativeArray<byte> _costField;
    private NativeArray<float2> _backBufferFlowField;

    private NativeQueue<int2> _cellsToCheck;
    private NativeList<int2> _targetPositions;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
        state.RequireForUpdate<TerrainGridComponent>();
        state.RequireForUpdate<EnemyFlowFieldComponent>();

        _genState = 0;
        _timer = 0.45f;

        _cellsToCheck = new NativeQueue<int2>(Allocator.Persistent);
        _targetPositions = new NativeList<int2>(Allocator.Persistent);
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<TerrainGridComponent>(out var terrainGrid)) return;
        if (!SystemAPI.TryGetSingleton<EnemyFlowFieldComponent>(out var flowFieldComp)) return;

        int gridWidth = terrainGrid.GridWidth;
        int gridHeight = terrainGrid.GridHeight;
        int totalCells = gridWidth * gridHeight;

        if (!_integrationField.IsCreated || _integrationField.Length != totalCells)
        {
            if (_integrationField.IsCreated) _integrationField.Dispose();
            if (_costField.IsCreated) _costField.Dispose();
            if (_backBufferFlowField.IsCreated) _backBufferFlowField.Dispose();

            _integrationField = new NativeArray<int>(totalCells, Allocator.Persistent);
            _costField = new NativeArray<byte>(totalCells, Allocator.Persistent);
            _backBufferFlowField = new NativeArray<float2>(totalCells, Allocator.Persistent);
        }


        if (_genState == 0) // State 0: Idle
        {
            _timer -= SystemAPI.Time.DeltaTime;
            if (_timer > 0) return;

            _genState = 1; 
        }
        else if (_genState == 1) // State 1: Setup and Noise Generation
        {
            _targetPositions.Clear();
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                int2 gridPos = new int2(
                    (int)math.round(transform.ValueRO.Position.x),
                    (int)math.round(transform.ValueRO.Position.y)
                ) - terrainGrid.WorldOrigin;
                _targetPositions.Add(gridPos);
            }

            // Schedule the Parallel Noise generation
            var setupJob = new SetupAndNoiseJob
            {
                IntegrationField = _integrationField,
                FlowField = _backBufferFlowField,
                CostGrid = _costField,
                ObstacleGrid = terrainGrid.Grid,
                GridWidth = gridWidth,
                CellSize = 1,
                ElapsedTime = (float)SystemAPI.Time.ElapsedTime,
                NoiseFrequency = 0.05f,
                MaxNoiseCost = 10
            }.Schedule(totalCells, 2048, state.Dependency);

            // Schedule the job that puts the player ship into the Dijkstra Queue
            var initQueueJob = new InitializeDijkstraTargetsJob
            {
                Targets = _targetPositions.AsArray(),
                IntegrationField = _integrationField,
                CellsToCheck = _cellsToCheck.AsParallelWriter(),
                Grid = terrainGrid.Grid,
                GridWidth = gridWidth
            }.Schedule(setupJob);

            state.Dependency = initQueueJob;
            _genState = 2;
        }
        else if (_genState == 2) // State 2: Dijkstra in Chunks
        {
            // Wait for the previous jobs to finish before starting Dijkstra
            state.Dependency.Complete();

            if (_cellsToCheck.IsEmpty())
            {
                _genState = 3;
            }
            else
            {
                var dijkstraJob = new ChunkedDijkstraJob
                {
                    CellsToCheck = _cellsToCheck,
                    IntegrationField = _integrationField,
                    CostGrid = _costField,
                    Grid = terrainGrid.Grid,
                    GridWidth = gridWidth,
                    GridHeight = gridHeight,
                    MaxCellsPerFrame = 5000
                }.Schedule(state.Dependency);

                state.Dependency = dijkstraJob;
                
            }
        }
        else if (_genState == 3) // State 3: Vector Generation
        {
            var vectorJob = new GenerateVectorsJob
            {
                IntegrationField = _integrationField,
                Grid = terrainGrid.Grid,
                Targets = _targetPositions.AsArray(),
                FlowField = _backBufferFlowField,
                GridWidth = gridWidth,
                GridHeight = gridHeight
            }.Schedule(totalCells, 2048, state.Dependency);

            state.Dependency = vectorJob;
            _genState = 4;
        }
        else if (_genState == 4) // State 4: Apply to flow field and reset
        {
            state.Dependency.Complete();

            _backBufferFlowField.CopyTo(flowFieldComp.FlowField);

            _timer = 0.25f;
            _genState = 0; 
        }
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_integrationField.IsCreated) _integrationField.Dispose();
        if (_costField.IsCreated) _costField.Dispose();
        if (_backBufferFlowField.IsCreated) _backBufferFlowField.Dispose();
        if (_cellsToCheck.IsCreated) _cellsToCheck.Dispose();
        if (_targetPositions.IsCreated) _targetPositions.Dispose();
    }
}

// Burst Jobs

[BurstCompile]
public struct SetupAndNoiseJob : IJobParallelFor
{
    [WriteOnly] public NativeArray<int> IntegrationField;
    [WriteOnly] public NativeArray<float2> FlowField;
    [WriteOnly] public NativeArray<byte> CostGrid;
    [ReadOnly] public NativeArray<int> ObstacleGrid;
    public int GridWidth;
    public float CellSize;
    public float ElapsedTime;
    public float NoiseFrequency;
    public byte MaxNoiseCost;

    public void Execute(int index)
    {
        IntegrationField[index] = 9999999;
        FlowField[index] = float2.zero;

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
        CostGrid[index] = (byte)math.clamp(math.lerp(1, MaxNoiseCost, normalizedNoise), 1, 254);
    }
}

[BurstCompile]
public struct InitializeDijkstraTargetsJob : IJob
{
    [ReadOnly] public NativeArray<int2> Targets;
    public NativeArray<int> IntegrationField;
    public NativeQueue<int2>.ParallelWriter CellsToCheck;
    [ReadOnly] public NativeArray<int> Grid;
    public int GridWidth;

    public void Execute()
    {
        for (int i = 0; i < Targets.Length; i++)
        {
            int2 pos = Targets[i];
            if (Grid[pos.x + pos.y * GridWidth] != 1)
            {
                IntegrationField[pos.x + pos.y * GridWidth] = 0;
                CellsToCheck.Enqueue(pos);
            }
        }
    }
}

[BurstCompile]
public struct ChunkedDijkstraJob : IJob
{
    public NativeQueue<int2> CellsToCheck;
    public NativeArray<int> IntegrationField;
    [ReadOnly] public NativeArray<byte> CostGrid;
    [ReadOnly] public NativeArray<int> Grid;
    public int GridWidth;
    public int GridHeight;
    public int MaxCellsPerFrame;

    public void Execute()
    {
        NativeArray<int2> neighbors = new NativeArray<int2>(4, Allocator.Temp);
        neighbors[0] = new int2(0, 1); neighbors[1] = new int2(0, -1);
        neighbors[2] = new int2(-1, 0); neighbors[3] = new int2(1, 0);

        int cellsProcessed = 0;

        while (cellsProcessed < MaxCellsPerFrame && CellsToCheck.TryDequeue(out int2 current))
        {
            cellsProcessed++;

            for (int i = 0; i < 4; i++)
            {
                int2 neighbor = current + neighbors[i];

                if (neighbor.x >= 0 && neighbor.x < GridWidth && neighbor.y >= 0 && neighbor.y < GridHeight)
                {
                    int tileType = Grid[neighbor.x + neighbor.y * GridWidth];
                    if (tileType == 1) continue;

                    int cost = (tileType == 2) ? 5 : (tileType == 3) ? 25 : 1;
                    int current1D = current.x + current.y * GridWidth;
                    int neighbor1D = neighbor.x + neighbor.y * GridWidth;

                    int newCost = IntegrationField[current1D] + cost + CostGrid[neighbor1D];

                    if (newCost < IntegrationField[neighbor1D])
                    {
                        IntegrationField[neighbor1D] = newCost;
                        CellsToCheck.Enqueue(neighbor);
                    }
                }
            }
        }
        neighbors.Dispose();
    }
}

[BurstCompile]
public struct GenerateVectorsJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<int> IntegrationField;
    [ReadOnly] public NativeArray<int> Grid;
    [ReadOnly] public NativeArray<int2> Targets;
    public int GridWidth;
    public int GridHeight;

    [WriteOnly] public NativeArray<float2> FlowField;

    public void Execute(int idx)
    {
        if (Grid[idx] == 1) return;

        int x = idx % GridWidth;
        int y = idx / GridWidth;
        int bestCost = IntegrationField[idx];

        if (bestCost >= 9999999 && Targets.Length > 0)
        {
            int2 dir = Targets[0] - new int2(x, y);
            float2 fallbackDir = new float2(dir.x, dir.y);
            if (math.lengthsq(fallbackDir) > 0.1f) FlowField[idx] = math.normalize(fallbackDir);
            return;
        }

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
                    if (dx != 0 && dy != 0)
                    {
                        if (Grid[(x + dx) + y * GridWidth] != 0 || Grid[x + (y + dy) * GridWidth] != 0) continue;
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