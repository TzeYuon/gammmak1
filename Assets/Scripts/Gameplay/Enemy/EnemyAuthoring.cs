using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

class EnemyAuthoring : MonoBehaviour
{
    [Header("Combat stats")]
    [SerializeField] float maxHealth;
    [SerializeField] float damageReduction = 0;
    [SerializeField] float baseSpeed;
    [SerializeField] float hitboxRadius;
    [SerializeField] float damage;

    [Header("Weapon Targeting Variables")]
    [SerializeField] float value;

    [Header("Movement weights")]
    [SerializeField] float separationWeight = 0.5f;
    [SerializeField] float terrainAvoidanceWeight = 1;
    [SerializeField] float noiseWeight = 1;

    class EnemyAuthoringBaker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new LevelEntityTag());
            AddComponent(entity, new HideInFogTag());
            AddComponent(entity, new SwarmTag());
            AddComponent(entity, new SwarmVelocityComponent());
            AddComponent(entity, new SwarmMovementStatsComponent()
            {
                BaseSpeed = authoring.baseSpeed,
                SeparationWeight = authoring.separationWeight,
                TerrainAvoidanceWeight = authoring.terrainAvoidanceWeight,
                NoiseWeight = authoring.noiseWeight,
            });

            // damage
            AddComponent(entity, new SwarmDamageComponent
            {
                Damage = authoring.damage,
            });

            // weapon targeting
            AddComponent(entity, new EnemyValueComponent
            {
                Value = authoring.value,
            });

            // health
            AddComponent(entity, new HealthComponent
            {
                MaxValue = authoring.maxHealth,
                Value = authoring.maxHealth,
                FlatDamageReduction = authoring.damageReduction
            });
            AddBuffer<DamageEvent>(entity);

            AddComponent(entity, new CollisionRadius
            {
                Value = authoring.hitboxRadius,
            });

            AddComponent(entity, new DeadTag());
            SetComponentEnabled<DeadTag>(entity, false);
        }
    }
}
