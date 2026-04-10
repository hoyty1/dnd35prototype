using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wizard class definition (D&D 3.5 PHB).
/// Arcane spellcasters who study magic through scholarship and practice.
/// </summary>
public class WizardClass : ICharacterClass
{
    public string ClassName => "Wizard";
    public string Description => "A potent spellcaster schooled in the arcane arts, wielding devastating magical power.";

    // Core Stats
    public int HitDie => 4;
    public int BABAtLevel3 => 1; // 1/2 BAB progression
    public int SkillPointsPerLevel => 2;

    // Save Progressions
    public bool GoodFortitude => false;
    public bool GoodReflex => false;
    public bool GoodWill => true;

    // Class Skills
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Appraise",
        "Diplomacy",
        "Search"
        // Note: Knowledge, Concentration, Spellcraft not in prototype skill list
    };

    // Starting Equipment Defaults
    public int DefaultArmorBonus => 0;
    public int DefaultShieldBonus => 0;
    public int DefaultDamageDice => 6;

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);

        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("crossbow_light"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("dagger"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        Debug.Log("[Wizard] Equipment: Quarterstaff, Light Crossbow, Dagger (no armor)");
    }

    // Spellcasting
    public bool IsSpellcaster => true;

    // UI
    public Color TitleColor => new Color(0.6f, 0.4f, 0.9f);
    public Color ButtonColor => new Color(0.35f, 0.2f, 0.55f);
    public string InfoText => "Hit Die: d4 | BAB: +1 (1/2)\nGood Saves: Will\n\u2022 Arcane Spells: Magic Missile,\n  Ray of Frost, Acid Splash, Mage Armor";

    // Class Features
    public void InitFeats(CharacterStats stats)
    {
        // Wizard gets Scribe Scroll at level 1 (not implemented) and bonus metamagic feat at level 1
        Debug.Log($"[Wizard] {stats.CharacterName}: Wizard class features active (Spellcasting, Arcane Bond)");
    }
}
