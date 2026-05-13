using UnityEngine;

public class CameraManager : MonoBehaviour
{
    Transform cameraTransform;
    Camera cameraComponent;

    Vector3 cameraTargetPos;
    float moveSpeed = 0.5f;
    Bounds cameraBounds;

    float cameraTargetZoom;
    float zoomSpeed = 700f;
    float minZoom = 14, maxZoom = 150;

    private void Start()
    {
        cameraTransform = transform;
        cameraComponent = cameraTransform.GetComponent<Camera>();

        cameraTargetPos = cameraTransform.position;
        cameraTargetZoom = cameraComponent.orthographicSize;
    }

    public void MoveCamera(Vector2 moveDir, float zoomDir)
    {
        ManageMove();
        ManageZoom();

        void ManageMove()
        {
            float zoomRatio = cameraComponent.orthographicSize / minZoom;
            cameraTargetPos += zoomRatio * moveSpeed * (Vector3)moveDir;
            cameraTargetPos.z = cameraTransform.position.z;

            cameraTransform.position = Vector3.Lerp(cameraTransform.position, cameraTargetPos, 5 * Time.unscaledDeltaTime);
        }

        void ManageZoom()
        {
            cameraTargetZoom -= zoomDir * zoomSpeed * Time.unscaledDeltaTime;
            cameraTargetZoom = Mathf.Clamp(cameraTargetZoom, minZoom, maxZoom);
            cameraComponent.orthographicSize = Mathf.Lerp(cameraComponent.orthographicSize, cameraTargetZoom, Time.unscaledDeltaTime * 5);
        }
    }

    public void Clear()
    {
        cameraTargetPos = cameraTransform.position;
    }
}