using Unity.Burst;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(LateGameplayGroup))]
public partial struct GamePauseSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputData>();
        state.RequireForUpdate<GameStateSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var input = SystemAPI.GetSingleton<PlayerInputData>();
        if (input.WasPressed(PlayerAction.Cancel))
        {
            var gameState = SystemAPI.GetSingletonRW<GameStateSingleton>();
            var curState = gameState.ValueRO.CurrentState;

            if (curState == GameState.Gameplay)
            {
                gameState.ValueRW.CurrentState = GameState.Paused;
            }
        }
    }
}