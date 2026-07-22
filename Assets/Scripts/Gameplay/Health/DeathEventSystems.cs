using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(LateGameplayGroup))]
public partial struct SwarmDeathEventSystem : ISystem
{
    EntityQuery queueQuery;

    public void OnCreate(ref SystemState state)
    {
        queueQuery = SystemAPI.QueryBuilder().WithAll<GlobalVFXQueue>().Build();
        state.RequireForUpdate<BiomassConfig>();
        state.RequireForUpdate<GlobalVFXQueue>();
        state.RequireForUpdate<LevelSpawnData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        queueQuery.CompleteDependency();
        if (!SystemAPI.TryGetSingletonRW<GlobalVFXQueue>(out var vfxSingleton)) return;
        var vfxQueue = vfxSingleton.ValueRW.Queue;

        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        BiomassConfig prefabSingleton = SystemAPI.GetSingleton<BiomassConfig>();
        Entity biomassPrefab = prefabSingleton.BiomassPrefab;

        var explodeLookup = SystemAPI.GetComponentLookup<ExplosionOnDeath>(true);
        var spawnDataComp = SystemAPI.GetSingletonRW<LevelSpawnData>();
        ref var spawnData = ref spawnDataComp.ValueRW;

        foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<DeadTag, SwarmTag>().WithEntityAccess())
        {
            var position = transform.ValueRO.Position;

            // Create death visual
            vfxQueue.Enqueue(new VFXPayload { 
                Position = position,
                Type = (uint)VFXType.DeathExplosion,
                Colour = new float4(1, 0, 0, 0), 
            });

            bool isSeed = SystemAPI.HasComponent<HiveDirectorSystem.HiveSeedComponent>(entity);
            if (isSeed)
            {
                unsafe
                {
                    int newIndex = spawnData.ExpansionSpotCount;
                    var queen = SystemAPI.GetComponent<HiveDirectorSystem.HiveSeedComponent>(entity);

                    if (newIndex < 8)
                    {
                        spawnData.ExpansionSpotsX[newIndex] = queen.Destination.x;
                        spawnData.ExpansionSpotsY[newIndex] = queen.Destination.y;
                        spawnData.ExpansionSpotCount++;
                    }
                }
            }

            // Explode if entity has relevant component
            if (explodeLookup.HasComponent(entity))
            {
                ExplosionOnDeath explosion = explodeLookup[entity];
                Entity explosionEntity = ecb.CreateEntity();
                ecb.AddComponent(explosionEntity, LocalTransform.FromPosition(position));
                if (isSeed)
                {
                    ecb.AddComponent(explosionEntity, new ExplosionEventTargetPlayer
                    {
                        Position = position,
                        Damage = explosion.Damage,
                        RadiusSq = explosion.Radius,
                        Colour = new float4(1, 0, 0, 0)
                    });
                }
                else
                {
                    ecb.AddComponent(explosionEntity, new ExplosionEventTargetSwarm
                    {
                        Position = position,
                        Damage = explosion.Damage,
                        RadiusSq = explosion.Radius,
                        Colour = new float4(1, 0, 0, 0)
                    });
                }
            }
        

            // Random chance to drop biomass
            var rng = Random.CreateFromIndex((uint)(entity.Index * SystemAPI.Time.ElapsedTime));
            if (rng.NextFloat() <= 0.1f)
            {
                Entity loot = ecb.Instantiate(biomassPrefab);

                float2 scatter = rng.NextFloat2Direction() * rng.NextFloat(0.5f, 1.5f);
                float3 spawnPos = transform.ValueRO.Position + new float3(scatter.x, scatter.y, 0);

                ecb.SetComponent(loot, LocalTransform.FromPosition(spawnPos));

                // Arbitrary 5 second lifespan and a value of 1
                ecb.SetComponent(loot, new BiomassLoot
                {
                    Value = 1.0f,
                    TimeRemaining = 20.0f
                });
            }
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(LateGameplayGroup))]
public partial struct HiveDeathEventSystem : ISystem
{
    EntityQuery queueQuery;

    public void OnCreate(ref SystemState state)
    {
        queueQuery = SystemAPI.QueryBuilder().WithAll<GlobalVFXQueue>().Build();
        state.RequireForUpdate<BiomassConfig>();
        state.RequireForUpdate<GlobalVFXQueue>();
        state.RequireForUpdate<DifficultyState>();
        state.RequireForUpdate<LevelSpawnData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        queueQuery.CompleteDependency();
        if (!SystemAPI.TryGetSingletonRW<GlobalVFXQueue>(out var vfxSingleton)) return;
        var vfxQueue = vfxSingleton.ValueRW.Queue;

        RefRW<DifficultyState> difficultyState = SystemAPI.GetSingletonRW<DifficultyState>();
        var spawnDataComp = SystemAPI.GetSingletonRW<LevelSpawnData>();
        ref var spawnData = ref spawnDataComp.ValueRW;

        foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<DeadTag, HiveComponent>().WithEntityAccess())
        {

            unsafe
            {
                int newIndex = spawnData.ExpansionSpotCount;
                if (SystemAPI.HasComponent<HiveDirectorSystem.HiveSeedComponent>(entity))
                {
                    var queen = SystemAPI.GetComponent<HiveDirectorSystem.HiveSeedComponent>(entity);
                    if (newIndex < 10)
                    {
                        spawnData.ExpansionSpotsX[newIndex] = queen.Destination.x;
                        spawnData.ExpansionSpotsY[newIndex] = queen.Destination.y;
                        spawnData.ExpansionSpotCount++;
                    }
                }
                else
                {
                    difficultyState.ValueRW.HivesDestroyed++;

                    if (newIndex < 8)
                    {
                        spawnData.ExpansionSpotsX[newIndex] = transform.ValueRO.Position.x;
                        spawnData.ExpansionSpotsY[newIndex] = transform.ValueRO.Position.y;
                        spawnData.ExpansionSpotCount++;

                        if (spawnData.ActiveHiveCount > 0) spawnData.ActiveHiveCount--;
                    }
                }
            }
            // TODO: Change death visual for hives
            // Create death visual
            vfxQueue.Enqueue(new VFXPayload
            {
                Position = transform.ValueRO.Position,
                Type = (uint)VFXType.DeathExplosion,
                Colour = new float4(1, 0, 0, 0),
            });
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(LateGameplayGroup))]
public partial struct PlayerDeathEventSystem : ISystem
{
    EntityQuery queueQuery;

    public void OnCreate(ref SystemState state)
    {
        queueQuery = SystemAPI.QueryBuilder().WithAll<GlobalVFXQueue>().Build();
        state.RequireForUpdate<GlobalVFXQueue>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        queueQuery.CompleteDependency();
        if (!SystemAPI.TryGetSingletonRW<GlobalVFXQueue>(out var vfxSingleton)) return;
        var vfxQueue = vfxSingleton.ValueRW.Queue;
        foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<DeadTag, PlayerTag>().WithAbsent<MothershipTag>())
        {
            vfxQueue.Enqueue(new VFXPayload
            {
                Position = transform.ValueRO.Position,
                Type = (uint)VFXType.DeathExplosion,
                Colour = new float4(0, 1, 1, 0)
            });
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(LateGameplayGroup))]
public partial struct MothershipDeathEventSystem : ISystem
{
    EntityQuery queueQuery;

    public void OnCreate(ref SystemState state)
    {
        queueQuery = SystemAPI.QueryBuilder().WithAll<GlobalVFXQueue>().Build();
        state.RequireForUpdate<GlobalVFXQueue>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        queueQuery.CompleteDependency();
        if (!SystemAPI.TryGetSingletonRW<GlobalVFXQueue>(out var vfxSingleton)) return;
        var vfxQueue = vfxSingleton.ValueRW.Queue;
        foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<DeadTag, MothershipTag>())
        {
            vfxQueue.Enqueue(new VFXPayload
            {
                Position = transform.ValueRO.Position,
                Type = (uint)VFXType.DeathExplosion,
                Colour = new float4(0, 1, 1, 0)
            });
        }
    }
}