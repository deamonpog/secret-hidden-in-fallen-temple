using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a moving entity, such as an animal, a pilgrim or a monk, that VisionController
/// shows only inside the player's vision radius and marks on screen while Sense runs.
/// It adds itself to a shared list while enabled, so neither side needs a scene reference.
/// </summary>
[DisallowMultipleComponent]
public class VisionTarget : MonoBehaviour
{
    private static readonly List<VisionTarget> active = new List<VisionTarget>();

    // Height above this object's pivot where the Sense marker is drawn.
    [SerializeField] private float markerHeight = 2f;

    // Color of the Sense marker, so the player can tell kinds apart.
    [SerializeField] private Color markerColor = Color.white;

    private Renderer[] renderers;
    private bool visible = true;

    // Every enabled target. VisionController reads this each frame.
    public static IReadOnlyList<VisionTarget> Active => active;

    // World position of the Sense marker.
    public Vector3 MarkerPosition => transform.position + Vector3.up * markerHeight;

    public Color MarkerColor => markerColor;

    // Empties the list when play starts, also when the editor skips the domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetList()
    {
        active.Clear();
    }

    private void Awake()
    {
        // Inactive children are included, so a renderer switched on later is covered too.
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);

        // A target that leaves the list is never left invisible.
        SetVisible(true);
    }

    // Shows or hides every renderer of this object and its children.
    public void SetVisible(bool value)
    {
        if (value == visible)
            return;

        visible = value;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = value;
        }
    }
}