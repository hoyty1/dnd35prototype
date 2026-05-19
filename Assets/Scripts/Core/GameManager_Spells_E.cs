// ============================================================================
// GameManager_Spells_E.cs — Spell resolution methods starting with "E".
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
    //  ENERVATION — PHB p.226
    //  Necromancy. Sor/Wiz 4.
    //  Ranged touch attack. Subject gains 1d4 negative levels.
    //  No save. SR: Yes. Negative levels last CL hours, then fade
    //  (no save to avoid permanent drain — they just go away).
    // ================================================================

    private static bool IsEnervationSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.ENERVATION, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Enervation: ranged touch attack, 1d4 negative levels, no save.
    /// Negative levels persist for CL hours (converted to rounds for combat tracking).
    /// </summary>
    private bool TryResolveEnervationSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (!IsEnervationSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null)
            return true;

        // Ranged touch missed → no effect
        if (result.RequiredAttackRoll && !result.AttackHit)
        {
            CombatUI?.ShowCombatLog($"❌ Enervation ray misses {target.Stats.CharacterName}.");
            return true;
        }

        // Roll 1d4 negative levels
        int negativeLevels = UnityEngine.Random.Range(1, 5); // 1d4

        // Apply negative levels using existing system
        int newTotal = NegativeLevelSystem.ApplyNegativeLevels(target, negativeLevels, "Enervation");

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        // Duration = CL hours. In combat: 1 hour = 600 rounds (10 rounds/min × 60 min)
        int durationRounds = casterLevel * 600;

        // Track the effect for duration/expiry via StatusEffectManager
        if (target.StatusEffectManager != null)
        {
            string cName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Enervation";
            target.StatusEffectManager.AddEffect(spell, cName, casterLevel);
        }

        result.BuffApplied = true;
        result.BuffDescription = $"Debuff: {negativeLevels} negative level(s) for {casterLevel} hour(s).";

        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
        CombatUI?.ShowCombatLog($"<color=#9933CC>💀 {target.Stats.CharacterName} gains {negativeLevels} negative level{(negativeLevels > 1 ? "s" : "")} from Enervation!</color>");
        CombatUI?.ShowCombatLog($"<color=#AA77CC>   Each negative level: -1 attack/saves/skills, -5 HP, -1 effective level</color>");
        CombatUI?.ShowCombatLog($"<color=#AA77CC>   Duration: {casterLevel} hour{(casterLevel > 1 ? "s" : "")} ({durationRounds} rounds)</color>");

        // Check if target dies from negative levels (HD reduced to 0)
        if (NegativeLevelSystem.IsDeadFromNegativeLevels(target))
        {
            CombatUI?.ShowCombatLog($"<color=#FF3333>☠ {target.Stats.CharacterName} is slain by negative levels! (negative levels ≥ HD)</color>");
            result.TargetKilled = true;
        }

        Debug.Log($"[Enervation] {casterName} -> {target.Stats.CharacterName}: {negativeLevels} negative levels applied (total: {newTotal}), duration {casterLevel}h ({durationRounds} rounds)");
        return true;
    }

}
