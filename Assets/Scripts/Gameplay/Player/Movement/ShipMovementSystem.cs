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
            //Grid = terrainGridComponent.Grid,
            //GridWidth = terrainGridComponent.GridWidth,
            //GridHeight = terrainGridComponent.GridHeight,
            //WorldOrigin = terrainGridComponent.WorldOrigin
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

    private void Execute(
        ref LocalTransform transform, 
        ref DynamicBuffer<ShipWaypoint> waypoints, 
        in ShipMovementStatsComponent stats, 
        EnabledRefRW<MoveCommand> moveEnabled, 
        ref MoveCommand command)
    {
        if (command.Recalculate) return;

        float2 currentPos = transform.Position.xy;
        float2 targetPos = command.MoveTarget;

        // --- 1. WAYPOINT LOGIC ---
        if (waypoints.Length > 0)
        {
            targetPos = waypoints[0].Value;
            if (math.distancesq(currentPos, targetPos) < 0.25f) // 0.5 unit radius snap
            {
                waypoints.RemoveAt(0);
                if (waypoints.Length > 0) targetPos = waypoints[0].Value;
            }
        }

        float2 dirToTarget = targetPos - currentPos;
        float distToTarget = math.length(dirToTarget);
        bool isFinalTarget = waypoints.Length == 0;

        bool hasArrivedPos = distToTarget < 0.1f;

        // --- 2. MOVEMENT ---
        if (!hasArrivedPos)
        {
            float speed = stats.MaxSpeed;

            // Slow down smoothly when approaching the final destination
            if (isFinalTarget && distToTarget < stats.BrakingDistance)
            {
                float slowdownMultiplier = math.max(0.1f, distToTarget / stats.BrakingDistance);
                speed *= slowdownMultiplier;
            }

            currentPos += (dirToTarget / distToTarget) * speed * DeltaTime;
            transform.Position.xy = currentPos;
        }

        // --- 3. ROTATION (The Early Turn) ---
        quaternion desiredRot = transform.Rotation;

        if (isFinalTarget && command.UseTargetRotation && distToTarget < stats.BrakingDistance)
        {
            // EARLY ROTATION: As soon as we start braking, we start turning to the final parked angle!
            desiredRot = command.TargetRotation;
        }
        else if (!hasArrivedPos)
        {
            // NORMAL ROTATION: Face the direction of travel
            float angle = math.atan2(dirToTarget.y, dirToTarget.x) - (math.PI / 2f);
            desiredRot = quaternion.RotateZ(angle);
        }

        // Smoothly rotate toward whatever target we chose
        transform.Rotation = math.slerp(transform.Rotation, desiredRot, stats.TurnSpeed * DeltaTime);

        // --- 4. COMPLETION CHECK ---
        if (hasArrivedPos)
        {
            if (command.UseTargetRotation)
            {
                // Wait for the ship to finish its spin before completing the command
                float3 currentFwd = math.rotate(transform.Rotation, new float3(0, 1, 0));
                float3 targetFwd = math.rotate(command.TargetRotation, new float3(0, 1, 0));

                if (math.dot(currentFwd, targetFwd) > 0.99f)
                {
                    moveEnabled.ValueRW = false; // Pos and Rot arrived!
                }
            }
            else
            {
                moveEnabled.ValueRW = false; // Only Pos mattered, arrived!
            }
        }
    }
}