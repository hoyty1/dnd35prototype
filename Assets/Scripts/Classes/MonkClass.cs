using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monk class definition (D&D 3.5 PHB).
/// Martial artists who combine speed, unarmed combat, and spiritual discipline.
/// </summary>
public class MonkClass : ICharacterClass
{
    public string ClassName => "Monk";
    public string Description => "A martial artist who hones body and mind to physical and spiritual perfection.";

    // Core Stats
    public int HitDie => 8;
    public int BABAtLevel3 => 2; // 3/4 BAB progression
    public int SkillPointsPerLevel => 4;

    // Save Progressions
    public bool GoodFortitude => true;
    public bool GoodReflex => true;
    public bool GoodWill => true;

    // Class Skills
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Balance",
        "Climb",
        "Diplomacy",
        "Hide",
        "Jump",
        "Listen",
        "Move Silently",
        "Spot",
        "Swim",
        "Tumble"
    };

    // Starting Equipment Defaults
    public int DefaultArmorBonus => 0;
    public int DefaultShieldBonus => 0;
    public int DefaultDamageDice => 6;

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);

        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("sling"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        Debug.Log("[Monk] Equipment: Quarterstaff, Sling (unarmored for WIS AC bonus)");
    }

    // Spellcasting
    public bool IsSpellcaster => false;

    // UI
    public Color TitleColor => new Color(0.4f, 0.7f, 0.9f);
    public Color ButtonColor => new Color(0.15f, 0.35f, 0.5f);
    public string InfoText => "Hit Die: d8 | BAB: +2 (3/4)\nGood Saves: Fort, Ref, Will\n\u2022 Flurry of Blows, +WIS to AC\nEquipment: Quarterstaff, Sling";

    // Class Features
    public void InitFeats(CharacterStats stats)
    {
        stats.Feats.Add("Improved Unarmed Strike");
        stats.Feats.Add("Stunning Fist");
        stats.Feats.Add("Improved Grapple");
        Debug.Log($"[Monk] {stats.CharacterName}: Granted monk bonus feats: Improved Unarmed Strike, Stunning Fist, Improved Grapple");
    }
}
