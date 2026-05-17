// ============================================================================
// ILineOfEffectBlocker.cs — Interface for objects that block line of effect
//
// Any area effect, terrain object, or spell that blocks line of effect
// between grid cells should implement this interface and register with
// LineOfEffectService.
// ============================================================================
using UnityEngine;

/// <summary>
/// Interface for anything that can block line of effect between two grid cells.
/// Implementations include Wall of Ice (intact sections block), Otiluke's
/// Resilient Sphere (blocks LoE across the sphere boundary), and future
/// wall spells / terrain objects.
/// </summary>
public interface ILineOfEffectBlocker
{
    /// <summary>
    /// Returns true if this blocker blocks line of effect between the two cells.
    /// The implementation decides what "blocking" means:
    ///   - Wall of Ice: intact wall cells along the Bresenham line
    ///   - Resilient Sphere: line crosses the sphere boundary
    ///   - Future walls: similar cell-based blocking
    /// </summary>
    /// <param name="from">Source cell (e.g., AoE origin or caster position).</param>
    /// <param name="to">Target cell being checked.</param>
    /// <returns>True if this blocker blocks LoE from <paramref name="from"/> to <paramref name="to"/>.</returns>
    bool BlocksLineOfEffect(Vector2Int from, Vector2Int to);

    /// <summary>
    /// Returns the set of cells that this blocker considers "its own" cells.
    /// AoE filtering uses this to avoid filtering out cells that belong to the
    /// blocker itself (e.g., a fireball can still damage a Wall of Ice cell
    /// even though LoE is blocked past it).
    /// May return null or empty if the blocker has no special "own" cells.
    /// </summary>
    System.Collections.Generic.HashSet<Vector2Int> GetBlockerCells();
}
