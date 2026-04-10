using System.Collections.Generic;
using System.Text;

/// <summary>
/// Holds the result of a single attack action using D&D 3.5 mechanics.
/// Attack roll: d20 + BAB + STR mod + flanking bonus vs AC (10 + DEX mod + armor + shield)
/// Damage roll: weapon dice + STR mod + bonus damage + sneak attack (if applicable)
/// Now includes full critical hit tracking per D&D 3.5 rules.
/// Enhanced with detailed breakdown fields for comprehensive combat logging.
/// </summary>
public class CombatResult
{
    public CharacterController Attacker;
    public CharacterController Defender;
    public int DieRoll;       // Raw d20 roll
    public int TotalRoll;     // Roll + total attack bonus (BAB + STR mod + flanking)
    public int TargetAC;      // Defender's AC (10 + DEX mod + armor + shield)
    public bool Hit;          // Whether the attack hit
    public int Damage;        // Base weapon damage dealt (0 if miss)
    public bool TargetKilled; // Whether the target died

    public bool NaturalTwenty;  // Natural 20 (auto-hit)
    public bool NaturalOne;     // Natural 1 (auto-miss)

    // Flanking fields
    public bool IsFlanking;               // Whether the attacker is flanking
    public int FlankingBonus;             // +2 if flanking, 0 otherwise
    public string FlankingPartnerName;    // Name of the flanking ally

    // Sneak Attack fields
    public bool SneakAttackApplied;       // Whether sneak attack damage was added
    public int SneakAttackDice;           // Number of d6 rolled (e.g., 2 for 2d6)
    public int SneakAttackDamage;         // Total sneak attack damage rolled

    // Critical Hit fields (D&D 3.5)
    public bool IsCritThreat;             // Whether the natural roll was in the weapon's threat range
    public bool CritConfirmed;            // Whether the confirmation roll succeeded
    public int ConfirmationRoll;          // Natural d20 of the confirmation roll
    public int ConfirmationTotal;         // Total of confirmation roll (roll + attack mod)
    public int CritMultiplier;            // Weapon's crit multiplier (×2, ×3, etc.)
    public int CritThreatMin;             // Weapon's threat range minimum (e.g. 19 for 19-20)
    public string CritDamageDice;         // Description of crit damage dice (e.g. "2d8+3")

    // Racial bonus fields
    public int RacialAttackBonus;         // Racial attack bonus applied (e.g., Dwarf +1 vs Goblinoids)

    // Size bonus fields
    public int SizeAttackBonus;           // Size modifier to attack (Small = +1, Medium = 0)

    // Damage modifier fields (D&D 3.5 weapon damage modifiers)
    public int DamageModifier;            // The actual STR-based damage bonus applied (e.g., +6 for 1.5× STR)
    public string DamageModifierDesc;     // Description for combat log (e.g., "1.5× STR", "composite +2")

    // Range increment fields (D&D 3.5 ranged attack rules)
    public bool IsRangedAttack;           // Whether this was a ranged attack
    public int RangeDistanceFeet;         // Distance to target in feet
    public int RangeDistanceSquares;      // Distance to target in squares
    public int RangeIncrementNumber;      // Which range increment (1 = first, no penalty)
    public int RangePenalty;              // Attack penalty from range (-2 per increment beyond first)
    public string WeaponName;             // Name of weapon used (for combat log)

    // Feat fields (D&D 3.5)
    public int PowerAttackValue;          // Power Attack penalty/bonus value (0 = not active)
    public int PowerAttackDamageBonus;    // Actual damage bonus from Power Attack (may be 2× for two-handed)
    public bool RapidShotActive;          // Whether Rapid Shot was active for this attack
    public bool PointBlankShotActive;     // Whether Point Blank Shot bonus was applied
    public int WeaponFocusBonus;          // Attack bonus from Weapon Focus/Greater Weapon Focus
    public int WeaponSpecBonus;           // Damage bonus from Weapon Specialization/Greater
    public int CombatExpertisePenalty;    // Attack penalty from Combat Expertise (for AC bonus)

    // ===== DETAILED BREAKDOWN FIELDS (for enhanced combat log) =====
    public int BreakdownBAB;              // Base Attack Bonus used for this attack
    public int BreakdownAbilityMod;       // STR or DEX modifier used for attack
    public string BreakdownAbilityName;   // "STR" or "DEX"
    public int BreakdownDualWieldPenalty;  // Dual wield penalty (0 if not dual wielding)
    public bool IsDualWieldAttack;        // Whether this is part of a dual wield sequence
    public bool IsOffHandAttack;          // Whether this is the off-hand attack

    // Damage breakdown
    public int BaseDamageRoll;            // The raw weapon dice roll (before modifiers)
    public string BaseDamageDiceStr;      // e.g. "1d8", "2d6"
    public int FeatDamageBonus;           // Total feat bonus to damage (Power Attack + PBS)

    // HP tracking
    public int DefenderHPBefore;          // Defender HP before this attack
    public int DefenderHPAfter;           // Defender HP after this attack

    /// <summary>Total damage dealt including sneak attack and feat bonuses.</summary>
    public int TotalDamage => Damage + SneakAttackDamage;

    public string GetSummary()
    {
        // Delegate to the detailed summary
        return GetDetailedSummary();
    }

    /// <summary>
    /// Generate a fully detailed combat log for this single attack with complete breakdown
    /// of all modifiers, rolls, and results.
    /// </summary>
    public string GetDetailedSummary()
    {
        var sb = new StringBuilder();
        string attackerName = Attacker.Stats.CharacterName;
        string defenderName = Defender.Stats.CharacterName;

        // === HEADER LINE ===
        string weaponNote = !string.IsNullOrEmpty(WeaponName) ? $" with {WeaponName}" : "";
        string attackType = IsRangedAttack ? "ranged" : "melee";

        sb.AppendLine($"═══════════════════════════════════");
        sb.AppendLine($"{attackerName} attacks {defenderName}{weaponNote} ({attackType})");

        // === WEAPON INFO ===
        if (!string.IsNullOrEmpty(WeaponName))
        {
            string wpnType = IsRangedAttack ? "ranged" : "melee";
            sb.AppendLine($"  Weapon: {WeaponName} ({wpnType})");
        }

        // === RANGE INFO ===
        if (IsRangedAttack)
        {
            string penaltyStr = RangePenalty == 0 ? "no penalty" : $"{RangePenalty} penalty";
            sb.AppendLine($"  Range: {RangeDistanceFeet} ft ({RangeDistanceSquares} squares) - Increment {RangeIncrementNumber}, {penaltyStr}");
        }

        // === ACTIVE FEATS ===
        var activeFeats = new List<string>();
        if (PowerAttackValue > 0) activeFeats.Add($"Power Attack (-{PowerAttackValue} atk/+{PowerAttackDamageBonus} dmg)");
        if (RapidShotActive) activeFeats.Add("Rapid Shot (-2 all attacks)");
        if (PointBlankShotActive) activeFeats.Add("Point Blank Shot (+1 atk/+1 dmg)");
        if (WeaponFocusBonus > 0) activeFeats.Add($"Weapon Focus (+{WeaponFocusBonus} atk)");
        if (WeaponSpecBonus > 0) activeFeats.Add($"Weapon Spec (+{WeaponSpecBonus} dmg)");
        if (CombatExpertisePenalty != 0) activeFeats.Add($"Combat Expertise ({CombatExpertisePenalty} atk/+{-CombatExpertisePenalty} AC)");
        if (activeFeats.Count > 0)
            sb.AppendLine($"  Active Feats: {string.Join(", ", activeFeats)}");

        // === FLANKING ===
        if (IsFlanking)
            sb.AppendLine($"  Flanking: Yes (with {FlankingPartnerName}, +{FlankingBonus})");

        sb.AppendLine();

        // === ATTACK ROLL BREAKDOWN ===
        sb.AppendLine($"  Attack Roll:");
        sb.AppendLine($"    Roll: d20 = {DieRoll}");

        // BAB
        string babLabel = BreakdownBAB != 0 ? $"    {FormatModLine(BreakdownBAB, "base attack bonus")}" : "";
        if (!string.IsNullOrEmpty(babLabel)) sb.AppendLine(babLabel);

        // Ability modifier (STR/DEX)
        string abilityName = !string.IsNullOrEmpty(BreakdownAbilityName) ? BreakdownAbilityName : "STR";
        if (BreakdownAbilityMod != 0)
            sb.AppendLine($"    {FormatModLine(BreakdownAbilityMod, abilityName)}");

        // Size modifier
        if (SizeAttackBonus != 0)
            sb.AppendLine($"    {FormatModLine(SizeAttackBonus, "size")}");

        // Flanking bonus
        if (IsFlanking && FlankingBonus != 0)
            sb.AppendLine($"    {FormatModLine(FlankingBonus, "flanking")}");

        // Racial attack bonus
        if (RacialAttackBonus != 0)
            sb.AppendLine($"    {FormatModLine(RacialAttackBonus, "racial")}");

        // Power Attack penalty
        if (PowerAttackValue > 0)
            sb.AppendLine($"    {FormatModLine(-PowerAttackValue, "Power Attack")}");

        // Rapid Shot penalty
        if (RapidShotActive)
            sb.AppendLine($"    {FormatModLine(-2, "Rapid Shot")}");

        // Point Blank Shot attack bonus
        if (PointBlankShotActive)
            sb.AppendLine($"    {FormatModLine(1, "Point Blank Shot")}");

        // Weapon Focus bonus
        if (WeaponFocusBonus > 0)
            sb.AppendLine($"    {FormatModLine(WeaponFocusBonus, "Weapon Focus")}");

        // Combat Expertise penalty
        if (CombatExpertisePenalty != 0)
            sb.AppendLine($"    {FormatModLine(CombatExpertisePenalty, "Combat Expertise")}");

        // Range penalty
        if (IsRangedAttack && RangePenalty != 0)
            sb.AppendLine($"    {FormatModLine(RangePenalty, "range")}");

        // Dual wield penalty
        if (IsDualWieldAttack && BreakdownDualWieldPenalty != 0)
            sb.AppendLine($"    {FormatModLine(BreakdownDualWieldPenalty, IsOffHandAttack ? "off-hand penalty" : "dual wield penalty")}");

        // === RESULT LINE ===
        string critNote = "";
        if (NaturalTwenty) critNote = " (NATURAL 20!)";
        else if (NaturalOne) critNote = " (NATURAL 1!)";

        string hitMiss = Hit ? "HIT!" : "MISS!";
        sb.AppendLine($"    = {TotalRoll} vs AC {TargetAC} - {hitMiss}{critNote}");

        // === CRITICAL THREAT ===
        if (IsCritThreat)
        {
            string threatRange = CritThreatMin < 20 ? $"{CritThreatMin}-20" : "20";
            sb.AppendLine($"  *** Critical Threat! (threat range {threatRange}) ***");
            string confModStr = CharacterStats.FormatMod(ConfirmationTotal - ConfirmationRoll);
            if (CritConfirmed)
                sb.AppendLine($"  Confirmation: d20 = {ConfirmationRoll} {confModStr} = {ConfirmationTotal} vs AC {TargetAC} - CONFIRMED! (×{CritMultiplier})");
            else
                sb.AppendLine($"  Confirmation: d20 = {ConfirmationRoll} {confModStr} = {ConfirmationTotal} vs AC {TargetAC} - Not confirmed, normal hit");
        }

        // === DAMAGE BREAKDOWN (only if hit) ===
        if (Hit)
        {
            sb.AppendLine();
            sb.AppendLine($"  Damage:");

            // Base damage dice
            string diceStr = !string.IsNullOrEmpty(BaseDamageDiceStr) ? BaseDamageDiceStr : "?";
            if (CritConfirmed)
            {
                sb.AppendLine($"  CRITICAL HIT! (×{CritMultiplier})");
                sb.AppendLine($"    {CritDamageDice} = {Damage - FeatDamageBonus} (weapon + mods)");
            }
            else
            {
                sb.AppendLine($"    {diceStr} = {BaseDamageRoll}");

                // STR/DEX damage modifier
                if (DamageModifier != 0)
                {
                    string dmgModLabel = !string.IsNullOrEmpty(DamageModifierDesc) ? DamageModifierDesc : abilityName;
                    sb.AppendLine($"    {FormatModLine(DamageModifier, dmgModLabel)}");
                }
            }

            // Power Attack damage bonus
            if (PowerAttackDamageBonus > 0)
                sb.AppendLine($"    {FormatModLine(PowerAttackDamageBonus, "Power Attack")}");

            // Point Blank Shot damage bonus
            if (PointBlankShotActive)
                sb.AppendLine($"    {FormatModLine(1, "Point Blank Shot")}");

            // Weapon Specialization damage bonus
            if (WeaponSpecBonus > 0)
                sb.AppendLine($"    {FormatModLine(WeaponSpecBonus, "Weapon Spec")}");

            // Total damage line
            sb.AppendLine($"    = {Damage} damage");

            // Sneak attack
            if (SneakAttackApplied)
            {
                sb.AppendLine($"    + {SneakAttackDamage} sneak attack ({SneakAttackDice}d6)");
                sb.AppendLine($"    = {TotalDamage} total damage");
            }

            // HP Change
            if (DefenderHPBefore > 0 || DefenderHPAfter >= 0)
            {
                sb.AppendLine($"  {Defender.Stats.CharacterName}: {DefenderHPBefore} → {DefenderHPAfter} HP");
            }

            if (TargetKilled)
                sb.AppendLine($"  {Defender.Stats.CharacterName} has been slain!");
        }

        sb.Append($"═══════════════════════════════════");
        return sb.ToString();
    }

    /// <summary>
    /// Generate a compact per-attack summary for use inside FullAttackResult.
    /// Shows the full breakdown indented under an attack label.
    /// </summary>
    public string GetAttackBreakdown(string label)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"  {label}:");

        // Attack roll
        sb.AppendLine($"    Roll: d20 = {DieRoll}");

        if (BreakdownBAB != 0)
            sb.AppendLine($"      {FormatModLine(BreakdownBAB, "BAB")}");
        if (BreakdownAbilityMod != 0)
        {
            string abilName = !string.IsNullOrEmpty(BreakdownAbilityName) ? BreakdownAbilityName : "STR";
            sb.AppendLine($"      {FormatModLine(BreakdownAbilityMod, abilName)}");
        }
        if (SizeAttackBonus != 0)
            sb.AppendLine($"      {FormatModLine(SizeAttackBonus, "size")}");
        if (IsFlanking && FlankingBonus != 0)
            sb.AppendLine($"      {FormatModLine(FlankingBonus, "flanking")}");
        if (RacialAttackBonus != 0)
            sb.AppendLine($"      {FormatModLine(RacialAttackBonus, "racial")}");
        if (PowerAttackValue > 0)
            sb.AppendLine($"      {FormatModLine(-PowerAttackValue, "Power Attack")}");
        if (RapidShotActive)
            sb.AppendLine($"      {FormatModLine(-2, "Rapid Shot")}");
        if (PointBlankShotActive)
            sb.AppendLine($"      {FormatModLine(1, "Point Blank Shot")}");
        if (WeaponFocusBonus > 0)
            sb.AppendLine($"      {FormatModLine(WeaponFocusBonus, "Weapon Focus")}");
        if (CombatExpertisePenalty != 0)
            sb.AppendLine($"      {FormatModLine(CombatExpertisePenalty, "Combat Expertise")}");
        if (IsRangedAttack && RangePenalty != 0)
            sb.AppendLine($"      {FormatModLine(RangePenalty, "range")}");
        if (IsDualWieldAttack && BreakdownDualWieldPenalty != 0)
            sb.AppendLine($"      {FormatModLine(BreakdownDualWieldPenalty, IsOffHandAttack ? "off-hand penalty" : "dual wield penalty")}");

        // Result
        string critNote = "";
        if (NaturalTwenty) critNote = " (NATURAL 20!)";
        else if (NaturalOne) critNote = " (NATURAL 1!)";

        string hitMiss = Hit ? "HIT!" : "MISS!";
        sb.AppendLine($"      = {TotalRoll} vs AC {TargetAC} - {hitMiss}{critNote}");

        // Critical threat
        if (IsCritThreat)
        {
            string threatRange = CritThreatMin < 20 ? $"{CritThreatMin}-20" : "20";
            string confModStr = CharacterStats.FormatMod(ConfirmationTotal - ConfirmationRoll);
            if (CritConfirmed)
                sb.AppendLine($"    *** Critical Threat ({threatRange})! Confirm: {ConfirmationRoll} {confModStr} = {ConfirmationTotal} vs AC {TargetAC} - CONFIRMED! (×{CritMultiplier}) ***");
            else
                sb.AppendLine($"    *** Critical Threat ({threatRange})! Confirm: {ConfirmationRoll} {confModStr} = {ConfirmationTotal} vs AC {TargetAC} - Not confirmed ***");
        }

        // Damage
        if (Hit)
        {
            sb.AppendLine();
            string diceStr = !string.IsNullOrEmpty(BaseDamageDiceStr) ? BaseDamageDiceStr : "?";

            if (CritConfirmed)
            {
                sb.AppendLine($"    CRITICAL HIT! (×{CritMultiplier})");
                sb.AppendLine($"    Damage: {CritDamageDice} = {Damage - FeatDamageBonus} (crit)");
            }
            else
            {
                sb.AppendLine($"    Damage: {diceStr} = {BaseDamageRoll}");
            }

            if (!CritConfirmed && DamageModifier != 0)
            {
                string dmgModLabel = !string.IsNullOrEmpty(DamageModifierDesc) ? DamageModifierDesc : "STR";
                sb.AppendLine($"      {FormatModLine(DamageModifier, dmgModLabel)}");
            }
            if (PowerAttackDamageBonus > 0)
                sb.AppendLine($"      {FormatModLine(PowerAttackDamageBonus, "Power Attack")}");
            if (PointBlankShotActive)
                sb.AppendLine($"      {FormatModLine(1, "Point Blank Shot")}");

            sb.AppendLine($"      = {Damage} damage");

            if (SneakAttackApplied)
            {
                sb.AppendLine($"      + {SneakAttackDamage} sneak attack ({SneakAttackDice}d6)");
                sb.AppendLine($"      = {TotalDamage} total damage");
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Format a modifier line like "+ 3 (STR)" or "- 2 (Rapid Shot)".</summary>
    private static string FormatModLine(int value, string label)
    {
        if (value >= 0)
            return $"+ {value} ({label})";
        else
            return $"- {-value} ({label})";
    }
}
