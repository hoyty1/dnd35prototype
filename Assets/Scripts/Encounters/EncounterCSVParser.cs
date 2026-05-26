using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// One raw row from the dungeon encounters CSV file, before any parsing
/// of the encounter description string.
///
/// Phase 5: Random Encounter Generator.
/// </summary>
[Serializable]
public struct RawEncounterRow
{
    /// <summary>Dungeon table level (1-9).</summary>
    public int DungeonLevel;

    /// <summary>Minimum d% roll for this entry (inclusive, 1-100).</summary>
    public int RollMin;

    /// <summary>Maximum d% roll for this entry (inclusive, 1-100).</summary>
    public int RollMax;

    /// <summary>
    /// Raw encounter description string (e.g., "1d3 dire rats",
    /// "Roll on 2nd-level table", "5th-level human monk NPC").
    /// </summary>
    public string Encounter;

    /// <summary>CSV line number this row came from (for error reporting).</summary>
    public int SourceLine;

    public override string ToString()
    {
        return $"L{DungeonLevel} [{RollMin:D2}-{RollMax:D2}] {Encounter}";
    }
}

/// <summary>
/// Static utility class for reading the dungeon encounters CSV file into
/// structured <see cref="RawEncounterRow"/> data.
///
/// The CSV format is:
///   Dungeon_Level,Roll_Min,Roll_Max,Encounter
///   1,1,3,1d3 Medium monstrous centipedes (vermin)
///   7,36,38,"1 ghost, 5th-level fighter"
///
/// Handles:
///   - Header row skipping
///   - Quoted fields containing commas
///   - Malformed row logging and skipping
///   - Empty line skipping
///
/// Phase 5: Random Encounter Generator.
/// </summary>
public static class EncounterCSVParser
{
    /// <summary>
    /// Parse a dungeon encounters CSV file into a list of raw rows.
    /// </summary>
    /// <param name="csvPath">Absolute path to the CSV file.</param>
    /// <returns>List of parsed rows, empty if file not found or entirely malformed.</returns>
    public static List<RawEncounterRow> ParseCSV(string csvPath)
    {
        var rows = new List<RawEncounterRow>();

        if (string.IsNullOrEmpty(csvPath))
        {
            Debug.LogError("[EncounterCSVParser] CSV path is null or empty.");
            return rows;
        }

        if (!File.Exists(csvPath))
        {
            Debug.LogError($"[EncounterCSVParser] CSV file not found: {csvPath}");
            return rows;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(csvPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EncounterCSVParser] Failed to read CSV: {ex.Message}");
            return rows;
        }

        if (lines.Length < 2)
        {
            Debug.LogWarning("[EncounterCSVParser] CSV has no data rows (only header or empty).");
            return rows;
        }

        // Skip header row (line 0)
        int skipped = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            RawEncounterRow row;
            if (TryParseLine(line, i + 1, out row))
            {
                rows.Add(row);
            }
            else
            {
                skipped++;
            }
        }

        Debug.Log($"[EncounterCSVParser] Parsed {rows.Count} rows from CSV " +
                  $"({skipped} skipped) across {CountDistinctLevels(rows)} levels.");
        return rows;
    }

    /// <summary>
    /// Try to parse a single CSV line into a <see cref="RawEncounterRow"/>.
    /// Handles quoted fields for encounter descriptions that contain commas.
    /// </summary>
    /// <param name="line">Raw CSV line text.</param>
    /// <param name="lineNumber">1-based line number for error reporting.</param>
    /// <param name="row">Parsed row output.</param>
    /// <returns>True if successfully parsed, false if malformed.</returns>
    private static bool TryParseLine(string line, int lineNumber, out RawEncounterRow row)
    {
        row = new RawEncounterRow { SourceLine = lineNumber };

        // Parse CSV fields, handling quoted strings
        string[] fields = SplitCSVLine(line);

        if (fields.Length < 4)
        {
            Debug.LogWarning($"[EncounterCSVParser] Line {lineNumber}: Expected 4+ fields, " +
                             $"got {fields.Length}. Skipping: '{line}'");
            return false;
        }

        // Field 0: Dungeon_Level
        if (!int.TryParse(fields[0].Trim(), out row.DungeonLevel))
        {
            Debug.LogWarning($"[EncounterCSVParser] Line {lineNumber}: Invalid dungeon level " +
                             $"'{fields[0]}'. Skipping.");
            return false;
        }

        // Field 1: Roll_Min
        if (!int.TryParse(fields[1].Trim(), out row.RollMin))
        {
            Debug.LogWarning($"[EncounterCSVParser] Line {lineNumber}: Invalid roll min " +
                             $"'{fields[1]}'. Skipping.");
            return false;
        }

        // Field 2: Roll_Max
        if (!int.TryParse(fields[2].Trim(), out row.RollMax))
        {
            Debug.LogWarning($"[EncounterCSVParser] Line {lineNumber}: Invalid roll max " +
                             $"'{fields[2]}'. Skipping.");
            return false;
        }

        // Field 3+: Encounter description (may span remaining fields if improperly quoted)
        // Rejoin fields 3+ to handle any edge cases
        if (fields.Length == 4)
        {
            row.Encounter = fields[3].Trim();
        }
        else
        {
            // More than 4 fields — rejoin remaining with commas
            // (shouldn't happen with proper quoting, but defensive)
            var parts = new string[fields.Length - 3];
            Array.Copy(fields, 3, parts, 0, parts.Length);
            row.Encounter = string.Join(",", parts).Trim();
        }

        // Basic validation
        if (row.DungeonLevel < 1 || row.DungeonLevel > 20)
        {
            Debug.LogWarning($"[EncounterCSVParser] Line {lineNumber}: Dungeon level " +
                             $"{row.DungeonLevel} out of expected range 1-20. Skipping.");
            return false;
        }

        if (row.RollMin < 1 || row.RollMax > 100 || row.RollMin > row.RollMax)
        {
            Debug.LogWarning($"[EncounterCSVParser] Line {lineNumber}: Invalid d% range " +
                             $"[{row.RollMin}-{row.RollMax}]. Skipping.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(row.Encounter))
        {
            Debug.LogWarning($"[EncounterCSVParser] Line {lineNumber}: Empty encounter " +
                             $"description. Skipping.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Split a CSV line into fields, respecting quoted strings.
    /// Handles standard CSV quoting where fields containing commas are
    /// wrapped in double quotes: 7,36,38,"1 ghost, 5th-level fighter"
    /// </summary>
    /// <param name="line">Raw CSV line.</param>
    /// <returns>Array of field values with quotes stripped.</returns>
    private static string[] SplitCSVLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        int fieldStart = 0;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(ExtractField(line, fieldStart, i));
                fieldStart = i + 1;
            }
        }

        // Add the last field
        fields.Add(ExtractField(line, fieldStart, line.Length));

        return fields.ToArray();
    }

    /// <summary>
    /// Extract a field value from a CSV line, stripping surrounding quotes.
    /// </summary>
    private static string ExtractField(string line, int start, int end)
    {
        string field = line.Substring(start, end - start).Trim();

        // Strip surrounding quotes
        if (field.Length >= 2 && field[0] == '"' && field[field.Length - 1] == '"')
        {
            field = field.Substring(1, field.Length - 2);
        }

        return field;
    }

    /// <summary>
    /// Group raw rows by dungeon level.
    /// </summary>
    /// <param name="rows">List of raw encounter rows.</param>
    /// <returns>Dictionary mapping dungeon level to its rows.</returns>
    public static Dictionary<int, List<RawEncounterRow>> GroupByLevel(List<RawEncounterRow> rows)
    {
        var grouped = new Dictionary<int, List<RawEncounterRow>>();
        for (int i = 0; i < rows.Count; i++)
        {
            int level = rows[i].DungeonLevel;
            if (!grouped.ContainsKey(level))
                grouped[level] = new List<RawEncounterRow>();
            grouped[level].Add(rows[i]);
        }
        return grouped;
    }

    /// <summary>Count distinct dungeon levels in the row list.</summary>
    private static int CountDistinctLevels(List<RawEncounterRow> rows)
    {
        var levels = new HashSet<int>();
        for (int i = 0; i < rows.Count; i++)
            levels.Add(rows[i].DungeonLevel);
        return levels.Count;
    }

    /// <summary>
    /// Get a summary string of the CSV data for debug logging.
    /// </summary>
    /// <param name="rows">Parsed rows.</param>
    /// <returns>Multi-line summary string.</returns>
    public static string GetSummary(List<RawEncounterRow> rows)
    {
        var grouped = GroupByLevel(rows);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[EncounterCSVParser] CSV Summary: {rows.Count} rows, " +
                      $"{grouped.Count} levels");

        var levels = new List<int>(grouped.Keys);
        levels.Sort();
        for (int i = 0; i < levels.Count; i++)
        {
            int level = levels[i];
            var levelRows = grouped[level];
            int cascadeCount = 0;
            int encounterCount = 0;
            for (int j = 0; j < levelRows.Count; j++)
            {
                if (levelRows[j].Encounter.StartsWith("Roll on",
                    StringComparison.OrdinalIgnoreCase))
                    cascadeCount++;
                else
                    encounterCount++;
            }

            // Check d% coverage
            int minRoll = int.MaxValue;
            int maxRoll = int.MinValue;
            for (int j = 0; j < levelRows.Count; j++)
            {
                if (levelRows[j].RollMin < minRoll) minRoll = levelRows[j].RollMin;
                if (levelRows[j].RollMax > maxRoll) maxRoll = levelRows[j].RollMax;
            }
            string coverage = (minRoll == 1 && maxRoll == 100) ? "✓" : $"⚠ {minRoll}-{maxRoll}";

            sb.AppendLine($"  Level {level}: {levelRows.Count} entries " +
                          $"({encounterCount} encounters + {cascadeCount} cascades) " +
                          $"d%: {coverage}");
        }
        return sb.ToString();
    }
}
