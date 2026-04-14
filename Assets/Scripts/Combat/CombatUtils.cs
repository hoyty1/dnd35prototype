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
    /// Check if two allies are flanking a target on the square grid.
    /// In D&D 3.5, flanking occurs when two allies are on opposite sides of an enemy.
    /// Both allies must be adjacent to the target.
    /// </summary>
    public static bool IsFlanking(Vector2Int ally1Pos, Vector2Int ally2Pos, Vector2Int targetPos)
    {
        if (!SquareGridUtils.IsAdjacent(ally1Pos, targetPos)) return false;
        if (!SquareGridUtils.IsAdjacent(ally2Pos, targetPos)) return false;

        Vector3 targetWorld = SquareGridUtils.GridToWorld(targetPos);
        Vector3 ally1World = SquareGridUtils.GridToWorld(ally1Pos);
        Vector3 ally2World = SquareGridUtils.GridToWorld(ally2Pos);

        Vector2 dir1 = new Vector2(ally1World.x - targetWorld.x, ally1World.y - targetWorld.y).normalized;
        Vector2 dir2 = new Vector2(ally2World.x - targetWorld.x, ally2World.y - targetWorld.y).normalized;

        float angle = Vector2.Angle(dir1, dir2);

        // 135+ degrees is effectively opposite side on an 8-neighbor square grid.
        return angle >= 135f;
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
    /// True if this character currently threatens the target for melee purposes.
    /// </summary>
    public static bool IsThreatening(CharacterController character, CharacterController target)
    {
        if (character == null || target == null) return false;
        if (character == target) return false;
        if (character.Stats == null || target.Stats == null) return false;
        if (character.Stats.IsDead || target.Stats.IsDead) return false;
        if (!SquareGridUtils.IsAdjacent(character.GridPosition, target.GridPosition)) return false;

        // D&D 3.5: ranged-only wielders do not threaten for flanking.
        if (!character.HasMeleeWeaponEquipped()) return false;

        return true;
    }

    /// <summary>
    /// Check if a specific attacker is flanking the target with any ally from the provided pool.
    /// Returns true if flanking is detected, and outputs the flanking partner.
    /// </summary>
    public static bool IsAttackerFlanking(CharacterController attacker, CharacterController target,
        List<CharacterController> potentialAllies, out CharacterController flankingPartner)
    {
        flankingPartner = null;

        if (!IsThreatening(attacker, target))
            return false;

        if (potentialAllies == null || potentialAllies.Count == 0)
            return false;

        foreach (var ally in potentialAllies)
        {
            if (ally == null) continue;
            if (ally == attacker || ally == target) continue;
            if (!AreAllies(attacker, ally)) continue;
            if (!IsThreatening(ally, target)) continue;

            if (IsFlanking(attacker.GridPosition, ally.GridPosition, target.GridPosition))
            {
                flankingPartner = ally;
                return true;
            }
        }

        return false;
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
