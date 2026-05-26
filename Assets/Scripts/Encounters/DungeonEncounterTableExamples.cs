using UnityEngine;

/// <summary>
/// Example usage and test scenarios for the Phase 3 dungeon encounter table system.
/// Demonstrates LoadTables, GetTable, GenerateRandomEncounter, cascade behavior,
/// and integration with Phase 2's spawner pipeline.
///
/// Phase 3: DMG Encounter Tables.
/// </summary>
public static class DungeonEncounterTableExamples
{
    /// <summary>
    /// Run all examples. Call from a MonoBehaviour or editor script.
    /// </summary>
    public static void RunAll()
    {
        Debug.Log("========================================");
        Debug.Log("  Phase 3: Encounter Table Examples");
        Debug.Log("========================================");

        Example1_BasicTableSetup();
        Example2_SpecificRolls();
        Example3_CascadeBehavior();
        Example4_FullPipeline();
        Example5_BatchTest();
        Example6_CSVLoading();

        Debug.Log("========================================");
        Debug.Log("  All Phase 3 examples complete.");
        Debug.Log("========================================");
    }

    /// <summary>
    /// Example 1: Basic table loading and inspection.
    /// </summary>
    public static void Example1_BasicTableSetup()
    {
        Debug.Log("\n--- Example 1: Basic Table Setup ---");

        // Load all 8 tables
        DungeonEncounterTableManager.LoadTables();

        // Get a specific table
        var table3 = DungeonEncounterTableManager.GetTable(3);
        Debug.Log($"Table 3: {table3.Name}");
        Debug.Log($"  Total entries: {table3.TotalEntryCount}");
        Debug.Log($"  Encounter entries: {table3.EncounterEntryCount}");

        // Print all entries for table 3
        table3.DebugPrint();
    }

    /// <summary>
    /// Example 2: Look up encounters for specific d% rolls.
    /// </summary>
    public static void Example2_SpecificRolls()
    {
        Debug.Log("\n--- Example 2: Specific Roll Lookups ---");

        if (!DungeonEncounterTableManager.IsLoaded)
            DungeonEncounterTableManager.LoadTables();

        // Roll 50 on table 5 — should get a mid-table encounter
        var enc = DungeonEncounterTableManager.GenerateEncounterForRoll(5, 50);
        if (enc != null)
            Debug.Log($"Roll 50 on table 5: {enc.GetPreview()}");

        // Roll 5 on table 4 — should cascade to table 3 (easier)
        enc = DungeonEncounterTableManager.GenerateEncounterForRoll(4, 5);
        if (enc != null)
            Debug.Log($"Roll 5 on table 4 (cascade easier): {enc.GetPreview()}");

        // Roll 95 on table 6 — should cascade to table 7 (harder)
        enc = DungeonEncounterTableManager.GenerateEncounterForRoll(6, 95);
        if (enc != null)
            Debug.Log($"Roll 95 on table 6 (cascade harder): {enc.GetPreview()}");
    }

    /// <summary>
    /// Example 3: Cascade boundary behavior.
    /// </summary>
    public static void Example3_CascadeBehavior()
    {
        Debug.Log("\n--- Example 3: Cascade Boundaries ---");

        if (!DungeonEncounterTableManager.IsLoaded)
            DungeonEncounterTableManager.LoadTables();

        // Roll 5 on table 1 — cascade easier, but at min level → wraps to table 1
        var enc = DungeonEncounterTableManager.GenerateEncounterForRoll(1, 5);
        if (enc != null)
            Debug.Log($"Roll 5 on table 1 (at min boundary): {enc.GetPreview()}");

        // Roll 95 on table 8 — cascade harder, but at max level → wraps to table 8
        enc = DungeonEncounterTableManager.GenerateEncounterForRoll(8, 95);
        if (enc != null)
            Debug.Log($"Roll 95 on table 8 (at max boundary): {enc.GetPreview()}");
    }

    /// <summary>
    /// Example 4: Full pipeline — table roll → EncounterDefinition → SpawnResult.
    /// This demonstrates Phase 3 feeding directly into Phase 2.
    /// </summary>
    public static void Example4_FullPipeline()
    {
        Debug.Log("\n--- Example 4: Full Phase 2+3 Pipeline ---");

        if (!DungeonEncounterTableManager.IsLoaded)
            DungeonEncounterTableManager.LoadTables();

        // Generate and prepare in one call
        var result = DungeonEncounterTableManager.GenerateAndPrepare(dungeonLevel: 4, partyLevel: 4);

        if (result.IsValid)
        {
            Debug.Log($"Encounter prepared successfully!");
            Debug.Log($"  Source: {result.Source.GetPreview()}");
            Debug.Log($"  NPCs spawned: {result.Count}");
            Debug.Log($"  Enemy IDs: {string.Join(", ", result.EnemyIds)}");

            if (result.Warnings.Count > 0)
                Debug.LogWarning($"  Warnings: {string.Join("; ", result.Warnings)}");
        }
        else
        {
            Debug.LogWarning("Encounter preparation failed or partially resolved.");
            if (result.Warnings.Count > 0)
                Debug.LogWarning($"  Warnings: {string.Join("; ", result.Warnings)}");
        }
    }

    /// <summary>
    /// Example 5: Batch random encounters for distribution testing.
    /// </summary>
    public static void Example5_BatchTest()
    {
        Debug.Log("\n--- Example 5: Batch Test ---");

        if (!DungeonEncounterTableManager.IsLoaded)
            DungeonEncounterTableManager.LoadTables();

        // Generate 5 random encounters for dungeon level 6
        DungeonEncounterTableManager.RunTestBatch(dungeonLevel: 6, count: 5);
    }

    /// <summary>
    /// Example 6: Loading from CSV file (with typo correction).
    /// </summary>
    public static void Example6_CSVLoading()
    {
        Debug.Log("\n--- Example 6: CSV Loading ---");

        // Load tables + CSV name mapping
        DungeonEncounterTableManager.Reset();
        DungeonEncounterTableManager.LoadFromCSV("Assets/Data/dungeon_encounters.csv");

        // Generate an encounter (uses the same table data, CSV adds name resolution)
        var enc = DungeonEncounterTableManager.GenerateRandomEncounter(3);
        if (enc != null)
            Debug.Log($"Encounter from CSV-enhanced tables: {enc.GetPreview()}");
    }
}
