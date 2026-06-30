using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("Movement Speeds")]
    [SerializeField] float cameraMoveSpeed = 50f;
    [SerializeField] float edgeScrollSpeed = 1f;
    [SerializeField] float dragPanSpeed = 400f;
    [SerializeField] float smoothing = 10f; // How fast the camera stops higher is faster

    [Header("Edge Scrolling")]
    [SerializeField] bool enableEdgeScrolling = true;
    [SerializeField] float edgeThickness = 20f; // Pixels from edge of screen

    Transform cameraTransform;
    Camera cameraComponent;

    bool isPanning;
    Vector2 lastMousePosition;

    Vector3 cameraTargetPos;

    Vector2 minBounds;
    Vector2 maxBounds;

    float cameraTargetZoom;
    float zoomSpeed = 700f;
    float minZoom = 14, maxZoom = 150;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        cameraTransform = transform;
        cameraComponent = cameraTransform.GetComponent<Camera>();

        cameraTargetPos = cameraTransform.position;
        cameraTargetZoom = cameraComponent.orthographicSize;
    }

    public void UpdateCamera(Vector2 mouseScreenPos, Vector2 mouseWorldPos, Vector2 cameraMoveFromKeyboard, bool cameraPan, float zoomDir)
    {
        if (cameraComponent == null) return;

        UpdateZoom();
        UpdateMove();

        void UpdateZoom()
        {
            cameraTargetZoom -= zoomDir * zoomSpeed * Time.unscaledDeltaTime;
            cameraTargetZoom = Mathf.Clamp(cameraTargetZoom, minZoom, maxZoom);
            cameraComponent.orthographicSize = Mathf.Lerp(cameraComponent.orthographicSize, cameraTargetZoom, Time.unscaledDeltaTime * 5);
        }

        void UpdateMove()
        {
            float dt = Time.unscaledDeltaTime;
            Vector2 moveInput = Vector3.zero;

            float zoomRatio = cameraComponent.orthographicSize / minZoom;

            if (cameraPan)
            {
                // handle panning
                if (!isPanning)
                {
                    // panning start
                    lastMousePosition = mouseWorldPos;
                }
                else
                {
                    // while panning
                    Vector3 deltaMouse = mouseWorldPos - lastMousePosition;

                    moveInput -= new Vector2(deltaMouse.x, deltaMouse.y) * dragPanSpeed * dt;
                    lastMousePosition = mouseWorldPos;
                }
            }
            else if (enableEdgeScrolling)
            {
                // only when not panning
                // handle camera move when mouse at edge
                bool isMouseInWindow = mouseScreenPos.x >= 0 && mouseScreenPos.x <= Screen.width &&
                                       mouseScreenPos.y >= 0 && mouseScreenPos.y <= Screen.height;

                if (isMouseInWindow)
                {
                    if (mouseScreenPos.y >= Screen.height - edgeThickness) moveInput.y += edgeScrollSpeed; // Top
                    if (mouseScreenPos.y <= edgeThickness) moveInput.y -= edgeScrollSpeed;                 // Bottom
                    if (mouseScreenPos.x >= Screen.width - edgeThickness) moveInput.x += edgeScrollSpeed;  // Right
                    if (mouseScreenPos.x <= edgeThickness) moveInput.x -= edgeScrollSpeed;                 // Left
                }
            }

            isPanning = cameraPan;

            // add any keyboard input
            moveInput += cameraMoveFromKeyboard;

            if (moveInput != Vector2.zero)
            {
                cameraTargetPos += dt * cameraMoveSpeed * zoomRatio * (Vector3)moveInput;

                cameraTargetPos.x = Mathf.Clamp(cameraTargetPos.x, minBounds.x, maxBounds.x);
                cameraTargetPos.y = Mathf.Clamp(cameraTargetPos.y, minBounds.y, maxBounds.y);
            }

            // smoothly move the camera to target pos
            transform.position = Vector3.Lerp(transform.position, cameraTargetPos, smoothing * dt);
        }
    }

    public void SetMapBounds(Vector2 halfExtents)
    {
        Vector2 center = new(1000, 1000);
        minBounds = center - halfExtents;
        maxBounds = center + halfExtents;
    }

    public void SnapToMothership(Vector3 mothershipPos)
    {
        SnapToPosition(mothershipPos);
    }

    public void SnapToPosition(Vector3 newPos)
    {
        cameraTargetPos = new Vector3(newPos.x, newPos.y, cameraTransform.position.z);
        cameraTransform.position = cameraTargetPos;
    }

    public void Clear()
    {
        cameraTargetPos = cameraTransform.position;
    }
}