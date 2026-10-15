using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Ranger class definition (D&D 3.5 PHB p.46-48).
/// Skilled woodsmen and trackers with favored enemies, combat styles,
/// animal companions, and partial divine spellcasting (WIS-based, max 4th level).
/// </summary>
public class RangerClass : ICharacterClass
{
    public string ClassName => "Ranger";
    public string Description => "A cunning, skilled warrior of the wilderness who hunts favored enemies and fights with a chosen combat style.";

    // Core Stats
    public int HitDie => 8;
    public int BABAtLevel3 => 3; // Full BAB progression
    public int SkillPointsPerLevel => 6;

    // Save Progressions
    public bool GoodFortitude => true;
    public bool GoodReflex => true;
    public bool GoodWill => false;

    // Class Skills (D&D 3.5e PHB p.47)
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Climb",
        "Concentration",
        "Handle Animal",
        "Heal",
        "Hide",
        "Jump",
        "Knowledge (Dungeoneering)",
        "Knowledge (Geography)",
        "Knowledge (Nature)",
        "Listen",
        "Move Silently",
        "Ride",
        "Search",
        "Spot",
        "Survival",
        "Swim",
        "Use Rope"
    };

    // Starting Equipment Defaults
    public int DefaultArmorBonus => 3; // Studded leather
    public int DefaultShieldBonus => 0; // Rangers typically don't use shields
    public int DefaultDamageDice => 8; // Longsword

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.STUDDED_LEATHER), EquipSlot.Armor);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);

        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.LONGBOW));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.AMMO_ARROW));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.AMMO_ARROW));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.DAGGER));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.TORCH));
        Debug.Log("[Ranger] Equipment: Studded Leather, Longsword, Longbow, Arrows (x2), Dagger");
    }

    // Spellcasting — partial divine caster starting at level 4
    public bool IsSpellcaster => true;

    // UI — nature-themed green/brown
    public Color TitleColor => new Color(0.35f, 0.7f, 0.3f);
    public Color ButtonColor => new Color(0.2f, 0.45f, 0.15f);
    public string InfoText => "Hit Die: d8 | BAB: full\nGood Saves: Fortitude, Reflex\n• Favored Enemy (+2 damage, skills)\n• Combat Style: archery or two-weapon (light armor)\n• Track, Endurance (L3), Evasion (L9)\n• Not yet implemented: animal companion, spells, wild empathy";

    // Class Features: Track (1st) and Endurance (3rd ranger level) are bonus feats (PHB p.47-48). Favored enemy and
    // combat style are choices (RangerRules; the creation and level-up flows for PCs, the definition for NPCs).
    public void InitFeats(CharacterStats stats)
    {
        RangerRules.GrantAutomaticFeats(stats);
    }

    /// <summary>
    /// Returns a pre-built Quick Start character: Kael the Human Ranger 3: archery combat style (Rapid Shot in his
    /// studded leather), favored enemy undead. WIS 14 for spells from 4th level.
    /// </summary>
    public static CharacterCreationData GetQuickStartCharacter()
    {
        RaceDatabase.Init();
        var data = new CharacterCreationData
        {
            CharacterName = "Kael",
            RaceName = "Human",
            Race = RaceDatabase.GetRace("Human"),
            ClassName = "Ranger",
            STR = 14, DEX = 16, CON = 12,
            INT = 10, WIS = 14, CHA = 10,
            SelectedFeats = new List<string> { "Point Blank Shot", "Precise Shot" },
            SelectedSpellIds = new List<string>(), // No spells at level 3
            ChosenAlignment = Alignment.NeutralGood,
            RangerCombatStyleChoice = RangerCombatStyle.Archery
        };
        RangerRules.TryApplyFavoredEnemySelection(data.RangerFavoredEnemies, 3, "Undead", null, out _);
        data.ComputeFinalStats();
        data.SkillRanks["Hide"] = 6;
        data.SkillRanks["Move Silently"] = 6;
        data.SkillRanks["Spot"] = 6;
        data.SkillRanks["Listen"] = 6;
        data.SkillRanks["Survival"] = 6;
        data.SkillRanks["Search"] = 3;
        data.SkillRanks["Climb"] = 3;
        return data;
    }

    // ─────────────────────────────────────────────
    // Ranger Class Feature Queries (static helpers)
    // ─────────────────────────────────────────────

    /// <summary>Ranger levels at which favored enemies are gained (PHB p.48).</summary>
    public static readonly int[] FavoredEnemyLevels = { 1, 5, 10, 15, 20 };

    /// <summary>Returns true if the ranger gains a combat style feat at this level.</summary>
    public static bool IsStyleFeatLevel(int level) => level == 2 || level == 6 || level == 11;

    /// <summary>Returns true if the ranger has Evasion (gained at level 9, PHB p.48).</summary>
    public static bool HasEvasion(int level) => level >= 9;

    /// <summary>Returns true if the ranger has Improved Evasion (gained at level 17 per errata... 
    /// actually Rangers do NOT get Improved Evasion in 3.5e — only Evasion at 9).</summary>
    public static bool HasImprovedEvasion(int level) => false; // Rangers don't get this in 3.5e

    /// <summary>Woodland Stride at level 7 (PHB p.48).</summary>
    public static bool HasWoodlandStride(int level) => level >= 7;

    /// <summary>Swift Tracker at level 8 (PHB p.48).</summary>
    public static bool HasSwiftTracker(int level) => level >= 8;

    /// <summary>Camouflage at level 13 (PHB p.48).</summary>
    public static bool HasCamouflage(int level) => level >= 13;

    /// <summary>Hide in Plain Sight at level 17 (PHB p.48).</summary>
    public static bool HasHideInPlainSight(int level) => level >= 17;

    /// <summary>
    /// Animal companion effective druid level: half the ranger level, from 4th (PHB p.48; the book's example gives a
    /// 4th-level ranger a 2nd-level druid's companion). Rounded down.
    /// </summary>
    public static int GetEffectiveDruidLevel(int rangerLevel)
    {
        if (rangerLevel < 4) return 0;
        return rangerLevel / 2;
    }
}
