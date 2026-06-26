using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct HiveDirectorSystem : ISystem
{
    private float wakeUpTimer;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DifficultyState>();
        state.RequireForUpdate<DormantHiveTag>();
        wakeUpTimer = 60.0f;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        wakeUpTimer -= deltaTime;

        if (wakeUpTimer <= 0)
        {
            wakeUpTimer = 60.0f;

            Entity hiveToWake = Entity.Null;

            // Find one Dormant Hive
            foreach (var (dormantTag, entity) in SystemAPI.Query<RefRO<DormantHiveTag>>().WithEntityAccess())
            {
                hiveToWake = entity;
                break;
            }

            if (hiveToWake != Entity.Null)
            {
                var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

                ecb.AddComponent<ActiveHiveTag>(hiveToWake);
                ecb.SetComponentEnabled<ActiveHiveTag>(hiveToWake, true);
                ecb.RemoveComponent<DormantHiveTag>(hiveToWake);
            }
        }
    }
}