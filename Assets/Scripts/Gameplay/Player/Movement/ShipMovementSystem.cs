using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(MovementSystemGroup))]
public partial struct ShipMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        MoveShipJob job = new()
        {
            DeltaTime = SystemAPI.Time.DeltaTime
        };

        job.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct MoveShipJob : IJobEntity
{
    public float DeltaTime;

    void Execute(
        ref LocalTransform transform,
        EnabledRefRW<MoveCommand> moveEnabled,
        ref MoveCommand command,
        in ShipMovementStatsComponent stats,
        ref ShipVelocity velocity
        )
    {
        float2 currentPos = transform.Position.xy;
        float2 targetPos = command.MoveTarget;

        float2 directionToTarget = targetPos - currentPos;
        float distToTarget = math.length(directionToTarget);

        if (distToTarget < 0.1f)
        {
            moveEnabled.ValueRW = false;
            velocity.Value = float2.zero;
            return;
        }

        float2 desiredDirection = directionToTarget / distToTarget;
        float targetSpeed = stats.MaxSpeed;

        if (distToTarget < stats.BrakingDistance)
        {
            targetSpeed = stats.MaxSpeed * (distToTarget / stats.BrakingDistance);
        }

        float2 desiredVelocity = desiredDirection * targetSpeed;
        velocity.Value = math.lerp(velocity.Value, desiredVelocity, stats.Acceleration * DeltaTime);

        transform.Position.xy += velocity.Value * DeltaTime;

        if (math.lengthsq(velocity.Value) > 0.01f)
        {
            float angle = math.atan2(velocity.Value.y, velocity.Value.x) - (math.PI / 2f);
            quaternion targetRotation = quaternion.RotateZ(angle);
            transform.Rotation = math.slerp(transform.Rotation, targetRotation, stats.TurnSpeed * DeltaTime);
        }
    }
}