using UnityEngine;

// ============================================================================
// D&D 3.5 Dice Service - Centralized dice rolling with optional logging
// ============================================================================

/// <summary>
/// Centralized dice-rolling service for all D&amp;D 3.5e random rolls.
/// Replaces scattered Random.Range calls with semantically meaningful methods
/// and provides optional debug logging for roll auditing.
/// </summary>
public static class DiceService
{
    /// <summary>Whether to log every dice roll to the Unity console (debug builds).</summary>
    public static bool EnableLogging = false;

    // ========================================================================
    // CORE ROLL METHOD
    // ========================================================================

    /// <summary>
    /// Roll a single die with the given range [min, max] (inclusive on both ends).
    /// This is the foundation method all other helpers delegate to.
    /// </summary>
    /// <param name="min">Minimum value (inclusive).</param>
    /// <param name="max">Maximum value (inclusive).</param>
    /// <param name="context">Optional description for debug logging (e.g. "Attack roll", "Fortitude save").</param>
    /// <returns>Random integer in [min, max].</returns>
    public static int Roll(int min, int max, string context = null)
    {
        // Random.Range(int, int) is exclusive on the upper bound
        int result = Random.Range(min, max + 1);

        if (EnableLogging && !string.IsNullOrEmpty(context))
        {
            Debug.Log($"[Dice] {context}: rolled {result} (range {min}-{max})");
        }

        return result;
    }

    // ========================================================================
    // STANDARD DICE
    // ========================================================================

    /// <summary>Roll 1d4 (1-4).</summary>
    public static int D4(string context = null) => Roll(1, 4, context);

    /// <summary>Roll 1d6 (1-6).</summary>
    public static int D6(string context = null) => Roll(1, 6, context);

    /// <summary>Roll 1d8 (1-8).</summary>
    public static int D8(string context = null) => Roll(1, 8, context);

    /// <summary>Roll 1d10 (1-10).</summary>
    public static int D10(string context = null) => Roll(1, 10, context);

    /// <summary>Roll 1d12 (1-12).</summary>
    public static int D12(string context = null) => Roll(1, 12, context);

    /// <summary>Roll 1d20 (1-20). Used for attack rolls, saving throws, skill checks.</summary>
    public static int D20(string context = null) => Roll(1, 20, context);

    /// <summary>Roll 1d100 / percentile (1-100). Used for concealment, spell failure, etc.</summary>
    public static int Percentile(string context = null) => Roll(1, 100, context);

    // ========================================================================
    // MULTI-DIE ROLLS
    // ========================================================================

    /// <summary>
    /// Roll multiple dice of the same type and sum them.
    /// E.g. RollMultiple(3, 6) = 3d6.
    /// </summary>
    /// <param name="count">Number of dice to roll.</param>
    /// <param name="sides">Number of sides on each die.</param>
    /// <param name="context">Optional description for debug logging.</param>
    /// <returns>Sum of all dice rolled.</returns>
    public static int RollMultiple(int count, int sides, string context = null)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
        {
            total += Roll(1, sides);
        }

        if (EnableLogging && !string.IsNullOrEmpty(context))
        {
            Debug.Log($"[Dice] {context}: {count}d{sides} = {total}");
        }

        return total;
    }

    /// <summary>
    /// Roll a variable-sided die (1 to sides). Useful when die type is determined at runtime.
    /// </summary>
    /// <param name="sides">Number of sides (e.g. weapon damage die).</param>
    /// <param name="context">Optional description for debug logging.</param>
    /// <returns>Random integer in [1, sides].</returns>
    public static int RollDie(int sides, string context = null)
    {
        return Roll(1, sides, context);
    }

    // ========================================================================
    // D&D-SPECIFIC HELPERS
    // ========================================================================

    /// <summary>
    /// Make a percentile check: roll 1d100 and return true if the roll is
    /// less than or equal to the threshold.
    /// E.g. PercentileCheck(20) = 20% chance of returning true.
    /// </summary>
    /// <param name="threshold">Value to check against (1-100).</param>
    /// <param name="context">Optional description for debug logging.</param>
    /// <param name="roll">The actual roll value (output for logging).</param>
    /// <returns>True if the roll &lt;= threshold.</returns>
    public static bool PercentileCheck(int threshold, string context, out int roll)
    {
        roll = Percentile(context);
        return roll <= threshold;
    }

    /// <summary>
    /// Coin flip: 50/50 chance. Returns true or false with equal probability.
    /// </summary>
    /// <param name="context">Optional description for debug logging.</param>
    /// <returns>True 50% of the time.</returns>
    public static bool CoinFlip(string context = null)
    {
        int result = Random.Range(0, 2);
        if (EnableLogging && !string.IsNullOrEmpty(context))
        {
            Debug.Log($"[Dice] {context}: coin flip = {(result == 0 ? "heads" : "tails")}");
        }
        return result == 0;
    }

    /// <summary>
    /// Generate a random identifier number (e.g. for summoned creature naming).
    /// NOT a dice roll — purely for unique ID generation.
    /// </summary>
    /// <param name="min">Minimum value (inclusive).</param>
    /// <param name="max">Maximum value (inclusive).</param>
    /// <returns>Random integer in [min, max].</returns>
    public static int RandomId(int min, int max)
    {
        return Random.Range(min, max + 1);
    }
}
