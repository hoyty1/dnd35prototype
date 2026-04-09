using System.Collections.Generic;
using System.Text;

/// <summary>
/// Holds results from a Full Attack, Dual Wield, or any multi-attack sequence.
/// Each individual attack is a CombatResult; this aggregates them.
/// </summary>
public class FullAttackResult
{
    /// <summary>The type of multi-attack action performed.</summary>
    public enum AttackType
    {
        SingleAttack,       // Standard action - 1 attack
        FullAttack,         // Full-round action - iterative attacks based on BAB
        DualWield           // Full-round action - main hand + off-hand
    }

    public AttackType Type;
    public CharacterController Attacker;
    public CharacterController Defender;
    public List<CombatResult> Attacks = new List<CombatResult>();
    public List<string> AttackLabels = new List<string>(); // e.g., "Main Hand", "Off-Hand", "2nd Attack"
    public bool TargetKilled;

    /// <summary>Total damage dealt across all attacks.</summary>
    public int TotalDamageDealt
    {
        get
        {
            int total = 0;
            foreach (var atk in Attacks)
                if (atk.Hit) total += atk.TotalDamage;
            return total;
        }
    }

    /// <summary>Number of attacks that hit.</summary>
    public int HitCount
    {
        get
        {
            int count = 0;
            foreach (var atk in Attacks)
                if (atk.Hit) count++;
            return count;
        }
    }

    /// <summary>Generate a full combat log summary of all attacks.</summary>
    public string GetFullSummary()
    {
        var sb = new StringBuilder();
        string attackerName = Attacker.Stats.CharacterName;
        string defenderName = Defender.Stats.CharacterName;

        // Header
        switch (Type)
        {
            case AttackType.FullAttack:
                sb.AppendLine($"=== {attackerName} FULL ATTACK vs {defenderName}! ===");
                break;
            case AttackType.DualWield:
                sb.AppendLine($"=== {attackerName} DUAL WIELD vs {defenderName}! ===");
                break;
            default:
                sb.AppendLine($"{attackerName} attacks {defenderName}!");
                break;
        }

        // Each attack
        for (int i = 0; i < Attacks.Count; i++)
        {
            var atk = Attacks[i];
            string label = (i < AttackLabels.Count) ? AttackLabels[i] : $"Attack {i + 1}";

            string critNote = "";
            if (atk.NaturalTwenty) critNote = " (NAT 20!)";
            else if (atk.NaturalOne) critNote = " (NAT 1!)";

            string flankStr = atk.IsFlanking ? $" +{atk.FlankingBonus} flank" : "";

            if (atk.Hit)
            {
                string dmgStr;
                if (atk.SneakAttackApplied)
                    dmgStr = $"{atk.Damage} + {atk.SneakAttackDamage} sneak ({atk.SneakAttackDice}d6) = {atk.TotalDamage}";
                else
                    dmgStr = $"{atk.Damage}";

                sb.AppendLine($"  [{label}] d20={atk.DieRoll}{flankStr} → {atk.TotalRoll} vs AC {atk.TargetAC} HIT!{critNote} ({dmgStr} dmg)");
            }
            else
            {
                sb.AppendLine($"  [{label}] d20={atk.DieRoll}{flankStr} → {atk.TotalRoll} vs AC {atk.TargetAC} MISS{critNote}");
            }
        }

        // Summary line
        sb.AppendLine($"--- {HitCount}/{Attacks.Count} hits, {TotalDamageDealt} total damage ---");

        if (TargetKilled)
            sb.Append($"{defenderName} has been slain!");

        return sb.ToString().TrimEnd();
    }
}
