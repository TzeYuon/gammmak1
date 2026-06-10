using System.Runtime.CompilerServices;
using Unity.Entities;
using UnityEngine;

class EnemyDatabaseAuthoring : MonoBehaviour
{
    public GameObject StandardPrefab;

    class Baker : Baker<EnemyDatabaseAuthoring>
    {
        public override void Bake(EnemyDatabaseAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new EnemyPrefabDatabase
            {
                StandardPrefab = GetEntity(authoring.StandardPrefab, TransformUsageFlags.Dynamic),
            });
        }
    }
}

public enum EnemyType : byte
{
    Standard,
}

public struct EnemyPrefabDatabase : IComponentData
{
    public Entity StandardPrefab;

    [MethodImpl(MethodImplOptions.AggressiveInlining)] 
    public Entity GetPrefab(EnemyType type)
    {
        return type switch
        {
            EnemyType.Standard => StandardPrefab,
            _ => Entity.Null,
        };
    }
}