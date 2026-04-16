using System.Collections.Generic;
using System.Text;

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
    public int CombatExpertisePenalty;
    public int FightingDefensivelyAttackPenalty;
    public int ShootingIntoMeleePenalty;
    public bool PreciseShotNegated;
    public int FightingDefensivelyACBonus;
    public int AidAnotherAttackBonus;
    public int AidAnotherTargetAcBonus;

    public int BreakdownBAB;
    public int BreakdownAbilityMod;
    public string BreakdownAbilityName;
    public int BreakdownDualWieldPenalty;
    public bool IsDualWieldAttack;
    public bool IsOffHandAttack;
    public int WeaponNonProficiencyPenalty;
    public int ArmorNonProficiencyPenalty;

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
        if (CombatExpertisePenalty != 0) activeFeats.Add($"Combat Expertise ({CombatExpertisePenalty} atk/+{-CombatExpertisePenalty} AC)");
        if (FightingDefensivelyAttackPenalty != 0) activeFeats.Add($"Fighting Defensively ({FightingDefensivelyAttackPenalty} atk/+2 AC)");
        if (ShootingIntoMeleePenalty != 0) activeFeats.Add($"Shooting into melee ({ShootingIntoMeleePenalty} atk)");
        if (PreciseShotNegated) activeFeats.Add("Precise Shot (no shooting-into-melee penalty)");
        if (AidAnotherAttackBonus > 0) activeFeats.Add($"Aid Another (+{AidAnotherAttackBonus} atk)");
        if (AidAnotherTargetAcBonus > 0) activeFeats.Add($"Target Aided (+{AidAnotherTargetAcBonus} AC)");
        if (activeFeats.Count > 0)
            sb.AppendLine($"  Active Feats: {string.Join(", ", activeFeats)}");

        if (IsFlanking)
            sb.AppendLine($"  Flanking: Yes (with {FlankingPartnerName}, +{FlankingBonus})");
        if (FightingDefensivelyACBonus != 0)
            sb.AppendLine($"  Defender stance: Fighting Defensively (+{FightingDefensivelyACBonus} AC)");
        if (AidAnotherTargetAcBonus > 0)
            sb.AppendLine($"  Defender aided: +{AidAnotherTargetAcBonus} AC vs this attack");

        sb.AppendLine();
        sb.AppendLine("  Attack Roll:");
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
        if (CombatExpertisePenalty != 0) sb.AppendLine($"    {FormatModLine(CombatExpertisePenalty, "Combat Expertise")}");
        if (FightingDefensivelyAttackPenalty != 0) sb.AppendLine($"    {FormatModLine(FightingDefensivelyAttackPenalty, "Fighting Defensively")}");
        if (ShootingIntoMeleePenalty != 0) sb.AppendLine($"    {FormatModLine(ShootingIntoMeleePenalty, "shooting into melee")}");
        if (PreciseShotNegated) sb.AppendLine("    + 0 (Precise Shot negates shooting into melee penalty)");
        if (AidAnotherAttackBonus > 0) sb.AppendLine($"    {FormatModLine(AidAnotherAttackBonus, "Aid Another")}");
        if (IsRangedAttack && RangePenalty != 0) sb.AppendLine($"    {FormatModLine(RangePenalty, "range")}");
        if (IsDualWieldAttack && BreakdownDualWieldPenalty != 0)
            sb.AppendLine($"    {FormatModLine(BreakdownDualWieldPenalty, IsOffHandAttack ? "off-hand penalty" : "dual wield penalty")}");
        if (WeaponNonProficiencyPenalty != 0)
            sb.AppendLine($"    {FormatModLine(WeaponNonProficiencyPenalty, "weapon non-proficiency")}");
        if (ArmorNonProficiencyPenalty != 0)
            sb.AppendLine($"    {FormatModLine(ArmorNonProficiencyPenalty, "armor/shield non-proficiency")}");

        string critNote = NaturalTwenty ? " (NATURAL 20!)" : NaturalOne ? " (NATURAL 1!)" : "";
        sb.AppendLine($"    = {TotalRoll} vs AC {TargetAC} - {(Hit ? "HIT!" : "MISS!")}{critNote}");

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
            string diceStr = !string.IsNullOrEmpty(BaseDamageDiceStr) ? BaseDamageDiceStr : "?";

            if (CritConfirmed)
                sb.AppendLine($"    {CritDamageDice} = {Damage - FeatDamageBonus} (weapon + mods)");
            else
            {
                sb.AppendLine($"    {diceStr} = {BaseDamageRoll}");
                if (DamageModifier != 0)
                    sb.AppendLine($"    {FormatModLine(DamageModifier, string.IsNullOrEmpty(DamageModifierDesc) ? abilityName : DamageModifierDesc)}");
            }

            if (PowerAttackDamageBonus > 0) sb.AppendLine($"    {FormatModLine(PowerAttackDamageBonus, "Power Attack")}");
            if (PointBlankShotActive) sb.AppendLine($"    {FormatModLine(1, "Point Blank Shot")}");
            if (WeaponSpecBonus > 0) sb.AppendLine($"    {FormatModLine(WeaponSpecBonus, "Weapon Spec")}");

            if (SneakAttackApplied)
                sb.AppendLine($"    Includes sneak attack: +{SneakAttackDamage} ({SneakAttackDice}d6)");
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
        sb.AppendLine($"    Roll: d20 = {DieRoll}");

        if (BreakdownBAB != 0) sb.AppendLine($"      {FormatModLine(BreakdownBAB, "BAB")}");
        if (BreakdownAbilityMod != 0) sb.AppendLine($"      {FormatModLine(BreakdownAbilityMod, string.IsNullOrEmpty(BreakdownAbilityName) ? "STR" : BreakdownAbilityName)}");
        if (SizeAttackBonus != 0) sb.AppendLine($"      {FormatModLine(SizeAttackBonus, "size")}");
        if (IsFlanking && FlankingBonus != 0) sb.AppendLine($"      {FormatModLine(FlankingBonus, "flanking")}");
        if (RacialAttackBonus != 0) sb.AppendLine($"      {FormatModLine(RacialAttackBonus, "racial")}");
        if (PowerAttackValue > 0) sb.AppendLine($"      {FormatModLine(-PowerAttackValue, "Power Attack")}");
        if (RapidShotActive) sb.AppendLine($"      {FormatModLine(-2, "Rapid Shot")}");
        if (PointBlankShotActive) sb.AppendLine($"      {FormatModLine(1, "Point Blank Shot")}");
        if (WeaponFocusBonus > 0) sb.AppendLine($"      {FormatModLine(WeaponFocusBonus, "Weapon Focus")}");
        if (CombatExpertisePenalty != 0) sb.AppendLine($"      {FormatModLine(CombatExpertisePenalty, "Combat Expertise")}");
        if (FightingDefensivelyAttackPenalty != 0) sb.AppendLine($"      {FormatModLine(FightingDefensivelyAttackPenalty, "Fighting Defensively")}");
        if (ShootingIntoMeleePenalty != 0) sb.AppendLine($"      {FormatModLine(ShootingIntoMeleePenalty, "shooting into melee")}");
        if (PreciseShotNegated) sb.AppendLine("      + 0 (Precise Shot negates shooting into melee penalty)");
        if (AidAnotherAttackBonus > 0) sb.AppendLine($"      {FormatModLine(AidAnotherAttackBonus, "Aid Another")}");
        if (IsRangedAttack && RangePenalty != 0) sb.AppendLine($"      {FormatModLine(RangePenalty, "range")}");
        if (IsDualWieldAttack && BreakdownDualWieldPenalty != 0)
            sb.AppendLine($"      {FormatModLine(BreakdownDualWieldPenalty, IsOffHandAttack ? "off-hand penalty" : "dual wield penalty")}");
        if (WeaponNonProficiencyPenalty != 0)
            sb.AppendLine($"      {FormatModLine(WeaponNonProficiencyPenalty, "weapon non-proficiency")}");
        if (ArmorNonProficiencyPenalty != 0)
            sb.AppendLine($"      {FormatModLine(ArmorNonProficiencyPenalty, "armor/shield non-proficiency")}");

        string critNote = NaturalTwenty ? " (NATURAL 20!)" : NaturalOne ? " (NATURAL 1!)" : "";
        sb.AppendLine($"      = {TotalRoll} vs AC {TargetAC} - {(Hit ? "HIT!" : "MISS!")}{critNote}");

        if (Hit)
        {
            sb.AppendLine();
            if (CritConfirmed)
                sb.AppendLine($"    Damage: {CritDamageDice} = {Damage - FeatDamageBonus} (crit)");
            else
                sb.AppendLine($"    Damage: {(!string.IsNullOrEmpty(BaseDamageDiceStr) ? BaseDamageDiceStr : "?")} = {BaseDamageRoll}");
            if (SneakAttackApplied)
                sb.AppendLine($"      Includes sneak attack: +{SneakAttackDamage} ({SneakAttackDice}d6)");

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