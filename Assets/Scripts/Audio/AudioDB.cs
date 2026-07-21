using UnityEngine;

// This allows you to right-click in Unity and create a "Sound Database" asset!
[CreateAssetMenu(fileName = "AudioDB", menuName = "Audio/SoundDatabase")]
public class AudioDB : ScriptableObject
{
    [Header("Music")]
    public AudioClip MainMenuMusic;
    public AudioClip LevelMusic;
    public AudioClip BossFightMusic1;
    public AudioClip BossFightMusic2;

    [Header("Weapons")]
    public AudioClip Artillery;
    public AudioClip Cannon;
    public AudioClip GungnirLance;
    public AudioClip HeavyLaser;
    public AudioClip PigeonMissiles;
    public AudioClip PointDefenseLaser;
    public AudioClip Railgun;
    public AudioClip SquallLauncher;
    public AudioClip StormCannon;
    public AudioClip SuperHeavyArtillery;
    public AudioClip TachyonLaser;
    public AudioClip TempestLauncher;
    public AudioClip TrebuchetMissiles;

    [Header("SFX")]
    public AudioClip Explosion;

    [Header("UI")]
    public AudioClip ButtonClick;
}