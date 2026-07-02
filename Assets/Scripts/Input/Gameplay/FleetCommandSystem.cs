using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
[UpdateInGroup(typeof(CommandAndSpawnGroup))]
partial struct FleetCommandSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputData>();
    }

    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ref var input = ref SystemAPI.GetSingletonRW<PlayerInputData>().ValueRW;
        float2 mousePos = input.MouseWorldPosition;

        var obstacleGridComponent = SystemAPI.GetSingleton<TerrainGridComponent>();
        NativeArray<int> obstacleGrid = obstacleGridComponent.Grid;
        int2 worldOrigin = obstacleGridComponent.WorldOrigin;
        int gridWidth = obstacleGridComponent.GridWidth;
        int gridHeight = obstacleGridComponent.GridHeight;
        float cellSize = obstacleGridComponent.CellSize;

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

        if (input.WasPressed(PlayerAction.Secondary) || input.IsHeld(PlayerAction.Secondary))
        {
            // place move target projection
            foreach (var (_, transform, entity) in SystemAPI.Query<RefRO<PlayerTag>, RefRO<LocalTransform>>().WithAll<SelectedTag>().WithPresent<MoveCommandProjection>().WithEntityAccess())
            {
                SystemAPI.SetComponent(entity, new MoveCommandProjection
                {
                    MoveTarget = ValidateMoveTarget(transform.ValueRO.Position.xy, input.MoveCommandStartDragPosition),
                    UseTargetRotation = input.MoveCommandUseRotation,
                    TargetRotation = quaternion.RotateZ(input.MoveCommandTargetRotation),
                });
                SystemAPI.SetComponentEnabled<MoveCommandProjection>(entity, true);
            }
        }

        if (input.MoveCommandTriggered)
        {
            // Enables move command
            foreach (var (_, transform, entity) in SystemAPI.Query<RefRO<PlayerTag>, RefRO<LocalTransform>>().WithAll<SelectedTag>().WithPresent<MoveCommand>().WithEntityAccess())
            {
                SystemAPI.SetComponent(entity, new MoveCommand
                {
                    MoveTarget = ValidateMoveTarget(transform.ValueRO.Position.xy, input.MoveCommandStartDragPosition),
                    Recalculate = true,
                    ForcePath = false,

                    UseTargetRotation = input.MoveCommandUseRotation,
                    TargetRotation = quaternion.RotateZ(input.MoveCommandTargetRotation),
                });
                SystemAPI.SetComponentEnabled<MoveCommand>(entity, true);

                // disable projection
                SystemAPI.SetComponentEnabled<MoveCommandProjection>(entity, false);
            }

            input.MoveCommandTriggered = false;
        }

        Vector2 ValidateMoveTarget(Vector2 start, Vector2 target)
        {
            int2 targetCell = GridUtility.WorldPosToCell(target, worldOrigin, cellSize);

            // return immediately if target is valid
            if (GridUtility.IsWithinBounds(targetCell, gridWidth, gridHeight) 
                && GridUtility.IsTerrainPassable(target, obstacleGrid, worldOrigin, gridWidth, gridHeight, cellSize))
            {
                return target;
            }

            // find valid target within range
            float searchRadiusWorld = 5.0f;
            int searchRadiusCells = (int)math.ceil(searchRadiusWorld / cellSize);
            for (int r = 1; r <= searchRadiusCells; r++)
            {
                // Loop a bounding box for the current ring
                for (int x = -r; x <= r; x++)
                {
                    for (int y = -r; y <= r; y++)
                    {
                        if (math.abs(x) != r && math.abs(y) != r) continue;

                        int2 checkCell = targetCell + new int2(x, y);
                        float2 checkWorldPos = GridUtility.CellToWorldPosCorner(checkCell, worldOrigin, cellSize);

                        if (!GridUtility.IsWithinBounds(checkCell, gridWidth, gridHeight)) continue;
                        if (!GridUtility.IsTerrainPassable(checkWorldPos, obstacleGrid, worldOrigin, gridWidth, gridHeight, cellSize)) continue;

                        // found a target, return target immediately
                        return checkWorldPos;
                    }
                }
            }

            // no valid target found use raymarch
            return GridUtility.GetLastSafePointOnLine(start, target, obstacleGrid, worldOrigin, gridWidth, gridHeight, cellSize);
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