using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
partial struct HeatmapInitSystem : ISystem
{
    private NativeArray<int> coarseEnemyArray;
    private NativeParallelMultiHashMap<int, Entity> fineEnemyHashmap;
    private NativeArray<int> fineEnemyArray;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyHeatmapSettings>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<TerrainGridComponent>(out var terrainGridComponent)) return;
        if (!terrainGridComponent.Grid.IsCreated) return;
        EnemyHeatmapSettings settings = SystemAPI.GetSingleton<EnemyHeatmapSettings>();

        Entity e = state.EntityManager.CreateEntity();

        int coarseEnemySize = settings.CoarseWidth * settings.CoarseHeight;
        coarseEnemyArray = new NativeArray<int>(coarseEnemySize, Allocator.Persistent);
        state.EntityManager.AddComponentData(e, new CoarseEnemyHeatmapComponent
        {
            Map = coarseEnemyArray,
            WorldOrigin = settings.WorldOrigin,
            CellSize = settings.CoarseCellSize,
            GridWidth = settings.CoarseWidth,
            GridHeight = settings.CoarseHeight
        });

        fineEnemyHashmap = new NativeParallelMultiHashMap<int, Entity>(10000, Allocator.Persistent);
        int fineEnemySize = settings.FineWidth * settings.FineHeight;
        fineEnemyArray = new NativeArray<int>(fineEnemySize, Allocator.Persistent);
        state.EntityManager.AddComponentData(e, new FineEnemyHeatmapComponent
        {
            Map = fineEnemyHashmap,
            DensityMap = fineEnemyArray,
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
        if (coarseEnemyArray.IsCreated) coarseEnemyArray.Dispose();
        if (fineEnemyHashmap.IsCreated) fineEnemyHashmap.Dispose();
        if (fineEnemyArray.IsCreated) fineEnemyArray.Dispose();
    }
}
