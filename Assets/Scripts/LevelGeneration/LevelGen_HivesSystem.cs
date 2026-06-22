using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup))]
partial struct LevelGen_HivesSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameStateSingleton>();
        state.RequireForUpdate<LevelGenerationStatus>();
        state.RequireForUpdate<HivePrefabDatabase>();
        state.RequireForUpdate<GlobalSurgeState>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<GameStateSingleton>(out var gameState) ||
            gameState.CurrentState != GameState.Generating) return;

        if (!SystemAPI.TryGetSingletonRW<LevelGenerationStatus>(out var status)) return;
        if (status.ValueRO.HivesGenerated) return; // hives already generated

        if (!SystemAPI.TryGetSingletonRW<LevelBlueprint>(out var levelBlueprint)) return;
        if (!SystemAPI.TryGetSingletonRW<GlobalSurgeState>(out var surgeState)) return;
        if (!SystemAPI.TryGetSingleton<HivePrefabDatabase>(out var prefabDatabase)) return;

        // mark as complete
        status.ValueRW.HivesGenerated = true;

        // reset surge state
        surgeState.ValueRW.LevelTimer = 0;
        surgeState.ValueRW.TriggerSurgeThisFrame = false;
        surgeState.ValueRW.SpawnCountMultiplier = 1;
        surgeState.ValueRW.SurgeTimer = 30; // TEMPORARY: hardcode to 30s for first surge
        surgeState.ValueRW.CurrentInterval = 30;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);

        // Generate hives
        // TEMPORARY: spawns one in each corner
        var hivePrefab = prefabDatabase.GetPrefab(HiveType.Standard);
        Entity hiveEntity1 = ecb.Instantiate(hivePrefab);
        Entity hiveEntity2 = ecb.Instantiate(hivePrefab);
        Entity hiveEntity3 = ecb.Instantiate(hivePrefab);
        Entity hiveEntity4 = ecb.Instantiate(hivePrefab);

        //ecb.SetComponent(hiveEntity1, LocalTransform.FromPosition(new float3(1000, 1010, 0)));
        ecb.SetComponent(hiveEntity1, LocalTransform.FromPosition(new float3(910, 910, 0)));
        ecb.SetComponent(hiveEntity2, LocalTransform.FromPosition(new float3(1090, 1090, 0)));
        ecb.SetComponent(hiveEntity3, LocalTransform.FromPosition(new float3(1090, 910, 0)));
        ecb.SetComponent(hiveEntity4, LocalTransform.FromPosition(new float3(910, 1090, 0)));
    }
}
