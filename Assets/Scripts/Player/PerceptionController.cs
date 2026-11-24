using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Blends the look and the ambient sound of the world when the personality shifts.
/// It only listens to the personality controller and never changes gameplay.
/// </summary>
[DefaultExecutionOrder(100)]
public class PerceptionController : MonoBehaviour
{
    [SerializeField] private PersonalityController personality;

    // Two global volumes. One holds the current look, the other fades the next look in,
    // and they swap roles after every blend.
    [SerializeField] private Volume volumeA;
    [SerializeField] private Volume volumeB;

    // Two looping sources used the same way for the ambient sound.
    [SerializeField] private AudioSource audioA;
    [SerializeField] private AudioSource audioB;

    // Seconds a blend takes.
    [SerializeField] private float blendSeconds = 1.5f;

    // Volume of the ambient sound when fully faded in.
    [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.6f;

    private Volume currentVolume;
    private Volume nextVolume;
    private AudioSource currentAudio;
    private AudioSource nextAudio;

    private Color fromFog;
    private Color toFog;
    private float blend;
    private bool blending;

    private void Awake()
    {
        if (personality == null || volumeA == null || volumeB == null || audioA == null || audioB == null)
        {
            Debug.LogError("PerceptionController needs the personality controller, both volumes and both audio sources.", this);
            enabled = false;
        }
    }

    private void OnValidate()
    {
        blendSeconds = Mathf.Max(0f, blendSeconds);
    }

    private void OnEnable()
    {
        personality.Shifted += OnShifted;
    }

    private void OnDisable()
    {
        if (personality != null)
            personality.Shifted -= OnShifted;
    }

    // Starts with the look of the current personality, with no blend.
    private void Start()
    {
        currentVolume = volumeA;
        nextVolume = volumeB;
        currentAudio = audioA;
        nextAudio = audioB;

        PersonalityProfile profile = personality.CurrentProfile;
        Load(currentVolume, currentAudio, profile);
        Load(nextVolume, nextAudio, null);

        currentVolume.weight = 1f;
        nextVolume.weight = 0f;
        currentAudio.volume = ambientVolume;
        nextAudio.volume = 0f;

        toFog = profile != null ? profile.FogTint : RenderSettings.fogColor;
        RenderSettings.fogColor = toFog;
    }

    private void Update()
    {
        if (!blending)
            return;

        blend += blendSeconds > 0f ? Time.deltaTime / blendSeconds : 1f;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blend));

        currentVolume.weight = 1f - t;
        nextVolume.weight = t;
        currentAudio.volume = (1f - t) * ambientVolume;
        nextAudio.volume = t * ambientVolume;
        RenderSettings.fogColor = Color.Lerp(fromFog, toFog, t);

        if (blend >= 1f)
            FinishBlend();
    }

    private void OnShifted(PersonalityType previous, PersonalityType current)
    {
        // A shift during a blend finishes the old blend at once.
        if (blending)
            FinishBlend();

        PersonalityProfile profile = personality.CurrentProfile;
        Load(nextVolume, nextAudio, profile);

        fromFog = RenderSettings.fogColor;
        toFog = profile != null ? profile.FogTint : fromFog;
        blend = 0f;
        blending = true;
    }

    // Puts the look of a profile into a volume and an audio source. A null profile, or one
    // without a volume profile or clip, leaves that part empty.
    private void Load(Volume volume, AudioSource audio, PersonalityProfile profile)
    {
        volume.sharedProfile = profile != null ? profile.VolumeProfile : null;

        audio.clip = profile != null ? profile.AmbientClip : null;
        if (audio.clip != null)
            audio.Play();
        else
            audio.Stop();
    }

    // Jumps to the end of the blend and swaps the roles of the two volumes and sources.
    private void FinishBlend()
    {
        blending = false;

        currentVolume.weight = 0f;
        nextVolume.weight = 1f;
        currentAudio.volume = 0f;
        nextAudio.volume = ambientVolume;
        currentAudio.Stop();
        RenderSettings.fogColor = toFog;

        Volume volume = currentVolume;
        currentVolume = nextVolume;
        nextVolume = volume;

        AudioSource audio = currentAudio;
        currentAudio = nextAudio;
        nextAudio = audio;
    }
}