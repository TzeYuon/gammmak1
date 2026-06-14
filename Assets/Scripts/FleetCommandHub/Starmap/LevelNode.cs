using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace Starmap
{
    public enum NodeType
    {
        Encounter,
        EliteEncounter,
        Store,
        Boss,
    }

    public class LevelNode
    {
        public readonly Vector2Int Position;
        public readonly HashSet<LevelNode> Incoming = new();
        public readonly HashSet<LevelNode> Outgoing = new();
        public readonly NodeType NodeType;

        public LevelNode(NodeType nodeType, Vector2Int point)
        {
            NodeType = nodeType;
            Position = point;
        }

        public bool HasNoConnections()
        {
            return Incoming.Count == 0 && Outgoing.Count == 0;
        }
    }
}