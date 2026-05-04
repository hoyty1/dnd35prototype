namespace DND35e.Identifiers
{
    /// <summary>
    /// Type-safe enumeration for items.
    /// Integer ranges are grouped by category for maintainability.
    /// </summary>
    public enum ItemID
    {
        None = 0,

        // Potions (1000-1999)
        PotionCureLightWounds = 1000,
        PotionCureModerateWounds = 1001,
        PotionCureSeriousWounds = 1002,
        PotionBullsStrength = 1010,
        PotionCatsGrace = 1011,
        PotionBearsEndurance = 1012,
        PotionInvisibility = 1020,
        PotionHaste = 1030,
        PotionShieldOfFaith = 1040,

        // Weapons - Simple Melee (2000-2099)
        WeaponDagger = 2000,
        WeaponQuarterstaff = 2001,
        WeaponClub = 2002,
        WeaponMaceLight = 2003,
        WeaponMaceHeavy = 2004,
        WeaponSpear = 2005,

        // Weapons - Martial Melee (2100-2199)
        WeaponLongsword = 2100,
        WeaponShortsword = 2101,
        WeaponGreatsword = 2102,
        WeaponBattleaxe = 2103,
        WeaponGreataxe = 2104,
        WeaponWarhammer = 2105,
        WeaponRapier = 2106,
        WeaponScimitar = 2107,
        WeaponFalchion = 2108,
        WeaponFlailHeavy = 2109,
        WeaponLance = 2110,
        WeaponMorningstar = 2111,
        WeaponJavelin = 2112,

        // Weapons - Ranged (2200-2299)
        WeaponShortbow = 2200,
        WeaponLongbow = 2201,
        WeaponCrossbowLight = 2202,
        WeaponCrossbowHeavy = 2203,
        WeaponSling = 2204,

        // Armor - Light (3000-3099)
        ArmorPadded = 3000,
        ArmorLeather = 3001,
        ArmorStuddedLeather = 3002,
        ArmorChainShirt = 3003,

        // Armor - Medium (3100-3199)
        ArmorHide = 3100,
        ArmorScaleMail = 3101,
        ArmorChainMail = 3102,
        ArmorBreastplate = 3103,

        // Armor - Heavy (3200-3299)
        ArmorSplintMail = 3200,
        ArmorBandedMail = 3201,
        ArmorHalfPlate = 3202,
        ArmorPlate = 3203,

        // Shields (3300-3399)
        ShieldBuckler = 3300,
        ShieldLightWooden = 3301,
        ShieldLightSteel = 3302,
        ShieldHeavyWooden = 3303,
        ShieldHeavySteel = 3304,
        ShieldTower = 3305,

        // Ammunition (4000-4099)
        AmmoArrow = 4000,
        AmmoBolt = 4001,
        AmmoSlingBullet = 4002,
        AmmoCrossbowBolts20 = 4003,

        // Scrolls (5000-5099)
        ScrollMagicMissile = 5000,
        ScrollCureLightWounds = 5001,
        ScrollFireball = 5002,
        ScrollLightningBolt = 5003,
        ScrollHaste = 5004,

        // Gear & misc (6000-6999)
        GearBackpack = 6000,
        GearBedroll = 6001,
        GearRope = 6002,
        GearTorch = 6003,
        GearRations = 6004,
        GearWaterskin = 6005,
        GearRopeHemp = 6006,
        GearRopeSilk = 6007
    }
}
