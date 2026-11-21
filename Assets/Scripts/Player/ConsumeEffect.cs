// What consuming one item does to the player.
public readonly struct ConsumeEffect
{
    public float HealAmount { get; }
    public bool GivesSpeedBoost { get; }

    public ConsumeEffect(float healAmount, bool givesSpeedBoost)
    {
        HealAmount = healAmount;
        GivesSpeedBoost = givesSpeedBoost;
    }
}
