using UnityEngine;
using UnityEngine.UIElements;

public class WeaponsScreen : UIScreen
{
    private PlayerSaveData playerSaveData;
    private UIAssetsDatabase uiAssetsDatabase;

    public WeaponsScreen(
        UIScreenManager manager, 
        VisualElement root,
        PlayerSaveData playerSaveData,
        UIAssetsDatabase uiAssetsDatabase) : base(manager, root)
    {
        this.playerSaveData = playerSaveData;
        this.uiAssetsDatabase = uiAssetsDatabase;
    }

    protected override void OnHide()
    {

    }

    protected override void OnShow()
    {
    }
}
