using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Sun Blade (SRD / DMG p.228)
//
// +2 bastard sword that functions as a short sword for size/finesse purposes.
//   - Built-in finesse: can use DEX for attack rolls (no feat needed)
//   - +4 total enhancement vs evil creatures (+2 extra)
//   - Double damage vs undead
//   - Emits sunlight on command (30 ft radius)
//
// Bastard sword base: 1d10, 19-20/x2
// ============================================================================

/// <summary>
/// Sun Blade specific item behavior.
/// Grants built-in finesse, enhanced bonuses vs evil, and double damage vs undead.
/// </summary>
public class SunBladeBehavior : SpecificItemBehavior
{
    private const int BaseBonusVsEvil = 4;       // Total enhancement vs evil (+2 extra over base +2)
    private const int BaseEnhancement = 2;       // Normal +2
    private bool _sunlightActive;

    public override string DisplayName => "Sun Blade";

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _sunlightActive = false;
    }

    // ========================================================================
    //  ATTACK ROLL: Built-in finesse + bonus vs evil
    // ========================================================================

    public override void OnPreAttackRoll(CharacterController target, ref int attackBonus, List<string> logNotes)
    {
        if (Wielder?.Stats == null) return;

        // Built-in finesse: if DEX > STR, add the difference as a bonus
        // This simulates using DEX for attack rolls without requiring Weapon Finesse feat
        int str = Wielder.Stats.STR;
        int dex = Wielder.Stats.DEX;
        int strMod = (str - 10) / 2;
        int dexMod = (dex - 10) / 2;

        if (dexMod > strMod)
        {
            int finesseBonus = dexMod - strMod;
            attackBonus += finesseBonus;
            logNotes?.Add($"Sun Blade finesse: +{finesseBonus} (DEX over STR)");
            Log($"Finesse bonus applied: +{finesseBonus} (DEX {dexMod} vs STR {strMod})");
        }

        // +4 total vs evil creatures (extra +2 on top of base +2)
        if (target?.Stats != null && AlignmentHelper.IsEvil(target.Stats.CharacterAlignment))
        {
            int extraAttack = BaseBonusVsEvil - BaseEnhancement; // +2 extra
            attackBonus += extraAttack;
            logNotes?.Add($"Sun Blade vs evil: +{extraAttack} attack");
            Log($"Evil target bonus: +{extraAttack} attack ({BaseBonusVsEvil} total enhancement)");
        }
    }

    // ========================================================================
    //  DAMAGE ROLL: Bonus vs evil + double damage vs undead
    // ========================================================================

    public override void OnDamageRoll(CharacterController target, ref int damage, bool isCrit, List<string> logNotes)
    {
        if (target?.Stats == null) return;

        // Extra enhancement damage vs evil (+2 extra)
        if (AlignmentHelper.IsEvil(target.Stats.CharacterAlignment))
        {
            int extraDamage = BaseBonusVsEvil - BaseEnhancement;
            damage += extraDamage;
            logNotes?.Add($"Sun Blade vs evil: +{extraDamage} damage");
            Log($"Evil target bonus: +{extraDamage} damage");
        }

        // Double damage vs undead
        if (target.Stats.CreatureType == "Undead")
        {
            int doubleDamage = damage; // Current damage becomes double
            damage += doubleDamage;
            logNotes?.Add($"☀ Sun Blade DOUBLES damage vs undead! (+{doubleDamage})");
            Log($"Undead double damage: {doubleDamage} extra (total {damage})");
        }
    }

    // ========================================================================
    //  CRITICAL HIT: Blind undead on crit
    // ========================================================================

    public override void OnCriticalHit(CharacterController target, int damage, List<string> logNotes)
    {
        if (target?.Stats == null) return;

        // Undead that are critically hit must make Will DC 14 or be blinded
        if (target.Stats.CreatureType == "Undead")
        {
            var save = SavingThrowResolver.ResolveWillSave(target.Stats, 14, "Sun Blade radiance");
            if (!save.Succeeded)
            {
                target.ApplyCondition(CombatConditionType.Blinded, 5, "Sun Blade radiance");
                logNotes?.Add($"☀ <color=#FFD700>Sun Blade</color> radiance blinds {target.Stats.CharacterName}! (Will DC 14, rolled {save.Roll}+{save.Total - save.Roll}={save.Total})");
                Log($"Undead blinded on crit: {target.Stats.CharacterName}");
            }
            else
            {
                logNotes?.Add($"{target.Stats.CharacterName} resists blinding radiance (Will DC 14, rolled {save.Total})");
            }
        }
    }

    // ========================================================================
    //  ACTIVATED: Toggle sunlight emission
    // ========================================================================

    public override bool CanActivate()
    {
        return Wielder != null;
    }

    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        _sunlightActive = !_sunlightActive;

        if (_sunlightActive)
        {
            logNotes?.Add("☀ <color=#FFD700>Sun Blade</color> blazes with sunlight! (30 ft radius)");
            Log("Sunlight activated");
        }
        else
        {
            logNotes?.Add("Sun Blade sunlight extinguished.");
            Log("Sunlight deactivated");
        }

        return true;
    }

    public override string GetActivateDescription()
    {
        if (_sunlightActive)
            return "Extinguish sunlight (currently blazing, 30 ft radius).";
        return "Emit sunlight (30 ft radius, illuminates as true sunlight).";
    }

    public override string GetUsesDisplay()
    {
        if (_sunlightActive)
            return "☀ Sunlight ON";
        return null;
    }

    // ========================================================================
    //  TOOLTIP
    // ========================================================================

    public override string GetTooltipText()
    {
        var lines = new List<string>();
        lines.Add("<b>Sun Blade</b> (+2 bastard sword)");
        lines.Add("Functions as short sword for finesse (built-in)");
        lines.Add($"+{BaseBonusVsEvil} total enhancement vs evil creatures");
        lines.Add("Double damage vs undead");
        lines.Add("Blinds undead on critical hit (Will DC 14)");
        lines.Add("Emits sunlight on command (30 ft radius)");
        if (_sunlightActive)
            lines.Add("<color=#FFD700>☀ Sunlight active</color>");
        return string.Join("\n", lines);
    }
}
