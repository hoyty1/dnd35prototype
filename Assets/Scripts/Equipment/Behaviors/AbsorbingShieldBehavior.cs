using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Absorbing Shield (SRD): +1 heavy steel shield that can disintegrate one object
/// per 2 days by touch. The shield absorbs the target object, destroying it utterly.
/// This functions like the disintegrate spell but only against objects (not creatures).
/// The shield can hold a maximum of one absorbed object at a time.
/// Recharges every 2 days.
/// </summary>
public class AbsorbingShieldBehavior : SpecificItemBehavior
{
    private bool _abilityAvailable;
    private int _daysUntilRecharge;

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _abilityAvailable = true;
        _daysUntilRecharge = 0;
    }

    public override bool CanActivate()
    {
        return IsEquipped && _abilityAvailable;
    }

    public override string GetActivateDescription()
    {
        if (!_abilityAvailable)
            return $"Disintegrate recharging ({_daysUntilRecharge} day(s) remaining).";
        return "Touch an object to disintegrate it utterly. Recharges in 2 days.";
    }

    /// <summary>
    /// Activate to disintegrate an object. In combat, this could be used to destroy
    /// an enemy's weapon or equipment.
    /// </summary>
    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (!_abilityAvailable)
        {
            logNotes.Add($"Absorbing Shield: ability recharging ({_daysUntilRecharge} day(s) remaining).");
            return false;
        }

        _abilityAvailable = false;
        _daysUntilRecharge = 2;

        logNotes.Add("Absorbing Shield: disintegrates the touched object!");
        Log("Disintegrate object used — recharges in 2 days");

        // In a full implementation, this would target a specific item on the target
        // or a held/worn object and destroy it.
        return true;
    }

    public override void OnLongRest()
    {
        if (!_abilityAvailable)
        {
            _daysUntilRecharge--;
            if (_daysUntilRecharge <= 0)
            {
                _abilityAvailable = true;
                _daysUntilRecharge = 0;
                Log("Disintegrate recharged");
            }
        }
    }

    public override string GetUsesDisplay()
    {
        return _abilityAvailable ? "Disintegrate ready" : $"Recharging ({_daysUntilRecharge} day(s))";
    }
}
