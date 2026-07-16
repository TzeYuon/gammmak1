using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class InputManagerSystem : SystemBase
{
    private UIDocument mainUIDocument;

    private GameInputActions gameInput;
    private Camera mainCamera;

    private bool hasSubscribedEvents = false;

    private bool isClickConsumedByUI = false;

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

        ref var input = ref SystemAPI.GetSingletonRW<PlayerInputData>().ValueRW;
        var gameState = SystemAPI.GetSingleton<GameStateSingleton>();
        var curState = gameState.CurrentState;

        // check if mouse is over UI
        bool isPointerOverUI = IsPointerOverUI();
        bool primaryIsPressed = gameInput.Gameplay.PrimaryAction.IsPressed();        
        bool primaryWasPressedThisFrame = primaryIsPressed && !input.HardwareState.HasFlag(PlayerAction.Primary);
        if (primaryIsPressed)
        {
            // click was used over UI, consume the click only for this frame
            if (primaryWasPressedThisFrame && isPointerOverUI) isClickConsumedByUI = true;
        }
        else isClickConsumedByUI = false;

        // update inputs
        PlayerAction rawInput = PlayerAction.None;
        if (primaryIsPressed && !isClickConsumedByUI) rawInput |= PlayerAction.Primary;
        if (gameInput.Gameplay.SecondaryAction.IsPressed()) rawInput |= PlayerAction.Secondary;
        if (gameInput.Gameplay.SelectionModifier.IsPressed()) rawInput |= PlayerAction.SelectionModifier;
        if (gameInput.Gameplay.Cancel.IsPressed()) rawInput |= PlayerAction.Cancel;
        if (gameInput.Gameplay.SplitSelection.IsPressed()) rawInput |= PlayerAction.SplitSelection;
        if (gameInput.Gameplay.SelectAll.IsPressed()) rawInput |= PlayerAction.SelectAll;

        input.PreviousHardware = input.HardwareState;
        input.HardwareState = rawInput;
        input.CurrentState = rawInput;

        // Requires camera for mouse pos
        if (CameraManager.Instance != null)
        {
            // Handle camera update
            Vector2 screenMousePos = Mouse.current.position.ReadValue();
            float2 mousePos = (Vector2)mainCamera.ScreenToWorldPoint(screenMousePos);
            input.MouseWorldPosition = mousePos;

            if (curState == GameState.Gameplay)
            {
                bool cameraPan = gameInput.Gameplay.CameraPan.IsPressed();
                Vector2 cameraMove = gameInput.Gameplay.CameraMovement.ReadValue<Vector2>();
                float cameraZoom = gameInput.Gameplay.CameraZoom.ReadValue<float>();
                CameraManager.Instance.UpdateCamera(screenMousePos, mousePos, cameraMove, cameraPan, cameraZoom);
            }

            if (input.WasPressed(PlayerAction.Primary)) // Set start drag position when mouse first pressed
            {
                input.MouseStartDragPosition = mousePos;
            }
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

    private bool IsPointerOverUI()
    {
        if (mainUIDocument == null) mainUIDocument = UnityEngine.Object.FindAnyObjectByType<UIDocument>(FindObjectsInactive.Include);
        if (mainUIDocument == null) return false;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 uiPos = new Vector2(mousePos.x, Screen.height - mousePos.y);

        VisualElement picked = mainUIDocument.rootVisualElement.panel.Pick(uiPos);

        if (picked == null) return false;

        return true;
    }
}

[Flags]
public enum PlayerAction : uint
{
    None = 0,
    Primary = 1 << 0,
    Secondary = 1 << 1,
    SelectionModifier = 1 << 2,
    Cancel = 1 << 4,
    SplitSelection = 1 << 5,
    SelectAll = 1 << 6
}

public struct PlayerInputData : IComponentData
{
    public float2 MouseWorldPosition;
    public float2 MouseStartDragPosition;

    public PlayerAction CurrentState;
    public PlayerAction HardwareState;
    public PlayerAction PreviousHardware; 

    public bool IsHeld(PlayerAction action)
    {
        return (CurrentState & action) != 0;
    }

    public bool WasPressed(PlayerAction action)
    {
        return (CurrentState & action) != 0 && (PreviousHardware & action) == 0;
    }

    public bool WasReleased(PlayerAction action)
    {
        return (CurrentState & action) == 0 && (PreviousHardware & action) != 0;
    }

    public void ClearState(PlayerAction action)
    {
        // removes the state for the rest of the frame
        CurrentState &= ~action;
    }
}