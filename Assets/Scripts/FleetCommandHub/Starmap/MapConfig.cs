using System.Collections.Generic;
using UnityEngine;

namespace Starmap
{
    [CreateAssetMenu]
    public class MapConfig : ScriptableObject
    {
        public List<NodeType> NodeBlueprints = new List<NodeType>();
        [Tooltip("Nodes that will be used on layers with Randomize Nodes > 0")]
        public List<NodeType> RandomNodes = new List<NodeType>
            {NodeType.Store, NodeType.Encounter};
        public int GridWidth => Mathf.Max(NumOfPreBossNodes.max, NumOfStartingNodes.max);

        public IntMinMax NumOfPreBossNodes;
        public IntMinMax NumOfStartingNodes;

        [Tooltip("Increase this number to generate more paths")]
        public int ExtraPaths;
        public List<MapLayer> Layers;
    }
}