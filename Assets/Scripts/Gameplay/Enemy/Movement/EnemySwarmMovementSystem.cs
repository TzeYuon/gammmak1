using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

partial struct EnemySwarmMovementSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PathFollowerComponent>();
    }

    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (GridManager.Instance == null || !GridManager.Instance.nativeGrid.IsCreated) return;

        var moveJob = new SwarmFollowerMovementJob
        {
            ElapsedTime = SystemAPI.Time.ElapsedTime,
            DeltaTime = SystemAPI.Time.DeltaTime,
            PathLeaderLookup = SystemAPI.GetComponentLookup<PathLeaderComponent>(),
            PathLookup = SystemAPI.GetBufferLookup<PathPosition>(),
            Grid = GridManager.Instance.nativeGrid,
            GridWidth = GridManager.Instance.width,
            GridHeight = GridManager.Instance.height,
            WorldOrigin = new int2(GridManager.Instance.worldOrigin.x, GridManager.Instance.worldOrigin.y)
        };
        state.Dependency = moveJob.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
[WithAll(typeof(SwarmTag))]
public partial struct SwarmFollowerMovementJob : IJobEntity
{
    public double ElapsedTime;
    public float DeltaTime;
    [ReadOnly] public ComponentLookup<PathLeaderComponent> PathLeaderLookup;
    [ReadOnly] public BufferLookup<PathPosition> PathLookup;
    [ReadOnly] public NativeArray<int> Grid;
    public int GridWidth;
    public int GridHeight;
    public int2 WorldOrigin;

    void Execute(
        Entity entity,
        in PathFollowerComponent follower,
        ref LocalTransform transform,
        ref VelocityComponent velocity
        )
    {
        // Get path leader and path buffer
        Entity pathLeaderEntity = follower.PathLeaderEntity;
        if (!PathLeaderLookup.EntityExists(pathLeaderEntity) || !PathLookup.EntityExists(pathLeaderEntity)) return;
        PathLeaderComponent pathLeader = PathLeaderLookup[follower.PathLeaderEntity];
        DynamicBuffer<PathPosition> pathBuffer = PathLookup[pathLeaderEntity];

        if (pathBuffer.Length == 0) return;

        float3 curPos = transform.Position;
        float2 curVel = velocity.Value;
        float2 steeringForce = float2.zero;

        float width = pathLeader.Width;
        float baseSpeed = pathLeader.Speed;
        float finalMoveSpeed = baseSpeed;
        float maxSpeedMult = pathLeader.MaxSpeedMult;

        uint uniqueSeed = (uint)(entity.Index + (ElapsedTime * 3140));
        var rng = Random.CreateFromIndex(uniqueSeed);

        float3 toTarget = GetTargetDirection(follower, pathBuffer, curPos, width, Grid, GridWidth, GridHeight, WorldOrigin, pathLeader);
        SetDynamicSpeed(curVel, ref steeringForce, ref finalMoveSpeed, maxSpeedMult, toTarget);

        // Calculate noise
        float2 heading;
        if (math.lengthsq(curVel) > 0.1f) heading = math.normalize(curVel);
        else heading = rng.NextFloat2Direction();
        float3 sidewaysVec = math.cross(math.float3(heading, 0), new float3(0, 0, 1));

        float2 noiseInput = new((float)ElapsedTime, entity.Index);
        float smoothRandom = noise.snoise(noiseInput);

        steeringForce += 5 * smoothRandom * sidewaysVec.xy;

        // force exerting from walls to swarm
        float2 wallPushForce = float2.zero;
        int2 gridPos = new int2((int)math.round(curPos.x), (int)math.round(curPos.y)) - WorldOrigin;
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                int checkX = gridPos.x + x;
                int checkY = gridPos.y + y;

                if (checkX >= 0 && checkX < GridWidth && checkY >= 0 && checkY < GridHeight)
                {
                    if (Grid[checkX + checkY * GridWidth] == 1) // only wall
                    {
                        float2 wallCenter = new float2(checkX + WorldOrigin.x, checkY + WorldOrigin.y);

                        float2 pushDir = curPos.xy - wallCenter;
                        float distSq = math.lengthsq(pushDir);

                        if (distSq < 2.25f && distSq > 0.01f)
                        {
                            float distance = math.sqrt(distSq);
                            float pushStrength = (1.5f - distance) * 100.0f;
                            //wallPushForce += (math.normalize(pushDir) * pushStrength);

                            // Calculate push away and sideways
                            float2 pushNormal = pushDir / distance;

                            float2 slideTangent = new float2(-pushNormal.y, pushNormal.x);
                            wallPushForce += (pushNormal * pushStrength) + (slideTangent * (pushStrength * 0.5f));
                        }
                    }
                }
            }
        }
        steeringForce += wallPushForce;

        // Slow the agent down so that it doesn't go through walls
        if (math.lengthsq(wallPushForce) > 10f)
        {
            finalMoveSpeed = math.min(finalMoveSpeed, baseSpeed * 0.5f);
        }

        // update velocity
        float2 newVel = curVel + (steeringForce * DeltaTime);
        float speedSq = math.lengthsq(newVel);
        float absoluteMaxSpeed = baseSpeed * maxSpeedMult;
        if (speedSq > absoluteMaxSpeed * absoluteMaxSpeed)
        {
            newVel = absoluteMaxSpeed * math.normalize(newVel);
        }
        velocity.Value = newVel;

        // update position
        transform.Position += math.float3(velocity.Value, 0) * DeltaTime;

        // rotate to face movement direction
        if (speedSq > 0.001f)
        {
            float angle = math.atan2(newVel.y, newVel.x) - math.PI / 2;
            transform.Rotation = quaternion.RotateZ(angle);
        }

        static float3 GetTargetDirection(PathFollowerComponent follower, DynamicBuffer<PathPosition> pathBuffer, float3 curPos, float width, NativeArray<int> grid, int gridWidth, int gridHeight, int2 origin, PathLeaderComponent pathLeader)
        {
            int headIndex = pathBuffer.Length - 1;
            int targetIndex = headIndex;
            bool foundLOS = false;

            int searchLimit = math.max(0, headIndex - 30);

            // 1. Line of Sight Check
            while (targetIndex >= searchLimit)
            {
                float3 checkPos = pathBuffer[targetIndex].Value;
                if (HasBurstLOS(curPos.xy, checkPos.xy, grid, gridWidth, gridHeight, origin))
                {
                    foundLOS = true;
                    break;
                }
                targetIndex -= 1;
            }

            if (!foundLOS)
            {
                float minDistSq = float.MaxValue;
                int closestIndex = searchLimit;
                for (int i = searchLimit; i <= headIndex; i++)
                {
                    float distSq = math.distancesq(curPos, pathBuffer[i].Value);
                    if (distSq < minDistSq)
                    {
                        minDistSq = distSq;
                        closestIndex = i;
                    }
                }
                targetIndex = math.min(headIndex, closestIndex + 2);
            }

            if (targetIndex < 0) targetIndex = 0;
            float3 posA = pathBuffer[math.max(0, targetIndex - 1)].Value;
            float3 posB = pathBuffer[targetIndex].Value;
            float3 pathForward = posB - posA;

            float leaderSpeedSq = math.lengthsq(pathLeader.MoveDirection);
            // Clump into a circle if stationary
            if (math.lengthsq(pathForward) < 0.001f || (targetIndex == headIndex && leaderSpeedSq < 0.1f)   )
            {
                float angle = follower.Rank * 2.4f;
                pathForward = new float3(math.cos(angle), math.sin(angle), 0);
            }
            else
            {
                pathForward = math.normalize(pathForward);
            }

            float3 pathRight = math.cross(pathForward, new float3(0, 0, 1));

            float3 targetPosition = posB + follower.Offset * width * pathRight;

            if (!(targetIndex == headIndex && leaderSpeedSq < 0.1f))
            {
                float depth = (follower.Rank % 10) * 0.2f;
                targetPosition -= pathForward * depth;
            }
            return targetPosition - curPos;
        }

        // Bresenham Line Algorithm for Burst
        static bool HasBurstLOS(float2 start, float2 end, NativeArray<int> grid, int gridWidth, int gridHeight, int2 worldOrigin)
        {
            int x0 = (int)math.round(start.x) - worldOrigin.x;
            int y0 = (int)math.round(start.y) - worldOrigin.y;
            int x1 = (int)math.round(end.x) - worldOrigin.x;
            int y1 = (int)math.round(end.y) - worldOrigin.y;

            int dx = math.abs(x1 - x0), dy = math.abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            int loopGuard = 0;
            while (loopGuard++ < 1000)
            {
                if (loopGuard > 1)
                {
                    if (x0 >= 0 && x0 < gridWidth && y0 >= 0 && y0 < gridHeight)
                    {
                        if (grid[x0 + y0 * gridWidth] != 0) return false;
                    }
                    else return false;
                }
                else return false; // Off grid

                if (x0 == x1 && y0 == y1) break; // Reached target

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
            return true;
        }

        static void SetDynamicSpeed(float2 curVel, ref float2 steeringForce, ref float finalMoveSpeed, float maxSpeedMult, float3 toTarget)
        {
            float distSqToTarget = math.lengthsq(toTarget);

            // Calculate dynamic speed to increase speed when behind desired position
            if (distSqToTarget > 0.1f)
            {
                if (distSqToTarget > 1.0f) // If far from desired position
                {
                    float catchUpMultiplier = 1.0f + (distSqToTarget * 0.1f);
                    catchUpMultiplier = math.min(catchUpMultiplier, maxSpeedMult);
                    finalMoveSpeed *= catchUpMultiplier; // increase move speed
                }

                float3 desired = math.normalize(toTarget) * finalMoveSpeed;
                steeringForce = desired.xy - curVel;
            }
        }
    }
}