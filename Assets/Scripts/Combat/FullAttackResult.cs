using System.Collections.Generic;
using System.Text;

/// <summary>
/// Holds results from a Full Attack, Dual Wield, or any multi-attack sequence.
/// Each individual attack is a CombatResult; this aggregates them.
/// Includes critical hit information for each individual attack.
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

    /// <summary>Number of confirmed critical hits.</summary>
    public int CritCount
    {
        get
        {
            int count = 0;
            foreach (var atk in Attacks)
                if (atk.CritConfirmed) count++;
            return count;
        }
    }

    /// <summary>Generate a full combat log summary of all attacks including crit details.</summary>
    public string GetFullSummary()
    {
        var sb = new StringBuilder();
        string attackerName = Attacker.Stats.CharacterName;
        string defenderName = Defender.Stats.CharacterName;

        // Build feat summary for header
        string featSummary = "";
        if (Attacks.Count > 0)
        {
            var feats = new List<string>();
            var first = Attacks[0];
            if (first.PowerAttackValue > 0) feats.Add($"Power Attack -{first.PowerAttackValue}");
            if (first.RapidShotActive) feats.Add("Rapid Shot");
            if (first.PointBlankShotActive) feats.Add("Point Blank Shot");
            if (feats.Count > 0) featSummary = $" ({string.Join(", ", feats)})";
        }

        // Header
        switch (Type)
        {
            case AttackType.FullAttack:
                sb.AppendLine($"=== {attackerName} FULL ATTACK vs {defenderName}!{featSummary} ===");
                break;
            case AttackType.DualWield:
                sb.AppendLine($"=== {attackerName} DUAL WIELD vs {defenderName}!{featSummary} ===");
                break;
            default:
                sb.AppendLine($"{attackerName} attacks {defenderName}!{featSummary}");
                break;
        }

        // Show Rapid Shot status if any attack has it
        if (Attacks.Count > 0 && Attacks[0].RapidShotActive)
        {
            sb.AppendLine($"  Rapid Shot: Active (-2 penalty to all attacks, +1 extra attack)");
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
            string rapidStr = atk.RapidShotActive ? " -2 RS" : "";

            if (atk.Hit)
            {
                // Build damage string
                string dmgStr;
                if (atk.CritConfirmed)
                {
                    // Critical hit!
                    if (atk.SneakAttackApplied)
                        dmgStr = $"CRIT! {atk.CritDamageDice}={atk.Damage} + {atk.SneakAttackDamage} sneak ({atk.SneakAttackDice}d6) = {atk.TotalDamage}";
                    else
                        dmgStr = $"CRIT(×{atk.CritMultiplier})! {atk.CritDamageDice}={atk.Damage}";
                }
                else
                {
                    if (atk.SneakAttackApplied)
                        dmgStr = $"{atk.Damage} + {atk.SneakAttackDamage} sneak ({atk.SneakAttackDice}d6) = {atk.TotalDamage}";
                    else
                        dmgStr = $"{atk.Damage}";
                }

                // Show threat info inline
                string threatStr = "";
                if (atk.IsCritThreat && !atk.CritConfirmed)
                    threatStr = $" [Threat! Confirm: {atk.ConfirmationRoll}→{atk.ConfirmationTotal} vs AC {atk.TargetAC} FAILED]";
                else if (atk.CritConfirmed)
                    threatStr = $" [Confirm: {atk.ConfirmationRoll}→{atk.ConfirmationTotal} vs AC {atk.TargetAC} ✓]";

                sb.AppendLine($"  [{label}] d20={atk.DieRoll}{flankStr}{rapidStr} → {atk.TotalRoll} vs AC {atk.TargetAC} HIT!{critNote}{threatStr} ({dmgStr} dmg)");
            }
            else
            {
                sb.AppendLine($"  [{label}] d20={atk.DieRoll}{flankStr}{rapidStr} → {atk.TotalRoll} vs AC {atk.TargetAC} MISS{critNote}");
            }
        }

        // Summary line
        string critSummary = CritCount > 0 ? $", {CritCount} crit(s)!" : "";
        sb.AppendLine($"--- {HitCount}/{Attacks.Count} hits{critSummary}, {TotalDamageDealt} total damage ---");

        if (TargetKilled)
            sb.Append($"{defenderName} has been slain!");

        return sb.ToString().TrimEnd();
    }
}
