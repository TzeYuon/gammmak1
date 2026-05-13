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

            // Spawn path leader
            float3 hivePosition = transform.ValueRO.Position;
            Entity pathLeaderEntity = ecb.CreateEntity();
            float density = hive.ValueRO.Density;
            ecb.AddComponent(pathLeaderEntity, new PathLeaderComponent {
                Density = density,
                Width = hive.ValueRO.Width,
                Speed = hive.ValueRO.EnemySpeed,
                MaxSpeedMult = hive.ValueRO.MaxSpeedMult,

                MoveDirection = new float2(0, 1),
                MaxPathLength = 500
            });
            ecb.AddComponent(pathLeaderEntity, LocalTransform.FromPosition(hivePosition));
            ecb.AddBuffer<PathPosition>(pathLeaderEntity);
            ecb.AppendToBuffer(pathLeaderEntity, new PathPosition { Value = hivePosition });

            // Spawn enemies
            Entity prefab = hive.ValueRO.Prefab;
            var rng = Random.CreateFromIndex((uint)SystemAPI.Time.ElapsedTime * 531);
            for (int i = 0; i < hive.ValueRO.SpawnAmount; i++)
            {
                Entity enemy = ecb.Instantiate(prefab);
                ecb.SetComponent(enemy, new PathFollowerComponent
                {
                    PathLeaderEntity = pathLeaderEntity,

                    Rank = i,
                    PathPosition = i / density,

                    Offset = rng.NextFloat(-1, 1),
                    NoiseStrength = 2
                });
                ecb.SetComponent(enemy, LocalTransform.FromPosition(hivePosition));
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
