using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class HangarScreen : UIScreen
{
    public PlayerSaveData PlayerSaveData;
    public UIAssetsDatabase UIAssetsDatabase;

    private ScrollView shipListContainer;
    private Dictionary<ShipBlueprintData, Button> shipButtons = new();

    public ShipDetailsElement ShipDetailsElement;
    public HangarContextMenu HangarContextMenu;

    public HangarScreen(
        UIScreenManager manager, 
        VisualElement root, 
        PlayerSaveData playerSaveData, 
        UIAssetsDatabase uiAssetsDatabase) : base(manager, root)
    {
        this.PlayerSaveData = playerSaveData;
        this.UIAssetsDatabase = uiAssetsDatabase;

        shipListContainer = root.Q<ScrollView>("ShipsScrollView");

        ShipDetailsElement = new (root.Q<VisualElement>("DetailsContainer"), this);
        HangarContextMenu = new (root.Q<VisualElement>("HangarContextMenu"), this);
    }

    protected override void OnHide()
    {
        ShipDetailsElement.Hide();
        HangarContextMenu.Hide();
    }

    protected override void OnShow()
    {
        PopulateShipList();
        ShipDetailsElement.Hide();
        HangarContextMenu.Hide();
    }

    private void PopulateShipList()
    {
        shipListContainer.Clear();
        shipButtons.Clear();

        foreach (var shipBlueprint in PlayerSaveData.AvailableShips)
        {
            VisualElement shipItemClone = UIAssetsDatabase.ShipListItem.Instantiate();

            Button btn = shipItemClone.Q<Button>("ShipItemBtn");
            Label nameLabel = shipItemClone.Q<Label>("ShipItemName");
            Label classLabel = shipItemClone.Q<Label>("ShipItemClass");
            Label statusLabel = shipItemClone.Q<Label>("ShipItemStatus");

            nameLabel.text = shipBlueprint.ShipName.ToUpper();
            classLabel.text = shipBlueprint.ShipClass.ToString().ToUpper();

            // Show status label if already in roster
            if (PlayerSaveData.IsShipInRoster(shipBlueprint)) statusLabel.style.display = DisplayStyle.Flex;
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

        ShipDetailsElement.UpdateDetails(ship);
    }

    public void UpdateShipButton(ShipBlueprintData ship)
    {
        var statusLabel = shipButtons[ship].Q<Label>("ShipItemStatus");
        if (PlayerSaveData.IsShipInRoster(ship)) statusLabel.style.display = DisplayStyle.Flex;
        else statusLabel.style.display = DisplayStyle.None;
    }
}

public class ShipDetailsElement
{
    private HangarScreen hangarScreen;
    private VisualElement container;
    private Label shipTitle;
    private Label shipClass;
    private VisualElement shipVisual;
    private Label ShipDescription;
    private Button toggleRosterBtn;

    private VisualElement weaponMountsScrollViewContainer;
    private List<VisualElement> weaponMountButtons = new();

    private ScrollView systemsScrollView;

    private ShipBlueprintData currentShip;

    public ShipDetailsElement(VisualElement root, HangarScreen hangarScreen)
    {
        this.hangarScreen = hangarScreen;
        container = root;
        shipTitle = root.Q<Label>("ShipTitle");
        shipClass = root.Q<Label>("ShipClass");

        weaponMountsScrollViewContainer = root.Q<VisualElement>("WeaponMountScrollViewContainer");

        systemsScrollView = root.Q<ScrollView>("SystemsScrollView");

        toggleRosterBtn = root.Q<Button>("ToggleRosterBtn");
        toggleRosterBtn.clicked += OnToggleRosterBtnClicked;

        container.style.display = DisplayStyle.None;
    }

    public void Hide()
    {
        container.style.display = DisplayStyle.None;
        hangarScreen.HangarContextMenu.Hide();
    }

    public void UpdateDetails(ShipBlueprintData data)
    {
        if (data == null)
        {
            Hide();
            return;
        }
        hangarScreen.HangarContextMenu.Hide();
        currentShip = data;
        
        // update ship name
        shipTitle.text = currentShip.ShipName.ToUpper();

        // update ship class
        shipClass.text = currentShip.ShipClass.ToString().ToUpper();

        // update systems scrollview
        systemsScrollView.Clear();
        foreach (var system in currentShip.ShipSystems)
        {
            VisualElement systemItem = hangarScreen.UIAssetsDatabase.ShipSystemItem.Instantiate();

            Label systemName = systemItem.Q<Label>("SystemName");
            systemName.text = system.SystemID.ToString().ToUpper();

            // add btn action

            systemsScrollView.Add(systemItem);
        }

        // update weapon mounts
        weaponMountButtons.Clear();
        weaponMountsScrollViewContainer.Clear();
        foreach (var mount in currentShip.Mounts)
        {
            Button weaponMountItem = hangarScreen.UIAssetsDatabase.WeaponMountItem.Instantiate().Q<Button>("WeaponMountItem");

            weaponMountItem.clicked += () =>
            {
                OnWeaponMountItemClicked(mount, weaponMountItem);
            };

            weaponMountButtons.Add(weaponMountItem);
            weaponMountsScrollViewContainer.Add(weaponMountItem);
        }

        UpdateToggleRosterBtn();

        container.style.display = DisplayStyle.Flex;
    }

    void UpdateToggleRosterBtn()
    {
        if (hangarScreen.PlayerSaveData.IsShipInRoster(currentShip)) toggleRosterBtn.text = "REMOVE FROM ROSTER";
        else toggleRosterBtn.text = "ADD TO ROSTER";
    }

    private void OnWeaponMountItemClicked(WeaponMount mount, Button clickedButton)
    {
        // Clear style for other buttons
        foreach (var btn in weaponMountButtons)
        {
            btn.style.backgroundColor = Color.clear;
        }

        // show style for currently viewed mount
        clickedButton.style.backgroundColor = new Color(0, 1, 1, 0.1f);

        hangarScreen.HangarContextMenu.ShowWeaponMount(mount);
    }

    public void OnToggleRosterBtnClicked()
    {
        if (currentShip == null) return;

        // Add or remove from roster
        if (hangarScreen.PlayerSaveData.IsShipInRoster(currentShip)) hangarScreen.PlayerSaveData.RemoveFromRoster(currentShip);
        else hangarScreen.PlayerSaveData.AddToRoster(currentShip);

        UpdateToggleRosterBtn();
        hangarScreen.UpdateShipButton(currentShip);
    }
}

public class HangarContextMenu
{
    private VisualElement container;

    private WeaponMountContextMenu weaponMountContext;

    public HangarContextMenu(VisualElement root, HangarScreen hangarScreen)
    {
        container = root;

        weaponMountContext = new(container.Q<VisualElement>("ContextWeaponMount"), hangarScreen);
    }

    public void Hide()
    {
        container.style.display = DisplayStyle.None;
    }

    public void ShowWeaponMount(WeaponMount mount)
    {
        if (mount == null)
        {
            container.style.display = DisplayStyle.None;
            return;
        }

        container.style.display = DisplayStyle.Flex;
        weaponMountContext.ShowWeaponMount(mount);
    }
}

public class WeaponMountContextMenu
{
    private HangarScreen hangarScreen;

    private VisualElement container;

    private CurrentEquippedWeaponElement currentEquippedWeaponElement;

    private VisualElement weaponsScrollViewContainer;

    private WeaponMount currentWeaponMount;

    public WeaponMountContextMenu(VisualElement root, HangarScreen hangarScreen)
    {
        this.hangarScreen = hangarScreen;

        container = root;

        currentEquippedWeaponElement = new(container.Q<VisualElement>("CurrentWeaponContainer"));

        weaponsScrollViewContainer = container.Q<VisualElement>("WeaponsScrollViewContainer");
    }

    public void ShowWeaponMount(WeaponMount weaponMount)
    {
        currentWeaponMount = weaponMount;
        UpdateCurrentWeapon();
        UpdateWeaponsList();
    }

    void UpdateCurrentWeapon()
    {
        if (currentWeaponMount.EquippedWeapon != null) currentEquippedWeaponElement.ShowWeapon(currentWeaponMount.EquippedWeapon);
        else currentEquippedWeaponElement.ShowEmpty();
    }

    void UpdateWeaponsList()
    {
        weaponsScrollViewContainer.Clear();
        foreach (var weapon in hangarScreen.PlayerSaveData.AvailableWeapons)
        {
            if (weapon == null) continue;

            Button weaponListItem = hangarScreen.UIAssetsDatabase.WeaponListItem.Instantiate().Q<Button>("WeaponsListItem");

            Label weaponName = weaponListItem.Q<Label>("WeaponName");
            weaponName.text = weapon.WeaponName;

            weaponListItem.clicked += () =>
            {
                // equip weapon then update current equipped UI
                currentWeaponMount.EquippedWeapon = weapon;
                UpdateCurrentWeapon();
            };

            weaponsScrollViewContainer.Add(weaponListItem);
        }
    }
}

public class CurrentEquippedWeaponElement
{
    private VisualElement currentWeaponContainer;

    private Label weaponName;
    private VisualElement weaponDetailsContainer;

    public CurrentEquippedWeaponElement(VisualElement root)
    {
        currentWeaponContainer = root;

        weaponName = currentWeaponContainer.Q<Label>("WeaponName");
        weaponDetailsContainer = currentWeaponContainer.Q<VisualElement>("WeaponDetailsContainer");
    }

    public void ShowWeapon(WeaponData weapon)
    {
        currentWeaponContainer.style.display = DisplayStyle.Flex;
        weaponName.text = weapon.WeaponName.ToUpper();
        weaponName.style.color = Color.lightCyan;

        // show equipped weapons details
        weaponDetailsContainer.style.display = DisplayStyle.Flex;
    }

    public void ShowEmpty()
    {
        currentWeaponContainer.style.display = DisplayStyle.Flex;
        weaponName.text = "[ NONE ]";
        weaponName.style.color = Color.red;

        // hide weapon details
        weaponDetailsContainer.style.display = DisplayStyle.None;
    }
}