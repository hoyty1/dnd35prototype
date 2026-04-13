using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monk class definition (D&D 3.5 PHB).
/// Martial artists who combine speed, unarmed combat, and spiritual discipline.
/// </summary>
public class MonkClass : ICharacterClass
{
    public string ClassName => "Monk";
    public string Description => "A martial artist who hones body and mind to physical and spiritual perfection.";

    // Core Stats
    public int HitDie => 8;
    public int BABAtLevel3 => 2; // 3/4 BAB progression
    public int SkillPointsPerLevel => 4;

    // Save Progressions
    public bool GoodFortitude => true;
    public bool GoodReflex => true;
    public bool GoodWill => true;

    // Class Skills
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Balance",
        "Climb",
        "Diplomacy",
        "Hide",
        "Jump",
        "Listen",
        "Move Silently",
        "Spot",
        "Swim",
        "Tumble"
    };

    // Starting Equipment Defaults
    public int DefaultArmorBonus => 0;
    public int DefaultShieldBonus => 0;
    public int DefaultDamageDice => 6;

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);

        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("sling"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
        Debug.Log("[Monk] Equipment: Quarterstaff, Sling (unarmored for WIS AC bonus)");
    }

    // Spellcasting
    public bool IsSpellcaster => false;

    // UI
    public Color TitleColor => new Color(0.4f, 0.7f, 0.9f);
    public Color ButtonColor => new Color(0.15f, 0.35f, 0.5f);
    public string InfoText => "Hit Die: d8 | BAB: +2 (3/4)\nGood Saves: Fort, Ref, Will\n\u2022 Flurry of Blows, +WIS to AC\nEquipment: Quarterstaff, Sling";

    // Class Features — D&D 3.5e PHB p.40, Table 3-10
    // AUTOMATIC feat at 1st level:
    //   - Improved Unarmed Strike (PHB p.40) — the ONLY automatic feat
    // BONUS FEAT CHOICES (handled by character creation UI, NOT here):
    //   - 1st level: Choose Improved Grapple OR Stunning Fist
    //   - 2nd level: Choose Combat Reflexes OR Deflect Arrows
    //   - 6th level: Choose Improved Disarm OR Improved Trip
    // Stunning Fist is NOT automatic — it is one of the two choices at 1st level.
    public void InitFeats(CharacterStats stats)
    {
        // Only automatic feat per PHB p.40: Improved Unarmed Strike
        stats.Feats.Add("Improved Unarmed Strike");
        // Note: Stunning Fist is a BONUS FEAT CHOICE at level 1 (not automatic).
        // Bonus feat selections are handled by CharacterCreationUI monk bonus feat system.
        Debug.Log($"[Monk] {stats.CharacterName}: Granted automatic monk feat: Improved Unarmed Strike");
    }

    /// <summary>
    /// Returns a pre-built level 3 Monk quick start character.
    /// Kira the Human Monk — focuses on DEX, WIS, STR.
    /// </summary>
    public static CharacterCreationData GetQuickStartCharacter()
    {
        RaceDatabase.Init();
        var data = new CharacterCreationData
        {
            CharacterName = "Kira",
            RaceName = "Human",
            Race = RaceDatabase.GetRace("Human"),
            ClassName = "Monk",
            STR = 14, DEX = 16, CON = 12,
            INT = 10, WIS = 15, CHA = 8,
            SelectedFeats = new List<string> { "Dodge", "Weapon Finesse" },
            BonusFeats = new List<string> { "Stunning Fist" }, // Monk 1st-level bonus feat choice
            SelectedSpellIds = new List<string>(), // Monks don't cast spells
            ChosenAlignment = Alignment.LawfulNeutral
        };
        data.ComputeFinalStats();
        data.SkillRanks["Tumble"] = 6;
        data.SkillRanks["Balance"] = 4;
        data.SkillRanks["Jump"] = 4;
        data.SkillRanks["Listen"] = 4;
        data.SkillRanks["Spot"] = 4;
        return data;
    }
}
