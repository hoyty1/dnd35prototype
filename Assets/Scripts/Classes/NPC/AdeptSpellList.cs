using System.Collections.Generic;

/// <summary>
/// Complete Adept spell list (D&D 3.5e DMG p.107-108).
/// The Adept is a prepared divine caster (WIS-based) with spells from 0-5th level.
/// This list is a unique mix of arcane and divine spells.
/// </summary>
public static class AdeptSpellList
{
    private static Dictionary<int, List<string>> _spellsByLevel;
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        _spellsByLevel = new Dictionary<int, List<string>>();

        // 0-Level (Cantrips/Orisons)
        _spellsByLevel[0] = new List<string>
        {
            "create_water",
            "cure_minor_wounds",
            "detect_magic",
            "ghost_sound",
            "guidance",
            "light",
            "mending",
            "purify_food_and_drink",
            "read_magic",
            "touch_of_fatigue"
        };

        // 1st Level
        _spellsByLevel[1] = new List<string>
        {
            "bless",
            "burning_hands",
            "cure_light_wounds",
            "endure_elements",
            "obscuring_mist",
            "protection_from_chaos",
            "protection_from_evil",
            "protection_from_good",
            "protection_from_law",
            "sleep"
        };

        // 2nd Level
        _spellsByLevel[2] = new List<string>
        {
            "aid",
            "animal_trance",
            "bears_endurance",
            "bulls_strength",
            "cats_grace",
            "cure_moderate_wounds",
            "darkness",
            "delay_poison",
            "invisibility",
            "mirror_image",
            "resist_energy",
            "scorching_ray",
            "see_invisibility",
            "web"
        };

        // 3rd Level
        _spellsByLevel[3] = new List<string>
        {
            "animate_dead",
            "bestow_curse",
            "contagion",
            "continual_flame",
            "cure_serious_wounds",
            "daylight",
            "deeper_darkness",
            "lightning_bolt",
            "neutralize_poison",
            "remove_curse",
            "remove_disease",
            "tongues"
        };

        // 4th Level
        _spellsByLevel[4] = new List<string>
        {
            "cure_critical_wounds",
            "minor_creation",
            "polymorph",
            "restoration",
            "stoneskin",
            "wall_of_fire"
        };

        // 5th Level
        _spellsByLevel[5] = new List<string>
        {
            "baleful_polymorph",
            "break_enchantment",
            "commune",
            "heal",
            "major_creation",
            "raise_dead",
            "true_seeing",
            "wall_of_stone"
        };
    }

    /// <summary>Get all spell IDs for a given spell level.</summary>
    public static List<string> GetSpellsForLevel(int spellLevel)
    {
        Init();
        if (_spellsByLevel.TryGetValue(spellLevel, out var list))
            return new List<string>(list);
        return new List<string>();
    }

    /// <summary>Check if a spell is on the Adept spell list at the given level.</summary>
    public static bool IsAdeptSpell(string spellId, int spellLevel)
    {
        Init();
        if (_spellsByLevel.TryGetValue(spellLevel, out var list))
            return list.Contains(spellId);
        return false;
    }

    /// <summary>Check if a spell is on the Adept spell list at any level.</summary>
    public static bool IsAdeptSpell(string spellId)
    {
        Init();
        foreach (var kvp in _spellsByLevel)
        {
            if (kvp.Value.Contains(spellId)) return true;
        }
        return false;
    }

    /// <summary>Get the spell level for a spell on the Adept list. Returns -1 if not found.</summary>
    public static int GetSpellLevel(string spellId)
    {
        Init();
        foreach (var kvp in _spellsByLevel)
        {
            if (kvp.Value.Contains(spellId)) return kvp.Key;
        }
        return -1;
    }

    /// <summary>Total number of spells on the Adept list.</summary>
    public static int TotalSpellCount
    {
        get
        {
            Init();
            int count = 0;
            foreach (var kvp in _spellsByLevel)
                count += kvp.Value.Count;
            return count;
        }
    }

    /// <summary>Number of spell levels (0-5 = 6 levels).</summary>
    public static int MaxSpellLevel => 5;
}
