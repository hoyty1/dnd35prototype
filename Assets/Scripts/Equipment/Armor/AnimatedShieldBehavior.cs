using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Animated Shield (SRD / DMG p.219)
//
// +2 heavy steel shield (standard) or +5 (greater variant).
// Upon command, the shield floats within 2 feet of the wielder, protecting
// them as if wielded but leaving both hands free. The animation lasts 4
// rounds, and can be activated multiple times per day.
//
// While animated: wielder gets full shield AC bonus but has both hands free
// for two-handed weapons, dual-wielding, or spellcasting.
// ============================================================================

/// <summary>
/// Animated Shield specific item behavior.
/// Grants hands-free shield AC for 4 rounds when activated.
/// </summary>
public class AnimatedShieldBehavior : SpecificItemBehavior
{
    private const int AnimationDurationRounds = 4;
    private const int MaxUsesPerDay = 2;

    private readonly bool _isGreater;
    private int _usesRemaining;
    private bool _animationActive;
    private int _animationRoundsRemaining;

    public override string DisplayName => _isGreater ? "Animated Shield (Greater)" : "Animated Shield";

    public AnimatedShieldBehavior(bool isGreater = false)
    {
        _isGreater = isGreater;
    }

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _usesRemaining = MaxUsesPerDay;
        _animationActive = false;
        _animationRoundsRemaining = 0;
    }

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);
        _animationActive = false;
        _animationRoundsRemaining = 0;
        Log($"Equipped by {character.Stats?.CharacterName} — can be animated for hands-free defense");
    }

    public override void OnUnequip()
    {
        _animationActive = false;
        _animationRoundsRemaining = 0;
        base.OnUnequip();
    }

    // ========================================================================
    //  ACTIVATED: Animate shield (2/day, 4 rounds each)
    // ========================================================================

    public override bool CanActivate()
    {
        return IsEquipped && (_animationActive || _usesRemaining > 0);
    }

    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (_animationActive)
        {
            // Dismiss animation early
            _animationActive = false;
            _animationRoundsRemaining = 0;
            logNotes?.Add($"🛡️ {DisplayName}: animation dismissed. Shield returns to arm.");
            Log("Shield animation dismissed");
            return true;
        }

        if (_usesRemaining <= 0)
        {
            logNotes?.Add($"{DisplayName}: no animation uses remaining today.");
            return false;
        }

        _usesRemaining--;
        _animationActive = true;
        _animationRoundsRemaining = AnimationDurationRounds;

        string enhText = _isGreater ? "+5" : "+2";
        logNotes?.Add($"🛡️✨ <color=#4169E1>{DisplayName}</color> ({enhText}) floats into the air, protecting {Wielder?.Stats?.CharacterName ?? "wielder"} while leaving both hands free! ({AnimationDurationRounds} rounds)");
        Log($"Shield animated for {AnimationDurationRounds} rounds ({_usesRemaining} uses remaining)");

        GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.RoyalBlue("🛡", $"️ {DisplayName} animates! Hands-free defense for {AnimationDurationRounds} rounds."));

        return true;
    }

    /// <summary>
    /// Whether the shield is currently animated (floating, hands-free).
    /// Other systems can check this to allow two-handed weapon use while shield is equipped.
    /// </summary>
    public bool IsAnimated => _animationActive;

    // ========================================================================
    //  ROUND TRACKING: Decrement animation duration
    // ========================================================================

    public void OnRoundStart()
    {

        if (_animationActive)
        {
            _animationRoundsRemaining--;
            if (_animationRoundsRemaining <= 0)
            {
                _animationActive = false;
                Log("Shield animation expired — returns to arm");

                GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.RoyalBlue("🛡", $"️ {DisplayName} stops floating and returns to {Wielder?.Stats?.CharacterName ?? "wielder"}'s arm."));
            }
            else
            {
                Log($"Shield animation: {_animationRoundsRemaining} rounds remaining");
            }
        }
    }

    // ========================================================================
    //  DISPLAY
    // ========================================================================

    public override string GetActivateDescription()
    {
        if (_animationActive)
            return $"Dismiss animation ({_animationRoundsRemaining} rounds remaining). Shield returns to arm.";

        string enhText = _isGreater ? "+5" : "+2";
        return $"Animate shield ({enhText}): floats for {AnimationDurationRounds} rounds, providing AC while both hands are free. ({_usesRemaining}/{MaxUsesPerDay} uses)";
    }

    public override string GetUsesDisplay()
    {
        if (_animationActive)
            return $"Animated ({_animationRoundsRemaining} rds)";
        return $"Animate: {_usesRemaining}/{MaxUsesPerDay}";
    }

    public override void OnLongRest()
    {
        base.OnLongRest();
        _usesRemaining = MaxUsesPerDay;
        _animationActive = false;
        _animationRoundsRemaining = 0;
        Log("Animation uses refreshed");
    }

    public override string GetTooltipText()
    {
        var lines = new List<string>();
        string enhText = _isGreater ? "+5" : "+2";
        lines.Add($"<b>{DisplayName}</b> ({enhText} heavy steel shield)");
        lines.Add($"Animate: shield floats for {AnimationDurationRounds} rounds, hands-free ({MaxUsesPerDay}/day)");
        lines.Add("While animated: full shield AC, both hands free");
        if (_animationActive)
            lines.Add($"<color=#4169E1>✨ Currently animated ({_animationRoundsRemaining} rounds)</color>");
        return string.Join("\n", lines);
    }
}
