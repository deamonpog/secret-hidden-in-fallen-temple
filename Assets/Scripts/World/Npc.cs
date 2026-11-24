using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A pilgrim or a monk. It takes one offered item from the player once and gives one
/// reward item once. When it has a NavMeshAgent it also lives in the world: it walks to
/// interest points (shrines, and scenic spots for pilgrims), rests there with its rest
/// animations, waves at a player who comes near (pilgrims), and runs from lions it can see
/// and from attack swings nearby (pilgrims), then walks on to its next point. A monk blesses
/// a restless lion it sees instead of running, which calms the lion and sends it away.
/// </summary>
[DefaultExecutionOrder(10)]
public class Npc : MonoBehaviour, IInteractable
{
    private enum Mode
    {
        Resting,
        StandingUp,
        Walking,
        Fleeing,
        Waiting,
        Blessing
    }

    // Extra distance that still counts as having arrived at a destination.
    private const float ArriveSlack = 0.5f;

    // Smoothing time in seconds for the Speed parameter.
    private const float SpeedDamping = 0.1f;

    // Seconds between checks for nearby lions.
    private const float LionCheckSeconds = 0.25f;

    // How far around a chosen spot the NavMesh is searched.
    private const float NavSampleDistance = 2f;

    // Attempts to find a free standing spot around a point.
    private const int StandSpotTries = 5;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int RestingHash = Animator.StringToHash("Resting");
    private static readonly int WaveHash = Animator.StringToHash("Wave");
    private static readonly int RestVariantHash = Animator.StringToHash("RestVariant");
    private static readonly int BlessHash = Animator.StringToHash("Bless");

    // Tag of the walking state in the Animator. Walking waits until the model is there.
    private const string MoveTag = "Move";

    // Points that pass the filter, reused so picking a point makes no garbage.
    private static readonly List<InterestPoint> candidates = new List<InterestPoint>();

    [SerializeField] private ItemType offerItem = ItemType.Fruit;
    [SerializeField] private ItemType rewardItem = ItemType.CluePoint;
    [SerializeField] private string prompt = "Give fruit";

    // The player. The world generator sets this at spawn.
    [SerializeField] private Player player;

    // Optional. The Animator on the model child. Speed, Resting and Wave are set when it has them.
    [SerializeField] private Animator animator;

    [Header("Roaming (needs a NavMeshAgent)")]
    // Pilgrims visit every interest point, monks only the points marked for monks.
    [SerializeField] private bool visitsAllPoints = true;
    [SerializeField] private float restMinSeconds = 6f;
    [SerializeField] private float restMaxSeconds = 15f;
    // Longest wait for the stand-up animation before walking starts anyway.
    // Walking starts sooner once the Animator is back in its state tagged Move.
    [SerializeField] private float standUpSeconds = 4f;
    // Points farther than this are picked only when no nearer point exists.
    [SerializeField] private float maxTravelDistance = 40f;
    [SerializeField] private float walkSpeed = 1.5f;
    // Number of rest animations to pick from. Each rest sets the Animator int RestVariant to a
    // random value from 0 to restVariants - 1, such as standing or kneeling prayer for a monk.
    [SerializeField] private int restVariants = 1;

    [Header("Waving")]
    [SerializeField] private bool wavesAtPlayer = true;
    [SerializeField] private float waveRadius = 6f;
    [SerializeField] private float waveCooldown = 20f;

    [Header("Fleeing")]
    // Runs from an attack swing within scareRadius.
    [SerializeField] private bool fleesFromAttack;
    // Runs from a living lion within lionSightRange.
    [SerializeField] private bool avoidsLions = true;
    [SerializeField] private float scareRadius = 8f;
    [SerializeField] private float lionSightRange = 10f;
    [SerializeField] private float fleeDistance = 10f;
    // Seconds it waits after running before it walks on.
    [SerializeField] private float waitSeconds = 3f;
    [SerializeField] private float runSpeed = 4f;

    [Header("Blessing lions (monks)")]
    // Blesses a living, restless lion within blessRange, which calms it and sends it away.
    [SerializeField] private bool blessesLions;
    [SerializeField] private float blessRange = 8f;
    // Seconds the blessing animation needs before the Npc walks on.
    [SerializeField] private float blessSeconds = 2.5f;
    // Seconds before this Npc can bless again.
    [SerializeField] private float blessCooldown = 15f;

    private readonly HashSet<int> animatorParameters = new HashSet<int>();

    private NavMeshAgent agent;
    private Abilities abilities;
    private Mode mode;
    private float modeLeft;
    private float lionCheckLeft;
    private float waveCooldownLeft;
    private InterestPoint target;
    private float blessCooldownLeft;
    private Lion blessTarget;

    // True after the trade happened.
    public bool Given { get; private set; }

    public string PromptText => prompt;

    // Sets the player and the reward. Call it before the Npc is first enabled, because
    // Awake checks the player and OnEnable subscribes to its attacks.
    public void Configure(Player value, ItemType reward)
    {
        player = value;
        rewardItem = reward;
    }

    private void Awake()
    {
        if (GetComponentInChildren<Collider>() == null)
            Debug.LogWarning("Npc has no collider on it or its children, " +
                             "so it cannot be interacted with.", this);

        agent = GetComponent<NavMeshAgent>();

        if (player == null && (fleesFromAttack || wavesAtPlayer))
        {
            Debug.LogWarning("Npc has no player, so it neither waves nor flees from swings.", this);
            fleesFromAttack = false;
            wavesAtPlayer = false;
        }

        if (animator != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                animatorParameters.Add(parameter.nameHash);
        }
    }

    private void OnEnable()
    {
        // Player.Awake has already run, because this runs at execution order 10.
        if (fleesFromAttack && agent != null && player.Abilities != null)
        {
            abilities = player.Abilities;
            abilities.AbilityStarted += OnAbilityStarted;
        }
    }

    private void OnDisable()
    {
        if (abilities != null)
        {
            abilities.AbilityStarted -= OnAbilityStarted;
            abilities = null;
        }
    }

    private void Start()
    {
        if (agent != null)
            EnterResting(Random.Range(restMinSeconds, restMaxSeconds));
    }

    // Keeps the tuning values in a sensible range when they are edited in the inspector.
    private void OnValidate()
    {
        restMinSeconds = Mathf.Max(0f, restMinSeconds);
        restMaxSeconds = Mathf.Max(restMinSeconds, restMaxSeconds);
        standUpSeconds = Mathf.Max(0f, standUpSeconds);
        maxTravelDistance = Mathf.Max(0f, maxTravelDistance);
        walkSpeed = Mathf.Max(0f, walkSpeed);
        waveRadius = Mathf.Max(0f, waveRadius);
        waveCooldown = Mathf.Max(0f, waveCooldown);
        scareRadius = Mathf.Max(0f, scareRadius);
        lionSightRange = Mathf.Max(0f, lionSightRange);
        fleeDistance = Mathf.Max(0f, fleeDistance);
        waitSeconds = Mathf.Max(0f, waitSeconds);
        runSpeed = Mathf.Max(0f, runSpeed);
        restVariants = Mathf.Max(1, restVariants);
        blessRange = Mathf.Max(0f, blessRange);
        blessSeconds = Mathf.Max(0f, blessSeconds);
        blessCooldown = Mathf.Max(0f, blessCooldown);
    }

    private void Update()
    {
        if (agent == null)
            return;

        float dt = Time.deltaTime;
        waveCooldownLeft = Mathf.Max(0f, waveCooldownLeft - dt);
        blessCooldownLeft = Mathf.Max(0f, blessCooldownLeft - dt);

        if (mode != Mode.Fleeing && mode != Mode.Blessing)
            CheckForLions(dt);

        switch (mode)
        {
            case Mode.Resting:
                UpdateResting(dt);
                break;
            case Mode.StandingUp:
                modeLeft -= dt;
                if (modeLeft <= 0f || ReadyToWalk())
                    WalkToNextPoint();
                break;
            case Mode.Walking:
                if (Arrived())
                    EnterResting(Random.Range(restMinSeconds, restMaxSeconds));
                break;
            case Mode.Fleeing:
                if (Arrived())
                {
                    mode = Mode.Waiting;
                    modeLeft = waitSeconds;
                }
                break;
            case Mode.Waiting:
                modeLeft -= dt;
                if (modeLeft <= 0f)
                    WalkToNextPoint();
                break;
            case Mode.Blessing:
                if (blessTarget != null)
                    Face(blessTarget.transform.position, dt);
                modeLeft -= dt;
                if (modeLeft <= 0f)
                {
                    mode = Mode.StandingUp;
                    modeLeft = standUpSeconds;
                }
                break;
        }

        if (HasParameter(SpeedHash))
            animator.SetFloat(SpeedHash, agent.velocity.magnitude, SpeedDamping, dt);
    }

    public bool CanInteract(Player player)
    {
        return !Given && player.Count(offerItem) > 0;
    }

    public void Interact(Player player)
    {
        if (!player.TryRemove(offerItem))
            return;

        Given = true;
        player.Add(rewardItem);
        player.RegisterCountedAction();
    }

    // Faces the player while the player is near, otherwise the point it rests at. Pilgrims
    // wave at a nearby player now and then. When the rest time ends it starts standing up.
    private void UpdateResting(float dt)
    {
        modeLeft -= dt;
        bool playerNear = PlayerNear();

        if (playerNear && wavesAtPlayer && waveCooldownLeft <= 0f)
        {
            SetTrigger(WaveHash);
            waveCooldownLeft = waveCooldown;
        }

        if (playerNear)
            Face(player.transform.position, dt);
        else if (target != null)
            Face(target.transform.position, dt);

        if (modeLeft <= 0f)
        {
            SetBool(RestingHash, false);
            mode = Mode.StandingUp;
            modeLeft = standUpSeconds;
        }
    }

    private void EnterResting(float seconds)
    {
        mode = Mode.Resting;
        modeLeft = seconds;
        StopMoving();

        if (restVariants > 1 && HasParameter(RestVariantHash))
            animator.SetInteger(RestVariantHash, Random.Range(0, restVariants));

        SetBool(RestingHash, true);
    }

    // True once the model is back in its walking state, so it never slides while still
    // sitting or kneeling. Without an Animator it is ready at once.
    private bool ReadyToWalk()
    {
        if (animator == null)
            return true;

        return animator.GetCurrentAnimatorStateInfo(0).IsTag(MoveTag) && !animator.IsInTransition(0);
    }

    // Picks a new point and walks to a spot beside it. With no point to go to, it rests again.
    private void WalkToNextPoint()
    {
        target = PickPoint();
        if (target == null || !FindStandSpot(target, out Vector3 spot))
        {
            EnterResting(Random.Range(restMinSeconds, restMaxSeconds));
            return;
        }

        agent.speed = walkSpeed;
        MoveTo(spot);
        mode = Mode.Walking;
    }

    // Runs to a NavMesh point about fleeDistance away from the threat.
    private void RunFrom(Vector3 threat)
    {
        Vector3 away = transform.position - threat;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = -transform.forward;

        Vector3 candidate = transform.position + away.normalized * fleeDistance;
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
            return;

        SetBool(RestingHash, false);
        agent.speed = runSpeed;
        MoveTo(hit.position);
        mode = Mode.Fleeing;
    }

    // Four times a second: a monk blesses a restless lion in range, and any other Npc
    // that avoids lions runs from the nearest one it sees.
    private void CheckForLions(float dt)
    {
        lionCheckLeft -= dt;
        if (lionCheckLeft > 0f)
            return;

        lionCheckLeft = LionCheckSeconds;

        if (blessesLions)
        {
            if (blessCooldownLeft <= 0f)
                TryBless();
            return;
        }

        if (!avoidsLions)
            return;

        Lion lion = NearestLion(lionSightRange, false);
        if (lion != null)
            RunFrom(lion.transform.position);
    }

    // Blesses the nearest restless lion in range, then plays the blessing.
    private void TryBless()
    {
        Lion lion = NearestLion(blessRange, true);
        if (lion == null || !lion.Bless(transform.position))
            return;

        blessTarget = lion;
        blessCooldownLeft = blessCooldown;
        SetBool(RestingHash, false);
        StopMoving();
        SetTrigger(BlessHash);
        mode = Mode.Blessing;
        modeLeft = blessSeconds;
    }

    // The nearest living lion within range, or null. With restlessOnly, calm lions are skipped.
    private Lion NearestLion(float range, bool restlessOnly)
    {
        Lion nearest = null;
        float bestSqr = range * range;
        IReadOnlyList<Lion> lions = Lion.Active;

        for (int i = 0; i < lions.Count; i++)
        {
            if (lions[i].Health.IsDead || (restlessOnly && lions[i].Distracted))
                continue;

            float sqr = (lions[i].transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = lions[i];
            }
        }

        return nearest;
    }

    // Runs from an attack swing within scareRadius, whether or not it hits anything.
    private void OnAbilityStarted(AbilityType ability)
    {
        if (ability != AbilityType.Attack)
            return;

        Vector3 origin = player.transform.position;
        if (Vector3.Distance(origin, transform.position) <= scareRadius)
            RunFrom(origin);
    }

    // A random eligible point within maxTravelDistance, or any eligible point when none is
    // that near, never the one it is at now.
    private InterestPoint PickPoint()
    {
        candidates.Clear();
        float maxSqr = maxTravelDistance * maxTravelDistance;
        IReadOnlyList<InterestPoint> points = InterestPoint.Active;

        for (int i = 0; i < points.Count; i++)
        {
            if (Eligible(points[i])
                && (points[i].transform.position - transform.position).sqrMagnitude <= maxSqr)
                candidates.Add(points[i]);
        }

        if (candidates.Count == 0)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (Eligible(points[i]))
                    candidates.Add(points[i]);
            }
        }

        return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
    }

    private bool Eligible(InterestPoint point)
    {
        return point != target && (visitsAllPoints || point.ForMonks);
    }

    // A NavMesh spot at the point's stand radius, in a random direction.
    private static bool FindStandSpot(InterestPoint point, out Vector3 spot)
    {
        for (int i = 0; i < StandSpotTries; i++)
        {
            float angle = Random.Range(0f, 360f);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 candidate = point.transform.position + direction * point.StandRadius;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavSampleDistance, NavMesh.AllAreas))
            {
                spot = hit.position;
                return true;
            }
        }

        spot = point.transform.position;
        return false;
    }

    private bool PlayerNear()
    {
        return player != null && !player.Health.IsDead && !player.IsConcealed
               && Vector3.Distance(player.transform.position, transform.position) <= waveRadius;
    }

    // Turns toward a point on the ground plane, at the agent's turn speed.
    private void Face(Vector3 point, float dt)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(direction), agent.angularSpeed * dt);
    }

    private bool Arrived()
    {
        if (!agent.isOnNavMesh)
            return true;

        return !agent.pathPending
               && (!agent.hasPath
                   || agent.remainingDistance <= agent.stoppingDistance + ArriveSlack);
    }

    private void MoveTo(Vector3 destination)
    {
        if (agent.isOnNavMesh)
            agent.SetDestination(destination);
    }

    private void StopMoving()
    {
        if (agent.isOnNavMesh)
            agent.ResetPath();
    }

    private void SetTrigger(int hash)
    {
        if (HasParameter(hash))
            animator.SetTrigger(hash);
    }

    private void SetBool(int hash, bool value)
    {
        if (HasParameter(hash))
            animator.SetBool(hash, value);
    }

    private bool HasParameter(int hash)
    {
        return animator != null && animatorParameters.Contains(hash);
    }
}