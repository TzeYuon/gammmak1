using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

partial struct HiveSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<HiveComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (hive, transform) in SystemAPI.Query<RefRW<HiveComponent>, RefRO<LocalTransform>>())
        {
            // Manage cooldown
            hive.ValueRW.SpawnTimer -= deltaTime;
            if (hive.ValueRO.SpawnTimer > 0) continue;
            hive.ValueRW.SpawnTimer += hive.ValueRW.SpawnCooldown;
            float3 hivePosition = transform.ValueRO.Position;

            // Spawn enemies
            Entity prefab = hive.ValueRO.Prefab;
            var rng = Random.CreateFromIndex((uint)SystemAPI.Time.ElapsedTime * 531);
            float spawnRadius = hive.ValueRO.Width * 3.0f;
            for (int i = 0; i < hive.ValueRO.SpawnAmount; i++)
            {
                Entity enemy = ecb.Instantiate(prefab);
                float angle = rng.NextFloat(0, math.PI * 2);
                float dist = math.sqrt(rng.NextFloat()) * spawnRadius;
                float2 randomOffset = new float2(math.cos(angle), math.sin(angle)) * dist;

                float3 spawnPos = hivePosition + math.float3(randomOffset, 0);

                ecb.SetComponent(enemy, new VelocityComponent { Value = randomOffset });
                ecb.SetComponent(enemy, LocalTransform.FromPosition(spawnPos));
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
