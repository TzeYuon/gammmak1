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
    public static UIScreenManager Instance { get; private set; }

    [SerializeField] SceneManager sceneManager;
    [SerializeField] PlayerSaveData playerSaveData;
    [SerializeField] HangarShipPreviewManager hangarShipPreviewManager;
    [SerializeField] AudioManager audioManager;

    [SerializeField] UIAssetsDatabase uiAssetsDatabase;
    public static UIAssetsDatabase UIAssetsDatabase { get { return Instance.uiAssetsDatabase; } }

    public VisualElement Root { get; private set; }
    public IUIScreen MenuScreen { get; private set; }
    public IUIScreen SettingsScreen { get; private set; }
    public IUIScreen PauseScreen { get; private set; }
    public IUIScreen HowToPlayScreen { get; private set; }
    public IUIScreen GameplayScreen { get; private set; }
    public IUIScreen FleetCommandHubScreen { get; private set; }

    public IUIScreen CurScreen;
    Stack<IUIScreen> ScreenHistory = new();
    List<IUIScreen> Screens;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        Root = GetComponent<UIDocument>().rootVisualElement;
        MenuScreen = new MenuScreen(this, Root.Q<VisualElement>("MenuScreen"), sceneManager);
        SettingsScreen = new SettingsScreen(this, audioManager, Root.Q<VisualElement>("SettingsScreen"));
        PauseScreen = new PauseScreen(this, Root.Q<VisualElement>("PauseScreen"));
        HowToPlayScreen = new HowToPlayScreen(this, Root.Q<VisualElement>("HowToPlayScreen"));
        GameplayScreen = new GameplayScreen(this, Root.Q<VisualElement>("GameplayScreen"), playerSaveData);
        FleetCommandHubScreen = new FleetCommandHubScreen(this, Root.Q<VisualElement>("FleetCommandHubScreen"), playerSaveData, hangarShipPreviewManager);

        if (playerSaveData == null) Debug.LogWarning("UIManager no save data assigned");

        Screens = new List<IUIScreen>
        {
            MenuScreen,
            SettingsScreen,
            PauseScreen,
            HowToPlayScreen,
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

    private void LateUpdate()
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
            CurScreen = ScreenHistory.Peek();
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
                if (prevState == GameState.Paused) BackToPrevScreen();
                else ShowRootScreen(GameplayScreen);
                break;
            case GameState.MainMenu:
                if (prevState == GameState.Settings) BackToPrevScreen();
                if (prevState == GameState.HowToPlay) BackToPrevScreen();
                else ShowRootScreen(MenuScreen);
                break;
            case GameState.FleetCommandHub:
                if (prevState == GameState.Paused) BackToPrevScreen();
                else ShowRootScreen(FleetCommandHubScreen);
                break;
            case GameState.Paused:
                if (prevState == GameState.Settings) BackToPrevScreen();
                if (prevState == GameState.HowToPlay) BackToPrevScreen();
                else Show(PauseScreen, true);
                break;
            case GameState.Settings:
                Show(SettingsScreen, true);
                break;
            case GameState.HowToPlay:
                Show(HowToPlayScreen, true);
                break;
        }
    }
    #endregion
}