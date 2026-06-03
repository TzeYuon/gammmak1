using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class HangarScreen : UIScreen
{
    private PlayerSaveData playerSaveData;
    private UIAssetsDatabase uiAssetsDatabase;

    private ScrollView shipListContainer;
    private Dictionary<ShipBlueprintData, Button> shipButtons = new();

    private ShipDetailsElement shipDetailsElement;
    private HangarContextMenu hangarContextMenu;

    public HangarScreen(
        UIScreenManager manager, 
        VisualElement root, 
        PlayerSaveData playerSaveData, 
        UIAssetsDatabase uiAssetsDatabase) : base(manager, root)
    {
        this.playerSaveData = playerSaveData;
        this.uiAssetsDatabase = uiAssetsDatabase;

        shipListContainer = root.Q<ScrollView>("ShipsScrollView");

        shipDetailsElement = new (root.Q<VisualElement>("DetailsContainer"), this, playerSaveData);
        hangarContextMenu = new (root.Q<VisualElement>("HangarContextMenu"));
    }

    protected override void OnHide()
    {
        shipDetailsElement.Hide();
        hangarContextMenu.Hide();
    }

    protected override void OnShow()
    {
        PopulateShipList();
        shipDetailsElement.Hide();
        hangarContextMenu.Hide();
    }

    private void PopulateShipList()
    {
        shipListContainer.Clear();
        shipButtons.Clear();

        foreach (var shipBlueprint in playerSaveData.AvailableShips)
        {
            VisualElement shipItemClone = uiAssetsDatabase.ShipListItem.Instantiate();

            Button btn = shipItemClone.Q<Button>("ShipItemBtn");
            Label nameLabel = shipItemClone.Q<Label>("ShipItemName");
            Label classLabel = shipItemClone.Q<Label>("ShipItemClass");
            Label statusLabel = shipItemClone.Q<Label>("ShipItemStatus");

            nameLabel.text = shipBlueprint.ShipName;

            // Show status label if already in roster
            if (playerSaveData.IsShipInRoster(shipBlueprint)) statusLabel.style.display = DisplayStyle.Flex;
            else statusLabel.style.display = DisplayStyle.None;

            btn.clicked += () =>
            {
                OnShipItemClicked(shipBlueprint, btn);
            };

            shipButtons.Add(shipBlueprint, btn);
            shipListContainer.Add(shipItemClone);
        }
    }

    private void OnShipItemClicked(ShipBlueprintData ship, Button clickedButton)
    {
        // Clear style for other buttons
        foreach (var btn in shipButtons.Values)
        {
            btn.style.borderLeftColor = new Color(0, 1, 1, 0.2f);
            btn.style.backgroundColor = Color.clear;
        }

        // show style for currently viewed ship
        clickedButton.style.borderLeftColor = Color.cyan;
        clickedButton.style.backgroundColor = new Color(0, 1, 1, 0.1f);

        shipDetailsElement.UpdateDetails(ship);
    }

    public void UpdateShipButton(ShipBlueprintData ship)
    {
        var statusLabel = shipButtons[ship].Q<Label>("ShipItemStatus");
        if (playerSaveData.IsShipInRoster(ship)) statusLabel.style.display = DisplayStyle.Flex;
        else statusLabel.style.display = DisplayStyle.None;
    }
}

public class ShipDetailsElement
{
    private PlayerSaveData playerSaveData;

    private HangarScreen hangarScreen;
    private VisualElement container;
    private Label shipTitle;
    private VisualElement shipVisual;
    private Label ShipDescription;
    private Button toggleRosterBtn;

    private ShipBlueprintData currentShip;

    public ShipDetailsElement(VisualElement root, HangarScreen hangarScreen, PlayerSaveData playerSaveData)
    {
        this.playerSaveData = playerSaveData;

        this.hangarScreen = hangarScreen;
        container = root;
        shipTitle = root.Q<Label>("ShipTitle");

        toggleRosterBtn = root.Q<Button>("ToggleRosterBtn");
        toggleRosterBtn.clicked += OnToggleRosterBtnClicked;

        container.style.display = DisplayStyle.None;
    }

    public void Hide()
    {
        container.style.display = DisplayStyle.None;
    }

    public void UpdateDetails(ShipBlueprintData data)
    {
        if (data == null)
        {
            Hide();
            return;
        }
        currentShip = data;
        
        // update ship name
        shipTitle.text = currentShip.ShipName.ToUpper();

        // update roster button
        if (playerSaveData.IsShipInRoster(currentShip)) toggleRosterBtn.text = "REMOVE FROM ROSTER";
        else toggleRosterBtn.text = "ADD TO ROSTER";

        container.style.display = DisplayStyle.Flex;
    }

    public void OnToggleRosterBtnClicked()
    {
        if (currentShip == null) return;

        // Add or remove from roster
        if (playerSaveData.IsShipInRoster(currentShip)) playerSaveData.RemoveFromRoster(currentShip);
        else playerSaveData.AddToRoster(currentShip);

        UpdateDetails(currentShip);
        hangarScreen.UpdateShipButton(currentShip);
    }
}

public class HangarContextMenu
{
    private VisualElement container;

    public HangarContextMenu(VisualElement root)
    {
        container = root;
    }

    public void Hide()
    {
        container.style.display = DisplayStyle.None;
    }
}