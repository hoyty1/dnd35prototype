// ============================================================================
// D&D 3.5e Spontaneous Casting Data — Sorcerer & Bard Support
// Tracks spells known and spell slots for spontaneous (non-prepared) casters.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages spontaneous casting for Sorcerer and Bard.
/// Instead of preparing specific spells into slots, the caster knows a fixed set
/// of spells and can cast any of them by spending a slot of the appropriate level.
/// 
/// D&D 3.5e PHB p.51-54 (Sorcerer), p.26-29 (Bard):
///   - Spells known are fixed per level (see progression tables)
///   - Any known spell can be cast by expending a slot of that level
///   - No preparation step — just pick a known spell and spend the slot
///   - Bonus slots from high casting ability (CHA for both)
///   - On level-up, may swap one known spell for another of the same level
/// </summary>
[Serializable]
public class SpontaneousCastingData
{
    /// <summary>The caster class name ("Sorcerer" or "Bard").</summary>
    public string CasterClassName;

    /// <summary>Current caster class level.</summary>
    public int CasterLevel;

    /// <summary>Spells known by level. Index = spell level (0-9). Each list contains spell IDs.</summary>
    public List<string>[] SpellsKnownByLevel = new List<string>[10];

    /// <summary>Max spells known per level (from class progression table).</summary>
    public int[] MaxSpellsKnownByLevel = new int[10];

    /// <summary>Spell slots remaining per level (0-9). Cantrips (0) are unlimited.</summary>
    public int[] SlotsRemaining = new int[10];

    /// <summary>Max spell slots per level (base from table + ability bonus).</summary>
    public int[] SlotsMax = new int[10];

    /// <summary>Whether this data has been initialized.</summary>
    public bool IsInitialized;

    // ============================== CONSTRUCTOR ==============================

    public SpontaneousCastingData()
    {
        for (int i = 0; i < 10; i++)
            SpellsKnownByLevel[i] = new List<string>();
    }

    // ============================== INITIALIZATION ==============================

    /// <summary>
    /// Initialize spontaneous casting data for a character.
    /// </summary>
    /// <param name="className">Class name ("Sorcerer" or "Bard").</param>
    /// <param name="classLevel">Level in the casting class.</param>
    /// <param name="abilityModifier">Casting ability modifier (CHA mod).</param>
    public void Initialize(string className, int classLevel, int abilityModifier)
    {
        CasterClassName = className;
        CasterLevel = Mathf.Max(1, classLevel);

        int safeLevel = Mathf.Clamp(CasterLevel, 1, 20);
        int tableIndex = safeLevel - 1;

        // Get base slots and spells known from class tables
        int[,] slotsTable = GetSlotsPerDayTable(className);
        int[,] knownTable = GetSpellsKnownTable(className);
        int maxSpellLevel = GetMaxSpellLevel(className);

        for (int spellLevel = 0; spellLevel <= maxSpellLevel; spellLevel++)
        {
            int baseSlots = slotsTable[tableIndex, spellLevel];
            int maxKnown = knownTable[tableIndex, spellLevel];

            // D&D 3.5e: Bonus slots from ability score (PHB p.8)
            // A caster gets +1 bonus slot at spell level L if ability modifier >= L
            // Never grants access to a new spell level (baseSlots must be > 0)
            int bonusSlots = 0;
            if (spellLevel > 0 && baseSlots > 0 && abilityModifier >= spellLevel)
                bonusSlots = 1 + (abilityModifier - spellLevel) / 4;

            SlotsMax[spellLevel] = baseSlots + bonusSlots;
            SlotsRemaining[spellLevel] = SlotsMax[spellLevel];
            MaxSpellsKnownByLevel[spellLevel] = maxKnown;
        }

        // Clear higher spell levels
        for (int spellLevel = maxSpellLevel + 1; spellLevel < 10; spellLevel++)
        {
            SlotsMax[spellLevel] = 0;
            SlotsRemaining[spellLevel] = 0;
            MaxSpellsKnownByLevel[spellLevel] = 0;
        }

        IsInitialized = true;
        Debug.Log($"[SpontaneousCasting] Initialized {className} L{CasterLevel}: " +
            $"Slots={FormatArray(SlotsMax)}, Known={FormatArray(MaxSpellsKnownByLevel)}");
    }

    // ============================== CASTING ==============================

    /// <summary>Can this caster cast a specific spell right now?</summary>
    public bool CanCast(string spellId, int spellLevel)
    {
        if (string.IsNullOrEmpty(spellId)) return false;
        if (spellLevel < 0 || spellLevel > 9) return false;

        // Must know the spell
        if (!IsSpellKnown(spellId, spellLevel)) return false;

        // Must have a slot available (cantrips are unlimited in 3.5e)
        if (spellLevel == 0) return true;
        return SlotsRemaining[spellLevel] > 0;
    }

    /// <summary>Spend a slot to cast a spell. Returns true if successful.</summary>
    public bool SpendSlot(int spellLevel)
    {
        if (spellLevel == 0) return true; // Cantrips unlimited
        if (spellLevel < 0 || spellLevel > 9) return false;
        if (SlotsRemaining[spellLevel] <= 0) return false;

        SlotsRemaining[spellLevel]--;
        return true;
    }

    /// <summary>Check if a spell is known at a given level.</summary>
    public bool IsSpellKnown(string spellId, int spellLevel)
    {
        if (spellLevel < 0 || spellLevel > 9) return false;
        if (SpellsKnownByLevel[spellLevel] == null) return false;
        return SpellsKnownByLevel[spellLevel].Contains(spellId);
    }

    /// <summary>Check if a spell is known at any level.</summary>
    public bool IsSpellKnownAtAnyLevel(string spellId)
    {
        for (int i = 0; i < 10; i++)
        {
            if (SpellsKnownByLevel[i] != null && SpellsKnownByLevel[i].Contains(spellId))
                return true;
        }
        return false;
    }

    // ============================== SPELL MANAGEMENT ==============================

    /// <summary>
    /// Learn a new spell. Returns true if the spell was successfully added.
    /// Fails if the caster already knows the max spells at that level.
    /// </summary>
    public bool LearnSpell(string spellId, int spellLevel)
    {
        if (string.IsNullOrEmpty(spellId)) return false;
        if (spellLevel < 0 || spellLevel > 9) return false;
        if (SpellsKnownByLevel[spellLevel] == null)
            SpellsKnownByLevel[spellLevel] = new List<string>();

        // Already known?
        if (SpellsKnownByLevel[spellLevel].Contains(spellId)) return false;

        // Check limit
        if (SpellsKnownByLevel[spellLevel].Count >= MaxSpellsKnownByLevel[spellLevel])
        {
            Debug.LogWarning($"[SpontaneousCasting] Cannot learn {spellId}: " +
                $"already at max {MaxSpellsKnownByLevel[spellLevel]} known spells at level {spellLevel}");
            return false;
        }

        SpellsKnownByLevel[spellLevel].Add(spellId);
        Debug.Log($"[SpontaneousCasting] Learned spell: {spellId} (level {spellLevel}). " +
            $"Now {SpellsKnownByLevel[spellLevel].Count}/{MaxSpellsKnownByLevel[spellLevel]}");
        return true;
    }

    /// <summary>
    /// Forget a known spell (for level-up swapping).
    /// D&D 3.5e PHB p.54: At 4th level and every even level after, a Sorcerer can
    /// swap one known spell for a different spell of the same level.
    /// </summary>
    public bool ForgetSpell(string spellId, int spellLevel)
    {
        if (spellLevel < 0 || spellLevel > 9) return false;
        if (SpellsKnownByLevel[spellLevel] == null) return false;
        return SpellsKnownByLevel[spellLevel].Remove(spellId);
    }

    /// <summary>
    /// Swap a known spell for a new one at the same level.
    /// Used during level-up (Sorcerer: even levels 4+; Bard: levels 5,7,9,11,...).
    /// </summary>
    public bool SwapSpell(string oldSpellId, string newSpellId, int spellLevel)
    {
        if (!ForgetSpell(oldSpellId, spellLevel)) return false;
        if (!LearnSpell(newSpellId, spellLevel))
        {
            // Roll back
            LearnSpell(oldSpellId, spellLevel);
            return false;
        }
        Debug.Log($"[SpontaneousCasting] Swapped {oldSpellId} → {newSpellId} at level {spellLevel}");
        return true;
    }

    // ============================== SLOT MANAGEMENT ==============================

    /// <summary>Refresh all spell slots (on rest). Cantrips stay unlimited.</summary>
    public void RefreshAllSlots()
    {
        for (int i = 0; i < 10; i++)
            SlotsRemaining[i] = SlotsMax[i];

        Debug.Log($"[SpontaneousCasting] {CasterClassName}: All spell slots refreshed.");
    }

    /// <summary>Get remaining slots at a spell level.</summary>
    public int GetSlotsRemaining(int spellLevel)
    {
        if (spellLevel < 0 || spellLevel > 9) return 0;
        return SlotsRemaining[spellLevel];
    }

    /// <summary>Get max slots at a spell level.</summary>
    public int GetMaxSlots(int spellLevel)
    {
        if (spellLevel < 0 || spellLevel > 9) return 0;
        return SlotsMax[spellLevel];
    }

    /// <summary>Get number of spells currently known at a level.</summary>
    public int GetSpellsKnownCount(int spellLevel)
    {
        if (spellLevel < 0 || spellLevel > 9) return 0;
        if (SpellsKnownByLevel[spellLevel] == null) return 0;
        return SpellsKnownByLevel[spellLevel].Count;
    }

    /// <summary>Get all known spell IDs at a specific level.</summary>
    public List<string> GetKnownSpellsAtLevel(int spellLevel)
    {
        if (spellLevel < 0 || spellLevel > 9) return new List<string>();
        if (SpellsKnownByLevel[spellLevel] == null) return new List<string>();
        return new List<string>(SpellsKnownByLevel[spellLevel]);
    }

    /// <summary>Get the highest spell level that has known spells or available slots.</summary>
    public int GetHighestKnownSpellLevel()
    {
        int highest = -1;
        for (int i = 9; i >= 0; i--)
        {
            bool hasKnown = SpellsKnownByLevel[i] != null && SpellsKnownByLevel[i].Count > 0;
            bool hasSlots = SlotsMax[i] > 0;
            if (hasKnown || hasSlots)
            {
                highest = i;
                break;
            }
        }
        return highest;
    }

    /// <summary>Get all known spell IDs across all levels.</summary>
    public List<string> GetAllKnownSpellIds()
    {
        var all = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            if (SpellsKnownByLevel[i] != null)
                all.AddRange(SpellsKnownByLevel[i]);
        }
        return all;
    }

    /// <summary>How many new spells can be learned at a given level?</summary>
    public int GetAvailableSlots(int spellLevel)
    {
        if (spellLevel < 0 || spellLevel > 9) return 0;
        int known = SpellsKnownByLevel[spellLevel]?.Count ?? 0;
        return Mathf.Max(0, MaxSpellsKnownByLevel[spellLevel] - known);
    }

    /// <summary>Get the highest spell level this caster can cast.</summary>
    public int GetHighestCastableLevel()
    {
        for (int i = 9; i >= 0; i--)
        {
            if (SlotsMax[i] > 0) return i;
        }
        return 0;
    }

    /// <summary>
    /// Can this caster swap a spell at their current level?
    /// Sorcerer: at levels 4, 6, 8, 10, 12, 14, 16, 18, 20
    /// Bard: at levels 5, 8, 11, 14, 17, 20 (every 3 levels starting at 5)
    /// </summary>
    public bool CanSwapSpellAtCurrentLevel()
    {
        if (string.Equals(CasterClassName, "Sorcerer", StringComparison.OrdinalIgnoreCase))
            return CasterLevel >= 4 && CasterLevel % 2 == 0;
        if (string.Equals(CasterClassName, "Bard", StringComparison.OrdinalIgnoreCase))
            return CasterLevel >= 5 && (CasterLevel - 5) % 3 == 0;
        return false;
    }

    /// <summary>
    /// The highest spell level that can be swapped out.
    /// Sorcerer: new spell must be one level lower than highest known (PHB p.54).
    /// </summary>
    public int GetMaxSwapSpellLevel()
    {
        int highest = GetHighestCastableLevel();
        return Mathf.Max(0, highest - 1);
    }

    // ============================== PROGRESSION TABLES ==============================

    /// <summary>Sorcerer Spells Per Day — PHB p.52 Table 3-17.</summary>
    private static readonly int[,] SorcererSlotsPerDay = new int[20, 10]
    {
        // Lvl  0  1  2  3  4  5  6  7  8  9
        /*  1*/ {5, 3, 0, 0, 0, 0, 0, 0, 0, 0},
        /*  2*/ {6, 4, 0, 0, 0, 0, 0, 0, 0, 0},
        /*  3*/ {6, 5, 0, 0, 0, 0, 0, 0, 0, 0},
        /*  4*/ {6, 6, 3, 0, 0, 0, 0, 0, 0, 0},
        /*  5*/ {6, 6, 4, 0, 0, 0, 0, 0, 0, 0},
        /*  6*/ {6, 6, 5, 3, 0, 0, 0, 0, 0, 0},
        /*  7*/ {6, 6, 6, 4, 0, 0, 0, 0, 0, 0},
        /*  8*/ {6, 6, 6, 5, 3, 0, 0, 0, 0, 0},
        /*  9*/ {6, 6, 6, 6, 4, 0, 0, 0, 0, 0},
        /* 10*/ {6, 6, 6, 6, 5, 3, 0, 0, 0, 0},
        /* 11*/ {6, 6, 6, 6, 6, 4, 0, 0, 0, 0},
        /* 12*/ {6, 6, 6, 6, 6, 5, 3, 0, 0, 0},
        /* 13*/ {6, 6, 6, 6, 6, 6, 4, 0, 0, 0},
        /* 14*/ {6, 6, 6, 6, 6, 6, 5, 3, 0, 0},
        /* 15*/ {6, 6, 6, 6, 6, 6, 6, 4, 0, 0},
        /* 16*/ {6, 6, 6, 6, 6, 6, 6, 5, 3, 0},
        /* 17*/ {6, 6, 6, 6, 6, 6, 6, 6, 4, 0},
        /* 18*/ {6, 6, 6, 6, 6, 6, 6, 6, 5, 3},
        /* 19*/ {6, 6, 6, 6, 6, 6, 6, 6, 6, 4},
        /* 20*/ {6, 6, 6, 6, 6, 6, 6, 6, 6, 6}
    };

    /// <summary>Sorcerer Spells Known — PHB p.52 Table 3-17.</summary>
    private static readonly int[,] SorcererSpellsKnown = new int[20, 10]
    {
        // Lvl  0  1  2  3  4  5  6  7  8  9
        /*  1*/ {4, 2, 0, 0, 0, 0, 0, 0, 0, 0},
        /*  2*/ {5, 2, 0, 0, 0, 0, 0, 0, 0, 0},
        /*  3*/ {5, 3, 0, 0, 0, 0, 0, 0, 0, 0},
        /*  4*/ {6, 3, 1, 0, 0, 0, 0, 0, 0, 0},
        /*  5*/ {6, 4, 2, 0, 0, 0, 0, 0, 0, 0},
        /*  6*/ {7, 4, 2, 1, 0, 0, 0, 0, 0, 0},
        /*  7*/ {7, 5, 3, 2, 0, 0, 0, 0, 0, 0},
        /*  8*/ {8, 5, 3, 2, 1, 0, 0, 0, 0, 0},
        /*  9*/ {8, 5, 4, 3, 2, 0, 0, 0, 0, 0},
        /* 10*/ {9, 5, 4, 3, 2, 1, 0, 0, 0, 0},
        /* 11*/ {9, 5, 5, 4, 3, 2, 0, 0, 0, 0},
        /* 12*/ {9, 5, 5, 4, 3, 2, 1, 0, 0, 0},
        /* 13*/ {9, 5, 5, 4, 4, 3, 2, 0, 0, 0},
        /* 14*/ {9, 5, 5, 4, 4, 3, 2, 1, 0, 0},
        /* 15*/ {9, 5, 5, 4, 4, 4, 3, 2, 0, 0},
        /* 16*/ {9, 5, 5, 4, 4, 4, 3, 2, 1, 0},
        /* 17*/ {9, 5, 5, 4, 4, 4, 3, 3, 2, 0},
        /* 18*/ {9, 5, 5, 4, 4, 4, 3, 3, 2, 1},
        /* 19*/ {9, 5, 5, 4, 4, 4, 3, 3, 3, 2},
        /* 20*/ {9, 5, 5, 4, 4, 4, 3, 3, 3, 3}
    };

    /// <summary>Bard Spells Per Day — PHB p.27 Table 3-4. Max 6th level spells.</summary>
    private static readonly int[,] BardSlotsPerDay = new int[20, 10]
    {
        // Lvl  0  1  2  3  4  5  6  7  8  9
        /*  1*/ {2, 0, 0, 0, 0, 0, 0, 0, 0, 0},
        /*  2*/ {3, 0, 0, 0, 0, 0, 0, 0, 0, 0},
        /*  3*/ {3, 1, 0, 0, 0, 0, 0, 0, 0, 0},
        /*  4*/ {3, 2, 0, 0, 0, 0, 0, 0, 0, 0},
        /*  5*/ {3, 3, 1, 0, 0, 0, 0, 0, 0, 0},
        /*  6*/ {3, 3, 2, 0, 0, 0, 0, 0, 0, 0},
        /*  7*/ {3, 3, 2, 0, 0, 0, 0, 0, 0, 0},
        /*  8*/ {3, 3, 3, 1, 0, 0, 0, 0, 0, 0},
        /*  9*/ {3, 3, 3, 2, 0, 0, 0, 0, 0, 0},
        /* 10*/ {3, 3, 3, 2, 0, 0, 0, 0, 0, 0},
        /* 11*/ {3, 3, 3, 3, 1, 0, 0, 0, 0, 0},
        /* 12*/ {3, 3, 3, 3, 2, 0, 0, 0, 0, 0},
        /* 13*/ {3, 3, 3, 3, 2, 0, 0, 0, 0, 0},
        /* 14*/ {4, 3, 3, 3, 3, 1, 0, 0, 0, 0},
        /* 15*/ {4, 4, 3, 3, 3, 2, 0, 0, 0, 0},
        /* 16*/ {4, 4, 4, 3, 3, 2, 0, 0, 0, 0},
        /* 17*/ {4, 4, 4, 4, 3, 3, 1, 0, 0, 0},
        /* 18*/ {4, 4, 4, 4, 4, 3, 2, 0, 0, 0},
        /* 19*/ {4, 4, 4, 4, 4, 4, 3, 0, 0, 0},
        /* 20*/ {4, 4, 4, 4, 4, 4, 4, 0, 0, 0}
    };

    /// <summary>Bard Spells Known — PHB p.27 Table 3-4.</summary>
    private static readonly int[,] BardSpellsKnown = new int[20, 10]
    {
        // Lvl  0  1  2  3  4  5  6  7  8  9
        /*  1*/ {4, 0, 0, 0, 0, 0, 0, 0, 0, 0},
        /*  2*/ {5, 2, 0, 0, 0, 0, 0, 0, 0, 0},
        /*  3*/ {6, 3, 0, 0, 0, 0, 0, 0, 0, 0},
        /*  4*/ {6, 3, 2, 0, 0, 0, 0, 0, 0, 0},
        /*  5*/ {6, 4, 3, 0, 0, 0, 0, 0, 0, 0},
        /*  6*/ {6, 4, 3, 0, 0, 0, 0, 0, 0, 0},
        /*  7*/ {6, 4, 4, 2, 0, 0, 0, 0, 0, 0},
        /*  8*/ {6, 4, 4, 3, 0, 0, 0, 0, 0, 0},
        /*  9*/ {6, 4, 4, 3, 0, 0, 0, 0, 0, 0},
        /* 10*/ {6, 4, 4, 4, 2, 0, 0, 0, 0, 0},
        /* 11*/ {6, 4, 4, 4, 3, 0, 0, 0, 0, 0},
        /* 12*/ {6, 4, 4, 4, 3, 0, 0, 0, 0, 0},
        /* 13*/ {6, 4, 4, 4, 4, 2, 0, 0, 0, 0},
        /* 14*/ {6, 4, 4, 4, 4, 3, 0, 0, 0, 0},
        /* 15*/ {6, 4, 4, 4, 4, 3, 0, 0, 0, 0},
        /* 16*/ {6, 5, 4, 4, 4, 4, 2, 0, 0, 0},
        /* 17*/ {6, 5, 5, 4, 4, 4, 3, 0, 0, 0},
        /* 18*/ {6, 5, 5, 5, 4, 4, 3, 0, 0, 0},
        /* 19*/ {6, 5, 5, 5, 5, 4, 4, 0, 0, 0},
        /* 20*/ {6, 5, 5, 5, 5, 5, 4, 0, 0, 0}
    };

    // ============================== TABLE HELPERS ==============================

    private static int[,] GetSlotsPerDayTable(string className)
    {
        if (string.Equals(className, "Bard", StringComparison.OrdinalIgnoreCase))
            return BardSlotsPerDay;
        return SorcererSlotsPerDay; // Default to Sorcerer
    }

    private static int[,] GetSpellsKnownTable(string className)
    {
        if (string.Equals(className, "Bard", StringComparison.OrdinalIgnoreCase))
            return BardSpellsKnown;
        return SorcererSpellsKnown;
    }

    private static int GetMaxSpellLevel(string className)
    {
        if (string.Equals(className, "Bard", StringComparison.OrdinalIgnoreCase))
            return 6; // Bard maxes out at 6th level spells
        return 9; // Sorcerer goes to 9th
    }

    // ============================== PUBLIC STATIC QUERIES ==============================

    /// <summary>
    /// Get base spells per day for a class at a given level (no ability bonuses).
    /// Useful for UI display and validation.
    /// </summary>
    public static int GetBaseSlotsPerDay(string className, int classLevel, int spellLevel)
    {
        if (classLevel < 1 || classLevel > 20) return 0;
        if (spellLevel < 0 || spellLevel > 9) return 0;
        int[,] table = GetSlotsPerDayTable(className);
        return table[classLevel - 1, spellLevel];
    }

    /// <summary>
    /// Get max spells known for a class at a given level.
    /// </summary>
    public static int GetMaxSpellsKnown(string className, int classLevel, int spellLevel)
    {
        if (classLevel < 1 || classLevel > 20) return 0;
        if (spellLevel < 0 || spellLevel > 9) return 0;
        int[,] table = GetSpellsKnownTable(className);
        return table[classLevel - 1, spellLevel];
    }

    /// <summary>
    /// Check if a given class uses spontaneous casting.
    /// </summary>
    public static bool IsSpontaneousCasterClass(string className)
    {
        if (string.IsNullOrEmpty(className)) return false;
        return string.Equals(className, "Sorcerer", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(className, "Bard", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if a CharacterStats has any class that uses spontaneous casting.
    /// </summary>
    public static bool IsSpontaneousCasterClass(CharacterStats stats)
    {
        if (stats == null) return false;
        if (stats.HasClass("Sorcerer") || stats.HasClass("Bard")) return true;
        return false;
    }

    // ============================== DEBUG ==============================

    private static string FormatArray(int[] arr)
    {
        if (arr == null || arr.Length == 0) return "[]";
        var parts = new string[arr.Length];
        for (int i = 0; i < arr.Length; i++)
            parts[i] = arr[i].ToString();
        return "[" + string.Join(",", parts) + "]";
    }

    /// <summary>Returns a clone of SlotsMax for legacy UI compatibility.</summary>
    public int[] GetSlotsMaxArray() => (int[])SlotsMax.Clone();

    /// <summary>Returns a clone of SlotsRemaining for legacy UI compatibility.</summary>
    public int[] GetSlotsRemainingArray() => (int[])SlotsRemaining.Clone();

    /// <summary>Get a debug summary string.</summary>
    public string GetDebugSummary()
    {
        string known = "";
        for (int i = 0; i <= 9; i++)
        {
            int count = SpellsKnownByLevel[i]?.Count ?? 0;
            int max = MaxSpellsKnownByLevel[i];
            if (max > 0) known += $" L{i}:{count}/{max}";
        }

        string slots = "";
        for (int i = 0; i <= 9; i++)
        {
            if (SlotsMax[i] > 0) slots += $" L{i}:{SlotsRemaining[i]}/{SlotsMax[i]}";
        }

        return $"{CasterClassName} CL{CasterLevel} | Known:{known} | Slots:{slots}";
    }
}
