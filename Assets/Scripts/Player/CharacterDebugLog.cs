using UnityEngine;

/// <summary>
/// Logs inventory and health changes to the console, and lets health be changed from the
/// component menu. For testing only; remove it once a HUD exists.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PlayerInventory))]
public class CharacterDebugLog : MonoBehaviour
{
    private Health health;
    private PlayerInventory inventory;

    private void Awake()
    {
        health = GetComponent<Health>();
        inventory = GetComponent<PlayerInventory>();
    }

    private void OnEnable()
    {
        inventory.Items.Changed += LogItemChanged;
        health.Changed += LogHealthChanged;
        health.Died += LogDied;
    }

    private void OnDisable()
    {
        inventory.Items.Changed -= LogItemChanged;
        health.Changed -= LogHealthChanged;
        health.Died -= LogDied;
    }

    private void LogItemChanged(ItemType item, int count)
    {
        Debug.Log($"Inventory: {item} = {count}");
    }

    private void LogHealthChanged(float current)
    {
        Debug.Log($"Health: {current} / {health.Max}");
    }

    private void LogDied()
    {
        Debug.Log("Player died");
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
}