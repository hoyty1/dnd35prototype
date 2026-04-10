using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rogue class definition (D&D 3.5 PHB).
/// Skilled combatants who use stealth and cunning, with sneak attack and evasion.
/// </summary>
public class RogueClass : ICharacterClass
{
    public string ClassName => "Rogue";
    public string Description => "A tricky, skillful scout and spy who wins the battle by stealth rather than brute force.";

    // Core Stats
    public int HitDie => 6;
    public int BABAtLevel3 => 2; // 3/4 BAB progression
    public int SkillPointsPerLevel => 8;

    // Save Progressions
    public bool GoodFortitude => false;
    public bool GoodReflex => true;
    public bool GoodWill => false;

    // Class Skills
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Appraise",
        "Balance",
        "Bluff",
        "Climb",
        "Diplomacy",
        "Disable Device",
        "Gather Information",
        "Hide",
        "Intimidate",
        "Jump",
        "Listen",
        "Move Silently",
        "Open Lock",
        "Search",
        "Sleight of Hand",
        "Spot",
        "Tumble",
        "Use Magic Device"
    };

    // Starting Equipment Defaults
    public int DefaultArmorBonus => 2;
    public int DefaultShieldBonus => 0;
    public int DefaultDamageDice => 6;

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("leather_armor"), EquipSlot.Armor);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("rapier"), EquipSlot.RightHand);

        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("shortbow"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("dagger"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("rope"));
        Debug.Log("[Rogue] Equipment: Leather Armor, Rapier, Shortbow, Dagger");
    }

    // Spellcasting
    public bool IsSpellcaster => false;

    // UI
    public Color TitleColor => new Color(0.5f, 0.8f, 0.5f);
    public Color ButtonColor => new Color(0.2f, 0.4f, 0.2f);
    public string InfoText => "Hit Die: d6 | BAB: +2 (3/4)\nGood Saves: Reflex\n\u2022 Sneak Attack +2d6, Evasion\nEquipment: Leather Armor, Rapier, Shortbow";

    // Class Features
    public void InitFeats(CharacterStats stats)
    {
        stats.Feats.Add("Point Blank Shot");
        stats.Feats.Add("Rapid Shot");
    }
}
