using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class HangarScreen : UIScreen
{
    public PlayerSaveData PlayerSaveData;

    private ScrollView shipListContainer;
    private Dictionary<ShipBlueprintData, Button> shipButtons = new();

    public ShipDetailsElement ShipDetailsElement;
    public HangarContextMenu HangarContextMenu;

    public HangarScreen(
        UIScreenManager manager, 
        VisualElement root, 
        PlayerSaveData playerSaveData) : base(manager, root)
    {
        this.PlayerSaveData = playerSaveData;

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
            VisualElement shipItemClone = UIScreenManager.UIAssetsDatabase.ShipListItem.Instantiate();

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
    private Dictionary<WeaponMount, Button> weaponMountButtons = new();

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
            VisualElement systemItem = UIScreenManager.UIAssetsDatabase.ShipSystemItem.Instantiate();

            // update system name
            Label systemName = systemItem.Q<Label>("SystemName");
            systemName.text = system.SystemID.ToString().ToUpper();

            // update upgrade pips
            PipContainerVisualElement pipContainer = systemItem.Q<PipContainerVisualElement>("PipContainer");
            pipContainer.MaxPips = system.MaxUpgradeLevel;
            pipContainer.Value = system.UpgradeLevel;

            // add btn action

            systemsScrollView.Add(systemItem);
        }

        // update weapon mounts
        weaponMountButtons.Clear();
        weaponMountsScrollViewContainer.Clear();
        foreach (var mount in currentShip.Mounts)
        {
            Button weaponMountItem = UIFactory.BuildWeaponMountItem(mount, UIScreenManager.UIAssetsDatabase);
            weaponMountItem.clicked += () =>
            {
                OnWeaponMountItemClicked(mount, weaponMountItem);
            };

            weaponMountButtons.Add(mount, weaponMountItem);
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
        foreach (var btn in weaponMountButtons.Values)
        {
            btn.style.backgroundColor = Color.clear;
        }

        // show style for currently viewed mount
        clickedButton.style.backgroundColor = new Color(0, 1, 1, 0.1f);

        hangarScreen.HangarContextMenu.ShowWeaponMount(mount);
    }

    public void UpdateWeaponMountItem(WeaponMount mount)
    {
        UIFactory.UpdateWeaponMountItem(weaponMountButtons[mount], mount, UIScreenManager.UIAssetsDatabase);
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

    private VisualElement currentWeaponContainer;
    private Label weaponName;
    private VisualElement weaponDetailsContainer;
    private Button unequipBtn;

    private VisualElement weaponsScrollViewContainer;

    private WeaponMount currentWeaponMount;

    public WeaponMountContextMenu(VisualElement root, HangarScreen hangarScreen)
    {
        this.hangarScreen = hangarScreen;

        container = root;

        currentWeaponContainer = container.Q<VisualElement>("CurrentWeaponContainer");
        weaponName = currentWeaponContainer.Q<Label>("WeaponName");
        weaponDetailsContainer = currentWeaponContainer.Q<VisualElement>("WeaponDetailsContainer");

        unequipBtn = currentWeaponContainer.Q<Button>("UnequipBtn");
        unequipBtn.clicked += () =>
        {
            // unequip weapon
            currentWeaponMount.EquippedWeapon = null;
            
            // update weapon mount item
            hangarScreen.ShipDetailsElement.UpdateWeaponMountItem(currentWeaponMount);
            
            // update current weapon ui
            UpdateCurrentWeapon();
        };

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
        if (currentWeaponMount.EquippedWeapon != null) ShowCurrentWeapon(currentWeaponMount.EquippedWeapon);
        else ShowCurrentEmpty();
    }

    void UpdateWeaponsList()
    {
        weaponsScrollViewContainer.Clear();
        foreach (var weapon in hangarScreen.PlayerSaveData.AvailableWeapons)
        {
            if (weapon == null) continue;

            VisualElement weaponListItem = UIFactory.BuildWeaponListItem(weapon, UIScreenManager.UIAssetsDatabase);
            
            weaponListItem.RegisterCallback<ClickEvent>(evt =>
            {
                // equip weapon then update current equipped UI
                currentWeaponMount.EquippedWeapon = weapon;
                UpdateCurrentWeapon();
                hangarScreen.ShipDetailsElement.UpdateWeaponMountItem(currentWeaponMount);
            });

            weaponsScrollViewContainer.Add(weaponListItem);
        }
    }

    public void ShowCurrentWeapon(WeaponData weapon)
    {
        currentWeaponContainer.style.display = DisplayStyle.Flex;
        weaponName.text = weapon.WeaponName.ToUpper();
        weaponName.style.color = Color.lightCyan;

        // show unequip btn
        unequipBtn.style.visibility = Visibility.Visible;

        // show equipped weapons details
        weaponDetailsContainer.style.display = DisplayStyle.Flex;
    }

    public void ShowCurrentEmpty()
    {
        currentWeaponContainer.style.display = DisplayStyle.Flex;
        weaponName.text = "[ NONE ]";
        weaponName.style.color = Color.red;

        // hide unequip btn
        unequipBtn.style.visibility = Visibility.Hidden;

        // hide weapon details
        weaponDetailsContainer.style.display = DisplayStyle.None;
    }
}
