using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

partial struct HeatmapInitSystem : ISystem
{
    private NativeArray<int> coarseEnemyArray;
    private NativeParallelMultiHashMap<int, Entity> fineEnemyHashmap;

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

        int coarseEnemySize = settings.CoarseWidth * settings.CoarseHeight;
        coarseEnemyArray = new NativeArray<int>(coarseEnemySize, Allocator.Persistent);
        state.EntityManager.AddComponentData(e, new CoarseEnemyHeatmapComponent
        {
            Map = coarseEnemyArray,
            CellSize = settings.CoarseCellSize,
            GridWidth = settings.CoarseWidth,
            GridHeight = settings.CoarseHeight
        });

        fineEnemyHashmap = new NativeParallelMultiHashMap<int, Entity>(10000, Allocator.Persistent);
        state.EntityManager.AddComponentData(e, new FineEnemyHeatmapComponent
        {
            Map = fineEnemyHashmap,
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
    }
}
