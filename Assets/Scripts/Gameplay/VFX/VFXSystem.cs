using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

partial struct VFXSystem : ISystem
{
    NativeQueue<VFXPayload> queue;

    public void OnCreate(ref SystemState state)
    {
        queue = new NativeQueue<VFXPayload>(Allocator.Persistent);
        state.EntityManager.CreateSingleton(new GlobalVFXQueue { Queue = queue });
    }

    public void OnDestroy(ref SystemState state)
    {
        if (queue.IsCreated) queue.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        SystemAPI.GetSingleton<GlobalVFXQueue>().Queue.Clear();
    }
}

public struct GlobalVFXQueue : IComponentData
{
    public NativeQueue<VFXPayload> Queue;
}