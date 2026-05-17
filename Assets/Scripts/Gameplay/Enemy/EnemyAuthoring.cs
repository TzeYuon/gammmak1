using Unity.Entities;
using UnityEngine;

class EnemyAuthoring : MonoBehaviour
{
    class EnemyAuthoringBaker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new SwarmTag());

            AddComponent(entity, new PathFollowerComponent());
            AddComponent(entity, new VelocityComponent());

            AddComponent(entity, new HealthComponent
            {
                MaxValue = 1,
                Value = 1,
            });

            AddComponent(entity, new DeadTag());
            SetComponentEnabled<DeadTag>(entity, false);
        }
    }
}
