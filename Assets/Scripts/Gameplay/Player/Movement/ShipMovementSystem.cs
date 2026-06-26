using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

//[BurstCompile]
[UpdateInGroup(typeof(ActionAndMovementGroup))]
public partial struct ShipMovementSystem : ISystem
{   
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TerrainGridComponent>();
    }

    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<TerrainGridComponent>(out var terrainGridComponent)) return;
        if (!terrainGridComponent.Grid.IsCreated) return;

        foreach (var (transform, command, buffer) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<MoveCommand>, DynamicBuffer<ShipWaypoint>>().WithAll<MoveCommand>())
        {
            if (command.ValueRO.Recalculate)
            {
                float3 curPos = transform.ValueRO.Position;
                float2 targetPos = command.ValueRO.MoveTarget;

                Vector2Int startWorld = new Vector2Int(Mathf.RoundToInt(curPos.x), Mathf.RoundToInt(curPos.y));
                Vector2Int endWorld = new Vector2Int(Mathf.RoundToInt(targetPos.x), Mathf.RoundToInt(targetPos.y));

                List<Vector2Int> path = Pathfinder.Astar(startWorld, endWorld, command.ValueRO.ForcePath);

                buffer.Clear();

                if (path != null && path.Count > 0)
                {
                    if (path.Count > 1) path.RemoveAt(0);
                    foreach (var p in path)
                    {
                        buffer.Add(new ShipWaypoint { Value = new float2(p.x, p.y) });
                    }
                }
                else
                {
                    command.ValueRW.PathExists = false;
                }

                command.ValueRW.Recalculate = false;
            }
        }

        MoveShipJob job = new()
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Grid = terrainGridComponent.Grid,
            GridWidth = terrainGridComponent.GridWidth,
            GridHeight = terrainGridComponent.GridHeight,
            WorldOrigin = terrainGridComponent.WorldOrigin
        };

        job.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct MoveShipJob : IJobEntity
{
    public float DeltaTime;
    [ReadOnly] public NativeArray<int> Grid;
    public int GridWidth;
    public int GridHeight;
    public int2 WorldOrigin;
    void Execute(
        ref LocalTransform transform,
        EnabledRefRW<MoveCommand> moveEnabled,
        ref MoveCommand command,
        in ShipMovementStatsComponent stats,
        ref ShipVelocity velocity,
        ref DynamicBuffer<ShipWaypoint> waypoints
        )
    {
        if (command.Recalculate) return;
        float2 currentPos = transform.Position.xy;
        float2 targetPos = command.MoveTarget;

        float distToFinal = math.distance(currentPos, targetPos);
        if (distToFinal < 0.1f)
        {
            moveEnabled.ValueRW = false;
            velocity.Value = float2.zero;
            return;
        }

        if (waypoints.Length > 0)
        {
            targetPos = waypoints[0].Value;

            float distToWaypoint = math.distance(currentPos, targetPos);

            if (distToWaypoint < 1.5f)
            {
                waypoints.RemoveAt(0);
                if (waypoints.Length > 0)
                {
                    targetPos = waypoints[0].Value;
                }
            }
        }

        float2 directionToTarget = targetPos - currentPos;
        float distToTarget = math.length(directionToTarget);
        if (distToTarget < 0.1f && waypoints.Length == 0)
        {
            moveEnabled.ValueRW = false;
            velocity.Value = float2.zero;
            return;
        }

        float2 desiredDirection = directionToTarget / distToTarget;
        float targetSpeed = stats.MaxSpeed;

        if (distToTarget < stats.BrakingDistance && waypoints.Length == 0)
        {
            targetSpeed = stats.MaxSpeed * (distToTarget / stats.BrakingDistance);
        }

        float2 desiredVelocity = desiredDirection * targetSpeed;
        velocity.Value = math.lerp(velocity.Value, desiredVelocity, stats.Acceleration * DeltaTime);

        currentPos += velocity.Value * DeltaTime;

        float2 wallPushForce = float2.zero;
        float2 positionCorrection = float2.zero;

        int gridX = (int)math.round(currentPos.x) - WorldOrigin.x;
        int gridY = (int)math.round(currentPos.y) - WorldOrigin.y;

        float shipRadius = 0.45f;

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                int checkX = gridX + x;
                int checkY = gridY + y;

                if (checkX >= 0 && checkX < GridWidth && checkY >= 0 && checkY < GridHeight)
                {
                    if (Grid[checkX + checkY * GridWidth] == 1)
                    {
                        float2 wallCenter = new float2(checkX + WorldOrigin.x, checkY + WorldOrigin.y);

                        float2 closestPoint = math.clamp(currentPos, wallCenter - 0.5f, wallCenter + 0.5f);
                        float2 pushDir = currentPos - closestPoint;
                        float distSq = math.lengthsq(pushDir);

                        if (distSq < shipRadius * shipRadius)
                        {
                            float2 pushNormal;
                            float overlap;

                            if (distSq < 0.0001f)
                            {
                                float2 toCenter = currentPos - wallCenter;
                                float signX = toCenter.x >= 0 ? 1f : -1f;
                                float signY = toCenter.y >= 0 ? 1f : -1f;

                                if (math.abs(toCenter.x) > math.abs(toCenter.y))
                                {
                                    pushNormal = new float2(signX, 0);
                                    overlap = shipRadius + (0.5f - math.abs(toCenter.x));
                                }
                                else
                                {
                                    pushNormal = new float2(0, signY);
                                    overlap = shipRadius + (0.5f - math.abs(toCenter.y));
                                }
                            }
                            else
                            {
                                float distance = math.sqrt(distSq);
                                pushNormal = pushDir / distance;
                                overlap = shipRadius - distance;
                            }

                            positionCorrection += pushNormal * overlap;

                            float velIntoWall = math.dot(velocity.Value, pushNormal);
                            if (velIntoWall < 0) velocity.Value -= pushNormal * velIntoWall;
                        }

                        float2 centerDir = currentPos - wallCenter;
                        float centerDistSq = math.lengthsq(centerDir);
                        if (centerDistSq < 1.44f && centerDistSq > 0.01f)
                        {
                            float centerDist = math.sqrt(centerDistSq);
                            float pushStrength = (1.2f - centerDist) * 15.0f;
                            float2 slideNormal = centerDir / centerDist;

                            float velIntoWall = math.dot(velocity.Value, slideNormal);
                            if (velIntoWall < 0) velocity.Value -= slideNormal * velIntoWall;

                            wallPushForce += (slideNormal * pushStrength);
                        }
                    }
                }
            }
        }


        currentPos += positionCorrection;

        velocity.Value += wallPushForce * DeltaTime;

        float currentSpeedSq = math.lengthsq(velocity.Value);
        float absoluteMaxSpeed = stats.MaxSpeed * 1.5f;
        if (currentSpeedSq > absoluteMaxSpeed * absoluteMaxSpeed)
        {
            velocity.Value = math.normalize(velocity.Value) * absoluteMaxSpeed;
        }

        transform.Position.xy = currentPos;

        if (math.lengthsq(velocity.Value) > 0.01f)
        {
            float angle = math.atan2(velocity.Value.y, velocity.Value.x) - (math.PI / 2f);
            quaternion targetRotation = quaternion.RotateZ(angle);
            transform.Rotation = math.slerp(transform.Rotation, targetRotation, stats.TurnSpeed * DeltaTime);
        }
    }
}