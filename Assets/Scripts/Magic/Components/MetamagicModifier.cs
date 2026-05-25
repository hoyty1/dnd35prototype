using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================================
// D&D 3.5 Metamagic System - MetamagicModifier & MetamagicSystem
// ============================================================================
// Provides the formal spell modification pipeline:
//   1. Clone original spell
//   2. Apply each metamagic modifier
//   3. Track effective spell level (base + increases from feats, not rods)
//   4. Cap at 9th level maximum
//   5. Track which metamagics applied (feat vs rod)
//
// Integrates with existing MetamagicData, SpellCaster, SpellcastingComponent.
// ============================================================================

/// <summary>
/// Represents a single metamagic modifier applied to a spell.
/// Wraps a MetamagicFeatId with additional context (rod source, heighten level).
/// </summary>
[Serializable]
public class MetamagicModifier
{
    /// <summary>Which metamagic feat this modifier represents.</summary>
    public MetamagicFeatId Type;

    /// <summary>
    /// Spell slot level increase for this metamagic.
    /// Computed from the feat type (e.g., Empower = +2, Maximize = +3).
    /// For Heighten, this is (target level - base level).
    /// </summary>
    public int SlotIncrease;

    /// <summary>
    /// True if this metamagic was applied by a Metamagic Rod (not a feat).
    /// Rod-applied metamagics do NOT increase the spell slot level consumed.
    /// </summary>
    public bool AppliedByRod;

    /// <summary>For Heighten Spell: the target spell level to heighten to.</summary>
    public int HeightenToLevel = -1;

    /// <summary>
    /// Create a metamagic modifier from a feat.
    /// </summary>
    public MetamagicModifier(MetamagicFeatId type, int baseSpellLevel, bool appliedByRod = false)
    {
        Type = type;
        AppliedByRod = appliedByRod;
        SlotIncrease = MetamagicData.GetStandardLevelAdjustment(type);
    }

    /// <summary>
    /// Create a Heighten Spell modifier with a specific target level.
    /// </summary>
    public static MetamagicModifier CreateHeighten(int baseSpellLevel, int targetLevel, bool appliedByRod = false)
    {
        return new MetamagicModifier(MetamagicFeatId.HeightenSpell, baseSpellLevel, appliedByRod)
        {
            HeightenToLevel = targetLevel,
            SlotIncrease = Mathf.Max(0, targetLevel - baseSpellLevel)
        };
    }
}

/// <summary>
/// Orchestrates the complete metamagic spell modification pipeline.
/// Central entry point for applying metamagic to spells with full validation.
/// </summary>
public static class MetamagicSystem
{
    /// <summary>Maximum effective spell level in D&D 3.5e.</summary>
    public const int MaxSpellLevel = 9;

    // ========================================================================
    // MAIN PIPELINE
    // ========================================================================

    /// <summary>
    /// Apply metamagic modifiers to a spell through the full pipeline.
    /// Returns a modified clone of the spell with all metamagic effects applied.
    /// The original spell is never modified.
    /// </summary>
    /// <param name="baseSpell">Original spell data (not modified).</param>
    /// <param name="modifiers">List of metamagic modifiers to apply.</param>
    /// <returns>Modified spell clone, or null if validation fails.</returns>
    public static MetamagicSpellResult PrepareMetamagicSpell(SpellData baseSpell, List<MetamagicModifier> modifiers)
    {
        if (baseSpell == null)
            return MetamagicSpellResult.Failure("Base spell is null.");

        if (modifiers == null || modifiers.Count == 0)
            return MetamagicSpellResult.Success(baseSpell.Clone(), baseSpell.SpellLevel, new List<MetamagicFeatId>(), false);

        // Step 1: Clone the spell
        SpellData modified = baseSpell.Clone();

        // Step 2: Build MetamagicData for the existing SpellCaster pipeline
        var metamagicData = new MetamagicData();
        var appliedTypes = new List<MetamagicFeatId>();
        int totalFeatSlotIncrease = 0;
        bool hasRodMetamagic = false;

        foreach (var mod in modifiers)
        {
            metamagicData.AppliedMetamagic.Add(mod.Type);
            appliedTypes.Add(mod.Type);

            if (mod.Type == MetamagicFeatId.HeightenSpell && mod.HeightenToLevel > 0)
            {
                metamagicData.HeightenToLevel = mod.HeightenToLevel;
            }

            if (!mod.AppliedByRod)
            {
                totalFeatSlotIncrease += mod.SlotIncrease;
            }
            else
            {
                hasRodMetamagic = true;
            }
        }

        // Step 3: Calculate effective spell level (only feat-based increases count)
        int effectiveLevel = baseSpell.SpellLevel + totalFeatSlotIncrease;

        // Step 4: Enforce 9th-level cap
        if (effectiveLevel > MaxSpellLevel)
        {
            return MetamagicSpellResult.Failure(
                $"Effective spell level {effectiveLevel} exceeds maximum ({MaxSpellLevel}). " +
                $"Base level {baseSpell.SpellLevel} + metamagic adjustment {totalFeatSlotIncrease} = {effectiveLevel}.");
        }

        // Step 5: Apply pre-cast modifications via existing SpellCaster pipeline
        SpellCaster.ApplyMetamagicToSpellData(modified, metamagicData);

        // Step 6: Set tracking fields on the modified spell
        modified.BaseSpellLevel = baseSpell.SpellLevel;
        modified.EffectiveSpellLevel = effectiveLevel;
        modified.AppliedMetamagics = new List<MetamagicFeatId>(appliedTypes);
        modified.HasRodMetamagic = hasRodMetamagic;
        modified.MetamagicDataRef = metamagicData;

        Debug.Log($"[MetamagicSystem] Prepared {baseSpell.Name}: " +
                  $"Lv{baseSpell.SpellLevel} → Lv{effectiveLevel} " +
                  $"({string.Join(", ", appliedTypes.Select(t => MetamagicData.GetDisplayName(t)))}" +
                  $"{(hasRodMetamagic ? " [rod]" : "")})");

        return MetamagicSpellResult.Success(modified, effectiveLevel, appliedTypes, hasRodMetamagic);
    }

    /// <summary>
    /// Simplified pipeline: apply metamagic from a MetamagicData instance.
    /// Used for backward compatibility with existing code paths.
    /// </summary>
    public static MetamagicSpellResult PrepareMetamagicSpell(SpellData baseSpell, MetamagicData metamagicData)
    {
        if (metamagicData == null || !metamagicData.HasAnyMetamagic)
            return MetamagicSpellResult.Success(baseSpell.Clone(), baseSpell.SpellLevel, new List<MetamagicFeatId>(), false);

        var modifiers = new List<MetamagicModifier>();
        foreach (var mmId in metamagicData.AppliedMetamagic)
        {
            var mod = new MetamagicModifier(mmId, baseSpell.SpellLevel);
            if (mmId == MetamagicFeatId.HeightenSpell)
                mod.HeightenToLevel = metamagicData.HeightenToLevel;
            modifiers.Add(mod);
        }

        return PrepareMetamagicSpell(baseSpell, modifiers);
    }

    // ========================================================================
    // VALIDATION
    // ========================================================================

    /// <summary>
    /// Validate whether a set of metamagic modifiers can be applied to a spell.
    /// Returns null if valid, or an error message string if invalid.
    /// </summary>
    public static string ValidateMetamagicApplication(
        SpellData spell,
        List<MetamagicModifier> modifiers,
        CharacterStats casterStats = null)
    {
        if (spell == null) return "Spell is null.";
        if (modifiers == null || modifiers.Count == 0) return null; // No metamagic = always valid

        // Check for duplicate metamagic types (D&D 3.5e: same metamagic can't be applied twice)
        var seenTypes = new HashSet<MetamagicFeatId>();
        foreach (var mod in modifiers)
        {
            if (mod.Type == MetamagicFeatId.None) continue;
            if (!seenTypes.Add(mod.Type))
                return $"Cannot apply {MetamagicData.GetDisplayName(mod.Type)} twice to the same spell.";
        }

        // Check spell compatibility for each metamagic
        foreach (var mod in modifiers)
        {
            if (!MetamagicData.IsApplicable(mod.Type, spell))
                return $"{MetamagicData.GetDisplayName(mod.Type)} cannot be applied to {spell.Name}.";
        }

        // Check character has the required feats (if caster stats provided)
        if (casterStats != null)
        {
            foreach (var mod in modifiers)
            {
                if (mod.AppliedByRod) continue; // Rod metamagics don't require feats
                string featName = MetamagicData.GetFeatName(mod.Type);
                if (!casterStats.HasFeat(featName))
                    return $"Character does not have the {featName} feat.";
            }
        }

        // Calculate effective level and check 9th-level cap
        int totalFeatIncrease = 0;
        foreach (var mod in modifiers)
        {
            if (!mod.AppliedByRod)
                totalFeatIncrease += mod.SlotIncrease;
        }
        int effectiveLevel = spell.SpellLevel + totalFeatIncrease;
        if (effectiveLevel > MaxSpellLevel)
            return $"Effective spell level {effectiveLevel} exceeds maximum ({MaxSpellLevel}).";

        return null; // Valid
    }

    /// <summary>
    /// Validate using MetamagicData (backward compatible).
    /// </summary>
    public static string ValidateMetamagicApplication(SpellData spell, MetamagicData metamagicData, CharacterStats casterStats = null)
    {
        if (metamagicData == null || !metamagicData.HasAnyMetamagic) return null;

        var modifiers = new List<MetamagicModifier>();
        foreach (var mmId in metamagicData.AppliedMetamagic)
        {
            var mod = new MetamagicModifier(mmId, spell.SpellLevel);
            if (mmId == MetamagicFeatId.HeightenSpell)
            {
                mod.HeightenToLevel = metamagicData.HeightenToLevel;
                mod.SlotIncrease = Mathf.Max(0, metamagicData.HeightenToLevel - spell.SpellLevel);
            }
            modifiers.Add(mod);
        }

        return ValidateMetamagicApplication(spell, modifiers, casterStats);
    }

    // ========================================================================
    // SPONTANEOUS VS PREPARED CASTER RULES
    // ========================================================================

    /// <summary>
    /// Determine if a caster is a spontaneous caster (Sorcerer, Bard).
    /// Spontaneous casters apply metamagic on-the-fly and take a full-round action
    /// (instead of standard action) when using metamagic.
    /// </summary>
    public static bool IsSpontaneousCaster(CharacterStats stats)
    {
        if (stats == null) return false;
        return stats.HasClass("Sorcerer") || stats.HasClass("Bard");
    }

    /// <summary>
    /// Determine if a caster is a prepared caster (Wizard, Cleric, Druid, Paladin, Ranger).
    /// Prepared casters choose metamagic during preparation; casting time is unchanged.
    /// </summary>
    public static bool IsPreparedCaster(CharacterStats stats)
    {
        if (stats == null) return false;
        return stats.IsWizard || stats.IsCleric ||
               stats.HasClass("Druid") || stats.IsPaladin || stats.HasClass("Ranger");
    }

    /// <summary>
    /// Get the casting action type for a metamagic spell, considering caster type.
    /// D&D 3.5e PHB p.88: Spontaneous casters using metamagic take a full-round action
    /// (instead of standard action). Exception: Quicken Spell still results in a free action.
    /// </summary>
    public static SpellActionType GetMetamagicCastingAction(
        SpellData spell,
        MetamagicData metamagic,
        CharacterStats casterStats)
    {
        if (metamagic == null || !metamagic.HasAnyMetamagic)
            return spell.ActionType;

        // Quicken Spell always makes casting a free action, regardless of caster type
        if (metamagic.Has(MetamagicFeatId.QuickenSpell))
            return SpellActionType.Free;

        // Spontaneous casters (Sorcerer/Bard): metamagic → full-round action
        if (IsSpontaneousCaster(casterStats))
        {
            // Standard action spells become full-round
            if (spell.ActionType == SpellActionType.Standard)
                return SpellActionType.FullRound;

            // Full-round action spells become full-round + standard (not implemented as separate type;
            // in practice this means they take longer but we keep FullRound for now)
            return spell.ActionType;
        }

        // Prepared casters: no change to casting time (metamagic is applied during preparation)
        return spell.ActionType;
    }

    // ========================================================================
    // UTILITY METHODS
    // ========================================================================

    /// <summary>
    /// Get the maximum base spell level that can have a specific metamagic applied
    /// while still fitting in a 9th-level slot.
    /// </summary>
    public static int GetMaxBaseSpellLevel(MetamagicFeatId feat)
    {
        int adjustment = MetamagicData.GetStandardLevelAdjustment(feat);
        return MaxSpellLevel - adjustment;
    }

    /// <summary>
    /// Get available metamagic feats a character can apply to a specific spell.
    /// Filters by: character has feat, spell is compatible, result fits in 9th-level slot.
    /// </summary>
    public static List<MetamagicFeatId> GetAvailableMetamagics(
        SpellData spell,
        CharacterStats stats,
        MetamagicData currentMetamagic = null)
    {
        var available = new List<MetamagicFeatId>();
        if (spell == null || stats == null) return available;

        int currentIncrease = currentMetamagic?.GetTotalLevelAdjustment(spell.SpellLevel) ?? 0;

        foreach (var mmId in MetamagicData.AllMetamagicFeats)
        {
            // Skip if character doesn't have the feat
            string featName = MetamagicData.GetFeatName(mmId);
            if (!stats.HasFeat(featName)) continue;

            // Skip if already applied (no duplicates)
            if (currentMetamagic != null && currentMetamagic.Has(mmId)) continue;

            // Skip if not applicable to this spell
            if (!MetamagicData.IsApplicable(mmId, spell)) continue;

            // Skip if adding this would exceed 9th level
            int additionalIncrease = MetamagicData.GetStandardLevelAdjustment(mmId);
            if (spell.SpellLevel + currentIncrease + additionalIncrease > MaxSpellLevel) continue;

            available.Add(mmId);
        }

        return available;
    }

    /// <summary>
    /// Calculate the total slot level increase for a set of metamagic modifiers,
    /// excluding rod-applied metamagics.
    /// </summary>
    public static int CalculateTotalSlotIncrease(List<MetamagicModifier> modifiers)
    {
        if (modifiers == null) return 0;
        int total = 0;
        foreach (var mod in modifiers)
        {
            if (!mod.AppliedByRod)
                total += mod.SlotIncrease;
        }
        return total;
    }
}

/// <summary>
/// Result of a metamagic spell preparation through MetamagicSystem.
/// </summary>
public class MetamagicSpellResult
{
    /// <summary>Whether the preparation succeeded.</summary>
    public bool IsSuccess;

    /// <summary>Error message if preparation failed.</summary>
    public string ErrorMessage;

    /// <summary>The modified spell data (clone of original with metamagic applied).</summary>
    public SpellData ModifiedSpell;

    /// <summary>The effective spell level (for slot consumption).</summary>
    public int EffectiveSpellLevel;

    /// <summary>List of applied metamagic types.</summary>
    public List<MetamagicFeatId> AppliedMetamagics;

    /// <summary>Whether any metamagic was applied by a rod.</summary>
    public bool HasRodMetamagic;

    public static MetamagicSpellResult Success(SpellData spell, int effectiveLevel, List<MetamagicFeatId> applied, bool hasRod)
    {
        return new MetamagicSpellResult
        {
            IsSuccess = true,
            ModifiedSpell = spell,
            EffectiveSpellLevel = effectiveLevel,
            AppliedMetamagics = applied,
            HasRodMetamagic = hasRod
        };
    }

    public static MetamagicSpellResult Failure(string error)
    {
        return new MetamagicSpellResult
        {
            IsSuccess = false,
            ErrorMessage = error
        };
    }
}
