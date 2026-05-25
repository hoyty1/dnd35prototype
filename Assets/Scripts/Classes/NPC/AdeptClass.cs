using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adept NPC class (D&D 3.5e DMG p.107-108).
/// Village wise folk and hedge wizards. Divine spellcaster (WIS-based, 0-5th level),
/// familiar at 1st level, d6 HD, poor BAB, Will good save.
/// </summary>
public class AdeptClass : ICharacterClass
{
    public string ClassName => "Adept";
    public string Description => "A minor divine spellcaster found in primitive villages and remote communities. Casts spells through innate wisdom.";

    // Core Stats
    public int HitDie => 6;
    public int BABAtLevel3 => 1; // Poor BAB (0.5/level → 1 at L3 via floor(3*1/2)=1)
    public int SkillPointsPerLevel => 2;

    // Save Progressions
    public bool GoodFortitude => false;
    public bool GoodReflex => false;
    public bool GoodWill => true;

    // Class Skills (DMG p.107)
    public HashSet<string> ClassSkills { get; } = new HashSet<string>
    {
        "Knowledge (Arcana)",
        "Knowledge (Religion)",
        "Knowledge (Nature)",
        "Knowledge (History)",
        "Survival",
        "Listen",
        "Spot",
        "Search"
    };

    // Starting Equipment
    public int DefaultArmorBonus => 0; // No armor proficiency
    public int DefaultShieldBonus => 0;
    public int DefaultDamageDice => 6; // Quarterstaff

    public void SetupStartingEquipment(InventoryComponent inv)
    {
        ItemDatabase.Init();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.DAGGER));
        inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemIDs.POTION_HEALING));
        Debug.Log("[Adept] Equipment: Quarterstaff, Dagger, Healing Potion");
    }

    // Spellcasting — prepared divine (WIS-based, 0-5th level)
    public bool IsSpellcaster => true;

    // UI — mystic purple/grey theme
    public Color TitleColor => new Color(0.5f, 0.35f, 0.6f);
    public Color ButtonColor => new Color(0.3f, 0.2f, 0.4f);
    public string InfoText => "Hit Die: d6 | BAB: Poor\nGood Save: Will\n• Divine spells (WIS, 0-5th)\n• Familiar at 1st level\n• NPC class";

    public void InitFeats(CharacterStats stats) { }

    // ─────────────────────────────────────────────
    // Adept Spell Progression (DMG p.107)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Adept spells per day. Indexed: [level-1][spellLevel].
    /// -1 means not accessible at that level.
    /// </summary>
    private static readonly int[,] SpellsPerDay = new int[20, 6]
    {
        // 0th  1st  2nd  3rd  4th  5th
        {  3,   1,  -1,  -1,  -1,  -1 }, // L1
        {  3,   1,  -1,  -1,  -1,  -1 }, // L2
        {  3,   2,  -1,  -1,  -1,  -1 }, // L3
        {  3,   2,   0,  -1,  -1,  -1 }, // L4
        {  3,   2,   1,  -1,  -1,  -1 }, // L5
        {  3,   2,   1,  -1,  -1,  -1 }, // L6
        {  3,   3,   2,  -1,  -1,  -1 }, // L7
        {  3,   3,   2,   0,  -1,  -1 }, // L8
        {  3,   3,   2,   1,  -1,  -1 }, // L9
        {  3,   3,   2,   1,  -1,  -1 }, // L10
        {  3,   3,   3,   2,  -1,  -1 }, // L11
        {  3,   3,   3,   2,   0,  -1 }, // L12
        {  3,   3,   3,   2,   1,  -1 }, // L13
        {  3,   3,   3,   2,   1,  -1 }, // L14
        {  3,   3,   3,   3,   2,  -1 }, // L15
        {  3,   3,   3,   3,   2,   0 }, // L16
        {  3,   3,   3,   3,   2,   1 }, // L17
        {  3,   3,   3,   3,   2,   1 }, // L18
        {  3,   3,   3,   3,   3,   2 }, // L19
        {  3,   3,   3,   3,   3,   2 }, // L20
    };

    /// <summary>Get spells per day for a given adept level and spell level.</summary>
    public static int GetSpellsPerDay(int adeptLevel, int spellLevel)
    {
        if (adeptLevel < 1 || adeptLevel > 20 || spellLevel < 0 || spellLevel > 5) return 0;
        int val = SpellsPerDay[adeptLevel - 1, spellLevel];
        return val < 0 ? 0 : val;
    }

    /// <summary>Can the adept access spells of this level?</summary>
    public static bool CanCastSpellLevel(int adeptLevel, int spellLevel)
    {
        if (adeptLevel < 1 || adeptLevel > 20 || spellLevel < 0 || spellLevel > 5) return false;
        return SpellsPerDay[adeptLevel - 1, spellLevel] >= 0;
    }

    /// <summary>Maximum spell level accessible at given adept level.</summary>
    public static int MaxSpellLevel(int adeptLevel)
    {
        for (int sl = 5; sl >= 0; sl--)
        {
            if (CanCastSpellLevel(adeptLevel, sl)) return sl;
        }
        return 0;
    }

    /// <summary>Adept gains a familiar at level 1 (DMG p.107).</summary>
    public static bool HasFamiliar(int adeptLevel) => adeptLevel >= 1;

    /// <summary>Whether the adept has Summon Familiar at this level (same as L2+ per DMG).</summary>
    public static bool HasSummonFamiliar(int adeptLevel) => adeptLevel >= 2;
}
