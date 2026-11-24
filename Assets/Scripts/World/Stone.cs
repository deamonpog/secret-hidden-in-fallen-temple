using UnityEngine;

/// <summary>
/// A stone with a different use for each personality. A sharpens the blade on any stone.
/// B hides behind a large stone. C climbs a large stone for a vision boost, then climbs
/// down on a movement press or after a time limit.
/// </summary>
public class Stone : MonoBehaviour, IInteractable
{
    [SerializeField] private bool isLarge;

    // Where the player stands on top of a large stone.
    [SerializeField] private Transform topPoint;

    // Seconds before a movement press climbs down, and seconds before it happens by itself.
    [SerializeField] private float climbMinSeconds = 1f;
    [SerializeField] private float climbMaxSeconds = 8f;

    [SerializeField] private float climbVisionMultiplier = 1.25f;

    // Movement input length that counts as a press to climb down.
    [SerializeField] private float moveThreshold = 0.1f;

    // Identifies this stone's vision multiplier so it can be removed again.
    private readonly object visionKey = new object();

    // The climbing player, or null when nobody is on this stone.
    private Player climber;
    private float climbTime;
    private Vector3 returnPoint;

    // The personality seen by the last CanInteract call. The prompt depends on it, and
    // the interface gives PromptText no player.
    private PersonalityType promptPersonality;

    public string PromptText
    {
        get
        {
            switch (promptPersonality)
            {
                case PersonalityType.A: return "Sharpen blade";
                case PersonalityType.B: return "Hide behind the stone";
                case PersonalityType.C: return "Climb the stone";
                default: return string.Empty;
            }
        }
    }

    private void Awake()
    {
        if (GetComponentInChildren<Collider>() == null)
            Debug.LogWarning("Stone has no collider on it or its children, " +
                             "so it cannot be interacted with.", this);

        if (isLarge && topPoint == null)
        {
            Debug.LogError("A large Stone needs a top point. Treating it as small.", this);
            isLarge = false;
        }
    }

    private void OnDisable()
    {
        // Leaves no player stuck in the climbing state when the stone goes away.
        if (climber != null)
            FinishClimb();
    }

    private void OnValidate()
    {
        climbMinSeconds = Mathf.Max(0f, climbMinSeconds);
        climbMaxSeconds = Mathf.Max(climbMinSeconds, climbMaxSeconds);
        climbVisionMultiplier = Mathf.Max(0f, climbVisionMultiplier);
        moveThreshold = Mathf.Clamp01(moveThreshold);
    }

    // Ends the climb on a movement press or when the time limit runs out.
    private void Update()
    {
        if (climber == null || !climber.InputReader.GameplayEnabled)
            return;

        climbTime += Time.deltaTime;

        bool pressed = climbTime >= climbMinSeconds
            && climber.InputReader.Move.sqrMagnitude > moveThreshold * moveThreshold;

        if (pressed || climbTime >= climbMaxSeconds)
            ClimbDown();
    }

    public bool CanInteract(Player player)
    {
        promptPersonality = player.Personality.Current;

        switch (promptPersonality)
        {
            case PersonalityType.A:
                return player.Abilities != null && player.Abilities.CanSharpen;
            case PersonalityType.B:
                return isLarge && !player.Hidden && !player.Climbing;
            case PersonalityType.C:
                return isLarge && !player.Climbing;
            default:
                return false;
        }
    }

    public void Interact(Player player)
    {
        switch (player.Personality.Current)
        {
            case PersonalityType.A:
                player.Abilities.Sharpen();
                break;
            case PersonalityType.B:
                player.Hide(transform);
                break;
            case PersonalityType.C:
                StartClimb(player);
                break;
            default:
                return;
        }

        player.RegisterCountedAction();
    }

    // Stores where the player stood, moves the player to the top and applies the boost.
    private void StartClimb(Player player)
    {
        climber = player;
        climbTime = 0f;
        returnPoint = player.transform.position;

        player.Movement.TeleportTo(topPoint.position);
        player.Climbing = true;
        player.Vision.SetModifier(visionKey, climbVisionMultiplier);
    }

    // Puts the player back where the climb began and ends the climb.
    private void ClimbDown()
    {
        climber.Movement.TeleportTo(returnPoint);
        FinishClimb();
    }

    // Clears the climbing state and the vision multiplier.
    private void FinishClimb()
    {
        climber.Climbing = false;
        climber.Vision.RemoveModifier(visionKey);
        climber = null;
    }
}