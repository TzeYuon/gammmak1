using System;
using System.Runtime.InteropServices;
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
            || !SystemAPI.TryGetSingleton<HivePrefabDatabase>(out var hivePrefabDatabase)
            || !SystemAPI.TryGetSingleton<EnemyPrefabDatabase>(out var enemyPrefabDatabase)
            || gameState.CurrentState != GameState.Generating) 
            return;

        // mark as complete
        status.ValueRW.HivesGenerated = true;

        // multiplier from levels completed
        float layerMult = 1f + (levelBlueprint.Layer * 0.15f);

        // set difficulty config
        float threatLevelMult = levelBlueprint.ThreatLevel switch
        {
            ThreatLevel.Low => 1f,
            ThreatLevel.Medium => 1.3f,
            ThreatLevel.High => 1.7f,
            ThreatLevel.Boss => 2.2f,
            _ => 1.0f // Fallback
        };

        // Base difficulty values
        float baseDifficultyPerMinute = 0.02f;
        float baseDifficultyPerHiveDestroyed = 0.05f;
        float baseMaxSpawnRateMult = 2f;
        float baseMaxSpawnCountMult = 2.5f;
        float baseMaxEnemyHealthMult = 1.5f;
        float baseSurgeInterval = 40; // TEMPORARY: hardcode to 30s between surges

        // set config
        ref var config = ref difficultyConfig.ValueRW;
        config.DifficultyPerMinute = baseDifficultyPerMinute * threatLevelMult * layerMult;
        config.DifficultyPerHiveDestroyed = baseDifficultyPerHiveDestroyed * threatLevelMult * layerMult;
        config.MaxSpawnRateMult = baseMaxSpawnRateMult * threatLevelMult * layerMult;
        config.MaxSpawnCountMult = baseMaxSpawnCountMult * threatLevelMult * layerMult;
        config.MaxEnemyHealthMult = baseMaxEnemyHealthMult * threatLevelMult * layerMult;

        // reset difficulty state
        ref var diffState = ref difficultyState.ValueRW;
        diffState.LevelTimer = 0;
        diffState.HivesDestroyed = 0;
        diffState.SpawnRateMult = 1;
        diffState.SpawnCountMult = 1;
        diffState.EnemyHealthMult = 1;
        
        // reset surge state
        ref var sState = ref surgeState.ValueRW;
        sState.TriggerSurgeThisFrame = false;
        sState.SurgeTimer = baseSurgeInterval * 2; // give more time before first surge
        sState.CurrentInterval = baseSurgeInterval;

        // create enemy blueprint singleton
        Entity blueprintDatabaseEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponent<LevelEntityTag>(blueprintDatabaseEntity);
        state.EntityManager.AddComponent<EnemyBlueprintDatabase>(blueprintDatabaseEntity);

        var blueprintBuffer = state.EntityManager.AddBuffer<EnemyBlueprint>(blueprintDatabaseEntity);
        var baseStatsLookup = SystemAPI.GetComponentLookup<EnemyBaseStats>(true);

        // Generate enemy blueprints
        for (int i = 0; i < (int)EnemyType.Count; i++)
        {
            EnemyType type = (EnemyType)i; // cast int to enum
         
            Entity prefab = enemyPrefabDatabase.GetPrefab(type);
            if (prefab == Entity.Null)
            {
                blueprintBuffer.Add(new EnemyBlueprint { Type = type });
                continue;
            }

            var baseStats = baseStatsLookup[prefab];
            blueprintBuffer.Add(new EnemyBlueprint
            {
                Type = type,
                Prefab = prefab,
                BaseHealthComponent = baseStats.BaseHealthComponent,
                ScaledHealthComponent = baseStats.BaseHealthComponent,
            });
        }

        // Generate hives
        var hivePrefab = hivePrefabDatabase.GetPrefab(HiveType.Standard);
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
            for (int i = 0; i < spawnData.ActiveHiveCount; i++)
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
                state.EntityManager.AddComponent<ActiveHiveTag>(hiveEntity);
                state.EntityManager.SetComponentEnabled<ActiveHiveTag>(hiveEntity, true);
            }

            hivePool.Dispose();
        }
    }
}
