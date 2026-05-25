// ============================================================================
// D&D 3.5e Item Creation Feats - Craftable Item Definition
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// Defines the crafting prerequisites and metadata for a single craftable magic item.
/// Links an item (by ID or template) to its required feat, caster level, spell prerequisites,
/// and market price. Used by CraftableItemRegistry to populate the Crafting Workshop.
/// </summary>
[System.Serializable]
public class CraftableItemDefinition
{
    /// <summary>
    /// The item database ID (e.g., "ring_of_protection_1", "cloak_of_resistance_2").
    /// For dynamic items (scrolls, potions, wands), this may be a template ID.
    /// </summary>
    public string ItemId;

    /// <summary>Display name for the crafting UI.</summary>
    public string DisplayName;

    /// <summary>Which item creation feat is required to craft this item.</summary>
    public CraftingFeatType RequiredFeat;

    /// <summary>
    /// Minimum caster level the crafter must have to create this item.
    /// This is the item's CL, not the feat's CL prerequisite.
    /// </summary>
    public int RequiredCasterLevel;

    /// <summary>
    /// Market price of the finished item in gp. Used to derive gold, XP, and time costs.
    /// </summary>
    public int MarketPriceGp;

    /// <summary>
    /// Spell prerequisites by spell ID. Each spell can optionally be skipped with a +5 DC increase
    /// to the Spellcraft check per DMG p.282.
    /// </summary>
    public List<string> RequiredSpellIds = new List<string>();

    /// <summary>
    /// Additional text prerequisites (e.g., "Must be evil" or "Must have access to fire domain").
    /// These are informational and validated by soft check / DM discretion.
    /// </summary>
    public List<string> OtherPrerequisites = new List<string>();

    /// <summary>
    /// If true, this is a dynamically generated item (scroll, potion, wand) based on a spell,
    /// rather than a fixed database item. The CraftingExecutor creates the item from scratch.
    /// </summary>
    public bool IsDynamic;

    /// <summary>For dynamic items: the source spell ID used to create the item.</summary>
    public string DynamicSpellId;

    /// <summary>For dynamic items: the spell level for pricing.</summary>
    public int DynamicSpellLevel;

    /// <summary>
    /// Short description for the crafting UI showing what the item does.
    /// </summary>
    public string Description;

    /// <summary>
    /// Category tag for filtering in the UI (e.g., "Ring", "Wondrous - Head", "Weapon Enhancement").
    /// </summary>
    public string Category;

    /// <summary>
    /// For arms & armor upgrades: whether this is an upgrade to an existing item
    /// rather than creating a new one.
    /// </summary>
    public bool IsUpgrade;

    /// <summary>
    /// For arms & armor: the enhancement bonus tier this represents (e.g., 1 for +1, 2 for +2).
    /// </summary>
    public int EnhancementTier;

    /// <summary>
    /// For arms & armor: whether this is a weapon (true) or armor/shield (false) enhancement.
    /// Only meaningful when RequiredFeat == CraftMagicArmsAndArmor.
    /// </summary>
    public bool IsWeaponEnhancement;

    /// <summary>Calculate crafting costs using the standard formula.</summary>
    public CraftingCostCalculator.CraftingCost GetCraftingCost()
    {
        return CraftingCostCalculator.FromMarketPrice(MarketPriceGp);
    }

    public override string ToString()
    {
        var cost = GetCraftingCost();
        return $"{DisplayName} (CL {RequiredCasterLevel}, {cost.Summary})";
    }
}
