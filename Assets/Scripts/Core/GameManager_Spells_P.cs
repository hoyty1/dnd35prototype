// ============================================================================
// GameManager_Spells_P.cs — Spell resolution methods starting with "P".
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
    //  PRAYER  (PHB p.264)
    // ================================================================
    // 40-ft burst centered on caster. 1 round/level.
    // Allies: +1 luck bonus to attack rolls, weapon damage, saves, skill checks.
    // Enemies: –1 luck penalty to attack rolls, weapon damage, saves, skill checks.
    // Note: Prayer is cast on "self" but affects all in area. The resolution
    // applies buffs to allies and debuffs to enemies, tracked via ActiveSpellEffect.

    private bool TryResolvePrayerSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.PRAYER)
            return false;

        if (caster == null || caster.Stats == null)
            return false;

        if (!result.Success)
            return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = casterLevel; // 1 round/level
        int radiusSquares = 8; // 40 ft = 8 squares

        List<CharacterController> allChars = GetAllCharacters();
        int allyCount = 0;
        int enemyCount = 0;

        foreach (var ch in allChars)
        {
            if (ch == null || ch.Stats == null || ch.Stats.IsDead) continue;

            // Check distance from caster
            int dist = SquareGridUtils.GetDistance(caster.GridPosition, ch.GridPosition);
            if (dist > radiusSquares) continue;

            bool isAlly = ch == caster || IsAllyTeam(caster, ch);

            if (isAlly)
            {
                // +1 luck bonus to attacks, damage, saves
                ch.Stats.PrayerActive = true;
                ch.Stats.PrayerRoundsRemaining = durationRounds;

                var statusMgr = ch.GetComponent<StatusEffectManager>();
                if (statusMgr != null)
                {
                    var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
                    if (effect != null)
                    {
                        effect.RemainingRounds = durationRounds;
                        effect.AppliedAttackBonus = 1;
                        effect.AppliedDamageBonus = 1;
                        effect.AppliedSaveBonus = 1;
                    }
                }

                // Apply luck bonuses (piggyback on morale fields — stacks with morale
                // in the real game, but this is the closest existing stat path).
                ch.Stats.MoraleAttackBonus += 1;
                ch.Stats.MoraleDamageBonus += 1;
                ch.Stats.MoraleSaveBonus += 1;

                allyCount++;
            }
            else
            {
                // –1 luck penalty to attacks, damage, saves
                // Apply via debuff ActiveSpellEffect
                var statusMgr = ch.GetComponent<StatusEffectManager>();
                if (statusMgr != null)
                {
                    var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
                    if (effect != null)
                    {
                        effect.RemainingRounds = durationRounds;
                        effect.AppliedAttackBonus = -1;
                        effect.AppliedDamageBonus = -1;
                        effect.AppliedSaveBonus = -1;
                    }
                }

                // Apply luck penalties (same morale fields — reversed on expiration)
                ch.Stats.MoraleAttackBonus -= 1;
                ch.Stats.MoraleDamageBonus -= 1;
                ch.Stats.MoraleSaveBonus -= 1;

                enemyCount++;
            }
        }

        CombatUI?.ShowCombatLog($"<color=#FFD700>🙏 Prayer! {casterName} prays — {allyCount} allies gain +1 luck bonus, {enemyCount} enemies suffer –1 luck penalty. Duration: {durationRounds} rounds.</color>");
        Debug.Log($"[Prayer] {casterName}: allies={allyCount}, enemies={enemyCount}, duration={durationRounds} rounds");

        return true;
    }

    // ================================================================
    //  POISON  (PHB p.262)
    // ================================================================
    // Touch attack. Fortitude DC 14 negates.
    // Initial: 1d10 CON. Secondary: 1d10 CON (1 minute later).
    // We apply the initial damage and the Poisoned condition.

    private bool TryResolvePoisonSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.POISON) return false;
        if (caster == null || caster.Stats == null || target == null || target.Stats == null) return false;
        if (!result.Success) return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));

        // Check if target has Neutralize Poison immunity
        if (target.Stats.NeutralizePoisonImmunityActive)
        {
            CombatUI?.ShowCombatLog($"<color=#88FF88>🛡 {targetName} is immune to poison (Neutralize Poison)!</color>");
            return true;
        }

        // Fort save: DC = 10 + spell level (4) + WIS mod
        int saveDC = 10 + 4 + caster.Stats.WISMod;
        var saveResult = SpellSaveResolver.RollSave(target, SaveType.Fortitude, saveDC);
        bool saveSuccess = saveResult.Saved;

        if (saveSuccess)
        {
            CombatUI?.ShowCombatLog($"<color=#88FF88>☠ Poison: {targetName} resists! (Fort {saveRoll} vs DC {saveDC})</color>");
            Debug.Log($"[Poison] {casterName} -> {targetName}: Fort save {saveRoll} vs DC {saveDC} — resisted");
            return true;
        }

        // Initial CON damage: 1d10
        int conDamage = DiceRoller.D10();
        target.Stats.AbilityScoreDamage.ApplyDamage(AbilityType.CON, conDamage);

        // Apply Poisoned condition (for secondary damage tracking)
        if (!target.HasCondition(CombatConditionType.Poisoned))
            target.ApplyCondition(CombatConditionType.Poisoned, 10, "Poison");

        CombatUI?.ShowCombatLog($"<color=#CC44CC>☠ Poison! {casterName} poisons {targetName}! {conDamage} CON damage (Fort {saveRoll} vs DC {saveDC}). Secondary: 1d10 CON in 1 minute.</color>");
        Debug.Log($"[Poison] {casterName} -> {targetName}: {conDamage} CON damage, Fort save {saveRoll} vs DC {saveDC}");

        result.BuffApplied = true;
        result.BuffDescription = $"Poisoned ({conDamage} CON damage)";
        return true;
    }

    // ================================================================
    //  PHANTASMAL KILLER — PHB p.260
    //  Illusion (Phantasm) [Fear, Mind-Affecting]. Sor/Wiz 4.
    //  Range: Medium (100 ft + 10 ft/level).
    //  Will save to disbelieve.
    //  If fails Will: Fort save or die (3d6 damage + shaken on Fort success).
    //  SR: Yes. Mind-affecting, fear descriptor.
    // ================================================================

    private static bool IsPhantasmalKillerSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.PHANTASMAL_KILLER, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Phantasmal Killer: Will disbelieve, then Fort or die.
    /// On Fort success: 3d6 damage + shaken for 1 round.
    /// Mind-affecting, fear descriptor — undead/constructs/mindless immune.
    /// </summary>
    private bool TryResolvePhantasmalKillerSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (!IsPhantasmalKillerSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null)
            return true;

        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int saveDc = GetSpellSaveDC(caster, spell);

        CombatUI?.ShowCombatLog($"<color=#6600CC>👻 {casterName} casts Phantasmal Killer on {targetName}!</color>");

        // Mind-affecting immunity check
        if (target.Stats.IsImmuneToMindAffecting())
        {
            result.Success = false;
            result.NoEffectReason = $"{targetName} is immune to mind-affecting effects.";
            CombatUI?.ShowCombatLog($"<color=#66CC66>🛡 {targetName} is immune to mind-affecting effects!</color>");
            return true;
        }

        // Fear immunity check (undead, constructs, etc. are immune to fear)
        if (!IsLivingCreatureForFearSpell(target))
        {
            result.Success = false;
            result.NoEffectReason = $"{targetName} is immune to fear effects (not a living creature).";
            CombatUI?.ShowCombatLog($"<color=#66CC66>🧟 {targetName} is immune to fear effects!</color>");
            return true;
        }

        // SR check (done by pipeline if SpellResistanceApplies — but also handle manual check)
        // The pipeline should handle SR, but if it didn't, trust the result

        // Will save to disbelieve (this is the primary save from the spell pipeline)
        if (result.RequiredSave && result.SaveSucceeded)
        {
            CombatUI?.ShowCombatLog($"<color=#66CC66>🛡 {targetName} disbelieves the phantasm (Will save)!</color>");
            return true;
        }

        // Will save failed — now target must make a Fortitude save or die
        CombatUI?.ShowCombatLog($"<color=#CC0000>😱 {targetName} fails to disbelieve the phantasm!</color>");
        CombatUI?.ShowCombatLog($"<color=#CC3333>   Must make Fortitude save DC {saveDc} or die from fear!</color>");

        SavingThrowResolver.SaveResult fortSave = SavingThrowResolver.ResolveFortitudeSave(target.Stats, saveDc, "Phantasmal Killer (Fort)");

        string fortRollStr = $"d20({fortSave.Roll}) + {fortSave.Modifier} = {fortSave.Total} vs DC {saveDc}";

        if (fortSave.Succeeded)
        {
            // Fort succeeded: 3d6 damage + shaken for 1 round
            int damage = DiceService.RollMultiple(3, 6, "Phantasmal Killer 3d6 damage");

            int hpBefore = target.Stats.CurrentHP;
            target.Stats.TakeDamage(damage);
            int hpAfter = target.Stats.CurrentHP;

            result.DamageDealt = damage;

            // Apply Shaken for 1 round
            target.ApplyCondition(CombatConditionType.Shaken, 1, "Phantasmal Killer");

            CombatUI?.ShowCombatLog($"<color=#CC9933>   Fort save: {fortRollStr} → SUCCESS!</color>");
            CombatUI?.ShowCombatLog($"<color=#CC6600>   Takes {damage} damage ({hpBefore} → {hpAfter} HP) and is shaken for 1 round.</color>");

            // Check if damage killed the target
            if (hpAfter <= 0)
            {
                result.TargetKilled = true;
                CombatUI?.ShowCombatLog($"<color=#FF3333>☠ {targetName} is slain by the phantasm's lingering terror!</color>");
            }

            Debug.Log($"[PhantasmalKiller] {casterName} -> {targetName}: Will failed, Fort succeeded. {damage} damage, shaken 1 round.");
        }
        else
        {
            // Fort failed: TARGET DIES
            result.TargetKilled = true;

            CombatUI?.ShowCombatLog($"<color=#FF0000>   Fort save: {fortRollStr} → FAILED!</color>");
            CombatUI?.ShowCombatLog($"<color=#FF0000>💀 {targetName} DIES FROM FEAR! The phantasm's terror stops their heart!</color>");

            Debug.Log($"[PhantasmalKiller] {casterName} -> {targetName}: Will failed, Fort failed. TARGET DIES.");
        }

        return true;
    }

}
