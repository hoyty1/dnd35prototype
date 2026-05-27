using System.Text;
using UnityEngine;

// ============================================================================
// SpellSaveResolver — Unified saving throw and spell resistance resolution
// for D&D 3.5e spell effects.
//
// Replaces duplicated 5-8 line save-resolution blocks across 20+ spell files.
// All saving throw resolution should go through this utility.
//
// Usage:
//   // Simple save check
//   var result = SpellSaveResolver.RollSave(target, SaveType.Will, dc);
//   result.AppendToLog(sb);
//   if (result.Saved) { /* reduce/negate effect */ }
//
//   // SR check
//   var srResult = SpellSaveResolver.RollSpellResistance(caster, target, casterLevel);
//   srResult.AppendToLog(sb);
//   if (!srResult.Overcame) { /* spell blocked */ }
//
//   // Combined: SR + Save in one call
//   var combo = SpellSaveResolver.ResolveSpellDefenses(caster, target, casterLevel, SaveType.Reflex, dc);
//   combo.AppendToLog(sb);
//   if (combo.BlockedBySR) { continue; }
//   if (combo.Saved) { damage /= 2; }
// ============================================================================

/// <summary>
/// Types of saving throws in D&D 3.5e.
/// </summary>
public enum SaveType
{
    Fortitude,
    Reflex,
    Will
}

/// <summary>
/// Result of a saving throw roll, with logging support.
/// </summary>
public struct SaveResult
{
    /// <summary>The type of save rolled (Fort/Ref/Will).</summary>
    public SaveType Type;

    /// <summary>The raw d20 roll result.</summary>
    public int Roll;

    /// <summary>The save modifier applied (e.g., target's Will save bonus).</summary>
    public int Modifier;

    /// <summary>Roll + Modifier.</summary>
    public int Total;

    /// <summary>The DC that needed to be met or exceeded.</summary>
    public int DC;

    /// <summary>True if Total >= DC.</summary>
    public bool Saved;

    /// <summary>Short display name for the save type ("Fort", "Reflex", "Will").</summary>
    public string TypeName => Type switch
    {
        SaveType.Fortitude => "Fort",
        SaveType.Reflex => "Reflex",
        SaveType.Will => "Will",
        _ => "Save"
    };

    /// <summary>
    /// Append a standard save result line to the combat log.
    /// Format: "  Will: d20(14) + 5 = 19 vs DC 17 → SAVED (negated)"
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="successText">Text shown on success (default: "SAVED").</param>
    /// <param name="failText">Text shown on failure (default: "FAILED").</param>
    public void AppendToLog(StringBuilder sb, string successText = null, string failText = null)
    {
        if (sb == null) return;
        string success = successText ?? "SAVED";
        string fail = failText ?? "FAILED";
        sb.AppendLine($"  {TypeName}: d20({Roll}) + {Modifier} = {Total} vs DC {DC} → {(Saved ? success : fail)}");
    }

    /// <summary>
    /// Append a save result line with standard "half" / "full" descriptors for Reflex-for-half spells.
    /// Format: "  Reflex save: d20(14) + 5 = 19 vs DC 17 → SAVED (half)"
    /// </summary>
    public void AppendHalfDamageLog(StringBuilder sb)
    {
        if (sb == null) return;
        sb.AppendLine($"  {TypeName} save: d20({Roll}) + {Modifier} = {Total} vs DC {DC} → {(Saved ? "SAVED (half)" : "FAILED (full)")}");
    }
}

/// <summary>
/// Result of a Spell Resistance check.
/// </summary>
public struct SRResult
{
    /// <summary>The raw d20 roll for the caster's SR check.</summary>
    public int Roll;

    /// <summary>The caster level used for the check.</summary>
    public int CasterLevel;

    /// <summary>Any bonus from Spell Penetration feats.</summary>
    public int PenetrationBonus;

    /// <summary>Roll + CasterLevel + PenetrationBonus.</summary>
    public int Total;

    /// <summary>The target's Spell Resistance value.</summary>
    public int TargetSR;

    /// <summary>True if Total >= TargetSR (spell gets through).</summary>
    public bool Overcame;

    /// <summary>True if the target had no SR and the check was skipped.</summary>
    public bool Skipped;

    /// <summary>
    /// Append a standard SR check line to the combat log.
    /// Format: "  SR Check: d20(14) + 7 = 21 vs SR 18 → OVERCAME SR"
    /// If skipped (no SR), appends nothing.
    /// </summary>
    public void AppendToLog(StringBuilder sb)
    {
        if (sb == null || Skipped) return;
        sb.AppendLine($"  SR Check: d20({Roll}) + {CasterLevel}{(PenetrationBonus > 0 ? $"+{PenetrationBonus}" : "")} = {Total} vs SR {TargetSR} → {(Overcame ? "OVERCAME SR" : "BLOCKED by SR")}");
    }
}

/// <summary>
/// Combined result of SR check + saving throw for a spell targeting a creature.
/// </summary>
public struct SpellDefenseResult
{
    /// <summary>The spell resistance check result (may be skipped if target has no SR).</summary>
    public SRResult SR;

    /// <summary>The saving throw result (only valid if not blocked by SR).</summary>
    public SaveResult Save;

    /// <summary>True if spell was blocked by Spell Resistance.</summary>
    public bool BlockedBySR;

    /// <summary>True if target saved against the spell effect.</summary>
    public bool Saved => !BlockedBySR && Save.Saved;

    /// <summary>True if the spell got through all defenses (passed SR and target failed save).</summary>
    public bool FullEffect => !BlockedBySR && !Save.Saved;

    /// <summary>
    /// Append both SR and save results to the combat log.
    /// </summary>
    public void AppendToLog(StringBuilder sb, string saveSuccessText = null, string saveFailText = null)
    {
        SR.AppendToLog(sb);
        if (!BlockedBySR)
            Save.AppendToLog(sb, saveSuccessText, saveFailText);
    }

    /// <summary>
    /// Append for Reflex-for-half spells (uses "SAVED (half)" / "FAILED (full)" format).
    /// </summary>
    public void AppendHalfDamageLog(StringBuilder sb)
    {
        SR.AppendToLog(sb);
        if (!BlockedBySR)
            Save.AppendHalfDamageLog(sb);
    }
}

/// <summary>
/// Centralized saving throw and spell resistance resolution for D&D 3.5e spells.
/// Eliminates duplicated save-resolution blocks across spell implementation files.
/// </summary>
public static class SpellSaveResolver
{
    // ════════════════════════════════════════════════════════════
    //  Saving Throw Resolution
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Roll a saving throw for a target against a spell DC.
    /// </summary>
    /// <param name="target">The character making the save.</param>
    /// <param name="type">Fort, Reflex, or Will.</param>
    /// <param name="dc">The spell DC to meet or exceed.</param>
    /// <returns>A SaveResult with all roll details.</returns>
    public static SaveResult RollSave(CharacterController target, SaveType type, int dc)
    {
        int roll = DiceRoller.D20();
        int modifier = GetSaveModifier(target, type);
        int total = roll + modifier;

        return new SaveResult
        {
            Type = type,
            Roll = roll,
            Modifier = modifier,
            Total = total,
            DC = dc,
            Saved = total >= dc
        };
    }

    /// <summary>
    /// Roll a saving throw using CharacterStats directly (for cases where CharacterController is unavailable).
    /// </summary>
    public static SaveResult RollSave(CharacterStats stats, SaveType type, int dc)
    {
        int roll = DiceRoller.D20();
        int modifier = GetSaveModifier(stats, type);
        int total = roll + modifier;

        return new SaveResult
        {
            Type = type,
            Roll = roll,
            Modifier = modifier,
            Total = total,
            DC = dc,
            Saved = total >= dc
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Spell Resistance Resolution
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Roll a Spell Resistance check: d20 + caster level + spell penetration vs target SR.
    /// Returns a skipped result if the target has no SR.
    /// </summary>
    /// <param name="caster">The spellcaster.</param>
    /// <param name="target">The target with potential SR.</param>
    /// <param name="casterLevel">The effective caster level for this spell.</param>
    /// <returns>An SRResult with all details. Check Skipped or Overcame.</returns>
    public static SRResult RollSpellResistance(CharacterController caster, CharacterController target, int casterLevel)
    {
        if (target == null || target.Stats == null || target.Stats.SpellResistance <= 0)
        {
            return new SRResult { Skipped = true, Overcame = true };
        }

        int roll = DiceRoller.D20();
        int penBonus = FeatManager.GetSpellPenetrationBonus(caster?.Stats);
        int total = roll + casterLevel + penBonus;
        int targetSR = target.Stats.SpellResistance;

        return new SRResult
        {
            Roll = roll,
            CasterLevel = casterLevel,
            PenetrationBonus = penBonus,
            Total = total,
            TargetSR = targetSR,
            Overcame = total >= targetSR,
            Skipped = false
        };
    }

    // ════════════════════════════════════════════════════════════
    //  Combined Defense Resolution (SR + Save)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolve both Spell Resistance and a saving throw in one call.
    /// If SR blocks the spell, the save is not rolled.
    /// </summary>
    /// <param name="caster">The spellcaster.</param>
    /// <param name="target">The target.</param>
    /// <param name="casterLevel">Effective caster level.</param>
    /// <param name="saveType">Type of saving throw (Fort/Ref/Will).</param>
    /// <param name="saveDC">The save DC.</param>
    /// <param name="spellAllowsSR">Whether the spell allows SR (default true).</param>
    /// <returns>A SpellDefenseResult with SR and save details.</returns>
    public static SpellDefenseResult ResolveSpellDefenses(
        CharacterController caster, CharacterController target,
        int casterLevel, SaveType saveType, int saveDC,
        bool spellAllowsSR = true)
    {
        var result = new SpellDefenseResult();

        // SR check
        if (spellAllowsSR)
        {
            result.SR = RollSpellResistance(caster, target, casterLevel);
            result.BlockedBySR = !result.SR.Overcame;
        }
        else
        {
            result.SR = new SRResult { Skipped = true, Overcame = true };
            result.BlockedBySR = false;
        }

        // Save (only if SR didn't block)
        if (!result.BlockedBySR)
        {
            result.Save = RollSave(target, saveType, saveDC);
        }

        return result;
    }

    // ════════════════════════════════════════════════════════════
    //  Evasion / Improved Evasion Helpers
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Apply Evasion/Improved Evasion to a Reflex-for-half damage result.
    /// D&D 3.5e PHB p.40: Evasion = no damage on successful Reflex save.
    /// Improved Evasion = half damage even on failed save.
    /// </summary>
    /// <param name="damage">The current damage value (already halved if save succeeded).</param>
    /// <param name="target">The target who may have Evasion.</param>
    /// <param name="saved">Whether the Reflex save was successful.</param>
    /// <param name="sb">StringBuilder for logging (optional).</param>
    /// <returns>The adjusted damage after Evasion/Improved Evasion.</returns>
    public static int ApplyEvasion(int damage, CharacterController target, bool saved, StringBuilder sb = null)
    {
        if (target?.Stats == null) return damage;

        if (saved && target.Stats.HasEvasion)
        {
            sb?.AppendLine($"  Evasion: no damage on successful save!");
            return 0;
        }

        // Improved Evasion: half damage on failed save (if target has it)
        if (!saved && target.Stats.HasImprovedEvasion)
        {
            int reduced = Mathf.Max(1, damage / 2);
            sb?.AppendLine($"  Improved Evasion: damage halved ({damage} → {reduced})!");
            return reduced;
        }

        return damage;
    }

    /// <summary>
    /// Apply Blink damage halving for area spells. PHB p.206.
    /// </summary>
    public static int ApplyBlinkHalving(int damage, CharacterController target, StringBuilder sb = null)
    {
        if (target != null && target.HasActiveBlinkEffect)
        {
            damage = Mathf.Max(0, damage / 2);
            sb?.AppendLine($"  Blink: area damage halved (target partially ethereal)");
        }
        return damage;
    }

    // ════════════════════════════════════════════════════════════
    //  DC Calculation Helper
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate a standard spell save DC: 10 + spell level + ability modifier.
    /// </summary>
    public static int CalculateDC(int spellLevel, int abilityModifier)
    {
        return 10 + spellLevel + abilityModifier;
    }

    // ════════════════════════════════════════════════════════════
    //  Internal Helpers
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Get the appropriate save modifier for a CharacterController.
    /// </summary>
    private static int GetSaveModifier(CharacterController target, SaveType type)
    {
        if (target?.Stats == null) return 0;
        return GetSaveModifier(target.Stats, type);
    }

    /// <summary>
    /// Get the appropriate save modifier from CharacterStats.
    /// </summary>
    private static int GetSaveModifier(CharacterStats stats, SaveType type)
    {
        if (stats == null) return 0;
        return type switch
        {
            SaveType.Fortitude => stats.FortitudeSave,
            SaveType.Reflex => stats.ReflexSave,
            SaveType.Will => stats.WillSave,
            _ => 0
        };
    }
}
