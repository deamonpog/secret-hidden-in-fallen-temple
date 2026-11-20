using UnityEngine;

// Loose fruit lying on the ground. Any personality can pick it up.
// Needs a collider so the interaction sensor can find it.
[RequireComponent(typeof(Collider))]
public class FruitPickup : MonoBehaviour, IInteractable
{
    private bool collected;

    public string PromptText => "Pick up fruit";

    public bool CanInteract(PlayerCharacter player)
    {
        return !collected;
    }

    public void Interact(PlayerCharacter player)
    {
        collected = true;
        player.Inventory.Items.Add(ItemType.Fruit);
        player.RegisterCountedAction();
        Destroy(gameObject);
    }
}
