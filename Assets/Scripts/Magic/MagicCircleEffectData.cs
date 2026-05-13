using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Runtime data for Magic Circle against Evil/Good/Law/Chaos (D&D 3.5e PHB).
/// Inherits generic emanation behavior from EmanationEffectData and adds
/// alignment-specific warding mechanics.
///
/// Effects:
///   1. +2 deflection bonus to AC vs creatures of warded alignment
///   2. +2 resistance bonus to saves vs creatures of warded alignment
///   3. Immunity to new possession/mental control from warded alignment
///   4. Suppresses existing mental control while in area
///   5. Blocks bodily contact by summoned/conjured creatures of warded alignment (requires SR check)
///
/// Area: 10-ft radius emanation that moves with the center creature.
/// Does NOT stack with Protection from [Alignment] spells.
/// </summary>
[System.Serializable]
public class MagicCircleEffectData : EmanationEffectData
{
    // ═══════════════════════════════════════════════════════════════════
    //  MAGIC CIRCLE-SPECIFIC PROPERTIES
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>The alignment component being warded against (Evil, Good, Law, Chaos).</summary>
    public AlignmentProtectionType WardedAlignment = AlignmentProtectionType.None;

    // ═══════════════════════════════════════════════════════════════════
    //  ALIGNMENT-SPECIFIC METHODS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if an attacker's alignment matches the warded alignment component.
    /// Uses AlignmentProtectionRules for proper alignment axis matching.
    /// </summary>
    /// <param name="attackerAlignment">The attacker's full alignment.</param>
    /// <returns>True if the attacker's alignment is warded against.</returns>
    public bool IsAttackerOfWardedAlignment(Alignment attackerAlignment)
    {
        return AlignmentProtectionRules.Matches(WardedAlignment, attackerAlignment);
    }

    /// <summary>
    /// Get the spell name for this Magic Circle variant.
    /// </summary>
    /// <returns>Display name (e.g., "Magic Circle against Evil").</returns>
    public string GetSpellName()
    {
        switch (WardedAlignment)
        {
            case AlignmentProtectionType.Evil: return "Magic Circle against Evil";
            case AlignmentProtectionType.Good: return "Magic Circle against Good";
            case AlignmentProtectionType.Law: return "Magic Circle against Law";
            case AlignmentProtectionType.Chaos: return "Magic Circle against Chaos";
            default: return "Magic Circle";
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EMANATION BASE CLASS OVERRIDES
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the spell name for this Magic Circle variant.
    /// </summary>
    public override string GetEffectName()
    {
        return GetSpellName();
    }

    /// <summary>
    /// Called when a creature enters the Magic Circle area.
    /// Currently logs for debugging; future: apply immediate protections.
    /// </summary>
    public override void OnCreatureEntersArea(CharacterController creature)
    {
        // Magic Circle benefits are checked dynamically via GetMagicCircleBenefitsAgainst,
        // so no persistent effects need to be applied on entry.
        // This hook is available for future extensions (e.g., visual indicators).
    }

    /// <summary>
    /// Called when a creature leaves the Magic Circle area.
    /// Currently no persistent effects to remove; benefits stop when out of area.
    /// </summary>
    public override void OnCreatureLeavesArea(CharacterController creature)
    {
        // Magic Circle benefits are checked dynamically, so leaving the area
        // naturally stops the benefits without needing explicit removal.
    }

    /// <summary>
    /// Apply Magic Circle benefits to a creature in the area.
    /// Benefits are actually resolved dynamically via GameManager.GetMagicCircleBenefitsAgainst().
    /// </summary>
    public override void ApplyEffectsToCreature(CharacterController creature)
    {
        // Benefits (+2 deflection AC, +2 resistance saves, mental control block,
        // summoned contact block) are resolved at query time in GameManager,
        // not applied as persistent modifications.
    }

    /// <summary>
    /// Remove Magic Circle effects from a creature (on expiration/dismissal).
    /// Since benefits are dynamic, cleanup is handled by removing the emanation.
    /// </summary>
    public override void RemoveEffectsFromCreature(CharacterController creature)
    {
        // No persistent effects to remove — benefits are query-based.
    }
}
