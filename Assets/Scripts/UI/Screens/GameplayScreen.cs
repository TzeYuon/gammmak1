using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class GameplayScreen : UIScreen
{
    private PlayerSaveData playerSaveData;

    private EntityManager entityManager;

    private Camera camera;

    private VisualElement selectionBox;

    private SelectedShipsDisplay selectedShipsDisplay;

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

        selectedShipsDisplay = new SelectedShipsDisplay(root.Q<VisualElement>("SelectedShipsDisplay"));

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
                ChangeGameState(GameState.FleetCommandHub);
            };

            defeatContinueButton.clicked += () =>
            {
                ChangeGameState(GameState.MainMenu);
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
        FleetCommandSystem.OnSelectionBoxChanged -= OnSelectionBoxChanged;
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
        FleetCommandSystem.OnSelectionBoxChanged += OnSelectionBoxChanged;

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
                    RegisterBtn(btn, count);
                    count++;

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
    
    private void OnSelectionBoxChanged(float2 min, float2 max)
    {
        if (float2.Equals(min, 0) && float2.Equals(max, 0))
        {
            // Hide selection box
            selectionBox.style.display = DisplayStyle.None;
            return;
        }

        Vector2 uiMin = WorldToUI(min);
        Vector2 uiMax = WorldToUI(max);
        
        float left = uiMin.x;
        float top = uiMax.y;

        float width = uiMax.x - uiMin.x;
        float height = uiMin.y - uiMax.y;

        selectionBox.style.left = left;
        selectionBox.style.top = top;
        selectionBox.style.width = width;
        selectionBox.style.height = height;

        selectionBox.style.display = DisplayStyle.Flex;
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
        UpdateBiomassCounter();
        UpdateSurgeWarning();

        selectedShipsDisplay.Update();

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
                surgeWarningLbl.style.color = new Color(1, 0, 0, smoothOpacity);
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
    }

    private void TriggerLoss()
    {
        defeatScreen.style.display = DisplayStyle.Flex;
        biomassCounter.style.display = DisplayStyle.None;
    }

    private void HideVictoryLoss()
    {
        victoryScreen.style.display = DisplayStyle.None;
        defeatScreen.style.display = DisplayStyle.None;
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

    private Vector2 WorldToUI(float2 worldPos)
    {
        return RuntimePanelUtils.CameraTransformWorldToPanel(
            selectionBox.panel,
            new Vector3(worldPos.x, worldPos.y, 0f),
            camera
        );
    }

    private void ToggleReinforcementPopup(bool show)
    {
        reinforcementPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ChangeGameState(GameState targetState)
    {
        if (gameStateQuery.IsEmptyIgnoreFilter) return;
        gameStateQuery.CompleteDependency();
        var gameState = gameStateQuery.GetSingletonRW<GameStateSingleton>();
        gameState.ValueRW.CurrentState = targetState;
    }
}

public class SelectedShipsDisplay
{
    private EntityManager entityManager;
    private EntityQuery inputQuery;
    private EntityQuery selectedQuery;

    private VisualElement selectionContainer;
    private int lastSelectedCount = -1;

    private Button selectAllBtn;
    private Button splitSelectionBtn;

    public SelectedShipsDisplay(VisualElement root)
    {
        selectionContainer = root.Q<VisualElement>("SelectedShipsContainer");

        selectAllBtn = root.Q<Button>("SelectAllBtn");
        selectAllBtn.clicked += () =>
        {
            OnSelectAllClicked();
        };

        splitSelectionBtn = root.Q<Button>("SplitSelectionBtn");
        splitSelectionBtn.clicked += () =>
        {
            OnSplitSelectionClicked();
        };

        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            entityManager = world.EntityManager;
            inputQuery = entityManager.CreateEntityQuery(typeof(PlayerInputData));
            selectedQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SelectedTag>(),
                ComponentType.ReadOnly<ShipIDComponent>(),
                ComponentType.Exclude<DeadTag>()
            );
        }

        RebuildSelectionUI();
    }

    public void Update()
    {
        int currentSelectedCount = selectedQuery.CalculateEntityCount();
        if (currentSelectedCount == lastSelectedCount) return;

        lastSelectedCount = currentSelectedCount;
        RebuildSelectionUI();
    }

    private void RebuildSelectionUI()
    {
        selectionContainer.Clear();

        // only show button when there are selected to split
        EnableSplitSelectionBtn(lastSelectedCount != 0);

        if (lastSelectedCount == 0) return;

        var selectedEntities = selectedQuery.ToEntityArray(Allocator.Temp);
        var shipTypes = selectedQuery.ToComponentDataArray<ShipIDComponent>(Allocator.Temp);

        Dictionary<ShipID, List<Entity>> groupedSelection = new Dictionary<ShipID, List<Entity>>();

        for (int i = 0; i < selectedEntities.Length; i++)
        {
            ShipID typeID = shipTypes[i].ShipID;
            if (!groupedSelection.ContainsKey(typeID))
            {
                groupedSelection[typeID] = new List<Entity>();
            }
            groupedSelection[typeID].Add(selectedEntities[i]);
        }

        foreach (var group in groupedSelection)
        {
            List<Entity> groupEntities = group.Value;
            VisualElement selectedUnitItem = UIFactory.BuildSelectedUnitItem(group.Key, groupEntities.Count);

            selectedUnitItem.RegisterCallback<ClickEvent>(evt =>
            {
                HandleGroupClicked(groupEntities);
            });

            selectionContainer.Add(selectedUnitItem);
        }

        selectedEntities.Dispose();
        shipTypes.Dispose();
    }

    private void HandleGroupClicked(List<Entity> clickedGroup)
    {
        var allSelected = selectedQuery.ToEntityArray(Allocator.Temp);

        foreach (var entity in allSelected)
        {
            if (!clickedGroup.Contains(entity))
            {
                entityManager.SetComponentEnabled<SelectedTag>(entity, false);
            }
        }

        allSelected.Dispose();

        lastSelectedCount = -1;
    }

    void EnableSplitSelectionBtn(bool toggle)
    {
        splitSelectionBtn.SetEnabled(toggle);
    }

    private void OnSelectAllClicked()
    {
        Entity commandEntity = entityManager.CreateEntity();
        entityManager.AddComponent<SelectAllCommand>(commandEntity);
    }

    private void OnSplitSelectionClicked()
    {
        Entity commandEntity = entityManager.CreateEntity();
        entityManager.AddComponent<SplitCommand>(commandEntity);
    }
}