using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MapGenerator
{
    public static Starmap GenerateMap(MapGenerationConfig mapConfig)
    {
        float connectionRadius = mapConfig.ConnectionRadius;

        int totalLayers = 0;
        int maxLanes = 0;
        foreach (var layerConfig in mapConfig.LayerConfigs)
        {
            totalLayers += layerConfig.NumLayers;
            maxLanes = Mathf.Max(maxLanes, layerConfig.NumNodes.max);
        }
        maxLanes = Mathf.RoundToInt(Mathf.Max(1, maxLanes) * 1.5f); // add some padding for variation

        int currentLayer = 0;
        List<List<LevelNode>> grid = new();
        foreach (var config in mapConfig.LayerConfigs)
        {
            // Generate nodes for layers
            for (int i = 0; i < config.NumLayers; i++)
            {
                List<LevelNode> currentLayerNodes = new();

                // Randomise number of nodes in layer
                int nodeCount = Mathf.Max(1, Random.Range(config.NumNodes.min, config.NumNodes.max + 1));

                // If single node (like Start or Boss), lock it to the center
                if (nodeCount == 1)
                {
                    currentLayerNodes.Add(new LevelNode(currentLayer, maxLanes / 2));
                }
                else
                {
                    // Assign random lanes to nodes
                    int offset = (maxLanes - nodeCount) / 3;
                    List<int> availableLanes = MathUtils.GetUniqueNumbers(i, nodeCount, offset, maxLanes - offset);

                    for (int j = 0; j < nodeCount; j++)
                    {
                        int randIndex = Random.Range(0, availableLanes.Count);
                        int chosenLane = availableLanes[randIndex];
                        availableLanes.RemoveAt(randIndex);
                        
                        currentLayerNodes.Add(new LevelNode(currentLayer, chosenLane));
                    }
                }

                grid.Add(currentLayerNodes);
                currentLayer++;
            }
        }

        // Wire the nodes connections
        for (int l = 0; l < totalLayers - 1; l++)
        {
            var layerA = grid[l];
            var layerB = grid[l + 1];

            // Wire closest connections for each node
            // - This ensures that all nodes have a connection that is also the shortest possible to the previous/next layer
            // Then Wire nodes that are close enough to each other
            // - This just creates more paths
            foreach (LevelNode nodeA in layerA)
            {
                if (layerB.Count == 1)
                {
                    nodeA.Outgoing.Add(layerB[0]);
                    continue;
                }

                // Add the closest connection
                LevelNode closestB = null;
                float minDistance = float.MaxValue;
                foreach (LevelNode nodeB in layerB)
                {
                    float dist = Vector2.Distance(nodeA.Pos, nodeB.Pos);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestB = nodeB;
                    }
                }
                nodeA.Outgoing.Add(closestB);

                // Add any other nodes that are close enough
                foreach (LevelNode nodeB in layerB)
                {
                    float dist = Vector2.Distance(nodeA.Pos, nodeB.Pos);
                    if (dist <= connectionRadius) nodeA.Outgoing.Add(nodeB);
                }
            }

            foreach (LevelNode nodeB in layerB)
            {
                if (layerA.Count == 1)
                {
                    layerA[0].Outgoing.Add(nodeB);
                    continue;
                }

                // Add the closest connection
                LevelNode closestA = null;
                float minDistance = float.MaxValue;
                foreach (LevelNode nodeA in layerA)
                {
                    float dist = Vector2.Distance(nodeA.Pos, nodeB.Pos);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestA = nodeA;
                    }
                }
                closestA.Outgoing.Add(nodeB);

                // Add any other nodes that are close enough
                foreach (LevelNode nodeA in layerA)
                {
                    float dist = Vector2.Distance(nodeA.Pos, nodeB.Pos);
                    if (dist <= connectionRadius) nodeA.Outgoing.Add(nodeB);
                }
            }
        }

        // flatten the nodes list
        List<LevelNode> finalNodes = new();
        List<LevelNode> startNodes = new();
        for (int l = 0; l < grid.Count; l++)
        {
            foreach (var node in grid[l])
            {
                finalNodes.Add(node);
                if (l == 0) startNodes.Add(node);
            }
        }

        return new Starmap(finalNodes, startNodes);
    }
}