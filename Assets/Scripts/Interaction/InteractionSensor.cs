using UnityEngine;

// Finds the nearest interactable in range and triggers it when the interact button is pressed.
[RequireComponent(typeof(PlayerCharacter))]
[RequireComponent(typeof(PlayerInputReader))]
public class InteractionSensor : MonoBehaviour
{
    [SerializeField] private float range = 2f;
    [SerializeField] private LayerMask layers = ~0;

    private readonly Collider[] hits = new Collider[32];

    private PlayerCharacter player;
    private PlayerInputReader input;

    // The interactable that would be triggered now, or null when none is in range.
    public IInteractable Current { get; private set; }

    public string CurrentPrompt => Current != null ? Current.PromptText : string.Empty;

    private void Awake()
    {
        player = GetComponent<PlayerCharacter>();
        input = GetComponent<PlayerInputReader>();
    }

    private void OnEnable()
    {
        input.InteractPressed += HandleInteractPressed;
    }

    private void OnDisable()
    {
        input.InteractPressed -= HandleInteractPressed;
    }

    private void Update()
    {
        Current = FindNearest();
    }

    private void HandleInteractPressed()
    {
        Current = FindNearest();
        if (Current != null)
            Current.Interact(player);
    }

    // Returns the closest interactable that currently accepts this player, or null.
    private IInteractable FindNearest()
    {
        Vector3 origin = transform.position;
        int count = Physics.OverlapSphereNonAlloc(
            origin, range, hits, layers, QueryTriggerInteraction.Collide);

        IInteractable best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            IInteractable candidate = hits[i].GetComponentInParent<IInteractable>();
            if (candidate == null || !candidate.CanInteract(player))
                continue;

            float distance = (hits[i].ClosestPoint(origin) - origin).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }
}
