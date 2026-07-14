using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RadiusVisualiser : MonoBehaviour
{
    public bool Enabled = true;
    public float VisualRadius = 2.0f;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Enabled) return;

        Vector3 center = transform.position;
        Vector3 forward = transform.up;
        Vector3 normal = Vector3.forward;

        Handles.color = Color.blue;
        Handles.DrawWireArc(center, normal, forward, 360, VisualRadius);
    }
#endif
}
