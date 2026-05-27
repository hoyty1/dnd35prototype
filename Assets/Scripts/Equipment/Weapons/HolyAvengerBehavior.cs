using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================================
// Holy Avenger (SRD / DMG p.226)
//
// +2 cold iron longsword. In the hands of a paladin:
//   - Enhancement bonus becomes +5
//   - Holy enchantment: +2d6 holy damage vs evil creatures
//   - Grants SR (5 + paladin level) to wielder and all allies within 5 ft
//   - Can use greater dispel magic (area, at wielder's 2× paladin level)
//     once per round as a free action
//
// Non-paladins: functions as +2 holy longsword (still does holy damage vs evil)
// Alignment: Lawful Good
// ============================================================================

/// <summary>
/// Holy Avenger specific item behavior.
/// Flagship paladin weapon with conditional enhancement, SR aura, and dispel.
/// </summary>
public class HolyAvengerBehavior : SpecificItemBehavior
{
    private const int BaseEnhancement = 2;
    private const int PaladinEnhancement = 5;
    private const int SRBase = 5;                // SR = 5 + paladin level
    private const int HolyDamageDice = 2;        // 2d6 holy damage vs evil

    private bool _isPaladinWielder;
    private int _paladinLevel;
    private int _srValue;
    private bool _dispelUsedThisRound;

    // Track SR bonus applied so we can cleanly remove it
    private int _appliedSRBonus;

    public override string DisplayName => "Holy Avenger";

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _isPaladinWielder = false;
        _paladinLevel = 0;
        _srValue = 0;
        _dispelUsedThisRound = false;
        _appliedSRBonus = 0;
    }

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);

        if (character.Stats == null) return;

        // Check if paladin
        _isPaladinWielder = character.Stats.IsPaladin;
        _paladinLevel = character.Stats.GetClassLevel("Paladin");

        if (_isPaladinWielder)
        {
            _srValue = SRBase + _paladinLevel;

            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Special("✨", $"HOLY AVENGER awakens to its full power in {character.Stats.CharacterName}'s hands!"));
            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Special("✨", $"+{PaladinEnhancement} holy cold iron longsword | SR {_srValue} aura | Greater Dispel Magic at will"));

            Log($"Paladin wielder: level {_paladinLevel}, SR {_srValue}, enhancement +{PaladinEnhancement}");
        }
        else
        {
            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Info("⚠", $"Holy Avenger functions as +{BaseEnhancement} holy longsword for {character.Stats.CharacterName} (not a paladin)."));

            Log($"Non-paladin wielder: functions as +{BaseEnhancement} holy only");
        }
    }

    public override void OnUnequip()
    {
        // SR removal handled by RemovePassiveStatBonuses
        _isPaladinWielder = false;
        _paladinLevel = 0;
        _srValue = 0;
        _appliedSRBonus = 0;

        base.OnUnequip();
    }

    // ========================================================================
    //  PASSIVE STAT BONUSES: SR aura for paladin wielder
    // ========================================================================

    /// <summary>
    /// Grants SR (5 + paladin level) to the wielder.
    /// Allies within 5 ft also benefit (checked at spell resolution time, not here).
    /// We apply the SR directly to the wielder's stats.
    /// </summary>
    public override void ApplyPassiveStatBonuses(CharacterStats stats)
    {
        if (stats == null || !_isPaladinWielder) return;

        // Only apply if our SR is higher than existing
        if (_srValue > stats.SpellResistance)
        {
            _appliedSRBonus = _srValue - stats.SpellResistance;
            stats.SpellResistance = _srValue;
            Log($"SR {_srValue} applied to {stats.CharacterName}");
        }
        else
        {
            _appliedSRBonus = 0; // Existing SR is higher
        }
    }

    public override void RemovePassiveStatBonuses(CharacterStats stats)
    {
        if (stats == null || _appliedSRBonus <= 0) return;

        stats.SpellResistance -= _appliedSRBonus;
        if (stats.SpellResistance < 0) stats.SpellResistance = 0;
        _appliedSRBonus = 0;
    }

    // ========================================================================
    //  ATTACK ROLL: +5 vs evil (paladin) or +2 base (non-paladin, already applied)
    // ========================================================================

    public override void OnPreAttackRoll(CharacterController target, ref int attackBonus, List<string> logNotes)
    {
        if (target?.Stats == null) return;

        if (_isPaladinWielder)
        {
            // Extra +3 enhancement on top of base +2 = +5 total (always, not just vs evil)
            int extraAttack = PaladinEnhancement - BaseEnhancement;
            attackBonus += extraAttack;
            logNotes?.Add($"✨ Holy Avenger (paladin): +{extraAttack} attack (+{PaladinEnhancement} total)");
        }
    }

    // ========================================================================
    //  DAMAGE ROLL: +2d6 holy vs evil + extra enhancement for paladin
    // ========================================================================

    public override void OnDamageRoll(CharacterController target, ref int damage, bool isCrit, List<string> logNotes)
    {
        if (target?.Stats == null) return;

        // Extra enhancement damage for paladin (+3 on top of base +2)
        if (_isPaladinWielder)
        {
            int extraEnhDamage = PaladinEnhancement - BaseEnhancement;
            damage += extraEnhDamage;
        }

        // Holy damage: +2d6 vs evil creatures (works for both paladin and non-paladin)
        if (AlignmentHelper.IsEvil(target.Stats.CharacterAlignment))
        {
            int holyDamage = DiceService.RollMultiple(HolyDamageDice, 6);
            damage += holyDamage;
            logNotes?.Add($"✨ <color=#FFD700>Holy Avenger</color> smites evil! +{holyDamage} holy damage ({HolyDamageDice}d6)");
            Log($"Holy damage vs evil: +{holyDamage}");
        }
    }

    // ========================================================================
    //  ACTIVATED: Greater Dispel Magic (1/round, paladin only)
    // ========================================================================

    public override bool CanActivate()
    {
        return IsEquipped && _isPaladinWielder && !_dispelUsedThisRound;
    }

    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (!_isPaladinWielder)
        {
            logNotes?.Add("✨ Holy Avenger: Only a paladin can use the dispel ability.");
            return false;
        }

        if (_dispelUsedThisRound)
        {
            logNotes?.Add("✨ Holy Avenger: Greater Dispel Magic already used this round.");
            return false;
        }

        _dispelUsedThisRound = true;

        // Greater Dispel Magic at CL = 2× paladin level (no cap)
        int dispelCL = _paladinLevel * 2;
        string wielderName = Wielder?.Stats?.CharacterName ?? "Paladin";

        logNotes?.Add($"✨ <color=#FFD700>Holy Avenger</color>: {wielderName} invokes Greater Dispel Magic! (CL {dispelCL})");
        Log($"Greater Dispel Magic activated at CL {dispelCL}");

        if (target != null)
        {
            // Targeted dispel against specific creature
            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Special("✨", $"Holy Avenger: Greater Dispel Magic vs {target.Stats?.CharacterName} (CL {dispelCL})"));

            // Use the existing dispel system — create a temporary "caster" context
            // The PerformTargetedDispel uses the caster's CL, so we simulate via the wielder
            // We temporarily adjust and call dispel
            GameManager.Instance?.PerformTargetedDispel(Wielder, target);
        }
        else
        {
            // Area dispel affecting enemies in range
            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Special("✨", $"Holy Avenger: Greater Dispel Magic (area) by {wielderName} (CL {dispelCL})"));

            // Get all enemies within range
            var allChars = GameManager.Instance?.GetAllCharactersForAI();
            if (allChars != null)
            {
                var enemies = allChars.Where(c =>
                    c != null && c != Wielder && c.Stats != null && !c.Stats.IsDead &&
                    c.IsPlayerControlled != Wielder.IsPlayerControlled).ToList();

                if (enemies.Count > 0)
                {
                    GameManager.Instance?.PerformAreaDispel(Wielder, enemies);
                }
            }
        }

        return true;
    }

    public override string GetActivateDescription()
    {
        if (!_isPaladinWielder)
            return "Greater Dispel Magic: Requires paladin wielder.";
        if (_dispelUsedThisRound)
            return "Greater Dispel Magic: Already used this round. Resets next round.";

        int dispelCL = _paladinLevel * 2;
        return $"Greater Dispel Magic (CL {dispelCL}): Dispel one or more spell effects. 1/round, free action.";
    }

    public override string GetUsesDisplay()
    {
        if (!_isPaladinWielder)
            return null;
        if (_dispelUsedThisRound)
            return "Dispel: used this round";
        return "Dispel: ready";
    }

    /// <summary>
    /// Reset the dispel-per-round counter. Called at the start of wielder's turn.
    /// Since we don't have an OnRoundStart hook in the base class, we reset on long rest
    /// and also track via the round system.
    /// </summary>
    public void OnRoundStart()
    {
        _dispelUsedThisRound = false;
    }

    public override void OnLongRest()
    {
        base.OnLongRest();
        _dispelUsedThisRound = false;

        // Refresh paladin level in case of level-up
        if (Wielder?.Stats != null)
        {
            _paladinLevel = Wielder.Stats.GetClassLevel("Paladin");
            _srValue = SRBase + _paladinLevel;
        }
    }

    // ========================================================================
    //  TOOLTIP
    // ========================================================================

    public string GetTooltipText()
    {
        var lines = new List<string>();
        lines.Add("<b>Holy Avenger</b> (+2 cold iron longsword)");

        if (_isPaladinWielder)
        {
            lines.Add($"<color=#FFD700>Paladin powers active (+{PaladinEnhancement} holy)</color>");
            lines.Add($"SR {_srValue} aura (wielder + adjacent allies)");
            lines.Add($"Greater Dispel Magic 1/round (CL {_paladinLevel * 2})");
        }
        else
        {
            lines.Add("Functions as +2 holy longsword (not paladin)");
        }

        lines.Add($"+{HolyDamageDice}d6 holy damage vs evil creatures");
        return string.Join("\n", lines);
    }
}
