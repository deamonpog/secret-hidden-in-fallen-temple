using UnityEngine;

// Uses items from the player's inventory when a consume button is pressed.
// Each slot is tied to one item. An item that cannot be consumed leaves its slot unused.
// The speed boost sets a temporary multiplier on PlayerMovement.
[RequireComponent(typeof(PlayerCharacter))]
[RequireComponent(typeof(PlayerInputReader))]
public class ConsumeController : MonoBehaviour
{
    [SerializeField] private ItemType[] slotItems =
        { ItemType.Fruit, ItemType.CookedSquirrel, ItemType.Flower };

    [SerializeField] private float fruitHeal = 20f;
    [SerializeField] private float cookedSquirrelHeal = 40f;
    [SerializeField] private float boostMultiplier = 1.5f;
    [SerializeField] private float boostDuration = 3f;

    private PlayerCharacter player;
    private PlayerInputReader input;
    private ConsumeRules rules;
    private float boostRemaining;

    private void Awake()
    {
        player = GetComponent<PlayerCharacter>();
        input = GetComponent<PlayerInputReader>();
        rules = new ConsumeRules(fruitHeal, cookedSquirrelHeal);
    }

    private void OnEnable()
    {
        input.Consume1Pressed += ConsumeSlot1;
        input.Consume2Pressed += ConsumeSlot2;
        input.Consume3Pressed += ConsumeSlot3;
    }

    private void OnDisable()
    {
        input.Consume1Pressed -= ConsumeSlot1;
        input.Consume2Pressed -= ConsumeSlot2;
        input.Consume3Pressed -= ConsumeSlot3;
        EndBoost();
    }

    private void Update()
    {
        if (boostRemaining <= 0f)
            return;

        boostRemaining -= Time.deltaTime;
        if (boostRemaining <= 0f)
            EndBoost();
    }

    private void ConsumeSlot1() => Consume(0);
    private void ConsumeSlot2() => Consume(1);
    private void ConsumeSlot3() => Consume(2);

    private void Consume(int slot)
    {
        if (slot >= slotItems.Length)
            return;

        ItemType item = slotItems[slot];
        if (!rules.TryGetEffect(item, out ConsumeEffect effect))
            return;

        if (!player.Inventory.Items.TryRemove(item))
            return;

        player.Health.Heal(effect.HealAmount);

        if (effect.GivesSpeedBoost)
            StartBoost();

        player.RegisterCountedAction();
    }

    private void StartBoost()
    {
        boostRemaining = boostDuration;
        player.Movement.SpeedMultiplier = boostMultiplier;
    }

    private void EndBoost()
    {
        boostRemaining = 0f;
        player.Movement.SpeedMultiplier = 1f;
    }
}
