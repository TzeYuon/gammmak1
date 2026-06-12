using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Map
{
    public static class MapGenerator
    {
        private static MapConfig config;

        // ALL nodes by layer:
        private static readonly List<List<LevelNode>> nodes = new List<List<LevelNode>>();

        public static Map GetMap(MapConfig conf)
        {
            if (conf == null)
            {
                Debug.LogWarning("Config was null in MapGenerator.Generate()");
                return null;
            }

            config = conf;
            nodes.Clear();

            for (int i = 0; i < conf.Layers.Count; i++)
                PlaceLayer(i);

            List<List<Vector2Int>> paths = GeneratePaths();

            SetUpConnections(paths);

            RemoveCrossConnections();

            // select all the nodes with connections:
            List<LevelNode> nodesList = nodes.SelectMany(n => n).Where(n => n.Incoming.Count > 0 || n.Outgoing.Count > 0).ToList();

            return new Map(nodesList, new());
        }

        private static void PlaceLayer(int layerIndex)
        {
            MapLayer layer = config.Layers[layerIndex];
            List<LevelNode> nodesOnThisLayer = new List<LevelNode>();

            for (int i = 0; i < config.GridWidth; i++)
            {
                var supportedRandomNodeTypes =
                    config.RandomNodes.Where(t => config.NodeBlueprints.Any(b => b == t)).ToList();
                NodeType nodeType = Random.Range(0f, 1f) < layer.randomizeNodes && supportedRandomNodeTypes.Count > 0
                    ? supportedRandomNodeTypes.Random()
                    : layer.nodeType;
                LevelNode node = new (nodeType, new Vector2Int(i, layerIndex));
                nodesOnThisLayer.Add(node);
            }

            nodes.Add(nodesOnThisLayer);
        }

        private static void SetUpConnections(List<List<Vector2Int>> paths)
        {
            foreach (List<Vector2Int> path in paths)
            {
                for (int i = 0; i < path.Count - 1; ++i)
                {
                    LevelNode node = GetNode(path[i]);
                    LevelNode nextNode = GetNode(path[i + 1]);
                    node.AddOutgoing(nextNode);
                    nextNode.AddIncoming(node);
                }
            }
        }

        private static void RemoveCrossConnections()
        {
            for (int i = 0; i < config.GridWidth - 1; ++i)
                for (int j = 0; j < config.Layers.Count - 1; ++j)
                {
                    LevelNode node = GetNode(new Vector2Int(i, j));
                    if (node == null || node.HasNoConnections()) continue;
                    LevelNode right = GetNode(new Vector2Int(i + 1, j));
                    if (right == null || right.HasNoConnections()) continue;
                    LevelNode top = GetNode(new Vector2Int(i, j + 1));
                    if (top == null || top.HasNoConnections()) continue;
                    LevelNode topRight = GetNode(new Vector2Int(i + 1, j + 1));
                    if (topRight == null || topRight.HasNoConnections()) continue;

                    // Debug.Log("Inspecting node for connections: " + node.point);
                    if (!node.Outgoing.Any(element => element.Equals(topRight))) continue;
                    if (!right.Outgoing.Any(element => element.Equals(top))) continue;

                    // Debug.Log("Found a cross node: " + node.point);

                    // we managed to find a cross node:
                    // 1) add direct connections:
                    node.AddOutgoing(top);
                    top.AddIncoming(node);

                    right.AddOutgoing(topRight);
                    topRight.AddIncoming(right);

                    float rnd = Random.Range(0f, 1f);
                    if (rnd < 0.2f)
                    {
                        // remove both cross connections:
                        // a) 
                        node.RemoveOutgoing(topRight);
                        topRight.RemoveIncoming(node);
                        // b) 
                        right.RemoveOutgoing(top);
                        top.RemoveIncoming(right);
                    }
                    else if (rnd < 0.6f)
                    {
                        // a) 
                        node.RemoveOutgoing(topRight);
                        topRight.RemoveIncoming(node);
                    }
                    else
                    {
                        // b) 
                        right.RemoveOutgoing(top);
                        top.RemoveIncoming(right);
                    }
                }
        }

        private static LevelNode GetNode(Vector2Int p)
        {
            if (p.y >= nodes.Count) return null;
            if (p.x >= nodes[p.y].Count) return null;

            return nodes[p.y][p.x];
        }

        private static Vector2Int GetFinalNode()
        {
            int y = config.Layers.Count - 1;
            if (config.GridWidth % 2 == 1)
                return new Vector2Int(config.GridWidth / 2, y);

            return Random.Range(0, 2) == 0
                ? new Vector2Int(config.GridWidth / 2, y)
                : new Vector2Int(config.GridWidth / 2 - 1, y);
        }

        private static List<List<Vector2Int>> GeneratePaths()
        {
            Vector2Int finalNode = GetFinalNode();
            var paths = new List<List<Vector2Int>>();
            int numOfStartingNodes = config.NumOfStartingNodes.GetValue();
            int numOfPreBossNodes = config.NumOfPreBossNodes.GetValue();

            List<int> candidateXs = new List<int>();
            for (int i = 0; i < config.GridWidth; i++)
                candidateXs.Add(i);

            candidateXs.Shuffle();
            IEnumerable<int> startingXs = candidateXs.Take(numOfStartingNodes);
            List<Vector2Int> startingPoints = (from x in startingXs select new Vector2Int(x, 0)).ToList();

            candidateXs.Shuffle();
            IEnumerable<int> preBossXs = candidateXs.Take(numOfPreBossNodes);
            List<Vector2Int> preBossPoints = (from x in preBossXs select new Vector2Int(x, finalNode.y - 1)).ToList();

            int numOfPaths = Mathf.Max(numOfStartingNodes, numOfPreBossNodes) + Mathf.Max(0, config.ExtraPaths);
            for (int i = 0; i < numOfPaths; ++i)
            {
                Vector2Int startNode = startingPoints[i % numOfStartingNodes];
                Vector2Int endNode = preBossPoints[i % numOfPreBossNodes];
                List<Vector2Int> path = Path(startNode, endNode);
                path.Add(finalNode);
                paths.Add(path);
            }

            return paths;
        }

        // Generates a random path bottom up.
        private static List<Vector2Int> Path(Vector2Int fromPoint, Vector2Int toPoint)
        {
            int toRow = toPoint.y;
            int toCol = toPoint.x;

            int lastNodeCol = fromPoint.x;

            List<Vector2Int> path = new List<Vector2Int> { fromPoint };
            List<int> candidateCols = new List<int>();
            for (int row = 1; row < toRow; ++row)
            {
                candidateCols.Clear();

                int verticalDistance = toRow - row;
                int horizontalDistance;

                int forwardCol = lastNodeCol;
                horizontalDistance = Mathf.Abs(toCol - forwardCol);
                if (horizontalDistance <= verticalDistance)
                    candidateCols.Add(lastNodeCol);

                int leftCol = lastNodeCol - 1;
                horizontalDistance = Mathf.Abs(toCol - leftCol);
                if (leftCol >= 0 && horizontalDistance <= verticalDistance)
                    candidateCols.Add(leftCol);

                int rightCol = lastNodeCol + 1;
                horizontalDistance = Mathf.Abs(toCol - rightCol);
                if (rightCol < config.GridWidth && horizontalDistance <= verticalDistance)
                    candidateCols.Add(rightCol);

                int randomCandidateIndex = Random.Range(0, candidateCols.Count);
                int candidateCol = candidateCols[randomCandidateIndex];
                Vector2Int nextPoint = new Vector2Int(candidateCol, row);

                path.Add(nextPoint);

                lastNodeCol = candidateCol;
            }

            path.Add(toPoint);

            return path;
        }
    }
}