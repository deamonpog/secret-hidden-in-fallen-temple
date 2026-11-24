using System;
using UnityEngine;

/// <summary>
/// A threat that builds up while the player stays near it with one personality, Shade (B)
/// by default. When the charge is full it hurts the player when damage is above zero,
/// raises Charged, then rests for a cooldown. Leaving the range, or another personality,
/// drops the charge to zero. A collider on a calm layer near the player, such as a pilgrim
/// or a monk, slows the charge. Trees use it for fear damage, squirrels to turn hostile.
/// </summary>
public class FearSource : MonoBehaviour
{
    // The player in the scene. A prefab cannot hold a scene reference, so the world
    // generator will set this at spawn. For now assign it on each placed object.
    [SerializeField] private Player player;
    [SerializeField] private PersonalityType affectedPersonality = PersonalityType.B;

    // Horizontal distance from this object within which the charge rises.
    [SerializeField] private float range = 4f;
    [SerializeField] private float chargeSeconds = 5f;
    [SerializeField] private float cooldownSeconds = 3f;

    // Damage dealt to the player at full charge. Zero only raises Charged.
    [SerializeField] private float damage = 10f;

    [Header("Calm")]
    // Layers of the colliders that calm the player, such as pilgrims and monks.
    [SerializeField] private LayerMask calmLayers;
    // Distance from the player within which a calm collider slows the charge.
    [SerializeField] private float calmRadius = 4f;
    // Charge rate multiplier while calmed. 0 stops the charge, 1 has no effect.
    [SerializeField, Range(0f, 1f)] private float calmFactor = 0.3f;

    private float cooldownLeft;

    // Progress toward the next fear event, from 0 to 1.
    public float Charge01 { get; private set; }

    // Raised each time the charge is full.
    public event Action Charged;

    // Sets the player the fear acts on. The world generator calls this at spawn.
    public void SetPlayer(Player value)
    {
        player = value;
    }

    private void Start()
    {
        if (player == null)
            Debug.LogWarning(name + " has a FearSource with no player assigned.", this);
    }

    private void Update()
    {
        if (player == null)
            return;

        if (cooldownLeft > 0f)
        {
            cooldownLeft -= Time.deltaTime;
            return;
        }

        if (!PlayerAffected())
        {
            Charge01 = 0f;
            return;
        }

        float rate = 1f / chargeSeconds;
        if (Calmed())
            rate *= calmFactor;

        Charge01 = Mathf.Min(1f, Charge01 + rate * Time.deltaTime);
        if (Charge01 < 1f)
            return;

        Charge01 = 0f;
        cooldownLeft = cooldownSeconds;
        if (damage > 0f)
            player.Health.Damage(damage);
        Charged?.Invoke();
    }

    // Keeps the tuning values in a sensible range when they are edited in the inspector.
    private void OnValidate()
    {
        range = Mathf.Max(0f, range);
        chargeSeconds = Mathf.Max(0.1f, chargeSeconds);
        cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        damage = Mathf.Max(0f, damage);
        calmRadius = Mathf.Max(0f, calmRadius);
    }

    // True while the player is alive, has the affected personality and is in range.
    private bool PlayerAffected()
    {
        if (player.Health.IsDead || player.Personality == null)
            return false;

        if (player.Personality.Current != affectedPersonality)
            return false;

        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= range * range;
    }

    // True when a collider on a calm layer is near the player.
    private bool Calmed()
    {
        return calmLayers.value != 0
               && Physics.CheckSphere(player.transform.position, calmRadius, calmLayers,
                                      QueryTriggerInteraction.Collide);
    }
}