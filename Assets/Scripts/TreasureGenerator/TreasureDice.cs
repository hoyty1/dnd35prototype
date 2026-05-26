// ============================================================================
// TreasureDice.cs — Dice rolling engine for the D&D 3.5e treasure generator.
// Ported from js/dice.js. Uses UnityEngine.Random for consistency with the
// rest of the codebase.
// ============================================================================
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DND35e.Treasure
{
    public static class TreasureDice
    {
        /// <summary>Roll a single die with N sides (1..sides).</summary>
        public static int Roll(int sides) => Random.Range(1, sides + 1);

        /// <summary>Roll d% (d100) – returns 1-100.</summary>
        public static int RollPercent() => Roll(100);

        /// <summary>Roll NdS and return the sum.</summary>
        public static int RollNdS(int count, int sides)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
                total += Roll(sides);
            return total;
        }

        /// <summary>
        /// Parse and evaluate DMG dice notation strings.
        /// Supports: "2d8×10", "1d6×1,000", "4d4", "1d4+1", "2d4×100"
        /// </summary>
        public static int Evaluate(string notation)
        {
            if (string.IsNullOrWhiteSpace(notation) || notation == "—" || notation == "-" || notation == "nil")
                return 0;

            // Clean up notation
            string expr = notation.Replace(",", "").Replace("×", "*").Replace("x", "*").Replace("X", "*").Trim();

            // Match pattern: NdS[+M][*X]
            var match = Regex.Match(expr, @"^(\d+)d(\d+)(?:\+(\d+))?(?:\*(\d+))?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                int count = int.Parse(match.Groups[1].Value);
                int sides = int.Parse(match.Groups[2].Value);
                int add = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
                int mult = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 1;
                return (RollNdS(count, sides) + add) * mult;
            }

            // Try as plain number
            if (int.TryParse(expr, out int num))
                return num;

            Debug.LogWarning($"[TreasureDice] Could not parse dice notation: {notation}");
            return 0;
        }

        /// <summary>
        /// Look up a d% result in a range table.
        /// Returns the matching entry index, or -1 if none found.
        /// </summary>
        public static int LookupPercentIndex<T>(T[] table, int roll = -1) where T : IPercentEntry
        {
            if (roll < 0) roll = RollPercent();
            for (int i = 0; i < table.Length; i++)
            {
                if (roll >= table[i].Min && roll <= table[i].Max)
                    return i;
            }
            return -1;
        }
    }

    /// <summary>Interface for percent-table entries with min/max ranges.</summary>
    public interface IPercentEntry
    {
        int Min { get; }
        int Max { get; }
    }
}
