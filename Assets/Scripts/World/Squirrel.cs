using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A squirrel. Reaver (A) scares it away, can kill it with Attack and then cook the body
/// for a CookedSquirrel. Thinker (C) can catch it alive for a CaughtSquirrel, which feeds
/// the lion. Shade (B) sees it turn hostile after staying near it too long, which its
/// FearSource measures. That hostility is part of how B sees the world, so any shift calms
/// the squirrel again. It runs from lions it sees, and in a group it wanders around the
/// group's centre and alarms the others when it flees.
/// </summary>
[DefaultExecutionOrder(10)]
[RequireComponent(typeof(FearSource))]
public class Squirrel : Animal
{
    private static readonly List<Squirrel> active = new List<Squirrel>();

    [SerializeField] private string catchPrompt = "Catch the squirrel";
    [SerializeField] private string cookPrompt = "Cook the squirrel";

    // Distance at which it notices a lion and runs.
    [SerializeField] private float lionSightRange = 8f;
    // Seconds a dead squirrel lies there before it is gone. It can be cooked until then.
    [SerializeField] private float corpseSeconds = 30f;

    private FearSource fear;
    private SquirrelGroup group;
    private bool hostile;
    // True when a lion ate it, so there is nothing left to cook.
    private bool eaten;

    // Set by CanInteract, because the prompt depends on the personality and the
    // interface gives PromptText no player.
    private string prompt = "";

    // Every enabled squirrel. Lions read this when they look for prey.
    public static IReadOnlyList<Squirrel> Active => active;
    // Raised when a squirrel dies or is caught, so a new one can take its place.
    public static event Action<Squirrel> Lost;

    public override string PromptText => prompt;

    protected override Vector3 WanderCenter => group != null ? group.Center : base.WanderCenter;

    // Empties the list when play starts, also when the editor skips the domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetList()
    {
        Lost = null;
        active.Clear();
    }

    // Puts the squirrel in a group. Call it before the squirrel is first enabled.
    public void SetGroup(SquirrelGroup value)
    {
        group = value;
    }

    protected override void Awake()
    {
        base.Awake();
        fear = GetComponent<FearSource>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        fear.Charged += OnFearCharged;
        active.Add(this);

        if (group != null)
            group.Add(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        fear.Charged -= OnFearCharged;
        active.Remove(this);

        if (group != null)
            group.Remove(this);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        lionSightRange = Mathf.Max(0f, lionSightRange);
        corpseSeconds = Mathf.Max(0f, corpseSeconds);
    }

    public override bool CanInteract(Player player)
    {
        PersonalityType current = player.Personality.Current;

        if (!Health.IsDead && current == PersonalityType.C)
        {
            prompt = catchPrompt;
            return true;
        }

        if (Health.IsDead && !eaten && current == PersonalityType.A)
        {
            prompt = cookPrompt;
            return true;
        }

        return false;
    }

    // A living squirrel is caught, a dead one is cooked. Either way it is gone afterwards.
    public override void Interact(Player player)
    {
        if (!CanInteract(player))
            return;

        player.Add(Health.IsDead ? ItemType.CookedSquirrel : ItemType.CaughtSquirrel);
        player.RegisterCountedAction();
        // A caught squirrel leaves the world alive, so it counts as lost. A dead one already did.
        if (!Health.IsDead)
            Lost?.Invoke(this);
        Destroy(gameObject);
    }

    protected override Attitude AttitudeFor(PersonalityType personality)
    {
        switch (personality)
        {
            case PersonalityType.A:
                return Attitude.Flee;
            case PersonalityType.B:
                return hostile ? Attitude.Hostile : Attitude.Neutral;
            default:
                return Attitude.Neutral;
        }
    }

    // The nearest living lion within lionSightRange.
    protected override Transform FindThreat()
    {
        Lion nearest = null;
        float bestSqr = lionSightRange * lionSightRange;
        IReadOnlyList<Lion> lions = Lion.Active;

        for (int i = 0; i < lions.Count; i++)
        {
            if (lions[i].Health.IsDead)
                continue;

            float sqr = (lions[i].transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = lions[i];
            }
        }

        return nearest != null ? nearest.transform : null;
    }

    protected override void OnStartedFleeing(Transform from)
    {
        if (group != null)
            group.Alarm(from, this);
    }

    protected override void OnShifted(PersonalityType previous, PersonalityType current)
    {
        hostile = false;
    }

    protected override void OnDied()
    {
        hostile = false;
        fear.enabled = false;
        Lost?.Invoke(this);
        Destroy(gameObject, corpseSeconds);
    }

    // A lion ate this squirrel, so nothing is left to cook.
    public void MarkEaten()
    {
        eaten = true;
    }

    private void OnFearCharged()
    {
        if (!Health.IsDead)
            hostile = true;
    }
}