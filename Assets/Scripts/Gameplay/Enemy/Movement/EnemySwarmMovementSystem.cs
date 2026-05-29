using System.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

partial struct EnemySwarmMovementSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (GridManager.Instance == null) return;
        if (!GridManager.Instance.nativeGrid.IsCreated || !GridManager.Instance.nativeFlowField.IsCreated) return;

        FineEnemyHeatmapComponent fineEnemyHeatmap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();

        var moveJob = new FlowFieldMovementJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ElapsedTime = (float)SystemAPI.Time.ElapsedTime,

            FlowField = GridManager.Instance.nativeFlowField,
            Grid = GridManager.Instance.nativeGrid,
            GridWidth = GridManager.Instance.width,
            GridHeight = GridManager.Instance.height,
            WorldOrigin = new int2(GridManager.Instance.worldOrigin.x, GridManager.Instance.worldOrigin.y),

            Heatmap = fineEnemyHeatmap.DensityMap,
            HeatmapCellSize = fineEnemyHeatmap.CellSize,
            HeatmapGridWidth = fineEnemyHeatmap.GridWidth,
            HeatmapGridHeight = fineEnemyHeatmap.GridHeight,
        };

        state.Dependency = moveJob.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(SwarmTag))]
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
    public float HeatmapCellSize;
    public int HeatmapGridWidth;
    public int HeatmapGridHeight;

    public void Execute(Entity entity, ref LocalTransform transform, ref VelocityComponent velocity, ref SwarmComponent agentData)
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
                                curVel -= pushNormal * velIntoWall;
                            }
                        }

                        if (distSq < 1.44f && distSq > 0.01f)
                        {
                            float distance = math.sqrt(distSq);

                            float pushStrength = (1.2f - distance) * 20.0f;

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
        float separationWeight = 0.5f;
        if (GridUtility.IsWithinBounds(curPos.xy, HeatmapCellSize, HeatmapGridWidth, HeatmapGridHeight))
        {
            int heatmapIndex = GridUtility.WorldPosToIndex(curPos, HeatmapCellSize, HeatmapGridWidth);
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

        int currentTileType = 0;

        if (gridX >= 0 && gridX < GridWidth && gridY >= 0 && gridY < GridHeight)
        {
            currentTileType = Grid[gridX + gridY * GridWidth];
        }

        if (currentTileType == 2) baseSpeed *= 0.5f; // Mud
        if (currentTileType == 3) baseSpeed *= 0.2f; // Poison

        if (math.lengthsq(targetDir) > 0.01f)
        {
            float2 noiseInput = new float2(ElapsedTime * 2f, entity.Index);
            float noiseValue = noise.snoise(noiseInput);

            float3 sideways3D = math.cross(new float3(math.normalizesafe(targetDir), 0), new float3(0, 0, 1));
            float2 sidewaysDir = sideways3D.xy;

            float2 wobblyTargetDir = math.normalize(targetDir + (1 * noiseValue * sidewaysDir));

            float uniqueSpeedVariation = 1.0f - ((agentData.WiggleOffset % 10) * 0.02f);
            float finalDesiredSpeed = baseSpeed * uniqueSpeedVariation;

            float2 desiredVelocity = wobblyTargetDir * finalDesiredSpeed;

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