using System.Collections.Generic;
using UnityEngine;

public static class MapDecorator
{
    public static void DecorateSector(List<LevelNode> nodes, MapGenerationConfig config)
    {
        if (nodes == null || nodes.Count == 0 || config == null) return;

        // Map each layer to a config
        Dictionary<int, LayerRangeConfig> layerConfigs = new();
        int maxLayerIndex = 0;
        foreach (var layerRange in config.LayerConfigs)
        {
            for (int i = 0; i < layerRange.NumLayers; i++)
            {
                layerConfigs.Add(maxLayerIndex, layerRange);
                maxLayerIndex++;
            }
        }
        maxLayerIndex--;

        // Decorate each node
        foreach (var node in nodes)
        {
            int layerIndex = node.Layer;

            // get the config from the dictionary
            LayerRangeConfig layerConfig = layerConfigs[layerIndex];
            if (layerConfig == null) continue;

            // Assign node type
            if (layerIndex == maxLayerIndex)
            {
                // Force the last index to be the boss
                node.NodeType = NodeType.Boss;
            }
            else
            {
                // get random node type
                node.NodeType = layerConfig.NodePool.GetRandom();
            }

            // generate random encounter settings
            if (node.NodeType == NodeType.Encounter || node.NodeType == NodeType.EliteEncounter || node.NodeType == NodeType.Boss)
            node.TerrainType = layerConfig.TerrainPool.GetRandom();
            node.ObjectiveType = layerConfig.ObjectivePool.GetRandom();
            node.EnemyProfile = layerConfig.EnemyPool.GetRandom();
            node.ThreatLevel = layerConfig.ThreatPool.GetRandom();

            // generate anomaly and reward based on node type
            if (node.NodeType == NodeType.EliteEncounter || node.NodeType == NodeType.Boss)
            {
                node.RewardType = layerConfig.EliteRewardPool.GetRandom();

                // Roll for Elite Anomalies
                float roll = Random.value;
                node.AnomalyType = roll < layerConfig.EliteAnomalyChance
                    ? layerConfig.AnomalyPool.GetRandom()
                    : AnomalyType.None;

                node.UpgradeCoreReward = layerConfig.EliteUpgradeCoreReward;
            }
            else if (node.NodeType == NodeType.Encounter)
            {
                node.RewardType = layerConfig.StandardRewardPool.GetRandom();

                // Roll for Standard Anomalies
                float roll = Random.value;
                node.AnomalyType = roll < layerConfig.StandardAnomalyChance
                    ? layerConfig.AnomalyPool.GetRandom()
                    : AnomalyType.None;

                node.UpgradeCoreReward = layerConfig.BaseUpgradeCoreReward;
            }
            else // shop node
            {
                node.RewardType = RewardType.None;
                node.AnomalyType = AnomalyType.None;
                node.UpgradeCoreReward = 0;
            }
        }
    }
}
