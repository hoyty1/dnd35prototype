using UnityEngine;

/// <summary>
/// Tracks Effective Character Level for creatures with racial HD and/or level adjustment
/// (D&D 3.5e DMG p.172-173, MM p.290).
///
/// ECL = Racial HD + Class Levels + Level Adjustment
///
/// Used for XP awards and encounter balancing when creatures gain class levels.
/// </summary>
[System.Serializable]
public class ECLTracker
{
    /// <summary>Racial (monster) Hit Dice.</summary>
    public int RacialHD;

    /// <summary>Total class levels across all classes.</summary>
    public int ClassLevels;

    /// <summary>Level Adjustment (LA) for the creature's race/template (e.g., Drow = +2).</summary>
    public int LevelAdjustment;

    /// <summary>
    /// Effective Character Level = Racial HD + Class Levels + Level Adjustment.
    /// Determines XP needed for advancement and encounter equivalence.
    /// </summary>
    public int ECL => RacialHD + ClassLevels + LevelAdjustment;

    /// <summary>
    /// Total Hit Dice = Racial HD + Class Levels.
    /// Used for feat progression, ability score increases, etc.
    /// </summary>
    public int TotalHD => RacialHD + ClassLevels;

    public ECLTracker() { }

    public ECLTracker(int racialHD, int classLevels, int levelAdjustment)
    {
        RacialHD = racialHD;
        ClassLevels = classLevels;
        LevelAdjustment = levelAdjustment;
    }

    /// <summary>
    /// XP required for next level based on ECL (DMG p.22).
    /// Uses standard XP table where XP = ECL × (ECL + 1) × 500.
    /// </summary>
    public int XPForNextLevel()
    {
        int ecl = ECL;
        return ecl * (ecl + 1) * 500;
    }

    /// <summary>
    /// Total XP accumulated at current ECL.
    /// </summary>
    public int XPAtCurrentLevel()
    {
        int ecl = ECL;
        if (ecl <= 1) return 0;
        return (ecl - 1) * ecl * 500;
    }

    /// <summary>
    /// Number of feats earned from total HD.
    /// One feat at 1st HD, then every 3 HD after (1, 3, 6, 9, 12...).
    /// </summary>
    public int FeatsFromHD()
    {
        int hd = TotalHD;
        if (hd <= 0) return 0;
        return 1 + (hd - 1) / 3;
    }

    /// <summary>
    /// Number of ability score increases from total HD.
    /// One increase every 4 HD (at 4, 8, 12, 16, 20...).
    /// </summary>
    public int AbilityIncreasesFromHD()
    {
        return TotalHD / 4;
    }

    /// <summary>
    /// Whether the creature qualifies for an XP penalty due to level adjustment.
    /// In the standard rules, LA doesn't directly cause XP penalties but affects advancement cost.
    /// </summary>
    public bool HasLevelAdjustmentPenalty => LevelAdjustment > 0;

    /// <summary>
    /// Get a display string for this ECL tracker.
    /// </summary>
    public string GetSummary()
    {
        string la = LevelAdjustment > 0 ? $" + LA {LevelAdjustment}" : "";
        return $"ECL {ECL} (Racial HD {RacialHD} + Class {ClassLevels}{la})";
    }
}
