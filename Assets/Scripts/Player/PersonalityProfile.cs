using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Everything that differs between the personalities, as a data asset. There is one asset
/// per personality, so the values can be tuned in the inspector without touching code.
/// </summary>
[CreateAssetMenu(menuName = "SHIFT/Personality Profile", fileName = "PersonalityProfile")]
public class PersonalityProfile : ScriptableObject
{
    [SerializeField] private PersonalityType personality;

    // The name shown to the player. The enum values A, B and C stay the identifiers in code.
    [SerializeField] private string displayName;

    // Multiplies the player's move speed while this personality is active.
    [SerializeField] private float speedMultiplier = 1f;

    // The vision radius while this personality is active, before any other multiplier.
    [SerializeField] private float baseVisionRadius = 10f;

    [SerializeField] private AbilityType ability1;
    [SerializeField] private AbilityType ability2;

    [Header("Look and sound")]
    // The post-processing overrides applied while this personality is active. May be empty.
    [SerializeField] private VolumeProfile volumeProfile;

    // The fog color while this personality is active.
    [SerializeField] private Color fogTint = Color.gray;

    // A looping ambient sound while this personality is active. May be empty.
    [SerializeField] private AudioClip ambientClip;

    public PersonalityType Personality => personality;
    public string DisplayName => displayName;
    public float SpeedMultiplier => speedMultiplier;
    public float BaseVisionRadius => baseVisionRadius;
    public AbilityType Ability1 => ability1;
    public AbilityType Ability2 => ability2;
    public VolumeProfile VolumeProfile => volumeProfile;
    public Color FogTint => fogTint;
    public AudioClip AmbientClip => ambientClip;

    private void OnValidate()
    {
        speedMultiplier = Mathf.Max(0f, speedMultiplier);
        baseVisionRadius = Mathf.Max(0f, baseVisionRadius);
    }
}