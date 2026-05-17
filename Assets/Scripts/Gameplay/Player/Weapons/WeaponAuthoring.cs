using Unity.Entities;
using UnityEngine;

class WeaponAuthoring : MonoBehaviour
{
    class Baker : Baker<WeaponAuthoring>
    {
        public override void Bake(WeaponAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new WeaponState() { FireRate = 12 });
            AddComponent(entity, new WeaponStats { Range = 30 });
        }
    }
}
