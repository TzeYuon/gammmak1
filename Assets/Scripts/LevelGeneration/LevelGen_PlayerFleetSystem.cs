using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup))]
partial struct LevelGen_PlayerFleetSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameStateSingleton>();
        state.RequireForUpdate<LevelGenerationStatus>();
        state.RequireForUpdate<PlayerEconomy>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<GameStateSingleton>(out var gameState) ||
            gameState.CurrentState != GameState.Generating) return;

        if (!SystemAPI.TryGetSingletonRW<LevelGenerationStatus>(out var status)) return;
        if (status.ValueRO.PlayerFleetSpawned) return; // player fleet already spawned in

        if (!SystemAPI.TryGetSingletonRW<PlayerEconomy>(out var playerEconomy)) return;

        if (!SystemAPI.TryGetSingleton<MothershipPrefabDatabase>(out var prefabDatabase)) return;

        // mark as complete
        status.ValueRW.PlayerFleetSpawned = true;

        playerEconomy.ValueRW.TotalBiomass = 15;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);

        // Generate mothership
        Entity mothershipEntity = ecb.Instantiate(prefabDatabase.MothershipPrefab);
        ecb.SetComponent(mothershipEntity, LocalTransform.FromPosition(new float3(1000, 1000, 0)));
    }
}
