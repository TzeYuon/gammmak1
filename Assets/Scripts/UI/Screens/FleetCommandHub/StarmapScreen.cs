using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class StarmapScreen : UIScreen
{
    private Button deployBtn;
    private EntityQuery gameStateQuery;

    public StarmapScreen(UIScreenManager manager, VisualElement root) : base(manager, root)
    {
        deployBtn = root.Q<Button>("DeployBtn");
        deployBtn.clicked += OnDeployBtnClicked;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            var entityManager = world.EntityManager;
            gameStateQuery = entityManager.CreateEntityQuery(typeof(GameStateSingleton));
        }
    }

    protected override void OnHide()
    {

    }

    protected override void OnShow()
    {

    }

    void OnDeployBtnClicked()
    {
        if (gameStateQuery.IsEmptyIgnoreFilter) return;
        gameStateQuery.CompleteDependency();
        var gameState = gameStateQuery.GetSingletonRW<GameStateSingleton>();
        gameState.ValueRW.CurrentState = GameState.Generating;
    }
}
