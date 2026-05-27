// ============================================================================
// GameManager_Spells_N.cs — Spell resolution methods starting with "N".
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
    //  NEUTRALIZE POISON  (PHB p.257)
    // ================================================================
    // Touch. Instantaneous cure + 10 min/level immunity.
    // Cures existing poison and grants temporary immunity.

    private bool TryResolveNeutralizePoisonSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.NEUTRALIZE_POISON) return false;
        if (caster == null || caster.Stats == null || target == null || target.Stats == null) return false;
        if (!result.Success) return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = casterLevel * 100; // 10 min/level

        // Cure existing poison
        bool wasPoisoned = target.HasCondition(CombatConditionType.Poisoned);
        if (wasPoisoned)
            target.RemoveCondition(CombatConditionType.Poisoned);

        // Grant immunity
        target.Stats.NeutralizePoisonImmunityActive = true;
        target.Stats.NeutralizePoisonImmunityRoundsRemaining = durationRounds;

        var statusMgr = target.StatusEffectManager;
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null) effect.RemainingRounds = durationRounds;
        }

        string curedMsg = wasPoisoned ? " Poison cured!" : "";
        CombatUI?.ShowCombatLog(CombatLogHelper.Success("🌿✨", $"Neutralize Poison! {casterName} neutralizes poison on {targetName}.{curedMsg} Poison immunity for {durationRounds} rounds."));
        Debug.Log($"[NeutralizePoison] {casterName} -> {targetName}: cured={wasPoisoned}, immunity {durationRounds} rounds");

        result.BuffApplied = true;
        result.BuffDescription = $"Neutralize Poison{(wasPoisoned ? " (cured)" : "")} + immunity ({durationRounds} rounds)";
        return true;
    }

}
