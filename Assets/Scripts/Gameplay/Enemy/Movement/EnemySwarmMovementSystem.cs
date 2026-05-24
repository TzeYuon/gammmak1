using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

partial struct EnemySwarmMovementSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (GridManager.Instance == null || FlowFieldManager.Instance == null) return;
        if (!GridManager.Instance.nativeGrid.IsCreated) return;
        if (!FlowFieldManager.Instance.flowField.IsCreated) return;

        var input = SystemAPI.GetSingleton<PlayerInputData>();

        var moveJob = new FlowFieldMovementJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,

            FlowField = FlowFieldManager.Instance.flowField,
            Grid = GridManager.Instance.nativeGrid,
            GridWidth = GridManager.Instance.width,
            GridHeight = GridManager.Instance.height,
            WorldOrigin = new int2(GridManager.Instance.worldOrigin.x, GridManager.Instance.worldOrigin.y),
            MousePos = input.MouseWorldPosition

        };
        state.Dependency = moveJob.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(SwarmTag))]
public partial struct FlowFieldMovementJob : IJobEntity
{
    public float DeltaTime;

    [ReadOnly] public NativeArray<float2> FlowField;
    [ReadOnly] public NativeArray<int> Grid;
    public int GridWidth;
    public int GridHeight;
    public int2 WorldOrigin;
    public float2 MousePos;

    public void Execute(Entity entity, ref LocalTransform transform, ref VelocityComponent velocity)
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
            targetDir = math.normalize(MousePos - curPos.xy);
        }

        float2 wallPushForce = float2.zero;

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                int checkX = gridX + x;
                int checkY = gridY + y;

                if (checkX >= 0 && checkX < GridWidth && checkY >= 0 && checkY < GridHeight)
                {
                    if (Grid[checkX + checkY * GridWidth] == 1) // Only walls push!
                    {
                        float2 wallCenter = new float2(checkX + WorldOrigin.x, checkY + WorldOrigin.y);
                        float2 pushDir = curPos.xy - wallCenter;
                        float distSq = math.lengthsq(pushDir);

                        if (distSq < 1.0f && distSq > 0.01f)
                        {
                            float distance = math.sqrt(distSq);

                            float pushStrength = (1.1f - distance) * 15.0f;
                            float2 pushNormal = pushDir / distance;

                            float2 slideTangent = new float2(-pushNormal.y, pushNormal.x);

                            wallPushForce += (pushNormal * pushStrength) + (slideTangent * pushStrength);
                        }
                    }
                }
            }
        }

        float2 steeringForce = float2.zero;
        float baseSpeed = 5.0f; // Replace with Hive speed value

        if (math.lengthsq(targetDir) > 0.1f)
        {
            float2 desiredVelocity = targetDir * baseSpeed;
            steeringForce = (desiredVelocity - curVel) + wallPushForce;
        }
        else
        {
            steeringForce = (-curVel * 5.0f) + wallPushForce;
        }

        float2 newVel = curVel + (steeringForce * DeltaTime);

        if (math.lengthsq(newVel) > baseSpeed * baseSpeed * 1.5f)
        {
            newVel = math.normalize(newVel) * baseSpeed * 1.2f;
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