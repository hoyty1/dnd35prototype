namespace DND35e.Identifiers
{
    /// <summary>
    /// Type-safe enumeration for damage types.
    /// Replaces string-based GameConstants.DAMAGE_* with compiler-enforced types.
    /// </summary>
    public enum DamageType
    {
        Slashing = 0,
        Piercing = 1,
        Bludgeoning = 2,
        Fire = 3,
        Cold = 4,
        Electricity = 5,
        Acid = 6,
        Force = 7,
        Sonic = 8
    }
}
