using Unity.Entities;
using UnityEngine;

class WeaponAuthoring : MonoBehaviour
{
    public GameObject ProjectilePrefab;

    class Baker : Baker<WeaponAuthoring>
    {
        public override void Bake(WeaponAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new WeaponState() { FireRate = 12 });
            
            // AddComponent(entity, new HeatmapTargetingComponent { Range = 30, AngleBias = 2f });
            AddComponent(entity, new PrecisionTargetingComponent { Range = 30, AngleBias = 0 });

            AddComponent(entity, new WeaponFiringArc { BaseDirection = new Vector3(0, 1, 0), MaxAngle = 180f });

            // AddComponent(entity, new TurretComponent { TurnSpeed = 5f });

            //AddComponent(entity, new ProjectileWeapon
            //{
            //    Damage = 1,
            //    ProjectilePrefab = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic),
            //    Spread = 1f
            //});

            AddComponent(entity, new HitscanWeapon { Damage = 1 });

            AddComponent(entity, new ShipReference());
        }
    }
}
