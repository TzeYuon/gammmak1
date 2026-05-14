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

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var moveJob = new SwarmFollowerMovementJob
        {
            ElapsedTime = SystemAPI.Time.ElapsedTime,
            DeltaTime = SystemAPI.Time.DeltaTime,
            PathLeaderLookup = SystemAPI.GetComponentLookup<PathLeaderComponent>(),
            PathLookup = SystemAPI.GetBufferLookup<PathPosition>(),
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

        float3 curPos = transform.Position;
        float2 curVel = velocity.Value;
        float2 steeringForce = float2.zero;

        float width = pathLeader.Width;
        float baseSpeed = pathLeader.Speed;
        float finalMoveSpeed = baseSpeed;
        float maxSpeedMult = pathLeader.MaxSpeedMult;

        uint uniqueSeed = (uint)(entity.Index + (ElapsedTime * 3140));
        var rng = Random.CreateFromIndex(uniqueSeed);

        float3 toTarget = GetTargetDirection(follower, pathBuffer, curPos, width);
        SetDynamicSpeed(curVel, ref steeringForce, ref finalMoveSpeed, maxSpeedMult, toTarget);

        // Calculate noise
        float2 heading;
        if (math.lengthsq(curVel) > 0.1f) heading = math.normalize(curVel);
        else heading = rng.NextFloat2Direction();
        float3 sidewaysVec = math.cross(math.float3(heading, 0), new float3(0, 0, 1));

        float2 noiseInput = new((float)ElapsedTime, entity.Index);
        float smoothRandom = noise.snoise(noiseInput);

        steeringForce += 5 * smoothRandom * sidewaysVec.xy;

        // update velocity
        float2 newVel = curVel + (steeringForce * DeltaTime);
        float speedSq = math.lengthsq(newVel);
        if (speedSq > baseSpeed * baseSpeed * maxSpeedMult * maxSpeedMult)
        {
            newVel = maxSpeedMult * baseSpeed * math.normalize(newVel);
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

        static float3 GetTargetDirection(PathFollowerComponent follower, DynamicBuffer<PathPosition> pathBuffer, float3 curPos, float width)
        {
            // Find target position based on rank
            float headIndex = pathBuffer.Length - 1;
            float targetIndex = headIndex - follower.PathPosition;

            if (targetIndex < 0) targetIndex = 0;

            int indexA = (int)math.floor(targetIndex);
            int indexB = math.min(indexA + 1, pathBuffer.Length - 1);
            float t = targetIndex - indexA;

            float3 posA = pathBuffer[indexA].Value;
            float3 posB = pathBuffer[indexB].Value;

            // Determine path forward and right for width
            float3 pathForward = posB - posA;
            if (math.lengthsq(pathForward) < 0.001f) pathForward = new float3(0, 1, 0);
            else pathForward = math.normalize(pathForward);
            float3 pathRight = math.cross(pathForward, new float3(0, 0, 1));

            float3 targetPosition = math.lerp(posA, posB, t) + follower.Offset * width * pathRight;
            float3 toTarget = targetPosition - curPos;
            return toTarget;
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