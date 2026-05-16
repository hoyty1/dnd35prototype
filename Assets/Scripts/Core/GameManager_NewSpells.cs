// ============================================================================
// GameManager_NewSpells.cs — Resolution logic for Lightning Bolt, Fireball,
// Daylight, Rage, Hold Person, Displacement, Blink, Wind Wall, Invisibility Sphere,
// Halt Undead, Ray of Exhaustion, Vampiric Touch, Flame Arrow, Keen Edge,
// Greater Magic Weapon, Haste, Slow, Mass Enlarge Person, Mass Reduce Person,
// Bestow Curse, Greater Invisibility, Phantasmal Killer, and Rainbow Pattern
// spells (PHB 3.5e).
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
                    int srCheckRoll = UnityEngine.Random.Range(1, 21);
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
                    damage += UnityEngine.Random.Range(1, 7); // 1d6

                // Reflex save
                int reflexRoll = UnityEngine.Random.Range(1, 21);
                int reflexMod = target.Stats.ReflexSave;
                int reflexTotal = reflexRoll + reflexMod;
                bool savePassed = reflexTotal >= saveDc;

                if (savePassed)
                    damage = Mathf.Max(0, damage / 2);

                // D&D 3.5e PHB p.206: Blinking creatures take half damage from area attacks
                // (they are partially on the Ethereal Plane). This stacks with Reflex halving.
                bool targetIsBlinking = target.HasActiveBlinkEffect;
                if (targetIsBlinking)
                    damage = Mathf.Max(0, damage / 2);

                damage = Mathf.Max(damage > 0 ? 1 : 0, damage);

                sb.AppendLine($"  Reflex save: d20({reflexRoll}) + {reflexMod} = {reflexTotal} vs DC {saveDc} → {(savePassed ? "SAVED (half)" : "FAILED (full)")}");
                if (targetIsBlinking)
                    sb.AppendLine($"  Blink: area damage halved (target partially ethereal)");

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

        // ── WALL OF ICE DAMAGE ──
        // AoE spells also damage any Wall of Ice in the affected area.
        if (aoeCells != null && AreaEffectManager.HasInstance)
        {
            // Collect walls hit by this AoE (may be multiple walls, or same wall hit by multiple cells)
            var wallsHit = new Dictionary<WallOfIceAreaEffect, bool>();
            foreach (Vector2Int cell in aoeCells)
            {
                WallOfIceAreaEffect wall = WallOfIceAreaEffect.GetWallAtCell(cell);
                if (wall != null && !wallsHit.ContainsKey(wall))
                    wallsHit[wall] = true;
            }

            foreach (var kvp in wallsHit)
            {
                WallOfIceAreaEffect wall = kvp.Key;
                if (wall == null || wall.WallHP <= 0)
                    continue;

                // Roll separate damage for the wall (same dice as character damage)
                int wallDamage = 0;
                for (int i = 0; i < diceCount; i++)
                    wallDamage += UnityEngine.Random.Range(1, 7);

                // No save for objects; fire is especially effective
                sb.AppendLine($"  --- Wall of Ice ---");
                sb.AppendLine($"  {damageType} damage to wall: {wallDamage}");

                bool destroyed = wall.DealDamageToWall(wallDamage, isFireball);

                if (destroyed)
                    sb.AppendLine($"  💥 The Wall of Ice is destroyed by the {spellName}!");
                else
                    sb.AppendLine($"  Wall HP: {wall.WallHP}/{wall.WallMaxHP}");

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
    //  BLINK — Ethereal/Material Plane Shifting (PHB p.206)
    // ================================================================

    /// <summary>
    /// Applies the Blink spell effect to the caster (Personal range).
    /// Per PHB p.206: Subject blinks between Material and Ethereal Planes randomly.
    ///
    /// Defensive benefits:
    ///   - 50% miss chance vs physical attacks (ethereal, not invisible)
    ///   - 20% miss chance if attacker can see invisible OR strike ethereal
    ///   - 0% miss chance if attacker can do BOTH
    ///   - Blind-Fight feat doesn't help
    ///   - 50% miss chance vs targeted spells
    ///   - Half damage from area attacks
    ///   - Half damage from falling
    ///
    /// Offensive penalties/bonuses:
    ///   - 20% miss chance on own attacks
    ///   - 20% failure chance on own spells
    ///   - +2 attack bonus (strikes as invisible)
    ///   - Denies target Dex bonus to AC
    ///
    /// Duration: 1 round/level (D).
    /// </summary>
    private ActiveSpellEffect ApplyBlinkBuff(CharacterController caster, SpellData spell, SpellcastingComponent spellComp)
    {
        if (caster == null || caster.Stats == null || spell == null)
            return null;

        StatusEffectManager statusMgr = caster.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
            statusMgr = caster.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(caster.Stats);

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        ActiveSpellEffect effect = statusMgr.AddEffect(
            spell,
            caster.Stats.CharacterName,
            casterLevel);

        if (effect != null)
        {
            SpellcastingComponent casterSpellComp = caster.GetComponent<SpellcastingComponent>();
            if (casterSpellComp != null)
                casterSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

            // Apply the Blinking condition
            caster.ApplyCondition(CombatConditionType.Blinking, effect.RemainingRounds, spell.Name);

            string casterName = caster.Stats.CharacterName;
            CombatUI?.ShowCombatLog($"<color=#88CCFF>✨ {casterName} casts Blink!</color>");
            CombatUI?.ShowCombatLog($"<color=#A6D4FF>   {casterName} begins blinking between the Material and Ethereal Planes.</color>");
            CombatUI?.ShowCombatLog($"<color=#A6D4FF>   Defensive: 50% miss chance vs attacks ({effect.GetDurationDisplayString()}).</color>");
            CombatUI?.ShowCombatLog($"<color=#A6D4FF>   Offensive: 20% miss chance, but +2 attack & deny Dex to AC.</color>");
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

    // ================================================================
    //  FLAME ARROW — PHB 3.5e p.231
    //  Transmutation [Fire]. Sor/Wiz 3.
    //  Targets up to 50 projectiles (arrows, bolts, sling bullets, or
    //  any other ammunition) in caster's inventory.
    //  Each deals +1d6 fire damage when shot.
    //  Duration: 10 min/level or until all charges discharged.
    //  Does NOT apply to versatile throwing weapons (daggers, javelins).
    // ================================================================

    private static bool IsFlameArrowSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.FLAME_ARROW, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Flame Arrow by finding all ammunition (arrows, bolts, sling bullets, etc.)
    /// in the caster's inventory and applying an ItemSpellEffect with BonusDamageDice="1d6",
    /// DamageType=fire. Works on any ItemType.Ammunition projectile; excludes versatile
    /// throwing weapons (items that can be both thrown and used in melee, e.g. daggers, javelins).
    /// </summary>
    private bool TryResolveFlameArrowSpell(CharacterController caster, SpellData spell)
    {
        if (!IsFlameArrowSpell(spell) || caster == null || caster.Stats == null)
            return false;

        var inventory = Combat_GetCharacterInventory(caster);
        if (inventory == null)
        {
            CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} has no inventory for Flame Arrow.");
            return true;
        }

        // Find all ammunition stacks in inventory (arrows, bolts, sling bullets, etc.)
        // Exclude versatile throwing weapons (IsThrown items that can also be used in melee)
        var ammoStacks = new List<ItemData>();
        if (inventory.GeneralSlots != null)
        {
            foreach (var item in inventory.GeneralSlots)
            {
                if (item != null && item.Type == ItemType.Ammunition && item.HasAmmoRemaining && !item.IsThrown)
                    ammoStacks.Add(item);
            }
        }

        if (ammoStacks.Count == 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} has no projectiles in inventory to enchant with Flame Arrow.");
            return true;
        }

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        string casterName = caster.Stats.CharacterName;

        int totalEnchanted = 0;
        int maxProjectiles = 50;

        foreach (var ammo in ammoStacks)
        {
            if (totalEnchanted >= maxProjectiles)
                break;

            int toEnchant = Mathf.Min(ammo.Quantity, maxProjectiles - totalEnchanted);

            var effect = new ItemSpellEffect(spell.SpellId, spell.Name, casterName, casterLevel, durationRounds)
            {
                BonusDamageDice = "1d6",
                BonusDamageType = "fire",
                EnchantedAmmoRemaining = toEnchant
            };

            ammo.AddOrReplaceItemSpellEffect(effect);
            totalEnchanted += toEnchant;
        }

        CombatUI?.ShowCombatLog($"<color=#FF8844>🔥 {casterName} casts Flame Arrow — {totalEnchanted} projectiles now deal +1d6 fire damage [{durationRounds} rounds].</color>");
        Debug.Log($"[GameManager] Flame Arrow: {casterName} enchanted {totalEnchanted} projectiles with +1d6 fire, CL {casterLevel}, {durationRounds} rounds");

        UpdateAllStatsUI();
        return true;
    }

    // ================================================================
    //  KEEN EDGE — PHB 3.5e p.246
    //  Transmutation. Sor/Wiz 3.
    //  Doubles the threat range of one slashing/piercing weapon.
    //  Duration: 10 min/level.
    // ================================================================

    private static bool IsKeenEdgeSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.KEEN_EDGE, StringComparison.Ordinal);
    }

    private ItemData _pendingKeenEdgeItem;
    private bool _pendingKeenEdgeIsAmmo; // true when user chose the ammo path

    private bool TryHandleKeenEdgeWeaponSelection(CharacterController caster, CharacterController target)
    {
        if (!IsKeenEdgeSpell(_pendingSpell))
        {
            _pendingKeenEdgeItem = null;
            _pendingKeenEdgeIsAmmo = false;
            return false;
        }

        if (target == null || target.Stats == null)
            return false;

        if (_pendingKeenEdgeItem != null || _pendingKeenEdgeIsAmmo)
            return false;

        // Gather weapon options from target
        TryGetKeenEdgeWeaponOptions(target, out List<ItemData> weaponOptions, out List<string> weaponLabels);

        // Also check for ammunition in the CASTER's inventory (like Flame Arrow)
        bool hasAmmo = false;
        if (caster != null)
        {
            var casterInv = Combat_GetCharacterInventory(caster);
            if (casterInv != null && casterInv.GeneralSlots != null)
            {
                foreach (var item in casterInv.GeneralSlots)
                {
                    if (item != null && item.Type == ItemType.Ammunition && item.HasAmmoRemaining && !item.IsThrown)
                    {
                        hasAmmo = true;
                        break;
                    }
                }
            }
        }

        if (weaponOptions.Count == 0 && !hasAmmo)
        {
            CombatUI?.ShowCombatLog($"⚠ {target.Stats.CharacterName} has no eligible slashing/piercing weapon and {(caster != null ? caster.Stats.CharacterName : "caster")} has no ammunition for Keen Edge.");
            _pendingSpell = null;
            _pendingKeenEdgeItem = null;
            _pendingKeenEdgeIsAmmo = false;
            ShowActionChoices();
            return true;
        }

        // If only weapons and exactly one, auto-select
        if (weaponOptions.Count == 1 && !hasAmmo)
        {
            _pendingKeenEdgeItem = weaponOptions[0];
            return false;
        }

        // If only ammo and no weapons, auto-select ammo path
        if (weaponOptions.Count == 0 && hasAmmo)
        {
            _pendingKeenEdgeIsAmmo = true;
            return false;
        }

        // Build combined option list: weapons + ammo option
        var allOptions = new List<ItemData>(weaponOptions);
        var allLabels = new List<string>(weaponLabels);

        if (hasAmmo)
        {
            allOptions.Add(null); // sentinel for ammo path
            allLabels.Add("🏹 Enchant Ammunition (up to 50 projectiles)");
        }

        CombatUI?.ShowPickUpItemSelection(
            actorName: caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster",
            itemOptions: allLabels,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= allOptions.Count)
                {
                    _pendingSpell = null;
                    _pendingKeenEdgeItem = null;
                    _pendingKeenEdgeIsAmmo = false;
                    ShowActionChoices();
                    return;
                }

                if (allOptions[selectedIndex] == null)
                {
                    // Ammo path selected
                    _pendingKeenEdgeIsAmmo = true;
                    _pendingKeenEdgeItem = null;
                }
                else
                {
                    _pendingKeenEdgeItem = allOptions[selectedIndex];
                    _pendingKeenEdgeIsAmmo = false;
                }
                PerformSpellCast(caster, target);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingKeenEdgeItem = null;
                _pendingKeenEdgeIsAmmo = false;
                ShowActionChoices();
            },
            titleOverride: "Keen Edge - Select Target",
            bodyOverride: $"Choose a weapon or ammunition to enchant with Keen Edge.",
            optionButtonColorOverride: new Color(0.24f, 0.34f, 0.56f, 1f));
        return true;
    }

    private static bool TryGetKeenEdgeWeaponOptions(CharacterController target, out List<ItemData> weapons, out List<string> labels)
    {
        weapons = new List<ItemData>();
        labels = new List<string>();

        var inventory = target.GetComponent<InventoryComponent>()?.CharacterInventory;
        if (inventory == null)
            return false;

        TryAddKeenEdgeOption(inventory.RightHandSlot, "Right Hand", weapons, labels);
        TryAddKeenEdgeOption(inventory.LeftHandSlot, "Left Hand", weapons, labels);
        TryAddKeenEdgeOption(inventory.HandsSlot, "Hands", weapons, labels);

        if (inventory.GeneralSlots != null)
        {
            for (int i = 0; i < inventory.GeneralSlots.Length; i++)
            {
                ItemData item = inventory.GeneralSlots[i];
                if (item == null) continue;
                TryAddKeenEdgeOption(item, $"Backpack Slot {i + 1}", weapons, labels);
            }
        }

        return weapons.Count > 0;
    }

    private static void TryAddKeenEdgeOption(ItemData item, string locationLabel, List<ItemData> weapons, List<string> labels)
    {
        if (item == null || !item.IsWeapon || weapons == null || labels == null)
            return;

        // Keen Edge only works on slashing or piercing weapons
        string dmgType = item.DamageType != null ? item.DamageType.ToLowerInvariant() : "";
        bool isSlashing = dmgType.Contains("slashing");
        bool isPiercing = dmgType.Contains("piercing");
        if (!isSlashing && !isPiercing)
            return;

        int currentThreat = item.CritThreatMin > 0 ? item.CritThreatMin : 20;
        weapons.Add(item);
        labels.Add($"{item.Name} ({locationLabel}, threat {currentThreat}-20)");
    }

    private bool TryApplyKeenEdgeToPendingItem(CharacterController caster, CharacterController target, SpellData spell)
    {
        if (!IsKeenEdgeSpell(spell))
            return false;

        // ── Ammo path: enchant up to 50 projectiles (like Flame Arrow) ──
        if (_pendingKeenEdgeIsAmmo)
        {
            _pendingKeenEdgeIsAmmo = false;
            _pendingKeenEdgeItem = null;
            return TryApplyKeenEdgeToAmmo(caster, spell);
        }

        ItemData weapon = _pendingKeenEdgeItem;
        _pendingKeenEdgeItem = null;

        if (weapon == null)
        {
            CombatUI?.ShowCombatLog("⚠ Keen Edge failed: no weapon selected.");
            return true;
        }

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        int rounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;

        // Calculate threat range doubling: the "threat range" is (21 - CritThreatMin).
        // E.g. 19-20 = range 2, doubled = range 4, new min = 17.
        // E.g. 20 = range 1, doubled = range 2, new min = 19.
        int baseThreatMin = weapon.CritThreatMin > 0 ? weapon.CritThreatMin : 20;
        int threatRange = 21 - baseThreatMin; // how many values threaten (e.g. 2 for 19-20)
        int doubledRange = threatRange * 2;
        int newThreatMin = 21 - doubledRange;
        int critModifier = newThreatMin - baseThreatMin; // negative number to lower the min

        var effect = new ItemSpellEffect(spell.SpellId, spell.Name, casterName, casterLevel, rounds)
        {
            CritThreatRangeModifier = critModifier
        };

        weapon.AddOrReplaceItemSpellEffect(effect);

        string recipientName = target != null && target.Stats != null ? target.Stats.CharacterName : "target";
        CombatUI?.ShowCombatLog($"<color=#88FFEE>🗡 {spell.Name} sharpens {recipientName}'s {weapon.Name}: threat range doubled to {newThreatMin}-20 [{effect.GetDurationDisplayString()}].</color>");
        Debug.Log($"[GameManager] Keen Edge: {weapon.Name} threat {baseThreatMin}-20 → {newThreatMin}-20 (modifier {critModifier}), CL {casterLevel}");

        UpdateAllStatsUI();
        return true;
    }

    /// <summary>
    /// Keen Edge ammo path: enchant up to 50 projectiles in the caster's inventory with
    /// doubled threat range, mirroring the Flame Arrow pattern.
    /// Excludes versatile throwing weapons (same as Flame Arrow).
    /// </summary>
    private bool TryApplyKeenEdgeToAmmo(CharacterController caster, SpellData spell)
    {
        if (caster == null || caster.Stats == null)
        {
            CombatUI?.ShowCombatLog("⚠ Keen Edge failed: no caster.");
            return true;
        }

        var inventory = Combat_GetCharacterInventory(caster);
        if (inventory == null)
        {
            CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} has no inventory for Keen Edge.");
            return true;
        }

        // Find all ammunition stacks (exclude versatile throwing weapons, same as Flame Arrow)
        var ammoStacks = new List<ItemData>();
        if (inventory.GeneralSlots != null)
        {
            foreach (var item in inventory.GeneralSlots)
            {
                if (item != null && item.Type == ItemType.Ammunition && item.HasAmmoRemaining && !item.IsThrown)
                    ammoStacks.Add(item);
            }
        }

        if (ammoStacks.Count == 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} has no projectiles in inventory to enchant with Keen Edge.");
            return true;
        }

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        string casterName = caster.Stats.CharacterName;

        int totalEnchanted = 0;
        int maxProjectiles = 50;

        // Ammo base threat is 20/x2 → doubled to 19-20 → modifier = -1
        // (Most ammunition has 20/x2 crit profile)
        int baseThreatMin = 20;
        int threatRange = 21 - baseThreatMin; // 1
        int doubledRange = threatRange * 2;   // 2
        int newThreatMin = 21 - doubledRange; // 19
        int critModifier = newThreatMin - baseThreatMin; // -1

        foreach (var ammo in ammoStacks)
        {
            if (totalEnchanted >= maxProjectiles)
                break;

            // Use ammo-specific threat range if it has one
            int ammoBaseThreat = ammo.CritThreatMin > 0 ? ammo.CritThreatMin : 20;
            int ammoThreatRange = 21 - ammoBaseThreat;
            int ammoDoubledRange = ammoThreatRange * 2;
            int ammoNewThreatMin = 21 - ammoDoubledRange;
            int ammoCritModifier = ammoNewThreatMin - ammoBaseThreat;

            int toEnchant = Mathf.Min(ammo.Quantity, maxProjectiles - totalEnchanted);

            var effect = new ItemSpellEffect(spell.SpellId, spell.Name, casterName, casterLevel, durationRounds)
            {
                CritThreatRangeModifier = ammoCritModifier,
                EnchantedAmmoRemaining = toEnchant
            };

            ammo.AddOrReplaceItemSpellEffect(effect);
            totalEnchanted += toEnchant;
        }

        CombatUI?.ShowCombatLog($"<color=#88FFEE>🗡 {casterName} casts Keen Edge — {totalEnchanted} projectiles now have doubled threat range (19-20) [{durationRounds} rounds].</color>");
        Debug.Log($"[GameManager] Keen Edge (Ammo): {casterName} enchanted {totalEnchanted} projectiles with doubled threat range, CL {casterLevel}, {durationRounds} rounds");

        UpdateAllStatsUI();
        return true;
    }

    // ================================================================
    //  GREATER MAGIC WEAPON — PHB 3.5e p.251
    //  Transmutation. Clr 4, Pal 3, Sor/Wiz 3.
    //  +1 enhancement bonus per 4 CL (max +5).
    //  Duration: 1 hour/level.
    // ================================================================

    private static bool IsGreaterMagicWeaponSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.GREATER_MAGIC_WEAPON, StringComparison.Ordinal);
    }

    private ItemData _pendingGreaterMagicWeaponItem;

    private bool TryHandleGreaterMagicWeaponSelection(CharacterController caster, CharacterController target)
    {
        if (!IsGreaterMagicWeaponSpell(_pendingSpell))
        {
            _pendingGreaterMagicWeaponItem = null;
            return false;
        }

        if (target == null || target.Stats == null)
            return false;

        if (_pendingGreaterMagicWeaponItem != null)
            return false;

        if (!TryGetMagicWeaponInventoryOptions(target, out List<ItemData> weaponOptions, out List<string> weaponLabels))
        {
            CombatUI?.ShowCombatLog($"⚠ {target.Stats.CharacterName} has no weapon in inventory to enchant with Greater Magic Weapon.");
            _pendingSpell = null;
            _pendingGreaterMagicWeaponItem = null;
            ShowActionChoices();
            return true;
        }

        if (weaponOptions.Count == 1)
        {
            _pendingGreaterMagicWeaponItem = weaponOptions[0];
            return false;
        }

        CombatUI?.ShowPickUpItemSelection(
            actorName: caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster",
            itemOptions: weaponLabels,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= weaponOptions.Count)
                {
                    _pendingSpell = null;
                    _pendingGreaterMagicWeaponItem = null;
                    ShowActionChoices();
                    return;
                }

                _pendingGreaterMagicWeaponItem = weaponOptions[selectedIndex];
                PerformSpellCast(caster, target);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingGreaterMagicWeaponItem = null;
                ShowActionChoices();
            },
            titleOverride: "Greater Magic Weapon - Select Weapon",
            bodyOverride: $"Choose which weapon from {target.Stats.CharacterName}'s inventory to enchant.",
            optionButtonColorOverride: new Color(0.24f, 0.34f, 0.56f, 1f));
        return true;
    }

    private bool TryApplyGreaterMagicWeaponToPendingItem(CharacterController caster, CharacterController target, SpellData spell)
    {
        if (!IsGreaterMagicWeaponSpell(spell))
            return false;

        ItemData weapon = _pendingGreaterMagicWeaponItem;
        _pendingGreaterMagicWeaponItem = null;

        if (weapon == null)
        {
            CombatUI?.ShowCombatLog("⚠ Greater Magic Weapon failed: no weapon selected.");
            return true;
        }

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        int rounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;

        // Enhancement bonus = +1 per 4 CL, max +5
        int enhancementBonus = Mathf.Min(5, Mathf.Max(1, casterLevel / 4));

        var effect = new ItemSpellEffect(spell.SpellId, spell.Name, casterName, casterLevel, rounds)
        {
            BonusType = BonusType.Enhancement,
            EnhancementBonusAttack = enhancementBonus,
            EnhancementBonusDamage = enhancementBonus,
            CountsAsMagicForBypass = true
        };

        weapon.AddOrReplaceItemSpellEffect(effect);

        int effectiveAttackBonus = weapon.GetEnhancementAttackBonus();
        int effectiveDamageBonus = weapon.GetEnhancementDamageBonus();
        string recipientName = target != null && target.Stats != null ? target.Stats.CharacterName : "target";

        CombatUI?.ShowCombatLog($"<color=#88FFEE>✨ {spell.Name} enchants {recipientName}'s {weapon.Name}: +{enhancementBonus} enhancement for {effect.GetDurationDisplayString()} (CL {casterLevel}).</color>");
        CombatUI?.ShowCombatLog($"<color=#88FFEE>🗡 {weapon.Name} effective enhancement now +{Mathf.Max(effectiveAttackBonus, effectiveDamageBonus)} (attack +{effectiveAttackBonus}, damage +{effectiveDamageBonus}); counts as magic: yes.</color>");
        Debug.Log($"[GameManager] Greater Magic Weapon: {weapon.Name} +{enhancementBonus} enhancement, CL {casterLevel}, {rounds} rounds");

        UpdateAllStatsUI();
        return true;
    }

    // ================================================================
    //  HASTE — Transmutation Speed Buff (PHB p.239)
    // ================================================================

    /// <summary>
    /// Applies the Haste spell effect to a target.
    /// Per PHB p.239:
    ///   • +1 bonus on attack rolls
    ///   • +1 dodge bonus to AC and Reflex saves
    ///   • +30 ft. movement speed
    ///   • One extra attack at full BAB on full attack action
    ///   • Haste dispels and counters Slow
    /// Duration: 1 round/level
    /// </summary>
    private ActiveSpellEffect ApplyHasteBuff(CharacterController caster, CharacterController target, SpellData spell, SpellcastingComponent spellComp)
    {
        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null || spell == null)
            return null;

        StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
        if (recipientStatusMgr == null)
            recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        recipientStatusMgr.Init(recipient.Stats);

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        // If target has Slow, Haste dispels it
        if (recipient.HasActiveSlowEffect)
        {
            recipient.ClearSlowEffect();
            recipient.Stats.SlowAttackPenalty = 0;
            recipient.Stats.SlowACPenalty = 0;
            recipient.Stats.SlowReflexPenalty = 0;
            recipient.Stats.SlowSpeedMultiplier = 1f;

            // Remove Slow from StatusEffectManager
            recipientStatusMgr.RemoveEffectsBySpellId(SpellNames.SLOW);

            string casterName2 = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
            CombatUI?.ShowCombatLog($"<color=#88FF88>⚡ {casterName2}'s Haste dispels Slow on {recipient.Stats.CharacterName}!</color>");
        }

        ActiveSpellEffect effect = recipientStatusMgr.AddEffect(
            spell,
            caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name,
            casterLevel);

        if (effect != null)
        {
            // Apply Haste bonuses to stats
            recipient.Stats.HasteAttackBonus = 1;
            recipient.Stats.HasteACBonus = 1;
            recipient.Stats.HasteReflexBonus = 1;

            // Apply custom effect data for extra attack tracking
            recipient.ApplyHasteEffect(durationRounds, caster);

            SpellcastingComponent recipientSpellComp = recipient.GetComponent<SpellcastingComponent>();
            if (recipientSpellComp != null)
                recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

            string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
            bool selfCast = recipient == caster;
            string castLine = selfCast
                ? $"<color=#88FF88>⚡ {casterName} casts Haste on self!</color>"
                : $"<color=#88FF88>⚡ {casterName} casts Haste on {recipient.Stats.CharacterName}!</color>";

            CombatUI?.ShowCombatLog(castLine);
            CombatUI?.ShowCombatLog($"<color=#AAFFAA>   +1 attack, +1 dodge AC, +1 Reflex, +30 ft speed, extra attack on full attack</color>");
            CombatUI?.ShowCombatLog($"<color=#AAFFAA>   Duration: {durationRounds} rounds (CL {casterLevel})</color>");
        }

        UpdateAllStatsUI();
        return effect;
    }

    // ================================================================
    //  SLOW — Transmutation Speed Debuff (PHB p.280)
    // ================================================================

    /// <summary>
    /// Applies the Slow spell effect to a target.
    /// Per PHB p.280:
    ///   • -1 penalty on attack rolls, AC, and Reflex saves
    ///   • Movement speed halved (round down to nearest 5 ft)
    ///   • Can only take single move or standard action (no full-round actions)
    ///   • Slow counters and dispels Haste
    /// Duration: 1 round/level. Will negates. SR: Yes.
    /// </summary>
    private ActiveSpellEffect ApplySlowDebuff(CharacterController caster, CharacterController target, SpellData spell, SpellcastingComponent spellComp)
    {
        if (target == null || target.Stats == null || spell == null)
            return null;

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        int saveDc = GetSpellSaveDC(caster, spell);
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";

        CombatUI?.ShowCombatLog($"<color=#CC88FF>🐌 {casterName} casts Slow on {target.Stats.CharacterName}!</color>");

        // Spell Resistance check
        if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)
        {
            int srCheckRoll = DiceService.D20();
            int srCheckTotal = srCheckRoll + casterLevel;
            bool srOvercome = srCheckTotal >= target.Stats.SpellResistance;

            CombatUI?.ShowCombatLog($"  SR Check: d20({srCheckRoll}) + {casterLevel} = {srCheckTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");

            if (!srOvercome)
            {
                CombatUI?.ShowCombatLog($"<color=#AAAAFF>  {target.Stats.CharacterName} resists Slow via Spell Resistance!</color>");
                return null;
            }
        }

        // Will save
        if (spell.AllowsSavingThrow)
        {
            int saveRoll = DiceService.D20();
            int saveTotal = saveRoll + target.Stats.WillSave;
            bool saved = saveTotal >= saveDc;

            CombatUI?.ShowCombatLog($"  Will Save: d20({saveRoll}) + {target.Stats.WillSave} = {saveTotal} vs DC {saveDc} → {(saved ? "SAVED" : "FAILED")}");

            if (saved)
            {
                CombatUI?.ShowCombatLog($"<color=#88FF88>  {target.Stats.CharacterName} resists the Slow spell!</color>");
                return null;
            }
        }

        // If target has Haste, Slow dispels it
        StatusEffectManager targetStatusMgr = target.GetComponent<StatusEffectManager>();
        if (targetStatusMgr == null)
            targetStatusMgr = target.gameObject.AddComponent<StatusEffectManager>();
        targetStatusMgr.Init(target.Stats);

        if (target.HasActiveHasteEffect)
        {
            target.ClearHasteEffect();
            target.Stats.HasteAttackBonus = 0;
            target.Stats.HasteACBonus = 0;
            target.Stats.HasteReflexBonus = 0;

            targetStatusMgr.RemoveEffectsBySpellId(SpellNames.HASTE);

            CombatUI?.ShowCombatLog($"<color=#CC88FF>  🐌 Slow dispels Haste on {target.Stats.CharacterName}!</color>");
        }

        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        ActiveSpellEffect effect = targetStatusMgr.AddEffect(
            spell,
            casterName,
            casterLevel);

        if (effect != null)
        {
            // Apply Slow penalties to stats
            target.Stats.SlowAttackPenalty = -1;
            target.Stats.SlowACPenalty = -1;
            target.Stats.SlowReflexPenalty = -1;
            target.Stats.SlowSpeedMultiplier = 0.5f;

            // Apply custom effect data
            target.ApplySlowEffect(durationRounds, caster);

            SpellcastingComponent targetSpellComp = target.GetComponent<SpellcastingComponent>();
            if (targetSpellComp != null)
                targetSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

            CombatUI?.ShowCombatLog($"<color=#CC88FF>  {target.Stats.CharacterName} is Slowed!</color>");
            CombatUI?.ShowCombatLog($"<color=#DDAAFF>   -1 attack, -1 AC, -1 Reflex, half speed, no full-round actions</color>");
            CombatUI?.ShowCombatLog($"<color=#DDAAFF>   Duration: {durationRounds} rounds (CL {casterLevel})</color>");
        }

        UpdateAllStatsUI();
        return effect;
    }

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
        int casterLevel = caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
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
    //  ENERVATION — PHB p.226
    //  Necromancy. Sor/Wiz 4.
    //  Ranged touch attack. Subject gains 1d4 negative levels.
    //  No save. SR: Yes. Negative levels last CL hours, then fade
    //  (no save to avoid permanent drain — they just go away).
    // ================================================================

    private static bool IsEnervationSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.ENERVATION, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Enervation: ranged touch attack, 1d4 negative levels, no save.
    /// Negative levels persist for CL hours (converted to rounds for combat tracking).
    /// </summary>
    private bool TryResolveEnervationSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (!IsEnervationSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null)
            return true;

        // Ranged touch missed → no effect
        if (result.RequiredAttackRoll && !result.AttackHit)
        {
            CombatUI?.ShowCombatLog($"❌ Enervation ray misses {target.Stats.CharacterName}.");
            return true;
        }

        // Roll 1d4 negative levels
        int negativeLevels = UnityEngine.Random.Range(1, 5); // 1d4

        // Apply negative levels using existing system
        int newTotal = NegativeLevelSystem.ApplyNegativeLevels(target, negativeLevels, "Enervation");

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        // Duration = CL hours. In combat: 1 hour = 600 rounds (10 rounds/min × 60 min)
        int durationRounds = casterLevel * 600;

        // Track the effect for duration/expiry via StatusEffectManager
        if (target.StatusEffectManager != null)
        {
            string cName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Enervation";
            target.StatusEffectManager.AddEffect(spell, cName, casterLevel);
        }

        result.BuffApplied = true;
        result.BuffDescription = $"Debuff: {negativeLevels} negative level(s) for {casterLevel} hour(s).";

        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
        CombatUI?.ShowCombatLog($"<color=#9933CC>💀 {target.Stats.CharacterName} gains {negativeLevels} negative level{(negativeLevels > 1 ? "s" : "")} from Enervation!</color>");
        CombatUI?.ShowCombatLog($"<color=#AA77CC>   Each negative level: -1 attack/saves/skills, -5 HP, -1 effective level</color>");
        CombatUI?.ShowCombatLog($"<color=#AA77CC>   Duration: {casterLevel} hour{(casterLevel > 1 ? "s" : "")} ({durationRounds} rounds)</color>");

        // Check if target dies from negative levels (HD reduced to 0)
        if (NegativeLevelSystem.IsDeadFromNegativeLevels(target))
        {
            CombatUI?.ShowCombatLog($"<color=#FF3333>☠ {target.Stats.CharacterName} is slain by negative levels! (negative levels ≥ HD)</color>");
            result.TargetKilled = true;
        }

        Debug.Log($"[Enervation] {casterName} -> {target.Stats.CharacterName}: {negativeLevels} negative levels applied (total: {newTotal}), duration {casterLevel}h ({durationRounds} rounds)");
        return true;
    }

    // ================================================================
    //  CONTAGION — PHB p.213
    //  Necromancy [Evil]. Clr 3, Dru 3, Sor/Wiz 4.
    //  Melee touch attack. Target contracts a disease chosen by caster.
    //  Disease takes effect immediately (no incubation period).
    //  Fortitude negates. SR: Yes.
    // ================================================================

    private static bool IsContagionSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.CONTAGION, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// The list of diseases available for Contagion (PHB standard diseases).
    /// </summary>
    private static readonly DiseaseType[] ContagionDiseases = new[]
    {
        DiseaseType.BlindingSickness,
        DiseaseType.CackleFever,
        DiseaseType.FilthFever,
        DiseaseType.Mindfire,
        DiseaseType.RedAche,
        DiseaseType.Shakes,
        DiseaseType.SlimyDoom
    };

    /// <summary>
    /// Resolves Contagion: melee touch attack, Fort negates, applies disease immediately.
    /// For AI/NPC casters, a random disease is chosen. For PC casters, a random one is also
    /// selected (disease selection UI can be added later).
    /// </summary>
    private bool TryResolveContagionSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (!IsContagionSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null)
            return true;

        // Melee touch missed → no effect (charge held)
        if (result.RequiredAttackRoll && !result.AttackHit)
        {
            CombatUI?.ShowCombatLog($"❌ Contagion touch misses {target.Stats.CharacterName}.");
            return true;
        }

        // Fort save negates
        if (result.RequiredSave && result.SaveSucceeded)
        {
            CombatUI?.ShowCombatLog($"<color=#66CC66>🛡 {target.Stats.CharacterName} resists Contagion with a Fortitude save!</color>");
            return true;
        }

        // Check disease immunity
        if (target.Stats.IsImmuneToDisease())
        {
            CombatUI?.ShowCombatLog($"<color=#66CC66>🛡 {target.Stats.CharacterName} is immune to disease!</color>");
            return true;
        }

        // Select a disease — random for now (both PC and NPC casters)
        DiseaseType selectedType = ContagionDiseases[UnityEngine.Random.Range(0, ContagionDiseases.Length)];
        DiseaseData diseaseData = DiseaseDatabase.GetDisease(selectedType);

        if (diseaseData == null)
        {
            Debug.LogWarning($"[Contagion] Failed to find disease data for {selectedType}");
            return true;
        }

        // Create the active disease with NO incubation (Contagion's special property)
        ActiveDisease activeDisease = new ActiveDisease(diseaseData);
        activeDisease.DaysUntilActive = 0;
        activeDisease.IsIncubating = false;

        // Add to target's active diseases
        target.ActiveDiseases.Add(activeDisease);

        // Apply the first round of disease damage immediately
        if (diseaseData.DamageEffects != null && diseaseData.DamageEffects.Count > 0)
        {
            string damageReport = "";
            foreach (AbilityDamageEffect dmgEffect in diseaseData.DamageEffects)
            {
                int damage = dmgEffect.RollDamage();
                if (damage > 0)
                {
                    target.ApplyAbilityDamage(dmgEffect.Ability, damage, diseaseData.Name);
                    if (damageReport.Length > 0) damageReport += ", ";
                    damageReport += $"{damage} {dmgEffect.Ability} damage";
                }
            }

            if (damageReport.Length > 0)
            {
                CombatUI?.ShowCombatLog($"<color=#CC6633>   Immediate effect: {damageReport}</color>");
            }
        }

        result.BuffApplied = true;
        result.BuffDescription = $"Disease: {diseaseData.Name} contracted (immediate onset).";

        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
        CombatUI?.ShowCombatLog($"<color=#CC6633>🦠 {target.Stats.CharacterName} contracts {diseaseData.Name} from Contagion!</color>");
        CombatUI?.ShowCombatLog($"<color=#CC9966>   Fort DC {diseaseData.FortitudeDC} daily to resist. 2 consecutive saves = cured.</color>");

        // Check if ability damage killed the target
        target.CheckAbilityScoreZeroEffects();

        Debug.Log($"[Contagion] {casterName} -> {target.Stats.CharacterName}: contracted {diseaseData.Name} (immediate onset, DC {diseaseData.FortitudeDC})");
        return true;
    }

    // ================================================================
    //  BESTOW CURSE — PHB p.203
    //  Necromancy. Clr 3, Sor/Wiz 4.
    //  Melee touch attack. Will negates. SR: Yes.
    //  Duration: Permanent.
    //  Effects (choose one):
    //    • -6 penalty to one ability score (minimum 1)
    //    • -4 penalty on attack rolls, saves, ability checks, skill checks
    //    • 50% chance each turn the creature can't act normally
    // ================================================================

    private static bool IsBestowCurseSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.BESTOW_CURSE, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// The type of curse effect applied by Bestow Curse.
    /// </summary>
    public enum BestowCurseType
    {
        /// <summary>-6 penalty to one ability score (minimum 1).</summary>
        AbilityPenalty,
        /// <summary>-4 penalty on attack rolls, saves, ability checks, and skill checks.</summary>
        GeneralPenalty,
        /// <summary>50% chance each turn the creature can't act normally.</summary>
        ActionLoss
    }

    /// <summary>
    /// Resolves Bestow Curse: melee touch attack, Will negates, applies permanent curse.
    /// AI/NPC casters pick a random curse type. PC casters also get random for now
    /// (curse selection UI can be added later).
    /// </summary>
    private bool TryResolveBestowCurseSpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (!IsBestowCurseSpell(spell) || target == null || target.Stats == null)
            return false;

        if (result == null)
            return true;

        // Melee touch missed → no effect (charge held)
        if (result.RequiredAttackRoll && !result.AttackHit)
        {
            CombatUI?.ShowCombatLog($"❌ Bestow Curse touch misses {target.Stats.CharacterName}.");
            return true;
        }

        // Will save negates
        if (result.RequiredSave && result.SaveSucceeded)
        {
            CombatUI?.ShowCombatLog($"<color=#66CC66>🛡 {target.Stats.CharacterName} resists Bestow Curse with a Will save!</color>");
            return true;
        }

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";

        // Choose a random curse type
        BestowCurseType curseType = (BestowCurseType)UnityEngine.Random.Range(0, 3);
        string curseDescription = "";

        switch (curseType)
        {
            case BestowCurseType.AbilityPenalty:
            {
                // Pick a random ability score
                AbilityType[] abilities = { AbilityType.STR, AbilityType.DEX, AbilityType.CON, AbilityType.INT, AbilityType.WIS, AbilityType.CHA };
                AbilityType chosenAbility = abilities[UnityEngine.Random.Range(0, abilities.Length)];
                int penalty = 6;

                // Apply as ability damage (tracked, permanent until Remove Curse)
                target.ApplyAbilityDamage(chosenAbility, penalty, "Bestow Curse");
                curseDescription = $"-{penalty} {chosenAbility} penalty";

                CombatUI?.ShowCombatLog($"<color=#8B0000>🔮 {target.Stats.CharacterName} is cursed! {curseDescription} (permanent).</color>");
                CombatUI?.ShowCombatLog($"<color=#AA5555>   Ability reduced by {penalty} (minimum effective score of 1).</color>");
                break;
            }

            case BestowCurseType.GeneralPenalty:
            {
                // Apply -4 penalty on attacks, saves, ability checks, skill checks
                // Use the condition system to track this
                target.ApplyCondition(CombatConditionType.BestowCurseGeneralPenalty, -1, "Bestow Curse");
                curseDescription = "-4 on attacks, saves, ability checks, and skill checks";

                CombatUI?.ShowCombatLog($"<color=#8B0000>🔮 {target.Stats.CharacterName} is cursed! {curseDescription} (permanent).</color>");
                break;
            }

            case BestowCurseType.ActionLoss:
            {
                // 50% chance each turn the creature can't act
                target.ApplyCondition(CombatConditionType.BestowCurseActionLoss, -1, "Bestow Curse");
                curseDescription = "50% chance each turn to lose all actions";

                CombatUI?.ShowCombatLog($"<color=#8B0000>🔮 {target.Stats.CharacterName} is cursed! {curseDescription} (permanent).</color>");
                break;
            }
        }

        // Track via StatusEffectManager for UI/dispel
        if (target.StatusEffectManager != null)
        {
            target.StatusEffectManager.AddEffect(spell, casterName, casterLevel);
        }

        result.BuffApplied = true;
        result.BuffDescription = $"Debuff: Bestow Curse — {curseDescription}.";

        // Check if ability damage killed the target
        target.CheckAbilityScoreZeroEffects();

        Debug.Log($"[BestowCurse] {casterName} -> {target.Stats.CharacterName}: {curseDescription} (permanent)");
        return true;
    }

    // ================================================================
    //  GREATER INVISIBILITY — PHB p.245
    //  Illusion (Glamer). Brd 4, Sor/Wiz 4.
    //  Range: Personal or Touch.
    //  Duration: 1 round/level (D) — Dismissible.
    //  Like Invisibility but does NOT break when attacking.
    // ================================================================

    private static bool IsGreaterInvisibilitySpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.GREATER_INVISIBILITY, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Greater Invisibility: touch spell, applies invisibility that does NOT
    /// break on attack. Duration: 1 round/level. Dismissible.
    /// </summary>
    private bool TryResolveGreaterInvisibilitySpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (!IsGreaterInvisibilitySpell(spell) || caster == null || caster.Stats == null)
            return false;

        if (result == null)
            return true;

        // Determine recipient (self or touched ally)
        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null)
            return true;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, casterLevel); // 1 round/level
        string casterName = caster.Stats.CharacterName ?? "Unknown";

        // Create Greater Invisibility effect data — key difference: BreaksOnAttack = false
        InvisibilityEffectData effectData = InvisibilityEffectData.CreateGreaterInvisibility(durationRounds, caster);
        recipient.ApplyInvisibilityEffectData(effectData);

        // Track via StatusEffectManager
        if (recipient.StatusEffectManager != null)
        {
            recipient.StatusEffectManager.AddEffect(spell, casterName, casterLevel);
        }

        result.BuffApplied = true;
        result.BuffDescription = $"Buff: Greater Invisibility for {durationRounds} round(s). Does NOT break on attack.";

        CombatUI?.ShowCombatLog($"<color=#9966FF>✨ {recipient.Stats.CharacterName} becomes invisible (Greater Invisibility)!</color>");
        CombatUI?.ShowCombatLog($"<color=#AA88FF>   Duration: {durationRounds} round(s). Does NOT break on attack.</color>");
        CombatUI?.ShowCombatLog($"<color=#AA88FF>   +2 attack bonus, enemies denied Dex to AC, 50% miss chance.</color>");

        Debug.Log($"[GreaterInvisibility] {casterName} -> {recipient.Stats.CharacterName}: {durationRounds} rounds, breaksOnAttack=false");
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
        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
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
    //  FIRE SHIELD — Self Buff with Retribution Damage (PHB p.230)
    // ================================================================

    private static bool IsFireShieldSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.FIRE_SHIELD, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Fire Shield: self-buff with two modes (warm/chill).
    /// Warm Shield: 50% cold damage reduction, retribution 1d6+CL fire (max +15)
    /// Chill Shield: 50% fire damage reduction, retribution 1d6+CL cold (max +15)
    /// If the reduced damage source allowed a save for half, the protected
    /// character instead takes no damage from that source.
    /// Duration: 1 round/level (D). PHB p.230
    /// </summary>
    private bool TryResolveFireShieldSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (!IsFireShieldSpell(spell) || caster == null || caster.Stats == null)
            return false;

        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null)
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        // Use the player's shield-type choice; fall back to warm if not set (NPC casts)
        bool isWarmShield = _pendingFireShieldIsWarm ?? true;

        // Track via StatusEffectManager for duration and UI
        StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
        if (recipientStatusMgr == null)
            recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        recipientStatusMgr.Init(recipient.Stats);

        ActiveSpellEffect effect = recipientStatusMgr.AddEffect(
            spell,
            caster.Stats.CharacterName ?? spell.Name,
            casterLevel);

        // Store warm/chill flag and CL on the character for retribution + damage reduction
        recipient.Stats.FireShieldActive = true;
        recipient.Stats.FireShieldIsWarm = isWarmShield;
        recipient.Stats.FireShieldCasterLevel = casterLevel;
        recipient.Stats.FireShieldDurationRounds = durationRounds;

        string resistType = isWarmShield ? "cold" : "fire";
        string retribType = isWarmShield ? "fire" : "cold";
        string shieldName = isWarmShield ? "Warm Shield" : "Chill Shield";
        int maxBonus = 15;

        CombatUI?.ShowCombatLog($"🔥 {recipient.Stats.CharacterName} is wreathed in {(isWarmShield ? "warm" : "chill")} flames! (Fire Shield — {shieldName})");
        CombatUI?.ShowCombatLog($"  {resistType} damage reduced by 50% (0 if save-for-half)");
        CombatUI?.ShowCombatLog($"  Retribution: 1d6+{Mathf.Min(casterLevel, maxBonus)} {retribType} damage to melee attackers");
        CombatUI?.ShowCombatLog($"  Duration: {durationRounds} round(s)");

        Debug.Log($"[FireShield] {recipient.Stats.CharacterName}: {shieldName}, CL {casterLevel}, {durationRounds} rounds");

        // Clear the pending choice
        _pendingFireShieldIsWarm = null;

        return true;
    }

    /// <summary>
    /// Called when a character with Fire Shield is struck by a melee attack.
    /// Deals retribution damage (1d6 + CL, max +15) to the attacker.
    /// No save for retribution damage.
    /// </summary>
    public void ResolveFireShieldRetribution(CharacterController defender, CharacterController attacker)
    {
        if (defender == null || defender.Stats == null || !defender.Stats.FireShieldActive)
            return;
        if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead)
            return;

        // Check distance — must be within 5 ft (1 square)
        int distance = SquareGridUtils.GetDistance(defender.GridPosition, attacker.GridPosition);
        if (distance > 1)
            return;

        int casterLevel = defender.Stats.FireShieldCasterLevel;
        int clBonus = Mathf.Min(casterLevel, 15);
        int damage = UnityEngine.Random.Range(1, 7) + clBonus; // 1d6 + CL (max +15)

        bool isWarm = defender.Stats.FireShieldIsWarm;
        string dmgType = isWarm ? "fire" : "cold";

        attacker.Stats.TakeDamage(damage);

        CombatUI?.ShowCombatLog($"  🔥 Fire Shield retribution! {attacker.Stats.CharacterName} takes {damage} {dmgType} damage (no save)!");

        if (attacker.Stats.IsDead)
        {
            attacker.OnDeath();
            HandleSummonDeathCleanup(attacker);
            CombatUI?.ShowCombatLog($"  💀 {attacker.Stats.CharacterName} is slain by Fire Shield retribution!");
        }
    }

    // ================================================================
    //  ICE STORM — AoE Damage, No Save (PHB p.243)
    // ================================================================

    private static bool IsIceStormSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.ICE_STORM, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Ice Storm: 3d6 bludgeoning + 2d6 cold (no save), SR: Yes.
    /// Area becomes difficult terrain for 1 round (logged for awareness).
    /// PHB p.243
    /// </summary>
    private bool TryResolveIceStormAoE(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        if (!IsIceStormSpell(spell))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"❄ {caster.Stats.CharacterName} casts Ice Storm! (20-ft radius cylinder)");
        sb.AppendLine($"  [Level {spell.SpellLevel}] {spell.School}");
        sb.AppendLine($"  Damage: 3d6 bludgeoning + 2d6 cold (NO SAVE)");
        sb.AppendLine($"  SR: Yes | Area becomes icy difficult terrain for 1 round");
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
                    int srCheckRoll = UnityEngine.Random.Range(1, 21);
                    int srCheckTotal = srCheckRoll + casterLevel;
                    bool srOvercome = srCheckTotal >= target.Stats.SpellResistance;

                    sb.AppendLine($"  SR Check: d20({srCheckRoll}) + {casterLevel} = {srCheckTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");

                    if (!srOvercome)
                    {
                        sb.AppendLine($"  {target.Stats.CharacterName} resists Ice Storm via Spell Resistance!");
                        sb.AppendLine();
                        continue;
                    }
                }

                // Roll 3d6 bludgeoning
                int bludgeoningDamage = 0;
                for (int i = 0; i < 3; i++)
                    bludgeoningDamage += UnityEngine.Random.Range(1, 7);

                // Roll 2d6 cold
                int coldDamage = 0;
                for (int i = 0; i < 2; i++)
                    coldDamage += UnityEngine.Random.Range(1, 7);

                int totalDamage = bludgeoningDamage + coldDamage;

                // D&D 3.5e PHB p.206: Blinking creatures take half damage from area attacks
                bool targetIsBlinking = target.HasActiveBlinkEffect;
                if (targetIsBlinking)
                    totalDamage = Mathf.Max(1, totalDamage / 2);

                sb.AppendLine($"  Damage: {bludgeoningDamage} bludgeoning + {coldDamage} cold = {totalDamage} total (no save)");
                if (targetIsBlinking)
                    sb.AppendLine($"  Blink: area damage halved");

                int hpBefore = target.Stats.CurrentHP;
                target.Stats.TakeDamage(totalDamage);
                int hpAfter = target.Stats.CurrentHP;

                sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

                CheckConcentrationOnDamage(target, totalDamage);

                if (target.Stats.IsDead)
                {
                    target.OnDeath();
                    HandleSummonDeathCleanup(target);
                    sb.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");
                }

                sb.AppendLine();
            }
        }

        // ── WALL OF ICE DAMAGE FROM ICE STORM ──
        if (aoeCells != null && AreaEffectManager.HasInstance)
        {
            var wallsHit = new Dictionary<WallOfIceAreaEffect, bool>();
            foreach (Vector2Int cell in aoeCells)
            {
                WallOfIceAreaEffect wall = WallOfIceAreaEffect.GetWallAtCell(cell);
                if (wall != null && !wallsHit.ContainsKey(wall))
                    wallsHit[wall] = true;
            }

            foreach (var kvp in wallsHit)
            {
                WallOfIceAreaEffect wall = kvp.Key;
                if (wall == null || wall.WallHP <= 0)
                    continue;

                // 3d6 bludgeoning + 2d6 cold (no save for objects)
                int wallBludg = 0;
                for (int i = 0; i < 3; i++) wallBludg += UnityEngine.Random.Range(1, 7);
                int wallCold = 0;
                for (int i = 0; i < 2; i++) wallCold += UnityEngine.Random.Range(1, 7);
                int wallTotal = wallBludg + wallCold;

                sb.AppendLine($"  --- Wall of Ice ---");
                sb.AppendLine($"  Bludgeoning + cold damage to wall: {wallTotal}");

                bool destroyed = wall.DealDamageToWall(wallTotal, false);

                if (destroyed)
                    sb.AppendLine($"  💥 The Wall of Ice is destroyed by Ice Storm!");
                else
                    sb.AppendLine($"  Wall HP: {wall.WallHP}/{wall.WallMaxHP}");

                sb.AppendLine();
            }
        }

        // Note about difficult terrain
        sb.AppendLine("  ❄ Area is covered in ice (difficult terrain for 1 round)");
        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  SHOUT — Cone AoE Sonic Damage + Deafen (PHB p.275)
    // ================================================================

    private static bool IsShoutSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.SHOUT, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Shout: 5d6 sonic in 30-ft cone. Fort half.
    /// Failed save = deafened for 2d6 rounds. SR: Yes. PHB p.275
    /// </summary>
    private bool TryResolveShoutAoE(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        if (!IsShoutSpell(spell))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int saveDc = GetSpellSaveDC(caster, spell);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"📣 {caster.Stats.CharacterName} casts Shout! (30-ft cone)");
        sb.AppendLine($"  [Level {spell.SpellLevel}] {spell.School}");
        sb.AppendLine($"  Damage: 5d6 sonic | Fort DC {saveDc} for half");
        sb.AppendLine($"  Failed save: deafened for 2d6 rounds | SR: Yes");
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
                    int srCheckRoll = UnityEngine.Random.Range(1, 21);
                    int srCheckTotal = srCheckRoll + casterLevel;
                    bool srOvercome = srCheckTotal >= target.Stats.SpellResistance;

                    sb.AppendLine($"  SR Check: d20({srCheckRoll}) + {casterLevel} = {srCheckTotal} vs SR {target.Stats.SpellResistance} → {(srOvercome ? "OVERCAME SR" : "BLOCKED by SR")}");

                    if (!srOvercome)
                    {
                        sb.AppendLine($"  {target.Stats.CharacterName} resists Shout via Spell Resistance!");
                        sb.AppendLine();
                        continue;
                    }
                }

                // Roll 5d6 sonic damage
                int damage = 0;
                for (int i = 0; i < 5; i++)
                    damage += UnityEngine.Random.Range(1, 7);

                // Fortitude save
                int fortRoll = UnityEngine.Random.Range(1, 21);
                int fortMod = target.Stats.FortitudeSave;
                int fortTotal = fortRoll + fortMod;
                bool savePassed = fortTotal >= saveDc;

                bool deafened = false;
                int deafRounds = 0;

                if (savePassed)
                {
                    damage = Mathf.Max(1, damage / 2);
                }
                else
                {
                    // Failed save: deafened for 2d6 rounds
                    deafRounds = UnityEngine.Random.Range(1, 7) + UnityEngine.Random.Range(1, 7);
                    deafened = true;
                }

                // D&D 3.5e: Blinking creatures take half damage from area attacks
                bool targetIsBlinking = target.HasActiveBlinkEffect;
                if (targetIsBlinking)
                    damage = Mathf.Max(1, damage / 2);

                sb.AppendLine($"  Fort save: d20({fortRoll}) + {fortMod} = {fortTotal} vs DC {saveDc} → {(savePassed ? "SAVED (half)" : "FAILED (full + deafened)")}");
                if (targetIsBlinking)
                    sb.AppendLine($"  Blink: area damage halved");

                int hpBefore = target.Stats.CurrentHP;
                target.Stats.TakeDamage(damage);
                int hpAfter = target.Stats.CurrentHP;

                sb.AppendLine($"  Damage: {damage} sonic");
                sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

                if (deafened && !target.Stats.IsDead)
                {
                    target.ApplyCondition(CombatConditionType.Deafened, deafRounds, "Shout");
                    sb.AppendLine($"  🔇 {target.Stats.CharacterName} is DEAFENED for {deafRounds} rounds!");
                }

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

        // ── WALL OF ICE DAMAGE FROM SHOUT ──
        // Sonic damage is especially effective against crystalline/brittle objects.
        if (aoeCells != null && AreaEffectManager.HasInstance)
        {
            var wallsHit = new Dictionary<WallOfIceAreaEffect, bool>();
            foreach (Vector2Int cell in aoeCells)
            {
                WallOfIceAreaEffect wall = WallOfIceAreaEffect.GetWallAtCell(cell);
                if (wall != null && !wallsHit.ContainsKey(wall))
                    wallsHit[wall] = true;
            }

            foreach (var kvp in wallsHit)
            {
                WallOfIceAreaEffect wall = kvp.Key;
                if (wall == null || wall.WallHP <= 0)
                    continue;

                // 5d6 sonic (no save for objects)
                int wallDamage = 0;
                for (int i = 0; i < 5; i++) wallDamage += UnityEngine.Random.Range(1, 7);

                sb.AppendLine($"  --- Wall of Ice ---");
                sb.AppendLine($"  Sonic damage to wall: {wallDamage}");

                bool destroyed = wall.DealDamageToWall(wallDamage, false);

                if (destroyed)
                    sb.AppendLine($"  💥 The Wall of Ice is shattered by Shout!");
                else
                    sb.AppendLine($"  Wall HP: {wall.WallHP}/{wall.WallMaxHP}");

                sb.AppendLine();
            }
        }

        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    // ================================================================
    //  WALL OF FIRE — Persistent Area Effect (PHB p.298)
    // ================================================================

    private static bool IsWallOfFireSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.WALL_OF_FIRE, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Wall of Fire: creates a WallOfFireAreaEffect along a line.
    /// Per PHB p.298: Concentration + 1 round/level duration.
    /// </summary>
    private bool TryResolveWallOfFireSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        if (!IsWallOfFireSpell(spell))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        int saveDc = GetSpellSaveDC(caster, spell);

        // Wall length: up to 20 ft/level = 4 squares per CL
        int maxLengthSquares = Mathf.Max(2, casterLevel * 4);

        Vector3 centerPosition = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);
        Vector2Int centerCell = SquareGridUtils.WorldToGrid(centerPosition);
        Vector2Int direction = ComputeWindWallDirection(caster.GridPosition, centerCell);

        // Create the area effect
        GameObject wallObj = new GameObject("WallOfFire_Area");
        wallObj.transform.position = centerPosition;

        WallOfFireAreaEffect wallEffect = wallObj.AddComponent<WallOfFireAreaEffect>();
        wallEffect.CenterPosition = centerPosition;
        wallEffect.CenterCell = centerCell;
        wallEffect.LengthSquares = Mathf.Max(2, maxLengthSquares);
        wallEffect.WallDirection = direction == Vector2Int.zero ? new Vector2Int(1, 0) : direction;
        wallEffect.RoundsRemaining = Mathf.Max(1, durationRounds);
        wallEffect.CasterLevel = casterLevel;
        wallEffect.Caster = caster;
        wallEffect.SaveDC = saveDc;

        if (aoeCells != null && aoeCells.Count > 0)
            wallEffect.SetExplicitCells(aoeCells);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🔥 {caster.Stats.CharacterName} casts Wall of Fire!");
        sb.AppendLine($"  Wall: {maxLengthSquares} squares long");
        sb.AppendLine($"  Duration: {durationRounds} round(s)");
        sb.AppendLine($"  Save DC: {saveDc} (Reflex half for pass-through)");
        sb.AppendLine("  • 2d4 fire damage within 10 ft (near side)");
        sb.AppendLine("  • 1d4 fire damage within 10 ft (far side)");
        sb.AppendLine("  • 2d6+CL (max +20) fire to those passing through");
        sb.AppendLine("  • Opaque — 50% concealment");

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

    // ================================================================
    //  WALL OF ICE — Persistent Area Effect (PHB p.299)
    // ================================================================

    private static bool IsWallOfIceSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.WALL_OF_ICE, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Wall of Ice: creates a WallOfIceAreaEffect.
    /// Per PHB p.299: 1 inch thick/CL, hardness 0, 3 HP/inch.
    /// Duration 1 min/level. Trapped creatures take CL cold damage.
    /// </summary>
    private bool TryResolveWallOfIceSpell(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        if (!IsWallOfIceSpell(spell))
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        // Wall panels: one 10-ft square per CL = 2 squares per CL
        int maxLengthSquares = Mathf.Max(2, casterLevel * 2);

        Vector3 centerPosition = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);
        Vector2Int centerCell = SquareGridUtils.WorldToGrid(centerPosition);
        Vector2Int direction = ComputeWindWallDirection(caster.GridPosition, centerCell);

        // Create the area effect
        GameObject wallObj = new GameObject("WallOfIce_Area");
        wallObj.transform.position = centerPosition;

        WallOfIceAreaEffect wallEffect = wallObj.AddComponent<WallOfIceAreaEffect>();
        wallEffect.CenterPosition = centerPosition;
        wallEffect.CenterCell = centerCell;
        wallEffect.LengthSquares = Mathf.Max(2, maxLengthSquares);
        wallEffect.WallDirection = direction == Vector2Int.zero ? new Vector2Int(1, 0) : direction;
        wallEffect.RoundsRemaining = Mathf.Max(1, durationRounds);
        wallEffect.CasterLevel = casterLevel;
        wallEffect.Caster = caster;

        if (aoeCells != null && aoeCells.Count > 0)
            wallEffect.SetExplicitCells(aoeCells);

        int thickness = casterLevel;
        int wallHP = thickness * 3;

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"❄ {caster.Stats.CharacterName} casts Wall of Ice!");
        sb.AppendLine($"  Wall: {maxLengthSquares} squares long, {thickness} inch(es) thick");
        sb.AppendLine($"  HP: {wallHP} (Hardness 0, 3 HP per inch)");
        sb.AppendLine($"  Duration: {durationRounds} round(s)");
        sb.AppendLine("  • Blocks movement through wall cells");
        sb.AppendLine("  • Creatures caught in wall take CL cold damage");
        sb.AppendLine("  • Fire damage is especially effective");

        if (targets != null && targets.Count > 0)
        {
            sb.Append("  Caught in wall: ");
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

    // ═══════════════════════════════════════════════════════════════════
    // OTILUKE'S RESILIENT SPHERE  (PHB p.263)
    // Evocation [Force]
    // Level: Sor/Wiz 4
    // Range: Close (25 ft. + 5 ft./2 levels)
    // Effect: Stationary sphere of force centered on target location
    // Duration: 1 min./level (D)
    // Saving Throw: Reflex negates (if ANY creature saves, spell fails)
    // Spell Resistance: Yes
    //
    // A globe of shimmering force is anchored to a location on the grid.
    // Diameter = 1 foot × caster level → square grid area.
    // The sphere is STATIONARY — creatures inside can move within but not leave.
    // Nothing can pass through the sphere boundary, in or out.
    // The sphere is INDESTRUCTIBLE by normal means (no HP, no Hardness).
    // Only Disintegrate, Rod of Cancellation, Rod of Negation, or Dispel Magic can remove it.
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns true if the given spell is Otiluke's Resilient Sphere.
    /// </summary>
    private static bool IsResilientSphereSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.RESILIENT_SPHERE, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Resilient Sphere as a stationary area effect.
    /// Cast on target creature's position — sphere forms at that location.
    /// Reflex save for each creature in the area — if ANY creature saves, the
    /// entire spell fails (they dodge out before the sphere forms).
    /// Creates a ResilientSphereAreaEffect registered with AreaEffectManager.
    /// PHB p.263
    /// </summary>
    private bool TryResolveResilientSphereSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (!IsResilientSphereSpell(spell) || target == null || target.Stats == null)
            return false;

        if (caster == null || caster.Stats == null)
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        int saveDC = GetSpellSaveDC(caster, spell);

        // Calculate sphere grid size
        int diameterFeet = Mathf.Max(1, casterLevel);
        int squareSize = Mathf.Max(1, diameterFeet / 5);

        // Center the sphere on the target's position
        Vector2Int centerCell = target.GridPosition;
        Vector3 centerWorldPos = SquareGridUtils.GridToWorld(centerCell);

        // Calculate which cells the sphere will occupy (same algorithm as PersistentAreaEffect square)
        HashSet<Vector2Int> sphereCells = new HashSet<Vector2Int>();
        int startX = centerCell.x - (squareSize / 2);
        int startY = centerCell.y - (squareSize / 2);
        for (int x = 0; x < squareSize; x++)
        {
            for (int y = 0; y < squareSize; y++)
            {
                sphereCells.Add(new Vector2Int(startX + x, startY + y));
            }
        }

        // Check if target is already inside an existing sphere
        if (ResilientSphereAreaEffect.IsCharacterInAnySphere(target))
        {
            CombatUI?.ShowCombatLog($"<color=#AAAAAA>🔮 {target.Stats.CharacterName} is already enclosed in a Resilient Sphere.</color>");
            return true;
        }

        // Find all creatures in the sphere area for Reflex saves
        CharacterController[] allCharacters = FindObjectsOfType<CharacterController>();
        List<CharacterController> creaturesInArea = new List<CharacterController>();
        foreach (CharacterController ch in allCharacters)
        {
            if (ch == null || ch.Stats == null || ch.Stats.IsDead)
                continue;
            if (sphereCells.Contains(ch.GridPosition))
                creaturesInArea.Add(ch);
        }

        // Reflex saves: if ANY creature saves, the entire spell fails
        // (The save/SR check for the primary target was already done upstream,
        //  but we check additional creatures in the area here)
        // NOTE: The primary target's save was already handled by the spell pipeline.
        // If we got here, the primary target failed their save. Check other creatures.
        foreach (CharacterController creature in creaturesInArea)
        {
            // Skip the primary target — their save was already resolved upstream
            if (creature == target)
                continue;

            // SR check for additional creatures
            if (spell.SpellResistanceApplies && creature.Stats.SpellResistance > 0)
            {
                int srRoll = UnityEngine.Random.Range(1, 21);
                int srTotal = srRoll + casterLevel;
                if (srTotal < creature.Stats.SpellResistance)
                {
                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>🔮 {creature.Stats.CharacterName} resists the Resilient Sphere via Spell Resistance — sphere fails to form!</color>");
                    return true;
                }
            }

            // Reflex save for additional creatures
            int reflexSave = UnityEngine.Random.Range(1, 21) + creature.Stats.ReflexSave;
            if (reflexSave >= saveDC)
            {
                CombatUI?.ShowCombatLog($"<color=#AAAAAA>🔮 {creature.Stats.CharacterName} dodges the forming Resilient Sphere (Reflex {reflexSave} vs DC {saveDC}) — sphere fails!</color>");
                return true; // Spell consumed but sphere doesn't form
            }
        }

        // All creatures failed saves — create the sphere area effect
        GameObject sphereObj = new GameObject("ResilientSphere_Area");
        sphereObj.transform.position = centerWorldPos;

        ResilientSphereAreaEffect sphereEffect = sphereObj.AddComponent<ResilientSphereAreaEffect>();
        sphereEffect.CenterPosition = centerWorldPos;
        sphereEffect.CenterCell = centerCell;
        sphereEffect.RoundsRemaining = Mathf.Max(1, durationRounds);
        sphereEffect.CasterLevel = casterLevel;
        sphereEffect.Caster = caster;
        sphereEffect.SaveDC = saveDC;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";

        CombatUI?.ShowCombatLog($"<color=#44CCFF>🔮 A Resilient Sphere forms at {targetName}'s position!</color>");
        CombatUI?.ShowCombatLog($"  Cast by {casterName} (CL {casterLevel})");
        CombatUI?.ShowCombatLog($"  Sphere: {diameterFeet} ft diameter, {squareSize}×{squareSize} squares");
        CombatUI?.ShowCombatLog($"  Creatures enclosed: {creaturesInArea.Count}");
        CombatUI?.ShowCombatLog($"  Sphere is STATIONARY — creatures can move within but cannot leave");
        CombatUI?.ShowCombatLog($"  Nothing passes through the sphere boundary in either direction");
        CombatUI?.ShowCombatLog($"  Indestructible except by Disintegrate, Rod of Cancellation/Negation, or Dispel Magic");
        CombatUI?.ShowCombatLog($"  Duration: {durationRounds} round(s)");

        Debug.Log($"[ResilientSphere] Sphere created at ({centerCell.x},{centerCell.y}) by {casterName}: CL {casterLevel}, {squareSize}×{squareSize}, {durationRounds} rounds, {creaturesInArea.Count} creatures enclosed");

        return true;
    }

    /// <summary>
    /// Enum for destruction types that can break a Resilient Sphere.
    /// Note: Normal damage CANNOT destroy Resilient Sphere. Only special methods work.
    /// </summary>
    public enum SphereDestructionType
    {
        /// <summary>Disintegrate spell automatically destroys the sphere.</summary>
        Disintegrate,
        /// <summary>Rod of Cancellation touch automatically destroys the sphere.</summary>
        RodOfCancellation,
        /// <summary>Rod of Negation touch automatically destroys the sphere.</summary>
        RodOfNegation
    }

    /// <summary>
    /// Attempts to destroy a Resilient Sphere containing the target via special means.
    /// Now works on the area effect, not character state.
    /// </summary>
    public void TryDestroyResilientSphere(CharacterController target, SphereDestructionType destructionType)
    {
        if (target == null)
            return;

        ResilientSphereAreaEffect sphere = ResilientSphereAreaEffect.GetSphereContainingCharacter(target);
        if (sphere == null)
            return;

        string targetName = target.Stats != null ? target.Stats.CharacterName ?? "Unknown" : "Unknown";

        switch (destructionType)
        {
            case SphereDestructionType.Disintegrate:
                CombatUI?.ShowCombatLog($"<color=#FF4444>💥 Resilient Sphere at {targetName}'s position is destroyed by Disintegrate!</color>");
                Debug.Log($"[ResilientSphere] Destroyed by Disintegrate near {targetName}");
                sphere.ExpireEffect();
                break;

            case SphereDestructionType.RodOfCancellation:
                CombatUI?.ShowCombatLog($"<color=#FF4444>💥 Resilient Sphere at {targetName}'s position is destroyed by Rod of Cancellation!</color>");
                Debug.Log($"[ResilientSphere] Destroyed by Rod of Cancellation near {targetName}");
                sphere.ExpireEffect();
                break;

            case SphereDestructionType.RodOfNegation:
                CombatUI?.ShowCombatLog($"<color=#FF4444>💥 Resilient Sphere at {targetName}'s position is destroyed by Rod of Negation!</color>");
                Debug.Log($"[ResilientSphere] Destroyed by Rod of Negation near {targetName}");
                sphere.ExpireEffect();
                break;
        }
    }

    /// <summary>
    /// Removes the Resilient Sphere area effect containing the given character.
    /// Called by dispel system. Now works on area effects instead of character stats.
    /// </summary>
    public void ClearResilientSphereState(CharacterController target)
    {
        if (target == null)
            return;

        ResilientSphereAreaEffect sphere = ResilientSphereAreaEffect.GetSphereContainingCharacter(target);
        if (sphere == null)
            return;

        string targetName = target.Stats != null ? target.Stats.CharacterName ?? "Unknown" : "Unknown";
        CombatUI?.ShowCombatLog($"<color=#44CCFF>🔮 Resilient Sphere near {targetName} has been dispelled!</color>");
        Debug.Log($"[ResilientSphere] Area effect removed near {targetName}");

        sphere.ExpireEffect();
    }
}
