using UnityEngine;

// Moves the player along the ground relative to the direction the camera is facing.
// Reads movement from PlayerInputReader and the speed from Player, and moves with a
// CharacterController. The body object turns toward the direction of travel.
// Movement input is ignored while the player is dead or climbing.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;

    // The object that turns toward the movement direction. It must not be the object the
    // camera hangs from. Leave it empty for no turning.
    [SerializeField] private Transform body;

    // Degrees per second.
    [SerializeField] private float turnSpeed = 720f;

    private CharacterController controller;
    private PlayerInputReader input;
    private Player player;

    // Ground speed in units per second, measured from the last move.
    public float HorizontalSpeed
    {
        get
        {
            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<PlayerInputReader>();
        player = GetComponent<Player>();

        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;
    }

    private void OnValidate()
    {
        turnSpeed = Mathf.Max(0f, turnSpeed);
    }

    private void Update()
    {
        Vector2 move = player.Health.IsDead || player.Climbing ? Vector2.zero : input.Move;

        // Flatten the camera axes so looking down at the ground does not slow movement.
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;

        Vector3 direction = Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f);

        // SimpleMove applies gravity and delta time itself, so it takes a speed in units per second.
        controller.SimpleMove(direction * player.Speed.Value);

        if (body != null && direction.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            body.rotation = Quaternion.RotateTowards(body.rotation, target, turnSpeed * Time.deltaTime);
        }
    }

    // Moves the player to a position at once. The controller is switched off for the move,
    // because it would otherwise overwrite a changed position.
    public void TeleportTo(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
    }
}