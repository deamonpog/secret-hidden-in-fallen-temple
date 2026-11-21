// Something the player can act on with the interact button.
public interface IInteractable
{
    // Text to show the player while this is the interaction target.
    string PromptText { get; }

    // True when this player can interact with it right now.
    bool CanInteract(Player player);

    // Performs the interaction. Only called when CanInteract is true.
    void Interact(Player player);
}
