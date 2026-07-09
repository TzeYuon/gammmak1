using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class SiegePathVisualiser : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var entityManager = world.EntityManager;
        var hiveQuery = entityManager.CreateEntityQuery(typeof(HiveComponent));        
        if (hiveQuery.IsEmptyIgnoreFilter) return;

        using NativeArray<Entity> hiveEntities = hiveQuery.ToEntityArray(Allocator.Temp);
        foreach (var hiveEntity in hiveEntities)
        {
            if (!entityManager.HasBuffer<SiegePathWaypointElement>(hiveEntity)) continue;

            DynamicBuffer<SiegePathWaypointElement> pathBuffer =
                entityManager.GetBuffer<SiegePathWaypointElement>(hiveEntity);

            if (pathBuffer.Length < 2) continue;

            Vector3[] leftBoundary = new Vector3[pathBuffer.Length];
            Vector3[] rightBoundary = new Vector3[pathBuffer.Length];

            for (int i = 0; i < pathBuffer.Length; i++)
            {
                float2 currentPos = pathBuffer[i].Position;

                float2 dir;
                if (i < pathBuffer.Length - 1)
                {
                    dir = math.normalizesafe(pathBuffer[i + 1].Position - currentPos.xy);
                }
                else
                {
                    dir = math.normalizesafe(currentPos.xy - pathBuffer[i - 1].Position);
                }

                float3 right = new float3(-dir.y, dir.x, 0f);

                Gizmos.color = new Color(1f, 0f, 0.2f, 0.3f);
                Gizmos.DrawLine(leftBoundary[i], rightBoundary[i]);

                Gizmos.color = new Color(1f, 0f, 0.2f, 0.6f);
                Gizmos.DrawSphere(new float3(currentPos, 0), 0.15f);
            }

            for (int i = 0; i < pathBuffer.Length - 1; i++)
            {
                float2 currentCenter = pathBuffer[i].Position;
                float2 nextCenter = pathBuffer[i + 1].Position;

                Gizmos.color = new Color(1f, 0f, 0.2f, 1.0f);
                Gizmos.DrawLine(new float3(currentCenter, 0), new float3(nextCenter, 0));

                Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
                Gizmos.DrawLine(leftBoundary[i], leftBoundary[i + 1]);
                Gizmos.DrawLine(rightBoundary[i], rightBoundary[i + 1]);
            }
        }
    }
}
