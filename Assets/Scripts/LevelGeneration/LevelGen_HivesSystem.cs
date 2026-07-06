using Unity.Burst;
using Unity.Collections;
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
            || !SystemAPI.TryGetSingletonEntity<LevelGenerationStatus>(out var statusEntity)
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
        float baseSurgeInterval = 30; // TEMPORARY: hardcode to 30s between surges

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
        sState.SurgeTimer = baseSurgeInterval; 
        sState.CurrentInterval = baseSurgeInterval;


        // Generate hives
        var hivePrefab = prefabDatabase.GetPrefab(HiveType.Standard);
        var spawnData = SystemAPI.GetSingleton<LevelSpawnData>();

        unsafe
        {
            // Create hive blueprint array for indexing
            var query = SystemAPI.QueryBuilder().WithAll<HiveBlueprint>().Build();
            int blueprintCount = query.CalculateEntityCount();

            NativeArray<HiveBlueprint> hivePool = new NativeArray<HiveBlueprint>(blueprintCount, Allocator.Temp);
            int index = 0;
            foreach (var hive in SystemAPI.Query<RefRO<HiveBlueprint>>())
            {
                hivePool[index] = hive.ValueRO;
                index++;
            }

            var rng = new Unity.Mathematics.Random(levelBlueprint.Seed);

            // Spawn hives at given spots
            for (int i = 0; i < spawnData.TotalHiveSpots; i++)
            {
                Entity hiveEntity = state.EntityManager.Instantiate(hivePrefab);

                float3 spawnPos = new float3(spawnData.HiveSpawnsX[i], spawnData.HiveSpawnsY[i], 0);
                state.EntityManager.SetComponentData(hiveEntity, LocalTransform.FromPosition(spawnPos));

                state.EntityManager.AddComponent<LevelEntityTag>(hiveEntity);

                if (i < spawnData.ActiveHiveCount)
                {
                    state.EntityManager.SetComponentEnabled<ActiveHiveTag>(hiveEntity, true);

                    if (hivePool.Length > 0)
                    {
                        float roll = rng.NextFloat(0f, levelBlueprint.TotalHiveWeight);
                        float cumulativeWeight = 0f;
                        HiveBlueprint selectedHive = hivePool[0];

                        for (int p = 0; p < hivePool.Length; p++)
                        {
                            HiveBlueprint h = hivePool[p];
                            cumulativeWeight += h.Weight;

                            if (roll <= cumulativeWeight)
                            {
                                selectedHive = h;
                                break;
                            }
                        }

                        state.EntityManager.SetComponentData(hiveEntity, selectedHive.HiveSurgeComponent);

                        // Copy trickle spawn data to native array
                        var recipeTrickles = state.EntityManager.GetBuffer<TrickleSpawnerData>(selectedHive.RecipeEntity);
                        var cachedTrickles = recipeTrickles.ToNativeArray(Allocator.Temp);

                        // Create trickle spawn data array on actual hive entity
                        var trickleBuffer = state.EntityManager.AddBuffer<TrickleSpawnerData>(hiveEntity);
                        for (int t = 0; t < cachedTrickles.Length; t++)
                        {
                            var trickleSpawn = cachedTrickles[t];
                            trickleBuffer.Add(trickleSpawn);
                        }

                        cachedTrickles.Dispose();
                    }
                }
                else
                {
                    state.EntityManager.SetComponentEnabled<ActiveHiveTag>(hiveEntity, false);
                }
            }

            hivePool.Dispose();
        }
    }
}
