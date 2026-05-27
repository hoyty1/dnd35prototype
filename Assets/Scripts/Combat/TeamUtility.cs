using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// TeamUtility — Centralized team/faction queries and creature type checks.
//
// Extracted from GameManager to eliminate scattered private helper methods
// for team comparison, creature type validation, and hit dice lookup.
//
// All methods are pure static — they depend only on their arguments,
// making them safe to call from any service or utility class.
//
// Replaces:
//   GameManager.IsEnemyTeam(a, b)       → TeamUtility.IsEnemy(a, b)
//   GameManager.IsAllyTeam(a, b)        → TeamUtility.IsAlly(a, b)
//   GameManager.IsHumanoid(target)      → TeamUtility.IsHumanoid(target)
//   GameManager.GetTargetHitDice(target) → TeamUtility.GetHitDice(target)
//   GameManager.IsEnemyTeamForAI(a, b)  → TeamUtility.IsEnemy(a, b)
// ============================================================================

/// <summary>
/// Static utility class for team/faction queries and creature type checks.
/// All methods are pure — no GameManager state required.
/// </summary>
public static class TeamUtility
{
    // ════════════════════════════════════════════════════════════
    //  Team / Faction Queries
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns true if <paramref name="source"/> and <paramref name="target"/>
    /// are on opposing teams (Player vs Enemy).
    /// D&D 3.5e: Used for determining valid attack/spell targets, AoO eligibility, etc.
    /// </summary>
    public static bool IsEnemy(CharacterController source, CharacterController target)
    {
        if (source == null || target == null) return false;

        return (source.Team == CharacterTeam.Player && target.Team == CharacterTeam.Enemy)
            || (source.Team == CharacterTeam.Enemy && target.Team == CharacterTeam.Player);
    }

    /// <summary>
    /// Returns true if <paramref name="source"/> and <paramref name="target"/>
    /// are on the same team (both Player or both Enemy). Neutrals are never allies.
    /// </summary>
    public static bool IsAlly(CharacterController source, CharacterController target)
    {
        if (source == null || target == null) return false;
        if (source.Team == CharacterTeam.Neutral || target.Team == CharacterTeam.Neutral) return false;
        return source.Team == target.Team;
    }

    // ════════════════════════════════════════════════════════════
    //  Creature Type Checks
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns true if the target is a Humanoid creature type.
    /// Used by targeting validation for Person-type spells (Enlarge Person,
    /// Charm Person, Hold Person, Dominate Person, etc.).
    /// </summary>
    public static bool IsHumanoid(CharacterController target)
    {
        if (target?.Stats == null) return false;
        return string.Equals(target.Stats.CreatureType, "Humanoid", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get the effective hit dice of a target creature.
    /// Returns the greater of HitDice and Level, with a minimum of 1.
    /// Used for HD-pool spells (Sleep, Color Spray, Hypnotism) and
    /// HD-limited targeting (Daze ≤ 4 HD, Cause Fear ≤ 5 HD, etc.).
    /// </summary>
    public static int GetHitDice(CharacterController target)
    {
        if (target?.Stats == null) return 0;
        return Mathf.Max(1, target.Stats.HitDice > 0 ? target.Stats.HitDice : target.Stats.Level);
    }

    // ════════════════════════════════════════════════════════════
    //  Team Collection Queries
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all living members of the specified team from a character list.
    /// Filters out null, dead, and stat-less characters.
    /// </summary>
    /// <param name="allCharacters">All active combatants.</param>
    /// <param name="teamFilter">The team to filter for.</param>
    /// <returns>List of alive characters on the specified team.</returns>
    public static List<CharacterController> GetAliveTeamMembers(
        List<CharacterController> allCharacters,
        CharacterTeam teamFilter)
    {
        var team = new List<CharacterController>();
        if (allCharacters == null) return team;

        foreach (var c in allCharacters)
        {
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (c.Team == teamFilter)
                team.Add(c);
        }
        return team;
    }

    /// <summary>
    /// Find the closest alive enemy to the source character.
    /// Uses grid distance (Chebyshev / square grid).
    /// </summary>
    /// <param name="source">The source character.</param>
    /// <param name="allCharacters">All active combatants.</param>
    /// <returns>The closest enemy, or null if none found.</returns>
    public static CharacterController GetClosestEnemy(
        CharacterController source,
        List<CharacterController> allCharacters)
    {
        if (source == null || allCharacters == null) return null;

        CharacterController closest = null;
        int closestDist = int.MaxValue;

        foreach (var candidate in allCharacters)
        {
            if (candidate == null || candidate == source || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemy(source, candidate))
                continue;

            int dist = SquareGridUtils.GetDistance(source.GridPosition, candidate.GridPosition);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = candidate;
            }
        }

        return closest;
    }
}
