using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class MapGenerationConfig : ScriptableObject
{
    [Tooltip("The max distance that a node will connect to another node when creating extra connections")]
    public float ConnectionRadius;
    public List<LayerRangeConfig> LayerConfigs = new();
}

[System.Serializable]
public class LayerRangeConfig
{
    [Tooltip("The number of layers to be affected by the current settings")]
    public int NumLayers = 1;
    [Tooltip("The range of nodes to spawn in these layers")]
    public IntMinMax NumNodes;

    [Header("Node Type")]
    public WeightedPool<NodeType> NodePool;

    [Header("Environment & Encounters")]
    public WeightedPool<ThreatLevel> ThreatPool;
    public WeightedPool<TerrainType> TerrainPool;
    public WeightedPool<ObjectiveType> ObjectivePool;
    public WeightedPool<EnemyProfile> EnemyPool;

    [Header("Standard Node Rewards")]
    public WeightedPool<RewardType> StandardRewardPool;
    public int BaseUpgradeCoreReward = 200;

    [Header("Elite Node Rewards")]
    public WeightedPool<RewardType> EliteRewardPool;
    public int EliteUpgradeCoreReward = 800;

    [Header("Anomalies")]
    [Range(0f, 1f)] public float StandardAnomalyChance = 0.1f;
    [Range(0f, 1f)] public float EliteAnomalyChance = 0.9f;
    public WeightedPool<AnomalyType> AnomalyPool;
}