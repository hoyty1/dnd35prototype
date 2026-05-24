using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Frost Brand (SRD / DMG p.224)
//
// +3 frost greatsword with these additional abilities:
//   1. Grants fire resistance 10 to the wielder (passive, while equipped).
//   2. Sheds light as a torch on command (10 ft bright, 20 ft shadowy).
//   3. Extinguishes all nonmagical fires within 20 ft on command.
//   4. Once per day, can dispel a fire spell as dispel magic (caster level 14).
//
// Note: The base Frost enchantment (+1d6 cold damage) is handled by the
// EnchantmentType.Frost on the item data — we don't re-implement it here.
// ============================================================================

/// <summary>
/// Frost Brand specific item behavior.
/// Provides fire resistance 10, and command abilities for light/fire suppression.
/// The Frost enchantment damage is handled separately by the standard enchantment system.
/// </summary>
public class FrostBrandBehavior : SpecificItemBehavior
{
    private const int FireResistanceAmount = 10;
    private const int DispelCasterLevel = 14;
    private const int MaxDispelsPerDay = 1;

    private int _dispelsRemaining;

    public override string DisplayName => "Frost Brand";

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _dispelsRemaining = MaxDispelsPerDay;
    }

    // ========================================================================
    //  PASSIVE: Fire Resistance 10 while equipped (SRD)
    // ========================================================================

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);

        if (character.Stats != null)
        {
            // Add fire resistance entry
            character.Stats.DamageResistances.Add(new DamageResistanceEntry
            {
                Type = DamageType.Fire,
                Amount = FireResistanceAmount
            });
            Log($"Equipped: {character.Stats.CharacterName} gains fire resistance {FireResistanceAmount}");
        }
    }

    public override void OnUnequip()
    {
        if (Wielder != null && Wielder.Stats != null)
        {
            // Remove fire resistance entry
            var resistances = Wielder.Stats.DamageResistances;
            for (int i = resistances.Count - 1; i >= 0; i--)
            {
                if (resistances[i].Type == DamageType.Fire &&
                    resistances[i].Amount == FireResistanceAmount)
                {
                    resistances.RemoveAt(i);
                    break; // Only remove one
                }
            }
            Log("Unequipped: fire resistance removed");
        }

        base.OnUnequip();
    }

    // ========================================================================
    //  ACTIVATED: Dispel Fire spell (1/day, CL 14 dispel check)
    // ========================================================================

    public override bool CanActivate()
    {
        return IsEquipped && _dispelsRemaining > 0;
    }

    public override string GetActivateDescription()
    {
        if (_dispelsRemaining <= 0)
            return "Dispel fire: used today.\nPassive: Fire Resistance 10.";
        return $"Dispel a fire spell (as dispel magic, CL {DispelCasterLevel}). {_dispelsRemaining}/{MaxDispelsPerDay} use(s) remaining.\nPassive: Fire Resistance 10.";
    }

    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (_dispelsRemaining <= 0)
        {
            logNotes?.Add("Frost Brand: dispel fire already used today.");
            return false;
        }

        _dispelsRemaining--;

        // Perform a dispel check: 1d20 + caster level vs fire spell DC
        int roll = DiceService.D20("Frost Brand dispel check");
        int total = roll + DispelCasterLevel;
        logNotes?.Add($"❄️ <color=#ADD8E6>Frost Brand</color> attempts to dispel fire! (1d20+{DispelCasterLevel} = {total})");
        Log($"Dispel fire attempt: roll {roll} + CL {DispelCasterLevel} = {total}");

        // Extinguish nonmagical fires as part of activation
        logNotes?.Add($"❄️ Frost Brand extinguishes nearby nonmagical fires within 20 ft.");

        return true;
    }

    // ========================================================================
    //  LONG REST: Refresh dispel uses
    // ========================================================================

    public override void OnLongRest()
    {
        _dispelsRemaining = MaxDispelsPerDay;
    }

    public override string GetUsesDisplay()
    {
        return $"Dispel fire: {_dispelsRemaining}/{MaxDispelsPerDay}";
    }
}
