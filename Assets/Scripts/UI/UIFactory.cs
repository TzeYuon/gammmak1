using UnityEngine;
using UnityEngine.UIElements;

public static class UIFactory
{
    public static VisualElement BuildReinforcementBtn(string name, int cost)
    {
        VisualElement reinforcementBtn = UIScreenManager.UIAssetsDatabase.ReinforcementBtn.Instantiate().Q<VisualElement>("ReinforcementBtn");
        reinforcementBtn.Q<Label>("ShipNameLbl").text = name;
        reinforcementBtn.Q<Label>("CostLbl").text = "" + cost;

        return reinforcementBtn;
    }

    public static VisualElement BuildWeaponListItem(WeaponData weapon)
    {
        VisualElement weaponListItem = UIScreenManager.UIAssetsDatabase.WeaponListItem.Instantiate().Q<VisualElement>("WeaponsListItem");

        UpdateWeaponListItem(weaponListItem, weapon);

        return weaponListItem;
    }

    public static void UpdateWeaponListItem(VisualElement container, WeaponData weapon)
    {
        // Update name
        Label weaponName = container.Q<Label>("WeaponName");
        weaponName.text = weapon.WeaponName.ToUpper();

        // Update equip cost pips
        PipContainerVisualElement equipCost = container.Q<PipContainerVisualElement>("EquipCost");
        equipCost.MaxPips = weapon.EquipCost;
        equipCost.Value = weapon.EquipCost;

        // Update upgrade pips
        PipContainerVisualElement pipContainer = container.Q<PipContainerVisualElement>("PipContainer");
        pipContainer.MaxPips = weapon.MaxUpgradeLevel;
        pipContainer.Value = weapon.UpgradeLevel;
    }

    public static Button BuildWeaponMountItem(WeaponMount mount)
    {
        Button weaponMountItem = UIScreenManager.UIAssetsDatabase.WeaponMountItem.Instantiate().Q<Button>("WeaponMountItem");

        UpdateWeaponMountItem(weaponMountItem, mount);

        return weaponMountItem;
    }

    public static void UpdateWeaponMountItem(Button weaponMountItem, WeaponMount mount)
    {
        // Display equipped weapon if not empty
        Label emptyLabel = weaponMountItem.Q<Label>("EmptyLabel");
        VisualElement weaponListItemContainer = weaponMountItem.Q<VisualElement>("WeaponListItemContainer");
        weaponListItemContainer.Clear();
        if (mount.EquippedWeapon == null)
        {
            emptyLabel.style.display = DisplayStyle.Flex;
            weaponListItemContainer.style.display = DisplayStyle.None;
        }
        else
        {
            emptyLabel.style.display = DisplayStyle.None;
            weaponListItemContainer.style.display = DisplayStyle.Flex;
            var weaponListItem = BuildWeaponListItem(mount.EquippedWeapon);
            weaponListItem.pickingMode = PickingMode.Ignore;
            weaponListItem.style.width = 125;
            weaponListItem.RemoveFromClassList("list-item");
            weaponListItem.AddToClassList("inactive-list-item");
            weaponListItemContainer.Add(weaponListItem);
        }
    }

    public static void BuildWeaponUpgradeContainer(VisualElement container, WeaponData weapon)
    {
        container.Clear();
        for (int i = 0; i < weapon.MaxUpgradeLevel; i++)
        {
            var upgradeItem = UIScreenManager.UIAssetsDatabase.WeaponUpgradeItem.Instantiate();

            if (i >= weapon.UpgradeLevel) upgradeItem.AddToClassList("upgrade-item--locked");

            Label upgradeLevelLabel = upgradeItem.Q<Label>("UpgradeLevelLabel");
            upgradeLevelLabel.text = (i + 1).ToString();

            // update effect text
            Label upgradeEffectLabel = upgradeItem.Q<Label>("UpgradeEffectLabel");
            string allEffectsText = "";
            foreach (var effect in weapon.Upgrades[i].Effects)
            {
                allEffectsText += effect.GetDescription() + "\n";
            }
            upgradeEffectLabel.text = allEffectsText.TrimEnd();

            container.Add(upgradeItem);
        }
    }

    public static VisualElement BuildSystemItem(ShipSystem system)
    {
        VisualElement systemItem = UIScreenManager.UIAssetsDatabase.ShipSystemItem.Instantiate();

        // update system name
        Label systemName = systemItem.Q<Label>("SystemName");
        systemName.text = system.SystemID.ToString().ToUpper();

        UpdateSystemItem(system, systemItem);

        return systemItem;
    }

    public static void UpdateSystemItem(ShipSystem system, VisualElement systemItem)
    {
        // update upgrade pips
        PipContainerVisualElement pipContainer = systemItem.Q<PipContainerVisualElement>("PipContainer");
        pipContainer.MaxPips = system.MaxUpgradeLevel;
        pipContainer.Value = system.UpgradeLevel;

        // update upgrade effect ui
        Label upgradeEffectLabel = systemItem.Q<Label>("SystemStats");
        if (system.CanBeUpgraded())
        {
            string allEffectsText = "";
            foreach (var effect in system.Upgrades[system.UpgradeLevel].Effects)
            {
                allEffectsText += effect.GetDescription() + "\n";
            }
            upgradeEffectLabel.text = allEffectsText.TrimEnd();
        }
        else
        {
            upgradeEffectLabel.text = "";
        }

        // update upgrade btn
        Button upgradeBtn = systemItem.Q<Button>("UpgradeBtn");
        if (system.CanBeUpgraded())
        {
            upgradeBtn.style.visibility = Visibility.Visible;

            int cost = system.GetUpgradeCost();
            if (RunManager.SaveData.UpgradeCoresRemaining >= cost)
            {
                upgradeBtn.text = $"UPGRADE [<color=#CCFF00>+{cost}</color>]";
            }
            else
            {
                upgradeBtn.text = $"UPGRADE [<color=#FF003C>+{cost}</color>]";
            }
        }
        else
        {
            upgradeBtn.style.visibility = Visibility.Hidden;
        }

        // update refund btn
        Button refundBtn = systemItem.Q<Button>("RefundBtn");
        if (system.CanBeRefunded())
        {
            refundBtn.style.visibility = Visibility.Visible;
            refundBtn.text = $"REFUND [<color=#CCFF00>+{system.GetRefundValue()}</color>]";
        }
        else
        {
            refundBtn.style.visibility = Visibility.Hidden;
        }
    }

    public static MapNodeVisualElement BuildMapNode(LevelNode node)
    {
        MapNodeVisualElement nodeElement = UIScreenManager.UIAssetsDatabase.MapNode.Instantiate().Q<MapNodeVisualElement>();
        nodeElement.Init(node);
        return nodeElement;
    }
}
