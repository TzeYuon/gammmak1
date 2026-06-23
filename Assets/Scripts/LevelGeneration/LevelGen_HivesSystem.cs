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
        state.RequireForUpdate<LevelSpawnData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<GameStateSingleton>(out var gameState) ||
            gameState.CurrentState != GameState.Generating) return;

        if (!SystemAPI.TryGetSingletonRW<LevelGenerationStatus>(out var status)) return;
        if (status.ValueRO.HivesGenerated) return; // hives already generated

        if (!SystemAPI.TryGetSingletonRW<LevelBlueprint>(out var levelBlueprint)) return;

        if (!SystemAPI.TryGetSingleton<HivePrefabDatabase>(out var prefabDatabase)) return;
        var spawnData = SystemAPI.GetSingleton<LevelSpawnData>();

        // mark as complete
        status.ValueRW.HivesGenerated = true;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);

        // Generate hives
        var hivePrefab = prefabDatabase.GetPrefab(HiveType.Standard);
        Entity hiveEntity1 = ecb.Instantiate(hivePrefab);
        Entity hiveEntity2 = ecb.Instantiate(hivePrefab);
        Entity hiveEntity3 = ecb.Instantiate(hivePrefab);
        Entity hiveEntity4 = ecb.Instantiate(hivePrefab);
        Entity hiveEntity5 = ecb.Instantiate(hivePrefab);
        Entity hiveEntity6 = ecb.Instantiate(hivePrefab);
        Entity hiveEntity7 = ecb.Instantiate(hivePrefab);
        Entity hiveEntity8 = ecb.Instantiate(hivePrefab);

        ecb.SetComponent(hiveEntity1, LocalTransform.FromPosition(spawnData.Hive1Spawn));
        ecb.SetComponent(hiveEntity2, LocalTransform.FromPosition(spawnData.Hive2Spawn));
        ecb.SetComponent(hiveEntity3, LocalTransform.FromPosition(spawnData.Hive3Spawn));
        ecb.SetComponent(hiveEntity4, LocalTransform.FromPosition(spawnData.Hive4Spawn));
        ecb.SetComponent(hiveEntity5, LocalTransform.FromPosition(spawnData.Hive5Spawn));
        ecb.SetComponent(hiveEntity6, LocalTransform.FromPosition(spawnData.Hive6Spawn));
        ecb.SetComponent(hiveEntity7, LocalTransform.FromPosition(spawnData.Hive7Spawn));
        ecb.SetComponent(hiveEntity8, LocalTransform.FromPosition(spawnData.Hive8Spawn));
    }
}
