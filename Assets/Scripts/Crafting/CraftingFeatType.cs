// ============================================================================
// D&D 3.5e Item Creation Feats - Crafting Feat Type Enum
// ============================================================================

/// <summary>
/// The eight D&D 3.5e Item Creation feats (DMG p.282–285, PHB p.91–92).
/// Each feat gates access to a family of craftable magic items.
/// </summary>
public enum CraftingFeatType
{
    /// <summary>Create spell scrolls from known spells. Wizard bonus feat at level 1. CL 1 prereq.</summary>
    ScribeScroll,

    /// <summary>Brew potions from spells of 3rd level or lower. CL 3 prereq.</summary>
    BrewPotion,

    /// <summary>Create wondrous items (cloaks, boots, amulets, etc.). CL 3 prereq.</summary>
    CraftWondrousItem,

    /// <summary>Create magic weapons, armor, and shields. CL 5 prereq.</summary>
    CraftMagicArmsAndArmor,

    /// <summary>Create wands with spells of 4th level or lower. CL 5 prereq.</summary>
    CraftWand,

    /// <summary>Create magic rods. CL 9 prereq.</summary>
    CraftRod,

    /// <summary>Create magic staves. CL 12 prereq.</summary>
    CraftStaff,

    /// <summary>Create magic rings. CL 12 prereq.</summary>
    ForgeRing
}
