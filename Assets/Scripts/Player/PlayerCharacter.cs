using UnityEngine;

// Groups the components that make up the player, so interactables and systems
// reach health, inventory, movement and personality through one reference.
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerCharacter : MonoBehaviour
{
    // Optional. When empty, the controller on this object or else any in the scene is used.
    [SerializeField] private PersonalityController personality;

    public Health Health { get; private set; }
    public PlayerInventory Inventory { get; private set; }
    public PlayerMovement Movement { get; private set; }
    public PersonalityController Personality => personality;

    private void Awake()
    {
        Health = GetComponent<Health>();
        Inventory = GetComponent<PlayerInventory>();
        Movement = GetComponent<PlayerMovement>();

        if (personality == null)
            personality = GetComponent<PersonalityController>();

        if (personality == null)
            personality = FindFirstObjectByType<PersonalityController>();

        if (personality == null)
            Debug.LogError("PlayerCharacter found no PersonalityController.", this);
    }

    // Reports one successful action to the personality cycle.
    public void RegisterCountedAction()
    {
        personality.RegisterCountedAction();
    }
}
