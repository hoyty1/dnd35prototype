using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Nine Lives Stealer (SRD / DMG p.228)
//
// +2 longsword with 1d8+1 charges. On a confirmed critical hit against a
// living creature with ≤100 HP: Fort DC 20 or instant death (uses 1 charge).
// No effect on undead, constructs, or creatures immune to critical hits.
// Useless when all charges are depleted (still functions as +2 longsword).
// ============================================================================

/// <summary>
/// Nine Lives Stealer specific item behavior.
/// Tracks charges and triggers instant death effect on critical hits.
/// </summary>
public class NineLivesStealerBehavior : SpecificItemBehavior
{
    private const int InstantDeathDC = 20;
    private const int HPThreshold = 100;

    private int _chargesRemaining;

    public override string DisplayName => "Nine Lives Stealer";

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);

        // Roll 1d8+1 charges on creation (PHB: 1d8+1 = 2–9 charges)
        _chargesRemaining = DiceService.D8("Nine Lives Stealer charge roll") + 1;
        Log($"Created with {_chargesRemaining} charges");
    }

    /// <summary>
    /// On confirmed critical hit: if target is living with ≤100 HP,
    /// Fort DC 20 or instant death. Uses 1 charge.
    /// </summary>
    public override void OnCriticalHit(CharacterController target, int damage, List<string> logNotes)
    {
        if (target == null || target.Stats == null) return;
        if (!IsEquipped || Wielder == null) return;

        // Check charges
        if (_chargesRemaining <= 0)
        {
            logNotes.Add($"☠️ Nine Lives Stealer: all charges depleted — no death effect.");
            return;
        }

        // Only affects living creatures — not undead or constructs
        string creatureType = target.Stats.CreatureType ?? "";
        if (creatureType.Equals("Undead", System.StringComparison.OrdinalIgnoreCase) ||
            creatureType.Equals("Construct", System.StringComparison.OrdinalIgnoreCase))
        {
            logNotes.Add($"☠️ Nine Lives Stealer: {target.Stats.CharacterName} is {creatureType} — no death effect.");
            return;
        }

        // Creatures immune to critical hits are immune to the death effect
        if (target.Stats.CreatureImmunities != null && target.Stats.CreatureImmunities.immuneToCriticalHits)
        {
            logNotes.Add($"☠️ Nine Lives Stealer: {target.Stats.CharacterName} is immune to critical hit effects.");
            return;
        }

        // Only affects creatures with ≤100 current HP
        if (target.Stats.TotalMaxHP > HPThreshold)
        {
            logNotes.Add($"☠️ Nine Lives Stealer: {target.Stats.CharacterName} has too many HP ({target.Stats.TotalMaxHP} > {HPThreshold}) — no death effect.");
            return;
        }

        // Fort save DC 20 or die
        var save = SavingThrowResolver.ResolveFortitudeSave(target.Stats, InstantDeathDC, DisplayName);

        if (!save.Succeeded)
        {
            // Instant death — consume a charge
            _chargesRemaining--;

            logNotes.Add($"☠️💀 <color=#8B0000>Nine Lives Stealer SLAYS {target.Stats.CharacterName}!</color> " +
                         $"(Fort DC {InstantDeathDC}: roll {save.Roll}, total {save.Total})");
            logNotes.Add($"☠️ Nine Lives Stealer charges remaining: {_chargesRemaining}");
            Log($"{target.Stats.CharacterName} slain (Fort {save.Total} < DC {InstantDeathDC}). Charges: {_chargesRemaining}");

            target.Stats.CurrentHP = -100;
            target.OnDeath();
        }
        else
        {
            logNotes.Add($"☠️ {target.Stats.CharacterName} resists Nine Lives Stealer! " +
                         $"(Fort DC {InstantDeathDC}: roll {save.Roll}, total {save.Total})");
            Log($"{target.Stats.CharacterName} survives (Fort {save.Total} >= DC {InstantDeathDC})");
        }
    }

    public override bool CanActivate()
    {
        return false; // Death effect is passive on crit — no activated ability
    }

    public override string GetActivateDescription()
    {
        return _chargesRemaining > 0
            ? $"On confirmed crit vs living creature (≤{HPThreshold} HP): Fort DC {InstantDeathDC} or instant death.\nCharges: {_chargesRemaining}"
            : "All charges depleted — functions as a normal +2 longsword.";
    }

    public override string GetUsesDisplay()
    {
        return $"Death charges: {_chargesRemaining}";
    }
}
