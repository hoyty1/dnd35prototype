using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Warrior NPC class (D&D 3.5e DMG p.110).
/// Guards, soldiers, and militia. d8 HD, GOOD BAB (full 1.0/level),
/// Fort good save, all weapons and armor, bonus feat at 1st level.
/// Equivalent to a Fighter without bonus feats progression.
/// </summary>
public class WarriorClass : ICharacterClass
{
    public string ClassName => "Warrior";
    public string Description => "A trained soldier or guard proficient with all weapons and armor. The martial NPC class.";

    // Core Stats
    public int HitDie => 8;
    public int BABAtLevel3 => 3; // Good BAB (full 1.0/level)
    public int SkillPointsPerLevel => 2;

    // Save Progressions
    public bool GoodFortitude => true;
    public bool GoodReflex => false;
    public bool GoodWill => false;

    // Class Skills (DMG p.110)
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Climb",
        "Intimidate",
        "Jump",
        "Swim"
    };

    // Starting Equipment — full martial
    public int DefaultArmorBonus => 5; // Chainmail
    public int DefaultShieldBonus => 2; // Heavy steel shield
    public int DefaultDamageDice => 8; // Longsword

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAINMAIL), EquipSlot.Armor);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_STEEL), EquipSlot.LeftHand);
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.SHORTBOW));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.AMMO_ARROW));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.DAGGER));
        Debug.Log("[Warrior] Equipment: Chainmail, Longsword, Heavy Steel Shield, Shortbow, Arrows, Dagger");
    }

    // No spellcasting
    public bool IsSpellcaster => false;

    // UI — steel grey/blue theme
    public Color TitleColor => new Color(0.5f, 0.55f, 0.6f);
    public Color ButtonColor => new Color(0.3f, 0.33f, 0.38f);
    public string InfoText => "Hit Die: d8 | BAB: +3 (full)\nGood Save: Fortitude\n• All weapons and armor\n• Bonus feat at 1st level\n• NPC class (Soldier)";

    public void InitFeats(CharacterStats stats) { }

    // ─────────────────────────────────────────────
    // Warrior Class Features
    // ─────────────────────────────────────────────

    /// <summary>Warrior gains a bonus feat at 1st level (DMG p.110).</summary>
    public static bool HasBonusFeat(int level) => level >= 1;

    /// <summary>All simple and martial weapon proficiency.</summary>
    public static bool HasMartialWeaponProficiency => true;

    /// <summary>All armor and shield proficiency (including tower shield).</summary>
    public static bool HasAllArmorProficiency => true;
}
