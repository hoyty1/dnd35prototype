using System.Collections.Generic;

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

    // Step 5: Skills
    public Dictionary<string, int> SkillRanks = new Dictionary<string, int>();

    // Step 5b: Feats
    public List<string> SelectedFeats = new List<string>();
    public List<string> BonusFeats = new List<string>(); // Fighter bonus feats
    public string WeaponFocusChoice = "";
    public string SkillFocusChoice = "";

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

        // Class-specific
        if (ClassName == "Fighter")
        {
            HitDie = 10;
            BAB = 3; // Level 3 fighter BAB (full progression)
        }
        else if (ClassName == "Rogue")
        {
            HitDie = 6;
            BAB = 2; // Level 3 rogue BAB (3/4 progression)
        }
        else if (ClassName == "Monk")
        {
            HitDie = 8;
            BAB = 2; // Level 3 monk BAB (3/4 progression)
        }
        else if (ClassName == "Barbarian")
        {
            HitDie = 12;
            BAB = 3; // Level 3 barbarian BAB (full progression)
        }
        else if (ClassName == "Wizard")
        {
            HitDie = 4;
            BAB = 1; // Level 3 wizard BAB (1/2 progression)
        }
        else if (ClassName == "Cleric")
        {
            HitDie = 8;
            BAB = 2; // Level 3 cleric BAB (3/4 progression)
        }
        else
        {
            HitDie = 6;
            BAB = 2; // Default fallback
        }

        // HP: Roll max at level 1, average for 2-3 (simplified: use fixed values)
        // Level 3: HitDie + 2*(HitDie/2+1) + CON_mod * 3
        int conMod = CharacterStats.GetModifier(FinalCON);
        int baseHP = HitDie + (HitDie / 2 + 1) * 2; // max at 1, avg+1 at 2-3
        HP = baseHP + conMod * 3;
        if (HP < 1) HP = 1;

        // Speed from race
        BaseSpeed = Race != null ? Race.BaseSpeedSquares : 6;
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
