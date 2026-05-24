// ============================================================================
// GameManager_Spells_W.cs — Spell resolution methods starting with "W".
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

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
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
    //  WALL OF FIRE — Persistent Area Effect (PHB p.298)
    // ================================================================

    private static bool IsWallOfFireSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.WALL_OF_FIRE, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Wall of Fire in both Line and Ring modes (PHB p.298).
    ///
    /// Line Mode: The aoeCells are computed from the player's chosen start/end
    ///   points via the two-click targeting system in GameManager_WallOfFire.cs.
    ///   The wall direction is derived from the line's start→end vector.
    ///
    /// Ring Mode: The aoeCells are computed as a ring (circle perimeter) with
    ///   the chosen center and radius. The area effect uses AreaShape.Circle
    ///   and the ring cells are set explicitly.
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

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        int saveDc = GetSpellSaveDC(caster, spell);

        // Determine which mode was selected
        bool isRingMode = _pendingWallOfFireMode.HasValue && _pendingWallOfFireMode.Value == WallOfFireMode.Ring;
        string modeLabel = isRingMode ? "Ring" : "Wall";

        // Compute center and direction from the AoE cells
        Vector3 centerPosition = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);
        Vector2Int centerCell = SquareGridUtils.WorldToGrid(centerPosition);
        Vector2Int direction;

        if (isRingMode)
        {
            // Ring mode: direction doesn't matter, use default
            direction = new Vector2Int(1, 0);

            // If we stored the ring center, use that instead of computed center
            if (_pendingWallRingCenter.HasValue)
                centerCell = _pendingWallRingCenter.Value;
        }
        else
        {
            // Line mode: compute direction from start→end points if available
            if (_pendingWallLineStart.HasValue)
            {
                // Use line start→center as direction proxy
                Vector2Int lineStart = _pendingWallLineStart.Value;
                Vector2Int diff = centerCell - lineStart;
                if (diff == Vector2Int.zero)
                    direction = new Vector2Int(1, 0);
                else
                    direction = new Vector2Int(
                        diff.x != 0 ? (diff.x > 0 ? 1 : -1) : 0,
                        diff.y != 0 ? (diff.y > 0 ? 1 : -1) : 0);
            }
            else
            {
                direction = ComputeWindWallDirection(caster.GridPosition, centerCell);
            }
        }

        // Wall length for line mode / ring circumference info
        int maxLengthSquares = Mathf.Max(2, casterLevel * 4);

        // Create the area effect
        string objName = isRingMode ? "WallOfFire_Ring_Area" : "WallOfFire_Line_Area";
        GameObject wallObj = new GameObject(objName);
        wallObj.transform.position = centerPosition;

        WallOfFireAreaEffect wallEffect = wallObj.AddComponent<WallOfFireAreaEffect>();
        wallEffect.CenterPosition = centerPosition;
        wallEffect.CenterCell = centerCell;
        wallEffect.WallDirection = direction == Vector2Int.zero ? new Vector2Int(1, 0) : direction;
        wallEffect.RoundsRemaining = Mathf.Max(1, durationRounds);
        wallEffect.CasterLevel = casterLevel;
        wallEffect.Caster = caster;
        wallEffect.SaveDC = saveDc;

        if (isRingMode)
        {
            // Ring mode: set ring-specific properties
            wallEffect.IsRingMode = true;
            wallEffect.RingRadius = _pendingWallRingRadius ?? 1;
            wallEffect.LengthSquares = 0; // Not applicable for ring
        }
        else
        {
            // Line mode
            wallEffect.IsRingMode = false;
            wallEffect.LengthSquares = Mathf.Max(2, maxLengthSquares);
        }

        // Pass heat wave direction (PHB p.298)
        if (isRingMode)
        {
            wallEffect.HeatWaveDirectionRing = _pendingWallHeatDirectionRing ?? "Inwards";
            Debug.Log($"[WallOfFire] Resolve: ring heat direction = {wallEffect.HeatWaveDirectionRing}");
        }
        else
        {
            wallEffect.HeatWaveDirectionLine = _pendingWallHeatDirectionLine;
            Debug.Log($"[WallOfFire] Resolve: line heat direction = {(_pendingWallHeatDirectionLine.HasValue ? _pendingWallHeatDirectionLine.Value.ToString() : "null")}");
        }

        if (aoeCells != null && aoeCells.Count > 0)
            wallEffect.SetExplicitCells(aoeCells);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🔥 {caster.Stats.CharacterName} casts Wall of Fire ({modeLabel})!");

        if (isRingMode)
        {
            int ringRad = _pendingWallRingRadius ?? 1;
            sb.AppendLine($"  Ring: {ringRad * 5}-ft radius ({aoeCells?.Count ?? 0} cells)");
        }
        else
        {
            sb.AppendLine($"  Wall: {aoeCells?.Count ?? 0} cells ({maxLengthSquares * 5} ft max)");
        }

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

        // Clean up Wall of Fire pending state after successful cast
        ResetPendingWallOfFireMode();

        return true;
    }

    // ================================================================
    //  WALL OF ICE — Persistent Area Effect (PHB p.299)
    // ================================================================

    private static bool IsWallOfIceSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.WALL_OF_ICE, System.StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════
    // WALL OF ICE — REFLEX SAVE TO DISRUPT WALL (PHB p.299)
    //
    // D&D 3.5e Rule: When Wall of Ice is cast, any creature adjacent to
    // a wall section (but not IN the wall) can attempt a Reflex save.
    //   DC = 10 + spell level (4) + caster's spellcasting ability modifier
    //   Success: Creature disrupts the wall by moving into a wall square;
    //            entire wall fails to manifest, spell slot is consumed.
    //   Failure: Creature stays in place, wall forms normally.
    // Player-controlled creatures get a UI prompt to choose whether to
    // attempt the save. AI-controlled creatures always attempt if the
    // wall caster is an enemy.
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Find all living creatures that are adjacent to any proposed wall cell
    /// but NOT standing in a wall cell. These creatures can attempt a Reflex
    /// save to disrupt the wall.
    /// </summary>
    private List<CharacterController> GetCreaturesAdjacentToWallCells(
        HashSet<Vector2Int> wallCells, CharacterController caster)
    {
        var result = new List<CharacterController>();
        if (wallCells == null || wallCells.Count == 0)
            return result;

        List<CharacterController> allChars = GetAllCharacters();
        if (allChars == null) return result;

        // Build set of neighbor cells adjacent to any wall cell
        var adjacentCells = new HashSet<Vector2Int>();
        foreach (Vector2Int wc in wallCells)
        {
            Vector2Int[] neighbors = SquareGridUtils.GetNeighbors(wc);
            for (int i = 0; i < neighbors.Length; i++)
            {
                if (!wallCells.Contains(neighbors[i]))
                    adjacentCells.Add(neighbors[i]);
            }
        }

        for (int i = 0; i < allChars.Count; i++)
        {
            CharacterController cc = allChars[i];
            if (cc == null || cc.Stats == null || cc.Stats.IsDead)
                continue;
            // Skip the caster — they don't save against their own wall
            if (cc == caster)
                continue;
            // Must be standing on a cell adjacent to the wall
            if (!adjacentCells.Contains(cc.GridPosition))
                continue;
            // Must not be standing IN a wall cell (those are "caught" and take damage)
            if (wallCells.Contains(cc.GridPosition))
                continue;

            result.Add(cc);
        }

        return result;
    }

    /// <summary>
    /// Calculate the Reflex save DC for Wall of Ice disruption.
    /// DC = 10 + spell level (4) + caster's spellcasting ability modifier.
    /// </summary>
    private int GetWallOfIceReflexDC(CharacterController caster, SpellData spell)
    {
        int abilityMod = GetSpellSaveAbilityModifier(caster, spell);
        int spellLevel = spell != null ? spell.SpellLevel : 4;
        return 10 + spellLevel + abilityMod;
    }

    /// <summary>
    /// Roll a Reflex save for a creature against the Wall of Ice disruption DC.
    /// Returns true if the save succeeds.
    /// </summary>
    private bool RollWallOfIceReflexSave(CharacterController creature, int dc, out int roll, out int total)
    {
        roll = Random.Range(1, 21);
        int reflexMod = creature.Stats.ReflexSave;
        total = roll + reflexMod;
        bool success = total >= dc;

        Debug.Log($"[WallOfIce] Reflex save: {creature.Stats.CharacterName} rolls d20({roll}) + {reflexMod} = {total} vs DC {dc} → {(success ? "SUCCESS" : "FAILURE")}");
        return success;
    }

    /// <summary>
    /// Find the closest wall cell to a creature for movement on successful save.
    /// </summary>
    private Vector2Int FindClosestWallCell(CharacterController creature, HashSet<Vector2Int> wallCells)
    {
        Vector2Int creaturePos = creature.GridPosition;
        Vector2Int closest = creaturePos;
        int bestDist = int.MaxValue;

        foreach (Vector2Int wc in wallCells)
        {
            int dist = SquareGridUtils.GetDistance(creaturePos, wc);
            if (dist < bestDist)
            {
                bestDist = dist;
                closest = wc;
            }
        }

        return closest;
    }

    /// <summary>
    /// Move a creature into a wall cell after successful Reflex save disruption.
    /// </summary>
    private void MoveCreatureToWallCell(CharacterController creature, Vector2Int wallCell)
    {
        if (creature == null || creature.Stats == null) return;

        Vector2Int oldPos = creature.GridPosition;

        // Update grid occupancy — remove from old cell first
        if (Grid != null)
        {
            SquareCell oldCell = Grid.GetCell(oldPos);
            if (oldCell != null)
                oldCell.RemoveOccupant(creature);
        }

        // Move creature
        Vector3 worldPos = SquareGridUtils.GridToWorld(wallCell);
        creature.transform.position = worldPos;
        creature.GridPosition = wallCell;

        // Add to new cell
        if (Grid != null)
        {
            SquareCell newCell = Grid.GetCell(wallCell);
            if (newCell != null)
                newCell.AddOccupant(creature);
        }

        Debug.Log($"[WallOfIce] {creature.Stats.CharacterName} moves from ({oldPos.x},{oldPos.y}) into wall cell ({wallCell.x},{wallCell.y}) to disrupt the wall!");
    }

    /// <summary>
    /// Resolves Wall of Ice with Reflex save checks for adjacent creatures.
    /// Uses a callback to handle async player prompts.
    /// The callback receives the combat log string when resolution is complete.
    /// </summary>
    private void ResolveWallOfIceWithReflexSaves(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        System.Action<string> onComplete)
    {
        if (caster == null || caster.Stats == null || spell == null)
        {
            onComplete?.Invoke("⚠ Wall of Ice failed: invalid caster or spell.");
            return;
        }

        // ── Validate proposed wall cells are not occupied ──
        if (aoeCells != null && aoeCells.Count > 0)
        {
            string validationError = WallOfIceAreaEffect.ValidateWallCreation(aoeCells, Grid);
            if (!string.IsNullOrEmpty(validationError))
            {
                Debug.Log($"[WallOfIce] Creation blocked: {validationError}");
                ResetPendingWallOfIceMode();
                onComplete?.Invoke($"⚠ Wall of Ice cannot be placed: {validationError}");
                return;
            }
        }

        int saveDC = GetWallOfIceReflexDC(caster, spell);

        // Find creatures adjacent to the proposed wall
        List<CharacterController> adjacentCreatures = GetCreaturesAdjacentToWallCells(aoeCells, caster);

        // Filter to only enemy creatures (only enemies attempt to disrupt the wall)
        adjacentCreatures.RemoveAll(cc => !IsEnemyTeam(caster, cc));

        Debug.Log($"[WallOfIce] Adjacent enemy creatures that can attempt Reflex save: {adjacentCreatures.Count} (DC {saveDC})");

        if (adjacentCreatures.Count == 0)
        {
            // No adjacent enemies — proceed directly to wall creation
            string log = CreateWallOfIceEffect(caster, spell, targets, aoeCells);
            onComplete?.Invoke(log);
            return;
        }

        // Separate into AI-controlled and player-controlled creatures
        var aiCreatures = new List<CharacterController>();
        var playerCreatures = new List<CharacterController>();

        for (int i = 0; i < adjacentCreatures.Count; i++)
        {
            CharacterController cc = adjacentCreatures[i];
            if (cc.Team == CharacterTeam.Player && cc.IsPlayerControlled)
                playerCreatures.Add(cc);
            else
                aiCreatures.Add(cc);
        }

        // Process AI saves first (synchronous)
        var saveLog = new StringBuilder();
        CharacterController aiDisruptor = null;
        Vector2Int aiDisruptorTargetCell = Vector2Int.zero;

        foreach (CharacterController aiCreature in aiCreatures)
        {
            bool success = RollWallOfIceReflexSave(aiCreature, saveDC, out int roll, out int total);
            string name = aiCreature.Stats.CharacterName;

            if (success)
            {
                saveLog.AppendLine($"🛡 {name} attempts Reflex save (DC {saveDC}): d20({roll}) + {aiCreature.Stats.ReflexSave} = {total} — SUCCESS!");
                saveLog.AppendLine($"  💥 {name} disrupts the Wall of Ice by moving into the wall's space!");
                aiDisruptor = aiCreature;
                aiDisruptorTargetCell = FindClosestWallCell(aiCreature, aoeCells);
                break; // First successful save disrupts the entire wall
            }
            else
            {
                saveLog.AppendLine($"🛡 {name} attempts Reflex save (DC {saveDC}): d20({roll}) + {aiCreature.Stats.ReflexSave} = {total} — FAILURE!");
                saveLog.AppendLine($"  {name} fails to disrupt the wall.");
            }
        }

        // If an AI creature already disrupted the wall
        if (aiDisruptor != null)
        {
            MoveCreatureToWallCell(aiDisruptor, aiDisruptorTargetCell);
            string disruptLog = BuildWallOfIceDisruptedLog(caster, spell, aoeCells, saveLog.ToString(), aiDisruptor);
            ResetPendingWallOfIceMode();
            onComplete?.Invoke(disruptLog);
            return;
        }

        // If there are player creatures that could attempt, show prompt
        if (playerCreatures.Count > 0)
        {
            // Process player creatures sequentially via callbacks
            ProcessPlayerWallOfIceReflexSaves(
                caster, spell, targets, aoeCells, saveDC,
                playerCreatures, 0, saveLog,
                onComplete);
            return;
        }

        // All AI creatures failed their saves — proceed to wall creation
        string wallLog = CreateWallOfIceEffect(caster, spell, targets, aoeCells);
        if (saveLog.Length > 0)
        {
            wallLog = saveLog.ToString() + "\n" + wallLog;
        }
        onComplete?.Invoke(wallLog);
    }

    /// <summary>
    /// Process player-controlled creatures' Reflex save prompts sequentially.
    /// Shows a UI prompt for each player creature, then continues with
    /// wall creation or disruption.
    /// </summary>
    private void ProcessPlayerWallOfIceReflexSaves(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        int saveDC,
        List<CharacterController> playerCreatures,
        int currentIndex,
        StringBuilder accumulatedLog,
        System.Action<string> onComplete)
    {
        // Base case: all player creatures processed, no disruption
        if (currentIndex >= playerCreatures.Count)
        {
            string wallLog = CreateWallOfIceEffect(caster, spell, targets, aoeCells);
            if (accumulatedLog.Length > 0)
                wallLog = accumulatedLog.ToString() + "\n" + wallLog;
            onComplete?.Invoke(wallLog);
            return;
        }

        CharacterController pc = playerCreatures[currentIndex];
        if (pc == null || pc.Stats == null || pc.Stats.IsDead)
        {
            // Skip dead/null — move to next
            ProcessPlayerWallOfIceReflexSaves(
                caster, spell, targets, aoeCells, saveDC,
                playerCreatures, currentIndex + 1, accumulatedLog, onComplete);
            return;
        }

        string pcName = pc.Stats.CharacterName;
        string casterName = caster.Stats.CharacterName;

        // Show prompt
        var options = new List<string>
        {
            $"Attempt Reflex Save (DC {saveDC})",
            "Let Wall Form"
        };

        CombatUI.ShowPickUpItemSelection(
            actorName: pcName,
            itemOptions: options,
            onSelect: selectedIndex =>
            {
                if (selectedIndex == 0)
                {
                    // Player chose to attempt save
                    bool success = RollWallOfIceReflexSave(pc, saveDC, out int roll, out int total);

                    if (success)
                    {
                        accumulatedLog.AppendLine($"🛡 {pcName} attempts Reflex save (DC {saveDC}): d20({roll}) + {pc.Stats.ReflexSave} = {total} — SUCCESS!");
                        accumulatedLog.AppendLine($"  💥 {pcName} disrupts the Wall of Ice by moving into the wall's space!");

                        Vector2Int targetCell = FindClosestWallCell(pc, aoeCells);
                        MoveCreatureToWallCell(pc, targetCell);

                        string disruptLog = BuildWallOfIceDisruptedLog(caster, spell, aoeCells, accumulatedLog.ToString(), pc);
                        ResetPendingWallOfIceMode();
                        onComplete?.Invoke(disruptLog);
                        return;
                    }
                    else
                    {
                        accumulatedLog.AppendLine($"🛡 {pcName} attempts Reflex save (DC {saveDC}): d20({roll}) + {pc.Stats.ReflexSave} = {total} — FAILURE!");
                        accumulatedLog.AppendLine($"  {pcName} fails to disrupt the wall.");
                    }
                }
                else
                {
                    // Player chose not to attempt
                    accumulatedLog.AppendLine($"🛡 {pcName} chooses not to attempt a Reflex save against the Wall of Ice.");
                }

                // Move to next player creature
                ProcessPlayerWallOfIceReflexSaves(
                    caster, spell, targets, aoeCells, saveDC,
                    playerCreatures, currentIndex + 1, accumulatedLog, onComplete);
            },
            onCancel: () =>
            {
                // Treat cancel as declining to save
                accumulatedLog.AppendLine($"🛡 {pcName} chooses not to attempt a Reflex save against the Wall of Ice.");

                ProcessPlayerWallOfIceReflexSaves(
                    caster, spell, targets, aoeCells, saveDC,
                    playerCreatures, currentIndex + 1, accumulatedLog, onComplete);
            },
            titleOverride: "Wall of Ice — Reflex Save to Disrupt",
            bodyOverride: $"{casterName} is casting Wall of Ice adjacent to {pcName}!\n\n"
                + $"Reflex Save DC {saveDC}: Success disrupts the entire wall and moves {pcName} into the wall's space.\n"
                + "Failure: Wall forms normally.",
            optionButtonColorOverride: new Color(0.4f, 0.7f, 0.9f, 1f));
    }

    /// <summary>
    /// Build the combat log for when a creature disrupts Wall of Ice via Reflex save.
    /// Spell slot was already consumed; wall does not manifest.
    /// </summary>
    private string BuildWallOfIceDisruptedLog(
        CharacterController caster,
        SpellData spell,
        HashSet<Vector2Int> aoeCells,
        string saveLog,
        CharacterController disruptor)
    {
        bool isCircleMode = _pendingWallOfIceMode.HasValue && _pendingWallOfIceMode.Value == WallOfIceMode.Circle;
        string modeLabel = isCircleMode ? "Hemisphere" : "Wall";

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"❄ {caster.Stats.CharacterName} casts Wall of Ice ({modeLabel})!");
        sb.AppendLine();
        sb.Append(saveLog);
        sb.AppendLine();
        sb.AppendLine($"  🚫 Wall of Ice fails to manifest! {disruptor.Stats.CharacterName} disrupted the wall.");
        sb.AppendLine("  (Spell slot consumed)");
        sb.Append("═══════════════════════════════════");

        return sb.ToString();
    }

    /// <summary>
    /// Creates the Wall of Ice area effect (the actual wall manifestation).
    /// Extracted from the original TryResolveWallOfIceSpell to allow
    /// the Reflex save flow to call this after saves are resolved.
    /// </summary>
    private string CreateWallOfIceEffect(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells)
    {
        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        // Determine which mode was selected
        bool isCircleMode = _pendingWallOfIceMode.HasValue && _pendingWallOfIceMode.Value == WallOfIceMode.Circle;
        string modeLabel = isCircleMode ? "Hemisphere" : "Wall";

        // Compute center and direction from the AoE cells
        Vector3 centerPosition = GetAreaCenterWorldPosition(aoeCells, caster.GridPosition);
        Vector2Int centerCell = SquareGridUtils.WorldToGrid(centerPosition);
        Vector2Int direction;

        if (isCircleMode)
        {
            direction = new Vector2Int(1, 0);
            if (_pendingWallOfIceCircleCenter.HasValue)
                centerCell = _pendingWallOfIceCircleCenter.Value;
        }
        else
        {
            if (_pendingWallOfIceLineStart.HasValue)
            {
                Vector2Int lineStart = _pendingWallOfIceLineStart.Value;
                Vector2Int diff = centerCell - lineStart;
                if (diff == Vector2Int.zero)
                    direction = new Vector2Int(1, 0);
                else
                    direction = new Vector2Int(
                        diff.x != 0 ? (diff.x > 0 ? 1 : -1) : 0,
                        diff.y != 0 ? (diff.y > 0 ? 1 : -1) : 0);
            }
            else
            {
                direction = ComputeWindWallDirection(caster.GridPosition, centerCell);
            }
        }

        int maxLengthSquares = Mathf.Max(2, casterLevel * 2);

        // Create the area effect
        string objName = isCircleMode ? "WallOfIce_Hemisphere_Area" : "WallOfIce_Line_Area";
        GameObject wallObj = new GameObject(objName);
        wallObj.transform.position = centerPosition;

        WallOfIceAreaEffect wallEffect = wallObj.AddComponent<WallOfIceAreaEffect>();
        wallEffect.CenterPosition = centerPosition;
        wallEffect.CenterCell = centerCell;
        wallEffect.WallDirection = direction == Vector2Int.zero ? new Vector2Int(1, 0) : direction;
        wallEffect.RoundsRemaining = Mathf.Max(1, durationRounds);
        wallEffect.CasterLevel = casterLevel;
        wallEffect.Caster = caster;

        if (isCircleMode)
        {
            wallEffect.IsCircleMode = true;
            wallEffect.CircleRadius = _pendingWallOfIceCircleRadius ?? 1;
            wallEffect.LengthSquares = 0;
        }
        else
        {
            wallEffect.IsCircleMode = false;
            wallEffect.LengthSquares = Mathf.Max(2, maxLengthSquares);
        }

        if (aoeCells != null && aoeCells.Count > 0)
            wallEffect.SetExplicitCells(aoeCells);

        int thickness = casterLevel;
        int wallHP = thickness * 3;

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"❄ {caster.Stats.CharacterName} casts Wall of Ice ({modeLabel})!");

        if (isCircleMode)
        {
            int circleRad = _pendingWallOfIceCircleRadius ?? 1;
            sb.AppendLine($"  Hemisphere: {circleRad * 5}-ft radius ({aoeCells?.Count ?? 0} cells)");
        }
        else
        {
            sb.AppendLine($"  Wall: {aoeCells?.Count ?? 0} cells ({maxLengthSquares * 5} ft max)");
        }

        sb.AppendLine($"  Thickness: {thickness} inch(es)");
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

        // Clean up Wall of Ice pending state after successful creation
        ResetPendingWallOfIceMode();

        return sb.ToString();
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

        int casterLevel = Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
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
                int srTotal = srRoll + casterLevel + FeatManager.GetSpellPenetrationBonus(caster.Stats);
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

    // ================================================================
    //  WISH — The mightiest spell (PHB p.302)
    // ================================================================

    /// <summary>
    /// Entry point when a Wish spell is cast. Opens WishUI for player characters
    /// or auto-decides via WishExecutor.DecideAIWish() for AI-controlled casters.
    /// Called from BeginPendingSpellTargeting in GameManager.SpellCasting.cs.
    /// </summary>
    private void HandleWishSpellCast(CharacterController caster)
    {
        if (caster == null || caster.Stats == null) return;

        Debug.Log($"[Wish] HandleWishSpellCast — caster={caster.Stats.CharacterName}  isPlayer={caster.Stats.IsPlayerControlled}");

        // Consume the spell slot (already handled by the spell casting pipeline before
        // BeginPendingSpellTargeting is called). We just need to handle the Wish effect.

        if (caster.Stats.IsPlayerControlled)
        {
            // Player: open the WishUI selection panel
            if (WishUI == null)
            {
                CombatUI?.ShowCombatLog("⚠ Wish UI not available — spell fizzles.");
                _pendingSpell = null;
                _pendingMetamagic = null;
                ShowActionChoices();
                return;
            }

            WishUI.OnWishConfirmed = (option, target, ability, affliction, spellId) =>
            {
                Debug.Log($"[Wish] Player confirmed: option={option}  target={target?.Stats?.CharacterName}  ability={ability}  affliction={affliction}  spellId={spellId}");

                bool success = WishExecutor.ExecuteWish(caster, option, target, ability,
                    affliction, spellId, isItemWish: false);

                if (success)
                    CombatUI?.ShowCombatLog($"<color=#FFD700>✨ {caster.Stats.CharacterName}'s Wish is granted!</color>");
                else
                    CombatUI?.ShowCombatLog($"<color=#FF6666>✨ {caster.Stats.CharacterName}'s Wish fails.</color>");

                _pendingSpell = null;
                _pendingMetamagic = null;
                ShowActionChoices();
            };

            WishUI.OnCancelled = () =>
            {
                CombatUI?.ShowCombatLog($"{caster.Stats.CharacterName} decides not to make a Wish.");
                // Note: spell slot is already consumed; this is intentional for D&D 3.5e (casting began).
                _pendingSpell = null;
                _pendingMetamagic = null;
                ShowActionChoices();
            };

            WishUI.Open(caster, isItemWish: false);
        }
        else
        {
            // AI: auto-decide
            var decision = WishExecutor.DecideAIWish(caster);
            Debug.Log($"[Wish] AI decision: option={decision.option}  target={decision.target?.Stats?.CharacterName}  ability={decision.ability}  spellId={decision.spellId}");

            bool success = WishExecutor.ExecuteWish(caster, decision.option, decision.target,
                decision.ability, decision.affliction, decision.spellId, isItemWish: false);

            if (success)
                CombatUI?.ShowCombatLog($"<color=#FFD700>✨ {caster.Stats.CharacterName}'s Wish is granted!</color>");
            else
                CombatUI?.ShowCombatLog($"<color=#FF6666>✨ {caster.Stats.CharacterName}'s Wish fails.</color>");

            _pendingSpell = null;
            _pendingMetamagic = null;
            ShowActionChoices();
        }
    }

    /// <summary>
    /// Handles a Wish cast from a magic item (e.g., Luck Blade). No XP cost.
    /// Can be called from item behavior code.
    /// </summary>
    public void HandleItemWishCast(CharacterController caster)
    {
        if (caster == null || caster.Stats == null) return;

        Debug.Log($"[Wish] HandleItemWishCast — caster={caster.Stats.CharacterName}  isPlayer={caster.Stats.IsPlayerControlled}");

        if (caster.Stats.IsPlayerControlled)
        {
            if (WishUI == null)
            {
                CombatUI?.ShowCombatLog("⚠ Wish UI not available.");
                return;
            }

            WishUI.OnWishConfirmed = (option, target, ability, affliction, spellId) =>
            {
                bool success = WishExecutor.ExecuteWish(caster, option, target, ability,
                    affliction, spellId, isItemWish: true);

                if (success)
                    CombatUI?.ShowCombatLog($"<color=#FFD700>✨ The Luck Blade grants {caster.Stats.CharacterName}'s Wish!</color>");
                else
                    CombatUI?.ShowCombatLog($"<color=#FF6666>✨ The Wish from the Luck Blade fails.</color>");

                ShowActionChoices();
            };

            WishUI.OnCancelled = () =>
            {
                CombatUI?.ShowCombatLog($"{caster.Stats.CharacterName} decides not to use the Luck Blade's Wish.");
                ShowActionChoices();
            };

            WishUI.Open(caster, isItemWish: true);
        }
        else
        {
            // AI uses Luck Blade wish
            var decision = WishExecutor.DecideAIWish(caster);
            bool success = WishExecutor.ExecuteWish(caster, decision.option, decision.target,
                decision.ability, decision.affliction, decision.spellId, isItemWish: true);

            if (success)
                CombatUI?.ShowCombatLog($"<color=#FFD700>✨ The Luck Blade grants {caster.Stats.CharacterName}'s Wish!</color>");
            else
                CombatUI?.ShowCombatLog($"<color=#FF6666>✨ The Wish from the Luck Blade fails.</color>");
        }
    }

}
