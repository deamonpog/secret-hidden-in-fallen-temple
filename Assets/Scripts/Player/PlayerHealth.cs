using UnityEngine;

// Unity wrapper around HealthModel. The model holds the rules, this holds the starting value.
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    private HealthModel model;

    // Created on first use so other components can read it during their own Awake.
    public HealthModel Model => model ??= new HealthModel(maxHealth);
}
