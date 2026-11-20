using System;

// Decides which items can be consumed and what each one does.
// Fruit heals and gives a speed boost. Cooked squirrel heals more and gives no boost.
// The heal amounts are passed in so they can be tuned from the Unity side.
public class ConsumeRules
{
    private readonly float fruitHeal;
    private readonly float cookedSquirrelHeal;

    public ConsumeRules(float fruitHeal, float cookedSquirrelHeal)
    {
        if (fruitHeal < 0f)
            throw new ArgumentOutOfRangeException(nameof(fruitHeal), "Heal amount must not be negative.");
        if (cookedSquirrelHeal < 0f)
            throw new ArgumentOutOfRangeException(nameof(cookedSquirrelHeal), "Heal amount must not be negative.");

        this.fruitHeal = fruitHeal;
        this.cookedSquirrelHeal = cookedSquirrelHeal;
    }

    // Returns true and the effect when the item is consumable, otherwise false.
    public bool TryGetEffect(ItemType item, out ConsumeEffect effect)
    {
        switch (item)
        {
            case ItemType.Fruit:
                effect = new ConsumeEffect(fruitHeal, true);
                return true;
            case ItemType.CookedSquirrel:
                effect = new ConsumeEffect(cookedSquirrelHeal, false);
                return true;
            default:
                effect = default;
                return false;
        }
    }
}
