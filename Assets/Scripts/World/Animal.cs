using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Shared behavior of the moving creatures, Squirrel and Lion. A NavMeshAgent moves the
/// animal through a small state machine: idle, wander, chase, attack, flee and dead. How it
/// treats the player depends on the player's current personality, which each subclass
/// answers in AttitudeFor. A subclass can also name a threat to run from (FindThreat) and
/// prey to hunt (FindPrey), and decides what the interact button does. The animal needs a
/// collider that is not a trigger, because Attack ignores triggers.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public abstract class Animal : MonoBehaviour, IInteractable
{
    // Seconds between new destinations while chasing.
    private const float ChaseRepathSeconds = 0.25f;

    // Seconds between new destinations while fleeing.
    private const float FleeRepathSeconds = 0.5f;

    // A target is lost when it stays farther than senseRange times this.
    private const float ChaseRangeFactor = 1.5f;

    // An attack turns back into a chase when the target is farther than attackRange times this.
    private const float AttackLeaveFactor = 1.2f;

    // Extra distance that still counts as having arrived at a destination.
    private const float ArriveSlack = 0.2f;

    // Search radius around a random wander point when looking for the nearest NavMesh point.
    private const float WanderSampleRadius = 2f;

    // Attempts to find a wander point before resting again.
    private const int WanderTries = 5;

    // Smoothing time in seconds for the Speed parameter.
    private const float SpeedDamping = 0.1f;

    // Directions tried when fleeing, in degrees from the line straight away from the threat.
    private static readonly float[] FleeAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f };

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    // The player in the scene. The world generator sets this at spawn. In the test scene,
    // assign it on each placed animal.
    [SerializeField] private Player player;

    // Optional. The Animator on the model child. Parameters it does not have are skipped.
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float runSpeed = 4f;
    // Wander points are picked within this distance of WanderCenter.
    [SerializeField] private float wanderRadius = 6f;
    [SerializeField] private float idleMinSeconds = 2f;
    [SerializeField] private float idleMaxSeconds = 5f;
    // An idle animal farther than this from its WanderCenter walks back at once. 0 turns it off.
    [SerializeField] private float regroupDistance;

    [Header("Senses")]
    // Distance at which it notices a player who is not concealed.
    [SerializeField] private float senseRange = 8f;
    // Seconds a chase goes on after the target is concealed or out of reach.
    [SerializeField] private float loseDelay = 2f;
    // A fleeing animal calms down at this distance from what it runs from.
    [SerializeField] private float safeDistance = 12f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDamage = 10f;
    // Seconds between bites or swipes.
    [SerializeField] private float attackInterval = 1.5f;

    private readonly HashSet<int> animatorParameters = new HashSet<int>();

    private NavMeshAgent agent;
    private PersonalityController personality;
    private Vector3 home;
    private float idleLeft;
    private float repathLeft;
    private float attackCooldownLeft;
    private float lostSeconds;
    private float lastHealth;

    // What the animal chases or bites: the player or prey. Null when it is not hunting.
    private Transform chaseTarget;
    private Health chaseHealth;
    private bool chasingPlayer;

    // What the animal runs from: the player or a threat. Null when it is not fleeing.
    private Transform fleeFrom;

    public AnimalState State { get; private set; }
    public Health Health { get; private set; }

    public abstract string PromptText { get; }
    public abstract bool CanInteract(Player player);
    public abstract void Interact(Player player);

    // How the animal treats the player under the given personality.
    protected abstract Attitude AttitudeFor(PersonalityType personality);

    // A threat to run from, checked before the player. None by default.
    protected virtual Transform FindThreat()
    {
        return null;
    }

    // Prey to hunt when the player gives no reason to act. None by default.
    protected virtual Health FindPrey()
    {
        return null;
    }

    // The point wander targets are picked around. The spawn point by default.
    protected virtual Vector3 WanderCenter => home;

    // Called when the animal starts to flee on its own, not when Scare made it flee.
    protected virtual void OnStartedFleeing(Transform from) { }

    // Called when a bite killed the prey it was hunting.
    protected virtual void OnPreyKilled(Health prey) { }

    // Called after the player's personality shifted. The state machine re-checks the
    // attitude on its own, so subclasses use this only for their own flags.
    protected virtual void OnShifted(PersonalityType previous, PersonalityType current) { }

    // Called once, right after the animal died.
    protected virtual void OnDied() { }

    // Called when the animal loses health and survives.
    protected virtual void OnDamaged() { }

    // Sets the player. Call it before the animal is first enabled, because OnEnable
    // subscribes to the player's personality.
    public void SetPlayer(Player value)
    {
        player = value;
    }

    // Makes the animal run from the threat, unless it is dead or already fleeing. A group
    // passes an alarm on this way, so the alarm is not raised again.
    public void Scare(Transform from)
    {
        if (from == null || State == AnimalState.Dead || State == AnimalState.Flee)
            return;

        EnterFlee(from, false);
    }

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        Health = GetComponent<Health>();
        home = transform.position;
        lastHealth = Health.Current;

        if (GetComponentInChildren<Collider>() == null)
            Debug.LogWarning(name + " has no collider on it or its children, " +
                             "so it cannot be attacked or interacted with.", this);

        if (animator != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                animatorParameters.Add(parameter.nameHash);
        }
    }

    protected virtual void OnEnable()
    {
        Health.Died += HandleDied;
        Health.Changed += HandleHealthChanged;

        // Player.Awake has already run, because Squirrel and Lion run at execution order 10.
        if (player != null && player.Personality != null)
        {
            personality = player.Personality;
            personality.Shifted += HandleShifted;
        }
    }

    protected virtual void OnDisable()
    {
        Health.Died -= HandleDied;
        Health.Changed -= HandleHealthChanged;

        if (personality != null)
        {
            personality.Shifted -= HandleShifted;
            personality = null;
        }
    }

    private void Start()
    {
        if (player == null)
            Debug.LogWarning(name + " has no player assigned, so it ignores the player.", this);

        if (!agent.isOnNavMesh)
            Debug.LogWarning(name + " is not on the NavMesh, so it cannot move.", this);

        EnterIdle(Random.Range(idleMinSeconds, idleMaxSeconds));
    }

    private void Update()
    {
        if (State == AnimalState.Dead)
            return;

        float dt = Time.deltaTime;
        repathLeft -= dt;
        attackCooldownLeft = Mathf.Max(0f, attackCooldownLeft - dt);

        switch (State)
        {
            case AnimalState.Idle:
                UpdateIdle(dt);
                break;
            case AnimalState.Wander:
                UpdateWander();
                break;
            case AnimalState.Chase:
                UpdateChase(dt);
                break;
            case AnimalState.Attack:
                UpdateAttack(dt);
                break;
            case AnimalState.Flee:
                UpdateFlee();
                break;
        }

        if (HasParameter(SpeedHash))
            animator.SetFloat(SpeedHash, agent.velocity.magnitude, SpeedDamping, dt);
    }

    // Keeps the tuning values in a sensible range when they are edited in the inspector.
    protected virtual void OnValidate()
    {
        walkSpeed = Mathf.Max(0f, walkSpeed);
        runSpeed = Mathf.Max(0f, runSpeed);
        wanderRadius = Mathf.Max(0f, wanderRadius);
        idleMinSeconds = Mathf.Max(0f, idleMinSeconds);
        idleMaxSeconds = Mathf.Max(idleMinSeconds, idleMaxSeconds);
        senseRange = Mathf.Max(0f, senseRange);
        loseDelay = Mathf.Max(0f, loseDelay);
        safeDistance = Mathf.Max(0f, safeDistance);
        attackRange = Mathf.Max(0f, attackRange);
        attackDamage = Mathf.Max(0f, attackDamage);
        attackInterval = Mathf.Max(0.1f, attackInterval);
        regroupDistance = Mathf.Max(0f, regroupDistance);
    }

    // Stops the animal on the spot for the given seconds, then it wanders again.
    // A threat or the player can still end the wait early.
    protected void HoldStill(float seconds)
    {
        if (State != AnimalState.Dead)
            EnterIdle(seconds);
    }

    // Fires an Animator trigger when the controller has it.
    protected void SetTrigger(int hash)
    {
        if (HasParameter(hash))
            animator.SetTrigger(hash);
    }

    private void UpdateIdle(float dt)
    {
        if (LookAround())
            return;

        // Strayed too far from its group or home, so it heads back without resting first.
        if (regroupDistance > 0f && Vector3.Distance(transform.position, WanderCenter) > regroupDistance)
        {
            EnterWander();
            return;
        }

        idleLeft -= dt;
        if (idleLeft <= 0f)
            EnterWander();
    }

    private void UpdateWander()
    {
        if (LookAround())
            return;

        if (Arrived())
            EnterIdle(Random.Range(idleMinSeconds, idleMaxSeconds));
    }

    private void UpdateChase(float dt)
    {
        if (RunFromThreat())
            return;

        if (!KeepHunting(dt))
        {
            EnterWander();
            return;
        }

        if (DistanceTo(chaseTarget) <= attackRange)
        {
            EnterAttack();
            return;
        }

        if (repathLeft <= 0f)
        {
            MoveTo(chaseTarget.position);
            repathLeft = ChaseRepathSeconds;
        }
    }

    private void UpdateAttack(float dt)
    {
        if (RunFromThreat())
            return;

        if (!KeepHunting(dt))
        {
            EnterWander();
            return;
        }

        if (DistanceTo(chaseTarget) > attackRange * AttackLeaveFactor)
        {
            EnterChase();
            return;
        }

        Face(chaseTarget.position, dt);

        // A concealed player is not bitten, but the animal waits nearby until it loses them.
        if (attackCooldownLeft > 0f || (chasingPlayer && player.IsConcealed))
            return;

        SetTrigger(AttackHash);
        attackCooldownLeft = attackInterval;

        Health bitten = chaseHealth;
        bitten.Damage(attackDamage);

        if (!chasingPlayer && bitten.IsDead)
        {
            chaseTarget = null;
            chaseHealth = null;
            OnPreyKilled(bitten);
        }
    }

    private void UpdateFlee()
    {
        bool fromPlayer = player != null && fleeFrom == player.transform;
        if (fleeFrom == null || DistanceTo(fleeFrom) >= safeDistance
            || (fromPlayer && CurrentAttitude() != Attitude.Flee))
        {
            EnterWander();
            return;
        }

        if (repathLeft > 0f)
            return;

        if (FindFleePoint(out Vector3 point))
            MoveTo(point);
        repathLeft = FleeRepathSeconds;
    }

    // Looks around while idle or wandering: a threat comes first, then prey, then the
    // player. Returns true when the state changed.
    private bool LookAround()
    {
        if (RunFromThreat())
            return true;

        Health prey = FindPrey();
        if (prey != null)
        {
            StartHunting(prey.transform, prey, false);
            return true;
        }

        if (NoticesPlayer())
        {
            switch (CurrentAttitude())
            {
                case Attitude.Hostile:
                    StartHunting(player.transform, player.Health, true);
                    return true;
                case Attitude.Flee:
                    EnterFlee(player.transform, true);
                    return true;
            }
        }

        return false;
    }

    // Starts fleeing from a threat the subclass names. Returns true when it did.
    private bool RunFromThreat()
    {
        Transform threat = FindThreat();
        if (threat == null)
            return false;

        EnterFlee(threat, true);
        return true;
    }

    private void StartHunting(Transform target, Health health, bool isPlayer)
    {
        chaseTarget = target;
        chaseHealth = health;
        chasingPlayer = isPlayer;
        lostSeconds = 0f;
        EnterChase();
    }

    // True while the hunt goes on. The player is lost when the attitude is no longer
    // Hostile, after loseDelay of being concealed or out of reach, and at once when dead.
    // Prey is lost when it dies or stays out of reach for loseDelay.
    private bool KeepHunting(float dt)
    {
        if (chaseTarget == null || chaseHealth == null || chaseHealth.IsDead)
            return false;

        if (chasingPlayer)
        {
            if (CurrentAttitude() != Attitude.Hostile)
                return false;

            bool tracked = !player.IsConcealed
                           && DistanceTo(chaseTarget) <= senseRange * ChaseRangeFactor;
            lostSeconds = tracked ? 0f : lostSeconds + dt;
            return lostSeconds < loseDelay;
        }

        bool inReach = DistanceTo(chaseTarget) <= senseRange * ChaseRangeFactor;
        lostSeconds = inReach ? 0f : lostSeconds + dt;
        return lostSeconds < loseDelay;
    }

    private void EnterIdle(float seconds)
    {
        State = AnimalState.Idle;
        idleLeft = seconds;
        ClearTargets();
        StopMoving();
    }

    private void EnterWander()
    {
        State = AnimalState.Wander;
        ClearTargets();
        agent.speed = walkSpeed;
        agent.stoppingDistance = 0f;

        if (FindWanderPoint(out Vector3 point))
            MoveTo(point);
        else
            EnterIdle(idleMinSeconds);
    }

    private void EnterChase()
    {
        State = AnimalState.Chase;
        agent.speed = runSpeed;
        agent.stoppingDistance = attackRange * 0.8f;
        repathLeft = 0f;
    }

    private void EnterAttack()
    {
        State = AnimalState.Attack;
        StopMoving();

        // The first bite comes a moment after arriving, not on the same frame.
        attackCooldownLeft = Mathf.Max(attackCooldownLeft, attackInterval * 0.5f);
    }

    // Runs from the given object. Raises OnStartedFleeing only when notify is true.
    private void EnterFlee(Transform from, bool notify)
    {
        State = AnimalState.Flee;
        chaseTarget = null;
        chaseHealth = null;
        fleeFrom = from;
        agent.speed = runSpeed;
        agent.stoppingDistance = 0f;
        repathLeft = 0f;

        if (notify)
            OnStartedFleeing(from);
    }

    private void ClearTargets()
    {
        chaseTarget = null;
        chaseHealth = null;
        fleeFrom = null;
    }

    private Attitude CurrentAttitude()
    {
        if (player == null || player.Personality == null)
            return Attitude.Neutral;

        return AttitudeFor(player.Personality.Current);
    }

    private bool NoticesPlayer()
    {
        return player != null && !player.Health.IsDead && !player.IsConcealed
               && DistanceTo(player.transform) <= senseRange;
    }

    private float DistanceTo(Transform other)
    {
        return Vector3.Distance(transform.position, other.position);
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

    // Picks a random NavMesh point within wanderRadius of WanderCenter.
    private bool FindWanderPoint(out Vector3 point)
    {
        Vector3 center = WanderCenter;
        for (int i = 0; i < WanderTries; i++)
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                                       WanderSampleRadius, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }
        }

        point = transform.position;
        return false;
    }

    // Looks for a NavMesh point about safeDistance away from what it runs from. When the
    // straight line away is blocked, for example at the edge of the map, it tries angles
    // to the side.
    private bool FindFleePoint(out Vector3 point)
    {
        Vector3 away = transform.position - fleeFrom.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = transform.forward;
        away.Normalize();

        float minSqr = safeDistance * safeDistance * 0.25f;
        for (int i = 0; i < FleeAngles.Length; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, FleeAngles[i], 0f) * away;
            Vector3 candidate = transform.position + direction * safeDistance;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                                       safeDistance * 0.5f, NavMesh.AllAreas)
                && (hit.position - transform.position).sqrMagnitude >= minSqr)
            {
                point = hit.position;
                return true;
            }
        }

        point = transform.position;
        return false;
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

    private void HandleShifted(PersonalityType previous, PersonalityType current)
    {
        OnShifted(previous, current);
    }

    // Plays the hit clip and tells the subclass only when health dropped and the animal is
    // still alive.
    private void HandleHealthChanged(float current)
    {
        if (current > 0f && current < lastHealth)
        {
            SetTrigger(HitHash);
            OnDamaged();
        }

        lastHealth = current;
    }

    private void HandleDied()
    {
        State = AnimalState.Dead;
        ClearTargets();
        StopMoving();
        agent.enabled = false;

        if (HasParameter(DeadHash))
            animator.SetBool(DeadHash, true);

        OnDied();
    }

    private bool HasParameter(int hash)
    {
        return animator != null && animatorParameters.Contains(hash);
    }
}