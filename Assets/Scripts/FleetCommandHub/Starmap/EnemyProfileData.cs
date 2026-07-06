using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyProfile", menuName = "FleetCommand/Enemy Profile")]
public class EnemyProfileData : ScriptableObject
{
    [Header("The Hive Pool")]
    [Tooltip("The pool of different Hive Archetypes that can spawn with this enemy profile.")]
    public WeightedPool<HiveArchetypeData> PossibleHives = new WeightedPool<HiveArchetypeData>();
}
