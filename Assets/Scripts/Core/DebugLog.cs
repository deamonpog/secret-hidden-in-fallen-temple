using UnityEngine;

/// <summary>
/// Logs personality, health and inventory events to the console and offers test
/// actions in the component menu. For testing only; it is deleted when the HUD is built.
/// </summary>
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PersonalityController))]
public class DebugLog : MonoBehaviour
{
    // Mirror the personality state so it can be watched in the inspector.
    // Editing these fields has no effect, since they are overwritten every frame.
    [SerializeField] private PersonalityType currentPersonality;
    [SerializeField] private int actionsInCycle;
    [SerializeField] private string upcomingPersonality;

    private Player player;
    private Health health;
    private PersonalityController personality;

    private void Awake()
    {
        // Each component is fetched directly. Player.Health is not used here because
        // Player may not have run its own Awake yet.
        player = GetComponent<Player>();
        health = GetComponent<Health>();
        personality = GetComponent<PersonalityController>();
    }

    private void OnEnable()
    {
        personality.UpcomingRevealed += LogUpcoming;
        personality.Shifted += LogShift;
        personality.ActionCounted += LogActionCounted;
        health.Changed += LogHealthChanged;
        health.Died += LogDied;
        player.InventoryChanged += LogItemChanged;
    }

    private void OnDisable()
    {
        personality.UpcomingRevealed -= LogUpcoming;
        personality.Shifted -= LogShift;
        personality.ActionCounted -= LogActionCounted;
        health.Changed -= LogHealthChanged;
        health.Died -= LogDied;
        player.InventoryChanged -= LogItemChanged;
    }

    private void Update()
    {
        currentPersonality = personality.Current;
        actionsInCycle = personality.ActionsInCycle;

        PersonalityType? upcoming = personality.Upcoming;
        upcomingPersonality = upcoming.HasValue ? upcoming.Value.ToString() : "hidden";
    }

    private void LogUpcoming(PersonalityType next)
    {
        Debug.Log($"Next personality revealed: {next}");
    }

    private void LogShift(PersonalityType previous, PersonalityType current)
    {
        Debug.Log($"Personality shifted: {previous} -> {current}");
    }

    private void LogActionCounted()
    {
        Debug.Log($"Counted action. Personality {personality.Current}, " +
                  $"actions in cycle {personality.ActionsInCycle}");
    }

    private void LogHealthChanged(float current)
    {
        Debug.Log($"Health: {current} / {health.Max}");
    }

    private void LogDied()
    {
        Debug.Log("Player died");
    }

    private void LogItemChanged(ItemType item, int count)
    {
        Debug.Log($"Inventory: {item} = {count}");
    }

    [ContextMenu("Register counted action")]
    private void RegisterTestAction()
    {
        personality.RegisterCountedAction();
    }

    [ContextMenu("Damage 30")]
    private void DamageTest()
    {
        health.Damage(30f);
    }

    [ContextMenu("Heal 30")]
    private void HealTest()
    {
        health.Heal(30f);
    }

    [ContextMenu("Add cooked squirrel")]
    private void AddCookedSquirrelTest()
    {
        player.Add(ItemType.CookedSquirrel);
    }
}