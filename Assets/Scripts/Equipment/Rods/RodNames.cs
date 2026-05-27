/// <summary>
/// Centralized string constants for all D&D 3.5e rod IDs (DMG pp. 224–228).
/// Used as keys in RodDatabase and ItemData.RodId.
/// 36 total rods: 21 metamagic + 7 combat + 5 utility + 3 legendary.
/// </summary>
public static class RodNames
{
    // ════════════════════════════════════════════════════════════
    //  METAMAGIC RODS — 7 types × 3 power levels = 21 rods
    //  Apply metamagic WITHOUT increasing spell slot level.
    //  All are use-activated, 3/day, CL 17th, 5 lbs.
    // ════════════════════════════════════════════════════════════

    // Rod of Empower Spell (slot +2 equivalent)
    public const string ROD_EMPOWER_LESSER   = "rod_empower_lesser";    // 9,000 gp — up to 3rd level
    public const string ROD_EMPOWER_NORMAL   = "rod_empower_normal";    // 32,500 gp — up to 6th level
    public const string ROD_EMPOWER_GREATER  = "rod_empower_greater";   // 73,000 gp — up to 9th level

    // Rod of Enlarge Spell (slot +1 equivalent)
    public const string ROD_ENLARGE_LESSER   = "rod_enlarge_lesser";    // 3,000 gp — up to 3rd level
    public const string ROD_ENLARGE_NORMAL   = "rod_enlarge_normal";    // 11,000 gp — up to 6th level
    public const string ROD_ENLARGE_GREATER  = "rod_enlarge_greater";   // 24,500 gp — up to 9th level

    // Rod of Extend Spell (slot +1 equivalent)
    public const string ROD_EXTEND_LESSER    = "rod_extend_lesser";     // 3,000 gp — up to 3rd level
    public const string ROD_EXTEND_NORMAL    = "rod_extend_normal";     // 11,000 gp — up to 6th level
    public const string ROD_EXTEND_GREATER   = "rod_extend_greater";    // 24,500 gp — up to 9th level

    // Rod of Maximize Spell (slot +3 equivalent)
    public const string ROD_MAXIMIZE_LESSER  = "rod_maximize_lesser";   // 14,000 gp — up to 3rd level
    public const string ROD_MAXIMIZE_NORMAL  = "rod_maximize_normal";   // 54,000 gp — up to 6th level
    public const string ROD_MAXIMIZE_GREATER = "rod_maximize_greater";  // 121,500 gp — up to 9th level

    // Rod of Quicken Spell (slot +4 equivalent)
    public const string ROD_QUICKEN_LESSER   = "rod_quicken_lesser";    // 35,000 gp — up to 3rd level
    public const string ROD_QUICKEN_NORMAL   = "rod_quicken_normal";    // 75,500 gp — up to 6th level
    public const string ROD_QUICKEN_GREATER  = "rod_quicken_greater";   // 170,000 gp — up to 9th level

    // Rod of Silent Spell (slot +1 equivalent)
    public const string ROD_SILENT_LESSER    = "rod_silent_lesser";     // 3,000 gp — up to 3rd level
    public const string ROD_SILENT_NORMAL    = "rod_silent_normal";     // 11,000 gp — up to 6th level
    public const string ROD_SILENT_GREATER   = "rod_silent_greater";    // 24,500 gp — up to 9th level

    // Rod of Widen Spell (slot +3 equivalent)
    public const string ROD_WIDEN_LESSER     = "rod_widen_lesser";      // 14,000 gp — up to 3rd level
    public const string ROD_WIDEN_NORMAL     = "rod_widen_normal";      // 54,000 gp — up to 6th level
    public const string ROD_WIDEN_GREATER    = "rod_widen_greater";     // 121,500 gp — up to 9th level

    // ════════════════════════════════════════════════════════════
    //  COMBAT RODS — 7 rods
    // ════════════════════════════════════════════════════════════

    public const string ROD_ABSORPTION                    = "rod_absorption";                     // 50,000 gp
    public const string ROD_CANCELLATION                  = "rod_cancellation";                   // 11,000 gp
    public const string ROD_FLAILING                      = "rod_flailing";                       // 50,000 gp
    public const string ROD_IMMOVABLE                     = "rod_immovable";                      // 5,000 gp
    public const string ROD_LORDLY_MIGHT                  = "rod_lordly_might";                   // 70,000 gp
    public const string ROD_METAL_AND_MINERAL_DETECTION   = "rod_metal_and_mineral_detection";    // 10,500 gp
    public const string ROD_SPLENDOR                      = "rod_splendor";                       // 25,000 gp

    // ════════════════════════════════════════════════════════════
    //  UTILITY RODS — 5 rods
    // ════════════════════════════════════════════════════════════

    public const string ROD_ALERTNESS        = "rod_alertness";          // 85,000 gp
    public const string ROD_ENEMY_DETECTION  = "rod_enemy_detection";    // 23,500 gp
    public const string ROD_NEGATION         = "rod_negation";           // 37,000 gp
    public const string ROD_PYTHON           = "rod_python";             // 13,000 gp
    public const string ROD_SECURITY         = "rod_security";           // 61,000 gp

    // ════════════════════════════════════════════════════════════
    //  Helper — All rod IDs for enumeration
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns all 36 rod IDs for database registration and enumeration.
    /// </summary>
    public static string[] GetAllRodIds()
    {
        return new string[]
        {
            // Metamagic (21)
            ROD_EMPOWER_LESSER, ROD_EMPOWER_NORMAL, ROD_EMPOWER_GREATER,
            ROD_ENLARGE_LESSER, ROD_ENLARGE_NORMAL, ROD_ENLARGE_GREATER,
            ROD_EXTEND_LESSER, ROD_EXTEND_NORMAL, ROD_EXTEND_GREATER,
            ROD_MAXIMIZE_LESSER, ROD_MAXIMIZE_NORMAL, ROD_MAXIMIZE_GREATER,
            ROD_QUICKEN_LESSER, ROD_QUICKEN_NORMAL, ROD_QUICKEN_GREATER,
            ROD_SILENT_LESSER, ROD_SILENT_NORMAL, ROD_SILENT_GREATER,
            ROD_WIDEN_LESSER, ROD_WIDEN_NORMAL, ROD_WIDEN_GREATER,
            // Combat (7)
            ROD_ABSORPTION, ROD_CANCELLATION, ROD_FLAILING,
            ROD_IMMOVABLE, ROD_LORDLY_MIGHT,
            ROD_METAL_AND_MINERAL_DETECTION, ROD_SPLENDOR,
            // Utility (5)
            ROD_ALERTNESS, ROD_ENEMY_DETECTION, ROD_NEGATION,
            ROD_PYTHON, ROD_SECURITY
        };
    }
}
