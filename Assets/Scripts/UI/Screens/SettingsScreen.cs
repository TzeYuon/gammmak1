using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class SettingsScreen : UIScreen
{
    private AudioManager audioManager;

    private EntityManager entityManager;
    private EntityQuery gameStateQuery;

    private GameState prevState;

    private Slider master;
    private Slider fx;
    private Slider music;

    public SettingsScreen(UIScreenManager manager, AudioManager audioManager, VisualElement root) : base(manager, root)
    {
        this.audioManager = audioManager;

        root.Q<Button>("BackBtn").clicked += OnBackClicked;

        master = root.Q<Slider>("MasterVolumeSlider");
        fx = root.Q<Slider>("FXVolumeSlider");
        music = root.Q<Slider>("MusicVolumeSlider");

        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            entityManager = world.EntityManager;
            gameStateQuery = entityManager.CreateEntityQuery(typeof(GameStateSingleton));
        }

        GameStateSystem.OnStateChanged += HandleStateChange;
    }

    protected override void OnShow()
    {
        master.value = audioManager.GetVolumeSliderValue(VolumeCategory.Master);
        fx.value = audioManager.GetVolumeSliderValue(VolumeCategory.FX);
        music.value = audioManager.GetVolumeSliderValue(VolumeCategory.Music);

        master.RegisterCallback<ChangeEvent<float>>(OnMasterVolumeSliderChange);
        fx.RegisterCallback<ChangeEvent<float>>(OnFXVolumeSliderChange);
        music.RegisterCallback<ChangeEvent<float>>(OnMusicVolumeSliderChange);

        GameStateSystem.OnStateChanged -= HandleStateChange;
    }

    protected override void OnHide()
    {
        master.UnregisterCallback<ChangeEvent<float>>(OnMasterVolumeSliderChange);
        fx.UnregisterCallback<ChangeEvent<float>>(OnFXVolumeSliderChange);
        music.UnregisterCallback<ChangeEvent<float>>(OnMusicVolumeSliderChange);

        GameStateSystem.OnStateChanged += HandleStateChange;
    }

    private void HandleStateChange(GameState prevState, GameState gameState)
    {
        if (gameState == GameState.Settings)
        {
            int prevDepth = GameStateSystem.GetStateDepth(prevState);
            int newDepth = GameStateSystem.GetStateDepth(gameState);

            // only store prev state if it is before current depth
            if (prevDepth < newDepth)
            {
                this.prevState = prevState;
            }
        }
    }

    void OnBackClicked()
    {
        ExitMenu();
    }

    void ExitMenu()
    {
        // return to previous state
        if (gameStateQuery.IsEmptyIgnoreFilter) return;
        gameStateQuery.CompleteDependency();
        var gameState = gameStateQuery.GetSingletonRW<GameStateSingleton>();
        gameState.ValueRW.CurrentState = prevState;
    }

    #region Events
    private void OnMasterVolumeSliderChange(ChangeEvent<float> evt)
    {
        SetVolume(evt.newValue, VolumeCategory.Master);
    }

    private void OnFXVolumeSliderChange(ChangeEvent<float> evt)
    {
        SetVolume(evt.newValue, VolumeCategory.FX);
    }

    private void OnMusicVolumeSliderChange(ChangeEvent<float> evt)
    {
        SetVolume(evt.newValue, VolumeCategory.Music);
    }

    void SetVolume(float value, VolumeCategory category)
    {
        audioManager.SetVolume(category, value);
    }
    #endregion
}