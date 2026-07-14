using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(ActionAndMovementGroup))]
public partial struct ShipMovementSystem : ISystem
{
    EntityQuery shipQuery; 

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TerrainGridComponent>();

        shipQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerTag, LocalTransform>()
            .WithDisabled<DeadTag>()
            .Build();
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

        int shipCount = shipQuery.CalculateEntityCount();
        var avoidanceArray = new NativeArray<ShipAvoidanceData>(shipCount, Allocator.TempJob);
        state.Dependency = new GatherShipDataJob
        {
            AvoidanceArray = avoidanceArray
        }.ScheduleParallel(state.Dependency);

        MoveShipJob moveJob = new MoveShipJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            AllShipData = avoidanceArray,
            ObstacleGrid = terrainGridComponent.Grid,
            CellSize = terrainGridComponent.CellSize,
            GridWidth = terrainGridComponent.GridWidth,
            GridHeight = terrainGridComponent.GridHeight,
            WorldOrigin = terrainGridComponent.WorldOrigin
        };
        state.Dependency = moveJob.ScheduleParallel(state.Dependency);

        avoidanceArray.Dispose(state.Dependency);
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

public struct ShipAvoidanceData
{
    public Entity ShipEntity;
    public float2 Position;
    public float Radius;
}

[BurstCompile]
[WithAll(typeof(PlayerTag))]
[WithAbsent(typeof(MothershipTag))]
public partial struct GatherShipDataJob : IJobEntity
{
    public NativeArray<ShipAvoidanceData> AvoidanceArray;

    private void Execute(
        [EntityIndexInQuery] int queryIndex,
        Entity entity,
        in LocalTransform transform,
        in ShipMovementStatsComponent stats)
    {
        float3 localOffset3D = new float3(stats.AvoidanceOffset, 0f);
        float3 worldOffset3D = math.rotate(transform.Rotation, localOffset3D);

        float2 worldOffsetPosition = transform.Position.xy + worldOffset3D.xy;

        AvoidanceArray[queryIndex] = new ShipAvoidanceData
        {
            ShipEntity = entity,
            Position = worldOffsetPosition,
            Radius = stats.AvoidanceRadius,
        };
    }
}

[BurstCompile]
[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
public partial struct MoveShipJob : IJobEntity
{
    public float DeltaTime;

    [ReadOnly] public NativeArray<ShipAvoidanceData> AllShipData;
    [ReadOnly] public NativeArray<int> ObstacleGrid;
    public float CellSize;
    public int GridWidth;
    public int GridHeight;
    public int2 WorldOrigin;

    private void Execute(
        in Entity entity,
        ref LocalTransform transform,
        ref DynamicBuffer<ShipWaypoint> waypoints,
        in ShipMovementStatsComponent stats,
        EnabledRefRW<MoveCommand> moveEnabled,
        ref MoveCommand command)
    {
        if (command.Recalculate) return;

        float2 currentPos = transform.Position.xy;
        float2 targetPos = command.MoveTarget;

        float brakingDistance = 1;

        if (moveEnabled.ValueRO)
        {
            // move to next waypoint if close enough
            if (waypoints.Length > 0)
            {
                targetPos = waypoints[0].Value;
                if (math.distancesq(currentPos, targetPos) < 0.25f)
                {
                    waypoints.RemoveAt(0);
                    if (waypoints.Length > 0) targetPos = waypoints[0].Value;
                }
            }

            float2 dirToTarget = targetPos - currentPos;
            float distToTarget = math.length(dirToTarget);
            bool isFinalTarget = waypoints.Length == 0;
            bool hasArrivedPos = distToTarget < 0.1f;

            // movement
            if (!hasArrivedPos)
            {
                float speed = stats.MaxSpeed;

                // Slow down smoothly when approaching the final destination
                if (isFinalTarget && distToTarget < brakingDistance)
                {
                    float slowdownMultiplier = math.max(0.1f, distToTarget / brakingDistance);
                    speed *= slowdownMultiplier;
                }

                currentPos += DeltaTime * speed * (dirToTarget / distToTarget);
                transform.Position.xy = currentPos;
            }

            // rotation
            quaternion desiredRot = transform.Rotation;
            if (isFinalTarget && command.UseTargetRotation && distToTarget < brakingDistance)
            {
                // rotate towards target rotation
                desiredRot = command.TargetRotation;
            }
            else if (!hasArrivedPos)
            {
                // face direction of movement
                float angle = math.atan2(dirToTarget.y, dirToTarget.x) - (math.PI / 2f);
                desiredRot = quaternion.RotateZ(angle);
            }

            // perform rotation
            transform.Rotation = math.slerp(transform.Rotation, desiredRot, stats.TurnSpeed * DeltaTime);

            // check if arrived then stop moving
            if (hasArrivedPos)
            {
                if (command.UseTargetRotation)
                {
                    // wait for rotation to finish
                    float3 currentFwd = math.rotate(transform.Rotation, new float3(0, 1, 0));
                    float3 targetFwd = math.rotate(command.TargetRotation, new float3(0, 1, 0));

                    if (math.dot(currentFwd, targetFwd) > 0.99f)
                    {
                        moveEnabled.ValueRW = false;
                    }
                }
                else
                {
                    moveEnabled.ValueRW = false;
                }
            }
        }
        else
        {
            // player ship avoidance
            // always happens even without move command
            float2 avoidanceForce = float2.zero;
            float avoidanceRadius = stats.AvoidanceRadius;
            float avoidanceWeight = 5.0f;
            for (int i = 0; i < AllShipData.Length; i++)
            {
                if (entity == AllShipData[i].ShipEntity) continue;

                float2 otherPos = AllShipData[i].Position;
                float otherRadius = AllShipData[i].Radius;

                float combinedRadius = avoidanceRadius + otherRadius;
                float combinedRadiusSq = combinedRadius * combinedRadius;

                float2 offset = currentPos - otherPos;
                float distSq = math.lengthsq(offset);

                if (distSq < combinedRadiusSq && distSq > 0.0001f)
                {
                    float dist = math.sqrt(distSq);
                    float penetrationDepth = (combinedRadius - dist) / combinedRadius;

                    float pushStrength = penetrationDepth * penetrationDepth;
                    avoidanceForce += avoidanceWeight * pushStrength * (offset / dist);
                }
            }

            // Terrain avoidance
            // always happens even without move command
            float2 wallAvoidanceForce = float2.zero;
            float avoidDistance = 1f;
            int2 centerCell = (int2)math.floor(currentPos / CellSize);
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    int2 checkCell = centerCell + new int2(x, y);
                    if (!GridUtility.IsTerrainPassable(checkCell, ObstacleGrid, GridWidth, GridHeight))
                    {
                        float2 worldPos = GridUtility.CellToWorldPosCorner(checkCell, WorldOrigin, CellSize);
                        float2 offset = currentPos - worldPos;

                        float2 absOffset = math.abs(offset);
                        float2 halfSize = new float2(CellSize * 0.5f);
                        float2 d = math.max(absOffset - halfSize, 0f);
                        float distToFace = math.length(d);

                        if (distToFace < avoidDistance)
                        {
                            float2 pushNormal = distToFace > 0.001f
                                ? math.normalize(d * math.sign(offset))
                                : math.normalizesafe(offset, float2.zero);

                            float pushStrength = (avoidDistance - distToFace) / avoidDistance;
                            pushStrength = math.pow(pushStrength, 2.0f);

                            wallAvoidanceForce += pushNormal * pushStrength;
                        }
                    }
                }
            }
            avoidanceForce += wallAvoidanceForce * 3.0f;

            // Check if movement would result in the ship entering the walls
            int2 currentCell = GridUtility.WorldPosToCell(currentPos, WorldOrigin, CellSize);
            float2 nextPosition = currentPos + avoidanceWeight * DeltaTime * avoidanceForce;
            int2 nextCell = GridUtility.WorldPosToCell(nextPosition, WorldOrigin, CellSize);
            if (!GridUtility.IsTerrainPassable(nextCell, ObstacleGrid, GridWidth, GridHeight))
            {
                // Check if we can move in x
                int2 cellX = nextCell;
                cellX.y = currentCell.y;
                bool canMoveX = GridUtility.IsTerrainPassable(cellX, ObstacleGrid, GridWidth, GridHeight);

                // Check if we can move in y
                int2 cellY = nextCell;
                cellY.x = currentCell.x;
                bool canMoveY = GridUtility.IsTerrainPassable(cellY, ObstacleGrid, GridWidth, GridHeight);

                if (canMoveX) avoidanceForce.y = 0f; // slide along x
                else if (canMoveY) avoidanceForce.x = 0f; // slide along y
                else return; // cannot move completely
            }

            // move the ship
            transform.Position += avoidanceWeight * DeltaTime * new float3(avoidanceForce, 0);
        }
    }
}