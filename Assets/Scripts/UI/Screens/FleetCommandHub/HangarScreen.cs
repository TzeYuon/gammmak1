using UnityEngine;
using UnityEngine.UIElements;

public class HangarScreen : UIScreen
{
    private PlayerSaveData playerSaveData;

    public HangarScreen(UIScreenManager manager, VisualElement root, PlayerSaveData playerSaveData) : base(manager, root)
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
