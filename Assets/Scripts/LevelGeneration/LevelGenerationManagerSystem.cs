using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
partial struct LevelGenerationManagerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameStateSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gameStateRW = SystemAPI.GetSingletonRW<GameStateSingleton>();

        if (gameStateRW.ValueRO.CurrentState == GameState.Generating)
        {
            if (SystemAPI.TryGetSingleton<LevelGenerationStatus>(out var status))
            {
                // all generation steps are completed
                if (status.IsComplete())
                {
                    // destroy status then start gameplay state
                    var ecb = new EntityCommandBuffer(Allocator.Temp);
                    Entity statusEntity = SystemAPI.GetSingletonEntity<LevelGenerationStatus>();
                    Entity gameStateEntity = SystemAPI.GetSingletonEntity<GameStateSingleton>();
                    ecb.DestroyEntity(statusEntity);
                    ecb.SetComponent(gameStateEntity, new GameStateSingleton { CurrentState = GameState.Gameplay });
                    ecb.Playback(state.EntityManager);
                    ecb.Dispose();
                }
            }
        }
    }
}
