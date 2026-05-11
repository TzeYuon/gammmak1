using UnityEngine;
using UnityEngine.UIElements;

public class PauseScreen : UIScreen
{
    public PauseScreen(UIScreenManager manager, VisualElement root) : base(manager, root)
    {
        Root.Q<Button>("ResumeButton").clicked += OnResumeClicked;
        Root.Q<Button>("SettingsButton").clicked += OnSettingsClicked;
        Root.Q<Button>("ExitButton").clicked += OnExitClicked;
    }

    protected override void OnShow()
    {
    }

    protected override void OnHide()
    {
    }

    #region Events
    void OnResumeClicked()
    {
        uiScreenManager.BackToPrevScreen();
    }
    void OnSettingsClicked()
    {
        uiScreenManager.Show(uiScreenManager.SettingsScreen);
    }
    void OnExitClicked()
    {
        Debug.LogWarning("Not implemented");
    }
    #endregion
}