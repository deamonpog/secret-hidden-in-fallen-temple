// Display names for the personalities. The enum values A, B and C stay the identifiers
// used in code; these names are only the text shown to the player.
public static class PersonalityTypeExtensions
{
    // Returns the name shown to the player for the given personality.
    public static string DisplayName(this PersonalityType type)
    {
        switch (type)
        {
            case PersonalityType.A: return "Reaver";
            case PersonalityType.B: return "Shade";
            case PersonalityType.C: return "Thinker";
            default: return type.ToString();
        }
    }
}
