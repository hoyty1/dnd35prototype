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

    public static bool TryGetStandardRangeProfile(
        SpellRangeCategory range,
        out int baseSquares,
        out int increasePerLevels,
        out int increaseSquares)
    {
        switch (range)
        {
            case SpellRangeCategory.Personal:
                baseSquares = -1;
                increasePerLevels = 0;
                increaseSquares = 0;
                return true;

            case SpellRangeCategory.Touch:
                baseSquares = 1;
                increasePerLevels = 0;
                increaseSquares = 0;
                return true;

            case SpellRangeCategory.Close:
                baseSquares = 5;
                increasePerLevels = 2;
                increaseSquares = 1;
                return true;

            case SpellRangeCategory.Medium:
                baseSquares = 20;
                increasePerLevels = 1;
                increaseSquares = 2;
                return true;

            case SpellRangeCategory.Long:
                baseSquares = 80;
                increasePerLevels = 1;
                increaseSquares = 8;
                return true;

            case SpellRangeCategory.Unlimited:
                baseSquares = UNLIMITED_RANGE_SQUARES;
                increasePerLevels = 0;
                increaseSquares = 0;
                return true;

            case SpellRangeCategory.Custom:
            default:
                baseSquares = 0;
                increasePerLevels = 0;
                increaseSquares = 0;
                return false;
        }
    }

    public static bool TryDetectCategory(
        int baseSquares,
        int increasePerLevels,
        int increaseSquares,
        out SpellRangeCategory category)
    {
        if (baseSquares == -1 && increasePerLevels <= 0 && increaseSquares <= 0)
        {
            category = SpellRangeCategory.Personal;
            return true;
        }

        if (baseSquares == 1 && increasePerLevels <= 0 && increaseSquares <= 0)
        {
            category = SpellRangeCategory.Touch;
            return true;
        }

        if (baseSquares == 5 && increasePerLevels == 2 && increaseSquares == 1)
        {
            category = SpellRangeCategory.Close;
            return true;
        }

        if (baseSquares == 20 && increasePerLevels == 1 && increaseSquares == 2)
        {
            category = SpellRangeCategory.Medium;
            return true;
        }

        if (baseSquares == 80 && increasePerLevels == 1 && increaseSquares == 8)
        {
            category = SpellRangeCategory.Long;
            return true;
        }

        if (baseSquares == UNLIMITED_RANGE_SQUARES && increasePerLevels <= 0 && increaseSquares <= 0)
        {
            category = SpellRangeCategory.Unlimited;
            return true;
        }

        category = SpellRangeCategory.Custom;
        return false;
    }

    public static void Configure(SpellData spell, SpellRangeCategory range)
    {
        if (spell == null) return;

        if (!TryGetStandardRangeProfile(range, out int baseSquares, out int increasePerLevels, out int increaseSquares))
        {
            return;
        }

        spell.RangeSquares = baseSquares;
        spell.RangeIncreasePerLevels = increasePerLevels;
        spell.RangeIncreaseSquares = increaseSquares;
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
