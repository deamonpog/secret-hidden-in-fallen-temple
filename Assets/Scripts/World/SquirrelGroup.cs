using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A group of squirrels that wander around one shared centre and flee together. The centre
/// moves to a new nearby NavMesh point every so often, and the squirrels follow because
/// they pick their wander points around it. When one squirrel starts to flee, the group
/// makes the others flee from the same threat. Respawned squirrels join the group with the
/// fewest living members.
/// </summary>
public class SquirrelGroup : MonoBehaviour
{
    // Attempts to find a new centre before waiting for the next move.
    private const int MoveTries = 5;

    // How far around a chosen point the NavMesh is searched.
    private const float NavSampleDistance = 2f;

    private static readonly List<SquirrelGroup> active = new List<SquirrelGroup>();

    // Farthest the centre moves in one step.
    [SerializeField] private float stepDistance = 6f;
    // The centre stays within this distance of where the group started.
    [SerializeField] private float homeRadius = 20f;
    [SerializeField] private float moveMinSeconds = 10f;
    [SerializeField] private float moveMaxSeconds = 20f;

    private readonly List<Squirrel> members = new List<Squirrel>();
    private Vector3 home;
    private float moveLeft;

    // Every enabled group. The world generator reads this to place respawned squirrels.
    public static IReadOnlyList<SquirrelGroup> Active => active;

    // The point the members wander around.
    public Vector3 Center { get; private set; }

    // Members that are still alive.
    public int LivingCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < members.Count; i++)
            {
                if (!members[i].Health.IsDead)
                    count++;
            }

            return count;
        }
    }

    // Empties the list when play starts, also when the editor skips the domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetList()
    {
        active.Clear();
    }

    private void Awake()
    {
        home = transform.position;
        Center = home;
        moveLeft = Random.Range(moveMinSeconds, moveMaxSeconds);
    }

    private void OnEnable()
    {
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    private void Update()
    {
        moveLeft -= Time.deltaTime;
        if (moveLeft > 0f)
            return;

        moveLeft = Random.Range(moveMinSeconds, moveMaxSeconds);
        MoveCenter();
    }

    // Keeps the tuning values in a sensible range when they are edited in the inspector.
    private void OnValidate()
    {
        stepDistance = Mathf.Max(0f, stepDistance);
        homeRadius = Mathf.Max(0f, homeRadius);
        moveMinSeconds = Mathf.Max(0f, moveMinSeconds);
        moveMaxSeconds = Mathf.Max(moveMinSeconds, moveMaxSeconds);
    }

    public void Add(Squirrel squirrel)
    {
        if (!members.Contains(squirrel))
            members.Add(squirrel);
    }

    public void Remove(Squirrel squirrel)
    {
        members.Remove(squirrel);
    }

    // Makes every other member flee from the threat.
    public void Alarm(Transform threat, Squirrel source)
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] != source)
                members[i].Scare(threat);
        }
    }

    // Moves the centre to a NavMesh point up to stepDistance away, inside homeRadius.
    private void MoveCenter()
    {
        for (int i = 0; i < MoveTries; i++)
        {
            Vector2 offset = Random.insideUnitCircle * stepDistance;
            Vector3 candidate = Center + new Vector3(offset.x, 0f, offset.y);

            Vector3 fromHome = candidate - home;
            fromHome.y = 0f;
            if (fromHome.magnitude > homeRadius)
                continue;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavSampleDistance, NavMesh.AllAreas))
            {
                Center = hit.position;
                transform.position = Center;
                return;
            }
        }
    }
}