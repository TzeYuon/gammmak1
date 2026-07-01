using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Used to show where the movement target would be placed before right click is released. Because the previous movement target needs to still be there while a new one gets created
/// </summary>
class MovementTargetProjectionVisualAuthoring : MonoBehaviour
{
    class Baker : Baker<MovementTargetProjectionVisualAuthoring>
    {
        public override void Bake(MovementTargetProjectionVisualAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ShipReference()
            {
                Value = Entity.Null,
            });

            AddComponent<NeedsShipReferenceTag>(entity);

            AddComponent(entity, new MovementTargetProjectionVisualComponent());
        }
    }
}

public struct MovementTargetProjectionVisualComponent : IComponentData { }