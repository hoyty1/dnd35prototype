using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        int moverSize = mover.GetVisualSquaresOccupied();

        List<SquareCell> cells = _grid.GetCellsInRange(mover.GridPosition, movementRange);
        for (int i = 0; i < cells.Count; i++)
        {
            SquareCell cell = cells[i];
            if (cell == null || cell.Coords == mover.GridPosition)
                continue;

            if (!_grid.CanPlaceCreature(cell.Coords, moverSize, mover))
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

        if (avoidThreats)
        {
            List<CharacterController> allCharacters = _allCharactersProvider != null
                ? _allCharactersProvider.Invoke()
                : new List<CharacterController>();

            result = _grid.FindSafePath(mover.GridPosition, destination, mover, allCharacters);
            return result ?? new AoOPathResult();
        }

        return FindPath(
            mover,
            destination,
            threatenedSquares: null,
            maxRangeOverride: maxRange,
            allowThroughAllies: allowThroughAllies,
            allowThroughEnemies: allowThroughEnemies,
            suppressFirstSquareAoO: suppressFirstSquareAoO);
    }

    public AoOPathResult FindPath(
        CharacterController mover,
        Vector2Int destination,
        HashSet<Vector2Int> threatenedSquares,
        int maxRangeOverride,
        bool allowThroughAllies = true,
        bool allowThroughEnemies = false,
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
            allowThroughEnemies: allowThroughEnemies) ?? new AoOPathResult();

        if (result.Path != null && result.Path.Count > 0)
            result.ProvokedAoOs = CheckForAoO(mover, result.Path, suppressFirstSquareAoO);

        return result;
    }

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

        if (!ThreatSystem.CanMakeAoO(threatener))
            return null;

        return ThreatSystem.ExecuteAoO(threatener, target);
    }

    // ========== MOVEMENT EXECUTION ==========

    public IEnumerator ExecuteMovement(CharacterController mover, List<Vector2Int> path, float secondsPerStep, bool markAsMoved = true)
    {
        if (mover == null || path == null || path.Count == 0)
            yield break;

        yield return mover.MoveAlongPath(path, secondsPerStep, markAsMoved);
    }

    public bool CanTake5FootStep(CharacterController character, out string reason)
    {
        reason = string.Empty;

        if (character == null || character.Stats == null)
        {
            reason = "No active character";
            return false;
        }

        if (character.HasMovedThisTurn)
        {
            reason = "Already moved this turn";
            return false;
        }

        if (character.HasTakenFiveFootStep)
        {
            reason = "Already used 5-foot step this turn";
            return false;
        }

        if (character.HasCondition(CombatConditionType.Prone))
        {
            reason = "Cannot 5-foot step while prone";
            return false;
        }

        if (character.HasCondition(CombatConditionType.Pinned))
        {
            reason = "Cannot 5-foot step while pinned";
            return false;
        }

        if (character.HasCondition(CombatConditionType.Grappled))
        {
            reason = "Cannot 5-foot step while grappled";
            return false;
        }

        List<Vector2Int> adjacent = GetAdjacentSquares(character.GridPosition);
        for (int i = 0; i < adjacent.Count; i++)
        {
            if (CanTake5FootStep(character, adjacent[i]))
                return true;
        }

        reason = "No valid adjacent square";
        return false;
    }

    public bool CanTake5FootStep(CharacterController character, Vector2Int destination)
    {
        if (character == null || character.Stats == null)
            return false;

        return IsValidAdjacentStepDestination(character, destination, disallowDifficultTerrain: true);
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
