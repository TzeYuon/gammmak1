using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

public class DeathVFXManager : MonoBehaviour
{
    public VisualEffect VfxGraph;

    private VFXEventAttribute eventAttribute;

    private EntityQuery queueQuery;

    private void Start()
    {
        eventAttribute = VfxGraph.CreateVFXEventAttribute();

        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            queueQuery = world.EntityManager.CreateEntityQuery(typeof(DeathVFXQueueSingleton));
        }
    }

    private void LateUpdate()
    {
        if (queueQuery.IsEmptyIgnoreFilter) return;
        queueQuery.CompleteDependency();

        var queue = queueQuery.GetSingleton<DeathVFXQueueSingleton>().Queue;
        while (queue.TryDequeue(out DeathEvent death))
        {
            eventAttribute.SetVector3("position", death.Position);
            eventAttribute.SetVector3("BurstColour", death.Colour);
            VfxGraph.SendEvent("OnDeath", eventAttribute);
        }
    }
}
