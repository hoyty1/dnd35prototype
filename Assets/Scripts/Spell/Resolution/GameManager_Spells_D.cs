// ============================================================================
//  GameManager_Spells_D.cs  —  Spell resolution: 'D' spells
//  (partial class GameManager)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DND35e.Identifiers;
using Random = UnityEngine.Random;

public partial class GameManager
{

    // ================================================================
    //  DEATH WARD  (PHB p.217)
    // ================================================================
    // Touch. 1 min/level. Immunity to death spells, death effects,
    // energy drain, and negative energy effects.

    private bool TryResolveDeathWardSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.DEATH_WARD) return false;
        if (caster == null || caster.Stats == null || target == null || target.Stats == null) return false;
        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(TryResolveDeathWardSpellEffect));
        if (!result.Success) return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel); // SpellDurationRules (SPL-002)

        target.Stats.DeathWardActive = true;
        target.Stats.DeathWardRoundsRemaining = durationRounds;

        var statusMgr = target.StatusEffectManager;
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null) effect.RemainingRounds = durationRounds;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Success("🛡✨", $"Death Ward! {casterName} wards {targetName} against death effects for {durationRounds} rounds."));
        Debug.Log($"[DeathWard] {casterName} -> {targetName}: duration {durationRounds} rounds");

        result.BuffApplied = true;
        result.BuffDescription = $"Death Ward ({durationRounds} rounds)";
        return true;
    }


    // ================================================================
    //  DIVINE POWER  (PHB p.224)
    // ================================================================
    // Personal. 1 round/level. +6 enhancement STR, +1 temp HP/level,
    // BAB equal to character level.

    private bool TryResolveDivinePowerSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.DIVINE_POWER) return false;
        if (caster == null || caster.Stats == null) return false;
        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(TryResolveDivinePowerSpellEffect));
        if (!result.Success) return true;

        // Personal spell — target is always caster
        target = caster;
        string casterName = caster.Stats.CharacterName ?? "Unknown";
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel); // SpellDurationRules (SPL-002)

        // +6 enhancement to STR
        int strBonus = 6;
        target.Stats.DivinePowerActive = true;
        target.Stats.DivinePowerRoundsRemaining = durationRounds;
        target.Stats.DivinePowerStrBonus = strBonus;
        target.Stats.STR += strBonus; // Enhancement bonus applied directly

        // +1 temp HP per caster level
        int tempHP = casterLevel;
        target.Stats.DivinePowerTempHP = tempHP;
        target.Stats.TempHP += tempHP;

        // BAB = character level (boost the difference)
        int currentBAB = target.Stats.BaseAttackBonus;
        int characterLevel = target.Stats.Level;
        int babBonus = Mathf.Max(0, characterLevel - currentBAB);
        target.Stats.DivinePowerBABBonus = babBonus;
        target.Stats.BaseAttackBonus += babBonus;

        var statusMgr = target.StatusEffectManager;
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null)
            {
                effect.RemainingRounds = durationRounds;
                // BAB is restored from DivinePowerBABBonus when the spell ends (EffectService); not an attack bonus.
            }
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Special("⚔✨", $"Divine Power! {casterName} gains +{strBonus} STR, +{tempHP} temp HP, BAB +{babBonus} for {durationRounds} rounds."));
        Debug.Log($"[DivinePower] {casterName}: STR+{strBonus}, tempHP+{tempHP}, BAB+{babBonus}, duration {durationRounds} rounds");

        result.BuffApplied = true;
        result.BuffDescription = $"Divine Power ({durationRounds} rounds)";
        return true;
    }


    // ================================================================
    //  DISMISSAL  (PHB p.222)
    // ================================================================
    // Close range. Will negates, against a special DC: the spell's save DC - the target's HD + the caster level
    // (PHB p.222; built on SpellSaveDCRules, SPL-001).
    // Sends an extraplanar creature back to its home plane.
    // In this prototype, we check for the "Extraplanar" or "Outsider"
    // creature type and kill/remove the creature on failed save.

    private bool TryResolveDismissalSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.DISMISSAL) return false;
        if (caster == null || caster.Stats == null || target == null || target.Stats == null) return false;
        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(TryResolveDismissalSpellEffect));
        if (!result.Success) return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);

        string creatureType = (target.Stats.CreatureType ?? "").Trim().ToLowerInvariant();
        bool isExtraplanar = creatureType == "outsider" || creatureType == "extraplanar";

        // Check if target is a summon (summoned creatures are always extraplanar)
        bool isSummon = IsSummonedCreature(target);

        if (!isExtraplanar && !isSummon)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("✦", $"Dismissal: {targetName} is not an extraplanar creature — spell has no effect."));
            return true;
        }

        // PHB p.222: a special Will save, DC = the spell's save DC - the creature's HD + your caster level
        // (SpellSaveDCRules gives the spell's DC: Clr 4 with WIS, Sor/Wiz 5 with INT or CHA; SPL-001, SPL-031).
        int spellDC = GetSpellSaveDC(caster, spell);
        int targetHD = TeamUtility.GetHitDice(target);
        int saveDC = spellDC - targetHD + casterLevel;
        var saveResult = RollSpellSave(caster, target, spell, SaveType.Will, saveDC);
        bool saveSuccess = saveResult.Saved;
        Debug.Log($"[Dismissal] DC {saveDC} = spell DC {spellDC} - {targetHD} HD + CL {casterLevel}");

        if (saveSuccess)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Success("🌀", $"Dismissal: {targetName} resists being sent home! (Will {saveResult.Total} vs DC {saveDC})"));
            Debug.Log($"[Dismissal] {casterName} -> {targetName}: Will save {saveResult.Total} vs DC {saveDC} — resisted");
            return true;
        }

        // Dismissed! Kill/remove the creature
        target.Stats.TakeDamage(target.Stats.CurrentHP + 100); // Ensure death
        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🌀✨", $"Dismissal! {casterName} sends {targetName} back to its home plane!"));
        Debug.Log($"[Dismissal] {casterName} -> {targetName}: dismissed (Will {saveResult.Total} vs DC {saveDC})");

        result.TargetKilled = true;
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

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel);

        Vector3 centerPosition = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);
        CreateDaylightArea(centerPosition, durationRounds, casterLevel, caster, footprint: aoeCells);

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
    public void CreateDaylightArea(Vector3 centerPosition, int durationRounds, int casterLevel, CharacterController caster, HashSet<Vector2Int> footprint = null)
    {
        GameObject daylightObject = new GameObject("Daylight_Area");
        daylightObject.transform.position = centerPosition;

        DaylightAreaEffect daylight = daylightObject.AddComponent<DaylightAreaEffect>();
        daylight.CenterPosition = centerPosition;
        daylight.CastFootprint = footprint != null ? new HashSet<Vector2Int>(footprint) : null; // the cast's squares (batch 11 step 24)
        daylight.RoundsRemaining = Mathf.Max(1, durationRounds);
        daylight.CasterLevel = Mathf.Max(1, casterLevel);
        daylight.Caster = caster;
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

        StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
        if (recipientStatusMgr == null)
            recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        recipientStatusMgr.Init(recipient.Stats);

        int casterLevel = CasterLevelRules.ForCaster(caster, spell);
        ActiveSpellEffect effect = recipientStatusMgr.AddEffect(
            spell,
            caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name,
            casterLevel);

        if (effect != null)
        {
            SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
            if (recipientSpellComp != null)
                recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

            string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
            bool selfCast = recipient == caster;
            string castLine = selfCast
                ? $"<color=#88FFEE>👻 {casterName} casts Displacement on self!</color>"
                : $"<color=#88FFEE>👻 {casterName} casts Displacement on {recipient.Stats.CharacterName}!</color>";

            CombatUI?.ShowCombatLog(castLine);
            CombatUI?.ShowCombatLog(CombatLogHelper.IceBlue("", $"  {recipient.Stats.CharacterName}'s outline shimmers and shifts about 2 ft from its true position."));
            CombatUI?.ShowCombatLog(CombatLogHelper.IceBlue("", $"  Attacks against {recipient.Stats.CharacterName} have 50% miss chance ({effect.GetDurationDisplayString()}, {Mathf.Max(0, effect.RemainingRounds)} rounds). True Seeing negates."));
        }

        UpdateAllStatsUI();
        return effect;
    }


    // ================================================================
    // DIMENSIONAL ANCHOR — PHB p.221
    // Abjuration. Cleric 4, Sor/Wiz 4. V, S.
    // Range: Medium (100 ft + 10 ft/level). Duration: 1 min/level.
    // Ranged touch attack ray. No save. SR: Yes.
    // Blocks all extradimensional travel on hit.
    // ================================================================

    private static bool IsDimensionalAnchorSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.DIMENSIONAL_ANCHOR, System.StringComparison.Ordinal);
    }


    // ================================================================
    //  DEATH KNELL — PHB p.217
    //  Necromancy. Cleric 2. V, S.
    //  Range: Touch. Target: Living creature with -1 or fewer HP.
    //  Duration: Instantaneous / 10 min per HD of subject.
    //  Saving Throw: Will negates. Spell Resistance: Yes.
    //  Kills dying creature. Caster gains 1d8 temp HP, +2 STR, +1 CL
    //  for 10 min/HD of slain creature.
    // ================================================================

    /// <summary>
    /// Resolves Death Knell. Called from ApplySpellBuff when the target
    /// has already failed their Will save and the spell hits.
    /// The target should be a dying creature (HP <= -1).
    /// </summary>
    private bool TryResolveDeathKnellSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.DEATH_KNELL)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(TryResolveDeathKnellSpellEffect));

        // Per PHB: target must be a living creature with -1 or fewer HP
        // In combat prototype, we check if target is dying (HP <= -1 and not dead)
        int targetHP = target.Stats.CurrentHP;
        bool isDying = targetHP <= -1 && targetHP > -10;

        if (!isDying)
        {
            // Target is not dying — spell has no valid target
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("☠", $"Death Knell fizzles — {target.Stats.CharacterName} is not dying (HP: {targetHP})."));
            Debug.Log($"[DeathKnell] Target {target.Stats.CharacterName} HP={targetHP}, not in dying range (-1 to -9)");
            return true; // Handled, but no effect
        }

        // Kill the target
        target.Stats.CurrentHP = -10;
        Debug.Log($"[DeathKnell] {target.Stats.CharacterName} killed by Death Knell (HP set to -10)");

        // Calculate buff duration: 10 min/HD of slain creature
        // 10 minutes = 100 rounds; per HD of the killed creature
        int targetHD = target.Stats.GetHitDice();
        // 10 min * 10 rounds/min * HD, doubled by Extend (the timed part of "Instantaneous/10 minutes per HD", PHB p.217;
        // SpellData.HasTimedAftereffect, delegated RAW reading 2026-10-14; batch 11 step 19, SPL-038)
        int buffRounds = SpellDurationRules.Extended(spell, targetHD * 100);

        // Caster gains 1d8 temporary HP, +2 enhancement to STR and +1 caster level. A second Death Knell while the first
        // lasts refreshes it and adds no more STR (the same spell's enhancement bonus does not stack; SPL-003).
        int tempHP = SpellDiceRules.RollFor(spell, SpellDice.Of(1, 8), "Death Knell temp HP 1d8"); // the cast's metamagic (batch 11 step 19)
        int cl = CasterLevelRules.ForCaster(caster, spell);
        bool newlyApplied = EffectService.ApplyDeathKnellBonus(caster, spell, cl, buffRounds, tempHP);
        if (!newlyApplied)
            Debug.Log($"[DeathKnell] {caster.Stats.CharacterName} already has Death Knell: duration and temp HP refreshed, no extra STR");

        CombatUI?.ShowCombatLog(CombatLogHelper.Color($"☠ Death Knell! {caster.Stats.CharacterName} kills {target.Stats.CharacterName} and gains {tempHP} temp HP, +2 STR, +1 caster level for {buffRounds} rounds ({targetHD * 10} minutes)!", "CC33FF"));
        Debug.Log($"[DeathKnell] {caster.Stats.CharacterName} gains {tempHP} temp HP, +2 STR, +1 CL for {buffRounds} rounds");

        return true;
    }

    // ================================================================
    //  DIMENSION DOOR — PHB p.221
    //  Conjuration (Teleportation). Brd 4, Sor/Wiz 4.
    //  V only (no somatic or material component).
    //  Range: Long (400 ft + 40 ft/level).
    //  Duration: Instantaneous.
    //  No save (for caster). No SR (for caster).
    //  Instantly transports caster to a chosen location within range.
    //  After dimension door, no other actions until next turn.
    //  Blocked by Dimensional Anchor (TeleportationBlocker).
    // ================================================================

    private static bool IsDimensionDoorSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.DIMENSION_DOOR, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Dimension Door (PHB p.221): self-target teleportation spell. Checks TeleportationBlocker, moves the caster
    /// to the square it chose (CastChoices.Destination, which the cast's decider filled: the UI decider and the AI decider
    /// both through <see cref="ChooseDimensionDoorDestination"/>; batch 11 step 27, SPL-091), after testing that the
    /// square is still within range and free, and ends the caster's turn (no further actions). The same for every caster:
    /// the handler never asks who controls the caster.
    /// </summary>
    private bool TryResolveDimensionDoorSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (!IsDimensionDoorSpell(spell) || caster == null || caster.Stats == null)
            return false;

        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(TryResolveDimensionDoorSpellEffect));

        if (result == null)
            return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";

        // ── Check TeleportationBlocker ──
        if (TeleportationBlocker.IsBlocked(caster))
        {
            string reason = TeleportationBlocker.GetBlockedReason(caster, TeleportationType.Teleportation)
                ?? "Extradimensional travel is blocked";

            CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("🚫", $"{casterName} attempts Dimension Door, but {reason}!"));
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", "  The spell fizzles. The spell slot is consumed."));

            // Increment blocked counter on Dimensional Anchor
            DimensionalAnchorEffectData anchor = caster.Stats.ActiveDimensionalAnchorEffect;
            if (anchor != null)
                anchor.AttemptsBlocked++;

            result.BuffApplied = false;
            result.BuffDescription = $"Dimension Door blocked: {reason}";
            Debug.Log($"[DimensionDoor] {casterName} blocked by TeleportationBlocker");
            return true;
        }

        // ── Calculate range ──
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int rangeSquares = DimensionDoorRangeSquares(spell, casterLevel);

        // ── The caster's chosen destination ──
        SquareGrid grid = Grid;
        if (grid == null)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("❌", "Dimension Door fails: no grid available."));
            return true;
        }

        Vector2Int casterPos = caster.GridPosition;
        int casterSize = caster.GetVisualSquaresOccupied();

        // The square the cast's decider chose (PHB p.221: the caster chooses; batch 11 step 27): it must still be within
        // range and free when the spell takes effect.
        CastContext cast = CurrentCast;
        Vector2Int? destination = cast != null && cast.Request.Choices != null ? cast.Request.Choices.Destination : null;
        if (destination.HasValue
            && (SquareGridUtils.ChebyshevDistance(casterPos, destination.Value) > rangeSquares
                || !grid.CanPlaceCreature(destination.Value, casterSize, caster)))
            destination = null;

        if (!destination.HasValue)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("❌", $"{casterName}'s Dimension Door fails — no valid destination found within range!"));
            result.BuffApplied = false;
            result.BuffDescription = "Dimension Door failed: no valid destination.";
            Debug.Log($"[DimensionDoor] {casterName}: no valid destination found within {rangeSquares} squares");
            return true;
        }

        Vector2Int dest = destination.Value;
        int distance = SquareGridUtils.ChebyshevDistance(casterPos, dest);

        // ── Perform teleportation ──
        // Clear old occupancy
        grid.ClearCreatureOccupancy(caster);

        // Update position
        caster.GridPosition = dest;
        grid.SetCreatureOccupancy(caster, dest, casterSize);
        if (casterPos != dest)
            ScenarioHooks.Moved?.Invoke(caster, casterPos, dest, "teleport");

        // Update visual position
        Vector3 worldPos = grid.GetCenteredWorldPosition(dest, casterSize);
        caster.transform.position = worldPos;

        // ── D&D 3.5e: After dimension door, you can't take any other actions until next turn ──
        caster.Actions.StandardActionUsed = true;
        caster.Actions.MoveActionUsed = true;
        caster.Actions.FullRoundActionUsed = true;
        caster.Actions.SwiftActionUsed = true;

        // ── Combat log ──
        int distanceFeet = distance * 5;
        CombatUI?.ShowCombatLog(CombatLogHelper.Color($"🌀 {casterName} vanishes in a flash and reappears {distanceFeet} ft away!", "00CCFF"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Color($"   Dimension Door: teleported from ({casterPos.x},{casterPos.y}) to ({dest.x},{dest.y}). No further actions this turn.", "66DDFF"));

        result.BuffApplied = true;
        result.BuffDescription = $"Dimension Door: teleported {distanceFeet} ft. Turn ends.";

        Debug.Log($"[DimensionDoor] {casterName} teleported from ({casterPos.x},{casterPos.y}) to ({dest.x},{dest.y}), distance={distance} sq ({distanceFeet} ft), CL={casterLevel}, maxRange={rangeSquares} sq");

        UpdateAllStatsUI();
        return true;
    }

    /// <summary>The range of Dimension Door in squares at <paramref name="casterLevel"/> (long range, PHB p.221, p.174).</summary>
    private static int DimensionDoorRangeSquares(SpellData spell, int casterLevel)
    {
        int rangeSquares = spell != null ? spell.GetRangeSquaresForCasterLevel(casterLevel) : 0;
        if (rangeSquares <= 0) rangeSquares = 80 + (casterLevel * 8); // fallback: 400ft + 40ft/level = 80sq + 8sq/level
        return rangeSquares;
    }

    /// <summary>
    /// The destination Dimension Door's caster chooses (PHB p.221; batch 11 step 27, SPL-091): the escape square
    /// (<see cref="FindDimensionDoorEscapeDestination"/>) within the spell's range at the cast's caster level. Both
    /// deciders call it to fill CastChoices.Destination: the UI decider for the player (the square the game picked for a
    /// PC before; a square picker is UI-053) and the AI decider, which casts Dimension Door to escape
    /// (AISpellcastingStrategist.GetEscapeScore). Null when no square is free.
    /// </summary>
    internal Vector2Int? ChooseDimensionDoorDestination(CharacterController caster, SpellData spell)
        => ChooseDimensionDoorDestination(caster, spell, FlightRules.IsOnChosenFlight(caster, out TurnCompulsion flight) ? flight.FlightSources : null);

    /// <summary>
    /// <see cref="ChooseDimensionDoorDestination(CharacterController, SpellData)"/> that keeps only squares farther from
    /// every creature in <paramref name="fleeFrom"/> than the caster stands (a chosen flight's Escape spell, D15, batch 13
    /// step 20: FlightRules.EndsFartherFromAll); null keeps every square.
    /// </summary>
    internal Vector2Int? ChooseDimensionDoorDestination(CharacterController caster, SpellData spell, System.Collections.Generic.IList<CharacterController> fleeFrom)
    {
        if (caster == null || caster.Stats == null || spell == null || Grid == null)
            return null;
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        return FindDimensionDoorEscapeDestination(caster, caster.GridPosition, DimensionDoorRangeSquares(spell, casterLevel),
            caster.GetVisualSquaresOccupied(), Grid, fleeFrom);
    }

    /// <summary>
    /// The escape square: a free square within range, at least 2 squares away, that maximizes the distance from the
    /// nearest foe (team allegiance, TeamUtility.IsEnemy) and prefers one beside no foe. Null when there is none.
    /// </summary>
    private Vector2Int? FindDimensionDoorEscapeDestination(
        CharacterController caster, Vector2Int casterPos, int maxRange,
        int casterSize, SquareGrid grid, System.Collections.Generic.IList<CharacterController> fleeFrom = null)
    {
        // Find nearest enemy
        Vector2Int? nearestEnemyPos = null;
        int nearestDist = int.MaxValue;

        var allChars = GetAllCharacters();
        if (allChars != null)
        {
            foreach (var ch in allChars)
            {
                if (ch == null || ch == caster || ch.Stats == null || ch.Stats.IsDead) continue;
                if (!TeamUtility.IsEnemy(caster, ch)) continue; // not a foe (team allegiance, not control)

                int dist = SquareGridUtils.ChebyshevDistance(casterPos, ch.GridPosition);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestEnemyPos = ch.GridPosition;
                }
            }
        }

        Vector2Int? bestDest = null;
        int bestScore = int.MinValue;

        // Scan grid for valid positions within range
        int scanRange = Mathf.Min(maxRange, 30); // Cap scan to reasonable radius
        for (int dx = -scanRange; dx <= scanRange; dx++)
        {
            for (int dy = -scanRange; dy <= scanRange; dy++)
            {
                Vector2Int candidate = new Vector2Int(casterPos.x + dx, casterPos.y + dy);
                int distFromCaster = SquareGridUtils.ChebyshevDistance(casterPos, candidate);

                if (distFromCaster > maxRange || distFromCaster < 2) continue; // at least 2 squares away
                if (!grid.CanPlaceCreature(candidate, casterSize, caster)) continue;
                if (fleeFrom != null && !FlightRules.EndsFartherFromAll(casterPos, candidate, fleeFrom)) continue;

                // Score: maximize distance from nearest enemy
                int score = 0;
                if (nearestEnemyPos.HasValue)
                {
                    score = SquareGridUtils.ChebyshevDistance(candidate, nearestEnemyPos.Value) * 10;
                }
                else
                {
                    score = distFromCaster; // no enemy, just go far
                }

                // Prefer positions not adjacent to any enemy
                bool adjacentToEnemy = false;
                if (allChars != null)
                {
                    foreach (var ch in allChars)
                    {
                        if (ch == null || ch == caster || ch.Stats == null || ch.Stats.IsDead) continue;
                        if (!TeamUtility.IsEnemy(caster, ch)) continue;
                        if (SquareGridUtils.ChebyshevDistance(candidate, ch.GridPosition) <= 1)
                        {
                            adjacentToEnemy = true;
                            break;
                        }
                    }
                }
                if (!adjacentToEnemy) score += 50;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDest = candidate;
                }
            }
        }

        return bestDest;
    }

    private bool TryResolveDimensionalAnchorSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (!IsDimensionalAnchorSpell(spell) || target == null || target.Stats == null)
            return false;

        if (caster == null || caster.Stats == null)
            return false;

        CastStageTrace.Mark(caster, spell, CastStageTrace.Handler, nameof(TryResolveDimensionalAnchorSpellEffect));

        // Ranged touch attack must have hit
        if (result == null || !result.Success || (result.RequiredAttackRoll && !result.AttackHit))
        {
            // Miss — combat log already handled by the standard miss path
            return true; // We claimed this spell, even though it missed
        }

        // Check if target already has Dimensional Anchor — refresh duration if so
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel);

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";

        // Apply the effect data
        DimensionalAnchorEffectData effectData = DimensionalAnchorEffectData.Create(
            casterLevel, casterName, durationRounds);

        target.Stats.ActiveDimensionalAnchorEffect = effectData;

        // Add tracked spell effect for duration management
        StatusEffectManager statusMgr = target.StatusEffectManager;
        if (statusMgr != null)
        {
            ActiveSpellEffect spellEffect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (spellEffect != null)
                spellEffect.RemainingRounds = durationRounds;
        }

        // Combat log with emerald/green styling
        CombatUI?.ShowCombatLog(CombatLogHelper.Success("🟢", $"{casterName}'s green ray strikes {targetName}! A shimmering emerald field envelops {targetName}, blocking all extradimensional travel for {durationRounds} rounds ({durationRounds / 10} minutes)."));

        Debug.Log($"[DimensionalAnchor] Applied to {targetName} by {casterName} " +
                  $"(CL {casterLevel}, {durationRounds} rounds). " +
                  $"Blocks: {effectData.BlockedTypes}");

        UpdateAllStatsUI();
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    // EVARD'S BLACK TENTACLES (PHB p.228)
    // ═══════════════════════════════════════════════════════════════

    private static bool IsBlackTentaclesSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.EVARDS_BLACK_TENTACLES, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Evard's Black Tentacles as an AoE area effect.
    /// Creates a 20-ft radius spread of grappling tentacles at the targeted location.
    /// PHB p.228
    /// </summary>
    public bool TryResolveBlackTentaclesAoECast(
        CharacterController caster, SpellData spell,
        HashSet<Vector2Int> aoeCells, out string log)
    {
        log = string.Empty;

        if (!IsBlackTentaclesSpell(spell) || caster == null || caster.Stats == null || aoeCells == null)
            return false;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = SpellCastingHelper.CalculateDuration(spell, casterLevel); // SpellDurationRules (SPL-002)
        int tentacleGrappleMod = casterLevel + 8; // CL + Str mod (4) + Large size (4)

        Vector3 centerWorldPos = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);

        // Create area effect
        GameObject tentaclesObj = new GameObject("BlackTentacles_Area");
        tentaclesObj.transform.position = centerWorldPos;

        BlackTentaclesAreaEffect tentaclesEffect = tentaclesObj.AddComponent<BlackTentaclesAreaEffect>();
        tentaclesEffect.Metamagic = SpellDiceRules.MetamagicOf(spell); // the cast's Empower and Maximize for the crush (batch 11 step 19)
        tentaclesEffect.CenterPosition = centerWorldPos;
        tentaclesEffect.CastFootprint = new HashSet<Vector2Int>(aoeCells); // the cast's squares (batch 11 step 24)
        tentaclesEffect.RoundsRemaining = durationRounds;
        tentaclesEffect.CasterLevel = casterLevel;
        tentaclesEffect.Caster = caster;

        string casterName = caster.Stats.CharacterName ?? "Unknown";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"✨ {casterName} casts Evard's Black Tentacles!");
        sb.AppendLine($"  Area: 20-ft radius spread ({aoeCells.Count} squares)");
        sb.AppendLine($"  Tentacle grapple modifier: +{tentacleGrappleMod} (CL {casterLevel} + 8)");
        sb.AppendLine($"  Damage: 1d6+4 bludgeoning per round (grappled creatures)");
        sb.AppendLine($"  Duration: {durationRounds} round(s)");
        sb.AppendLine("═══════════════════════════════════");

        log = sb.ToString();

        Debug.Log($"[BlackTentacles] Created by {casterName}: CL {casterLevel}, grapple +{tentacleGrappleMod}, {durationRounds} rounds, {aoeCells.Count} cells");

        return true;
    }


}
