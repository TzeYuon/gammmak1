using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class PlayerShipAuthoring : MonoBehaviour
{
    [Header("Health Stats")]
    [SerializeField] float Health = 10f;

    [Header("Movement Stats")]
    [SerializeField] float MaxSpeed = 15f;
    [SerializeField] float Acceleration = 10;
    [SerializeField] float TurnSpeed = 15;
    [SerializeField] float BrakingDistance = 1;

    [Header("Biomass Collection Stats")]
    [SerializeField] float BiomassCollectionRadius = 10f;

    class PlayerShipAuthoringBaker : Baker<PlayerShipAuthoring>
    {
        public override void Bake(PlayerShipAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Ship movement stats
            AddComponent(entity, new ShipMovementStatsComponent
            {
                MaxSpeed = authoring.MaxSpeed,
                Acceleration = authoring.Acceleration,
                TurnSpeed = authoring.TurnSpeed,
                BrakingDistance = authoring.BrakingDistance,
            });

            AddComponent(entity, new ShipVelocity
            {
                Value = float2.zero,
            });

            // Identity components
            AddComponent(entity, new PlayerTag());

            // Health
            AddComponent(entity, new HealthComponent()
            {
                Value = authoring.Health,
                MaxValue = authoring.Health,
            });
            AddBuffer<DamageEvent>(entity);

            // Death tag
            AddComponent(entity, new DeadTag());
            SetComponentEnabled<DeadTag>(entity, false);

            // Add disabled selected tag
            AddComponent<SelectedTag>(entity);
            SetComponentEnabled<SelectedTag>(entity, false);

            AddComponent<MoveCommand>(entity);
            SetComponentEnabled<MoveCommand>(entity, false);

            // Collection radius for biomass
            AddComponent(entity, new ShipCollectionRadiusComponent 
            { 
                RadiusSq = authoring.BiomassCollectionRadius * authoring.BiomassCollectionRadius 
            });

            // Add children to linked group to delete them together when the ship dies
            var linkedGroup = AddBuffer<LinkedEntityGroup>(entity);
            var allTransforms = authoring.GetComponentsInChildren<Transform>();

            foreach (var t in allTransforms)
            {
                var childEntity = GetEntity(t.gameObject, TransformUsageFlags.Dynamic);
                linkedGroup.Add(new LinkedEntityGroup { Value = childEntity });
            }

            // Add hitboxes to buffer
            var hitboxBuffer = AddBuffer<Hitbox>(entity);
            float maxRadiusSq = 0; // radius of a circle that contains all hitboxes
            var hitboxes = authoring.GetComponentsInChildren<ShipHitboxAuthoring>();

            foreach (var hb in hitboxes)
            {
                Vector3 localPos = authoring.transform.InverseTransformPoint(hb.transform.position);
                float localRot = (hb.transform.eulerAngles.z - authoring.transform.eulerAngles.z) * Mathf.Deg2Rad;

                hitboxBuffer.Add(new Hitbox
                {
                    Shape = hb.Shape,
                    LocalOffset = new float2(localPos.x, localPos.y),
                    LocalRotation = localRot,
                    Size = hb.Size
                });

                float distToCenter = math.length(new float2(localPos.x, localPos.y));
                float maxDimension = math.max(hb.Size.x, hb.Size.y);
                float totalReach = distToCenter + maxDimension;

                if (totalReach * totalReach > maxRadiusSq)
                {
                    maxRadiusSq = totalReach * totalReach;
                }
            }

            AddComponent(entity, new HitboxCombined { MaxBoundingRadiusSq = maxRadiusSq });
        }
    }
}
