// ============================================================================
// GameManager_Spells_M.cs — Spell resolution methods starting with "M".
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
    //  MASS ENLARGE/REDUCE PERSON — Transmutation Size Change (PHB p.226/269)
    // ================================================================

    /// <summary>
    /// Applies Mass Enlarge Person or Mass Reduce Person.
    /// Targets one humanoid creature per caster level; no two more than 30 ft apart.
    /// Each target gets a Fort save (willing allies auto-fail) and SR check.
    /// Duration: 1 min/level.
    /// </summary>
    private ActiveSpellEffect ApplyMassSizeChangeBuff(CharacterController caster, CharacterController primaryTarget, SpellData spell, SpellcastingComponent spellComp)
    {
        if (caster == null || spell == null)
            return null;

        bool isEnlarge = spell.SpellId == SpellNames.MASS_ENLARGE_PERSON;
        string spellName = isEnlarge ? "Mass Enlarge Person" : "Mass Reduce Person";
        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Caster";
        int casterLevel = caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int maxTargets = casterLevel; // One creature per caster level
        int saveDc = GetSpellSaveDC(caster, spell);

        CombatUI?.ShowCombatLog($"<color=#FFDD44>📏 {casterName} casts {spellName}!</color>");

        // Gather valid humanoid targets near the primary target.
        // "No two of which can be more than 30 ft. apart" — we use the primary target as anchor
        // and gather all valid humanoids within 30 ft (6 squares).
        List<CharacterController> candidates = new List<CharacterController>();

        // Always include the primary target first
        if (primaryTarget != null && primaryTarget.Stats != null && !primaryTarget.Stats.IsDead && IsHumanoid(primaryTarget))
        {
            candidates.Add(primaryTarget);
        }

        // Find additional valid targets within 30 ft of the primary target
        List<CharacterController> allCharacters = GetAllCharacters();
        foreach (var candidate in allCharacters)
        {
            if (candidate == primaryTarget) continue;
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead) continue;
            if (!IsHumanoid(candidate)) continue;

            // Must be an ally of the caster (or the caster themselves)
            if (candidate != caster && !IsAllyTeam(caster, candidate)) continue;

            // Must be within 30 ft (6 squares) of the primary target
            if (primaryTarget != null)
            {
                int distSquares = SquareGridUtils.GetDistance(candidate.GridPosition, primaryTarget.GridPosition);
                if (distSquares > 6) continue; // > 30 ft
            }

            candidates.Add(candidate);
        }

        // Cap at max targets
        if (candidates.Count > maxTargets)
            candidates.RemoveRange(maxTargets, candidates.Count - maxTargets);

        if (candidates.Count == 0)
        {
            CombatUI?.ShowCombatLog($"<color=#FF8888>  No valid humanoid targets found for {spellName}.</color>");
            return null;
        }

        CombatUI?.ShowCombatLog($"<color=#FFDD44>  Targeting {candidates.Count} humanoid creature(s) (max {maxTargets} at CL {casterLevel})</color>");

        ActiveSpellEffect firstEffect = null;
        int affectedCount = 0;

        foreach (var target in candidates)
        {
            bool isAlly = target == caster || IsAllyTeam(caster, target);

            // Spell Resistance check
            if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
            {
                // Harmless SR — allies can voluntarily lower SR
                if (!isAlly)
                {
                    int srCheckRoll = DiceService.D20();
                    int srCheckTotal = srCheckRoll + casterLevel;
                    bool srOvercome = srCheckTotal >= target.Stats.SpellResistance;

                    CombatUI?.ShowCombatLog($"  SR Check ({target.Stats.CharacterName}): d20({srCheckRoll}) + {casterLevel} = {srCheckTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");

                    if (!srOvercome)
                    {
                        CombatUI?.ShowCombatLog($"<color=#AAAAFF>  {target.Stats.CharacterName} resists {spellName} via Spell Resistance!</color>");
                        continue;
                    }
                }
            }

            // Fort save — willing allies auto-fail (accept the spell)
            if (spell.AllowsSavingThrow)
            {
                if (isAlly)
                {
                    // Willing target — auto-accept
                    CombatUI?.ShowCombatLog($"  {target.Stats.CharacterName}: willing target, auto-accepts {spellName}.");
                }
                else
                {
                    // Unwilling target — Fort save to negate
                    int saveRoll = DiceService.D20();
                    int saveTotal = saveRoll + target.Stats.FortitudeSave;
                    bool saved = saveTotal >= saveDc;

                    CombatUI?.ShowCombatLog($"  Fort Save ({target.Stats.CharacterName}): d20({saveRoll}) + {target.Stats.FortitudeSave} = {saveTotal} vs DC {saveDc} → {(saved ? "SAVED" : "FAILED")}");

                    if (saved)
                    {
                        CombatUI?.ShowCombatLog($"<color=#88FF88>  {target.Stats.CharacterName} resists {spellName}!</color>");
                        continue;
                    }
                }
            }

            // Apply the size change via StatusEffectManager (same path as base Enlarge/Reduce Person)
            StatusEffectManager targetStatusMgr = target.GetComponent<StatusEffectManager>();
            if (targetStatusMgr == null)
                targetStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            targetStatusMgr.Init(target.Stats);

            ActiveSpellEffect effect = targetStatusMgr.AddEffect(
                spell,
                casterName,
                casterLevel);

            if (effect != null)
            {
                SpellcastingComponent targetSpellComp = target.GetComponent<SpellcastingComponent>();
                if (targetSpellComp != null)
                    targetSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

                affectedCount++;
                if (firstEffect == null) firstEffect = effect;

                string sizeChangeDesc = isEnlarge
                    ? "+2 STR, -2 DEX, -1 size penalty to AC/attack"
                    : "-2 STR, +2 DEX, +1 size bonus to AC/attack";

                CombatUI?.ShowCombatLog($"<color=#FFDD44>  📏 {target.Stats.CharacterName} is {(isEnlarge ? "enlarged" : "reduced")}! {sizeChangeDesc}</color>");
            }
        }

        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        CombatUI?.ShowCombatLog($"<color=#FFDD44>  {spellName}: {affectedCount} creature(s) affected. Duration: {durationRounds} rounds (CL {casterLevel})</color>");

        UpdateAllStatsUI();
        return firstEffect;
    }

    /// <summary>
    /// Checks if Haste/Slow have active effects and whether they should be cleared.
    /// Called by HasActiveHaste/HasActiveSlow public accessors.
    /// </summary>
    public bool HasActiveHaste(CharacterController character)
    {
        if (character == null)
            return false;
        return character.HasActiveHasteEffect;
    }

    public bool HasActiveSlow(CharacterController character)
    {
        if (character == null)
            return false;
        return character.HasActiveSlowEffect;
    }

    // ================================================================
    //  MAGIC VESTMENT — PHB p.251
    //  Transmutation
    //  +1 enhancement bonus to armor/shield per 4 caster levels (max +5)
    //  Duration: 1 hour/level
    // ================================================================

    /// <summary>
    /// Resolves Magic Vestment: grants +1 armor enhancement bonus per 4 caster levels
    /// (max +5 at CL 20). Applied as a tracked buff via the StatusEffectManager and
    /// stored in MagicVestmentACBonus on the target's stats.
    /// </summary>
    private bool TryResolveMagicVestmentSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.MAGIC_VESTMENT, StringComparison.Ordinal))
            return false;

        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null)
            return true; // Handled but nothing to apply

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int enhBonus = Mathf.Clamp(casterLevel / 4, 1, 5);

        // Track effect via StatusEffectManager for proper duration/dispel
        StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
        if (recipientStatusMgr == null)
            recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        recipientStatusMgr.Init(recipient.Stats);

        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        ActiveSpellEffect effect = recipientStatusMgr.AddEffect(
            spell,
            caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name,
            casterLevel);

        if (effect != null)
        {
            // Apply armor enhancement bonus directly to stats
            recipient.Stats.MagicVestmentACBonus = enhBonus;
            effect.CustomTag = "MagicVestment";

            SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
            if (recipientSpellComp != null)
                recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;
        }

        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
        bool selfCast = recipient == caster;
        string targetName = selfCast ? "self" : recipient.Stats.CharacterName;

        CombatUI?.ShowCombatLog($"<color=#88FF88>🛡 {casterName} casts Magic Vestment on {targetName}!</color>");
        CombatUI?.ShowCombatLog($"<color=#AAFFAA>   +{enhBonus} enhancement bonus to armor (CL {casterLevel})</color>");
        CombatUI?.ShowCombatLog($"<color=#AAFFAA>   Duration: {durationRounds} rounds ({durationRounds / 600} hours)</color>");

        UpdateAllStatsUI();
        return true;
    }

}
