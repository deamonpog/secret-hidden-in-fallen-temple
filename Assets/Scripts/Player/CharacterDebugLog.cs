using UnityEngine;

// Logs inventory and health changes to the console. For testing only;
// remove it once a HUD exists.
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerInventory))]
public class CharacterDebugLog : MonoBehaviour
{
    private PlayerHealth health;
    private PlayerInventory inventory;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        inventory = GetComponent<PlayerInventory>();
    }

    private void OnEnable()
    {
        inventory.Items.Changed += LogItemChanged;
        health.Model.Changed += LogHealthChanged;
        health.Model.Died += LogDied;
    }

    private void OnDisable()
    {
        inventory.Items.Changed -= LogItemChanged;
        health.Model.Changed -= LogHealthChanged;
        health.Model.Died -= LogDied;
    }

    private void LogItemChanged(ItemType item, int count)
    {
        Debug.Log($"Inventory: {item} = {count}");
    }

    private void LogHealthChanged(float current)
    {
        Debug.Log($"Health: {current} / {health.Model.Max}");
    }

    private void LogDied()
    {
        Debug.Log("Player died");
    }
}
