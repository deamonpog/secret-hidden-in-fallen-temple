using System;

// Tracks current and maximum health and raises events when it changes.
// Plain C# with no Unity types.
public class HealthModel
{
    public float Max { get; }
    public float Current { get; private set; }
    public bool IsDead => Current <= 0f;

    // Raised after health changes, with the new current value.
    public event Action<float> Changed;

    // Raised once, when health reaches zero.
    public event Action Died;

    public HealthModel(float max)
    {
        if (max <= 0f)
            throw new ArgumentOutOfRangeException(nameof(max), "Max health must be positive.");

        Max = max;
        Current = max;
    }

    // Adds health up to Max and returns the amount actually gained.
    // A dead model cannot be healed. Healing at full health gains nothing and raises no event.
    public float Heal(float amount)
    {
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must not be negative.");

        if (IsDead)
            return 0f;

        float gained = Math.Min(amount, Max - Current);
        if (gained <= 0f)
            return 0f;

        Current += gained;
        Changed?.Invoke(Current);
        return gained;
    }

    // Removes health down to zero and returns the amount actually lost.
    public float Damage(float amount)
    {
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must not be negative.");

        if (IsDead)
            return 0f;

        float lost = Math.Min(amount, Current);
        if (lost <= 0f)
            return 0f;

        Current -= lost;
        Changed?.Invoke(Current);

        if (IsDead)
            Died?.Invoke();

        return lost;
    }
}
