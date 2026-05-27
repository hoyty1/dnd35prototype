using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Druid class definition (D&D 3.5e PHB p.33-37).
/// Prepared divine caster (WIS-based, full 9th-level spells) with Wild Shape,
/// animal companion, nature bond, and unique armor/weapon restrictions.
/// </summary>
public class DruidClass : ICharacterClass
{
    public string ClassName => "Druid";
    public string Description => "A divine spellcaster who draws power from nature, capable of shapeshifting into animal forms and commanding an animal companion.";

    // Core Stats
    public int HitDie => 8;
    public int BABAtLevel3 => 2; // Medium BAB (3/4 progression)
    public int SkillPointsPerLevel => 4;

    // Save Progressions
    public bool GoodFortitude => true;
    public bool GoodReflex => false;
    public bool GoodWill => true;

    // Class Skills (D&D 3.5e PHB p.35)
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Knowledge (Nature)",
        "Listen",
        "Spot",
        "Survival",
        "Swim",
        "Climb",
        "Jump",
        "Search"
        // Note: Concentration, Diplomacy, Handle Animal, Heal, Ride, Spellcraft not in prototype skill list
    };

    // Starting Equipment Defaults — Druid restrictions: no metal armor or shields
    public int DefaultArmorBonus => 3; // Hide armor (non-metal)
    public int DefaultShieldBonus => 1; // Wooden shield
    public int DefaultDamageDice => 6; // Scimitar

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.HIDE_ARMOR), EquipSlot.Armor);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SCIMITAR), EquipSlot.RightHand);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_LIGHT_WOODEN), EquipSlot.LeftHand);

        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.SLING));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.AMMO_SLING_BULLET));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.DAGGER));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.TORCH));
        Debug.Log("[Druid] Equipment: Hide Armor, Scimitar, Wooden Shield, Sling, Bullets, Dagger, Quarterstaff");
    }

    // Spellcasting — full prepared divine caster (WIS-based)
    public bool IsSpellcaster => true;

    // UI — earthy green/brown nature theme
    public Color TitleColor => new Color(0.2f, 0.6f, 0.2f);
    public Color ButtonColor => new Color(0.15f, 0.4f, 0.1f);
    public string InfoText => "Hit Die: d8 | BAB: +2 (medium)\nGood Saves: Fortitude, Will\n• Full divine spellcasting (WIS)\n• Wild Shape (L5+)\n• Animal Companion (full level)\n• Nature Sense, Trackless Step";

    // Class Features
    public void InitFeats(CharacterStats stats)
    {
        if (stats != null)
        {
            // Druids are proficient with natural weapons (claws, bite, etc.)
            // Nature Sense is a passive +2 bonus handled in CharacterStats
            Debug.Log($"[Druid] {stats.CharacterName}: Druid class features initialized.");
        }
    }

    /// <summary>
    /// Returns a pre-built Quick Start character: Ashara the Human Druid.
    /// Nature-focused with high WIS, prepared for Wild Shape at higher levels.
    /// </summary>
    public static CharacterCreationData GetQuickStartCharacter()
    {
        RaceDatabase.Init();
        var data = new CharacterCreationData
        {
            CharacterName = "Ashara",
            RaceName = "Human",
            Race = RaceDatabase.GetRace("Human"),
            ClassName = "Druid",
            STR = 12, DEX = 10, CON = 14,
            INT = 10, WIS = 16, CHA = 13,
            SelectedFeats = new List<string> { "Spell Focus (Conjuration)", "Augment Summoning" },
            SelectedSpellIds = new List<string>(),
            ChosenAlignment = Alignment.TrueNeutral
        };
        data.ComputeFinalStats();
        data.SkillRanks["Knowledge (Nature)"] = 6;
        data.SkillRanks["Survival"] = 6;
        data.SkillRanks["Listen"] = 6;
        data.SkillRanks["Spot"] = 6;
        return data;
    }

    // ─────────────────────────────────────────────
    // Druid Class Feature Queries (static helpers)
    // ─────────────────────────────────────────────

    /// <summary>Nature Sense (L1): +2 bonus on Knowledge (Nature) and Survival checks (PHB p.35).</summary>
    public static int NatureSenseBonus => 2;

    /// <summary>Whether the druid has Wild Empathy at this level (L1, PHB p.35).</summary>
    public static bool HasWildEmpathy(int level) => level >= 1;

    /// <summary>Woodland Stride at level 2 (PHB p.36): move through natural undergrowth at normal speed.</summary>
    public static bool HasWoodlandStride(int level) => level >= 2;

    /// <summary>Trackless Step at level 3 (PHB p.36): leaves no trail in natural surroundings.</summary>
    public static bool HasTracklessStep(int level) => level >= 3;

    /// <summary>Resist Nature's Lure at level 4 (PHB p.36): +4 vs spell-like abilities of fey and plant creatures.</summary>
    public static bool HasResistNaturesLure(int level) => level >= 4;

    /// <summary>Wild Shape starts at level 5 (PHB p.37).</summary>
    public static bool HasWildShape(int level) => level >= 5;

    /// <summary>Venom Immunity at level 9 (PHB p.37): immune to all poisons.</summary>
    public static bool HasVenomImmunity(int level) => level >= 9;

    /// <summary>A Thousand Faces at level 13 (PHB p.37): alter self at will.</summary>
    public static bool HasAThousandFaces(int level) => level >= 13;

    /// <summary>Timeless Body at level 15 (PHB p.37): no longer takes aging penalties.</summary>
    public static bool HasTimelessBody(int level) => level >= 15;

    /// <summary>Wild Shape uses per day at the given level (delegates to WildShapeData).</summary>
    public static int WildShapeUsesPerDay(int level) => WildShapeData.GetUsesPerDay(level);

    /// <summary>Wild Shape duration in hours at the given level.</summary>
    public static int WildShapeDurationHours(int level) => WildShapeData.GetDurationHours(level);

    /// <summary>
    /// Druid's effective level for animal companion = full druid level (PHB p.36).
    /// Unlike Ranger (level - 3), druids get full companion progression.
    /// </summary>
    public static int GetEffectiveDruidLevel(int druidLevel)
    {
        return druidLevel >= 1 ? druidLevel : 0;
    }

    /// <summary>Resist Nature's Lure save bonus (PHB p.36).</summary>
    public static int ResistNaturesLureBonus => 4;

    /// <summary>
    /// Druid weapon proficiencies (PHB p.34): club, dagger, dart, quarterstaff,
    /// scimitar, sickle, shortspear, sling, spear. NO metal weapons beyond these.
    /// </summary>
    public static readonly string[] DruidWeapons = {
        "Club", "Dagger", "Dart", "Quarterstaff", "Scimitar",
        "Sickle", "Shortspear", "Sling", "Spear"
    };

    /// <summary>
    /// Druid armor proficiencies (PHB p.34): light and medium armor,
    /// but ONLY non-metallic (e.g., leather, hide, wooden). Shields (non-metal) allowed.
    /// Wearing metal armor or shield prevents druid spellcasting and supernatural abilities for 24h.
    /// </summary>
    public static bool IsMetalArmor(string armorName)
    {
        if (string.IsNullOrEmpty(armorName)) return false;
        string lower = armorName.ToLower();
        // Non-metal armors the druid CAN wear
        if (lower.Contains("leather") || lower.Contains("hide") || lower.Contains("padded") || lower.Contains("wooden"))
            return false;
        // Chain, scale, banded, splint, plate, etc. are metal
        if (lower.Contains("chain") || lower.Contains("scale") || lower.Contains("banded") ||
            lower.Contains("splint") || lower.Contains("plate") || lower.Contains("breastplate"))
            return true;
        return false; // Unknown armor defaults to allowed
    }
}
