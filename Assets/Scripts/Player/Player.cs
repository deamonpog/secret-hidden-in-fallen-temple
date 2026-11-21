using System;
using UnityEngine;

/// <summary>
/// The one handle gameplay code gets for the player. It holds the item counts,
/// forwards counted actions to the personality cycle, and gives access to the
/// player's other components.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PlayerMovement))]
public class Player : MonoBehaviour
{
    // Optional. When empty, the controller on this object is used.
    [SerializeField] private PersonalityController personality;

    // One count per ItemType, indexed by the enum value.
    private readonly int[] counts = new int[Enum.GetValues(typeof(ItemType)).Length];

    public Health Health { get; private set; }
    public PlayerMovement Movement { get; private set; }
    public PersonalityController Personality => personality;

    // Raised after an item count changes, with the item and its new count.
    public event Action<ItemType, int> InventoryChanged;

    private void Awake()
    {
        Health = GetComponent<Health>();
        Movement = GetComponent<PlayerMovement>();

        if (personality == null)
            personality = GetComponent<PersonalityController>();

        if (personality == null)
            Debug.LogError("Player found no PersonalityController.", this);
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

    // Reports one successful action to the personality cycle.
    public void RegisterCountedAction()
    {
        personality.RegisterCountedAction();
    }
}