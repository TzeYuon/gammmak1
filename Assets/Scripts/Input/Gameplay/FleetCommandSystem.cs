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
    bool moveStarted;
    float2 moveTarget;
    quaternion moveTargetRotation;
    NativeArray<float2> validPositions;

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

        // Handle ship selection
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

        state.Dependency.Complete();

        // Handle ship move command
        int selectedCount = SystemAPI.QueryBuilder().WithAll<SelectedTag, PlayerTag>().Build().CalculateEntityCount();
        if (selectedCount == 0)
        {
            // no ships can be assigned to move, cancel move
            moveStarted = false;
            state.Dependency = new HideProjectionJob().ScheduleParallel(state.Dependency);
            return;
        }

        // Start move command
        if (input.WasPressed(PlayerAction.Secondary))
        {
            // get center of all selected ships
            float3 centerOfMass = float3.zero;
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag, SelectedTag>().WithDisabled<DeadTag>())
            {
                centerOfMass += transform.ValueRO.Position;
            }
            centerOfMass /= selectedCount;

            // find a target position that is reachable
            moveStarted = true;
            moveTarget = ValidateMoveTarget(centerOfMass.xy, math.round(mousePos));

            // create positions to assign to ships when move command is given
            float shipSpacing = 1f;
            if (validPositions.IsCreated) validPositions.Dispose();
            validPositions = GenerateBFSFormationPositions(
                selectedCount,
                moveTarget,
                shipSpacing,
                obstacleGrid, worldOrigin, gridWidth, gridHeight, cellSize,
                Allocator.Persistent
            );
        }

        if (!moveStarted) return;

        // Check if player wants to add desired rotation
        Vector2 drag = mousePos - moveTarget;
        bool useRotation = math.lengthsq(drag) > 0.25f;

        // set desired rotation
        if (useRotation) moveTargetRotation = quaternion.RotateZ(math.atan2(drag.y, drag.x) - (math.PI / 2f));

        if (input.WasReleased(PlayerAction.Secondary) && moveStarted) // move command triggered
        {
            var moveJob = new AssignMoveCommandJob
            {
                ValidPositions = validPositions,
                UseRotation = useRotation,
                TargetRotation = moveTargetRotation
            };
            state.Dependency = moveJob.ScheduleParallel(state.Dependency);

            // dispose valid positions array
            validPositions.Dispose(state.Dependency);

            moveStarted = false;
        }
        else if (input.WasPressed(PlayerAction.Secondary) || input.IsHeld(PlayerAction.Secondary)) // not yet released right click
        {
            var projectionJob = new AssignProjectionJob
            {
                ValidPositions = validPositions,
                UseRotation = useRotation,
                TargetRotation = moveTargetRotation
            };
            state.Dependency = projectionJob.ScheduleParallel(state.Dependency);
        }

        static NativeArray<float2> GenerateBFSFormationPositions(
            int totalCount,
            float2 center,
            float shipSpacing,
            NativeArray<int> obstacleGrid,
            int2 worldOrigin,
            int gridWidth,
            int gridHeight,
            float obstacleCellSize,
            Allocator allocator)
        {
            var validPositions = new NativeArray<float2>(totalCount, allocator);

            var queue = new NativeQueue<int2>(Allocator.Temp);
            var visited = new NativeHashSet<int2>(totalCount * 4, Allocator.Temp);

            queue.Enqueue(int2.zero);
            visited.Add(int2.zero);

            int foundCount = 0;

            NativeArray<int2> directions = new NativeArray<int2>(4, Allocator.Temp);
            directions[0] = new int2(1, 0);
            directions[1] = new int2(-1, 0);
            directions[2] = new int2(0, 1);
            directions[3] = new int2(0, -1);

            while (queue.Count > 0 && foundCount < totalCount)
            {
                int2 virtualCoord = queue.Dequeue();

                float2 offset = new float2(virtualCoord.x * shipSpacing, virtualCoord.y * shipSpacing);

                float2 worldPos = center + offset;

                int2 obstacleCell = GridUtility.WorldPosToCell(worldPos, worldOrigin, obstacleCellSize);

                bool isSafe = GridUtility.IsWithinBounds(obstacleCell, gridWidth, gridHeight) &&
                              GridUtility.IsTerrainPassable(worldPos, obstacleGrid, worldOrigin, gridWidth, gridHeight, obstacleCellSize);

                if (isSafe)
                {
                    validPositions[foundCount] = worldPos;
                    foundCount++;

                    for (int i = 0; i < 4; i++)
                    {
                        int2 neighbor = virtualCoord + directions[i];
                        if (visited.Add(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            for (int i = foundCount; i < totalCount; i++)
            {
                validPositions[i] = foundCount > 0 ? validPositions[foundCount - 1] : center;
            }

            // Cleanup
            directions.Dispose();
            queue.Dispose();
            visited.Dispose();

            return validPositions;
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
        if (validPositions.IsCreated) validPositions.Dispose();
    }
}

[BurstCompile]
[WithAll(typeof(PlayerTag))]
[WithDisabled(typeof(DeadTag))]
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

[BurstCompile]
[WithAll(typeof(PlayerTag))]
[WithPresent(typeof(MoveCommandProjection))]
[WithDisabled(typeof(DeadTag))]
public partial struct HideProjectionJob : IJobEntity
{
    private void Execute(
        EnabledRefRW<MoveCommandProjection> projection)
    {
        projection.ValueRW = false;
    }
}

[BurstCompile]
[WithAll(typeof(PlayerTag), typeof(SelectedTag))]
[WithDisabled(typeof(DeadTag))]
[WithPresent(typeof(MoveCommandProjection))]
public partial struct AssignProjectionJob : IJobEntity
{
    [ReadOnly] public NativeArray<float2> ValidPositions;
    public bool UseRotation;
    public quaternion TargetRotation;

    private void Execute(
        [EntityIndexInQuery] int index,
        ref MoveCommandProjection projection,
        EnabledRefRW<MoveCommandProjection> projectionEnabled)
    {
        // safety check
        if (index >= ValidPositions.Length) return;

        projection.MoveTarget = ValidPositions[index];
        projection.UseTargetRotation = UseRotation;
        projection.TargetRotation = TargetRotation;

        projectionEnabled.ValueRW = true;
    }
}

[BurstCompile]
[WithAll(typeof(PlayerTag), typeof(SelectedTag))]
[WithDisabled(typeof(DeadTag))]
[WithPresent(typeof(MoveCommand))]
public partial struct AssignMoveCommandJob : IJobEntity
{
    [ReadOnly] public NativeArray<float2> ValidPositions;
    public bool UseRotation;
    public quaternion TargetRotation;

    private void Execute(
        [EntityIndexInQuery] int index,
        ref MoveCommand command,
        EnabledRefRW<MoveCommand> commandEnabled,
        EnabledRefRW<MoveCommandProjection> projectionEnabled)
    {
        if (index >= ValidPositions.Length) return;

        command.MoveTarget = ValidPositions[index];
        command.Recalculate = true;
        command.ForcePath = false;
        command.UseTargetRotation = UseRotation;
        command.TargetRotation = TargetRotation;

        commandEnabled.ValueRW = true;
        projectionEnabled.ValueRW = false;
    }
}