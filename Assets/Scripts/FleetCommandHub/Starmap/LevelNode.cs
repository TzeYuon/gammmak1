using System.Collections.Generic;
using UnityEngine;

public enum NodeType
{
    Encounter,
    EliteEncounter,
    Store,
    Boss,
}

public class LevelNode
{
    public Vector2Int Pos;
    public readonly int Layer;
    public readonly int Lane;
    public readonly HashSet<LevelNode> Outgoing = new();
    public NodeType NodeType;

    public RewardType RewardType;
    public ThreatLevel ThreatLevel;
    public ObjectiveType ObjectiveType;
    public TerrainType TerrainType;
    public EnemyProfile EnemyProfile;
    public AnomalyType AnomalyType;

    public int UpgradeCoreReward;

    public LevelNode(int layer, int lane)
    {
        Layer = layer;
        Lane = lane;
        Pos = new Vector2Int(layer, lane);
    }
}

public enum RewardType { UpgradeCores, WeaponBlueprint, ShipBlueprint, MothershipRepair }
public enum ThreatLevel { Low, Medium, High, Boss }
public enum ObjectiveType { HiveAssault, Survival }
public enum TerrainType { Asteroids, TwoTunnel, Ring, TwoRing, Swamp, Cavern, OneTunnel }
public enum EnemyProfile { Standard, Tendrils, Ranged }
public enum AnomalyType { None, DoubleEnemySpeed, ArmouredEnemies, LargerEnemies }