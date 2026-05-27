using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Aristocrat NPC class (D&D 3.5e DMG p.108).
/// Nobles, merchants, and leaders. d8 HD, medium BAB, all weapons and armor,
/// Will good save, bonus feat at 1st level.
/// </summary>
public class AristocratClass : ICharacterClass
{
    public string ClassName => "Aristocrat";
    public string Description => "A noble or leader skilled in politics, diplomacy, and command. Proficient with all weapons and armor.";

    // Core Stats
    public int HitDie => 8;
    public int BABAtLevel3 => 2; // Medium BAB (3/4 progression)
    public int SkillPointsPerLevel => 4;

    // Save Progressions
    public bool GoodFortitude => false;
    public bool GoodReflex => false;
    public bool GoodWill => true;

    // Class Skills (DMG p.108)
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Bluff",
        "Diplomacy",
        "Intimidate",
        "Knowledge (History)",
        "Knowledge (Nobility)",
        "Knowledge (Religion)",
        "Listen",
        "Spot",
        "Search",
        "Swim"
    };

    // Starting Equipment — all martial weapons and armor
    public int DefaultArmorBonus => 5; // Chainmail
    public int DefaultShieldBonus => 2; // Heavy steel shield
    public int DefaultDamageDice => 8; // Longsword

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAINMAIL), EquipSlot.Armor);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_STEEL), EquipSlot.LeftHand);
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.DAGGER));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        Debug.Log("[Aristocrat] Equipment: Chainmail, Longsword, Heavy Steel Shield, Dagger");
    }

    // No spellcasting
    public bool IsSpellcaster => false;

    // UI — gold/royal theme
    public Color TitleColor => new Color(0.85f, 0.75f, 0.3f);
    public Color ButtonColor => new Color(0.5f, 0.4f, 0.15f);
    public string InfoText => "Hit Die: d8 | BAB: Medium\nGood Save: Will\n• All weapons and armor\n• Bonus feat at 1st level\n• NPC class (Noble)";

    public void InitFeats(CharacterStats stats) { }

    // ─────────────────────────────────────────────
    // Aristocrat Class Features
    // ─────────────────────────────────────────────

    /// <summary>Aristocrat gains a bonus feat at 1st level (DMG p.108).</summary>
    public static bool HasBonusFeat(int level) => level >= 1;

    /// <summary>All simple and martial weapon proficiency.</summary>
    public static bool HasMartialWeaponProficiency => true;

    /// <summary>All armor and shield proficiency.</summary>
    public static bool HasAllArmorProficiency => true;
}
