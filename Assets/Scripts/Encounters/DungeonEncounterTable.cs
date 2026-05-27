using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One of the eight DMG 3.5e dungeon encounter tables (levels 1-8).
/// Contains d% entries and supports rolling to select an encounter.
///
/// Each table has:
///   - Entries 01-10 → cascade to easier table (level - 1)
///   - Entries 11-90 → actual encounter entries for this level
///   - Entries 91-100 → cascade to harder table (level + 1)
///
/// Phase 3: DMG Encounter Tables.
/// </summary>
[Serializable]
public class DungeonEncounterTable
{
    /// <summary>Dungeon level this table serves (1-8).</summary>
    public int DungeonLevel;

    /// <summary>Encounter Level target for this table (usually equals DungeonLevel).</summary>
    public int TargetEL;

    /// <summary>All entries in this table, sorted by MinRoll ascending.</summary>
    public List<DungeonEncounterTableEntry> Entries = new List<DungeonEncounterTableEntry>();

    /// <summary>Display name for the table.</summary>
    public string Name => $"Dungeon Level {DungeonLevel} Encounter Table (EL {TargetEL})";

    public DungeonEncounterTable() { }

    public DungeonEncounterTable(int dungeonLevel)
    {
        DungeonLevel = dungeonLevel;
        TargetEL = dungeonLevel;
    }

    /// <summary>
    /// Find the entry matching the given d% roll (1-100).
    /// Returns null if no entry matches (table construction error).
    /// </summary>
    public DungeonEncounterTableEntry GetEntryByRoll(int roll)
    {
        roll = Mathf.Clamp(roll, 1, 100);
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].MatchesRoll(roll))
                return Entries[i];
        }

        Debug.LogWarning($"[DungeonEncounterTable] No entry found for roll {roll} on {Name}. " +
                         $"Table has {Entries.Count} entries.");
        return null;
    }

    /// <summary>
    /// Roll a d% (1d100) and return the matching entry.
    /// Does NOT handle cascade — that is done by DungeonEncounterTableManager.
    /// </summary>
    public DungeonEncounterTableEntry RollEncounter()
    {
        int roll = DiceRoller.D100(); // 1-100 inclusive
        Debug.Log($"[DungeonEncounterTable] Rolled {roll} on {Name}");
        return GetEntryByRoll(roll);
    }

    /// <summary>Number of non-cascade entries (actual encounters).</summary>
    public int EncounterEntryCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Entries.Count; i++)
                if (!Entries[i].IsCascade) count++;
            return count;
        }
    }

    /// <summary>Total number of entries including cascade redirects.</summary>
    public int TotalEntryCount => Entries.Count;

    /// <summary>
    /// Validate that the table covers the full 1-100 range with no gaps or overlaps.
    /// Returns a list of issues found (empty if valid).
    /// </summary>
    public List<string> Validate()
    {
        var issues = new List<string>();

        if (Entries.Count == 0)
        {
            issues.Add($"Table {DungeonLevel}: No entries.");
            return issues;
        }

        // Sort by MinRoll
        Entries.Sort((a, b) => a.MinRoll.CompareTo(b.MinRoll));

        // Check coverage
        int expectedNext = 1;
        for (int i = 0; i < Entries.Count; i++)
        {
            var e = Entries[i];
            if (e.MinRoll != expectedNext)
            {
                issues.Add($"Table {DungeonLevel}: Gap or overlap at roll {expectedNext}, " +
                           $"entry starts at {e.MinRoll}.");
            }
            if (e.MaxRoll < e.MinRoll)
            {
                issues.Add($"Table {DungeonLevel}: Invalid range [{e.MinRoll}-{e.MaxRoll}].");
            }
            expectedNext = e.MaxRoll + 1;
        }

        if (expectedNext != 101)
        {
            issues.Add($"Table {DungeonLevel}: Table ends at {expectedNext - 1}, expected 100.");
        }

        // Check cascade entries exist
        bool hasEasier = false, hasHarder = false;
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].Cascade == CascadeDirection.Easier) hasEasier = true;
            if (Entries[i].Cascade == CascadeDirection.Harder) hasHarder = true;
        }

        if (!hasEasier)
            issues.Add($"Table {DungeonLevel}: Missing cascade-easier entry (01-10).");
        if (!hasHarder)
            issues.Add($"Table {DungeonLevel}: Missing cascade-harder entry (91-100).");

        return issues;
    }

    /// <summary>
    /// Print the full table to Debug.Log for inspection.
    /// </summary>
    public void DebugPrint()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== {Name} ({Entries.Count} entries) ===");
        for (int i = 0; i < Entries.Count; i++)
        {
            sb.AppendLine($"  {Entries[i]}");
        }
        Debug.Log(sb.ToString());
    }
}
