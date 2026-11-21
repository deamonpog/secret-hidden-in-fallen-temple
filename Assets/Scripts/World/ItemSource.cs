using System;
using UnityEngine;

/// <summary>
/// An object that gives the player an item once: loose fruit, a flower plant, or the fruit
/// on a tree. Which personalities may use it, and what happens to it afterwards, are set in
/// the inspector. A collider must exist on this object or on a child so the interaction
/// sensor can find it.
/// </summary>
public class ItemSource : MonoBehaviour, IInteractable
{
    [SerializeField] private ItemType item = ItemType.Fruit;
    [SerializeField] private int amount = 1;

    // The personalities that may use this. An empty list means nobody can.
    [SerializeField]
    private PersonalityType[] allowedPersonalities =
        { PersonalityType.A, PersonalityType.B, PersonalityType.C };

    [SerializeField] private UseMode onUse = UseMode.Destroy;

    // Hidden after use when onUse is HideVisual, such as the fruit on the branches.
    [SerializeField] private GameObject visual;

    [SerializeField] private string prompt = "Pick up";

    private bool used;

    public string PromptText => prompt;

    private void Awake()
    {
        if (GetComponentInChildren<Collider>() == null)
            Debug.LogWarning("ItemSource has no collider on it or its children, " +
                             "so it cannot be interacted with.", this);
    }

    private void OnValidate()
    {
        amount = Mathf.Max(1, amount);
    }

    public bool CanInteract(Player player)
    {
        return !used && Array.IndexOf(allowedPersonalities, player.Personality.Current) >= 0;
    }

    public void Interact(Player player)
    {
        used = true;
        player.Add(item, amount);
        player.RegisterCountedAction();

        switch (onUse)
        {
            case UseMode.Destroy:
                Destroy(gameObject);
                break;
            case UseMode.HideVisual:
                if (visual != null)
                    visual.SetActive(false);
                break;
        }
    }
}