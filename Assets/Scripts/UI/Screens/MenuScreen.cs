using System;
using System.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MenuScreen : UIScreen
{
    private EntityManager entityManager;
    private EntityQuery gameStateQuery;

    SceneManager sceneManager;

    public MenuScreen(
        UIScreenManager manager,
        VisualElement root,
        SceneManager sceneManager
        ) : base(manager, root)
    {
        this.sceneManager = sceneManager;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            entityManager = world.EntityManager;
            gameStateQuery = entityManager.CreateEntityQuery(typeof(GameStateSingleton));
        }
    }

    protected override void OnShow()
    {
        Root.Q<Button>("StartButton").clicked += OnStartClicked;
        Root.Q<Button>("HowToPlayButton").clicked += OnHowToPlayClicked;
        Root.Q<Button>("SettingsButton").clicked += OnSettingsClicked;
        Root.Q<Button>("ExitButton").clicked += OnExitClicked;
    }

    protected override void OnHide()
    {
        Root.Q<Button>("StartButton").clicked -= OnStartClicked;
        Root.Q<Button>("HowToPlayButton").clicked -= OnHowToPlayClicked;
        Root.Q<Button>("SettingsButton").clicked -= OnSettingsClicked;
        Root.Q<Button>("ExitButton").clicked -= OnExitClicked;
    }

    #region Events

    void OnStartClicked()
    {
        RunManager.Instance.StartNewRun();
        sceneManager.LoadScene(SceneId.GameplayScene);
    }

    void OnHowToPlayClicked()
    {
        ChangeGameState(GameState.HowToPlay);
    }

    void ChangeGameState(GameState state)
    {
        if (gameStateQuery.IsEmptyIgnoreFilter) return;
        gameStateQuery.CompleteDependency();
        var gameState = gameStateQuery.GetSingletonRW<GameStateSingleton>();
        gameState.ValueRW.CurrentState = state;
    }

    void OnSettingsClicked()
    {
        ChangeGameState(GameState.Settings);
    }

    void OnExitClicked()
    {
        Application.Quit();
    }

    #endregion
}