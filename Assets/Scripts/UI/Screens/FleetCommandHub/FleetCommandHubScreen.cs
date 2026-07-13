using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class FleetCommandHubScreen : UIScreen
{
    private PlayerSaveData playerSaveData;

    private IUIScreen hangarScreen;
    private IUIScreen weaponsScreen;
    private IUIScreen starmapScreen;

    private Label upgradeCoreCounter;

    private EntityManager entityManager;
    private EntityQuery inputQuery;
    private EntityQuery gameStateQuery;

    public FleetCommandHubScreen(
        UIScreenManager manager, 
        VisualElement root, 
        PlayerSaveData playerSaveData, 
        HangarShipPreviewManager hangarShipPreviewManager) : base(manager, root)
    {
        this.playerSaveData = playerSaveData;

        hangarScreen = new HangarScreen(manager, root.Q<VisualElement>("HangarScreen"), this, hangarShipPreviewManager);
        weaponsScreen = new WeaponsScreen(manager, root.Q<VisualElement>("WeaponsScreen"));
        starmapScreen = new StarmapScreen(manager, root.Q<VisualElement>("StarmapScreen"));

        // Register buttons
        root.Q<Button>("HangarBtn").clicked += () => ShowScreen(hangarScreen);
        root.Q<Button>("WeaponsBtn").clicked += () => ShowScreen(weaponsScreen);
        root.Q<Button>("StarmapBtn").clicked += () => ShowScreen(starmapScreen);

        upgradeCoreCounter = root.Q<Label>("UpgradeCoreCounter");
        UpdateUpgradeCoreCounter();

        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            entityManager = world.EntityManager;
            inputQuery = entityManager.CreateEntityQuery(typeof(PlayerInputData));
            gameStateQuery = entityManager.CreateEntityQuery(typeof(GameStateSingleton));
        }
    }

    protected override void OnHide()
    {

    }

    protected override void OnShow()
    {
        // Show default screen
        hangarScreen.Hide();
        weaponsScreen.Hide();
        starmapScreen.Hide();
        UpdateUpgradeCoreCounter();
    }

    public override void DoUpdate()
    {
        // exit if not in fleet command
        if (gameStateQuery.IsEmptyIgnoreFilter) return;
        gameStateQuery.CompleteDependency();
        var gameState = gameStateQuery.GetSingleton<GameStateSingleton>();
        if (gameState.CurrentState != GameState.FleetCommandHub) return;

        if (inputQuery.IsEmptyIgnoreFilter) return;
        var inputData = inputQuery.GetSingleton<PlayerInputData>();

        // check if pause was pressed
        if (inputData.WasPressed(PlayerAction.Cancel))
        {
            ChangeGameState(GameState.Paused);
        }
    }

    private void ShowScreen(IUIScreen screenToShow)
    {
        // Hide all screens
        hangarScreen.Hide();
        weaponsScreen.Hide();
        starmapScreen.Hide();

        // Show the requested one
        screenToShow.Show();
    }

    public void UpdateUpgradeCoreCounter()
    {
        if (playerSaveData == null) return;
        upgradeCoreCounter.text = $"CORES: {playerSaveData.UpgradeCoresRemaining}";
    }

    private void ChangeGameState(GameState targetState)
    {
        if (gameStateQuery.IsEmptyIgnoreFilter) return;
        gameStateQuery.CompleteDependency();
        var gameState = gameStateQuery.GetSingletonRW<GameStateSingleton>();
        gameState.ValueRW.CurrentState = targetState;
    }
}
