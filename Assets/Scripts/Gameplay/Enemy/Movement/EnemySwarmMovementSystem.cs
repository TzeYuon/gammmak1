using Unity.Burst;
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
        double elapsedTime = SystemAPI.Time.ElapsedTime;
        float deltaTime = SystemAPI.Time.DeltaTime;
        var pathLeaderLookup = SystemAPI.GetComponentLookup<PathLeaderComponent>();
        var pathLookup = SystemAPI.GetBufferLookup<PathPosition>();
        foreach (var (follower, transform, velocity, entity) in 
            SystemAPI.Query<RefRO<PathFollowerComponent>, RefRW<LocalTransform>, RefRW<VelocityComponent>>().WithEntityAccess())
        {
            // Get path leader and path buffer
            Entity pathLeaderEntity = follower.ValueRO.PathLeaderEntity;
            if (!pathLeaderLookup.EntityExists(pathLeaderEntity) || !pathLookup.EntityExists(pathLeaderEntity)) continue;
            PathLeaderComponent pathLeader = pathLeaderLookup[follower.ValueRO.PathLeaderEntity];
            DynamicBuffer<PathPosition> pathBuffer = pathLookup[pathLeaderEntity];

            float3 curPos = transform.ValueRO.Position;
            float2 curVel = velocity.ValueRO.Value;
            float2 steeringForce = float2.zero;

            float width = pathLeader.Width;
            float baseSpeed = pathLeader.Speed;
            float finalMoveSpeed = baseSpeed;
            float maxSpeedMult = pathLeader.MaxSpeedMult;

            uint uniqueSeed = (uint)(entity.Index + (elapsedTime * 3140));
            var rng = Random.CreateFromIndex(uniqueSeed);

            // Find target position based on rank in path
            float headIndex = pathBuffer.Length - 1;
            float targetIndex = headIndex - follower.ValueRO.PathPosition;

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

            float3 targetPosition = math.lerp(posA, posB, t) + follower.ValueRO.Offset * width * pathRight;
            float3 toTarget = targetPosition - curPos;

            float distSqToTarget = math.lengthsq(toTarget);

            // Calculate dynamic speed to increase speed when behind desired position
            if (distSqToTarget > 1.0f) // If far away from desired position
            {
                float catchUpMultiplier = 1.0f + (distSqToTarget * 0.1f);
                catchUpMultiplier = math.min(catchUpMultiplier, maxSpeedMult);
                finalMoveSpeed *= catchUpMultiplier;
            }

            if (distSqToTarget > 0.1f)
            {
                float3 desired = math.normalize(toTarget) * finalMoveSpeed;
                steeringForce = desired.xy - curVel;
            }

            // Calculate noise
            float2 heading;
            if (math.lengthsq(curVel) > 0.1f) heading = math.normalize(curVel);
            else heading = rng.NextFloat2Direction();
            float3 sidewaysVec = math.cross(math.float3(heading, 0), new float3(0, 0, 1));

            float2 noiseInput = new((float)elapsedTime, entity.Index);
            float smoothRandom = noise.snoise(noiseInput);

            steeringForce += 5 * smoothRandom * sidewaysVec.xy;

            // update velocity
            float2 newVel = curVel + (steeringForce * deltaTime);
            float speedSq = math.lengthsq(newVel);
            if (speedSq > baseSpeed * baseSpeed * maxSpeedMult * maxSpeedMult)
            {
                newVel = maxSpeedMult * baseSpeed * math.normalize(newVel);
            }
            velocity.ValueRW.Value = newVel;

            // update position
            transform.ValueRW.Position += math.float3(velocity.ValueRO.Value, 0) * deltaTime;

            // rotate to face movement direction
            if (speedSq > 0.001f)
            {
                float angle = math.atan2(newVel.y, newVel.x) - math.PI / 2;
                transform.ValueRW.Rotation = quaternion.RotateZ(angle);
            }
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
