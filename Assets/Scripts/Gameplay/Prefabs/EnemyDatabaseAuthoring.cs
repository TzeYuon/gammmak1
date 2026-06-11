using System.Runtime.CompilerServices;
using Unity.Entities;
using UnityEngine;

class EnemyDatabaseAuthoring : MonoBehaviour
{
    public GameObject StandardHivePrefab;

    public GameObject StandardPrefab;

    class Baker : Baker<EnemyDatabaseAuthoring>
    {
        public override void Bake(EnemyDatabaseAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new HivePrefabDatabase
            {
                StandardPrefab = GetEntity(authoring.StandardHivePrefab, TransformUsageFlags.Dynamic),
            });

            AddComponent(entity, new EnemyPrefabDatabase
            {
                StandardPrefab = GetEntity(authoring.StandardPrefab, TransformUsageFlags.Dynamic),
            });
        }
    }
}

public enum HiveType : byte
{
    Standard,
}

public struct HivePrefabDatabase : IComponentData
{
    public Entity StandardPrefab;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity GetPrefab(HiveType type)
    {
        return type switch
        {
            HiveType.Standard => StandardPrefab,
            _ => Entity.Null,
        };
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