using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

class HiveAuthoring : MonoBehaviour
{
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
                Size = authoring.Size
            });

            // spawning
            AddComponent(entity, new HiveSurgeComponent());
            AddBuffer<TrickleSpawnerData>(entity);

            // siege enemy path
            AddBuffer<SiegePathWaypointElement>(entity);

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

            AddComponent<ActiveHiveTag>(entity);
            SetComponentEnabled<ActiveHiveTag>(entity, false);

            AddComponent(entity, new DeadTag());
            SetComponentEnabled<DeadTag>(entity, false);
        }
    }
}