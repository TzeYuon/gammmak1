using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class FleetCommandHubScreen : UIScreen
{
    private PlayerSaveData playerSaveData;
    private UIAssetsDatabase uiAssetsDatabase;

    private IUIScreen hangarScreen;
    private IUIScreen starmapScreen;

    public FleetCommandHubScreen(
        UIScreenManager manager, 
        VisualElement root, 
        PlayerSaveData playerSaveData, 
        UIAssetsDatabase uiAssetsDatabase) : base(manager, root)
    {
        this.playerSaveData = playerSaveData;
        this.uiAssetsDatabase = uiAssetsDatabase;

        hangarScreen = new HangarScreen(manager, root.Q<VisualElement>("HangarScreen"), playerSaveData, uiAssetsDatabase);
        starmapScreen = new StarmapScreen(manager, root.Q<VisualElement>("StarmapScreen"));

        // Register buttons
        root.Q<Button>("HangarBtn").clicked += () => ShowScreen(hangarScreen);
        root.Q<Button>("StarmapBtn").clicked += () => ShowScreen(starmapScreen);
    }

    protected override void OnHide()
    {

    }

    protected override void OnShow()
    {
        // Show default screen
        ShowScreen(hangarScreen);
    }

    private void ShowScreen(IUIScreen screenToShow)
    {
        // Hide all screens
        hangarScreen.Hide();
        starmapScreen.Hide();

        // Show the requested one
        screenToShow.Show();
    }
}
