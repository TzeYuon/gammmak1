using UnityEngine;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private GameInputActions gameInput;

    [SerializeField] SceneManager sceneManager;
    [SerializeField] CameraManager cameraManager;

    private void Start()
    {
        gameInput ??= new GameInputActions();

        sceneManager.OnSceneChanged += OnSceneChanged;
    }

    private void OnDisable()
    {
        gameInput.Dispose();

        sceneManager.OnSceneChanged -= OnSceneChanged;
    }

    private void Update()
    {
        if (gameInput == null) return;

        HandleGameplayInput();
    }

    void HandleGameplayInput()
    {
        if (gameInput.Gameplay.enabled)
        {
            // Handle camera update
            Vector2 cameraMove = gameInput.Gameplay.CameraMovement.ReadValue<Vector2>();
            float cameraZoom = gameInput.Gameplay.CameraZoom.ReadValue<float>();
            cameraManager.MoveCamera(cameraMove, cameraZoom);

            InputBridge.MouseWorldPosition = (Vector2)Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        }
        else
        {

        }
    }

    void EnterGameplayScene()
    {
        gameInput.Gameplay.Enable();
    }

    void ExitGameplayScene()
    {
        gameInput.Gameplay.Disable();
    }

    #region EVENTS
    void OnSceneChanged(object sender, SceneChangeArgs args)
    {
        switch (args.newSceneId)
        {
            case SceneId.BootstrapScene:
                ExitGameplayScene();
                break;
            case SceneId.MainMenuScene:
                ExitGameplayScene();
                break;
            case SceneId.GameplayScene:
                EnterGameplayScene();
                break;
        }
    }
    #endregion
}

public static class InputBridge
{
    public static float2 MouseWorldPosition;
    public static bool IsRightClickPressed;
    public static bool IsLeftClickHeld;
}

public struct PlayerInputData : IComponentData
{
    public float2 MouseWorldPosition;
    public bool IsRightClickPressed;
    public bool IsLeftClickHeld;
}