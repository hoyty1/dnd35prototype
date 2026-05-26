using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hardcoded DMG 3.5e dungeon encounter table data for levels 1-8,
/// plus CSV-based table builder for levels 1-9 (Phase 5).
///
/// Two loading paths:
///   <see cref="BuildAllTables"/> — Original hardcoded tables (Phase 3)
///   <see cref="BuildFromCSV"/>   — CSV-driven tables with dice expressions (Phase 5)
///
/// Creature IDs reference NPCDatabase entries. Creatures from the CSV that
/// do not exist in NPCDatabase are noted in comments but omitted from tables
/// to avoid runtime errors.
///
/// Phase 3: DMG Encounter Tables.
/// Phase 5: CSV-based encounter table loading.
/// </summary>
public static class DungeonEncounterTableData
{
    /// <summary>
    /// Build all eight encounter tables with cascade entries and d% ranges.
    /// Each table covers d% 01-100:
    ///   01-10  → Cascade easier (re-roll on level N-1)
    ///   11-90  → Actual encounter entries for this level
    ///   91-100 → Cascade harder (re-roll on level N+1)
    /// </summary>
    public static Dictionary<int, DungeonEncounterTable> BuildAllTables()
    {
        var tables = new Dictionary<int, DungeonEncounterTable>();
        tables[1] = BuildTable1();
        tables[2] = BuildTable2();
        tables[3] = BuildTable3();
        tables[4] = BuildTable4();
        tables[5] = BuildTable5();
        tables[6] = BuildTable6();
        tables[7] = BuildTable7();
        tables[8] = BuildTable8();
        return tables;
    }

    // =========================================================================
    //  Phase 5: CSV-Based Table Builder
    // =========================================================================

    /// <summary>
    /// Build encounter tables from a CSV file. Parses encounter descriptions
    /// using <see cref="EncounterDescriptionParser"/>, supports dice expressions
    /// for variable creature counts, and handles compound/NPC entries.
    ///
    /// Tables are built for all dungeon levels present in the CSV (typically 1-9).
    /// Each table's d% coverage is validated after construction.
    /// </summary>
    /// <param name="csvPath">Absolute path to dungeon_encounters.csv.</param>
    /// <param name="creatureNameMap">
    /// Name resolution map (CSV creature name → NPCDatabase ID).
    /// May be null; fallback normalization will be used.
    /// </param>
    /// <returns>
    /// Dictionary of tables keyed by dungeon level.
    /// Empty if CSV loading fails entirely.
    /// </returns>
    public static Dictionary<int, DungeonEncounterTable> BuildFromCSV(
        string csvPath,
        Dictionary<string, string> creatureNameMap)
    {
        var tables = new Dictionary<int, DungeonEncounterTable>();

        // ── Step 1: Parse CSV into raw rows ──
        List<RawEncounterRow> rows = EncounterCSVParser.ParseCSV(csvPath);
        if (rows.Count == 0)
        {
            Debug.LogError("[EncounterTableData] CSV produced no rows. " +
                           "Falling back to hardcoded tables.");
            return BuildAllTables();
        }

        Debug.Log(EncounterCSVParser.GetSummary(rows));

        // ── Step 2: Group rows by dungeon level ──
        Dictionary<int, List<RawEncounterRow>> grouped =
            EncounterCSVParser.GroupByLevel(rows);

        // ── Step 3: Build a table for each level ──
        int totalEntries = 0;
        int totalWarnings = 0;
        int unresolvedCreatures = 0;

        var levels = new List<int>(grouped.Keys);
        levels.Sort();

        for (int i = 0; i < levels.Count; i++)
        {
            int level = levels[i];
            List<RawEncounterRow> levelRows = grouped[level];

            var table = new DungeonEncounterTable(level);

            for (int j = 0; j < levelRows.Count; j++)
            {
                RawEncounterRow row = levelRows[j];

                // Parse the encounter description
                ParsedEncounterDescription parsed =
                    EncounterDescriptionParser.Parse(row.Encounter);

                if (parsed.HasWarnings)
                {
                    for (int w = 0; w < parsed.Warnings.Count; w++)
                    {
                        Debug.LogWarning($"[EncounterTableData] L{level} " +
                            $"[{row.RollMin}-{row.RollMax}]: {parsed.Warnings[w]}");
                    }
                    totalWarnings += parsed.Warnings.Count;
                }

                // Build the table entry from parsed data
                DungeonEncounterTableEntry entry = BuildEntryFromParsed(
                    row.RollMin, row.RollMax, level, parsed, creatureNameMap,
                    out int entryUnresolved);

                unresolvedCreatures += entryUnresolved;

                if (entry != null)
                {
                    table.Entries.Add(entry);
                    totalEntries++;
                }
                else
                {
                    Debug.LogWarning($"[EncounterTableData] L{level} " +
                        $"[{row.RollMin}-{row.RollMax}]: Failed to build entry " +
                        $"from '{row.Encounter}'");
                }
            }

            // Validate the table
            List<string> issues = table.Validate();
            for (int v = 0; v < issues.Count; v++)
            {
                Debug.LogWarning($"[EncounterTableData] Validation: {issues[v]}");
            }

            tables[level] = table;
        }

        Debug.Log($"[EncounterTableData] Built {tables.Count} tables from CSV: " +
                  $"{totalEntries} entries, {totalWarnings} parse warnings, " +
                  $"{unresolvedCreatures} unresolved creature names.");

        return tables;
    }

    // =========================================================================
    //  Entry Builder — converts ParsedEncounterDescription to table entry
    // =========================================================================

    /// <summary>
    /// Convert a parsed CSV row into a <see cref="DungeonEncounterTableEntry"/>.
    /// Handles cascade entries, single creatures, compound groups, and NPC entries.
    /// </summary>
    /// <param name="rollMin">Minimum d% roll (inclusive).</param>
    /// <param name="rollMax">Maximum d% roll (inclusive).</param>
    /// <param name="dungeonLevel">Dungeon level for EL estimation.</param>
    /// <param name="parsed">Parsed encounter description.</param>
    /// <param name="nameMap">Creature name resolution map (may be null).</param>
    /// <param name="unresolvedCount">Output: number of creature names that could not be resolved.</param>
    /// <returns>Built entry, or null if completely unparseable.</returns>
    private static DungeonEncounterTableEntry BuildEntryFromParsed(
        int rollMin, int rollMax, int dungeonLevel,
        ParsedEncounterDescription parsed,
        Dictionary<string, string> nameMap,
        out int unresolvedCount)
    {
        unresolvedCount = 0;

        // ── Cascade entry ──
        if (parsed.IsCascade)
        {
            CascadeDirection dir = parsed.CascadeTargetLevel < dungeonLevel
                ? CascadeDirection.Easier
                : CascadeDirection.Harder;
            return DungeonEncounterTableEntry.CascadeEntry(rollMin, rollMax, dir);
        }

        // ── Normal encounter entry ──
        if (parsed.Groups.Count == 0)
        {
            // No groups parsed — create a description-only entry
            Debug.LogWarning($"[EncounterTableData] No creature groups parsed from: " +
                             $"'{parsed.RawDescription}'");
            return null;
        }

        var entry = new DungeonEncounterTableEntry
        {
            MinRoll = rollMin,
            MaxRoll = rollMax,
            EL = EstimateEL(dungeonLevel, parsed),
            Description = parsed.RawDescription
        };

        // Build creature entries for each parsed group
        for (int i = 0; i < parsed.Groups.Count; i++)
        {
            ParsedCreatureGroup group = parsed.Groups[i];
            EncounterCreatureEntry creature = BuildCreatureFromGroup(
                group, nameMap, out bool unresolved);

            if (unresolved) unresolvedCount++;

            if (creature != null)
            {
                entry.Creatures.Add(creature);
            }
        }

        // If no creatures could be built, still return the entry with description
        // (spawner will handle empty creature lists gracefully)
        if (entry.Creatures.Count == 0)
        {
            Debug.LogWarning($"[EncounterTableData] Entry [{rollMin}-{rollMax}] has no " +
                             $"resolvable creatures: '{parsed.RawDescription}'");
        }

        return entry;
    }

    /// <summary>
    /// Build a single <see cref="EncounterCreatureEntry"/> from a parsed creature group.
    /// </summary>
    /// <param name="group">Parsed creature group.</param>
    /// <param name="nameMap">Creature name resolution map (may be null).</param>
    /// <param name="unresolved">Output: true if creature name could not be resolved.</param>
    /// <returns>Creature entry, or null if completely unusable.</returns>
    private static EncounterCreatureEntry BuildCreatureFromGroup(
        ParsedCreatureGroup group,
        Dictionary<string, string> nameMap,
        out bool unresolved)
    {
        unresolved = false;

        var creature = new EncounterCreatureEntry();

        if (group.IsNpc)
        {
            // NPC with class levels — resolve race as creature ID
            string raceId = ResolveCreatureName(group.NpcRace, nameMap);
            if (raceId == null)
            {
                // Try the full NPC description as a name map key
                raceId = ResolveCreatureName(group.RawText, nameMap);
            }
            if (raceId == null)
            {
                // Last resort: use race as ID directly
                raceId = group.NpcRace != null
                    ? group.NpcRace.ToLower().Replace(" ", "_")
                    : "human";
                unresolved = true;
            }

            creature.BaseCreatureId = raceId;
            creature.TemplateClass = CapitalizeFirst(group.NpcClass);
            creature.TemplateLevel = group.NpcLevel;
        }
        else
        {
            // Standard creature — resolve name to NPCDatabase ID
            string creatureId = ResolveCreatureName(group.CreatureName, nameMap);
            if (creatureId == null)
            {
                // Fallback: normalize the creature name to ID format
                creatureId = NormalizeName(group.CreatureName);
                unresolved = true;
            }
            creature.BaseCreatureId = creatureId;
        }

        // Set dice expression for count (resolved at encounter generation time)
        if (group.CountExpression != null)
        {
            creature.CountExpression = group.CountExpression;
            // Set Count to the minimum as a safe default
            creature.Count = Math.Max(1, group.CountExpression.Minimum);
        }
        else
        {
            creature.Count = 1;
        }

        // Apply creature templates
        if (group.HasTemplates)
        {
            creature.CreatureTemplateIds = new List<string>(group.TemplateIds);
        }

        return creature;
    }

    // =========================================================================
    //  Name Resolution Helpers
    // =========================================================================

    /// <summary>
    /// Resolve a CSV creature name to an NPCDatabase ID using the name map
    /// and fallback normalization.
    /// </summary>
    /// <param name="csvName">Creature name from the CSV.</param>
    /// <param name="nameMap">Name resolution map (may be null).</param>
    /// <returns>Resolved ID, or null if unresolvable.</returns>
    private static string ResolveCreatureName(
        string csvName, Dictionary<string, string> nameMap)
    {
        if (string.IsNullOrWhiteSpace(csvName)) return null;
        csvName = csvName.Trim();

        // Check name map (case-insensitive)
        if (nameMap != null)
        {
            string mapped;
            if (nameMap.TryGetValue(csvName, out mapped))
                return mapped;

            // Try lowercase
            string lower = csvName.ToLowerInvariant();
            if (nameMap.TryGetValue(lower, out mapped))
                return mapped;

            // Try singular (strip trailing 's')
            if (lower.EndsWith("s") && !lower.EndsWith("ss"))
            {
                string singular = lower.Substring(0, lower.Length - 1);
                if (nameMap.TryGetValue(singular, out mapped))
                    return mapped;
            }
        }

        // Fallback: normalize to ID format
        string normalized = NormalizeName(csvName);

        // Return normalized form — it will be validated against NPCDatabase at spawn time
        return normalized;
    }

    /// <summary>
    /// Normalize a creature name to NPCDatabase ID format:
    /// lowercase, spaces to underscores, strip hyphens and apostrophes.
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unknown";
        return name.Trim()
            .ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace("'", "");
    }

    /// <summary>
    /// Capitalize the first letter of a string (for class names).
    /// </summary>
    private static string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Length == 1) return s.ToUpperInvariant();
        return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
    }

    /// <summary>
    /// Estimate the Encounter Level for a table entry.
    /// Default strategy: use the dungeon level. A more accurate approach
    /// would look up creature CRs and compute EL from the party formula,
    /// but that requires a CR database (future enhancement).
    /// </summary>
    private static int EstimateEL(int dungeonLevel, ParsedEncounterDescription parsed)
    {
        // Simple heuristic: EL ≈ dungeon level
        // Compound entries with many creatures might be higher,
        // but this is adequate for display purposes.
        return dungeonLevel;
    }

    /// <summary>
    /// Run a diagnostic on the CSV-built tables and return a report string.
    /// Useful for testing the CSV loading pipeline.
    /// </summary>
    /// <param name="tables">Tables to diagnose.</param>
    /// <returns>Multi-line diagnostic report.</returns>
    public static string DiagnoseCSVTables(Dictionary<int, DungeonEncounterTable> tables)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== CSV Table Diagnostic ===");

        var levels = new List<int>(tables.Keys);
        levels.Sort();

        int totalEntries = 0;
        int totalCreatures = 0;
        int totalDiceEntries = 0;
        int totalCascades = 0;

        for (int i = 0; i < levels.Count; i++)
        {
            int level = levels[i];
            DungeonEncounterTable table = tables[level];

            int entries = table.TotalEntryCount;
            int encounters = table.EncounterEntryCount;
            int cascades = entries - encounters;
            int creaturesInLevel = 0;
            int diceInLevel = 0;

            for (int j = 0; j < table.Entries.Count; j++)
            {
                var entry = table.Entries[j];
                if (!entry.IsCascade)
                {
                    creaturesInLevel += entry.Creatures.Count;
                    for (int k = 0; k < entry.Creatures.Count; k++)
                    {
                        if (entry.Creatures[k].CountExpression != null &&
                            !entry.Creatures[k].CountExpression.IsFixed)
                        {
                            diceInLevel++;
                        }
                    }
                }
            }

            List<string> issues = table.Validate();
            string status = issues.Count == 0 ? "✓" : $"⚠ {issues.Count} issues";

            sb.AppendLine($"  Level {level}: {entries} entries " +
                $"({encounters} encounters + {cascades} cascades), " +
                $"{creaturesInLevel} creature groups ({diceInLevel} dice-based) " +
                $"[{status}]");

            // Print first 3 encounter entries as samples
            int sampleCount = 0;
            for (int j = 0; j < table.Entries.Count && sampleCount < 3; j++)
            {
                var entry = table.Entries[j];
                if (!entry.IsCascade)
                {
                    sb.AppendLine($"    Sample: {entry}");
                    for (int k = 0; k < entry.Creatures.Count; k++)
                    {
                        var c = entry.Creatures[k];
                        string countStr = c.CountExpression != null
                            ? c.CountExpression.ToString() : $"{c.Count}";
                        string classStr = c.HasClassLevels
                            ? $" {c.TemplateClass} {c.TemplateLevel}" : "";
                        sb.AppendLine($"      → {countStr}x {c.BaseCreatureId}{classStr}");
                    }
                    sampleCount++;
                }
            }

            totalEntries += entries;
            totalCreatures += creaturesInLevel;
            totalDiceEntries += diceInLevel;
            totalCascades += cascades;
        }

        sb.AppendLine($"  ────────────────────────────────");
        sb.AppendLine($"  Total: {totalEntries} entries, {totalCreatures} creature groups, " +
            $"{totalDiceEntries} dice-based, {totalCascades} cascades");

        return sb.ToString();
    }

    // =========================================================================
    //  TABLE 1 — Dungeon Level 1 (EL 1)
    //  Creatures: CR 1/8 to CR 1
    // =========================================================================
    private static DungeonEncounterTable BuildTable1()
    {
        var t = new DungeonEncounterTable(1);

        // Cascade easier (level 1 wraps to self)
        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        // CR 1/8 – CR 1/4 creatures
        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 15, 1,
            "monstrous_centipede_medium", 2, "2x Medium Monstrous Centipede"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(16, 20, 1,
            "dire_rat", 2, "2x Dire Rat"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(21, 25, 1,
            "giant_fire_beetle", 3, "3x Giant Fire Beetle"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(26, 30, 1,
            "monstrous_scorpion_small", 2, "2x Small Monstrous Scorpion"));

        // CR 1/2 creatures
        t.Entries.Add(DungeonEncounterTableEntry.Basic(31, 35, 1,
            "dwarf_warrior", 1, "1x Dwarf Warrior"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(36, 40, 1,
            "elf_warrior", 1, "1x Elf Warrior"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(41, 44, 1,
            "goblin", 2, "2x Goblin"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(45, 48, 1,
            "hobgoblin", 1, "1x Hobgoblin"));

        // CR 1 creatures
        t.Entries.Add(DungeonEncounterTableEntry.Basic(49, 53, 1,
            "krenshar", 1, "1x Krenshar"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(54, 58, 1,
            "lemure", 1, "1x Lemure"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(59, 63, 1,
            "stirge", 2, "2x Stirge"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(64, 68, 1,
            "spider_swarm", 1, "1x Spider Swarm"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(69, 73, 1,
            "lantern_archon", 1, "1x Lantern Archon"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(74, 78, 1,
            "halfling_warrior", 1, "1x Halfling Warrior"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(79, 83, 1,
            "fiendish_dire_rat", 1, "1x Fiendish Dire Rat"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(84, 87, 1,
            "bat_swarm", 1, "1x Bat Swarm"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(88, 90, 1,
            "rat_swarm", 1, "1x Rat Swarm"));

        // Cascade harder
        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 2 — Dungeon Level 2 (EL 2)
    //  Creatures: CR 1 to CR 2
    // =========================================================================
    private static DungeonEncounterTable BuildTable2()
    {
        var t = new DungeonEncounterTable(2);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        // CR 1 creatures (pairs or singles for EL 2)
        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 15, 2,
            "bugbear", 1, "1x Bugbear"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(16, 20, 2,
            "choker", 1, "1x Choker"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(21, 25, 2,
            "dretch", 1, "1x Dretch"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(26, 29, 2,
            "quasit", 1, "1x Quasit"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(30, 33, 2,
            "imp", 1, "1x Imp"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(34, 37, 2,
            "dire_bat", 1, "1x Dire Bat"));

        // CR 2 creatures
        t.Entries.Add(DungeonEncounterTableEntry.Basic(38, 42, 2,
            "formian_worker", 2, "2x Formian Worker"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(43, 47, 2,
            "shocker_lizard", 2, "2x Shocker Lizard"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(48, 52, 2,
            "worg", 1, "1x Worg"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(53, 57, 2,
            "constrictor_snake", 1, "1x Constrictor Snake"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(58, 62, 2,
            "huge_monstrous_centipede", 1, "1x Huge Monstrous Centipede"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(63, 67, 2,
            "gnoll", 2, "2x Gnoll"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(68, 72, 2,
            "lizardfolk", 1, "1x Lizardfolk"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(73, 77, 2,
            "troglodyte", 2, "2x Troglodyte"));

        // Swarms & misc
        t.Entries.Add(DungeonEncounterTableEntry.Basic(78, 82, 2,
            "locust_swarm", 1, "1x Locust Swarm"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(83, 87, 2,
            "small_viper", 3, "3x Small Viper Snake"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(88, 90, 2,
            "dire_weasel", 1, "1x Dire Weasel"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 3 — Dungeon Level 3 (EL 3)
    //  Creatures: CR 1 to CR 3
    // =========================================================================
    private static DungeonEncounterTable BuildTable3()
    {
        var t = new DungeonEncounterTable(3);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        // CR 2-3 singles, CR 1 groups
        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 15, 3,
            "allip", 1, "1x Allip"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(16, 20, 3,
            "cockatrice", 1, "1x Cockatrice"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(21, 25, 3,
            "doppelganger", 1, "1x Doppelganger"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(26, 29, 3,
            "drow", 2, "2x Drow"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(30, 33, 3,
            "ethereal_filcher", 1, "1x Ethereal Filcher"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(34, 37, 3,
            "ethereal_marauder", 1, "1x Ethereal Marauder"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(38, 41, 3,
            "ettercap", 1, "1x Ettercap"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(42, 45, 3,
            "violet_fungus", 2, "2x Violet Fungus"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(46, 49, 3,
            "ghast", 1, "1x Ghast"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(50, 53, 3,
            "grick", 1, "1x Grick"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(54, 57, 3,
            "hell_hound", 1, "1x Hell Hound"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(58, 61, 3,
            "howler", 1, "1x Howler"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(62, 65, 3,
            "ogre", 1, "1x Ogre"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(66, 69, 3,
            "gelatinous_cube", 1, "1x Gelatinous Cube"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(70, 73, 3,
            "phantom_fungus", 1, "1x Phantom Fungus"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(74, 77, 3,
            "rust_monster", 1, "1x Rust Monster"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(78, 81, 3,
            "shadow", 1, "1x Shadow"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(82, 84, 3,
            "yuan_ti_pureblood", 1, "1x Yuan-ti Pureblood"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(85, 86, 3,
            "giant_praying_mantis", 1, "1x Giant Praying Mantis"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(87, 88, 3,
            "human_monk_3", 1, "1x Human Monk (3rd level)"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(89, 90, 3,
            "human_paladin_3", 1, "1x Human Paladin (3rd level)"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 4 — Dungeon Level 4 (EL 4)
    //  Creatures: CR 2 to CR 4
    // =========================================================================
    private static DungeonEncounterTable BuildTable4()
    {
        var t = new DungeonEncounterTable(4);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 14, 4,
            "barghest", 1, "1x Barghest"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(15, 18, 4,
            "hound_archon", 1, "1x Hound Archon"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(19, 22, 4,
            "carrion_crawler", 1, "1x Carrion Crawler"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(23, 26, 4,
            "displacer_beast", 1, "1x Displacer Beast"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(27, 30, 4,
            "gargoyle", 1, "1x Gargoyle"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(31, 34, 4,
            "janni", 1, "1x Janni"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(35, 38, 4,
            "ghoul", 3, "3x Ghoul"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(39, 42, 4,
            "svirfneblin", 2, "2x Svirfneblin"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(43, 46, 4,
            "grimlock", 3, "3x Grimlock"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(47, 50, 4,
            "harpy", 1, "1x Harpy"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(51, 54, 4,
            "mimic", 1, "1x Mimic"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(55, 58, 4,
            "minotaur", 1, "1x Minotaur"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(59, 62, 4,
            "gray_ooze", 1, "1x Grey Ooze"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(63, 66, 4,
            "otyugh", 1, "1x Otyugh"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(67, 70, 4,
            "owlbear", 1, "1x Owlbear"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(71, 74, 4,
            "centipede_swarm", 1, "1x Centipede Swarm"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(75, 78, 4,
            "vampire_spawn", 1, "1x Vampire Spawn"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(79, 82, 4,
            "duergar", 2, "2x Duergar"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(83, 86, 4,
            "viper_large", 1, "1x Large Viper Snake"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(87, 90, 4,
            "monstrous_spider_small", 4, "4x Small Monstrous Spider"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 5 — Dungeon Level 5 (EL 5)
    //  Creatures: CR 3 to CR 5
    // =========================================================================
    private static DungeonEncounterTable BuildTable5()
    {
        var t = new DungeonEncounterTable(5);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 14, 5,
            "basilisk", 1, "1x Basilisk"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(15, 18, 5,
            "greater_barghest", 1, "1x Greater Barghest"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(19, 22, 5,
            "celestial_lion", 1, "1x Celestial Lion"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(23, 26, 5,
            "cloaker", 1, "1x Cloaker"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(27, 30, 5,
            "bearded_devil", 1, "1x Bearded Devil"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(31, 34, 5,
            "djinni", 1, "1x Djinni"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(35, 38, 5,
            "gibbering_mouther", 1, "1x Gibbering Mouther"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(39, 42, 5,
            "hell_hound", 2, "2x Hell Hound"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(43, 46, 5,
            "manticore", 1, "1x Manticore"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(47, 50, 5,
            "mummy", 1, "1x Mummy"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(51, 54, 5,
            "ochre_jelly", 1, "1x Ochre Jelly"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(55, 58, 5,
            "phase_spider", 1, "1x Phase Spider"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(59, 62, 5,
            "shadow_mastiff", 2, "2x Shadow Mastiff"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(63, 66, 5,
            "skum", 3, "3x Skum"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(67, 70, 5,
            "troll", 1, "1x Troll"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(71, 74, 5,
            "vargouille", 3, "3x Vargouille"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(75, 78, 5,
            "wraith", 1, "1x Wraith"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(79, 82, 5,
            "yuan_ti_halfblood", 1, "1x Yuan-ti Halfblood"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(83, 85, 5,
            "giant_constrictor_snake", 1, "1x Giant Constrictor Snake"));
        t.Entries.Add(DungeonEncounterTableEntry.Classed(86, 87, 5,
            "hobgoblin", "Fighter", 5, 1, "1x Hobgoblin Fighter 5"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(88, 89, 5,
            "human_monk_5", 1, "1x Human Monk (5th level)"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(90, 90, 5,
            "human_paladin_5", 1, "1x Human Paladin (5th level)"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 6 — Dungeon Level 6 (EL 6)
    //  Creatures: CR 4 to CR 6
    // =========================================================================
    private static DungeonEncounterTable BuildTable6()
    {
        var t = new DungeonEncounterTable(6);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 14, 6,
            "babau", 1, "1x Babau"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(15, 18, 6,
            "derro", 3, "3x Derro"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(19, 22, 6,
            "chain_devil", 1, "1x Chain Devil"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(23, 26, 6,
            "digester", 1, "1x Digester"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(27, 30, 6,
            "displacer_beast", 2, "2x Displacer Beast"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(31, 34, 6,
            "bralani", 1, "1x Bralani"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(35, 38, 6,
            "ettin", 1, "1x Ettin"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(39, 42, 6,
            "formian_worker", 4, "4x Formian Worker"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(43, 46, 6,
            "gargoyle", 2, "2x Gargoyle"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(47, 50, 6,
            "ghast", 3, "3x Ghast"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(51, 54, 6,
            "grick", 3, "3x Grick"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(55, 58, 6,
            "harpy", 2, "2x Harpy"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(59, 62, 6,
            "howler", 2, "2x Howler"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(63, 66, 6,
            "shadow", 3, "3x Shadow"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(67, 70, 6,
            "shocker_lizard", 4, "4x Shocker Lizard"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(71, 74, 6,
            "xill", 1, "1x Xill"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(75, 78, 6,
            "minor_xorn", 1, "1x Minor Xorn"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(79, 82, 6,
            "yuan_ti_pureblood", 2, "2x Yuan-ti Pureblood"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(83, 86, 6,
            "giant_bombardier_beetle", 2, "2x Giant Bombardier Beetle"));
        t.Entries.Add(DungeonEncounterTableEntry.Classed(87, 90, 6,
            "lizardfolk", "Druid", 5, 1, "1x Lizardfolk Druid 5"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 7 — Dungeon Level 7 (EL 7)
    //  Creatures: CR 5 to CR 7
    // =========================================================================
    private static DungeonEncounterTable BuildTable7()
    {
        var t = new DungeonEncounterTable(7);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 14, 7,
            "aboleth", 1, "1x Aboleth"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(15, 18, 7,
            "chaos_beast", 1, "1x Chaos Beast"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(19, 22, 7,
            "chuul", 1, "1x Chuul"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(23, 26, 7,
            "succubus", 1, "1x Succubus"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(27, 30, 7,
            "hellcat", 1, "1x Hellcat"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(31, 34, 7,
            "drider", 1, "1x Drider"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(35, 38, 7,
            "shrieker", 4, "4x Shrieker"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(39, 42, 7,
            "hill_giant", 1, "1x Hill Giant"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(43, 46, 7,
            "flesh_golem", 1, "1x Flesh Golem"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(47, 50, 7,
            "invisible_stalker", 1, "1x Invisible Stalker"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(51, 54, 7,
            "manticore", 2, "2x Manticore"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(55, 58, 7,
            "medusa", 1, "1x Medusa"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(59, 62, 7,
            "minotaur", 2, "2x Minotaur"));
        t.Entries.Add(DungeonEncounterTableEntry.Classed(63, 66, 7,
            "ogre", "Barbarian", 4, 1, "1x Ogre Barbarian 4"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(67, 70, 7,
            "black_pudding", 1, "1x Black Pudding"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(71, 74, 7,
            "phasm", 1, "1x Phasm"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(75, 78, 7,
            "shadow_mastiff", 3, "3x Shadow Mastiff"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(79, 82, 7,
            "red_slaad", 1, "1x Red Slaad"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(83, 85, 7,
            "spectre", 1, "1x Spectre"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(86, 87, 7,
            "umber_hulk", 1, "1x Umber Hulk"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(88, 89, 7,
            "human_monk_7", 1, "1x Human Monk (7th level)"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(90, 90, 7,
            "human_paladin_7", 1, "1x Human Paladin (7th level)"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 8 — Dungeon Level 8 (EL 8)
    //  Creatures: CR 5 to CR 8+
    // =========================================================================
    private static DungeonEncounterTable BuildTable8()
    {
        var t = new DungeonEncounterTable(8);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 14, 8,
            "hound_archon", 2, "2x Hound Archon"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(15, 18, 8,
            "behir", 1, "1x Behir"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(19, 22, 8,
            "bodak", 1, "1x Bodak"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(23, 26, 8,
            "destrachan", 1, "1x Destrachan"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(27, 30, 8,
            "erinyes", 1, "1x Erinyes"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(31, 34, 8,
            "bralani", 2, "2x Bralani"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(35, 38, 8,
            "ettin", 2, "2x Ettin"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(39, 42, 8,
            "formian_taskmaster", 1, "1x Formian Taskmaster"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(43, 46, 8,
            "noble_djinni", 1, "1x Noble Djinni"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(47, 50, 8,
            "efreeti", 1, "1x Efreeti"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(51, 54, 8,
            "stone_giant", 1, "1x Stone Giant"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(55, 58, 8,
            "gorgon", 1, "1x Gorgon"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(59, 62, 8,
            "mind_flayer", 1, "1x Mind Flayer"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(63, 66, 8,
            "mohrg", 1, "1x Mohrg"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(67, 70, 8,
            "mummy", 2, "2x Mummy"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(71, 74, 8,
            "dark_naga", 1, "1x Dark Naga"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(75, 78, 8,
            "ogre_mage", 1, "1x Ogre Mage"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(79, 82, 8,
            "greater_shadow", 1, "1x Greater Shadow"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(83, 86, 8,
            "blue_slaad", 1, "1x Blue Slaad"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(87, 90, 8,
            "yuan_ti_halfblood", 2, "2x Yuan-ti Halfblood"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }
}
