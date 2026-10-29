using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OverrunSystem : BaseCombatManeuver
{
}

/// <summary>
/// What one overrun move did (batch 12 step 20; CMB-015, D11), for the PC continuation, the AI turn and the scenario steps.
/// </summary>
public sealed class OverrunMoveOutcome
{
    /// <summary>The move action was spent and the attacker set off.</summary>
    public bool Started;
    /// <summary>The overrun attempt was made (the attacker reached an opponent's space).</summary>
    public bool Attempted;
    public CharacterController Defender;
    /// <summary>The defender let the attacker by (PHB p.157 step 2): the attempt cost no action.</summary>
    public bool Avoided;
    /// <summary>The defender blocked and the opposed check was rolled (PHB p.157 step 3).</summary>
    public bool Blocked;
    /// <summary>The attacker won the block: the defender is prone and the attacker went on.</summary>
    public bool Succeeded;
    /// <summary>The overrun's standard action was spent (a block, or an attempt stopped by the defender's AoO).</summary>
    public bool StandardSpent;
    /// <summary>The attacker ended dead, dying or unconscious.</summary>
    public bool AttackerDown;
    /// <summary>An attack of opportunity stopped the movement (tripped, grabbed, held): the attacker stays where it stood.</summary>
    public bool StoppedByAoO;
    /// <summary>Why the movement stopped before the path's end (null: it reached the end).</summary>
    public string StopReason;

    public void Reset()
    {
        Started = Attempted = Avoided = Blocked = Succeeded = StandardSpent = AttackerDown = StoppedByAoO = false;
        Defender = null;
        StopReason = null;
    }

    public string Describe()
        => !Started ? "not started" + (StopReason != null ? " (" + StopReason + ")" : "")
            : !Attempted ? "moved, no overrun attempt" + (StopReason != null ? " (" + StopReason + ")" : "")
            : (Avoided ? "avoided" : Succeeded ? "blocked, attacker won" : Blocked ? "blocked, attacker lost" : "attempt stopped")
              + (StandardSpent ? ", standard spent" : ", no action spent")
              + (StopReason != null ? " (" + StopReason + ")" : "")
              + (AttackerDown ? ", attacker down" : "");
}

/// <summary>
/// The overrun (PHB p.157-158; batch 12 step 20, CMB-015, CMB-086, D11): a standard action taken during a move (a charge
/// overrun is step 21's), one path for PCs and NPCs. The PC picks a destination (the Special Attack menu's Overrun); the AI
/// plans a move through the defender's square (<see cref="TryPlanOverrunPathForAI"/>). Both run <see cref="RunOverrunMove"/>:
/// the move action is spent, each step resolves its movement AoOs (<see cref="ResolveMovementAoOsBeforeStep"/>) and moves
/// through MoveAlongPath; entering an opponent's space is the overrun attempt: the defender's AoO (step 1, Improved
/// Overrun or not), its avoid-or-block choice (step 2, <see cref="IManeuverDecider.ChooseOverrunResponse"/> of the
/// defender's decider; Improved Overrun forbids avoiding, PHB p.96), the opposed check (step 3,
/// CharacterController.ResolveOverrunAttempt through ExecuteSpecialAttack) and its consequences (step 4: a winner knocks
/// the defender prone and goes on, a loser ends its movement in the square it came from). An avoided overrun does not
/// count against the attacker's actions.
/// </summary>
public partial class GameManager
{
    private bool _isSelectingOverrunDestination;
    private CharacterController _overrunAttacker;
    private Vector2Int _overrunDestination;

    // The PC's "Overrun or Normal Move" dialog while it is open (the scenario harness answers it).
    private bool _overrunChoicePending;
    private CharacterController _overrunChoiceAttacker;
    private List<Vector2Int> _overrunChoiceOverrunPath;
    private List<Vector2Int> _overrunChoiceNormalPath;

    // A controllable defender's avoid-or-block prompt while it is open (D11).
    private CharacterController _overrunResponsePromptDefender;
    private Action<bool> _overrunResponsePromptAnswer;

    /// <summary>The seconds the overrun waits after each AoO it resolves (the PC movement's pace).</summary>
    private const float OverrunSecondsAfterAoO = 1.0f;

    /// <summary>
    /// May <paramref name="actor"/> overrun now (PHB p.157)? A standard action during a move: it needs both its move and its
    /// standard action, so a creature limited to a single action (staggered, disabled, slowed; PHB p.145-146, p.280) cannot,
    /// no 5-foot step this turn, and to be standing. (A charge overrun is step 21's, through the charge core.)
    /// </summary>
    public bool CanUseOverrun(CharacterController actor, out string reason)
    {
        reason = "Unavailable";
        if (actor == null || actor.Stats == null)
            return false;

        if (!actor.Actions.HasMoveAction)
        {
            reason = "No move action";
            return false;
        }

        if (!actor.Actions.HasStandardAction)
        {
            reason = "No standard action";
            return false;
        }

        if (actor.Actions.SingleActionOnly)
        {
            reason = "Single action only";
            return false;
        }

        if (actor.HasTakenFiveFootStep)
        {
            reason = "After 5-ft step";
            return false;
        }

        if (actor.HasCondition(CombatConditionType.Prone))
        {
            reason = "Prone";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    // ── The PC flow: Special Attack > Overrun > a destination ──────────────────────────────────────────

    private void ClearOverrunDestinationSelectionState()
    {
        _isSelectingOverrunDestination = false;
        _overrunAttacker = null;
        _overrunDestination = Vector2Int.zero;
    }

    private void ClearOverrunChoiceState()
    {
        _overrunChoicePending = false;
        _overrunChoiceAttacker = null;
        _overrunChoiceOverrunPath = null;
        _overrunChoiceNormalPath = null;
    }

    private void StartOverrunDestinationSelection(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null || Grid == null)
        {
            ShowActionChoices();
            return;
        }

        Debug.Log($"[Overrun][Flow] Entering destination selection for {attacker.Stats.CharacterName}.");

        ClearOverrunChoiceState();
        _isSelectingOverrunDestination = true;
        _overrunAttacker = attacker;
        CurrentSubPhase = PlayerSubPhase.Moving;

        ShowOverrunDestinationOptions(attacker);
        CombatUI?.SetActionButtonsVisible(false);
        CombatUI?.SetTurnIndicator($"{attacker.Stats.CharacterName} - Select destination for overrun");
        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("", "Select destination for overrun."));
    }

    private void ShowOverrunDestinationOptions(CharacterController attacker)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        if (attacker == null || attacker.Stats == null)
            return;

        int movementSpeed = GetCurrentMoveRangeSquares(attacker);
        List<SquareCell> candidateCells = Grid.GetCellsInRange(attacker.GridPosition, movementSpeed);

        for (int i = 0; i < candidateCells.Count; i++)
        {
            SquareCell cell = candidateCells[i];
            if (cell == null || cell.Coords == attacker.GridPosition)
                continue;

            AoOPathResult candidate = FindOverrunPath(attacker, cell.Coords, out _);
            if (candidate == null || !OverrunPathMeetsOpponent(attacker, candidate.Path))
                continue;

            cell.SetHighlight(HighlightType.Move);
            _highlightedCells.Add(cell);
        }

        HighlightCharacterFootprint(attacker, HighlightType.Selected);

        if (_highlightedCells.Count <= 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} has no reachable overrun destination."));
            ShowActionChoices();
        }
    }

    private void HandleOverrunDestinationClick(CharacterController pc, SquareCell cell)
    {
        if (!_isSelectingOverrunDestination || pc == null || cell == null)
            return;

        if (_overrunAttacker != pc)
        {
            ShowActionChoices();
            return;
        }

        if (cell.Coords == pc.GridPosition)
        {
            CancelMovementSelection();
            return;
        }

        if (!_highlightedCells.Contains(cell))
            return;

        _overrunDestination = cell.Coords;
        AnalyzeOverrunPath(pc, _overrunDestination);
    }

    private void AnalyzeOverrunPath(CharacterController attacker, Vector2Int destination)
    {
        if (attacker == null || attacker.Stats == null || Grid == null)
        {
            ShowActionChoices();
            return;
        }

        int movementSpeed = GetCurrentMoveRangeSquares(attacker);
        AoOPathResult overrunPathResult = FindOverrunPath(attacker, destination, out string invalidReason);
        if (overrunPathResult != null && !OverrunPathMeetsOpponent(attacker, overrunPathResult.Path))
        {
            overrunPathResult = null;
            invalidReason = "the path enters no opponent's space";
        }
        if (overrunPathResult == null)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"No valid overrun path to that destination{(string.IsNullOrEmpty(invalidReason) ? "" : ": " + invalidReason)}."));
            ShowOverrunDestinationOptions(attacker);
            return;
        }

        List<Vector2Int> overrunPath = new List<Vector2Int>(overrunPathResult.Path);

        AoOPathResult normalPathResult = FindPath(
            attacker,
            destination,
            avoidThreats: false,
            maxRangeOverride: movementSpeed,
            allowThroughAllies: true,
            allowThroughEnemies: false);

        List<Vector2Int> normalPath = normalPathResult != null && normalPathResult.Path != null
            ? new List<Vector2Int>(normalPathResult.Path)
            : new List<Vector2Int>();

        bool canAvoidEnemies = normalPath.Count > 0 && normalPath[normalPath.Count - 1] == destination;
        if (canAvoidEnemies && CombatUI != null)
        {
            _overrunChoicePending = true;
            _overrunChoiceAttacker = attacker;
            _overrunChoiceOverrunPath = overrunPath;
            _overrunChoiceNormalPath = normalPath;
            CombatUI.ShowConfirmationDialog(
                "OVERRUN CHOICE",
                "You can reach this destination without overrunning. Still overrun?",
                "Overrun",
                "Normal Move",
                onConfirm: () => AnswerOverrunChoice(true),
                onCancel: () => AnswerOverrunChoice(false));
            return;
        }

        ExecuteOverrunMovementPath(attacker, overrunPath);
    }

    /// <summary>The "Overrun or Normal Move" dialog's answer: true overruns, false takes the normal path.</summary>
    private void AnswerOverrunChoice(bool overrun)
    {
        if (!_overrunChoicePending)
            return;
        CharacterController attacker = _overrunChoiceAttacker;
        List<Vector2Int> overrunPath = _overrunChoiceOverrunPath;
        List<Vector2Int> normalPath = _overrunChoiceNormalPath;
        ClearOverrunChoiceState();
        if (overrun)
            ExecuteOverrunMovementPath(attacker, overrunPath);
        else
            ExecuteNormalMovementPath(attacker, normalPath);
    }

    private void ExecuteNormalMovementPath(CharacterController attacker, List<Vector2Int> path)
    {
        ClearOverrunDestinationSelectionState();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        if (attacker == null || path == null || path.Count == 0)
        {
            ShowActionChoices();
            return;
        }

        // An ordinary move provokes as any other (PHB p.137; CMB-151).
        StartCoroutine(ExecuteMovement(attacker, path, isWithdraw: false, provokedAoOs: CheckForAoO(attacker, path)));
    }

    /// <summary>The PC's overrun move along <paramref name="path"/> (the shared <see cref="RunOverrunMove"/>), then its continuation.</summary>
    private void ExecuteOverrunMovementPath(CharacterController attacker, List<Vector2Int> path)
    {
        ClearOverrunDestinationSelectionState();
        if (Grid != null)
            Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        if (attacker == null || attacker.Stats == null || path == null || path.Count == 0)
        {
            ShowActionChoices();
            return;
        }

        if (!CanUseOverrun(attacker, out string reason))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot overrun: {reason}."));
            ShowActionChoices();
            return;
        }

        StartCoroutine(RunPcOverrunMove(attacker, path));
    }

    private IEnumerator RunPcOverrunMove(CharacterController pc, List<Vector2Int> path)
    {
        CurrentSubPhase = PlayerSubPhase.Animating;
        if (_pathPreview != null) _pathPreview.HidePath();
        if (_hoverMarker != null) _hoverMarker.Hide();

        var outcome = new OverrunMoveOutcome();
        yield return StartCoroutine(RunOverrunMove(pc, path, CheckForAoO(pc, path), PlayerMoveSecondsPerStep, outcome));

        if (outcome.AttackerDown || ThreatSystem.IsMoverIncapacitated(pc))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("⛔", $"{pc.Stats.CharacterName}'s overrun ends: {pc.Stats.CharacterName} is down."));
            if (EvaluateCombatEnd("Overrun.AttackerDown"))
                yield break;
            EndActivePCTurn();
            yield break;
        }

        if (EvaluateCombatEnd("Overrun.Resolved"))
            yield break;

        ShowActionChoices();
    }

    // ── The AI flow: a planned move through the defender's square ──────────────────────────────────────

    /// <summary>
    /// The AI's overrun plan against <paramref name="target"/> (an AI stopgap until the batch 13 catalog): of the squares
    /// next to the target's space, those an overrun path (<see cref="FindOverrunPath"/>) reaches through the target's
    /// space and no other opponent's, the one with the fewest squares moved, then the lowest cost, then the nearest to the
    /// square straight past the target, then the lowest square.
    /// False when the attacker cannot overrun now or no such path exists.
    /// </summary>
    public bool TryPlanOverrunPathForAI(CharacterController npc, CharacterController target, out List<Vector2Int> path)
    {
        path = null;
        if (npc == null || target == null || npc.Stats == null || target.Stats == null || Grid == null)
            return false;
        if (!CanUseOverrun(npc, out _) || !IsLegalOverrunOpponent(npc, target, out _))
            return false;

        var targetSquares = new HashSet<Vector2Int>(target.GetOccupiedSquares());
        int npcSquares = Mathf.Max(1, npc.GetVisualSquaresOccupied());
        int targetSize = Mathf.Max(1, target.GetVisualSquaresOccupied());
        // The point straight past the target, as seen from the attacker, measured between footprint centres (a
        // multi-square creature's base square is its footprint's corner): the tie-break after the squares and the cost.
        Vector2 npcCentre = FootprintCentre(npc.GridPosition, npcSquares);
        Vector2 targetCentre = FootprintCentre(target.GridPosition, targetSize);
        Vector2 straightPast = targetCentre + (targetCentre - npcCentre);
        int bestCount = int.MaxValue;
        int bestCost = int.MaxValue;
        float bestStraight = float.MaxValue;
        Vector2Int bestCell = default(Vector2Int);
        // A base square from which the attacker's footprint clears the target's space on any side: up to the target's
        // size plus the attacker's own beyond the target's base square.
        foreach (SquareCell cell in GetCellsInChebyshevRange(target.GridPosition, targetSize + npcSquares, includeCenter: false))
        {
            if (cell == null || cell.Coords == npc.GridPosition || targetSquares.Contains(cell.Coords))
                continue;
            bool clears = true;
            foreach (Vector2Int sq in npc.GetOccupiedSquaresAt(cell.Coords))
                if (targetSquares.Contains(sq)) { clears = false; break; }
            if (!clears)
                continue;
            AoOPathResult result = FindOverrunPath(npc, cell.Coords, out _);
            if (result == null || !PathEntersAnySquare(npc, result.Path, targetSquares))
                continue;
            int count = result.Path.Count;
            int cost = SquareGridUtils.CalculatePathCost(npc.GridPosition, result.Path);
            float straight = Vector2.Distance(FootprintCentre(cell.Coords, npcSquares), straightPast);
            bool better = count < bestCount
                || (count == bestCount && cost < bestCost)
                || (count == bestCount && cost == bestCost && straight < bestStraight)
                || (count == bestCount && cost == bestCost && straight == bestStraight
                    && (cell.Coords.y < bestCell.y || (cell.Coords.y == bestCell.y && cell.Coords.x < bestCell.x)));
            if (!better)
                continue;
            bestCount = count;
            bestCost = cost;
            bestStraight = straight;
            bestCell = cell.Coords;
            path = new List<Vector2Int>(result.Path);
        }

        return path != null;
    }

    private static Vector2 FootprintCentre(Vector2Int baseSquare, int squares)
        => new Vector2(baseSquare.x + (squares - 1) * 0.5f, baseSquare.y + (squares - 1) * 0.5f);

    /// <summary>The overrun path to <paramref name="destination"/> for an AI-path mover (a scenario step names the square); false with the reason when there is none.</summary>
    public bool TryBuildOverrunPathForAI(CharacterController npc, Vector2Int destination, out List<Vector2Int> path, out string reason)
    {
        path = null;
        reason = null;
        if (npc == null || !CanUseOverrun(npc, out reason))
            return false;
        AoOPathResult result = FindOverrunPath(npc, destination, out reason);
        if (result == null)
            return false;
        if (!OverrunPathMeetsOpponent(npc, result.Path))
        {
            reason = "the path enters no opponent's space";
            return false;
        }
        path = new List<Vector2Int>(result.Path);
        return true;
    }

    /// <summary>The NPC turn path's overrun move along <paramref name="path"/> (the shared <see cref="RunOverrunMove"/>, at the AI's pace).</summary>
    public IEnumerator NPCExecuteOverrunForAI(CharacterController npc, List<Vector2Int> path, OverrunMoveOutcome outcome)
    {
        outcome = outcome ?? new OverrunMoveOutcome();
        yield return StartCoroutine(RunOverrunMove(npc, path, CheckForAoO(npc, path), GetPlayerMoveSecondsPerStepForAI(), outcome));
        // Victory or defeat when an AoO or the overrun dropped the last creature of a side (CORE-011).
        EvaluateCombatEnd("Overrun.NPC");
    }

    // ── The shared overrun ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The overrun path to <paramref name="destination"/> within the attacker's move (through allies and opponents), or null
    /// with the reason: no path, or a path that enters more than one opponent's space (one overrun attempt per round, PHB
    /// p.157) or the space of an opponent that cannot be overrun (<see cref="IsLegalOverrunOpponent"/>: more than one size
    /// category larger).
    /// </summary>
    private AoOPathResult FindOverrunPath(CharacterController attacker, Vector2Int destination, out string reason)
    {
        reason = null;
        if (attacker == null || attacker.Stats == null || Grid == null)
        {
            reason = "no attacker";
            return null;
        }

        AoOPathResult result = FindPath(
            attacker,
            destination,
            avoidThreats: false,
            maxRangeOverride: GetCurrentMoveRangeSquares(attacker),
            allowThroughAllies: true,
            allowThroughEnemies: true);
        if (result == null || result.Path == null || result.Path.Count == 0 || result.Path[result.Path.Count - 1] != destination)
        {
            reason = "no path";
            return null;
        }

        CharacterController met = null;
        foreach (Vector2Int step in result.Path)
        {
            foreach (CharacterController opponent in GetOverrunOpponentsAt(attacker, step, null))
            {
                if (met != null && opponent != met)
                {
                    reason = "only one overrun attempt per round (PHB p.157)";
                    return null;
                }
                if (met == null && !IsLegalOverrunOpponent(attacker, opponent, out reason))
                    return null;
                met = opponent;
            }
        }

        return result;
    }

    private static bool PathEntersAnySquare(CharacterController mover, List<Vector2Int> path, HashSet<Vector2Int> squares)
    {
        if (mover == null || path == null)
            return false;
        foreach (Vector2Int step in path)
            foreach (Vector2Int sq in mover.GetOccupiedSquaresAt(step))
                if (squares.Contains(sq))
                    return true;
        return false;
    }

    /// <summary>True when <paramref name="path"/> enters the space of an opponent it would have to overrun (the PC's destinations and a scripted destination; the AI plan checks its target's space).</summary>
    private bool OverrunPathMeetsOpponent(CharacterController attacker, List<Vector2Int> path)
    {
        if (attacker == null || path == null)
            return false;
        foreach (Vector2Int step in path)
            if (GetOverrunOpponentsAt(attacker, step, null).Count > 0)
                return true;
        return false;
    }

    /// <summary>
    /// An opponent whose space anyone may move through with no overrun, since it cannot block (PHB p.147: a helpless
    /// opponent's square): unconscious, dying, stable or helpless (paralyzed, asleep, bound;
    /// <see cref="CharacterController.IsHelplessForCoupDeGrace"/>); the one test the charge reads too (ChargeRules.IsPassableHelpless).
    /// </summary>
    private static bool IsPassableHelplessOpponent(CharacterController occupant)
        => ChargeRules.IsPassableHelpless(occupant);

    /// <summary>
    /// The living opponents whose space the attacker's footprint enters at <paramref name="basePosition"/>, other than those
    /// in <paramref name="passed"/> (a defender that let it by or was knocked prone) and helpless ones, whose squares need
    /// no overrun (PHB p.147; <paramref name="helpless"/>, when given, collects them).
    /// </summary>
    private List<CharacterController> GetOverrunOpponentsAt(CharacterController attacker, Vector2Int basePosition, HashSet<CharacterController> passed,
        List<CharacterController> helpless = null)
    {
        var opponents = new List<CharacterController>();
        if (attacker == null || Grid == null)
            return opponents;

        foreach (Vector2Int square in attacker.GetOccupiedSquaresAt(basePosition))
        {
            SquareCell cell = Grid.GetCell(square);
            if (cell == null || !cell.IsOccupied)
                continue;

            IReadOnlyList<CharacterController> occupants = cell.Occupants;
            for (int i = 0; i < occupants.Count; i++)
            {
                CharacterController occupant = occupants[i];
                if (occupant == null || occupant == attacker || occupant.Stats == null || occupant.Stats.IsDead)
                    continue;
                if (!TeamUtility.IsEnemy(attacker, occupant))
                    continue;
                if (passed != null && passed.Contains(occupant))
                    continue;
                if (IsPassableHelplessOpponent(occupant))
                {
                    if (helpless != null && !helpless.Contains(occupant))
                        helpless.Add(occupant);
                    continue;
                }
                if (!opponents.Contains(occupant))
                    opponents.Add(occupant);
            }
        }

        return opponents;
    }

    /// <summary>True when another living creature shares a square of the attacker's footprint (an ally it is passing through).</summary>
    private bool IsSharingSquare(CharacterController attacker)
    {
        if (attacker == null || Grid == null)
            return false;
        foreach (Vector2Int square in attacker.GetOccupiedSquares())
        {
            SquareCell cell = Grid.GetCell(square);
            if (cell == null || !cell.IsOccupied)
                continue;
            foreach (CharacterController occupant in cell.Occupants)
                if (occupant != null && occupant != attacker && occupant.Stats != null && !occupant.Stats.IsDead)
                    return true;
        }
        return false;
    }

    /// <summary>
    /// The one overrun move, PC and NPC alike (PHB p.157-158; CMB-015, CMB-086, D11). Spends the move action, then walks
    /// <paramref name="path"/> one square at a time: the AoOs for leaving each square first (PHB p.137, CMB-151), then the
    /// step through MoveAlongPath. Reaching an opponent's space is the overrun attempt (<see cref="ResolveOverrunAttempt"/>):
    /// leaving the square next to the defender provokes a movement AoO as any move does (PHB p.137); entering its space is
    /// a second, separate provocation (step 1, PHB p.157), which a defender with an AoO left (Combat Reflexes) may also take
    /// (delegated RAW reading 2026-10-29, RULES_COVERAGE 4.1); then its avoid-or-block choice and the check. A helpless
    /// opponent's space is passed with no attempt (PHB p.147). A second opponent on the path stops the movement before its
    /// space (one overrun attempt per round). The caller checks the combat end and continues the turn.
    /// </summary>
    internal IEnumerator RunOverrunMove(
        CharacterController attacker,
        List<Vector2Int> path,
        List<AoOThreatInfo> provokedAoOs,
        float secondsPerStep,
        OverrunMoveOutcome outcome)
    {
        outcome.Reset();
        if (attacker == null || attacker.Stats == null || path == null || path.Count == 0 || Grid == null)
        {
            outcome.StopReason = "no path";
            yield break;
        }

        if (!CanUseOverrun(attacker, out string unavailable))
        {
            outcome.StopReason = unavailable;
            yield break;
        }

        attacker.Actions.UseMoveAction();
        outcome.Started = true;

        var aoos = provokedAoOs != null ? new List<AoOThreatInfo>(provokedAoOs) : new List<AoOThreatInfo>();
        var passed = new HashSet<CharacterController>();
        var stepOutcome = new MovementAoOStepOutcome();
        Vector2Int lastSquareAlone = attacker.GridPosition;

        for (int pathIndex = 0; pathIndex < path.Count; pathIndex++)
        {
            Vector2Int step = path[pathIndex];
            CharacterController defender = null;
            var helpless = new List<CharacterController>();
            List<CharacterController> opponents = GetOverrunOpponentsAt(attacker, step, passed, helpless);
            // A helpless opponent's square is moved through freely (PHB p.147).
            foreach (CharacterController h in helpless)
                passed.Add(h);
            if (opponents.Count > 0)
            {
                string refusal = null;
                if (outcome.Attempted || opponents.Count > 1)
                    refusal = "only one overrun attempt per round (PHB p.157)";
                else if (!IsLegalOverrunOpponent(attacker, opponents[0], out string invalid))
                    refusal = invalid;
                if (refusal != null)
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⛔", $"{attacker.Stats.CharacterName} stops before {opponents[0].Stats.CharacterName}: {refusal}."));
                    outcome.StopReason = refusal;
                    break;
                }

                defender = opponents[0];
            }

            // A step the mover cannot take (a barrier, a hold, Grease's failed Balance check) provokes nothing: it never
            // leaves its square (PHB p.137, p.237).
            if (MovementService.IsStepRefused(attacker, attacker.GridPosition, step, MoveKind.Move))
            {
                outcome.StopReason = "blocked";
                break;
            }

            // AoOs for leaving the current square resolve before the step (PHB p.137).
            yield return StartCoroutine(ResolveMovementAoOsBeforeStep(attacker, aoos, pathIndex, "AoO", OverrunSecondsAfterAoO, stepOutcome));
            if (stepOutcome.StopMovement)
            {
                outcome.StopReason = stepOutcome.Reason;
                outcome.AttackerDown = stepOutcome.Incapacitated;
                outcome.StoppedByAoO = true;
                CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("⛔",
                    $"{attacker.Stats.CharacterName}'s movement stops ({stepOutcome.Reason}) after an attack of opportunity."));
                break;
            }

            if (defender != null)
            {
                bool goOn = false;
                yield return StartCoroutine(ResolveOverrunAttempt(attacker, defender, outcome, proceed => goOn = proceed));
                if (!goOn)
                    break;
                passed.Add(defender);
            }

            bool atEnd = pathIndex == path.Count - 1;
            yield return StartCoroutine(attacker.MoveAlongPath(new List<Vector2Int> { step }, secondsPerStep,
                markAsMoved: true, lastStepIsDestination: atEnd, allowThroughEnemies: passed.Count > 0));

            if (ThreatSystem.IsMoverIncapacitated(attacker))
            {
                outcome.AttackerDown = true;
                outcome.StopReason = "incapacitated";
                break;
            }

            // Blocked (MoveAlongPath refused the square): stop here rather than jump to the next square.
            if (attacker.GridPosition != step)
            {
                outcome.StopReason = "blocked";
                break;
            }

            if (!IsSharingSquare(attacker))
                lastSquareAlone = attacker.GridPosition;
        }

        // A move that stops in a square it only passes through (an ally's) ends in the last square it stood alone in
        // (a lost block, CMB-117). An AoO that stops the attacker leaves it where it stood, as for any move (ExecuteMovement).
        if (!outcome.AttackerDown && !outcome.StoppedByAoO && IsSharingSquare(attacker))
            ReturnToSquare(attacker, lastSquareAlone);

        if (outcome.Attempted && !outcome.Avoided && outcome.StopReason == null)
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{attacker.Stats.CharacterName} completes the overrun!"));

        CheckTurnUndeadProximityBreakingForMover(attacker);
        PruneTurnUndeadTrackers();
        InvalidatePreviewThreats();
        UpdateAllStatsUI();
    }

    /// <summary>
    /// The overrun attempt at the edge of <paramref name="defender"/>'s space (PHB p.157-158 steps 1-4).
    /// <paramref name="proceed"/> answers true when the attacker moves on into the space (avoided, or the block lost by
    /// the defender), false when its movement ends here.
    /// </summary>
    private IEnumerator ResolveOverrunAttempt(
        CharacterController attacker,
        CharacterController defender,
        OverrunMoveOutcome outcome,
        Action<bool> proceed)
    {
        outcome.Attempted = true;
        outcome.Defender = defender;
        string attackerName = attacker.Stats.CharacterName;
        string defenderName = defender.Stats.CharacterName;
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("➤", $"{attackerName} tries to overrun {defenderName}!"));

        // Step 1: moving into the defender's space provokes its AoO, Improved Overrun or not (PHB p.157, p.96; CMB-086).
        ThreatSystem.MoverAoOSnapshot before = ThreatSystem.CaptureMoverState(attacker);
        ManeuverAoOOutcome aoo = ResolveManeuverInitiationAoOs(attacker, defender, SpecialAttackType.Overrun);
        if (aoo == ManeuverAoOOutcome.AttackerIncapacitated || ThreatSystem.IsMoverIncapacitated(attacker))
        {
            outcome.AttackerDown = true;
            outcome.StopReason = "incapacitated";
            proceed(false);
            yield break;
        }

        if (ThreatSystem.ShouldStopMovementAfterAoO(attacker, before, out string stopReason))
        {
            // The attempt was made and not avoided, so it is the overrun's standard action (delegated RAW reading
            // 2026-10-29, RULES_COVERAGE 4.1); the movement ends where the attacker stands.
            outcome.StandardSpent = attacker.CommitStandardAction();
            outcome.StoppedByAoO = true;
            outcome.StopReason = stopReason;
            CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("⛔", $"{attackerName}'s overrun stops ({stopReason}) after {defenderName}'s attack of opportunity."));
            proceed(false);
            yield break;
        }

        if (defender.IsDead || defender.Stats.IsDead || IsPassableHelplessOpponent(defender))
        {
            // Its own AoO's retribution (Fire Shield) can kill the defender or leave it helpless: a dead or helpless
            // creature does not block, and its square is moved through freely (PHB p.147).
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{defenderName} falls; {attackerName} moves on."));
            proceed(true);
            yield break;
        }

        // Step 2: the defender avoids or blocks; Improved Overrun forbids avoiding (PHB p.96).
        bool block = true;
        if (attacker.Stats.HasFeat("Improved Overrun"))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{defenderName} cannot avoid {attackerName} (Improved Overrun)."));
        }
        else
        {
            bool answered = false;
            ManeuverDeciders.For(defender).ChooseOverrunResponse(this, defender, attacker, b => { block = b; answered = true; });
            while (!answered && CurrentPhase != TurnPhase.CombatOver)
                yield return null;
            if (!answered)
            {
                outcome.StopReason = "combat over";
                proceed(false);
                yield break;
            }
        }

        if (!block)
        {
            // The attempt costs no action (PHB p.157), only the movement into the space.
            outcome.Avoided = true;
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("", $"{defenderName} avoids the overrun and lets {attackerName} by."));
            proceed(true);
            yield break;
        }

        // Step 3: the block, the opposed check (PHB p.157), as the overrun's standard action.
        if (!attacker.CommitStandardAction())
        {
            // CanUseOverrun checked both actions at the start; with no standard action left there is nothing to block with.
            outcome.StopReason = "no standard action";
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("", $"{attackerName} has no standard action left; the overrun ends."));
            proceed(false);
            yield break;
        }
        outcome.StandardSpent = true;
        outcome.Blocked = true;
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{defenderName} blocks!"));
        SpecialAttackResult result = attacker.ExecuteSpecialAttack(SpecialAttackType.Overrun, defender);
        CombatUI?.ShowCombatLog(CombatLogHelper.Buff("⚔", $"SPECIAL [Overrun]: {result.Log}"));
        UpdateAllStatsUI();

        // Step 4: a winner goes on; a loser moves 5 ft back the way it came, ending its movement there: the square it
        // entered the attempt from, or, when that square is another creature's, the last square it stood alone in
        // (RunOverrunMove; RAW has it fall prone in the occupied square, CMB-117, as the defender's knock-prone counter,
        // which is not rolled).
        if (result.Success)
        {
            outcome.Succeeded = true;
            proceed(true);
            yield break;
        }

        outcome.StopReason = "blocked by " + defenderName;
        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("", $"{defenderName} stops the overrun; {attackerName}'s movement ends."));
        proceed(false);
    }

    private void ReturnToSquare(CharacterController mover, Vector2Int square)
    {
        if (mover == null || Grid == null || mover.GridPosition == square)
            return;
        SquareCell cell = Grid.GetCell(square);
        if (cell != null)
            mover.MoveToCell(cell, markAsMoved: true);
    }

    // ── The defender's choice (D11) ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The UI decider's avoid-or-block prompt for a controllable defender (PHB p.157 step 2; D11): "Block" or "Avoid".
    /// False while another one is open or without a UI (the AI then answers).
    /// </summary>
    internal bool PromptOverrunResponse(CharacterController defender, CharacterController attacker, Action<bool> answer)
    {
        if (CombatUI == null || defender == null || attacker == null || defender.Stats == null || attacker.Stats == null
            || _overrunResponsePromptAnswer != null)
            return false;

        string defenderName = defender.Stats.CharacterName;
        string attackerName = attacker.Stats.CharacterName;
        CombatUI.ShowCombatLog(CombatLogHelper.Warning("", $"Waiting for {defenderName}'s decision: avoid or block {attackerName}'s overrun..."));

        _overrunResponsePromptDefender = defender;
        _overrunResponsePromptAnswer = answer;
        CombatUI.ShowConfirmationDialog(
            title: $"OVERRUN - {defenderName}",
            message: $"{attackerName} tries to overrun {defenderName}. Avoid (let {attackerName} pass, no ill effect) or block "
                + $"(an opposed Strength check: the loser is knocked prone if it is {defenderName}, or stopped if it is {attackerName})?",
            confirmLabel: "Block",
            cancelLabel: "Avoid",
            onConfirm: () => AnswerOverrunResponsePrompt(true),
            onCancel: () => AnswerOverrunResponsePrompt(false));
        return true;
    }

    /// <summary>True while a controllable defender's overrun prompt waits for its answer (D11).</summary>
    public bool IsAwaitingOverrunResponseChoice => _overrunResponsePromptAnswer != null;

    /// <summary>The prompt's answer: true = Block, false = Avoid.</summary>
    private void AnswerOverrunResponsePrompt(bool block)
    {
        Action<bool> answer = _overrunResponsePromptAnswer;
        _overrunResponsePromptAnswer = null;
        _overrunResponsePromptDefender = null;
        answer?.Invoke(block);
    }

    /// <summary>Drops an unanswered overrun prompt without resolving it (a combat reset or a halted fight).</summary>
    private void ClearOverrunResponsePrompt()
    {
        bool wasOpen = _overrunResponsePromptAnswer != null;
        _overrunResponsePromptAnswer = null;
        _overrunResponsePromptDefender = null;
        if (wasOpen)
            CombatUI?.HideConfirmationDialog();
    }

    // ── Target rules ────────────────────────────────────────────────────────────────────────────────────

    private bool IsOverrunTargetSizeLegal(CharacterController attacker, CharacterController target, out string reason)
    {
        reason = string.Empty;
        if (attacker == null || target == null || attacker.Stats == null || target.Stats == null)
        {
            reason = "Invalid";
            return false;
        }

        // PHB p.157: one size category larger than the attacker at most.
        int sizeDelta = (int)target.GetCurrentSizeCategory() - (int)attacker.GetCurrentSizeCategory();
        if (sizeDelta > 1)
        {
            reason = "Target too large";
            return false;
        }

        return true;
    }

    /// <summary>
    /// May <paramref name="attacker"/> overrun <paramref name="target"/>: a living opponent, not helpless (PHB p.147), at most
    /// one size category larger (PHB p.157). With <paramref name="requireAdjacency"/>, also adjacent (not sharing the attacker's square, owner
    /// interpretation 2026-10-11, CMB-154) with room for the attacker in the defender's space; the move-through and the AI
    /// plan check the path instead.
    /// </summary>
    private bool IsValidOverrunTarget(CharacterController attacker, CharacterController target, out string reason, bool requireAdjacency)
    {
        reason = string.Empty;
        if (attacker == null || target == null || attacker.Stats == null || target.Stats == null)
        {
            reason = "Invalid";
            return false;
        }

        if (attacker == target || target.Stats.IsDead)
        {
            reason = "Invalid target";
            return false;
        }

        if (IsPassableHelplessOpponent(target))
        {
            // A helpless opponent's square is moved through with no overrun (PHB p.147).
            reason = "Helpless: move through";
            return false;
        }

        if (!TeamUtility.IsEnemy(attacker, target))
        {
            reason = "Not enemy";
            return false;
        }

        if (!IsOverrunTargetSizeLegal(attacker, target, out reason))
            return false;

        if (!requireAdjacency)
            return true;

        // Adjacent only: the overrunner moves through the defender's space (PHB p.157), so a creature sharing the
        // overrunner's square cannot be overrun (owner interpretation 2026-10-11, CMB-154).
        int distance = attacker.GetMinimumDistanceToTarget(target, chebyshev: true);
        if (distance != 1)
        {
            reason = "Not adjacent";
            return false;
        }

        if (!CanAttackerOccupyDefenderSquareIfVacated(attacker, target))
        {
            reason = "No room to pass";
            return false;
        }

        return true;
    }

    /// <summary><see cref="IsValidOverrunTarget(CharacterController, CharacterController, out string, bool)"/> without the adjacency test (the path's opponents, the AI plan).</summary>
    private bool IsLegalOverrunOpponent(CharacterController attacker, CharacterController target, out string reason)
        => IsValidOverrunTarget(attacker, target, out reason, requireAdjacency: false);

    private bool CanAttackerOccupyDefenderSquareIfVacated(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null || Grid == null)
            return false;

        Vector2Int destination = target.GridPosition;
        int attackerSizeSquares = attacker.GetVisualSquaresOccupied();

        // Treat target's occupied footprint as temporarily vacated for legality checks.
        return Grid.CanPlaceCreature(
            destination,
            attackerSizeSquares,
            ignoreOccupant: attacker,
            ignoreOtherOccupants: false,
            additionalIgnoredOccupants: new List<CharacterController> { target });
    }
}
