// ============================================================================
// D&D 3.5e Planar System — Planes of Existence
//
// The D&D multiverse contains multiple planes of existence. The Material Plane
// is the default "home" plane for most campaigns. Other planes include the
// Transitive Planes (Astral, Ethereal, Shadow), Inner Planes (Elemental, Energy),
// and Outer Planes (alignment-based afterlife realms).
//
// Used by: Sword of the Planes, Plane Shift spell, creature origin tracking.
// ============================================================================

/// <summary>
/// Represents the various planes of existence in the D&D 3.5e multiverse.
/// </summary>
public enum PlaneType
{
    // === DEFAULT ===
    Material,               // Prime Material Plane (default for most campaigns)

    // === TRANSITIVE PLANES ===
    Ethereal,               // Ethereal Plane (overlaps Material, home of ghosts)
    Astral,                 // Astral Plane (connects all planes, silver void)
    Shadow,                 // Plane of Shadow (dark reflection of Material)

    // === INNER PLANES (Elemental) ===
    ElementalFire,          // Elemental Plane of Fire
    ElementalWater,         // Elemental Plane of Water
    ElementalAir,           // Elemental Plane of Air
    ElementalEarth,         // Elemental Plane of Earth
    PositiveEnergy,         // Positive Energy Plane (source of life/healing)
    NegativeEnergy,         // Negative Energy Plane (source of undeath/drain)

    // === OUTER PLANES (Upper — Good) ===
    MountCelestia,          // Seven Mounting Heavens (LG)
    Bytopia,                // Twin Paradises (NG/LG)
    Elysium,                // Blessed Fields (NG)
    Beastlands,             // Happy Hunting Grounds (NG/CG)
    Arborea,                // Olympian Glades (CG)

    // === OUTER PLANES (Neutral) ===
    Ysgard,                 // Heroic Domains (CN/CG)
    Limbo,                  // Ever-Changing Chaos (CN)
    Outlands,               // Concordant Domain (TN, center of the Outer Planes)
    Mechanus,               // Clockwork Nirvana (LN)
    Arcadia,                // Peaceable Kingdoms (LN/LG)

    // === OUTER PLANES (Lower — Evil) ===
    Acheron,                // Infernal Battlefield (LE/LN)
    NineHells,              // Baator (LE)
    Gehenna,                // Bleak Eternity (NE/LE)
    Hades,                  // Gray Waste (NE)
    Carceri,                // Tarterian Depths (NE/CE)
    Abyss,                  // Infinite Layers (CE)
    Pandemonium              // Windswept Depths (CE/CN)
}

/// <summary>
/// Helper methods for plane classification.
/// </summary>
public static class PlaneHelper
{
    /// <summary>Whether this is an Inner (Elemental/Energy) Plane.</summary>
    public static bool IsInnerPlane(PlaneType plane)
    {
        return plane == PlaneType.ElementalFire || plane == PlaneType.ElementalWater ||
               plane == PlaneType.ElementalAir || plane == PlaneType.ElementalEarth ||
               plane == PlaneType.PositiveEnergy || plane == PlaneType.NegativeEnergy;
    }

    /// <summary>Whether this is a Transitive Plane.</summary>
    public static bool IsTransitivePlane(PlaneType plane)
    {
        return plane == PlaneType.Astral || plane == PlaneType.Ethereal || plane == PlaneType.Shadow;
    }

    /// <summary>Whether this is an Outer Plane.</summary>
    public static bool IsOuterPlane(PlaneType plane)
    {
        return !IsInnerPlane(plane) && !IsTransitivePlane(plane) && plane != PlaneType.Material;
    }

    /// <summary>Whether this plane is NOT the Material Plane.</summary>
    public static bool IsExtraplanar(PlaneType plane)
    {
        return plane != PlaneType.Material;
    }

    /// <summary>Get the display name for a plane.</summary>
    public static string GetDisplayName(PlaneType plane)
    {
        switch (plane)
        {
            case PlaneType.Material: return "Material Plane";
            case PlaneType.Ethereal: return "Ethereal Plane";
            case PlaneType.Astral: return "Astral Plane";
            case PlaneType.Shadow: return "Plane of Shadow";
            case PlaneType.ElementalFire: return "Elemental Plane of Fire";
            case PlaneType.ElementalWater: return "Elemental Plane of Water";
            case PlaneType.ElementalAir: return "Elemental Plane of Air";
            case PlaneType.ElementalEarth: return "Elemental Plane of Earth";
            case PlaneType.PositiveEnergy: return "Positive Energy Plane";
            case PlaneType.NegativeEnergy: return "Negative Energy Plane";
            case PlaneType.MountCelestia: return "Seven Mounting Heavens of Celestia";
            case PlaneType.Elysium: return "Blessed Fields of Elysium";
            case PlaneType.Abyss: return "Infinite Layers of the Abyss";
            case PlaneType.NineHells: return "Nine Hells of Baator";
            case PlaneType.Limbo: return "Ever-Changing Chaos of Limbo";
            case PlaneType.Mechanus: return "Clockwork Nirvana of Mechanus";
            default: return plane.ToString();
        }
    }
}
