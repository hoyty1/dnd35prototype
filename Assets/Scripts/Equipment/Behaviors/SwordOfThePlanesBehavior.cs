using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sword of the Planes (SRD): +1 longsword that has variable enhancement bonus
/// depending on the creature type of the target:
///   - +1 normally (Material Plane)
///   - +2 vs extraplanar creatures (Outsider, Elemental)
///   - +3 on the Astral/Ethereal plane (treated as vs certain subtypes)
///   - +4 on any Outer Plane (treated as vs Celestial/Fiend types)
/// For our prototype, we approximate by creature type:
///   - Outsider, Elemental → +2 effective enhancement
///   - Celestial, Fiend, Demon, Devil, Angel → +3 effective enhancement
///   - Everything else → +1 (base)
/// </summary>
public class SwordOfThePlanesBehavior : SpecificItemBehavior
{
    public override void OnPreAttackRoll(CharacterController target, ref int attackBonus, List<string> logNotes)
    {
        int bonus = GetExtraEnhancement(target);
        if (bonus > 0)
        {
            attackBonus += bonus;
            logNotes.Add($"Sword of the Planes: +{bonus} attack vs {target.Stats.CreatureType}");
        }
    }

    public override void OnDamageRoll(CharacterController target, ref int damage, bool isCrit, List<string> logNotes)
    {
        int bonus = GetExtraEnhancement(target);
        if (bonus > 0)
        {
            damage += bonus;
            logNotes.Add($"Sword of the Planes: +{bonus} damage vs {target.Stats.CreatureType}");
        }
    }

    /// <summary>
    /// Returns the EXTRA enhancement bonus beyond the base +1 already on the weapon.
    /// </summary>
    private int GetExtraEnhancement(CharacterController target)
    {
        if (target?.Stats == null) return 0;

        // +3 effective vs celestials/fiends (outer planar beings) → +2 extra over base +1
        if (IsCreatureTypeAny(target, "Celestial", "Fiend", "Demon", "Devil", "Angel", "Archon"))
            return 3; // total effective +4, but base +1 is already applied → +3 extra

        // +1 effective vs outsiders/elementals → +1 extra over base +1
        if (IsCreatureTypeAny(target, "Outsider", "Elemental"))
            return 1; // total effective +2, base +1 already → +1 extra

        return 0;
    }
}
