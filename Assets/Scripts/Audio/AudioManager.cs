using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Audio;

public enum VolumeCategory
{
    Master,
    FX,
    Music,
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;
    public AudioMixer mainMixer;

    [Header("Sound Files")]
    public AudioDB Database;

    private void Start()
    {
        if (Database != null && Database.MainMenuMusic != null)
        {
            PlayBGM(Database.MainMenuMusic);
        }
    }
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void PlaySFX(AudioClip clip, float volume)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, volume);
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        StopBGM();
        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void SetVolume(VolumeCategory category, float sliderValue)
    {
        string name = "";
        switch (category) 
        {
            case VolumeCategory.Master:
                name = "MasterVol";
                break;
            case VolumeCategory.FX:
                name = "SFXVol";
                break;
            case VolumeCategory.Music:
                name = "MusicVol";
                break;
        }

        sliderValue = Mathf.Clamp(sliderValue, 0.0001f, 1f);
        mainMixer.SetFloat(name, Mathf.Log10(sliderValue) * 20);
    }

    public float GetVolumeSliderValue(VolumeCategory category)
    {
        string name = "";
        switch (category)
        {
            case VolumeCategory.Master:
                name = "MasterVol";
                break;
            case VolumeCategory.FX:
                name = "SFXVol";
                break;
            case VolumeCategory.Music:
                name = "MusicVol";
                break;
        }

        mainMixer.GetFloat(name, out float value);
        return Mathf.Pow(10f, value / 20f); ;
    }

    public void StopBGM()
    {
        if (bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }
    }
}