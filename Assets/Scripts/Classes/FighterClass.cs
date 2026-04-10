using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fighter class definition (D&D 3.5 PHB).
/// Masters of martial combat with full BAB, d10 hit die, and bonus combat feats.
/// </summary>
public class FighterClass : ICharacterClass
{
    public string ClassName => "Fighter";
    public string Description => "A warrior with great combat capability and unmatched prowess with weapons and armor.";

    // Core Stats
    public int HitDie => 10;
    public int BABAtLevel3 => 3; // Full BAB progression
    public int SkillPointsPerLevel => 2;

    // Save Progressions
    public bool GoodFortitude => true;
    public bool GoodReflex => false;
    public bool GoodWill => false;

    // Class Skills
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Climb",
        "Intimidate",
        "Jump",
        "Swim"
    };

    // Starting Equipment Defaults
    public int DefaultArmorBonus => 4;
    public int DefaultShieldBonus => 2;
    public int DefaultDamageDice => 8;

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("scale_mail"), EquipSlot.Armor);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("longsword"), EquipSlot.RightHand);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("shield_heavy_wooden"), EquipSlot.LeftHand);

        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("shortbow"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("torch"));
        Debug.Log("[Fighter] Equipment: Scale Mail, Heavy Shield, Longsword, Shortbow");
    }

    // Spellcasting
    public bool IsSpellcaster => false;

    // UI
    public Color TitleColor => new Color(0.9f, 0.6f, 0.3f);
    public Color ButtonColor => new Color(0.5f, 0.3f, 0.15f);
    public string InfoText => "Hit Die: d10 | BAB: +3 (full)\nGood Saves: Fortitude\n\u2022 Bonus combat feats\nEquipment: Scale Mail, Shield, Longsword";

    // Class Features
    public void InitFeats(CharacterStats stats)
    {
        stats.Feats.Add("Power Attack");
    }
}
