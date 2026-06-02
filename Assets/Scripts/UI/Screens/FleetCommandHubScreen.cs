using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class FleetCommandHubScreen : UIScreen
{
    private PlayerSaveData playerSaveData;

    private Label currencyLabel;
    private VisualElement hangarContainer;
    private VisualElement loadoutContainer;
    private VisualElement starmapContainer;

    public FleetCommandHubScreen(UIScreenManager manager, VisualElement root, PlayerSaveData playerSaveData) : base(manager, root)
    {
        this.playerSaveData = playerSaveData;

        hangarContainer = root.Q<VisualElement>("HangarContainer");
        loadoutContainer = root.Q<VisualElement>("LoadoutContainer");
        starmapContainer = root.Q<VisualElement>("StarmapContainer");

        currencyLabel = root.Q<Label>("CurrencyLabel");

        // Register buttons
        root.Q<Button>("HangarBtn").clicked += () => ShowScreen(hangarContainer);
        root.Q<Button>("LoadoutBtn").clicked += () => ShowScreen(loadoutContainer);
        root.Q<Button>("StarmapBtn").clicked += () => ShowScreen(starmapContainer);

        // Show default screen
        ShowScreen(hangarContainer);
    }

    protected override void OnHide()
    {

    }

    protected override void OnShow()
    {
        ShowScreen(hangarContainer);
    }

    private void ShowScreen(VisualElement screenToShow)
    {
        // Hide all screens
        hangarContainer.style.display = DisplayStyle.None;
        loadoutContainer.style.display = DisplayStyle.None;
        starmapContainer.style.display = DisplayStyle.None;

        // Show the requested one
        screenToShow.style.display = DisplayStyle.Flex;
    }
}
