using Unity.Collections;
using Unity.Entities;

public struct CoarseEnemyHeatmapComponent : IComponentData
{
    public NativeArray<int> Map;
    public int GridWidth;
    public int GridHeight;
    public float CellSize;
}

public struct FineEnemyHeatmapComponent : IComponentData
{
    public NativeParallelMultiHashMap<int, Entity> Map;
    public NativeArray<int> DensityMap;
    public int GridWidth;
    public int GridHeight;
    public float CellSize;
}
