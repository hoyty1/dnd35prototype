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
    /// The save DC of <paramref name="spell"/> cast by <paramref name="caster"/>. Delegates to the one DC rule,
    /// <see cref="SpellSaveDCRules"/> (PHB p.177, SPL-001): a pre-baked DC (scroll, wand, imbued spell) is kept;
    /// otherwise 10 + the casting class's level for the spell (heightened level with Heighten Spell) + that class's key
    /// ability modifier + Spell Focus + the gnome illusion bonus.
    /// </summary>
    public static int GetSpellSaveDC(CharacterController caster, SpellData spell, MetamagicData metamagic = null)
    {
        return SpellSaveDCRules.Compute(caster, spell, metamagic);
    }

    /// <summary>As <see cref="GetSpellSaveDC(CharacterController, SpellData, MetamagicData)"/>, from stats alone.</summary>
    public static int GetSpellSaveDC(CharacterStats stats, SpellData spell, MetamagicData metamagic = null)
    {
        return SpellSaveDCRules.Compute(stats, spell, metamagic);
    }

    /// <summary>
    /// The bare arithmetic 10 + spell level + modifier. Spell casts use <see cref="SpellSaveDCRules"/> instead.
    /// </summary>
    public static int GetSpellSaveDC(int spellLevel, int castingAbilityModifier)
    {
        return 10 + spellLevel + castingAbilityModifier;
    }

    // ════════════════════════════════════════════════════════════
    //  Casting Ability Modifier
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// The key spellcasting ability modifier of the character's casting class (PHB p.177: Wizard INT, Sorcerer and
    /// Bard CHA, Cleric, Druid, Paladin and Ranger WIS; DMG p.108: Adept WIS). A multiclass caster uses the highest
    /// casting class; without a casting class, the best of INT, WIS and CHA. Delegates to
    /// <see cref="SpellSaveDCRules.GetKeyAbilityModifier(CharacterStats, SpellData)"/>.
    /// </summary>
    public static int GetCastingAbilityModifier(CharacterStats stats, SpellData spell = null)
    {
        if (stats == null) return 0;
        return SpellSaveDCRules.GetKeyAbilityModifier(stats, spell);
    }

    /// <summary>
    /// Shortcut: get casting ability modifier from a CharacterController.
    /// </summary>
    public static int GetCastingAbilityModifier(CharacterController caster, SpellData spell = null)
    {
        return GetCastingAbilityModifier(caster?.Stats, spell);
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

    // ════════════════════════════════════════════════════════════
    //  Saving Throw Outcome (single-target cast paths)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// True when the target's successful save means a single-target spell's tracked effect
    /// is not applied. Shared by the PC path (PerformSpellCast) and the NPC path
    /// (TryNPCPerformSpellCast) so both follow one rule (SPL-007).
    ///
    /// Hostile effects whose save is "negates" (Hold Person, Charm Person, Slow, Confusion and so on)
    /// carry EffectType Debuff or Control: SpellCategoryClassifier.ReclassifyAll rewrites many Debuff
    /// spells to Control at database init, so both count. Blur (Will negates, harmless) is negated
    /// when an unwilling target saves.
    ///
    /// Not negated: Cause Fear (PHB p.208) and Scare (PHB p.274) have Will partial saves (shaken for
    /// 1 round), resolved by their own handlers; nonintelligent undead get no save against
    /// Command Undead (PHB p.211), so a rolled save does not count.
    /// </summary>
    public static bool IsEffectNegatedBySave(SpellData spell, SpellResult result, bool commandUndeadTargetIsNonintelligent)
    {
        if (spell == null || result == null) return false;
        if (!result.RequiredSave || !result.SaveSucceeded) return false;

        string id = spell.SpellId;
        if (string.Equals(id, SpellNames.CAUSE_FEAR, StringComparison.Ordinal)
            || string.Equals(id, SpellNames.SCARE, StringComparison.Ordinal))
            return false;

        if (commandUndeadTargetIsNonintelligent && string.Equals(id, SpellNames.COMMAND_UNDEAD, StringComparison.Ordinal))
            return false;

        return spell.EffectType == SpellEffectType.Debuff
            || spell.EffectType == SpellEffectType.Control
            || string.Equals(id, SpellNames.BLUR, StringComparison.Ordinal);
    }
}
