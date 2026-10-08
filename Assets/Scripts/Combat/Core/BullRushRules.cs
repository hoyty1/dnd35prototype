using UnityEngine;

/// <summary>
/// Pure bull rush rules (PHB p.154) shared by the PC wrapper, the NPC executor and the charge
/// bull rush (GameManager.ResolveBullRushPushAndFollow). No grid or UI access except the speed
/// and condition lookups in <see cref="GetMovementLimitSquares"/>.
/// </summary>
public static class BullRushRules
{
    /// <summary>
    /// How far the defender can be pushed, in squares (PHB p.154). A failed check
    /// (<paramref name="margin"/> below 0) pushes nothing. A success pushes 5 ft; only an attacker
    /// that moves with the defender may push a further 5 ft for each 5 points of margin, and never
    /// beyond its normal movement limit. When following, the result counts the squares the attacker
    /// can actually move with the defender: 0 when <paramref name="movementLimitSquares"/> is below 1
    /// (the attacker cannot follow at all and may only push 5 ft and stay). On a diagonal push the
    /// follower pays diagonal movement costs (PHB p.148: every second diagonal costs 10 ft), counted
    /// on from <paramref name="previousDiagonals"/> already moved this round (a charge path).
    /// </summary>
    public static int GetMaxPushSquares(int margin, bool attackerFollows, int movementLimitSquares, bool diagonal = false, int previousDiagonals = 0)
    {
        if (margin < 0)
            return 0;

        if (!attackerFollows)
            return 1;

        int byMargin = 1 + margin / 5;
        int squares = 0;
        while (squares < byMargin && GetFollowCostSquares(squares + 1, diagonal, previousDiagonals) <= movementLimitSquares)
            squares++;
        return squares;
    }

    /// <summary>
    /// Movement cost in squares of following <paramref name="steps"/> squares: one each, or on a
    /// diagonal 1 and 2 alternately, continuing from <paramref name="previousDiagonals"/> (PHB p.148,
    /// <see cref="SquareGridUtils.GetDiagonalCost(int, int)"/>).
    /// </summary>
    public static int GetFollowCostSquares(int steps, bool diagonal, int previousDiagonals = 0)
    {
        if (steps <= 0)
            return 0;
        return diagonal ? SquareGridUtils.GetDiagonalCost(steps, Mathf.Max(0, previousDiagonals)) : steps;
    }

    /// <summary>
    /// The attacker's normal movement limit for following a pushed defender, in squares (PHB p.154:
    /// the attacker cannot exceed its normal movement limit). Owner decision 2026-10-07: a standard
    /// bull rush allows the attacker's current speed (0 while prone or entangled by a web,
    /// <see cref="GetSpeedSquares"/>), and a move action taken earlier in the same turn does not count
    /// against it; a charge bull rush allows twice the speed minus the squares the charge already
    /// cost, so the charge distance plus the follow distance is at most twice the speed (PHB
    /// p.154-155). Owner ruling 2026-10-08 (CMB-129): after a standard bull rush the squares followed are
    /// independent of the move action in both directions, so they are not deducted from a move action taken
    /// later in the turn either (GameManager.ExecuteBullRushMovement records no squares; a later move reads
    /// the whole budget from GameManager.GetCurrentMoveRangeSquares). It is 0, so the
    /// attacker cannot follow, when the attacker is incapacitated or a condition blocks its
    /// movement, or after a 5-foot step this turn (PHB p.144: no 5-foot step in a round in which you
    /// move any distance, and following is movement).
    /// </summary>
    public static int GetMovementLimitSquares(CharacterController attacker, bool isCharge, int squaresAlreadyMoved)
    {
        if (attacker == null || attacker.Stats == null)
            return 0;

        if (attacker.HasTakenFiveFootStep
            || attacker.Stats.MovementBlockedByCondition
            || ThreatSystem.IsMoverIncapacitated(attacker))
            return 0;

        int speed = GetSpeedSquares(attacker);
        if (!isCharge)
            return speed;

        return Mathf.Max(0, speed * 2 - Mathf.Max(0, squaresAlreadyMoved));
    }

    /// <summary>
    /// The creature's current speed in squares, from the same source as movement and charges
    /// (<see cref="GameManager.GetCurrentMoveRangeSquares"/>); raw Stats.MoveRange outside a scene.
    /// </summary>
    public static int GetSpeedSquares(CharacterController creature)
    {
        if (creature == null || creature.Stats == null)
            return 0;

        GameManager gm = GameManager.Instance;
        return gm != null ? gm.GetCurrentMoveRangeSquares(creature) : Mathf.Max(0, creature.Stats.MoveRange);
    }

    /// <summary>
    /// "Straight back" for a push (PHB p.154). For each axis, compare the two footprints: +1 when
    /// the defender's footprint lies wholly beyond the attacker's, -1 when wholly before it, 0 when
    /// they overlap on that axis. So a Medium attacker beside either row of a Large defender pushes
    /// it along the row, and the push is diagonal only when the footprints touch at a corner. When
    /// the footprints overlap on both axes, the sign of the difference of the footprint centres
    /// (anchor + (squares - 1) / 2) decides, and (1, 0) only when the centres coincide.
    /// </summary>
    public static Vector2Int GetPushDirection(CharacterController attacker, CharacterController defender)
    {
        if (attacker == null || defender == null)
            return Vector2Int.right;

        return GetPushDirection(
            attacker.GridPosition, Mathf.Max(1, attacker.GetVisualSquaresOccupied()),
            defender.GridPosition, Mathf.Max(1, defender.GetVisualSquaresOccupied()));
    }

    /// <summary>Footprint form of <see cref="GetPushDirection(CharacterController, CharacterController)"/>.</summary>
    public static Vector2Int GetPushDirection(Vector2Int attackerAnchor, int attackerSquares, Vector2Int defenderAnchor, int defenderSquares)
    {
        int aSize = Mathf.Max(1, attackerSquares);
        int dSize = Mathf.Max(1, defenderSquares);

        int AxisSign(int attackerMin, int defenderMin)
        {
            int attackerMax = attackerMin + aSize - 1;
            int defenderMax = defenderMin + dSize - 1;
            if (defenderMin > attackerMax) return 1;
            if (defenderMax < attackerMin) return -1;
            return 0;
        }

        var direction = new Vector2Int(AxisSign(attackerAnchor.x, defenderAnchor.x), AxisSign(attackerAnchor.y, defenderAnchor.y));
        if (direction == Vector2Int.zero)
        {
            float dx = (defenderAnchor.x + (dSize - 1) * 0.5f) - (attackerAnchor.x + (aSize - 1) * 0.5f);
            float dy = (defenderAnchor.y + (dSize - 1) * 0.5f) - (attackerAnchor.y + (aSize - 1) * 0.5f);
            direction = new Vector2Int(dx > 0f ? 1 : (dx < 0f ? -1 : 0), dy > 0f ? 1 : (dy < 0f ? -1 : 0));
        }

        return direction == Vector2Int.zero ? Vector2Int.right : direction;
    }
}
