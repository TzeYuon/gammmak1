using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct HiveDirectorSystem : ISystem
{
    private Random rng;
    private int _lastSurgeCount;
    private int _surgesSinceLastSpawn;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<LevelSpawnData>();
        state.RequireForUpdate<HivePrefabDatabase>();
        state.RequireForUpdate<HiveComponent>();

        rng = new Random(6767);
        _lastSurgeCount = 0;
        _surgesSinceLastSpawn = 0;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        SystemAPI.TryGetSingleton<GlobalSurgeState>(out var surgeState);
        SystemAPI.TryGetSingleton<DifficultyState>(out var difficultyState);
        SystemAPI.TryGetSingleton<LevelBlueprint>(out var levelBlueprint);

        if (surgeState.TotalSurges > _lastSurgeCount)
        {
            _lastSurgeCount = surgeState.TotalSurges;
            _surgesSinceLastSpawn++;
        }
        int requiredSurgesToSpawn = 5; // Default (Low)

        if (levelBlueprint.ThreatLevel == ThreatLevel.Medium) requiredSurgesToSpawn = 4;
        else if (levelBlueprint.ThreatLevel == ThreatLevel.High) requiredSurgesToSpawn = 3;
        else if (levelBlueprint.ThreatLevel == ThreatLevel.Boss) requiredSurgesToSpawn = 2;

        if (_surgesSinceLastSpawn < requiredSurgesToSpawn) return;

        var spawnDataComp = SystemAPI.GetSingletonRW<LevelSpawnData>();
        ref var spawnData = ref spawnDataComp.ValueRW;

        if (spawnData.ExpansionSpotCount <= 0) return;

        float3 startPos = float3.zero;
        bool foundHive = false;
        foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<HiveComponent>())
        {
            startPos = transform.ValueRO.Position;
            foundHive = true;
            break;
        }

        if (!foundHive) return;
        _surgesSinceLastSpawn = 0;

        float3 destinationPos = float3.zero;
        unsafe
        {
            int randomIndex = rng.NextInt(0, spawnData.ExpansionSpotCount);
            destinationPos = new float3(spawnData.ExpansionSpotsX[randomIndex], spawnData.ExpansionSpotsY[randomIndex], 0);

            int lastIndex = spawnData.ExpansionSpotCount - 1;
            spawnData.ExpansionSpotsX[randomIndex] = spawnData.ExpansionSpotsX[lastIndex];
            spawnData.ExpansionSpotsY[randomIndex] = spawnData.ExpansionSpotsY[lastIndex];
            spawnData.ExpansionSpotCount--;
        }

        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var prefabs = SystemAPI.GetSingleton<HivePrefabDatabase>();
        float scale = 0.2f;

        Entity seed = ecb.Instantiate(prefabs.GetPrefab(HiveType.Standard));
        ecb.SetComponent(seed, LocalTransform.FromPosition(startPos).WithScale(scale));
        ecb.RemoveComponent<HiveComponent>(seed);
        ecb.AddComponent<SwarmTag>(seed);

        ecb.AddComponent(seed, new SwarmVelocityComponent { Value = float2.zero });
        ecb.AddComponent(seed, new ExplosionOnDeath { TargetSwarm = false, Radius = 400.0f, Damage = 300.0f });
        ecb.AddComponent(seed, new HiveSeedComponent
        {
            Destination = destinationPos,
            Speed = 5.0f,
            HasArrived = false,
            IncubationTimer = 30.0f,
            Scale = scale
        });
        ecb.AddComponent(seed, new HealthComponent { MaxValue = 500, Value = 500 });
        ecb.AddBuffer<DamageEvent>(seed);
        ecb.AddComponent<DeadTag>(seed);
        ecb.SetComponentEnabled<DeadTag>(seed, false);

        var terrainGrid = SystemAPI.GetSingleton<TerrainGridComponent>();
        int2 startGrid = new int2((int)math.round(startPos.x), (int)math.round(startPos.y)) - terrainGrid.WorldOrigin;
        int2 endGrid = new int2((int)math.round(destinationPos.x), (int)math.round(destinationPos.y)) - terrainGrid.WorldOrigin;

        NativeList<float2> path = new NativeList<float2>(Allocator.Temp);

        BurstPathfinder.CalculateAStar(startGrid, endGrid, terrainGrid.Grid, terrainGrid.GridWidth, terrainGrid.GridHeight, terrainGrid.WorldOrigin, false, ref path);

        var waypointBuffer = ecb.AddBuffer<CaravanWaypoint>(seed);

        if (path.Length > 1) path.RemoveAt(0);
        for (int i = 0; i < path.Length; i++)
        {
            waypointBuffer.Add(new CaravanWaypoint { Value = path[i] });
        }

        //UnityEngine.Debug.LogWarning("WARNING: A HIVE SEED HAS DEPARTED DURING THE SURGE!");
        path.Dispose();
    }

    public struct HiveSeedComponent : IComponentData
    {
        public float3 Destination;
        public float Speed;
        public bool HasArrived;
        public float IncubationTimer;
        public float Scale;
    }

    [InternalBufferCapacity(20)]
    public struct CaravanWaypoint : IBufferElementData
    {
        public float2 Value;
    }
}
