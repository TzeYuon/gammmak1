using UnityEngine;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

public class InputManager : MonoBehaviour
{
    private GameInputActions GameInput;
    public static AbstractInputState GameplayInputState { get; private set; }
    public static AbstractInputState CameraInputState { get; private set; }
    static List<AbstractInputState> ActiveStates { get; set; }

    [SerializeField] SceneManager sceneManager;
    [SerializeField] CameraManager cameraManager;

    private void Start()
    {
        GameInput ??= new GameInputActions();
        GameInput.Enable();

        InitControlStates();

        sceneManager.OnSceneChanged += OnSceneChanged;
    }

    private void OnDisable()
    {
        GameInput.Disable();

        sceneManager.OnSceneChanged -= OnSceneChanged;
    }

    private void Update()
    {
        foreach (AbstractInputState state in ActiveStates)
        {
            state.Update();
        }
    }

    private void InitControlStates()
    {
        ActiveStates = new();

        GameplayInputState = new GameplayInputState(this, GameInput);
        CameraInputState = new CameraInputState(this, GameInput, cameraManager);
    }

    public void AddState(AbstractInputState state, EnterStateArgs args = null)
    {
        if (ActiveStates.Contains(state))
            return;

        ActiveStates.Add(state);
        state.Enter(args);
    }

    public void RemoveState(AbstractInputState state)
    {
        if (ActiveStates.Contains(state))
        {
            ActiveStates.Remove(state);
            state.Exit();
        }
    }

    public void SwapStates(AbstractInputState outgoingState, AbstractInputState incomingState, EnterStateArgs args = null)
    {
        RemoveState(outgoingState);
        AddState(incomingState, args);
    }

    void ClearStates()
    {
        while (ActiveStates.Count > 0)
        {
            AbstractInputState state = ActiveStates[0];
            ActiveStates.Remove(state);
            state.Exit();
        }
    }

    void EnterGameplayScene()
    {
        AddState(GameplayInputState);
        AddState(CameraInputState);
    }

    void ExitGameplayScene()
    {
        ClearStates();
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