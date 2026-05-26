using System;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Immutable dice expression supporting "NdS+M" notation (e.g., "1d3", "2d4+1")
/// and fixed integer values (e.g., "3"). Parses from string and rolls at runtime.
///
/// Used by the encounter generator to represent variable creature counts like
/// "1d3+1 goblins" where the actual count is determined at encounter generation time.
///
/// Supported formats:
///   "3"      → fixed value 3 (NumDice=0, DiceSides=0, Modifier=3)
///   "1d3"    → roll 1d3 (NumDice=1, DiceSides=3, Modifier=0)
///   "2d4+1"  → roll 2d4+1 (NumDice=2, DiceSides=4, Modifier=1)
///   "1d4+4"  → roll 1d4+4 (NumDice=1, DiceSides=4, Modifier=4)
///
/// Phase 5: Random Encounter Generator.
/// </summary>
[Serializable]
public class DiceExpression
{
    // =========================================================================
    //  Fields (readonly after construction)
    // =========================================================================

    /// <summary>Number of dice to roll. Zero for fixed values.</summary>
    public readonly int NumDice;

    /// <summary>Number of sides per die (e.g., 3, 4, 6, 8, 10, 12, 20).</summary>
    public readonly int DiceSides;

    /// <summary>Flat modifier added after rolling all dice. Can be 0 or negative.</summary>
    public readonly int Modifier;

    /// <summary>Original string this expression was parsed from.</summary>
    public readonly string Original;

    // =========================================================================
    //  Regex pattern — compiled once for performance
    // =========================================================================

    /// <summary>
    /// Matches dice expressions: "NdS", "NdS+M", "NdS-M", or plain integers "N".
    /// Group 1: number of dice (or fixed value)
    /// Group 2: dice sides (optional — absent for fixed values)
    /// Group 3: modifier with sign (optional, e.g., "+1", "-2")
    /// </summary>
    private static readonly Regex DicePattern = new Regex(
        @"^(\d+)(?:d(\d+)([+-]\d+)?)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // =========================================================================
    //  Constructor
    // =========================================================================

    /// <summary>
    /// Create a DiceExpression directly. Prefer <see cref="Parse"/> for string input.
    /// </summary>
    /// <param name="numDice">Number of dice (0 for fixed values).</param>
    /// <param name="diceSides">Sides per die (0 for fixed values).</param>
    /// <param name="modifier">Flat modifier added after rolling.</param>
    /// <param name="original">Original string representation.</param>
    public DiceExpression(int numDice, int diceSides, int modifier, string original = null)
    {
        NumDice = Math.Max(0, numDice);
        DiceSides = Math.Max(0, diceSides);
        Modifier = modifier;
        Original = original ?? ToString();
    }

    // =========================================================================
    //  Properties
    // =========================================================================

    /// <summary>Whether this is a fixed value (no dice roll needed).</summary>
    public bool IsFixed => NumDice == 0 || DiceSides == 0;

    /// <summary>The minimum possible result of this expression.</summary>
    public int Minimum => IsFixed ? Modifier : NumDice + Modifier;

    /// <summary>The maximum possible result of this expression.</summary>
    public int Maximum => IsFixed ? Modifier : (NumDice * DiceSides) + Modifier;

    /// <summary>The average (expected) result as a float.</summary>
    public float Average => IsFixed ? Modifier : NumDice * ((DiceSides + 1f) / 2f) + Modifier;

    // =========================================================================
    //  Parsing
    // =========================================================================

    /// <summary>
    /// Parse a string like "1d3+1", "2d4", or "5" into a DiceExpression.
    /// Returns null if the string cannot be parsed.
    /// </summary>
    /// <param name="input">Dice notation string to parse.</param>
    /// <returns>A DiceExpression, or null if input is invalid.</returns>
    public static DiceExpression Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();

        Match match = DicePattern.Match(input);
        if (!match.Success) return null;

        int firstNum = int.Parse(match.Groups[1].Value);

        // If group 2 (dice sides) is absent, this is a fixed integer
        if (!match.Groups[2].Success)
        {
            return new DiceExpression(0, 0, firstNum, input);
        }

        // Full dice expression: NdS or NdS+M
        int sides = int.Parse(match.Groups[2].Value);
        int mod = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

        if (sides <= 0)
        {
            Debug.LogWarning($"[DiceExpression] Invalid dice sides ({sides}) in '{input}', treating as fixed value.");
            return new DiceExpression(0, 0, firstNum + mod, input);
        }

        return new DiceExpression(firstNum, sides, mod, input);
    }

    /// <summary>
    /// Try to parse a dice expression. Returns true if successful.
    /// </summary>
    /// <param name="input">Dice notation string.</param>
    /// <param name="result">Parsed expression, or null on failure.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryParse(string input, out DiceExpression result)
    {
        result = Parse(input);
        return result != null;
    }

    // =========================================================================
    //  Rolling
    // =========================================================================

    /// <summary>
    /// Roll the dice and return the result. Fixed values return Modifier directly.
    /// Result is floored at 1 (at least one creature is always spawned).
    /// </summary>
    /// <returns>The rolled result, minimum 1.</returns>
    public int Roll()
    {
        if (IsFixed)
            return Math.Max(1, Modifier);

        int total = Modifier;
        for (int i = 0; i < NumDice; i++)
        {
            total += UnityEngine.Random.Range(1, DiceSides + 1);
        }
        return Math.Max(1, total);
    }

    // =========================================================================
    //  Display
    // =========================================================================

    /// <summary>
    /// Returns the canonical string representation (e.g., "2d4+1", "1d3", "3").
    /// </summary>
    public override string ToString()
    {
        if (IsFixed)
            return Modifier.ToString();

        string result = $"{NumDice}d{DiceSides}";
        if (Modifier > 0) result += $"+{Modifier}";
        else if (Modifier < 0) result += Modifier.ToString(); // negative sign included
        return result;
    }

    /// <summary>
    /// Returns a display string showing the expression and its range.
    /// E.g., "2d4+1 [3-9]".
    /// </summary>
    public string ToRangeString()
    {
        if (IsFixed)
            return Modifier.ToString();
        return $"{ToString()} [{Minimum}-{Maximum}]";
    }

    // =========================================================================
    //  Equality / Hashing
    // =========================================================================

    public override bool Equals(object obj)
    {
        if (obj is DiceExpression other)
            return NumDice == other.NumDice && DiceSides == other.DiceSides && Modifier == other.Modifier;
        return false;
    }

    public override int GetHashCode()
    {
        return (NumDice * 397) ^ (DiceSides * 31) ^ Modifier;
    }
}
