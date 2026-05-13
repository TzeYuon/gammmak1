using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

partial struct PathLeaderSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PathLeaderComponent>();
        state.RequireForUpdate<PlayerInputData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var input = SystemAPI.GetSingleton<PlayerInputData>();
        float deltaTime = SystemAPI.Time.DeltaTime;
        float2 mousePos = input.MouseWorldPosition;
        foreach (var (pathLeader, transform, buffer) in SystemAPI.Query<RefRW<PathLeaderComponent>, RefRW<LocalTransform>, DynamicBuffer<PathPosition>>())
        {
            float3 curPos = transform.ValueRO.Position;
            float2 targetDir = mousePos - curPos.xy;
            targetDir = math.normalizesafe(targetDir);
            float2 moveDir = math.lerp(pathLeader.ValueRO.MoveDirection, targetDir, 10f * deltaTime);
            pathLeader.ValueRW.MoveDirection = moveDir;

            float3 newPos = curPos + math.float3(moveDir, 0) * pathLeader.ValueRO.Speed;
            transform.ValueRW.Position = newPos;

            if (buffer.IsEmpty)
            {
                buffer.Add(new PathPosition { Value = newPos });
            }
            else
            {
                // Add to buffer if min distance reached
                float3 lastPoint = buffer[buffer.Length - 1].Value;
                if (math.distancesq(newPos, lastPoint) >= 1.0f)
                {
                    buffer.Add(new PathPosition { Value = newPos });
                    if (buffer.Length > pathLeader.ValueRO.MaxPathLength) buffer.RemoveAt(0);
                }
            }
        }
    }
    
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
