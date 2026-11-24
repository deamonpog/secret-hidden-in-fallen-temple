using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns what the player can see. Each frame it sets the linear fog so it reaches full
/// strength at the player's vision radius, hides every VisionTarget beyond that radius,
/// and while the Sense ability runs, draws a screen marker over every target, which the
/// fog cannot hide. It only reads game state.
/// </summary>
[DefaultExecutionOrder(100)]
public class VisionController : MonoBehaviour
{
    [SerializeField] private Player player;

    // Defaults to the main camera when empty.
    [SerializeField] private Camera viewCamera;

    [Header("Fog")]
    // Fog starts at this fraction of the vision radius and is solid at the radius.
    [SerializeField, Range(0f, 1f)] private float fogStartFraction = 0.6f;

    // Seconds the shown radius takes to follow a change, such as a shift or Improve vision.
    [SerializeField] private float radiusSmoothTime = 0.4f;

    [Header("Sense markers")]
    // A UI graphic, such as an Image, copied once per marker.
    [SerializeField] private Graphic markerPrefab;

    // A RectTransform under a Screen Space - Overlay canvas that holds the markers.
    [SerializeField] private RectTransform markerParent;

    // Distance in pixels from the screen edge for markers of targets that are off screen.
    [SerializeField] private float edgeMargin = 24f;

    private readonly List<Graphic> markers = new List<Graphic>();
    private float shownRadius;
    private float radiusVelocity;

    // The vision radius used this frame, after smoothing.
    public float ShownRadius => shownRadius;

    private void Awake()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;

        if (player == null || viewCamera == null)
        {
            Debug.LogError("VisionController needs a player and a camera.", this);
            enabled = false;
            return;
        }

        if (markerPrefab == null || markerParent == null)
            Debug.LogWarning("VisionController has no marker prefab or parent, " +
                             "so Sense shows nothing.", this);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
    }

    private void Start()
    {
        // Player.Awake has set the vision from the profile, so the first frame starts at
        // the full radius instead of growing from zero.
        shownRadius = player.Vision.Value;
    }

    private void LateUpdate()
    {
        shownRadius = Mathf.SmoothDamp(shownRadius, player.Vision.Value,
                                       ref radiusVelocity, radiusSmoothTime);

        Vector3 center = player.transform.position;
        UpdateFog(center);
        UpdateTargets(center);
        UpdateMarkers();
    }

    // Shows every target again and hides the markers, so nothing stays hidden while
    // this component is off.
    private void OnDisable()
    {
        IReadOnlyList<VisionTarget> targets = VisionTarget.Active;
        for (int i = 0; i < targets.Count; i++)
            targets[i].SetVisible(true);

        for (int i = 0; i < markers.Count; i++)
        {
            if (markers[i] != null)
                markers[i].gameObject.SetActive(false);
        }
    }

    // Keeps the tuning values in a sensible range when they are edited in the inspector.
    private void OnValidate()
    {
        radiusSmoothTime = Mathf.Max(0f, radiusSmoothTime);
        edgeMargin = Mathf.Max(0f, edgeMargin);
    }

    // Unity fog grows with depth along the camera's view direction, so both distances are
    // measured from the camera's depth of the player. This gives a straight fog edge
    // behind the player, not a circle around them.
    private void UpdateFog(Vector3 center)
    {
        Transform cam = viewCamera.transform;
        float playerDepth = Vector3.Dot(center - cam.position, cam.forward);
        RenderSettings.fogStartDistance = playerDepth + shownRadius * fogStartFraction;
        RenderSettings.fogEndDistance = playerDepth + shownRadius;
    }

    // Hides every target farther than the radius on the ground plane.
    private void UpdateTargets(Vector3 center)
    {
        float radiusSqr = shownRadius * shownRadius;
        IReadOnlyList<VisionTarget> targets = VisionTarget.Active;
        for (int i = 0; i < targets.Count; i++)
        {
            Vector3 offset = targets[i].transform.position - center;
            offset.y = 0f;
            targets[i].SetVisible(offset.sqrMagnitude <= radiusSqr);
        }
    }

    // While Sense runs, places one marker over each target in front of the camera.
    // Targets off screen get their marker at the nearest screen edge.
    private void UpdateMarkers()
    {
        int used = 0;
        bool sensing = player.Abilities != null && player.Abilities.IsSensing;

        if (sensing && markerPrefab != null && markerParent != null)
        {
            IReadOnlyList<VisionTarget> targets = VisionTarget.Active;
            for (int i = 0; i < targets.Count; i++)
            {
                Vector3 screen = viewCamera.WorldToScreenPoint(targets[i].MarkerPosition);
                if (screen.z <= 0f)
                    continue;

                float x = Mathf.Clamp(screen.x, edgeMargin, Screen.width - edgeMargin);
                float y = Mathf.Clamp(screen.y, edgeMargin, Screen.height - edgeMargin);

                Graphic marker = GetMarker(used);
                used++;
                marker.rectTransform.position = new Vector3(x, y, 0f);
                marker.color = targets[i].MarkerColor;
            }
        }

        for (int i = used; i < markers.Count; i++)
        {
            if (markers[i].gameObject.activeSelf)
                markers[i].gameObject.SetActive(false);
        }
    }

    // Returns the marker at the index, creating it when the pool is too small.
    private Graphic GetMarker(int index)
    {
        if (index == markers.Count)
            markers.Add(Instantiate(markerPrefab, markerParent));

        Graphic marker = markers[index];
        if (!marker.gameObject.activeSelf)
            marker.gameObject.SetActive(true);

        return marker;
    }
}