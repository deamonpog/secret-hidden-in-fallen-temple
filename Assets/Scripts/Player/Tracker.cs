using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles the Track button. A press spends one clue point and, for a few seconds, gives the
/// direction to the nearest active shrine of the lowest tier. The direction follows the
/// NavMesh path, so it bends around obstacles. A press with no target costs nothing.
/// </summary>
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(PlayerInputReader))]
public class Tracker : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private float showSeconds = 5f;

    // How far from the player or the shrine a NavMesh point may be found.
    [SerializeField] private float sampleDistance = 3f;

    private Player player;
    private PlayerInputReader input;
    private NavMeshPath path;
    private float secondsLeft;

    // True while the direction is being shown.
    public bool Active => secondsLeft > 0f;

    // Flat unit vector toward the target, or zero when there is none.
    public Vector3 Direction { get; private set; }

    // The shrine being tracked, or null when nothing is shown.
    public Shrine Target { get; private set; }

    public float SecondsLeft => secondsLeft;

    private void Awake()
    {
        if (gameManager == null)
        {
            Debug.LogError("Tracker needs the GameManager assigned.", this);
            enabled = false;
            return;
        }

        player = GetComponent<Player>();
        input = GetComponent<PlayerInputReader>();
        path = new NavMeshPath();
    }

    private void OnEnable()
    {
        input.TrackPressed += OnTrackPressed;
    }

    private void OnDisable()
    {
        input.TrackPressed -= OnTrackPressed;
    }

    private void OnValidate()
    {
        showSeconds = Mathf.Max(0.1f, showSeconds);
        sampleDistance = Mathf.Max(0.1f, sampleDistance);
    }

    // Counts down the display time and keeps the direction up to date while it shows.
    private void Update()
    {
        if (!Active)
            return;

        secondsLeft -= Time.deltaTime;
        if (secondsLeft <= 0f)
        {
            secondsLeft = 0f;
            Target = null;
            Direction = Vector3.zero;
            return;
        }

        UpdateDirection();

        // Visible in the Scene view until the HUD arrow exists.
        Debug.DrawRay(transform.position + Vector3.up, Direction * 3f, Color.cyan);
    }

    private void OnTrackPressed()
    {
        Shrine target = FindTarget();
        if (target == null)
            return;

        if (!player.TryRemove(ItemType.CluePoint))
            return;

        Target = target;
        secondsLeft = showSeconds;
        UpdateDirection();
        player.RegisterCountedAction();
    }

    // Returns the active, unsatisfied shrine with the lowest tier, nearest first on a tie.
    private Shrine FindTarget()
    {
        Shrine best = null;
        float bestDistance = float.MaxValue;

        foreach (Shrine shrine in gameManager.Shrines)
        {
            if (!shrine.Active || shrine.Satisfied)
                continue;

            float distance = (shrine.transform.position - transform.position).sqrMagnitude;
            bool better = best == null
                || shrine.Tier < best.Tier
                || (shrine.Tier == best.Tier && distance < bestDistance);

            if (better)
            {
                best = shrine;
                bestDistance = distance;
            }
        }

        return best;
    }

    // Aims at the first corner of the NavMesh path, or straight at the shrine when
    // there is no path.
    private void UpdateDirection()
    {
        Vector3 from = transform.position;
        Vector3 to = Target.transform.position;
        Vector3 aim = to;

        if (NavMesh.SamplePosition(from, out NavMeshHit start, sampleDistance, NavMesh.AllAreas)
            && NavMesh.SamplePosition(to, out NavMeshHit end, sampleDistance, NavMesh.AllAreas)
            && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
            && path.corners.Length > 1)
        {
            aim = path.corners[1];
        }

        Vector3 flat = aim - from;
        flat.y = 0f;
        Direction = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.zero;
    }
}