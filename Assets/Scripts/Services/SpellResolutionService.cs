using UnityEngine;

// ============================================================================
// D&D 3.5 Spell Resolution Service - Unified spell failure & resistance checks
// ============================================================================

/// <summary>
/// Centralized spell resolution service that unifies Blink spell failure,
/// targeted spell failure, and spell resistance checks for both PC and NPC
/// casting paths. Eliminates duplicated logic between PerformSpellCast()
/// and TryNPCPerformSpellCast() in GameManager.
/// </summary>
public static class SpellResolutionService
{
    // ========================================================================
    // RESULT STRUCTURES
    // ========================================================================

    /// <summary>
    /// Result of a spell resolution pre-check (before SpellCaster.Cast).
    /// </summary>
    public struct SpellPreCheckResult
    {
        /// <summary>Whether the spell should proceed to SpellCaster.Cast.</summary>
        public bool SpellProceeds;
        /// <summary>Whether the spell fizzled due to Blink caster failure.</summary>
        public bool BlinkCasterFailed;
        /// <summary>Whether the spell failed to reach the target due to Blink.</summary>
        public bool BlinkTargetFailed;
        /// <summary>The percentile roll for Blink caster check (1-100), or 0 if not checked.</summary>
        public int BlinkCasterRoll;
        /// <summary>The percentile roll for Blink target check (1-100), or 0 if not checked.</summary>
        public int BlinkTargetRoll;
        /// <summary>Combat log message describing the result (for display).</summary>
        public string LogMessage;
    }

    // ========================================================================
    // BLINK CASTER SPELL FAILURE (20%)
    // ========================================================================

    /// <summary>
    /// Check if a blinking caster's spell fizzles.
    /// D&amp;D 3.5e PHB p.206: Blinking caster has a 20% spell failure chance.
    /// The caster may be on the Ethereal Plane when the spell is cast.
    /// This is checked separately from (and in addition to) arcane spell failure from armor.
    /// </summary>
    /// <param name="caster">The character casting the spell.</param>
    /// <param name="spellName">Name of the spell being cast (for logging).</param>
    /// <param name="roll">Output: the percentile roll made (1-100), or 0 if no roll.</param>
    /// <returns>True if the spell fizzles (failed), false if it proceeds.</returns>
    public static bool TryBlinkCasterFailure(CharacterController caster, string spellName, out int roll)
    {
        roll = 0;

        if (caster == null || !caster.HasActiveBlinkEffect)
            return false;

        roll = DiceService.Percentile($"Blink caster spell failure ({caster.Stats.CharacterName})");

        if (roll <= 20)
        {
            Debug.Log($"[SpellResolution] Blink caster failure: {caster.Stats.CharacterName}'s {spellName} fizzles (rolled {roll} ≤ 20%)");
            return true;
        }

        Debug.Log($"[SpellResolution] Blink caster check passed: {caster.Stats.CharacterName} rolled {roll} > 20%");
        return false;
    }

    // ========================================================================
    // BLINK TARGET SPELL FAILURE (50%)
    // ========================================================================

    /// <summary>
    /// Check if a targeted spell fails to reach a blinking target.
    /// D&amp;D 3.5e PHB p.206: Individually targeted spells have a 50% chance
    /// to fail against a blinking creature (the target may be on the Ethereal Plane).
    /// Does NOT apply to area spells or self-targeted spells.
    /// </summary>
    /// <param name="caster">The character casting the spell.</param>
    /// <param name="target">The target of the spell.</param>
    /// <param name="spell">The spell being cast.</param>
    /// <param name="roll">Output: the percentile roll made (1-100), or 0 if no roll.</param>
    /// <returns>True if the spell fails to reach the target, false if it connects.</returns>
    public static bool TryBlinkTargetFailure(CharacterController caster, CharacterController target, SpellData spell, out int roll)
    {
        roll = 0;

        if (target == null || target == caster || !target.HasActiveBlinkEffect)
            return false;

        // Does not apply to self-targeted or area spells
        if (spell.TargetType == SpellTargetType.Self || spell.TargetType == SpellTargetType.Area)
            return false;

        string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;
        roll = DiceService.Percentile($"Blink target spell failure ({targetName})");

        if (roll <= 50)
        {
            Debug.Log($"[SpellResolution] Blink target failure: {spell.Name} fails to reach {targetName} (rolled {roll} ≤ 50%)");
            return true;
        }

        Debug.Log($"[SpellResolution] Blink target check passed: {targetName} rolled {roll} > 50%");
        return false;
    }

    // ========================================================================
    // COMBINED PRE-CAST CHECKS
    // ========================================================================

    /// <summary>
    /// Perform all pre-cast spell resolution checks in order:
    /// 1. Blink caster spell failure (20%)
    /// 2. Blink target spell failure (50%)
    /// 
    /// This method consolidates the duplicated logic from PerformSpellCast()
    /// and TryNPCPerformSpellCast() into a single call.
    /// 
    /// Note: Arcane spell failure from armor is NOT included here as it has
    /// different slot-consumption semantics handled by the caller.
    /// </summary>
    /// <param name="caster">The character casting the spell.</param>
    /// <param name="target">The target of the spell (may be null for area spells).</param>
    /// <param name="spell">The spell being cast.</param>
    /// <returns>Pre-check result indicating if the spell should proceed.</returns>
    public static SpellPreCheckResult RunPreCastChecks(CharacterController caster, CharacterController target, SpellData spell)
    {
        var result = new SpellPreCheckResult { SpellProceeds = true };
        string casterName = caster?.Stats?.CharacterName ?? "Unknown";
        string targetName = target?.Stats?.CharacterName ?? target?.name ?? "Unknown";

        // Step 1: Blink caster failure (20%)
        if (TryBlinkCasterFailure(caster, spell.Name, out int blinkCasterRoll))
        {
            result.SpellProceeds = false;
            result.BlinkCasterFailed = true;
            result.BlinkCasterRoll = blinkCasterRoll;
            result.LogMessage = CombatLogger.FormatBlinkCasterSpellFailure(casterName, spell.Name, blinkCasterRoll, true);
            return result;
        }
        else if (blinkCasterRoll > 0)
        {
            result.BlinkCasterRoll = blinkCasterRoll;
            // Log success but continue
            CombatLogger.Show(CombatLogger.FormatBlinkCasterSpellFailure(casterName, spell.Name, blinkCasterRoll, false));
        }

        // Step 2: Blink target failure (50%)
        if (TryBlinkTargetFailure(caster, target, spell, out int blinkTargetRoll))
        {
            result.SpellProceeds = false;
            result.BlinkTargetFailed = true;
            result.BlinkTargetRoll = blinkTargetRoll;
            result.LogMessage = CombatLogger.FormatBlinkTargetSpellFailure(spell.Name, targetName, blinkTargetRoll, true);
            return result;
        }
        else if (blinkTargetRoll > 0)
        {
            result.BlinkTargetRoll = blinkTargetRoll;
            CombatLogger.Show(CombatLogger.FormatBlinkTargetSpellFailure(spell.Name, targetName, blinkTargetRoll, false));
        }

        return result;
    }

    // ========================================================================
    // SPELL RESISTANCE CHECK (informational helper)
    // ========================================================================

    /// <summary>
    /// Perform a spell resistance check.
    /// D&amp;D 3.5e PHB p.177: Caster rolls d20 + caster level vs target's SR.
    /// Note: The actual SR check is primarily handled inside SpellCaster.Cast().
    /// This method is provided for cases where SR needs to be checked outside
    /// the standard SpellCaster.Cast pipeline (e.g. Hypnotism, Sleep, Color Spray).
    /// </summary>
    /// <param name="casterLevel">Effective caster level for the check.</param>
    /// <param name="spellResistance">Target's spell resistance value.</param>
    /// <param name="context">Description for dice logging.</param>
    /// <param name="roll">Output: the d20 roll.</param>
    /// <param name="total">Output: roll + caster level.</param>
    /// <returns>True if the caster overcomes SR (total >= SR).</returns>
    public static bool TryOvercomeSpellResistance(int casterLevel, int spellResistance, string context, out int roll, out int total)
    {
        roll = DiceService.D20(context);
        total = roll + casterLevel;
        return total >= spellResistance;
    }
}
