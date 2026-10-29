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

    /// <summary>
    /// A critical hit against an outsider deals x4 damage rather than x2 (DMG specific weapons): the multiplier of the weapon damage
    /// roll, so the damage goes through the hit's one damage packet and its mitigation (CMB-013).
    /// </summary>
    public override int GetCriticalMultiplierAgainst(CharacterController target, int multiplier)
        => target != null && target.Stats != null && target.Stats.CreatureType == "Outsider" ? Mathf.Max(multiplier, 4) : multiplier;

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

        // Outsider: x4 instead of x2, rolled in the weapon damage roll (GetCriticalMultiplierAgainst), not added here.
        if (target.Stats.CreatureType == "Outsider")
            logNotes.Add($"{DisplayName}: critical hit vs outsider: x4 multiplier (DMG specific weapons).");
    }
}
