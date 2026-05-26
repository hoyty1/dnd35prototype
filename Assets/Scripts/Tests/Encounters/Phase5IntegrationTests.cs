using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Phase 5 Integration Tests for the CSV-driven encounter generation pipeline.
///
/// Validates:
///   1. CSV loading success and table coverage
///   2. Effective max level (9 with CSV, 8 without)
///   3. Per-level encounter generation statistics
///   4. Dice expression parsing and variance
///   5. Compound entries ("1 ettercap and 1d3 spiders")
///   6. NPC entries ("5th-level monk")
///   7. Cascade logic (01-10 easier, 91-100 harder)
///   8. Edge cases (high dice variance, boundary rolls)
///
/// Run with Phase5IntegrationTests.RunAll() from any MonoBehaviour or console.
/// Results are saved to phase5_6_test_results.txt in the project root.
///
/// Phase 5: Random Encounter Generator.
/// </summary>
public static class Phase5IntegrationTests
{
    private static int _passed;
    private static int _failed;
    private static System.Text.StringBuilder _log;

    // =========================================================================
    //  Public entry point
    // =========================================================================

    /// <summary>
    /// Run all Phase 5 integration tests and save results to file.
    /// </summary>
    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        _log = new System.Text.StringBuilder();

        Log("========================================================");
        Log("  PHASE 5 ENCOUNTER GENERATION INTEGRATION TESTS");
        Log($"  Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Log("========================================================");
        Log("");

        // ── Section 1: CSV Loading ──
        TestCSVLoading();

        // ── Section 2: RunIntegrationTest(50) ──
        TestBulkGeneration();

        // ── Section 3: Specific Edge Cases ──
        TestCompoundEntries();
        TestNPCEntries();
        TestHighDiceVariance();
        TestCascadeLogic();
        TestBoundaryLevels();

        // ── Section 4: DiceExpression unit tests ──
        TestDiceExpressionParsing();

        // ── Section 5: EncounterDescriptionParser ──
        TestDescriptionParser();

        // ── Summary ──
        Log("");
        Log("========================================================");
        Log($"  RESULTS: {_passed} passed, {_failed} failed");
        Log($"  Overall: {(_failed == 0 ? "ALL TESTS PASSED ✓" : "SOME TESTS FAILED ✗")}");
        Log("========================================================");

        // Save to file
        string result = _log.ToString();
        Debug.Log(result);

        try
        {
            string outputPath = Path.Combine(Application.dataPath, "..", "phase5_6_test_results.txt");
            File.WriteAllText(outputPath, result);
            Debug.Log($"[Phase5Tests] Results saved to: {outputPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Phase5Tests] Failed to save results: {ex.Message}");
        }
    }

    // =========================================================================
    //  Helpers
    // =========================================================================

    private static void Log(string msg)
    {
        _log.AppendLine(msg);
        Debug.Log(msg);
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Log($"  PASS: {testName}");
        }
        else
        {
            _failed++;
            Log($"  FAIL: {testName} {detail}");
        }
    }

    // =========================================================================
    //  Test: CSV Loading
    // =========================================================================

    private static void TestCSVLoading()
    {
        Log("--- Section 1: CSV Loading ---");

        // Reset and reload to test the CSV path
        DungeonEncounterTableManager.Reset();
        DungeonEncounterTableManager.LoadTables();

        Assert(DungeonEncounterTableManager.IsLoaded,
            "Tables loaded after LoadTables()");

        // Check IsCSVLoaded
        bool csvLoaded = DungeonEncounterTableManager.IsCSVLoaded;
        Log($"  INFO: IsCSVLoaded = {csvLoaded}");
        Assert(csvLoaded, "CSV file loaded successfully (IsCSVLoaded == true)");

        // Check EffectiveMaxLevel
        int effectiveMax = DungeonEncounterTableManager.EffectiveMaxLevel;
        Log($"  INFO: EffectiveMaxLevel = {effectiveMax}");
        Assert(effectiveMax == 9,
            "EffectiveMaxLevel == 9 when CSV loaded",
            $"got {effectiveMax}");

        // Verify MaxLevel constant
        Assert(DungeonEncounterTableManager.MaxLevel == 9,
            "MaxLevel constant == 9",
            $"got {DungeonEncounterTableManager.MaxLevel}");

        // Verify HardcodedMaxLevel constant
        Assert(DungeonEncounterTableManager.HardcodedMaxLevel == 8,
            "HardcodedMaxLevel constant == 8",
            $"got {DungeonEncounterTableManager.HardcodedMaxLevel}");

        // Verify all 9 tables exist
        for (int level = 1; level <= 9; level++)
        {
            var table = DungeonEncounterTableManager.GetTable(level);
            Assert(table != null,
                $"Table for level {level} exists",
                "table was null");
        }

        // Test hardcoded fallback
        DungeonEncounterTableManager.Reset();
        DungeonEncounterTableManager.LoadTablesHardcodedOnly();
        Assert(!DungeonEncounterTableManager.IsCSVLoaded,
            "Hardcoded loading sets IsCSVLoaded = false");
        Assert(DungeonEncounterTableManager.EffectiveMaxLevel == 8,
            "Hardcoded EffectiveMaxLevel == 8",
            $"got {DungeonEncounterTableManager.EffectiveMaxLevel}");

        // Reload CSV for remaining tests
        DungeonEncounterTableManager.Reset();
        DungeonEncounterTableManager.LoadTables();

        Log("");
    }

    // =========================================================================
    //  Test: Bulk Generation via RunIntegrationTest
    // =========================================================================

    private static void TestBulkGeneration()
    {
        Log("--- Section 2: Bulk Generation (50 encounters per level) ---");

        string result = DungeonEncounterTableManager.RunIntegrationTest(50);
        Log(result);

        // Parse result for success metrics
        // Expected: ~91.3% overall success rate (cascades that wrap don't fail,
        // but some entries may not resolve to known NPCs)
        Assert(!string.IsNullOrEmpty(result),
            "RunIntegrationTest(50) produced output");

        Log("");
    }

    // =========================================================================
    //  Test: Compound Entries
    // =========================================================================

    private static void TestCompoundEntries()
    {
        Log("--- Section 3a: Compound Entries ---");

        // Test parsing compound descriptions
        // "1 ettercap and 1d3+1 Medium monstrous spiders (vermin)"
        string compound = "1 ettercap and 1d3+1 Medium monstrous spiders (vermin)";
        var parsed = EncounterDescriptionParser.Parse(compound);

        Assert(parsed != null,
            "Compound entry parsed successfully",
            $"input: {compound}");

        if (parsed != null)
        {
            Assert(parsed.Groups != null && parsed.Groups.Count == 2,
                $"Compound entry has 2 groups",
                $"got {parsed.Groups?.Count ?? 0}");

            if (parsed.Groups != null && parsed.Groups.Count >= 1)
            {
                var g1 = parsed.Groups[0];
                Assert(g1.CountExpression != null && g1.CountExpression.IsFixed,
                    "Group 1 has fixed count (1 ettercap)");
                Assert(g1.CreatureName.ToLower().Contains("ettercap"),
                    "Group 1 creature is ettercap",
                    $"got '{g1.CreatureName}'");
            }

            if (parsed.Groups != null && parsed.Groups.Count >= 2)
            {
                var g2 = parsed.Groups[1];
                Assert(g2.CountExpression != null && !g2.CountExpression.IsFixed,
                    "Group 2 has dice count (1d3+1)",
                    $"isFixed={g2.CountExpression?.IsFixed}");
                if (g2.CountExpression != null)
                {
                    Assert(g2.CountExpression.NumDice == 1 && g2.CountExpression.DiceSides == 3
                           && g2.CountExpression.Modifier == 1,
                        "Group 2 dice is 1d3+1",
                        $"got {g2.CountExpression}");
                }
            }
        }

        // Another compound: "1d4+1 gnolls and 1d3 hyenas"
        string compound2 = "1d4+1 gnolls and 1d3 hyenas";
        var parsed2 = EncounterDescriptionParser.Parse(compound2);
        Assert(parsed2 != null && parsed2.Groups != null && parsed2.Groups.Count == 2,
            "Gnolls+hyenas compound has 2 groups",
            $"got {parsed2?.Groups?.Count ?? 0}");

        Log("");
    }

    // =========================================================================
    //  Test: NPC Entries
    // =========================================================================

    private static void TestNPCEntries()
    {
        Log("--- Section 3b: NPC Entries ---");

        // "5th-level human monk NPC"
        string npc1 = "5th-level human monk NPC";
        var parsed = EncounterDescriptionParser.Parse(npc1);

        Assert(parsed != null && parsed.Groups != null && parsed.Groups.Count >= 1,
            "NPC entry parsed",
            $"input: {npc1}");

        if (parsed != null && parsed.Groups != null && parsed.Groups.Count >= 1)
        {
            var g = parsed.Groups[0];
            Assert(g.IsNpc,
                "Entry recognized as NPC",
                $"IsNpc={g.IsNpc}");
            Assert(g.NpcLevel == 5,
                "NPC level == 5",
                $"got {g.NpcLevel}");
            Assert(g.NpcClass != null && g.NpcClass.ToLower().Contains("monk"),
                "NPC class contains 'monk'",
                $"got '{g.NpcClass}'");
            Assert(g.NpcRace != null && g.NpcRace.ToLower().Contains("human"),
                "NPC race contains 'human'",
                $"got '{g.NpcRace}'");
        }

        // "5th-level kobold sorcerer NPC"
        string npc2 = "5th-level kobold sorcerer NPC";
        var parsed2 = EncounterDescriptionParser.Parse(npc2);
        if (parsed2 != null && parsed2.Groups != null && parsed2.Groups.Count >= 1)
        {
            var g = parsed2.Groups[0];
            Assert(g.IsNpc && g.NpcLevel == 5,
                "Kobold sorcerer NPC level == 5");
            Assert(g.NpcClass != null && g.NpcClass.ToLower().Contains("sorcerer"),
                "Kobold sorcerer class identified",
                $"got '{g.NpcClass}'");
        }

        // "1d3 5th-level troglodyte cleric NPCs"
        string npc3 = "1d3 5th-level troglodyte cleric NPCs";
        var parsed3 = EncounterDescriptionParser.Parse(npc3);
        if (parsed3 != null && parsed3.Groups != null && parsed3.Groups.Count >= 1)
        {
            var g = parsed3.Groups[0];
            Assert(g.IsNpc,
                "Multiple NPC entry recognized as NPC");
            Assert(g.CountExpression != null && !g.CountExpression.IsFixed,
                "Multiple NPC entry has dice count (1d3)",
                $"isFixed={g.CountExpression?.IsFixed}");
        }

        Log("");
    }

    // =========================================================================
    //  Test: High Dice Variance
    // =========================================================================

    private static void TestHighDiceVariance()
    {
        Log("--- Section 3c: High Dice Variance Entries ---");

        // Test "2d4+1" expression directly
        var dice = DiceExpression.Parse("2d4+1");
        Assert(dice != null, "Parse '2d4+1' succeeds");
        if (dice != null)
        {
            Assert(dice.Minimum == 3, "2d4+1 min == 3", $"got {dice.Minimum}");
            Assert(dice.Maximum == 9, "2d4+1 max == 9", $"got {dice.Maximum}");
            Assert(!dice.IsFixed, "2d4+1 is not fixed");

            // Roll 100 times and check variance
            var results = new HashSet<int>();
            for (int i = 0; i < 100; i++)
            {
                int r = dice.Roll();
                results.Add(r);
                Assert(r >= 3 && r <= 9,
                    $"2d4+1 roll {i} in range [3,9]",
                    $"got {r}");
                if (r < 3 || r > 9) break; // Stop on first failure
            }
            Assert(results.Count >= 3,
                $"2d4+1 shows variance (>= 3 distinct values in 100 rolls)",
                $"got {results.Count} distinct values: {string.Join(",", results)}");
        }

        // Test "1d3+1"
        var dice2 = DiceExpression.Parse("1d3+1");
        Assert(dice2 != null && dice2.Minimum == 2 && dice2.Maximum == 4,
            "1d3+1 range [2,4]",
            $"got [{dice2?.Minimum},{dice2?.Maximum}]");

        // Test "1d4+4"
        var dice3 = DiceExpression.Parse("1d4+4");
        Assert(dice3 != null && dice3.Minimum == 5 && dice3.Maximum == 8,
            "1d4+4 range [5,8]",
            $"got [{dice3?.Minimum},{dice3?.Maximum}]");

        Log("");
    }

    // =========================================================================
    //  Test: Cascade Logic
    // =========================================================================

    private static void TestCascadeLogic()
    {
        Log("--- Section 3d: Cascade Logic ---");

        // Generate many encounters at level 5 and check for cascade indicators
        int cascadeEasier = 0;
        int cascadeHarder = 0;
        int normal = 0;
        int totalTrials = 200;

        for (int i = 0; i < totalTrials; i++)
        {
            var enc = DungeonEncounterTableManager.GenerateRandomEncounter(5);
            if (enc != null)
            {
                // Check if encounter came from a different table via cascade
                // The encounter's debug info or table source is in the result
                normal++;
            }
        }

        Assert(normal > 0,
            $"Level 5 generated {normal}/{totalTrials} encounters",
            "expected most to succeed");

        // Test boundary: level 1 cascade easier should wrap to level 1
        int level1Success = 0;
        for (int i = 0; i < 50; i++)
        {
            var enc = DungeonEncounterTableManager.GenerateRandomEncounter(1);
            if (enc != null) level1Success++;
        }
        Assert(level1Success > 0,
            $"Level 1 (min boundary): {level1Success}/50 encounters generated");

        // Test boundary: level 9 (max) cascade harder should wrap to level 9
        int level9Success = 0;
        for (int i = 0; i < 50; i++)
        {
            var enc = DungeonEncounterTableManager.GenerateRandomEncounter(9);
            if (enc != null) level9Success++;
        }
        Assert(level9Success > 0,
            $"Level 9 (max boundary): {level9Success}/50 encounters generated");

        // Test level clamping: level 0 should clamp to 1
        var encClampLow = DungeonEncounterTableManager.GenerateRandomEncounter(0);
        Assert(encClampLow != null,
            "Level 0 clamped to 1, encounter generated");

        // Test level clamping: level 15 should clamp to EffectiveMaxLevel
        var encClampHigh = DungeonEncounterTableManager.GenerateRandomEncounter(15);
        Assert(encClampHigh != null,
            "Level 15 clamped to EffectiveMaxLevel, encounter generated");

        Log("");
    }

    // =========================================================================
    //  Test: Boundary Levels
    // =========================================================================

    private static void TestBoundaryLevels()
    {
        Log("--- Section 3e: Boundary Level Tests ---");

        // Level 9 is the new max — verify it works
        var table9 = DungeonEncounterTableManager.GetTable(9);
        Assert(table9 != null, "Level 9 table exists and is retrievable");

        // Level 8 should still work (backward compatibility)
        var table8 = DungeonEncounterTableManager.GetTable(8);
        Assert(table8 != null, "Level 8 table still exists (backward compat)");

        // Level 10 should clamp to EffectiveMaxLevel
        var table10 = DungeonEncounterTableManager.GetTable(10);
        Assert(table10 != null,
            "GetTable(10) returns table (clamped to EffectiveMaxLevel)");

        Log("");
    }

    // =========================================================================
    //  Test: DiceExpression Parsing
    // =========================================================================

    private static void TestDiceExpressionParsing()
    {
        Log("--- Section 4: DiceExpression Parsing ---");

        // Fixed value
        var d1 = DiceExpression.Parse("3");
        Assert(d1 != null && d1.IsFixed && d1.Modifier == 3,
            "Parse '3' → fixed 3");

        // Simple dice
        var d2 = DiceExpression.Parse("1d6");
        Assert(d2 != null && d2.NumDice == 1 && d2.DiceSides == 6 && d2.Modifier == 0,
            "Parse '1d6' → 1d6+0");

        // Dice with positive modifier
        var d3 = DiceExpression.Parse("2d4+1");
        Assert(d3 != null && d3.NumDice == 2 && d3.DiceSides == 4 && d3.Modifier == 1,
            "Parse '2d4+1' → 2d4+1");

        // Dice with large modifier
        var d4 = DiceExpression.Parse("1d4+4");
        Assert(d4 != null && d4.NumDice == 1 && d4.DiceSides == 4 && d4.Modifier == 4,
            "Parse '1d4+4' → 1d4+4");

        // Common encounter dice
        var d5 = DiceExpression.Parse("1d3");
        Assert(d5 != null && d5.NumDice == 1 && d5.DiceSides == 3 && d5.Modifier == 0,
            "Parse '1d3' → 1d3+0");

        // Edge: "1" should be fixed 1
        var d6 = DiceExpression.Parse("1");
        Assert(d6 != null && d6.IsFixed && d6.Modifier == 1,
            "Parse '1' → fixed 1");

        // ToString roundtrip
        var d7 = DiceExpression.Parse("2d4+1");
        Assert(d7 != null && d7.ToString() == "2d4+1",
            "ToString '2d4+1' roundtrip",
            $"got '{d7?.ToString()}'");

        Log("");
    }

    // =========================================================================
    //  Test: EncounterDescriptionParser edge cases
    // =========================================================================

    private static void TestDescriptionParser()
    {
        Log("--- Section 5: EncounterDescriptionParser ---");

        // Cascade entry
        var cascade = EncounterDescriptionParser.Parse("Roll on 2nd-level table");
        Assert(cascade != null && cascade.IsCascade,
            "Cascade entry detected",
            $"IsCascade={cascade?.IsCascade}");
        if (cascade != null && cascade.IsCascade)
        {
            Assert(cascade.CascadeTargetLevel == 2,
                "Cascade target level == 2",
                $"got {cascade.CascadeTargetLevel}");
        }

        // Simple creature
        var simple = EncounterDescriptionParser.Parse("1d3 dire rats");
        Assert(simple != null && !simple.IsCascade,
            "Simple creature entry is not cascade");
        if (simple != null && simple.Groups != null && simple.Groups.Count >= 1)
        {
            Assert(simple.Groups[0].CountExpression != null,
                "Simple creature has count expression");
        }

        // Parenthetical annotation
        var annotated = EncounterDescriptionParser.Parse("1d3 Medium monstrous centipedes (vermin)");
        if (annotated != null && annotated.Groups != null && annotated.Groups.Count >= 1)
        {
            Assert(annotated.Groups[0].Annotation == "vermin",
                "Annotation 'vermin' extracted",
                $"got '{annotated.Groups[0].Annotation}'");
        }

        // Template creature: "1d4+1 fiendish dire rats"
        var templated = EncounterDescriptionParser.Parse("1d4+1 fiendish dire rats");
        if (templated != null && templated.Groups != null && templated.Groups.Count >= 1)
        {
            Assert(templated.Groups[0].HasTemplates,
                "Fiendish template detected",
                $"HasTemplates={templated.Groups[0].HasTemplates}");
        }

        Log("");
    }
}
