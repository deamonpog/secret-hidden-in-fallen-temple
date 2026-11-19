using System;
using System.Collections.Generic;

// Holds how many of each item the player carries.
public class Inventory
{
    private readonly Dictionary<ItemType, int> item_counts = new Dictionary<ItemType, int>();

    // Raised after a count changes, with the item and its new count.
    public event Action<ItemType, int> Changed;

    public int Count(ItemType item)
    {
        return item_counts.TryGetValue(item, out int count) ? count : 0;
    }

    public void Add(ItemType item, int amount = 1)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");

        int updated = Count(item) + amount;
        item_counts[item] = updated;
        Changed?.Invoke(item, updated);
    }

    // Removes the amount and returns true, or returns false and changes nothing when we have lower than the requested amount.
    public bool TryRemove(ItemType item, int amount = 1)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");

        int current = Count(item);
        if (current < amount)
            return false;

        int updated = current - amount;
        item_counts[item] = updated;
        Changed?.Invoke(item, updated);
        return true;
    }
}
