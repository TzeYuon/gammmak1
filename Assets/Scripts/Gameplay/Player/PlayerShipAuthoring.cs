using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class PlayerShipAuthoring : MonoBehaviour
{
    [Header("Movement Stats")]
    [SerializeField] float Health = 10f;

    [Header("Movement Stats")]
    [SerializeField] float Thrust = 10f;
    [SerializeField] float MaxSpeed = 4f;

    class PlayerShipAuthoringBaker : Baker<PlayerShipAuthoring>
    {
        public override void Bake(PlayerShipAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Ship movement stats
            AddComponent(entity, new ShipMovementStatsComponent
            {
                Thrust = authoring.Thrust,
                MaxSpeed = authoring.MaxSpeed,
            });

            // Identity components
            AddComponent(entity, new PlayerTag());

            // Health
            AddComponent(entity, new HealthComponent()
            {
                Value = authoring.Health,
                MaxValue = authoring.Health,
            });

            // Death tag
            AddComponent(entity, new DeadTag());
            SetComponentEnabled<DeadTag>(entity, false);

            // Add disabled selected tag
            AddComponent<SelectedTag>(entity);
            SetComponentEnabled<SelectedTag>(entity, false);

            AddComponent<MoveCommand>(entity);
            SetComponentEnabled<MoveCommand>(entity, false);


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
