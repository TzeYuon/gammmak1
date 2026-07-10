using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class MothershipAuthoring : MonoBehaviour
{
    public float Health;
    public float RevealRadius;
    public float BiomassCollectionRadius;

    class MothershipAuthoringBaker : Baker<MothershipAuthoring>
    {
        public override void Bake(MothershipAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Identity components
            AddComponent<LevelEntityTag>(entity);
            AddComponent(entity, new PlayerTag());
            AddComponent(entity, new MothershipTag());

            // Health
            AddComponent(entity, new HealthComponent()
            {
                Value = authoring.Health,
                MaxValue = authoring.Health,
            });
            AddBuffer<DamageEvent>(entity);

            // fog of war component
            AddComponent(entity, new ShipRevealFogRadius
            {
                Value = authoring.RevealRadius,
                ValueSq = authoring.RevealRadius * authoring.RevealRadius,
            });

            // Death tag
            AddComponent(entity, new DeadTag());
            SetComponentEnabled<DeadTag>(entity, false);

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
