using System;
using Unity.Entities;

public enum GameState : byte
{
    None = 0,
    MainMenu,
    FleetCommandHub, // In the menus (Shop, Upgrades, Level select)
    Generating, // Generating next level
    Gameplay, // Main gameplay screen (RTS)
    Paused,
    Victory,
    Defeat
}

public struct GameStateSingleton : IComponentData
{
    public GameState CurrentState;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class GameStateSystem : SystemBase
{
    private bool hasSubscribedEvents = false;

    public static event Action<GameState, GameState> OnStateChanged;
    private GameState previousState = GameState.MainMenu;

    protected override void OnCreate()
    {
        EntityManager.CreateSingleton(new GameStateSingleton()
        {
            CurrentState = GameState.MainMenu
        });
    }

    protected override void OnDestroy()
    {
        if (hasSubscribedEvents && SceneManager.Instance != null)
        {
            SceneManager.Instance.OnSceneChanged -= OnSceneChanged;
        }
    }

    protected override void OnUpdate()
    {
        if (!hasSubscribedEvents && SceneManager.Instance != null)
        {
            SceneManager.Instance.OnSceneChanged += OnSceneChanged;
            hasSubscribedEvents = true;
        }

        if (!SystemAPI.TryGetSingleton<GameStateSingleton>(out var stateData)) return;
        if (stateData.CurrentState != previousState)
        {
            OnStateChanged?.Invoke(previousState, stateData.CurrentState);
            previousState = stateData.CurrentState;
        }
    }

    void OnSceneChanged(object sender, SceneChangeArgs args)
    {
        if (!SystemAPI.TryGetSingletonRW<GameStateSingleton>(out var stateData)) return;
        switch (args.newSceneId)
        {
            case SceneId.BootstrapScene:
                break;
            case SceneId.MainMenuScene:
                stateData.ValueRW.CurrentState = GameState.MainMenu;
                OnStateChanged?.Invoke(previousState, stateData.ValueRO.CurrentState);
                previousState = GameState.MainMenu;
                break;
            case SceneId.GameplayScene:
                stateData.ValueRW.CurrentState = GameState.FleetCommandHub;
                OnStateChanged?.Invoke(previousState, stateData.ValueRO.CurrentState);
                previousState = GameState.FleetCommandHub;
                break;
        }
    }
}