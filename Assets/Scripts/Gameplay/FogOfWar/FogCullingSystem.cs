using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(FogOfWarRenderSystem))]
partial struct FogCullingSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<FogOfWarState>(out var fogState)) return;
        if (!SystemAPI.TryGetSingleton<TerrainGridComponent>(out var terrainGrid)) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var fogArray = fogState.FogArray;
        int width = terrainGrid.GridWidth;
        int height = terrainGrid.GridHeight;
        int2 origin = terrainGrid.WorldOrigin;

        // Add DisableRendering
        var hideJob = new HideEntitiesJob
        {
            FogArray = fogArray,
            GridWidth = width,
            GridHeight = height,
            WorldOrigin = origin,
            ECB = ecb
        }.ScheduleParallel(state.Dependency);

        // Remove DisableRendering
        var revealJob = new RevealEntitiesJob
        {
            FogArray = fogArray,
            GridWidth = width,
            GridHeight = height,
            WorldOrigin = origin,
            ECB = ecb
        }.ScheduleParallel(hideJob);

        state.Dependency = revealJob;
    }
}

[BurstCompile]
[WithAll(typeof(HideInFogTag))]
[WithNone(typeof(DisableRendering))]
public partial struct HideEntitiesJob : IJobEntity
{
    [ReadOnly] public NativeArray<Color32> FogArray;
    public int GridWidth;
    public int GridHeight;
    public int2 WorldOrigin;
    public EntityCommandBuffer.ParallelWriter ECB;

    void Execute(Entity entity, [ChunkIndexInQuery] int sortKey, in LocalTransform transform)
    {
        int gridX = (int)math.round(transform.Position.x) - WorldOrigin.x;
        int gridY = (int)math.round(transform.Position.y) - WorldOrigin.y;

        if (gridX >= 0 && gridX < GridWidth && gridY >= 0 && gridY < GridHeight)
        {
            byte alpha = FogArray[gridX + gridY * GridWidth].a;

            if (alpha > 50)
            {
                ECB.AddComponent<DisableRendering>(sortKey, entity);
            }
        }
    }
}

[BurstCompile]
[WithAll(typeof(HideInFogTag), typeof(DisableRendering))]
public partial struct RevealEntitiesJob : IJobEntity
{
    [ReadOnly] public NativeArray<Color32> FogArray;
    public int GridWidth;
    public int GridHeight;
    public int2 WorldOrigin;
    public EntityCommandBuffer.ParallelWriter ECB;

    void Execute(Entity entity, [ChunkIndexInQuery] int sortKey, in LocalTransform transform)
    {
        int gridX = (int)math.round(transform.Position.x) - WorldOrigin.x;
        int gridY = (int)math.round(transform.Position.y) - WorldOrigin.y;

        if (gridX >= 0 && gridX < GridWidth && gridY >= 0 && gridY < GridHeight)
        {
            byte alpha = FogArray[gridX + gridY * GridWidth].a;

            if (alpha < 50)
            {
                ECB.RemoveComponent<DisableRendering>(sortKey, entity);
            }
        }
    }
}