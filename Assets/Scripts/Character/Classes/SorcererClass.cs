using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Sorcerer class definition (D&D 3.5 PHB p.51-54).
/// Arcane spellcasters who cast spells through innate talent rather than study.
/// Key difference from Wizard: spontaneous casting (cast any known spell using slots,
/// no preparation needed), fewer spells known but unlimited flexibility.
/// </summary>
public class SorcererClass : ICharacterClass
{
    public string ClassName => "Sorcerer";
    public string Description => "An arcane spellcaster who draws power from innate magical talent rather than bookish study.";

    // Core Stats (PHB p.51)
    public int HitDie => 4;          // d4 hit die, same as Wizard
    public int BABAtLevel3 => 1;     // Poor (1/2) BAB progression
    public int SkillPointsPerLevel => 2;

    // Save Progressions (PHB p.51)
    public bool GoodFortitude => false;
    public bool GoodReflex => false;
    public bool GoodWill => true;     // Only good save is Will

    // Class Skills (PHB p.51)
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Bluff",
        "Concentration",
        "Diplomacy",
        "Knowledge (Arcana)",
        "Spellcraft"
    };

    // Starting Equipment Defaults - no armor proficiency
    public int DefaultArmorBonus => 0;
    public int DefaultShieldBonus => 0;
    public int DefaultDamageDice => 6;   // Quarterstaff 1d6

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
        Debug.Log("[Sorcerer] Equipment: Quarterstaff, Light Crossbow, Dagger, Spell Component Pouch (no armor)");
    }

    // Spellcasting - Sorcerer is an arcane spontaneous caster
    public bool IsSpellcaster => true;

    // UI - Red/gold arcane theme to distinguish from Wizard's purple
    public Color TitleColor => new Color(0.85f, 0.25f, 0.15f);    // Deep red
    public Color ButtonColor => new Color(0.55f, 0.15f, 0.1f);     // Dark crimson
    public string InfoText => "Hit Die: d4 | BAB: +1 (1/2)\nGood Saves: Will\n\u2022 Spontaneous Arcane Caster\n\u2022 Fewer spells known, unlimited flexibility";

    // Class Features
    // D&D 3.5e PHB p.51: Sorcerers receive NO automatic feats.
    // Summon Familiar is a class feature (same as Wizard), not a feat.
    // Sorcerers do NOT get Scribe Scroll or any bonus feat at 1st level.
    public void InitFeats(CharacterStats stats)
    {
        // Sorcerers have no automatic feats - familiar is handled as a class feature
        Debug.Log($"[Sorcerer] {stats.CharacterName}: No automatic feats (familiar is a class feature, not a feat).");
    }

    /// <summary>
    /// Returns a pre-built Quick Start character: Kael the Human Sorcerer.
    /// Spontaneous caster: knows a fixed set of spells, casts any of them using slots.
    /// PHB p.52: Level 1 Sorcerer knows 4 cantrips + 2 first-level spells.
    /// CHA is primary casting stat.
    /// </summary>
    public static CharacterCreationData GetQuickStartCharacter()
    {
        RaceDatabase.Init();
        SpellDatabase.Init();

        var data = new CharacterCreationData
        {
            CharacterName = "Kael",
            RaceName = "Human",
            Race = RaceDatabase.GetRace("Human"),
            ClassName = "Sorcerer",
            STR = 8, DEX = 14, CON = 12,
            INT = 10, WIS = 13, CHA = 17,  // CHA is primary for Sorcerer
            SelectedFeats = new List<string> { "Spell Focus", "Improved Initiative" },
            BonusFeats = new List<string>(),  // Sorcerer gets no bonus feats
            SelectedSpellIds = new List<string>(),
            ChosenAlignment = Alignment.ChaoticGood
        };

        // Sorcerer known spells (PHB p.52, Level 1):
        // 4 cantrips known + 2 first-level spells known
        // These go into SelectedSpellIds which will be loaded as known spells
        // for spontaneous casting (not a spellbook).

        // 4 Cantrips (0-level) - chosen for combat and utility
        data.SelectedSpellIds.AddRange(new List<string>
        {
            SpellNames.RAY_OF_FROST,      // Ranged attack cantrip
            SpellNames.ACID_SPLASH,       // Ranged touch attack
            SpellNames.DETECT_MAGIC,  // Utility
            SpellNames.DAZE              // Crowd control
        });

        // 2 First-level spells known
        data.SelectedSpellIds.AddRange(new List<string>
        {
            SpellNames.MAGIC_MISSILE,     // Reliable damage, auto-hit
            SpellNames.MAGE_ARMOR         // Defensive buff, long duration
        });

        data.ComputeFinalStats();

        // Sorcerers don't prepare spells - they cast spontaneously from known spells.
        // PreparedSpellSlotIds left empty; SpellcastingComponent will use SpontaneousCastingData.
        data.PreparedSpellSlotIds = new List<string>();

        data.SkillRanks["Concentration"] = 4;
        data.SkillRanks["Spellcraft"] = 4;
        data.SkillRanks["Bluff"] = 4;
        return data;
    }
}
