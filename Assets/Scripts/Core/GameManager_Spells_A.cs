// ============================================================================
// GameManager_Spells_A.cs — Spell resolution methods starting with "A".
//
// Part of the GameManager partial class.
// D&D 3.5e PHB rules.
// ============================================================================
using DND35e.Identifiers;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine;

public partial class GameManager
{
    // ================================================================
    //  ALIGN WEAPON — PHB p.197
    //  Transmutation. Cleric 2. V, S, DF.
    //  Range: Touch. Target: Weapon touched or 50 projectiles.
    //  Duration: 1 min/level. Saving Throw: Will negates (harmless, object).
    //  Spell Resistance: Yes (harmless, object).
    //  Makes weapon good/evil/lawful/chaotic-aligned to bypass DR.
    //  Cannot make a weapon aligned to the opposite of the caster's alignment.
    // ================================================================

    private bool TryResolveAlignWeaponSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.ALIGN_WEAPON)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        if (!result.Success)
            return true;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = casterLevel * 10; // 1 min/level = 10 rounds/level

        // Determine alignment to apply
        // For simplicity, use "good" as default. Caster's alignment determines options:
        // Cannot make weapon opposed to caster's alignment.
        // AI casters: pick "good" for good-aligned, "evil" for evil-aligned, etc.
        string alignment = DetermineAlignWeaponAlignment(caster);

        target.Stats.AlignWeaponActive = true;
        target.Stats.AlignWeaponAlignment = alignment;
        target.Stats.AlignWeaponRoundsRemaining = durationRounds;

        // Track via StatusEffectManager
        var statusMgr = target.StatusEffectManager;
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, caster.Stats.CharacterName, casterLevel);
            if (effect != null)
                effect.RemainingRounds = durationRounds;
        }

        CombatUI?.ShowCombatLog($"<color=#FFCC33>⚔✨ Align Weapon! {target.Stats.CharacterName}'s weapon is now {alignment}-aligned for {durationRounds} rounds ({casterLevel} minutes). Bypasses DR/{alignment}.</color>");
        Debug.Log($"[AlignWeapon] {target.Stats.CharacterName}'s weapon aligned as '{alignment}' for {durationRounds} rounds");

        return true;
    }

    /// <summary>
    /// Determines which alignment to apply to Align Weapon based on caster's alignment.
    /// Per PHB: caster cannot choose an alignment component opposite to their own.
    /// Good casters pick "good", evil pick "evil", lawful pick "lawful", chaotic pick "chaotic".
    /// Neutral casters default to "good".
    /// </summary>
    private string DetermineAlignWeaponAlignment(CharacterController caster)
    {
        if (caster?.Stats != null)
        {
            Alignment a = caster.Stats.CharacterAlignment;
            if (AlignmentHelper.IsGood(a)) return "good";
            if (AlignmentHelper.IsEvil(a)) return "evil";
            if (AlignmentHelper.IsLawful(a)) return "lawful";
            if (AlignmentHelper.IsChaotic(a)) return "chaotic";
        }
        return "good"; // Default for True Neutral or unknown
    }

    /// <summary>
    /// Checks if a character has an aligned weapon that can bypass a given DR alignment type.
    /// </summary>
    public static bool CanBypassAlignmentDR(CharacterController attacker, string drAlignment)
    {
        if (attacker?.Stats == null || !attacker.Stats.AlignWeaponActive)
            return false;

        if (string.IsNullOrWhiteSpace(drAlignment) || string.IsNullOrWhiteSpace(attacker.Stats.AlignWeaponAlignment))
            return false;

        return attacker.Stats.AlignWeaponAlignment.Equals(drAlignment, StringComparison.OrdinalIgnoreCase);
    }

}
