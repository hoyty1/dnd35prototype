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
    public string InfoText => "Hit Die: d8 | BAB: +3 (full)\nGood Saves: Fortitude, Reflex\n• Favored Enemy, Combat Style\n• Animal Companion (L4)\n• Spells at L4+ (WIS-based)";

    // Class Features
    // D&D 3.5e PHB p.48: Rangers receive Track as a FREE bonus feat at 1st level.
    // Endurance at 3rd level is also automatic.
    public void InitFeats(CharacterStats stats)
    {
        if (stats != null)
        {
            // Track is granted free at level 1 (PHB p.48)
            if (!stats.HasFeat("Track"))
            {
                stats.Feats.Add("Track");
                Debug.Log($"[Ranger] {stats.CharacterName}: Granted Track as free Ranger class feature (PHB p.48).");
            }

            // Endurance at level 3 (PHB p.48)
            if (stats.Level >= 3 && !stats.HasFeat("Endurance"))
            {
                stats.Feats.Add("Endurance");
                Debug.Log($"[Ranger] {stats.CharacterName}: Granted Endurance at level 3 (PHB p.48).");
            }
        }
    }

    /// <summary>
    /// Returns a pre-built Quick Start character: Kael the Human Ranger.
    /// Archery combat style, favored enemy: Undead, wolf companion.
    /// Level 3 Ranger with WIS 14 (spells not yet available at L3).
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
            ChosenAlignment = Alignment.NeutralGood
        };
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

    /// <summary>Animal companion effective druid level = ranger level - 3 (PHB p.48).</summary>
    public static int GetEffectiveDruidLevel(int rangerLevel)
    {
        if (rangerLevel < 4) return 0;
        return rangerLevel - 3; // Ranger's companion is as a druid of level ranger-3
    }
}
