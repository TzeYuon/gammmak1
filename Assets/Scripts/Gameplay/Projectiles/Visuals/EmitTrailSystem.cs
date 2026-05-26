using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(PostMovementSystemGroup))]
partial struct EmitTrailSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GlobalVFXQueue>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        GlobalVFXQueue globalVFXQueue = SystemAPI.GetSingleton<GlobalVFXQueue>();
        state.Dependency = new EmitTrailJob
        {
            VfxQueue = globalVFXQueue.Queue.AsParallelWriter()
        }.ScheduleParallel(state.Dependency);

        state.Dependency.Complete();
    }
}

[BurstCompile]
public partial struct EmitTrailJob : IJobEntity
{
    public NativeQueue<VFXPayload>.ParallelWriter VfxQueue;

    private void Execute(ref VFXTrailEmitter emitter, in LocalToWorld transform)
    {
        float3 currentPos = transform.Position;
        if (!emitter.IsInitialized || emitter.LastDropPosition.Equals(float3.zero))
        {
            emitter.LastDropPosition = currentPos;
            emitter.IsInitialized = true;
            return;
        }

        float distSq = math.distancesq(currentPos, emitter.LastDropPosition);
        while (distSq >= emitter.DropDistanceSq)
        {
            float dropDistance = math.sqrt(emitter.DropDistanceSq);
            float3 dir = math.normalize(currentPos - emitter.LastDropPosition);
            float3 dropPoint = emitter.LastDropPosition + (dir * dropDistance);
            
            VfxQueue.Enqueue(new VFXPayload
            {
                Position = dropPoint,
                Type = (uint)VFXType.Trail,
                Colour = emitter.Colour,
                CustomData = new float4(emitter.Size, emitter.Duration, 0, 0)
            });

            emitter.LastDropPosition = dropPoint;
            distSq = math.distancesq(currentPos, emitter.LastDropPosition);
        }
    }
}