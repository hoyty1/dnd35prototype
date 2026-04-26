/// <summary>
/// Standardized D&D 3.5e spell range categories.
/// Distances are represented in 5-ft squares.
/// </summary>
public enum SpellRangeCategory
{
    Custom = 0,   // Uses manually configured RangeSquares/RangeIncrease* values
    Personal,     // Self only
    Touch,        // Touch range
    Close,        // 5 sq + 1 sq / 2 levels (25 ft + 5 ft / 2 levels)
    Medium,       // 20 sq + 2 sq / level (100 ft + 10 ft / level)
    Long,         // 80 sq + 8 sq / level (400 ft + 40 ft / level)
    Unlimited     // No practical limit
}

/// <summary>
/// Helper methods and constants for standard D&D 3.5e range formulas.
/// </summary>
public static class SpellRanges
{
    public const string CLOSE_DESC = "5 sq + 1 sq/2 lv";
    public const string MEDIUM_DESC = "20 sq + 2 sq/lv";
    public const string LONG_DESC = "80 sq + 8 sq/lv";

    public const int UNLIMITED_RANGE_SQUARES = 9999;

    public static void Configure(SpellData spell, SpellRangeCategory range)
    {
        if (spell == null) return;

        switch (range)
        {
            case SpellRangeCategory.Personal:
                spell.RangeSquares = -1;
                spell.RangeIncreasePerLevels = 0;
                spell.RangeIncreaseSquares = 0;
                break;

            case SpellRangeCategory.Touch:
                spell.RangeSquares = 1;
                spell.RangeIncreasePerLevels = 0;
                spell.RangeIncreaseSquares = 0;
                break;

            case SpellRangeCategory.Close:
                spell.RangeSquares = 5;
                spell.RangeIncreasePerLevels = 2;
                spell.RangeIncreaseSquares = 1;
                break;

            case SpellRangeCategory.Medium:
                spell.RangeSquares = 20;
                spell.RangeIncreasePerLevels = 1;
                spell.RangeIncreaseSquares = 2;
                break;

            case SpellRangeCategory.Long:
                spell.RangeSquares = 80;
                spell.RangeIncreasePerLevels = 1;
                spell.RangeIncreaseSquares = 8;
                break;

            case SpellRangeCategory.Unlimited:
                spell.RangeSquares = UNLIMITED_RANGE_SQUARES;
                spell.RangeIncreasePerLevels = 0;
                spell.RangeIncreaseSquares = 0;
                break;

            case SpellRangeCategory.Custom:
            default:
                break;
        }
    }

    public static string GetFormulaDescription(SpellRangeCategory range)
    {
        switch (range)
        {
            case SpellRangeCategory.Close:
                return CLOSE_DESC;
            case SpellRangeCategory.Medium:
                return MEDIUM_DESC;
            case SpellRangeCategory.Long:
                return LONG_DESC;
            case SpellRangeCategory.Personal:
                return "Self";
            case SpellRangeCategory.Touch:
                return "Touch";
            case SpellRangeCategory.Unlimited:
                return "Unlimited";
            default:
                return string.Empty;
        }
    }
}
