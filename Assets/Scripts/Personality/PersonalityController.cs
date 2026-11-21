using System;
using UnityEngine;

/// <summary>
/// Owns the player's current personality and shifts it as counted actions accumulate. 
/// Gameplay code calls RegisterCountedAction once for every counted action performed. 
/// A shift happens on every ActionsPerShift-th action. The next personality is chosen at the start of each cycle and is revealed once RevealAtAction actions have happened.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PersonalityController : MonoBehaviour
{
    // The number of counted actions that must happen before the player shifts to a new personality.
    public const int ActionsPerShift = 5;

    // The number of counted actions that must happen before the next personality is revealed to the player.
    public const int RevealAtAction = 3;

    [SerializeField] private PersonalityType startingPersonality = PersonalityType.C;

    private System.Random random;

    // The next personality.
    private PersonalityType upcoming;

    public PersonalityType Current { get; private set; }

    // The number of counted actions that have happened in the current cycle.
    public int ActionsInCycle { get; private set; }

    // The next personality, or null while it is still hidden from the player. This can be null because its a property, not a field.
    public PersonalityType? Upcoming => ActionsInCycle >= RevealAtAction ? upcoming : (PersonalityType?)null;

    // Raised once per cycle, with the personality the player will shift into.
    public event Action<PersonalityType> UpcomingRevealed;

    // Raised after every counted action has been registered.
    public event Action ActionCounted;

    // Raised after a shift, with the previous and the new personality.
    public event Action<PersonalityType, PersonalityType> Shifted;

    private void Awake()
    {
        random = new System.Random();
        Current = startingPersonality;
        upcoming = RollUpcoming();
    }

    // Records one counted action and shifts personality when the cycle is complete.
    public void RegisterCountedAction()
    {
        ActionsInCycle++;

        if (ActionsInCycle == RevealAtAction)
            UpcomingRevealed?.Invoke(upcoming);

        if (ActionsInCycle >= ActionsPerShift)
            Shift();

        ActionCounted?.Invoke();
    }

    private void Shift()
    {
        PersonalityType previous = Current;
        Current = upcoming;
        ActionsInCycle = 0;
        upcoming = RollUpcoming();
        Shifted?.Invoke(previous, Current);
    }

    // Picks any personality other than the current one, each with equal chance.
    private PersonalityType RollUpcoming()
    {
        int count = Enum.GetValues(typeof(PersonalityType)).Length;
        int offset = random.Next(1, count);
        return (PersonalityType)(((int)Current + offset) % count);
    }
}