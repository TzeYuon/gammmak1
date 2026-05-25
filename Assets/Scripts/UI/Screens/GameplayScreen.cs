using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

public class GameplayScreen : UIScreen
{
    private VisualElement selectionBox;
    private EntityManager entityManager;

    private EntityQuery inputQuery;
    private Camera camera;

    public GameplayScreen(UIScreenManager manager, VisualElement root) : base(manager, root)
    {
        selectionBox = new VisualElement();
        selectionBox.style.position = Position.Absolute;
        selectionBox.style.borderLeftWidth = 1.5f;
        selectionBox.style.borderLeftColor = Color.green;
        selectionBox.style.backgroundColor = new Color(0f, 1f, 0f, 0.15f);
        selectionBox.style.display = DisplayStyle.None;

        root.Add(selectionBox);

        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            entityManager = world.EntityManager;
            inputQuery = entityManager.CreateEntityQuery(typeof(PlayerInputData));
        }

        camera = Camera.main;
    }

    protected override void OnHide()
    {

    }

    protected override void OnShow()
    {

    }

    public override void DoUpdate()
    {
        if (inputQuery.IsEmptyIgnoreFilter) return;
        var inputData = inputQuery.GetSingleton<PlayerInputData>();

        if (inputData.IsHeld(PlayerAction.Primary))
        {
            selectionBox.style.display = DisplayStyle.Flex;

            Vector2 uiStart = ScreenToUI(inputData.MouseStartDragPosition);
            Vector2 uiCurrent = ScreenToUI(inputData.MouseWorldPosition);

            float left = math.min(uiStart.x, uiCurrent.x);
            float top = math.min(uiStart.y, uiCurrent.y);
            float width = math.abs(uiStart.x - uiCurrent.x);
            float height = math.abs(uiStart.y - uiCurrent.y);

            selectionBox.style.left = left;
            selectionBox.style.top = top;
            selectionBox.style.width = width;
            selectionBox.style.height = height;
        }
        else
        {
            // Hide selection box
            selectionBox.style.display = DisplayStyle.None;
        }
    }

    private Vector2 ScreenToUI(float2 worldPos)
    {
        float2 screenPos = (Vector2)camera.WorldToScreenPoint((Vector2)worldPos);
        return new Vector2(screenPos.x, Screen.height - screenPos.y);
    }
}
