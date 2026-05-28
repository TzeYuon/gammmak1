using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial struct GameWinLossSystem : ISystem
{
    private EntityQuery livingHivesQuery;
    private EntityQuery livingMothershipQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.CreateSingleton(new LevelStateComponent
        {
            Result = LevelState.Playing,
            IsInitialized = false
        });

        livingHivesQuery = SystemAPI.QueryBuilder()
            .WithAll<HiveComponent>()
            .WithDisabled<DeadTag>()
            .Build();

        livingMothershipQuery = SystemAPI.QueryBuilder()
            .WithAll<MothershipTag>()
            .WithDisabled<DeadTag>()
            .Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var levelState = SystemAPI.GetSingletonRW<LevelStateComponent>();
        if (levelState.ValueRO.Result != LevelState.Playing) return;

        int mothershipCount = livingMothershipQuery.CalculateEntityCount();
        int hiveCount = livingHivesQuery.CalculateEntityCount();

        if (!levelState.ValueRO.IsInitialized)
        {
            if (mothershipCount > 0 && hiveCount > 0)
            {
                levelState.ValueRW.IsInitialized = true;
            }
            return;
        }

        if (mothershipCount == 0)
        {
            levelState.ValueRW.Result = LevelState.Defeat;
        }
        else if (hiveCount == 0)
        {
            levelState.ValueRW.Result = LevelState.Victory;
        }
    }
}