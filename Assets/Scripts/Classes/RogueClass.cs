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
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_shield_of_faith"));
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
    // D&D 3.5e PHB p.49-51: Rogues do NOT receive any automatic feats.
    // Rogues gain class features like Sneak Attack, Trapfinding, Evasion, etc.
    // but these are class features, not feats. Do not add any feats here.
    public void InitFeats(CharacterStats stats)
    {
        // No automatic feats for Rogue — all feats must be selected by the player.
        Debug.Log($"[Rogue] {stats.CharacterName}: Rogue has no automatic feats");
    }

    /// <summary>
    /// Returns a pre-built Quick Start character: Lyra the Elf Rogue.
    /// Used by CharacterCreationUI for the Quick Start button.
    /// </summary>
    public static CharacterCreationData GetQuickStartCharacter()
    {
        RaceDatabase.Init();
        var data = new CharacterCreationData
        {
            CharacterName = "Lyra",
            RaceName = "Elf",
            Race = RaceDatabase.GetRace("Elf"),
            ClassName = "Rogue",
            STR = 12, DEX = 17, CON = 12,
            INT = 14, WIS = 13, CHA = 10,
            SelectedFeats = new List<string> { "Weapon Finesse", "Dodge" },
            SelectedSpellIds = new List<string>(), // Rogues don't cast spells
            ChosenAlignment = Alignment.ChaoticGood
        };
        data.ComputeFinalStats();
        data.SkillRanks["Hide"] = 6;
        data.SkillRanks["Move Silently"] = 6;
        data.SkillRanks["Spot"] = 6;
        data.SkillRanks["Listen"] = 6;
        data.SkillRanks["Disable Device"] = 5;
        data.SkillRanks["Open Lock"] = 5;
        data.SkillRanks["Search"] = 5;
        data.SkillRanks["Tumble"] = 4;
        data.SkillRanks["Bluff"] = 4;
        data.SkillRanks["Diplomacy"] = 4;
        data.SkillRanks["Climb"] = 4;
        data.SkillRanks["Balance"] = 3;
        data.SkillRanks["Sleight of Hand"] = 2;
        return data;
    }
}
