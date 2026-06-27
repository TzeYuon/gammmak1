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
        state.RequireForUpdate<LevelSpawnData>();
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

        //var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
        //                   .CreateCommandBuffer(state.WorldUnmanaged);

        // Generate hives
        var hivePrefab = prefabDatabase.GetPrefab(HiveType.Standard);
        var spawnData = SystemAPI.GetSingleton<LevelSpawnData>();

        unsafe
        {
            for (int i = 0; i < spawnData.TotalHiveSpots; i++)
            {
                Entity hiveEntity = state.EntityManager.Instantiate(hivePrefab);

                float3 spawnPos = new float3(spawnData.HiveSpawnsX[i], spawnData.HiveSpawnsY[i], 0);
                state.EntityManager.SetComponentData(hiveEntity, LocalTransform.FromPosition(spawnPos));

                if (i < spawnData.ActiveHiveCount)
                {
                    state.EntityManager.SetComponentEnabled<ActiveHiveTag>(hiveEntity, true);
                }
                else
                {
                    state.EntityManager.SetComponentEnabled<ActiveHiveTag>(hiveEntity, false);
                }
            }
        }
    }
}
