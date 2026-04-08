/// <summary>
/// Holds the result of a single attack action.
/// </summary>
public class CombatResult
{
    public CharacterController Attacker;
    public CharacterController Defender;
    public int DieRoll;       // Raw d20 roll
    public int TotalRoll;     // Roll + attack bonus
    public int TargetAC;      // Defender's AC
    public bool Hit;          // Whether the attack hit
    public int Damage;        // Damage dealt (0 if miss)
    public bool TargetKilled; // Whether the target died

    public string GetSummary()
    {
        string attackerName = Attacker.Stats.CharacterName;
        string defenderName = Defender.Stats.CharacterName;

        if (Hit)
        {
            string msg = $"{attackerName} attacks {defenderName}!\n" +
                         $"Roll: {DieRoll} + {Attacker.Stats.AttackBonus} = {TotalRoll} vs AC {TargetAC} - HIT!\n" +
                         $"Deals {Damage} damage!";
            if (TargetKilled)
                msg += $"\n{defenderName} has been slain!";
            return msg;
        }
        else
        {
            return $"{attackerName} attacks {defenderName}!\n" +
                   $"Roll: {DieRoll} + {Attacker.Stats.AttackBonus} = {TotalRoll} vs AC {TargetAC} - MISS!";
        }
    }
}
