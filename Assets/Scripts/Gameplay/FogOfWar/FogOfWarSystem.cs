using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct FogOfWarSystem : ISystem
{
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.TryGetSingletonRW<FogOfWarState>(out var fogCompRW))
        {
            if (fogCompRW.ValueRW.FogArray.IsCreated)
            {
                fogCompRW.ValueRW.FogArray.Dispose();
            }
        }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<TerrainGridComponent>(out var terrainGrid)) return;
        if (!terrainGrid.Grid.IsCreated) return;
        if (!SystemAPI.TryGetSingletonRW<FogOfWarSettings>(out var settings)) return;

        int totalCells = terrainGrid.GridWidth * terrainGrid.GridHeight;
        if (!SystemAPI.HasSingleton<FogOfWarState>())
        {
            var newFogArray = new NativeArray<Color32>(totalCells, Allocator.Persistent);
            for (int i = 0; i < totalCells; i++) newFogArray[i] = new Color32(0, 0, 0, 255);

            state.EntityManager.CreateSingleton(new FogOfWarState { FogArray = newFogArray });
            return;
        }

        var fogStateRW = SystemAPI.GetSingletonRW<FogOfWarState>();
        var fadeJob = new FadeFogJob
        {
            FogArray = fogStateRW.ValueRW.FogArray,
            MemFogValue = settings.ValueRO.MemFogValue
        }.Schedule(totalCells, 1024, state.Dependency);

        float FogRadius = settings.ValueRO.FogRadius;
        var revealJob = new RevealFogJob
        {
            FogArray = fogStateRW.ValueRW.FogArray,
            GridWidth = terrainGrid.GridWidth,
            GridHeight = terrainGrid.GridHeight,
            WorldOrigin = terrainGrid.WorldOrigin,
            VisionRadiusSq = FogRadius * FogRadius
        }.ScheduleParallel(fadeJob);

        state.Dependency = revealJob;
    }
}

[BurstCompile]
public struct FadeFogJob : IJobParallelFor
{
    public NativeArray<Color32> FogArray;
    public int MemFogValue;
    public void Execute(int index)
    {
        byte currentAlpha = FogArray[index].a;
        if (currentAlpha < MemFogValue)
        {
            FogArray[index] = new Color32(0, 0, 0, (byte)(currentAlpha + 1));
        }
    }
}

[BurstCompile]
[WithAll(typeof(PlayerTag))]
public partial struct RevealFogJob : IJobEntity
{
    [NativeDisableParallelForRestriction] public NativeArray<Color32> FogArray;
    public int GridWidth;
    public int GridHeight;
    public int2 WorldOrigin;
    public float VisionRadiusSq;

    void Execute(in LocalTransform transform)
    {
        int gridX = (int)math.round(transform.Position.x) - WorldOrigin.x;
        int gridY = (int)math.round(transform.Position.y) - WorldOrigin.y;
        int radiusCells = (int)math.sqrt(VisionRadiusSq);

        for (int x = -radiusCells; x <= radiusCells; x++)
        {
            for (int y = -radiusCells; y <= radiusCells; y++)
            {
                if (x * x + y * y <= VisionRadiusSq)
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