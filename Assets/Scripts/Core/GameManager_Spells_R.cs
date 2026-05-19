// ============================================================================
//  GameManager_Spells_R.cs  —  Spell resolution: 'R' spells
//  (partial class GameManager)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DND35e.Identifiers;
using Random = UnityEngine.Random;

public partial class GameManager
{

    // ================================================================
    //  REMOVE DISEASE  (PHB p.271)
    // ================================================================
    // Cures all diseases on the subject. Requires caster level check
    // (1d20 + CL) vs each disease's DC. Instantaneous.
    // Since the disease subsystem uses CombatConditionType, we remove
    // any Diseased condition on the target.

    private bool TryResolveRemoveDiseaseSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.REMOVE_DISEASE)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        if (!result.Success)
            return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());

        // Diseases are tracked via target.ActiveDiseases (populated by Contagion, etc.)
        if (target.ActiveDiseases == null || target.ActiveDiseases.Count == 0)
        {
            CombatUI?.ShowCombatLog($"<color=#AAAAAA>✦ {casterName} casts Remove Disease on {targetName}, but no diseases are found.</color>");
            result.BuffApplied = true;
            result.BuffDescription = "No diseases to remove.";
            return true;
        }

        // Remove each disease with a caster level check (1d20 + CL vs disease Fort DC)
        int removedCount = 0;
        StringBuilder removedList = new StringBuilder();

        // Iterate backward so removals don't shift indices
        for (int i = target.ActiveDiseases.Count - 1; i >= 0; i--)
        {
            var disease = target.ActiveDiseases[i];
            int diseaseDC = disease.DiseaseData != null ? disease.DiseaseData.FortitudeDC : 14;
            int check = Random.Range(1, 21) + casterLevel;

            string diseaseName = disease.DiseaseData != null ? disease.DiseaseData.Name : "Unknown Disease";

            if (check >= diseaseDC)
            {
                target.ActiveDiseases.RemoveAt(i);
                removedCount++;
                if (removedList.Length > 0) removedList.Append(", ");
                removedList.Append(diseaseName);
                Debug.Log($"[RemoveDisease] Removed {diseaseName} from {targetName} (check {check} vs DC {diseaseDC})");
            }
            else
            {
                CombatUI?.ShowCombatLog($"<color=#FF8888>  Failed to remove {diseaseName} (check {check} vs DC {diseaseDC})</color>");
                Debug.Log($"[RemoveDisease] Failed to remove {diseaseName} from {targetName} (check {check} vs DC {diseaseDC})");
            }
        }

        if (removedCount > 0)
        {
            CombatUI?.ShowCombatLog($"<color=#88FF88>🌿 Remove Disease! {casterName} cures {targetName} of {removedCount} disease(s): {removedList}</color>");
        }
        else
        {
            CombatUI?.ShowCombatLog($"<color=#FF8888>🌿 Remove Disease: {casterName} attempts to cure {targetName} but all caster level checks fail!</color>");
        }

        result.BuffApplied = true;
        result.BuffDescription = removedCount > 0
            ? $"Removed {removedCount} disease(s)"
            : "All disease removal checks failed";

        return true;
    }

    // ================================================================
    //  REMOVE BLINDNESS / DEAFNESS  (PHB p.270)
    // ================================================================
    // Touch. Instantaneous. Removes Blinded or Deafened condition.
    // No saving throw. SR: Yes (harmless).

    private bool TryResolveRemoveBlindnessDeafnessSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.REMOVE_BLINDNESS_DEAFNESS)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        if (!result.Success)
            return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";

        bool wasBlinded = target.HasCondition(CombatConditionType.Blinded);
        bool wasDeafened = target.HasCondition(CombatConditionType.Deafened);

        if (!wasBlinded && !wasDeafened)
        {
            CombatUI?.ShowCombatLog($"<color=#AAAAAA>✦ {casterName} casts Remove Blindness/Deafness on {targetName}, but they are neither blind nor deaf.</color>");
            result.BuffApplied = true;
            result.BuffDescription = "No blindness or deafness to remove.";
            return true;
        }

        StringBuilder removedList = new StringBuilder();

        if (wasBlinded)
        {
            target.RemoveCondition(CombatConditionType.Blinded);
            removedList.Append("Blindness");
        }

        if (wasDeafened)
        {
            target.RemoveCondition(CombatConditionType.Deafened);
            if (removedList.Length > 0) removedList.Append(" and ");
            removedList.Append("Deafness");
        }

        CombatUI?.ShowCombatLog($"<color=#88FF88>👁✨ Remove Blindness/Deafness! {casterName} cures {targetName}'s {removedList}!</color>");
        Debug.Log($"[RemoveBlindnessDeafness] {casterName} -> {targetName}: removed {removedList}");

        result.BuffApplied = true;
        result.BuffDescription = $"Removed {removedList}";

        return true;
    }

    // ================================================================
    //  REPEL VERMIN  (PHB p.271)
    // ================================================================
    // Personal emanation. 10 min/level. 10 ft/level radius.
    // Keeps vermin out. Sets flag for AI/movement checks.

    private bool TryResolveRepelVerminSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.REPEL_VERMIN) return false;
        if (caster == null || caster.Stats == null) return false;
        if (!result.Success) return true;

        // Personal spell
        target = caster;
        string casterName = caster.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = casterLevel * 100; // 10 min/level
        int radiusFeet = casterLevel * 10; // 10 ft/level

        target.Stats.RepelVerminActive = true;
        target.Stats.RepelVerminRoundsRemaining = durationRounds;

        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null) effect.RemainingRounds = durationRounds;
        }

        CombatUI?.ShowCombatLog($"<color=#88CC88>🐛🚫 Repel Vermin! {casterName} creates a {radiusFeet}-ft anti-vermin emanation for {durationRounds} rounds.</color>");
        Debug.Log($"[RepelVermin] {casterName}: radius {radiusFeet} ft, duration {durationRounds} rounds");

        result.BuffApplied = true;
        result.BuffDescription = $"Repel Vermin ({radiusFeet} ft, {durationRounds} rounds)";
        return true;
    }

    // ================================================================
    //  RAGE — Spell-Based Rage Buff
    // ================================================================

    /// <summary>
    /// Applies the Rage spell effect to a target.
    /// Per PHB p.268: +2 morale bonus to Str and Con, +1 morale bonus on Will saves, -2 AC.
    /// Uses the existing stat buff system (direct stat modification) for consistency
    /// with Bull's Strength, Bear's Endurance, etc.
    /// Called from ApplySpellBuff when the spell matches.
    /// </summary>
    private ActiveSpellEffect ApplyRageSpellBuff(CharacterController caster, CharacterController target, SpellData spell, SpellcastingComponent spellComp)
    {
        if (target == null || target.Stats == null || spell == null)
            return null;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;

        // Rage duration: Concentration + 1 round/level (max 10 rounds after concentration ends)
        // For simplicity in combat, we use caster level rounds (max 10)
        int rageRounds = Mathf.Clamp(casterLevel, 1, 10);

        // Apply stat bonuses using the same pattern as Bull's Strength / Bear's Endurance
        // +2 morale bonus to Str
        ApplyStatBuff(target, "STR", 2);
        // +2 morale bonus to Con (ApplyStatBuff handles HP gain)
        ApplyStatBuff(target, "CON", 2);

        // +1 morale bonus on Will saves (uses existing MoraleSaveBonus field)
        target.Stats.MoraleSaveBonus += 1;

        // -2 penalty to AC (separate from barbarian rage AC penalty)
        target.Stats.SpellRageACPenalty = -2;

        // Create the tracked effect
        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(target.Stats);

        var effect = new ActiveSpellEffect
        {
            Spell = spell,
            CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown",
            CasterLevel = casterLevel,
            RemainingRounds = rageRounds,
            DurationType = DurationType.Rounds,
            AffectedCharacterName = target.Stats.CharacterName,
            BonusTypeLegacy = "morale",
            BonusTypeEnum = BonusType.Morale,
            IsApplied = true
        };

        statusMgr.ActiveEffects.Add(effect);

        CombatUI?.ShowCombatLog($"<color=#FF6633>🔥 {target.Stats.CharacterName} is filled with magical rage! (+2 Str, +2 Con, +1 Will, -2 AC) for {rageRounds} round(s)!</color>");
        Debug.Log($"[GameManager] Rage spell applied to {target.Stats.CharacterName} for {rageRounds} rounds");

        return effect;
    }

    // ================================================================
    //  RAY OF EXHAUSTION — PHB p.269
    //  Ranged touch attack. On hit: target Exhausted for 1 min/level
    //  (-6 STR, -6 DEX, half speed, no run/charge).
    //  Successful Fort save → Fatigued instead. SR: Yes.
    // ================================================================

    private static bool IsRayOfExhaustionSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.RAY_OF_EXHAUSTION, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Ray of Exhaustion. The ray must hit (ranged touch attack).
    /// On hit: applies Exhausted for 1 min/level. A successful Fort save
    /// reduces the effect to Fatigued instead.
    /// Called from the touch/ray spell pipeline in PC and NPC casts.
    /// </summary>
    private bool TryResolveRayOfExhaustionSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (!IsRayOfExhaustionSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null)
            return true;

        // Ranged touch missed → no effect
        if (result.RequiredAttackRoll && !result.AttackHit)
        {
            CombatUI?.ShowCombatLog($"❌ Ray of Exhaustion misses {target.Stats.CharacterName}.");
            return true;
        }

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        // PHB: Fortitude partial. Failed save = Exhausted. Successful save = Fatigued.
        bool savePassed = result.RequiredSave && result.SaveSucceeded;
        CombatConditionType conditionToApply = savePassed
            ? CombatConditionType.Fatigued
            : CombatConditionType.Exhausted;
        string conditionName = savePassed ? "Fatigued" : "Exhausted";
        string sourceName = spell.Name;

        if (_conditionService != null)
        {
            _conditionService.ApplyCondition(
                target,
                conditionToApply,
                durationRounds,
                source: caster,
                sourceNameOverride: sourceName,
                sourceCategory: "Spell",
                sourceId: spell.SpellId);
        }
        else
        {
            string fallbackSource = caster != null && caster.Stats != null ? caster.Stats.CharacterName : sourceName;
            target.ApplyCondition(conditionToApply, durationRounds, fallbackSource);
        }

        result.BuffApplied = true;
        result.BuffDescription = savePassed
            ? $"Debuff: Fatigued for {durationRounds} round(s) (Fort save reduced)."
            : $"Debuff: Exhausted (-6 STR/DEX, half speed) for {durationRounds} round(s).";

        if (savePassed)
        {
            CombatUI?.ShowCombatLog($"<color=#9966FF>🩸 {target.Stats.CharacterName} resists the worst of the ray with a Fort save — only Fatigued for {durationRounds} round(s).</color>");
        }
        else
        {
            CombatUI?.ShowCombatLog($"<color=#9933CC>🩸 {target.Stats.CharacterName} is Exhausted by Ray of Exhaustion! (-6 STR, -6 DEX, half speed) for {durationRounds} round(s).</color>");
        }

        Debug.Log($"[GameManager] Ray of Exhaustion applied {conditionName} to {target.Stats.CharacterName} for {durationRounds} rounds (CL {casterLevel}, savePassed={savePassed})");
        return true;
    }

    // ================================================================
    //  RAINBOW PATTERN — PHB p.268
    //  Illusion (Pattern) [Mind-Affecting]. Brd 4, Sor/Wiz 4.
    //  Will negates. SR: Yes.
    //  Fascinates creatures within 20-ft radius (up to 24 HD total).
    //  Duration: Concentration + 1 round/level (D).
    //  Fascinated creatures stand still, -4 to reaction skill checks.
    //  New Will save each round on creature's turn to break free.
    // ================================================================

    private static bool IsRainbowPatternSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.RAINBOW_PATTERN, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Rainbow Pattern as an AoE fascination spell.
    /// Fascinates creatures up to 24 HD total. Will negates. SR: Yes.
    /// Mind-affecting — undead/constructs/mindless immune.
    /// </summary>
    private bool TryResolveRainbowPatternAoE(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        if (!IsRainbowPatternSpell(spell))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int saveDc = GetSpellSaveDC(caster, spell);
        int maxHDTotal = 24;
        int hdAffected = 0;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        int durationRounds = Mathf.Max(1, casterLevel); // 1 round/level after concentration ends

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"✨ {casterName} casts Rainbow Pattern! (20-ft radius)");
        sb.AppendLine($"  [Level {spell.SpellLevel}] {spell.School} [Mind-Affecting]");
        sb.AppendLine($"  Will DC {saveDc} negates | Up to {maxHDTotal} HD total");
        sb.AppendLine($"  Targets: {(targets != null ? targets.Count : 0)} creature(s) in area");
        sb.AppendLine();

        if (targets == null || targets.Count == 0)
        {
            sb.AppendLine("  No valid targets in area!");
            log = sb.ToString();
            return true;
        }

        // Sort targets by HD ascending (lower HD are affected first per PHB)
        var sortedTargets = new List<CharacterController>(targets);
        sortedTargets.Sort((a, b) =>
        {
            int aHd = a != null && a.Stats != null ? Mathf.Max(1, GetTargetHitDice(a)) : 0;
            int bHd = b != null && b.Stats != null ? Mathf.Max(1, GetTargetHitDice(b)) : 0;
            return aHd.CompareTo(bHd);
        });

        int targetIndex = 0;
        foreach (CharacterController target in sortedTargets)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;

            targetIndex++;
            int targetHd = Mathf.Max(1, GetTargetHitDice(target));
            string targetName = target.Stats.CharacterName ?? "Unknown";

            sb.AppendLine($"  --- Target {targetIndex}: {targetName} ({targetHd} HD) ---");

            // Check HD cap
            if (hdAffected + targetHd > maxHDTotal)
            {
                sb.AppendLine($"  {targetName}: Exceeds 24 HD cap ({hdAffected}/{maxHDTotal} HD used). Skipped.");
                sb.AppendLine();
                continue;
            }

            // Mind-affecting immunity check
            if (target.Stats.IsImmuneToMindAffecting())
            {
                sb.AppendLine($"  🛡 {targetName} is immune to mind-affecting effects!");
                sb.AppendLine();
                continue;
            }

            // SR check
            if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
            {
                bool srOvercome = SpellResolutionService.TryOvercomeSpellResistance(
                    casterLevel, target.Stats.SpellResistance, "Rainbow Pattern SR", out int srRoll, out int srTotal);

                sb.AppendLine($"  SR Check: d20({srRoll}) + {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");

                if (!srOvercome)
                {
                    sb.AppendLine($"  {targetName} resists Rainbow Pattern via Spell Resistance!");
                    sb.AppendLine();
                    continue;
                }
            }

            // Will save negates
            SavingThrowResolver.SaveResult willSave = SavingThrowResolver.ResolveWillSave(target.Stats, saveDc, "Rainbow Pattern");
            string saveStr = $"d20({willSave.Roll}) + {willSave.Modifier} = {willSave.Total} vs DC {saveDc}";

            if (willSave.Succeeded)
            {
                sb.AppendLine($"  Will save: {saveStr} → SUCCESS! Not fascinated.");
                sb.AppendLine();
                continue;
            }

            // Failed save — target is fascinated!
            hdAffected += targetHd;
            target.ApplyCondition(CombatConditionType.Fascinated, durationRounds, "Rainbow Pattern");

            // Track via StatusEffectManager
            if (target.StatusEffectManager != null)
            {
                target.StatusEffectManager.AddEffect(spell, casterName, casterLevel);
            }

            sb.AppendLine($"  Will save: {saveStr} → FAILED!");
            sb.AppendLine($"  🌈 {targetName} is fascinated by the rainbow pattern! ({durationRounds} rounds)");
            sb.AppendLine($"  (HD used: {hdAffected}/{maxHDTotal})");
            sb.AppendLine();
        }

        if (hdAffected > 0)
        {
            sb.AppendLine($"  Total HD fascinated: {hdAffected}/{maxHDTotal}");
        }
        else
        {
            sb.AppendLine("  No creatures were fascinated.");
        }

        log = sb.ToString();
        Debug.Log($"[RainbowPattern] {casterName}: {hdAffected} HD fascinated out of {maxHDTotal} max, {durationRounds} rounds duration");
        return true;
    }

    // ================================================================
    //  REMOVE CURSE — PHB p.270
    //  Abjuration. Cleric 3, Paladin 3, Sor/Wiz 4.
    //  V, S (no material component).
    //  Range: Touch. Duration: Instantaneous.
    //  Will negates (harmless). SR: Yes (harmless).
    //  Removes all curses on a creature or object.
    //  Counters and dispels Bestow Curse.
    // ================================================================

    private static bool IsRemoveCurseSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.REMOVE_CURSE, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Remove Curse: touch spell, removes all curse effects from target.
    /// Reverses Bestow Curse ability penalties, general penalties, and action loss.
    /// Uses CurseTracker for centralized curse management.
    /// </summary>
    private bool TryResolveRemoveCurseSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (!IsRemoveCurseSpell(spell) || target == null || target.Stats == null)
            return false;

        if (caster == null || caster.Stats == null)
            return false;

        if (result == null)
            return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";

        // Check if target has any curses to remove
        bool hasCurseTracker = CurseTracker.IsCursed(target);
        bool hasBestowCurseConditionGP = target.HasCondition(CombatConditionType.BestowCurseGeneralPenalty);
        bool hasBestowCurseConditionAL = target.HasCondition(CombatConditionType.BestowCurseActionLoss);
        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        bool hasBestowCurseStatusEffect = statusMgr != null && statusMgr.HasEffect(SpellNames.BESTOW_CURSE);

        bool hasAnyCurse = hasCurseTracker || hasBestowCurseConditionGP || hasBestowCurseConditionAL || hasBestowCurseStatusEffect;

        if (!hasAnyCurse)
        {
            CombatUI?.ShowCombatLog($"<color=#AAAAAA>✦ {casterName} casts Remove Curse on {targetName}, but no curses are found.</color>");
            result.BuffApplied = true;
            result.BuffDescription = "No curses to remove.";
            return true;
        }

        int cursesRemoved = 0;
        var removedDescriptions = new System.Collections.Generic.List<string>();

        // 1. Remove all curses tracked by CurseTracker
        if (hasCurseTracker)
        {
            System.Collections.Generic.List<CurseEffectData> removedCurses;
            int trackerRemoved = CurseTracker.RemoveAllCurses(target, out removedCurses);

            foreach (var curse in removedCurses)
            {
                // Reverse ability damage from ability penalty curses
                if (curse.Type == CurseType.BestowCurseAbilityPenalty && !string.IsNullOrEmpty(curse.AffectedAbility))
                {
                    AbilityType ability;
                    if (System.Enum.TryParse(curse.AffectedAbility, out ability))
                    {
                        int healed = target.HealAbilityDamage(ability, curse.PenaltyAmount, "Remove Curse");
                        if (healed > 0)
                        {
                            removedDescriptions.Add($"+{healed} {ability} restored");
                        }
                    }
                }
                else
                {
                    removedDescriptions.Add(curse.Description ?? curse.Type.ToString());
                }

                cursesRemoved++;
            }
        }

        // 2. Remove Bestow Curse conditions
        if (hasBestowCurseConditionGP)
        {
            target.RemoveCondition(CombatConditionType.BestowCurseGeneralPenalty);
            if (!removedDescriptions.Exists(d => d.Contains("General Penalty")))
                removedDescriptions.Add("Bestow Curse (-4 penalty) removed");
            cursesRemoved++;
        }

        if (hasBestowCurseConditionAL)
        {
            target.RemoveCondition(CombatConditionType.BestowCurseActionLoss);
            if (!removedDescriptions.Exists(d => d.Contains("Action Loss")))
                removedDescriptions.Add("Bestow Curse (action loss) removed");
            cursesRemoved++;
        }

        // 3. Remove Bestow Curse from StatusEffectManager
        if (hasBestowCurseStatusEffect && statusMgr != null)
        {
            statusMgr.RemoveEffectsBySpellId(SpellNames.BESTOW_CURSE);
        }

        // Combat log
        string removedSummary = string.Join(", ", removedDescriptions);
        CombatUI?.ShowCombatLog($"<color=#FFD700>✦ {casterName} casts Remove Curse on {targetName}!</color>");
        CombatUI?.ShowCombatLog($"<color=#FFD700>   {(cursesRemoved > 0 ? $"{cursesRemoved} curse(s) removed: {removedSummary}" : "Curses lifted!")}.</color>");

        result.BuffApplied = true;
        result.BuffDescription = $"Remove Curse: {cursesRemoved} curse(s) removed from {targetName}.";

        Debug.Log($"[RemoveCurse] {casterName} -> {targetName}: {cursesRemoved} curses removed. {removedSummary}");

        UpdateAllStatsUI();
        return true;
    }

}
