using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
public enum SFXType
{
    Artillery,
    Cannon,
    Railgun,
    HeavyLaser,
    Explosion
}

public struct AudioEvent
{
    public SFXType Type;
}

public struct GlobalAudioQueue : IComponentData
{
    public NativeQueue<AudioEvent> Queue;
}

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial class AudioPlaybackSystem : SystemBase
{
    // Use a dictionary to track the time each sound was last played
    private Dictionary<SFXType, float> _lastPlayedTimes;

    private float _rapidFireCooldown = 0.05f;

    protected override void OnCreate()
    {
        var queue = new NativeQueue<AudioEvent>(Allocator.Persistent);
        EntityManager.CreateSingleton(new GlobalAudioQueue { Queue = queue });

        _lastPlayedTimes = new Dictionary<SFXType, float>();
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
            if (_lastPlayedTimes.TryGetValue(audioEvent.Type, out float lastTime))
            {
                if (currentTime - lastTime < _rapidFireCooldown)
                {
                    continue;
                }
            }

            AudioClip clipToPlay = null;
            float volume = 1.0f;

            switch (audioEvent.Type)
            {
                case SFXType.Railgun:
                    clipToPlay = AudioManager.Instance.Database.Railgun;
                    break;

                case SFXType.Cannon:
                    clipToPlay = AudioManager.Instance.Database.Cannon;
                    break;

                case SFXType.HeavyLaser:
                    clipToPlay = AudioManager.Instance.Database.HeavyLaser;
                    volume = 0.5f;
                    break;

                case SFXType.Artillery:
                    clipToPlay = AudioManager.Instance.Database.Artillery;
                    break;

                case SFXType.Explosion:
                    clipToPlay = AudioManager.Instance.Database.Explosion;
                    break;
            }

            if (clipToPlay != null)
            {
                // Pitch variance to make the sound less repetitive
                AudioManager.Instance.sfxSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                AudioManager.Instance.PlaySFX(clipToPlay, volume);

                _lastPlayedTimes[audioEvent.Type] = currentTime;
            }
        }
    }
}