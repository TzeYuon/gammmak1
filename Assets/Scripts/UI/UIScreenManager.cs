using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// Manages UI Document screens using a stack.
/// </summary>
/// <remarks>Using pattern explained here: https://discussions.unity.com/t/quizu-managing-menu-screens-in-ui-toolkit/310272</remarks>
[RequireComponent(typeof(UIDocument))]
public class UIScreenManager : MonoBehaviour
{
    [SerializeField] SceneManager sceneManager;

    public VisualElement Root { get; private set; }
    public IUIScreen MenuScreen { get; private set; }
    public IUIScreen SettingsScreen { get; private set; }
    public IUIScreen PauseScreen { get; private set; }
    public IUIScreen GameplayScreen { get; private set; }

    public IUIScreen CurScreen;
    Stack<IUIScreen> ScreenHistory = new();
    List<IUIScreen> Screens;

    private void Awake()
    {
        Root = GetComponent<UIDocument>().rootVisualElement;
        MenuScreen = new MenuScreen(this, Root.Q<VisualElement>("MenuScreen"), sceneManager);
        SettingsScreen = new SettingsScreen(this, Root.Q<VisualElement>("SettingsScreen"));
        PauseScreen = new PauseScreen(this, Root.Q<VisualElement>("PauseScreen"));
        GameplayScreen = new GameplayScreen(this, Root.Q<VisualElement>("GameplayScreen"));

        Screens = new List<IUIScreen>
        {
            MenuScreen,
            SettingsScreen,
            PauseScreen,
            GameplayScreen,
        };

        // All screens start hidden.
        HideAll();

        sceneManager.OnSceneChanged += OnSceneChanged; // to change screens when changing scenes
    }

    private void OnDisable()
    {
        sceneManager.OnSceneChanged -= OnSceneChanged;
    }

    private void Update()
    {
        if (CurScreen != null)
        {
            CurScreen.DoUpdate();
        }
    }

    /// <summary>
    /// Hide current screen, display previous screen.
    /// Does nothing if there is no previous screen.
    /// </summary>
    public void BackToPrevScreen()
    {
        if (ScreenHistory.Count > 1)
        {
            CurScreen.Hide();
            ScreenHistory.Pop();
            Show(ScreenHistory.Pop());
        }
    }

    public void HideAll()
    {
        foreach (IUIScreen screen in Screens)
        {
            screen.Hide();
        }
        ClearHistory();
    }


    /// <summary>
    /// Show a new screen and push it to screen history.
    /// </summary>
    /// <param name="displayAbovePrevScreen">If true, previous screen is not hidden.</param>
    public void Show(IUIScreen screen, bool displayAbovePrevScreen = false)
    {
        if (!displayAbovePrevScreen && CurScreen != null)
        {
            // Hide the previous screen.
            CurScreen.Hide();
        }

        // Show the new screen.
        CurScreen = screen;
        ScreenHistory.Push(screen);
        screen.Show();
    }

    /// <summary>
    /// Show a screen as the new root screen, hiding all screens and clearing screen history.
    /// </summary>
    public void ShowNewRootScreen(IUIScreen screen)
    {
        HideAll();
        ClearHistory();
        Show(screen);
    }

    public void ClearHistory()
    {
        ScreenHistory.Clear();
    }

    #region EVENTS
    void OnSceneChanged(object sender, SceneChangeArgs args)
    {
        switch (args.newSceneId)
        {
            case SceneId.BootstrapScene:
                HideAll();
                break;
            case SceneId.MainMenuScene:
                ShowNewRootScreen(MenuScreen);
                break;
            case SceneId.GameplayScene:
                HideAll();
                Show(GameplayScreen);
                break;
        }
    }
    #endregion
}