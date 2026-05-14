// ============================================================================
// GameManager_NewSpells.cs — Resolution logic for Lightning Bolt, Fireball,
// Daylight, Rage, Hold Person, Displacement, Wind Wall, Invisibility Sphere,
// Halt Undead, Ray of Exhaustion, and Vampiric Touch spells (PHB 3.5e).
// Part of the GameManager partial class.
// ============================================================================
using System;
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

    // ================================================================
    //  INVISIBILITY SPHERE — Mobile Emanation (PHB p.245)
    // ================================================================

    /// <summary>
    /// Applies the Invisibility Sphere spell (Bard 3, Sorcerer/Wizard 3).
    /// Per PHB p.245: 10-ft-radius emanation centered on the recipient.
    ///   - All creatures within the emanation at cast time become invisible.
    ///   - The area moves with the recipient (mobile emanation).
    ///   - Creatures that LEAVE the emanation become visible immediately.
    ///   - Creatures that ENTER the emanation later do NOT become invisible.
    ///   - If a creature OTHER THAN the recipient attacks, only that creature
    ///     becomes visible.
    ///   - If the RECIPIENT attacks, the entire spell ends.
    ///
    /// Called from ApplySpellBuff when spell.SpellId == INVISIBILITY_SPHERE.
    /// </summary>
    private ActiveSpellEffect ApplyInvisibilitySphere(CharacterController caster, CharacterController target, SpellData spell, SpellcastingComponent spellComp)
    {
        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null || spell == null)
            return null;

        // Track the spell on the recipient so duration/dispel/dismiss
        // work via the standard StatusEffectManager path.
        StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
        if (recipientStatusMgr == null)
            recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        recipientStatusMgr.Init(recipient.Stats);

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        ActiveSpellEffect effect = recipientStatusMgr.AddEffect(
            spell,
            caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name,
            casterLevel);

        if (effect == null)
        {
            UpdateAllStatsUI();
            return null;
        }

        SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
        if (recipientSpellComp != null)
            recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

        int durationRounds = Mathf.Max(1, effect.RemainingRounds);

        // Build & register the emanation
        var sphere = InvisibilitySphereEffect.Create(recipient, caster, durationRounds, casterLevel);
        RegisterEmanation(sphere);

        // Capture initial affected creatures (everyone within 10 ft of recipient)
        List<CharacterController> all = GetAllCharacters();
        sphere.ApplyInitialAffectedCreatures(all);

        // Logging
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
        bool selfCast = recipient == caster;
        string castLine = selfCast
            ? $"<color=#88CCFF>👻 {casterName} casts Invisibility Sphere on self!</color>"
            : $"<color=#88CCFF>👻 {casterName} casts Invisibility Sphere on {recipient.Stats.CharacterName}!</color>";
        CombatUI?.ShowCombatLog(castLine);

        int affectedCount = sphere.InitiallyAffectedCreatures != null ? sphere.InitiallyAffectedCreatures.Count : 0;
        CombatUI?.ShowCombatLog($"<color=#A6F3FF>   A 10-ft emanation forms around {recipient.Stats.CharacterName}; {affectedCount} creature(s) become invisible.</color>");

        if (affectedCount > 0)
        {
            var sb = new StringBuilder("<color=#A6F3FF>   Affected: ");
            for (int i = 0; i < sphere.InitiallyAffectedCreatures.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var c = sphere.InitiallyAffectedCreatures[i];
                sb.Append(c != null && c.Stats != null ? c.Stats.CharacterName : "?");
            }
            sb.Append("</color>");
            CombatUI?.ShowCombatLog(sb.ToString());
        }

        CombatUI?.ShowCombatLog($"<color=#A6F3FF>   Duration: {effect.GetDurationDisplayString()}. Leaving the sphere or attacking ends invisibility.</color>");

        UpdateEnemyLastKnownPositionForInvisibility(recipient);
        UpdateAllStatsUI();
        return effect;
    }

    /// <summary>
    /// Per-round refresh of all active Invisibility Sphere emanations.
    /// Removes invisibility from creatures who have stepped out of the sphere.
    /// Called from TickEmanations (and may be invoked after movement actions).
    /// </summary>
    public void RefreshInvisibilitySpheres()
    {
        var spheres = GetActiveEmanationsOfType<InvisibilitySphereEffect>();
        for (int i = 0; i < spheres.Count; i++)
        {
            spheres[i]?.RefreshMembership();
        }
    }

    /// <summary>
    /// Ends an Invisibility Sphere centered on the given recipient (e.g. when
    /// the recipient attacks, the spell expires/is dismissed, or is dispelled).
    /// All initially-affected creatures become visible at once.
    /// </summary>
    /// <param name="recipient">The creature on whom the sphere is centered.</param>
    /// <param name="reason">Free-text reason shown in the combat log.</param>
    public void EndInvisibilitySphereForRecipient(CharacterController recipient, string reason = "spell ended")
    {
        if (recipient == null) return;

        var spheres = GetActiveEmanationsOfType<InvisibilitySphereEffect>();
        for (int i = 0; i < spheres.Count; i++)
        {
            var s = spheres[i];
            if (s == null || s.HasEnded) continue;
            if (s.CenterCreature != recipient) continue;

            s.EndForAll(reason);
        }
    }

    /// <summary>
    /// Returns the active Invisibility Sphere this creature is currently
    /// invisible from, or null if none.
    /// </summary>
    public InvisibilitySphereEffect GetInvisibilitySphereAffecting(CharacterController creature)
    {
        if (creature == null) return null;

        var spheres = GetActiveEmanationsOfType<InvisibilitySphereEffect>();
        for (int i = 0; i < spheres.Count; i++)
        {
            var s = spheres[i];
            if (s == null || s.HasEnded) continue;
            if (s.IsCreatureAffected(creature))
                return s;
        }
        return null;
    }

    /// <summary>
    /// Handles an attack made by a creature that is invisible due to an
    /// Invisibility Sphere. Per PHB p.245:
    ///   - If the attacker is the recipient → ALL affected creatures become visible.
    ///   - Otherwise → only that one creature becomes visible.
    /// Returns true if a sphere matched and was processed (so the standard
    /// invisibility-on-attack flow can be skipped for this attacker).
    /// </summary>
    public bool TryHandleInvisibilitySphereAttack(CharacterController attacker, string reason = "attacked")
    {
        if (attacker == null) return false;

        var sphere = GetInvisibilitySphereAffecting(attacker);
        if (sphere == null) return false;

        if (sphere.CenterCreature == attacker)
        {
            sphere.EndForAll(reason);

            // Also clean the recipient's tracking ActiveSpellEffect so the duration
            // bar / dismiss UI clears on the same round as the sphere ending.
            StatusEffectManager mgr = attacker.GetComponent<StatusEffectManager>();
            mgr?.RemoveEffectsBySpellId(SpellNames.INVISIBILITY_SPHERE);
        }
        else
        {
            sphere.EndForCreature(attacker, reason);
        }
        return true;
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

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        int saveDc = GetSpellSaveDC(caster, spell);

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
            if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
            {
                int srRoll = UnityEngine.Random.Range(1, 21);
                int srTotal = srRoll + casterLevel;
                bool srOvercome = srTotal >= target.Stats.SpellResistance;
                sb.AppendLine($"  SR Check: d20({srRoll}) + {casterLevel} = {srTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");
                if (!srOvercome)
                {
                    sb.AppendLine($"  {target.Stats.CharacterName} resists Halt Undead via Spell Resistance!");
                    sb.AppendLine();
                    continue;
                }
            }

            // Save check (only intelligent undead get a save)
            bool isIntelligent = target.IsIntelligentUndead();
            bool savePassed = false;
            if (isIntelligent)
            {
                int willRoll = UnityEngine.Random.Range(1, 21);
                int willMod = target.Stats.WillSave;
                int willTotal = willRoll + willMod;
                savePassed = willTotal >= saveDc;
                sb.AppendLine($"  Will save (intelligent undead): d20({willRoll}) + {willMod} = {willTotal} vs DC {saveDc} → {(savePassed ? "SAVED (negated)" : "FAILED")}");
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
    //  VAMPIRIC TOUCH — PHB p.281
    //  Melee touch attack. Deals 1d6 negative energy damage per 2 CL
    //  (max 10d6). Caster gains temp HP equal to damage dealt
    //  (capped at caster's max HP), lasting 1 hour. SR: Yes.
    // ================================================================

    private static bool IsVampiricTouchSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.VAMPIRIC_TOUCH, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Vampiric Touch. The melee touch must hit. Rolls
    /// 1d6/2CL (max 10d6) negative energy damage. Caster gains temporary
    /// HP equal to damage dealt, capped at caster's max HP, for 1 hour.
    /// Called from the touch/ray spell pipeline in PC and NPC casts.
    /// </summary>
    private bool TryResolveVampiricTouchSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (!IsVampiricTouchSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null)
            return true;

        // Melee touch missed → no effect
        if (result.RequiredAttackRoll && !result.AttackHit)
        {
            CombatUI?.ShowCombatLog($"❌ Vampiric Touch misses {target.Stats.CharacterName}.");
            return true;
        }

        if (caster == null || caster.Stats == null)
            return true;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());

        // SR is already checked by the base spell pipeline (SpellCaster.cs).
        // If this method is called with result.Success = true, SR has already passed.

        // Roll damage: 1d6 per 2 caster levels, max 10d6
        int diceCount = Mathf.Clamp(casterLevel / 2, 1, 10);
        int damage = 0;
        for (int i = 0; i < diceCount; i++)
            damage += UnityEngine.Random.Range(1, 7);

        int hpBefore = target.Stats.CurrentHP;
        target.Stats.TakeDamage(damage);
        int hpAfter = target.Stats.CurrentHP;

        result.DamageDealt = damage;
        result.DamageRolled = damage;
        result.DamageType = "negative";
        result.TargetHPBefore = hpBefore;
        result.TargetHPAfter = hpAfter;
        result.TargetKilled = target.Stats.IsDead;

        CombatUI?.ShowCombatLog($"<color=#9933CC>🖤 {caster.Stats.CharacterName}'s Vampiric Touch deals {damage} negative energy damage to {target.Stats.CharacterName} ({hpBefore} → {hpAfter} HP).</color>");

        // Concentration on damage is checked downstream in the touch pipeline (uses result.DamageDealt).

        // Caster gains temp HP equal to damage dealt, capped at caster's max HP
        int casterMaxHP = Mathf.Max(1, caster.Stats.MaxHP);
        int tempHP = Mathf.Min(damage, casterMaxHP);

        if (tempHP > 0)
        {
            // Vampiric Touch temp HP lasts 1 hour = 600 rounds
            int durationRounds = 600;
            FalseLifeEffectData vampiricTempHP = FalseLifeEffectData.CreateGenericTempHP(
                SpellNames.VAMPIRIC_TOUCH,
                "Vampiric Touch",
                tempHP,
                casterLevel,
                durationRounds,
                caster);

            caster.ApplyFalseLifeEffect(vampiricTempHP);

            CombatUI?.ShowCombatLog($"<color=#FF6666>🩸 {caster.Stats.CharacterName} drains {tempHP} temporary HP from {target.Stats.CharacterName} (lasts 1 hour).</color>");
        }

        if (target.Stats.IsDead)
        {
            CombatUI?.ShowCombatLog($"<color=#660033>💀 {target.Stats.CharacterName} is slain by Vampiric Touch!</color>");
            // OnDeath/HandleSummonDeathCleanup are called downstream in the touch pipeline (uses result.TargetKilled).
        }

        Debug.Log($"[GameManager] Vampiric Touch: {caster.Stats.CharacterName} dealt {damage} negative damage to {target.Stats.CharacterName}, gained {tempHP} temp HP (CL {casterLevel}, {diceCount}d6)");
        return true;
    }
}
