using UnityEngine;

// Moves this transform along its own forward axis in response to scroll input.
// Attach it to the camera, which must be a descendant of the object that has PlayerInputReader.
public class CameraZoom : MonoBehaviour
{
    [SerializeField] private float stepSize = 1f;
    [SerializeField] private float maxZoomIn = 5f;
    [SerializeField] private float maxZoomOut = 8f;
    [SerializeField] private float smoothTime = 0.12f;

    private PlayerInputReader input;
    private Vector3 basePosition;
    private Vector3 zoomAxis;
    private float targetOffset;
    private float currentOffset;
    private float offsetVelocity;

    private void Awake()
    {
        input = GetComponentInParent<PlayerInputReader>();
        if (input == null)
        {
            Debug.LogError("CameraZoom needs a PlayerInputReader on a parent object.", this);
            enabled = false;
            return;
        }

        // Zoom slides the camera along its view direction, in the parent's local space.
        basePosition = transform.localPosition;
        zoomAxis = transform.localRotation * Vector3.forward;
    }

    private void Update()
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
        transform.localPosition = basePosition + zoomAxis * currentOffset;
    }
}
