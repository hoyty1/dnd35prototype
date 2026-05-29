using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

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
        "Concentration",
        "Diplomacy",
        "Heal",
        "Intimidate",
        "Knowledge (Arcana)",
        "Knowledge (History)",
        "Knowledge (Religion)",
        "Knowledge (The Planes)",
        "Spellcraft"
    };

    // Starting Equipment Defaults
    public int DefaultArmorBonus => 4;
    public int DefaultShieldBonus => 2;
    public int DefaultDamageDice => 8;

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAIN_SHIRT), EquipSlot.Armor);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.MACE_HEAVY), EquipSlot.RightHand);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_WOODEN), EquipSlot.LeftHand);

        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.CROSSBOW_LIGHT));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.AMMO_BOLT));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_SHIELD_OF_FAITH));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.COMPONENT_SPELL_POUCH));
        Debug.Log("[Cleric] Equipment: Chain Shirt, Heavy Shield, Heavy Mace, Light Crossbow, Spell Component Pouch");
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
            WeaponFocusChoice = "Mace, Heavy",
            // Cleric selects 4 orisons (D&D 3.5e PHB)
            SelectedSpellIds = new List<string>
            {
                SpellNames.CURE_MINOR_WOUNDS, "detect_magic_clr", SpellNames.GUIDANCE, "light_clr"
            },
            // Pre-prepared spell slots: 4 orisons + 4 level-1 + 3 level-2 = 11 total
            // Curated for a combat-ready healer/support cleric
            PreparedSpellSlotIds = new List<string>
            {
                // Level 0 orisons (4 slots — unlimited use)
                SpellNames.CURE_MINOR_WOUNDS, "detect_magic_clr", SpellNames.GUIDANCE, "light_clr",
                // Level 1 spells (4 slots: 2 base + 1 domain + 1 WIS bonus)
                SpellNames.CURE_LIGHT_WOUNDS, SpellNames.CURE_LIGHT_WOUNDS, SpellNames.BLESS, SpellNames.SHIELD_OF_FAITH,
                // Level 2 spells (3 slots: 1 base + 1 domain + 1 WIS bonus)
                SpellNames.CURE_MODERATE_WOUNDS, SpellNames.SPIRITUAL_WEAPON, SpellNames.FLAME_STRIKE
            },
            ChosenAlignment = Alignment.NeutralGood,
            ChosenDeityId = "pelor",
            ChosenDomains = new List<string> { "Healing", "Good" },
            SpontaneousCasting = SpontaneousCastingType.Cure // NG cleric → cure spells
        };
        data.ComputeFinalStats();
        data.SkillRanks["Concentration"] = 6;
        data.SkillRanks["Heal"] = 6;
        data.SkillRanks["Diplomacy"] = 4;
        data.SkillRanks["Knowledge (Religion)"] = 4;
        return data;
    }
}
