using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct HiveDirectorSystem : ISystem
{
    private float eventTimer;
    private Random rng;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DifficultyState>();
        state.RequireForUpdate<LevelSpawnData>();
        state.RequireForUpdate<ActiveHiveTag>();
        state.RequireForUpdate<HivePrefabDatabase>();

        eventTimer = 10.0f; // Change this
        rng = new Random(6767);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        eventTimer -= SystemAPI.Time.DeltaTime;
        if (eventTimer > 0) return;
        eventTimer = 10.0f; // Change this

        var spawnDataComp = SystemAPI.GetSingletonRW<LevelSpawnData>();
        ref var spawnData = ref spawnDataComp.ValueRW;

        if (spawnData.ExpansionSpotCount <= 0) return;

        float3 destinationPos = float3.zero;
        unsafe
        {
            // Pick random index
            int randomIndex = rng.NextInt(0, spawnData.ExpansionSpotCount);
            destinationPos = new float3(spawnData.ExpansionSpotsX[randomIndex], spawnData.ExpansionSpotsY[randomIndex], 0);

            int lastIndex = spawnData.ExpansionSpotCount - 1;
            spawnData.ExpansionSpotsX[randomIndex] = spawnData.ExpansionSpotsX[lastIndex];
            spawnData.ExpansionSpotsY[randomIndex] = spawnData.ExpansionSpotsY[lastIndex];
            spawnData.ExpansionSpotCount--;
        }

        // Find an active hive
        float3 startPos = float3.zero;
        foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<ActiveHiveTag>())
        {
            startPos = transform.ValueRO.Position;
            break;
        }

        if (math.lengthsq(startPos) > 0)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var prefabs = SystemAPI.GetSingleton<HivePrefabDatabase>();
            var swarmPrefab = SystemAPI.GetSingleton<EnemyPrefabDatabase>();

            Entity queen = ecb.Instantiate(prefabs.GetPrefab(HiveType.Standard));
            ecb.SetComponent(queen, LocalTransform.FromPosition(startPos));

            ecb.AddComponent(queen, new SwarmVelocityComponent { Value = float2.zero });
            ecb.AddComponent(queen, new CaravanQueenComponent
            {
                Destination = destinationPos,
                Speed = 20.0f, // Change this
                HasArrived = false,
                IncubationTimer = 10.0f
            });

            for (int i = 0; i < 20; i++) // dont hard code later
            {
                Entity guard = ecb.Instantiate(swarmPrefab.GetPrefab(EnemyType.Standard));
                ecb.SetComponent(guard, LocalTransform.FromPosition(startPos));
                ecb.AddComponent(guard, new SwarmVelocityComponent { Value = float2.zero });

                ecb.AddComponent(guard, new CaravanGuardComponent
                {
                    QueenEntity = queen,
                    OrbitAngle = (math.PI * 2 / 20) * i,
                    OrbitRadius = 8.0f
                });
            }

            UnityEngine.Debug.LogWarning("WARNING: A HIVE SEED HAS DEPARTED!");
        }
    }


public struct CaravanQueenComponent : IComponentData
    {
        public float3 Destination;
        public float Speed;
        public bool HasArrived;
        public float IncubationTimer;
    }

    public struct CaravanGuardComponent : IComponentData
    {
        public Entity QueenEntity;
        public float OrbitAngle;
        public float OrbitRadius;
    }
}