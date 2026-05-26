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
        if (!result.Success) return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = casterLevel * 10; // 1 min/level = 10 rounds/level

        target.Stats.DeathWardActive = true;
        target.Stats.DeathWardRoundsRemaining = durationRounds;

        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null) effect.RemainingRounds = durationRounds;
        }

        CombatUI?.ShowCombatLog($"<color=#CCFFCC>🛡✨ Death Ward! {casterName} wards {targetName} against death effects for {durationRounds} rounds.</color>");
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
        if (!result.Success) return true;

        // Personal spell — target is always caster
        target = caster;
        string casterName = caster.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = casterLevel; // 1 round/level

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

        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null)
            {
                effect.RemainingRounds = durationRounds;
                effect.AppliedAttackBonus = babBonus; // Track for reversal
            }
        }

        CombatUI?.ShowCombatLog($"<color=#FFD700>⚔✨ Divine Power! {casterName} gains +{strBonus} STR, +{tempHP} temp HP, BAB +{babBonus} for {durationRounds} rounds.</color>");
        Debug.Log($"[DivinePower] {casterName}: STR+{strBonus}, tempHP+{tempHP}, BAB+{babBonus}, duration {durationRounds} rounds");

        result.BuffApplied = true;
        result.BuffDescription = $"Divine Power ({durationRounds} rounds)";
        return true;
    }


    // ================================================================
    //  DISMISSAL  (PHB p.222)
    // ================================================================
    // Close range. Will save negates (–5 penalty for extraplanar creatures).
    // Sends an extraplanar creature back to its home plane.
    // In this prototype, we check for the "Extraplanar" or "Outsider"
    // creature type and kill/remove the creature on failed save.

    private bool TryResolveDismissalSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.DISMISSAL) return false;
        if (caster == null || caster.Stats == null || target == null || target.Stats == null) return false;
        if (!result.Success) return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));

        string creatureType = (target.Stats.CreatureType ?? "").Trim().ToLowerInvariant();
        bool isExtraplanar = creatureType == "outsider" || creatureType == "extraplanar";

        // Check if target is a summon (summoned creatures are always extraplanar)
        bool isSummon = IsSummonedCreature(target);

        if (!isExtraplanar && !isSummon)
        {
            CombatUI?.ShowCombatLog($"<color=#AAAAAA>  ✦ Dismissal: {targetName} is not an extraplanar creature — spell has no effect.</color>");
            return true;
        }

        // Will save with -5 penalty (D&D 3.5e)
        int saveDC = 10 + 4 + caster.Stats.WISMod;
        int saveRoll = Random.Range(1, 21) + target.Stats.WillSave - 5;
        bool saveSuccess = saveRoll >= saveDC;

        if (saveSuccess)
        {
            CombatUI?.ShowCombatLog($"<color=#88FF88>🌀 Dismissal: {targetName} resists being sent home! (Will {saveRoll} vs DC {saveDC})</color>");
            Debug.Log($"[Dismissal] {casterName} -> {targetName}: Will save {saveRoll} vs DC {saveDC} — resisted");
            return true;
        }

        // Dismissed! Kill/remove the creature
        target.Stats.TakeDamage(target.Stats.CurrentHP + 100); // Ensure death
        CombatUI?.ShowCombatLog($"<color=#FF8800>🌀✨ Dismissal! {casterName} sends {targetName} back to its home plane!</color>");
        Debug.Log($"[Dismissal] {casterName} -> {targetName}: dismissed (Will {saveRoll} vs DC {saveDC})");

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

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
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

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
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

        // Per PHB: target must be a living creature with -1 or fewer HP
        // In combat prototype, we check if target is dying (HP <= -1 and not dead)
        int targetHP = target.Stats.CurrentHP;
        bool isDying = targetHP <= -1 && targetHP > -10;

        if (!isDying)
        {
            // Target is not dying — spell has no valid target
            CombatUI?.ShowCombatLog($"<color=#FF6666>☠ Death Knell fizzles — {target.Stats.CharacterName} is not dying (HP: {targetHP}).</color>");
            Debug.Log($"[DeathKnell] Target {target.Stats.CharacterName} HP={targetHP}, not in dying range (-1 to -9)");
            return true; // Handled, but no effect
        }

        // Kill the target
        target.Stats.CurrentHP = -10;
        Debug.Log($"[DeathKnell] {target.Stats.CharacterName} killed by Death Knell (HP set to -10)");

        // Calculate buff duration: 10 min/HD of slain creature
        // 10 minutes = 100 rounds; per HD of the killed creature
        int targetHD = Mathf.Max(1, target.Stats.Level);
        int buffRounds = targetHD * 100; // 10 min * 10 rounds/min * HD

        // Caster gains 1d8 temporary HP
        int tempHP = DiceService.D8("Death Knell temp HP 1d8");
        caster.Stats.TempHP = Mathf.Max(caster.Stats.TempHP, tempHP); // Don't stack, use higher

        // Caster gains +2 enhancement bonus to STR
        caster.Stats.DeathKnellActive = true;
        caster.Stats.DeathKnellStrBonus = 2;
        caster.Stats.DeathKnellCLBonus = 1;
        caster.Stats.DeathKnellRoundsRemaining = buffRounds;

        // Apply the STR bonus
        caster.Stats.STR += 2;

        // Track via StatusEffectManager for display/cleanup
        var statusMgr = caster.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            int cl = caster.Stats.GetDomainBoostedCasterLevel(spell);
            var effect = statusMgr.AddEffect(spell, caster.Stats.CharacterName, cl);
            if (effect != null)
            {
                effect.RemainingRounds = buffRounds;
                effect.AppliedStatName = "STR";
                effect.AppliedStatBonus = 2;
                effect.AppliedTempHP = tempHP;
            }
        }

        CombatUI?.ShowCombatLog($"<color=#CC33FF>☠ Death Knell! {caster.Stats.CharacterName} kills {target.Stats.CharacterName} and gains {tempHP} temp HP, +2 STR, +1 caster level for {buffRounds} rounds ({targetHD * 10} minutes)!</color>");
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
    /// Resolves Dimension Door: self-target teleportation spell.
    /// Checks TeleportationBlocker, validates destination, moves caster,
    /// and ends the caster's turn (no further actions).
    /// For PC casters: selects a random valid cell within range (future: UI selection).
    /// For NPC casters: attempts to teleport to a tactically useful position.
    /// </summary>
    private bool TryResolveDimensionDoorSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (!IsDimensionDoorSpell(spell) || caster == null || caster.Stats == null)
            return false;

        if (result == null)
            return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";

        // ── Check TeleportationBlocker ──
        if (TeleportationBlocker.IsBlocked(caster))
        {
            string reason = TeleportationBlocker.GetBlockedReason(caster, TeleportationType.Teleportation)
                ?? "Extradimensional travel is blocked";

            CombatUI?.ShowCombatLog($"<color=#FF4444>🚫 {casterName} attempts Dimension Door, but {reason}!</color>");
            CombatUI?.ShowCombatLog($"<color=#FF6666>   The spell fizzles. The spell slot is consumed.</color>");

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
        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int rangeSquares = spell.GetRangeSquaresForCasterLevel(casterLevel);
        if (rangeSquares <= 0) rangeSquares = 80 + (casterLevel * 8); // fallback: 400ft + 40ft/level = 80sq + 8sq/level

        // ── Find valid destination ──
        SquareGrid grid = Grid;
        if (grid == null)
        {
            CombatUI?.ShowCombatLog($"<color=#FF4444>❌ Dimension Door fails: no grid available.</color>");
            return true;
        }

        Vector2Int casterPos = caster.GridPosition;
        int casterSize = caster.GetVisualSquaresOccupied();

        // For this prototype: auto-select best destination
        // PC: find an empty cell at max range away from enemies (escape)
        // NPC: find an empty cell near a suitable target
        Vector2Int? destination = FindDimensionDoorDestination(caster, casterPos, rangeSquares, casterSize, grid);

        if (!destination.HasValue)
        {
            CombatUI?.ShowCombatLog($"<color=#FF4444>❌ {casterName}'s Dimension Door fails — no valid destination found within range!</color>");
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
        CombatUI?.ShowCombatLog($"<color=#00CCFF>🌀 {casterName} vanishes in a flash and reappears {distanceFeet} ft away!</color>");
        CombatUI?.ShowCombatLog($"<color=#66DDFF>   Dimension Door: teleported from ({casterPos.x},{casterPos.y}) to ({dest.x},{dest.y}). No further actions this turn.</color>");

        result.BuffApplied = true;
        result.BuffDescription = $"Dimension Door: teleported {distanceFeet} ft. Turn ends.";

        Debug.Log($"[DimensionDoor] {casterName} teleported from ({casterPos.x},{casterPos.y}) to ({dest.x},{dest.y}), distance={distance} sq ({distanceFeet} ft), CL={casterLevel}, maxRange={rangeSquares} sq");

        UpdateAllStatsUI();
        return true;
    }

    /// <summary>
    /// Find the best destination for Dimension Door based on character context.
    /// PCs: try to teleport away from nearest enemy (escape) or to a flanking position.
    /// NPCs: try to teleport adjacent to their preferred target.
    /// Returns null if no valid destination found.
    /// </summary>
    private Vector2Int? FindDimensionDoorDestination(
        CharacterController caster, Vector2Int casterPos, int maxRange,
        int casterSize, SquareGrid grid)
    {
        bool isPC = caster.Stats != null && !!caster.IsPlayerControlled;

        if (isPC)
        {
            // PC: find a safe position away from enemies (escape teleport)
            return FindDimensionDoorEscapeDestination(caster, casterPos, maxRange, casterSize, grid);
        }
        else
        {
            // NPC: find a position adjacent to a target
            return FindDimensionDoorAggressiveDestination(caster, casterPos, maxRange, casterSize, grid);
        }
    }

    /// <summary>
    /// PC escape destination: find a valid empty cell that maximizes distance
    /// from the nearest enemy while staying within range.
    /// </summary>
    private Vector2Int? FindDimensionDoorEscapeDestination(
        CharacterController caster, Vector2Int casterPos, int maxRange,
        int casterSize, SquareGrid grid)
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
                if (!ch.IsPlayerControlled == !caster.IsPlayerControlled) continue; // same side

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
                        if (!ch.IsPlayerControlled == !caster.IsPlayerControlled) continue;
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

    /// <summary>
    /// NPC aggressive destination: find a position adjacent to the best PC target.
    /// </summary>
    private Vector2Int? FindDimensionDoorAggressiveDestination(
        CharacterController caster, Vector2Int casterPos, int maxRange,
        int casterSize, SquareGrid grid)
    {
        // Find best target (lowest HP PC)
        CharacterController bestTarget = null;
        int lowestHP = int.MaxValue;

        var allChars = GetAllCharacters();
        if (allChars != null)
        {
            foreach (var ch in allChars)
            {
                if (ch == null || ch == caster || ch.Stats == null || ch.Stats.IsDead) continue;
                if (!ch.IsPlayerControlled == !caster.IsPlayerControlled) continue; // same side

                if (ch.Stats.CurrentHP < lowestHP)
                {
                    lowestHP = ch.Stats.CurrentHP;
                    bestTarget = ch;
                }
            }
        }

        if (bestTarget == null) return null;

        // Find valid cell adjacent to target
        Vector2Int targetPos = bestTarget.GridPosition;
        int[] offsets = { -1, 0, 1 };
        Vector2Int? bestDest = null;
        int bestDist = int.MaxValue;

        foreach (int ox in offsets)
        {
            foreach (int oy in offsets)
            {
                if (ox == 0 && oy == 0) continue;
                Vector2Int candidate = new Vector2Int(targetPos.x + ox, targetPos.y + oy);
                int distFromCaster = SquareGridUtils.ChebyshevDistance(casterPos, candidate);

                if (distFromCaster > maxRange) continue;
                if (!grid.CanPlaceCreature(candidate, casterSize, caster)) continue;

                // Prefer closer to where we already are (energy efficiency)
                if (distFromCaster < bestDist)
                {
                    bestDist = distFromCaster;
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

        // Ranged touch attack must have hit
        if (result == null || !result.Success || (result.RequiredAttackRoll && !result.AttackHit))
        {
            // Miss — combat log already handled by the standard miss path
            return true; // We claimed this spell, even though it missed
        }

        // Check if target already has Dimensional Anchor — refresh duration if so
        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";

        // Apply the effect data
        DimensionalAnchorEffectData effectData = DimensionalAnchorEffectData.Create(
            casterLevel, casterName, durationRounds);

        target.Stats.ActiveDimensionalAnchorEffect = effectData;

        // Add tracked spell effect for duration management
        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            ActiveSpellEffect spellEffect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (spellEffect != null)
                spellEffect.RemainingRounds = durationRounds;
        }

        // Combat log with emerald/green styling
        CombatUI?.ShowCombatLog(
            $"<color=#00FF88>🟢 {casterName}'s green ray strikes {targetName}! " +
            $"A shimmering emerald field envelops {targetName}, blocking all extradimensional travel " +
            $"for {durationRounds} rounds ({durationRounds / 10} minutes).</color>");

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

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = Mathf.Max(1, casterLevel); // 1 round/level
        int tentacleGrappleMod = casterLevel + 8; // CL + Str mod (4) + Large size (4)

        Vector3 centerWorldPos = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);

        // Create area effect
        GameObject tentaclesObj = new GameObject("BlackTentacles_Area");
        tentaclesObj.transform.position = centerWorldPos;

        BlackTentaclesAreaEffect tentaclesEffect = tentaclesObj.AddComponent<BlackTentaclesAreaEffect>();
        tentaclesEffect.CenterPosition = centerWorldPos;
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
