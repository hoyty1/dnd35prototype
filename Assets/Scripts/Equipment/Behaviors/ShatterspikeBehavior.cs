using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shatterspike (SRD): +1 longsword that grants the Improved Sunder feat
/// and gets a +4 bonus (total +5) on sunder attempts to destroy weapons/shields.
/// Wielder is treated as having Improved Sunder even without prerequisites.
/// </summary>
public class ShatterspikeBehavior : SpecificItemBehavior
{
    private const int SunderBonus = 4; // +4 bonus on sunder attempts (stacks with +1 enhancement)

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);
        Log("Equipped: grants Improved Sunder feat + sunder bonus");
    }

    /// <summary>
    /// In sunder combat maneuvers, Shatterspike grants +4 bonus on the opposed roll.
    /// The wielder is also treated as having Improved Sunder (no AoO for sunder attempts).
    /// Note: Actual sunder implementation should check for this behavior.
    /// </summary>
    public override void OnPreAttackRoll(CharacterController target, ref int attackBonus, List<string> logNotes)
    {
        // The sunder bonus is applied contextually — the combat system should check
        // SpecificItemBehavior for sunder-specific bonuses when resolving sunder attempts.
        // Standard attacks use the base +1 enhancement without the extra +4.
    }

    /// <summary>
    /// Check if wielder has Improved Sunder from Shatterspike.
    /// Called by combat system during sunder resolution.
    /// </summary>
    public bool GrantsImprovedSunder => true;

    /// <summary>
    /// Get the bonus on sunder opposed roll.
    /// </summary>
    public int GetSunderBonus()
    {
        return SunderBonus;
    }
}
