using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(LateGameplayGroup))]
partial struct SelectionVisualSystem : ISystem
{
    private ComponentLookup<SelectedTag> selectedLookup;
    private ComponentLookup<MoveCommand> moveCommandLookup;
    private ComponentLookup<MoveCommandProjection> moveCommandProjectionLookup;
    private ComponentLookup<LocalTransform> shipTransformLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        selectedLookup = state.GetComponentLookup<SelectedTag>(true);
        moveCommandLookup = state.GetComponentLookup<MoveCommand>(true);
        moveCommandProjectionLookup = state.GetComponentLookup<MoveCommandProjection>(true);
        shipTransformLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        selectedLookup.Update(ref state);
        moveCommandLookup.Update(ref state);
        moveCommandProjectionLookup.Update(ref state);
        shipTransformLookup.Update(ref state);
        float deltaTime = SystemAPI.Time.DeltaTime;

        var selectionVisualHandle = new UpdateSelectionVisualsJob
        {
            SelectedLookup = selectedLookup,
            DeltaTime = deltaTime
        }.ScheduleParallel(state.Dependency);

        var movementLineHandle = new UpdateMovementLinesJob
        {
            SelectedLookup = selectedLookup,
            MoveCommandLookup = moveCommandLookup,
            ShipTransformLookup = shipTransformLookup
        }.ScheduleParallel(selectionVisualHandle);

        var movementTargetProjectionHandle = new UpdateMovementTargetProjectionJob
        {
            SelectedLookup = selectedLookup,
            MoveCommandProjectionLookup = moveCommandProjectionLookup,
        }.ScheduleParallel(movementLineHandle);

        state.Dependency = new UpdateMovementTargetJob
        {
            SelectedLookup = selectedLookup,
            MoveCommandLookup = moveCommandLookup,
        }.ScheduleParallel(movementTargetProjectionHandle);
    }
}

[BurstCompile]
public partial struct UpdateSelectionVisualsJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<SelectedTag> SelectedLookup;
    public float DeltaTime;

    private void Execute(
        ref LocalToWorld localToWorld, 
        in SelectionVisualComponent visual, 
        in ShipReference shipReference
        )
    {
        if (shipReference.Value == Entity.Null) return; // Invalid ship reference

        if (!SelectedLookup.HasComponent(shipReference.Value)) // ship does not have selected component
        {
            localToWorld.Value = float4x4.TRS(localToWorld.Value.Translation(), quaternion.identity, 0);
            return;
        }

        if (SelectedLookup.IsComponentEnabled(shipReference.Value)) // ship selected
        {
            localToWorld.Value = float4x4.TRS(localToWorld.Value.Translation(), quaternion.identity, visual.Scale);
        }
        else // ship not selected
        {
            localToWorld.Value = float4x4.TRS(localToWorld.Value.Translation(), quaternion.identity, 0);
        }
    }
}

[BurstCompile]
public partial struct UpdateMovementLinesJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<SelectedTag> SelectedLookup;
    [ReadOnly] public ComponentLookup<MoveCommand> MoveCommandLookup;
    [NativeDisableParallelForRestriction] [ReadOnly] public ComponentLookup<LocalTransform> ShipTransformLookup;

    private void Execute(
        ref LocalToWorld localToWorld,
        in MovementLineVisualComponent lineVisual,
        in ShipReference shipRef)
    {
        if (shipRef.Value == Entity.Null 
            || !SelectedLookup.HasComponent(shipRef.Value) 
            || !MoveCommandLookup.HasComponent(shipRef.Value) 
            || !ShipTransformLookup.HasComponent(shipRef.Value))
        {
            localToWorld.Value = float4x4.Scale(0f);
            return;
        }

        bool isSelected = SelectedLookup.IsComponentEnabled(shipRef.Value);
        bool isMoving = MoveCommandLookup.IsComponentEnabled(shipRef.Value);

        if (isSelected && isMoving)
        {
            float3 shipPos = ShipTransformLookup[shipRef.Value].Position;
            float2 targetPos = MoveCommandLookup[shipRef.Value].MoveTarget;

            float2 direction = targetPos - shipPos.xy;
            float distance = math.length(direction);

            if (distance < 0.1f)
            {
                localToWorld.Value = float4x4.Scale(0f);
                return;
            }
            
            float3 midpoint = new(shipPos.xy + (direction * 0.5f), 0);
            float angle = math.atan2(direction.y, direction.x);
            quaternion rotation = quaternion.RotateZ(angle);
            float3 scale = new(distance, lineVisual.Thickness, 1f);
            localToWorld.Value = float4x4.TRS(midpoint, rotation, scale);
        }
        else
        {
            localToWorld.Value = float4x4.Scale(0f);
        }
    }
}

[BurstCompile]
public partial struct UpdateMovementTargetProjectionJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<SelectedTag> SelectedLookup;
    [ReadOnly] public ComponentLookup<MoveCommandProjection> MoveCommandProjectionLookup;

    private void Execute(
        ref LocalTransform transform,
        in MovementTargetProjectionVisualComponent movementTargetProjection,
        in ShipReference shipRef)
    {
        if (shipRef.Value == Entity.Null
            || !SelectedLookup.HasComponent(shipRef.Value)
            || !MoveCommandProjectionLookup.HasComponent(shipRef.Value))
        {
            transform.Scale = 0f;
            return;
        }

        bool isSelected = SelectedLookup.IsComponentEnabled(shipRef.Value);
        bool isMoving = MoveCommandProjectionLookup.IsComponentEnabled(shipRef.Value);

        if (isSelected && isMoving)
        {
            var command = MoveCommandProjectionLookup[shipRef.Value];
            transform.Position = new float3(command.MoveTarget, 0f);
            transform.Rotation = command.TargetRotation;
            transform.Scale = 1.0f;
        }
        else
        {
            transform.Scale = 0f;
        }
    }
}

[BurstCompile]
public partial struct UpdateMovementTargetJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<SelectedTag> SelectedLookup;
    [ReadOnly] public ComponentLookup<MoveCommand> MoveCommandLookup;

    private void Execute(
        ref LocalTransform transform,
        in MovementTargetVisualComponent movementTarget,
        in ShipReference shipRef)
    {
        if (shipRef.Value == Entity.Null
            || !SelectedLookup.HasComponent(shipRef.Value)
            || !MoveCommandLookup.HasComponent(shipRef.Value))
        {
            transform.Scale = 0f;
            return;
        }

        bool isSelected = SelectedLookup.IsComponentEnabled(shipRef.Value);
        bool isMoving = MoveCommandLookup.IsComponentEnabled(shipRef.Value);

        if (isSelected && isMoving)
        {
            var command = MoveCommandLookup[shipRef.Value];
            transform.Position = new float3(command.MoveTarget, 0f);
            transform.Rotation = command.TargetRotation;
            transform.Scale = 1.0f;
        }
        else
        {
            transform.Scale = 0f;
        }
    }
}