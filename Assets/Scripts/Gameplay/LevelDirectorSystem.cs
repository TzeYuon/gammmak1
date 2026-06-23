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
        state.EntityManager.CreateSingleton<GlobalSurgeState>();
        state.EntityManager.CreateSingleton<DifficultyConfig>();
        state.EntityManager.CreateSingleton<DifficultyState>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gameState = SystemAPI.GetSingleton<GameStateSingleton>().CurrentState;
        if (gameState != GameState.Gameplay) return;

        float deltaTime = SystemAPI.Time.DeltaTime;

        // Update difficulty state
        DifficultyConfig config = SystemAPI.GetSingleton<DifficultyConfig>();
        ref var difficultyState = ref SystemAPI.GetSingletonRW<DifficultyState>().ValueRW;
        difficultyState.LevelTimer += deltaTime;

        float timeFactor = (difficultyState.LevelTimer / 60.0f) * config.DifficultyPerMinute;
        float enrageFactor = difficultyState.HivesDestroyed * config.DifficultyPerHiveDestroyed;

        float difficultyFactor = 1.0f + enrageFactor + timeFactor;

        difficultyState.SpawnRateMult = math.min(difficultyFactor, config.MaxSpawnRateMult);
        difficultyState.SpawnCountMult = math.min(difficultyFactor, config.MaxSpawnCountMult);

        // Update surge state and stats
        ref GlobalSurgeState surgeState = ref SystemAPI.GetSingletonRW<GlobalSurgeState>().ValueRW;
        surgeState.TriggerSurgeThisFrame = false;
        surgeState.SurgeTimer -= deltaTime;
        if (surgeState.SurgeTimer <= 0)
        {
            // Trigger surge
            surgeState.TriggerSurgeThisFrame = true;
            surgeState.SurgeTimer = surgeState.CurrentInterval;
        }
    }
}

public struct DifficultyConfig : IComponentData
{
    public float DifficultyPerMinute;
    public float DifficultyPerHiveDestroyed;
    public float MaxSpawnRateMult;
    public float MaxSpawnCountMult;
}

public struct DifficultyState : IComponentData
{
    /// <summary>
    /// Duration that the level has been running for
    /// </summary>
    public float LevelTimer;
    public int HivesDestroyed;
    
    /// <summary>
    /// Multipier to interval between trickle spawns
    /// </summary>
    public float SpawnRateMult; // larger number means faster
    
    /// <summary>
    /// Multiplier to apply to hives to trickle spawn
    /// </summary>
    public float SpawnCountMult;
    
    //public float EnemyHealthMult;
    //public float MaxEnemyHealthMult;
}

public struct GlobalSurgeState : IComponentData
{
    /// <summary>
    /// How long until the next surge
    /// </summary>
    public float SurgeTimer; 
    /// <summary>
    /// The interval between surges that changes across the level
    /// </summary>
    public float CurrentInterval;
    /// <summary>
    /// bool to signal to hives to surge for one frame
    /// </summary>
    public bool TriggerSurgeThisFrame;
}