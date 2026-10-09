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
    // Allies: +1 luck bonus on attack rolls, weapon damage rolls, saves and skill checks.
    // Foes: -1 penalty (untyped) on the same rolls.
    // Note: Prayer is cast on "self" but affects all in area. Each creature gets a tracked ActiveSpellEffect built from
    // a clone of the spell with the right values and type, so the luck bonus does not stack with another luck bonus
    // (Divine Favor, a stone of good luck; PHB glossary p.310) and the penalty stacks with other untyped penalties
    // (CharacterStats.Bonuses, BonusStacking).

    /// <summary>
    /// A clone of Prayer (never the database template) carrying what it gives one creature (PHB p.264): an ally a +1 luck
    /// bonus on attack rolls, weapon damage rolls, saves and skill checks; a foe a -1 penalty (untyped) on the same rolls.
    /// </summary>
    public static SpellData PrayerEffectSpell(SpellData prayer, bool ally)
    {
        SpellData clone = prayer.Clone();
        int value = ally ? 1 : -1;
        clone.BuffAttackBonus = value;
        clone.BuffDamageBonus = value;
        clone.BuffSaveBonus = value;
        clone.BuffSkillName = SpellData.AllSkillsBuffSkillName;
        clone.BuffSkillBonus = value;
        clone.BuffType = ally ? "luck" : "untyped";
        clone.BuffBonusType = ally ? BonusType.Luck : BonusType.Untyped;
        clone.BonusTypeExplicitlySet = true;
        return clone;
    }

    /// <summary>
    /// Gives one creature in Prayer's burst its share of the spell (PHB p.264): the caster and each ally the +1 luck
    /// effect, each foe the -1 penalty effect (<see cref="PrayerEffectSpell"/>). Shared by the burst resolver
    /// (<see cref="TryResolvePrayerSpellEffect"/>, the single-target and NPC cast paths) and by <c>ApplySpellBuff</c>,
    /// which the area cast path calls once per creature in the burst (SPL-042). Returns true for an ally.
    /// </summary>
    private bool ApplyPrayerToCreature(CharacterController caster, CharacterController creature, SpellData spell,
        int casterLevel, int durationRounds, out ActiveSpellEffect effect)
    {
        effect = null;
        if (creature == null || creature.Stats == null || spell == null)
            return false;
        bool isAlly = caster == null || creature == caster || TeamUtility.IsAlly(caster, creature);
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName ?? "Unknown" : "Unknown";
        StatusEffectManager statusMgr = creature.StatusEffectManager;
        if (statusMgr == null)
        {
            statusMgr = creature.gameObject.AddComponent<StatusEffectManager>();
            statusMgr.Init(creature.Stats);
        }
        if (isAlly)
        {
            creature.Stats.PrayerActive = true;
            creature.Stats.PrayerRoundsRemaining = durationRounds;
        }
        effect = statusMgr.AddEffect(PrayerEffectSpell(spell, isAlly), casterName, casterLevel, durationRounds);
        return isAlly;
    }

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
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel); // SpellDurationRules (SPL-002)
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

            if (ApplyPrayerToCreature(caster, ch, spell, casterLevel, durationRounds, out _))
                allyCount++;
            else
                enemyCount++;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Special("🙏", $"Prayer! {casterName} prays — {allyCount} allies gain +1 luck bonus, {enemyCount} enemies suffer a –1 penalty. Duration: {durationRounds} rounds."));
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
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);

        // Check if target has Neutralize Poison immunity
        if (target.Stats.NeutralizePoisonImmunityActive)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Success("🛡", $"{targetName} is immune to poison (Neutralize Poison)!"));
            return true;
        }

        // Fort save at the spell's DC (Clr 4, Drd 3; SpellSaveDCRules, SPL-001, SPL-031)
        int saveDC = GetSpellSaveDC(caster, spell);
        var saveResult = SpellSaveResolver.RollSave(target, SaveType.Fortitude, saveDC, SaveContext.ForSpell(spell, caster != null ? caster.Stats : null));
        bool saveSuccess = saveResult.Saved;

        if (saveSuccess)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Success("☠", $"Poison: {targetName} resists! (Fort {saveResult.Total} vs DC {saveDC})"));
            Debug.Log($"[Poison] {casterName} -> {targetName}: Fort save {saveResult.Total} vs DC {saveDC} — resisted");
            return true;
        }

        // Initial CON damage: 1d10
        int conDamage = DiceRoller.D10();
        target.Stats.AbilityScoreDamage.ApplyDamage(AbilityType.CON, conDamage);

        // Apply Poisoned condition (for secondary damage tracking)
        if (!target.HasCondition(CombatConditionType.Poisoned))
            target.ApplyCondition(CombatConditionType.Poisoned, 10, "Poison");

        CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("☠", $"Poison! {casterName} poisons {targetName}! {conDamage} CON damage (Fort {saveResult.Total} vs DC {saveDC}). Secondary: 1d10 CON in 1 minute."));
        Debug.Log($"[Poison] {casterName} -> {targetName}: {conDamage} CON damage, Fort save {saveResult.Total} vs DC {saveDC}");

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

        CombatUI?.ShowCombatLog(CombatLogHelper.Curse("", $"👻 {casterName} casts Phantasmal Killer on {targetName}!"));

        // Mind-affecting immunity check
        if (target.Stats.IsImmuneToMindAffecting())
        {
            result.Success = false;
            result.NoEffectReason = $"{targetName} is immune to mind-affecting effects.";
            CombatUI?.ShowCombatLog(CombatLogHelper.Immune("🛡", $"{targetName} is immune to mind-affecting effects!"));
            return true;
        }

        // Fear immunity check (undead, constructs, etc. are immune to fear)
        if (!SpellTargetingService.IsLivingCreature(target))
        {
            result.Success = false;
            result.NoEffectReason = $"{targetName} is immune to fear effects (not a living creature).";
            CombatUI?.ShowCombatLog(CombatLogHelper.Immune("🧟", $"{targetName} is immune to fear effects!"));
            return true;
        }

        // SR check (done by pipeline if SpellResistanceApplies — but also handle manual check)
        // The pipeline should handle SR, but if it didn't, trust the result

        // Will save to disbelieve (this is the primary save from the spell pipeline)
        if (result.RequiredSave && result.SaveSucceeded)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Immune("🛡", $"{targetName} disbelieves the phantasm (Will save)!"));
            return true;
        }

        // Will save failed — now target must make a Fortitude save or die
        CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("😱", $"{targetName} fails to disbelieve the phantasm!"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"   Must make Fortitude save DC {saveDc} or die from fear!"));

        SavingThrowResolver.SaveResult fortSave = SavingThrowResolver.ResolveFortitudeSave(target.Stats, saveDc, "Phantasmal Killer (Fort)",
            SaveContext.ForSpell(spell, caster != null ? caster.Stats : null));

        string fortRollStr = $"d20({fortSave.Roll}) + {fortSave.Modifier} = {fortSave.Total} vs DC {saveDc}";

        if (fortSave.Succeeded)
        {
            // Fort succeeded: 3d6 damage + shaken for 1 round
            int damage = DiceService.RollMultiple(3, 6, "Phantasmal Killer 3d6 damage");

            // Untyped damage through the mitigation pipeline (SPL-004); the single-target pipeline runs
            // concentration, death and the combat-end check from result.
            int hpBefore = target.Stats.CurrentHP;
            damage = ApplyDamagePacket(target, damage, DamagePackets.Spell(spell.Name, DamageType.Untyped)).FinalDamage;
            int hpAfter = target.Stats.CurrentHP;

            result.DamageDealt = damage;

            // Apply Shaken for 1 round
            target.ApplyCondition(CombatConditionType.Shaken, 1, "Phantasmal Killer");

            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("", $"   Fort save: {fortRollStr} → SUCCESS!"));
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("", $"   Takes {damage} damage ({hpBefore} → {hpAfter} HP) and is shaken for 1 round."));

            // Check if damage killed the target (dead at -10 or lower, PHB p.145; 0 or below is disabled or dying)
            if (target.Stats.IsDead)
            {
                result.TargetKilled = true;
                CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("☠", $"{targetName} is slain by the phantasm's lingering terror!"));
            }

            Debug.Log($"[PhantasmalKiller] {casterName} -> {targetName}: Will failed, Fort succeeded. {damage} damage, shaken 1 round.");
        }
        else
        {
            // Fort failed: TARGET DIES
            result.TargetKilled = true;

            CombatUI?.ShowCombatLog(CombatLogHelper.Death("", $"  Fort save: {fortRollStr} → FAILED!"));
            CombatUI?.ShowCombatLog(CombatLogHelper.Death("💀", $"{targetName} DIES FROM FEAR! The phantasm's terror stops their heart!"));

            Debug.Log($"[PhantasmalKiller] {casterName} -> {targetName}: Will failed, Fort failed. TARGET DIES.");
        }

        return true;
    }

}
