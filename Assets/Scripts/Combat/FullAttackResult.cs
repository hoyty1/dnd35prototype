using System.Collections.Generic;
using System.Text;

/// <summary>
/// Holds results from a Full Attack, Dual Wield, or any multi-attack sequence.
/// Each individual attack is a CombatResult; this aggregates them.
/// Includes critical hit information for each individual attack.
/// Enhanced with comprehensive per-attack breakdowns for combat logging.
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

    // HP tracking for the overall sequence
    public int DefenderHPBefore;    // HP before the first attack
    public int DefenderHPAfter;     // HP after the last attack

    // Weapon info for header
    public string MainWeaponName;
    public string OffWeaponName;    // For dual wield

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

    /// <summary>Generate a full combat log summary of all attacks with comprehensive breakdowns.</summary>
    public string GetFullSummary()
    {
        var sb = new StringBuilder();
        string attackerName = Attacker.Stats.CharacterName;
        string defenderName = Defender.Stats.CharacterName;

        // ═══ HEADER ═══
        sb.AppendLine("═══════════════════════════════════");

        switch (Type)
        {
            case AttackType.FullAttack:
                sb.AppendLine($"{attackerName} full attacks {defenderName}");
                break;
            case AttackType.DualWield:
                sb.AppendLine($"{attackerName} dual wields against {defenderName}");
                break;
            default:
                sb.AppendLine($"{attackerName} attacks {defenderName}");
                break;
        }

        // Weapon info
        if (Type == AttackType.DualWield && !string.IsNullOrEmpty(MainWeaponName) && !string.IsNullOrEmpty(OffWeaponName))
        {
            sb.AppendLine($"  Main Hand: {MainWeaponName}");
            sb.AppendLine($"  Off Hand: {OffWeaponName}");
        }
        else if (!string.IsNullOrEmpty(MainWeaponName))
        {
            bool isRanged = Attacks.Count > 0 && Attacks[0].IsRangedAttack;
            string wpnType = isRanged ? "ranged" : "melee";
            sb.AppendLine($"  Weapon: {MainWeaponName} ({wpnType})");
        }

        // Range info (from first attack)
        if (Attacks.Count > 0 && Attacks[0].IsRangedAttack)
        {
            var first = Attacks[0];
            string penaltyStr = first.RangePenalty == 0 ? "no penalty" : $"{first.RangePenalty} penalty";
            sb.AppendLine($"  Range: {first.RangeDistanceFeet} ft ({first.RangeDistanceSquares} squares) - Increment {first.RangeIncrementNumber}, {penaltyStr}");
        }

        // Active feats (from first attack as representative)
        if (Attacks.Count > 0)
        {
            var first = Attacks[0];
            var feats = new List<string>();
            if (first.PowerAttackValue > 0) feats.Add($"Power Attack (-{first.PowerAttackValue} atk/+{first.PowerAttackDamageBonus} dmg)");
            if (first.RapidShotActive) feats.Add("Rapid Shot (-2 all attacks, +1 extra attack)");
            if (first.PointBlankShotActive) feats.Add("Point Blank Shot (+1 atk/+1 dmg)");
            if (feats.Count > 0)
                sb.AppendLine($"  Active Feats: {string.Join(", ", feats)}");
        }

        // Flanking
        if (Attacks.Count > 0 && Attacks[0].IsFlanking)
        {
            sb.AppendLine($"  Flanking: Yes (with {Attacks[0].FlankingPartnerName}, +{Attacks[0].FlankingBonus})");
        }

        sb.AppendLine();

        // ═══ EACH ATTACK ═══
        for (int i = 0; i < Attacks.Count; i++)
        {
            var atk = Attacks[i];
            string label = (i < AttackLabels.Count) ? AttackLabels[i] : $"Attack {i + 1}";

            sb.AppendLine(atk.GetAttackBreakdown(label));
            sb.AppendLine();
        }

        // ═══ SUMMARY ═══
        string critSummary = CritCount > 0 ? $", {CritCount} critical(s)!" : "";
        sb.AppendLine($"  Total: {HitCount}/{Attacks.Count} hits{critSummary}, {TotalDamageDealt} damage");

        // HP Change
        if (DefenderHPBefore > 0 || DefenderHPAfter >= 0)
        {
            sb.AppendLine($"  {defenderName}: {DefenderHPBefore} → {DefenderHPAfter} HP");
        }

        if (TargetKilled)
            sb.AppendLine($"  {defenderName} has been slain!");

        sb.Append("═══════════════════════════════════");

        return sb.ToString();
    }
}
