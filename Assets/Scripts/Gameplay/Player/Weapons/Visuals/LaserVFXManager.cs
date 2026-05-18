using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

public class LaserVFXManager : MonoBehaviour
{
    public VisualEffect VfxGraph;

    private GraphicsBuffer laserBuffer;
    private int bufferPropertyID;
    private int countPropertyID;

    void Start()
    {
        bufferPropertyID = Shader.PropertyToID("LaserBuffer");
        countPropertyID = Shader.PropertyToID("LaserCount");
    }

    void LateUpdate()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var weaponSystem = world.GetExistingSystemManaged<PrecisionWeaponFireSystem>();
        if (weaponSystem == null) return;

        var queue = weaponSystem.GetLaserQueue();

        var laserArray = queue.ToArray(Allocator.Temp);
        int laserCount = laserArray.Length;

        if (laserCount == 0) return;

        if (laserBuffer == null || laserBuffer.count < laserCount)
        {
            laserBuffer?.Release();
            laserBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.NextPowerOfTwo(laserCount), 48);
        }

        laserBuffer.SetData(laserArray);

        VfxGraph.SetGraphicsBuffer(bufferPropertyID, laserBuffer);
        VfxGraph.SetInt(countPropertyID, laserCount);

        VfxGraph.SendEvent("OnLasersFired");

        laserArray.Dispose();
        queue.Clear();
    }

    private void OnDestroy()
    {
        laserBuffer?.Release();
    }
}