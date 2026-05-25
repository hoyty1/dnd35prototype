using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

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
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);

        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.CROSSBOW_LIGHT));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.AMMO_BOLT));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.DAGGER));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_SHIELD_OF_FAITH));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.COMPONENT_SPELL_POUCH));
        Debug.Log("[Wizard] Equipment: Quarterstaff, Light Crossbow, Dagger, Spell Component Pouch (no armor)");
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
        // D&D 3.5e PHB p.57: Wizards receive Scribe Scroll as a FREE bonus feat at 1st level.
        // This is a class feature, not a regular feat selection.
        if (stats != null && !stats.HasFeat("Scribe Scroll"))
        {
            stats.AddFeats(new System.Collections.Generic.List<string> { "Scribe Scroll" });
            Debug.Log($"[Wizard] {stats.CharacterName}: Granted Scribe Scroll as free Wizard class feature (PHB p.57).");
        }
        else
        {
            Debug.Log($"[Wizard] {stats.CharacterName}: Already has Scribe Scroll, skipping auto-grant.");
        }
    }

    /// <summary>
    /// Returns a pre-built Quick Start character: Elara the Elf Wizard.
    /// Used by CharacterCreationUI for the Quick Start button.
    /// D&D 3.5e PHB: All cantrips auto-added to spellbook + 3+INT mod 1st-level + 2 2nd-level.
    /// Elara has INT 17 (base) + 0 (Elf racial) = 17, INT mod = +3
    /// Spellbook: All 20 cantrips + 6 (3+3) 1st-level + 6 2nd-level spells.
    /// Slots: 4 cantrips, 3 (2 base + 1 bonus) 1st, 2 (1 base + 1 bonus) 2nd.
    /// Prepared for quick testing: includes Summon Monster I and Summon Monster II,
    /// plus core damage staples (Magic Missile, Scorching Ray).
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

        // 1st-level spells for spellbook: include core damage + size magic testing spells.
        data.SelectedSpellIds.AddRange(new List<string>
        {
            SpellNames.MAGIC_MISSILE, SpellNames.BURNING_HANDS, SpellNames.ENLARGE_PERSON, SpellNames.REDUCE_PERSON,
            SpellNames.MAGE_ARMOR, SpellNames.SHIELD, SpellNames.SLEEP, SpellNames.CHARM_PERSON
        });

        // 2nd-level spells for spellbook: includes combat staples + summon test/debug options
        data.SelectedSpellIds.AddRange(new List<string>
        {
            SpellNames.SCORCHING_RAY, SpellNames.SUMMON_MONSTER_2, SpellNames.ACID_FOG,
            SpellNames.TEST_CONE_30, SpellNames.TEST_CONE_60, SpellNames.TEST_LINE_60
        });

        data.ComputeFinalStats();

        // Pre-set spell preparation (slot order: 4 cantrips, 3 1st-level, 2 2nd-level)
        // Includes Summon Monster I/II for quick summon-system testing.
        data.PreparedSpellSlotIds = new List<string>
        {
            // 4 cantrip slots (unlimited use)
            SpellNames.RAY_OF_FROST, SpellNames.DETECT_MAGIC_WIZ, SpellNames.ACID_SPLASH, SpellNames.PRESTIDIGITATION,
            // 3 1st-level slots (2 base + 1 INT bonus)
            SpellNames.MAGIC_MISSILE, SpellNames.ENLARGE_PERSON, SpellNames.REDUCE_PERSON,
            // 2 2nd-level slots (1 base + 1 INT bonus)
            SpellNames.SCORCHING_RAY, SpellNames.SUMMON_MONSTER_2
        };

        data.SkillRanks["Concentration"] = 6;
        data.SkillRanks["Spellcraft"] = 6;
        data.SkillRanks["Knowledge (Arcana)"] = 6;
        return data;
    }
}
