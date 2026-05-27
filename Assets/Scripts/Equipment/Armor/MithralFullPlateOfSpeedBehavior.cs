using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mithral Full Plate of Speed (SRD): +1 mithral full plate that grants the
/// wearer the ability to activate haste (as the spell) for up to 10 rounds per day.
/// The rounds need not be consecutive. Activating/deactivating is a free action.
/// Haste grants: +1 attack bonus, +1 AC (dodge), +1 Reflex save, +30 ft speed,
/// and one extra attack at full BAB on a full attack action.
/// </summary>
public class MithralFullPlateOfSpeedBehavior : SpecificItemBehavior
{
    private const int MaxRoundsPerDay = 10;
    private const int HasteAttackBonusValue = 1;
    private const int HasteACBonusValue = 1;
    private const int HasteReflexBonusValue = 1;

    private int _roundsRemaining;
    private bool _hasteActive;

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _roundsRemaining = MaxRoundsPerDay;
        _hasteActive = false;
    }

    public override bool CanActivate()
    {
        // Can activate to toggle haste on/off
        return IsEquipped && (_hasteActive || _roundsRemaining > 0);
    }

    public override string GetActivateDescription()
    {
        if (_hasteActive)
            return $"Deactivate haste. ({_roundsRemaining}/{MaxRoundsPerDay} rounds remaining)";
        return $"Activate haste: +1 attack, +1 dodge AC, +1 Reflex, extra attack. ({_roundsRemaining}/{MaxRoundsPerDay} rounds remaining)";
    }

    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (_hasteActive)
        {
            _hasteActive = false;
            logNotes.Add("Mithral Full Plate of Speed: haste deactivated.");
            Log("Haste deactivated");
            return true;
        }

        if (_roundsRemaining <= 0)
        {
            logNotes.Add("Mithral Full Plate of Speed: no haste rounds remaining today.");
            return false;
        }

        _hasteActive = true;
        logNotes.Add($"Mithral Full Plate of Speed: haste activated! ({_roundsRemaining} rounds remaining)");
        Log("Haste activated");
        return true;
    }

    /// <summary>
    /// Call at start of each round when haste is active to decrement counter.
    /// </summary>
    public void OnRoundStart()
    {
        if (_hasteActive)
        {
            _roundsRemaining--;
            if (_roundsRemaining <= 0)
            {
                _hasteActive = false;
                Log("Haste expired — no rounds remaining");
            }
        }
    }

    public bool IsHasteActive => _hasteActive;

    /// <summary>
    /// Apply haste bonuses to CharacterStats using the existing haste fields.
    /// These integrate automatically into attack rolls, AC, and Reflex saves.
    /// </summary>
    public override void ApplyPassiveStatBonuses(CharacterStats stats)
    {
        if (stats == null || !_hasteActive) return;
        stats.HasteAttackBonus = HasteAttackBonusValue;
        stats.HasteACBonus = HasteACBonusValue;
        stats.HasteReflexBonus = HasteReflexBonusValue;
        Log("Haste stat bonuses applied");
    }

    public override void RemovePassiveStatBonuses(CharacterStats stats)
    {
        if (stats == null) return;
        // Only remove if we set them
        if (stats.HasteAttackBonus == HasteAttackBonusValue)
            stats.HasteAttackBonus = 0;
        if (stats.HasteACBonus == HasteACBonusValue)
            stats.HasteACBonus = 0;
        if (stats.HasteReflexBonus == HasteReflexBonusValue)
            stats.HasteReflexBonus = 0;
    }

    public override void OnLongRest()
    {
        _roundsRemaining = MaxRoundsPerDay;
        _hasteActive = false;
    }

    public override string GetUsesDisplay()
    {
        string status = _hasteActive ? " [ACTIVE]" : "";
        return $"{_roundsRemaining}/{MaxRoundsPerDay} haste rounds{status}";
    }
}
