using Unity.Entities;
using UnityEngine;

public class EconomyAuthoring : MonoBehaviour
{
    [Header("Economy Settings")]
    public float StartingBiomass = 0f;

    class Baker : Baker<EconomyAuthoring>
    {
        public override void Bake(EconomyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new PlayerEconomy
            {
                TotalBiomass = authoring.StartingBiomass,
            });
        }
    }
}
