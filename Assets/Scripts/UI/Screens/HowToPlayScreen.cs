using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;
public class HowToPlayScreen : UIScreen
{
    private EntityManager entityManager;
    private EntityQuery inputQuery;
    private EntityQuery gameStateQuery;

    private GameState prevState;
    
    private VisualElement basicControlsContainer;
    private VisualElement economyContainer;
    private VisualElement theSwarmContainer;
    private VisualElement hangarContainer;
    private VisualElement starmapContainer;

    public HowToPlayScreen(UIScreenManager manager, VisualElement root) : base(manager, root)
    {
        root.Q<Button>("BackBtn").clicked += OnBackClicked;

        basicControlsContainer = root.Q<VisualElement>("BasicControlsContainer");
        economyContainer = root.Q<VisualElement>("EconomyContainer");
        theSwarmContainer = root.Q<VisualElement>("TheSwarmContainer");
        hangarContainer = root.Q<VisualElement>("HangarContainer");
        starmapContainer = root.Q<VisualElement>("StarmapContainer");

        root.Q<Button>("BasicControlsBtn").clicked += () => DisplayPage(basicControlsContainer);
        root.Q<Button>("EconomyBtn").clicked += () => DisplayPage(economyContainer);
        root.Q<Button>("TheSwarmBtn").clicked += () => DisplayPage(theSwarmContainer);
        root.Q<Button>("HangarBtn").clicked += () => DisplayPage(hangarContainer);
        root.Q<Button>("StarmapBtn").clicked += () => DisplayPage(starmapContainer);

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
        DisplayPage(basicControlsContainer);

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

    void DisplayPage(VisualElement element)
    {
        basicControlsContainer.style.visibility = Visibility.Hidden;
        economyContainer.style.visibility = Visibility.Hidden;
        theSwarmContainer.style.visibility = Visibility.Hidden;
        hangarContainer.style.visibility = Visibility.Hidden;
        starmapContainer.style.visibility = Visibility.Hidden;

        element.style.visibility = Visibility.Visible;
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