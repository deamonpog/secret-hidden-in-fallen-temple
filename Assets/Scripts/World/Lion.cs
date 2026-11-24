using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A mountain lion, shown with the Sabertooth model. It hunts squirrels first and the player
/// when no prey is near, every personality alike. Reaver (A) can kill it with Attack.
/// Thinker (C) can distract it with a caught squirrel: it stops to eat, stays calm for a
/// while, then hunts again and can be fed again. After a kill it eats all the meat and
/// waits a while before the next hunt. A monk's blessing calms it: it bows and walks off,
/// away from the monk. Hurting it ends any calm at once.
/// </summary>
[DefaultExecutionOrder(10)]
public class Lion : Animal
{
    private static readonly int EatHash = Animator.StringToHash("Eat");
    private static readonly int BowHash = Animator.StringToHash("Bow");

    private static readonly List<Lion> active = new List<Lion>();

    [SerializeField] private string feedPrompt = "Feed the lion";

    // Seconds the lion stands still to eat, after a feeding or a kill.
    [SerializeField] private float eatSeconds = 5f;

    // Seconds from feeding until it hunts again, eating included.
    [SerializeField] private float distractSeconds = 20f;

    [Header("Hunting squirrels")]
    // Distance at which it notices a squirrel to hunt.
    [SerializeField] private float huntRange = 10f;
    // Seconds after a kill before it hunts another squirrel.
    [SerializeField] private float huntCooldown = 60f;

    [Header("Blessing")]
    // Seconds a monk's blessing keeps the lion calm.
    [SerializeField] private float blessCalmSeconds = 30f;
    // Seconds the bow takes before it walks off.
    [SerializeField] private float bowSeconds = 2.5f;
    // How far from the monk it heads after the bow.
    [SerializeField] private float retreatDistance = 20f;

    // Game time at which the calm ends. Time.time stops while the game is paused.
    private float distractedUntil;

    // Game time from which it may hunt squirrels again.
    private float nextHuntTime;

    // Game time until which it wanders around the retreat point after a blessing.
    private float retreatUntil;
    private Vector3 retreatPoint;

    // Every enabled lion. Pilgrims, monks and squirrels read this.
    public static IReadOnlyList<Lion> Active => active;

    // True while the lion is eating, calm after being fed, or blessed.
    public bool Distracted => Time.time < distractedUntil;

    public override string PromptText => feedPrompt;

    // After a blessing it wanders around the retreat point, otherwise around its spawn point.
    protected override Vector3 WanderCenter =>
        Time.time < retreatUntil ? retreatPoint : base.WanderCenter;

    // Empties the list when play starts, also when the editor skips the domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetList()
    {
        active.Clear();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        active.Add(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        active.Remove(this);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        eatSeconds = Mathf.Max(0f, eatSeconds);
        distractSeconds = Mathf.Max(eatSeconds, distractSeconds);
        huntRange = Mathf.Max(0f, huntRange);
        huntCooldown = Mathf.Max(0f, huntCooldown);
        blessCalmSeconds = Mathf.Max(0f, blessCalmSeconds);
        bowSeconds = Mathf.Max(0f, bowSeconds);
        retreatDistance = Mathf.Max(0f, retreatDistance);
    }

    // A monk blesses the lion: it calms down, turns to the monk and bows, then wanders off
    // around a point away from the monk until the calm ends. Returns false when it is dead
    // or already calm.
    public bool Bless(Vector3 monkPosition)
    {
        if (Health.IsDead || Distracted)
            return false;

        distractedUntil = Time.time + blessCalmSeconds;
        retreatUntil = distractedUntil;

        Vector3 away = transform.position - monkPosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = transform.forward;

        Vector3 target = transform.position + away.normalized * retreatDistance;
        retreatPoint = NavMesh.SamplePosition(target, out NavMeshHit hit, retreatDistance * 0.5f, NavMesh.AllAreas)
            ? hit.position
            : transform.position;

        if (away.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(-away);

        HoldStill(bowSeconds);
        SetTrigger(BowHash);
        return true;
    }

    // Feeding works again once the calm is over, so a squirrel is never spent on a lion
    // that is already calm.
    public override bool CanInteract(Player player)
    {
        return !Distracted && !Health.IsDead
               && player.Personality.Current == PersonalityType.C
               && player.Count(ItemType.CaughtSquirrel) > 0;
    }

    public override void Interact(Player player)
    {
        if (!CanInteract(player) || !player.TryRemove(ItemType.CaughtSquirrel))
            return;

        distractedUntil = Time.time + distractSeconds;
        HoldStill(eatSeconds);
        SetTrigger(EatHash);
        player.RegisterCountedAction();
    }

    protected override Attitude AttitudeFor(PersonalityType personality)
    {
        return Distracted ? Attitude.Neutral : Attitude.Hostile;
    }

    // The nearest living squirrel within huntRange, while the lion is calm enough to hunt.
    protected override Health FindPrey()
    {
        if (Distracted || Time.time < nextHuntTime)
            return null;

        Squirrel nearest = null;
        float bestSqr = huntRange * huntRange;
        IReadOnlyList<Squirrel> squirrels = Squirrel.Active;

        for (int i = 0; i < squirrels.Count; i++)
        {
            if (squirrels[i].Health.IsDead)
                continue;

            float sqr = (squirrels[i].transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = squirrels[i];
            }
        }

        return nearest != null ? nearest.Health : null;
    }

    // Eats the kill like a feeding, then waits out the hunt cooldown. The lion eats all the
    // meat, so the body cannot be cooked and is gone when the meal ends.
    protected override void OnPreyKilled(Health prey)
    {
        Squirrel squirrel = prey.GetComponent<Squirrel>();
        if (squirrel != null)
            squirrel.MarkEaten();

        distractedUntil = Time.time + eatSeconds;
        nextHuntTime = Time.time + huntCooldown;
        HoldStill(eatSeconds);
        SetTrigger(EatHash);
        Destroy(prey.gameObject, eatSeconds);
    }

    // A hit breaks any calm and the retreat, so the lion turns on the player at once.
    protected override void OnDamaged()
    {
        distractedUntil = 0f;
        retreatUntil = 0f;
    }
}