using System;
using UnityEngine;

// Owns the player's current personality and shifts it as counted actions accumulate.
// Gameplay code calls RegisterCountedAction once for every counted action performed.
[DefaultExecutionOrder(-100)]
public class PersonalityController : MonoBehaviour
{
    [SerializeField] private PersonalityType startingPersonality = PersonalityType.C;

    private PersonalityCycle cycle;

    public PersonalityType Current => cycle.Current;
    public PersonalityType? Upcoming => cycle.Upcoming;
    public int ActionsInCycle => cycle.ActionsInCycle;

    public event Action<PersonalityType> UpcomingRevealed;
    public event Action<PersonalityType, PersonalityType> Shifted;

    private void Awake()
    {
        cycle = new PersonalityCycle(startingPersonality, new System.Random());
        cycle.UpcomingRevealed += next => UpcomingRevealed?.Invoke(next);
        cycle.Shifted += (previous, current) => Shifted?.Invoke(previous, current);
    }

    public void RegisterCountedAction()
    {
        cycle.RegisterAction();
    }
}
