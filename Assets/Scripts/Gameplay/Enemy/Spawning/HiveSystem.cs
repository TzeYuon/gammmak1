using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(CommandAndSpawnGroup))]
partial struct HiveSystem : ISystem
{
    private EntityQuery swarmQuery;
    private const int GlobalSwarmCap = 50000;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<HiveComponent>();
        state.RequireForUpdate<EnemyPrefabDatabase>();
        state.RequireForUpdate<GlobalSurgeState>();
        swarmQuery = SystemAPI.QueryBuilder()
            .WithAll<SwarmTag>()
            .Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        int currentEnemyCount = swarmQuery.CalculateEntityCount();

        GlobalSurgeState surgeState = SystemAPI.GetSingleton<GlobalSurgeState>();
        
        EnemyPrefabDatabase enemyPrefabDatabase = SystemAPI.GetSingleton<EnemyPrefabDatabase>();
        var rng = Random.CreateFromIndex((uint)SystemAPI.Time.ElapsedTime * 531);

        // Exceeded amount
        if (currentEnemyCount > GlobalSwarmCap)
        {
            // Skip spawning, and leave the timer at or near 0 so it tries again next frame.
            return;
        }

        foreach (var (hive, transform, spawnerBuffer, entity) in SystemAPI.Query<RefRW<HiveComponent>, RefRO<LocalTransform>, DynamicBuffer<TrickleSpawnerData>>().WithEntityAccess())
        {
            float3 hivePosition = transform.ValueRO.Position;
            float spawnRadius = hive.ValueRO.Size;
            int count = spawnerBuffer.Length;

            for (int i = 0; i < count; i++)
            {
                var spawner = spawnerBuffer[i];
                if (spawner.NextSpawnTime <= elapsedTime)
                {
                    spawner.NextSpawnTime = elapsedTime + spawner.Interval; // Reset timer

                    // spawn trickle amount
                    Entity prefab = enemyPrefabDatabase.GetPrefab(spawner.Type);
                    for (int b = 0; b < spawner.SpawnCount; b++)
                    {
                        Entity spawnedEnemy = ecb.Instantiate(entity.Index, prefab);

                        float angle = rng.NextFloat(0, math.PI * 2);
                        float dist = math.sqrt(rng.NextFloat()) * spawnRadius;
                        float2 randomOffset = new float2(math.cos(angle), math.sin(angle)) * dist;
                        float3 spawnPos = hivePosition + math.float3(randomOffset, 0);

                        ecb.SetComponent(entity.Index, spawnedEnemy, LocalTransform.FromPosition(spawnPos));
                    }

                    spawnerBuffer.RemoveAt(i);
                    i--;

                    spawnerBuffer.Add(spawner);
                }
            }
        }

        foreach (var (hive, transform, entity) in SystemAPI.Query<RefRW<HiveComponent>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            // queue spawns for surge
            if (surgeState.TriggerSurgeThisFrame)
            {
                hive.ValueRW.SurgePendingSpawns += (int)math.floor(hive.ValueRO.SurgeSpawnCount * surgeState.SpawnCountMultiplier);
            }

            // spawn over multiple frames
            float3 hivePosition = transform.ValueRO.Position;
            float spawnRadius = hive.ValueRO.Size;
            if (hive.ValueRO.SurgePendingSpawns > 0)
            {
                hive.ValueRW.SurgeSpawnAccumulator += hive.ValueRO.SurgeSpawnRate * deltaTime;
                int spawnCount = (int)math.floor(hive.ValueRO.SurgeSpawnAccumulator);

                if (spawnCount > 0)
                {
                    int amountToSpawn = math.min(spawnCount, hive.ValueRO.SurgePendingSpawns);
                    hive.ValueRW.SurgeSpawnAccumulator -= amountToSpawn;

                    Entity prefab = enemyPrefabDatabase.GetPrefab(hive.ValueRO.SurgeEnemyType);
                    for (int i = 0; i < amountToSpawn; i++)
                    {
                        Entity spawnedEnemy = ecb.Instantiate(entity.Index, prefab);

                        float angle = rng.NextFloat(0, math.PI * 2);
                        float dist = math.sqrt(rng.NextFloat()) * spawnRadius;
                        float2 randomOffset = new float2(math.cos(angle), math.sin(angle)) * dist;
                        float3 spawnPos = hivePosition + math.float3(randomOffset, 0);

                        ecb.SetComponent(entity.Index, spawnedEnemy, LocalTransform.FromPosition(spawnPos));
                    }

                    hive.ValueRW.SurgePendingSpawns -= amountToSpawn;
                }
            }
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
