using UnityEngine;

// Logs personality events to the console and lets a counted action be fired from the
// component menu. For testing only; remove it once a HUD and real actions exist.
[RequireComponent(typeof(PersonalityController))]
public class PersonalityDebugLog : MonoBehaviour
{
    // Mirror the controller state so it can be watched in the inspector.
    // Editing these fields has no effect, since they are overwritten every frame.
    [SerializeField] private PersonalityType currentPersonality;
    [SerializeField] private int actionsInCycle;
    [SerializeField] private string upcomingPersonality;

    private PersonalityController controller;

    private void Awake()
    {
        controller = GetComponent<PersonalityController>();
    }

    private void OnEnable()
    {
        controller.UpcomingRevealed += LogUpcoming;
        controller.Shifted += LogShift;
    }

    private void OnDisable()
    {
        controller.UpcomingRevealed -= LogUpcoming;
        controller.Shifted -= LogShift;
    }

    private void Update()
    {
        currentPersonality = controller.Current;
        actionsInCycle = controller.ActionsInCycle;

        PersonalityType? upcoming = controller.Upcoming;
        upcomingPersonality = upcoming.HasValue ? upcoming.Value.ToString() : "hidden";
    }

    private void LogUpcoming(PersonalityType next)
    {
        Debug.Log($"Next personality revealed: {next}");
    }

    private void LogShift(PersonalityType previous, PersonalityType current)
    {
        Debug.Log($"Personality shifted: {previous} -> {current}");
    }

    [ContextMenu("Register counted action")]
    private void RegisterTestAction()
    {
        controller.RegisterCountedAction();
        Debug.Log($"Counted action. Personality {controller.Current}, " +
                  $"actions in cycle {controller.ActionsInCycle}");
    }
}
