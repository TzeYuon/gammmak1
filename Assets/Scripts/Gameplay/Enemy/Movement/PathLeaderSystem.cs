using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

partial struct PathLeaderSystem : ISystem
{
    //[BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PathLeaderComponent>();
        state.RequireForUpdate<PlayerInputData>();
    }

    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        int offsetX = 1000;
        int offsetY = 1000;
        var input = SystemAPI.GetSingleton<PlayerInputData>();
        float deltaTime = SystemAPI.Time.DeltaTime;
        float2 mousePos = input.MouseWorldPosition;
        foreach (var (pathLeader, transform, buffer) in SystemAPI.Query<RefRW<PathLeaderComponent>, RefRW<LocalTransform>, DynamicBuffer<PathPosition>>())
        {
            float3 curPos = transform.ValueRO.Position;
            //float2 targetDir = mousePos - curPos.xy;
            //17/5/26 addon for A*
            Vector2Int startGrid = new Vector2Int((int)math.round(curPos.x - offsetX), (int)math.round(curPos.y - offsetY));
            Vector2Int endGrid = new Vector2Int((int)math.round(mousePos.x - offsetX), (int)math.round(mousePos.y - offsetY));
            float2 targetDir = float2.zero;
            List<Vector2Int> path = Pathfinder.Astar(startGrid, endGrid);

            if (path != null && path.Count > 1)
            {
                // We don't want to look at the very end of the path...
                // We want to look at the VERY NEXT CORNER (Index 1) so we steer around the walls!
                float2 nextWaypoint = new float2(path[1].x, path[1].y);
                targetDir = nextWaypoint - curPos.xy;
            }
            else
            {
                targetDir = mousePos - curPos.xy;
            }
            targetDir = math.normalizesafe(targetDir);
            float2 moveDir = math.lerp(pathLeader.ValueRO.MoveDirection, targetDir, 10f * deltaTime);
            pathLeader.ValueRW.MoveDirection = moveDir;

            float3 newPos = curPos + math.float3(moveDir, 0) * pathLeader.ValueRO.Speed;
            transform.ValueRW.Position = newPos;

            if (buffer.IsEmpty)
            {
                buffer.Add(new PathPosition { Value = newPos });
            }
            else
            {
                // Add to buffer if min distance reached
                float3 lastPoint = buffer[buffer.Length - 1].Value;
                if (math.distancesq(newPos, lastPoint) >= 1.0f)
                {
                    buffer.Add(new PathPosition { Value = newPos });
                    if (buffer.Length > pathLeader.ValueRO.MaxPathLength) buffer.RemoveAt(0);
                }
            }
        }
    }
    
    //[BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
