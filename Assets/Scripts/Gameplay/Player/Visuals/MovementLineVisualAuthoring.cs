using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

class MovementLineVisualAuthoring : MonoBehaviour
{
    class Baker : Baker<MovementLineVisualAuthoring>
    {
        public override void Bake(MovementLineVisualAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ShipReference()
            {
                Value = Entity.Null,
            });

            AddComponent<NeedsShipReferenceTag>(entity);

            AddComponent(entity, new MovementLineVisualComponent
            {
                Thickness = 0.15f
            });
        }
    }
}

public struct MovementLineVisualComponent : IComponentData
{
    public float Thickness;
}