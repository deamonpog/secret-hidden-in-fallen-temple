using UnityEngine;

/// <summary>
/// Logs personality, health, inventory and ability events to the console and offers test
/// actions in the component menu. For testing only; it is deleted when the HUD is built.
/// </summary>
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PersonalityController))]
[RequireComponent(typeof(Abilities))]
public class DebugLog : MonoBehaviour
{
    // Mirror the state so it can be watched in the inspector.
    // Editing these fields has no effect, since they are overwritten every frame.
    [Header("Personality")]
    [SerializeField] private PersonalityType currentPersonality;
    [SerializeField] private int actionsInCycle;
    [SerializeField] private string upcomingPersonality;
    [SerializeField] private string profileName;

    [Header("Stats")]
    [SerializeField] private float speed;
    [SerializeField] private float vision;

    [Header("Abilities")]
    [SerializeField] private string ability1;
    [SerializeField] private string ability2;
    [SerializeField] private float cooldown1;
    [SerializeField] private float cooldown2;
    [SerializeField] private bool sharpened;
    [SerializeField] private bool invisible;
    [SerializeField] private bool sensing;

    [Header("Game")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private string gameState;
    [SerializeField] private int seed;
    [SerializeField] private int stars;
    [SerializeField] private float timeScale;

    private Player player;
    private Health health;
    private PersonalityController personality;
    private Abilities abilities;

    private void Awake()
    {
        // Each component is fetched directly. Player.Health is not used here because
        // Player may not have run its own Awake yet.
        player = GetComponent<Player>();
        health = GetComponent<Health>();
        personality = GetComponent<PersonalityController>();
        abilities = GetComponent<Abilities>();
    }

    private void OnEnable()
    {
        if (gameManager != null) gameManager.StateChanged += LogState;

        personality.UpcomingRevealed += LogUpcoming;
        personality.Shifted += LogShift;
        personality.ActionCounted += LogActionCounted;
        health.Changed += LogHealthChanged;
        health.Died += LogDied;
        player.InventoryChanged += LogItemChanged;
        abilities.AttackPerformed += LogAttack;
    }

    private void OnDisable()
    {
        if (gameManager != null) gameManager.StateChanged -= LogState;

        personality.UpcomingRevealed -= LogUpcoming;
        personality.Shifted -= LogShift;
        personality.ActionCounted -= LogActionCounted;
        health.Changed -= LogHealthChanged;
        health.Died -= LogDied;
        player.InventoryChanged -= LogItemChanged;
        abilities.AttackPerformed -= LogAttack;
    }

    private void LogState(GameState state)
    {
        Debug.Log($"Game state: {state}");
    }

    private void Update()
    {
        currentPersonality = personality.Current;
        actionsInCycle = personality.ActionsInCycle;

        PersonalityType? upcoming = personality.Upcoming;
        upcomingPersonality = upcoming.HasValue ? upcoming.Value.ToString() : "hidden";

        PersonalityProfile profile = personality.CurrentProfile;
        profileName = profile != null ? profile.DisplayName : "none";
        ability1 = profile != null ? profile.Ability1.ToString() : "none";
        ability2 = profile != null ? profile.Ability2.ToString() : "none";

        speed = player.Speed.Value;
        vision = player.Vision.Value;

        cooldown1 = abilities.CooldownLeft(0);
        cooldown2 = abilities.CooldownLeft(1);
        sharpened = abilities.IsSharpened;
        invisible = abilities.IsInvisible;
        sensing = abilities.IsSensing;

        timeScale = Time.timeScale;
        if (gameManager != null)
        {
            gameState = gameManager.State.ToString();
            seed = gameManager.Seed;
            stars = gameManager.Stars;
        }
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

    private void LogAttack(Vector3 origin, float damageDealt)
    {
        Debug.Log($"Attack hit, damage dealt: {damageDealt}");
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

    [ContextMenu("Sharpen blade")]
    private void SharpenTest()
    {
        abilities.Sharpen();
    }

    [ContextMenu("Run: complete")]
    private void CompleteTest()
    {
        gameManager.Complete();
    }

    [ContextMenu("Run: add star")]
    private void AddStarTest()
    {
        gameManager.AddStar();
    }

    [ContextMenu("Run: new run")]
    private void NewRunTest()
    {
        gameManager.NewRun();
    }
}