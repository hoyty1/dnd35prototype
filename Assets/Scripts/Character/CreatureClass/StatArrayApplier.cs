using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The elite and nonelite ability score arrays (elite: DMG p.169, MM p.290; nonelite: MM p.290, MM p.7) and their arrangement for creatures that gain
/// class levels (CRE-058, delegated RAW readings 2026-10-14):
///
/// Elite array: 15, 14, 13, 12, 10, 8. PCs, the DMG chapter 4 NPCs (DMG p.110-111) and any creature given a PC class
/// (MM p.294) use it, arranged as the DMG chapter 4 class table prints its starting scores, with that table's increases
/// (<see cref="TryGetDmgNpcStartingScores"/>, <see cref="DmgNpcIncreaseAbility"/>).
/// Nonelite array: 13, 12, 11, 10, 9, 8. Creatures with NPC classes use it (MM p.294; MM p.7: humanoid warriors), arranged
/// by <see cref="GetPriorityStats"/>, since no DMG table arranges it.
/// Racial adjustments go on after the array (DMG p.126-127, MM p.294): <see cref="TryGetRacialAdjustments"/>.
/// </summary>
public enum StatArrayType
{
    /// <summary>Elite array: 15, 14, 13, 12, 10, 8 (DMG p.169, MM p.290).</summary>
    Elite,
    /// <summary>Nonelite array: 13, 12, 11, 10, 9, 8 (MM p.290; MM p.7).</summary>
    Nonelite
}

public static class StatArrayApplier
{
    /// <summary>Elite array (DMG p.169, MM p.290): 15, 14, 13, 12, 10, 8.</summary>
    public static readonly int[] EliteArray = { 15, 14, 13, 12, 10, 8 };

    /// <summary>Nonelite array (MM p.290; MM p.7): 13, 12, 11, 10, 9, 8.</summary>
    public static readonly int[] NoneliteArray = { 13, 12, 11, 10, 9, 8 };

    /// <summary>Get the stat array for the given type.</summary>
    public static int[] GetArray(StatArrayType type)
    {
        return type == StatArrayType.Elite
            ? (int[])EliteArray.Clone()
            : (int[])NoneliteArray.Clone();
    }

    /// <summary>
    /// Get the stat priority order for a class (highest priority first).
    /// Returns an array of 6 stat names in priority order.
    /// The highest array value is assigned to the first stat, etc.
    /// </summary>
    public static string[] GetPriorityStats(string className)
    {
        switch (className)
        {
            // Martial classes — STR first
            case "Fighter":
            case "Warrior":
                return new[] { "STR", "CON", "DEX", "WIS", "INT", "CHA" };

            case "Barbarian":
                return new[] { "STR", "CON", "DEX", "WIS", "CHA", "INT" };

            case "Paladin":
                return new[] { "STR", "CHA", "CON", "WIS", "DEX", "INT" };

            // DEX-based martial
            case "Ranger":
                return new[] { "DEX", "STR", "CON", "WIS", "INT", "CHA" };

            case "Monk":
                return new[] { "DEX", "STR", "WIS", "CON", "INT", "CHA" };

            case "Rogue":
                return new[] { "DEX", "INT", "CON", "WIS", "STR", "CHA" };

            // Arcane casters
            case "Wizard":
                return new[] { "INT", "DEX", "CON", "WIS", "CHA", "STR" };

            case "Sorcerer":
                return new[] { "CHA", "DEX", "CON", "WIS", "INT", "STR" };

            case "Bard":
                return new[] { "CHA", "DEX", "CON", "INT", "WIS", "STR" };

            // Divine casters
            case "Cleric":
                return new[] { "WIS", "CON", "STR", "CHA", "DEX", "INT" };

            case "Druid":
                return new[] { "WIS", "CON", "STR", "CHA", "DEX", "INT" };

            case "Adept":
                return new[] { "WIS", "INT", "CON", "DEX", "CHA", "STR" };

            // NPC classes
            case "Aristocrat":
                return new[] { "CHA", "INT", "WIS", "DEX", "STR", "CON" };

            case "Expert":
                return new[] { "INT", "DEX", "WIS", "CON", "STR", "CHA" };

            case "Commoner":
                return new[] { "CON", "STR", "DEX", "WIS", "INT", "CHA" };

            default:
                return new[] { "STR", "DEX", "CON", "INT", "WIS", "CHA" };
        }
    }

    /// <summary>
    /// Apply a stat array to a set of ability scores based on class priority.
    /// Returns a dictionary mapping stat name → assigned value.
    /// Test-only, like <see cref="ApplyRacialModifiers"/> and <see cref="ApplyAbilityIncreases"/> (CRE-028); live spawns
    /// use <see cref="ArrangeByPriority"/>, <see cref="TryGetRacialAdjustments"/> and <see cref="DmgNpcIncreaseAbility"/>.
    /// </summary>
    public static Dictionary<string, int> ApplyArray(StatArrayType arrayType, string className)
    {
        int[] array = GetArray(arrayType);
        string[] priority = GetPriorityStats(className);

        // Sort array descending (already is, but be safe)
        System.Array.Sort(array);
        System.Array.Reverse(array);

        var result = new Dictionary<string, int>();
        for (int i = 0; i < 6 && i < priority.Length && i < array.Length; i++)
        {
            result[priority[i]] = array[i];
        }
        return result;
    }

    /// <summary>
    /// Apply racial modifiers on top of the stat array.
    /// Does not modify the input dictionary — returns a new one.
    /// </summary>
    public static Dictionary<string, int> ApplyRacialModifiers(
        Dictionary<string, int> baseStats,
        int strMod = 0, int dexMod = 0, int conMod = 0,
        int intMod = 0, int wisMod = 0, int chaMod = 0)
    {
        var result = new Dictionary<string, int>(baseStats);
        if (result.ContainsKey("STR")) result["STR"] += strMod;
        if (result.ContainsKey("DEX")) result["DEX"] += dexMod;
        if (result.ContainsKey("CON")) result["CON"] += conMod;
        if (result.ContainsKey("INT")) result["INT"] += intMod;
        if (result.ContainsKey("WIS")) result["WIS"] += wisMod;
        if (result.ContainsKey("CHA")) result["CHA"] += chaMod;
        return result;
    }

    /// <summary>
    /// Apply ability score increases from HD progression (every 4 HD).
    /// Adds to the highest-priority stat for the class.
    /// </summary>
    public static void ApplyAbilityIncreases(Dictionary<string, int> stats, string className, int totalHD)
    {
        int increases = totalHD / 4;
        if (increases <= 0) return;

        string[] priority = GetPriorityStats(className);
        if (priority.Length == 0) return;

        // Add all increases to the primary stat
        string primaryStat = priority[0];
        if (stats.ContainsKey(primaryStat))
            stats[primaryStat] += increases;
    }

    // ── DMG chapter 4 NPC tables (CRE-058) ─────────────────────────────

    /// <summary>Ability order of the score arrays below: STR, DEX, CON, INT, WIS, CHA.</summary>
    public const int Str = 0, Dex = 1, Con = 2, Int = 3, Wis = 4, Cha = 5;

    // Starting ability scores of each DMG chapter 4 NPC class table (the elite array arranged per class, DMG p.111-125),
    // in STR, DEX, CON, INT, WIS, CHA order.
    private static readonly Dictionary<string, int[]> DmgNpcStartingScores = new Dictionary<string, int[]>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "Barbarian", new[] { 15, 14, 13, 10, 12, 8 } },  // DMG p.112
        { "Bard",      new[] { 10, 13, 12, 14, 8, 15 } },  // DMG p.113
        { "Cleric",    new[] { 13, 8, 14, 10, 15, 12 } },  // DMG p.114
        { "Druid",     new[] { 10, 14, 13, 12, 15, 8 } },  // DMG p.115
        { "Fighter",   new[] { 15, 13, 14, 10, 12, 8 } },  // DMG p.117
        { "Monk",      new[] { 14, 13, 12, 10, 15, 8 } },  // DMG p.118
        { "Paladin",   new[] { 14, 8, 12, 10, 13, 15 } },  // DMG p.118
        { "Ranger",    new[] { 14, 15, 13, 10, 12, 8 } },  // DMG p.121
        { "Rogue",     new[] { 12, 15, 13, 14, 10, 8 } },  // DMG p.123
        { "Sorcerer",  new[] { 8, 14, 13, 10, 12, 15 } },  // DMG p.124
        { "Wizard",    new[] { 10, 14, 13, 15, 12, 8 } },  // DMG p.125
    };

    // The ability each DMG chapter 4 table raises at 4th, 8th, 12th, 16th and 20th level (the base increase; the tables'
    // parenthesized values add magic items, which are not counted here).
    private static readonly Dictionary<string, int[]> DmgNpcIncreases = new Dictionary<string, int[]>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "Barbarian", new[] { Str, Con, Str, Str, Str } },
        { "Bard",      new[] { Cha, Cha, Cha, Cha, Cha } },
        { "Cleric",    new[] { Wis, Wis, Wis, Wis, Wis } },
        { "Druid",     new[] { Wis, Wis, Wis, Wis, Wis } },
        { "Fighter",   new[] { Str, Str, Str, Str, Str } },
        { "Monk",      new[] { Dex, Wis, Dex, Dex, Wis } },
        { "Paladin",   new[] { Wis, Cha, Cha, Cha, Cha } },
        { "Ranger",    new[] { Dex, Dex, Dex, Dex, Dex } },
        { "Rogue",     new[] { Dex, Dex, Dex, Dex, Dex } },
        { "Sorcerer",  new[] { Cha, Cha, Cha, Cha, Cha } },
        { "Wizard",    new[] { Int, Int, Int, Int, Int } },
    };

    /// <summary>
    /// The DMG chapter 4 table's starting ability scores for <paramref name="className"/> (STR, DEX, CON, INT, WIS, CHA):
    /// the elite array arranged as that table prints it (DMG p.111: "to the character's best advantage"). False for a
    /// class without a DMG NPC table (the NPC classes).
    /// </summary>
    public static bool TryGetDmgNpcStartingScores(string className, out int[] scores)
    {
        scores = null;
        if (string.IsNullOrEmpty(className) || !DmgNpcStartingScores.TryGetValue(className, out int[] table))
            return false;
        scores = (int[])table.Clone();
        return true;
    }

    /// <summary>
    /// The ability (index into the STR..CHA order) that receives the <paramref name="increaseNumber"/>-th ability increase
    /// (1 at 4 Hit Dice, 2 at 8, ...; PHB p.58, MM p.291): the DMG chapter 4 table's choice for a PC class, else the
    /// class's first priority ability (<see cref="GetPriorityStats"/>).
    /// </summary>
    public static int DmgNpcIncreaseAbility(string className, int increaseNumber)
    {
        if (!string.IsNullOrEmpty(className) && DmgNpcIncreases.TryGetValue(className, out int[] table) && table.Length > 0)
            return table[Mathf.Clamp(increaseNumber - 1, 0, table.Length - 1)];
        return AbilityIndex(GetPriorityStats(className)[0]);
    }

    /// <summary>
    /// <paramref name="arrayType"/> arranged by <see cref="GetPriorityStats"/> for <paramref name="className"/>, in STR,
    /// DEX, CON, INT, WIS, CHA order (for classes without a DMG chapter 4 table).
    /// </summary>
    public static int[] ArrangeByPriority(StatArrayType arrayType, string className)
    {
        Dictionary<string, int> byName = ApplyArray(arrayType, className);
        var scores = new int[6];
        foreach (KeyValuePair<string, int> pair in byName)
        {
            int i = AbilityIndex(pair.Key);
            if (i >= 0)
                scores[i] = pair.Value;
        }
        return scores;
    }

    private static int AbilityIndex(string stat)
    {
        switch (stat)
        {
            case "STR": return Str;
            case "DEX": return Dex;
            case "CON": return Con;
            case "INT": return Int;
            case "WIS": return Wis;
            case "CHA": return Cha;
            default: return -1;
        }
    }

    // Racial adjustments of the MM humanoids with 1 Hit Die whose entries name no PHB race (STR, DEX, CON, INT, WIS, CHA).
    // Checked in order, so "hobgoblin" before "goblin".
    private static readonly KeyValuePair<string, int[]>[] MonsterManualHumanoidAdjustments =
    {
        new KeyValuePair<string, int[]>("hobgoblin", new[] { 0, 2, 2, 0, 0, 0 }),    // MM p.154
        new KeyValuePair<string, int[]>("goblin", new[] { -2, 2, 0, 0, 0, -2 }),     // MM p.134
        new KeyValuePair<string, int[]>("kobold", new[] { -4, 2, -2, 0, 0, 0 }),     // MM p.162
        new KeyValuePair<string, int[]>("orc", new[] { 4, 0, 0, -2, -2, -2 }),       // MM p.204
    };

    /// <summary>
    /// The racial ability adjustments of <paramref name="def"/> (STR, DEX, CON, INT, WIS, CHA), which go on after the
    /// array (DMG p.126-127, MM p.294). A 1-HD humanoid whose Hit Die a class replaces (<paramref name="replacedHumanoid"/>)
    /// stores the MM warrior's scores, which already hold the nonelite array, so its adjustments come from its race: the
    /// PHB or MM race data (<see cref="RacialTraitRules.ResolveNpcRace"/>) or the MM humanoid entries above; false when
    /// neither knows it. Any other creature's entry holds the standard scores plus its adjustments: an even score less 10,
    /// an odd one less 11 (MM p.7), which also keeps the increases of the Hit Dice it already has (MM p.291). A score of 0
    /// (a nonability) has no adjustment.
    /// </summary>
    public static bool TryGetRacialAdjustments(NPCDefinition def, bool replacedHumanoid, out int[] adjustments)
    {
        adjustments = new int[6];
        if (def == null)
            return false;

        if (!replacedHumanoid)
        {
            int[] stored = { def.STR, def.DEX, def.CON, def.INT, def.WIS, def.CHA };
            for (int i = 0; i < 6; i++)
                adjustments[i] = stored[i] <= 0 ? 0 : stored[i] - (stored[i] % 2 == 0 ? 10 : 11);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(def.RaceName))
        {
            RaceData race = RacialTraitRules.ResolveNpcRace(def);
            if (race == null)
                return false;
            adjustments[Str] = race.STRModifier;
            adjustments[Dex] = race.DEXModifier;
            adjustments[Con] = race.CONModifier;
            adjustments[Int] = race.INTModifier;
            adjustments[Wis] = race.WISModifier;
            adjustments[Cha] = race.CHAModifier;
            return true;
        }

        string id = (def.Id ?? string.Empty).ToLowerInvariant();
        if (id.StartsWith("spawn_"))
            id = id.Substring("spawn_".Length);
        foreach (KeyValuePair<string, int[]> pair in MonsterManualHumanoidAdjustments)
        {
            if (id.StartsWith(pair.Key))
            {
                adjustments = (int[])pair.Value.Clone();
                return true;
            }
        }
        return false;
    }
}
