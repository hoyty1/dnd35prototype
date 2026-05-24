using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Demon Armor (SRD / DMG p.219)
//
// +4 full plate armor. Makes the wearer appear demonic.
//   - Grants claw attacks (1d10 damage, treated as +1 weapons)
//   - Contagion on claw hit (Fort DC 14 negates)
//   - Non-evil wearers gain 1 negative level while equipped
//
// Cursed: cannot be removed once donned except by remove curse.
// ============================================================================

/// <summary>
/// Demon Armor specific item behavior.
/// Grants claw attacks with contagion, penalizes non-evil wearers.
/// </summary>
public class DemonArmorBehavior : SpecificItemBehavior
{
    private const int ClawDamage = 10;         // 1d10
    private const int ClawEnhancement = 1;     // +1 weapon equivalent
    private const int ContagionDC = 14;        // Fort save DC
    private const int NonEvilNegativeLevels = 1;

    private bool _negativeLevelApplied;
    private int _clawUsesRemaining = 3;  // 3/day claw attacks

    public override string DisplayName => "Demon Armor";

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _negativeLevelApplied = false;
        _clawUsesRemaining = 3;
    }

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);
        _negativeLevelApplied = false;

        // Non-evil wielders gain 1 negative level
        if (character.Stats != null && !AlignmentHelper.IsEvil(character.Stats.CharacterAlignment))
        {
            NegativeLevelSystem.ApplyNegativeLevels(character, NonEvilNegativeLevels, "Demon Armor (non-evil penalty)");
            _negativeLevelApplied = true;
            Log($"Non-evil wielder {character.Stats.CharacterName} gains {NonEvilNegativeLevels} negative level");

            GameManager.Instance?.CombatUI?.ShowCombatLog(
                $"<color=#8B0000>👿 Demon Armor inflicts a negative level on {character.Stats.CharacterName} (non-evil wearer)!</color>");
        }

        Log($"Equipped by {character.Stats?.CharacterName} — grants claw attacks with contagion");
    }

    public override void OnUnequip()
    {
        // Remove negative level penalty if we applied it
        if (_negativeLevelApplied && Wielder != null)
        {
            Wielder.RemoveNegativeLevels(NonEvilNegativeLevels);
            _negativeLevelApplied = false;
            Log("Non-evil negative level removed on unequip");
        }

        base.OnUnequip();
    }

    // ========================================================================
    //  ACTIVATED: Claw Attack (3/day)
    // ========================================================================

    public override bool CanActivate()
    {
        return _clawUsesRemaining > 0 && Wielder != null;
    }

    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (!CanActivate())
        {
            logNotes?.Add("Demon Armor: No claw uses remaining today.");
            return false;
        }

        if (target == null || target.Stats == null)
        {
            logNotes?.Add("Demon Armor: No valid target for claw attack.");
            return false;
        }

        _clawUsesRemaining--;

        // Roll claw damage: 1d10 + 1 (enhancement)
        int damage = DiceService.D10("Demon Armor claw 1d10") + ClawEnhancement;

        // Apply damage
        target.Stats.CurrentHP -= damage;
        logNotes?.Add($"👿 <color=#8B0000>Demon Armor</color> claw attack hits {target.Stats.CharacterName} for {damage} damage!");
        Log($"Claw attack: {damage} damage to {target.Stats.CharacterName}");

        // Contagion effect: Fort save DC 14
        var save = SavingThrowResolver.ResolveFortitudeSave(target.Stats, ContagionDC, "Demon Armor contagion");
        if (!save.Succeeded)
        {
            // Apply sickened as contagion proxy (disease effect)
            target.ApplyCondition(CombatConditionType.Sickened, 10, "Demon Armor contagion");
            logNotes?.Add($"🦠 {target.Stats.CharacterName} contracts contagion! (Fort DC {ContagionDC}, rolled {save.Roll}+{save.Total - save.Roll}={save.Total}) — Sickened!");
            Log($"Contagion applied to {target.Stats.CharacterName} (failed Fort DC {ContagionDC})");
        }
        else
        {
            logNotes?.Add($"{target.Stats.CharacterName} resists contagion (Fort DC {ContagionDC}, rolled {save.Roll}+{save.Total - save.Roll}={save.Total})");
            Log($"Contagion resisted by {target.Stats.CharacterName}");
        }

        // Check if target died
        if (target.Stats.CurrentHP <= -10)
        {
            target.OnDeath();
        }

        return true;
    }

    public override string GetActivateDescription()
    {
        return $"Claw Attack: Deal 1d10+{ClawEnhancement} damage. On hit, target must make Fort DC {ContagionDC} or contract contagion (sickened). {_clawUsesRemaining}/3 uses remaining today.";
    }

    public override string GetUsesDisplay()
    {
        return $"Claws: {_clawUsesRemaining}/3";
    }

    public override void OnLongRest()
    {
        base.OnLongRest();
        _clawUsesRemaining = 3;
        Log("Claw attack uses refreshed");
    }

    // ========================================================================
    //  TOOLTIP
    // ========================================================================

    public override string GetTooltipText()
    {
        var lines = new List<string>();
        lines.Add("<b>Demon Armor</b> (+4 full plate)");
        lines.Add("Grants claw attacks: 1d10+1 damage, 3/day");
        lines.Add($"Contagion on claw hit (Fort DC {ContagionDC})");
        lines.Add("Non-evil wearers gain 1 negative level");
        if (_negativeLevelApplied)
            lines.Add("<color=#FF0000>⚠ Negative level active (non-evil)</color>");
        return string.Join("\n", lines);
    }
}
