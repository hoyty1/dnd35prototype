using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Winged Shield (SRD): +3 heavy wooden shield that can fly off the wielder's arm
/// and grant the fly spell effect 1/day. While flying, the shield provides no
/// AC bonus. The fly effect lasts for 5 minutes (50 rounds) or until dismissed.
/// Caster level 5th, good maneuverability.
/// </summary>
public class WingedShieldBehavior : SpecificItemBehavior
{
    private const int MaxUsesPerDay = 1;
    private const int FlyDurationRounds = 50; // 5 minutes

    private int _usesRemaining;
    private bool _flyActive;
    private int _flyRoundsRemaining;

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _usesRemaining = MaxUsesPerDay;
        _flyActive = false;
    }

    public override bool CanActivate()
    {
        return IsEquipped && (_flyActive || _usesRemaining > 0);
    }

    public override string GetActivateDescription()
    {
        if (_flyActive)
            return $"Dismiss fly effect. ({_flyRoundsRemaining} rounds remaining)";
        return $"Activate fly spell (5 min, good maneuverability). Shield provides no AC while flying. ({_usesRemaining}/{MaxUsesPerDay} uses)";
    }

    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (_flyActive)
        {
            // Dismiss fly
            _flyActive = false;
            _flyRoundsRemaining = 0;
            logNotes.Add("Winged Shield: fly effect dismissed. Shield AC restored.");
            Log("Fly dismissed");
            return true;
        }

        if (_usesRemaining <= 0)
        {
            logNotes.Add("Winged Shield: fly already used today.");
            return false;
        }

        _usesRemaining--;
        _flyActive = true;
        _flyRoundsRemaining = FlyDurationRounds;

        logNotes.Add($"Winged Shield: fly activated! Good maneuverability, {FlyDurationRounds} rounds. Shield provides no AC bonus while active.");
        Log("Fly activated");
        return true;
    }

    public bool IsFlyActive => _flyActive;

    /// <summary>
    /// Call at start of each round to decrement fly duration.
    /// </summary>
    public void OnRoundStart()
    {
        if (_flyActive)
        {
            _flyRoundsRemaining--;
            if (_flyRoundsRemaining <= 0)
            {
                _flyActive = false;
                Log("Fly expired");
            }
        }
    }

    /// <summary>
    /// While fly is active, shield provides no AC bonus.
    /// </summary>
    public override void OnDefendAgainstAttack(CharacterController attacker, ref int acBonus, List<string> logNotes)
    {
        if (_flyActive)
        {
            // Remove the shield's AC contribution while flying
            // The shield normally gives +3 enhancement + shield base
            // We apply a negative modifier to negate it
            int shieldAC = (Item?.ShieldBonus ?? 0) + (Item?.EnhancementBonus ?? 0);
            if (shieldAC > 0)
            {
                acBonus -= shieldAC;
                logNotes.Add($"Winged Shield: no AC bonus while flying (-{shieldAC})");
            }
        }
    }

    public override void OnLongRest()
    {
        _usesRemaining = MaxUsesPerDay;
        _flyActive = false;
        _flyRoundsRemaining = 0;
    }

    public override string GetUsesDisplay()
    {
        if (_flyActive) return $"Flying ({_flyRoundsRemaining} rounds remaining)";
        return _usesRemaining > 0 ? "Fly available" : "Fly expended today";
    }
}
