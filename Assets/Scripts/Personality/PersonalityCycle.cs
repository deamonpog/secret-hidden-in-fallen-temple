using System;

// Counts actions and decides when, and to which personality, the player shifts.
// A shift happens on every ActionsPerShift-th action. The next personality is chosen
// at the start of each cycle and is revealed once RevealAtAction actions have happened.
public class PersonalityCycle
{
    public const int ActionsPerShift = 5;
    public const int RevealAtAction = 3;

    private readonly Random random;
    private PersonalityType upcoming;

    public PersonalityType Current { get; private set; }
    public int ActionsInCycle { get; private set; }

    // The next personality, or null while it is still hidden from the player.
    public PersonalityType? Upcoming =>
        ActionsInCycle >= RevealAtAction ? upcoming : (PersonalityType?)null;

    // Raised once per cycle, with the personality the player will shift into.
    public event Action<PersonalityType> UpcomingRevealed;

    // Raised after a shift, with the previous and the new personality.
    public event Action<PersonalityType, PersonalityType> Shifted;

    public PersonalityCycle(PersonalityType start, Random random)
    {
        this.random = random;
        Current = start;
        upcoming = RollUpcoming();
    }

    // Records one counted action and shifts personality when the cycle is complete.
    public void RegisterAction()
    {
        ActionsInCycle++;

        if (ActionsInCycle == RevealAtAction)
            UpcomingRevealed?.Invoke(upcoming);

        if (ActionsInCycle >= ActionsPerShift)
            Shift();
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
