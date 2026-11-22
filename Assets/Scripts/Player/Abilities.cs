using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the two abilities of the current personality on the Skill1 and Skill2 buttons.
/// It owns the cooldown of each button slot, the sharpened blade, and the timers for
/// Invisible and Sense. Every ability that succeeds counts as one action.
/// </summary>
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(PlayerInputReader))]
public class Abilities : MonoBehaviour
{
    private const int SlotCount = 2;
    private const int HitBufferSize = 32;

    // Number of personalities, so every personality gets its own cooldown per slot.
    private static readonly int PersonalityCount = Enum.GetValues(typeof(PersonalityType)).Length;

    [Header("Attack")]
    [SerializeField] private float attackDamage = 25f;
    // Distance from the player at which a collider can still be hit.
    [SerializeField] private float attackRange = 2.5f;
    // Total angle of the hit area, measured from the facing object. 360 hits all around.
    [SerializeField, Range(10f, 360f)] private float attackArc = 120f;
    // Object whose forward direction the arc uses. When empty, this object is used.
    [SerializeField] private Transform facing;
    [SerializeField] private float attackCooldown = 0.5f;

    [Header("Sharpen (called by the stone)")]
    [SerializeField] private float sharpenMultiplier = 2f;
    [SerializeField] private float sharpenDuration = 10f;
    [SerializeField] private float sharpenCooldown = 20f;

    [Header("Heal")]
    [SerializeField] private float healAmount = 25f;
    [SerializeField] private float healCooldown = 8f;

    [Header("Invisible")]
    [SerializeField] private float invisibleDuration = 4f;
    [SerializeField] private float invisibleCooldown = 12f;

    [Header("Dash")]
    [SerializeField] private float dashMultiplier = 2.5f;
    [SerializeField] private float dashDuration = 1.5f;
    [SerializeField] private float dashCooldown = 6f;

    [Header("Sense")]
    [SerializeField] private float senseDuration = 5f;
    [SerializeField] private float senseCooldown = 12f;

    [Header("Improve vision")]
    [SerializeField] private float visionMultiplier = 1.5f;
    [SerializeField] private float visionDuration = 6f;
    [SerializeField] private float visionCooldown = 15f;

    // Keys that identify which ability set a multiplier, so each can restart or remove its own.
    private readonly object sharpenKey = new object();
    private readonly object dashKey = new object();
    private readonly object visionKey = new object();

    // Seconds left for each personality and button slot, see CooldownIndex.
    private readonly float[] cooldownLeft = new float[PersonalityCount * SlotCount];

    private readonly Collider[] hitBuffer = new Collider[HitBufferSize];
    private readonly List<IDamageable> targets = new List<IDamageable>();

    private Player player;
    private PlayerInputReader input;

    // Multiplier on attack damage. Sharpen raises it for a while.
    private Stat damageScale;

    private float sharpenCooldownLeft;
    private float invisibleLeft;
    private float senseLeft;

    // True while the Invisible ability is running.
    public bool IsInvisible => invisibleLeft > 0f;

    // True while the Sense ability is running. The vision system reads this to show
    // hidden creatures, so the ability itself never touches the vision system.
    public bool IsSensing => senseLeft > 0f;

    // True while the blade is sharpened and attacks deal extra damage.
    public bool IsSharpened => damageScale.Value > 1f;

    // False during the sharpen cooldown. The stone checks this before it offers sharpening.
    public bool CanSharpen => sharpenCooldownLeft <= 0f;

    // Raised after an attack hit something, with the attack origin and the total damage dealt.
    public event Action<Vector3, float> AttackPerformed;

    // Raised when a press is accepted, before the ability runs. It also fires for an
    // attack that misses, so the swing can still be animated.
    public event Action<AbilityType> AbilityStarted;

    private void Awake()
    {
        player = GetComponent<Player>();
        input = GetComponent<PlayerInputReader>();
        damageScale = new Stat(1f);

        if (facing == null)
            facing = transform;
    }

    private void OnEnable()
    {
        input.Skill1Pressed += UseSlot1;
        input.Skill2Pressed += UseSlot2;
    }

    private void OnDisable()
    {
        input.Skill1Pressed -= UseSlot1;
        input.Skill2Pressed -= UseSlot2;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // Every personality's cooldowns run all the time, also while it is not active.
        for (int i = 0; i < cooldownLeft.Length; i++)
            cooldownLeft[i] = Mathf.Max(0f, cooldownLeft[i] - dt);

        sharpenCooldownLeft = Mathf.Max(0f, sharpenCooldownLeft - dt);
        invisibleLeft = Mathf.Max(0f, invisibleLeft - dt);
        senseLeft = Mathf.Max(0f, senseLeft - dt);
        damageScale.Tick(dt);
    }

    // Keeps the tuning values in a sensible range when they are edited in the inspector.
    private void OnValidate()
    {
        attackDamage = Mathf.Max(0f, attackDamage);
        attackRange = Mathf.Max(0f, attackRange);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        sharpenMultiplier = Mathf.Max(0f, sharpenMultiplier);
        sharpenDuration = Mathf.Max(0f, sharpenDuration);
        sharpenCooldown = Mathf.Max(0f, sharpenCooldown);
        healAmount = Mathf.Max(0f, healAmount);
        healCooldown = Mathf.Max(0f, healCooldown);
        invisibleDuration = Mathf.Max(0f, invisibleDuration);
        invisibleCooldown = Mathf.Max(0f, invisibleCooldown);
        dashMultiplier = Mathf.Max(0f, dashMultiplier);
        dashDuration = Mathf.Max(0f, dashDuration);
        dashCooldown = Mathf.Max(0f, dashCooldown);
        senseDuration = Mathf.Max(0f, senseDuration);
        senseCooldown = Mathf.Max(0f, senseCooldown);
        visionMultiplier = Mathf.Max(0f, visionMultiplier);
        visionDuration = Mathf.Max(0f, visionDuration);
        visionCooldown = Mathf.Max(0f, visionCooldown);
    }

    // Seconds until the button slot (0 or 1) of the current personality can be used again.
    public float CooldownLeft(int slot)
    {
        if (slot < 0 || slot >= SlotCount || player == null || player.Personality == null)
            return 0f;

        return cooldownLeft[CooldownIndex(player.Personality.Current, slot)];
    }

    // The position of a personality's slot in the cooldown array.
    private static int CooldownIndex(PersonalityType type, int slot)
    {
        return (int)type * SlotCount + slot;
    }

    // Doubles attack damage for a while. Does nothing during the sharpen cooldown.
    // The stone calls this and registers the counted action itself.
    public void Sharpen()
    {
        if (!CanSharpen)
            return;

        damageScale.AddTimed(sharpenKey, sharpenMultiplier, sharpenDuration);
        sharpenCooldownLeft = sharpenCooldown;
    }

    private void UseSlot1() => Use(0);
    private void UseSlot2() => Use(1);

    // Runs the ability in the slot for the current personality.
    private void Use(int slot)
    {
        PersonalityController personality = player.Personality;
        if (personality == null || player.Health.IsDead)
            return;

        PersonalityProfile profile = personality.CurrentProfile;

        // Taken now, because the counted action below can shift to another personality.
        int index = CooldownIndex(personality.Current, slot);
        if (profile == null || cooldownLeft[index] > 0f)
            return;

        AbilityType ability = slot == 0 ? profile.Ability1 : profile.Ability2;
        AbilityStarted?.Invoke(ability);
        if (!Perform(ability))
            return;

        // The cooldown goes to the personality that used the ability, and is set before
        // the action is counted, because the count can shift the personality.
        cooldownLeft[index] = CooldownOf(ability);
        player.RegisterCountedAction();
    }

    // Returns true when the ability took effect and should count as an action.
    private bool Perform(AbilityType ability)
    {
        switch (ability)
        {
            case AbilityType.Attack:
                return Attack();
            case AbilityType.Heal:
                // Counts even at full health.
                player.Health.Heal(healAmount);
                return true;
            case AbilityType.Invisible:
                invisibleLeft = invisibleDuration;
                return true;
            case AbilityType.Dash:
                player.Speed.AddTimed(dashKey, dashMultiplier, dashDuration);
                return true;
            case AbilityType.Sense:
                senseLeft = senseDuration;
                return true;
            case AbilityType.ImproveVision:
                player.Vision.AddTimed(visionKey, visionMultiplier, visionDuration);
                return true;
            default:
                return false;
        }
    }

    private float CooldownOf(AbilityType ability)
    {
        switch (ability)
        {
            case AbilityType.Attack: return attackCooldown;
            case AbilityType.Heal: return healCooldown;
            case AbilityType.Invisible: return invisibleCooldown;
            case AbilityType.Dash: return dashCooldown;
            case AbilityType.Sense: return senseCooldown;
            case AbilityType.ImproveVision: return visionCooldown;
            default: return 0f;
        }
    }

    // Damages every IDamageable within range and inside the arc. Returns false when
    // nothing lost health, so a swing at nothing is not an action.
    private bool Attack()
    {
        Vector3 origin = transform.position;
        int count = Physics.OverlapSphereNonAlloc(
            origin, attackRange, hitBuffer, ~0, QueryTriggerInteraction.Ignore);

        // Collect each target once, even when it has several colliders.
        targets.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit.transform.IsChildOf(transform))
                continue;

            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || targets.Contains(target))
                continue;

            if (!InsideArc(hit.bounds.ClosestPoint(origin)))
                continue;

            targets.Add(target);
        }

        float amount = attackDamage * damageScale.Value;
        float dealt = 0f;
        for (int i = 0; i < targets.Count; i++)
            dealt += targets[i].Damage(amount);

        if (dealt <= 0f)
            return false;

        AttackPerformed?.Invoke(origin, dealt);
        return true;
    }

    // True when the point lies inside the attack arc, measured on the ground plane.
    private bool InsideArc(Vector3 point)
    {
        if (attackArc >= 360f)
            return true;

        Vector3 toPoint = point - transform.position;
        toPoint.y = 0f;
        if (toPoint.sqrMagnitude < 0.0001f)
            return true;

        Vector3 forward = facing.forward;
        forward.y = 0f;
        return Vector3.Angle(forward, toPoint) <= attackArc * 0.5f;
    }
}