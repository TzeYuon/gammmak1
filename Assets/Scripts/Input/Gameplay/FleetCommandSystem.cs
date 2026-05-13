using Unity.Burst;
using Unity.Entities;
using UnityEngine;

partial struct FleetCommandSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputData>();
    }

    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var input = SystemAPI.GetSingleton<PlayerInputData>();
        //Debug.Log(input.IsLeftClickHeld);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
