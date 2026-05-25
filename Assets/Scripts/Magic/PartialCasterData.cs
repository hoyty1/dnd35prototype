// ============================================================================
// D&D 3.5e Partial Caster Data — Ranger & Paladin Spell Progression
// Both use identical spell progression tables (PHB p.46/p.48).
// Wisdom-based prepared divine casters, 1st-4th level spells only.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks spell slots for partial casters (Ranger and Paladin).
/// D&D 3.5e PHB: Both classes gain spellcasting at level 4, with usable
/// slots at level 5. Max 4th-level spells. Wisdom-based, prepared (like Cleric).
/// Bonus slots from Wisdom modifier apply per D&D 3.5e formula.
/// </summary>
public class PartialCasterData
{
    public string ClassName { get; private set; }
    public int ClassLevel { get; private set; }
    public bool IsInitialized { get; private set; }

    /// <summary>Maximum spell slots per spell level (1-4). Index 0 unused.</summary>
    public int[] SlotsMax = new int[5]; // [0]=unused, [1]-[4] = spell levels 1-4

    /// <summary>Remaining spell slots per spell level.</summary>
    public int[] SlotsRemaining = new int[5];

    /// <summary>Prepared spell IDs per spell level.</summary>
    public List<string>[] PreparedSpells = new List<string>[5];

    // ============================== SPELL PROGRESSION TABLE ==============================
    // PHB p.46 (Paladin) / p.48 (Ranger) — identical progression.
    // Rows = class levels 1-20, Columns = spell levels 1-4.
    // -1 means "not available", 0 means "available but 0 base slots" (bonus only).

    private static readonly int[,] SpellsPerDay = new int[20, 4]
    {
        // L1    L2    L3    L4      ← spell levels
        { -1,   -1,   -1,   -1 },  // Class Level 1
        { -1,   -1,   -1,   -1 },  // Class Level 2
        { -1,   -1,   -1,   -1 },  // Class Level 3
        {  0,   -1,   -1,   -1 },  // Class Level 4
        {  1,   -1,   -1,   -1 },  // Class Level 5
        {  1,   -1,   -1,   -1 },  // Class Level 6
        {  1,    0,   -1,   -1 },  // Class Level 7
        {  1,    1,   -1,   -1 },  // Class Level 8
        {  2,    1,   -1,   -1 },  // Class Level 9
        {  2,    1,    0,   -1 },  // Class Level 10
        {  2,    1,    1,   -1 },  // Class Level 11
        {  2,    2,    1,   -1 },  // Class Level 12
        {  3,    2,    1,    0 },  // Class Level 13
        {  3,    2,    1,    1 },  // Class Level 14
        {  3,    2,    2,    1 },  // Class Level 15
        {  3,    3,    2,    1 },  // Class Level 16
        {  4,    3,    2,    1 },  // Class Level 17
        {  4,    3,    2,    2 },  // Class Level 18
        {  4,    3,    3,    2 },  // Class Level 19
        {  4,    4,    3,    3 },  // Class Level 20
    };

    /// <summary>
    /// Initialize the partial caster data for a given class and level.
    /// </summary>
    /// <param name="className">"Ranger" or "Paladin"</param>
    /// <param name="classLevel">Class level (1-20)</param>
    /// <param name="wisMod">Wisdom modifier for bonus slots</param>
    public void Initialize(string className, int classLevel, int wisMod)
    {
        ClassName = className;
        ClassLevel = Mathf.Clamp(classLevel, 1, 20);
        IsInitialized = true;

        for (int i = 0; i < 5; i++)
            PreparedSpells[i] = new List<string>();

        // Calculate slots from table + Wisdom bonus
        for (int spellLevel = 1; spellLevel <= 4; spellLevel++)
        {
            int baseSlots = SpellsPerDay[ClassLevel - 1, spellLevel - 1];
            if (baseSlots < 0)
            {
                // Not available at this class level
                SlotsMax[spellLevel] = 0;
                SlotsRemaining[spellLevel] = 0;
                continue;
            }

            // D&D 3.5e bonus slots: +1 per spell level where wisMod >= spellLevel
            // But only if the class has access to that spell level (baseSlots >= 0)
            int bonusSlots = (wisMod >= spellLevel) ? 1 + (wisMod - spellLevel) / 4 : 0;

            SlotsMax[spellLevel] = baseSlots + bonusSlots;
            SlotsRemaining[spellLevel] = SlotsMax[spellLevel];
        }

        Debug.Log($"[PartialCaster] {className} L{ClassLevel} initialized: " +
                  $"Slots=[{SlotsMax[1]}/{SlotsMax[2]}/{SlotsMax[3]}/{SlotsMax[4]}] WIS mod={wisMod}");
    }

    /// <summary>Whether this partial caster has any spell slots available.</summary>
    public bool HasSpellcasting => IsInitialized && ClassLevel >= 4;

    /// <summary>Whether a specific spell level is accessible at current class level.</summary>
    public bool HasAccessToSpellLevel(int spellLevel)
    {
        if (!IsInitialized || spellLevel < 1 || spellLevel > 4) return false;
        return SpellsPerDay[ClassLevel - 1, spellLevel - 1] >= 0;
    }

    /// <summary>Get base slots (before bonus) at a spell level for a class level.</summary>
    public static int GetBaseSlots(int classLevel, int spellLevel)
    {
        if (classLevel < 1 || classLevel > 20 || spellLevel < 1 || spellLevel > 4) return -1;
        return SpellsPerDay[classLevel - 1, spellLevel - 1];
    }

    /// <summary>Check if a prepared spell can be cast at the given level.</summary>
    public bool CanCast(int spellLevel)
    {
        if (spellLevel < 1 || spellLevel > 4) return false;
        return SlotsRemaining[spellLevel] > 0;
    }

    /// <summary>Spend a slot at the given spell level. Returns false if none available.</summary>
    public bool SpendSlot(int spellLevel)
    {
        if (spellLevel < 1 || spellLevel > 4) return false;
        if (SlotsRemaining[spellLevel] <= 0) return false;
        SlotsRemaining[spellLevel]--;
        return true;
    }

    /// <summary>Prepare a spell in a slot at the given level.</summary>
    public bool PrepareSpell(string spellId, int spellLevel)
    {
        if (string.IsNullOrEmpty(spellId) || spellLevel < 1 || spellLevel > 4) return false;
        if (SlotsMax[spellLevel] <= 0) return false;
        if (PreparedSpells[spellLevel].Count >= SlotsMax[spellLevel]) return false;

        PreparedSpells[spellLevel].Add(spellId);
        return true;
    }

    /// <summary>Get all prepared spell IDs at a given level.</summary>
    public List<string> GetPreparedSpellsAtLevel(int spellLevel)
    {
        if (spellLevel < 1 || spellLevel > 4) return new List<string>();
        return new List<string>(PreparedSpells[spellLevel]);
    }

    /// <summary>Get all prepared spell IDs across all levels.</summary>
    public List<string> GetAllPreparedSpellIds()
    {
        var all = new List<string>();
        for (int i = 1; i <= 4; i++)
            all.AddRange(PreparedSpells[i]);
        return all;
    }

    /// <summary>Clear all prepared spells (for re-preparation after rest).</summary>
    public void ClearPreparedSpells()
    {
        for (int i = 1; i <= 4; i++)
            PreparedSpells[i].Clear();
    }

    /// <summary>Refresh all spell slots to maximum (after rest).</summary>
    public void RefreshAllSlots()
    {
        for (int i = 1; i <= 4; i++)
            SlotsRemaining[i] = SlotsMax[i];
    }

    /// <summary>Get the highest spell level accessible at current class level.</summary>
    public int GetHighestSpellLevel()
    {
        for (int i = 4; i >= 1; i--)
        {
            if (HasAccessToSpellLevel(i)) return i;
        }
        return 0;
    }

    /// <summary>Whether this class uses partial caster progression.</summary>
    public static bool IsPartialCasterClass(string className)
    {
        if (string.IsNullOrEmpty(className)) return false;
        return string.Equals(className, "Ranger", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(className, "Paladin", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether a CharacterStats has a partial caster class.</summary>
    public static bool IsPartialCasterClass(CharacterStats stats)
    {
        if (stats == null) return false;
        return stats.HasClass("Ranger") || stats.HasClass("Paladin");
    }

    public string GetDebugSummary()
    {
        string slots = "";
        for (int i = 1; i <= 4; i++)
        {
            if (SlotsMax[i] > 0 || HasAccessToSpellLevel(i))
                slots += $" L{i}:{SlotsRemaining[i]}/{SlotsMax[i]}";
        }
        return $"{ClassName} L{ClassLevel}:{(string.IsNullOrEmpty(slots) ? " (no slots)" : slots)}";
    }
}
