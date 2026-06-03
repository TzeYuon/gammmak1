using Unity.Burst;
using Unity.Entities;

[BurstCompile]
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial class LevelWinLossSystem : SystemBase
{
    private EntityQuery livingHivesQuery;
    private EntityQuery livingMothershipQuery;

    private bool isInitialized;

    protected override void OnCreate()
    {
        isInitialized = false;

        livingHivesQuery = SystemAPI.QueryBuilder()
            .WithAll<HiveComponent>()
            .WithDisabled<DeadTag>()
            .Build();

        livingMothershipQuery = SystemAPI.QueryBuilder()
            .WithAll<MothershipTag>()
            .WithDisabled<DeadTag>()
            .Build();

        GameStateSystem.OnStateChanged += HandleStateChange;
    }

    void HandleStateChange(GameState prev, GameState curState)
    {
        if (curState == GameState.Gameplay) isInitialized = false;
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        var gameState = SystemAPI.GetSingletonRW<GameStateSingleton>();
        if (gameState.ValueRO.CurrentState != GameState.Gameplay) return;

        int mothershipCount = livingMothershipQuery.CalculateEntityCount();
        int hiveCount = livingHivesQuery.CalculateEntityCount();

        if (!isInitialized)
        {
            if (mothershipCount > 0 && hiveCount > 0)
            {
                isInitialized = true;
            }
            return;
        }

        if (mothershipCount == 0)
        {
            gameState.ValueRW.CurrentState = GameState.Defeat;
        }
        else if (hiveCount == 0)
        {
            gameState.ValueRW.CurrentState = GameState.Victory;
        }
    }
}