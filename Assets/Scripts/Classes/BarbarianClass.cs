using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Barbarian class definition (D&D 3.5 PHB).
/// Fierce warriors who channel primal rage for devastating combat prowess.
/// </summary>
public class BarbarianClass : ICharacterClass
{
    public string ClassName => "Barbarian";
    public string Description => "A ferocious warrior of great strength and toughness from the wild frontier.";

    // Core Stats
    public int HitDie => 12;
    public int BABAtLevel3 => 3; // Full BAB progression
    public int SkillPointsPerLevel => 4;

    // Save Progressions
    public bool GoodFortitude => true;
    public bool GoodReflex => false;
    public bool GoodWill => false;

    // Class Skills
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Climb",
        "Intimidate",
        "Jump",
        "Listen",
        "Swim"
    };

    // Starting Equipment Defaults
    public int DefaultArmorBonus => 3;
    public int DefaultShieldBonus => 0;
    public int DefaultDamageDice => 12;

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("hide_armor"), EquipSlot.Armor);
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("greataxe"), EquipSlot.RightHand);

        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("javelin"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("javelin"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("javelin"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        Debug.Log("[Barbarian] Equipment: Hide Armor, Greataxe, 3x Javelin");
    }

    // Spellcasting
    public bool IsSpellcaster => false;

    // UI
    public Color TitleColor => new Color(0.9f, 0.4f, 0.3f);
    public Color ButtonColor => new Color(0.5f, 0.15f, 0.1f);
    public string InfoText => "Hit Die: d12 | BAB: +3 (full)\nGood Saves: Fortitude\n\u2022 Rage 1/day (+4 STR/CON)\nEquipment: Hide Armor, Greataxe";

    // Class Features
    // D&D 3.5e PHB p.24-26: Barbarians do NOT receive any automatic feats.
    // Rage, Fast Movement, Uncanny Dodge, etc. are class features, not feats.
    public void InitFeats(CharacterStats stats)
    {
        // No automatic feats for Barbarian — Rage, Fast Movement, etc. are class features.
        Debug.Log($"[Barbarian] {stats.CharacterName}: Barbarian has no automatic feats (Rage, Fast Movement are class features)");
    }

    /// <summary>
    /// Returns a pre-built level 3 Barbarian quick start character.
    /// Grok the Half-Orc Barbarian — focuses on STR, CON, DEX.
    /// </summary>
    public static CharacterCreationData GetQuickStartCharacter()
    {
        RaceDatabase.Init();
        var data = new CharacterCreationData
        {
            CharacterName = "Grok",
            RaceName = "Half-Orc",
            Race = RaceDatabase.GetRace("Half-Orc"),
            ClassName = "Barbarian",
            STR = 17, DEX = 13, CON = 16,
            INT = 8, WIS = 10, CHA = 6,
            SelectedFeats = new List<string> { "Power Attack", "Cleave" },
            BonusFeats = new List<string>(),
            SelectedSpellIds = new List<string>(), // Barbarians don't cast spells
            ChosenAlignment = Alignment.ChaoticNeutral
        };
        data.ComputeFinalStats();
        data.SkillRanks["Intimidate"] = 6;
        data.SkillRanks["Listen"] = 4;
        data.SkillRanks["Climb"] = 4;
        data.SkillRanks["Swim"] = 4;
        return data;
    }
}
