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
    [SerializeField] PlayerSaveData playerSaveData;
    [SerializeField] UIAssetsDatabase uiAssetsDatabase;

    public VisualElement Root { get; private set; }
    public IUIScreen MenuScreen { get; private set; }
    public IUIScreen SettingsScreen { get; private set; }
    public IUIScreen PauseScreen { get; private set; }
    public IUIScreen GameplayScreen { get; private set; }
    public IUIScreen FleetCommandHubScreen { get; private set; }

    public IUIScreen CurScreen;
    Stack<IUIScreen> ScreenHistory = new();
    List<IUIScreen> Screens;

    private void Awake()
    {
        Root = GetComponent<UIDocument>().rootVisualElement;
        MenuScreen = new MenuScreen(this, Root.Q<VisualElement>("MenuScreen"), sceneManager);
        SettingsScreen = new SettingsScreen(this, Root.Q<VisualElement>("SettingsScreen"));
        PauseScreen = new PauseScreen(this, Root.Q<VisualElement>("PauseScreen"));
        GameplayScreen = new GameplayScreen(this, Root.Q<VisualElement>("GameplayScreen"), playerSaveData);
        FleetCommandHubScreen = new FleetCommandHubScreen(this, Root.Q<VisualElement>("FleetCommandHubScreen"), playerSaveData, uiAssetsDatabase);

        if (playerSaveData == null) Debug.LogWarning("UIManager no save data assigned");

        Screens = new List<IUIScreen>
        {
            MenuScreen,
            SettingsScreen,
            PauseScreen,
            GameplayScreen,
            FleetCommandHubScreen,
        };

        // All screens start hidden.
        HideAll();

        GameStateSystem.OnStateChanged += OnStateChanged; // to change screens when changing game states
    }

    private void OnDisable()
    {
        GameStateSystem.OnStateChanged -= OnStateChanged;
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
            ShowRootScreen(ScreenHistory.Pop());
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

    public void ClearHistory()
    {
        ScreenHistory.Clear();
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
    public void ShowRootScreen(IUIScreen screen)
    {
        HideAll(); 
        ClearHistory();
        Show(screen);
    }

    #region EVENTS
    void OnStateChanged(GameState prevState, GameState state)
    {
        switch (state)
        {
            case GameState.Gameplay:
                ShowRootScreen(GameplayScreen);
                break;
            case GameState.MainMenu:
                ShowRootScreen(MenuScreen);
                break;
            case GameState.FleetCommandHub:
                ShowRootScreen(FleetCommandHubScreen);
                break;
        }
    }
    #endregion
}