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
    //  DISINTEGRATE — PHB p.222 (printed)
    //  Transmutation. Ranged touch ray, 2d6 per caster level (max 40d6).
    //  Fortitude partial: a creature that saves takes 5d6 instead. Any
    //  creature reduced to 0 or fewer hit points by the spell is entirely
    //  disintegrated (dead, a trace of fine dust). SR: Yes.
    //  The cast pipeline (SpellCaster.Cast) rolls the ranged touch attack,
    //  spell resistance and the save; this handler deals the damage
    //  (DamageResolvedByHandler) and reads the save from castResult (SPL-037).
    // ================================================================

    /// <summary>Disintegrate's damage dice at a caster level (PHB p.222): 2d6 per level, max 40d6; 5d6 after a successful save.</summary>
    public static int DisintegrateDiceCount(int casterLevel, bool saved)
    {
        return saved ? 5 : Mathf.Clamp(casterLevel * 2, 2, 40);
    }

    /// <summary>
    /// Rolls <paramref name="diceCount"/>d6 of Disintegrate damage with the cast's metamagic, through the same dice
    /// rule as SpellCaster.Cast (SpellDiceRules.Roll): Maximize sets every die to 6 (PHB p.97), Empower adds half the
    /// total (PHB p.93), as SpellCaster does.
    /// </summary>
    public static int RollDisintegrateDamage(int diceCount, bool maximized, bool empowered, out int empowerBonus)
    {
        var dice = new SpellDice { Count = diceCount, Sides = 6 };
        int[] rolls = SpellDiceRules.Roll(dice, maximized, SpellDiceRules.DamageDieContext);
        int damage = 0;
        for (int i = 0; i < rolls.Length; i++)
            damage += rolls[i];
        empowerBonus = empowered ? Mathf.RoundToInt(damage * 0.5f) : 0;
        return damage + empowerBonus;
    }

    private ActiveSpellEffect ApplyDisintegrateEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp,
        SpellResult castResult = null)
    {
        if (caster == null || target == null || target.Stats == null || spell == null) return null;

        // The cast's caster level when there is a cast (SpellCaster.Cast records it), else the caster's.
        int casterLevel = castResult != null && castResult.CasterLevel > 0
            ? castResult.CasterLevel
            : SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int saveDc = GetSpellSaveDC(caster, spell);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"💀 {caster.Stats.CharacterName} casts Disintegrate!");
        sb.AppendLine($"  School: Transmutation | Level: 6 | Ranged Touch Ray");
        sb.AppendLine($"  Fort DC {saveDc} partial (5d6) | SR: Yes");
        sb.AppendLine($"  Target: {target.Stats.CharacterName}");

        bool saved;
        if (castResult != null)
        {
            // The ray hit, spell resistance was overcome and the save was rolled by SpellCaster.Cast.
            saved = castResult.RequiredSave && castResult.SaveSucceeded;
            if (castResult.RequiredSave)
                sb.AppendLine($"  Fortitude save: {castResult.SaveTotal} vs DC {castResult.SaveDC} — {(saved ? "SAVED" : "FAILED")}");
        }
        else
        {
            var srResult = SpellSaveResolver.RollSpellResistance(caster, target, casterLevel);
            srResult.AppendToLog(sb);
            if (!srResult.Overcame)
            {
                sb.AppendLine($"  ✦ {target.Stats.CharacterName} resists (Spell Resistance)!");
                sb.Append("═══════════════════════════════════");
                CombatUI?.ShowCombatLog(sb.ToString());
                return null;
            }

            var saveResult = SpellSaveResolver.RollSave(target, SaveType.Fortitude, saveDc, SaveContext.ForSpell(spell, caster != null ? caster.Stats : null));
            saved = saveResult.Saved;
            saveResult.AppendToLog(sb, "SAVED", "FAILED");
        }

        // Metamagic from the cast (PHB p.93 Empower: variable numeric effects x1.5; p.97 Maximize: dice at their
        // maximum), applied as SpellCaster.Cast applies it to every other damage spell.
        MetamagicData metamagic = castResult?.Metamagic;
        bool maximized = metamagic != null && metamagic.Has(MetamagicFeatId.MaximizeSpell);
        bool empowered = metamagic != null && metamagic.Has(MetamagicFeatId.EmpowerSpell);
        int diceCount = DisintegrateDiceCount(casterLevel, saved);
        int damage = RollDisintegrateDamage(diceCount, maximized, empowered, out int empowerBonus);
        if (castResult != null)
            castResult.EmpowerBonus = empowerBonus;
        string metamagicNote = (maximized ? " maximized" : "") + (empowered ? $", empowered +{empowerBonus}" : "");
        sb.AppendLine(saved
            ? $"  Partial: 5d6{metamagicNote} = {damage} damage (Fort save succeeded)"
            : $"  {diceCount}d6{metamagicNote} = {damage} damage!");

        int hpBefore = target.Stats.CurrentHP;
        DamageResolutionResult dealt = ApplyDamagePacket(target, damage, DamagePackets.Spell(spell.Name, DamageType.Untyped, false, true));
        if (dealt.FinalDamage != damage)
            sb.AppendLine($"  Damage taken: {dealt.FinalDamage}{DescribeMitigation(dealt)}");

        // PHB p.222: a creature reduced to 0 or fewer hit points by this spell is entirely disintegrated.
        bool disintegrated = dealt.FinalDamage > 0 && target.Stats.CurrentHP <= 0;
        if (disintegrated && !target.Stats.IsDead)
            target.Stats.CurrentHP = Mathf.Min(target.Stats.CurrentHP, -10);
        sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {target.Stats.CurrentHP} HP");
        if (disintegrated)
            sb.AppendLine($"  💀 {target.Stats.CharacterName} is reduced to fine dust!");

        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());

        // Concentration check and death handling (OnDeath, summon cleanup); the pipelines' combat-end check reads
        // CombatEndRules.IsOutOfFight(target).
        AfterDamageTaken(target, dealt.FinalDamage);
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
        int saveDc = GetSpellSaveDC(caster, spell);

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
                bool isUndead = SpellTargetingService.IsUndead(target);
                int diceCount = isUndead ? Mathf.Clamp(casterLevel, 1, 25) : 6;

                int damage = 0;
                for (int i = 0; i < diceCount; i++)
                    damage += DiceRoller.D6();
                sb.AppendLine($"    Damage roll: {diceCount}d6 = {damage}{(isUndead ? " (Undead)" : "")}");

                // Reflex save + Evasion
                var saveResult = SpellSaveResolver.RollSave(target, SaveType.Reflex, saveDc, SaveContext.ForSpell(spell, caster != null ? caster.Stats : null));
                saveResult.AppendHalfDamageLog(sb);
                if (saveResult.Saved)
                {
                    damage = Mathf.Max(1, damage / 2);
                    damage = SpellSaveResolver.ApplyEvasion(damage, target, true, sb);
                }

                if (damage > 0)
                {
                    int hpBefore = target.Stats.CurrentHP;
                    DamageResolutionResult dealt = DealDamage(target, damage, DamagePackets.Spell(spell.Name, DamageType.Untyped, saveResult.Saved));
                    sb.AppendLine($"    {target.Stats.CharacterName}: {hpBefore} → {target.Stats.CurrentHP} HP{DescribeMitigation(dealt)}");
                }

                // Blindness on failed save (permanent)
                if (!saveResult.Saved)
                {
                    target.Stats.ApplyCondition(CombatConditionType.Blinded, 9999, "Sunburst");
                    sb.AppendLine($"    👁 BLINDED permanently!");
                }

                if (target.Stats.IsDead)
                    sb.AppendLine($"    💀 {target.Stats.CharacterName} has been destroyed!");

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
        int saveDc = GetSpellSaveDC(caster, spell);

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

                var saveResult = SpellSaveResolver.RollSave(target, SaveType.Reflex, saveDc, SaveContext.ForSpell(spell, caster != null ? caster.Stats : null));
                saveResult.AppendToLog(sb, "SAVED", "FAILED");

                if (!saveResult.Saved)
                {
                    target.Stats.ApplyCondition(CombatConditionType.Prone, 1, "Earthquake");
                    sb.AppendLine($"    🔻 Knocked PRONE!");

                    // Debris damage: simplified (structures collapse for 8d6 in PHB, minor outdoors)
                    int debrisDmg = DiceRoller.D6();
                    DamageResolutionResult dealt = DealDamage(target, debrisDmg, DamagePackets.Spell(spell.Name, DamageType.Bludgeoning));
                    sb.AppendLine($"    Debris: {dealt.FinalDamage} bludgeoning damage{DescribeMitigation(dealt)}");

                    if (target.Stats.IsDead)
                        sb.AppendLine($"    💀 {target.Stats.CharacterName} crushed!");
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
                    statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
                statusMgr.Init(target.Stats); // always rebind to the current stats, as the generic branch does

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
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(target.Stats); // always rebind to the current stats, as the generic branch does

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
        return effect;
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
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(target.Stats); // always rebind to the current stats, as the generic branch does

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
    //  HEAL — PHB p.239 (printed)
    //  Conjuration (Healing). Touch. Cures 10 points of damage per caster
    //  level (max 150 at 15th) and ends ability damage, blinded, confused,
    //  dazed, dazzled, deafened, diseased, exhausted, fatigued, feebleminded,
    //  insanity, nauseated, sickened, stunned and poisoned. It does not
    //  remove negative levels or drained levels or ability points.
    //  Against an undead it acts like harm: 10 per level (max 150), Will
    //  half; a successful save cannot reduce it below 1 hp. No effect on a
    //  construct (positive energy cures only the living, as for the cures;
    //  SpellDiceRules.OutcomeFor, SPL-005).
    //  Heal is positive energy (SpellEnergy.Positive): SpellCaster.Cast
    //  rolls the touch attack, spell resistance and Will save against an
    //  undead, and this handler deals the damage or does the healing
    //  (DamageResolvedByHandler, HealingResolvedByHandler; SPL-022, SPL-037).
    // ================================================================

    /// <summary>Heal's hit points cured, or harm damage dealt, at a caster level (PHB p.239): 10 per level, max 150.</summary>
    public static int HealAmount(int casterLevel)
    {
        return Mathf.Clamp(casterLevel * 10, 0, 150);
    }

    /// <summary>The conditions Heal ends (PHB p.239) that the game tracks as conditions.</summary>
    public static readonly CombatConditionType[] HealCuredConditions =
    {
        CombatConditionType.Blinded,
        CombatConditionType.Confused,
        CombatConditionType.Dazed,
        CombatConditionType.Dazzled,
        CombatConditionType.Deafened,
        CombatConditionType.Exhausted,
        CombatConditionType.Fatigued,
        CombatConditionType.Nauseated,
        CombatConditionType.Sickened,
        CombatConditionType.Stunned,
        CombatConditionType.Poisoned,
    };

    private ActiveSpellEffect ApplyHealSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp,
        SpellResult castResult = null)
    {
        if (caster == null || target == null || target.Stats == null || spell == null) return null;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int amount = HealAmount(casterLevel);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"💚 {caster.Stats.CharacterName} casts Heal!");
        sb.AppendLine($"  School: Conjuration (Healing) | Level: 6 | Touch");
        sb.AppendLine($"  Target: {target.Stats.CharacterName}");

        bool isUndead = SpellTargetingService.IsUndead(target);
        if (!isUndead && SpellTargetingService.IsConstruct(target))
        {
            sb.AppendLine($"  ✘ No effect: positive energy cures only living creatures.");
            sb.Append("═══════════════════════════════════");
            CombatUI?.ShowCombatLog(sb.ToString());
            return null;
        }

        if (isUndead)
        {
            // Acts like harm (PHB p.239): Will half, and a successful save cannot take the undead below 1 hp.
            bool saved;
            if (castResult != null)
            {
                saved = castResult.RequiredSave && castResult.SaveSucceeded;
                if (castResult.RequiredSave)
                    sb.AppendLine($"  Will save: {castResult.SaveTotal} vs DC {castResult.SaveDC} — {(saved ? "SAVED (half)" : "FAILED")}");
            }
            else
            {
                var saveResult = SpellSaveResolver.RollSave(target, SaveType.Will, GetSpellSaveDC(caster, spell), SaveContext.ForSpell(spell, caster != null ? caster.Stats : null));
                saveResult.AppendHalfDamageLog(sb);
                saved = saveResult.Saved;
            }

            int damage = saved ? amount / 2 : amount;
            if (saved)
                damage = Mathf.Min(damage, Mathf.Max(0, target.Stats.CurrentHP - 1));
            sb.AppendLine($"  ☠ Undead — positive energy acts like harm: {damage} damage{(saved ? " (half, not below 1 hp)" : "")}");

            int hpBefore = target.Stats.CurrentHP;
            DamageResolutionResult dealt = ApplyDamagePacket(target, damage, DamagePackets.Spell(spell.Name, DamageType.Positive));
            sb.AppendLine($"  Damage: {dealt.FinalDamage}{DescribeMitigation(dealt)} | {target.Stats.CharacterName}: {hpBefore} → {target.Stats.CurrentHP} HP");
            if (target.Stats.IsDead)
                sb.AppendLine($"  💀 {target.Stats.CharacterName} destroyed by positive energy!");

            sb.Append("═══════════════════════════════════");
            CombatUI?.ShowCombatLog(sb.ToString());
            AfterDamageTaken(target, dealt.FinalDamage);
            return null;
        }

        int before = target.Stats.CurrentHP;
        int actualHealed = target.Stats.HealDamage(amount, out int nonlethalHealed);
        sb.AppendLine($"  Heals: {amount} HP (CL {casterLevel}; actual: {actualHealed}{(nonlethalHealed > 0 ? $", {nonlethalHealed} nonlethal removed" : "")})");
        sb.AppendLine($"  {target.Stats.CharacterName}: {before} → {target.Stats.CurrentHP} HP");

        var curedList = new List<string>();
        foreach (var cond in HealCuredConditions)
        {
            if (target.RemoveCondition(cond))
                curedList.Add(cond.ToString());
        }

        if (target.ActivePoisons != null && target.ActivePoisons.Count > 0)
        {
            target.ActivePoisons.Clear();
            curedList.Add("poison");
        }

        if (target.ActiveDiseases != null && target.ActiveDiseases.Count > 0)
        {
            target.ActiveDiseases.Clear();
            curedList.Add("disease");
        }

        int abilityHealed = target.Stats.HealAllAbilityDamage(999);
        if (abilityHealed > 0)
            curedList.Add($"ability damage ({abilityHealed} pts)");

        sb.AppendLine(curedList.Count > 0
            ? $"  Cured: {string.Join(", ", curedList)}"
            : "  No conditions to cure.");

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

        // PHB p.272: constructs, elementals, outsiders and undead creatures can't be resurrected.
        bool isUndead = SpellTargetingService.IsUndead(target);
        bool isConstruct = SpellTargetingService.IsConstruct(target);
        string creatureType = target.Stats.CreatureType ?? string.Empty;
        bool isElemental = creatureType.IndexOf("Elemental", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isOutsider = creatureType.IndexOf("Outsider", StringComparison.OrdinalIgnoreCase) >= 0;

        if (isUndead || isConstruct || isElemental || isOutsider)
        {
            string ct = isUndead ? "undead" : isConstruct ? "construct" : isElemental ? "elemental" : "outsider";
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
        int durationRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel); // SpellDurationRules (SPL-002)
        string casterName = caster.Stats.CharacterName;

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"👁 {casterName} casts True Seeing!");
        sb.AppendLine($"  School: Divination | Level: 5 | Touch");
        sb.AppendLine($"  Duration: {durationRounds} rounds ({SpellDurationRules.DescribeRounds(durationRounds)})");
        sb.AppendLine($"  Target: {target.Stats.CharacterName}");

        // Apply via StatusEffectManager
        StatusEffectManager statusMgr = target.StatusEffectManager;
        if (statusMgr == null)
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(target.Stats); // always rebind to the current stats, as the generic branch does
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
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(target.Stats); // always rebind to the current stats, as the generic branch does
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
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(target.Stats); // always rebind to the current stats, as the generic branch does
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
        SpellcastingComponent spellComp,
        SpellResult castResult = null)
    {
        if (caster == null || target == null || target.Stats == null || spell == null) return null;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int saveDc = GetSpellSaveDC(caster, spell);
        string casterName = caster.Stats.CharacterName;

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🌀 {casterName} casts Plane Shift!");
        sb.AppendLine($"  School: Conjuration (Teleportation) | Touch");
        sb.AppendLine($"  Will DC {saveDc} negates | SR: Yes");
        sb.AppendLine($"  Target: {target.Stats.CharacterName}");

        // The landed cast already rolled the touch attack, spell resistance and the Will save (SpellCaster.Cast), and
        // the pipelines reach this handler only when the save failed (SPL-037). Roll them only without a cast (harness).
        if (castResult == null)
        {
            var srResult = SpellSaveResolver.RollSpellResistance(caster, target, casterLevel);
            srResult.AppendToLog(sb);
            if (!srResult.Overcame)
            {
                sb.AppendLine($"  ✦ {target.Stats.CharacterName} resists (Spell Resistance)!");
                sb.Append("═══════════════════════════════════");
                CombatUI?.ShowCombatLog(sb.ToString());
                return null;
            }

            var saveResult = SpellSaveResolver.RollSave(target, SaveType.Will, saveDc, SaveContext.ForSpell(spell, caster != null ? caster.Stats : null));
            saveResult.AppendToLog(sb, "SAVED", "FAILED");
            if (saveResult.Saved)
            {
                sb.AppendLine($"  ✦ {target.Stats.CharacterName} resists the planar transport!");
                sb.Append("═══════════════════════════════════");
                CombatUI?.ShowCombatLog(sb.ToString());
                return null;
            }
        }

        sb.AppendLine($"  🌀 {target.Stats.CharacterName} is shifted to another plane!");
        sb.AppendLine($"  Target is removed from combat!");

        // Removal from the battle is modelled as death (HP -10), so the combat-end check and loot treat the
        // creature as gone; RAW it is alive on another plane (SPL-131).
        target.Stats.CurrentHP = -10;
        target.OnDeath();
        HandleSummonDeathCleanup(target);
        sb.AppendLine($"  💫 {target.Stats.CharacterName} vanishes in a shimmer of planar energy!");

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
            statusMgr = target.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(target.Stats); // always rebind to the current stats, as the generic branch does

        ActiveSpellEffect effect = statusMgr.AddEffect(spell, casterName, casterLevel);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🎭 {casterName} casts Alter Self!");
        sb.AppendLine($"  School: Transmutation | Level: 2 | Self");
        sb.AppendLine($"  +2 size bonus to STR, +10 Disguise check bonus");
        int dur = ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel);
        sb.AppendLine($"  Duration: {dur} rounds ({SpellDurationRules.DescribeRounds(dur)})");

        if (effect != null)
            sb.AppendLine($"  ✦ {target.Stats.CharacterName}: +2 size STR, altered form");
        else
            sb.AppendLine($"  ✦ Effect already active (stacking prevented)");

        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        return effect;
    }
}
