using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Armor of Rage (SRD / DMG p.220)
//
// +1 breastplate. When worn by a barbarian who activates rage:
//   - Rage bonuses increase by +2 (total +6 STR, +6 CON instead of +4/+4)
//   - The extra bonuses are removed when rage ends
//
// Has no special effect for non-barbarians.
// Standard breastplate stats: Max Dex +3, ACP -4, ASF 25%, 30 lbs, speed 20 ft
// ============================================================================

/// <summary>
/// Armor of Rage specific item behavior.
/// Enhances barbarian rage to provide +6 STR/CON instead of the normal +4.
/// Monitors rage state each round via ApplyPassiveStatBonuses.
/// </summary>
public class ArmorOfRageBehavior : SpecificItemBehavior
{
    private const int BonusRageEnhancement = 2; // Extra +2 on top of normal +4

    /// <summary>
    /// Whether we have currently applied the extra rage bonuses.
    /// Tracks state to avoid double-applying or failing to remove.
    /// </summary>
    private bool _enhancementActive;

    public override string DisplayName => "Armor of Rage";

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _enhancementActive = false;
    }

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);
        _enhancementActive = false;

        if (character.Stats != null && character.Stats.IsBarbarian)
            Log($"Equipped on barbarian {character.Stats.CharacterName} — will enhance rage");
    }

    public override void OnUnequip()
    {
        // If rage enhancement is active when unequipping, remove it
        if (_enhancementActive && Wielder != null && Wielder.Stats != null)
        {
            RemoveRageEnhancement(Wielder.Stats);
        }

        base.OnUnequip();
    }

    // ========================================================================
    //  PASSIVE STAT BONUSES: Apply/remove rage enhancement based on state
    // ========================================================================

    /// <summary>
    /// Called during stat recalculation. If the wearer is a barbarian who is
    /// currently raging, apply the extra +2 STR/+2 CON enhancement.
    /// </summary>
    public override void ApplyPassiveStatBonuses(CharacterStats stats)
    {
        if (stats == null) return;

        // Only enhances barbarian rage
        if (!stats.IsBarbarian) return;

        if (stats.IsRaging && !_enhancementActive)
        {
            ApplyRageEnhancement(stats);
        }
        else if (!stats.IsRaging && _enhancementActive)
        {
            RemoveRageEnhancement(stats);
        }
    }

    public override void RemovePassiveStatBonuses(CharacterStats stats)
    {
        if (stats == null) return;

        if (_enhancementActive)
        {
            RemoveRageEnhancement(stats);
        }
    }

    // ========================================================================
    //  ACTIVATED: Show status (no manual activation needed)
    // ========================================================================

    public override bool CanActivate()
    {
        return false; // Enhancement is automatic during rage
    }

    public override string GetActivateDescription()
    {
        if (Wielder == null || Wielder.Stats == null)
            return "Passive: Enhances barbarian rage (+6 STR/CON instead of +4).";

        if (!Wielder.Stats.IsBarbarian)
            return "No effect — wearer is not a barbarian.";

        if (_enhancementActive)
            return "ACTIVE: Rage enhanced! +6 STR/CON (instead of normal +4).";

        return "Ready: Will enhance next rage (+6 STR/CON instead of +4).";
    }

    public override string GetUsesDisplay()
    {
        if (_enhancementActive)
            return "Rage enhanced";
        return null;
    }

    // ========================================================================
    //  INTERNAL HELPERS
    // ========================================================================

    private void ApplyRageEnhancement(CharacterStats stats)
    {
        stats.STR += BonusRageEnhancement;
        stats.CON += BonusRageEnhancement;

        // Additional HP from CON increase: +1 HP per level (from +2 CON → +1 CON mod)
        int hpGain = stats.Level * 1;
        stats.MaxHP += hpGain;
        stats.CurrentHP += hpGain;

        _enhancementActive = true;
        Log($"Rage enhancement applied: +{BonusRageEnhancement} STR/CON, +{hpGain} HP");

        GameManager.Instance?.CombatUI?.ShowCombatLog(
            $"<color=#FF4500>💢 Armor of Rage amplifies {stats.CharacterName}'s fury! " +
            $"(+{4 + BonusRageEnhancement} STR/CON total)</color>");
    }

    private void RemoveRageEnhancement(CharacterStats stats)
    {
        stats.STR -= BonusRageEnhancement;
        stats.CON -= BonusRageEnhancement;

        int hpLoss = stats.Level * 1;
        stats.MaxHP -= hpLoss;
        if (stats.CurrentHP > stats.MaxHP) stats.CurrentHP = stats.MaxHP;
        if (stats.CurrentHP < -10) stats.CurrentHP = -10;

        _enhancementActive = false;
        Log("Rage enhancement removed");
    }
}
