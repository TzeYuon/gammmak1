using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public partial struct TerrainGridComponent : IComponentData
{
    public NativeArray<int> Grid;
    public int2 WorldOrigin;
    public int GridWidth;
    public int GridHeight;
    public float CellSize;
}

public partial struct EnemyFlowFieldComponent : IComponentData
{
    public NativeArray<float2> FlowField;
    public int2 WorldOrigin;
    public int GridWidth;
    public int GridHeight;
    public float CellSize;
}
