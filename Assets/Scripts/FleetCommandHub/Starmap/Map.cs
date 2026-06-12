using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Map
{
    public class Map
    {
        public List<LevelNode> AllNodes;
        public List<LevelNode> PathTaken;

        public Map(List<LevelNode> nodes, List<LevelNode> path)
        {
            this.AllNodes = nodes;
            this.PathTaken = path;
        }
    }
}