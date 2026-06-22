using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public partial struct LevelDirectorSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameStateSingleton>();

        // Create the global surge singleton
        state.EntityManager.CreateSingleton(new GlobalSurgeState());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gameState = SystemAPI.GetSingleton<GameStateSingleton>().CurrentState;
        if (gameState != GameState.Gameplay) return;

        float deltaTime = SystemAPI.Time.DeltaTime;

        // Update surge state and stats
        RefRW<GlobalSurgeState> surgeState = SystemAPI.GetSingletonRW<GlobalSurgeState>();
        surgeState.ValueRW.LevelTimer += deltaTime;
        float levelTime = surgeState.ValueRO.LevelTimer;

        surgeState.ValueRW.TriggerSurgeThisFrame = false;
        surgeState.ValueRW.SurgeTimer -= deltaTime;
        if (surgeState.ValueRO.SurgeTimer <= 0)
        {
            // Trigger surge
            surgeState.ValueRW.TriggerSurgeThisFrame = true;

            // TEMPORARY: hardcoded scaling of surge stats
            float difficultyMultiplier = 1.0f + (levelTime / 60.0f); // +100% difficulty per minute
            // Interval between surges not affected by level time
            surgeState.ValueRW.SpawnCountMultiplier = math.min(3f, difficultyMultiplier / 2);

            surgeState.ValueRW.SurgeTimer = surgeState.ValueRO.CurrentInterval;
        }
    }
}

public struct GlobalSurgeState : IComponentData
{
    public float LevelTimer;
    public float SurgeTimer;
    public float CurrentInterval;
    public float SpawnCountMultiplier;
    public bool TriggerSurgeThisFrame;
}