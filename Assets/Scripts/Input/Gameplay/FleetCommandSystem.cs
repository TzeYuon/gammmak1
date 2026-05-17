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

        if (input.WasPressed(PlayerAction.Primary))
        {
            // Select ships nearby to click
            float selectionRadiusSq = 25f;
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>().WithDisabled<DeadTag>().WithEntityAccess())
            {
                float distSq = math.distancesq(transform.ValueRO.Position.xy, mousePos);

                if (distSq <= selectionRadiusSq)
                {
                    SystemAPI.SetComponentEnabled<SelectedTag>(entity, true);
                }
                else if (!input.IsHeld(PlayerAction.SelectionModifier))
                {
                    SystemAPI.SetComponentEnabled<SelectedTag>(entity, false);
                }
            }
        }

        if (input.WasPressed(PlayerAction.Secondary))
        {
            // Enables move command
            foreach (var (_, entity) in SystemAPI.Query<RefRO<PlayerTag>>().WithAll<SelectedTag>().WithPresent<MoveCommand>().WithEntityAccess())
            {
                SystemAPI.SetComponent(entity, new MoveCommand { MoveTarget = mousePos });
                SystemAPI.SetComponentEnabled<MoveCommand>(entity, true);
            }
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
