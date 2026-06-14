using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class EnemyAuthoring : MonoBehaviour
{
    class EnemyAuthoringBaker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new LevelEntityTag());
            AddComponent(entity, new SwarmTag());
            AddComponent(entity, new SwarmVelocityComponent());
            AddComponent(entity, new SwarmMovementStatsComponent());
            AddComponent(entity, new HealthComponent
            {
                MaxValue = 1,
                Value = 1,
            });
            AddBuffer<DamageEvent>(entity);

            AddComponent(entity, new CollisionRadius
            {
                Value = 1,
            });

            AddComponent(entity, new DeadTag());
            SetComponentEnabled<DeadTag>(entity, false);
        }
    }
}
