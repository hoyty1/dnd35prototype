using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Paladin class definition (D&D 3.5 PHB p.42-45).
/// Holy warriors dedicated to Law and Good, with smite evil, lay on hands,
/// divine grace, aura of courage, turn undead (L4), and partial divine spellcasting.
/// Alignment restriction: Lawful Good only.
/// </summary>
public class PaladinClass : ICharacterClass
{
    public string ClassName => "Paladin";
    public string Description => "A holy warrior bound to Lawful Good, wielding divine power to smite evil and protect the innocent.";

    // Core Stats
    public int HitDie => 10;
    public int BABAtLevel3 => 3; // Full BAB progression
    public int SkillPointsPerLevel => 2;

    // Save Progressions
    public bool GoodFortitude => true;
    public bool GoodReflex => false;
    public bool GoodWill => false;

    // Class Skills (D&D 3.5e PHB p.43)
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Concentration",
        "Diplomacy",
        "Handle Animal",
        "Heal",
        "Knowledge (Nobility)",
        "Knowledge (Religion)",
        "Knowledge (The Planes)",
        "Ride",
        "Sense Motive"
    };

    // Starting Equipment Defaults
    public int DefaultArmorBonus => 5; // Chainmail
    public int DefaultShieldBonus => 2; // Heavy steel shield
    public int DefaultDamageDice => 8; // Longsword

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAINMAIL), EquipSlot.Armor);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_STEEL), EquipSlot.LeftHand);

        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.DAGGER));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.SHORTBOW));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.AMMO_ARROW));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_SHIELD_OF_FAITH));
        Debug.Log("[Paladin] Equipment: Chainmail, Heavy Steel Shield, Longsword, Dagger, Shortbow");
    }

    // Spellcasting — partial divine caster starting at level 4
    public bool IsSpellcaster => true;

    // UI — gold/white holy theme
    public Color TitleColor => new Color(0.95f, 0.85f, 0.35f);
    public Color ButtonColor => new Color(0.6f, 0.5f, 0.15f);
    public string InfoText => "Hit Die: d10 | BAB: +3 (full)\nGood Saves: Fortitude\n• Smite Evil, Lay on Hands\n• Divine Grace, Aura of Courage\n• Turn Undead (L4), Spells (L4+)\n• Alignment: Lawful Good only";

    // Class Features
    // D&D 3.5e PHB p.44: Paladins do NOT receive any automatic feats.
    // Their abilities (Smite Evil, Divine Grace, etc.) are class features.
    public void InitFeats(CharacterStats stats)
    {
        // No automatic feats for Paladin — all abilities are class features, not feats.
        Debug.Log($"[Paladin] {stats.CharacterName}: Paladin has no automatic feats (Smite Evil, Lay on Hands, etc. are class features)");
    }

    /// <summary>
    /// Returns a pre-built Quick Start character: Corrin the Human Paladin.
    /// Level 3 Paladin with STR 16, CHA 14 for smite/lay on hands, WIS 12 for future spells.
    /// Alignment: Lawful Good (required).
    /// </summary>
    public static CharacterCreationData GetQuickStartCharacter()
    {
        RaceDatabase.Init();
        var data = new CharacterCreationData
        {
            CharacterName = "Corrin",
            RaceName = "Human",
            Race = RaceDatabase.GetRace("Human"),
            ClassName = "Paladin",
            STR = 16, DEX = 10, CON = 14,
            INT = 10, WIS = 12, CHA = 14,
            SelectedFeats = new List<string> { "Power Attack", "Cleave" },
            SelectedSpellIds = new List<string>(), // No spells at level 3
            ChosenAlignment = Alignment.LawfulGood
        };
        data.ComputeFinalStats();
        data.SkillRanks["Diplomacy"] = 6;
        data.SkillRanks["Knowledge (Religion)"] = 4;
        return data;
    }

    // ─────────────────────────────────────────────
    // Paladin Class Feature Queries (static helpers)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Smites per day at given paladin level (PHB p.44).
    /// 1/day at L1, 2/day at L5, 3/day at L10, 4/day at L15, 5/day at L20.
    /// </summary>
    public static int SmitesPerDay(int level)
    {
        if (level <= 0) return 0;
        if (level < 5) return 1;
        if (level < 10) return 2;
        if (level < 15) return 3;
        if (level < 20) return 4;
        return 5;
    }

    /// <summary>Divine Grace at level 2 (PHB p.44): +CHA bonus to all saves.</summary>
    public static bool HasDivineGrace(int level) => level >= 2;

    /// <summary>Aura of Courage at level 3 (PHB p.44): Immune to fear, allies +4 within 10ft.</summary>
    public static bool HasAuraOfCourage(int level) => level >= 3;

    /// <summary>Divine Health at level 3 (PHB p.44): Immune to all diseases.</summary>
    public static bool HasDivineHealth(int level) => level >= 3;

    /// <summary>Turn Undead at level 4 (PHB p.44): Turns undead as cleric 3 levels lower.</summary>
    public static bool HasTurnUndead(int level) => level >= 4;

    /// <summary>Effective cleric level for Turn Undead = paladin level - 3 (PHB p.44).</summary>
    public static int TurnUndeadEffectiveLevel(int paladinLevel)
    {
        if (paladinLevel < 4) return 0;
        return paladinLevel - 3;
    }

    /// <summary>
    /// Remove Disease uses per week (PHB p.44).
    /// 1/week at L6, 2/week at L9, 3/week at L12, 4/week at L15, 5/week at L18.
    /// </summary>
    public static int RemoveDiseasePerWeek(int level)
    {
        if (level < 6) return 0;
        if (level < 9) return 1;
        if (level < 12) return 2;
        if (level < 15) return 3;
        if (level < 18) return 4;
        return 5;
    }

    /// <summary>
    /// Lay on Hands pool = paladin level × CHA modifier (PHB p.44).
    /// Returns 0 if CHA modifier is 0 or negative.
    /// </summary>
    public static int LayOnHandsPool(int level, int chaModifier)
    {
        if (level <= 0 || chaModifier <= 0) return 0;
        return level * chaModifier;
    }
}
