using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

[BurstCompile]
[UpdateInGroup(typeof(PostUpdateSystemGroup))]
public partial class SwarmDeathEventSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<BiomassConfig>();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<GlobalVFXQueue>(out var vfxSingleton)) return;
        var vfxQueue = vfxSingleton.Queue;

        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged);

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
[UpdateInGroup(typeof(PostUpdateSystemGroup))]
public partial class PlayerDeathEventSystem : SystemBase
{
    [BurstCompile]
    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<GlobalVFXQueue>(out var vfxSingleton)) return;
        var vfxQueue = vfxSingleton.Queue;
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
[UpdateInGroup(typeof(PostUpdateSystemGroup))]
public partial struct MothershipDeathEventSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<GlobalVFXQueue>(out var vfxSingleton)) return;
        var vfxQueue = vfxSingleton.Queue;
        foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<DeadTag, MothershipTag>())
        {
            // 1. Spawn the biggest explosion in the game
            vfxQueue.Enqueue(new VFXPayload
            {
                Position = transform.ValueRO.Position,
                Type = (uint)VFXType.DeathExplosion,
                Colour = new float4(0, 1, 1, 0)
            });
        }
    }
}