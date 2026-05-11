using UnityEngine;
using UnityEngine.InputSystem;

public class CameraInputState : AbstractInputState
{
    CameraManager camera;

    Vector2 move;
    float zoom;

    public CameraInputState(InputManager inputManager, GameInputActions gameInput, CameraManager cameraManager) : base(inputManager, gameInput)
    {
        camera = cameraManager;
    }

    public override void EnterOverride(EnterStateArgs args)
    {
        camera.MoveCamera(Vector2.zero, 0);
    }

    public override void ExitOverride()
    {
        camera.MoveCamera(Vector2.zero, 0);
    }

    protected override void SubscribeEvents()
    {
        GameInput.Gameplay.CameraMovement.performed += OnCameraMove;
        GameInput.Gameplay.CameraMovement.canceled += OnCameraMove;

        GameInput.Gameplay.CameraZoom.performed += OnCameraZoom;
        GameInput.Gameplay.CameraZoom.canceled += OnCameraZoom;
    }

    protected override void UnsubscribeEvents()
    {
        GameInput.Gameplay.CameraMovement.performed -= OnCameraMove;
        GameInput.Gameplay.CameraMovement.canceled -= OnCameraMove;

        GameInput.Gameplay.CameraZoom.performed -= OnCameraZoom;
        GameInput.Gameplay.CameraZoom.canceled -= OnCameraZoom;
    }

    protected override void UpdateOverride()
    {
        camera.MoveCamera(move, zoom);
    }

    protected void OnCameraMove(InputAction.CallbackContext context)
    {
        move = context.ReadValue<Vector2>();
    }

    protected void OnCameraZoom(InputAction.CallbackContext context)
    {
        zoom = context.ReadValue<float>();
    }
}
