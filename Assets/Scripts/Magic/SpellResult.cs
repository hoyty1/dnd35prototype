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
    public int FightingDefensivelyAttackPenalty; // -4 while caster fights defensively
    public int ShootingIntoMeleePenalty;         // -4 when shooting into melee (unless negated)
    public bool PreciseShotNegated;
    public int TargetFightingDefensivelyACBonus; // +2 dodge AC on touch AC if active

    // ========== SAVING THROW ==========
    public bool RequiredSave;
    public string SaveType;             // "Reflex", "Will", "Fortitude"
    public int SaveDC;
    public int SaveRoll;                // Target's d20 roll
    public int SaveMod;                 // Target's save modifier
    public int SaveTotal;               // d20 + save modifier
    public bool SaveSucceeded;

    // ========== SPELL RESISTANCE / IMMUNITY ==========
    public bool SpellResistanceChecked;
    public int SpellResistanceValue;
    public int SpellResistanceRoll;
    public int SpellResistanceTotal;
    public bool SpellResistancePassed = true;
    public bool MindAffectingImmunityBlocked;
    public bool MindAffectingBlockedByProtection;
    public bool SummonedContactBlockedByProtection;
    public int ProtectionAcBonus;
    public int ProtectionSaveBonus;
    public string ProtectionSourceName;
    public string NoEffectReason;

    // ========== DAMAGE ==========
    public int DamageDealt;             // Final damage applied after save and mitigation
    public int DamageRolled;            // Raw damage before save reduction
    public string DamageType;           // Legacy display string ("cold", "acid", "force", etc.)
    public string DamageTypeSummary;    // Canonical typed summary used by mitigation model
    public int ResistancePrevented;
    public int DRPrevented;
    public bool ImmunityPrevented;
    public string MitigationSummary;
    // ========== HEALING ==========
    public int HealingDone;             // Lethal HP restored
    public int NonlethalHealed;         // Nonlethal damage removed
    public int HealRolled;              // Raw healing dice roll

    // ========== BUFF ==========
    public bool BuffApplied;
    public string BuffDescription;

    // ========== MAGIC MISSILE (special) ==========
    public int MissileCount;
    public int[] MissileDamages;        // Damage per missile
    public bool MagicMissileBlockedByShield;

    // ========== METAMAGIC ==========
    public MetamagicData Metamagic;       // Applied metamagic (null if none)
    public int EmpowerBonus;              // Extra damage/healing from Empower

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

        // Metamagic info
        if (Metamagic != null && Metamagic.HasAnyMetamagic)
        {
            string mmSummary = Metamagic.GetSummary(Spell.SpellLevel);
            sb.AppendLine($"  ⚡ {mmSummary}");
        }

        // Target
        if (Spell.TargetType == SpellTargetType.Self)
            sb.AppendLine($"  Target: Self");
        else
            sb.AppendLine($"  Target: {TargetName}");

        sb.AppendLine();

        // ========== MIND-AFFECTING IMMUNITY ==========
        if (MindAffectingImmunityBlocked)
        {
            sb.AppendLine("  Mind-affecting immunity: target is immune — spell has no effect.");
            sb.Append($"═══════════════════════════════════");
            return sb.ToString();
        }

        if (!string.IsNullOrWhiteSpace(NoEffectReason))
        {
            sb.AppendLine($"  {NoEffectReason}");
            if (MindAffectingBlockedByProtection)
                sb.AppendLine("  Protection from alignment: mental control is blocked.");
            if (SummonedContactBlockedByProtection)
                sb.AppendLine("  Protection from alignment barrier: summoned creature cannot make bodily contact.");
            sb.Append($"═══════════════════════════════════");
            return sb.ToString();
        }

        // ========== SPELL RESISTANCE ==========
        if (SpellResistanceChecked)
        {
            string srResult = SpellResistancePassed ? "PASSED" : "BLOCKED";
            sb.AppendLine($"  Spell Resistance: d20 {SpellResistanceRoll} + CL = {SpellResistanceTotal} vs SR {SpellResistanceValue} — {srResult}");
            sb.AppendLine();

            if (!SpellResistancePassed)
            {
                sb.Append($"═══════════════════════════════════");
                return sb.ToString();
            }
        }

        // ========== ATTACK ROLL (if required) ==========
        if (RequiredAttackRoll)
        {
            string touchType = IsRangedTouch ? "Ranged Touch Attack" : "Melee Touch Attack";
            sb.AppendLine($"  {touchType}:");
            sb.AppendLine($"    Roll: d20 = {AttackRoll}");
            if (AttackBonus != 0)
                sb.AppendLine($"    {FormatModLine(AttackBonus, "attack modifier")}");
            if (FightingDefensivelyAttackPenalty != 0)
                sb.AppendLine($"    {FormatModLine(FightingDefensivelyAttackPenalty, "Fighting Defensively")}");
            if (ShootingIntoMeleePenalty != 0)
                sb.AppendLine($"    {FormatModLine(ShootingIntoMeleePenalty, "shooting into melee")}");
            else if (PreciseShotNegated)
                sb.AppendLine("    + 0 (Precise Shot negates shooting into melee penalty)");
            if (TargetFightingDefensivelyACBonus > 0)
                sb.AppendLine($"    Defender stance: +{TargetFightingDefensivelyACBonus} Touch AC (Fighting Defensively)");
            if (ProtectionAcBonus > 0)
            {
                string source = string.IsNullOrEmpty(ProtectionSourceName) ? "Protection from Alignment" : ProtectionSourceName;
                sb.AppendLine($"    Defender ward: +{ProtectionAcBonus} Touch AC ({source})");
            }
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
            if (ProtectionSaveBonus > 0)
            {
                string source = string.IsNullOrEmpty(ProtectionSourceName) ? "Protection from Alignment" : ProtectionSourceName;
                sb.AppendLine($"    + {ProtectionSaveBonus} ({source} ward)");
            }
            string saveResult = SaveSucceeded ? "RESISTED!" : "FAILED!";
            sb.AppendLine($"    = {SaveTotal} vs DC {SaveDC} - {saveResult}");
            sb.AppendLine();
        }

        if (MagicMissileBlockedByShield)
        {
            sb.AppendLine("  Shield negates Magic Missile: all missiles are blocked.");
            sb.AppendLine($"  {TargetName}: {TargetHPBefore} → {TargetHPAfter} HP");
            sb.Append($"═══════════════════════════════════");
            return sb.ToString();
        }

        // ========== DAMAGE ==========
        if (Spell.EffectType == SpellEffectType.Damage && (AttackHit || !RequiredAttackRoll))
        {
            if (MissileCount > 0 && MissileDamages != null)
            {
                sb.AppendLine($"  Damage ({MissileCount} missiles):");
                for (int i = 0; i < MissileDamages.Length; i++)
                    sb.AppendLine($"    Missile {i + 1}: 1d{Spell.DamageDice}+{Spell.BonusDamage} = {MissileDamages[i]} {DamageType}");
                if (EmpowerBonus > 0)
                    sb.AppendLine($"    Empower: +{EmpowerBonus} (×1.5)");
                string dmgType = !string.IsNullOrEmpty(DamageTypeSummary) ? DamageTypeSummary : DamageType;
                sb.AppendLine($"    = {DamageRolled} raw {dmgType} damage");
                if (!string.IsNullOrEmpty(MitigationSummary))
                    sb.AppendLine($"    Mitigation: {MitigationSummary}");
                sb.AppendLine($"    = {DamageDealt} final damage");
            }
            else if (DamageDealt > 0 || DamageRolled > 0)
            {
                sb.AppendLine($"  Damage:");
                if (Spell.DamageCount > 0)
                    sb.AppendLine($"    {Spell.DamageCount}d{Spell.DamageDice} = {DamageRolled}");
                if (Spell.BonusDamage > 0 && Spell.DamageCount > 0)
                    sb.AppendLine($"    + {Spell.BonusDamage} bonus");

                if (EmpowerBonus > 0)
                    sb.AppendLine($"    Empower: +{EmpowerBonus} (×1.5)");
                int postSaveDamage = DamageRolled;
                if (RequiredSave && SaveSucceeded && Spell.SaveHalves)
                {
                    postSaveDamage = Mathf.Max(0, DamageRolled / 2);
                    sb.AppendLine($"    Save halves: {DamageRolled} → {postSaveDamage}");
                }
                string dmgType = !string.IsNullOrEmpty(DamageTypeSummary) ? DamageTypeSummary : DamageType;
                sb.AppendLine($"    = {postSaveDamage} raw {dmgType} damage");
                if (!string.IsNullOrEmpty(MitigationSummary))
                    sb.AppendLine($"    Mitigation: {MitigationSummary}");
                sb.AppendLine($"    = {DamageDealt} final damage");
            }
            else if (Spell.BonusDamage > 0 && Spell.DamageCount == 0)
            {
                string dmgType = !string.IsNullOrEmpty(DamageTypeSummary) ? DamageTypeSummary : DamageType;
                sb.AppendLine($"  Damage: {DamageRolled} raw {dmgType}");
                if (!string.IsNullOrEmpty(MitigationSummary))
                    sb.AppendLine($"    Mitigation: {MitigationSummary}");
                sb.AppendLine($"    = {DamageDealt} final damage");
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
            if (EmpowerBonus > 0)
                sb.AppendLine($"    Empower: +{EmpowerBonus} (×1.5)");
            sb.AppendLine($"    = {HealingDone} HP restored");
            if (NonlethalHealed > 0)
                sb.AppendLine($"    + {NonlethalHealed} nonlethal removed");
            sb.AppendLine($"  {TargetName}: {TargetHPBefore} → {TargetHPAfter} HP");
        }

        // ========== BUFF ==========
        if (BuffApplied)
        {
            bool isDebuffLog = !string.IsNullOrEmpty(BuffDescription) && BuffDescription.StartsWith("Debuff:");
            string label = isDebuffLog ? "DEBUFF APPLIED" : "BUFF APPLIED";
            sb.AppendLine($"  {label}! {BuffDescription}");
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