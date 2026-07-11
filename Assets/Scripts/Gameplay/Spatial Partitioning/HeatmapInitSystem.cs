using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
partial struct HeatmapInitSystem : ISystem
{
    private NativeParallelMultiHashMap<int, Entity> fineEnemyHashmap;
    private NativeArray<HeatmapData> heatmapArray;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyHeatmapSettings>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EnemyHeatmapSettings settings = SystemAPI.GetSingleton<EnemyHeatmapSettings>();

        Entity e = state.EntityManager.CreateEntity();

        fineEnemyHashmap = new NativeParallelMultiHashMap<int, Entity>(10000, Allocator.Persistent);
        int fineEnemySize = settings.FineWidth * settings.FineHeight;
        heatmapArray = new NativeArray<HeatmapData>(fineEnemySize, Allocator.Persistent);
        state.EntityManager.AddComponentData(e, new FineEnemyHeatmapComponent
        {
            Map = fineEnemyHashmap,
            Grid = heatmapArray,
            WorldOrigin = settings.WorldOrigin,
            CellSize = settings.FineCellSize,
            GridWidth = settings.FineWidth,
            GridHeight = settings.FineHeight
        });

        state.Enabled = false; // This system only runs once
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (fineEnemyHashmap.IsCreated) fineEnemyHashmap.Dispose();
        if (heatmapArray.IsCreated) heatmapArray.Dispose();
    }
}
