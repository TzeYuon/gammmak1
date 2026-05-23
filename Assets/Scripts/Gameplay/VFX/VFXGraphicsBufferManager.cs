using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.VFX;

public class VFXGraphicsBufferManager : MonoBehaviour
{
    public VisualEffect VfxGraph;

    private Dictionary<VFXType, VFXBufferHandler> bufferHandlers;
    private NativeList<VFXPayload>[] buckets;

    void Start()
    {
        bufferHandlers = new Dictionary<VFXType, VFXBufferHandler>();
        foreach (VFXType type in Enum.GetValues(typeof(VFXType)))
        {
            bufferHandlers.Add(type, new VFXBufferHandler(type.ToString()));
        }

        var vfxTypes = (VFXType[])Enum.GetValues(typeof(VFXType));
        buckets = new NativeList<VFXPayload>[vfxTypes.Length];
    }

    void LateUpdate()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var query = world.EntityManager.CreateEntityQuery(typeof(GlobalVFXQueue));
        if (query.IsEmptyIgnoreFilter) return;

        query.CompleteDependency();
        var queue = query.GetSingleton<GlobalVFXQueue>().Queue;
        if (queue.Count == 0)
        {
            ClearAllCounts();
            return;
        }

        // sort the vfx events by their type
        for (int i = 0; i < buckets.Length; i++) buckets[i] = new NativeList<VFXPayload>(Allocator.Temp);

        while (queue.TryDequeue(out var payload))
        {
            buckets[(int)payload.Type].Add(payload);
        }

        // send each buffer to the correct property
        int stride = Marshal.SizeOf<VFXPayload>();
        foreach (var entry in bufferHandlers)
        {
            VFXType type = entry.Key;
            VFXBufferHandler handler = entry.Value;
            NativeList<VFXPayload> data = buckets[(int)type];

            if (data.Length > 0)
            {
                // resize buffer if needed
                if (handler.Buffer == null || handler.Buffer.count < data.Length)
                {
                    handler.Buffer?.Release();
                    handler.Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, data.Length, stride);
                    VfxGraph.SetGraphicsBuffer(handler.BufferPropertyID, handler.Buffer);
                }

                handler.Buffer.SetData(data.AsArray());
                VfxGraph.SetInt(handler.CountPropertyID, data.Length);
            }
            else
            {
                VfxGraph.SetInt(handler.CountPropertyID, 0);
            }

            data.Dispose();
        }

        VfxGraph.SendEvent("OnBufferReady");
    }

    private void ClearAllCounts()
    {
        foreach (var handler in bufferHandlers.Values)
            VfxGraph.SetInt(handler.CountPropertyID, 0);
    }

    void OnDestroy()
    {
        if (bufferHandlers == null) return;
        foreach (var handler in bufferHandlers.Values) handler.Release();
    }
}

public class VFXBufferHandler
{
    public GraphicsBuffer Buffer;
    public int BufferPropertyID;
    public int CountPropertyID;
    public string Name;

    public VFXBufferHandler(string name)
    {
        this.Name = name;
        this.BufferPropertyID = Shader.PropertyToID($"{name}Buffer");
        this.CountPropertyID = Shader.PropertyToID($"{name}Count");
    }

    public void Release()
    {
        Buffer?.Release();
    }
}