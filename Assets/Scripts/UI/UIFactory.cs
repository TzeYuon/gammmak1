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

    public static VisualElement BuildRewardItem(LevelReward levelReward)
    {
        VisualElement rewardItem = UIScreenManager.UIAssetsDatabase.RewardItem.Instantiate().Q<VisualElement>("RewardItem");

        VisualElement rewardIcon = rewardItem.Q<VisualElement>("RewardIcon");
        Label rewardLbl = rewardItem.Q<Label>("RewardLbl");

        Label rewardDetailsLbl = rewardItem.Q<Label>("RewardDetailsLbl");
        rewardDetailsLbl.text = levelReward.Details;

        switch (levelReward.Type)
        {
            case RewardType.None:
                break;
            case RewardType.UpgradeCores:
                rewardIcon.AddToClassList("reward-cores");
                rewardLbl.text = "UPGRADE CORES: +" + levelReward.UpgradeCores;
                break;
            case RewardType.WeaponBlueprint:
                rewardIcon.AddToClassList("reward-weapon");
                rewardLbl.text = "NEW WEAPON: +" + levelReward.WeaponBlueprint.WeaponName;
                break;
            case RewardType.ShipBlueprint:
                rewardIcon.AddToClassList("reward-ship");
                rewardLbl.text = "NEW HULL: +" + levelReward.ShipBlueprint.ShipName;
                break;
            case RewardType.MothershipRepair: // NOT IMPLEMENTED/UNUSED
                rewardIcon.AddToClassList("reward-repair");
                rewardLbl.text = "REPAIR: +";
                break;
        }

        return rewardItem;
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

    public static WeaponMountElement BuildWeaponMountItem(WeaponMount mount, Vector2 pos)
    {
        WeaponMountElement weaponMountItem = new();
        weaponMountItem.style.top = pos.y;
        weaponMountItem.style.left = pos.x;

        weaponMountItem.Init(mount);

        return weaponMountItem;
    }

    public static void UpdateWeaponMountItem(WeaponMountElement weaponMountItem)
    {
        weaponMountItem.Rebuild();
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

    public static VisualElement BuildSystemItem(ShipSystemState system)
    {
        VisualElement systemItem = UIScreenManager.UIAssetsDatabase.ShipSystemItem.Instantiate();

        // update system name
        Label systemName = systemItem.Q<Label>("SystemName");
        systemName.text = system.SystemID.ToString().ToUpper();

        UpdateSystemItem(system, systemItem);

        return systemItem;
    }

    public static void UpdateSystemItem(ShipSystemState system, VisualElement systemItem)
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
            foreach (var effect in system.Definition.Upgrades[system.UpgradeLevel].Effects)
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
