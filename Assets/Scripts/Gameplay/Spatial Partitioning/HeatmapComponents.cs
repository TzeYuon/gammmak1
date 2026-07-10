using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct FineEnemyHeatmapComponent : IComponentData
{
    public NativeParallelMultiHashMap<int, Entity> Map;
    public NativeArray<int> DensityMap;
    public int2 WorldOrigin;
    public int GridWidth;
    public int GridHeight;
    public float CellSize;
}