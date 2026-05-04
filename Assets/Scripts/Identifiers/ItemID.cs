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


        // Enhanced weapons (+1/+2) (7000-7999)
        WeaponDaggerPlus1 = 7000,
        WeaponDaggerPlus2 = 7001,
        WeaponQuarterstaffPlus1 = 7002,
        WeaponQuarterstaffPlus2 = 7003,
        WeaponClubPlus1 = 7004,
        WeaponClubPlus2 = 7005,
        WeaponMaceLightPlus1 = 7006,
        WeaponMaceLightPlus2 = 7007,
        WeaponMaceHeavyPlus1 = 7008,
        WeaponMaceHeavyPlus2 = 7009,
        WeaponSpearPlus1 = 7010,
        WeaponSpearPlus2 = 7011,
        WeaponLongswordPlus1 = 7012,
        WeaponLongswordPlus2 = 7013,
        WeaponShortswordPlus1 = 7014,
        WeaponShortswordPlus2 = 7015,
        WeaponGreatswordPlus1 = 7016,
        WeaponGreatswordPlus2 = 7017,
        WeaponBattleaxePlus1 = 7018,
        WeaponBattleaxePlus2 = 7019,
        WeaponGreataxePlus1 = 7020,
        WeaponGreataxePlus2 = 7021,
        WeaponWarhammerPlus1 = 7022,
        WeaponWarhammerPlus2 = 7023,
        WeaponRapierPlus1 = 7024,
        WeaponRapierPlus2 = 7025,
        WeaponScimitarPlus1 = 7026,
        WeaponScimitarPlus2 = 7027,
        WeaponFalchionPlus1 = 7028,
        WeaponFalchionPlus2 = 7029,
        WeaponFlailHeavyPlus1 = 7030,
        WeaponFlailHeavyPlus2 = 7031,
        WeaponLancePlus1 = 7032,
        WeaponLancePlus2 = 7033,
        WeaponMorningstarPlus1 = 7034,
        WeaponMorningstarPlus2 = 7035,
        WeaponJavelinPlus1 = 7036,
        WeaponJavelinPlus2 = 7037,
        WeaponShortbowPlus1 = 7038,
        WeaponShortbowPlus2 = 7039,
        WeaponLongbowPlus1 = 7040,
        WeaponLongbowPlus2 = 7041,
        WeaponCrossbowLightPlus1 = 7042,
        WeaponCrossbowLightPlus2 = 7043,
        WeaponCrossbowHeavyPlus1 = 7044,
        WeaponCrossbowHeavyPlus2 = 7045,
        WeaponSlingPlus1 = 7046,
        WeaponSlingPlus2 = 7047,

        // Enhanced armor and shields (+1/+2) (8000-8999)
        ArmorPaddedPlus1 = 8000,
        ArmorPaddedPlus2 = 8001,
        ArmorLeatherPlus1 = 8002,
        ArmorLeatherPlus2 = 8003,
        ArmorStuddedLeatherPlus1 = 8004,
        ArmorStuddedLeatherPlus2 = 8005,
        ArmorChainShirtPlus1 = 8006,
        ArmorChainShirtPlus2 = 8007,
        ArmorHidePlus1 = 8008,
        ArmorHidePlus2 = 8009,
        ArmorScaleMailPlus1 = 8010,
        ArmorScaleMailPlus2 = 8011,
        ArmorChainMailPlus1 = 8012,
        ArmorChainMailPlus2 = 8013,
        ArmorBreastplatePlus1 = 8014,
        ArmorBreastplatePlus2 = 8015,
        ArmorSplintMailPlus1 = 8016,
        ArmorSplintMailPlus2 = 8017,
        ArmorBandedMailPlus1 = 8018,
        ArmorBandedMailPlus2 = 8019,
        ArmorHalfPlatePlus1 = 8020,
        ArmorHalfPlatePlus2 = 8021,
        ArmorPlatePlus1 = 8022,
        ArmorPlatePlus2 = 8023,
        ShieldBucklerPlus1 = 8100,
        ShieldBucklerPlus2 = 8101,
        ShieldLightWoodenPlus1 = 8102,
        ShieldLightWoodenPlus2 = 8103,
        ShieldLightSteelPlus1 = 8104,
        ShieldLightSteelPlus2 = 8105,
        ShieldHeavyWoodenPlus1 = 8106,
        ShieldHeavyWoodenPlus2 = 8107,
        ShieldHeavySteelPlus1 = 8108,
        ShieldHeavySteelPlus2 = 8109,
        ShieldTowerPlus1 = 8110,
        ShieldTowerPlus2 = 8111,

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
