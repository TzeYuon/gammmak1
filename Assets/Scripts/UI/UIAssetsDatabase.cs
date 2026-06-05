using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "UIAssetsDatabase", menuName = "UI Assets Database")]
public class UIAssetsDatabase : ScriptableObject
{
    public VisualTreeAsset ShipListItem;
    public VisualTreeAsset ShipSystemItem;

    public VisualTreeAsset WeaponListItem;
    public VisualTreeAsset WeaponMountItem;
    public VisualTreeAsset WeaponUpgradeItem;
}
