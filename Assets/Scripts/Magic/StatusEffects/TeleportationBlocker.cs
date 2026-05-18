using UnityEngine;

// ============================================================================
// TeleportationBlocker.cs — Extensible Teleportation Blocking System
// D&D 3.5e: Dimensional Anchor and similar effects block extradimensional travel.
// This system provides a centralized way for future teleportation/planar spells
// to check whether a creature is blocked from teleporting.
// ============================================================================

/// <summary>
/// Categories of extradimensional movement that can be blocked.
/// Effects like Dimensional Anchor block ALL categories.
/// Future effects might block only specific categories.
/// </summary>
[System.Flags]
public enum TeleportationType
{
    None            = 0,

    /// <summary>Teleportation spells: teleport, dimension door, word of recall, etc.</summary>
    Teleportation   = 1 << 0,

    /// <summary>Planar travel: plane shift, gate (travel), shadow walk, etc.</summary>
    PlanarTravel    = 1 << 1,

    /// <summary>Ethereal movement: etherealness, ethereal jaunt, blink, etc.</summary>
    Ethereal        = 1 << 2,

    /// <summary>Astral movement: astral projection, etc.</summary>
    Astral          = 1 << 3,

    /// <summary>Other dimensional effects: maze, rope trick (entry/exit), etc.</summary>
    OtherDimensional = 1 << 4,

    /// <summary>All forms of extradimensional travel (used by Dimensional Anchor).</summary>
    All = Teleportation | PlanarTravel | Ethereal | Astral | OtherDimensional
}

/// <summary>
/// Centralized system for checking whether a creature can use extradimensional travel.
/// Future teleportation spells should call TeleportationBlocker.CanTeleport() or
/// TeleportationBlocker.IsBlocked() before allowing the effect.
///
/// Usage examples:
///   if (TeleportationBlocker.IsBlocked(target))
///       // "A shimmering field prevents the teleportation!"
///
///   if (!TeleportationBlocker.CanTeleport(target, TeleportationType.Teleportation))
///       // "Dimensional Anchor prevents dimension door!"
/// </summary>
public static class TeleportationBlocker
{
    /// <summary>
    /// Check if a creature is blocked from ALL forms of extradimensional travel.
    /// Quick check for the most common case (Dimensional Anchor blocks everything).
    /// </summary>
    public static bool IsBlocked(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return false;

        return character.Stats.ActiveDimensionalAnchorEffect != null;
    }

    /// <summary>
    /// Check if a creature can perform a specific type of extradimensional travel.
    /// Returns true if the travel is allowed, false if blocked.
    /// </summary>
    public static bool CanTeleport(CharacterController character, TeleportationType type = TeleportationType.All)
    {
        if (character == null || character.Stats == null)
            return true;

        DimensionalAnchorEffectData anchor = character.Stats.ActiveDimensionalAnchorEffect;
        if (anchor != null && (anchor.BlockedTypes & type) != 0)
            return false;

        // Future: check other blocking effects here (e.g., Forbiddance, Antimagic Field)
        // if (character.Stats.ActiveForbiddanceEffect != null && ...)
        //     return false;

        return true;
    }

    /// <summary>
    /// Get a user-friendly reason why teleportation is blocked, for combat log messages.
    /// Returns null if not blocked.
    /// </summary>
    public static string GetBlockedReason(CharacterController character, TeleportationType type = TeleportationType.All)
    {
        if (character == null || character.Stats == null)
            return null;

        DimensionalAnchorEffectData anchor = character.Stats.ActiveDimensionalAnchorEffect;
        if (anchor != null && (anchor.BlockedTypes & type) != 0)
            return "Dimensional Anchor prevents extradimensional travel";

        // Future: check other blocking effects
        return null;
    }

    /// <summary>
    /// Get a display-friendly name for a teleportation type.
    /// </summary>
    public static string GetTypeName(TeleportationType type)
    {
        switch (type)
        {
            case TeleportationType.Teleportation:   return "teleportation";
            case TeleportationType.PlanarTravel:     return "planar travel";
            case TeleportationType.Ethereal:         return "ethereal movement";
            case TeleportationType.Astral:           return "astral travel";
            case TeleportationType.OtherDimensional: return "dimensional movement";
            case TeleportationType.All:              return "extradimensional travel";
            default:                                 return "dimensional movement";
        }
    }
}
