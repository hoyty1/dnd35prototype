using System.Collections.Generic;

/// <summary>
/// Static database of D&D 3.5 racial data.
/// Contains all PHB races starting with Dwarf and Elf.
/// </summary>
public static class RaceDatabase
{
    private static Dictionary<string, RaceData> _races;
    private static bool _initialized;

    /// <summary>Initialize the race database with all available races.</summary>
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        _races = new Dictionary<string, RaceData>();

        RegisterDwarf();
        RegisterElf();
        RegisterHuman();
    }

    /// <summary>Get a race by name (case-insensitive).</summary>
    public static RaceData GetRace(string raceName)
    {
        Init();
        if (raceName == null) return null;
        string key = raceName.ToLower();
        return _races.ContainsKey(key) ? _races[key] : null;
    }

    /// <summary>Get all available race names.</summary>
    public static List<string> GetAllRaceNames()
    {
        Init();
        return new List<string>(_races.Keys);
    }

    // ========== DWARF ==========
    private static void RegisterDwarf()
    {
        var dwarf = new RaceData
        {
            RaceName = "Dwarf",
            RaceSize = RaceData.Size.Medium,
            Type = RaceData.CreatureType.Humanoid,

            // Ability Score Modifiers: +2 CON, -2 CHA
            STRModifier = 0,
            DEXModifier = 0,
            CONModifier = +2,
            WISModifier = 0,
            INTModifier = 0,
            CHAModifier = -2,

            // Physical Traits
            BaseSpeedFeet = 20,  // 20 ft = 4 hexes
            SpeedNotReducedByArmor = true,  // Dwarf special: speed not reduced by armor/encumbrance

            // Vision
            Vision = RaceData.VisionType.Darkvision,
            DarkvisionRange = 60,

            // Combat: Stability +4 on checks to resist bull rush/trip
            StabilityBonus = 4,

            // Saving throws (for future implementation)
            SaveVsPoison = 2,
            SaveVsSpells = 2,
            SaveVsEnchantment = 0,

            // Special traits
            Stonecunning = true,
            ImmunityToSleep = false,
            AutoSearchSecretDoors = false,

            // Weapon Familiarity: dwarven waraxe and dwarven urgrosh treated as martial
            WeaponFamiliarity = new List<string> { "dwarven_waraxe", "dwarven_urgrosh" },

            // Racial weapon proficiencies (none beyond familiarity for dwarves)
            RacialWeaponProficiencies = new List<string>(),

            // Racial attack bonuses: +1 vs orcs and goblinoids
            RacialAttackBonuses = new Dictionary<string, int>
            {
                { "Orc", 1 },
                { "Goblinoid", 1 }
            },

            // Racial AC bonuses: +4 dodge vs giants (for future)
            RacialACBonuses = new Dictionary<string, int>
            {
                { "Giant", 4 }
            },

            // Racial skill bonuses (for future)
            RacialSkillBonuses = new Dictionary<string, int>
            {
                { "Appraise_Stone", 2 },  // Stonecunning-related
                { "Search_Stone", 2 },     // Stonecunning-related
                { "Craft_Stone", 2 },
                { "Craft_Metal", 2 }
            }
        };

        _races["dwarf"] = dwarf;
    }

    // ========== ELF ==========
    private static void RegisterElf()
    {
        var elf = new RaceData
        {
            RaceName = "Elf",
            RaceSize = RaceData.Size.Medium,
            Type = RaceData.CreatureType.Humanoid,

            // Ability Score Modifiers: +2 DEX, -2 CON
            STRModifier = 0,
            DEXModifier = +2,
            CONModifier = -2,
            WISModifier = 0,
            INTModifier = 0,
            CHAModifier = 0,

            // Physical Traits
            BaseSpeedFeet = 30,  // 30 ft = 6 hexes (standard speed)
            SpeedNotReducedByArmor = false,

            // Vision
            Vision = RaceData.VisionType.LowLight,
            DarkvisionRange = 0,

            // Combat
            StabilityBonus = 0,

            // Saving throws (for future implementation)
            SaveVsPoison = 0,
            SaveVsSpells = 0,
            SaveVsEnchantment = 2,  // +2 vs enchantment spells and effects

            // Special traits
            Stonecunning = false,
            ImmunityToSleep = true,     // Immune to sleep spells and effects
            AutoSearchSecretDoors = true, // Auto Search within 5 ft of secret door

            // Weapon Familiarity (none for elves)
            WeaponFamiliarity = new List<string>(),

            // Racial weapon proficiencies: longsword, rapier, longbow, shortbow,
            // composite longbow, composite shortbow (treated as martial even for non-martial classes)
            RacialWeaponProficiencies = new List<string>
            {
                "longsword",
                "rapier",
                "longbow",
                "shortbow",
                "longbow_composite",
                "shortbow_composite"
            },

            // No racial attack bonuses
            RacialAttackBonuses = new Dictionary<string, int>(),

            // No racial AC bonuses
            RacialACBonuses = new Dictionary<string, int>(),

            // Racial skill bonuses: +2 Listen, Search, Spot
            RacialSkillBonuses = new Dictionary<string, int>
            {
                { "Listen", 2 },
                { "Search", 2 },
                { "Spot", 2 }
            }
        };

        _races["elf"] = elf;
    }

    // ========== HUMAN (baseline, no modifiers) ==========
    private static void RegisterHuman()
    {
        var human = new RaceData
        {
            RaceName = "Human",
            RaceSize = RaceData.Size.Medium,
            Type = RaceData.CreatureType.Humanoid,

            // No ability score modifiers
            STRModifier = 0,
            DEXModifier = 0,
            CONModifier = 0,
            WISModifier = 0,
            INTModifier = 0,
            CHAModifier = 0,

            BaseSpeedFeet = 30,
            SpeedNotReducedByArmor = false,

            Vision = RaceData.VisionType.Normal,
            DarkvisionRange = 0,

            StabilityBonus = 0,
            SaveVsPoison = 0,
            SaveVsSpells = 0,
            SaveVsEnchantment = 0,

            Stonecunning = false,
            ImmunityToSleep = false,
            AutoSearchSecretDoors = false,

            WeaponFamiliarity = new List<string>(),
            RacialWeaponProficiencies = new List<string>(),
            RacialAttackBonuses = new Dictionary<string, int>(),
            RacialACBonuses = new Dictionary<string, int>(),
            RacialSkillBonuses = new Dictionary<string, int>()
            // Note: Humans get 1 extra feat and 4 extra skill points at 1st level,
            // +1 skill point per level thereafter. These are for future implementation.
        };

        _races["human"] = human;
    }
}
