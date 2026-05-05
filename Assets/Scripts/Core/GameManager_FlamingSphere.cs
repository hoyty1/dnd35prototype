using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DND35e.Identifiers;

public partial class GameManager
{
    private readonly List<FlamingSphereEntity> _activeFlamingSpheres = new List<FlamingSphereEntity>();
    private FlamingSphereEntity _selectedFlamingSphereForControl;

    public bool HasActiveFlamingSphere(CharacterController caster)
    {
        CleanupFlamingSpheres();
        return GetPrimaryFlamingSphereForCaster(caster) != null;
    }

    public bool CanControlFlamingSphere(CharacterController caster)
    {
        return CanControlFlamingSphere(caster, out _);
    }

    public bool CanControlFlamingSphere(CharacterController caster, out string reason)
    {
        reason = string.Empty;

        FlamingSphereEntity sphere = GetPrimaryFlamingSphereForCaster(caster);
        if (sphere == null)
        {
            reason = "No active sphere";
            return false;
        }

        if (caster == null || caster.Actions == null)
        {
            reason = "No actor";
            return false;
        }

        if (!(caster.Actions.HasMoveAction || caster.Actions.CanConvertStandardToMove))
        {
            reason = "Move used";
            return false;
        }

        if (sphere.MovedThisTurn)
        {
            reason = "Already moved";
            return false;
        }

        if (!IsFlamingSphereWithinRangeOfCaster(sphere, caster))
        {
            reason = "Out of range";
            return false;
        }

        return true;
    }

    public void OnControlFlamingSphereButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        if (!CanControlFlamingSphere(pc, out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                CombatUI?.ShowCombatLog($"⚠ Cannot control Flaming Sphere: {reason}.");
            ShowActionChoices();
            return;
        }

        BeginFlamingSphereControlSelection(pc);
    }

    public void HandleFlamingSphereTurnStart(CharacterController character)
    {
        CleanupFlamingSpheres();
        if (character == null)
            return;

        for (int i = _activeFlamingSpheres.Count - 1; i >= 0; i--)
        {
            FlamingSphereEntity sphere = _activeFlamingSpheres[i];
            if (sphere == null)
                continue;

            if (sphere.Caster == null || sphere.Caster == character)
            {
                sphere.RemainingRounds = Mathf.Max(0, sphere.RemainingRounds - 1);
                sphere.MovedThisTurn = false;
                sphere.WarnedNotMovedThisTurn = false;

                if (sphere.RemainingRounds <= 0)
                {
                    DissipateFlamingSphere(sphere, "Duration expired.");
                    continue;
                }
            }

            if (!IsFlamingSphereWithinRangeOfCaster(sphere, sphere.Caster))
            {
                DissipateFlamingSphere(sphere, "Sphere exceeded maximum spell range.");
            }
        }
    }

    public void WarnFlamingSphereNotMovedAtTurnEnd(CharacterController character)
    {
        if (character == null)
            return;

        CleanupFlamingSpheres();
        for (int i = 0; i < _activeFlamingSpheres.Count; i++)
        {
            FlamingSphereEntity sphere = _activeFlamingSpheres[i];
            if (sphere == null || sphere.Caster != character || sphere.RemainingRounds <= 0)
                continue;
            if (sphere.MovedThisTurn || sphere.WarnedNotMovedThisTurn)
                continue;

            CombatUI?.ShowCombatLog($"⚠ {character.Stats.CharacterName} ends turn without moving Flaming Sphere.");
            sphere.WarnedNotMovedThisTurn = true;
        }
    }

    public bool TryControlFlamingSphereForAI(CharacterController caster, CharacterController preferredTarget)
    {
        if (caster == null || caster.Actions == null)
            return false;

        FlamingSphereEntity sphere = GetPrimaryFlamingSphereForCaster(caster);
        if (sphere == null)
            return false;

        if (!(caster.Actions.HasMoveAction || caster.Actions.CanConvertStandardToMove) || sphere.MovedThisTurn)
            return false;

        if (!IsFlamingSphereWithinRangeOfCaster(sphere, caster))
        {
            DissipateFlamingSphere(sphere, "Sphere exceeded maximum spell range.");
            return false;
        }

        CharacterController bestTarget = preferredTarget;
        if (bestTarget == null || bestTarget.Stats == null || bestTarget.Stats.IsDead || !IsEnemyTeam(caster, bestTarget))
        {
            int bestDist = int.MaxValue;
            foreach (CharacterController c in GetAllCharacters())
            {
                if (c == null || c.Stats == null || c.Stats.IsDead || !IsEnemyTeam(caster, c))
                    continue;

                int dist = SquareGridUtils.GetDistance(sphere.GridPosition, c.GridPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = c;
                }
            }
        }

        if (bestTarget == null)
            return false;

        return TryMoveFlamingSphere(caster, sphere, bestTarget.GridPosition, consumeMoveAction: true, showLog: true, actorIsAI: true);
    }

    private bool TryResolveFlamingSphereAoECast(CharacterController caster, SpellData spell, HashSet<Vector2Int> aoeCells, out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;
        if (!string.Equals(spell.SpellId, SpellNames.FLAMING_SPHERE, StringComparison.Ordinal))
            return false;

        CleanupFlamingSpheres();

        Vector2Int targetCell = caster.GridPosition;
        if (aoeCells != null && aoeCells.Count > 0)
        {
            int bestDist = int.MaxValue;
            foreach (Vector2Int cell in aoeCells)
            {
                int d = SquareGridUtils.GetDistance(caster.GridPosition, cell);
                if (d < bestDist)
                {
                    bestDist = d;
                    targetCell = cell;
                }
            }
        }

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        int maxRangeSquares = Mathf.Max(1, spell.GetRangeSquaresForCasterLevel(casterLevel));

        GameObject go = new GameObject($"FlamingSphere_{caster.Stats.CharacterName}");
        FlamingSphereEntity sphere = go.AddComponent<FlamingSphereEntity>();
        sphere.Initialize(caster, spell, targetCell, durationRounds, maxRangeSquares);
        _activeFlamingSpheres.Add(sphere);

        CharacterController occupant = GetLivingCharacterAtCell(targetCell);
        SpellResult impactResult = null;
        if (occupant != null)
            impactResult = ResolveFlamingSphereImpactDamage(caster, sphere, occupant, spell, "on creation");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"✨ {caster.Stats.CharacterName} casts Flaming Sphere!");
        sb.AppendLine($"  Sphere placed at ({targetCell.x}, {targetCell.y})");
        sb.AppendLine($"  Duration: {durationRounds} rounds");
        sb.AppendLine($"  Control: move action each turn, up to 30 ft");
        sb.AppendLine($"  Maximum tether range: {maxRangeSquares * 5} ft");

        if (impactResult != null)
        {
            sb.AppendLine($"  Initial impact: {occupant.Stats.CharacterName}");
            if (impactResult.RequiredSave)
            {
                string saveOutcome = impactResult.SaveSucceeded ? "SUCCESS" : "FAIL";
                sb.AppendLine($"    Reflex d20({impactResult.SaveRoll}) + {impactResult.SaveMod} = {impactResult.SaveTotal} vs DC {impactResult.SaveDC} → {saveOutcome}");
            }

            sb.AppendLine($"    Damage: {impactResult.DamageDealt} fire");
            sb.AppendLine($"    HP: {impactResult.TargetHPBefore} → {impactResult.TargetHPAfter}");
        }
        else
        {
            sb.AppendLine("  No creature in target space on creation.");
        }

        sb.AppendLine($"  ⚠ Use Control Flaming Sphere (Move) before ending turn to reposition.");
        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

    private void BeginFlamingSphereControlSelection(CharacterController caster)
    {
        FlamingSphereEntity sphere = GetPrimaryFlamingSphereForCaster(caster);
        if (sphere == null)
        {
            ShowActionChoices();
            return;
        }

        _selectedFlamingSphereForControl = sphere;
        CurrentSubPhase = PlayerSubPhase.SelectingFlamingSphereTarget;
        CombatUI?.SetActionButtonsVisible(false);

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        foreach (SquareCell gridCell in GetCellsInChebyshevRange(sphere.GridPosition, sphere.MoveRangeSquares, includeCenter: true))
        {
            if (gridCell == null)
                continue;

            if (gridCell.Coords == sphere.GridPosition)
                gridCell.SetHighlight(HighlightType.Selected);
            else
                gridCell.SetHighlight(HighlightType.Movement);

            if (!_highlightedCells.Contains(gridCell))
                _highlightedCells.Add(gridCell);
        }

        if (_highlightedCells.Count == 0)
        {
            CombatUI?.ShowCombatLog("⚠ No valid control destinations for Flaming Sphere.");
            CancelFlamingSphereControlSelection(showCancelLog: false);
            return;
        }

        CombatUI?.SetTurnIndicator($"FLAMING SPHERE: Select destination within 30 ft (6 squares). Right-click/ESC to cancel.");
    }

    private void HandleFlamingSphereControlClick(CharacterController caster, SquareCell cell)
    {
        FlamingSphereEntity sphere = _selectedFlamingSphereForControl;
        if (caster == null || sphere == null || cell == null)
        {
            CancelFlamingSphereControlSelection(showCancelLog: false);
            return;
        }

        if (!_highlightedCells.Contains(cell))
        {
            CombatUI?.ShowCombatLog("⚠ Choose a highlighted destination for Flaming Sphere.");
            return;
        }

        if (cell.Coords == sphere.GridPosition)
        {
            CancelFlamingSphereControlSelection(showCancelLog: false);
            ShowActionChoices();
            return;
        }

        bool moved = TryMoveFlamingSphere(caster, sphere, cell.Coords, consumeMoveAction: true, showLog: true, actorIsAI: false);
        _selectedFlamingSphereForControl = null;

        if (moved)
            ShowActionChoices();
        else
            BeginFlamingSphereControlSelection(caster);
    }

    private void CancelFlamingSphereControlSelection(bool showCancelLog)
    {
        CharacterController caster = ActivePC;
        _selectedFlamingSphereForControl = null;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        if (showCancelLog && caster != null && caster.Stats != null)
            CombatUI?.ShowCombatLog($"↩ {caster.Stats.CharacterName} cancels Flaming Sphere control.");

        ShowActionChoices();
    }

    private bool TryMoveFlamingSphere(
        CharacterController caster,
        FlamingSphereEntity sphere,
        Vector2Int destination,
        bool consumeMoveAction,
        bool showLog,
        bool actorIsAI)
    {
        if (caster == null || sphere == null)
            return false;

        if (consumeMoveAction)
        {
            if (!(caster.Actions.HasMoveAction || caster.Actions.CanConvertStandardToMove))
                return false;
        }

        if (!IsFlamingSphereWithinRangeOfCaster(sphere, caster))
        {
            DissipateFlamingSphere(sphere, "Sphere exceeded maximum spell range.");
            return false;
        }

        List<Vector2Int> line = BuildBresenhamPath(sphere.GridPosition, destination);
        if (line.Count <= 1)
            return false;

        int maxSteps = Mathf.Max(1, sphere.MoveRangeSquares);
        Vector2Int finalPos = sphere.GridPosition;
        CharacterController hitTarget = null;

        for (int i = 1; i < line.Count && i <= maxSteps; i++)
        {
            Vector2Int step = line[i];
            SquareCell stepCell = Grid != null ? Grid.GetCell(step) : null;
            if (stepCell == null)
                break;

            finalPos = step;
            CharacterController occupant = stepCell.Occupant;
            if (occupant != null && occupant.Stats != null && !occupant.Stats.IsDead)
            {
                hitTarget = occupant;
                break;
            }
        }

        sphere.SetGridPosition(finalPos);
        sphere.MovedThisTurn = true;
        sphere.WarnedNotMovedThisTurn = false;

        if (consumeMoveAction)
            ConsumeMoveAction(caster);

        if (showLog)
        {
            string who = caster.Stats != null ? caster.Stats.CharacterName : "Caster";
            CombatUI?.ShowCombatLog($"🔥 {who} rolls Flaming Sphere to ({finalPos.x}, {finalPos.y}).");
        }

        if (hitTarget != null)
        {
            ResolveFlamingSphereImpactDamage(caster, sphere, hitTarget, sphere.SourceSpell, actorIsAI ? "while guided by AI" : "while moving");
        }

        if (!IsFlamingSphereWithinRangeOfCaster(sphere, caster))
        {
            DissipateFlamingSphere(sphere, "Sphere exceeded maximum spell range.");
        }

        UpdateAllStatsUI();
        return true;
    }

    private SpellResult ResolveFlamingSphereImpactDamage(CharacterController caster, FlamingSphereEntity sphere, CharacterController target, SpellData spell, string context)
    {
        if (caster == null || target == null || spell == null || caster.Stats == null || target.Stats == null)
            return null;

        SpellResult result = SpellCaster.Cast(spell, caster.Stats, target.Stats, null, false, false, caster, target);

        if (result != null)
        {
            if (result.RequiredSave)
            {
                string saveOutcome = result.SaveSucceeded ? "negates" : "fails";
                CombatUI?.ShowCombatLog($"🔥 Flaming Sphere hits {target.Stats.CharacterName} {context}: Reflex {saveOutcome} (d20 {result.SaveRoll} + {result.SaveMod} = {result.SaveTotal} vs DC {result.SaveDC}).");
            }

            if (result.DamageDealt > 0)
            {
                CombatUI?.ShowCombatLog($"   {target.Stats.CharacterName} takes {result.DamageDealt} fire damage ({result.TargetHPBefore} → {result.TargetHPAfter} HP).");
                CheckConcentrationOnDamage(target, result.DamageDealt);
            }

            if (result.TargetKilled)
            {
                target.OnDeath();
                HandleSummonDeathCleanup(target);
                CombatUI?.ShowCombatLog($"💀 {target.Stats.CharacterName} is slain by Flaming Sphere.");
            }
        }

        return result;
    }

    private FlamingSphereEntity GetPrimaryFlamingSphereForCaster(CharacterController caster)
    {
        if (caster == null)
            return null;

        CleanupFlamingSpheres();
        for (int i = 0; i < _activeFlamingSpheres.Count; i++)
        {
            FlamingSphereEntity sphere = _activeFlamingSpheres[i];
            if (sphere != null && sphere.Caster == caster && sphere.RemainingRounds > 0)
                return sphere;
        }

        return null;
    }

    private bool IsFlamingSphereWithinRangeOfCaster(FlamingSphereEntity sphere, CharacterController caster)
    {
        if (sphere == null || caster == null || caster.Stats == null)
            return false;

        int maxRange = Mathf.Max(1, sphere.MaxRangeSquares);
        int distance = SquareGridUtils.GetDistance(caster.GridPosition, sphere.GridPosition);
        return distance <= maxRange;
    }

    private CharacterController GetLivingCharacterAtCell(Vector2Int cell)
    {
        SquareCell squareCell = Grid != null ? Grid.GetCell(cell) : null;
        if (squareCell == null || !squareCell.IsOccupied || squareCell.Occupant == null)
            return null;

        CharacterController occupant = squareCell.Occupant;
        return occupant.Stats != null && !occupant.Stats.IsDead ? occupant : null;
    }

    private void DissipateFlamingSphere(FlamingSphereEntity sphere, string reason)
    {
        if (sphere == null)
            return;

        CharacterController caster = sphere.Caster;
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
        CombatUI?.ShowCombatLog($"🕯 Flaming Sphere ({casterName}) dissipates: {reason}");

        _activeFlamingSpheres.Remove(sphere);
        if (_selectedFlamingSphereForControl == sphere)
            _selectedFlamingSphereForControl = null;

        if (sphere != null)
            Destroy(sphere.gameObject);
    }

    private void CleanupFlamingSpheres()
    {
        for (int i = _activeFlamingSpheres.Count - 1; i >= 0; i--)
        {
            FlamingSphereEntity sphere = _activeFlamingSpheres[i];
            if (sphere == null)
                _activeFlamingSpheres.RemoveAt(i);
        }
    }

    private static List<Vector2Int> BuildBresenhamPath(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> points = new List<Vector2Int>();

        int x0 = start.x;
        int y0 = start.y;
        int x1 = end.x;
        int y1 = end.y;

        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            points.Add(new Vector2Int(x0, y0));
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return points;
    }
}
