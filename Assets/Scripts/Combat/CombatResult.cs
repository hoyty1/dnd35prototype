/// <summary>
/// Holds the result of a single attack action using D&D 3.5 mechanics.
/// Attack roll: d20 + BAB + STR mod vs AC (10 + DEX mod + armor + shield)
/// Damage roll: weapon dice + STR mod + bonus damage
/// </summary>
public class CombatResult
{
    public CharacterController Attacker;
    public CharacterController Defender;
    public int DieRoll;       // Raw d20 roll
    public int TotalRoll;     // Roll + total attack bonus (BAB + STR mod)
    public int TargetAC;      // Defender's AC (10 + DEX mod + armor + shield)
    public bool Hit;          // Whether the attack hit
    public int Damage;        // Damage dealt (0 if miss)
    public bool TargetKilled; // Whether the target died
    public bool NaturalTwenty;  // Natural 20 (auto-hit)
    public bool NaturalOne;     // Natural 1 (auto-miss)

    public string GetSummary()
    {
        string attackerName = Attacker.Stats.CharacterName;
        string defenderName = Defender.Stats.CharacterName;
        int atkBonus = Attacker.Stats.AttackBonus;
        string atkBonusStr = CharacterStats.FormatMod(atkBonus);

        string critNote = "";
        if (NaturalTwenty) critNote = " (NATURAL 20!)";
        else if (NaturalOne) critNote = " (NATURAL 1!)";

        if (Hit)
        {
            string msg = $"{attackerName} attacks {defenderName}!\n" +
                         $"Roll: {DieRoll} {atkBonusStr} = {TotalRoll} vs AC {TargetAC} - HIT!{critNote}\n" +
                         $"Deals {Damage} damage! (STR {CharacterStats.FormatMod(Attacker.Stats.STRMod)})";
            if (TargetKilled)
                msg += $"\n{defenderName} has been slain!";
            return msg;
        }
        else
        {
            return $"{attackerName} attacks {defenderName}!\n" +
                   $"Roll: {DieRoll} {atkBonusStr} = {TotalRoll} vs AC {TargetAC} - MISS!{critNote}";
        }
    }
}
