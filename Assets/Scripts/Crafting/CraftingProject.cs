// ============================================================================
// D&D 3.5e Item Creation Feats - Crafting Project (Session Data)
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// Represents a validated, ready-to-execute crafting project.
/// Created by CraftingValidator, consumed by CraftingExecutor.
/// This is a transient data object — not persisted between sessions.
/// </summary>
[System.Serializable]
public class CraftingProject
{
    /// <summary>The item definition being crafted.</summary>
    public CraftableItemDefinition Definition;

    /// <summary>The character performing the crafting.</summary>
    [System.NonSerialized]
    public CharacterStats Crafter;

    /// <summary>Gold cost in gp (raw materials).</summary>
    public int GoldCost;

    /// <summary>XP cost to the crafter.</summary>
    public int XPCost;

    /// <summary>Number of days required.</summary>
    public int CraftingDays;

    /// <summary>Market price of the finished item.</summary>
    public int MarketPriceGp;

    /// <summary>
    /// List of missing spell prerequisites. Each missing spell adds +5 to the Spellcraft DC.
    /// Empty if all spell prereqs are met.
    /// </summary>
    public List<string> MissingSpells = new List<string>();

    /// <summary>
    /// The Spellcraft DC for this project (base 5 + 5 per missing spell).
    /// 0 if no substitution is needed (all prereqs met).
    /// </summary>
    public int SpellcraftDC;

    /// <summary>
    /// For arms & armor upgrades: the item being upgraded (from inventory).
    /// Null for new item creation.
    /// </summary>
    [System.NonSerialized]
    public ItemData UpgradeTargetItem;

    /// <summary>
    /// For dynamic items (scroll/potion/wand): the caster level to encode into the item.
    /// For scrolls this affects spell DC; for wands/potions it affects pricing.
    /// </summary>
    public int ItemCasterLevel;

    /// <summary>Whether this crafting project passed all validation checks.</summary>
    public bool IsValid;

    /// <summary>Human-readable reason if validation failed.</summary>
    public string ValidationError;

    /// <summary>
    /// Summary string for the confirmation dialog.
    /// </summary>
    public string GetSummary()
    {
        string name = Definition?.DisplayName ?? "Unknown Item";
        string costLine = $"Gold: {GoldCost:N0} gp | XP: {XPCost:N0} | Time: {CraftingDays} day{(CraftingDays != 1 ? "s" : "")}";
        string missingLine = MissingSpells.Count > 0
            ? $"\n⚠ Missing spells ({MissingSpells.Count}): Spellcraft DC {SpellcraftDC}"
            : "";
        return $"Craft: {name}\n{costLine}{missingLine}";
    }
}
