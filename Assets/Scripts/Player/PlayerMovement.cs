using UnityEngine;

// Moves the player along the ground relative to the direction the camera is facing.
// Reads movement from PlayerInputReader and moves with a CharacterController.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private PlayerInputReader input;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<PlayerInputReader>();

        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        Vector2 move = input.Move;

        // Flatten the camera axes so looking down at the ground does not slow movement.
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;

        Vector3 direction = Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f);

        // SimpleMove applies gravity and delta time itself, so it takes a speed in units per second.
        controller.SimpleMove(direction * moveSpeed);
    }
}
