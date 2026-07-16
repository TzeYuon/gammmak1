using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(CommandAndSpawnGroup))]
[UpdateAfter(typeof(FleetSplitSystem))]
public partial struct FleetSelectAllSystem : ISystem
{
    private EntityQuery selectedQuery;
    private EntityQuery commandQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        selectedQuery = SystemAPI.QueryBuilder()
            .WithAll<SelectedTag, ShipIDComponent>()
            .WithNone<DeadTag>()
            .Build();

        commandQuery = SystemAPI.QueryBuilder()
            .WithAll<SelectAllCommand>()
            .Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gameState = SystemAPI.GetSingletonRW<GameStateSingleton>();
        if (gameState.ValueRO.CurrentState != GameState.Gameplay) return;

        var input = SystemAPI.GetSingleton<PlayerInputData>();
        if (input.WasPressed(PlayerAction.SelectAll) || !commandQuery.IsEmptyIgnoreFilter)
        {
            // delete if command exists
            if (!commandQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.DestroyEntity(commandQuery);
            }

            // select all ships
            state.Dependency = new SelectAllJob().ScheduleParallel(state.Dependency);
        }
    }
}

public struct SelectAllCommand : IComponentData { }

[BurstCompile]
[WithAll(typeof(PlayerTag))]
[WithDisabled(typeof(DeadTag))]
[WithAbsent(typeof(MothershipTag))]
[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
public partial struct SelectAllJob : IJobEntity
{
    private void Execute(
        EnabledRefRW<SelectedTag> selected
        )
    {
        selected.ValueRW = true;
    }
}