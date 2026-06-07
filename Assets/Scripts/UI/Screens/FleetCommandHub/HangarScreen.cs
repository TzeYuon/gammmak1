using Mono.Cecil.Cil;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class HangarScreen : UIScreen
{
    private ScrollView shipListContainer;
    private Dictionary<ShipBlueprintData, Button> shipButtons = new();

    public ShipDetailsElement ShipDetailsElement;
    public HangarContextMenu HangarContextMenu;

    public HangarScreen(
        UIScreenManager manager, 
        VisualElement root) : base(manager, root)
    {
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

        foreach (var shipBlueprint in PlayerSaveManager.PlayerSaveData.AvailableShips)
        {
            VisualElement shipItemClone = UIScreenManager.UIAssetsDatabase.ShipListItem.Instantiate();

            Button btn = shipItemClone.Q<Button>("ShipItemBtn");
            Label nameLabel = shipItemClone.Q<Label>("ShipItemName");
            Label classLabel = shipItemClone.Q<Label>("ShipItemClass");
            Label statusLabel = shipItemClone.Q<Label>("ShipItemStatus");

            nameLabel.text = shipBlueprint.ShipName.ToUpper();
            classLabel.text = shipBlueprint.ShipClass.ToString().ToUpper();

            // Show status label if already in roster
            if (PlayerSaveManager.PlayerSaveData.IsShipInRoster(shipBlueprint)) statusLabel.style.display = DisplayStyle.Flex;
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
        if (PlayerSaveManager.PlayerSaveData.IsShipInRoster(ship)) statusLabel.style.display = DisplayStyle.Flex;
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
        if (PlayerSaveManager.PlayerSaveData.IsShipInRoster(currentShip)) toggleRosterBtn.text = "REMOVE FROM ROSTER";
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

    public void UpdateAllWeaponMountItems()
    {
        foreach (var mount in weaponMountButtons.Keys)
        {
            UpdateWeaponMountItem(mount);
        }
    }

    public void OnToggleRosterBtnClicked()
    {
        if (currentShip == null) return;

        // Add or remove from roster
        if (PlayerSaveManager.PlayerSaveData.IsShipInRoster(currentShip)) PlayerSaveManager.PlayerSaveData.RemoveFromRoster(currentShip);
        else PlayerSaveManager.PlayerSaveData.AddToRoster(currentShip);

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
    private Label equippedWeaponName;
    private Label viewingWeaponName;
    private VisualElement weaponDetailsContainer;
    private Button equipBtn;
    private Button unequipBtn;

    private VisualElement weaponUpgradeContainer;
    private Button upgradeBtn;
    private Button refundWeaponBtn;

    private VisualElement weaponsScrollViewContainer;
    private List<WeaponID> weaponsListed = new();
    private Dictionary<WeaponData, VisualElement> weaponsListItems = new();

    private WeaponMount currentWeaponMount;
    private WeaponData currentViewingWeapon;

    public WeaponMountContextMenu(VisualElement root, HangarScreen hangarScreen)
    {
        this.hangarScreen = hangarScreen;

        container = root;

        currentWeaponContainer = container.Q<VisualElement>("CurrentWeaponContainer");
        equippedWeaponName = currentWeaponContainer.Q<Label>("EquippedWeaponName");
        viewingWeaponName = currentWeaponContainer.Q<Label>("ViewingWeaponName");
        weaponDetailsContainer = currentWeaponContainer.Q<VisualElement>("WeaponDetailsContainer");

        equipBtn = currentWeaponContainer.Q<Button>("EquipBtn");
        equipBtn.clicked += () =>
        {
            // equip weapon
            currentWeaponMount.EquippedWeapon = currentViewingWeapon;

            // update weapon mount item
            hangarScreen.ShipDetailsElement.UpdateWeaponMountItem(currentWeaponMount);

            // update current weapon ui
            UpdateEquippedWeapon();
        };

        unequipBtn = currentWeaponContainer.Q<Button>("UnequipBtn");
        unequipBtn.clicked += () =>
        {
            // unequip weapon
            currentWeaponMount.EquippedWeapon = null;

            // update weapon mount item
            hangarScreen.ShipDetailsElement.UpdateWeaponMountItem(currentWeaponMount);

            // update current weapon ui
            UpdateEquippedWeapon();
        };

        weaponUpgradeContainer = currentWeaponContainer.Q<VisualElement>("WeaponUpgradeContainer");

        upgradeBtn = currentWeaponContainer.Q<Button>("UpgradeWeaponBtn");
        upgradeBtn.clicked += OnUpgradeBtnClicked;

        refundWeaponBtn = currentWeaponContainer.Q<Button>("RefundWeaponBtn");
        refundWeaponBtn.clicked += OnRefundBtnClicked;

        weaponsScrollViewContainer = container.Q<VisualElement>("WeaponsScrollViewContainer");
    }

    public void ShowWeaponMount(WeaponMount weaponMount)
    {
        currentWeaponMount = weaponMount;
        UpdateEquippedWeapon();
        UpdateWeaponList();
    }

    void UpdateEquippedWeapon()
    {
        if (currentWeaponMount.EquippedWeapon != null) ShowEquippedWeapon(currentWeaponMount.EquippedWeapon);
        else ShowEquippedEmpty();
    }

    void UpdateWeaponList()
    {
        List<WeaponData> weapons = PlayerSaveManager.PlayerSaveData.AvailableWeapons;
        if (weapons.Count != weaponsListItems.Count)
        {
            RebuildWeaponList();
            return;
        }

        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].WeaponID != weaponsListed[i])
            {
                RebuildWeaponList();
                return;
            }
        }

        void RebuildWeaponList()
        {
            weaponsScrollViewContainer.Clear();
            weaponsListItems.Clear();
            weaponsListed.Clear();
            foreach (var weapon in PlayerSaveManager.PlayerSaveData.AvailableWeapons)
            {
                if (weapon == null) continue;

                VisualElement weaponListItem = UIFactory.BuildWeaponListItem(weapon, UIScreenManager.UIAssetsDatabase);

                weaponListItem.RegisterCallback<ClickEvent>(evt =>
                {
                    UpdateViewingWeapon(weapon);
                });

                weaponsScrollViewContainer.Add(weaponListItem);
                weaponsListItems.Add(weapon, weaponListItem);
                weaponsListed.Add(weapon.WeaponID);
            }
        }
    }

    void UpdateWeaponInWeaponsList(WeaponData weapon)
    {
        if (!weaponsListItems.ContainsKey(weapon)) return;

        UIFactory.UpdateWeaponListItem(weaponsListItems[weapon], weapon);
    }

    void UpdateUpgradeContainer()
    {
        if (currentViewingWeapon == null) return;

        UIFactory.BuildWeaponUpgradeContainer(weaponUpgradeContainer, currentViewingWeapon, UIScreenManager.UIAssetsDatabase);
    }

    void UpdateUpgradeBtn()
    {
        if (
            currentViewingWeapon == null // no current weapon
            || currentViewingWeapon.UpgradeLevel >= currentViewingWeapon.MaxUpgradeLevel // max upgrade reached
            ) 
        {
            // hide btn
            upgradeBtn.style.visibility = Visibility.Hidden;
            return; 
        }

        // btn visible
        upgradeBtn.style.visibility = Visibility.Visible;

        // update cost of upgrade on label
    }

    void OnUpgradeBtnClicked()
    {
        if (currentViewingWeapon == null) return;
        if (currentViewingWeapon.UpgradeLevel == currentViewingWeapon.MaxUpgradeLevel) return;

        // check affordable
        var saveData = PlayerSaveManager.PlayerSaveData;
        if (saveData.DataCoresRemaining < currentViewingWeapon.GetUpgradeCost()) return;

        // subtract cost
        saveData.DataCoresRemaining -= currentViewingWeapon.GetUpgradeCost();

        // update data
        currentViewingWeapon.UpgradeLevel++;

        // update ui
        OnUpgradeLevelChanged(currentViewingWeapon);
    }

    void UpdateRefundBtn()
    {
        WeaponData currentWeapon = currentWeaponMount.EquippedWeapon;
        if (
            currentWeapon == null // no current weapon
            || currentWeapon.UpgradeLevel == 0 // min upgrade reached
            )
        {
            // hide btn
            refundWeaponBtn.style.visibility = Visibility.Hidden;
            return;
        }

        // btn visible
        refundWeaponBtn.style.visibility = Visibility.Visible;

        // update refund value on label
    }

    void OnRefundBtnClicked()
    {
        if (currentViewingWeapon == null) return;
        if (currentViewingWeapon.UpgradeLevel == 0) return;

        // refund cost
        var saveData = PlayerSaveManager.PlayerSaveData;
        saveData.DataCoresRemaining += currentViewingWeapon.GetRefundValue();

        // update data
        currentViewingWeapon.UpgradeLevel--;

        // update ui
        OnUpgradeLevelChanged(currentViewingWeapon);
    }

    void OnUpgradeLevelChanged(WeaponData weapon)
    {
        UpdateUpgradeContainer();
        UpdateUpgradeBtn();
        UpdateRefundBtn();
        UpdateWeaponInWeaponsList(weapon); // to update upgrade pips in the weapons list below
        hangarScreen.ShipDetailsElement.UpdateAllWeaponMountItems(); // to update upgrade pips on weapon mounts
    }

    public void UpdateViewingWeapon(WeaponData weapon)
    {
        currentViewingWeapon = weapon;

        // show upgrades
        UpdateUpgradeContainer();
        UpdateUpgradeBtn();
        UpdateRefundBtn();

        if (currentViewingWeapon == null)
        {
            weaponDetailsContainer.style.display = DisplayStyle.None;
        }
        else 
        {
            viewingWeaponName.text = currentViewingWeapon.WeaponName;
            weaponDetailsContainer.style.display = DisplayStyle.Flex; 
        }
    }

    public void ShowEquippedWeapon(WeaponData weapon)
    {
        equippedWeaponName.text = weapon.WeaponName.ToUpper();
        equippedWeaponName.style.color = Color.lightCyan;

        // show unequip btn
        unequipBtn.style.visibility = Visibility.Visible;

        // show equipped weapons details
        if (currentViewingWeapon == null) UpdateViewingWeapon(weapon);
        
    }

    public void ShowEquippedEmpty()
    {
        equippedWeaponName.text = "[ NONE ]";
        equippedWeaponName.style.color = Color.red;

        // hide unequip btn
        unequipBtn.style.visibility = Visibility.Hidden;
    }
}
