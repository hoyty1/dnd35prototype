using System.Text;
using UnityEngine;

// ============================================================================
// DiceRoller — Centralized dice rolling utility for D&D 3.5e prototype.
//
// Replaces scattered UnityEngine.Random.Range(1, N+1) calls with named methods.
// Benefits:
//   - Readable code: DiceRoller.D20() vs Random.Range(1, 21)
//   - Single point for RNG seeding / deterministic replay
//   - Optional combat log integration via StringBuilder overloads
//   - Easy to swap to a different RNG source for testing
//
// Usage:
//   int roll = DiceRoller.D20();
//   int damage = DiceRoller.Roll(3, 6);          // 3d6
//   int damage = DiceRoller.Roll(3, 6, sb);       // 3d6, appends "[4+2+6]=12" to sb
//   bool success = DiceRoller.Check(roll + mod, dc);
// ============================================================================

/// <summary>
/// Centralized dice rolling for D&D 3.5e. All dice rolls should go through this utility
/// to enable future features like deterministic replay, roll logging, and RNG seeding.
/// </summary>
public static class DiceRoller
{
    // ════════════════════════════════════════════════════════════
    //  Standard Die Rolls
    // ════════════════════════════════════════════════════════════

    /// <summary>Roll 1d4 (1-4).</summary>
    public static int D4() => Random.Range(1, 5);

    /// <summary>Roll 1d6 (1-6).</summary>
    public static int D6() => Random.Range(1, 7);

    /// <summary>Roll 1d8 (1-8).</summary>
    public static int D8() => Random.Range(1, 9);

    /// <summary>Roll 1d10 (1-10).</summary>
    public static int D10() => Random.Range(1, 11);

    /// <summary>Roll 1d12 (1-12).</summary>
    public static int D12() => Random.Range(1, 13);

    /// <summary>Roll 1d20 (1-20). The most common roll in D&D — attack rolls, saves, ability checks.</summary>
    public static int D20() => Random.Range(1, 21);

    /// <summary>Roll 1d100 (1-100). Percentile roll.</summary>
    public static int D100() => Random.Range(1, 101);

    // ════════════════════════════════════════════════════════════
    //  Multi-Dice Rolls
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Roll multiple dice of the same type (e.g., 3d6, 2d8).
    /// </summary>
    /// <param name="count">Number of dice to roll.</param>
    /// <param name="sides">Number of sides per die (4, 6, 8, 10, 12, 20).</param>
    /// <returns>Sum of all dice rolled.</returns>
    public static int Roll(int count, int sides)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
            total += Random.Range(1, sides + 1);
        return total;
    }

    /// <summary>
    /// Roll multiple dice and append individual results to a StringBuilder for combat logging.
    /// Appends format: "[4+2+6]=12" for 3d6 rolling 4, 2, 6.
    /// </summary>
    /// <param name="count">Number of dice to roll.</param>
    /// <param name="sides">Number of sides per die.</param>
    /// <param name="log">StringBuilder to append roll details to. Can be null (no logging).</param>
    /// <returns>Sum of all dice rolled.</returns>
    public static int Roll(int count, int sides, StringBuilder log)
    {
        if (count <= 0) return 0;

        int total = 0;
        if (log != null) log.Append('[');
        for (int i = 0; i < count; i++)
        {
            int roll = Random.Range(1, sides + 1);
            total += roll;
            if (log != null)
            {
                if (i > 0) log.Append('+');
                log.Append(roll);
            }
        }
        if (log != null)
        {
            log.Append("]=");
            log.Append(total);
        }
        return total;
    }

    // ════════════════════════════════════════════════════════════
    //  Utility Methods
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if a total meets or exceeds a DC. Standard D&D 3.5e success check.
    /// </summary>
    public static bool MeetsDC(int total, int dc) => total >= dc;

    /// <summary>
    /// Roll 1d20 + modifier and check against a DC. Returns the roll, total, and success.
    /// </summary>
    /// <param name="modifier">Bonus added to the d20 roll.</param>
    /// <param name="dc">Difficulty class to meet or exceed.</param>
    /// <param name="roll">Output: the raw d20 result.</param>
    /// <param name="total">Output: roll + modifier.</param>
    /// <returns>True if total >= dc.</returns>
    public static bool D20Check(int modifier, int dc, out int roll, out int total)
    {
        roll = D20();
        total = roll + modifier;
        return total >= dc;
    }

    /// <summary>
    /// Calculate the average result of rolling count dice with given sides.
    /// Useful for HP calculations: average(d8) = 4.5.
    /// </summary>
    public static float Average(int count, int sides) => count * (sides + 1) / 2f;
}
