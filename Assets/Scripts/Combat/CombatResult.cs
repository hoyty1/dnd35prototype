/// <summary>
/// Holds the result of a single attack action using D&D 3.5 mechanics.
/// Attack roll: d20 + BAB + STR mod + flanking bonus vs AC (10 + DEX mod + armor + shield)
/// Damage roll: weapon dice + STR mod + bonus damage + sneak attack (if applicable)
/// </summary>
public class CombatResult
{
    public CharacterController Attacker;
    public CharacterController Defender;
    public int DieRoll;       // Raw d20 roll
    public int TotalRoll;     // Roll + total attack bonus (BAB + STR mod + flanking)
    public int TargetAC;      // Defender's AC (10 + DEX mod + armor + shield)
    public bool Hit;          // Whether the attack hit
    public int Damage;        // Base damage dealt (0 if miss)
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

        if (Hit)
        {
            // Build the attack roll breakdown
            string rollBreakdown;
            if (IsFlanking)
                rollBreakdown = $"Roll: {DieRoll} {atkBonusStr} +{FlankingBonus} flanking = {TotalRoll} vs AC {TargetAC} - HIT!{critNote}";
            else
                rollBreakdown = $"Roll: {DieRoll} {atkBonusStr} = {TotalRoll} vs AC {TargetAC} - HIT!{critNote}";

            // Build the damage line
            string damageStr;
            if (SneakAttackApplied)
                damageStr = $"Deals {Damage} damage + {SneakAttackDamage} sneak attack ({SneakAttackDice}d6) = {TotalDamage} total!";
            else
                damageStr = $"Deals {Damage} damage! (STR {CharacterStats.FormatMod(Attacker.Stats.STRMod)})";

            string msg = $"{attackerName} attacks {defenderName}!{flankNote}\n{rollBreakdown}\n{damageStr}";

            if (TargetKilled)
                msg += $"\n{defenderName} has been slain!";
            return msg;
        }
        else
        {
            string rollBreakdown;
            if (IsFlanking)
                rollBreakdown = $"Roll: {DieRoll} {atkBonusStr} +{FlankingBonus} flanking = {TotalRoll} vs AC {TargetAC} - MISS!{critNote}";
            else
                rollBreakdown = $"Roll: {DieRoll} {atkBonusStr} = {TotalRoll} vs AC {TargetAC} - MISS!{critNote}";

            return $"{attackerName} attacks {defenderName}!{flankNote}\n{rollBreakdown}";
        }
    }
}
