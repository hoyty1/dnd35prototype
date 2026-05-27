using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mace of Smiting (SRD / DMG p.228):
/// +3 adamantine heavy mace that functions as +5 vs constructs.
/// Critical hit vs construct = instant destruction (no save).
/// Critical hit vs outsider = x4 multiplier instead of x2.
/// Adamantine properties (bypass hardness) handled by material system.
/// </summary>
public class MaceOfSmitingBehavior : SpecificItemBehavior
{
    // Base enhancement is +3; effective +5 vs constructs means +2 extra
    private const int ExtraBonusVsConstructs = 2;

    public override string DisplayName => "Mace of Smiting";

    public override void OnPreAttackRoll(CharacterController target, ref int attackBonus, List<string> logNotes)
    {
        if (target == null || target.Stats == null) return;

        if (target.Stats.CreatureType == "Construct")
        {
            attackBonus += ExtraBonusVsConstructs;
            logNotes.Add($"{DisplayName}: +{ExtraBonusVsConstructs} attack bonus vs construct (effective +5).");
            Log($"Mace of Smiting: +{ExtraBonusVsConstructs} attack vs {target.Stats.CharacterName} (Construct)");
        }
    }

    public override void OnDamageRoll(CharacterController target, ref int damage, bool isCrit, List<string> logNotes)
    {
        if (target == null || target.Stats == null) return;

        if (target.Stats.CreatureType == "Construct")
        {
            damage += ExtraBonusVsConstructs;
            logNotes.Add($"{DisplayName}: +{ExtraBonusVsConstructs} damage bonus vs construct (effective +5).");
            Log($"Mace of Smiting: +{ExtraBonusVsConstructs} damage vs {target.Stats.CharacterName} (Construct)");
        }
    }

    public override void OnCriticalHit(CharacterController target, int damage, List<string> logNotes)
    {
        if (target == null || target.Stats == null) return;

        // Construct: instant destruction, no save
        if (target.Stats.CreatureType == "Construct")
        {
            // Check if immune to critical hits (some constructs may not be)
            if (target.Stats.Immunities != null && target.Stats.Immunities.immuneToCriticalHits)
            {
                // Per SRD, Mace of Smiting bypasses normal crit immunity for constructs
                // The destruction effect is a special property, not a normal crit
            }

            logNotes.Add($"{DisplayName}: CONSTRUCT DESTROYED! {target.Stats.CharacterName} is instantly shattered!");
            Log($"Mace of Smiting: {target.Stats.CharacterName} (Construct) destroyed on critical hit!");

            target.Stats.CurrentHP = -100;
            target.OnDeath();
            return;
        }

        // Outsider: x4 crit multiplier instead of x2
        // The base damage was already calculated with x2 crit; we add another x2 worth
        // (i.e., add the base damage again to effectively get x4 from x2)
        if (target.Stats.CreatureType == "Outsider")
        {
            // damage parameter is the total crit damage (base * 2). To make it x4,
            // we need to add another (base * 2) = damage worth of extra damage.
            // Since we only have the final crit damage, adding 'damage' would give x4 total.
            int extraDamage = damage; // This doubles the crit from x2 to x4
            logNotes.Add($"{DisplayName}: Critical hit vs outsider — x4 multiplier! +{extraDamage} extra damage.");
            Log($"Mace of Smiting: x4 crit vs {target.Stats.CharacterName} (Outsider), +{extraDamage} extra damage");

            // Apply extra damage directly to target
            target.Stats.CurrentHP -= extraDamage;
        }
    }
}
