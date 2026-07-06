using Unity.Burst;
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
        state.RequireForUpdate<DifficultyState>();
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
        
        DifficultyState difficultyState = SystemAPI.GetSingleton<DifficultyState>();

        EnemyPrefabDatabase enemyPrefabDatabase = SystemAPI.GetSingleton<EnemyPrefabDatabase>();
        var rng = Random.CreateFromIndex((uint)SystemAPI.Time.ElapsedTime * 531);

        // Exceeded amount
        if (currentEnemyCount > GlobalSwarmCap)
        {
            // Skip spawning, and leave the timer at or near 0 so it tries again next frame.
            return;
        }

        // Trickle spawning behaviour
        foreach (var (hive, transform, spawnerBuffer, entity) in SystemAPI.Query<RefRW<HiveComponent>, RefRO<LocalTransform>, DynamicBuffer<TrickleSpawnerData>>().WithAll<ActiveHiveTag>().WithEntityAccess())
        {
            float3 hivePosition = transform.ValueRO.Position;
            float spawnRadius = hive.ValueRO.Size;
            int count = spawnerBuffer.Length;

            for (int i = 0; i < count; i++)
            {
                var spawner = spawnerBuffer[i];
                if (spawner.NextSpawnTime <= elapsedTime)
                {
                    spawner.NextSpawnTime = elapsedTime + (spawner.Interval / difficultyState.SpawnRateMult); // Reset timer

                    // spawn trickle amount
                    Entity prefab = enemyPrefabDatabase.GetPrefab(spawner.Type);
                    int spawnCount = (int)math.floor(spawner.SpawnCount * difficultyState.SpawnCountMult);
                    for (int b = 0; b < spawnCount; b++)
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

        // Surge behaviour
        if (!SystemAPI.TryGetSingleton(out GlobalSurgeState surgeState)) return;
        foreach (var (surge, hive, transform, entity) in SystemAPI.Query<RefRW<HiveSurgeComponent>, RefRO<HiveComponent>, RefRO<LocalTransform>>().WithAll<ActiveHiveTag>().WithEntityAccess())
        {
            // queue spawns for surge
            if (surgeState.TriggerSurgeThisFrame)
            {
                surge.ValueRW.SurgePendingSpawns += (int)math.floor(surge.ValueRO.SurgeSpawnCount * difficultyState.SpawnCountMult);
            }

            // spawn over multiple frames
            float3 hivePosition = transform.ValueRO.Position;
            float spawnRadius = hive.ValueRO.Size;
            if (surge.ValueRO.SurgePendingSpawns > 0)
            {
                surge.ValueRW.SurgeSpawnAccumulator += surge.ValueRO.SurgeSpawnRate * deltaTime;
                int spawnCount = (int)math.floor(surge.ValueRO.SurgeSpawnAccumulator);

                if (spawnCount > 0)
                {
                    int amountToSpawn = math.min(spawnCount, surge.ValueRO.SurgePendingSpawns);
                    surge.ValueRW.SurgeSpawnAccumulator -= amountToSpawn;

                    Entity prefab = enemyPrefabDatabase.GetPrefab(surge.ValueRO.SurgeEnemyType);
                    for (int i = 0; i < amountToSpawn; i++)
                    {
                        Entity spawnedEnemy = ecb.Instantiate(entity.Index, prefab);

                        float angle = rng.NextFloat(0, math.PI * 2);
                        float dist = math.sqrt(rng.NextFloat()) * spawnRadius;
                        float2 randomOffset = new float2(math.cos(angle), math.sin(angle)) * dist;
                        float3 spawnPos = hivePosition + math.float3(randomOffset, 0);

                        ecb.SetComponent(entity.Index, spawnedEnemy, LocalTransform.FromPosition(spawnPos));
                    }

                    surge.ValueRW.SurgePendingSpawns -= amountToSpawn;
                }
            }
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
