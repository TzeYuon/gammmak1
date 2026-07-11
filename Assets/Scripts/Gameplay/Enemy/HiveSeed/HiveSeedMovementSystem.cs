using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static HiveDirectorSystem;

[UpdateInGroup(typeof(ActionAndMovementGroup))]
partial struct HiveSeedMovementSystem : ISystem
{
    private ComponentLookup<LocalTransform> transformLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TerrainGridComponent>();
        transformLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        transformLookup.Update(ref state);

        foreach (var (seed, transform, velocity, health, explosion, buffer, entity) in SystemAPI.Query<RefRW<HiveSeedComponent>, RefRW<LocalTransform>, RefRW<SwarmVelocityComponent>, RefRO<HealthComponent>, RefRO<ExplosionOnDeath>, DynamicBuffer<CaravanWaypoint>>().WithEntityAccess())
        {
            float3 curPos = transform.ValueRO.Position;

            if (health.ValueRO.Value <= 0 || SystemAPI.IsComponentEnabled<DeadTag>(entity))
            {
                if (SystemAPI.HasSingleton<LevelSpawnData>())
                {
                    ref var spawnData = ref SystemAPI.GetSingletonRW<LevelSpawnData>().ValueRW;
                    unsafe
                    {
                        int newIndex = spawnData.ExpansionSpotCount;
                        if (newIndex < 8)
                        {
                            spawnData.ExpansionSpotsX[newIndex] = seed.ValueRO.Destination.x;
                            spawnData.ExpansionSpotsY[newIndex] = seed.ValueRO.Destination.y;
                            spawnData.ExpansionSpotCount++;
                        }
                    }
                }
                float explodeRadius = explosion.ValueRO.Radius;
                float explodeDamage = explosion.ValueRO.Damage;
                bool targetSwarm = explosion.ValueRO.TargetSwarm;

                foreach (var (targetTransform, targetBuffer, targetEntity) in SystemAPI.Query<RefRO<LocalTransform>, DynamicBuffer<DamageEvent>>().WithAll<HealthComponent>().WithEntityAccess())
                {
                    if (math.distance(targetTransform.ValueRO.Position, curPos) < explodeRadius)
                    {
                        bool isSwarm = SystemAPI.HasComponent<SwarmTag>(targetEntity);
                        if (!targetSwarm && isSwarm) continue;

                        targetBuffer.Add(new DamageEvent { Value = explodeDamage });
                    }
                }

                ecb.DestroyEntity(entity);
                continue;
            }

            if (seed.ValueRO.HasArrived)
            {
                velocity.ValueRW.Value = float2.zero;
                seed.ValueRW.IncubationTimer -= deltaTime;

                transform.ValueRW.Scale = math.lerp(seed.ValueRO.Scale, 1.0f, 1.0f - (seed.ValueRO.IncubationTimer / 30.0f));

                if (seed.ValueRO.IncubationTimer <= 0)
                {
                    var prefabs = SystemAPI.GetSingleton<HivePrefabDatabase>();
                    Entity newHive = ecb.Instantiate(prefabs.GetPrefab(HiveType.Standard));
                    ecb.SetComponent(newHive, LocalTransform.FromPosition(transform.ValueRO.Position));

                    ecb.AddComponent<ActiveHiveTag>(newHive);
                    ecb.SetComponentEnabled<ActiveHiveTag>(newHive, true);

                    if (SystemAPI.HasSingleton<LevelSpawnData>())
                    {
                        SystemAPI.GetSingletonRW<LevelSpawnData>().ValueRW.ActiveHiveCount++;
                    }
                    ecb.AddComponent(newHive, new HiveComponent
                    {
                        Size = 1.0f
                    });

                    ecb.SetComponent(newHive, new HiveSurgeComponent
                    {
                        SurgeEnemyType = EnemyType.Standard,
                        SurgeSpawnCount = 200,
                        SurgeSpawnRate = 100,
                        SurgeSpawnAccumulator = 0f,
                        SurgePendingSpawns = 0,
                        SurgeCountIgnoreScaling = false
                    });

                    ecb.AppendToBuffer(newHive, new TrickleSpawnerData
                    {
                        Type = EnemyType.Standard,
                        SpawnCount = 5,
                        Interval = 2.0f,
                        NextSpawnTime = 5.0f
                    });

                    ecb.SetComponent(newHive, new CollisionRadius { Value = 5.0f });
                    ecb.SetComponent(newHive, new HealthComponent { MaxValue = 500, Value = 500 });
                    ecb.SetComponentEnabled<DeadTag>(newHive, false);

                    ecb.DestroyEntity(entity);
                    //Debug.LogWarning("YOU FAILED! THE SWARM HAS MADE A NEW HIVE!");
                }
                continue;
            }

            float3 targetPos = seed.ValueRO.Destination;
            if (buffer.Length > 0)
            {
                targetPos = new float3(buffer[0].Value.x, buffer[0].Value.y, 0);
                if (math.distance(curPos, targetPos) < 1.0f)
                {
                    buffer.RemoveAt(0);
                    if (buffer.Length > 0) targetPos = new float3(buffer[0].Value.x, buffer[0].Value.y, 0);
                }
            }

            if (math.distance(curPos, targetPos) < 1.0f && buffer.Length == 0)
            {
                seed.ValueRW.HasArrived = true;
                //Debug.LogWarning("A NEW HIVE IS SPAWNING! YOU HAVE 30 SECONDS TO DESTROY IT!");
                continue;
            }

            float3 direction = math.normalize(targetPos - curPos);
            velocity.ValueRW.Value = direction.xy * seed.ValueRO.Speed;
            transform.ValueRW.Position += math.float3(velocity.ValueRW.Value, 0) * deltaTime;
        }
    }
}