using UnityEngine;

/// <summary>
/// A bush the player can hide in. Every personality can use it. The hiding ends when the
/// player moves.
/// </summary>
public class Bush : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Hide in the bush";

    public string PromptText => prompt;

    private void Awake()
    {
        if (GetComponentInChildren<Collider>() == null)
            Debug.LogWarning("Bush has no collider on it or its children, " +
                             "so it cannot be interacted with.", this);
    }

    public bool CanInteract(Player player)
    {
        return !player.Hidden && !player.Climbing;
    }

    public void Interact(Player player)
    {
        player.Hide(transform);
        player.RegisterCountedAction();
    }
}