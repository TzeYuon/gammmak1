using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class FleetCommandHubScreen : UIScreen
{
    private PlayerSaveData playerSaveData;

    private IUIScreen hangarScreen;
    private IUIScreen weaponsScreen;
    private IUIScreen starmapScreen;

    private Label upgradeCoreCounter;

    public FleetCommandHubScreen(
        UIScreenManager manager, 
        VisualElement root, 
        PlayerSaveData playerSaveData) : base(manager, root)
    {
        this.playerSaveData = playerSaveData;

        hangarScreen = new HangarScreen(manager, root.Q<VisualElement>("HangarScreen"), this);
        weaponsScreen = new WeaponsScreen(manager, root.Q<VisualElement>("WeaponsScreen"));
        starmapScreen = new StarmapScreen(manager, root.Q<VisualElement>("StarmapScreen"));

        // Register buttons
        root.Q<Button>("HangarBtn").clicked += () => ShowScreen(hangarScreen);
        root.Q<Button>("WeaponsBtn").clicked += () => ShowScreen(weaponsScreen);
        root.Q<Button>("StarmapBtn").clicked += () => ShowScreen(starmapScreen);

        upgradeCoreCounter = root.Q<Label>("UpgradeCoreCounter");
        UpdateUpgradeCoreCounter();
    }

    protected override void OnHide()
    {

    }

    protected override void OnShow()
    {
        // Show default screen
        ShowScreen(hangarScreen);
        UpdateUpgradeCoreCounter();
    }

    private void ShowScreen(IUIScreen screenToShow)
    {
        // Hide all screens
        hangarScreen.Hide();
        weaponsScreen.Hide();
        starmapScreen.Hide();

        // Show the requested one
        screenToShow.Show();
    }

    public void UpdateUpgradeCoreCounter()
    {
        if (playerSaveData == null) return;
        upgradeCoreCounter.text = $"CORES: {playerSaveData.UpgradeCoresRemaining}";
    }
}
