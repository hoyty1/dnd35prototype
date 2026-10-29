using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The kinds of movement a creature may start (PHB p.143-144, p.154; MovementService.CanStartMovement). Batch 12 step 22.
/// </summary>
public enum MoveKind
{
    /// <summary>One move action (a standard action converted to a move counts).</summary>
    Move,
    /// <summary>Both move actions as one movement, up to double speed.</summary>
    DoubleMove,
    /// <summary>A full-round run, up to four times speed (PHB p.144).</summary>
    Run,
    /// <summary>A full-round withdraw, up to double speed; the start square provokes from no seen enemy (PHB p.143).</summary>
    Withdraw,
    /// <summary>A withdraw as a standard action, up to speed, by a creature limited to a single action (PHB p.144).</summary>
    RestrictedWithdraw,
    /// <summary>A 5-foot step (PHB p.144).</summary>
    FiveFootStep,
    /// <summary>A charge's movement (PHB p.154; the charge core's own rules decide its lane and target).</summary>
    Charge
}

/// <summary>
/// What a movement executor did with a movement request (GameManager.MoveCharacterAlongComputedPath[Withdraw]): refused
/// by <see cref="MovementService.CanStartMovement"/>, with its reason, or started. An AI routine reads it and goes on.
/// </summary>
public sealed class MoveAttempt
{
    public bool Started;
    public bool Refused;
    public string Reason = string.Empty;
}

/// <summary>
/// Owns reusable movement logic (grid validation, pathfinding, movement cost/range, AoO checks, and 5-foot-step rules).
/// GameManager delegates movement-specific calculations and execution helpers to this service.
/// </summary>
public class MovementService : MonoBehaviour
{
    [SerializeField] private SquareGrid _grid;

    private Func<List<CharacterController>> _allCharactersProvider;
    private readonly HashSet<Vector2Int> _difficultTerrainSquares = new HashSet<Vector2Int>();

    public void Initialize(SquareGrid grid, Func<List<CharacterController>> allCharactersProvider)
    {
        _grid = grid;
        _allCharactersProvider = allCharactersProvider;
    }

    public void SetGrid(SquareGrid grid)
    {
        _grid = grid;
    }

    public SquareGrid Grid => _grid;

    // ========== GRID / POSITION HELPERS ==========

    public bool ValidateGridPosition(Vector2Int position)
    {
        return _grid != null && _grid.GetCell(position) != null;
    }

    public bool IsPositionValid(Vector2Int position)
    {
        return ValidateGridPosition(position);
    }

    public CharacterController GetCharacterAtPosition(Vector2Int position, CharacterController ignore = null)
    {
        if (!ValidateGridPosition(position))
            return null;

        SquareCell cell = _grid.GetCell(position);
        if (cell == null || !cell.IsOccupied)
            return null;

        IReadOnlyList<CharacterController> occupants = cell.Occupants;
        for (int i = 0; i < occupants.Count; i++)
        {
            CharacterController occupant = occupants[i];
            if (occupant == null || occupant == ignore)
                continue;

            if (occupant.Stats == null || occupant.Stats.IsDead)
                continue;

            return occupant;
        }

        return null;
    }

    public bool IsSquareOccupied(Vector2Int position, CharacterController ignore = null)
    {
        return GetCharacterAtPosition(position, ignore) != null;
    }

    public bool IsPositionBlocked(Vector2Int position, int moverSizeSquares = 1, CharacterController mover = null)
    {
        if (!ValidateGridPosition(position))
            return true;

        if (_grid == null)
            return true;

        return !_grid.CanPlaceCreature(position, Mathf.Max(1, moverSizeSquares), mover);
    }

    public List<Vector2Int> GetAdjacentSquares(Vector2Int origin)
    {
        Vector2Int[] neighbors = SquareGridUtils.GetNeighbors(origin);
        return new List<Vector2Int>(neighbors);
    }

    public int CalculateDistance(Vector2Int from, Vector2Int to, bool chebyshev = false)
    {
        return chebyshev ? SquareGridUtils.GetChebyshevDistance(from, to) : SquareGridUtils.GetDistance(from, to);
    }

    public List<Vector2Int> GetSquaresInRange(Vector2Int origin, int range, bool includeOrigin = false)
    {
        var result = new List<Vector2Int>();
        if (_grid == null)
            return result;

        List<SquareCell> cells = _grid.GetCellsInRange(origin, Mathf.Max(0, range));
        for (int i = 0; i < cells.Count; i++)
        {
            SquareCell cell = cells[i];
            if (cell == null)
                continue;
            if (!includeOrigin && cell.Coords == origin)
                continue;

            result.Add(cell.Coords);
        }

        return result;
    }

    // ========== MOVEMENT RANGE / COST ==========

    public List<SquareCell> CalculateMovementRange(CharacterController mover, int maxRangeOverride = -1)
    {
        var reachable = new List<SquareCell>();
        if (_grid == null || mover == null || mover.Stats == null)
            return reachable;

        int movementRange = maxRangeOverride >= 0 ? maxRangeOverride : mover.Stats.MoveRange;
        movementRange = Mathf.Max(0, movementRange);
        List<SquareCell> cells = _grid.GetCellsInRange(mover.GridPosition, movementRange);
        for (int i = 0; i < cells.Count; i++)
        {
            SquareCell cell = cells[i];
            if (cell == null || cell.Coords == mover.GridPosition)
                continue;

            if (!IsValidMoveDestination(mover, cell.Coords))
                continue;

            // Intact Wall of Ice cells block movement — only breached cells allow passage.
            if (WallOfIceAreaEffect.DoesCellBlockMovement(cell.Coords))
                continue;

            reachable.Add(cell);
        }

        return reachable;
    }

    public int GetMovementCost(Vector2Int start, List<Vector2Int> path)
    {
        if (path == null || path.Count == 0)
            return 0;

        int cost = SquareGridUtils.CalculatePathCost(start, path);

        // Difficult terrain doubles movement cost for each square entered.
        // Base path cost already includes the first cost per square.
        for (int i = 0; i < path.Count; i++)
        {
            if (IsDifficultTerrain(path[i]))
                cost += 1;
        }

        return cost;
    }

    /// <summary>
    /// Whether <paramref name="mover"/> may end a move with its footprint based at <paramref name="destination"/>:
    /// on the grid and not sharing a square with a living creature (PHB p.148), except that a swarm may end in
    /// an enemy's square (MM p.316). The same rule <see cref="FindPath(CharacterController, Vector2Int, bool, int?, bool, bool, bool)"/>
    /// applies to the path's last node, so the PC highlight and click check agree with the AI's paths (GRID-001).
    /// </summary>
    public bool IsValidMoveDestination(CharacterController mover, Vector2Int destination)
    {
        if (_grid == null || mover == null || mover.Stats == null)
            return false;

        bool moverIsSwarm = mover.Stats.IsSwarm;
        return _grid.CanTraversePathNode(
            destination,
            mover.GetVisualSquaresOccupied(),
            mover,
            isDestinationNode: true,
            allowThroughAllies: true,
            allowThroughEnemies: moverIsSwarm,
            allowDestinationEnemyOverlap: moverIsSwarm);
    }

    // ========== PATHFINDING ==========

    public AoOPathResult FindPath(
        CharacterController mover,
        Vector2Int destination,
        bool avoidThreats = true,
        int? maxRangeOverride = null,
        bool allowThroughAllies = true,
        bool allowThroughEnemies = false,
        bool suppressFirstSquareAoO = false)
    {
        var result = new AoOPathResult();

        if (_grid == null || mover == null || mover.Stats == null)
            return result;

        int maxRange = Mathf.Max(1, maxRangeOverride ?? mover.Stats.MoveRange);

        bool moverIsSwarm = mover != null && mover.Stats != null && mover.Stats.IsSwarm;
        allowThroughEnemies = allowThroughEnemies || moverIsSwarm;
        bool allowDestinationEnemyOverlap = moverIsSwarm;

        // avoidThreats only makes the A* prefer routes that leave fewer threatened squares (the PC click
        // path asks for it), and falls back to the direct route when the detour would stop short. Either way
        // the caller's range, the pass-through flags and the Withdraw first-square rule apply exactly as for
        // the AI movers, which pass avoidThreats: false (GRID-001).
        HashSet<Vector2Int> threatenedSquares = null;
        if (avoidThreats)
        {
            List<CharacterController> allCharacters = _allCharactersProvider != null
                ? _allCharactersProvider.Invoke()
                : new List<CharacterController>();
            threatenedSquares = ThreatSystem.GetSquaresThreatenedByEnemies(mover, allCharacters);
        }

        return FindPath(
            mover,
            destination,
            threatenedSquares: threatenedSquares,
            maxRangeOverride: maxRange,
            allowThroughAllies: allowThroughAllies,
            allowThroughEnemies: allowThroughEnemies,
            allowDestinationEnemyOverlap: allowDestinationEnemyOverlap,
            suppressFirstSquareAoO: suppressFirstSquareAoO);
    }

    public AoOPathResult FindPath(
        CharacterController mover,
        Vector2Int destination,
        HashSet<Vector2Int> threatenedSquares,
        int maxRangeOverride,
        bool allowThroughAllies = true,
        bool allowThroughEnemies = false,
        bool allowDestinationEnemyOverlap = false,
        bool suppressFirstSquareAoO = false)
    {
        var result = new AoOPathResult();

        if (_grid == null || mover == null || mover.Stats == null)
            return result;

        int maxRange = Mathf.Max(1, maxRangeOverride);

        result = _grid.FindPathAoOAware(
            mover.GridPosition,
            destination,
            threatenedSquares,
            maxRange: maxRange,
            moverSizeSquares: mover.GetVisualSquaresOccupied(),
            mover: mover,
            allowThroughAllies: allowThroughAllies,
            allowThroughEnemies: allowThroughEnemies,
            allowDestinationEnemyOverlap: allowDestinationEnemyOverlap) ?? new AoOPathResult();

        // The threat cost only steers the route. A* prunes by straight-line distance, not path cost, so a
        // threat-avoiding detour can cost more than the budget and come back trimmed short of the destination
        // (or not at all) where the direct route, the one an AI mover takes, fits. Then use the direct route,
        // so the PC click path never reaches less far than the AI's (GRID-001).
        if (threatenedSquares != null && threatenedSquares.Count > 0 && !EndsAt(result, destination))
        {
            AoOPathResult direct = _grid.FindPathAoOAware(
                mover.GridPosition,
                destination,
                null,
                maxRange: maxRange,
                moverSizeSquares: mover.GetVisualSquaresOccupied(),
                mover: mover,
                allowThroughAllies: allowThroughAllies,
                allowThroughEnemies: allowThroughEnemies,
                allowDestinationEnemyOverlap: allowDestinationEnemyOverlap);
            if (EndsAt(direct, destination))
                result = direct;
        }

        if (result.Path != null && result.Path.Count > 0)
            result.ProvokedAoOs = CheckForAoO(mover, result.Path, suppressFirstSquareAoO);

        return result;
    }

    private static bool EndsAt(AoOPathResult result, Vector2Int destination)
        => result != null && result.Path != null && result.Path.Count > 0 && result.Path[result.Path.Count - 1] == destination;

    // ========== ATTACKS OF OPPORTUNITY ==========

    public List<AoOThreatInfo> CheckForAoO(CharacterController mover, List<Vector2Int> path, bool suppressFirstSquareAoO = false)
    {
        if (mover == null)
            return new List<AoOThreatInfo>();

        List<CharacterController> allCharacters = _allCharactersProvider != null
            ? _allCharactersProvider.Invoke()
            : new List<CharacterController>();

        return ThreatSystem.AnalyzePathForAoOs(mover, path, allCharacters, suppressFirstSquareAoO);
    }

    public CombatResult TriggerAoO(CharacterController threatener, CharacterController target)
    {
        if (threatener == null || target == null)
            return null;

        if (threatener.Stats == null || threatener.Stats.IsDead)
            return null;

        if (!ThreatSystem.CanMakeAoOAgainst(threatener, target))
            return null;

        return ThreatSystem.ExecuteAoO(threatener, target, isFromMovement: true);
    }

    // ========== MOVEMENT EXECUTION ==========

    public IEnumerator ExecuteMovement(CharacterController mover, List<Vector2Int> path, float secondsPerStep, bool markAsMoved = true, bool lastStepIsDestination = true)
    {
        if (mover == null || path == null || path.Count == 0)
            yield break;

        yield return mover.MoveAlongPath(path, secondsPerStep, markAsMoved, lastStepIsDestination);
    }

    public bool CanTake5FootStep(CharacterController character, out string reason)
    {
        // The shared movement legality (CanStartMovement, batch 12 step 22): the PC 5-ft button and the AI's steps alike.
        Legality legal = CanStartMovement(character, MoveKind.FiveFootStep, 1);
        if (!legal.Ok)
        {
            reason = legal.Reason;
            return false;
        }

        reason = string.Empty;
        List<Vector2Int> adjacent = GetAdjacentSquares(character.GridPosition);
        for (int i = 0; i < adjacent.Count; i++)
        {
            if (IsValidAdjacentStepDestination(character, adjacent[i], disallowDifficultTerrain: true))
                return true;
        }

        reason = "No valid adjacent square";
        return false;
    }

    public bool CanTake5FootStep(CharacterController character, Vector2Int destination)
    {
        if (!CanStartMovement(character, MoveKind.FiveFootStep, 1).Ok)
            return false;

        return IsValidAdjacentStepDestination(character, destination, disallowDifficultTerrain: true);
    }

    // ========== MOVEMENT LEGALITY (batch 12 step 22; GRID-019, GRID-022) ==========

    /// <summary>
    /// The one movement legality check for every mover, PC or NPC (PHB p.143-144): may <paramref name="actor"/> start a
    /// movement of this <paramref name="kind"/> covering <paramref name="squares"/> squares (0 or less: any distance within
    /// the kind's budget)? Pure: no dice, no log, no action spent. The PC buttons (Move, Withdraw, 5-Foot Step), the NPC
    /// movement executors (GameManager.MoveCharacterAlongComputedPath[Withdraw]), the AI's 5-foot steps and withdraws and
    /// the compulsion controllers all ask it.
    /// <list type="bullet">
    /// <item>No movement while prone (stand up or crawl first, PHB p.142-143), grappling or pinned (only the grapple's own
    /// move, PHB p.156-157), held by a condition that prevents movement, or with no speed left this turn (a web).</item>
    /// <item>A 5-foot step: not in a round with any other movement, only one, and not with a speed of 5 ft or less
    /// (PHB p.144). Any other movement: not after a 5-foot step (PHB p.144; p.143 for the withdraw).</item>
    /// <item>Withdraw: a full-round action, double speed, not while blinded (PHB p.143). Restricted Withdraw: a creature
    /// limited to a single action (<see cref="ActionEconomy.SingleActionOnly"/>: slowed, disabled, staggered) withdraws as
    /// a standard action, up to its speed (PHB p.144); <see cref="WithdrawKindFor"/> picks the kind.</item>
    /// <item>A creature limited to a move action (<see cref="ActionEconomy.MoveActionOnly"/>, nauseated) may move, but
    /// never double move, run, withdraw or charge; a single-action creature never double moves or runs.</item>
    /// <item>No run or charge while entangled (PHB p.309), fatigued (p.308) or exhausted; a run covers four times the
    /// speed, three times in heavy armor or with a heavy load (<see cref="RunMultiplier"/>, PHB p.144, p.162).</item>
    /// <item>A charge takes the charge core's action rule (<see cref="ChargeRules.HasChargeAction"/>, D2): a full-round
    /// action at double speed, or a partial charge at speed. ChargeCore.CanCharge asks this check for the charge's
    /// movement; its lane, target and attack stay the charge core's.</item>
    /// </list>
    /// </summary>
    public static Legality CanStartMovement(CharacterController actor, MoveKind kind, int squares = 0)
    {
        if (actor == null || actor.Stats == null || actor.Actions == null)
            return Legality.Refused("No active character");

        ActionEconomy actions = actor.Actions;
        bool step = kind == MoveKind.FiveFootStep;
        bool withdraw = kind == MoveKind.Withdraw || kind == MoveKind.RestrictedWithdraw;

        // A disabled creature whose strenuous act will leave it dying takes nothing further this turn (PHB p.145).
        if (actions.NoFurtherActions)
            return Legality.Refused("Acting while disabled has spent this turn");

        if (actor.HasCondition(CombatConditionType.Prone))
            return Legality.Refused(step ? "Cannot 5-foot step while prone" : "Stand up first");

        if (actor.HasCondition(CombatConditionType.Pinned))
            return Legality.Refused(step ? "Cannot 5-foot step while pinned" : withdraw ? "Pinned creatures cannot withdraw" : "Pinned: grapple escape only");

        if (actor.IsGrappling())
            return Legality.Refused(step ? "Cannot 5-foot step while grappled" : withdraw ? "Cannot withdraw while grappled" : "Grappling: only the grapple's own move");

        if (actor.Stats.MovementBlockedByCondition)
            return Legality.Refused("Movement blocked by condition");

        int speed = SingleMoveBudgetSquares(actor);
        if (speed <= 0)
            return Legality.Refused("Cannot move this turn");

        if (step)
        {
            if (actor.HasMovedThisTurn)
                return Legality.Refused("Already moved this turn");
            if (actor.HasTakenFiveFootStep)
                return Legality.Refused("Already used 5-foot step this turn");
            if (speed <= 1)
                return Legality.Refused("A speed of 5 ft or less allows no 5-foot step");
            if (squares > 1)
                return Legality.Refused("A 5-foot step moves one square");
            return Legality.Allowed();
        }

        if (actor.HasTakenFiveFootStep)
            return Legality.Refused(withdraw ? "Cannot withdraw after a 5-foot step" : "Cannot move after a 5-foot step");

        int budget;
        switch (kind)
        {
            case MoveKind.Move:
                if (!actions.HasMoveAction && !actions.CanConvertStandardToMove)
                    return Legality.Refused("No move action available");
                budget = speed;
                break;

            case MoveKind.DoubleMove:
            case MoveKind.Run:
            {
                string name = kind == MoveKind.Run ? "run" : "double move";
                if (actions.MoveActionOnly)
                    return Legality.Refused("Limited to a move action: no " + name);
                if (actions.SingleActionOnly)
                    return Legality.Refused("Limited to a single action: no " + name);
                if (!actions.HasFullRoundAction)
                    return Legality.Refused(kind == MoveKind.Run ? "Requires a full-round action" : "Requires both move actions");
                // Entangled (PHB p.309), fatigued (p.308) or exhausted creatures cannot run; the charge refuses them too.
                if (kind == MoveKind.Run && actor.Stats.CannotRunOrCharge)
                    return Legality.Refused(RunOrChargeRefusal(actor, "run"));
                budget = kind == MoveKind.Run ? speed * RunMultiplier(actor) : speed * 2;
                break;
            }

            case MoveKind.Withdraw:
            case MoveKind.RestrictedWithdraw:
                if (actor.HasCondition(CombatConditionType.Blinded))
                    return Legality.Refused("Cannot withdraw while blinded");
                if (actions.MoveActionOnly)
                    return Legality.Refused("Limited to a move action: no withdraw");
                if (kind == MoveKind.Withdraw)
                {
                    if (actions.SingleActionOnly)
                        return Legality.Refused("Limited to a single action: only a Restricted Withdraw (standard action, speed)");
                    if (!actions.HasFullRoundAction)
                        return Legality.Refused("Requires a full-round action");
                    budget = speed * 2;
                }
                else
                {
                    if (!actions.SingleActionOnly)
                        return Legality.Refused("Restricted Withdraw is for a creature limited to a single action");
                    if (!actions.HasStandardAction)
                        return Legality.Refused("Needs the turn's single action");
                    budget = speed;
                }
                break;

            case MoveKind.Charge:
                if (!ChargeRules.HasChargeAction(actor, out bool partial, out string chargeReason))
                    return Legality.Refused(chargeReason);
                if (actor.Stats.CannotRunOrCharge)
                    return Legality.Refused(RunOrChargeRefusal(actor, "charge"));
                budget = partial ? speed : speed * 2;
                break;

            default:
                return Legality.Refused("Unknown movement");
        }

        if (squares > budget)
            return Legality.Refused($"{squares * 5} ft is beyond the {budget * 5} ft this movement allows");

        return Legality.Allowed();
    }

    /// <summary>
    /// A run's speed multiplier (PHB p.144): four times the speed, three times in heavy armor (mithral counts one category
    /// lighter, DMG p.284) or with a heavy load (PHB p.162, Table 7-7).
    /// </summary>
    public static int RunMultiplier(CharacterController actor)
    {
        if (actor == null || actor.Stats == null)
            return 4;
        CharacterStats stats = actor.Stats;
        bool heavyArmor = stats.EquippedArmorItem != null && stats.EquippedArmorItem.IsArmor
            && MaterialProperties.GetEffectiveArmorCategory(stats.EquippedArmorItem) == ArmorCategory.Heavy;
        bool heavyLoad = stats.CurrentEncumbrance == EncumbranceLevel.Heavy || stats.CurrentEncumbrance == EncumbranceLevel.Overloaded;
        return heavyArmor || heavyLoad ? 3 : 4;
    }

    private static string RunOrChargeRefusal(CharacterController actor, string what)
    {
        CharacterStats stats = actor.Stats;
        string state = stats.IsEntangledState ? "Entangled" : stats.IsExhaustedState ? "Exhausted" : "Fatigued";
        return $"{state} creatures cannot {what}";
    }

    /// <summary>
    /// The withdraw <paramref name="actor"/> may take: a Restricted Withdraw (a standard action at speed) when it is
    /// limited to a single action (PHB p.144), otherwise the full-round withdraw at double speed (PHB p.143).
    /// </summary>
    public static MoveKind WithdrawKindFor(CharacterController actor)
        => actor != null && actor.Actions != null && actor.Actions.SingleActionOnly ? MoveKind.RestrictedWithdraw : MoveKind.Withdraw;

    /// <summary>The withdraw's movement budget in squares: speed for a Restricted Withdraw, double speed otherwise.</summary>
    public static int WithdrawBudgetSquares(CharacterController actor)
    {
        int speed = SingleMoveBudgetSquares(actor);
        if (speed <= 0)
            return 0;
        return WithdrawKindFor(actor) == MoveKind.RestrictedWithdraw ? speed : speed * 2;
    }

    /// <summary>
    /// One move action's budget in squares: GameManager.GetCurrentMoveRangeSquares (0 while grappling, prone or held in a
    /// web), or, with no GameManager, the same rules without the web.
    /// </summary>
    public static int SingleMoveBudgetSquares(CharacterController actor)
    {
        if (actor == null || actor.Stats == null)
            return 0;

        GameManager gm = GameManager.Instance;
        if (gm != null)
            return gm.GetCurrentMoveRangeSquares(actor);

        if (actor.IsGrappling() || actor.Stats.IsProne)
            return 0;
        return Mathf.Max(0, actor.Stats.MoveRange);
    }

    public bool IsValidAdjacentStepDestination(CharacterController character, Vector2Int destination, bool disallowDifficultTerrain = true)
    {
        if (character == null)
            return false;

        if (!SquareGridUtils.IsAdjacent(character.GridPosition, destination))
            return false;

        if (!ValidateGridPosition(destination))
            return false;

        if (_grid == null)
            return false;

        if (!_grid.CanPlaceCreature(destination, character.GetVisualSquaresOccupied(), character))
            return false;

        if (disallowDifficultTerrain && IsDifficultTerrain(destination))
            return false;

        // Intact Wall of Ice cells block movement (including 5-foot steps).
        if (WallOfIceAreaEffect.DoesCellBlockMovement(destination))
            return false;

        return true;
    }

    public bool Execute5FootStep(CharacterController character, SquareCell destinationCell)
    {
        if (character == null || destinationCell == null)
            return false;

        if (!CanTake5FootStep(character, destinationCell.Coords))
            return false;

        return character.FiveFootStep(destinationCell);
    }

    // ========== DIFFICULT TERRAIN ==========

    public bool IsDifficultTerrain(Vector2Int position)
    {
        // Hook for data-driven terrain in future milestones.
        return _difficultTerrainSquares.Contains(position);
    }

    public void SetDifficultTerrain(Vector2Int position, bool isDifficult)
    {
        if (isDifficult)
            _difficultTerrainSquares.Add(position);
        else
            _difficultTerrainSquares.Remove(position);
    }

    public void ClearDifficultTerrain()
    {
        _difficultTerrainSquares.Clear();
    }
}
