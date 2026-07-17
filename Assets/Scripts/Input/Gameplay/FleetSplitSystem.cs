using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(CommandAndSpawnGroup))]
[UpdateAfter(typeof(FleetCommandSystem))]
public partial struct FleetSplitSystem : ISystem
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
            .WithAll<SplitCommand>()
            .Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gameState = SystemAPI.GetSingletonRW<GameStateSingleton>();
        if (gameState.ValueRO.CurrentState != GameState.Gameplay) return;

        var input = SystemAPI.GetSingleton<PlayerInputData>();
        if (input.WasPressed(PlayerAction.SplitSelection) || !commandQuery.IsEmptyIgnoreFilter)
        {
            // delete if command exists
            if (!commandQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.DestroyEntity(commandQuery);
            }

            // get all selected ships
            var selectedEntities = selectedQuery.ToEntityArray(Allocator.Temp);
            var shipIDs = selectedQuery.ToComponentDataArray<ShipIDComponent>(Allocator.Temp);

            // do not split if only one ship
            if (selectedEntities.Length <= 1) return;
            
            // keep every odd ship and deselect even ships by id
            var typeCounters = new NativeHashMap<uint, int>(16, Allocator.Temp);
            for (int i = 0; i < selectedEntities.Length; i++)
            {
                uint shipID = (uint)shipIDs[i].ShipID;

                if (!typeCounters.TryGetValue(shipID, out int count))
                {
                    count = 0;
                }

                count++;
                typeCounters[shipID] = count;

                if (count % 2 == 0)
                {
                    SystemAPI.SetComponentEnabled<SelectedTag>(selectedEntities[i], false);
                }
            }

            selectedEntities.Dispose();
            shipIDs.Dispose();
            typeCounters.Dispose();
        }
    }
}

public struct SplitCommand : IComponentData { }