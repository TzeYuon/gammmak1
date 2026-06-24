using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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
        state.RequireForUpdate<LevelBlueprint>();
        state.RequireForUpdate<DifficultyConfig>();
        state.RequireForUpdate<DifficultyState>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<GameStateSingleton>(out var gameState) 
            || !SystemAPI.TryGetSingletonRW<LevelGenerationStatus>(out var status)
            || status.ValueRO.HivesGenerated
            || !SystemAPI.TryGetSingleton<LevelBlueprint>(out var levelBlueprint)
            || !SystemAPI.TryGetSingletonRW<DifficultyConfig>(out var difficultyConfig)
            || !SystemAPI.TryGetSingletonRW<DifficultyState>(out var difficultyState)
            || !SystemAPI.TryGetSingletonRW<GlobalSurgeState>(out var surgeState)
            || !SystemAPI.TryGetSingleton<HivePrefabDatabase>(out var prefabDatabase)
            || gameState.CurrentState != GameState.Generating) 
            return;

        // mark as complete
        status.ValueRW.HivesGenerated = true;

        // set difficulty config
        float threatLevelMult = levelBlueprint.ThreatLevel switch
        {
            ThreatLevel.Low => 0.7f,
            ThreatLevel.Medium => 0.9f,
            ThreatLevel.High => 1.5f,
            ThreatLevel.Boss => 2f,
            _ => 1.0f // Fallback
        };

        // Base values
        float baseDifficultyPerMinute = 0.05f;
        float baseDifficultyPerHiveDestroyed = 0.10f;
        float baseMaxSpawnRateMult = 3.0f;
        float baseMaxSpawnCountMult = 4.0f;
        float baseSurgeInterval = 30;

        // set config
        ref var config = ref difficultyConfig.ValueRW;
        config.DifficultyPerMinute = baseDifficultyPerMinute * threatLevelMult;
        config.DifficultyPerHiveDestroyed = baseDifficultyPerHiveDestroyed * threatLevelMult;
        config.MaxSpawnRateMult = baseMaxSpawnRateMult * threatLevelMult;
        config.MaxSpawnCountMult = baseMaxSpawnCountMult * threatLevelMult;

        // reset difficulty state
        ref var diffState = ref difficultyState.ValueRW;
        diffState.LevelTimer = 0;
        diffState.HivesDestroyed = 0;
        diffState.SpawnRateMult = 1;
        diffState.SpawnCountMult = 1;

        // reset surge state
        ref var sState = ref surgeState.ValueRW;
        sState.TriggerSurgeThisFrame = false;
        sState.SurgeTimer = baseSurgeInterval; // TEMPORARY: hardcode to 30s between surges
        sState.CurrentInterval = baseSurgeInterval;

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
