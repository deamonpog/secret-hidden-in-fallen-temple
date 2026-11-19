using UnityEngine;

// Rotates this transform around the world Y axis using horizontal look input.
// Attach it to the camera root, which must be a child of the object that has PlayerInputReader.
public class CameraYaw : MonoBehaviour
{
    [SerializeField] private float degreesPerUnit = 0.15f;

    private PlayerInputReader input;
    private float yaw;

    private void Awake()
    {
        input = GetComponentInParent<PlayerInputReader>();
        if (input == null)
        {
            Debug.LogError("CameraYaw needs a PlayerInputReader on a parent object.", this);
            enabled = false;
            return;
        }

        yaw = transform.localEulerAngles.y;
    }

    private void Update()
    {
        // Rotation only happens while the cursor is locked, which CursorController decides.
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        // Pointer delta is already per frame, so it is not scaled by delta time.
        yaw += input.Look.x * degreesPerUnit;
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }
}
