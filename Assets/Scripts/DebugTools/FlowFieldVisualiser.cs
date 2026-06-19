using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class FlowFieldVisualiser : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var entityManager = world.EntityManager;

        var terrainQuery = entityManager.CreateEntityQuery(typeof(TerrainGridComponent));
        var flowQuery = entityManager.CreateEntityQuery(typeof(EnemyFlowFieldComponent));

        if (!terrainQuery.HasSingleton<TerrainGridComponent>()) return;
        if (!flowQuery.HasSingleton<EnemyFlowFieldComponent>()) return;

        var terrainGrid = terrainQuery.GetSingleton<TerrainGridComponent>();
        var flowFieldComp = flowQuery.GetSingleton<EnemyFlowFieldComponent>();

        var flowField = flowFieldComp.FlowField;

        if (flowField.IsCreated)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < flowField.Length; i++)
            {
                if (math.lengthsq(flowField[i]) < 0.01f) continue;

                Vector3 start = new Vector2(terrainGrid.WorldOrigin.x, terrainGrid.WorldOrigin.y) + IndexToWorldPos(i, terrainGrid.GridWidth);
                Vector3 end = start + new Vector3(flowField[i].x, flowField[i].y, 0) * 0.5f;

                float r = (flowField[i].x + 1.0f) * 0.5f;
                float g = (flowField[i].y + 1.0f) * 0.5f;
                float b = 0.2f;
                Gizmos.color = new Color(r, g, b, 1.0f);

                Gizmos.DrawLine(start, end);
            }
        }
    }

    private Vector2 IndexToWorldPos(int flatIndex, int gridWidth)
    {
        var cell = new Vector2(flatIndex % gridWidth, flatIndex / gridWidth);
        return new Vector2(cell.x, cell.y) + (Vector2.one * 0.5f);
    }
}
