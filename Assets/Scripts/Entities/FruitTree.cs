using UnityEngine;

// A tree that gives fruit once. Only personality C can take it; A and B cannot.
// Needs a collider so the interaction sensor can find it.
[RequireComponent(typeof(Collider))]
public class FruitTree : MonoBehaviour, IInteractable
{
    [SerializeField] private int fruitAmount = 1;

    // Optional object, such as the fruit on the branches, hidden once the tree is used.
    [SerializeField] private GameObject fruitVisual;

    private bool used;

    public string PromptText => "Take fruit";

    public bool CanInteract(PlayerCharacter player)
    {
        return !used && player.Personality.Current == PersonalityType.C;
    }

    public void Interact(PlayerCharacter player)
    {
        used = true;
        if (fruitVisual != null)
            fruitVisual.SetActive(false);

        player.Inventory.Items.Add(ItemType.Fruit, fruitAmount);
        player.RegisterCountedAction();
    }
}
