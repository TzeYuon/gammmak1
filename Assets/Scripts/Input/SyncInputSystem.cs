using Unity.Burst;
using Unity.Entities;

public partial struct SyncInputSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.CreateSingleton<PlayerInputData>();
    }

    public void OnUpdate(ref SystemState state)
    {
        // Copies data from static class to struct every frame
        SystemAPI.SetSingleton(new PlayerInputData
        {
            MouseWorldPosition = InputBridge.MouseWorldPosition,
            IsRightClickPressed = InputBridge.IsRightClickPressed,
            IsLeftClickHeld = InputBridge.IsLeftClickHeld
        });
    }
}