using System;
using UnityEngine;

/// <summary>
/// Shows the model that belongs to the current personality and drives its Animator.
/// It only listens to events and reads state, it never changes gameplay.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(Abilities))]
[RequireComponent(typeof(InteractionSensor))]
public class PlayerAnimator : MonoBehaviour
{
    [Serializable]
    private class ModelEntry
    {
        // The personality this model is shown for.
        public PersonalityType personality;

        // The Animator on the model. Its GameObject is shown or hidden as a whole.
        public Animator animator;
    }

    [SerializeField] private ModelEntry[] models;

    // Seconds between a shift and the model swap, so the animation of the action that
    // caused the shift is not cut off. 0 swaps at once.
    [SerializeField] private float swapDelay = 0.35f;

    // Smoothing time in seconds for the Speed parameter.
    [SerializeField] private float speedDamping = 0.1f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DeadHash = Animator.StringToHash("Dead");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int CastHash = Animator.StringToHash("Cast");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int InteractHash = Animator.StringToHash("Interact");
    private static readonly int UseHash = Animator.StringToHash("Use");

    private Player player;
    private Abilities abilities;
    private InteractionSensor sensor;

    // The animators sorted by personality, so a model is found by its enum value.
    private Animator[] animatorByType;

    // The animator of the model that is shown now.
    private Animator active;

    private float lastHealth;
    private bool swapPending;
    private float swapLeft;

    private void Awake()
    {
        player = GetComponent<Player>();
        abilities = GetComponent<Abilities>();
        sensor = GetComponent<InteractionSensor>();
        BuildModelLookup();
    }

    // Sorts the assigned models by personality and reports any that are missing or duplicated.
    private void BuildModelLookup()
    {
        int count = Enum.GetValues(typeof(PersonalityType)).Length;
        animatorByType = new Animator[count];

        if (models != null)
        {
            foreach (ModelEntry entry in models)
            {
                if (entry == null || entry.animator == null)
                    continue;

                int index = (int)entry.personality;
                if (animatorByType[index] != null)
                    Debug.LogError($"Two models are assigned to personality {entry.personality}.", this);

                animatorByType[index] = entry.animator;
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (animatorByType[i] == null)
                Debug.LogError($"No model is assigned to personality {(PersonalityType)i}.", this);
        }
    }

    private void Start()
    {
        lastHealth = player.Health.Current;
        ShowModel(player.Personality.Current);
    }

    // This runs after the gameplay components (execution order 100), so their events exist.
    private void OnEnable()
    {
        player.Personality.Shifted += OnShifted;
        player.Health.Changed += OnHealthChanged;
        player.Health.Died += OnDied;
        player.Consumed += OnConsumed;
        abilities.AbilityStarted += OnAbilityStarted;
        sensor.Interacted += OnInteracted;
    }

    private void OnDisable()
    {
        player.Personality.Shifted -= OnShifted;
        player.Health.Changed -= OnHealthChanged;
        player.Health.Died -= OnDied;
        player.Consumed -= OnConsumed;
        abilities.AbilityStarted -= OnAbilityStarted;
        sensor.Interacted -= OnInteracted;
    }

    private void Update()
    {
        if (swapPending)
        {
            swapLeft -= Time.deltaTime;
            if (swapLeft <= 0f)
            {
                swapPending = false;
                ShowModel(player.Personality.Current);
            }
        }

        if (active != null)
            active.SetFloat(SpeedHash, player.Movement.HorizontalSpeed, speedDamping, Time.deltaTime);
    }

    // Shows the model of the personality and hides the others.
    private void ShowModel(PersonalityType type)
    {
        for (int i = 0; i < animatorByType.Length; i++)
        {
            if (animatorByType[i] != null)
                animatorByType[i].gameObject.SetActive(i == (int)type);
        }

        active = animatorByType[(int)type];
    }

    private void Fire(int trigger)
    {
        if (active != null)
            active.SetTrigger(trigger);
    }

    private void OnShifted(PersonalityType previous, PersonalityType current)
    {
        if (swapDelay <= 0f)
        {
            ShowModel(current);
            return;
        }

        swapPending = true;
        swapLeft = swapDelay;
    }

    // A drop in health plays the hit reaction. The death itself is handled by OnDied.
    private void OnHealthChanged(float current)
    {
        if (current < lastHealth && current > 0f)
            Fire(HitHash);

        lastHealth = current;
    }

    private void OnDied()
    {
        if (active != null)
            active.SetBool(DeadHash, true);
    }

    private void OnAbilityStarted(AbilityType ability)
    {
        Fire(ability == AbilityType.Attack ? AttackHash : CastHash);
    }

    private void OnInteracted(IInteractable interactable) => Fire(InteractHash);

    private void OnConsumed(ItemType item) => Fire(UseHash);
}