using UnityEngine;

// ============================================================================
// SpellCastingHelper — Centralized spell casting computation helpers.
//
// Extracted from GameManager partial classes to eliminate repeated inline
// caster level clamping, duration calculation, damage dice calculation,
// and spell resistance checking patterns.
//
// Complements SpellUtilities (DC, immunity) and SpellSaveResolver (saves, SR).
//
// Usage:
//   int cl = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
//   int dur = SpellCastingHelper.CalculateDuration(spell, cl);
//   int dice = SpellCastingHelper.GetDamageDiceCount(cl, 10);
//   var ctx = SpellCastingHelper.BuildContext(caster, spell);
// ============================================================================

/// <summary>
/// Static utility class for common spell casting computations.
/// Eliminates hundreds of repeated inline patterns in GameManager spell files:
///   - Caster level clamping (139 instances)
///   - Duration calculation (57 instances)
///   - Damage dice capping (12 instances)
///   - Spell preamble setup (casterLevel + saveDC + casterName)
/// </summary>
public static class SpellCastingHelper
{
    // ════════════════════════════════════════════════════════════
    //  Spell Cast Context — encapsulates the common spell preamble
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Pre-computed context for a spell cast, encapsulating the preamble
    /// that nearly every spell resolver computes inline.
    /// </summary>
    public struct SpellCastContext
    {
        /// <summary>Effective caster level (domain-boosted, minimum 1).</summary>
        public int CasterLevel;

        /// <summary>Spell save DC (10 + spell level + casting modifier).</summary>
        public int SaveDC;

        /// <summary>The caster's name for log messages.</summary>
        public string CasterName;

        /// <summary>The spell being cast.</summary>
        public SpellData Spell;

        /// <summary>The caster reference.</summary>
        public CharacterController Caster;

        /// <summary>
        /// Calculate spell duration in rounds (minimum 1).
        /// Convenience method using this context's caster level.
        /// </summary>
        public int DurationRounds =>
            Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(Spell, CasterLevel));

        /// <summary>
        /// Get the number of damage dice capped at a maximum (e.g., 1d6/CL, max 10d6).
        /// </summary>
        /// <param name="maxDice">Maximum number of dice (e.g., 10 for 10d6 cap).</param>
        /// <returns>Number of dice to roll, between 1 and maxDice.</returns>
        public int DamageDice(int maxDice) => GetDamageDiceCount(CasterLevel, maxDice);
    }

    /// <summary>
    /// Build a standard spell cast context from caster and spell.
    /// Replaces the repeated 3-line preamble:
    ///   int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
    ///   int saveDc = SpellUtilities.GetSpellSaveDC(caster, spell);
    ///   string casterName = caster.Stats.CharacterName;
    /// </summary>
    public static SpellCastContext BuildContext(CharacterController caster, SpellData spell)
    {
        int cl = GetEffectiveCasterLevel(caster, spell);
        return new SpellCastContext
        {
            CasterLevel = cl,
            SaveDC = SpellUtilities.GetSpellSaveDC(caster, spell),
            CasterName = caster != null && caster.Stats != null
                ? caster.Stats.CharacterName
                : "Unknown",
            Spell = spell,
            Caster = caster
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Caster Level
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Get the effective caster level for a spell, applying domain boosts
    /// and clamping to a minimum of 1.
    /// Replaces: Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell))
    /// </summary>
    /// <param name="caster">The spellcaster.</param>
    /// <param name="spell">The spell being cast (for domain boost check).</param>
    /// <returns>Effective caster level, minimum 1.</returns>
    public static int GetEffectiveCasterLevel(CharacterController caster, SpellData spell)
    {
        if (caster == null || caster.Stats == null)
            return 1;

        return Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
    }

    /// <summary>
    /// Get the base caster level (no domain boost), clamped to minimum 1.
    /// Replaces: Mathf.Max(1, caster.Stats.GetCasterLevel())
    /// </summary>
    public static int GetBaseCasterLevel(CharacterController caster)
    {
        if (caster == null || caster.Stats == null)
            return 1;

        return Mathf.Max(1, caster.Stats.GetCasterLevel());
    }

    /// <summary>
    /// Get the base caster level from stats directly, clamped to minimum 1.
    /// Replaces: Mathf.Max(1, stats.GetCasterLevel())
    /// </summary>
    public static int GetBaseCasterLevel(CharacterStats stats)
    {
        if (stats == null) return 1;
        return Mathf.Max(1, stats.GetCasterLevel());
    }

    // ════════════════════════════════════════════════════════════
    //  Duration Calculation
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate spell duration in rounds, clamped to minimum 1.
    /// Replaces: Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel))
    /// </summary>
    /// <param name="spell">The spell data (contains duration formula).</param>
    /// <param name="casterLevel">Effective caster level.</param>
    /// <returns>Duration in rounds, minimum 1.</returns>
    public static int CalculateDuration(SpellData spell, int casterLevel)
    {
        return Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
    }

    /// <summary>
    /// Calculate spell duration using caster and spell directly.
    /// Convenience overload that computes the caster level automatically.
    /// </summary>
    public static int CalculateDuration(CharacterController caster, SpellData spell)
    {
        int cl = GetEffectiveCasterLevel(caster, spell);
        return CalculateDuration(spell, cl);
    }

    // ════════════════════════════════════════════════════════════
    //  Damage Dice Calculation
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Get the number of damage dice for level-scaled spells (e.g., 1d6/CL, max 10d6).
    /// Replaces: Mathf.Clamp(casterLevel, 1, maxDice)
    /// </summary>
    /// <param name="casterLevel">Effective caster level.</param>
    /// <param name="maxDice">Maximum dice cap for this spell.</param>
    /// <returns>Number of dice to roll, between 1 and maxDice.</returns>
    public static int GetDamageDiceCount(int casterLevel, int maxDice)
    {
        return Mathf.Clamp(casterLevel, 1, maxDice);
    }

    /// <summary>
    /// Roll level-scaled spell damage (e.g., Fireball: 1d6/CL, max 10d6).
    /// Combines dice count capping with the actual roll.
    /// </summary>
    /// <param name="casterLevel">Effective caster level.</param>
    /// <param name="maxDice">Maximum number of dice.</param>
    /// <param name="dieSize">Size of each die (e.g., 6 for d6).</param>
    /// <param name="rollLabel">Label for DiceService log.</param>
    /// <returns>Total damage rolled.</returns>
    public static int RollSpellDamage(int casterLevel, int maxDice, int dieSize, string rollLabel)
    {
        int diceCount = GetDamageDiceCount(casterLevel, maxDice);
        return DiceService.RollMultiple(diceCount, dieSize, rollLabel);
    }

    // ════════════════════════════════════════════════════════════
    //  Spell Resistance Helpers
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if a target has spell resistance and attempt to overcome it.
    /// Returns true if the spell is blocked by SR, false if it penetrates (or no SR).
    /// Wraps SpellSaveResolver.RollSpellResistance for consistent handling.
    /// </summary>
    /// <param name="caster">The spellcaster.</param>
    /// <param name="target">The target creature.</param>
    /// <param name="casterLevel">Effective caster level for the SR check.</param>
    /// <param name="spellAllowsSR">Whether the spell allows SR (from SpellData).</param>
    /// <param name="result">The SR check result (valid even if SR doesn't apply).</param>
    /// <returns>True if spell is BLOCKED by SR; false if it penetrates or SR doesn't apply.</returns>
    public static bool IsBlockedBySpellResistance(
        CharacterController caster,
        CharacterController target,
        int casterLevel,
        bool spellAllowsSR,
        out SRResult result)
    {
        result = default;

        if (!spellAllowsSR || target == null || target.Stats == null || target.Stats.SpellResistance <= 0)
            return false;

        result = SpellSaveResolver.RollSpellResistance(caster, target, casterLevel);
        return !result.Overcame;
    }

    /// <summary>
    /// Simplified SR check: returns true if spell penetrates (or SR doesn't apply).
    /// </summary>
    public static bool PenetratesSpellResistance(
        CharacterController caster,
        CharacterController target,
        int casterLevel,
        bool spellAllowsSR)
    {
        return !IsBlockedBySpellResistance(caster, target, casterLevel, spellAllowsSR, out _);
    }
}
