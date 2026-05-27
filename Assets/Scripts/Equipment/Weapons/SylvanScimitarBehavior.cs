using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sylvan Scimitar (SRD): +3 scimitar that grants the wielder the Cleave feat.
/// When outdoors in a temperate environment, deals an additional +1d6 damage on every hit.
/// NOT vorpal — the SRD does not give this weapon vorpal properties.
/// </summary>
public class SylvanScimitarBehavior : SpecificItemBehavior
{
    /// <summary>
    /// Whether the current environment is outdoors in temperate climate.
    /// This should be set by the encounter/environment system.
    /// Defaults to true for prototype since most encounters are outdoor.
    /// </summary>
    public bool IsOutdoorsTemperate = true;

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);
        Log("Equipped: grants Cleave feat");
        // In full implementation: grant Cleave feat to wielder's feat list
    }

    public override void OnDamageRoll(CharacterController target, ref int damage, bool isCrit, List<string> logNotes)
    {
        if (!IsOutdoorsTemperate) return;

        int bonusDamage = DiceService.D6("Sylvan Scimitar outdoor bonus");
        damage += bonusDamage;
        logNotes.Add($"Sylvan Scimitar: +{bonusDamage} damage (outdoors temperate)");
    }

    /// <summary>
    /// Check if the wielder has the Cleave feat granted by this weapon.
    /// Called by combat system during Cleave resolution.
    /// </summary>
    public bool GrantsCleave => true;

    public override void OnKill(CharacterController target, List<string> logNotes)
    {
        if (!IsEquipped || Wielder == null) return;

        // Cleave: On killing a target, wielder gets a free melee attack against an adjacent foe.
        // In a real implementation this would trigger the Cleave feat logic.
        logNotes.Add("Sylvan Scimitar: Cleave opportunity available (killed target)");
        Log("Kill triggers Cleave opportunity");
    }
}
