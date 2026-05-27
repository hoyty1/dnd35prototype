using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Expert NPC class (D&D 3.5e DMG p.109-110).
/// Skilled professionals: smiths, merchants, scouts, artisans.
/// d6 HD, medium BAB, Ref+Will good, 6 skill points (highest of NPC classes!),
/// ALL skills are class skills, bonus feat every 4 levels.
/// </summary>
public class ExpertClass : ICharacterClass
{
    public string ClassName => "Expert";
    public string Description => "A skilled professional or artisan with broad expertise. All skills are class skills.";

    // Core Stats
    public int HitDie => 6;
    public int BABAtLevel3 => 2; // Medium BAB (3/4 progression)
    public int SkillPointsPerLevel => 6; // Highest of NPC classes

    // Save Progressions
    public bool GoodFortitude => false;
    public bool GoodReflex => true;
    public bool GoodWill => true;

    // Class Skills — ALL skills are class skills for Expert (DMG p.110)
    // We include every known skill in the prototype
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Bluff", "Climb", "Diplomacy", "Hide", "Intimidate",
        "Jump", "Knowledge (Arcana)", "Knowledge (History)",
        "Knowledge (Nature)", "Knowledge (Nobility)", "Knowledge (Religion)",
        "Knowledge (The Planes)", "Knowledge (Dungeoneering)", "Knowledge (Local)",
        "Knowledge (Geography)", "Listen", "Move Silently",
        "Open Lock", "Search", "Spot", "Survival", "Swim",
        "Tumble", "Use Magic Device", "Disable Device",
        "Decipher Script", "Forgery", "Gather Information",
        "Perform", "Profession", "Craft", "Appraise", "Sense Motive",
        "Sleight of Hand", "Speak Language", "Use Rope"
    };

    // Starting Equipment
    public int DefaultArmorBonus => 1; // Padded or leather armor (light only)
    public int DefaultShieldBonus => 0;
    public int DefaultDamageDice => 6; // Short sword

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LEATHER_ARMOR), EquipSlot.Armor);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHORT_SWORD), EquipSlot.RightHand);
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.DAGGER));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.TORCH));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        Debug.Log("[Expert] Equipment: Leather Armor, Short Sword, Dagger, Torch");
    }

    // No spellcasting
    public bool IsSpellcaster => false;

    // UI — teal/professional theme
    public Color TitleColor => new Color(0.3f, 0.6f, 0.65f);
    public Color ButtonColor => new Color(0.15f, 0.35f, 0.4f);
    public string InfoText => "Hit Die: d6 | BAB: Medium\nGood Saves: Reflex, Will\n• 6 skill points/level\n• All skills are class skills\n• Bonus feat every 4 levels\n• NPC class (Professional)";

    public void InitFeats(CharacterStats stats) { }

    // ─────────────────────────────────────────────
    // Expert Class Features
    // ─────────────────────────────────────────────

    /// <summary>
    /// Expert gets bonus feats at levels 1, 4, 8, 12, 16, 20 (every 4 levels, DMG p.110).
    /// </summary>
    public static int BonusFeats(int level)
    {
        if (level < 1) return 0;
        // 1 at L1, then +1 at 4, 8, 12, 16, 20
        return 1 + (level - 1) / 4;
    }

    /// <summary>Whether this level grants a bonus feat.</summary>
    public static bool IsBonusFeatLevel(int level)
    {
        return level == 1 || (level >= 4 && (level % 4 == 0));
    }

    /// <summary>All skills are class skills for Expert (DMG p.110).</summary>
    public static bool AllSkillsAreClassSkills => true;

    /// <summary>Simple weapon proficiency and light armor proficiency.</summary>
    public static bool HasLightArmorProficiency => true;
}
