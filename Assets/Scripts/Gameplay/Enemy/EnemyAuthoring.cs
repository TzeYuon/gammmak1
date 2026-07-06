using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

class EnemyAuthoring : MonoBehaviour
{
    [SerializeField] float maxHealth;
    [SerializeField] float damageReduction = 0;
    [SerializeField] float baseSpeed;
    [SerializeField] float hitboxRadius;

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
            });
            
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
