using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
public enum AudioType
{
    Artillery,
    Cannon,
    GungnirLance,
    HeavyLaser,
    PigeonMissiles,
    PointDefenseLaser,
    Railgun,
    SquallLauncher,
    StormCannon,
    SuperHeavyArtillery,
    TachyonLaser,
    TempestLauncher,
    TrebuchetMissiles,
    Explosion,
    ButtonClick
}

public struct AudioEvent
{
    public AudioType Type;
}

public struct GlobalAudioQueue : IComponentData
{
    public NativeQueue<AudioEvent> Queue;
}

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial class AudioPlaybackSystem : SystemBase
{
    private Dictionary<AudioType, float> _lastPlayedTimes;

    protected override void OnCreate()
    {
        var queue = new NativeQueue<AudioEvent>(Allocator.Persistent);
        EntityManager.CreateSingleton(new GlobalAudioQueue { Queue = queue });

        _lastPlayedTimes = new Dictionary<AudioType, float>();
    }

    protected override void OnDestroy()
    {
        if (SystemAPI.TryGetSingleton<GlobalAudioQueue>(out var audioSingleton))
        {
            if (audioSingleton.Queue.IsCreated) audioSingleton.Queue.Dispose();
        }
    }

    protected override void OnUpdate()
    {
        if (AudioManager.Instance == null || AudioManager.Instance.Database == null) return;
        if (!SystemAPI.TryGetSingleton<GlobalAudioQueue>(out var audioSingleton)) return;

        float currentTime = (float)SystemAPI.Time.ElapsedTime;

        while (audioSingleton.Queue.TryDequeue(out AudioEvent audioEvent))
        {
            AudioClip clipToPlay = null;
            float volume = 0.2f;
            float cooldownForThisWeapon = 0.05f;

            switch (audioEvent.Type)
            {
                case AudioType.Artillery:
                    clipToPlay = AudioManager.Instance.Database.Artillery;
                    cooldownForThisWeapon = 0.2f;
                    break;

                case AudioType.Cannon:
                    clipToPlay = AudioManager.Instance.Database.Cannon;
                    break;

                case AudioType.GungnirLance:
                    clipToPlay = AudioManager.Instance.Database.GungnirLance;
                    cooldownForThisWeapon = 0.2f;
                    break;

                case AudioType.HeavyLaser:
                    clipToPlay = AudioManager.Instance.Database.HeavyLaser;
                    cooldownForThisWeapon = 0.1f;
                    break;

                case AudioType.PigeonMissiles:
                    clipToPlay = AudioManager.Instance.Database.PigeonMissiles;
                    cooldownForThisWeapon = 0.1f;
                    break;

                case AudioType.PointDefenseLaser:
                    clipToPlay = AudioManager.Instance.Database.PointDefenseLaser;
                    cooldownForThisWeapon = 0.1f;
                    break;

                case AudioType.Railgun:
                    clipToPlay = AudioManager.Instance.Database.Railgun;
                    break;

                case AudioType.SquallLauncher:
                    clipToPlay = AudioManager.Instance.Database.SquallLauncher;
                    cooldownForThisWeapon = 0.2f;
                    break;

                case AudioType.StormCannon:
                    clipToPlay = AudioManager.Instance.Database.StormCannon;
                    break;

                case AudioType.SuperHeavyArtillery:
                    clipToPlay = AudioManager.Instance.Database.SuperHeavyArtillery;
                    cooldownForThisWeapon = 0.2f;
                    break;

                case AudioType.TachyonLaser:
                    clipToPlay = AudioManager.Instance.Database.TachyonLaser;
                    cooldownForThisWeapon = 0.1f;
                    break;

                case AudioType.TempestLauncher:
                    clipToPlay = AudioManager.Instance.Database.TempestLauncher;
                    cooldownForThisWeapon = 0.2f;
                    break;

                case AudioType.TrebuchetMissiles:
                    clipToPlay = AudioManager.Instance.Database.TrebuchetMissiles;
                    cooldownForThisWeapon = 0.2f;
                    break;

                case AudioType.Explosion:
                    clipToPlay = AudioManager.Instance.Database.Explosion;
                    cooldownForThisWeapon = 0.1f;
                    break;

                case AudioType.ButtonClick:
                    clipToPlay = AudioManager.Instance.Database.ButtonClick;
                    volume = 0.8f;
                    cooldownForThisWeapon = 0f;
                    break;
            }

            if (_lastPlayedTimes.TryGetValue(audioEvent.Type, out float lastTime))
            {
                if (currentTime - lastTime < cooldownForThisWeapon)
                {
                    continue;
                }
            }

            if (clipToPlay != null)
            {
                AudioManager.Instance.sfxSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                AudioManager.Instance.PlaySFX(clipToPlay, volume);

                _lastPlayedTimes[audioEvent.Type] = currentTime;
            }
        }
    }
}