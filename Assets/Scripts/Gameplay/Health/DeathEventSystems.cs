using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

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

        foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<DeadTag, SwarmTag>().WithEntityAccess())
        {
            // Create death visual
            vfxQueue.Enqueue(new VFXPayload { 
                Position = transform.ValueRO.Position,
                Type = (uint)VFXType.DeathExplosion,
                Colour = new float4(1, 0, 0, 0), 
            });

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
                    TimeRemaining = 5.0f
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
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        queueQuery.CompleteDependency();
        if (!SystemAPI.TryGetSingletonRW<GlobalVFXQueue>(out var vfxSingleton)) return;
        var vfxQueue = vfxSingleton.ValueRW.Queue;

        RefRW<DifficultyState> difficultyState = SystemAPI.GetSingletonRW<DifficultyState>();

        foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<DeadTag, HiveComponent>().WithEntityAccess())
        {
            difficultyState.ValueRW.HivesDestroyed++;

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