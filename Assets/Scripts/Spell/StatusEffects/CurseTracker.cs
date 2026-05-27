using DND35e.Identifiers;
using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// CurseTracker.cs — Centralized Curse Tracking System
// D&D 3.5e: Curses are permanent until removed by Remove Curse or similar.
// This system provides a centralized way to track, query, and remove curse
// effects on a character, similar to TeleportationBlocker for teleportation.
//
// Currently tracks curses from:
//   - Bestow Curse (PHB p.203): ability penalty, general penalty, action loss
//
// Future curse sources can be added here:
//   - Cursed items (shield, weapon, armor)
//   - Mummy rot
//   - Hex effects
//   - Custom encounter curses
// ============================================================================

/// <summary>
/// Individual curse effect record.
/// </summary>
[Serializable]
public class CurseEffectData
{
    /// <summary>Unique identifier for this curse instance.</summary>
    public string CurseId;

    /// <summary>Source spell ID (e.g., SpellNames.BESTOW_CURSE).</summary>
    public string SourceSpellId;

    /// <summary>Human-readable description of the curse effect.</summary>
    public string Description;

    /// <summary>Name of the caster who applied the curse.</summary>
    public string CasterName;

    /// <summary>Caster level at time of application.</summary>
    public int CasterLevel;

    /// <summary>Which ability is penalized (if applicable). Null if not an ability curse.</summary>
    public string AffectedAbility;

    /// <summary>Penalty amount (positive number, applied as negative).</summary>
    public int PenaltyAmount;

    /// <summary>The BestowCurseType if from Bestow Curse, or Custom for other sources.</summary>
    public CurseType Type;

    /// <summary>When the curse was applied (combat round or real time).</summary>
    public int AppliedOnRound;
}

/// <summary>
/// Categories of curse effects for tracking and removal.
/// </summary>
public enum CurseType
{
    /// <summary>Bestow Curse: -6 to one ability score.</summary>
    BestowCurseAbilityPenalty,

    /// <summary>Bestow Curse: -4 on attacks, saves, ability checks, skill checks.</summary>
    BestowCurseGeneralPenalty,

    /// <summary>Bestow Curse: 50% chance each turn to lose all actions.</summary>
    BestowCurseActionLoss,

    /// <summary>Future: Cursed item effects.</summary>
    CursedItem,

    /// <summary>Future: Custom encounter curses.</summary>
    Custom
}

/// <summary>
/// Centralized curse management — tracks active curses on a character
/// and provides methods for querying and removing them.
///
/// Usage examples:
///   if (CurseTracker.IsCursed(character))
///       // "The creature is afflicted by a curse!"
///
///   int removed = CurseTracker.RemoveAllCurses(character);
///       // Remove Curse removes all active curses
///
///   CurseTracker.AddCurse(character, curseData);
///       // Bestow Curse adds a new curse
/// </summary>
public static class CurseTracker
{
    // Storage: curses per character instance ID
    private static readonly Dictionary<int, List<CurseEffectData>> _activeCurses
        = new Dictionary<int, List<CurseEffectData>>();

    private static int _nextCurseId = 1;

    /// <summary>
    /// Check if a character has any active curses.
    /// </summary>
    public static bool IsCursed(CharacterController character)
    {
        if (character == null) return false;
        int id = character.GetInstanceID();
        return _activeCurses.TryGetValue(id, out var list) && list != null && list.Count > 0;
    }

    /// <summary>
    /// Get a read-only view of all active curses on a character.
    /// </summary>
    public static IReadOnlyList<CurseEffectData> GetCurses(CharacterController character)
    {
        if (character == null) return Array.Empty<CurseEffectData>();
        int id = character.GetInstanceID();
        if (_activeCurses.TryGetValue(id, out var list) && list != null)
            return list;
        return Array.Empty<CurseEffectData>();
    }

    /// <summary>
    /// Get the count of active curses on a character.
    /// </summary>
    public static int GetCurseCount(CharacterController character)
    {
        if (character == null) return 0;
        int id = character.GetInstanceID();
        if (_activeCurses.TryGetValue(id, out var list) && list != null)
            return list.Count;
        return 0;
    }

    /// <summary>
    /// Add a curse effect to a character.
    /// </summary>
    public static void AddCurse(CharacterController character, CurseEffectData curse)
    {
        if (character == null || curse == null) return;

        int id = character.GetInstanceID();
        if (!_activeCurses.TryGetValue(id, out var list) || list == null)
        {
            list = new List<CurseEffectData>();
            _activeCurses[id] = list;
        }

        if (string.IsNullOrEmpty(curse.CurseId))
            curse.CurseId = $"curse_{_nextCurseId++}";

        list.Add(curse);
        Debug.Log($"[CurseTracker] Added curse '{curse.CurseId}' ({curse.Type}) to {character.Stats?.CharacterName ?? "Unknown"}. Total curses: {list.Count}");
    }

    /// <summary>
    /// Remove all curses from a character (used by Remove Curse spell).
    /// Returns the number of curses removed and the list of removed curse data.
    /// </summary>
    public static int RemoveAllCurses(CharacterController character, out List<CurseEffectData> removedCurses)
    {
        removedCurses = new List<CurseEffectData>();

        if (character == null) return 0;
        int id = character.GetInstanceID();

        if (!_activeCurses.TryGetValue(id, out var list) || list == null || list.Count == 0)
            return 0;

        removedCurses.AddRange(list);
        int count = list.Count;
        list.Clear();

        Debug.Log($"[CurseTracker] Removed {count} curse(s) from {character.Stats?.CharacterName ?? "Unknown"}");
        return count;
    }

    /// <summary>
    /// Remove a specific curse by curse ID.
    /// </summary>
    public static bool RemoveCurseById(CharacterController character, string curseId)
    {
        if (character == null || string.IsNullOrEmpty(curseId)) return false;
        int id = character.GetInstanceID();

        if (!_activeCurses.TryGetValue(id, out var list) || list == null)
            return false;

        int idx = list.FindIndex(c => c.CurseId == curseId);
        if (idx < 0) return false;

        list.RemoveAt(idx);
        Debug.Log($"[CurseTracker] Removed curse '{curseId}' from {character.Stats?.CharacterName ?? "Unknown"}");
        return true;
    }

    /// <summary>
    /// Remove curses from a specific source spell (e.g., all Bestow Curse effects).
    /// </summary>
    public static int RemoveCursesBySource(CharacterController character, string sourceSpellId)
    {
        if (character == null || string.IsNullOrEmpty(sourceSpellId)) return 0;
        int id = character.GetInstanceID();

        if (!_activeCurses.TryGetValue(id, out var list) || list == null)
            return 0;

        int removed = list.RemoveAll(c =>
            string.Equals(c.SourceSpellId, sourceSpellId, StringComparison.Ordinal));

        if (removed > 0)
            Debug.Log($"[CurseTracker] Removed {removed} curse(s) from source '{sourceSpellId}' on {character.Stats?.CharacterName ?? "Unknown"}");

        return removed;
    }

    /// <summary>
    /// Check if a character has a specific type of curse.
    /// </summary>
    public static bool HasCurseOfType(CharacterController character, CurseType type)
    {
        if (character == null) return false;
        int id = character.GetInstanceID();

        if (!_activeCurses.TryGetValue(id, out var list) || list == null)
            return false;

        return list.Exists(c => c.Type == type);
    }

    /// <summary>
    /// Get a human-readable summary of all curses for UI display.
    /// </summary>
    public static string GetCurseSummary(CharacterController character)
    {
        if (!IsCursed(character)) return null;

        var curses = GetCurses(character);
        var sb = new System.Text.StringBuilder();
        sb.Append($"Cursed ({curses.Count}):");

        for (int i = 0; i < curses.Count; i++)
        {
            sb.Append($"\n  • {curses[i].Description}");
            if (!string.IsNullOrEmpty(curses[i].CasterName))
                sb.Append($" (by {curses[i].CasterName})");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Clear all curse tracking data (e.g., on scene reset).
    /// </summary>
    public static void ClearAll()
    {
        _activeCurses.Clear();
        _nextCurseId = 1;
        Debug.Log("[CurseTracker] All curse data cleared");
    }

    /// <summary>
    /// Clear curses for a specific character (e.g., on character reset/death).
    /// </summary>
    public static void ClearForCharacter(CharacterController character)
    {
        if (character == null) return;
        int id = character.GetInstanceID();
        if (_activeCurses.ContainsKey(id))
        {
            _activeCurses[id].Clear();
            _activeCurses.Remove(id);
        }
    }
}
