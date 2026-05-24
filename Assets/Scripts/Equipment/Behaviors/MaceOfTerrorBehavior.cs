using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mace of Terror (SRD): +2 heavy mace. 3/day on command, creates a 30-ft cone of fear.
/// All creatures in the cone must make a Will save DC 16 or become frightened for 1d4 rounds.
/// Those who succeed are shaken for 1 round instead.
/// </summary>
public class MaceOfTerrorBehavior : SpecificItemBehavior
{
    private const int FearDC = 16;
    private const int MaxUsesPerDay = 3;

    private int _usesRemaining;

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _usesRemaining = MaxUsesPerDay;
    }

    public override bool CanActivate()
    {
        return IsEquipped && _usesRemaining > 0;
    }

    public override string GetActivateDescription()
    {
        return $"30-ft cone of fear. Will DC {FearDC} or frightened 1d4 rounds; shaken 1 round on success. ({_usesRemaining}/{MaxUsesPerDay} uses)";
    }

    /// <summary>
    /// Activate the fear cone against a target. In a real implementation this would
    /// affect all creatures in a 30-ft cone; here we apply it to the specified target.
    /// </summary>
    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (_usesRemaining <= 0)
        {
            logNotes.Add("Mace of Terror: no uses remaining today.");
            return false;
        }

        _usesRemaining--;

        if (target == null || target.Stats == null)
        {
            logNotes.Add("Mace of Terror: no valid target.");
            return true; // Use is expended regardless
        }

        var save = SavingThrowResolver.ResolveWillSave(target.Stats, FearDC, "Mace of Terror");

        if (!save.Succeeded)
        {
            // Frightened for 1d4 rounds
            int duration = DiceService.Roll(1, 4, "Mace of Terror fear duration");
            target.ApplyCondition(CombatConditionType.Frightened, duration, "Mace of Terror");
            logNotes.Add($"Mace of Terror: {target.Stats.CharacterName} is frightened for {duration} rounds! (Will DC {FearDC}: {save.Total})");
            Log($"{target.Stats.CharacterName} frightened {duration} rounds (Will {save.Total} < DC {FearDC})");
        }
        else
        {
            // Shaken for 1 round
            target.ApplyCondition(CombatConditionType.Shaken, 1, "Mace of Terror");
            logNotes.Add($"Mace of Terror: {target.Stats.CharacterName} is shaken for 1 round (Will DC {FearDC}: {save.Total})");
            Log($"{target.Stats.CharacterName} shaken 1 round (Will {save.Total} >= DC {FearDC})");
        }

        return true;
    }

    public override void OnLongRest()
    {
        _usesRemaining = MaxUsesPerDay;
    }

    public override string GetUsesDisplay()
    {
        return $"{_usesRemaining}/{MaxUsesPerDay} fear uses";
    }
}
