using System;
using System.Collections.Generic;

// A number with a base value and multipliers from several sources, such as personality,
// items, abilities and shrines. Each multiplier has a key, so the source that added it can
// replace or remove it later. A multiplier is either permanent or lasts a number of seconds.
public class Stat
{
    private class Modifier
    {
        public object Key;
        public float Multiplier;
        public float Remaining;
    }

    private readonly List<Modifier> modifiers = new List<Modifier>();

    public Stat(float baseValue)
    {
        Base = baseValue;
    }

    public float Base { get; set; }

    // The base value times every multiplier currently in effect.
    public float Value
    {
        get
        {
            float value = Base;
            for (int i = 0; i < modifiers.Count; i++)
                value *= modifiers[i].Multiplier;
            return value;
        }
    }

    // Adds a permanent multiplier, or replaces the one already stored under the key.
    public void SetModifier(object key, float multiplier)
    {
        Put(key, multiplier, float.PositiveInfinity);
    }

    // Adds a multiplier that ends after the given seconds. Using a key that is already
    // stored replaces that multiplier and restarts its timer, so it never stacks with itself.
    public void AddTimed(object key, float multiplier, float seconds)
    {
        if (seconds <= 0f)
        {
            RemoveModifier(key);
            return;
        }

        Put(key, multiplier, seconds);
    }

    // Removes the multiplier stored under the key. Returns false when there was none.
    public bool RemoveModifier(object key)
    {
        Modifier existing = Find(key);
        return existing != null && modifiers.Remove(existing);
    }

    // Counts down the timed multipliers and drops the ones that have run out.
    public void Tick(float deltaTime)
    {
        // Walk backwards so entries can be removed while looping.
        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            Modifier modifier = modifiers[i];
            if (float.IsPositiveInfinity(modifier.Remaining))
                continue;

            modifier.Remaining -= deltaTime;
            if (modifier.Remaining <= 0f)
                modifiers.RemoveAt(i);
        }
    }

    private void Put(object key, float multiplier, float remaining)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        Modifier modifier = Find(key);
        if (modifier == null)
        {
            modifier = new Modifier { Key = key };
            modifiers.Add(modifier);
        }

        modifier.Multiplier = multiplier;
        modifier.Remaining = remaining;
    }

    private Modifier Find(object key)
    {
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (Equals(modifiers[i].Key, key))
                return modifiers[i];
        }

        return null;
    }
}