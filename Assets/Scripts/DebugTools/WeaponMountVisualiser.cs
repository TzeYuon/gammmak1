using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WeaponMountVisualiser : MonoBehaviour
{
    [Header("Firing Arc")]
    [Range(0, 180)]
    public float MaxAngleLimit = 90f; // 90 = a 180-degree hemisphere

    public float VisualRadius = 2.0f; // How big the gizmo is

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 center = transform.position;
        Vector3 forward = transform.up;
        Vector3 normal = Vector3.forward;

        Handles.color = Color.red;
        Handles.DrawLine(center, center + (forward * (VisualRadius * 1.2f)));

        Vector3 leftLimit = Quaternion.AngleAxis(-MaxAngleLimit, normal) * forward;
        Vector3 rightLimit = Quaternion.AngleAxis(MaxAngleLimit, normal) * forward;

        Handles.color = Color.cyan;
        Handles.DrawLine(center, center + (leftLimit * VisualRadius));
        Handles.DrawLine(center, center + (rightLimit * VisualRadius));

        Handles.color = new Color(0, 1, 1, 0.1f);
        Handles.DrawSolidArc(center, normal, leftLimit, MaxAngleLimit * 2, VisualRadius);

        Handles.color = Color.cyan;
        Handles.DrawWireArc(center, normal, leftLimit, MaxAngleLimit * 2, VisualRadius);
    }
#endif
}
