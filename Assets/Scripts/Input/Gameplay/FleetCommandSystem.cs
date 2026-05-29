using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
[UpdateInGroup(typeof(PlayerInputSystemGroup))]
partial struct FleetCommandSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var input = SystemAPI.GetSingleton<PlayerInputData>();
        float2 mousePos = input.MouseWorldPosition;

        if (input.WasReleased(PlayerAction.Primary))
        {
            float2 startPos = input.MouseStartDragPosition;
            state.Dependency = new CheckSelectionBoxJob
            {
                Min = math.min(mousePos, startPos),
                Max = math.max(mousePos, startPos),
                AddToSelection = input.IsHeld(PlayerAction.SelectionModifier)
            }.ScheduleParallel(state.Dependency);
        }

        if (input.WasPressed(PlayerAction.Secondary))
        {
            // Enables move command
            foreach (var (_, entity) in SystemAPI.Query<RefRO<PlayerTag>>().WithAll<SelectedTag>().WithPresent<MoveCommand>().WithEntityAccess())
            {
                SystemAPI.SetComponent(entity, new MoveCommand
                {
                    MoveTarget = mousePos,
                    Recalculate = true,
                    ForcePath = false
                });
                SystemAPI.SetComponentEnabled<MoveCommand>(entity, true);
            }
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
[WithAll(typeof(PlayerTag))]
[WithAbsent(typeof(MothershipTag))]
[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
public partial struct CheckSelectionBoxJob : IJobEntity
{
    public float2 Min;
    public float2 Max;
    public bool AddToSelection;

    private void Execute(
        in LocalTransform transform, 
        EnabledRefRW<SelectedTag> selected
        )
    {
        float2 pos = transform.Position.xy;
        bool inBox = pos.x >= Min.x && pos.x <= Max.x &&
                     pos.y >= Min.y && pos.y <= Max.y;

        if (inBox) selected.ValueRW = true;
        else if (!AddToSelection) selected.ValueRW = false;
    }
}