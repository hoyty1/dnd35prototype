// ============================================================================
// GameManager_Spells_H.cs — Spell resolution methods starting with "H".
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
    //  HOLD PERSON — Enhanced Resolution with Duration Scaling
    // ================================================================

    /// <summary>
    /// Enhanced Hold Person resolution with proper duration scaling (1 round/level)
    /// and tracking for the cumulative +2 Will save each round to break free.
    /// </summary>
    private ActiveSpellEffect ApplyHoldPersonBuff(CharacterController caster, CharacterController target, SpellData spell, SpellcastingComponent spellComp)
    {
        if (target == null || target.Stats == null || spell == null)
            return null;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int holdRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel);
        string sourceName = spell.Name;

        // Apply Paralyzed condition with the scaled duration
        if (_conditionService != null)
        {
            _conditionService.ApplyCondition(
                target,
                CombatConditionType.Paralyzed,
                holdRounds,
                source: caster,
                sourceNameOverride: sourceName,
                sourceCategory: "Spell",
                sourceId: spell.SpellId);

            // Also apply Helpless condition (paralyzed creatures are helpless)
            _conditionService.ApplyCondition(
                target,
                CombatConditionType.Helpless,
                holdRounds,
                source: caster,
                sourceNameOverride: sourceName,
                sourceCategory: "Spell",
                sourceId: spell.SpellId);
        }
        else
        {
            string fallbackSource = caster != null && caster.Stats != null ? caster.Stats.CharacterName : sourceName;
            target.ApplyCondition(CombatConditionType.Paralyzed, holdRounds, fallbackSource);
            target.ApplyCondition(CombatConditionType.Helpless, holdRounds, fallbackSource);
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("⛓", $"{target.Stats.CharacterName} is paralyzed by Hold Person for {holdRounds} round(s)! (Will save each round with cumulative +2 to break free)"));
        Debug.Log($"[GameManager] Hold Person applied Paralyzed+Helpless to {target.Stats.CharacterName} for {holdRounds} rounds (CL {casterLevel})");

        return null;
    }

    // ================================================================
    //  HALT UNDEAD — PHB p.239
    //  Up to 3 undead within 30 ft of each other; 1 round/level paralyze.
    //  Nonintelligent undead get NO save. Intelligent undead get Will save.
    //  SR: Yes.
    // ================================================================

    /// <summary>
    /// Resolves Halt Undead spell. Filters the AoE target list to undead only,
    /// caps to 3 closest to the caster, performs SR check, and (for intelligent
    /// undead only) a Will save. On failure, applies Paralyzed + Helpless for
    /// 1 round per caster level.
    /// Called from PerformAoESpellCast when the pending spell matches.
    /// </summary>
    private bool TryResolveHaltUndeadSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        if (!string.Equals(spell.SpellId, SpellNames.HALT_UNDEAD, StringComparison.Ordinal))
            return false;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel);
        int saveDc = SpellUtilities.GetSpellSaveDC(caster, spell);

        // Filter to undead only
        List<CharacterController> undeadCandidates = new List<CharacterController>();
        if (targets != null)
        {
            foreach (CharacterController t in targets)
            {
                if (t == null || t.Stats == null || t.Stats.IsDead) continue;
                if (!t.CanBeCommandedAsUndead()) continue;
                undeadCandidates.Add(t);
            }
        }

        // Cap to 3, choose closest to caster (per PHB targeting rules)
        Vector2Int casterCell = caster.GridPosition;
        undeadCandidates.Sort((a, b) =>
        {
            int da = Mathf.Max(Mathf.Abs(a.GridPosition.x - casterCell.x), Mathf.Abs(a.GridPosition.y - casterCell.y));
            int db = Mathf.Max(Mathf.Abs(b.GridPosition.x - casterCell.x), Mathf.Abs(b.GridPosition.y - casterCell.y));
            return da.CompareTo(db);
        });
        int affectedCount = Mathf.Min(3, undeadCandidates.Count);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"💀 {caster.Stats.CharacterName} casts Halt Undead!");
        sb.AppendLine($"  School: Necromancy | Level: 3 | Range: Medium");
        sb.AppendLine($"  Targets: up to 3 undead (no two more than 30 ft apart)");
        sb.AppendLine($"  Duration: {durationRounds} round(s) | Will DC {saveDc} (intelligent only) | SR: Yes");
        sb.AppendLine();

        if (undeadCandidates.Count == 0)
        {
            sb.AppendLine($"  No undead in area — spell has no effect.");
            sb.Append("═══════════════════════════════════");
            log = sb.ToString();
            return true;
        }

        // 30-ft constraint: ensure no two affected creatures are more than 6 squares apart.
        // Build a chosen list starting from the closest, then add others within 6 squares of any chosen.
        List<CharacterController> chosen = new List<CharacterController>();
        for (int i = 0; i < undeadCandidates.Count && chosen.Count < affectedCount; i++)
        {
            CharacterController cand = undeadCandidates[i];
            if (chosen.Count == 0)
            {
                chosen.Add(cand);
                continue;
            }
            // Check max chebyshev distance to any chosen
            bool withinRange = true;
            for (int j = 0; j < chosen.Count; j++)
            {
                int dist = Mathf.Max(
                    Mathf.Abs(cand.GridPosition.x - chosen[j].GridPosition.x),
                    Mathf.Abs(cand.GridPosition.y - chosen[j].GridPosition.y));
                if (dist > 6) // 30 ft = 6 squares
                {
                    withinRange = false;
                    break;
                }
            }
            if (withinRange)
                chosen.Add(cand);
        }

        sb.AppendLine($"  Affected undead: {chosen.Count} of {undeadCandidates.Count} undead in area");
        sb.AppendLine();

        int targetIndex = 0;
        foreach (CharacterController target in chosen)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;

            targetIndex++;
            sb.AppendLine($"  --- Target {targetIndex}: {target.Stats.CharacterName} ---");

            // Spell Resistance check
            var srResult = SpellSaveResolver.RollSpellResistance(caster, target, casterLevel);
            srResult.AppendToLog(sb);
            if (!srResult.Overcame)
            {
                sb.AppendLine($"  {target.Stats.CharacterName} resists Halt Undead via Spell Resistance!");
                sb.AppendLine();
                continue;
            }

            // Save check (only intelligent undead get a save)
            bool isIntelligent = target.IsIntelligentUndead();
            bool savePassed = false;
            if (isIntelligent)
            {
                var saveResult = SpellSaveResolver.RollSave(target, SaveType.Will, saveDc);
                savePassed = saveResult.Saved;
                saveResult.AppendToLog(sb, "SAVED (negated)", "FAILED");
            }
            else
            {
                sb.AppendLine($"  No save (mindless undead — automatic failure).");
            }

            if (savePassed)
            {
                sb.AppendLine($"  {target.Stats.CharacterName} resists Halt Undead!");
                sb.AppendLine();
                continue;
            }

            // Apply paralysis + helpless conditions
            string sourceName = spell.Name;
            if (_conditionService != null)
            {
                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Paralyzed,
                    durationRounds,
                    source: caster,
                    sourceNameOverride: sourceName,
                    sourceCategory: "Spell",
                    sourceId: spell.SpellId);

                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Helpless,
                    durationRounds,
                    source: caster,
                    sourceNameOverride: sourceName,
                    sourceCategory: "Spell",
                    sourceId: spell.SpellId);
            }
            else
            {
                string fallbackSource = caster.Stats.CharacterName;
                target.ApplyCondition(CombatConditionType.Paralyzed, durationRounds, fallbackSource);
                target.ApplyCondition(CombatConditionType.Helpless, durationRounds, fallbackSource);
            }

            sb.AppendLine($"  ⛓ {target.Stats.CharacterName} is paralyzed by Halt Undead for {durationRounds} round(s)!");
            sb.AppendLine();
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  HASTE — Transmutation Speed Buff (PHB p.239)
    // ================================================================

    /// <summary>
    /// Applies the Haste spell effect to a target.
    /// Per PHB p.239:
    ///   • +1 bonus on attack rolls
    ///   • +1 dodge bonus to AC and Reflex saves
    ///   • +30 ft. movement speed
    ///   • One extra attack at full BAB on full attack action
    ///   • Haste dispels and counters Slow
    /// Duration: 1 round/level
    /// </summary>
    private ActiveSpellEffect ApplyHasteBuff(CharacterController caster, CharacterController target, SpellData spell, SpellcastingComponent spellComp)
    {
        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null || spell == null)
            return null;

        StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
        if (recipientStatusMgr == null)
            recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        recipientStatusMgr.Init(recipient.Stats);

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int durationRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel);

        // If target has Slow, Haste dispels it
        if (recipient.HasActiveSlowEffect)
        {
            recipient.ClearSlowEffect();
            recipient.Stats.SlowAttackPenalty = 0;
            recipient.Stats.SlowACPenalty = 0;
            recipient.Stats.SlowReflexPenalty = 0;
            recipient.Stats.SlowSpeedMultiplier = 1f;

            // Remove Slow from StatusEffectManager
            recipientStatusMgr.RemoveEffectsBySpellId(SpellNames.SLOW);

            string casterName2 = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
            CombatUI?.ShowCombatLog($"<color=#88FF88>⚡ {casterName2}'s Haste dispels Slow on {recipient.Stats.CharacterName}!</color>");
        }

        ActiveSpellEffect effect = recipientStatusMgr.AddEffect(
            spell,
            caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name,
            casterLevel);

        if (effect != null)
        {
            // Apply Haste bonuses to stats
            recipient.Stats.HasteAttackBonus = 1;
            recipient.Stats.HasteACBonus = 1;
            recipient.Stats.HasteReflexBonus = 1;

            // Apply custom effect data for extra attack tracking
            recipient.ApplyHasteEffect(durationRounds, caster);

            SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
            if (recipientSpellComp != null)
                recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

            string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
            bool selfCast = recipient == caster;
            string castLine = selfCast
                ? $"<color=#88FF88>⚡ {casterName} casts Haste on self!</color>"
                : $"<color=#88FF88>⚡ {casterName} casts Haste on {recipient.Stats.CharacterName}!</color>";

            CombatUI?.ShowCombatLog(castLine);
            CombatUI?.ShowCombatLog($"<color=#AAFFAA>   +1 attack, +1 dodge AC, +1 Reflex, +30 ft speed, extra attack on full attack</color>");
            CombatUI?.ShowCombatLog($"<color=#AAFFAA>   Duration: {durationRounds} rounds (CL {casterLevel})</color>");
        }

        UpdateAllStatsUI();
        return effect;
    }

}
