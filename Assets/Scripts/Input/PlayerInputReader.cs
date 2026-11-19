using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Reads the Player action map and exposes it to gameplay code.
// Continuous values are polled; button presses are raised as events.
[RequireComponent(typeof(PlayerInput))]
public class PlayerInputReader : MonoBehaviour
{
    private const string MapName = "Player";

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction zoomAction;
    private InputAction interactAction;
    private InputAction skill1Action;
    private InputAction skill2Action;
    private InputAction trackAction;
    private InputAction rotateHoldAction;
    private InputAction pauseAction;

    // Movement input as a 2D vector (x = right, y = forward).
    public Vector2 Move => moveAction.ReadValue<Vector2>();

    // Pointer or stick delta used to rotate the camera.
    public Vector2 Look => lookAction.ReadValue<Vector2>();

    // Scroll value used to zoom the camera.
    public float Zoom => zoomAction.ReadValue<float>();

    // True while the rotate-hold button (middle mouse) is held down.
    public bool RotateHeld => rotateHoldAction.IsPressed();

    public event Action InteractPressed;
    public event Action Skill1Pressed;
    public event Action Skill2Pressed;
    public event Action TrackPressed;
    public event Action PausePressed;

    private void Awake()
    {
        InputActionAsset actions = GetComponent<PlayerInput>().actions;

        moveAction = actions.FindAction(MapName + "/Move", true);
        lookAction = actions.FindAction(MapName + "/Look", true);
        zoomAction = actions.FindAction(MapName + "/Zoom", true);
        interactAction = actions.FindAction(MapName + "/Interact", true);
        skill1Action = actions.FindAction(MapName + "/Skill1", true);
        skill2Action = actions.FindAction(MapName + "/Skill2", true);
        trackAction = actions.FindAction(MapName + "/Track", true);
        rotateHoldAction = actions.FindAction(MapName + "/RotateHold", true);
        pauseAction = actions.FindAction(MapName + "/Pause", true);
    }

    private void OnEnable()
    {
        interactAction.performed += HandleInteract;
        skill1Action.performed += HandleSkill1;
        skill2Action.performed += HandleSkill2;
        trackAction.performed += HandleTrack;
        pauseAction.performed += HandlePause;
    }

    private void OnDisable()
    {
        interactAction.performed -= HandleInteract;
        skill1Action.performed -= HandleSkill1;
        skill2Action.performed -= HandleSkill2;
        trackAction.performed -= HandleTrack;
        pauseAction.performed -= HandlePause;
    }

    private void HandleInteract(InputAction.CallbackContext context) => InteractPressed?.Invoke();
    private void HandleSkill1(InputAction.CallbackContext context) => Skill1Pressed?.Invoke();
    private void HandleSkill2(InputAction.CallbackContext context) => Skill2Pressed?.Invoke();
    private void HandleTrack(InputAction.CallbackContext context) => TrackPressed?.Invoke();
    private void HandlePause(InputAction.CallbackContext context) => PausePressed?.Invoke();
}
