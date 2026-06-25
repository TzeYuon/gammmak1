using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

class HiveAuthoring : MonoBehaviour
{
    [Header("Trickle Spawner Settings")]
    [SerializeField] List<SpawnConfig> trickleSpawnConfigs = new();

    [Header("Surge Spawning Settings")]
    [SerializeField] EnemyType surgeEnemyType = EnemyType.Standard;
    [SerializeField] int surgeSpawnCount = 500;

    [Header("Collision Settings")]
    [SerializeField] float Size = 5;

    [Header("Health Settings")]
    [SerializeField] float maxHealth = 500;

    class HiveAuthoringBaker : Baker<HiveAuthoring>
    {
        public override void Bake(HiveAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);

            AddComponent<LevelEntityTag>(entity);
            AddComponent(entity, new HiveComponent
            {
                SurgeEnemyType = authoring.surgeEnemyType,
                SurgeSpawnCount = authoring.surgeSpawnCount,
                SurgeSpawnRate = 100,
                SurgeSpawnAccumulator = 0,
                SurgePendingSpawns = 0,

                Size = authoring.Size
            });

            DynamicBuffer<TrickleSpawnerData> ambientBuffer = AddBuffer<TrickleSpawnerData>(entity);
            foreach (var config in authoring.trickleSpawnConfigs)
            {
                ambientBuffer.Add(new TrickleSpawnerData
                {
                    Type = config.Type,
                    SpawnCount = config.SpawnCount,
                    Interval = config.Interval,
                    NextSpawnTime = config.Interval,
                });
            }

            AddComponent(entity, new HealthComponent
            {
                MaxValue = authoring.maxHealth,
                Value = authoring.maxHealth,
            });
            AddBuffer<DamageEvent>(entity);

            AddComponent(entity, new CollisionRadius
            {
                Value = authoring.Size,
            });

            AddComponent<DormantHiveTag>(entity);
            AddComponent<ActiveHiveTag>(entity);
            SetComponentEnabled<ActiveHiveTag>(entity, false);

            AddComponent(entity, new DeadTag());
            SetComponentEnabled<DeadTag>(entity, false);
        }
    }
}

[System.Serializable]
public struct SpawnConfig
{
    public EnemyType Type;
    public int SpawnCount;
    public float Interval;
}