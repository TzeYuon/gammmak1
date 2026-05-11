using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class AbstractInputState
{
    public InputManager InputManager { get; set; }
    public GameInputActions GameInput { get; set; }

    public AbstractInputState(InputManager inputManager, GameInputActions gameInput)
    {
        InputManager = inputManager;
        GameInput = gameInput;
    }

    public void Enter(EnterStateArgs args)
    {
        SubscribeEvents();
        EnterOverride(args);
    }

    public abstract void EnterOverride(EnterStateArgs args);

    public void Exit()
    {
        UnsubscribeEvents();
        ExitOverride();
    }

    public abstract void ExitOverride();

    public void Update()
    {
        UpdateOverride();
    }

    protected abstract void UpdateOverride();

    // Subscribe to all events needed in this state
    // Includes UI inputs and normal inputs
    protected abstract void SubscribeEvents();
    protected abstract void UnsubscribeEvents();
}

public class EnterStateArgs
{
    public EntityQuery InputQuery;
}