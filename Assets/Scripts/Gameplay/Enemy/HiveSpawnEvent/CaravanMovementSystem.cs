using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static HiveDirectorSystem;

[UpdateInGroup(typeof(ActionAndMovementGroup))]
partial struct CaravanMovementSystem : ISystem
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
        var terrainGrid = SystemAPI.GetSingleton<TerrainGridComponent>();

        transformLookup.Update(ref state);

        foreach (var (queen, transform, velocity, entity) in SystemAPI.Query<RefRW<CaravanQueenComponent>, RefRW<LocalTransform>, RefRW<SwarmVelocityComponent>>().WithEntityAccess())
        {
            if (queen.ValueRO.HasArrived)
            {
                velocity.ValueRW.Value = float2.zero;
                queen.ValueRW.IncubationTimer -= deltaTime;

                if (queen.ValueRO.IncubationTimer <= 0)
                {
                    var prefabs = SystemAPI.GetSingleton<HivePrefabDatabase>();
                    Entity newHive = ecb.Instantiate(prefabs.GetPrefab(HiveType.Standard));
                    ecb.SetComponent(newHive, LocalTransform.FromPosition(transform.ValueRO.Position));

                    ecb.SetComponentEnabled<ActiveHiveTag>(newHive, true);
                    if (SystemAPI.HasSingleton<LevelSpawnData>())
                    {
                        SystemAPI.GetSingletonRW<LevelSpawnData>().ValueRW.ActiveHiveCount++;
                    }
                    ecb.AddComponent(newHive, new HiveComponent
                    {
                        Size = 5.0f
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

                    var ambientBuffer = ecb.AddBuffer<TrickleSpawnerData>(newHive);
                    ambientBuffer.Clear();
                    ambientBuffer.Add(new TrickleSpawnerData
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
                    Debug.LogWarning("YOU FAILED! THE SWARM HAS MADE A NEW HIVE!");
                }
                continue;
            }

            float3 curPos = transform.ValueRO.Position;
            float3 targetPos = queen.ValueRO.Destination;

            if (math.distance(curPos, targetPos) < 1.0f)
            {
                queen.ValueRW.HasArrived = true;
                Debug.LogWarning("A NEW HIVE IS SPAWNING! YOU HAVE 30 SECONDS TO DESTROY IT!");
                continue;
            }

            float3 direction = math.normalize(targetPos - curPos);
            velocity.ValueRW.Value = direction.xy * queen.ValueRO.Speed;
            transform.ValueRW.Position += math.float3(velocity.ValueRW.Value, 0) * deltaTime;
        }

        foreach (var (guard, transform, velocity, entity) in SystemAPI.Query<RefRW<CaravanGuardComponent>, RefRW<LocalTransform>, RefRW<SwarmVelocityComponent>>().WithEntityAccess())
        {
            if (!SystemAPI.Exists(guard.ValueRO.QueenEntity))
            {
                ecb.RemoveComponent<CaravanGuardComponent>(entity);
                continue;
            }

            if (transformLookup.TryGetComponent(guard.ValueRO.QueenEntity, out LocalTransform queenTransform))
            {
                guard.ValueRW.OrbitAngle += 1.5f * deltaTime;
                float3 orbitOffset = new float3(math.cos(guard.ValueRO.OrbitAngle), math.sin(guard.ValueRO.OrbitAngle), 0) * guard.ValueRO.OrbitRadius;
                float3 targetPos = queenTransform.Position + orbitOffset;

                velocity.ValueRW.Value = (targetPos - transform.ValueRO.Position).xy * 15.0f;
                transform.ValueRW.Position += math.float3(velocity.ValueRW.Value, 0) * deltaTime;
            }
        }
    }
}