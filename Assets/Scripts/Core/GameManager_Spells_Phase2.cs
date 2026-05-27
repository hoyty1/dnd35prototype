// ============================================================================
// GameManager_Spells_Phase2.cs — Phase 2 & 3 Staff Spell Resolution Methods
//
// Part of the GameManager partial class.
// Implements 12 spells required for Phase 2 & 3 staff completion.
// All spells follow D&D 3.5e PHB/DMG/MM core rules ONLY.
// ============================================================================
using DND35e.Identifiers;
using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine;

public partial class GameManager
{
    // ================================================================
    //  DISINTEGRATE — PHB p.222
    //  Transmutation. Ranged touch ray, 2d6/CL (max 40d6).
    //  Fort save: 5d6 instead. SR: Yes.
    // ================================================================

    private ActiveSpellEffect ApplyDisintegrateEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || target == null || spell == null) return null;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int saveDc = SpellUtilities.GetSpellSaveDC(caster, spell);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"💀 {caster.Stats.CharacterName} casts Disintegrate!");
        sb.AppendLine($"  School: Transmutation | Level: 6 | Ranged Touch Ray");
        sb.AppendLine($"  Fort DC {saveDc} partial (5d6) | SR: Yes");
        sb.AppendLine($"  Target: {target.Stats.CharacterName}");

        // Spell Resistance + Fort save
        var srResult = SpellSaveResolver.RollSpellResistance(caster, target, casterLevel);
        srResult.AppendToLog(sb);
        if (!srResult.Overcame)
        {
            sb.AppendLine($"  ✦ {target.Stats.CharacterName} resists (Spell Resistance)!");
            sb.Append("═══════════════════════════════════");
            CombatUI?.ShowCombatLog(sb.ToString());
            return null;
        }

        // Fort save
        var saveResult = SpellSaveResolver.RollSave(target, SaveType.Fortitude, saveDc);
        bool saved = saveResult.Saved;
        saveResult.AppendToLog(sb, "SAVED", "FAILED");

        int damage;
        if (saved)
        {
            // On successful save: 5d6 damage
            damage = 0;
            for (int i = 0; i < 5; i++)
                damage += DiceRoller.D6();
            sb.AppendLine($"  Partial: 5d6 = {damage} damage (Fort save succeeded)");
        }
        else
        {
            // Full damage: 2d6 per CL, max 40d6
            int diceCount = Mathf.Clamp(casterLevel * 2, 2, 40);
            damage = 0;
            for (int i = 0; i < diceCount; i++)
                damage += DiceRoller.D6();
            sb.AppendLine($"  DISINTEGRATED: {diceCount}d6 = {damage} damage!");
        }

        int hpBefore = target.Stats.CurrentHP;
        target.Stats.TakeDamage(damage);
        int hpAfter = target.Stats.CurrentHP;
        sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

        CheckConcentrationOnDamage(target, damage);

        if (target.Stats.IsDead)
        {
            target.OnDeath();
            HandleSummonDeathCleanup(target);
            sb.AppendLine($"  💀 {target.Stats.CharacterName} is reduced to fine dust!");
        }

        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        return null;
    }

    // ================================================================
    //  SUNBURST — PHB p.289
    //  Evocation [Light]. 80-ft burst. 6d6 damage (undead: 1d6/CL max 25d6).
    //  Blinds permanently. Reflex negates blind, halves damage. SR: Yes.
    // ================================================================

    private bool TryResolveSunburstSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || spell == null) return false;
        if (!string.Equals(spell.SpellId, SpellNames.SUNBURST, StringComparison.Ordinal))
            return false;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int saveDc = SpellUtilities.GetSpellSaveDC(caster, spell);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"☀ {caster.Stats.CharacterName} casts Sunburst!");
        sb.AppendLine($"  School: Evocation [Light] | Level: 8 | 80-ft Burst");
        sb.AppendLine($"  Damage: 6d6 (undead: {Mathf.Min(casterLevel, 25)}d6) | Reflex DC {saveDc} | SR: Yes");
        sb.AppendLine($"  Targets: {(targets != null ? targets.Count : 0)} creature(s)");
        sb.AppendLine();

        if (targets == null || targets.Count == 0)
        {
            sb.AppendLine("  No valid targets in area!");
        }
        else
        {
            int idx = 0;
            foreach (var target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead) continue;
                idx++;
                sb.AppendLine($"  --- Target {idx}: {target.Stats.CharacterName} ---");

                // SR check
                var srResult = SpellSaveResolver.RollSpellResistance(caster, target, casterLevel);
                srResult.AppendToLog(sb);
                if (!srResult.Overcame) { sb.AppendLine(); continue; }

                // Determine damage dice: undead get 1d6/CL (max 25d6), others 6d6
                bool isUndead = !string.IsNullOrEmpty(target.Stats.CreatureType) &&
                    string.Equals(target.Stats.CreatureType, "Undead", StringComparison.OrdinalIgnoreCase);
                int diceCount = isUndead ? Mathf.Clamp(casterLevel, 1, 25) : 6;

                int damage = 0;
                for (int i = 0; i < diceCount; i++)
                    damage += DiceRoller.D6();
                sb.AppendLine($"    Damage roll: {diceCount}d6 = {damage}{(isUndead ? " (Undead)" : "")}");

                // Reflex save + Evasion
                var saveResult = SpellSaveResolver.RollSave(target, SaveType.Reflex, saveDc);
                saveResult.AppendHalfDamageLog(sb);
                if (saveResult.Saved)
                {
                    damage = Mathf.Max(1, damage / 2);
                    damage = SpellSaveResolver.ApplyEvasion(damage, target, true, sb);
                }

                if (damage > 0)
                {
                    int hpBefore = target.Stats.CurrentHP;
                    target.Stats.TakeDamage(damage);
                    sb.AppendLine($"    {target.Stats.CharacterName}: {hpBefore} → {target.Stats.CurrentHP} HP");
                    CheckConcentrationOnDamage(target, damage);
                }

                // Blindness on failed save (permanent)
                if (!saved)
                {
                    target.Stats.ApplyCondition(CombatConditionType.Blinded, 9999, "Sunburst");
                    sb.AppendLine($"    👁 BLINDED permanently!");
                }

                if (target.Stats.IsDead)
                {
                    target.OnDeath();
                    HandleSummonDeathCleanup(target);
                    sb.AppendLine($"    💀 {target.Stats.CharacterName} has been destroyed!");
                }

                sb.AppendLine();
            }
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  EARTHQUAKE — PHB p.225
    //  Evocation [Earth]. 80-ft spread. Knocks prone. 1 round.
    //  Reflex DC to avoid falling. No SR.
    // ================================================================

    private bool TryResolveEarthquakeSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || spell == null) return false;
        if (!string.Equals(spell.SpellId, SpellNames.EARTHQUAKE, StringComparison.Ordinal))
            return false;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int saveDc = SpellUtilities.GetSpellSaveDC(caster, spell);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🌋 {caster.Stats.CharacterName} casts Earthquake!");
        sb.AppendLine($"  School: Evocation [Earth] | Level: 8 | 80-ft Spread");
        sb.AppendLine($"  Reflex DC {saveDc} or knocked prone | Duration: 1 round | No SR");
        sb.AppendLine($"  Targets: {(targets != null ? targets.Count : 0)} creature(s)");
        sb.AppendLine();

        if (targets != null)
        {
            int idx = 0;
            foreach (var target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead) continue;
                idx++;
                sb.AppendLine($"  --- Target {idx}: {target.Stats.CharacterName} ---");

                var saveResult = SpellSaveResolver.RollSave(target, SaveType.Reflex, saveDc);
                saveResult.AppendToLog(sb, "SAVED", "FAILED");

                if (!saveResult.Saved)
                {
                    target.Stats.ApplyCondition(CombatConditionType.Prone, 1, "Earthquake");
                    sb.AppendLine($"    🔻 Knocked PRONE!");

                    // Debris damage: simplified (structures collapse for 8d6 in PHB, minor outdoors)
                    int debrisDmg = DiceRoller.D6();
                    target.Stats.TakeDamage(debrisDmg);
                    sb.AppendLine($"    Debris: {debrisDmg} bludgeoning damage");
                    CheckConcentrationOnDamage(target, debrisDmg);

                    if (target.Stats.IsDead)
                    {
                        target.OnDeath();
                        HandleSummonDeathCleanup(target);
                        sb.AppendLine($"    💀 {target.Stats.CharacterName} crushed!");
                    }
                }
                else
                {
                    sb.AppendLine($"    Keeps footing!");
                }
                sb.AppendLine();
            }
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  SHIELD OF LAW — PHB p.278
    //  Abjuration [Lawful]. +4 deflection AC, +4 resistance saves.
    //  SR 25 vs chaotic spells. Blocks chaotic mental control.
    //  Duration: 1 round/level. AoE buff on allies.
    // ================================================================

    private bool TryResolveShieldOfLawSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || spell == null) return false;
        if (!string.Equals(spell.SpellId, SpellNames.SHIELD_OF_LAW, StringComparison.Ordinal))
            return false;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        string casterName = caster.Stats.CharacterName;

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"⚖ {casterName} casts Shield of Law!");
        sb.AppendLine($"  School: Abjuration [Lawful] | Level: 8");
        sb.AppendLine($"  +4 deflection AC, +4 resistance saves, SR 25 vs chaos");
        sb.AppendLine($"  Duration: {casterLevel} rounds | Targets: {(targets != null ? targets.Count : 0)}");
        sb.AppendLine();

        if (targets != null)
        {
            foreach (var target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead) continue;

                StatusEffectManager statusMgr = target.StatusEffectManager;
                if (statusMgr == null)
                {
                    statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
                    statusMgr.Init(target.Stats);
                }

                ActiveSpellEffect effect = statusMgr.AddEffect(spell, casterName, casterLevel);
                if (effect != null)
                    sb.AppendLine($"  ✦ {target.Stats.CharacterName}: +4 deflection AC, +4 resistance saves [{casterLevel} rds]");
                else
                    sb.AppendLine($"  ✦ {target.Stats.CharacterName}: effect already active (stacking prevented)");
            }
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  PROTECTION FROM SPELLS — PHB p.266
    //  Abjuration. +8 resistance bonus on saves vs spells.
    //  Duration: 10 min/level. Single target (touch).
    // ================================================================

    private ActiveSpellEffect ApplyProtectionFromSpellsEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || target == null || spell == null) return null;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        string casterName = caster.Stats.CharacterName;

        StatusEffectManager statusMgr = target.StatusEffectManager;
        if (statusMgr == null)
        {
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            statusMgr.Init(target.Stats);
        }

        ActiveSpellEffect effect = statusMgr.AddEffect(spell, casterName, casterLevel);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🛡 {casterName} casts Protection from Spells!");
        sb.AppendLine($"  School: Abjuration | Level: 8 | Touch");
        sb.AppendLine($"  +8 resistance bonus on saves vs spells");
        sb.AppendLine($"  Target: {target.Stats.CharacterName}");
        if (effect != null)
            sb.AppendLine($"  ✦ {target.Stats.CharacterName}: +8 resistance saves (vs spells)");
        else
            sb.AppendLine($"  ✦ Effect already active (stacking prevented)");
        sb.Append("═══════════════════════════════════");

        CombatUI?.ShowCombatLog(sb.ToString());
        return null;
    }

    // ================================================================
    //  SPELL TURNING — PHB p.282
    //  Abjuration. Reflect 1d4+6 spell levels back at caster.
    //  Duration: until expended or 10 min/level. Self-only.
    // ================================================================

    private ActiveSpellEffect ApplySpellTurningEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || spell == null) return null;

        target = caster; // Self-only
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int turningLevels = DiceRoller.D4() + 6; // 1d4+6
        string casterName = caster.Stats.CharacterName;

        StatusEffectManager statusMgr = target.StatusEffectManager;
        if (statusMgr == null)
        {
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            statusMgr.Init(target.Stats);
        }

        ActiveSpellEffect effect = statusMgr.AddEffect(spell, casterName, casterLevel);
        if (effect != null)
            effect.CustomTag = $"SpellTurning:{turningLevels}"; // Store turning pool

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🔄 {casterName} casts Spell Turning!");
        sb.AppendLine($"  School: Abjuration | Level: 7 | Self");
        sb.AppendLine($"  Reflects {turningLevels} spell levels back at casters");
        sb.AppendLine($"  ✦ {target.Stats.CharacterName}: Spell Turning active ({turningLevels} levels remaining)");
        sb.Append("═══════════════════════════════════");

        CombatUI?.ShowCombatLog(sb.ToString());
        return null;
    }

    // ================================================================
    //  HEAL — PHB p.239
    //  Conjuration (Healing). 10 HP/CL (max 150). Cures conditions.
    //  No effect on undead (deals damage to undead). SR: Yes (harmless).
    // ================================================================

    private ActiveSpellEffect ApplyHealSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || target == null || spell == null) return null;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"💚 {caster.Stats.CharacterName} casts Heal!");
        sb.AppendLine($"  School: Conjuration (Healing) | Level: 6 | Touch");
        sb.AppendLine($"  Target: {target.Stats.CharacterName}");

        // Check undead — Heal damages undead (like Harm)
        bool isUndead = !string.IsNullOrEmpty(target.Stats.CreatureType) &&
            string.Equals(target.Stats.CreatureType, "Undead", StringComparison.OrdinalIgnoreCase);

        if (isUndead)
        {
            int damage = Mathf.Min(casterLevel * 10, 150);
            int saveDc = SpellUtilities.GetSpellSaveDC(caster, spell);
            var saveResult = SpellSaveResolver.RollSave(target, SaveType.Will, saveDc);
            sb.AppendLine($"  ☠ Undead — positive energy deals damage!");
            saveResult.AppendHalfDamageLog(sb);
            bool saved = saveResult.Saved;

            if (saved) damage = Mathf.Max(1, damage / 2);
            int hpBefore = target.Stats.CurrentHP;
            target.Stats.TakeDamage(damage);
            sb.AppendLine($"  Damage: {damage} | {target.Stats.CharacterName}: {hpBefore} → {target.Stats.CurrentHP} HP");
            CheckConcentrationOnDamage(target, damage);

            if (target.Stats.IsDead)
            {
                target.OnDeath();
                HandleSummonDeathCleanup(target);
                sb.AppendLine($"  💀 {target.Stats.CharacterName} destroyed by positive energy!");
            }
        }
        else
        {
            // Heal living creature: 10 HP/CL, max 150
            int healAmount = Mathf.Min(casterLevel * 10, 150);
            int hpBefore = target.Stats.CurrentHP;
            int nonlethalHealed;
            int actualHealed = target.Stats.HealDamage(healAmount, out nonlethalHealed);
            sb.AppendLine($"  Heals: {healAmount} HP (actual: {actualHealed})");
            sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {target.Stats.CurrentHP} HP");

            // Cure conditions per PHB p.239
            var conditionsToCure = new[]
            {
                CombatConditionType.Blinded,
                CombatConditionType.Confused,
                CombatConditionType.Dazed,
                CombatConditionType.Deafened,
                CombatConditionType.Exhausted,
                CombatConditionType.Fatigued,
                CombatConditionType.Nauseated,
                CombatConditionType.Sickened,
                CombatConditionType.Stunned,
            };

            var curedList = new List<string>();
            foreach (var cond in conditionsToCure)
            {
                if (target.Stats.RemoveCondition(cond))
                    curedList.Add(cond.ToString());
            }

            // Heal all ability damage
            int abilityHealed = target.Stats.HealAllAbilityDamage(999);
            if (abilityHealed > 0)
                curedList.Add($"Ability damage ({abilityHealed} pts)");

            if (curedList.Count > 0)
                sb.AppendLine($"  Conditions cured: {string.Join(", ", curedList)}");
            else
                sb.AppendLine($"  No conditions to cure.");
        }

        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        return null;
    }

    // ================================================================
    //  RESURRECTION — PHB p.272
    //  Conjuration (Healing). Restore dead to life, full HP, lose 1 level.
    //  Cannot raise undead/constructs/outsiders.
    // ================================================================

    private ActiveSpellEffect ApplyResurrectionEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || target == null || spell == null) return null;

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"✝ {caster.Stats.CharacterName} casts Resurrection!");
        sb.AppendLine($"  School: Conjuration (Healing) | Level: 7 | Touch");
        sb.AppendLine($"  Target: {target.Stats.CharacterName}");

        // Check creature type restrictions
        string ct = target.Stats.CreatureType ?? "";
        bool isUndead = string.Equals(ct, "Undead", StringComparison.OrdinalIgnoreCase);
        bool isConstruct = string.Equals(ct, "Construct", StringComparison.OrdinalIgnoreCase);

        if (isUndead || isConstruct)
        {
            sb.AppendLine($"  ✘ Cannot resurrect {ct} creatures!");
            sb.Append("═══════════════════════════════════");
            CombatUI?.ShowCombatLog(sb.ToString());
            return null;
        }

        if (!target.Stats.IsDead)
        {
            sb.AppendLine($"  ✘ {target.Stats.CharacterName} is not dead!");
            sb.Append("═══════════════════════════════════");
            CombatUI?.ShowCombatLog(sb.ToString());
            return null;
        }

        // Restore to life with full HP
        int maxHP = target.Stats.TotalMaxHP;
        target.Stats.CurrentHP = maxHP;

        // Lose one level: apply as a negative level (permanent)
        // D&D 3.5e: "creature loses one level" — we use the energy drain system
        int level = target.Stats.Level;
        if (level > 1)
        {
            target.Stats.ApplyCondition(CombatConditionType.EnergyDrained, -1, "Resurrection");
            sb.AppendLine($"  Restored to life! Full HP ({maxHP})");
            sb.AppendLine($"  Lost 1 level (negative level applied, effective level {level - 1})");
        }
        else
        {
            // Level 1: lose 2 CON instead
            int conLoss = 2;
            target.Stats.CON = Mathf.Max(1, target.Stats.CON - conLoss);
            sb.AppendLine($"  Restored to life! Full HP ({maxHP})");
            sb.AppendLine($"  Lost 2 CON (now CON {target.Stats.CON}) — too low level to lose a level");
        }

        // Clear death/prone conditions
        target.Stats.RemoveCondition(CombatConditionType.Prone);

        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        return null;
    }

    // ================================================================
    //  TRUE SEEING — PHB p.296
    //  Divination. See through illusions, darkness, invisibility.
    //  120 ft range. Duration: 1 min/level.
    // ================================================================

    private ActiveSpellEffect ApplyTrueSeeingEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || target == null || spell == null) return null;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = casterLevel * 10; // 1 min/level
        string casterName = caster.Stats.CharacterName;

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"👁 {casterName} casts True Seeing!");
        sb.AppendLine($"  School: Divination | Level: 5 | Touch");
        sb.AppendLine($"  Duration: {durationRounds} rounds ({casterLevel} min)");
        sb.AppendLine($"  Target: {target.Stats.CharacterName}");

        // Apply via StatusEffectManager
        StatusEffectManager statusMgr = target.StatusEffectManager;
        if (statusMgr == null)
        {
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            statusMgr.Init(target.Stats);
        }
        ActiveSpellEffect effect = statusMgr.AddEffect(spell, casterName, casterLevel);

        // Grant see invisibility via the proper system
        target.ApplySeeInvisibilityEffect(durationRounds, caster);

        sb.AppendLine($"  ✦ {target.Stats.CharacterName}: True Seeing active — sees through illusions, darkness, invisibility");
        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        return null;
    }

    // ================================================================
    //  MISLEAD — PHB p.254
    //  Illusion. Caster becomes invisible (Greater Invisibility).
    //  Illusory double in place. Duration: 1 round/level (concentration).
    // ================================================================

    private ActiveSpellEffect ApplyMisleadEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || spell == null) return null;

        target = caster; // Self-only
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = casterLevel; // 1 round/level
        string casterName = caster.Stats.CharacterName;

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"👻 {casterName} casts Mislead!");
        sb.AppendLine($"  School: Illusion | Level: 6 | Self");
        sb.AppendLine($"  Greater Invisibility + Illusory Double");
        sb.AppendLine($"  Duration: {durationRounds} rounds");

        // Apply Greater Invisibility via the proper invisibility system
        var invisData = InvisibilityEffectData.CreateGreaterInvisibility(durationRounds, caster);
        invisData.SourceSpellId = SpellNames.MISLEAD;
        invisData.SourceName = "Mislead";
        target.ApplyInvisibilityEffectData(invisData);

        // Also register as a spell effect for duration tracking
        StatusEffectManager statusMgr = target.StatusEffectManager;
        if (statusMgr == null)
        {
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            statusMgr.Init(target.Stats);
        }
        statusMgr.AddEffect(spell, casterName, casterLevel);

        sb.AppendLine($"  ✦ {target.Stats.CharacterName}: INVISIBLE (Greater) + illusory double created");
        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        return null;
    }

    // ================================================================
    //  DANCING LIGHTS — PHB p.216
    //  Evocation. 4 torch-like lights. Move 100 ft/round.
    //  Duration: 1 minute. Cantrip (level 0).
    // ================================================================

    private ActiveSpellEffect ApplyDancingLightsEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || spell == null) return null;

        target = caster; // Self-only
        string casterName = caster.Stats.CharacterName;

        StatusEffectManager statusMgr = target.StatusEffectManager;
        if (statusMgr == null)
        {
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            statusMgr.Init(target.Stats);
        }
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        statusMgr.AddEffect(spell, casterName, casterLevel);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"💡 {casterName} casts Dancing Lights!");
        sb.AppendLine($"  School: Evocation | Level: 0 (Cantrip)");
        sb.AppendLine($"  4 torch-like lights appear within Medium range");
        sb.AppendLine($"  Duration: 10 rounds (1 min) | Move lights up to 100 ft as move action");
        sb.AppendLine($"  ✦ {casterName}: Dancing Lights active");
        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        return null;
    }

    // ================================================================
    //  PLANE SHIFT — PHB p.262
    //  Conjuration (Teleportation). Touch attack, Will negates.
    //  On failed save: target is removed from combat (shifted to another plane).
    //  Treated as instant removal for combat purposes. SR: Yes.
    // ================================================================

    private ActiveSpellEffect ApplyPlaneShiftEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || target == null || spell == null) return null;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int saveDc = SpellUtilities.GetSpellSaveDC(caster, spell);
        string casterName = caster.Stats.CharacterName;

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🌀 {casterName} casts Plane Shift!");
        sb.AppendLine($"  School: Conjuration (Teleportation) | Level: 7 | Touch");
        sb.AppendLine($"  Will DC {saveDc} negates | SR: Yes");
        sb.AppendLine($"  Target: {target.Stats.CharacterName}");

        // Spell Resistance + Will save
        var srResult = SpellSaveResolver.RollSpellResistance(caster, target, casterLevel);
        srResult.AppendToLog(sb);
        if (!srResult.Overcame)
        {
            sb.AppendLine($"  ✦ {target.Stats.CharacterName} resists (Spell Resistance)!");
            sb.Append("═══════════════════════════════════");
            CombatUI?.ShowCombatLog(sb.ToString());
            return null;
        }

        // Will save
        var saveResult = SpellSaveResolver.RollSave(target, SaveType.Will, saveDc);
        bool saved = saveResult.Saved;
        saveResult.AppendToLog(sb, "SAVED", "FAILED");

        if (saved)
        {
            sb.AppendLine($"  ✦ {target.Stats.CharacterName} resists the planar transport!");
        }
        else
        {
            sb.AppendLine($"  🌀 {target.Stats.CharacterName} is shifted to another plane!");
            sb.AppendLine($"  Target is removed from combat!");

            // Remove from combat: kill the target (treated as removed from battlefield)
            // Set HP to lethal threshold to trigger death/removal
            target.Stats.CurrentHP = -10;
            target.OnDeath();
            HandleSummonDeathCleanup(target);
            sb.AppendLine($"  💫 {target.Stats.CharacterName} vanishes in a shimmer of planar energy!");
        }

        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        return null;
    }

    // ================================================================
    //  ALTER SELF — PHB p.197
    //  Transmutation. Change form. +2 size STR (or DEX).
    //  +10 Disguise. Duration: 10 min/level (D).
    // ================================================================

    private ActiveSpellEffect ApplyAlterSelfEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || spell == null) return null;

        target = caster; // Self-only
        string casterName = caster.Stats.CharacterName;
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);

        StatusEffectManager statusMgr = target.StatusEffectManager;
        if (statusMgr == null)
        {
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
            statusMgr.Init(target.Stats);
        }

        ActiveSpellEffect effect = statusMgr.AddEffect(spell, casterName, casterLevel);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🎭 {casterName} casts Alter Self!");
        sb.AppendLine($"  School: Transmutation | Level: 2 | Self");
        sb.AppendLine($"  +2 size bonus to STR, +10 Disguise check bonus");
        int dur = ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel);
        sb.AppendLine($"  Duration: {dur} rounds ({casterLevel * 10} min)");

        if (effect != null)
            sb.AppendLine($"  ✦ {target.Stats.CharacterName}: +2 size STR, altered form");
        else
            sb.AppendLine($"  ✦ Effect already active (stacking prevented)");

        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        return null;
    }
}
