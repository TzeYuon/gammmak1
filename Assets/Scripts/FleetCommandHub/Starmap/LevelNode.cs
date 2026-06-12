using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace Map
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
        public readonly Vector2Int Point;
        public readonly List<LevelNode> Incoming = new();
        public readonly List<LevelNode> Outgoing = new();
        [JsonConverter(typeof(StringEnumConverter))]
        public readonly NodeType NodeType;

        public LevelNode(NodeType nodeType, Vector2Int point)
        {
            this.NodeType = nodeType;
            this.Point = point;
        }

        public void AddIncoming(LevelNode p)
        {
            if (Incoming.Any(element => element.Equals(p)))
                return;

            Incoming.Add(p);
        }

        public void AddOutgoing(LevelNode p)
        {
            if (Outgoing.Any(element => element.Equals(p)))
                return;

            Outgoing.Add(p);
        }

        public void RemoveIncoming(LevelNode p)
        {
            Incoming.Remove(p);
        }

        public void RemoveOutgoing(LevelNode p)
        {
            Outgoing.Remove(p);
        }

        public bool HasNoConnections()
        {
            return Incoming.Count == 0 && Outgoing.Count == 0;
        }
    }
}