using UnityEngine;
using UnityEngine.UIElements;

public class WeaponsScreen : UIScreen
{
    private PlayerSaveData playerSaveData;

    public WeaponsScreen(
        UIScreenManager manager, 
        VisualElement root,
        PlayerSaveData playerSaveData) : base(manager, root)
    {
        this.playerSaveData = playerSaveData;
    }

    protected override void OnHide()
    {

    }

    protected override void OnShow()
    {
    }
}
