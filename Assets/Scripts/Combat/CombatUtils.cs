using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utility class for D&D 3.5 combat mechanics including flanking detection
/// and sneak attack calculations on a square grid.
/// </summary>
public static class CombatUtils
{
    /// <summary>
    /// Flanking bonus to attack rolls per D&D 3.5 rules.
    /// </summary>
    public const int FlankingAttackBonus = 2;

    /// <summary>
    /// Tolerant opposite-side threshold.
    /// Dot product <= -0.5 means vectors are at least ~120° apart.
    /// </summary>
    public const float FlankingOppositeDotThreshold = -0.5f;

    /// <summary>
    /// Check if two allies are on opposite sides of the target using a tolerant rule.
    /// This keeps the geometric requirement independent from reach.
    /// </summary>
    public static bool IsFlanking(Vector2Int ally1Pos, Vector2Int ally2Pos, Vector2Int targetPos)
    {
        Vector2 vec1 = new Vector2(ally1Pos.x - targetPos.x, ally1Pos.y - targetPos.y);
        Vector2 vec2 = new Vector2(ally2Pos.x - targetPos.x, ally2Pos.y - targetPos.y);

        if (vec1.sqrMagnitude < 0.001f || vec2.sqrMagnitude < 0.001f)
            return false;

        float dotProduct = Vector2.Dot(vec1.normalized, vec2.normalized);
        return dotProduct <= FlankingOppositeDotThreshold;
    }

    /// <summary>
    /// True if attacker and ally are on the same team.
    /// </summary>
    public static bool AreAllies(CharacterController attacker, CharacterController ally)
    {
        if (attacker == null || ally == null) return false;
        return attacker.IsPlayerControlled == ally.IsPlayerControlled;
    }

    /// <summary>
    /// Returns true if the attacker would threaten target while standing at the provided grid position.
    /// Uses melee threat rings (including reach dead zones) instead of simple adjacency.
    /// </summary>
    public static bool CanThreatenTargetFromPosition(CharacterController attacker, Vector2Int attackerPos, CharacterController target)
    {
        if (attacker == null || target == null) return false;
        if (attacker == target) return false;
        if (attacker.Stats == null || target.Stats == null) return false;
        if (attacker.Stats.IsDead || target.Stats.IsDead) return false;

        // D&D 3.5: ranged-only wielders do not threaten for flanking/AoO.
        if (!attacker.HasMeleeWeaponEquipped()) return false;

        List<Vector2Int> attackerSquares = attacker.GetOccupiedSquaresAt(attackerPos);
        List<Vector2Int> targetSquares = target.GetOccupiedSquares();

        int minDistance = int.MaxValue;
        for (int i = 0; i < attackerSquares.Count; i++)
        {
            for (int j = 0; j < targetSquares.Count; j++)
            {
                int distance = SquareGridUtils.GetChebyshevDistance(attackerSquares[i], targetSquares[j]);
                if (distance < minDistance)
                    minDistance = distance;
            }
        }

        if (minDistance == int.MaxValue)
            return false;

        return attacker.CanMeleeAttackDistance(minDistance);
    }

    /// <summary>
    /// True if this character currently threatens the target for melee purposes.
    /// </summary>
    public static bool IsThreatening(CharacterController character, CharacterController target)
    {
        if (character == null) return false;
        return CanThreatenTargetFromPosition(character, character.GridPosition, target);
    }

    /// <summary>
    /// Alias for readability at flanking call sites.
    /// </summary>
    public static bool IsThreateningTarget(CharacterController attacker, CharacterController target)
    {
        return IsThreatening(attacker, target);
    }

    /// <summary>
    /// Check if the attacker would be flanking the target from a hypothetical position with any ally.
    /// Useful for AI movement planning.
    /// </summary>
    public static bool IsAttackerFlankingFromPosition(
        CharacterController attacker,
        Vector2Int attackerPosition,
        CharacterController target,
        List<CharacterController> potentialAllies,
        out CharacterController flankingPartner)
    {
        flankingPartner = null;

        if (!CanThreatenTargetFromPosition(attacker, attackerPosition, target))
            return false;

        if (potentialAllies == null || potentialAllies.Count == 0)
            return false;

        foreach (var ally in potentialAllies)
        {
            if (ally == null) continue;
            if (ally == attacker || ally == target) continue;
            if (!AreAllies(attacker, ally)) continue;
            if (!IsThreateningTarget(ally, target)) continue;

            if (IsFlanking(attackerPosition, ally.GridPosition, target.GridPosition))
            {
                flankingPartner = ally;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a specific attacker is flanking the target with any ally from the provided pool.
    /// Returns true if flanking is detected, and outputs the flanking partner.
    /// </summary>
    public static bool IsAttackerFlanking(CharacterController attacker, CharacterController target,
        List<CharacterController> potentialAllies, out CharacterController flankingPartner)
    {
        return IsAttackerFlankingFromPosition(attacker, attacker != null ? attacker.GridPosition : Vector2Int.zero,
            target, potentialAllies, out flankingPartner);
    }

    /// <summary>
    /// Roll sneak attack damage for a rogue.
    /// D&D 3.5: Rogue gets +Xd6 sneak attack damage based on level.
    /// Level 1: 1d6, Level 3: 2d6, Level 5: 3d6, etc. (every odd level)
    /// </summary>
    public static int RollSneakAttackDamage(int rogueLevel)
    {
        int sneakDice = GetSneakAttackDice(rogueLevel);
        int total = 0;
        for (int i = 0; i < sneakDice; i++)
        {
            total += Random.Range(1, 7); // d6
        }
        return total;
    }

    /// <summary>
    /// Get the number of sneak attack dice for a given rogue level.
    /// Rogues gain +1d6 at levels 1, 3, 5, 7, 9, etc.
    /// </summary>
    public static int GetSneakAttackDice(int rogueLevel)
    {
        return (rogueLevel + 1) / 2; // Level 1=1, 3=2, 5=3, etc.
    }
}
