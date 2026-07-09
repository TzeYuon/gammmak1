using Unity.Entities;
using UnityEngine;

public class ExplosionOnDeathAuthoring : MonoBehaviour
{
    [SerializeField] private bool targetSwarm;
    [SerializeField] private float radius;
    [SerializeField] private float damage;

    class Baker : Baker<ExplosionOnDeathAuthoring>
    {
        public override void Bake(ExplosionOnDeathAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new ExplosionOnDeath
            {
                TargetSwarm = authoring.targetSwarm,
                Radius = authoring.radius,
                Damage = authoring.damage,
            });
        }
    }
}

public struct ExplosionOnDeath : IComponentData
{
    public bool TargetSwarm;
    public float Radius;
    public float Damage;
}
