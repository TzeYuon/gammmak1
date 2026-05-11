using System;
using UnityEngine;
using UnityEngine.UIElements;

public class SettingsScreen : UIScreen
{
    Slider master;
    Slider fx;
    Slider music;
    Slider ui;

    public SettingsScreen(UIScreenManager manager, VisualElement root) : base(manager, root)
    {
        //master = Root.Q<Slider>("MasterVolumeSlider");
        //master.RegisterCallback<ChangeEvent<float>>(OnMasterVolumeSliderChange);

        //fx = Root.Q<Slider>("FXVolumeSlider");
        //fx.RegisterCallback<ChangeEvent<float>>(OnFXVolumeSliderChange);

        //music = Root.Q<Slider>("MusicVolumeSlider");
        //music.RegisterCallback<ChangeEvent<float>>(OnMusicVolumeSliderChange);

        //ui = Root.Q<Slider>("UIVolumeSlider");
        //ui.RegisterCallback<ChangeEvent<float>>(OnUIVolumeSliderChange);

        Root.Q<Button>("ExitButton").clicked += OnExitClicked;
    }

    protected override void OnShow()
    {
        //master.value = audioManager.GetVolume(VolumeCategory.Master);
        //fx.value = audioManager.GetVolume(VolumeCategory.FX);
        //music.value = audioManager.GetVolume(VolumeCategory.Music);
        //ui.value = audioManager.GetVolume(VolumeCategory.UI);
    }

    protected override void OnHide()
    {
    }

    #region Events

    //private void OnMasterVolumeSliderChange(ChangeEvent<float> evt)
    //{
    //    SetVolume(evt, VolumeCategory.Master);
    //}

    //private void OnFXVolumeSliderChange(ChangeEvent<float> evt)
    //{
    //    SetVolume(evt, VolumeCategory.FX);
    //}

    //private void OnMusicVolumeSliderChange(ChangeEvent<float> evt)
    //{
    //    SetVolume(evt, VolumeCategory.Music);
    //}

    //private void OnUIVolumeSliderChange(ChangeEvent<float> evt)
    //{
    //    SetVolume(evt, VolumeCategory.UI);
    //}

    //void SetVolume(ChangeEvent<float> evt, VolumeCategory category)
    //{
    //    audioManager.SetVolume(category, evt.newValue);
    //}


    void OnExitClicked()
    {
        uiScreenManager.BackToPrevScreen();
    }

    #endregion
}