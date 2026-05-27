using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Luck Blade — Specific Weapon (SRD / DMG p.227)
//
// A +2 short sword that grants:
//   1. +1 luck bonus on all saving throws (passive, while equipped)
//   2. 1/day reroll of any d20 roll the wielder just made (standard action to decide)
//   3. 0–3 wish charges (permanent; do not refresh on rest)
//      Wishes from a Luck Blade have no XP cost.
//
// Variants: Luck Blade (0 wishes), Luck Blade (1 wish), etc.
// ============================================================================

/// <summary>
/// Luck Blade specific item behavior. Tracks wish charges and provides
/// +1 luck save bonus + 1/day reroll.
/// </summary>
public class LuckBladeBehavior : SpecificItemBehavior
{
    // Number of wish charges remaining (permanent, not refreshed)
    private int _wishCharges;
    private int _initialWishCharges;

    // 1/day reroll
    private bool _rerollAvailable;

    /// <summary>
    /// Create a Luck Blade behavior with the given number of initial wish charges.
    /// </summary>
    /// <param name="wishCharges">Number of wish charges (0–3).</param>
    public LuckBladeBehavior(int wishCharges = 0)
    {
        _wishCharges = Mathf.Clamp(wishCharges, 0, 3);
        _initialWishCharges = _wishCharges;
        _rerollAvailable = true;
    }

    public override string DisplayName => "Luck Blade";

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _rerollAvailable = true;
    }

    // ========================================================================
    //  PASSIVE: +1 luck bonus to all saving throws (SRD)
    // ========================================================================

    public override void ApplyPassiveStatBonuses(CharacterStats stats)
    {
        if (stats == null) return;
        // D&D 3.5e: luck bonuses of the same type don't stack;
        // only the highest applies. For simplicity, we add +1 and
        // trust that other luck items are uncommon in a typical game.
        stats.LuckSaveBonus += 1;
    }

    public override void RemovePassiveStatBonuses(CharacterStats stats)
    {
        if (stats == null) return;
        stats.LuckSaveBonus -= 1;
        if (stats.LuckSaveBonus < 0) stats.LuckSaveBonus = 0;
    }

    // ========================================================================
    //  ACTIVATED: Use a Wish charge (no XP cost)
    // ========================================================================

    public override bool CanActivate()
    {
        return IsEquipped && _wishCharges > 0;
    }

    public override string GetActivateDescription()
    {
        string wishInfo = _wishCharges > 0
            ? $"{_wishCharges} wish charge(s) remaining — activate to cast Wish (no XP cost)"
            : "No wish charges remaining";
        string rerollInfo = _rerollAvailable
            ? "Reroll: available (1/day)"
            : "Reroll: used today";
        return $"{wishInfo}\n{rerollInfo}\nPassive: +1 luck bonus to all saves";
    }

    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (!IsEquipped || Wielder == null)
        {
            logNotes?.Add("Luck Blade: not equipped.");
            return false;
        }

        if (_wishCharges <= 0)
        {
            logNotes?.Add("Luck Blade: no wish charges remaining.");
            return false;
        }

        _wishCharges--;
        logNotes?.Add($"⚔ Luck Blade: expending a wish charge ({_wishCharges} remaining).");
        Log($"Wish charge expended. Remaining: {_wishCharges}");

        // Delegate to GameManager's item wish handler (opens WishUI for player, auto-decides for AI)
        GameManager.Instance?.HandleItemWishCast(Wielder);

        return true;
    }

    // ========================================================================
    //  DEFENSIVE: 1/day reroll of an attack against the wielder
    // ========================================================================
    // The SRD reroll can apply to any d20 roll by the wielder. For combat
    // purposes, we implement it as a forced re-roll when the wielder is hit
    // by an attack (similar to Banded Mail of Luck, but from the wielder's
    // perspective). A full implementation would hook into the saving throw
    // system as well, but that requires broader changes.

    public override void OnAttackedBy(CombatResult attackResult, ref bool forceReroll, List<string> logNotes)
    {
        if (!_rerollAvailable || !IsEquipped) return;
        if (attackResult == null || !attackResult.Hit) return;
        if (attackResult.DieRoll == 20) return; // Natural 20 always hits

        _rerollAvailable = false;
        forceReroll = true;
        logNotes?.Add($"⚔ Luck Blade: forces attacker to reroll! (1/day used)");
        Log("Forced attacker reroll (1/day expended)");
    }

    // ========================================================================
    //  LONG REST: Reroll refreshes; wish charges do NOT refresh
    // ========================================================================

    public override void OnLongRest()
    {
        _rerollAvailable = true;
        // Wish charges are permanent — they never refresh.
    }

    public override string GetUsesDisplay()
    {
        var parts = new List<string>();

        if (_initialWishCharges > 0)
            parts.Add($"Wishes: {_wishCharges}/{_initialWishCharges}");

        parts.Add(_rerollAvailable ? "Reroll: ready" : "Reroll: used");

        return string.Join(" | ", parts);
    }
}
