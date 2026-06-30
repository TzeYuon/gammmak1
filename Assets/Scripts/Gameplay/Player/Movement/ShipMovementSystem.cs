using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(ActionAndMovementGroup))]
public partial struct ShipMovementSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TerrainGridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<TerrainGridComponent>(out var terrainGridComponent)) return;
        if (!terrainGridComponent.Grid.IsCreated) return;

        var pathfindJob = new CalculateShipPathsJob
        {
            Grid = terrainGridComponent.Grid,
            GridWidth = terrainGridComponent.GridWidth,
            GridHeight = terrainGridComponent.GridHeight,
            WorldOrigin = terrainGridComponent.WorldOrigin
        };
        state.Dependency = pathfindJob.ScheduleParallel(state.Dependency);

        MoveShipJob moveJob = new MoveShipJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Grid = terrainGridComponent.Grid,
            GridWidth = terrainGridComponent.GridWidth,
            GridHeight = terrainGridComponent.GridHeight,
            WorldOrigin = terrainGridComponent.WorldOrigin
        };
        state.Dependency = moveJob.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct CalculateShipPathsJob : IJobEntity
{
    [ReadOnly] public NativeArray<int> Grid;
    public int GridWidth;
    public int GridHeight;
    public int2 WorldOrigin;

    void Execute(ref LocalTransform transform, ref MoveCommand command, ref DynamicBuffer<ShipWaypoint> waypoints)
    {
        if (!command.Recalculate) return;

        float3 curPos = transform.Position;
        float2 targetPos = command.MoveTarget;

        int2 startGrid = new int2((int)math.round(curPos.x), (int)math.round(curPos.y)) - WorldOrigin;
        int2 endGrid = new int2((int)math.round(targetPos.x), (int)math.round(targetPos.y)) - WorldOrigin;

        NativeList<float2> newPath = new NativeList<float2>(Allocator.Temp);

        BurstPathfinder.CalculateAStar(startGrid, endGrid, Grid, GridWidth, GridHeight, WorldOrigin, command.ForcePath, ref newPath);

        waypoints.Clear();

        if (newPath.Length > 0)
        {
            if (newPath.Length > 1) newPath.RemoveAt(0);

            for (int i = 0; i < newPath.Length; i++)
            {
                waypoints.Add(new ShipWaypoint { Value = newPath[i] });
            }
            command.PathExists = true;
        }
        else
        {
            command.PathExists = false;
        }

        newPath.Dispose();
        command.Recalculate = false;
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

        // Reached destination
        if (distToTarget < 0.1f && waypoints.Length == 0)
        {
            velocity.Value = float2.zero;

            // Rotate to desired direction
            if (command.UseTargetRotation)
            {
                quaternion targetRot = command.targetRotation;
                transform.Rotation = math.slerp(transform.Rotation, targetRot, stats.TurnSpeed * DeltaTime);

                // Check if rotated enough
                float3 currentForward = math.rotate(transform.Rotation, new float3(0, 1, 0));
                float3 desiredForward = math.rotate(targetRot, new float3(0, 1, 0));
                if (math.dot(currentForward, desiredForward) <= 0.99f)
                    return;
            }

            moveEnabled.ValueRW = false;
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
                        if (centerDistSq < 1.0f && centerDistSq > 0.01f)
                        {
                            float centerDist = math.sqrt(centerDistSq);
                            float2 slideNormal = centerDir / centerDist;

                            float forwardDot = math.dot(math.normalizesafe(velocity.Value), -slideNormal);

                            if (forwardDot > 0.1f)
                            {
                                float pushStrength = (1.0f - centerDist) * 15.0f;

                                float velIntoWall = math.dot(velocity.Value, slideNormal);
                                if (velIntoWall < 0) velocity.Value -= slideNormal * velIntoWall;

                                wallPushForce += (slideNormal * pushStrength);
                            }
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

        if (math.lengthsq(velocity.Value) > 0.5f)
        {
            float angle = math.atan2(velocity.Value.y, velocity.Value.x) - (math.PI / 2f);
            quaternion targetRotation = quaternion.RotateZ(angle);
            float dynamicTurnSpeed = stats.TurnSpeed;
            if (targetSpeed < stats.MaxSpeed * 0.5f) dynamicTurnSpeed *= 2.0f;
            transform.Rotation = math.slerp(transform.Rotation, targetRotation, stats.TurnSpeed * DeltaTime);
        }
    }
}