using UnityEngine;
using UnityEngine.UIElements;

public static class UIFactory
{
    public static VisualElement BuildWeaponListItem(WeaponData weapon, UIAssetsDatabase db)
    {
        VisualElement weaponListItem = db.WeaponListItem.Instantiate().Q<VisualElement>("WeaponsListItem");

        UpdateWeaponListItem(weaponListItem, weapon);

        return weaponListItem;
    }

    public static void UpdateWeaponListItem(VisualElement container, WeaponData weapon)
    {
        // Update name
        Label weaponName = container.Q<Label>("WeaponName");
        weaponName.text = weapon.WeaponName;

        // Update upgrade pips
        PipContainerVisualElement pipContainer = container.Q<PipContainerVisualElement>("PipContainer");
        pipContainer.MaxPips = weapon.MaxUpgradeLevel;
        pipContainer.Value = weapon.UpgradeLevel;
    }

    public static Button BuildWeaponMountItem(WeaponMount mount, UIAssetsDatabase db)
    {
        Button weaponMountItem = db.WeaponMountItem.Instantiate().Q<Button>("WeaponMountItem");

        UpdateWeaponMountItem(weaponMountItem, mount, db);

        return weaponMountItem;
    }

    public static void UpdateWeaponMountItem(Button weaponMountItem, WeaponMount mount, UIAssetsDatabase db)
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
            var weaponListItem = BuildWeaponListItem(mount.EquippedWeapon, db);
            weaponListItem.pickingMode = PickingMode.Ignore;
            weaponListItem.style.width = 100;
            weaponListItem.RemoveFromClassList("list-item");
            weaponListItem.AddToClassList("inactive-list-item");
            weaponListItemContainer.Add(weaponListItem);
        }
    }

    public static void BuildWeaponUpgradeContainer(VisualElement container, WeaponData weapon, UIAssetsDatabase db)
    {
        container.Clear();
        for (int i = 0; i < weapon.MaxUpgradeLevel; i++)
        {
            var upgradeItem = db.WeaponUpgradeItem.Instantiate();

            if (i >= weapon.UpgradeLevel) upgradeItem.AddToClassList("upgrade-item--locked");

            Label upgradeLevelLabel = upgradeItem.Q<Label>("UpgradeLevelLabel");
            upgradeLevelLabel.text = i.ToString();

            // update effect text

            container.Add(upgradeItem);
        }
    }
}
