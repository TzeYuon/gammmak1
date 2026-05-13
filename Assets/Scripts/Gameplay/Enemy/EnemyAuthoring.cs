using Unity.Entities;
using UnityEngine;

class EnemyAuthoring : MonoBehaviour
{
    class EnemyAuthoringBaker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new PathFollowerComponent());
            AddComponent(entity, new VelocityComponent());
        }
    }
}
