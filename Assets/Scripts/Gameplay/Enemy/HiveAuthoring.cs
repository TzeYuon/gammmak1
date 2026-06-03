using Unity.Entities;
using UnityEngine;

class HiveAuthoring : MonoBehaviour
{
    public GameObject Prefab;
    public int SpawnAmount;

    public float EnemySpeed;
    public float DynamicSpeedMultiplier; // Max speed multiplier for enemies that are behind desired position
    public float Density;
    public float Width;

    public float SpawnCooldown;
    public float SpawnTimer;

    public float Size;

    class HiveAuthoringBaker : Baker<HiveAuthoring>
    {
        public override void Bake(HiveAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);

            var prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic);

            AddComponent<LevelEntityTag>(entity);
            AddComponent(entity, new HiveComponent
            {
                Prefab = prefab,
                SpawnAmount = authoring.SpawnAmount,

                EnemySpeed = authoring.EnemySpeed,
                MaxSpeedMult = authoring.DynamicSpeedMultiplier,
                Density = authoring.Density,
                Width = authoring.Width,

                SpawnCooldown = authoring.SpawnCooldown,
                SpawnTimer = authoring.SpawnTimer,

                Size = authoring.Size
            });

            AddComponent(entity, new HealthComponent
            {
                MaxValue = 200,
                Value = 200,
            });
            AddBuffer<DamageEvent>(entity);

            AddComponent(entity, new CollisionRadius
            {
                Value = authoring.Size,
            });

            AddComponent(entity, new DeadTag());
            SetComponentEnabled<DeadTag>(entity, false);
        }
    }
}
