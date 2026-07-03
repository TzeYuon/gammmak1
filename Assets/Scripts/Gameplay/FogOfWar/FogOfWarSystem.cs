using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
partial struct FogOfWarSystem : ISystem
{
    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (FogOfWarManager.Instance == null || !FogOfWarManager.Instance.fogVisibilityArray.IsCreated) return;

        if (!SystemAPI.TryGetSingleton<TerrainGridComponent>(out var terrainGrid)) return;

        var visionJob = new RevealFogJob
        {
            FogArray = FogOfWarManager.Instance.fogVisibilityArray,
            GridWidth = terrainGrid.GridWidth,
            GridHeight = terrainGrid.GridHeight,
            WorldOrigin = terrainGrid.WorldOrigin
        };

        state.Dependency = visionJob.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct RevealFogJob : IJobEntity
{
    [NativeDisableParallelForRestriction]
    public NativeArray<Color32> FogArray;

    public int GridWidth;
    public int GridHeight;
    public int2 WorldOrigin;

    void Execute(ref LocalTransform transform, in PlayerTag playerTag)
    {
        int visionRadius = 15; // How far the ship can see
        int radiusSq = visionRadius * visionRadius;

        int gridX = (int)math.round(transform.Position.x) - WorldOrigin.x;
        int gridY = (int)math.round(transform.Position.y) - WorldOrigin.y;

        // Draw a circle of light around the ship
        for (int x = -visionRadius; x <= visionRadius; x++)
        {
            for (int y = -visionRadius; y <= visionRadius; y++)
            {
                if (x * x + y * y <= radiusSq)
                {
                    int checkX = gridX + x;
                    int checkY = gridY + y;
                    
                    if (checkX >= 0 && checkX < GridWidth && checkY >= 0 && checkY < GridHeight)
                    {
                        FogArray[checkX + checkY * GridWidth] = new Color32(0, 0, 0, 0);
                    }
                }
            }
        }
    }
}