using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
partial struct VFXSystem : ISystem
{
    EntityQuery queueQuery;
    NativeQueue<VFXPayload> queue;

    public void OnCreate(ref SystemState state)
    {
        queue = new NativeQueue<VFXPayload>(Allocator.Persistent);
        queueQuery = SystemAPI.QueryBuilder().WithAll<GlobalVFXQueue>().Build();
        state.EntityManager.CreateSingleton(new GlobalVFXQueue { Queue = queue });
    }

    public void OnDestroy(ref SystemState state)
    {
        if (queue.IsCreated) queue.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        queueQuery.CompleteDependency();
        queue.Clear();
    }
}

public struct GlobalVFXQueue : IComponentData
{
    public NativeQueue<VFXPayload> Queue;
}