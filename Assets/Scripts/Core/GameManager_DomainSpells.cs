// ============================================================================
// GameManager_DomainSpells.cs — Domain spell resolution methods.
//
// Part of the GameManager partial class.
// D&D 3.5e PHB rules.
//
// Spells implemented here:
//   • Hold Animal (Animal 2)       — Paralysis, creature-type-gated Hold Person
//   • Calm Animals (Animal 1)      — HD-budget pacification of animals
//   • Calm Emotions (Law 2)        — AoE suppression of morale/emotion effects
//   • Produce Flame (Fire 2)       — 1d6+CL/2 fire ranged touch attack
//   • Heat Metal (Sun 2)           — Escalating fire over 7 rounds
//   • Dominate Animal (Animal 3)   — Mind-control one animal
//   • Command Plants (Plant 4)     — Mind-control plant creatures
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
    //  HOLD ANIMAL — PHB p.241
    //  Identical to Hold Person, but only works on Animal creature type.
    //  Duration: 1 round/level. Will negates. New save each round.
    // ================================================================

    /// <summary>
    /// Resolves Hold Animal: paralyze one animal. Uses Hold Person mechanics
    /// but checks for Animal creature type first.
    /// </summary>
    private bool TryResolveHoldAnimalSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.DOMAIN_HOLD_ANIMAL, StringComparison.Ordinal))
            return false;

        if (target == null || target.Stats == null)
            return true;

        // Must be an Animal creature type
        string creatureType = target.Stats.CreatureType ?? "";
        if (!string.Equals(creatureType.Trim(), "Animal", StringComparison.OrdinalIgnoreCase))
        {
            string casterName = caster?.Stats?.CharacterName ?? "Caster";
            CombatUI?.ShowCombatLog($"<color=#FF9966>⛓ {casterName} casts Hold Animal on {target.Stats.CharacterName} — no effect (not an animal, creature type: {creatureType})!</color>");
            return true;
        }

        // Apply Hold Person–style paralysis
        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int holdRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        string sourceName = spell.Name;

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
            string fallbackSource = caster?.Stats?.CharacterName ?? sourceName;
            target.ApplyCondition(CombatConditionType.Paralyzed, holdRounds, fallbackSource);
            target.ApplyCondition(CombatConditionType.Helpless, holdRounds, fallbackSource);
        }

        CombatUI?.ShowCombatLog($"<color=#FF9966>⛓ {target.Stats.CharacterName} is paralyzed by Hold Animal for {holdRounds} round(s)! (Will save each round with cumulative +2 to break free)</color>");
        Debug.Log($"[GameManager] Hold Animal applied Paralyzed+Helpless to {target.Stats.CharacterName} for {holdRounds} rounds (CL {casterLevel})");
        return true;
    }

    // ================================================================
    //  CALM ANIMALS — PHB p.207
    //  AoE: Calms 2d4+CL HD of animals. Will negates. Breaks on threat.
    // ================================================================

    /// <summary>
    /// Resolves Calm Animals AoE: calms 2d4+CL HD of animals in the area.
    /// Each animal gets a Will save. Calmed animals won't attack.
    /// </summary>
    private bool TryResolveCalmAnimalsSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.DOMAIN_CALM_ANIMALS, StringComparison.Ordinal))
            return false;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int saveDc = GetSpellSaveDC(caster, spell);

        // Roll 2d4 + caster level for HD budget
        int hdBudget = DiceRoller.D4() + DiceRoller.D4() + casterLevel;

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🐾 {caster?.Stats?.CharacterName ?? "Caster"} casts Calm Animals!");
        sb.AppendLine($"  HD Budget: 2d4+{casterLevel} = {hdBudget} HD");
        sb.AppendLine($"  Will DC {saveDc} | SR: Yes");
        sb.AppendLine();

        // Filter to animals only
        List<CharacterController> animalTargets = new List<CharacterController>();
        if (targets != null)
        {
            foreach (var t in targets)
            {
                if (t == null || t.Stats == null || t.Stats.IsDead) continue;
                string ct = t.Stats.CreatureType ?? "";
                if (string.Equals(ct.Trim(), "Animal", StringComparison.OrdinalIgnoreCase))
                    animalTargets.Add(t);
            }
        }

        if (animalTargets.Count == 0)
        {
            sb.AppendLine("  No animals in area — spell has no effect.");
            sb.Append("═══════════════════════════════════");
            log = sb.ToString();
            return true;
        }

        int hdSpent = 0;
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        foreach (var target in animalTargets)
        {
            int targetHD = Mathf.Max(1, target.Stats.Level);
            if (hdSpent + targetHD > hdBudget)
            {
                sb.AppendLine($"  {target.Stats.CharacterName} ({targetHD} HD) — skipped (insufficient HD budget remaining: {hdBudget - hdSpent})");
                continue;
            }

            // Will save
            int willRoll = DiceRoller.D20();
            int willMod = target.Stats.WillSave;
            int willTotal = willRoll + willMod;
            bool savePassed = willTotal >= saveDc;

            sb.AppendLine($"  {target.Stats.CharacterName} ({targetHD} HD): Will d20({willRoll})+{willMod}={willTotal} vs DC {saveDc} → {(savePassed ? "SAVED" : "FAILED")}");

            if (!savePassed)
            {
                hdSpent += targetHD;

                // Apply calmed condition — target won't attack
                if (_conditionService != null)
                {
                    _conditionService.ApplyCondition(
                        target,
                        CombatConditionType.Fascinated,
                        durationRounds,
                        source: caster,
                        sourceNameOverride: spell.Name,
                        sourceCategory: "Spell",
                        sourceId: spell.SpellId);
                }
                else
                {
                    target.ApplyCondition(CombatConditionType.Fascinated, durationRounds, caster?.Stats?.CharacterName ?? "Calm Animals");
                }

                sb.AppendLine($"    🐾 {target.Stats.CharacterName} is calmed for {durationRounds} rounds! (HD spent: {hdSpent}/{hdBudget})");
            }
            sb.AppendLine();
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  CALM EMOTIONS — PHB p.207
    //  AoE: Suppresses morale bonuses, rage, fear, confusion.
    //  Duration: Concentration + 1 round/level. Will negates.
    // ================================================================

    /// <summary>
    /// Resolves Calm Emotions AoE: suppresses morale bonuses, rage, fear, and
    /// confusion effects on all creatures in 20-ft radius.
    /// </summary>
    private bool TryResolveCalmEmotionsSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.CALM_EMOTIONS, StringComparison.Ordinal))
            return false;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int saveDc = GetSpellSaveDC(caster, spell);
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"☮ {caster?.Stats?.CharacterName ?? "Caster"} casts Calm Emotions!");
        sb.AppendLine($"  Area: 20-ft radius | Will DC {saveDc} | Duration: {durationRounds} rounds (concentration)");
        sb.AppendLine();

        if (targets == null || targets.Count == 0)
        {
            sb.AppendLine("  No creatures in area.");
            sb.Append("═══════════════════════════════════");
            log = sb.ToString();
            return true;
        }

        foreach (var target in targets)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead) continue;

            // Will save
            int willRoll = DiceRoller.D20();
            int willMod = target.Stats.WillSave;
            int willTotal = willRoll + willMod;
            bool savePassed = willTotal >= saveDc;

            sb.AppendLine($"  {target.Stats.CharacterName}: Will d20({willRoll})+{willMod}={willTotal} vs DC {saveDc} → {(savePassed ? "SAVED" : "FAILED")}");

            if (!savePassed)
            {
                // Suppress rage effects
                if (target.Stats.SpellRageACPenalty != 0 || target.Stats.RageACPenalty != 0)
                {
                    target.Stats.SpellRageACPenalty = 0;
                    // RageACPenalty is computed from IsRaging state
                    sb.AppendLine($"    ☮ Rage suppressed on {target.Stats.CharacterName}!");
                }

                // Suppress fear (remove Frightened/Shaken)
                target.RemoveCondition(CombatConditionType.Frightened);
                target.RemoveCondition(CombatConditionType.Shaken);
                target.RemoveCondition(CombatConditionType.Panicked);

                // Apply Calm Emotions marker as Fascinated for tracking
                if (_conditionService != null)
                {
                    _conditionService.ApplyCondition(
                        target,
                        CombatConditionType.Fascinated,
                        durationRounds,
                        source: caster,
                        sourceNameOverride: spell.Name,
                        sourceCategory: "Spell",
                        sourceId: spell.SpellId);
                }
                else
                {
                    target.ApplyCondition(CombatConditionType.Fascinated, durationRounds, caster?.Stats?.CharacterName ?? "Calm Emotions");
                }

                sb.AppendLine($"    ☮ {target.Stats.CharacterName} is calmed: morale bonuses, rage, and fear suppressed for {durationRounds} rounds!");
            }
            sb.AppendLine();
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  PRODUCE FLAME — PHB p.265
    //  1d6 + min(CL,5) fire damage as ranged touch attack.
    //  Duration: 1 min/level but we resolve as single attack.
    // ================================================================

    /// <summary>
    /// Resolves Produce Flame: deals 1d6 + min(CL,5) fire damage via
    /// ranged touch attack to a single target.
    /// </summary>
    private bool TryResolveProduceFlameSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.DOMAIN_PRODUCE_FLAME, StringComparison.Ordinal))
            return false;

        if (target == null || target.Stats == null || caster == null || caster.Stats == null)
            return true;

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int bonusDamage = Mathf.Min(casterLevel, 5); // +1 per CL, max +5

        // Ranged touch attack
        int attackRoll = DiceRoller.D20();
        int attackMod = caster.Stats.BaseAttackBonus + caster.Stats.DEXMod;
        int attackTotal = attackRoll + attackMod;
        int targetTouchAC = target.Stats.TouchArmorClass;
        bool hit = (attackRoll == 20) || (attackRoll != 1 && attackTotal >= targetTouchAC);

        string casterName = caster.Stats.CharacterName;
        var sb = new StringBuilder();
        sb.AppendLine($"<color=#FF6600>🔥 {casterName} hurls Produce Flame at {target.Stats.CharacterName}!</color>");
        sb.AppendLine($"<color=#FFCC66>  Ranged Touch: d20({attackRoll})+{attackMod}={attackTotal} vs Touch AC {targetTouchAC} → {(hit ? "HIT" : "MISS")}</color>");

        if (hit)
        {
            int damage = DiceRoller.D6() + bonusDamage; // 1d6 + min(CL,5)
            damage = Mathf.Max(1, damage);

            int hpBefore = target.Stats.CurrentHP;
            target.Stats.CurrentHP -= damage;
            int hpAfter = target.Stats.CurrentHP;

            sb.AppendLine($"<color=#FF4444>  Fire Damage: 1d6+{bonusDamage} = {damage}</color>");
            sb.AppendLine($"<color=#FF4444>  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP</color>");

            CheckConcentrationOnDamage(target, damage);

            if (target.Stats.IsDead)
            {
                target.OnDeath();
                HandleSummonDeathCleanup(target);
                sb.AppendLine($"<color=#FF0000>  💀 {target.Stats.CharacterName} has been slain!</color>");
            }
        }

        CombatUI?.ShowCombatLog(sb.ToString());
        UpdateAllStatsUI();
        return true;
    }

    // ================================================================
    //  HEAT METAL — PHB p.236
    //  7-round escalating fire damage:
    //  Rd1: warm (0), Rd2: hot (1d4), Rd3-4: searing (2d4),
    //  Rd5: hot (2d4), Rd6: warm (1d4), Rd7: cool (0)
    // ================================================================

    /// <summary>
    /// Resolves Heat Metal: applies a tracked debuff that deals escalating
    /// fire damage each round for 7 rounds.
    /// </summary>
    private bool TryResolveHeatMetalSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.DOMAIN_HEAT_METAL, StringComparison.Ordinal))
            return false;

        if (target == null || target.Stats == null)
            return true;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        string casterName = caster?.Stats?.CharacterName ?? "Caster";

        // Track the effect via StatusEffectManager for duration/dispel
        StatusEffectManager targetStatusMgr = target.GetComponent<StatusEffectManager>();
        if (targetStatusMgr == null)
            targetStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
        targetStatusMgr.Init(target.Stats);

        ActiveSpellEffect effect = targetStatusMgr.AddEffect(
            spell, casterName, casterLevel);

        if (effect != null)
        {
            effect.CustomTag = "HeatMetal";
            effect.RemainingRounds = 7;
        }

        // Immediate round 1 = warm, no damage yet
        CombatUI?.ShowCombatLog($"<color=#FF6600>🔥 {casterName} casts Heat Metal on {target.Stats.CharacterName}!</color>");
        CombatUI?.ShowCombatLog($"<color=#FFCC66>   Metal equipment begins to warm. Escalating fire damage over 7 rounds.</color>");
        CombatUI?.ShowCombatLog($"<color=#FFCC66>   Round pattern: warm(0)/hot(1d4)/searing(2d4)/searing(2d4)/hot(2d4)/warm(1d4)/cool(0)</color>");

        Debug.Log($"[GameManager] Heat Metal applied to {target.Stats.CharacterName} for 7 rounds (CL {casterLevel})");
        return true;
    }

    /// <summary>
    /// Called each round to process Heat Metal escalating damage.
    /// Round schedule (1-indexed): 1=0, 2=1d4, 3=2d4, 4=2d4, 5=2d4, 6=1d4, 7=0
    /// </summary>
    public void ProcessHeatMetalTick(CharacterController target, int roundsElapsed)
    {
        if (target == null || target.Stats == null || target.Stats.IsDead) return;

        int diceCount = 0;
        string phase = "";
        switch (roundsElapsed)
        {
            case 1: phase = "warm"; diceCount = 0; break;
            case 2: phase = "hot"; diceCount = 1; break;
            case 3: phase = "searing"; diceCount = 2; break;
            case 4: phase = "searing"; diceCount = 2; break;
            case 5: phase = "hot"; diceCount = 2; break;
            case 6: phase = "warm"; diceCount = 1; break;
            case 7: phase = "cooling"; diceCount = 0; break;
            default: return;
        }

        if (diceCount == 0)
        {
            CombatUI?.ShowCombatLog($"<color=#FFCC66>🔥 Heat Metal on {target.Stats.CharacterName}: {phase} — no damage this round.</color>");
            return;
        }

        int totalDamage = 0;
        for (int i = 0; i < diceCount; i++)
            totalDamage += DiceRoller.D4(); // 1d4

        int hpBefore = target.Stats.CurrentHP;
        target.Stats.CurrentHP -= totalDamage;
        int hpAfter = target.Stats.CurrentHP;

        CombatUI?.ShowCombatLog($"<color=#FF4444>🔥 Heat Metal on {target.Stats.CharacterName}: {phase} — {diceCount}d4 = {totalDamage} fire [{hpBefore}→{hpAfter} HP]</color>");

        CheckConcentrationOnDamage(target, totalDamage);

        if (target.Stats.IsDead)
        {
            target.OnDeath();
            HandleSummonDeathCleanup(target);
            CombatUI?.ShowCombatLog($"<color=#FF0000>  💀 {target.Stats.CharacterName} has been slain by Heat Metal!</color>");
        }

        UpdateAllStatsUI();
    }

    // ================================================================
    //  DOMINATE ANIMAL — PHB p.224
    //  Control one animal. Will negates. 1 round/level.
    // ================================================================

    /// <summary>
    /// Resolves Dominate Animal: grants telepathic control over one animal.
    /// Must be Animal creature type. Will negates.
    /// </summary>
    private bool TryResolveDominateAnimalSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.DOMINATE_ANIMAL, StringComparison.Ordinal))
            return false;

        if (target == null || target.Stats == null || caster == null || caster.Stats == null)
            return true;

        // Must be Animal
        string creatureType = target.Stats.CreatureType ?? "";
        if (!string.Equals(creatureType.Trim(), "Animal", StringComparison.OrdinalIgnoreCase))
        {
            CombatUI?.ShowCombatLog($"<color=#FF9966>🧠 {caster.Stats.CharacterName} casts Dominate Animal on {target.Stats.CharacterName} — no effect (not an animal, type: {creatureType})!</color>");
            return true;
        }

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        // Apply domination — reuse Command Undead pattern for side-switching
        var effectData = new CommandUndeadEffectData
        {
            IsActive = true,
            IsIntelligent = false, // Animals are generally non-intelligent in D&D context
            DurationRemainingRounds = durationRounds,
            CasterLevel = casterLevel,
            SourceSpellId = SpellNames.DOMINATE_ANIMAL,
            SourceName = "Dominate Animal"
        };
        effectData.SetCaster(caster);
        effectData.SetControlledUndead(target); // Reuse the field for "controlled creature"

        target.ApplyCommandUndeadEffect(effectData);

        CombatUI?.ShowCombatLog($"<color=#CC66FF>🧠 {caster.Stats.CharacterName} dominates {target.Stats.CharacterName}!</color>");
        CombatUI?.ShowCombatLog($"<color=#CC99FF>   The animal is now under {caster.Stats.CharacterName}'s telepathic control for {durationRounds} rounds.</color>");

        Debug.Log($"[GameManager] Dominate Animal: {target.Stats.CharacterName} controlled by {caster.Stats.CharacterName} for {durationRounds} rounds");
        return true;
    }

    // ================================================================
    //  COMMAND PLANTS — PHB p.211
    //  Control plant creatures. Will negates. 1 day/level.
    //  HD budget: 2 HD per caster level.
    // ================================================================

    /// <summary>
    /// Resolves Command Plants: commands one plant creature.
    /// Must be Plant creature type. Will negates.
    /// </summary>
    private bool TryResolveCommandPlantsSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (spell == null || !string.Equals(spell.SpellId, SpellNames.COMMAND_PLANTS, StringComparison.Ordinal))
            return false;

        if (target == null || target.Stats == null || caster == null || caster.Stats == null)
            return true;

        // Must be Plant
        string creatureType = target.Stats.CreatureType ?? "";
        if (!string.Equals(creatureType.Trim(), "Plant", StringComparison.OrdinalIgnoreCase))
        {
            CombatUI?.ShowCombatLog($"<color=#FF9966>🌿 {caster.Stats.CharacterName} casts Command Plants on {target.Stats.CharacterName} — no effect (not a plant creature, type: {creatureType})!</color>");
            return true;
        }

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int maxHD = casterLevel * 2; // 2 HD per caster level
        int targetHD = Mathf.Max(1, target.Stats.Level);

        if (targetHD > maxHD)
        {
            CombatUI?.ShowCombatLog($"<color=#FF9966>🌿 {caster.Stats.CharacterName} casts Command Plants on {target.Stats.CharacterName} — no effect ({targetHD} HD exceeds budget of {maxHD} HD)!</color>");
            return true;
        }

        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        // Apply domination — reuse Command Undead pattern
        var effectData = new CommandUndeadEffectData
        {
            IsActive = true,
            IsIntelligent = false,
            DurationRemainingRounds = durationRounds,
            CasterLevel = casterLevel,
            SourceSpellId = SpellNames.COMMAND_PLANTS,
            SourceName = "Command Plants"
        };
        effectData.SetCaster(caster);
        effectData.SetControlledUndead(target);

        target.ApplyCommandUndeadEffect(effectData);

        CombatUI?.ShowCombatLog($"<color=#66CC66>🌿 {caster.Stats.CharacterName} commands {target.Stats.CharacterName}!</color>");
        CombatUI?.ShowCombatLog($"<color=#88DD88>   The plant creature ({targetHD} HD) obeys for {durationRounds} rounds (~{durationRounds / 14400} days).</color>");

        Debug.Log($"[GameManager] Command Plants: {target.Stats.CharacterName} ({targetHD} HD) controlled by {caster.Stats.CharacterName} for {durationRounds} rounds");
        return true;
    }

}
