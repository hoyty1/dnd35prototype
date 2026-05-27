using DND35e.Identifiers;
using UnityEngine;

// ============================================================================
// ConcentrationService — Centralized concentration DC formulas and checks.
//
// D&D 3.5e PHB p.170: Concentration checks determine whether a spellcaster
// can maintain focus when distracted. The check is:
//   d20 + Concentration skill modifier  vs  DC (varies by distraction).
//
// This service centralizes the DC formulas that were duplicated across
// GameManager, AISpellcastingStrategist, ConcentrationManager, and
// GrappleSystem. It provides:
//   - Pure static DC calculators (no dependencies)
//   - Success chance estimation for AI / UI display
//   - Concentration bonus lookup helper
//   - Utility to check if a caster is maintaining a specific concentration spell
//
// The actual check ROLLING is still in ConcentrationManager (per-character
// MonoBehaviour with state), and the orchestration (CombatUI logging,
// Summon Swarm transitions, held charge handling) stays in GameManager.
// This service extracts the scattered FORMULAS into one canonical source.
//
// Replaces:
//   GameManager.CalculateDefensiveCastSuccessChancePercent()
//   AISpellcastingStrategist.DEFENSIVE_CAST_DC_BASE (15)
//   Inline "15 + spell.SpellLevel" in 5+ locations
//   Inline "10 + damage + spellLevel" in 3+ locations
//   Inline "20 + spell.SpellLevel" in GrappleSystem
//   IsCasterMaintainingSummonSwarmConcentration() in GameManager
// ============================================================================

/// <summary>
/// Static utility class for D&D 3.5e concentration DC formulas and checks.
/// All methods are pure — no GameManager or MonoBehaviour state required.
/// </summary>
public static class ConcentrationService
{
    // ════════════════════════════════════════════════════════════
    //  DC Constants (PHB p.170, Table 3-13)
    // ════════════════════════════════════════════════════════════

    /// <summary>Base DC for casting defensively (PHB p.170).</summary>
    public const int DEFENSIVE_CASTING_DC_BASE = 15;

    /// <summary>Base DC for injury/damage concentration checks (PHB p.170).</summary>
    public const int DAMAGE_DC_BASE = 10;

    /// <summary>Base DC for grappled/pinned concentration checks (PHB p.170).</summary>
    public const int GRAPPLED_DC_BASE = 20;

    /// <summary>Base DC for vigorous motion (PHB p.170).</summary>
    public const int VIGOROUS_MOTION_DC_BASE = 10;

    /// <summary>Base DC for violent motion (PHB p.170).</summary>
    public const int VIOLENT_MOTION_DC_BASE = 15;

    /// <summary>Base DC for entangled casting (PHB p.170).</summary>
    public const int ENTANGLED_DC_BASE = 15;

    /// <summary>
    /// Base DC for maintaining concentration while casting another spell (PHB p.170).
    /// DC = 15 + spell level of the NEW spell being cast.
    /// </summary>
    public const int CASTING_WHILE_CONCENTRATING_DC_BASE = 15;

    // ════════════════════════════════════════════════════════════
    //  DC Calculation Methods
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate the DC for casting a spell defensively.
    /// D&D 3.5e PHB p.170: DC = 15 + spell level.
    /// </summary>
    /// <param name="spellLevel">The level of the spell being cast.</param>
    /// <returns>The concentration DC.</returns>
    public static int GetDefensiveCastingDC(int spellLevel)
    {
        return DEFENSIVE_CASTING_DC_BASE + spellLevel;
    }

    /// <summary>
    /// Calculate the DC for maintaining concentration after taking damage.
    /// D&D 3.5e PHB p.170: DC = 10 + damage dealt + spell level.
    /// Used for both ongoing concentration spells and held touch charges.
    /// </summary>
    /// <param name="damageDealt">Amount of damage taken.</param>
    /// <param name="spellLevel">Level of the concentration spell or held spell.</param>
    /// <returns>The concentration DC.</returns>
    public static int GetDamageDC(int damageDealt, int spellLevel)
    {
        return DAMAGE_DC_BASE + damageDealt + spellLevel;
    }

    /// <summary>
    /// Calculate the DC for casting while grappled or pinned.
    /// D&D 3.5e PHB p.170: DC = 20 + spell level.
    /// </summary>
    /// <param name="spellLevel">The level of the spell being cast.</param>
    /// <returns>The concentration DC.</returns>
    public static int GetGrappledCastingDC(int spellLevel)
    {
        return GRAPPLED_DC_BASE + spellLevel;
    }

    /// <summary>
    /// Calculate the DC for maintaining concentration during vigorous motion.
    /// D&D 3.5e PHB p.170: DC = 10 + spell level.
    /// </summary>
    /// <param name="spellLevel">The level of the concentration spell.</param>
    /// <returns>The concentration DC.</returns>
    public static int GetVigorousMotionDC(int spellLevel)
    {
        return VIGOROUS_MOTION_DC_BASE + spellLevel;
    }

    /// <summary>
    /// Calculate the DC for maintaining concentration during violent motion.
    /// D&D 3.5e PHB p.170: DC = 15 + spell level.
    /// </summary>
    /// <param name="spellLevel">The level of the concentration spell.</param>
    /// <returns>The concentration DC.</returns>
    public static int GetViolentMotionDC(int spellLevel)
    {
        return VIOLENT_MOTION_DC_BASE + spellLevel;
    }

    /// <summary>
    /// Calculate the DC for casting while entangled.
    /// D&D 3.5e PHB p.170: DC = 15 + spell level.
    /// </summary>
    /// <param name="spellLevel">The level of the spell being cast.</param>
    /// <returns>The concentration DC.</returns>
    public static int GetEntangledCastingDC(int spellLevel)
    {
        return ENTANGLED_DC_BASE + spellLevel;
    }

    /// <summary>
    /// Calculate the DC for maintaining an existing concentration spell
    /// while casting a new (non-concentration) spell.
    /// D&D 3.5e PHB p.170: DC = 15 + spell level of the NEW spell.
    /// </summary>
    /// <param name="newSpellLevel">The level of the new spell being cast.</param>
    /// <returns>The concentration DC.</returns>
    public static int GetCastingWhileConcentratingDC(int newSpellLevel)
    {
        return CASTING_WHILE_CONCENTRATING_DC_BASE + newSpellLevel;
    }

    // ════════════════════════════════════════════════════════════
    //  Success Chance Estimation
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate the percentage chance of passing a concentration check.
    /// Used by AI for spell selection and by UI for player information.
    /// Clamped to 5%–95% (natural 1 always fails, natural 20 always succeeds).
    /// </summary>
    /// <param name="concentrationBonus">Total concentration modifier (skill + ability + misc).</param>
    /// <param name="dc">The concentration DC to beat.</param>
    /// <returns>Success percentage (5.0 to 95.0).</returns>
    public static float CalculateSuccessChancePercent(int concentrationBonus, int dc)
    {
        int requiredRoll = dc - concentrationBonus;
        float successChance = (21 - requiredRoll) / 20f * 100f;
        return Mathf.Clamp(successChance, 5f, 95f);
    }

    /// <summary>
    /// Calculate success chance as a 0–1 fraction (for AI scoring).
    /// </summary>
    /// <param name="concentrationBonus">Total concentration modifier.</param>
    /// <param name="dc">The concentration DC.</param>
    /// <returns>Success fraction (0.05 to 0.95).</returns>
    public static float CalculateSuccessChanceFraction(int concentrationBonus, int dc)
    {
        int requiredRoll = dc - concentrationBonus;
        return Mathf.Clamp01((21f - requiredRoll) / 20f);
    }

    // ════════════════════════════════════════════════════════════
    //  Concentration Bonus
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Get the concentration bonus for spellcasting checks.
    /// Delegates to CharacterStats.GetSpellcastingConcentrationBonus.
    /// Convenience wrapper for use when you only have a CharacterController.
    /// </summary>
    /// <param name="caster">The spellcaster.</param>
    /// <param name="includeCombatCasting">Whether to include Combat Casting feat bonus.</param>
    /// <returns>Total concentration bonus.</returns>
    public static int GetConcentrationBonus(CharacterController caster, bool includeCombatCasting = true)
    {
        if (caster == null || caster.Stats == null) return 0;
        return caster.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting);
    }

    // ════════════════════════════════════════════════════════════
    //  Concentration Spell Query Helpers
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if a caster is currently maintaining concentration on a specific spell.
    /// </summary>
    /// <param name="caster">The character to check.</param>
    /// <param name="spellId">The spell ID to check for (e.g., SpellNames.SUMMON_SWARM).</param>
    /// <returns>True if the caster is concentrating on the specified spell.</returns>
    public static bool IsConcentratingOnSpell(CharacterController caster, string spellId)
    {
        if (caster == null || string.IsNullOrEmpty(spellId)) return false;

        ConcentrationManager concMgr = caster.Concentration;
        if (concMgr == null || !concMgr.IsConcentrating
            || concMgr.ConcentratingOn == null || concMgr.ConcentratingOn.Spell == null)
            return false;

        return string.Equals(concMgr.ConcentratingOn.Spell.SpellId, spellId, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Check if a caster is maintaining concentration on any spell.
    /// </summary>
    /// <param name="caster">The character to check.</param>
    /// <returns>True if the caster is concentrating on any spell.</returns>
    public static bool IsConcentrating(CharacterController caster)
    {
        if (caster == null) return false;
        ConcentrationManager concMgr = caster.Concentration;
        return concMgr != null && concMgr.IsConcentrating;
    }

    /// <summary>
    /// Get the spell level of the spell currently being concentrated on.
    /// Returns 0 if not concentrating.
    /// </summary>
    public static int GetConcentrationSpellLevel(CharacterController caster)
    {
        if (caster == null) return 0;
        ConcentrationManager concMgr = caster.Concentration;
        return concMgr != null ? concMgr.ConcentrationSpellLevel : 0;
    }
}
