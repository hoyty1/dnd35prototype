using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utility class for D&D 3.5 combat mechanics including flanking detection
/// and sneak attack calculations on a hex grid.
/// </summary>
public static class CombatUtils
{
    /// <summary>
    /// Flanking bonus to attack rolls per D&D 3.5 rules.
    /// </summary>
    public const int FlankingAttackBonus = 2;

    /// <summary>
    /// Check if two allies are flanking a target on the hex grid.
    /// In D&D 3.5, flanking occurs when two allies are on opposite sides of an enemy.
    /// For hex grids, we check if the two allies are roughly 180 degrees apart
    /// relative to the target (within a tolerance for hex geometry).
    /// Both allies must be adjacent (within attack range 1) to the target.
    /// </summary>
    public static bool IsFlanking(Vector2Int ally1Pos, Vector2Int ally2Pos, Vector2Int targetPos)
    {
        // Both allies must be adjacent to the target (distance 1)
        int dist1 = HexUtils.HexDistance(ally1Pos, targetPos);
        int dist2 = HexUtils.HexDistance(ally2Pos, targetPos);

        if (dist1 != 1 || dist2 != 1) return false;

        // Convert hex positions to world positions to calculate angle
        Vector3 targetWorld = HexUtils.AxialToWorld(targetPos.x, targetPos.y);
        Vector3 ally1World = HexUtils.AxialToWorld(ally1Pos.x, ally1Pos.y);
        Vector3 ally2World = HexUtils.AxialToWorld(ally2Pos.x, ally2Pos.y);

        // Calculate vectors from target to each ally
        Vector2 dir1 = new Vector2(ally1World.x - targetWorld.x, ally1World.y - targetWorld.y).normalized;
        Vector2 dir2 = new Vector2(ally2World.x - targetWorld.x, ally2World.y - targetWorld.y).normalized;

        // Calculate the angle between the two directions
        float angle = Vector2.Angle(dir1, dir2);

        // For hex grids, opposite hexes are exactly 180 degrees apart.
        // We use a tolerance of 30 degrees to account for hex geometry
        // (hex neighbors are 60 degrees apart, so 150+ degrees means opposite side).
        return angle >= 150f;
    }

    /// <summary>
    /// Check if a specific attacker is flanking the target with any ally.
    /// Returns true if flanking is detected, and outputs the flanking partner.
    /// </summary>
    public static bool IsAttackerFlanking(CharacterController attacker, CharacterController target,
        List<CharacterController> potentialAllies, out CharacterController flankingPartner)
    {
        flankingPartner = null;

        foreach (var ally in potentialAllies)
        {
            if (ally == attacker) continue;
            if (ally == target) continue;
            if (ally.Stats.IsDead) continue;

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
