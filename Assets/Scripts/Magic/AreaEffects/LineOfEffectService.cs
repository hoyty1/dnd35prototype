// ============================================================================
// LineOfEffectService.cs — Centralized line-of-effect blocking registry
//
// Maintains a list of all active ILineOfEffectBlocker instances and provides
// a single entry point for checking whether LoE is blocked between two cells.
//
// Blockers register themselves on creation and unregister on destruction.
// This replaces all hardcoded Wall of Ice LoE checks and is extensible for
// future wall spells, terrain objects, Resilient Sphere, etc.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Centralized service for line-of-effect blocking.
/// All <see cref="ILineOfEffectBlocker"/> implementations register here
/// so that any system (AoE filtering, spell targeting, AI line-of-sight)
/// can query a single method instead of checking each blocker type.
/// </summary>
public static class LineOfEffectService
{
    private static readonly List<ILineOfEffectBlocker> _blockers = new List<ILineOfEffectBlocker>();

    // ═══════════════════════════════════════════════════
    // REGISTRATION
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Register a blocker. Call this when the blocker becomes active
    /// (e.g., in Start() or OnAreaCreated()).
    /// </summary>
    public static void Register(ILineOfEffectBlocker blocker)
    {
        if (blocker == null) return;
        if (!_blockers.Contains(blocker))
        {
            _blockers.Add(blocker);
            Debug.Log($"[LineOfEffectService] Registered blocker: {blocker.GetType().Name} (total: {_blockers.Count})");
        }
    }

    /// <summary>
    /// Unregister a blocker. Call this when the blocker is destroyed or expires
    /// (e.g., in OnDestroy() or OnAreaExpires()).
    /// </summary>
    public static void Unregister(ILineOfEffectBlocker blocker)
    {
        if (blocker == null) return;
        if (_blockers.Remove(blocker))
        {
            Debug.Log($"[LineOfEffectService] Unregistered blocker: {blocker.GetType().Name} (total: {_blockers.Count})");
        }
    }

    /// <summary>
    /// Remove all registered blockers. Useful for scene cleanup / game reset.
    /// </summary>
    public static void ClearAll()
    {
        _blockers.Clear();
        Debug.Log("[LineOfEffectService] All blockers cleared.");
    }

    // ═══════════════════════════════════════════════════
    // QUERIES
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Returns true if ANY registered blocker blocks line of effect
    /// from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    public static bool IsBlocked(Vector2Int from, Vector2Int to)
    {
        for (int i = 0; i < _blockers.Count; i++)
        {
            // Defend against destroyed Unity objects still in the list
            if (_blockers[i] == null || (_blockers[i] is Object obj && obj == null))
            {
                _blockers.RemoveAt(i);
                i--;
                continue;
            }

            if (_blockers[i].BlocksLineOfEffect(from, to))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the combined set of all "blocker-owned" cells from every
    /// active blocker. These cells should typically NOT be filtered out
    /// by AoE LoE filtering (e.g., a fireball can still hit a Wall cell).
    /// </summary>
    public static HashSet<Vector2Int> GetAllBlockerCells()
    {
        var allCells = new HashSet<Vector2Int>();
        for (int i = 0; i < _blockers.Count; i++)
        {
            if (_blockers[i] == null || (_blockers[i] is Object obj && obj == null))
            {
                _blockers.RemoveAt(i);
                i--;
                continue;
            }

            HashSet<Vector2Int> cells = _blockers[i].GetBlockerCells();
            if (cells != null && cells.Count > 0)
                allCells.UnionWith(cells);
        }
        return allCells;
    }

    /// <summary>
    /// Returns the current number of registered blockers.
    /// Useful for debugging and tests.
    /// </summary>
    public static int BlockerCount => _blockers.Count;

    /// <summary>
    /// Returns true if there are any active blockers registered.
    /// Quick early-out check for performance.
    /// </summary>
    public static bool HasBlockers => _blockers.Count > 0;
}
