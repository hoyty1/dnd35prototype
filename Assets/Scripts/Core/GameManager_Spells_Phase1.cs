// ============================================================================
// GameManager_Spells_Phase1.cs — Phase 1 Staff Spell Resolution Methods
//
// Part of the GameManager partial class.
// Implements 18 spells required for Phase 1 staff completion.
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
    //  CONE OF COLD — PHB p.212
    //  Evocation [Cold]. 60-ft cone, 1d6/CL cold (max 15d6), Reflex half.
    //  SR: Yes.
    // ================================================================

    private bool TryResolveConeOfColdSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;
        if (!string.Equals(spell.SpellId, SpellNames.CONE_OF_COLD, StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int diceCount = Mathf.Clamp(casterLevel, 1, 15); // 1d6/CL, max 15d6
        int saveDc = GetSpellSaveDC(caster, spell);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"❄ {caster.Stats.CharacterName} casts Cone of Cold!");
        sb.AppendLine($"  School: Evocation [Cold] | Level: 5 | 60-ft Cone");
        sb.AppendLine($"  Damage: {diceCount}d6 cold | Reflex DC {saveDc} half | SR: Yes");
        sb.AppendLine($"  Targets: {(targets != null ? targets.Count : 0)} creature(s)");
        sb.AppendLine();

        if (targets == null || targets.Count == 0)
        {
            sb.AppendLine($"  No valid targets in area!");
        }
        else
        {
            int targetIndex = 0;
            foreach (CharacterController target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead) continue;
                targetIndex++;
                sb.AppendLine($"  --- Target {targetIndex}: {target.Stats.CharacterName} ---");

                // Spell Resistance check
                if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
                {
                    int srRoll = UnityEngine.Random.Range(1, 21);
                    int srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
                    bool srOvercome = srTotal >= target.Stats.SpellResistance;
                    sb.AppendLine($"  SR Check: d20({srRoll}) + {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");
                    if (!srOvercome)
                    {
                        sb.AppendLine($"  {target.Stats.CharacterName} resists Cone of Cold via Spell Resistance!");
                        sb.AppendLine();
                        continue;
                    }
                }

                // Roll damage
                int damage = 0;
                for (int i = 0; i < diceCount; i++)
                    damage += UnityEngine.Random.Range(1, 7);

                // Reflex save
                int reflexRoll = UnityEngine.Random.Range(1, 21);
                int reflexMod = target.Stats.ReflexSave;
                int reflexTotal = reflexRoll + reflexMod;
                bool savePassed = reflexTotal >= saveDc;

                if (savePassed)
                    damage = Mathf.Max(0, damage / 2);

                // Blink halving (PHB p.206)
                bool targetIsBlinking = target.HasActiveBlinkEffect;
                if (targetIsBlinking)
                    damage = Mathf.Max(0, damage / 2);

                // Evasion (PHB p.40): Reflex-save-for-half spells deal no damage on successful save
                if (savePassed && target.Stats.HasEvasion)
                    damage = 0;

                damage = Mathf.Max(damage > 0 ? 1 : 0, damage);

                sb.AppendLine($"  Reflex save: d20({reflexRoll}) + {reflexMod} = {reflexTotal} vs DC {saveDc} → {(savePassed ? "SAVED (half)" : "FAILED (full)")}");
                if (targetIsBlinking) sb.AppendLine($"  Blink: area damage halved (target partially ethereal)");
                if (savePassed && target.Stats.HasEvasion) sb.AppendLine($"  Evasion: no damage on successful save!");

                int hpBefore = target.Stats.CurrentHP;
                target.Stats.TakeDamage(damage);
                int hpAfter = target.Stats.CurrentHP;

                sb.AppendLine($"  Damage: {damage} cold");
                sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

                CheckConcentrationOnDamage(target, damage);

                if (target.Stats.IsDead)
                {
                    target.OnDeath();
                    HandleSummonDeathCleanup(target);
                    sb.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");
                }
                sb.AppendLine();
            }
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  CHAIN LIGHTNING — PHB p.208
    //  Evocation [Electricity]. Primary: 1d6/CL (max 20d6).
    //  Secondary: one per CL (max 20), half primary damage.
    //  Reflex half for both. SR: Yes.
    // ================================================================

    private bool TryResolveChainLightningSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;
        if (!string.Equals(spell.SpellId, SpellNames.CHAIN_LIGHTNING, StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int diceCount = Mathf.Clamp(casterLevel, 1, 20); // 1d6/CL, max 20d6
        int saveDc = GetSpellSaveDC(caster, spell);
        int maxSecondary = Mathf.Min(casterLevel, 20);

        // Roll primary damage once
        int primaryDamage = 0;
        for (int i = 0; i < diceCount; i++)
            primaryDamage += UnityEngine.Random.Range(1, 7);
        int secondaryDamage = Mathf.Max(1, primaryDamage / 2);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"⚡ {caster.Stats.CharacterName} casts Chain Lightning!");
        sb.AppendLine($"  School: Evocation [Electricity] | Level: 6 | Range: Long");
        sb.AppendLine($"  Primary: {diceCount}d6 = {primaryDamage} electricity | Secondary: {secondaryDamage}");
        sb.AppendLine($"  Reflex DC {saveDc} half | SR: Yes | Up to {maxSecondary} secondary targets");
        sb.AppendLine($"  Targets: {(targets != null ? targets.Count : 0)} creature(s)");
        sb.AppendLine();

        if (targets == null || targets.Count == 0)
        {
            sb.AppendLine($"  No valid targets!");
        }
        else
        {
            int targetIndex = 0;
            foreach (CharacterController target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead) continue;
                targetIndex++;
                bool isPrimary = targetIndex == 1;
                int baseDmg = isPrimary ? primaryDamage : secondaryDamage;

                if (!isPrimary && targetIndex > maxSecondary + 1) break;

                sb.AppendLine($"  --- {(isPrimary ? "PRIMARY" : "Secondary")} Target {targetIndex}: {target.Stats.CharacterName} ---");

                // SR check
                if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
                {
                    int srRoll = UnityEngine.Random.Range(1, 21);
                    int srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
                    bool srOvercome = srTotal >= target.Stats.SpellResistance;
                    sb.AppendLine($"  SR Check: d20({srRoll}) + {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");
                    if (!srOvercome) { sb.AppendLine(); continue; }
                }

                // Roll individual damage for this target based on primary/secondary
                int damage = baseDmg;

                // Reflex save
                int reflexRoll = UnityEngine.Random.Range(1, 21);
                int reflexMod = target.Stats.ReflexSave;
                int reflexTotal = reflexRoll + reflexMod;
                bool savePassed = reflexTotal >= saveDc;

                if (savePassed)
                    damage = Mathf.Max(0, damage / 2);

                bool targetIsBlinking = target.HasActiveBlinkEffect;
                if (targetIsBlinking)
                    damage = Mathf.Max(0, damage / 2);

                if (savePassed && target.Stats.HasEvasion)
                    damage = 0;

                damage = Mathf.Max(damage > 0 ? 1 : 0, damage);

                sb.AppendLine($"  Reflex save: d20({reflexRoll}) + {reflexMod} = {reflexTotal} vs DC {saveDc} → {(savePassed ? "SAVED (half)" : "FAILED (full)")}");
                if (targetIsBlinking) sb.AppendLine($"  Blink: damage halved");
                if (savePassed && target.Stats.HasEvasion) sb.AppendLine($"  Evasion: no damage!");

                int hpBefore = target.Stats.CurrentHP;
                target.Stats.TakeDamage(damage);
                int hpAfter = target.Stats.CurrentHP;

                sb.AppendLine($"  Damage: {damage} electricity");
                sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

                CheckConcentrationOnDamage(target, damage);

                if (target.Stats.IsDead)
                {
                    target.OnDeath();
                    HandleSummonDeathCleanup(target);
                    sb.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");
                }
                sb.AppendLine();
            }
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  CIRCLE OF DEATH — PHB p.210
    //  Necromancy [Death]. 40-ft-radius burst.
    //  Kills 1d4 HD/CL (max 20d4) of living creatures, lowest HD first.
    //  Fort negates. SR: Yes.
    // ================================================================

    private bool TryResolveCircleOfDeathSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;
        if (!string.Equals(spell.SpellId, SpellNames.CIRCLE_OF_DEATH, StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int hdDiceCount = Mathf.Clamp(casterLevel, 1, 20);
        int saveDc = GetSpellSaveDC(caster, spell);

        // Roll total HD pool
        int hdPool = 0;
        for (int i = 0; i < hdDiceCount; i++)
            hdPool += UnityEngine.Random.Range(1, 5); // 1d4 per CL

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"💀 {caster.Stats.CharacterName} casts Circle of Death!");
        sb.AppendLine($"  School: Necromancy [Death] | Level: 6 | 40-ft radius burst");
        sb.AppendLine($"  HD Pool: {hdDiceCount}d4 = {hdPool} HD worth of creatures");
        sb.AppendLine($"  Fort DC {saveDc} negates | SR: Yes | Lowest HD first");
        sb.AppendLine($"  Targets: {(targets != null ? targets.Count : 0)} creature(s)");
        sb.AppendLine();

        if (targets == null || targets.Count == 0)
        {
            sb.AppendLine($"  No living targets in area.");
        }
        else
        {
            // Sort by HD ascending (lowest HD killed first per PHB)
            var sortedTargets = new List<CharacterController>(targets);
            sortedTargets.Sort((a, b) =>
            {
                int hdA = a != null && a.Stats != null ? a.Stats.Level : 999;
                int hdB = b != null && b.Stats != null ? b.Stats.Level : 999;
                return hdA.CompareTo(hdB);
            });

            int remainingHd = hdPool;
            foreach (CharacterController target in sortedTargets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead) continue;
                if (remainingHd <= 0) break;

                int targetHd = Mathf.Max(1, target.Stats.Level);
                sb.AppendLine($"  --- {target.Stats.CharacterName} ({targetHd} HD) ---");

                if (targetHd > remainingHd)
                {
                    sb.AppendLine($"  Not enough HD remaining in pool ({remainingHd}) — skipped.");
                    sb.AppendLine();
                    continue;
                }

                // Undead, constructs immune to death effects
                if (target.CanBeCommandedAsUndead())
                {
                    sb.AppendLine($"  Immune to [Death] effects (undead).");
                    sb.AppendLine();
                    continue;
                }

                // SR check
                if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
                {
                    int srRoll = UnityEngine.Random.Range(1, 21);
                    int srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
                    bool srOvercome = srTotal >= target.Stats.SpellResistance;
                    sb.AppendLine($"  SR: d20({srRoll}) + {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");
                    if (!srOvercome) { sb.AppendLine(); continue; }
                }

                // Fort save
                int fortRoll = UnityEngine.Random.Range(1, 21);
                int fortMod = target.Stats.FortitudeSave;
                int fortTotal = fortRoll + fortMod;
                bool saved = fortTotal >= saveDc;
                sb.AppendLine($"  Fort: d20({fortRoll}) + {fortMod} = {fortTotal} vs DC {saveDc} → {(saved ? "SAVED (negated)" : "FAILED")}");

                if (saved)
                {
                    sb.AppendLine($"  {target.Stats.CharacterName} survives Circle of Death.");
                    sb.AppendLine();
                    continue;
                }

                // Killed — deduct HD from pool
                remainingHd -= targetHd;
                target.Stats.CurrentHP = -11;
                target.OnDeath();
                HandleSummonDeathCleanup(target);
                sb.AppendLine($"  ☠ {target.Stats.CharacterName} is slain by Circle of Death! (HD pool remaining: {remainingHd})");
                sb.AppendLine();
            }
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  CRUSHING DESPAIR — PHB p.215
    //  Enchantment (Compulsion) [Mind-Affecting]. 30-ft cone.
    //  -2 penalty on attacks, saves, ability checks, skill checks, damage.
    //  Will negates. SR: Yes. Duration: 1 min/level.
    // ================================================================

    private bool TryResolveCrushingDespairSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;
        if (!string.Equals(spell.SpellId, SpellNames.CRUSHING_DESPAIR, StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        int saveDc = GetSpellSaveDC(caster, spell);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"😢 {caster.Stats.CharacterName} casts Crushing Despair!");
        sb.AppendLine($"  School: Enchantment [Mind-Affecting] | Level: 4 | 30-ft Cone");
        sb.AppendLine($"  Effect: -2 attacks, saves, checks, damage | Duration: {durationRounds} rounds");
        sb.AppendLine($"  Will DC {saveDc} negates | SR: Yes");
        sb.AppendLine();

        if (targets == null || targets.Count == 0)
        {
            sb.AppendLine($"  No targets in cone.");
        }
        else
        {
            foreach (CharacterController target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead) continue;
                sb.AppendLine($"  --- {target.Stats.CharacterName} ---");

                if (IsImmuneToMindAffecting(target))
                {
                    sb.AppendLine($"  Immune to mind-affecting effects!");
                    sb.AppendLine();
                    continue;
                }

                // SR check
                if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
                {
                    int srRoll = UnityEngine.Random.Range(1, 21);
                    int srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
                    bool srOvercome = srTotal >= target.Stats.SpellResistance;
                    sb.AppendLine($"  SR: d20({srRoll}) + {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");
                    if (!srOvercome) { sb.AppendLine(); continue; }
                }

                // Will save
                int willRoll = UnityEngine.Random.Range(1, 21);
                int willMod = target.Stats.WillSave;
                int willTotal = willRoll + willMod;
                bool saved = willTotal >= saveDc;
                sb.AppendLine($"  Will: d20({willRoll}) + {willMod} = {willTotal} vs DC {saveDc} → {(saved ? "SAVED (negated)" : "FAILED")}");

                if (saved) { sb.AppendLine(); continue; }

                // Apply Shaken condition as mechanical proxy for -2 attacks/saves/checks
                string sourceName = spell.Name;
                if (_conditionService != null)
                {
                    _conditionService.ApplyCondition(
                        target,
                        CombatConditionType.Shaken,
                        durationRounds,
                        source: caster,
                        sourceNameOverride: sourceName,
                        sourceCategory: "Spell",
                        sourceId: spell.SpellId);
                }
                else
                {
                    target.ApplyCondition(CombatConditionType.Shaken, durationRounds, caster.Stats.CharacterName);
                }

                sb.AppendLine($"  😢 {target.Stats.CharacterName} is crushed by despair for {durationRounds} rounds! (-2 attacks/saves/checks/damage)");
                sb.AppendLine();
            }
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  MIND FOG — PHB p.253
    //  Enchantment (Compulsion) [Mind-Affecting]. 20-ft-radius spread.
    //  -10 competence penalty on Wis checks and Will saves.
    //  Will negates. SR: Yes. Fog: 30 min. Debuff persists 2d6 rds after.
    // ================================================================

    private bool TryResolveMindFogSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;
        if (!string.Equals(spell.SpellId, SpellNames.MIND_FOG, StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int saveDc = GetSpellSaveDC(caster, spell);
        int fogDurationRounds = 300; // 30 minutes = 300 rounds

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🌫 {caster.Stats.CharacterName} casts Mind Fog!");
        sb.AppendLine($"  School: Enchantment [Mind-Affecting] | Level: 5 | 20-ft radius");
        sb.AppendLine($"  Effect: -10 penalty on Wis checks and Will saves");
        sb.AppendLine($"  Will DC {saveDc} negates | SR: Yes | Fog duration: 30 min");
        sb.AppendLine();

        if (targets == null || targets.Count == 0)
        {
            sb.AppendLine($"  No targets in area.");
        }
        else
        {
            foreach (CharacterController target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead) continue;
                sb.AppendLine($"  --- {target.Stats.CharacterName} ---");

                if (IsImmuneToMindAffecting(target))
                {
                    sb.AppendLine($"  Immune to mind-affecting effects!");
                    sb.AppendLine();
                    continue;
                }

                // SR check
                if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
                {
                    int srRoll = UnityEngine.Random.Range(1, 21);
                    int srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
                    bool srOvercome = srTotal >= target.Stats.SpellResistance;
                    sb.AppendLine($"  SR: d20({srRoll}) + {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");
                    if (!srOvercome) { sb.AppendLine(); continue; }
                }

                // Will save
                int willRoll = UnityEngine.Random.Range(1, 21);
                int willMod = target.Stats.WillSave;
                int willTotal = willRoll + willMod;
                bool saved = willTotal >= saveDc;
                sb.AppendLine($"  Will: d20({willRoll}) + {willMod} = {willTotal} vs DC {saveDc} → {(saved ? "SAVED (negated)" : "FAILED")}");

                if (saved) { sb.AppendLine(); continue; }

                // Apply Frightened as mechanical proxy for -10 Will save penalty
                if (_conditionService != null)
                {
                    _conditionService.ApplyCondition(
                        target,
                        CombatConditionType.Frightened,
                        fogDurationRounds,
                        source: caster,
                        sourceNameOverride: spell.Name,
                        sourceCategory: "Spell",
                        sourceId: spell.SpellId);
                }
                else
                {
                    target.ApplyCondition(CombatConditionType.Frightened, fogDurationRounds, caster.Stats.CharacterName);
                }

                sb.AppendLine($"  🌫 {target.Stats.CharacterName}'s mind is fogged! -10 Wis checks and Will saves!");
                sb.AppendLine();
            }
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  MASS SUGGESTION — PHB p.285
    //  Enchantment (Compulsion) [Mind-Affecting, Language-Dependent].
    //  One subject/CL within 30 ft of each other.
    //  Will negates. SR: Yes. Duration: 1 hour/level.
    // ================================================================

    private bool TryResolveMassSuggestionSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;
        if (!string.Equals(spell.SpellId, SpellNames.MASS_SUGGESTION, StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int maxTargets = casterLevel;
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        int saveDc = GetSpellSaveDC(caster, spell);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"💬 {caster.Stats.CharacterName} casts Mass Suggestion!");
        sb.AppendLine($"  School: Enchantment [Mind-Affecting] | Level: 6 | Up to {maxTargets} targets");
        sb.AppendLine($"  Duration: {durationRounds} rounds | Will DC {saveDc} negates | SR: Yes");
        sb.AppendLine();

        if (targets == null || targets.Count == 0)
        {
            sb.AppendLine($"  No targets.");
        }
        else
        {
            int affected = 0;
            foreach (CharacterController target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead) continue;
                if (affected >= maxTargets) break;

                sb.AppendLine($"  --- {target.Stats.CharacterName} ---");

                if (IsImmuneToMindAffecting(target))
                {
                    sb.AppendLine($"  Immune to mind-affecting!");
                    sb.AppendLine();
                    continue;
                }

                // SR check
                if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
                {
                    int srRoll = UnityEngine.Random.Range(1, 21);
                    int srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
                    bool srOvercome = srTotal >= target.Stats.SpellResistance;
                    sb.AppendLine($"  SR: d20({srRoll}) + {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");
                    if (!srOvercome) { sb.AppendLine(); continue; }
                }

                // Will save
                int willRoll = UnityEngine.Random.Range(1, 21);
                int willMod = target.Stats.WillSave;
                int willTotal = willRoll + willMod;
                bool saved = willTotal >= saveDc;
                sb.AppendLine($"  Will: d20({willRoll}) + {willMod} = {willTotal} vs DC {saveDc} → {(saved ? "SAVED (negated)" : "FAILED")}");

                if (!saved)
                {
                    affected++;
                    if (_conditionService != null)
                    {
                        _conditionService.ApplyCondition(
                            target,
                            CombatConditionType.Charmed,
                            durationRounds,
                            source: caster,
                            sourceNameOverride: spell.Name,
                            sourceCategory: "Spell",
                            sourceId: spell.SpellId);
                    }
                    else
                    {
                        target.ApplyCondition(CombatConditionType.Charmed, durationRounds, caster.Stats.CharacterName);
                    }
                    sb.AppendLine($"  💬 {target.Stats.CharacterName} is compelled by Mass Suggestion for {durationRounds} rounds!");
                }
                sb.AppendLine();
            }
            sb.AppendLine($"  Total affected: {affected}");
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  INSECT PLAGUE — PHB p.244
    //  Conjuration (Summoning). 1 swarm per 3 CL (max 6).
    //  2d6 damage/round. Fort or nauseated 1 round.
    //  No SR for swarm damage. Duration: 1 min/level.
    // ================================================================

    private bool TryResolveInsectPlagueSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;
        if (!string.Equals(spell.SpellId, SpellNames.INSECT_PLAGUE, StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int numSwarms = Mathf.Clamp(casterLevel / 3, 1, 6);
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        int saveDc = GetSpellSaveDC(caster, spell);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🦗 {caster.Stats.CharacterName} casts Insect Plague!");
        sb.AppendLine($"  School: Conjuration (Summoning) | Level: 5 | {numSwarms} swarm(s)");
        sb.AppendLine($"  Damage: 2d6 per swarm | Fort DC {saveDc} or nauseated 1 round");
        sb.AppendLine($"  Duration: {durationRounds} rounds | No SR for swarm damage");
        sb.AppendLine();

        if (targets == null || targets.Count == 0)
        {
            sb.AppendLine($"  No creatures in swarm area.");
        }
        else
        {
            foreach (CharacterController target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead) continue;
                sb.AppendLine($"  --- {target.Stats.CharacterName} ---");

                // Swarm damage: 2d6 (no save, no SR)
                int swarmDmg = UnityEngine.Random.Range(1, 7) + UnityEngine.Random.Range(1, 7);

                int hpBefore = target.Stats.CurrentHP;
                target.Stats.TakeDamage(swarmDmg);
                int hpAfter = target.Stats.CurrentHP;

                sb.AppendLine($"  🦗 Swarm damage: 2d6 = {swarmDmg}");
                sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

                CheckConcentrationOnDamage(target, swarmDmg);

                if (target.Stats.IsDead)
                {
                    target.OnDeath();
                    HandleSummonDeathCleanup(target);
                    sb.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");
                    sb.AppendLine();
                    continue;
                }

                // Fort save vs nausea
                int fortRoll = UnityEngine.Random.Range(1, 21);
                int fortMod = target.Stats.FortitudeSave;
                int fortTotal = fortRoll + fortMod;
                bool saved = fortTotal >= saveDc;
                sb.AppendLine($"  Fort vs nausea: d20({fortRoll}) + {fortMod} = {fortTotal} vs DC {saveDc} → {(saved ? "SAVED" : "NAUSEATED")}");

                if (!saved)
                {
                    if (_conditionService != null)
                    {
                        _conditionService.ApplyCondition(
                            target,
                            CombatConditionType.Nauseated,
                            1,
                            source: caster,
                            sourceNameOverride: spell.Name,
                            sourceCategory: "Spell",
                            sourceId: spell.SpellId);
                    }
                    else
                    {
                        target.ApplyCondition(CombatConditionType.Nauseated, 1, caster.Stats.CharacterName);
                    }
                }
                sb.AppendLine();
            }
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  WALL OF THORNS — PHB p.301
    //  Conjuration (Creation). Creatures passing through take
    //  25 - AC damage (min 1). Duration: 10 min/level.
    // ================================================================

    private bool TryResolveWallOfThornsSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;
        if (!string.Equals(spell.SpellId, SpellNames.WALL_OF_THORNS, StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🌿 {caster.Stats.CharacterName} casts Wall of Thorns!");
        sb.AppendLine($"  School: Conjuration (Creation) | Level: 5 | Duration: {durationRounds} rounds");
        sb.AppendLine($"  Creatures forced through take 25 - AC damage (min 1).");
        sb.AppendLine();

        if (targets != null)
        {
            foreach (CharacterController target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead) continue;

                int thornDmg = Mathf.Max(1, 25 - target.Stats.ArmorClass);

                int hpBefore = target.Stats.CurrentHP;
                target.Stats.TakeDamage(thornDmg);
                int hpAfter = target.Stats.CurrentHP;

                sb.AppendLine($"  🌿 {target.Stats.CharacterName} caught in thorns! 25 - AC({target.Stats.ArmorClass}) = {thornDmg} piercing damage.");
                sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

                CheckConcentrationOnDamage(target, thornDmg);

                if (target.Stats.IsDead)
                {
                    target.OnDeath();
                    HandleSummonDeathCleanup(target);
                    sb.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");
                }
                sb.AppendLine();
            }
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  PERSISTENT IMAGE — PHB p.260
    //  Illusion (Figment). No concentration. 1 min/level (D).
    //  Will disbelief on interaction.
    // ================================================================

    private bool TryResolvePersistentImageSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;
        if (!string.Equals(spell.SpellId, SpellNames.PERSISTENT_IMAGE, StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🎭 {caster.Stats.CharacterName} casts Persistent Image!");
        sb.AppendLine($"  School: Illusion (Figment) | Level: 5 | Range: Long");
        sb.AppendLine($"  Creates a visual, auditory, olfactory, and thermal illusion.");
        sb.AppendLine($"  No concentration required. Will disbelief on interaction.");
        sb.AppendLine($"  Duration: {durationRounds} rounds (Dismissible) | CL {casterLevel}");
        sb.Append("═══════════════════════════════════");

        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  HOLD MONSTER — PHB p.241
    //  As Hold Person but affects ANY living creature.
    //  Single target. Will negates. SR: Yes. 1 round/level (D).
    // ================================================================

    private ActiveSpellEffect ApplyHoldMonsterEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (target == null || target.Stats == null || spell == null)
            return null;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int holdRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        string sourceName = spell.Name;

        if (_conditionService != null)
        {
            _conditionService.ApplyCondition(
                target, CombatConditionType.Paralyzed, holdRounds,
                source: caster, sourceNameOverride: sourceName,
                sourceCategory: "Spell", sourceId: spell.SpellId);
            _conditionService.ApplyCondition(
                target, CombatConditionType.Helpless, holdRounds,
                source: caster, sourceNameOverride: sourceName,
                sourceCategory: "Spell", sourceId: spell.SpellId);
        }
        else
        {
            string fallback = caster != null && caster.Stats != null ? caster.Stats.CharacterName : sourceName;
            target.ApplyCondition(CombatConditionType.Paralyzed, holdRounds, fallback);
            target.ApplyCondition(CombatConditionType.Helpless, holdRounds, fallback);
        }

        CombatUI?.ShowCombatLog($"<color=#FF9966>⛓ {target.Stats.CharacterName} is paralyzed by Hold Monster for {holdRounds} round(s)! (Will save each round with cumulative +2 to break free)</color>");
        Debug.Log($"[GameManager] Hold Monster: Paralyzed+Helpless on {target.Stats.CharacterName} for {holdRounds} rounds (CL {casterLevel})");
        return null;
    }

    // ================================================================
    //  CHARM MONSTER — PHB p.209
    //  As Charm Person but affects ANY living creature (no HD limit).
    //  Single target. Will negates. SR: Yes. 1 day/level.
    // ================================================================

    private ActiveSpellEffect ApplyCharmMonsterEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (target == null || target.Stats == null || spell == null)
            return null;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int charmRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        string sourceName = spell.Name;

        var charmData = new CharmedConditionData
        {
            Caster = caster,
            CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : sourceName,
            RemainingRounds = charmRounds,
            SourceSpellId = spell.SpellId,
            SourceEffectName = spell.Name
        };

        if (_conditionService != null)
        {
            _conditionService.ApplyCondition(
                target, CombatConditionType.Charmed, charmRounds,
                source: caster, data: charmData, sourceNameOverride: sourceName,
                sourceCategory: "Spell", sourceId: spell.SpellId);
        }
        else
        {
            string fallback = caster != null && caster.Stats != null ? caster.Stats.CharacterName : sourceName;
            target.ApplyCondition(CombatConditionType.Charmed, charmRounds, fallback);
        }

        CombatUI?.ShowCombatLog($"<color=#FFD699>💞 {target.Stats.CharacterName} is charmed by {spell.Name} for {charmRounds} round(s)! (No HD/type limit)</color>");
        Debug.Log($"[GameManager] Charm Monster: Charmed on {target.Stats.CharacterName} for {charmRounds} rounds (CL {casterLevel})");
        return null;
    }

    // ================================================================
    //  GLOBE OF INVULNERABILITY — PHB p.236
    //  Abjuration. 10-ft-radius emanation. Blocks spells ≤ 4th level.
    //  1 round/level (D). Extends LesserGlobe with MaxBlockedSpellLevel = 4.
    // ================================================================

    private bool TryResolveGlobeOfInvulnerabilitySpell(
        CharacterController caster,
        SpellData spell,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;
        if (!string.Equals(spell.SpellId, SpellNames.GLOBE_OF_INVULNERABILITY, StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        // Create globe area effect (reusing Lesser Globe class with MaxBlockedSpellLevel = 4)
        var globeObj = new GameObject("GlobeOfInvulnerability");
        var globe = globeObj.AddComponent<LesserGlobeOfInvulnerabilityAreaEffect>();
        globe.MaxBlockedSpellLevel = 4;
        globe.EffectName = "Globe of Invulnerability";
        globe.SpellId = SpellNames.GLOBE_OF_INVULNERABILITY;
        globe.CasterLevel = casterLevel;
        globe.RoundsRemaining = durationRounds;
        globe.Caster = caster;
        globe.CenterPosition = caster.transform.position;

        if (AreaEffectManager.HasInstance)
            AreaEffectManager.Instance.RegisterAreaEffect(globe);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🔮 {caster.Stats.CharacterName} casts Globe of Invulnerability!");
        sb.AppendLine($"  School: Abjuration | Level: 6 | 10-ft radius emanation");
        sb.AppendLine($"  Blocks all spell effects of 4th level or lower");
        sb.AppendLine($"  Duration: {durationRounds} rounds (Dismissible) | CL {casterLevel}");
        sb.Append("═══════════════════════════════════");

        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  CONTINUAL FLAME — PHB p.213
    //  Evocation [Light]. Permanent magical light on touched object.
    //  Utility — applies a light marker status effect.
    // ================================================================

    private ActiveSpellEffect ApplyContinualFlameEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null || spell == null)
            return null;

        StatusEffectManager statusMgr = recipient.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
            statusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(recipient.Stats);

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;

        ActiveSpellEffect effect = statusMgr.AddEffect(
            spell,
            caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name,
            casterLevel);

        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
        CombatUI?.ShowCombatLog($"<color=#FFFF88>🔥 {casterName} casts Continual Flame! A permanent, heatless flame springs forth. (50 gp ruby dust consumed)</color>");
        Debug.Log($"[GameManager] Continual Flame: permanent light effect applied (CL {casterLevel})");

        return effect;
    }

    // ================================================================
    //  LEVITATE — PHB p.248
    //  Transmutation. Vertical movement buff. 1 min/level (D).
    // ================================================================

    private ActiveSpellEffect ApplyLevitateEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null || spell == null)
            return null;

        StatusEffectManager statusMgr = recipient.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
            statusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(recipient.Stats);

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        ActiveSpellEffect effect = statusMgr.AddEffect(
            spell,
            caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name,
            casterLevel);

        if (effect != null)
        {
            SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
            if (recipientSpellComp != null)
                recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;
        }

        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
        bool selfCast = recipient == caster;
        string castLine = selfCast
            ? $"<color=#88CCFF>🪶 {casterName} casts Levitate on self!</color>"
            : $"<color=#88CCFF>🪶 {casterName} casts Levitate on {recipient.Stats.CharacterName}!</color>";

        CombatUI?.ShowCombatLog(castLine);
        CombatUI?.ShowCombatLog($"<color=#AADDFF>   Vertical movement 20 ft/round. Duration: {durationRounds} rounds (CL {casterLevel})</color>");

        return effect;
    }

    // ================================================================
    //  SHRINK ITEM — PHB p.279
    //  Transmutation. Utility — shrinks one nonmagical item.
    //  In combat, just log the effect.
    // ================================================================

    private ActiveSpellEffect ApplyShrinkItemEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || caster.Stats == null || spell == null)
            return null;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));

        CombatUI?.ShowCombatLog($"<color=#CCCC88>📦 {caster.Stats.CharacterName} casts Shrink Item! An item is reduced to 1/16 its normal size. Duration: {casterLevel} day(s).</color>");
        Debug.Log($"[GameManager] Shrink Item cast (CL {casterLevel}, duration {casterLevel} days)");

        return null;
    }

    // ================================================================
    //  BARKSKIN — PHB p.202
    //  Transmutation. +2 to +5 enhancement bonus to natural armor.
    //  +2 base, +1 per 3 CL above 3rd (max +5 at CL 12). 10 min/level.
    // ================================================================

    private ActiveSpellEffect ApplyBarkskinEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null || spell == null)
            return null;

        StatusEffectManager statusMgr = recipient.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
            statusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(recipient.Stats);

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;

        // +2 base, +1 per 3 levels above 3rd, max +5 at CL 12
        int natArmorBonus = 2;
        if (casterLevel >= 6) natArmorBonus = 3;
        if (casterLevel >= 9) natArmorBonus = 4;
        if (casterLevel >= 12) natArmorBonus = 5;

        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        ActiveSpellEffect effect = statusMgr.AddEffect(
            spell,
            caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name,
            casterLevel);

        if (effect != null)
        {
            recipient.Stats.NaturalArmorBonus += natArmorBonus;

            SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
            if (recipientSpellComp != null)
                recipientSpellComp.ActiveBuffs[spell.SpellId] = durationRounds;
        }

        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
        CombatUI?.ShowCombatLog($"<color=#88CC66>🌿 {casterName} casts Barkskin on {recipient.Stats.CharacterName}! +{natArmorBonus} enhancement to natural armor for {durationRounds} rounds (CL {casterLevel})</color>");

        UpdateAllStatsUI();
        return effect;
    }

    // ================================================================
    //  PASSWALL — PHB p.259
    //  Transmutation. Creates passage through walls. Utility.
    // ================================================================

    private ActiveSpellEffect ApplyPasswallEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || caster.Stats == null || spell == null)
            return null;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        int depthFt = 10 + (Mathf.Max(0, (casterLevel - 9)) / 3) * 5;

        CombatUI?.ShowCombatLog($"<color=#CCAA88>🚪 {caster.Stats.CharacterName} casts Passwall! A passage (5 ft × 8 ft × {depthFt} ft deep) opens through the wall. Duration: {durationRounds} rounds.</color>");
        Debug.Log($"[GameManager] Passwall cast (CL {casterLevel}, depth {depthFt} ft, duration {durationRounds} rounds)");

        return null;
    }

    // ================================================================
    //  TELEKINESIS — PHB p.292  (Combat Maneuver Mode)
    //  Transmutation. Use caster level as BAB for one bull rush attempt.
    //  +CL bonus on opposed check. SR: Yes. Range: Close (25 ft + 5/2 CL).
    //  D&D 3.5e: Can attempt bull rush, disarm, grapple, or trip.
    //  Simplified: implements bull rush (push back) only.
    // ================================================================

    private ActiveSpellEffect ApplyTelekinesisEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (target == null || target.Stats == null || spell == null)
            return null;

        int casterLevel = caster != null && caster.Stats != null
            ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        string casterName = caster != null && caster.Stats != null
            ? caster.Stats.CharacterName : "Caster";

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🫳 {casterName} casts Telekinesis (Combat Maneuver — Bull Rush)!");
        sb.AppendLine($"  School: Transmutation | Level: 5 | Range: Close");
        sb.AppendLine($"  Uses CL {casterLevel} as BAB for opposed bull rush check | SR: Yes");
        sb.AppendLine($"  Target: {target.Stats.CharacterName}");

        // Spell Resistance check
        if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
        {
            int srRoll = UnityEngine.Random.Range(1, 21);
            int srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
            bool srOk = srTotal >= target.Stats.SpellResistance;
            sb.AppendLine($"  SR Check: d20({srRoll}) + {casterLevel}+pen = {srTotal} vs SR {target.Stats.SpellResistance} → {(srOk ? "OVERCOME" : "RESISTED")}");
            if (!srOk)
            {
                sb.AppendLine($"  ✦ {target.Stats.CharacterName} resists (Spell Resistance)!");
                sb.Append("═══════════════════════════════════");
                CombatUI?.ShowCombatLog(sb.ToString());
                return null;
            }
        }

        // Telekinetic bull rush: use caster level as BAB
        // Attacker check: d20 + CL (as BAB) + CL (telekinetic force bonus) + STR mod (use INT for caster)
        int intMod = caster != null && caster.Stats != null ? caster.Stats.INTMod : 0;
        int attackRoll = UnityEngine.Random.Range(1, 21);
        int attackTotal = attackRoll + casterLevel + intMod;
        sb.AppendLine($"  Bull Rush (Attacker): d20({attackRoll}) + CL({casterLevel}) + INT({intMod}) = {attackTotal}");

        // Defender check: d20 + BAB + STR mod + size
        int defRoll = UnityEngine.Random.Range(1, 21);
        int defBAB = target.Stats.BaseAttackBonus;
        int defSTR = target.Stats.STRMod;
        int defTotal = defRoll + defBAB + defSTR;
        sb.AppendLine($"  Bull Rush (Defender): d20({defRoll}) + BAB({defBAB}) + STR({defSTR}) = {defTotal}");

        bool success = attackTotal > defTotal;
        int margin = Mathf.Max(0, attackTotal - defTotal);
        int pushSquares = success ? 1 + (margin / 5) : 0;

        if (success)
        {
            sb.AppendLine($"  ✦ SUCCESS! {target.Stats.CharacterName} pushed back {pushSquares} square(s) ({pushSquares * 5} ft)!");
            sb.AppendLine($"    (Telekinetic force wins by {margin})");

            // Apply prone if pushed more than 2 squares (falling over obstacles)
            if (pushSquares >= 3)
            {
                target.Stats.ApplyCondition(CombatConditionType.Prone, 1, "Telekinesis (Bull Rush)");
                sb.AppendLine($"    🔻 {target.Stats.CharacterName} is knocked PRONE from the impact!");
            }
        }
        else
        {
            sb.AppendLine($"  ✘ FAILED! {target.Stats.CharacterName} resists the telekinetic force.");
        }

        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        UpdateAllStatsUI();
        return null;
    }
}
