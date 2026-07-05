//using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class HangarScreen : UIScreen
{
    public FleetCommandHubScreen FleetCommandHubScreen;

    private Label hangarWarningLbl;
    private IVisualElementScheduledItem hideWarningTask;

    private ScrollView shipListContainer;
    private Dictionary<ShipBlueprintData, Button> shipButtons = new();

    public ShipDetailsElement ShipDetailsElement;
    public HangarContextMenu HangarContextMenu;

    public HangarScreen(
        UIScreenManager manager, 
        VisualElement root,
        FleetCommandHubScreen fleetCommandHubScreen,
        HangarShipPreviewManager hangarShipPreviewManager) : base(manager, root)
    {
        FleetCommandHubScreen = fleetCommandHubScreen;

        hangarWarningLbl = root.Q<Label>("HangarWarningLbl");

        shipListContainer = root.Q<ScrollView>("ShipsScrollView");

        ShipDetailsElement = new (root.Q<VisualElement>("DetailsContainer"), this, hangarShipPreviewManager);
        HangarContextMenu = new (root.Q<VisualElement>("HangarContextMenu"), this);
    }

    protected override void OnHide()
    {
        ShipDetailsElement.Hide();
        HangarContextMenu.Hide();
        HideWarningImmediate();
    }

    protected override void OnShow()
    {
        PopulateShipList();
        ShipDetailsElement.Hide();
        HangarContextMenu.Hide();
        HideWarningImmediate();
    }

    public void HideWarningImmediate()
    {
        hangarWarningLbl.RemoveFromClassList("warning--visible");
    }

    public void ShowWarning(string message)
    {
        hangarWarningLbl.text = message;
        hangarWarningLbl.AddToClassList("warning--visible");

        hideWarningTask?.Pause();
        hideWarningTask = hangarWarningLbl.schedule.Execute(() =>
        {
            HideWarningImmediate();
        }).StartingIn(2000);
    }

    private void PopulateShipList()
    {
        shipListContainer.Clear();
        shipButtons.Clear();

        foreach (var shipBlueprint in RunManager.SaveData.AvailableShips)
        {
            VisualElement shipItemClone = UIScreenManager.UIAssetsDatabase.ShipListItem.Instantiate();

            Button btn = shipItemClone.Q<Button>("ShipItemBtn");
            Label nameLabel = shipItemClone.Q<Label>("ShipItemName");
            Label classLabel = shipItemClone.Q<Label>("ShipItemClass");
            Label statusLabel = shipItemClone.Q<Label>("ShipItemStatus");

            nameLabel.text = shipBlueprint.ShipName.ToUpper();
            classLabel.text = shipBlueprint.ShipClass.ToString().ToUpper();

            // Show status label if already in roster
            if (RunManager.SaveData.IsShipInRoster(shipBlueprint)) statusLabel.style.display = DisplayStyle.Flex;
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
        if (RunManager.SaveData.IsShipInRoster(ship)) statusLabel.style.display = DisplayStyle.Flex;
        else statusLabel.style.display = DisplayStyle.None;
    }
}

public class ShipDetailsElement
{
    private HangarScreen hangarScreen;
    private VisualElement container;
    private Label shipTitle;
    private Label shipClass;

    HangarShipPreviewManager hangarShipPreviewManager;
    private VisualElement shipVisual;

    private Label shipDescription;
    private Button toggleRosterBtn;

    private PipContainerVisualElement weaponCapacityElement;
    private VisualElement weaponMountsScrollViewContainer;
    private Dictionary<WeaponMount, Button> weaponMountButtons = new();

    private ScrollView systemsScrollView;
    private List<(ShipSystem system, VisualElement element)> systemItems = new();

    public ShipBlueprintData CurrentShip;

    public ShipDetailsElement(VisualElement root, HangarScreen hangarScreen, HangarShipPreviewManager hangarShipPreviewManager)
    {
        this.hangarScreen = hangarScreen;
        container = root;
        shipTitle = root.Q<Label>("ShipTitle");
        shipClass = root.Q<Label>("ShipClass");

        shipVisual = root.Q<VisualElement>("ShipVisualContainer");

        shipDescription = root.Q<Label>("ShipDescription");

        this.hangarShipPreviewManager = hangarShipPreviewManager;

        weaponCapacityElement = root.Q<PipContainerVisualElement>("WeaponCapacityContainer");
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
        CurrentShip = data;

        // update ship name
        shipTitle.text = CurrentShip.ShipName.ToUpper();

        // update ship class
        shipClass.text = CurrentShip.ShipClass.ToString().ToUpper();

        // Update ship preview
        hangarShipPreviewManager.DisplayShip(CurrentShip.ShipID);

        // Update ship stats in description
        UpdateShipStatsDescription();

        // update systems scrollview
        systemsScrollView.Clear();
        systemItems.Clear();
        foreach (var system in CurrentShip.ShipSystems)
        {
            VisualElement systemItem = UIFactory.BuildSystemItem(system);

            // add btn action
            Button upgradeBtn = systemItem.Q<Button>("UpgradeBtn");
            upgradeBtn.clicked += () => { OnSytemItemUpgradeClicked(system, systemItem); };

            Button refundBtn = systemItem.Q<Button>("RefundBtn");
            refundBtn.clicked += () => { OnSytemItemRefundClicked(system, systemItem); };

            systemsScrollView.Add(systemItem);
            systemItems.Add((system, systemItem));
        }

        // Update weapon capacity pips
        UpdateWeaponCapacity();

        // update weapon mounts
        weaponMountButtons.Clear();
        weaponMountsScrollViewContainer.Clear();
        foreach (var mount in CurrentShip.Mounts)
        {
            Button weaponMountItem = UIFactory.BuildWeaponMountItem(mount);
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

    void UpdateShipStatsDescription()
    {
        string stats = "";
        foreach (ShipStat stat in Enum.GetValues(typeof(ShipStat)))
        {
            stats += EnumUtils.GetStatName(stat) + ": " + CurrentShip.GetStat(stat) + "\n";
        }
        shipDescription.text = stats.TrimEnd();
    }

    void OnSytemItemUpgradeClicked(ShipSystem system, VisualElement systemItem)
    {
        if (system == null) return;
        if (!system.CanBeUpgraded()) return;

        // check affordable
        var saveData = RunManager.SaveData;
        int cost = system.GetUpgradeCost();
        if (saveData.UpgradeCoresRemaining < cost) return;

        // subtract cost
        saveData.UpgradeCoresRemaining -= cost;
        hangarScreen.FleetCommandHubScreen.UpdateUpgradeCoreCounter();

        // update data
        system.UpgradeLevel++;

        OnPlayerCurrencyChanged();
        UpdateWeaponCapacity(); // If weapons capacity was affected
        UpdateShipStatsDescription(); // stats were affected
    }

    void OnSytemItemRefundClicked(ShipSystem system, VisualElement systemItem)
    {
        if (system == null) return;
        if (!CurrentShip.CanRefundSystem(system, out string reason))
        {
            // show warning
            hangarScreen.ShowWarning("Cannot Refund System: " + reason);
            return;
        }

        // refund cost
        var saveData = RunManager.SaveData;
        saveData.UpgradeCoresRemaining += system.GetRefundValue();
        hangarScreen.FleetCommandHubScreen.UpdateUpgradeCoreCounter();

        // update data
        system.UpgradeLevel--;

        // update ui
        OnPlayerCurrencyChanged();
        UpdateWeaponCapacity(); // If weapons capacity was affected
        UpdateShipStatsDescription(); // stats were affected
    }

    public void UpdateAllSystemItems()
    {
        foreach (var system in systemItems)
        {
            UIFactory.UpdateSystemItem(system.system, system.element);
        }
    }

    void OnPlayerCurrencyChanged()
    {
        hangarScreen.HangarContextMenu.WeaponMountContext.UpdateUpgradeBtn();
        UpdateAllSystemItems();
    }

    void UpdateToggleRosterBtn()
    {
        if (RunManager.SaveData.IsShipInRoster(CurrentShip))
        {
            toggleRosterBtn.text = "REMOVE FROM ROSTER";
            toggleRosterBtn.SetEnabled(true);
        }
        else
        {
            if (RunManager.SaveData.IsRosterFull())
            {
                toggleRosterBtn.text = "ROSTER FULL";
                toggleRosterBtn.SetEnabled(false);
            }
            else 
            {
                toggleRosterBtn.text = "ADD TO ROSTER";
                toggleRosterBtn.SetEnabled(true); 
            }
        }
    }

    void UpdateWeaponCapacity()
    {
        weaponCapacityElement.MaxPips = CurrentShip.WeaponCapacity;
        weaponCapacityElement.Value = CurrentShip.UsedWeaponCapacity;
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
        UIFactory.UpdateWeaponMountItem(weaponMountButtons[mount], mount);
        UpdateWeaponCapacity();
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
        if (CurrentShip == null) return;

        // Add or remove from roster
        if (RunManager.SaveData.IsShipInRoster(CurrentShip)) RunManager.SaveData.RemoveFromRoster(CurrentShip);
        else RunManager.SaveData.AddToRoster(CurrentShip);

        UpdateToggleRosterBtn();
        hangarScreen.UpdateShipButton(CurrentShip);
    }
}

public class HangarContextMenu
{
    private VisualElement container;

    public WeaponMountContextMenu WeaponMountContext;

    public HangarContextMenu(VisualElement root, HangarScreen hangarScreen)
    {
        container = root;

        WeaponMountContext = new(container.Q<VisualElement>("ContextWeaponMount"), hangarScreen);
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
        WeaponMountContext.ShowWeaponMount(mount);
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
        UpdateViewingWeapon();
        UpdateWeaponList();
    }

    void UpdateEquippedWeapon()
    {
        if (currentWeaponMount.EquippedWeapon != null) ShowEquippedWeapon(currentWeaponMount.EquippedWeapon);
        else ShowEquippedEmpty();
    }

    void UpdateWeaponList()
    {
        List<WeaponData> weapons = RunManager.SaveData.AvailableWeapons;
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
            foreach (var weapon in RunManager.SaveData.AvailableWeapons)
            {
                if (weapon == null) continue;

                VisualElement weaponListItem = UIFactory.BuildWeaponListItem(weapon);

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

        UIFactory.BuildWeaponUpgradeContainer(weaponUpgradeContainer, currentViewingWeapon);
    }

    public void UpdateUpgradeBtn()
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

        // update cost of upgrade on label, update colour if not affordable
        int cost = currentViewingWeapon.GetUpgradeCost();
        if (RunManager.SaveData.UpgradeCoresRemaining >= cost)
        {
            upgradeBtn.text = $"UPGRADE [<color=#CCFF00>+{cost}</color>]";
        }
        else
        {
            upgradeBtn.text = $"UPGRADE [<color=#FF003C>+{cost}</color>]";
        }
    }

    void OnUpgradeBtnClicked()
    {
        if (currentViewingWeapon == null) return;
        if (!currentViewingWeapon.CanBeUpgraded()) return;

        // check affordable
        var saveData = RunManager.SaveData;
        if (saveData.UpgradeCoresRemaining < currentViewingWeapon.GetUpgradeCost()) return;

        // subtract cost
        saveData.UpgradeCoresRemaining -= currentViewingWeapon.GetUpgradeCost();
        OnPlayerCurrencyChanged();

        // update data
        currentViewingWeapon.UpgradeLevel++;

        // update ui
        OnUpgradeLevelChanged(currentViewingWeapon);
    }

    void UpdateRefundBtn()
    {
        if (
            currentViewingWeapon == null // no current weapon
            || currentViewingWeapon.UpgradeLevel == 0 // min upgrade reached
            )
        {
            // hide btn
            refundWeaponBtn.style.visibility = Visibility.Hidden;
            return;
        }

        // btn visible
        refundWeaponBtn.style.visibility = Visibility.Visible;

        // update refund value on label
        refundWeaponBtn.text = $"REFUND [<color=#CCFF00>+{currentViewingWeapon.GetRefundValue()}</color>]";
    }

    void OnRefundBtnClicked()
    {
        if (currentViewingWeapon == null) return;
        if (!currentViewingWeapon.CanBeRefunded()) return;

        // refund cost
        var saveData = RunManager.SaveData;
        saveData.UpgradeCoresRemaining += currentViewingWeapon.GetRefundValue();
        OnPlayerCurrencyChanged();

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

    void OnPlayerCurrencyChanged()
    {
        hangarScreen.ShipDetailsElement.UpdateAllSystemItems();
        hangarScreen.FleetCommandHubScreen.UpdateUpgradeCoreCounter();
    }

    public void UpdateViewingWeapon()
    {
        UpdateViewingWeapon(currentViewingWeapon);
    }

    public void UpdateViewingWeapon(WeaponData weapon)
    {
        currentViewingWeapon = weapon;

        // show upgrades
        UpdateUpgradeContainer();
        UpdateUpgradeBtn();
        UpdateRefundBtn();
        UpdateEquipBtn();

        if (currentViewingWeapon == null)
        {
            viewingWeaponName.text = "<color=#FF0000>[ NONE ]</color>";
            weaponDetailsContainer.style.display = DisplayStyle.None;
        }
        else 
        {
            viewingWeaponName.text = currentViewingWeapon.WeaponName.ToUpper();
            weaponDetailsContainer.style.display = DisplayStyle.Flex;
        }
    }

    void UpdateEquipBtn()
    {
        if (currentViewingWeapon == null)
        {
            equipBtn.style.visibility = Visibility.Hidden;
        }
        else
        {
            if (hangarScreen.ShipDetailsElement.CurrentShip.CanEquipWeaponInMount(currentViewingWeapon, currentWeaponMount)) equipBtn.SetEnabled(true);
            else equipBtn.SetEnabled(false);

            equipBtn.style.visibility = Visibility.Visible;
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
