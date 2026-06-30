using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class InputManagerSystem : SystemBase
{
    private GameInputActions gameInput;
    private Camera mainCamera;

    private bool hasSubscribedEvents = false;

    protected override void OnCreate()
    {
        gameInput = new GameInputActions();
        gameInput.Disable();

        EntityManager.CreateSingleton<PlayerInputData>();
    }

    protected override void OnDestroy()
    {
        gameInput?.Dispose();

        if (hasSubscribedEvents && SceneManager.Instance != null)
        {
            SceneManager.Instance.OnSceneChanged -= OnSceneChanged;
        }
    }

    protected override void OnUpdate()
    {
        if (!hasSubscribedEvents && SceneManager.Instance != null)
        {
            SceneManager.Instance.OnSceneChanged += OnSceneChanged;
            hasSubscribedEvents = true;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        var inputData = SystemAPI.GetSingleton<PlayerInputData>();

        // update current and previous inputs
        inputData.PreviousState = inputData.CurrentState;

        PlayerAction newState = PlayerAction.None;
        if (gameInput.Gameplay.PrimaryAction.IsPressed()) newState |= PlayerAction.Primary;
        if (gameInput.Gameplay.SecondaryAction.IsPressed()) newState |= PlayerAction.Secondary;
        if (gameInput.Gameplay.SelectionModifier.IsPressed()) newState |= PlayerAction.SelectionModifier;
        if (gameInput.Gameplay.ToggleReinforcementPopup.IsPressed()) newState |= PlayerAction.ToggleReinforcementPopup;
        inputData.CurrentState = newState;

        // Requires camera for mouse pos
        if (CameraManager.Instance != null)
        {
            // Handle camera update
            Vector2 screenMousePos = Mouse.current.position.ReadValue();
            float2 mousePos = (Vector2)mainCamera.ScreenToWorldPoint(screenMousePos);
            inputData.MouseWorldPosition = mousePos;

            bool cameraPan = gameInput.Gameplay.CameraPan.IsPressed();
            Vector2 cameraMove = gameInput.Gameplay.CameraMovement.ReadValue<Vector2>();
            float cameraZoom = gameInput.Gameplay.CameraZoom.ReadValue<float>();
            CameraManager.Instance.UpdateCamera(screenMousePos, mousePos, cameraMove, cameraPan, cameraZoom);

            if (inputData.WasPressed(PlayerAction.Primary)) // Set start drag position when mouse first pressed
            {
                inputData.MouseStartDragPosition = mousePos;
            }

            // Set move command and rotation
            if (inputData.WasPressed(PlayerAction.Secondary))
            {
                inputData.MoveCommandStartDragPosition = mousePos;
            }

            if (inputData.WasReleased(PlayerAction.Secondary))
            {
                Vector2 drag = mousePos - inputData.MoveCommandStartDragPosition;
                bool isDrag = math.lengthsq(drag) > 0.25f;

                inputData.MoveCommandTriggered = true;
                inputData.MoveCommandUseRotation = isDrag;

                if (isDrag)
                {
                    float angle = math.atan2(drag.y, drag.x) - (math.PI / 2f);
                    inputData.MoveCommandTargetRotation = angle;
                }
            }
        }

        SystemAPI.SetSingleton(inputData);
    }

    void EnterGameplayScene()
    {
        gameInput.Gameplay.Enable();
    }

    void ExitGameplayScene()
    {
        gameInput.Gameplay.Disable();
    }

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
}

[Flags]
public enum PlayerAction : uint
{
    None = 0,
    Primary = 1 << 0,
    Secondary = 1 << 1,
    SelectionModifier = 1 << 2,
    ToggleReinforcementPopup = 1 << 3,
}

public struct PlayerInputData : IComponentData
{
    public float2 MouseWorldPosition;
    public float2 MouseStartDragPosition;

    public bool MoveCommandTriggered;
    public float2 MoveCommandStartDragPosition;
    public bool MoveCommandUseRotation;
    public float MoveCommandTargetRotation;

    public PlayerAction CurrentState;
    public PlayerAction PreviousState;

    public bool IsHeld(PlayerAction action)
    {
        return (CurrentState & action) != 0;
    }

    public bool WasPressed(PlayerAction action)
    {
        return (CurrentState & action) != 0 && (PreviousState & action) == 0;
    }

    public bool WasReleased(PlayerAction action)
    {
        return (CurrentState & action) == 0 && (PreviousState & action) != 0;
    }
}