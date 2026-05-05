using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Individual attack-roll modifier entry used for detailed combat log breakdowns.
/// </summary>
public struct AttackModifierBreakdownEntry
{
    public string Label;
    public int Value;

    public AttackModifierBreakdownEntry(string label, int value)
    {
        Label = label;
        Value = value;
    }
}

/// <summary>
/// Structured attack-roll breakdown used for compact combat-log formula output.
/// </summary>
public sealed class AttackRollBreakdown
{
    public int D20Roll;
    public int Total;
    public int TargetArmorClass;
    public bool Hit;
    public readonly List<AttackModifierBreakdownEntry> Modifiers = new List<AttackModifierBreakdownEntry>();
}

/// <summary>
/// Structured damage breakdown used for compact combat-log formula output.
/// </summary>
public sealed class DamageBreakdown
{
    public string SourceName = string.Empty;
    public string BaseDice = string.Empty;
    public int BaseRoll;
    public int AbilityModifier;
    public string AbilityLabel = string.Empty;
    public int EnhancementBonus;
    public int PowerAttackBonus;
    public int SneakAttackDice;
    public int SneakAttackRoll;
    public int CriticalMultiplier = 1;
    public int OtherBonus;
    public readonly List<AttackModifierBreakdownEntry> OtherBonuses = new List<AttackModifierBreakdownEntry>();
}

/// <summary>
/// Holds the result of a single attack action using D&D 3.5 mechanics.
/// </summary>
public class CombatResult
{
    public CharacterController Attacker;
    public CharacterController Defender;
    public int DieRoll;
    public int TotalRoll;
    public int TargetAC;
    public bool Hit;
    public int Damage;
    public bool TargetKilled;
    public bool IsAttackOfOpportunity;

    public bool NaturalTwenty;
    public bool NaturalOne;

    public bool IsFlanking;
    public int FlankingBonus;
    public string FlankingPartnerName;

    public bool SneakAttackApplied;
    public int SneakAttackDice;
    public int SneakAttackDamage;
    public bool SneakAttackByFlanking;
    public bool SneakAttackByDexDenied;
    public string SneakAttackTriggerReason = "";

    public bool IsCritThreat;
    public bool CritConfirmed;
    public int ConfirmationRoll;
    public int ConfirmationTotal;
    public int CritMultiplier;
    public int CritThreatMin;
    public string CritDamageDice;

    public int RacialAttackBonus;
    public int SizeAttackBonus;

    public int DamageModifier;
    public string DamageModifierDesc;

    public bool IsRangedAttack;
    public int RangeDistanceFeet;
    public int RangeDistanceSquares;
    public int RangeIncrementNumber;
    public int RangePenalty;
    public string WeaponName;

    public int PowerAttackValue;
    public int PowerAttackDamageBonus;
    public bool RapidShotActive;
    public bool PointBlankShotActive;
    public int WeaponFocusBonus;
    public int WeaponSpecBonus;
    public int WeaponEnhancementAttackBonus;
    public int WeaponEnhancementDamageBonus;
    public int CombatExpertisePenalty;
    public int FightingDefensivelyAttackPenalty;
    public int ShootingIntoMeleePenalty;
    public bool PreciseShotNegated;
    public int FightingDefensivelyACBonus;
    public int AidAnotherAttackBonus;
    public int AidAnotherTargetAcBonus;

    public AttackDamageMode AttackDamageMode = AttackDamageMode.Lethal;
    public int DamageModeAttackPenalty;
    public string DamageModePenaltySource = "";

    public int GrappleDexDeniedToAc;
    public string GrappleDexRuleNote = "";

    public int FeintDexDeniedToAc;
    public string FeintWindowNote = "";

    public int ProtectionDeflectionBonusToAc;
    public string ProtectionSourceName = "";
    public bool ProtectionSummonedBarrierBlocked;
    public string ProtectionBarrierNote = "";

    public int ConcealmentMissChance;
    public int ConcealmentRoll;
    public bool MissedDueToConcealment;
    public string ConcealmentDescription = "";

    public string SpecialAttackNote = "";

    public int BreakdownBAB;
    public int BreakdownAbilityMod;
    public string BreakdownAbilityName;
    public int BreakdownDualWieldPenalty;
    public bool IsDualWieldAttack;
    public bool IsOffHandAttack;
    public int WeaponNonProficiencyPenalty;
    public int ArmorNonProficiencyPenalty;
    public List<AttackModifierBreakdownEntry> AttackBuffDebuffModifiers = new List<AttackModifierBreakdownEntry>();

    /// <summary>Structured attack-roll data for compact formula-style combat log output.</summary>
    public AttackRollBreakdown AttackRollBreakdown = new AttackRollBreakdown();

    /// <summary>Structured damage-roll data for compact formula-style combat log output.</summary>
    public DamageBreakdown DamageRollBreakdown = new DamageBreakdown();

    public int BaseDamageRoll;
    public string BaseDamageDiceStr;
    public int FeatDamageBonus;

    public string DamageTypeSummary = "";
    public int RawTotalDamage;
    public int FinalDamageDealt;
    public int ResistancePrevented;
    public int DRPrevented;
    public bool ImmunityPrevented;
    public string MitigationSummary = "";

    public int DefenderHPBefore;
    public int DefenderHPAfter;

    public int TotalDamage => FinalDamageDealt > 0 || ImmunityPrevented || ResistancePrevented > 0 || DRPrevented > 0
        ? FinalDamageDealt
        : Damage + SneakAttackDamage;

    public string GetSummary() => GetDetailedSummary();

    public void AddAttackBuffDebuffModifier(string label, int value)
    {
        if (string.IsNullOrWhiteSpace(label) || value == 0)
            return;

        AttackBuffDebuffModifiers.Add(new AttackModifierBreakdownEntry(label.Trim(), value));
    }

    /// <summary>
    /// Rebuilds compact attack/damage breakdown objects from the canonical combat-result fields.
    /// </summary>
    public void RebuildBreakdownsFromComputedValues()
    {
        if (AttackRollBreakdown == null)
            AttackRollBreakdown = new AttackRollBreakdown();
        if (DamageRollBreakdown == null)
            DamageRollBreakdown = new DamageBreakdown();

        AttackRollBreakdown.D20Roll = DieRoll;
        AttackRollBreakdown.Total = TotalRoll;
        AttackRollBreakdown.TargetArmorClass = TargetAC;
        AttackRollBreakdown.Hit = Hit;
        AttackRollBreakdown.Modifiers.Clear();

        if (BreakdownBAB != 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("BAB", BreakdownBAB));
        if (BreakdownAbilityMod != 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry(string.IsNullOrWhiteSpace(BreakdownAbilityName) ? "STR" : BreakdownAbilityName, BreakdownAbilityMod));
        if (SizeAttackBonus != 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("size", SizeAttackBonus));
        if (IsFlanking && FlankingBonus != 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("flanking", FlankingBonus));
        if (RacialAttackBonus != 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("racial", RacialAttackBonus));
        if (PowerAttackValue > 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("Power Attack", -PowerAttackValue));
        if (RapidShotActive)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("Rapid Shot", -2));
        if (PointBlankShotActive)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("Point Blank Shot", 1));
        if (WeaponFocusBonus > 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("Weapon Focus", WeaponFocusBonus));
        if (WeaponEnhancementAttackBonus > 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("enhancement", WeaponEnhancementAttackBonus));
        if (CombatExpertisePenalty != 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("Combat Expertise", CombatExpertisePenalty));
        if (FightingDefensivelyAttackPenalty != 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("Fighting Defensively", FightingDefensivelyAttackPenalty));
        if (ShootingIntoMeleePenalty != 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("shooting into melee", ShootingIntoMeleePenalty));
        if (AidAnotherAttackBonus > 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("Aid Another", AidAnotherAttackBonus));
        if (DamageModeAttackPenalty != 0)
        {
            string sourceLabel = string.IsNullOrEmpty(DamageModePenaltySource) ? "damage mode" : DamageModePenaltySource;
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry(sourceLabel, DamageModeAttackPenalty));
        }
        if (IsRangedAttack && RangePenalty != 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("range", RangePenalty));
        if (IsDualWieldAttack && BreakdownDualWieldPenalty != 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry(IsOffHandAttack ? "off-hand" : "dual wield", BreakdownDualWieldPenalty));
        if (WeaponNonProficiencyPenalty != 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("weapon non-proficiency", WeaponNonProficiencyPenalty));
        if (ArmorNonProficiencyPenalty != 0)
            AttackRollBreakdown.Modifiers.Add(new AttackModifierBreakdownEntry("armor/shield non-proficiency", ArmorNonProficiencyPenalty));
        if (AttackBuffDebuffModifiers != null)
        {
            for (int i = 0; i < AttackBuffDebuffModifiers.Count; i++)
                AttackRollBreakdown.Modifiers.Add(AttackBuffDebuffModifiers[i]);
        }

        DamageRollBreakdown.SourceName = string.IsNullOrWhiteSpace(WeaponName) ? "Attack" : WeaponName;
        DamageRollBreakdown.BaseDice = string.IsNullOrWhiteSpace(BaseDamageDiceStr) ? "?" : BaseDamageDiceStr;
        DamageRollBreakdown.BaseRoll = BaseDamageRoll;
        DamageRollBreakdown.AbilityModifier = DamageModifier;
        DamageRollBreakdown.AbilityLabel = ResolveDamageAbilityLabel();
        DamageRollBreakdown.EnhancementBonus = WeaponEnhancementDamageBonus;
        DamageRollBreakdown.PowerAttackBonus = PowerAttackDamageBonus;
        DamageRollBreakdown.SneakAttackDice = SneakAttackDice;
        DamageRollBreakdown.SneakAttackRoll = SneakAttackDamage;
        DamageRollBreakdown.CriticalMultiplier = CritConfirmed ? (CritMultiplier > 1 ? CritMultiplier : 2) : 1;
        DamageRollBreakdown.OtherBonuses.Clear();
        DamageRollBreakdown.OtherBonus = 0;

        if (PointBlankShotActive)
            DamageRollBreakdown.OtherBonuses.Add(new AttackModifierBreakdownEntry("Point Blank Shot", 1));
        if (WeaponSpecBonus > 0)
            DamageRollBreakdown.OtherBonuses.Add(new AttackModifierBreakdownEntry("Weapon Spec", WeaponSpecBonus));

        int subtotalRaw = RawTotalDamage > 0 ? RawTotalDamage : (Damage + SneakAttackDamage);
        int known = BaseDamageRoll + DamageModifier + WeaponEnhancementDamageBonus + PowerAttackDamageBonus + SneakAttackDamage;
        if (PointBlankShotActive)
            known += 1;
        if (WeaponSpecBonus > 0)
            known += WeaponSpecBonus;

        int remainingOtherBonus = subtotalRaw - known;
        if (remainingOtherBonus != 0)
            DamageRollBreakdown.OtherBonuses.Add(new AttackModifierBreakdownEntry("other bonuses", remainingOtherBonus));

        for (int i = 0; i < DamageRollBreakdown.OtherBonuses.Count; i++)
            DamageRollBreakdown.OtherBonus += DamageRollBreakdown.OtherBonuses[i].Value;
    }

    private string ResolveDamageAbilityLabel()
    {
        string desc = DamageModifierDesc ?? string.Empty;
        if (desc.Contains("1.5× STR")) return "Str×1.5";
        if (desc.Contains("0.5× STR")) return "Str×0.5";
        if (desc.Contains("DEX")) return "Dex";
        if (desc.Contains("STR")) return "Str";
        return string.IsNullOrWhiteSpace(desc) ? "ability" : desc;
    }

    private static string FormatSignedLabel(int value, string label)
    {
        if (value == 0)
            return string.Empty;

        return value > 0
            ? $"+ {value} {label}"
            : $"- {-value} {label}";
    }

    private string BuildCompactAttackRollFormula()
    {
        RebuildBreakdownsFromComputedValues();

        var pieces = new List<string>
        {
            $"1d20({AttackRollBreakdown.D20Roll})"
        };

        for (int i = 0; i < AttackRollBreakdown.Modifiers.Count; i++)
        {
            AttackModifierBreakdownEntry mod = AttackRollBreakdown.Modifiers[i];
            if (mod.Value == 0)
                continue;

            pieces.Add(FormatSignedLabel(mod.Value, mod.Label));
        }

        string critSuffix = NaturalTwenty ? " (NAT 20)" : NaturalOne ? " (NAT 1)" : string.Empty;
        string actorPrefix = Attacker != null && Attacker.Stats != null && Defender != null && Defender.Stats != null
            ? $"{Attacker.Stats.CharacterName} attacks {Defender.Stats.CharacterName}: "
            : string.Empty;

        string summary = actorPrefix
            + string.Join(" ", pieces)
            + $" = {AttackRollBreakdown.Total} vs AC {AttackRollBreakdown.TargetArmorClass} - {(AttackRollBreakdown.Hit ? "HIT!" : "MISS!")}{critSuffix}";
        return summary.Trim();
    }

    private string BuildCompactDamageFormula()
    {
        RebuildBreakdownsFromComputedValues();

        var pieces = new List<string>
        {
            $"{DamageRollBreakdown.BaseDice}({DamageRollBreakdown.BaseRoll})"
        };

        bool showZeroAbilityLine = DamageRollBreakdown.AbilityModifier == 0
            && !string.IsNullOrWhiteSpace(DamageModifierDesc)
            && DamageModifierDesc.IndexOf("no STR", StringComparison.OrdinalIgnoreCase) >= 0;

        if (DamageRollBreakdown.AbilityModifier != 0)
            pieces.Add(FormatSignedLabel(DamageRollBreakdown.AbilityModifier, DamageRollBreakdown.AbilityLabel));
        else if (showZeroAbilityLine)
            pieces.Add("+ 0 no STR modifier");
        if (DamageRollBreakdown.EnhancementBonus != 0)
            pieces.Add(FormatSignedLabel(DamageRollBreakdown.EnhancementBonus, "enhancement"));
        if (DamageRollBreakdown.PowerAttackBonus != 0)
            pieces.Add(FormatSignedLabel(DamageRollBreakdown.PowerAttackBonus, "Power Attack"));

        for (int i = 0; i < DamageRollBreakdown.OtherBonuses.Count; i++)
        {
            AttackModifierBreakdownEntry other = DamageRollBreakdown.OtherBonuses[i];
            pieces.Add(FormatSignedLabel(other.Value, other.Label));
        }

        string sneakSuffix = string.Empty;
        if (DamageRollBreakdown.SneakAttackDice > 0 && DamageRollBreakdown.SneakAttackRoll > 0)
            sneakSuffix = $" + {DamageRollBreakdown.SneakAttackDice}d6({DamageRollBreakdown.SneakAttackRoll}) sneak attack";

        int shownDamage = TotalDamage;
        string coreExpression = string.Join(" ", pieces).Trim();

        if (CritConfirmed)
            return $"CRITICAL! {DamageRollBreakdown.SourceName}: [{coreExpression}] ×{DamageRollBreakdown.CriticalMultiplier}{sneakSuffix} = {shownDamage} damage";

        return $"{DamageRollBreakdown.SourceName}: {coreExpression}{sneakSuffix} = {shownDamage} damage";
    }

    public string GetDetailedSummary()
    {
        var sb = new StringBuilder();
        string attackerName = Attacker.Stats.CharacterName;
        string defenderName = Defender.Stats.CharacterName;

        string weaponNote = !string.IsNullOrEmpty(WeaponName) ? $" with {WeaponName}" : "";
        string attackType = IsRangedAttack ? "ranged" : "melee";
        string aooNote = IsAttackOfOpportunity ? " [ATTACK OF OPPORTUNITY]" : "";

        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"{attackerName} attacks {defenderName}{weaponNote} ({attackType}){aooNote}");

        if (!string.IsNullOrEmpty(WeaponName))
            sb.AppendLine($"  Weapon: {WeaponName} ({attackType})");

        string damageModeLabel = AttackDamageMode == AttackDamageMode.Nonlethal ? "Nonlethal" : "Lethal";
        sb.AppendLine($"  Damage Mode: {damageModeLabel}");
        if (DamageModeAttackPenalty != 0)
        {
            string sourceLabel = string.IsNullOrEmpty(DamageModePenaltySource) ? "damage mode" : DamageModePenaltySource;
            sb.AppendLine($"  Damage Mode Penalty: {FormatModLine(DamageModeAttackPenalty, sourceLabel)}");
        }

        if (IsRangedAttack)
        {
            string penaltyStr = RangePenalty == 0 ? "no penalty" : $"{RangePenalty} penalty";
            sb.AppendLine($"  Range: {RangeDistanceFeet} ft ({RangeDistanceSquares} squares) - Increment {RangeIncrementNumber}, {penaltyStr}");
        }

        var activeFeats = new List<string>();
        if (PowerAttackValue > 0) activeFeats.Add($"Power Attack (-{PowerAttackValue} atk/+{PowerAttackDamageBonus} dmg)");
        if (RapidShotActive) activeFeats.Add("Rapid Shot (-2 all attacks)");
        if (PointBlankShotActive) activeFeats.Add("Point Blank Shot (+1 atk/+1 dmg)");
        if (WeaponFocusBonus > 0) activeFeats.Add($"Weapon Focus (+{WeaponFocusBonus} atk)");
        if (WeaponSpecBonus > 0) activeFeats.Add($"Weapon Spec (+{WeaponSpecBonus} dmg)");
        if (WeaponEnhancementAttackBonus > 0 || WeaponEnhancementDamageBonus > 0)
        {
            string attackText = WeaponEnhancementAttackBonus > 0 ? $"+{WeaponEnhancementAttackBonus} atk" : null;
            string damageText = WeaponEnhancementDamageBonus > 0 ? $"+{WeaponEnhancementDamageBonus} dmg" : null;
            string combined = attackText;
            if (!string.IsNullOrEmpty(damageText))
                combined = string.IsNullOrEmpty(combined) ? damageText : $"{combined}/{damageText}";
            activeFeats.Add($"Weapon Enhancement ({combined})");
        }
        if (CombatExpertisePenalty != 0) activeFeats.Add($"Combat Expertise ({CombatExpertisePenalty} atk/+{-CombatExpertisePenalty} AC)");
        if (FightingDefensivelyAttackPenalty != 0) activeFeats.Add($"Fighting Defensively ({FightingDefensivelyAttackPenalty} atk/+2 AC)");
        if (ShootingIntoMeleePenalty != 0) activeFeats.Add($"Shooting into melee ({ShootingIntoMeleePenalty} atk)");
        if (PreciseShotNegated) activeFeats.Add("Precise Shot (no shooting-into-melee penalty)");
        if (AidAnotherAttackBonus > 0) activeFeats.Add($"Aid Another (+{AidAnotherAttackBonus} atk)");
        if (AidAnotherTargetAcBonus > 0) activeFeats.Add($"Target Aided (+{AidAnotherTargetAcBonus} AC)");
        if (AttackDamageMode == AttackDamageMode.Nonlethal) activeFeats.Add("Nonlethal attack mode");
        if (activeFeats.Count > 0)
            sb.AppendLine($"  Active Feats: {string.Join(", ", activeFeats)}");

        if (IsFlanking)
            sb.AppendLine($"  Flanking: Yes (with {FlankingPartnerName}, +{FlankingBonus})");
        if (FightingDefensivelyACBonus != 0)
            sb.AppendLine($"  Defender stance: Fighting Defensively (+{FightingDefensivelyACBonus} AC)");
        if (AidAnotherTargetAcBonus > 0)
            sb.AppendLine($"  Defender aided: +{AidAnotherTargetAcBonus} AC vs this attack");
        if (GrappleDexDeniedToAc > 0)
            sb.AppendLine($"  Grapple: defender loses +{GrappleDexDeniedToAc} DEX bonus to AC vs non-grappled attacker");
        else if (!string.IsNullOrEmpty(GrappleDexRuleNote))
            sb.AppendLine($"  Grapple: {GrappleDexRuleNote}");
        if (FeintDexDeniedToAc > 0)
            sb.AppendLine($"  Feint: defender loses +{FeintDexDeniedToAc} DEX bonus to AC");
        else if (!string.IsNullOrEmpty(FeintWindowNote))
            sb.AppendLine($"  Feint: {FeintWindowNote}");
        if (ProtectionDeflectionBonusToAc > 0)
        {
            string sourceName = string.IsNullOrEmpty(ProtectionSourceName) ? "Protection from Alignment" : ProtectionSourceName;
            sb.AppendLine($"  {sourceName}: +{ProtectionDeflectionBonusToAc} deflection AC vs this attacker");
            sb.AppendLine("  COMPARISON: A non-opposed attacker making the same roll would target the defender's normal AC.");
        }
        if (ProtectionSummonedBarrierBlocked && !string.IsNullOrEmpty(ProtectionBarrierNote))
            sb.AppendLine($"  Barrier: {ProtectionBarrierNote}");
        if (!string.IsNullOrEmpty(SpecialAttackNote))
            sb.AppendLine($"  Special: {SpecialAttackNote}");

        sb.AppendLine();
        sb.AppendLine("  Attack Roll:");
        sb.AppendLine($"    <color=#8FD3FF>{BuildCompactAttackRollFormula()}</color>");
        sb.AppendLine($"    Roll: d20 = {DieRoll}");

        string abilityName = !string.IsNullOrEmpty(BreakdownAbilityName) ? BreakdownAbilityName : "STR";
        if (BreakdownBAB != 0) sb.AppendLine($"    {FormatModLine(BreakdownBAB, "base attack bonus")}");
        if (BreakdownAbilityMod != 0) sb.AppendLine($"    {FormatModLine(BreakdownAbilityMod, abilityName)}");
        if (SizeAttackBonus != 0) sb.AppendLine($"    {FormatModLine(SizeAttackBonus, "size")}");
        if (IsFlanking && FlankingBonus != 0) sb.AppendLine($"    {FormatModLine(FlankingBonus, "flanking")}");
        if (RacialAttackBonus != 0) sb.AppendLine($"    {FormatModLine(RacialAttackBonus, "racial")}");
        if (PowerAttackValue > 0) sb.AppendLine($"    {FormatModLine(-PowerAttackValue, "Power Attack")}");
        if (RapidShotActive) sb.AppendLine($"    {FormatModLine(-2, "Rapid Shot")}");
        if (PointBlankShotActive) sb.AppendLine($"    {FormatModLine(1, "Point Blank Shot")}");
        if (WeaponFocusBonus > 0) sb.AppendLine($"    {FormatModLine(WeaponFocusBonus, "Weapon Focus")}");
        if (WeaponEnhancementAttackBonus > 0) sb.AppendLine($"    {FormatModLine(WeaponEnhancementAttackBonus, "weapon enhancement")}");
        if (CombatExpertisePenalty != 0) sb.AppendLine($"    {FormatModLine(CombatExpertisePenalty, "Combat Expertise")}");
        if (FightingDefensivelyAttackPenalty != 0) sb.AppendLine($"    {FormatModLine(FightingDefensivelyAttackPenalty, "Fighting Defensively")}");
        if (ShootingIntoMeleePenalty != 0) sb.AppendLine($"    {FormatModLine(ShootingIntoMeleePenalty, "shooting into melee")}");
        if (PreciseShotNegated) sb.AppendLine("    + 0 (Precise Shot negates shooting into melee penalty)");
        if (AidAnotherAttackBonus > 0) sb.AppendLine($"    {FormatModLine(AidAnotherAttackBonus, "Aid Another")}");
        if (DamageModeAttackPenalty != 0)
        {
            string sourceLabel = string.IsNullOrEmpty(DamageModePenaltySource) ? "damage mode" : DamageModePenaltySource;
            sb.AppendLine($"    {FormatModLine(DamageModeAttackPenalty, sourceLabel)}");
        }
        if (IsRangedAttack && RangePenalty != 0) sb.AppendLine($"    {FormatModLine(RangePenalty, "range")}");
        if (IsDualWieldAttack && BreakdownDualWieldPenalty != 0)
            sb.AppendLine($"    {FormatModLine(BreakdownDualWieldPenalty, IsOffHandAttack ? "off-hand penalty" : "dual wield penalty")}");
        if (WeaponNonProficiencyPenalty != 0)
            sb.AppendLine($"    {FormatModLine(WeaponNonProficiencyPenalty, "weapon non-proficiency")}");
        if (ArmorNonProficiencyPenalty != 0)
            sb.AppendLine($"    {FormatModLine(ArmorNonProficiencyPenalty, "armor/shield non-proficiency")}");

        if (AttackBuffDebuffModifiers != null && AttackBuffDebuffModifiers.Count > 0)
        {
            sb.AppendLine("    Buffs/Debuffs:");
            for (int i = 0; i < AttackBuffDebuffModifiers.Count; i++)
            {
                AttackModifierBreakdownEntry entry = AttackBuffDebuffModifiers[i];
                sb.AppendLine($"      {FormatModLine(entry.Value, entry.Label)}");
            }
        }

        string critNote = NaturalTwenty ? " (NATURAL 20!)" : NaturalOne ? " (NATURAL 1!)" : "";
        sb.AppendLine($"    = {TotalRoll} vs AC {TargetAC} - {(Hit ? "HIT!" : "MISS!")}{critNote}");

        if (ConcealmentMissChance > 0)
        {
            string concealDesc = string.IsNullOrWhiteSpace(ConcealmentDescription)
                ? "Concealment"
                : ConcealmentDescription;
            string concealmentOutcome = MissedDueToConcealment ? "MISS!" : "HIT!";
            sb.AppendLine($"  Concealment check: 1d100({ConcealmentRoll}) - {concealmentOutcome} ({concealDesc})");
        }

        if (IsCritThreat)
        {
            string threatRange = CritThreatMin < 20 ? $"{CritThreatMin}-20" : "20";
            string confModStr = CharacterStats.FormatMod(ConfirmationTotal - ConfirmationRoll);
            if (CritConfirmed)
                sb.AppendLine($"  Confirmation: d20 = {ConfirmationRoll} {confModStr} = {ConfirmationTotal} vs AC {TargetAC} - CONFIRMED! (×{CritMultiplier})");
            else
                sb.AppendLine($"  Confirmation: d20 = {ConfirmationRoll} {confModStr} = {ConfirmationTotal} vs AC {TargetAC} - Not confirmed");
        }

        if (Hit)
        {
            sb.AppendLine();
            sb.AppendLine("  Damage:");
            sb.AppendLine($"    <b><color=#FFF29A>{BuildCompactDamageFormula()}</color></b>");
            string diceStr = !string.IsNullOrEmpty(BaseDamageDiceStr) ? BaseDamageDiceStr : "?";

            if (CritConfirmed)
                sb.AppendLine($"    {CritDamageDice} = {Damage - FeatDamageBonus} (weapon + mods)");
            else
            {
                sb.AppendLine($"    {diceStr} = {BaseDamageRoll}");
                if (DamageModifier != 0)
                    sb.AppendLine($"    {FormatModLine(DamageModifier, string.IsNullOrEmpty(DamageModifierDesc) ? abilityName : DamageModifierDesc)}");
                else if (!string.IsNullOrWhiteSpace(DamageModifierDesc) && DamageModifierDesc.IndexOf("no STR", StringComparison.OrdinalIgnoreCase) >= 0)
                    sb.AppendLine("    + 0 (no STR modifier)");
            }

            if (PowerAttackDamageBonus > 0) sb.AppendLine($"    {FormatModLine(PowerAttackDamageBonus, "Power Attack")}");
            if (PointBlankShotActive) sb.AppendLine($"    {FormatModLine(1, "Point Blank Shot")}");
            if (WeaponSpecBonus > 0) sb.AppendLine($"    {FormatModLine(WeaponSpecBonus, "Weapon Spec")}");
            if (WeaponEnhancementDamageBonus > 0) sb.AppendLine($"    {FormatModLine(WeaponEnhancementDamageBonus, "weapon enhancement")}");

            if (SneakAttackApplied)
            {
                string trigger = string.IsNullOrEmpty(SneakAttackTriggerReason) ? "" : $" [{SneakAttackTriggerReason}]";
                sb.AppendLine($"    Includes sneak attack: +{SneakAttackDamage} ({SneakAttackDice}d6){trigger}");
            }
            int rawSubtotal = RawTotalDamage > 0 ? RawTotalDamage : (Damage + SneakAttackDamage);
            sb.AppendLine($"    = {rawSubtotal} raw damage{(string.IsNullOrEmpty(DamageTypeSummary) ? "" : $" ({DamageTypeSummary})")}");
            if (!string.IsNullOrEmpty(MitigationSummary)) sb.AppendLine($"    Mitigation: {MitigationSummary}");
            sb.AppendLine($"    = {TotalDamage} final damage");

            if (DefenderHPBefore > 0 || DefenderHPAfter >= 0)
                sb.AppendLine($"  {Defender.Stats.CharacterName}: {DefenderHPBefore} → {DefenderHPAfter} HP");
            if (TargetKilled)
                sb.AppendLine($"  {Defender.Stats.CharacterName} has been slain!");
        }

        sb.Append("═══════════════════════════════════");
        return sb.ToString();
    }

    public string GetAttackBreakdown(string label)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  {label}:");
        sb.AppendLine($"    <color=#8FD3FF>{BuildCompactAttackRollFormula()}</color>");
        sb.AppendLine($"    Roll: d20 = {DieRoll}");
        sb.AppendLine($"      Damage mode: {(AttackDamageMode == AttackDamageMode.Nonlethal ? "Nonlethal" : "Lethal")}");
        if (GrappleDexDeniedToAc > 0)
            sb.AppendLine($"      Defender grappled: -{GrappleDexDeniedToAc} DEX to AC vs this attacker");
        else if (!string.IsNullOrEmpty(GrappleDexRuleNote))
            sb.AppendLine($"      {GrappleDexRuleNote}");
        if (ProtectionDeflectionBonusToAc > 0)
        {
            string sourceName = string.IsNullOrEmpty(ProtectionSourceName) ? "Protection from Alignment" : ProtectionSourceName;
            sb.AppendLine($"      {sourceName}: +{ProtectionDeflectionBonusToAc} deflection AC");
        }
        if (ProtectionSummonedBarrierBlocked && !string.IsNullOrEmpty(ProtectionBarrierNote))
            sb.AppendLine($"      Barrier: {ProtectionBarrierNote}");
        if (!string.IsNullOrEmpty(SpecialAttackNote))
            sb.AppendLine($"      Special: {SpecialAttackNote}");

        if (BreakdownBAB != 0) sb.AppendLine($"      {FormatModLine(BreakdownBAB, "BAB")}");
        if (BreakdownAbilityMod != 0) sb.AppendLine($"      {FormatModLine(BreakdownAbilityMod, string.IsNullOrEmpty(BreakdownAbilityName) ? "STR" : BreakdownAbilityName)}");
        if (SizeAttackBonus != 0) sb.AppendLine($"      {FormatModLine(SizeAttackBonus, "size")}");
        if (IsFlanking && FlankingBonus != 0) sb.AppendLine($"      {FormatModLine(FlankingBonus, "flanking")}");
        if (RacialAttackBonus != 0) sb.AppendLine($"      {FormatModLine(RacialAttackBonus, "racial")}");
        if (PowerAttackValue > 0) sb.AppendLine($"      {FormatModLine(-PowerAttackValue, "Power Attack")}");
        if (RapidShotActive) sb.AppendLine($"      {FormatModLine(-2, "Rapid Shot")}");
        if (PointBlankShotActive) sb.AppendLine($"      {FormatModLine(1, "Point Blank Shot")}");
        if (WeaponFocusBonus > 0) sb.AppendLine($"      {FormatModLine(WeaponFocusBonus, "Weapon Focus")}");
        if (WeaponEnhancementAttackBonus > 0) sb.AppendLine($"      {FormatModLine(WeaponEnhancementAttackBonus, "weapon enhancement")}");
        if (CombatExpertisePenalty != 0) sb.AppendLine($"      {FormatModLine(CombatExpertisePenalty, "Combat Expertise")}");
        if (FightingDefensivelyAttackPenalty != 0) sb.AppendLine($"      {FormatModLine(FightingDefensivelyAttackPenalty, "Fighting Defensively")}");
        if (ShootingIntoMeleePenalty != 0) sb.AppendLine($"      {FormatModLine(ShootingIntoMeleePenalty, "shooting into melee")}");
        if (PreciseShotNegated) sb.AppendLine("      + 0 (Precise Shot negates shooting into melee penalty)");
        if (AidAnotherAttackBonus > 0) sb.AppendLine($"      {FormatModLine(AidAnotherAttackBonus, "Aid Another")}");
        if (DamageModeAttackPenalty != 0)
        {
            string sourceLabel = string.IsNullOrEmpty(DamageModePenaltySource) ? "damage mode" : DamageModePenaltySource;
            sb.AppendLine($"      {FormatModLine(DamageModeAttackPenalty, sourceLabel)}");
        }
        if (IsRangedAttack && RangePenalty != 0) sb.AppendLine($"      {FormatModLine(RangePenalty, "range")}");
        if (IsDualWieldAttack && BreakdownDualWieldPenalty != 0)
            sb.AppendLine($"      {FormatModLine(BreakdownDualWieldPenalty, IsOffHandAttack ? "off-hand penalty" : "dual wield penalty")}");
        if (WeaponNonProficiencyPenalty != 0)
            sb.AppendLine($"      {FormatModLine(WeaponNonProficiencyPenalty, "weapon non-proficiency")}");
        if (ArmorNonProficiencyPenalty != 0)
            sb.AppendLine($"      {FormatModLine(ArmorNonProficiencyPenalty, "armor/shield non-proficiency")}");

        if (AttackBuffDebuffModifiers != null && AttackBuffDebuffModifiers.Count > 0)
        {
            sb.AppendLine("      Buffs/Debuffs:");
            for (int i = 0; i < AttackBuffDebuffModifiers.Count; i++)
            {
                AttackModifierBreakdownEntry entry = AttackBuffDebuffModifiers[i];
                sb.AppendLine($"        {FormatModLine(entry.Value, entry.Label)}");
            }
        }

        string critNote = NaturalTwenty ? " (NATURAL 20!)" : NaturalOne ? " (NATURAL 1!)" : "";
        sb.AppendLine($"      = {TotalRoll} vs AC {TargetAC} - {(Hit ? "HIT!" : "MISS!")}{critNote}");

        if (ConcealmentMissChance > 0)
        {
            string concealDesc = string.IsNullOrWhiteSpace(ConcealmentDescription) ? "Concealment" : ConcealmentDescription;
            string concealmentOutcome = MissedDueToConcealment ? "MISS!" : "HIT!";
            sb.AppendLine($"      Concealment check: 1d100({ConcealmentRoll}) - {concealmentOutcome} ({concealDesc})");
        }

        if (Hit)
        {
            sb.AppendLine();
            sb.AppendLine($"    <b><color=#FFF29A>{BuildCompactDamageFormula()}</color></b>");
            if (CritConfirmed)
                sb.AppendLine($"    Damage: {CritDamageDice} = {Damage - FeatDamageBonus} (crit)");
            else
            {
                sb.AppendLine($"    Damage: {(!string.IsNullOrEmpty(BaseDamageDiceStr) ? BaseDamageDiceStr : "?")} = {BaseDamageRoll}");
                if (DamageModifier != 0)
                    sb.AppendLine($"      {FormatModLine(DamageModifier, string.IsNullOrEmpty(DamageModifierDesc) ? "ability" : DamageModifierDesc)}");
                else if (!string.IsNullOrWhiteSpace(DamageModifierDesc) && DamageModifierDesc.IndexOf("no STR", StringComparison.OrdinalIgnoreCase) >= 0)
                    sb.AppendLine("      + 0 (no STR modifier)");
            }
            if (WeaponEnhancementDamageBonus > 0)
                sb.AppendLine($"      {FormatModLine(WeaponEnhancementDamageBonus, "weapon enhancement")}");

            if (SneakAttackApplied)
            {
                string trigger = string.IsNullOrEmpty(SneakAttackTriggerReason) ? "" : $" [{SneakAttackTriggerReason}]";
                sb.AppendLine($"      Includes sneak attack: +{SneakAttackDamage} ({SneakAttackDice}d6){trigger}");
            }

            int rawSubtotal = RawTotalDamage > 0 ? RawTotalDamage : (Damage + SneakAttackDamage);
            sb.AppendLine($"      = {rawSubtotal} raw damage{(string.IsNullOrEmpty(DamageTypeSummary) ? "" : $" ({DamageTypeSummary})")}");
            if (!string.IsNullOrEmpty(MitigationSummary)) sb.AppendLine($"      Mitigation: {MitigationSummary}");
            sb.AppendLine($"      = {TotalDamage} final damage");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatModLine(int value, string label)
    {
        return value >= 0 ? $"+ {value} ({label})" : $"- {-value} ({label})";
    }
}