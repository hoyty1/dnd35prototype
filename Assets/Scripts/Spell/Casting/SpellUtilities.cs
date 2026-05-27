using DND35e.Identifiers;
using System;
using UnityEngine;

// ============================================================================
// SpellUtilities — Centralized spell computation helpers for D&D 3.5e.
//
// Extracted from GameManager partial classes to enable reuse across services,
// spell files, area effects, and AI without requiring a GameManager reference.
//
// All methods are pure functions of character/spell data — no side effects.
//
// Usage:
//   int dc = SpellUtilities.GetSpellSaveDC(caster, spell);
//   int mod = SpellUtilities.GetCastingAbilityModifier(caster.Stats);
//   bool immune = SpellUtilities.IsImmuneToMindAffecting(target);
// ============================================================================

/// <summary>
/// Static utility class for spell-related computations.
/// Contains pure functions extracted from GameManager to reduce coupling
/// and enable reuse across the codebase.
/// </summary>
public static class SpellUtilities
{
    // ════════════════════════════════════════════════════════════
    //  Spell Save DC Calculation
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate the save DC for a spell: 10 + spell level + casting ability modifier.
    /// PHB p.171: "Saving Throw and Spell DCs"
    /// </summary>
    /// <param name="caster">The spellcaster.</param>
    /// <param name="spell">The spell being cast.</param>
    /// <returns>The spell save DC, minimum 10.</returns>
    public static int GetSpellSaveDC(CharacterController caster, SpellData spell)
    {
        if (caster == null || caster.Stats == null || spell == null)
            return 10;

        int castingMod = GetCastingAbilityModifier(caster.Stats);
        return 10 + spell.SpellLevel + castingMod;
    }

    /// <summary>
    /// Calculate the save DC using CharacterStats directly (for cases without a controller).
    /// </summary>
    public static int GetSpellSaveDC(CharacterStats stats, SpellData spell)
    {
        if (stats == null || spell == null)
            return 10;

        int castingMod = GetCastingAbilityModifier(stats);
        return 10 + spell.SpellLevel + castingMod;
    }

    /// <summary>
    /// Calculate the save DC given a spell level and explicit casting modifier.
    /// Useful when the modifier is already known or overridden.
    /// </summary>
    public static int GetSpellSaveDC(int spellLevel, int castingAbilityModifier)
    {
        return 10 + spellLevel + castingAbilityModifier;
    }

    // ════════════════════════════════════════════════════════════
    //  Casting Ability Modifier
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Determine the spellcasting ability modifier for a character based on their class.
    /// PHB p.8/p.43: Wizard=INT, Cleric/Druid/Ranger/Paladin=WIS, Sorcerer/Bard=CHA.
    /// Paladin uses WIS for spell DCs (CHA governs Smite/Turn/Lay on Hands only).
    /// Falls back to WIS for unrecognized classes, or highest of INT/WIS/CHA for no class.
    /// </summary>
    /// <param name="stats">The character's stats.</param>
    /// <returns>The relevant ability modifier for spell DCs and casting.</returns>
    public static int GetCastingAbilityModifier(CharacterStats stats)
    {
        if (stats == null) return 0;

        // INT-based casters
        if (stats.IsWizard)
            return stats.INTMod;

        // WIS-based casters (Paladin uses WIS for spell DCs — PHB p.43;
        // CHA is for Smite Evil, Turn Undead, Lay on Hands, NOT spell DCs)
        if (stats.IsCleric || stats.IsDruid || stats.IsRanger || stats.IsPaladin)
            return stats.WISMod;

        // CHA-based casters
        if (stats.IsSorcerer || stats.IsBard)
            return stats.CHAMod;

        // NPC classes: Adept uses WIS
        if (stats.IsAdept)
            return stats.WISMod;

        // Fallback: check class name string for edge cases
        string className = (stats.CharacterClass ?? string.Empty).Trim();
        if (string.Equals(className, "Druid", StringComparison.OrdinalIgnoreCase)
            || string.Equals(className, "Ranger", StringComparison.OrdinalIgnoreCase)
            || string.Equals(className, "Paladin", StringComparison.OrdinalIgnoreCase))
            return stats.WISMod;

        if (string.Equals(className, "Sorcerer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(className, "Bard", StringComparison.OrdinalIgnoreCase))
            return stats.CHAMod;

        // Unknown class: use highest modifier (reasonable for monsters/custom NPCs)
        return Mathf.Max(stats.INTMod, Mathf.Max(stats.WISMod, stats.CHAMod));
    }

    /// <summary>
    /// Shortcut: get casting ability modifier from a CharacterController.
    /// </summary>
    public static int GetCastingAbilityModifier(CharacterController caster)
    {
        return GetCastingAbilityModifier(caster?.Stats);
    }

    // ════════════════════════════════════════════════════════════
    //  Immunity Checks
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if a creature is immune to [Mind-Affecting] effects.
    /// PHB p.309: Undead, constructs, oozes, plants, vermin, and mindless creatures
    /// are immune to all mind-affecting effects.
    /// Delegates to CharacterStats.IsImmuneToMindAffecting() for canonical logic.
    /// </summary>
    public static bool IsImmuneToMindAffecting(CharacterController target)
    {
        if (target?.Stats == null) return false;
        return target.Stats.IsImmuneToMindAffecting();
    }

    /// <summary>
    /// Check if a creature is immune to [Sleep] effects.
    /// Elves and half-elves have innate sleep immunity (PHB p.15).
    /// Also immune if immune to mind-affecting.
    /// </summary>
    public static bool IsImmuneToSleepEffects(CharacterController target)
    {
        if (target == null || target.Stats == null)
            return true; // Null = can't target

        // Mind-affecting immunity covers sleep
        if (target.Stats.IsImmuneToMindAffecting())
            return true;

        // Racial immunity (e.g., Elves)
        if (target.Stats.Race != null && target.Stats.Race.ImmunityToSleep)
            return true;

        return false;
    }

    /// <summary>
    /// Check if a target is a living creature for [Fear] effects.
    /// Undead and constructs are immune to fear (PHB p.309).
    /// </summary>
    public static bool IsLivingCreatureForFear(CharacterController target)
    {
        if (target?.Stats == null) return false;

        string creatureType = string.IsNullOrWhiteSpace(target.Stats.CreatureType)
            ? string.Empty
            : target.Stats.CreatureType.Trim().ToLowerInvariant();

        return creatureType != "undead" && creatureType != "construct";
    }

    // ════════════════════════════════════════════════════════════
    //  Spell Identification Helpers
    // ════════════════════════════════════════════════════════════

    /// <summary>Check if a spell is a [Fear] descriptor spell.</summary>
    public static bool IsFearSpell(SpellData spell)
    {
        if (spell == null) return false;
        string id = spell.SpellId;
        return string.Equals(id, SpellNames.CAUSE_FEAR, StringComparison.Ordinal)
            || string.Equals(id, SpellNames.SCARE, StringComparison.Ordinal)
            || string.Equals(id, SpellNames.FEAR, StringComparison.Ordinal);
    }
}
