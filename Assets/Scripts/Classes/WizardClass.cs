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
    // D&D 3.5e PHB p.55-57: Wizards do NOT receive any automatic feats.
    // Scribe Scroll is a bonus feat granted at 1st level (PHB p.57), but it is a
    // class feature/bonus feat, not truly "automatic" — it's listed under class features.
    // Wizard bonus feats (metamagic/item creation) at levels 5, 10, 15, 20 are
    // SELECTIONS handled by the character creation UI / level-up system.
    public void InitFeats(CharacterStats stats)
    {
        // No automatic feats for Wizard — Scribe Scroll and bonus feat selections
        // are handled by the character creation UI / level-up system.
        Debug.Log($"[Wizard] {stats.CharacterName}: Wizard has no automatic feats (Scribe Scroll and bonus feats handled separately)");
    }

    /// <summary>
    /// Returns a pre-built Quick Start character: Elara the Elf Wizard.
    /// Used by CharacterCreationUI for the Quick Start button.
    /// D&D 3.5e PHB: All cantrips auto-added to spellbook + 3+INT mod 1st-level + 2 2nd-level.
    /// Elara has INT 17 (base) + 0 (Elf racial) = 17, INT mod = +3
    /// Spellbook: All 20 cantrips + 6 (3+3) 1st-level + 2 2nd-level
    /// Slots: 4 cantrips, 3 (2 base + 1 bonus) 1st, 2 (1 base + 1 bonus) 2nd
    /// </summary>
    public static CharacterCreationData GetQuickStartCharacter()
    {
        RaceDatabase.Init();
        SpellDatabase.Init();

        var data = new CharacterCreationData
        {
            CharacterName = "Elara",
            RaceName = "Elf",
            Race = RaceDatabase.GetRace("Elf"),
            ClassName = "Wizard",
            STR = 8, DEX = 14, CON = 12,
            INT = 17, WIS = 13, CHA = 10,
            SelectedFeats = new List<string> { "Spell Focus", "Improved Initiative" },
            BonusFeats = new List<string> { "Scribe Scroll" },
            SelectedSpellIds = new List<string>(),
            ChosenAlignment = Alignment.NeutralGood
        };

        // All cantrips automatically added to spellbook (D&D 3.5e PHB p.57)
        var allCantrips = SpellDatabase.GetSpellsForClassAtLevel("Wizard", 0);
        foreach (var cantrip in allCantrips)
        {
            data.SelectedSpellIds.Add(cantrip.SpellId);
        }

        // 1st-level spells for spellbook: 3 + INT mod(+3) = 6 spells
        data.SelectedSpellIds.AddRange(new List<string>
        {
            "magic_missile", "mage_armor", "shield",
            "burning_hands", "sleep", "charm_person"
        });

        // 2nd-level spells for spellbook: 2 spells
        data.SelectedSpellIds.AddRange(new List<string>
        {
            "scorching_ray", "bulls_strength"
        });

        data.ComputeFinalStats();

        // Pre-set spell preparation (slot order: 4 cantrips, 3 1st-level, 2 2nd-level)
        data.PreparedSpellSlotIds = new List<string>
        {
            // 4 cantrip slots (unlimited use)
            "ray_of_frost", "detect_magic_wiz", "acid_splash", "prestidigitation",
            // 3 1st-level slots (2 base + 1 INT bonus)
            "magic_missile", "mage_armor", "shield",
            // 2 2nd-level slots (1 base + 1 INT bonus)
            "scorching_ray", "bulls_strength"
        };

        data.SkillRanks["Concentration"] = 6;
        data.SkillRanks["Spellcraft"] = 6;
        data.SkillRanks["Knowledge (Arcana)"] = 6;
        return data;
    }
}
