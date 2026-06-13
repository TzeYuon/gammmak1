using Starmap;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class StarmapScreen : UIScreen
{
    private Map map;

    private VisualElement mapContainer;
    private Dictionary<LevelNode, MapNodeVisualElement> mapNodes = new();

    private Button deployBtn;
    private EntityQuery gameStateQuery;

    private LevelNode activeNode;

    public StarmapScreen(UIScreenManager manager, VisualElement root) : base(manager, root)
    {
        mapContainer = root.Q<VisualElement>("MapContainer");

        deployBtn = root.Q<Button>("DeployBtn");
        deployBtn.clicked += OnDeployBtnClicked;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            var entityManager = world.EntityManager;
            gameStateQuery = entityManager.CreateEntityQuery(typeof(GameStateSingleton));
        }

        map = RunManager.SaveData.ActiveRun.SectorMap;

        // add invisible active node to connect to start nodes
        activeNode = new(NodeType.Encounter, new UnityEngine.Vector2Int(-1, -1));
        foreach (var startNode in map.StartNodes) {
            activeNode.AddOutgoing(startNode);
        }

        // add visual elements to map
        foreach (LevelNode node in map.AllNodes)
        {
            var pos = node.Position;
            MapNodeVisualElement nodeElement = UIScreenManager.UIAssetsDatabase.MapNode.Instantiate().Q<MapNodeVisualElement>();
            nodeElement.style.position = Position.Absolute;
            nodeElement.style.top = pos.y * 105;
            nodeElement.style.left = pos.x * 105;
            mapContainer.Add(nodeElement);
            mapNodes.Add(node, nodeElement);
        }

        RebuildMap();
    }

    protected override void OnHide()
    {

    }

    protected override void OnShow()
    {
        RebuildMap();
    }

    void RebuildMap()
    {
        foreach (var node in mapNodes.Values)
        {
            node.NodeState = MapNodeState.Default;
        }

        // set active node
        if (mapNodes.ContainsKey(activeNode))
        {
            mapNodes[activeNode].NodeState = MapNodeState.ActiveSelected;
        }

        // set nodes that are reachable
        foreach (var selectable in activeNode.Outgoing)
        {
            mapNodes[selectable].NodeState = MapNodeState.Selectable;
        }
    }

    void OnDeployBtnClicked()
    {
        if (gameStateQuery.IsEmptyIgnoreFilter) return;
        gameStateQuery.CompleteDependency();
        var gameState = gameStateQuery.GetSingletonRW<GameStateSingleton>();
        gameState.ValueRW.CurrentState = GameState.Generating;
    }
}

public enum MapNodeState
{
    Default,
    ActiveSelected,
    Taken,
    Selectable,
}

[UxmlElement("MapNode")]
public partial class MapNodeVisualElement : VisualElement
{
    private MapNodeState m_NodeState;

    [UxmlAttribute]
    public MapNodeState NodeState
    {
        get => m_NodeState;
        set
        {
            m_NodeState = value;
            Rebuild();
        }
    }

    public MapNodeVisualElement()
    {
        AddToClassList("map-node");
        Rebuild();
    }

    private void Rebuild()
    {
        RemoveFromClassList("map-node-default");
        RemoveFromClassList("map-node-activeselected");
        RemoveFromClassList("map-node-taken");
        RemoveFromClassList("map-node-selectable");

        switch (m_NodeState)
        {
            case MapNodeState.Default:
                AddToClassList("map-node-default");
                break;
            case MapNodeState.ActiveSelected:
                AddToClassList("map-node-activeSelected");
                break;
            case MapNodeState.Taken:
                AddToClassList("map-node-taken");
                break;
            case MapNodeState.Selectable:
                AddToClassList("map-node-selectable");
                break;
        }
    }
}