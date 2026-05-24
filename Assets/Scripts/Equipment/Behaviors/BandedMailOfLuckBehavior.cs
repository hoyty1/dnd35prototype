using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Banded Mail of Luck (SRD): +3 banded mail that allows the wearer to force
/// an enemy to reroll one successful attack roll against them, 1/week.
/// The enemy must use the second roll result. This does NOT grant any
/// passive AC or save bonus — it's purely a forced reroll.
/// </summary>
public class BandedMailOfLuckBehavior : SpecificItemBehavior
{
    private bool _rerollAvailable;

    // Track days since last use — recharges after 7 days (long rests)
    private int _daysUntilRecharge;

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _rerollAvailable = true;
        _daysUntilRecharge = 0;
    }

    /// <summary>
    /// When the wearer is attacked and the attack hits, this can force a reroll.
    /// </summary>
    public override void OnAttackedBy(CombatResult attackResult, ref bool forceReroll, List<string> logNotes)
    {
        // Only trigger on hits, and only if the reroll is available
        if (!_rerollAvailable) return;
        if (!IsEquipped) return;

        // Only force reroll on successful attack rolls (not natural 20s — those always hit regardless)
        if (attackResult == null) return;
        if (attackResult.DieRoll == 20) return; // Natural 20 always hits — reroll would be wasted
        if (!attackResult.Hit) return; // Only trigger on hits

        _rerollAvailable = false;
        _daysUntilRecharge = 7;
        forceReroll = true;

        logNotes.Add($"Banded Mail of Luck: forces attacker to reroll attack! (1/week used)");
        Log("Forced enemy attack reroll (1/week expended)");
    }

    public override bool CanActivate()
    {
        // The reroll is reactive — it triggers automatically when attacked.
        // But we expose it here so the UI can show the status.
        return false;
    }

    public override string GetActivateDescription()
    {
        if (!_rerollAvailable)
            return $"Luck reroll used. Recharges in {_daysUntilRecharge} day(s).";
        return "Passive: when hit by an attack, automatically forces the attacker to reroll (1/week).";
    }

    public override void OnLongRest()
    {
        if (!_rerollAvailable)
        {
            _daysUntilRecharge--;
            if (_daysUntilRecharge <= 0)
            {
                _rerollAvailable = true;
                _daysUntilRecharge = 0;
                Log("Luck reroll recharged (1 week elapsed)");
            }
        }
    }

    public override string GetUsesDisplay()
    {
        return _rerollAvailable ? "Luck reroll available (1/week)" : $"Recharging ({_daysUntilRecharge} day(s))";
    }
}
