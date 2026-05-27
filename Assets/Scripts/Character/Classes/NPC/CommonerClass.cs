using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Commoner NPC class (D&D 3.5e DMG p.108-109).
/// Peasants, farmers, and common folk. Weakest NPC class:
/// d4 HD, poor BAB, ALL saves poor, no armor, simple weapons only.
/// </summary>
public class CommonerClass : ICharacterClass
{
    public string ClassName => "Commoner";
    public string Description => "An ordinary person with no special training. Peasants, farmers, and laborers.";

    // Core Stats — weakest class
    public int HitDie => 4;
    public int BABAtLevel3 => 1; // Poor BAB (0.5/level)
    public int SkillPointsPerLevel => 2;

    // Save Progressions — ALL poor
    public bool GoodFortitude => false;
    public bool GoodReflex => false;
    public bool GoodWill => false;

    // Class Skills (DMG p.109) — very limited
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Climb",
        "Jump",
        "Listen",
        "Spot",
        "Swim",
        "Search"
    };

    // Starting Equipment — minimal
    public int DefaultArmorBonus => 0; // No armor proficiency
    public int DefaultShieldBonus => 0;
    public int DefaultDamageDice => 4; // Club or dagger

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CLUB), EquipSlot.RightHand);
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.DAGGER));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.TORCH));
        Debug.Log("[Commoner] Equipment: Club, Dagger, Torch");
    }

    // No spellcasting
    public bool IsSpellcaster => false;

    // UI — dull brown theme
    public Color TitleColor => new Color(0.55f, 0.45f, 0.3f);
    public Color ButtonColor => new Color(0.35f, 0.28f, 0.18f);
    public string InfoText => "Hit Die: d4 | BAB: Poor\nAll Saves: Poor\n• Simple weapons only\n• No armor proficiency\n• NPC class (Peasant)";

    public void InitFeats(CharacterStats stats) { }

    // ─────────────────────────────────────────────
    // Commoner has NO special class features
    // ─────────────────────────────────────────────

    /// <summary>Commoner has no special abilities whatsoever (DMG p.109).</summary>
    public static bool HasSpecialAbilities => false;

    /// <summary>Simple weapon proficiency only (one weapon, not all simple).</summary>
    public static bool HasSimpleWeaponProficiency => true;
}
