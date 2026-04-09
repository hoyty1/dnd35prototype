/// <summary>
/// Holds the result of a single attack action using D&D 3.5 mechanics.
/// Attack roll: d20 + BAB + STR mod + flanking bonus vs AC (10 + DEX mod + armor + shield)
/// Damage roll: weapon dice + STR mod + bonus damage + sneak attack (if applicable)
/// Now includes full critical hit tracking per D&D 3.5 rules.
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

    /// <summary>Total damage dealt including sneak attack.</summary>
    public int TotalDamage => Damage + SneakAttackDamage;

    public string GetSummary()
    {
        string attackerName = Attacker.Stats.CharacterName;
        string defenderName = Defender.Stats.CharacterName;
        int atkBonus = Attacker.Stats.AttackBonus;
        string atkBonusStr = CharacterStats.FormatMod(atkBonus);

        string critNote = "";
        if (NaturalTwenty) critNote = " (NATURAL 20!)";
        else if (NaturalOne) critNote = " (NATURAL 1!)";

        // Flanking note for the attack line
        string flankNote = "";
        if (IsFlanking)
            flankNote = $" [FLANKING with {FlankingPartnerName}, +{FlankingBonus}]";

        // Racial bonus note
        string racialNote = "";
        if (RacialAttackBonus > 0)
            racialNote = $" [Racial +{RacialAttackBonus} vs {Defender.Stats.CharacterName}]";

        if (Hit)
        {
            // Build the attack roll breakdown
            string rollBreakdown;
            string racialStr = RacialAttackBonus > 0 ? $" +{RacialAttackBonus} racial" : "";
            if (IsFlanking)
                rollBreakdown = $"Roll: {DieRoll} {atkBonusStr} +{FlankingBonus} flanking{racialStr} = {TotalRoll} vs AC {TargetAC} - HIT!{critNote}";
            else
                rollBreakdown = $"Roll: {DieRoll} {atkBonusStr}{racialStr} = {TotalRoll} vs AC {TargetAC} - HIT!{critNote}";

            // Critical hit info
            string critInfo = "";
            if (IsCritThreat)
            {
                string threatRange = CritThreatMin < 20 ? $"{CritThreatMin}-20" : "20";
                critInfo = $"\n*** Critical Threat! (threat range {threatRange}) ***";
                string confModStr = CharacterStats.FormatMod(ConfirmationTotal - ConfirmationRoll);
                if (CritConfirmed)
                {
                    critInfo += $"\nConfirmation: {ConfirmationRoll} {confModStr} = {ConfirmationTotal} vs AC {TargetAC} - CONFIRMED! (×{CritMultiplier})";
                }
                else
                {
                    critInfo += $"\nConfirmation: {ConfirmationRoll} {confModStr} = {ConfirmationTotal} vs AC {TargetAC} - Not confirmed, normal hit";
                }
            }

            // Build the damage line
            string damageStr;
            if (CritConfirmed)
            {
                if (SneakAttackApplied)
                    damageStr = $"CRITICAL HIT! {CritDamageDice} = {Damage} damage + {SneakAttackDamage} sneak attack ({SneakAttackDice}d6) = {TotalDamage} total!";
                else
                    damageStr = $"CRITICAL HIT! {CritDamageDice} = {Damage} damage!";
            }
            else
            {
                if (SneakAttackApplied)
                    damageStr = $"Deals {Damage} damage + {SneakAttackDamage} sneak attack ({SneakAttackDice}d6) = {TotalDamage} total!";
                else
                    damageStr = $"Deals {Damage} damage! (STR {CharacterStats.FormatMod(Attacker.Stats.STRMod)})";
            }

            string msg = $"{attackerName} attacks {defenderName}!{flankNote}{racialNote}\n{rollBreakdown}{critInfo}\n{damageStr}";

            if (TargetKilled)
                msg += $"\n{defenderName} has been slain!";
            return msg;
        }
        else
        {
            string rollBreakdown;
            string racialStr = RacialAttackBonus > 0 ? $" +{RacialAttackBonus} racial" : "";
            if (IsFlanking)
                rollBreakdown = $"Roll: {DieRoll} {atkBonusStr} +{FlankingBonus} flanking{racialStr} = {TotalRoll} vs AC {TargetAC} - MISS!{critNote}";
            else
                rollBreakdown = $"Roll: {DieRoll} {atkBonusStr}{racialStr} = {TotalRoll} vs AC {TargetAC} - MISS!{critNote}";

            return $"{attackerName} attacks {defenderName}!{flankNote}{racialNote}\n{rollBreakdown}";
        }
    }
}
