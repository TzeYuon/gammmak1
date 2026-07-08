using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(ActionAndMovementGroup))]
partial struct EnemySwarmMovementSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
        state.RequireForUpdate<TerrainGridComponent>();
        state.RequireForUpdate<EnemyFlowFieldComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<TerrainGridComponent>(out var terrainGrid)) return;
        if (!SystemAPI.TryGetSingleton<EnemyFlowFieldComponent>(out var flowFieldComp)) return;
        if (!SystemAPI.TryGetSingleton<FineEnemyHeatmapComponent>(out var fineEnemyHeatmap)) return;
        if (!terrainGrid.Grid.IsCreated || !flowFieldComp.FlowField.IsCreated) return;

        var pathLookup = SystemAPI.GetBufferLookup<SiegePathWaypointElement>();
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var moveJob = new FlowFieldMovementJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ElapsedTime = (float)SystemAPI.Time.ElapsedTime,

            FlowField = flowFieldComp.FlowField,
            Grid = terrainGrid.Grid,
            GridWidth = terrainGrid.GridWidth,
            GridHeight = terrainGrid.GridHeight,
            WorldOrigin = terrainGrid.WorldOrigin,

            Heatmap = fineEnemyHeatmap.DensityMap,
            HeatmapWorldOrigin = fineEnemyHeatmap.WorldOrigin,
            HeatmapCellSize = fineEnemyHeatmap.CellSize,
            HeatmapGridWidth = fineEnemyHeatmap.GridWidth,
            HeatmapGridHeight = fineEnemyHeatmap.GridHeight,
        };
        state.Dependency = moveJob.ScheduleParallel(state.Dependency);

        var siegeMoveJob = new SiegePathMovementJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ElapsedTime = (float)SystemAPI.Time.ElapsedTime,

            Grid = terrainGrid.Grid,
            GridWidth = terrainGrid.GridWidth,
            GridHeight = terrainGrid.GridHeight,
            WorldOrigin = terrainGrid.WorldOrigin,

            Heatmap = fineEnemyHeatmap.DensityMap,
            HeatmapWorldOrigin = fineEnemyHeatmap.WorldOrigin,
            HeatmapCellSize = fineEnemyHeatmap.CellSize,
            HeatmapGridWidth = fineEnemyHeatmap.GridWidth,
            HeatmapGridHeight = fineEnemyHeatmap.GridHeight,

            PathLookup = pathLookup,
            ECB = ecb
        };
        state.Dependency = siegeMoveJob.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(SwarmTag))]
[WithAbsent(typeof(PathFollowerComponent))]
public partial struct FlowFieldMovementJob : IJobEntity
{
    public float DeltaTime;
    public float ElapsedTime;

    [ReadOnly] public NativeArray<float2> FlowField;
    [ReadOnly] public NativeArray<int> Grid;
    public int GridWidth;
    public int GridHeight;
    public int2 WorldOrigin;

    [ReadOnly] public NativeArray<int> Heatmap;
    public int2 HeatmapWorldOrigin;
    public float HeatmapCellSize;
    public int HeatmapGridWidth;
    public int HeatmapGridHeight;

    public void Execute(
        Entity entity, 
        ref LocalTransform transform, 
        ref SwarmVelocityComponent velocity, 
        in SwarmMovementStatsComponent agentData
        )
    {
        float3 curPos = transform.Position;
        float2 curVel = velocity.Value;

        int gridX = (int)math.round(curPos.x) - WorldOrigin.x;
        int gridY = (int)math.round(curPos.y) - WorldOrigin.y;

        float2 targetDir = float2.zero;

        if (gridX >= 0 && gridX < GridWidth && gridY >= 0 && gridY < GridHeight)
        {
            targetDir = FlowField[gridX + gridY * GridWidth];
        }
        else
        {
            float2 mapCenter = new float2(GridWidth / 2.0f, GridHeight / 2.0f) + WorldOrigin;
            targetDir = math.normalize(mapCenter - curPos.xy);
        }

        if (math.lengthsq(targetDir) < 0.01f)
        {
            float2 tileCenter = new float2(gridX + WorldOrigin.x, gridY + WorldOrigin.y);

            float2 noiseOffset = new float2(
                noise.snoise(new float2(ElapsedTime * 5f, entity.Index)),
                noise.snoise(new float2(ElapsedTime * 5f, entity.Index + 100))
            );

            targetDir = (tileCenter - curPos.xy) + (noiseOffset * 0.5f);
        }

        // Terrain & Wall Pushing
        float terrainAvoidanceWeight = agentData.TerrainAvoidanceWeight;
        float2 terrainPushForce = float2.zero;
        float2 positionCorrection = float2.zero;

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                int checkX = gridX + x;
                int checkY = gridY + y;

                if (checkX >= 0 && checkX < GridWidth && checkY >= 0 && checkY < GridHeight)
                {
                    int tileType = Grid[checkX + checkY * GridWidth];

                    if (tileType == 1)
                    {
                        float2 tileCenter = new float2(checkX + WorldOrigin.x, checkY + WorldOrigin.y);
                        float2 pushDir = curPos.xy - tileCenter;
                        float distSq = math.lengthsq(pushDir);

                        // If swarm is really near a wall
                        if (distSq < 0.56f && distSq > 0.001f)
                        {
                            float distance = math.sqrt(distSq);
                            float2 pushNormal = pushDir / distance;

                            float overlap = 0.75f - distance;
                            positionCorrection += pushNormal * overlap;

                            float velIntoWall = math.dot(curVel, pushNormal);
                            if (velIntoWall < 0)
                            {
                                curVel -= pushNormal * velIntoWall * terrainAvoidanceWeight;
                            }
                        }

                        if (distSq < 1.44f && distSq > 0.01f)
                        {
                            float distance = math.sqrt(distSq);

                            float pushStrength = (1.2f - distance) * 20.0f * terrainAvoidanceWeight;

                            float2 pushNormal = pushDir / distance;
                            float2 slideTangent = new float2(-pushNormal.y, pushNormal.x);

                            terrainPushForce += (pushNormal * pushStrength) + (slideTangent * pushStrength);
                        }
                    }
                }
            }
        }

        // Separation calculation
        // Use heatmap to move entity away from highly dense areas
        float separationWeight = agentData.SeparationWeight;
        if (GridUtility.IsWithinBounds(curPos.xy, HeatmapWorldOrigin, HeatmapCellSize, HeatmapGridWidth, HeatmapGridHeight))
        {
            int heatmapIndex = GridUtility.WorldPosToIndex(curPos, HeatmapWorldOrigin, HeatmapCellSize, HeatmapGridWidth);
            int densityRight = GridUtility.IsWithinBounds(heatmapIndex + 1, HeatmapGridWidth, HeatmapGridHeight) ? Heatmap[heatmapIndex + 1] : 100;
            int densityLeft = GridUtility.IsWithinBounds(heatmapIndex - 1, HeatmapGridWidth, HeatmapGridHeight) ? Heatmap[heatmapIndex - 1] : 100;
            int densityUp = GridUtility.IsWithinBounds(heatmapIndex + HeatmapGridWidth, HeatmapGridWidth, HeatmapGridHeight) ? Heatmap[heatmapIndex + HeatmapGridWidth] : 100;
            int densityDown = GridUtility.IsWithinBounds(heatmapIndex - HeatmapGridWidth, HeatmapGridWidth, HeatmapGridHeight) ? Heatmap[heatmapIndex - HeatmapGridWidth] : 100;

            float gradientX = densityRight - densityLeft;
            float gradientY = densityUp - densityDown;

            // Point away from high density
            float2 separationDir = new float2(-gradientX, -gradientY);
            if (math.lengthsq(separationDir) > 0.01f)
            {
                separationDir = math.normalize(separationDir);
                targetDir += separationDir * separationWeight;
            }
        }

        // Steering & Swarm Wobble
        float2 steeringForce = float2.zero;
        float baseSpeed = agentData.BaseSpeed;
        float noiseWeight = agentData.NoiseWeight;

        int currentTileType = 0;

        if (gridX >= 0 && gridX < GridWidth && gridY >= 0 && gridY < GridHeight)
        {
            currentTileType = Grid[gridX + gridY * GridWidth];
        }

        if (currentTileType == (int)TerrainTileType.Mud) baseSpeed *= 0.5f;
        if (currentTileType == (int)TerrainTileType.Poison) baseSpeed *= 0.5f; 

        if (math.lengthsq(targetDir) > 0.01f)
        {
            float2 noiseInput = new float2(ElapsedTime * 2f, entity.Index);
            float noiseValue = noise.snoise(noiseInput);

            float3 sideways3D = math.cross(new float3(math.normalizesafe(targetDir), 0), new float3(0, 0, 1));
            float2 sidewaysDir = sideways3D.xy;

            float2 wobblyTargetDir = math.normalize(targetDir + (2 * noiseValue * sidewaysDir * noiseWeight));

            float2 desiredVelocity = wobblyTargetDir * baseSpeed;

            steeringForce = ((desiredVelocity + terrainPushForce) - curVel) * 3.0f;
        }
        else
        {
            steeringForce = (-curVel * 5.0f) + terrainPushForce;
        }

        // Apply Velocity
        float2 newVel = curVel + (steeringForce * DeltaTime);

        float currentSpeedSq = math.lengthsq(newVel);
        float absoluteMaxSpeed = baseSpeed * 1.2f;

        if (currentSpeedSq > absoluteMaxSpeed * absoluteMaxSpeed)
        {
            newVel = math.normalize(newVel) * absoluteMaxSpeed;
        }

        velocity.Value = newVel;
        transform.Position += math.float3(velocity.Value, 0) * DeltaTime;

        if (math.lengthsq(newVel) > 0.001f)
        {
            float angle = math.atan2(newVel.y, newVel.x) - math.PI / 2;
            transform.Rotation = quaternion.RotateZ(angle);
        }
    }
}

[BurstCompile]
[WithAll(typeof(SwarmTag))]
public partial struct SiegePathMovementJob : IJobEntity
{
    public float DeltaTime;
    public float ElapsedTime;

    [ReadOnly] public NativeArray<int> Grid;
    public int GridWidth;
    public int GridHeight;
    public int2 WorldOrigin;

    [ReadOnly] public NativeArray<int> Heatmap;
    public int2 HeatmapWorldOrigin;
    public float HeatmapCellSize;
    public int HeatmapGridWidth;
    public int HeatmapGridHeight;

    [ReadOnly] public BufferLookup<SiegePathWaypointElement> PathLookup;

    public EntityCommandBuffer.ParallelWriter ECB;

    public void Execute(
        Entity entity,
        [ChunkIndexInQuery] int chunkIndex,
        ref LocalTransform transform,
        ref SwarmVelocityComponent velocity,
        ref PathFollowerComponent follower,
        in SwarmMovementStatsComponent agentData
        )
    {
        float3 curPos = transform.Position;
        float2 curVel = velocity.Value;

        float baseSpeed = agentData.BaseSpeed;

        int gridX = (int)math.round(curPos.x) - WorldOrigin.x;
        int gridY = (int)math.round(curPos.y) - WorldOrigin.y;
        int currentTileType = 0;

        if (gridX >= 0 && gridX < GridWidth && gridY >= 0 && gridY < GridHeight)
        {
            currentTileType = Grid[gridX + gridY * GridWidth];
        }

        if (currentTileType == (int)TerrainTileType.Mud) baseSpeed *= 0.5f;
        if (currentTileType == (int)TerrainTileType.Poison) baseSpeed *= 0.5f;

        float2 targetDir = float2.zero;

        // Move along siege path
        if (PathLookup.HasBuffer(follower.TargetPath))
        {
            var path = PathLookup[follower.TargetPath];
            if (path.Length >= 2)
            {
                // interpolate between next two points
                follower.CurrentProgress += baseSpeed * DeltaTime;

                float maxProgress = path.Length - 1;
                if (follower.CurrentProgress >= maxProgress)
                {
                    follower.CurrentProgress = maxProgress;
                }

                int indexA = (int)math.floor(follower.CurrentProgress);
                int indexB = math.min(indexA + 1, path.Length - 1);
                float t = follower.CurrentProgress - indexA;

                float2 posA = path[indexA].Position;
                float2 posB = path[indexB].Position;
                float2 targetPos2D = math.lerp(posA, posB, t);

                // move in direction of path
                targetDir = targetPos2D - curPos.xy;

                if (math.lengthsq(targetDir) > 0.001f)
                {
                    targetDir = math.normalize(targetDir);
                }
            }
        }
        else
        {
            // fallback if hive is already destroyed
            // Set it to follow the flow field next frame
            ECB.RemoveComponent<PathFollowerComponent>(chunkIndex, entity);
            return;
        }

        if (math.lengthsq(targetDir) < 0.01f)
        {
            float2 tileCenter = new float2(gridX + WorldOrigin.x, gridY + WorldOrigin.y);

            float2 noiseOffset = new float2(
                noise.snoise(new float2(ElapsedTime * 5f, entity.Index)),
                noise.snoise(new float2(ElapsedTime * 5f, entity.Index + 100))
            );

            targetDir = (tileCenter - curPos.xy) + (noiseOffset * 0.5f);
        }

        ///
        /// BELOW IS COPIED FROM OTHER SWARM MOVEMENT JOB ABOVE
        ///

        // Terrain & Wall Pushing
        float terrainAvoidanceWeight = agentData.TerrainAvoidanceWeight;
        float2 terrainPushForce = float2.zero;
        float2 positionCorrection = float2.zero;

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                int checkX = gridX + x;
                int checkY = gridY + y;

                if (checkX >= 0 && checkX < GridWidth && checkY >= 0 && checkY < GridHeight)
                {
                    int tileType = Grid[checkX + checkY * GridWidth];

                    if (tileType == 1)
                    {
                        float2 tileCenter = new float2(checkX + WorldOrigin.x, checkY + WorldOrigin.y);
                        float2 pushDir = curPos.xy - tileCenter;
                        float distSq = math.lengthsq(pushDir);

                        // If swarm is really near a wall
                        if (distSq < 0.56f && distSq > 0.001f)
                        {
                            float distance = math.sqrt(distSq);
                            float2 pushNormal = pushDir / distance;

                            float overlap = 0.75f - distance;
                            positionCorrection += pushNormal * overlap;

                            float velIntoWall = math.dot(curVel, pushNormal);
                            if (velIntoWall < 0)
                            {
                                curVel -= pushNormal * velIntoWall * terrainAvoidanceWeight;
                            }
                        }

                        if (distSq < 1.44f && distSq > 0.01f)
                        {
                            float distance = math.sqrt(distSq);

                            float pushStrength = (1.2f - distance) * 20.0f * terrainAvoidanceWeight;

                            float2 pushNormal = pushDir / distance;
                            float2 slideTangent = new float2(-pushNormal.y, pushNormal.x);

                            terrainPushForce += (pushNormal * pushStrength) + (slideTangent * pushStrength);
                        }
                    }
                }
            }
        }

        // Separation calculation
        // Use heatmap to move entity away from highly dense areas
        float separationWeight = agentData.SeparationWeight;
        if (GridUtility.IsWithinBounds(curPos.xy, HeatmapWorldOrigin, HeatmapCellSize, HeatmapGridWidth, HeatmapGridHeight))
        {
            int heatmapIndex = GridUtility.WorldPosToIndex(curPos, HeatmapWorldOrigin, HeatmapCellSize, HeatmapGridWidth);
            int densityRight = GridUtility.IsWithinBounds(heatmapIndex + 1, HeatmapGridWidth, HeatmapGridHeight) ? Heatmap[heatmapIndex + 1] : 100;
            int densityLeft = GridUtility.IsWithinBounds(heatmapIndex - 1, HeatmapGridWidth, HeatmapGridHeight) ? Heatmap[heatmapIndex - 1] : 100;
            int densityUp = GridUtility.IsWithinBounds(heatmapIndex + HeatmapGridWidth, HeatmapGridWidth, HeatmapGridHeight) ? Heatmap[heatmapIndex + HeatmapGridWidth] : 100;
            int densityDown = GridUtility.IsWithinBounds(heatmapIndex - HeatmapGridWidth, HeatmapGridWidth, HeatmapGridHeight) ? Heatmap[heatmapIndex - HeatmapGridWidth] : 100;

            float gradientX = densityRight - densityLeft;
            float gradientY = densityUp - densityDown;

            // Point away from high density
            float2 separationDir = new float2(-gradientX, -gradientY);
            if (math.lengthsq(separationDir) > 0.01f)
            {
                separationDir = math.normalize(separationDir);
                targetDir += separationDir * separationWeight;
            }
        }

        // Steering & Swarm Wobble
        float2 steeringForce = float2.zero;
        float noiseWeight = agentData.NoiseWeight;

        if (gridX >= 0 && gridX < GridWidth && gridY >= 0 && gridY < GridHeight)
        {
            currentTileType = Grid[gridX + gridY * GridWidth];
        }

        if (currentTileType == (int)TerrainTileType.Mud) baseSpeed *= 0.5f;
        if (currentTileType == (int)TerrainTileType.Poison) baseSpeed *= 0.5f; 

        if (math.lengthsq(targetDir) > 0.01f)
        {
            float2 noiseInput = new float2(ElapsedTime * 2f, entity.Index);
            float noiseValue = noise.snoise(noiseInput);

            float3 sideways3D = math.cross(new float3(math.normalizesafe(targetDir), 0), new float3(0, 0, 1));
            float2 sidewaysDir = sideways3D.xy;

            float2 wobblyTargetDir = math.normalize(targetDir + (2 * noiseValue * sidewaysDir * noiseWeight));

            float2 desiredVelocity = wobblyTargetDir * baseSpeed;

            steeringForce = ((desiredVelocity + terrainPushForce) - curVel) * 3.0f;
        }
        else
        {
            steeringForce = (-curVel * 5.0f) + terrainPushForce;
        }

        // Apply Velocity
        float2 newVel = curVel + (steeringForce * DeltaTime);

        float currentSpeedSq = math.lengthsq(newVel);
        float absoluteMaxSpeed = baseSpeed * 1.2f;

        if (currentSpeedSq > absoluteMaxSpeed * absoluteMaxSpeed)
        {
            newVel = math.normalize(newVel) * absoluteMaxSpeed;
        }

        velocity.Value = newVel;
        transform.Position += math.float3(velocity.Value, 0) * DeltaTime;

        if (math.lengthsq(newVel) > 0.001f)
        {
            float angle = math.atan2(newVel.y, newVel.x) - math.PI / 2;
            transform.Rotation = quaternion.RotateZ(angle);
        }
    }
}