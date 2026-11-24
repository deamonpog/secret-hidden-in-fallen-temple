using System;
using UnityEngine;

/// <summary>
/// Tracks current and maximum health and raises events when it changes.
/// Attach it to anything that can be hurt or healed. 
/// It runs early so other components can read Current during their own Awake.
/// </summary>
[DefaultExecutionOrder(-90)]
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    public float Max => maxHealth;
    public float Current { get; private set; }
    public bool IsDead => Current <= 0f;

    // Raised after health changes, with the new current value.
    public event Action<float> Changed;

    // Raised once, when health reaches zero.
    public event Action Died;

    private void Awake()
    {
        Current = maxHealth;
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth); // Keeps the inspector value usable, since a maximum of zero or less has no meaning.
    }

    // Adds health up to Max and returns the amount actually gained.
    // A dead object cannot be healed. Healing at full health gains nothing and raises no event.
    public float Heal(float amount)
    {
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must not be negative.");

        if (IsDead)
            return 0f;

        float gained = Mathf.Min(amount, Max - Current);
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

        float lost = Mathf.Min(amount, Current);
        if (lost <= 0f)
            return 0f;

        Current -= lost;
        Changed?.Invoke(Current);

        if (IsDead)
            Died?.Invoke();

        return lost;
    }
}