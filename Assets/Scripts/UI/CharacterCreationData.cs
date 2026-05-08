using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores all data for a character being created through the character creation UI.
/// Used as a data transfer object between CharacterCreationUI and GameManager.
/// </summary>
[System.Serializable]
public class CharacterCreationData
{
    // Step 1: Rolled stats (6 values from 4d6 drop lowest)
    public int[] RolledStats = new int[6];

    // Step 2: Assigned stats
    public int STR, DEX, CON, INT, WIS, CHA;

    // Step 3: Race
    public string RaceName;
    public RaceData Race;

    // Step 4: Class
    public string ClassName; // "Fighter" or "Rogue"

    // Character level state for creation flow
    public int CharacterLevel = 3;
    public int TargetLevel = 3;

    // Step 4b: Alignment
    public Alignment ChosenAlignment = Alignment.None;

    // Step 4c: Deity (all classes, especially important for clerics)
    /// <summary>The deity ID chosen during character creation (e.g., "pelor").</summary>
    public string ChosenDeityId = "";

    // Step 4d: Domains (clerics only — choose 2 from deity's domain list)
    /// <summary>Domain names selected for a cleric (e.g., "Healing", "Good"). Max 2.</summary>
    public List<string> ChosenDomains = new List<string>();

    // Step 4e: Spontaneous Casting (clerics only)
    /// <summary>
    /// D&D 3.5e Spontaneous Casting type for clerics.
    /// Good clerics automatically get Cure, Evil clerics get Inflict.
    /// Neutral clerics (on Good/Evil axis) must choose during character creation.
    /// </summary>
    public SpontaneousCastingType SpontaneousCasting = SpontaneousCastingType.None;

    // Step 4f: Wizard specialization (wizard level 1 only)
    public WizardSpecialization WizardSpecialization = WizardSpecialization.CreateGeneralist();

    // Step 4g: Wizard familiar (wizard level 1 only)
    public WizardFamiliar WizardFamiliar = WizardFamiliar.CreateNone();

    // Step 5: Skills
    public Dictionary<string, int> SkillRanks = new Dictionary<string, int>();
    /// <summary>Unspent skill points remaining after character creation allocation.
    /// These carry over into the class skill point pool for the first level-up.</summary>
    public int UnspentSkillPoints;

    // Step 5b: Feats
    public List<string> SelectedFeats = new List<string>();
    public List<string> BonusFeats = new List<string>(); // Fighter bonus feats
    public string WeaponFocusChoice = "";
    public string SkillFocusChoice = "";

    // Step 5c: Spells (Wizard spellbook selection)
    /// <summary>SpellIds selected for Wizard spellbook during character creation.
    /// Clerics don't select — they have access to all Cleric spells.</summary>
    public List<string> SelectedSpellIds = new List<string>();

    // Step 5d: Spell Preparation (Wizard spell slot assignments)
    /// <summary>SpellIds prepared in each spell slot (in slot order: cantrip slots, then 1st, then 2nd).
    /// Empty string means the slot is empty. Used to initialize SpellcastingComponent slots.</summary>
    public List<string> PreparedSpellSlotIds = new List<string>();

    // Step 6: Name
    public string CharacterName = "";

    // Derived values (computed during review)
    public int FinalSTR, FinalDEX, FinalCON, FinalINT, FinalWIS, FinalCHA;
    public int HP;
    public int AC;
    public int AttackBonus;
    public int BAB;
    public int HitDie;
    public int BaseSpeed;

    /// <summary>Apply racial modifiers to compute final ability scores.</summary>
    public void ComputeFinalStats()
    {
        FinalSTR = STR + (Race != null ? Race.STRModifier : 0);
        FinalDEX = DEX + (Race != null ? Race.DEXModifier : 0);
        FinalCON = CON + (Race != null ? Race.CONModifier : 0);
        FinalINT = INT + (Race != null ? Race.INTModifier : 0);
        FinalWIS = WIS + (Race != null ? Race.WISModifier : 0);
        FinalCHA = CHA + (Race != null ? Race.CHAModifier : 0);

        int safeLevel = Mathf.Max(1, CharacterLevel);

        // Look up class definition from registry
        ClassRegistry.Init();
        ICharacterClass classDef = ClassRegistry.GetClass(ClassName);
        HitDie = classDef != null ? classDef.HitDie : 6;
        BAB = CalculateClassBab(ClassName, safeLevel);

        // HP: Max at level 1, average thereafter
        int conMod = CharacterStats.GetModifier(FinalCON);
        int baseHP = HitDie + Mathf.Max(0, safeLevel - 1) * (HitDie / 2 + 1);
        HP = baseHP + conMod * safeLevel;
        if (HP < 1) HP = 1;

        // Speed from race
        BaseSpeed = Race != null ? Race.BaseSpeedSquares : 6;
    }

    private static int CalculateClassBab(string className, int level)
    {
        int safeLevel = Mathf.Max(1, level);
        switch (className)
        {
            case "Fighter":
            case "Barbarian":
            case "Paladin":
            case "Ranger":
                return safeLevel;
            case "Cleric":
            case "Druid":
            case "Monk":
            case "Rogue":
                return (safeLevel * 3) / 4;
            case "Wizard":
            case "Sorcerer":
            case "Bard":
                return safeLevel / 2;
            default:
                return safeLevel;
        }
    }

    /// <summary>Get a formatted stat line with racial mods shown.</summary>
    public string GetFinalStatString(string label, int baseVal, int raceMod)
    {
        int final_ = baseVal + raceMod;
        int mod = CharacterStats.GetModifier(final_);
        string modStr = mod >= 0 ? $"+{mod}" : $"{mod}";
        string raceStr = raceMod != 0 ? $" ({(raceMod > 0 ? "+" : "")}{raceMod} racial)" : "";
        return $"{label}: {final_} ({modStr}){raceStr}";
    }
}
