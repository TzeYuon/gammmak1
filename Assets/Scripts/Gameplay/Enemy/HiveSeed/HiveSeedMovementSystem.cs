using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static HiveDirectorSystem;

[UpdateInGroup(typeof(ActionAndMovementGroup))]
partial struct HiveSeedMovementSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TerrainGridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (seed, transform, buffer, entity) in 
            SystemAPI.Query<
                RefRW<HiveSeedComponent>, 
                RefRW<LocalTransform>, 
                DynamicBuffer<CaravanWaypoint>>()
                .WithEntityAccess()
                .WithNone<DeadTag>())
        {
            float3 curPos = transform.ValueRO.Position;

            if (seed.ValueRO.HasArrived)
            {
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
                        EnemyType = EnemyType.Standard,
                        SpawnCount = 5,
                        Interval = 2.0f,
                        NextSpawnTime = 5.0f
                    });

                    ecb.SetComponent(newHive, new CollisionRadius { Value = 5.0f });
                    ecb.SetComponent(newHive, new HealthComponent { MaxValue = 500, Value = 500 });
                    ecb.SetComponentEnabled<DeadTag>(newHive, false);

                    ecb.DestroyEntity(entity);
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
                continue;
            }

            float3 direction = math.normalize(targetPos - curPos);
            transform.ValueRW.Position += math.float3(direction.xy * seed.ValueRO.Speed, 0) * deltaTime;
        }
    }
}