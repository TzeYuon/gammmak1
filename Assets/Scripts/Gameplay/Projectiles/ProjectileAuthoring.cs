using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum MovementType { Linear, Homing }
public enum TriggerType { Contact, Coordinate, Timer }
public enum PayloadType { Kinetic, Piercing, Explosive }

class ProjectileAuthoring : MonoBehaviour
{
    public MovementType Movement;
    public TriggerType Trigger;
    public PayloadType Payload;

    class ProjectileAuthoringBaker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent<LevelEntityTag>(entity);

            // Universal stats
            AddComponent<ProjectileMovementStats>(entity);
            AddComponent<ProjectileTriggerState>(entity);
            AddComponent<ProjectileTriggerStats>(entity);
            AddComponent<ProjectilePayloadStats>(entity);
            AddComponent<ProjectileLifespan>(entity);

            // Movement
            if (authoring.Movement == MovementType.Linear)
                AddComponent<LinearFlight>(entity);
            else
            {
                AddComponent(entity, new HomingFlight
                {
                    TurnSpeed = 1,
                    DistanceBias = 0,
                    AngleBias = 0,
                });
            }

            // TRIGGER
            if (authoring.Trigger == TriggerType.Contact)
                AddComponent<ContactTrigger>(entity);
            else if (authoring.Trigger == TriggerType.Coordinate)
                AddComponent<CoordinateTrigger>(entity);
            else
                AddComponent<TimerTrigger>(entity);

            // Payload
            if (authoring.Payload == PayloadType.Kinetic)
                AddComponent<KineticPayload>(entity);
            else if (authoring.Payload == PayloadType.Explosive)
                AddComponent<ExplosivePayload>(entity);
            else if (authoring.Payload == PayloadType.Piercing)
                AddComponent<PiercingPayload>(entity);
        }
    }
}
