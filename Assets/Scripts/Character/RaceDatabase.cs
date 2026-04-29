using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Static database of D&D 3.5 racial data.
/// Contains all PHB races starting with Dwarf and Elf.
/// </summary>
public static class RaceDatabase
{
    private static Dictionary<string, RaceData> _races;
    private static readonly Dictionary<SizeCategory, List<string>> _raceNamesBySizeCategory = new Dictionary<SizeCategory, List<string>>();
    private static bool _initialized;

    /// <summary>Initialize the race database with all available races.</summary>
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        _races = new Dictionary<string, RaceData>();

        RegisterDwarf();
        RegisterElf();
        RegisterGnome();
        RegisterHalfElf();
        RegisterHalfOrc();
        RegisterHalfling();
        RegisterHuman();

        RebuildRaceSizeLookup();
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

    /// <summary>
    /// Returns all races whose size category matches <paramref name="sizeCategory"/>.
    /// Results are display names (for UI) sorted alphabetically.
    /// </summary>
    public static List<string> GetRaceNamesBySizeCategory(SizeCategory sizeCategory)
    {
        Init();

        if (_raceNamesBySizeCategory.TryGetValue(sizeCategory, out List<string> raceNames) && raceNames != null)
            return new List<string>(raceNames);

        return new List<string>();
    }

    /// <summary>Resolve a race's size category from race name. Returns false if race is unknown.</summary>
    public static bool TryGetRaceSizeCategory(string raceName, out SizeCategory sizeCategory)
    {
        sizeCategory = SizeCategory.Medium;
        RaceData race = GetRace(raceName);
        if (race == null)
            return false;

        sizeCategory = race.RaceSize.ToSizeCategory();
        return true;
    }

    private static void RebuildRaceSizeLookup()
    {
        _raceNamesBySizeCategory.Clear();

        foreach (RaceData race in _races.Values)
        {
            if (race == null || string.IsNullOrWhiteSpace(race.RaceName))
                continue;

            SizeCategory size = race.RaceSize.ToSizeCategory();
            if (!_raceNamesBySizeCategory.TryGetValue(size, out List<string> namesForSize))
            {
                namesForSize = new List<string>();
                _raceNamesBySizeCategory[size] = namesForSize;
            }

            if (!namesForSize.Contains(race.RaceName, StringComparer.OrdinalIgnoreCase))
                namesForSize.Add(race.RaceName);
        }

        foreach (List<string> names in _raceNamesBySizeCategory.Values)
            names.Sort(StringComparer.OrdinalIgnoreCase);
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
            BaseSpeedFeet = 20,  // 20 ft = 4 squares
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
            BaseSpeedFeet = 30,  // 30 ft = 6 squares (standard speed)
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

    // ========== GNOME ==========
    private static void RegisterGnome()
    {
        var gnome = new RaceData
        {
            RaceName = "Gnome",
            RaceSize = RaceData.Size.Small,  // Small size: +1 AC, +1 attack, +4 Hide, -4 grapple
            Type = RaceData.CreatureType.Humanoid,

            // Ability Score Modifiers: +2 CON, -2 STR
            STRModifier = -2,
            DEXModifier = 0,
            CONModifier = +2,
            WISModifier = 0,
            INTModifier = 0,
            CHAModifier = 0,

            // Physical Traits
            BaseSpeedFeet = 20,  // 20 ft = 4 squares (Small size)
            SpeedNotReducedByArmor = false,

            // Vision
            Vision = RaceData.VisionType.LowLight,
            DarkvisionRange = 0,

            // Combat
            StabilityBonus = 0,

            // Saving throws
            SaveVsPoison = 0,
            SaveVsSpells = 0,
            SaveVsEnchantment = 0,
            SaveVsIllusion = 2,  // +2 racial bonus on saves vs illusions
            SaveVsFear = 0,

            // Special traits
            Stonecunning = false,
            ImmunityToSleep = false,
            AutoSearchSecretDoors = false,
            ExtraFeatAtFirstLevel = false,
            ExtraSkillPointsPerLevel = 0,
            FavoredClass = "Bard",

            // Weapon Familiarity: gnome hooked hammer treated as martial
            WeaponFamiliarity = new List<string> { "gnome_hooked_hammer" },

            // Racial weapon proficiencies (none beyond familiarity)
            RacialWeaponProficiencies = new List<string>(),

            // Racial attack bonuses: +1 vs kobolds and goblinoids
            RacialAttackBonuses = new Dictionary<string, int>
            {
                { "Kobold", 1 },
                { "Goblinoid", 1 }
            },

            // Racial AC bonuses: +4 dodge vs giants
            RacialACBonuses = new Dictionary<string, int>
            {
                { "Giant", 4 }
            },

            // Racial skill bonuses: +2 Listen, +2 Craft (alchemy)
            RacialSkillBonuses = new Dictionary<string, int>
            {
                { "Listen", 2 },
                { "Craft_Alchemy", 2 }
            },

            ThrownAndSlingAttackBonus = 0
            // Note for future: Spell-like abilities (1/day): speak with animals (burrowing mammals),
            // dancing lights, ghost sound, prestidigitation (if CHA >= 10)
        };

        _races["gnome"] = gnome;
    }

    // ========== HALF-ELF ==========
    private static void RegisterHalfElf()
    {
        var halfElf = new RaceData
        {
            RaceName = "Half-Elf",
            RaceSize = RaceData.Size.Medium,
            Type = RaceData.CreatureType.Humanoid,

            // No ability score modifiers
            STRModifier = 0,
            DEXModifier = 0,
            CONModifier = 0,
            WISModifier = 0,
            INTModifier = 0,
            CHAModifier = 0,

            // Physical Traits
            BaseSpeedFeet = 30,  // 30 ft = 6 squares
            SpeedNotReducedByArmor = false,

            // Vision
            Vision = RaceData.VisionType.LowLight,
            DarkvisionRange = 0,

            // Combat
            StabilityBonus = 0,

            // Saving throws
            SaveVsPoison = 0,
            SaveVsSpells = 0,
            SaveVsEnchantment = 2,  // +2 vs enchantment spells and effects
            SaveVsIllusion = 0,
            SaveVsFear = 0,

            // Special traits
            Stonecunning = false,
            ImmunityToSleep = true,  // Immunity to sleep spells and effects
            AutoSearchSecretDoors = false,
            ExtraFeatAtFirstLevel = false,
            ExtraSkillPointsPerLevel = 0,
            CountsAsRace = "Elf",  // Elven Blood: counts as elf for prerequisites
            FavoredClass = "Any",  // Favored class: Any (like human, note for future)

            // No weapon familiarity or racial proficiencies
            WeaponFamiliarity = new List<string>(),
            RacialWeaponProficiencies = new List<string>(),

            // No racial attack or AC bonuses
            RacialAttackBonuses = new Dictionary<string, int>(),
            RacialACBonuses = new Dictionary<string, int>(),

            // Racial skill bonuses: +1 Listen, Search, Spot; +2 Diplomacy, Gather Information
            RacialSkillBonuses = new Dictionary<string, int>
            {
                { "Listen", 1 },
                { "Search", 1 },
                { "Spot", 1 },
                { "Diplomacy", 2 },
                { "Gather Information", 2 }
            },

            ThrownAndSlingAttackBonus = 0
        };

        _races["half-elf"] = halfElf;
    }

    // ========== HALF-ORC ==========
    private static void RegisterHalfOrc()
    {
        var halfOrc = new RaceData
        {
            RaceName = "Half-Orc",
            RaceSize = RaceData.Size.Medium,
            Type = RaceData.CreatureType.Humanoid,

            // Ability Score Modifiers: +2 STR, -2 INT, -2 CHA
            STRModifier = +2,
            DEXModifier = 0,
            CONModifier = 0,
            WISModifier = 0,
            INTModifier = -2,
            CHAModifier = -2,

            // Physical Traits
            BaseSpeedFeet = 30,  // 30 ft = 6 squares
            SpeedNotReducedByArmor = false,

            // Vision
            Vision = RaceData.VisionType.Darkvision,
            DarkvisionRange = 60,

            // Combat
            StabilityBonus = 0,

            // No racial saving throw bonuses
            SaveVsPoison = 0,
            SaveVsSpells = 0,
            SaveVsEnchantment = 0,
            SaveVsIllusion = 0,
            SaveVsFear = 0,

            // Special traits
            Stonecunning = false,
            ImmunityToSleep = false,
            AutoSearchSecretDoors = false,
            ExtraFeatAtFirstLevel = false,
            ExtraSkillPointsPerLevel = 0,
            CountsAsRace = "Orc",  // Orc Blood: counts as orc for prerequisites
            FavoredClass = "Barbarian",

            // No weapon familiarity or racial proficiencies
            WeaponFamiliarity = new List<string>(),
            RacialWeaponProficiencies = new List<string>(),

            // No racial attack or AC bonuses
            RacialAttackBonuses = new Dictionary<string, int>(),
            RacialACBonuses = new Dictionary<string, int>(),

            // No racial skill bonuses
            RacialSkillBonuses = new Dictionary<string, int>(),

            ThrownAndSlingAttackBonus = 0
        };

        _races["half-orc"] = halfOrc;
    }

    // ========== HALFLING ==========
    private static void RegisterHalfling()
    {
        var halfling = new RaceData
        {
            RaceName = "Halfling",
            RaceSize = RaceData.Size.Small,  // Small size: +1 AC, +1 attack, +4 Hide, -4 grapple
            Type = RaceData.CreatureType.Humanoid,

            // Ability Score Modifiers: +2 DEX, -2 STR
            STRModifier = -2,
            DEXModifier = +2,
            CONModifier = 0,
            WISModifier = 0,
            INTModifier = 0,
            CHAModifier = 0,

            // Physical Traits
            BaseSpeedFeet = 20,  // 20 ft = 4 squares (Small size)
            SpeedNotReducedByArmor = false,

            // Vision
            Vision = RaceData.VisionType.Normal,
            DarkvisionRange = 0,

            // Combat
            StabilityBonus = 0,

            // Saving throws
            SaveVsPoison = 0,
            SaveVsSpells = 0,
            SaveVsEnchantment = 0,
            SaveVsIllusion = 0,
            SaveVsFear = 2,  // +2 racial bonus on saving throws against fear (morale bonus)

            // Special traits
            Stonecunning = false,
            ImmunityToSleep = false,
            AutoSearchSecretDoors = false,
            ExtraFeatAtFirstLevel = false,
            ExtraSkillPointsPerLevel = 0,
            FavoredClass = "Rogue",

            // No weapon familiarity or racial proficiencies
            WeaponFamiliarity = new List<string>(),
            RacialWeaponProficiencies = new List<string>(),

            // No racial attack bonuses vs specific creatures
            RacialAttackBonuses = new Dictionary<string, int>(),

            // No racial AC bonuses
            RacialACBonuses = new Dictionary<string, int>(),

            // Racial skill bonuses: +2 Climb, Jump, Listen, Move Silently
            RacialSkillBonuses = new Dictionary<string, int>
            {
                { "Climb", 2 },
                { "Jump", 2 },
                { "Listen", 2 },
                { "Move Silently", 2 }
            },

            // +1 racial bonus on attack rolls with thrown weapons and slings
            ThrownAndSlingAttackBonus = 1
        };

        _races["halfling"] = halfling;
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
            SaveVsIllusion = 0,
            SaveVsFear = 0,

            Stonecunning = false,
            ImmunityToSleep = false,
            AutoSearchSecretDoors = false,

            // Human special traits
            ExtraFeatAtFirstLevel = true,       // Extra feat at 1st level (for future feat system)
            ExtraSkillPointsPerLevel = 1,       // +1 skill point per level (+4 at 1st, for future skill system)
            FavoredClass = "Any",               // Favored class: Any

            WeaponFamiliarity = new List<string>(),
            RacialWeaponProficiencies = new List<string>(),
            RacialAttackBonuses = new Dictionary<string, int>(),
            RacialACBonuses = new Dictionary<string, int>(),
            RacialSkillBonuses = new Dictionary<string, int>(),

            ThrownAndSlingAttackBonus = 0
        };

        _races["human"] = human;
    }
}
