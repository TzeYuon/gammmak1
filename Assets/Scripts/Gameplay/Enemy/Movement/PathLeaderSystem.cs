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
        var input = SystemAPI.GetSingleton<PlayerInputData>();
        float deltaTime = SystemAPI.Time.DeltaTime;
        float2 mousePos = input.MouseWorldPosition;

        foreach (var (pathLeader, transform, buffer) in SystemAPI.Query<RefRW<PathLeaderComponent>, RefRW<LocalTransform>, DynamicBuffer<PathPosition>>())
        {
            float3 curPos = transform.ValueRO.Position;
            float2 targetDir = float2.zero;
            //float2 targetDir = mousePos - curPos.xy;
            //17/5/26 addon for A*
            Vector2Int startGrid = new Vector2Int((int)math.round(curPos.x), (int)math.round(curPos.y));
            Vector2Int endGrid = new Vector2Int((int)math.round(mousePos.x), (int)math.round(mousePos.y));
            Vector2Int lastEnd = new Vector2Int((int)math.round(pathLeader.ValueRO.LastTargetPos.x), (int)math.round(pathLeader.ValueRO.LastTargetPos.y));
            if (endGrid.x != lastEnd.x || endGrid.y != lastEnd.y)
            {
                buffer.Clear();
                buffer.Add(new PathPosition { Value = curPos }); // Add current pos so it doesn't crash
            }
            List<Vector2Int> path = Pathfinder.Astar(startGrid, endGrid);

            if (path != null && path.Count > 0)
            {
                if (path.Count > 1)
                {
                    float2 nextWaypoint = new float2(path[1].x, path[1].y);

                    if (math.distance(curPos.xy, nextWaypoint) < 0.15f && path.Count > 2)
                    {
                        nextWaypoint = new float2(path[2].x, path[2].y);
                    }
                    targetDir = nextWaypoint - curPos.xy;
                }
                else
                {
                    targetDir = mousePos - curPos.xy;
                }
            }
            else
            {
                targetDir = float2.zero;
            }

            pathLeader.ValueRW.LastTargetPos = mousePos;

            
            targetDir = math.normalizesafe(targetDir);

            float2 moveDir = math.lerp(pathLeader.ValueRO.MoveDirection, targetDir, 40f * deltaTime);
            pathLeader.ValueRW.MoveDirection = moveDir;

            float3 newPos = curPos + math.float3(moveDir, 0) * pathLeader.ValueRO.Speed * deltaTime;
            transform.ValueRW.Position = newPos;

            if (buffer.IsEmpty)
            {
                buffer.Add(new PathPosition { Value = newPos });
            }
            else
            {
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
