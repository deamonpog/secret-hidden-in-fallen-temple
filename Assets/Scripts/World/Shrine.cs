using UnityEngine;

/// <summary>
/// A shrine the player activates by offering flowers. A satisfied normal shrine activates the
/// shrine it points to. The secret shrine gives a star and ends the run. The shrine shows its
/// state by a glow on its material's emission: none when inactive, warm yellow when active,
/// green when satisfied. The colors and their strength are set in the inspector.
/// </summary>
public class Shrine : MonoBehaviour, IInteractable
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private ShrineKind kind = ShrineKind.Normal;

    // Hops to the Secret: 3 for a chain start, 2 for the middle, 1 for the top, 0 for the Secret.
    [SerializeField] private int tier = 3;
    [SerializeField] private int flowersRequired = 3;

    // The shrine this one activates when satisfied. A chain top points at the Secret.
    [SerializeField] private Shrine next;

    // True for the first shrine of each chain.
    [SerializeField] private bool startsActive;

    [Header("Look")]
    // The renderer that glows. Its material must be URP Lit with Emission ticked.
    [SerializeField] private Renderer shrineRenderer;

    // Glow color for each state. The picker is HDR, so a color can be brighter than white.
    [SerializeField, ColorUsage(false, true)] private Color inactiveEmission = Color.black;
    [SerializeField, ColorUsage(false, true)] private Color activeEmission = new Color(1f, 0.8f, 0.2f);
    [SerializeField, ColorUsage(false, true)] private Color satisfiedEmission = new Color(0.3f, 1f, 0.4f);

    // Multiplies every state color, so the overall glow strength is tuned in one place.
    [SerializeField] private float emissionIntensity = 2f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock block;

    public ShrineKind Kind => kind;
    public int Tier => tier;
    public bool Active { get; private set; }
    public bool Satisfied { get; private set; }

    public string PromptText =>
        kind == ShrineKind.Secret ? "Open the secret shrine" : $"Offer {flowersRequired} flowers";

    // Sets what the world generator decides. Call it before the shrine is first enabled,
    // because Awake reads startsActive and OnEnable registers with the GameManager.
    public void Configure(GameManager manager, int newTier, Shrine nextShrine, bool active)
    {
        gameManager = manager;
        tier = Mathf.Max(0, newTier);
        next = nextShrine;
        startsActive = active;
    }

    private void Awake()
    {
        if (gameManager == null)
        {
            Debug.LogError("Shrine needs the GameManager assigned.", this);
            enabled = false;
            return;
        }

        if (GetComponentInChildren<Collider>() == null)
            Debug.LogWarning("Shrine has no collider on it or its children, " +
                             "so it cannot be interacted with.", this);

        block = new MaterialPropertyBlock();
        if (shrineRenderer != null && shrineRenderer.sharedMaterial != null && !shrineRenderer.sharedMaterial.IsKeywordEnabled("_EMISSION"))
            Debug.LogWarning("Shrine material has Emission off, so the state glow will not show. Tick Emission on the material.", this);
        Active = startsActive;
        UpdateLook();
    }

    private void OnEnable()
    {
        gameManager.RegisterShrine(this);
    }

    private void OnDisable()
    {
        if (gameManager != null)
            gameManager.UnregisterShrine(this);
    }

    private void OnValidate()
    {
        tier = Mathf.Max(0, tier);
        flowersRequired = Mathf.Max(1, flowersRequired);
        emissionIntensity = Mathf.Max(0f, emissionIntensity);

        // Shows inspector changes at once while playing.
        if (block != null)
            UpdateLook();
    }

    // Makes the shrine active. Does nothing when it already is.
    public void Activate()
    {
        if (Active)
            return;

        Active = true;
        UpdateLook();
    }

    public bool CanInteract(Player player)
    {
        if (!Active || Satisfied)
            return false;

        return kind == ShrineKind.Secret || player.Count(ItemType.Flower) >= flowersRequired;
    }

    public void Interact(Player player)
    {
        if (kind == ShrineKind.Secret)
        {
            Satisfied = true;
            UpdateLook();
            gameManager.AddStar();
            gameManager.Complete();
            return;
        }

        if (!player.TryRemove(ItemType.Flower, flowersRequired))
            return;

        Satisfied = true;
        UpdateLook();

        if (next != null)
            next.Activate();

        player.RegisterCountedAction();
    }

    // Sets the glow to match the state, without creating a material copy. The material's
    // base color is left alone.
    private void UpdateLook()
    {
        if (shrineRenderer == null)
            return;

        Color color = Satisfied ? satisfiedEmission : Active ? activeEmission : inactiveEmission;
        shrineRenderer.GetPropertyBlock(block);
        block.SetColor(EmissionColorId, color * emissionIntensity);
        shrineRenderer.SetPropertyBlock(block);
    }
}