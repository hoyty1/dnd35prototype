using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic base class for all emanation area effects in D&D 3.5e.
///
/// An emanation is a persistent area effect that radiates from a center point
/// (usually a creature) and affects all creatures within its radius. The area
/// moves with the center creature (mobile emanation) or remains at a fixed
/// position (stationary emanation).
///
/// Subclasses implement the specific mechanical effects of each emanation type:
///   - MagicCircleEffectData: Alignment-based protection (+2 AC/saves, mental control block)
///   - Future: Prayer, Consecrate/Desecrate, Paladin Aura, Bard Inspire Courage, etc.
///
/// Usage:
///   1. Create a subclass with spell-specific properties and logic.
///   2. Override abstract methods for enter/leave/apply/remove behavior.
///   3. Register with GameManager.RegisterEmanation() to activate.
///   4. GameManager handles ticking, area membership, and cleanup.
///
/// Grid distance uses Chebyshev (max of dx, dy) for D&D 3.5e square grid rules.
/// </summary>
[System.Serializable]
public abstract class EmanationEffectData
{
    // ═══════════════════════════════════════════════════════════════════
    //  CORE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>The creature this emanation is centered on (for mobile emanations).</summary>
    [System.NonSerialized] public CharacterController CenterCreature;

    /// <summary>
    /// Fixed center position for stationary emanations (e.g., Consecrate, Desecrate).
    /// If set, takes priority over CenterCreature position for area calculations.
    /// </summary>
    public Vector2Int? CenterPosition;

    /// <summary>Radius in grid squares (e.g., 2 squares = 10 ft at 5ft/square).</summary>
    public int RadiusSquares = 2;

    /// <summary>Radius in feet for display purposes.</summary>
    public float RadiusFeet = 10f;

    /// <summary>Remaining duration in combat rounds (6 seconds each).</summary>
    public int RemainingRounds;

    /// <summary>Caster level for SR checks, dispel DCs, and scaling effects.</summary>
    public int CasterLevel;

    /// <summary>The spell or ability ID that created this emanation.</summary>
    public string SourceSpellId;

    /// <summary>Name of the caster for logging and display.</summary>
    public string CasterName;

    /// <summary>Whether the emanation is currently active. Set to false to mark for removal.</summary>
    public bool IsActive = true;

    // ═══════════════════════════════════════════════════════════════════
    //  CENTER POSITION
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the current center of the emanation.
    /// Uses CenterPosition if set (stationary), otherwise CenterCreature's grid position (mobile).
    /// Returns null if neither is available (invalid state).
    /// </summary>
    public Vector2Int? GetCurrentCenter()
    {
        if (CenterPosition.HasValue)
            return CenterPosition.Value;

        if (CenterCreature != null)
            return CenterCreature.GridPosition;

        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AREA MEMBERSHIP
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if a creature is within the emanation area.
    /// Uses Chebyshev distance (max of |dx|, |dy|) for D&D 3.5e square grid.
    /// The center creature is always considered to be in its own area.
    /// </summary>
    /// <param name="creature">The creature to check.</param>
    /// <returns>True if the creature is within RadiusSquares of the emanation center.</returns>
    public bool IsCreatureInArea(CharacterController creature)
    {
        if (creature == null)
            return false;

        // The center creature is always in its own area
        if (CenterCreature != null && creature == CenterCreature)
            return true;

        Vector2Int? center = GetCurrentCenter();
        if (!center.HasValue)
            return false;

        Vector2Int creaturePos = creature.GridPosition;
        int dx = Mathf.Abs(center.Value.x - creaturePos.x);
        int dy = Mathf.Abs(center.Value.y - creaturePos.y);

        // Chebyshev distance for grid-based radius
        int distance = Mathf.Max(dx, dy);
        return distance <= RadiusSquares;
    }

    /// <summary>
    /// Get all living creatures within the emanation area from a provided list.
    /// Filters out null and dead creatures.
    /// </summary>
    /// <param name="allCharacters">List of all characters to check.</param>
    /// <returns>List of living creatures within the emanation area.</returns>
    public List<CharacterController> GetCreaturesInArea(List<CharacterController> allCharacters)
    {
        var result = new List<CharacterController>();
        if (allCharacters == null)
            return result;

        for (int i = 0; i < allCharacters.Count; i++)
        {
            if (allCharacters[i] != null && !allCharacters[i].IsDead && IsCreatureInArea(allCharacters[i]))
                result.Add(allCharacters[i]);
        }
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DURATION MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tick the duration by one round. Returns true if the effect is still active.
    /// Also calls OnTick() for subclass-specific per-round processing.
    /// </summary>
    /// <returns>True if RemainingRounds > 0 after decrement.</returns>
    public bool Tick()
    {
        if (RemainingRounds > 0)
            RemainingRounds--;

        OnTick();
        return RemainingRounds > 0;
    }

    /// <summary>
    /// Check if this emanation should be removed (center creature dead, null, or expired).
    /// </summary>
    /// <returns>True if the emanation should be cleaned up.</returns>
    public bool ShouldRemove()
    {
        if (!IsActive)
            return true;

        if (RemainingRounds <= 0)
            return true;

        // Mobile emanation: remove if center creature is gone or dead
        if (!CenterPosition.HasValue)
        {
            if (CenterCreature == null || CenterCreature.IsDead)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Get a display string for the remaining duration.
    /// Converts rounds to minutes (10 rounds = 1 minute) for readability.
    /// </summary>
    /// <returns>Human-readable duration string (e.g., "5 min 3 rnd").</returns>
    public string GetDurationDisplay()
    {
        if (RemainingRounds <= 0) return "Expired";
        int minutes = RemainingRounds / 10;
        int rounds = RemainingRounds % 10;
        if (minutes > 0 && rounds > 0)
            return $"{minutes} min {rounds} rnd";
        if (minutes > 0)
            return $"{minutes} min";
        return $"{rounds} rnd";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ABSTRACT METHODS — Must be implemented by subclasses
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get a human-readable name for this emanation effect (e.g., "Magic Circle against Evil").
    /// Used for logging and UI display.
    /// </summary>
    public abstract string GetEffectName();

    /// <summary>
    /// Called when a creature enters the emanation area.
    /// Subclasses should apply initial effects or start tracking the creature.
    /// </summary>
    /// <param name="creature">The creature that entered the area.</param>
    public abstract void OnCreatureEntersArea(CharacterController creature);

    /// <summary>
    /// Called when a creature leaves the emanation area.
    /// Subclasses should remove effects or stop tracking the creature.
    /// </summary>
    /// <param name="creature">The creature that left the area.</param>
    public abstract void OnCreatureLeavesArea(CharacterController creature);

    /// <summary>
    /// Apply this emanation's mechanical effects to a creature currently in the area.
    /// Called when querying for active benefits on a creature.
    /// </summary>
    /// <param name="creature">The creature to apply effects to.</param>
    public abstract void ApplyEffectsToCreature(CharacterController creature);

    /// <summary>
    /// Remove this emanation's mechanical effects from a creature.
    /// Called when the emanation expires, is dismissed, or the creature leaves.
    /// </summary>
    /// <param name="creature">The creature to remove effects from.</param>
    public abstract void RemoveEffectsFromCreature(CharacterController creature);

    // ═══════════════════════════════════════════════════════════════════
    //  VIRTUAL METHODS — Optional overrides for subclasses
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called each round during Tick(). Override for per-round effects
    /// (e.g., Prayer's morale bonuses, ongoing damage emanations).
    /// Default implementation does nothing.
    /// </summary>
    protected virtual void OnTick() { }
}
