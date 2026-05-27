using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Dwarven Thrower (SRD / DMG p.224)
//
// +2 warhammer. Only usable by dwarves.
// When thrown by a dwarf:
//   - Functions as +3 returning throwing weapon
//   - Deals extra +1d8 damage on a thrown hit
//   - Deals extra +2d8 damage against giants instead of +1d8
//   - Returns to thrower's hand immediately after each thrown attack
//   - Range increment: 30 ft (6 squares)
//
// In melee, functions as a standard +2 warhammer with no special abilities.
// Non-dwarves cannot wield it effectively (no enhancement or special abilities).
// ============================================================================

/// <summary>
/// Dwarven Thrower specific item behavior.
/// Race-restricted warhammer with enhanced thrown capabilities.
/// </summary>
public class DwarvenThrowerBehavior : SpecificItemBehavior
{
    // Extra +1 enhancement when thrown (base +2 → effective +3)
    private const int ThrownBonusEnhancement = 1;

    public override string DisplayName => "Dwarven Thrower";

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);

        if (character.Stats == null) return;

        // Check race restriction
        string race = character.Stats.RaceName ?? "";
        if (!race.Equals("Dwarf", System.StringComparison.OrdinalIgnoreCase))
        {
            Log($"WARNING: {character.Stats.CharacterName} ({race}) equips Dwarven Thrower — no special abilities for non-dwarves");
            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Damage("⚠", $"{character.Stats.CharacterName} is not a dwarf — Dwarven Thrower functions as a normal warhammer."));
        }
        else
        {
            Log($"Equipped by dwarf {character.Stats.CharacterName}");
            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"🪓 Dwarven Thrower bonds with {character.Stats.CharacterName}!"));
        }
    }

    /// <summary>
    /// When attacking at range (thrown), grant +1 attack bonus (effective +3).
    /// Only works for dwarf wielders.
    /// </summary>
    public override void OnPreAttackRoll(CharacterController target, ref int attackBonus, List<string> logNotes)
    {
        if (target == null || target.Stats == null) return;
        if (!IsEquipped || Wielder == null) return;
        if (!IsDwarfWielder()) return;

        // Detect thrown attack: target is not adjacent to wielder
        if (!IsTargetAdjacent(target)) 
        {
            attackBonus += ThrownBonusEnhancement;
            logNotes.Add($"🪓 Dwarven Thrower: +{ThrownBonusEnhancement} thrown enhancement (effective +3).");
        }
    }

    /// <summary>
    /// When hitting at range (thrown), deal extra +1d8 damage (+2d8 vs giants).
    /// </summary>
    public override void OnDamageRoll(CharacterController target, ref int damage, bool isCrit, List<string> logNotes)
    {
        if (target == null || target.Stats == null) return;
        if (!IsEquipped || Wielder == null) return;
        if (!IsDwarfWielder()) return;

        // Only on thrown attacks (non-adjacent)
        if (IsTargetAdjacent(target)) return;

        string creatureType = target.Stats.CreatureType ?? "";
        bool isGiant = creatureType.Equals("Giant", System.StringComparison.OrdinalIgnoreCase);

        if (isGiant)
        {
            // +2d8 vs giants
            int bonus = DiceService.Roll(1, 8, "Dwarven Thrower vs Giant (1)") +
                        DiceService.Roll(1, 8, "Dwarven Thrower vs Giant (2)");
            damage += bonus;
            logNotes.Add($"🪓 <color=#FF4500>GIANT SLAYER! Dwarven Thrower deals +{bonus} ({ThrownBonusEnhancement} enh + 2d8) damage!</color>");
            Log($"Thrown vs Giant: +{bonus} extra damage");
        }
        else
        {
            // +1d8 thrown damage
            int bonus = DiceService.D8("Dwarven Thrower thrown damage");
            damage += bonus;
            logNotes.Add($"🪓 Dwarven Thrower: +{bonus} thrown damage (1d8).");
            Log($"Thrown: +{bonus} extra damage");
        }

        // Extra +1 to damage from higher enhancement (to match +3 damage in thrown mode)
        damage += ThrownBonusEnhancement;
    }

    /// <summary>
    /// After hitting a target at range, the weapon returns to the wielder's hand.
    /// </summary>
    public override void OnHitApplied(CharacterController target, int finalDamage, List<string> logNotes)
    {
        if (target == null || target.Stats == null) return;
        if (!IsEquipped || Wielder == null) return;
        if (!IsDwarfWielder()) return;

        // Returning behavior on thrown hit
        if (!IsTargetAdjacent(target))
        {
            logNotes.Add($"🪓 Dwarven Thrower returns to {Wielder.Stats.CharacterName}'s hand!");
            Log("Returned after thrown attack");
        }
    }

    public override bool CanActivate()
    {
        return false; // All abilities are passive/reactive
    }

    public override string GetActivateDescription()
    {
        if (Wielder == null || Wielder.Stats == null)
            return "Dwarf-only +2 warhammer. +3 returning when thrown. +1d8 thrown, +2d8 vs giants.";

        bool isDwarf = IsDwarfWielder();
        return isDwarf
            ? "Active: +2 melee / +3 returning thrown. +1d8 thrown damage (+2d8 vs giants). Returns to hand."
            : "⚠ Non-dwarf wielder — functions as normal warhammer (no special abilities).";
    }

    // ========================================================================
    //  HELPERS
    // ========================================================================

    private bool IsDwarfWielder()
    {
        if (Wielder == null || Wielder.Stats == null) return false;
        string race = Wielder.Stats.RaceName ?? "";
        return race.Equals("Dwarf", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool IsTargetAdjacent(CharacterController target)
    {
        if (Wielder == null || target == null) return true; // Assume melee if unknown
        return SquareGridUtils.IsAdjacent(Wielder.GridPosition, target.GridPosition);
    }
}
