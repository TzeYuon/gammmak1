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
        in ShipMovementStatsComponent ship)
    {
        float2 currentPos = transform.Position.xy;
        float2 targetPos = command.MoveTarget;

        float2 direction = targetPos - currentPos;
        float distanceSq = math.lengthsq(direction);

        // Stop distance
        if (distanceSq < 0.25f)
        {
            moveEnabled.ValueRW = false;
            return;
        }

        // Normalize direction
        float distance = math.sqrt(distanceSq);
        float2 moveDir = direction / distance;

        // Move
        transform.Position.xy += DeltaTime * ship.MaxSpeed * moveDir;

        // Rotate towards target smoothly
        float angle = math.atan2(moveDir.y, moveDir.x);
        transform.Rotation = quaternion.RotateZ(angle - math.PI / 2f);
    }
}