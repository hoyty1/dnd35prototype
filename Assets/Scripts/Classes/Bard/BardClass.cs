using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Bard class definition (D&D 3.5 PHB p.26-29).
/// Versatile performers who weave magic through music, with spontaneous arcane casting
/// (CHA-based, max 6th-level spells), Bardic Music abilities, and Bardic Knowledge.
/// High skill class (6 + Int per level).
/// </summary>
public class BardClass : ICharacterClass
{
    public string ClassName => "Bard";
    public string Description => "A versatile performer who weaves magic through music, with extensive lore knowledge and inspiring abilities.";

    // Core Stats (PHB p.26)
    public int HitDie => 6;
    public int BABAtLevel3 => 2; // Medium (3/4) BAB progression
    public int SkillPointsPerLevel => 6;

    // Save Progressions (PHB p.26)
    public bool GoodFortitude => false;
    public bool GoodReflex => true;
    public bool GoodWill => true;

    // Class Skills (PHB p.27) — high-skill class
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Bluff",
        "Climb",
        "Diplomacy",
        "Disguise",
        "Hide",
        "Intimidate",
        "Jump",
        "Knowledge (Arcana)",
        "Knowledge (History)",
        "Knowledge (Nature)",
        "Knowledge (Religion)",
        "Knowledge (The Planes)",
        "Listen",
        "Move Silently",
        "Search",
        "Spot",
        "Swim",
        "Tumble",
        "Use Rope"
        // Note: Concentration, Decipher Script, Gather Information, Perform,
        // Sense Motive, Sleight of Hand, Speak Language, Use Magic Device
        // not in prototype skill list
    };

    // Starting Equipment Defaults — light armor proficiency
    public int DefaultArmorBonus => 2; // Leather armor
    public int DefaultShieldBonus => 0; // Bards typically don't use shields (arcane spell failure)
    public int DefaultDamageDice => 6; // Rapier

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LEATHER_ARMOR), EquipSlot.Armor);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.RAPIER), EquipSlot.RightHand);

        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.SHORTBOW));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.AMMO_ARROW));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.DAGGER));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.COMPONENT_SPELL_POUCH));
        Debug.Log("[Bard] Equipment: Leather Armor, Rapier, Shortbow, Dagger, Spell Component Pouch");
    }

    // Spellcasting — spontaneous arcane caster (CHA-based, max 6th-level)
    public bool IsSpellcaster => true;

    // UI — purple/silver artistic theme
    public Color TitleColor => new Color(0.6f, 0.35f, 0.75f);
    public Color ButtonColor => new Color(0.35f, 0.18f, 0.45f);
    public string InfoText => "Hit Die: d6 | BAB: +2 (3/4)\nGood Saves: Reflex, Will\n• Bardic Music (9 abilities)\n• Bardic Knowledge\n• Spontaneous Arcane Spells (CHA)\n• 6 skill points per level";

    // Class Features
    // D&D 3.5e PHB p.28: Bards do NOT receive any automatic feats.
    // Bardic Music and Bardic Knowledge are class features.
    public void InitFeats(CharacterStats stats)
    {
        // No automatic feats for Bard — Bardic Music and Bardic Knowledge are class features
        Debug.Log($"[Bard] {stats.CharacterName}: Bard has no automatic feats (Bardic Music is a class feature)");
    }

    /// <summary>
    /// Returns a pre-built Quick Start character: Lyric the Half-Elf Bard.
    /// Spontaneous caster: knows a fixed set of spells, uses CHA.
    /// Level 3 Bard with CHA 16 for casting and Bardic Music DCs.
    /// </summary>
    public static CharacterCreationData GetQuickStartCharacter()
    {
        RaceDatabase.Init();
        SpellDatabase.Init();

        var data = new CharacterCreationData
        {
            CharacterName = "Lyric",
            RaceName = "Half-Elf",
            Race = RaceDatabase.GetRace("Half-Elf"),
            ClassName = "Bard",
            STR = 10, DEX = 14, CON = 12,
            INT = 13, WIS = 10, CHA = 16, // CHA primary for casting + music DCs
            SelectedFeats = new List<string> { "Dodge" },
            SelectedSpellIds = new List<string>(),
            ChosenAlignment = Alignment.ChaoticGood
        };

        // Bard known spells at level 3 (PHB p.26):
        // 6 cantrips + 3 first-level spells known
        // Use spells that exist in the database
        // Cantrips — pick from what's available
        var cantrips = new List<string> { "detect_magic_wiz", "daze", "ghost_sound", "mage_hand", "prestidigitation", "read_magic_wiz" };
        var level1Spells = new List<string> { "charm_person", "cure_light_wounds", "sleep" };

        foreach (var id in cantrips)
        {
            if (SpellDatabase.GetSpell(id) != null) data.SelectedSpellIds.Add(id);
        }
        foreach (var id in level1Spells)
        {
            if (SpellDatabase.GetSpell(id) != null) data.SelectedSpellIds.Add(id);
        }

        data.ComputeFinalStats();
        data.PreparedSpellSlotIds = new List<string>(); // Spontaneous caster

        data.SkillRanks["Diplomacy"] = 6;
        data.SkillRanks["Bluff"] = 6;
        data.SkillRanks["Listen"] = 4;
        data.SkillRanks["Hide"] = 4;
        data.SkillRanks["Move Silently"] = 4;
        data.SkillRanks["Tumble"] = 4;
        data.SkillRanks["Knowledge (Arcana)"] = 2;
        data.SkillRanks["Knowledge (History)"] = 2;
        return data;
    }

    // ─────────────────────────────────────────────
    // Bard Class Feature Queries (static helpers)
    // ─────────────────────────────────────────────

    /// <summary>Bardic Music uses per day = Bard level (PHB p.28).</summary>
    public static int BardicMusicUsesPerDay(int level) => Mathf.Max(0, level);

    /// <summary>
    /// Inspire Courage morale bonus progression (PHB p.28).
    /// +1 at L1, +2 at L8, +3 at L14, +4 at L20.
    /// </summary>
    public static int InspireCourageBonus(int level)
    {
        if (level < 1) return 0;
        if (level < 8) return 1;
        if (level < 14) return 2;
        if (level < 20) return 3;
        return 4;
    }

    /// <summary>
    /// Fascinate target count (PHB p.28).
    /// 1 at L1, +1 per 3 levels after 1st.
    /// </summary>
    public static int FascinateTargets(int level)
    {
        if (level < 1) return 0;
        return 1 + (level - 1) / 3;
    }

    /// <summary>
    /// Bardic Music DC for Will saves.
    /// DC = 10 + 1/2 bard level + Cha modifier (PHB p.28).
    /// </summary>
    public static int BardicMusicDC(int level, int chaMod)
    {
        return 10 + level / 2 + Mathf.Max(0, chaMod);
    }

    /// <summary>
    /// Bardic Knowledge modifier = Bard level + Int modifier (PHB p.28).
    /// </summary>
    public static int BardicKnowledgeModifier(int level, int intMod)
    {
        return level + intMod;
    }
}
