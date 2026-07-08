using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewHiveArchetype", menuName = "FleetCommand/Hive Archetype")]
public class HiveArchetypeData : ScriptableObject
{
    [Header("Trickle Settings")]
    public List<SpawnConfig> TrickleSpawns = new List<SpawnConfig>();

    [Header("Surge Settings")]
    public EnemyType SurgeType;
    public bool SurgeCountIgnoreScaling = false;
    public int SurgeCount;
}

[System.Serializable]
public struct SpawnConfig
{
    public EnemyType Type;
    public int SpawnCount;
    public float Interval;
}