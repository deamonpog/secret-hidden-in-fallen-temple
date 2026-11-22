using System;
using UnityEngine;

/// <summary>
/// The one handle gameplay code gets for the player. It holds the item counts, consumes
/// items from the three consume slots, owns the speed and vision stats, forwards counted
/// actions to the personality cycle, and gives access to the player's other components.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerInputReader))]
public class Player : MonoBehaviour
{
    // Optional. When empty, the controller on this object is used.
    [SerializeField] private PersonalityController personality;

    [Header("Stats")]
    // Move speed in units per second before any multiplier.
    [SerializeField] private float baseSpeed = 5f;

    [Header("Consuming")]
    // Each consume button is tied to one item. An item that cannot be consumed leaves its slot unused.
    [SerializeField]
    private ItemType[] slotItems =
        { ItemType.Fruit, ItemType.CookedSquirrel, ItemType.Flower };
    [SerializeField] private float fruitHeal = 20f;
    [SerializeField] private float cookedSquirrelHeal = 40f;
    [SerializeField] private float boostMultiplier = 1.5f;
    [SerializeField] private float boostDuration = 3f;

    // One count per ItemType, indexed by the enum value.
    private readonly int[] counts = new int[Enum.GetValues(typeof(ItemType)).Length];

    // Keys that identify who set a multiplier, so each source can replace or remove its own.
    private readonly object personalityKey = new object();
    private readonly object boostKey = new object();

    private PlayerInputReader input;

    public Health Health { get; private set; }
    public PlayerMovement Movement { get; private set; }
    public PersonalityController Personality => personality;

    // Move speed in units per second, and vision radius in world units.
    public Stat Speed { get; private set; }
    public Stat Vision { get; private set; }

    // Raised after an item count changes, with the item and its new count.
    public event Action<ItemType, int> InventoryChanged;

    // Raised after an item was consumed, with the item.
    public event Action<ItemType> Consumed;

    private void Awake()
    {
        Health = GetComponent<Health>();
        Movement = GetComponent<PlayerMovement>();
        input = GetComponent<PlayerInputReader>();

        Speed = new Stat(baseSpeed);
        Vision = new Stat(0f);

        if (personality == null)
            personality = GetComponent<PersonalityController>();

        if (personality == null)
        {
            Debug.LogError("Player found no PersonalityController.", this);
            return;
        }

        // PersonalityController runs at execution order -100, so its Awake has already
        // run and its profiles are ready.
        ApplyProfile(personality.CurrentProfile);
    }

    private void OnEnable()
    {
        input.Consume1Pressed += ConsumeSlot1;
        input.Consume2Pressed += ConsumeSlot2;
        input.Consume3Pressed += ConsumeSlot3;

        if (personality != null)
            personality.Shifted += OnShifted;
    }

    private void OnDisable()
    {
        input.Consume1Pressed -= ConsumeSlot1;
        input.Consume2Pressed -= ConsumeSlot2;
        input.Consume3Pressed -= ConsumeSlot3;

        if (personality != null)
            personality.Shifted -= OnShifted;
    }

    private void Update()
    {
        Speed.Tick(Time.deltaTime);
        Vision.Tick(Time.deltaTime);
    }

    // Keeps the tuning values in a sensible range when they are edited in the inspector.
    private void OnValidate()
    {
        baseSpeed = Mathf.Max(0f, baseSpeed);
        fruitHeal = Mathf.Max(0f, fruitHeal);
        cookedSquirrelHeal = Mathf.Max(0f, cookedSquirrelHeal);
        boostMultiplier = Mathf.Max(0f, boostMultiplier);
        boostDuration = Mathf.Max(0f, boostDuration);
    }

    // Returns how many of the item the player carries.
    public int Count(ItemType item)
    {
        return counts[(int)item];
    }

    public void Add(ItemType item, int amount = 1)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");

        counts[(int)item] += amount;
        InventoryChanged?.Invoke(item, counts[(int)item]);
    }

    // Removes the amount and returns true, or returns false and changes nothing
    // when the player carries fewer than the requested amount.
    public bool TryRemove(ItemType item, int amount = 1)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");

        if (counts[(int)item] < amount)
            return false;

        counts[(int)item] -= amount;
        InventoryChanged?.Invoke(item, counts[(int)item]);
        return true;
    }

    // Uses the item tied to the slot (0 to 2). Does nothing when the slot is empty,
    // the item cannot be consumed, or the player carries none of it.
    public void Consume(int slot)
    {
        if (slot < 0 || slot >= slotItems.Length)
            return;

        ItemType item = slotItems[slot];

        float heal;
        bool givesBoost;
        switch (item)
        {
            case ItemType.Fruit:
                heal = fruitHeal;
                givesBoost = true;
                break;
            case ItemType.CookedSquirrel:
                heal = cookedSquirrelHeal;
                givesBoost = false;
                break;
            default:
                return;
        }

        if (!TryRemove(item))
            return;

        Health.Heal(heal);

        // A second boost while one is running restarts the timer and does not stack.
        if (givesBoost)
            Speed.AddTimed(boostKey, boostMultiplier, boostDuration);

        Consumed?.Invoke(item);
        RegisterCountedAction();
    }

    // Reports one successful action to the personality cycle.
    public void RegisterCountedAction()
    {
        personality.RegisterCountedAction();
    }

    private void ConsumeSlot1() => Consume(0);
    private void ConsumeSlot2() => Consume(1);
    private void ConsumeSlot3() => Consume(2);

    private void OnShifted(PersonalityType previous, PersonalityType current)
    {
        ApplyProfile(personality.CurrentProfile);
    }

    // Sets the stats that depend on the personality. Other multipliers, such as a running
    // fruit boost, stay in effect across a shift.
    private void ApplyProfile(PersonalityProfile profile)
    {
        if (profile == null)
            return;

        Speed.SetModifier(personalityKey, profile.SpeedMultiplier);
        Vision.Base = profile.BaseVisionRadius;
    }
}