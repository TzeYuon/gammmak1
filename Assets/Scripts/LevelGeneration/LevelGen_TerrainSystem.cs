using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitializationSystemGroup))]
partial struct LevelGen_TerrainSystem : ISystem
{
    private NativeArray<int> obstacleGrid;
    private NativeArray<float2> flowField;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameStateSingleton>();
        state.RequireForUpdate<LevelGenerationStatus>();

        state.EntityManager.CreateSingleton(new TerrainGridComponent());
        state.EntityManager.CreateSingleton(new EnemyFlowFieldComponent());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (obstacleGrid.IsCreated) obstacleGrid.Dispose();
        if (flowField.IsCreated) flowField.Dispose();
    }

    // [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<GameStateSingleton>(out var gameState) ||
            gameState.CurrentState != GameState.Generating) return;

        if (!SystemAPI.TryGetSingletonRW<LevelGenerationStatus>(out var status)) return;
        if (status.ValueRO.TerrainGenerated) return; // terrain already generated

        // TODO: GENERATE TERRAIN
    
        /// TEMPORARY: Get grid information from GridManager
        /// All use of GridManager.Instance should be replaced by whatever goes in this generation section

        // Update singleton to pass around grid information
        RefRW<TerrainGridComponent> terrainGridComponent = SystemAPI.GetSingletonRW<TerrainGridComponent>();
        RefRW<EnemyFlowFieldComponent> enemyFlowFieldComponent = SystemAPI.GetSingletonRW<EnemyFlowFieldComponent>();

        // Copy new sizes of the new level to the singleton
        terrainGridComponent.ValueRW.Grid = GridManager.Instance.nativeGrid;
        terrainGridComponent.ValueRW.WorldOrigin = GridManager.Instance.WorldOrigin;
        terrainGridComponent.ValueRW.GridWidth = GridManager.Instance.Width;
        terrainGridComponent.ValueRW.GridHeight = GridManager.Instance.Height;
        terrainGridComponent.ValueRW.CellSize = 1; // Hardcoded cell size of 1

        enemyFlowFieldComponent.ValueRW.FlowField = GridManager.Instance.nativeFlowField;
        enemyFlowFieldComponent.ValueRW.WorldOrigin = GridManager.Instance.WorldOrigin;
        enemyFlowFieldComponent.ValueRW.GridWidth = GridManager.Instance.Width;
        enemyFlowFieldComponent.ValueRW.GridHeight = GridManager.Instance.Height;
        enemyFlowFieldComponent.ValueRW.CellSize = 1; // Hardcoded cell size of 1

        // mark as complete
        status.ValueRW.TerrainGenerated = true;
    }
}
