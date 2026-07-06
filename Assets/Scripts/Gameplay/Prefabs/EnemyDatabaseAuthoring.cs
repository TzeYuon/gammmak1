using System.Runtime.CompilerServices;
using Unity.Entities;
using UnityEngine;

class EnemyDatabaseAuthoring : MonoBehaviour
{
    public GameObject StandardHivePrefab;

    public GameObject StandardPrefab;
    public GameObject GoliathPrefab;
    public GameObject LocustPrefab;
    public GameObject SpitterPrefab;

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
                GoliathPrefab = GetEntity(authoring.GoliathPrefab, TransformUsageFlags.Dynamic),
                //LocustPrefab = GetEntity(authoring.LocustPrefab, TransformUsageFlags.Dynamic),
                //SpitterPrefab = GetEntity(authoring.SpitterPrefab, TransformUsageFlags.Dynamic),
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
    Goliath,
    Locust,
    Spitter
}

public struct EnemyPrefabDatabase : IComponentData
{
    public Entity StandardPrefab;
    public Entity GoliathPrefab;
    public Entity LocustPrefab;
    public Entity SpitterPrefab;

    [MethodImpl(MethodImplOptions.AggressiveInlining)] 
    public Entity GetPrefab(EnemyType type)
    {
        return type switch
        {
            EnemyType.Standard => StandardPrefab,
            EnemyType.Goliath => GoliathPrefab,
            EnemyType.Locust => LocustPrefab,
            EnemyType.Spitter => SpitterPrefab,
            _ => Entity.Null,
        };
    }
}