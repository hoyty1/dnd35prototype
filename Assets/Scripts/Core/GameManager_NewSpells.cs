// ============================================================================
// GameManager_NewSpells.cs — Resolution logic for Lightning Bolt, Fireball,
// Daylight, Rage, Hold Person, Displacement, and Wind Wall spells (PHB 3.5e).
// Part of the GameManager partial class.
// ============================================================================
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DND35e.Identifiers;

public partial class GameManager
{
    // ================================================================
    //  LIGHTNING BOLT & FIREBALL — Scalable AoE Damage Resolution
    // ================================================================

    /// <summary>
    /// Resolves Lightning Bolt or Fireball as AoE damage spells with
    /// 1d6/CL damage (max 10d6), Reflex half, and SR.
    /// Called from PerformAoESpellCast when the pending spell matches.
    /// </summary>
    private bool TryResolveScaledAoEDamageSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        bool isFireball = string.Equals(spell.SpellId, SpellNames.FIREBALL, System.StringComparison.Ordinal);
        bool isLightningBolt = string.Equals(spell.SpellId, SpellNames.LIGHTNING_BOLT, System.StringComparison.Ordinal);

        if (!isFireball && !isLightningBolt)
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int diceCount = Mathf.Clamp(casterLevel, 1, 10); // 1d6/CL, max 10d6
        int saveDc = GetSpellSaveDC(caster, spell);
        int castingAbilityMod = GetSpellSaveAbilityModifier(caster, spell);

        string damageType = isFireball ? "fire" : "electricity";
        string shapeStr = isFireball ? "20-ft radius burst" : "120-ft line";
        string spellName = spell.Name;

        // Fire damage notifies for fire-based area effects (pyrotechnics, web ignition, etc.)
        if (isFireball && aoeCells != null)
        {
            foreach (Vector2Int cell in aoeCells)
                NotifyFireDamageAtPosition(cell, spellName);
        }

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"✨ {caster.Stats.CharacterName} casts {spellName}! ({shapeStr})");
        sb.AppendLine($"  [{(spell.SpellLevel == 0 ? "Cantrip" : $"Level {spell.SpellLevel}")}] {spell.School}");
        sb.AppendLine($"  Damage: {diceCount}d6 {damageType} | Reflex DC {saveDc} for half");
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
                if (target == null || target.Stats == null || target.Stats.IsDead)
                    continue;

                targetIndex++;
                sb.AppendLine($"  --- Target {targetIndex}: {target.Stats.CharacterName} ---");

                // Check Spell Resistance
                if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
                {
                    int srCheckRoll = Random.Range(1, 21);
                    int srCheckTotal = srCheckRoll + casterLevel;
                    bool srOvercome = srCheckTotal >= target.Stats.SpellResistance;

                    sb.AppendLine($"  SR Check: d20({srCheckRoll}) + {casterLevel} = {srCheckTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");

                    if (!srOvercome)
                    {
                        sb.AppendLine($"  {target.Stats.CharacterName} resists {spellName} via Spell Resistance!");
                        sb.AppendLine();
                        continue;
                    }
                }

                // Roll damage
                int damage = 0;
                for (int i = 0; i < diceCount; i++)
                    damage += Random.Range(1, 7); // 1d6

                // Reflex save
                int reflexRoll = Random.Range(1, 21);
                int reflexMod = target.Stats.ReflexSave;
                int reflexTotal = reflexRoll + reflexMod;
                bool savePassed = reflexTotal >= saveDc;

                if (savePassed)
                    damage = Mathf.Max(0, damage / 2);

                damage = Mathf.Max(damage > 0 ? 1 : 0, damage);

                sb.AppendLine($"  Reflex save: d20({reflexRoll}) + {reflexMod} = {reflexTotal} vs DC {saveDc} → {(savePassed ? "SAVED (half)" : "FAILED (full)")}");

                int hpBefore = target.Stats.CurrentHP;
                target.Stats.TakeDamage(damage);
                int hpAfter = target.Stats.CurrentHP;

                sb.AppendLine($"  Damage: {damage} {damageType}");
                sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

                // Check concentration for spell damage on the target
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
    //  DAYLIGHT — Persistent Light Area Effect
    // ================================================================

    /// <summary>
    /// Resolves Daylight spell: creates a DaylightAreaEffect that provides
    /// 60-ft radius bright illumination and counters/dispels darkness effects.
    /// </summary>
    private bool TryResolveDaylightSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        if (!string.Equals(spell.SpellId, SpellNames.DAYLIGHT, System.StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        Vector3 centerPosition = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);
        CreateDaylightArea(centerPosition, durationRounds, casterLevel, caster);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"☀ {caster.Stats.CharacterName} casts Daylight!");
        sb.AppendLine($"  Area: 60-ft radius bright illumination ({(aoeCells != null ? aoeCells.Count : 0)} squares)");
        sb.AppendLine($"  Duration: {durationRounds} rounds ({durationRounds / 10} minutes)");
        sb.AppendLine("  • Bright light in 60-ft radius");
        sb.AppendLine("  • Counters/dispels Darkness spells of 3rd level or lower");
        sb.AppendLine("  • No save, no SR");

        if (targets != null && targets.Count > 0)
        {
            sb.Append("  Currently in area: ");
            for (int i = 0; i < targets.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(targets[i] != null && targets[i].Stats != null ? targets[i].Stats.CharacterName : "Unknown");
            }
            sb.AppendLine();
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    /// <summary>
    /// Creates a DaylightAreaEffect at the specified position.
    /// </summary>
    public void CreateDaylightArea(Vector3 centerPosition, int durationRounds, int casterLevel, CharacterController caster)
    {
        GameObject daylightObject = new GameObject("Daylight_Area");
        daylightObject.transform.position = centerPosition;

        DaylightAreaEffect daylight = daylightObject.AddComponent<DaylightAreaEffect>();
        daylight.CenterPosition = centerPosition;
        daylight.RoundsRemaining = Mathf.Max(1, durationRounds);
        daylight.CasterLevel = Mathf.Max(1, casterLevel);
        daylight.Caster = caster;
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

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        int holdRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
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

        CombatUI?.ShowCombatLog($"<color=#FF9966>⛓ {target.Stats.CharacterName} is paralyzed by Hold Person for {holdRounds} round(s)! (Will save each round with cumulative +2 to break free)</color>");
        Debug.Log($"[GameManager] Hold Person applied Paralyzed+Helpless to {target.Stats.CharacterName} for {holdRounds} rounds (CL {casterLevel})");

        return null;
    }

    // ================================================================
    //  DISPLACEMENT — 50% Miss Chance Buff (PHB p.222)
    // ================================================================

    /// <summary>
    /// Applies the Displacement spell effect to a target.
    /// Per PHB p.222: Subject appears about 2 ft from its true location, gaining a
    /// 50% miss chance as if it had total concealment. True Seeing negates this.
    /// Duration: 1 round/level (D).
    ///
    /// The actual MissChance/IsTotalConcealment fields on the ActiveSpellEffect are
    /// configured by StatusEffectManager.AddEffect (see StatusEffectManager.cs lines
    /// where spell.SpellId == "displacement" sets MissChance=50).
    /// Called from ApplySpellBuff when the spell matches.
    /// </summary>
    private ActiveSpellEffect ApplyDisplacementBuff(CharacterController caster, CharacterController target, SpellData spell, SpellcastingComponent spellComp)
    {
        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null || spell == null)
            return null;

        StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
        if (recipientStatusMgr == null)
            recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        recipientStatusMgr.Init(recipient.Stats);

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        ActiveSpellEffect effect = recipientStatusMgr.AddEffect(
            spell,
            caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name,
            casterLevel);

        if (effect != null)
        {
            SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
            if (recipientSpellComp != null)
                recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

            string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
            bool selfCast = recipient == caster;
            string castLine = selfCast
                ? $"<color=#88FFEE>👻 {casterName} casts Displacement on self!</color>"
                : $"<color=#88FFEE>👻 {casterName} casts Displacement on {recipient.Stats.CharacterName}!</color>";

            CombatUI?.ShowCombatLog(castLine);
            CombatUI?.ShowCombatLog($"<color=#A6F3FF>   {recipient.Stats.CharacterName}'s outline shimmers and shifts about 2 ft from its true position.</color>");
            CombatUI?.ShowCombatLog($"<color=#A6F3FF>   Attacks against {recipient.Stats.CharacterName} have 50% miss chance ({effect.GetDurationDisplayString()}, {Mathf.Max(0, effect.RemainingRounds)} rounds). True Seeing negates.</color>");
        }

        UpdateAllStatsUI();
        return effect;
    }

    // ================================================================
    //  WIND WALL — Persistent Line Area Effect (PHB p.302)
    // ================================================================

    /// <summary>
    /// Resolves Wind Wall spell: creates a WindWallAreaEffect along a line.
    /// Per PHB p.302:
    ///   - Wall up to 10 ft/level long and 5 ft/level high (S)
    ///   - Duration: 1 round/level
    ///   - Deflects arrows, bolts, and tiny/smaller flying creatures
    ///   - Disperses gases and fog
    ///   - Tiny or smaller occupants take 3d6 nonlethal damage
    /// </summary>
    private bool TryResolveWindWallSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        if (!string.Equals(spell.SpellId, SpellNames.WIND_WALL, System.StringComparison.Ordinal))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        // Length scales 2 squares per CL (10 ft per CL); use the AoE cells from targeting,
        // but cap length to caster level squares for safety.
        int maxLengthSquares = Mathf.Max(2, casterLevel * 2);

        Vector3 centerPosition = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);
        Vector2Int centerCell = SquareGridUtils.WorldToGrid(centerPosition);

        // Determine wall direction from caster facing (or from caster -> centerCell vector)
        Vector2Int direction = ComputeWindWallDirection(caster.GridPosition, centerCell);

        CreateWindWallArea(centerPosition, centerCell, maxLengthSquares, direction, durationRounds, casterLevel, caster, aoeCells);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"💨 {caster.Stats.CharacterName} casts Wind Wall!");
        sb.AppendLine($"  Wall: {maxLengthSquares} squares long, {Mathf.Max(1, casterLevel)} squares high (5 ft/level)");
        sb.AppendLine($"  Duration: {durationRounds} round(s)");
        sb.AppendLine("  • Deflects arrows, bolts, and tiny/smaller flying creatures");
        sb.AppendLine("  • Disperses gases and fog (Strong wind)");
        sb.AppendLine("  • Larger ranged weapons (spears) pass through");
        sb.AppendLine("  • Tiny or smaller occupants take 3d6 nonlethal damage");
        sb.AppendLine("  • No save; SR: Yes");

        if (targets != null && targets.Count > 0)
        {
            sb.Append("  In wall area: ");
            for (int i = 0; i < targets.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(targets[i] != null && targets[i].Stats != null ? targets[i].Stats.CharacterName : "Unknown");
            }
            sb.AppendLine();
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    /// <summary>
    /// Computes the wind wall facing direction (orthogonal Vector2Int) from
    /// the caster towards the wall center cell. Defaults to East if degenerate.
    /// </summary>
    private Vector2Int ComputeWindWallDirection(Vector2Int casterCell, Vector2Int centerCell)
    {
        int dx = centerCell.x - casterCell.x;
        int dy = centerCell.y - casterCell.y;

        if (Mathf.Abs(dx) >= Mathf.Abs(dy))
            return dx >= 0 ? new Vector2Int(1, 0) : new Vector2Int(-1, 0);

        return dy >= 0 ? new Vector2Int(0, 1) : new Vector2Int(0, -1);
    }

    /// <summary>
    /// Creates a WindWallAreaEffect at the specified position aligned along a line.
    /// Provided cells (from targeting) are used as the wall's footprint when supplied;
    /// otherwise we generate them along the perpendicular of the caster->center direction.
    /// </summary>
    public void CreateWindWallArea(
        Vector3 centerPosition,
        Vector2Int centerCell,
        int lengthSquares,
        Vector2Int direction,
        int durationRounds,
        int casterLevel,
        CharacterController caster,
        HashSet<Vector2Int> providedCells = null)
    {
        GameObject windWallObject = new GameObject("WindWall_Area");
        windWallObject.transform.position = centerPosition;

        WindWallAreaEffect windWall = windWallObject.AddComponent<WindWallAreaEffect>();
        windWall.CenterPosition = centerPosition;
        windWall.CenterCell = centerCell;
        windWall.LengthSquares = Mathf.Max(2, lengthSquares);
        windWall.HeightSquares = Mathf.Max(1, casterLevel);
        windWall.WallDirection = direction == Vector2Int.zero ? new Vector2Int(1, 0) : direction;
        windWall.RoundsRemaining = Mathf.Max(1, durationRounds);
        windWall.CasterLevel = Mathf.Max(1, casterLevel);
        windWall.Caster = caster;

        if (providedCells != null && providedCells.Count > 0)
            windWall.SetExplicitCells(providedCells);
    }
}
