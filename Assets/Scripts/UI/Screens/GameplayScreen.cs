using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class GameplayScreen : UIScreen
{
    private PlayerSaveData playerSaveData;

    private EntityManager entityManager;
    private EntityQuery inputQuery;

    private Camera camera;

    private VisualElement selectionBox;

    private Label biomassCounter;
    private EntityQuery economyQuery;

    private Label surgeWarningLbl;
    private EntityQuery surgeStateQuery;

    private VisualElement reinforcementPanel;
    private VisualElement reinforcementContainer;
    private List<(int cost, VisualElement btn)> reinforcementBtns = new();
    private EntityQuery queueQuery;

    private VisualElement victoryScreen;
    private Button victoryContinueButton;
    private VisualElement defeatScreen;
    private Button defeatContinueButton;
    private EntityQuery gameStateQuery;

    private VisualElement upgradeCoresRewardContainer;
    private VisualElement weaponsRewardContainer;
    private VisualElement shipsRewardContainer;

    public GameplayScreen(UIScreenManager manager, VisualElement root, PlayerSaveData playerSaveData) : base(manager, root)
    {
        InitInput();

        selectionBox = root.Q<VisualElement>("SelectionBox");

        biomassCounter = root.Q<Label>("BiomassCounter");

        surgeWarningLbl = root.Q<Label>("SurgeWarningLbl");

        this.playerSaveData = playerSaveData;
        reinforcementPanel = root.Q<VisualElement>("ReinforcementPanel");
        reinforcementContainer = root.Q<VisualElement>("ReinforcementBtnContainer");

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
                surgeStateQuery = world.EntityManager.CreateEntityQuery(typeof(GlobalSurgeState));
            }
        }

        void InitVictoryLoss()
        {
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

            // init rewards
            upgradeCoresRewardContainer = victoryScreen.Q<VisualElement>("UpgradeCoresRewardContainer");
            weaponsRewardContainer = victoryScreen.Q<VisualElement>("WeaponsRewardContainer");
            shipsRewardContainer = victoryScreen.Q<VisualElement>("ShipsRewardContainer");
        }
    }

    protected override void OnHide()
    {
        HideVictoryLoss();
        ToggleReinforcementPopup(false);

        GameStateSystem.OnStateChanged -= HandleStateChange;
        EconomyEventSystem.OnBiomassChanged -= UpdateReinforcementBtns;
        RewardManager.OnRewardGranted -= OnRewardGranted;
    }

    protected override void OnShow()
    {
        HideVictoryLoss();
        ToggleReinforcementPopup(true);
        RebuildReinforcementPopup();

        biomassCounter.style.display = DisplayStyle.Flex;

        GameStateSystem.OnStateChanged += HandleStateChange;
        EconomyEventSystem.OnBiomassChanged += UpdateReinforcementBtns;
        RewardManager.OnRewardGranted += OnRewardGranted;

        void RebuildReinforcementPopup()
        {
            reinforcementBtns.Clear();
            reinforcementContainer.Clear();
            if (playerSaveData != null)
            {
                var activeRoster = playerSaveData.ActiveRoster;
                if (activeRoster == null) return;

                int count = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (activeRoster[i] == null) continue;
                    var cur = activeRoster[i];

                    int cost = cur.GetStatInt(ShipStat.Cost);
                    VisualElement btn = UIFactory.BuildReinforcementBtn(cur.ShipName, cost);
                    RegisterBtn(btn, count++);

                    reinforcementBtns.Add((cost, btn));
                    reinforcementContainer.Add(btn);
                }
            }

            economyQuery.CompleteDependency();
            var economy = economyQuery.GetSingleton<PlayerEconomy>();
            UpdateReinforcementBtns(economy.TotalBiomass);

            void RegisterBtn(VisualElement btn, int slot)
            {
                btn.RegisterCallback<ClickEvent>(evt =>
                {
                    if (queueQuery.IsEmptyIgnoreFilter) return;

                    queueQuery.CompleteDependency();
                    var queue = queueQuery.GetSingleton<SpawnQueueSingleton>().Queue;
                    queue.Enqueue(new SpawnRequest
                    {
                        SlotIndex = slot,
                    });
                });
            }
        }
    }

    /// <summary>
    /// This only updates the victory screen to display the rewards granted
    /// </summary>
    /// <param name="rewards"></param>
    private void OnRewardGranted(List<LevelReward> rewards)
    {
        // empty previous rewards
        upgradeCoresRewardContainer.Clear();
        weaponsRewardContainer.Clear();
        shipsRewardContainer.Clear();

        // display list of rewards
        foreach (var reward in rewards)
        {
            switch (reward.Type)
            {
                case RewardType.UpgradeCores:
                    upgradeCoresRewardContainer.Add(UIFactory.BuildRewardItem(reward));
                    break;
                
                case RewardType.WeaponBlueprint:
                    weaponsRewardContainer.Add(UIFactory.BuildRewardItem(reward));
                    break;
                
                case RewardType.ShipBlueprint:
                    shipsRewardContainer.Add(UIFactory.BuildRewardItem(reward));
                    break;
            }
        }
    }

    void UpdateReinforcementBtns(float amount)
    {
        foreach (var (cost, btn) in reinforcementBtns)
        {
            btn.SetEnabled(amount >= cost);
        }
    }

    public override void DoUpdate()
    {
        if (inputQuery.IsEmptyIgnoreFilter) return;
        var inputData = inputQuery.GetSingleton<PlayerInputData>();

        UpdateSelectionBox();
        UpdateBiomassCounter();
        UpdateReinforcementPopup();
        UpdateSurgeWarning();

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
            if (gameStateQuery.IsEmptyIgnoreFilter) return;
            gameStateQuery.CompleteDependency();
            var gameState = gameStateQuery.GetSingleton<GameStateSingleton>();
            if (gameState.CurrentState != GameState.Gameplay)
            {
                return;
            }
        }

        void UpdateBiomassCounter()
        {
            economyQuery.CompleteDependency();
            var economy = economyQuery.GetSingleton<PlayerEconomy>();
            biomassCounter.text = "Biomass: " + economy.TotalBiomass;
        }
        
        void UpdateSurgeWarning()
        {
            if (surgeStateQuery.IsEmptyIgnoreFilter) return;
            var surgeState = surgeStateQuery.GetSingleton<GlobalSurgeState>();

            if (surgeState.SurgeTimer > 0f && surgeState.SurgeTimer <= 3)
            {
                if (surgeWarningLbl.style.display != DisplayStyle.Flex) surgeWarningLbl.style.display = DisplayStyle.Flex;

                float rawSine = math.sin(surgeState.SurgeTimer * 4);
                float smoothOpacity = (rawSine * 0.5f) + 0.5f;
                surgeWarningLbl.style.opacity = smoothOpacity;
            }
            else
            {
                if (surgeWarningLbl.style.display != DisplayStyle.None) surgeWarningLbl.style.display = DisplayStyle.None;
            }
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
        Time.timeScale = 1f;
    }

    private void HandleStateChange(GameState prevState, GameState gameState)
    {
        if (gameState == GameState.Victory)
        {
            ToggleReinforcementPopup(false);
            TriggerVictory();
        }
        else if (gameState == GameState.Defeat)
        {
            ToggleReinforcementPopup(false);
            TriggerLoss();
        }
    }

    private Vector2 ScreenToUI(float2 worldPos)
    {
        float2 screenPos = (Vector2)camera.WorldToScreenPoint((Vector2)worldPos);
        return new Vector2(screenPos.x, Screen.height - screenPos.y);
    }

    private void ToggleReinforcementPopup(bool show)
    {
        reinforcementPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
