using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A place pilgrims and monks walk to and rest at, such as a shrine or a scenic spot. It adds
/// itself to a shared list while enabled, so no scene reference is needed.
/// </summary>
[DisallowMultipleComponent]
public class InterestPoint : MonoBehaviour
{
    private static readonly List<InterestPoint> active = new List<InterestPoint>();

    // Monks visit only points with this ticked. Pilgrims visit every point.
    [SerializeField] private bool forMonks = true;

    // Visitors rest at a random spot this far from the point, so they do not stand inside it.
    [SerializeField] private float standRadius = 3f;

    // Every enabled point. Pilgrims and monks read this when they pick where to go.
    public static IReadOnlyList<InterestPoint> Active => active;

    public bool ForMonks => forMonks;
    public float StandRadius => standRadius;

    // Empties the list when play starts, also when the editor skips the domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetList()
    {
        active.Clear();
    }

    private void OnEnable()
    {
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    private void OnValidate()
    {
        standRadius = Mathf.Max(0f, standRadius);
    }

    // Sets up a point made in code, such as a scenic spot. Call it before the point is enabled.
    public void Configure(bool monks, float radius)
    {
        forMonks = monks;
        standRadius = Mathf.Max(0f, radius);
    }
}