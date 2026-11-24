/// <summary>
/// Something that can lose health. The attack ability only needs this, so it never has to know whether the target is a dummy, a squirrel or a lion.
/// </summary>
public interface IDamageable
{
    // Removes health and returns the amount actually lost. Returns 0 when nothing was lost.
    float Damage(float amount);
}