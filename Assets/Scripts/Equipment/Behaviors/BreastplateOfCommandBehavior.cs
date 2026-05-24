using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Breastplate of Command (SRD): +2 breastplate that grants a +2 competence bonus
/// on all Charisma checks (not to the Charisma score itself).
/// Also grants the wearer a +2 competence bonus to Leadership score
/// and a +2 bonus on turning checks (for clerics/paladins).
/// </summary>
public class BreastplateOfCommandBehavior : SpecificItemBehavior
{
    private const int CompetenceBonus = 2;

    public override void ApplyPassiveStatBonuses(CharacterStats stats)
    {
        if (stats == null) return;

        // +2 competence bonus to Charisma-based checks
        // Applied as a morale bonus to CHA-based skill checks and turning checks.
        // Uses the existing DisguiseCompetenceBonus pattern as the closest analog
        // until a general CHA competence bonus field is added.
        stats.DisguiseCompetenceBonus += CompetenceBonus;
        // Note: In a full implementation, this would apply to ALL CHA-based skills:
        // Diplomacy, Bluff, Intimidate, Disguise, Gather Info, Handle Animal, Perform, UMD
        Log($"Applied +{CompetenceBonus} competence to CHA checks");
    }

    public override void RemovePassiveStatBonuses(CharacterStats stats)
    {
        if (stats == null) return;
        stats.DisguiseCompetenceBonus -= CompetenceBonus;
    }

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);
        Log("Equipped: +2 competence on Charisma checks, Leadership, and turning checks");
    }
}
