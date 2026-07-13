using Unity.Burst;
using Unity.Entities;

/// <summary>
/// Used for entities that should be destroyed when a level is exited
/// </summary>
public struct LevelEntityTag : IComponentData { }

[UpdateInGroup(typeof(InitializationSystemGroup))]
partial struct LevelPurgeSystem : ISystem
{
    private EntityQuery levelEntitiesQuery;
    private GameState previousState;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        levelEntitiesQuery = SystemAPI.QueryBuilder()
            .WithAll<LevelEntityTag>()
            .Build();

        state.RequireForUpdate<GameStateSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var currentState = SystemAPI.GetSingleton<GameStateSingleton>().CurrentState;
        if (currentState != previousState)
        {
            bool justExitedGameplay = previousState == GameState.Victory || previousState == GameState.Defeat || currentState == GameState.MainMenu;

            if (justExitedGameplay)
            {
                var entitiesToDestroy = levelEntitiesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                for (int i = 0; i < entitiesToDestroy.Length; i++)
                {
                    state.EntityManager.DestroyEntity(entitiesToDestroy[i]);
                }

                entitiesToDestroy.Dispose();
            }

            previousState = currentState;
        }
    }
}
