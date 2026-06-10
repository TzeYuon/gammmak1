using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(CombatTargetingGroup))]
partial struct TurretAimingSystem : ISystem
{
    private ComponentLookup<LocalToWorld> worldLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        worldLookup = state.GetComponentLookup<LocalToWorld>(true);

        state.RequireForUpdate<TurretComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        worldLookup.Update(ref state);

        var job = new RotateJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            WorldLookup = worldLookup
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
public partial struct RotateJob : IJobEntity
{
    public float DeltaTime;
    [ReadOnly] public ComponentLookup<LocalToWorld> WorldLookup;

    private void Execute(
        ref LocalTransform transform,
        in TurretComponent turret,
        in WeaponState state,
        in WeaponFiringArc arc,
        in LocalToWorld weaponWorld,
        in ShipReference ship
        )
    {
        quaternion desiredLocalRotation;

        if (state.HasTarget)
        {
            float2 turretWorldPos = weaponWorld.Position.xy;
            float2 dirToTarget = math.normalize(state.TargetPos - turretWorldPos);
            float desiredWorldAngle = math.atan2(dirToTarget.y, dirToTarget.x) - (math.PI / 2f);
            quaternion desiredWorldRot = quaternion.RotateZ(desiredWorldAngle);
            quaternion parentWorldRot = WorldLookup[ship.Value].Rotation;
            desiredLocalRotation = math.mul(math.inverse(parentWorldRot), desiredWorldRot);
        }
        else
        {
            // Return to base rotation
            float desiredLocalAngle = math.atan2(arc.BaseDirection.y, arc.BaseDirection.x) - (math.PI / 2f);
            desiredLocalRotation = quaternion.RotateZ(desiredLocalAngle);
        }

        transform.Rotation = math.slerp(transform.Rotation, desiredLocalRotation, turret.TurnSpeed * DeltaTime);
    }
}