using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rapier of Puncturing (SRD): +2 wounding rapier. In addition to the standard
/// Wounding property (1 CON damage on hit), 3/day as a free action on a successful
/// hit the wielder can activate the rapier to deal 1d6 Constitution damage to the target.
/// This activated ability is a touch-range effect resolved as part of the hit.
/// </summary>
public class RapierOfPuncturingBehavior : SpecificItemBehavior
{
    private const int MaxUsesPerDay = 3;
    private const int ConDamageDice = 6; // 1d6

    private int _usesRemaining;
    private bool _activateOnNextHit;

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _usesRemaining = MaxUsesPerDay;
        _activateOnNextHit = false;
    }

    public override bool CanActivate()
    {
        return IsEquipped && _usesRemaining > 0;
    }

    public override string GetActivateDescription()
    {
        return $"On next hit, deal 1d6 CON damage (touch attack). ({_usesRemaining}/{MaxUsesPerDay} uses)";
    }

    /// <summary>
    /// Activate to queue the CON damage for the next hit.
    /// </summary>
    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (_usesRemaining <= 0)
        {
            logNotes.Add("Rapier of Puncturing: no uses remaining today.");
            return false;
        }

        _activateOnNextHit = true;
        logNotes.Add("Rapier of Puncturing: activated — next hit deals 1d6 CON damage.");
        return true; // Don't decrement yet — decrement on actual hit
    }

    public override void OnHitApplied(CharacterController target, int finalDamage, List<string> logNotes)
    {
        if (target == null || target.Stats == null) return;

        // Standard Wounding effect: 1 CON damage per hit (this is the base enchantment)
        // Note: The Wounding enchantment is already handled by EnchantmentEffects.
        // We only handle the EXTRA activated 1d6 Con damage here.

        if (_activateOnNextHit && _usesRemaining > 0)
        {
            _activateOnNextHit = false;
            _usesRemaining--;

            int conDamage = DiceService.D6("Rapier of Puncturing CON damage");
            target.ApplyAbilityDamage(AbilityType.CON, conDamage, "Rapier of Puncturing");
            logNotes.Add($"Rapier of Puncturing: {target.Stats.CharacterName} takes {conDamage} CON damage! ({_usesRemaining}/{MaxUsesPerDay} uses left)");
            Log($"{target.Stats.CharacterName} takes {conDamage} CON damage");
        }
    }

    public override void OnLongRest()
    {
        _usesRemaining = MaxUsesPerDay;
        _activateOnNextHit = false;
    }

    public override string GetUsesDisplay()
    {
        return $"{_usesRemaining}/{MaxUsesPerDay} puncturing uses";
    }
}
