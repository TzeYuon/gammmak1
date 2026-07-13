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
        Root.Q<Button>("ResumeButton").clicked += OnResumeClicked;
        Root.Q<Button>("ExitButton").clicked += OnExitClicked;

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
        // exit if not paused
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
        if (gameStateQuery.IsEmptyIgnoreFilter) return;
        gameStateQuery.CompleteDependency();
        var gameState = gameStateQuery.GetSingletonRW<GameStateSingleton>();
        gameState.ValueRW.CurrentState = prevState;
    }

    private void HandleStateChange(GameState prevState, GameState gameState)
    {
        if (gameState == GameState.Paused)
        {
            // store prev state to return to after exiting pause
            this.prevState = prevState;
        }
    }

    void OnResumeClicked()
    {
        ExitPauseMenu();
    }

    void OnExitClicked()
    {
        if (gameStateQuery.IsEmptyIgnoreFilter) return;
        gameStateQuery.CompleteDependency();
        var gameState = gameStateQuery.GetSingletonRW<GameStateSingleton>();
        gameState.ValueRW.CurrentState = GameState.MainMenu;
    }
}