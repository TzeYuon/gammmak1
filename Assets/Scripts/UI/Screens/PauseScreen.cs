using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class PauseScreen : UIScreen
{
    private EntityManager entityManager;
    private EntityQuery inputQuery;
    private EntityQuery gameStateQuery;

    private GameState prevState;

    public PauseScreen(UIScreenManager manager, VisualElement root) : base(manager, root)
    {
        Root.Q<Button>("ResumeButton").clicked += ExitPauseMenu;
        Root.Q<Button>("SettingsButton").clicked += () => ChangeGameState(GameState.Settings);
        Root.Q<Button>("HowToPlayButton").clicked += () => ChangeGameState(GameState.HowToPlay);
        Root.Q<Button>("ExitButton").clicked += () => ChangeGameState(GameState.MainMenu);

        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            entityManager = world.EntityManager;
            inputQuery = entityManager.CreateEntityQuery(typeof(PlayerInputData));
            gameStateQuery = entityManager.CreateEntityQuery(typeof(GameStateSingleton));
        }

        GameStateSystem.OnStateChanged += HandleStateChange;
    }

    protected override void OnShow()
    {
        GameStateSystem.OnStateChanged -= HandleStateChange;
    }

    protected override void OnHide()
    {
        GameStateSystem.OnStateChanged += HandleStateChange;
    }

    public override void Dispose()
    {
        GameStateSystem.OnStateChanged -= HandleStateChange;
    }

    public override void DoUpdate()
    {
        // exit if not in how to play
        if (gameStateQuery.IsEmptyIgnoreFilter) return;
        gameStateQuery.CompleteDependency();
        var gameState = gameStateQuery.GetSingleton<GameStateSingleton>();
        if (gameState.CurrentState != GameState.Paused) return;

        // get input for this frame
        if (inputQuery.IsEmptyIgnoreFilter) return;
        var inputData = inputQuery.GetSingleton<PlayerInputData>();

        if (inputData.WasPressed(PlayerAction.Cancel))
        {
            inputData.ClearState(PlayerAction.Cancel);
            ExitPauseMenu();
        }
    }

    void ExitPauseMenu()
    {
        // return to previous state
        ChangeGameState(prevState);
    }

    private void HandleStateChange(GameState prevState, GameState gameState)
    {
        if (gameState == GameState.Paused)
        {
            int prevDepth = GameStateSystem.GetStateDepth(prevState);
            int newDepth = GameStateSystem.GetStateDepth(gameState);

            // only store prev state if it is before current depth
            if (prevDepth < newDepth)
            {
                this.prevState = prevState;
            }
        }
    }

    void ChangeGameState(GameState state)
    {
        if (gameStateQuery.IsEmptyIgnoreFilter) return;
        gameStateQuery.CompleteDependency();
        var gameState = gameStateQuery.GetSingletonRW<GameStateSingleton>();
        gameState.ValueRW.CurrentState = state;
    }
}