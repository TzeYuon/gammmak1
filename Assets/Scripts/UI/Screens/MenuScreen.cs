using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MenuScreen : UIScreen
{
    SceneManager sceneManager;

    public MenuScreen(
        UIScreenManager manager,
        VisualElement root,
        SceneManager sceneManager
        ) : base(manager, root)
    {
        this.sceneManager = sceneManager;
    }

    protected override void OnShow()
    {
        Root.Q<Button>("StartButton").clicked += OnStartClicked;
        Root.Q<Button>("SettingsButton").clicked += OnSettingsClicked;
        Root.Q<Button>("ExitButton").clicked += OnExitClicked;
    }

    protected override void OnHide()
    {
        Root.Q<Button>("StartButton").clicked -= OnStartClicked;
        Root.Q<Button>("SettingsButton").clicked -= OnSettingsClicked;
        Root.Q<Button>("ExitButton").clicked -= OnExitClicked;
    }

    #region Events

    void OnStartClicked()
    {
        sceneManager.LoadScene(SceneId.GameplayScene);
    }

    void OnSettingsClicked()
    {
        uiScreenManager.Show(uiScreenManager.SettingsScreen);
    }

    void OnExitClicked()
    {
        //ApplicationEvents.OnApplicationQuit?.Invoke(this, new());
        Application.Quit();
    }

    #endregion
}