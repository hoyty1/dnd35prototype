namespace DND35e.Identifiers
{
    /// <summary>
    /// Type-safe enumeration for spells.
    /// Organized by spell level ranges for maintainability.
    /// </summary>
    public enum SpellID
    {
        None = 0,

        // 0-level (1-99)
        AcidSplash = 1,
        DetectMagic = 2,
        Light = 3,
        MageHand = 4,
        RayOfFrost = 5,
        ReadMagic = 6,
        Resistance = 7,

        // 1st level (100-199)
        BurningHands = 100,
        CureLightWounds = 101,
        MagicMissile = 102,
        Shield = 103,
        MageArmor = 104,
        EnlargePerson = 105,
        Grease = 106,
        ColorSpray = 107,
        Sleep = 108,

        // 2nd level (200-299)
        CureModerateWounds = 200,
        ScorchingRay = 201,
        BullsStrength = 202,
        CatsGrace = 203,
        BearsEndurance = 204,
        FoxsCunning = 205,
        OwlsWisdom = 206,
        EaglesSplendor = 207,
        Invisibility = 208,
        MirrorImage = 209,
        Web = 210,

        // 3rd level (300-399)
        CureSeriousWounds = 300,
        Fireball = 301,
        LightningBolt = 302,
        Haste = 303,
        Slow = 304,
        DispelMagic = 305,
        Fly = 306,
        HoldPerson = 307,

        // 4th level (400-499)
        CureCriticalWounds = 400,
        IceStorm = 401,
        GreaterInvisibility = 402,
        Stoneskin = 403,

        // 5th level (500-599)
        ConeOfCold = 500,
        Teleport = 501,
        Cloudkill = 502
    }
}
