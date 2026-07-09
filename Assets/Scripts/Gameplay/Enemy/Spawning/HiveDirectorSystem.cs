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
        eventTimer = 120.0f;

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

        var terrainGrid = SystemAPI.GetSingleton<TerrainGridComponent>();
        int2 startGrid = new int2((int)math.round(startPos.x), (int)math.round(startPos.y)) - terrainGrid.WorldOrigin;
        int2 endGrid = new int2((int)math.round(destinationPos.x), (int)math.round(destinationPos.y)) - terrainGrid.WorldOrigin;

        NativeList<float2> path = new NativeList<float2>(Allocator.Temp);

        BurstPathfinder.CalculateAStar(startGrid, endGrid, terrainGrid.Grid, terrainGrid.GridWidth, terrainGrid.GridHeight, terrainGrid.WorldOrigin, false, ref path);

        if (math.lengthsq(startPos) > 0)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var prefabs = SystemAPI.GetSingleton<HivePrefabDatabase>();
            var swarmPrefab = SystemAPI.GetSingleton<EnemyPrefabDatabase>().GetPrefab(EnemyType.Standard);
            float scale = 0.2f;

            Entity queen = ecb.Instantiate(prefabs.GetPrefab(HiveType.Standard));
            ecb.SetComponent(queen, LocalTransform.FromPosition(startPos).WithScale(scale));

            ecb.AddComponent(queen, new SwarmVelocityComponent { Value = float2.zero });
            ecb.AddComponent(queen, new CaravanQueenComponent
            {
                Destination = destinationPos,
                Speed = 5.0f,
                HasArrived = false,
                IncubationTimer = 30.0f,
                Scale = scale
            });

            for (int i = 0; i < 30; i++) // dont hard code the value later
            {
                Entity guard = ecb.Instantiate(swarmPrefab);
                float angle = rng.NextFloat(0, math.PI * 2);
                float dist = math.sqrt(rng.NextFloat()) * 5.0f;
                float3 spawnPos = startPos + new float3(math.cos(angle) * dist, math.sin(angle) * dist, 0);

                ecb.SetComponent(guard, LocalTransform.FromPosition(spawnPos));
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
        public float Scale;
    }

    //[InternalBuffer
    //apacity(20)]
    //public struct CaravanWaypoint : IBufferElementData
    //{
    //    public float2 Value;
    //}
}
