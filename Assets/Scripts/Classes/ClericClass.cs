using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cleric class definition (D&D 3.5 PHB).
/// Divine spellcasters who channel the power of their deity to heal, protect, and smite.
/// </summary>
public class ClericClass : ICharacterClass
{
    public string ClassName => "Cleric";
    public string Description => "A master of divine magic and target capable healer who serves a higher power.";

    // Core Stats
    public int HitDie => 8;
    public int BABAtLevel3 => 2; // 3/4 BAB progression
    public int SkillPointsPerLevel => 2;

    // Save Progressions
    public bool GoodFortitude => true;
    public bool GoodReflex => false;
    public bool GoodWill => true;

    // Class Skills
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Diplomacy",
        "Intimidate"
        // Note: Concentration, Heal, Knowledge (Religion) not in prototype skill list
    };

    // Starting Equipment Defaults
    public int DefaultArmorBonus => 4;
    public int DefaultShieldBonus => 2;
    public int DefaultDamageDice => 8;

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chain_shirt"), EquipSlot.Armor);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("mace_heavy"), EquipSlot.RightHand);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("shield_heavy_wooden"), EquipSlot.LeftHand);

        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("crossbow_light"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        Debug.Log("[Cleric] Equipment: Chain Shirt, Heavy Shield, Heavy Mace, Light Crossbow");
    }

    // Spellcasting
    public bool IsSpellcaster => true;

    // UI
    public Color TitleColor => new Color(0.9f, 0.85f, 0.3f);
    public Color ButtonColor => new Color(0.55f, 0.5f, 0.1f);
    public string InfoText => "Hit Die: d8 | BAB: +2 (3/4)\nGood Saves: Fortitude, Will\n\u2022 Divine Spells: Cure Light Wounds,\n  Inflict Minor Wounds";

    // Class Features
    // D&D 3.5e PHB p.30-33: Clerics do NOT receive any automatic feats.
    // Turn/Rebuke Undead is a class feature, not a feat.
    // Domain powers are class features, not feats.
    public void InitFeats(CharacterStats stats)
    {
        // No automatic feats for Cleric — Turn Undead and domain powers are class features.
        Debug.Log($"[Cleric] {stats.CharacterName}: Cleric has no automatic feats (Turn Undead is a class feature)");
    }

    /// <summary>
    /// Returns a pre-built Quick Start character: Theron the Human Cleric.
    /// Used by CharacterCreationUI for the Quick Start button.
    ///
    /// D&D 3.5e Level 3 Cleric with WIS 16 (+3) spell slots:
    ///   Level 0 (Orisons): 4 slots (unlimited use)
    ///   Level 1: 2 base + 1 domain + 1 WIS bonus = 4 slots
    ///   Level 2: 1 base + 1 domain + 1 WIS bonus = 3 slots
    ///
    /// PreparedSpellSlotIds order: [4 orisons, 4 level-1, 3 level-2]
    /// </summary>
    public static CharacterCreationData GetQuickStartCharacter()
    {
        RaceDatabase.Init();
        var data = new CharacterCreationData
        {
            CharacterName = "Theron",
            RaceName = "Human",
            Race = RaceDatabase.GetRace("Human"),
            ClassName = "Cleric",
            STR = 14, DEX = 10, CON = 14,
            INT = 10, WIS = 16, CHA = 12,
            SelectedFeats = new List<string> { "Combat Casting", "Weapon Focus" },
            WeaponFocusChoice = "Mace",
            // Cleric selects 4 orisons (D&D 3.5e PHB)
            SelectedSpellIds = new List<string>
            {
                "cure_minor_wounds", "detect_magic_clr", "guidance", "light_clr"
            },
            // Pre-prepared spell slots: 4 orisons + 4 level-1 + 3 level-2 = 11 total
            // Curated for a combat-ready healer/support cleric
            PreparedSpellSlotIds = new List<string>
            {
                // Level 0 orisons (4 slots — unlimited use)
                "cure_minor_wounds", "detect_magic_clr", "guidance", "light_clr",
                // Level 1 spells (4 slots: 2 base + 1 domain + 1 WIS bonus)
                "cure_light_wounds", "cure_light_wounds", "bless", "shield_of_faith",
                // Level 2 spells (3 slots: 1 base + 1 domain + 1 WIS bonus)
                "cure_moderate_wounds", "spiritual_weapon", "bulls_strength_clr"
            }
        };
        data.ComputeFinalStats();
        data.SkillRanks["Concentration"] = 6;
        data.SkillRanks["Heal"] = 6;
        data.SkillRanks["Diplomacy"] = 4;
        data.SkillRanks["Knowledge (Religion)"] = 4;
        return data;
    }
}
