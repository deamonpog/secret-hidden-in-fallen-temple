using UnityEngine;

/// <summary>
/// Handles everything the camera does with input: cursor lock, yaw and zoom. 
/// Attach it to the camera root, which must be a child of the object that has PlayerInputReader. 
/// Assign the camera in the inspector. It must be a child of the camera root. 
/// The cursor follows the saved camera rotate mode, and is always free while the game is 
/// not being played (paused, failed or completed): 
///     LockedCursor: the cursor stays locked while the game is played. 
///     HoldToRotate: the cursor is locked only while the rotate-hold button is held.
/// </summary>
public class CameraRig : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;

    [Header("Yaw")]
    [SerializeField] private float degreesPerUnit = 0.15f;

    [Header("Zoom")]
    [SerializeField] private float stepSize = 1f;
    [SerializeField] private float maxZoomIn = 5f;
    [SerializeField] private float maxZoomOut = 8f;
    [SerializeField] private float smoothTime = 0.12f;

    private PlayerInputReader input;

    private float yaw;

    private Vector3 baseCameraPosition;
    private Vector3 zoomAxis;
    private float targetOffset;
    private float currentOffset;
    private float offsetVelocity;

    private void Awake()
    {
        input = GetComponentInParent<PlayerInputReader>();
        if (input == null || cameraTransform == null)
        {
            Debug.LogError("CameraRig needs a PlayerInputReader on a parent object and a camera assigned.", this);
            enabled = false;
            return;
        }

        yaw = transform.localEulerAngles.y;

        // Zoom slides the camera along its view direction, in its parent's local space.
        baseCameraPosition = cameraTransform.localPosition;
        zoomAxis = cameraTransform.localRotation * Vector3.forward;
    }

    private void OnDisable()
    {
        SetCursorLocked(false);
    }

    // The cursor is settled first, so yaw always sees this frame's lock state.
    private void Update()
    {
        UpdateCursor();
        UpdateYaw();
        UpdateZoom();
    }

    // RotateHeld is already false while the game is not being played, so both modes
    // free the cursor then.
    private void UpdateCursor()
    {
        bool wantLocked = GameSettings.RotateMode == CameraRotateMode.LockedCursor
            ? input.GameplayEnabled
            : input.RotateHeld;

        SetCursorLocked(wantLocked);
    }

    // Rotates this transform around the world Y axis, only while the cursor is locked.
    private void UpdateYaw()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        // Pointer delta is already per frame, so it is not scaled by delta time.
        yaw += input.Look.x * degreesPerUnit;
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // Moves the camera along its own forward axis in response to scroll input.
    private void UpdateZoom()
    {
        // Only the sign of the scroll is used, since scroll magnitude differs between mice.
        float scroll = input.Zoom;
        if (scroll != 0f)
        {
            targetOffset = Mathf.Clamp(
                targetOffset + Mathf.Sign(scroll) * stepSize,
                -maxZoomOut,
                maxZoomIn);
        }

        currentOffset = Mathf.SmoothDamp(currentOffset, targetOffset, ref offsetVelocity, smoothTime);
        cameraTransform.localPosition = baseCameraPosition + zoomAxis * currentOffset;
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