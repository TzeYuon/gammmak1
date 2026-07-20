using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;
public class HowToPlayScreen : UIScreen
{
    private EntityManager entityManager;
    private EntityQuery inputQuery;
    private EntityQuery gameStateQuery;

    private GameState prevState;

    public HowToPlayScreen(UIScreenManager manager, VisualElement root) : base(manager, root)
    {
        root.Q<Button>("BackBtn").clicked += OnBackClicked;
        
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
        if (gameState.CurrentState != GameState.HowToPlay) return;

        // get input for this frame
        if (inputQuery.IsEmptyIgnoreFilter) return;
        var inputData = inputQuery.GetSingleton<PlayerInputData>();

        if (inputData.WasPressed(PlayerAction.Cancel))
        {
            inputData.ClearState(PlayerAction.Cancel);
            ExitMenu();
        }
    }

    void ExitMenu()
    {
        // return to previous state
        if (gameStateQuery.IsEmptyIgnoreFilter) return;
        gameStateQuery.CompleteDependency();
        var gameState = gameStateQuery.GetSingletonRW<GameStateSingleton>();
        gameState.ValueRW.CurrentState = prevState;
    }

    private void HandleStateChange(GameState prevState, GameState gameState)
    {
        if (gameState == GameState.HowToPlay)
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

    void OnBackClicked()
    {
        ExitMenu();
    }
}