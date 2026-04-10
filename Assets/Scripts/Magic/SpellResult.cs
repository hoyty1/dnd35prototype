using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Holds the result of casting a spell, analogous to CombatResult for attacks.
/// Tracks all rolls, saves, damage/healing, and provides formatted combat log output.
/// </summary>
public class SpellResult
{
    public SpellData Spell;
    public string CasterName;
    public string TargetName;

    /// <summary>Whether the spell succeeded overall (e.g., touch attack hit).</summary>
    public bool Success = true;

    // ========== ATTACK (for spells requiring attack rolls) ==========
    public bool RequiredAttackRoll;
    public bool IsRangedTouch;          // True for ranged touch, false for melee touch
    public int AttackRoll;              // Natural d20
    public int AttackBonus;             // Modifier added to roll
    public int AttackTotal;             // d20 + modifier
    public int TouchAC;                 // Target's touch AC
    public bool AttackHit;              // Whether the touch attack hit

    // ========== SAVING THROW ==========
    public bool RequiredSave;
    public string SaveType;             // "Reflex", "Will", "Fortitude"
    public int SaveDC;
    public int SaveRoll;                // Target's d20 roll
    public int SaveMod;                 // Target's save modifier
    public int SaveTotal;               // d20 + save modifier
    public bool SaveSucceeded;

    // ========== DAMAGE ==========
    public int DamageDealt;             // Total damage (after save reduction if applicable)
    public int DamageRolled;            // Raw damage before save reduction
    public string DamageType;           // "cold", "acid", "force", etc.

    // ========== HEALING ==========
    public int HealingDone;
    public int HealRolled;              // Raw healing dice roll

    // ========== BUFF ==========
    public bool BuffApplied;
    public string BuffDescription;

    // ========== MAGIC MISSILE (special) ==========
    public int MissileCount;
    public int[] MissileDamages;        // Damage per missile

    // ========== HP TRACKING ==========
    public int TargetHPBefore;
    public int TargetHPAfter;
    public bool TargetKilled;

    /// <summary>
    /// Generate a formatted combat log string for display in the UI.
    /// Alias for GetDetailedSummary().
    /// </summary>
    public string GetFormattedLog() => GetDetailedSummary();

    /// <summary>
    /// Generate a detailed combat log for this spell cast.
    /// </summary>
    public string GetDetailedSummary()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"═══════════════════════════════════");
        sb.AppendLine($"✨ {CasterName} casts {Spell.Name}! - SPELL CAST!");

        // Spell info
        string levelStr = Spell.SpellLevel == 0 ? "Cantrip" : $"Level {Spell.SpellLevel}";
        sb.AppendLine($"  [{levelStr}] {Spell.School}");

        // Target
        if (Spell.TargetType == SpellTargetType.Self)
            sb.AppendLine($"  Target: Self");
        else
            sb.AppendLine($"  Target: {TargetName}");

        sb.AppendLine();

        // ========== ATTACK ROLL (if required) ==========
        if (RequiredAttackRoll)
        {
            string touchType = IsRangedTouch ? "Ranged Touch Attack" : "Melee Touch Attack";
            sb.AppendLine($"  {touchType}:");
            sb.AppendLine($"    Roll: d20 = {AttackRoll}");
            if (AttackBonus != 0)
                sb.AppendLine($"    {FormatModLine(AttackBonus, "attack modifier")}");
            string hitMiss = AttackHit ? "HIT!" : "MISS!";
            string natNote = AttackRoll == 20 ? " (NATURAL 20!)" : AttackRoll == 1 ? " (NATURAL 1!)" : "";
            sb.AppendLine($"    = {AttackTotal} vs Touch AC {TouchAC} - {hitMiss}{natNote}");
            sb.AppendLine();
        }

        // ========== SAVING THROW ==========
        if (RequiredSave && AttackHit)
        {
            sb.AppendLine($"  {SaveType} save (DC {SaveDC}):");
            sb.AppendLine($"    Roll: d20 = {SaveRoll}");
            if (SaveMod != 0)
                sb.AppendLine($"    {FormatModLine(SaveMod, SaveType)}");
            string saveResult = SaveSucceeded ? "RESISTED!" : "FAILED!";
            sb.AppendLine($"    = {SaveTotal} vs DC {SaveDC} - {saveResult}");
            sb.AppendLine();
        }

        // ========== DAMAGE ==========
        if (Spell.EffectType == SpellEffectType.Damage && (AttackHit || !RequiredAttackRoll))
        {
            if (MissileCount > 0 && MissileDamages != null)
            {
                sb.AppendLine($"  Damage ({MissileCount} missiles):");
                for (int i = 0; i < MissileDamages.Length; i++)
                    sb.AppendLine($"    Missile {i + 1}: 1d{Spell.DamageDice}+{Spell.BonusDamage} = {MissileDamages[i]} {DamageType}");
                sb.AppendLine($"    = {DamageDealt} total {DamageType} damage");
            }
            else if (DamageDealt > 0 || DamageRolled > 0)
            {
                sb.AppendLine($"  Damage:");
                if (Spell.DamageCount > 0)
                    sb.AppendLine($"    {Spell.DamageCount}d{Spell.DamageDice} = {DamageRolled}");
                if (Spell.BonusDamage > 0 && Spell.DamageCount > 0)
                    sb.AppendLine($"    + {Spell.BonusDamage} bonus");

                if (RequiredSave && SaveSucceeded && Spell.SaveHalves)
                    sb.AppendLine($"    Save halves: {DamageRolled} → {DamageDealt}");
                sb.AppendLine($"    = {DamageDealt} {DamageType} damage");
            }
            else if (Spell.BonusDamage > 0 && Spell.DamageCount == 0)
            {
                sb.AppendLine($"  Damage: {DamageDealt} {DamageType}");
            }

            sb.AppendLine($"  {TargetName}: {TargetHPBefore} → {TargetHPAfter} HP");
            if (TargetKilled)
                sb.AppendLine($"  {TargetName} has been slain!");
        }

        // ========== HEALING ==========
        if (Spell.EffectType == SpellEffectType.Healing)
        {
            sb.AppendLine($"  Healing: healed!");
            if (Spell.HealCount > 0)
                sb.AppendLine($"    {Spell.HealCount}d{Spell.HealDice} = {HealRolled}");
            if (Spell.BonusHealing > 0)
                sb.AppendLine($"    + {Spell.BonusHealing} (caster level)");
            sb.AppendLine($"    = {HealingDone} HP restored");
            sb.AppendLine($"  {TargetName}: {TargetHPBefore} → {TargetHPAfter} HP");
        }

        // ========== BUFF ==========
        if (BuffApplied)
        {
            sb.AppendLine($"  BUFF APPLIED! {BuffDescription}");
        }

        sb.Append($"═══════════════════════════════════");
        return sb.ToString();
    }

    private static string FormatModLine(int value, string label)
    {
        if (value >= 0)
            return $"+ {value} ({label})";
        else
            return $"- {-value} ({label})";
    }
}
