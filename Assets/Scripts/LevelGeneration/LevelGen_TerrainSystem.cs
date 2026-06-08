using Unity.Burst;
using Unity.Entities;

partial struct LevelGen_TerrainSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameStateSingleton>();
        state.RequireForUpdate<LevelGenerationStatus>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<GameStateSingleton>(out var gameState) ||
            gameState.CurrentState != GameState.Generating) return;

        if (!SystemAPI.TryGetSingletonRW<LevelGenerationStatus>(out var status)) return;
        if (status.ValueRO.TerrainGenerated) return; // terrain already generated

        // TODO: GENERATE TERRAIN


        // mark as complete
        status.ValueRW.TerrainGenerated = true;
    }
}
