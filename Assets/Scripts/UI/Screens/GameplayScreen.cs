using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class GameplayScreen : UIScreen
{
    private EntityManager entityManager;
    private EntityQuery inputQuery;

    private Camera camera;

    private VisualElement selectionBox;

    private Label biomassCounter;
    private EntityQuery economyQuery;

    private VisualElement reinforcementPopup;
    private bool isReinforcementPopupOpen;
    private float2 spawnPosition;
    private EntityQuery queueQuery;

    private VisualElement victoryScreen;
    private Button victoryContinueButton;
    private VisualElement defeatScreen;
    private Button defeatContinueButton;
    private EntityQuery gameStateQuery;

    public GameplayScreen(UIScreenManager manager, VisualElement root) : base(manager, root)
    {
        InitInput();

        selectionBox = root.Q<VisualElement>("SelectionBox");

        biomassCounter = root.Q<Label>("BiomassCounter");

        reinforcementPopup = root.Q<VisualElement>("ReinforcementPopup");
        InitReinforcementPopup();

        InitVictoryLoss();

        camera = Camera.main;

        void InitInput()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null)
            {
                entityManager = world.EntityManager;
                inputQuery = entityManager.CreateEntityQuery(typeof(PlayerInputData));
                economyQuery = entityManager.CreateEntityQuery(typeof(PlayerEconomy));
                queueQuery = entityManager.CreateEntityQuery(typeof(SpawnQueueSingleton));
                gameStateQuery = entityManager.CreateEntityQuery(typeof(GameStateSingleton));
            }
        }

        void InitReinforcementPopup()
        {
            isReinforcementPopupOpen = false;
            reinforcementPopup.Add(CreateButton("Frigate (10)", 0));
            reinforcementPopup.Add(CreateButton("Destroyer (30)", 1));
            reinforcementPopup.Add(CreateButton("Cruiser (50)", 2));
        }

        Button CreateButton(string text, int shipType)
        {
            var btn = new Button { text = text };
            btn.style.backgroundColor = new Color(0.1f, 0.2f, 0.3f);
            btn.style.color = Color.cyan;
            btn.style.marginTop = 5;

            btn.clicked += () =>
            {
                if (queueQuery.IsEmptyIgnoreFilter) return;

                queueQuery.CompleteDependency();
                var queue = queueQuery.GetSingleton<SpawnQueueSingleton>().Queue;
                queue.Enqueue(new SpawnRequest
                {
                    ShipType = shipType,
                    Position = GetSpawnPosition()
                });

                CloseReinforcementPopup();
            };

            return btn;
        }

        void InitVictoryLoss()
        {
            GameStateSystem.OnStateChanged += HandleStateChange;

            victoryScreen = root.Q<VisualElement>("VictoryScreen");
            defeatScreen = root.Q<VisualElement>("DefeatScreen");

            victoryScreen.style.display = DisplayStyle.None;
            defeatScreen.style.display = DisplayStyle.None;

            victoryContinueButton = root.Q<Button>("VictoryContinueButton");
            defeatContinueButton = root.Q<Button>("DefeatContinueButton");

            victoryContinueButton.clicked += () =>
            {
                if (gameStateQuery.IsEmptyIgnoreFilter) return;
                gameStateQuery.CompleteDependency();
                var gameState = gameStateQuery.GetSingletonRW<GameStateSingleton>();
                gameState.ValueRW.CurrentState = GameState.FleetCommandHub;
            };

            defeatContinueButton.clicked += () =>
            {
                if (gameStateQuery.IsEmptyIgnoreFilter) return;
                gameStateQuery.CompleteDependency();
                var gameState = gameStateQuery.GetSingletonRW<GameStateSingleton>();
                gameState.ValueRW.CurrentState = GameState.MainMenu;
            };
        }
    }

    protected override void OnHide()
    {
        HideVictoryLoss();
        CloseReinforcementPopup();
    }

    protected override void OnShow()
    {
        HideVictoryLoss();
        CloseReinforcementPopup();
        biomassCounter.style.display = DisplayStyle.Flex;
    }

    public override void DoUpdate()
    {
        if (inputQuery.IsEmptyIgnoreFilter) return;
        var inputData = inputQuery.GetSingleton<PlayerInputData>();

        UpdateSelectionBox();

        UpdateBiomassCounter();

        UpdateReinforcementPopup();

        void UpdateSelectionBox()
        {
            if (inputData.IsHeld(PlayerAction.Primary))
            {
                selectionBox.style.display = DisplayStyle.Flex;

                Vector2 uiStart = ScreenToUI(inputData.MouseStartDragPosition);
                Vector2 uiCurrent = ScreenToUI(inputData.MouseWorldPosition);

                float left = math.min(uiStart.x, uiCurrent.x);
                float top = math.min(uiStart.y, uiCurrent.y);
                float width = math.abs(uiStart.x - uiCurrent.x);
                float height = math.abs(uiStart.y - uiCurrent.y);

                selectionBox.style.left = left;
                selectionBox.style.top = top;
                selectionBox.style.width = width;
                selectionBox.style.height = height;
            }
            else
            {
                // Hide selection box
                selectionBox.style.display = DisplayStyle.None;
            }
        }

        void UpdateReinforcementPopup()
        {
            spawnPosition = inputData.MouseWorldPosition;
            if (inputData.WasPressed(PlayerAction.ToggleReinforcementPopup))
            {
                if (isReinforcementPopupOpen)
                {
                    CloseReinforcementPopup();
                }
                else
                {
                    reinforcementPopup.style.display = DisplayStyle.Flex;
                    isReinforcementPopupOpen = true;

                    Vector2 uiCurrent = ScreenToUI(inputData.MouseWorldPosition);

                    reinforcementPopup.style.left = uiCurrent.x;
                    reinforcementPopup.style.top = uiCurrent.y;
                }
            }
        }

        void UpdateBiomassCounter()
        {
            var economy = economyQuery.GetSingleton<PlayerEconomy>();
            biomassCounter.text = "Biomass: " + economy.TotalBiomass;
        }
    }

    private void TriggerVictory()
    {
        victoryScreen.style.display = DisplayStyle.Flex;
        biomassCounter.style.display = DisplayStyle.None;
        Time.timeScale = 0.2f;
    }

    private void TriggerLoss()
    {
        defeatScreen.style.display = DisplayStyle.Flex;
        biomassCounter.style.display = DisplayStyle.None;
        Time.timeScale = 0.2f;
    }

    private void HideVictoryLoss()
    {
        victoryScreen.style.display = DisplayStyle.None;
        defeatScreen.style.display = DisplayStyle.None;
        Time.timeScale = 1f; // I dont know if this is the best way to do this
    }

    private void HandleStateChange(GameState prevState, GameState gameState)
    {
        if (gameState == GameState.Victory)
        {
            TriggerVictory();
        }
        else if (gameState == GameState.Defeat)
        {
            TriggerLoss();
        }
    }

    private Vector2 ScreenToUI(float2 worldPos)
    {
        float2 screenPos = (Vector2)camera.WorldToScreenPoint((Vector2)worldPos);
        return new Vector2(screenPos.x, Screen.height - screenPos.y);
    }

    private void CloseReinforcementPopup()
    {
        reinforcementPopup.style.display = DisplayStyle.None;
        isReinforcementPopupOpen = false;
    }

    private float2 GetSpawnPosition()
    {
        return spawnPosition;
    }
}
