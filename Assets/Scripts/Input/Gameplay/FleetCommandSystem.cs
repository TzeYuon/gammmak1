using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
[UpdateInGroup(typeof(CommandAndSpawnGroup))]
partial class FleetCommandSystem : SystemBase
{
    bool moveStarted;
    float2 moveTarget;
    quaternion moveTargetRotation;
    NativeParallelHashMap<Entity, float2> assignedPositions;

    bool isSelecting;
    public static event Action<float2, float2> OnSelectionBoxChanged;

    [BurstCompile]
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerInputData>();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        var gameState = SystemAPI.GetSingletonRW<GameStateSingleton>();
        if (gameState.ValueRO.CurrentState != GameState.Gameplay) return;

        ref var input = ref SystemAPI.GetSingletonRW<PlayerInputData>().ValueRW;
        float2 mousePos = input.MouseWorldPosition;

        var obstacleGridComponent = SystemAPI.GetSingleton<TerrainGridComponent>();
        NativeArray<int> obstacleGrid = obstacleGridComponent.Grid;
        int2 worldOrigin = obstacleGridComponent.WorldOrigin;
        int gridWidth = obstacleGridComponent.GridWidth;
        int gridHeight = obstacleGridComponent.GridHeight;
        float cellSize = obstacleGridComponent.CellSize;

        // Handle ship selection
        if (input.WasPressed(PlayerAction.Primary))
        {
            isSelecting = true;
        }

        if (isSelecting)
        {
            if (input.WasPressed(PlayerAction.Cancel))
            {
                HideSelectionBox();
                input.ClearState(PlayerAction.Cancel);
            }
            else if (input.IsHeld(PlayerAction.Primary))
            {
                float2 startPos = input.MouseStartDragPosition;
                float2 min = math.min(mousePos, startPos);
                float2 max = math.max(mousePos, startPos);

                // update box
                OnSelectionBoxChanged?.Invoke(min, max);
            }
            else if (input.WasReleased(PlayerAction.Primary))
            {
                float2 startPos = input.MouseStartDragPosition;
                float2 min = math.min(mousePos, startPos);
                float2 max = math.max(mousePos, startPos);
                Dependency = new CheckSelectionBoxJob
                {
                    Min = min,
                    Max = max,
                    AddToSelection = input.IsHeld(PlayerAction.SelectionModifier)
                }.ScheduleParallel(Dependency);

                HideSelectionBox();
            }
            else
            {
                HideSelectionBox();
            }
        }
        else
        {
            HideSelectionBox();
        }

        Dependency.Complete();

        // Handle ship move command
        var selectedQuery = SystemAPI.QueryBuilder().WithAll<SelectedTag, PlayerTag>().WithDisabled<DeadTag>().Build();
        int selectedCount = selectedQuery.CalculateEntityCount();
        if (selectedCount == 0)
        {
            // no ships can be assigned to move, cancel move
            moveStarted = false;
            Dependency = new HideProjectionJob().ScheduleParallel(Dependency);
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

            var selectedEntities = selectedQuery.ToEntityArray(Allocator.Temp);
            var avoidanceLookup = SystemAPI.GetComponentLookup<ShipMovementStatsComponent>(true);

            // collect all selected ships data
            var placementData = new NativeArray<ShipPlacementData>(selectedCount, Allocator.Temp);
            for (int i = 0; i < selectedCount; i++)
            {
                Entity ship = selectedEntities[i];

                float radius = 1.0f;
                float2 offset = 0;
                if (avoidanceLookup.HasComponent(ship)) 
                {
                    radius = avoidanceLookup[ship].AvoidanceRadius;
                    offset = avoidanceLookup[ship].AvoidanceOffset;
                }

                placementData[i] = new ShipPlacementData
                {
                    ShipEntity = ship,
                    Radius = radius,
                    LocalOffset = offset,
                };
            }

            // sort by size
            placementData.Sort();

            // generate positions in spiral
            float shipSpacing = 0f;
            var generatedPositions = GenerateFormation(
                selectedCount,
                moveTarget,
                placementData,
                shipSpacing,
                obstacleGrid, worldOrigin, gridWidth, gridHeight, cellSize,
                Allocator.Temp
            );

            // clear previous assigned position if any
            if (assignedPositions.IsCreated) assignedPositions.Dispose();
            assignedPositions = new NativeParallelHashMap<Entity, float2>(selectedCount, Allocator.Persistent);

            // place positions into hashmap
            for (int i = 0; i < selectedCount; i++)
            {
                float2 finalPos = generatedPositions[i] - placementData[i].LocalOffset;
                assignedPositions.Add(placementData[i].ShipEntity, finalPos);
            }

            // cleanup temp arrays
            placementData.Dispose();
            generatedPositions.Dispose();
            selectedEntities.Dispose();
        }

        if (!moveStarted) return;

        // cancel move command
        if (input.WasPressed(PlayerAction.Cancel))
        {
            moveStarted = false;
            input.ClearState(PlayerAction.Cancel);

            var cancelJob = new CancelMoveCommandJob();
            Dependency = cancelJob.ScheduleParallel(Dependency);
            return;
        }

        // Check if player wants to add desired rotation
        Vector2 drag = mousePos - moveTarget;
        bool useRotation = math.lengthsq(drag) > 0.25f;

        // set desired rotation
        if (useRotation) moveTargetRotation = quaternion.RotateZ(math.atan2(drag.y, drag.x) - (math.PI / 2f));

        if (input.WasReleased(PlayerAction.Secondary)) // move command triggered
        {
            var moveJob = new AssignMoveCommandJob
            {
                AssignedPositions = assignedPositions,
                UseRotation = useRotation,
                TargetRotation = moveTargetRotation
            };
            Dependency = moveJob.ScheduleParallel(Dependency);

            // dispose valid positions array
            assignedPositions.Dispose(Dependency);

            moveStarted = false;
        }
        else if (input.WasPressed(PlayerAction.Secondary) || input.IsHeld(PlayerAction.Secondary)) // not yet released right click
        {
            var projectionJob = new AssignProjectionJob
            {
                AssignedPositions = assignedPositions,
                UseRotation = useRotation,
                TargetRotation = moveTargetRotation
            };
            Dependency = projectionJob.ScheduleParallel(Dependency);
        }

        static NativeArray<float2> GenerateFormation(
            int totalCount,
            float2 center,
            NativeArray<ShipPlacementData> shipRadii,
            float padding,
            NativeArray<int> obstacleGrid,
            int2 worldOrigin,
            int gridWidth,
            int gridHeight,
            float obstacleCellSize,
            Allocator allocator)
        {
            var validPositions = new NativeArray<float2>(totalCount, allocator);
            float sweepResolution = 0.5f; // how much to step outwards per ring
            for (int i = 0; i < totalCount; i++)
            {
                float currentShipRadius = shipRadii[i].Radius;
                bool placed = false;

                float currentRadius = 0f;

                int radiusAttempts = 0;
                int maxRadiusAttempts = 200;

                while (!placed && radiusAttempts < maxRadiusAttempts)
                {
                    if (currentRadius == 0f)
                    {
                        // check center
                        if (IsValidSpot(
                            center, 
                            shipRadii,
                            currentShipRadius, 
                            padding, 
                            validPositions, 
                            i, 
                            obstacleGrid, 
                            worldOrigin, 
                            gridWidth, 
                            gridHeight, 
                            obstacleCellSize))
                        {
                            validPositions[i] = center;
                            placed = true;
                            break;
                        }
                        currentRadius += sweepResolution;
                        radiusAttempts++;
                        continue;
                    }

                    // check entire circumference at current radius
                    float circumference = 2f * math.PI * currentRadius;
                    int angleSteps = (int)math.ceil(circumference / sweepResolution);
                    float angleStep = (2f * math.PI) / angleSteps;

                    for (int a = 0; a < angleSteps; a++)
                    {
                        float currentAngle = a * angleStep;
                        float2 offset = new float2(math.cos(currentAngle), math.sin(currentAngle)) * currentRadius;
                        float2 testPos = center + offset;

                        if (IsValidSpot(
                            testPos,
                            shipRadii,
                            currentShipRadius, 
                            padding, 
                            validPositions, 
                            i, 
                            obstacleGrid,
                            worldOrigin, 
                            gridWidth, 
                            gridHeight, 
                            obstacleCellSize))
                        {
                            validPositions[i] = testPos;
                            placed = true;
                            break;
                        }
                    }

                    // step outwards by sweep resolution
                    currentRadius += sweepResolution;
                    radiusAttempts++;
                }

                if (!placed)
                {
                    // fallback to use previously assigned position
                    validPositions[i] = validPositions[i - 1];
                }
            }

            return validPositions;
        }

        static bool IsValidSpot(
            float2 testPos,
            NativeArray<ShipPlacementData> shipRadii,
            float shipRadius, 
            float padding,
            NativeArray<float2> placedPositions, 
            int placedCount,
            NativeArray<int> obstacleGrid, 
            int2 worldOrigin, 
            int gridWidth, 
            int gridHeight, 
            float obstacleCellSize)
        {
            float requiredSpace = shipRadius + padding;

            // check overlap with other ships
            for (int j = 0; j < placedCount; j++)
            {
                float distSq = math.distancesq(testPos, placedPositions[j]);
                float minSafeDist = requiredSpace + shipRadii[j].Radius;
                if (distSq < minSafeDist * minSafeDist)
                    return false;
            }

            // Check terrain and bounds in radius
            int radiusCells = (int)math.ceil(requiredSpace / obstacleCellSize);
            int2 centerCell = GridUtility.WorldPosToCell(testPos, worldOrigin, obstacleCellSize);
            for (int x = -radiusCells; x <= radiusCells; x++)
            {
                for (int y = -radiusCells; y <= radiusCells; y++)
                {
                    int2 checkCell = centerCell + new int2(x, y);

                    if (!GridUtility.IsWithinBounds(checkCell, gridWidth, gridHeight))
                        return false; // out of bounds

                    if (!GridUtility.IsTerrainPassable(checkCell, obstacleGrid, gridWidth, gridHeight))
                        return false; // hit wall
                }
            }

            return true;
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

        void HideSelectionBox()
        {
            isSelecting = false;
            // clear selection box
            OnSelectionBoxChanged?.Invoke(0, 0);
        }
    }

    [BurstCompile]
    protected override void OnDestroy()
    {
        if (assignedPositions.IsCreated) assignedPositions.Dispose();
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

public struct ShipPlacementData : IComparable<ShipPlacementData>
{
    public Entity ShipEntity;
    public float Radius;
    public float2 LocalOffset;

    public int CompareTo(ShipPlacementData other)
    {
        return other.Radius.CompareTo(this.Radius);
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
    [ReadOnly] public NativeParallelHashMap<Entity, float2> AssignedPositions;
    public bool UseRotation;
    public quaternion TargetRotation;

    private void Execute(
        Entity entity,
        ref MoveCommandProjection projection,
        EnabledRefRW<MoveCommandProjection> projectionEnabled)
    {

        if (AssignedPositions.TryGetValue(entity, out float2 assignedPos))
        {
            projection.MoveTarget = assignedPos;
            projection.UseTargetRotation = UseRotation;
            projection.TargetRotation = TargetRotation;

            projectionEnabled.ValueRW = true;
        }
    }
}

[BurstCompile]
[WithAll(typeof(PlayerTag), typeof(SelectedTag))]
[WithDisabled(typeof(DeadTag))]
[WithPresent(typeof(MoveCommand))]
public partial struct AssignMoveCommandJob : IJobEntity
{
    [ReadOnly] public NativeParallelHashMap<Entity, float2> AssignedPositions;
    public bool UseRotation;
    public quaternion TargetRotation;

    private void Execute(
        Entity entity,
        ref MoveCommand command,
        EnabledRefRW<MoveCommand> commandEnabled,
        EnabledRefRW<MoveCommandProjection> projectionEnabled)
    {
        if (AssignedPositions.TryGetValue(entity, out float2 assignedPos))
        {
            command.MoveTarget = assignedPos;
            command.Recalculate = true;
            command.ForcePath = false;
            command.UseTargetRotation = UseRotation;
            command.TargetRotation = TargetRotation;

            commandEnabled.ValueRW = true;
            projectionEnabled.ValueRW = false;
        }
    }
}

[BurstCompile]
[WithAll(typeof(PlayerTag), typeof(SelectedTag))]
[WithDisabled(typeof(DeadTag))]
[WithPresent(typeof(MoveCommandProjection))]
public partial struct CancelMoveCommandJob : IJobEntity
{
    private void Execute(
        EnabledRefRW<MoveCommandProjection> projectionEnabled)
    {
        projectionEnabled.ValueRW = false;
    }
}