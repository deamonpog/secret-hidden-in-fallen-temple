using UnityEngine;

// Applies the cursor lock state that matches the player's camera rotate mode.
// LockedCursor: the cursor stays locked, and the Pause button frees or re-locks it.
// HoldToRotate: the cursor is locked only while the rotate-hold button is held.
// Attach it to the object that has PlayerInputReader.
public class CursorController : MonoBehaviour
{
    private PlayerInputReader input;
    private bool releasedByPlayer;

    private void Awake()
    {
        input = GetComponentInParent<PlayerInputReader>();
        if (input == null)
        {
            Debug.LogError("CursorController needs a PlayerInputReader on this or a parent object.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        input.PausePressed += OnPausePressed;
        GameSettings.RotateModeChanged += OnRotateModeChanged;
    }

    private void OnDisable()
    {
        input.PausePressed -= OnPausePressed;
        GameSettings.RotateModeChanged -= OnRotateModeChanged;
        SetCursorLocked(false);
    }

    private void Update()
    {
        bool wantLocked = GameSettings.RotateMode == CameraRotateMode.LockedCursor
            ? !releasedByPlayer
            : input.RotateHeld;

        SetCursorLocked(wantLocked);
    }

    private void OnPausePressed()
    {
        releasedByPlayer = !releasedByPlayer;
    }

    private void OnRotateModeChanged()
    {
        releasedByPlayer = false;
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // Switches the saved rotate mode from the component menu until a settings screen exists.
    [ContextMenu("Toggle rotate mode")]
    private void ToggleRotateMode()
    {
        GameSettings.RotateMode = GameSettings.RotateMode == CameraRotateMode.LockedCursor
            ? CameraRotateMode.HoldToRotate
            : CameraRotateMode.LockedCursor;
    }
}
