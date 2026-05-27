using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class LevelUpData
{
    public int OldLevel;
    public int NewLevel;
    public CharacterController Character;

    // Multiclass selection
    public string SelectedClassName;
    public List<string> AvailableClasses = new List<string>();

    // HP increase already rolled
    public int HPGained;

    // BAB and saves
    public int OldBAB;
    public int NewBAB;
    public int OldFortSave;
    public int NewFortSave;
    public int OldRefSave;
    public int NewRefSave;
    public int OldWillSave;
    public int NewWillSave;

    // Level-up choices
    public bool NeedsAbilityIncrease; // Every 4 total levels
    public bool NeedsFeat; // Any feat selection required this level-up
    public int GeneralFeatsToSelect; // Total-level progression feats (1,3,6,...)
    public int FighterBonusFeatsToSelect; // Fighter class-level progression feats
    public int WizardBonusFeatsToSelect; // Wizard class-level progression feats (5,10,15,20)
    public int MonkBonusFeatLevelToSelect; // Monk class-level bonus feat milestone (1,2,6) or 0
    public int TotalFeatsToSelect => GeneralFeatsToSelect + FighterBonusFeatsToSelect + WizardBonusFeatsToSelect + (MonkBonusFeatLevelToSelect > 0 ? 1 : 0);
    public int SkillPointsNew;
    public int SkillPointsFromClassPool;
    public int SkillPointsToAllocate;
    public bool NeedsSpellSelection; // For spellcasters
    public bool XPPenaltyActive;
    public string FavoredClass;

    // Choices made
    public string SelectedAbility = ""; // STR, DEX, CON, INT, WIS, CHA
    public FeatData SelectedFeat;
    public Dictionary<string, int> SkillPointsAllocated = new Dictionary<string, int>();
    public List<SpellData> SelectedSpells = new List<SpellData>();

    public bool IsComplete()
    {
        if (NeedsAbilityIncrease && string.IsNullOrEmpty(SelectedAbility))
            return false;

        if (NeedsFeat && SelectedFeat == null)
            return false;

        if (SkillPointsToAllocate > 0 && SkillPointsAllocated.Values.Sum() < SkillPointsToAllocate)
            return false;

        // Spell selection is optional (can skip)
        return true;
    }
}

[System.Serializable]
public class FeatData
{
    public string FeatName;
    public string Description;
    public string Prerequisites;
    public string Benefit;

    public FeatData(string name, string desc, string prereq, string benefit)
    {
        FeatName = name;
        Description = desc;
        Prerequisites = prereq;
        Benefit = benefit;
    }
}
