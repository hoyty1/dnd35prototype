using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
    /// <summary>
    /// Regression checks for Gust of Wind spell definition and core targeting/condition rules.
    /// Run with GustOfWindRulesTests.RunAll().
    /// </summary>
    public static class GustOfWindRulesTests
    {
        private static int _passed;
        private static int _failed;

        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            Debug.Log("====== GUST OF WIND RULES TESTS ======");

            SpellDatabase.Init();

            TestSpellDefinition();
            TestCheckedConditionRepresentsBlockedMovement();
            TestLineTargetingSnapsToEightDirections();

            Debug.Log($"====== Gust of Wind Rules Results: {_passed} passed, {_failed} failed ======");
        }

        private static void Assert(bool condition, string testName, string detail = "")
        {
            if (condition)
            {
                _passed++;
                Debug.Log($"  PASS: {testName}");
            }
            else
            {
                _failed++;
                Debug.LogError($"  FAIL: {testName} {detail}");
            }
        }

        private static void TestSpellDefinition()
        {
            SpellData spell = SpellDatabase.GetSpell(SpellNames.GUST_OF_WIND);
            Assert(spell != null, "Gust of Wind spell exists");
            if (spell == null)
                return;

            Assert(spell.SpellLevel == 2, "Gust of Wind is level 2");
            Assert(spell.TargetType == SpellTargetType.Area && spell.AoEShapeType == AoEShape.Line,
                "Gust of Wind is a line area spell",
                $"targetType={spell.TargetType}, shape={spell.AoEShapeType}");
            Assert(spell.AoESizeSquares == 12 && spell.RangeSquares == 12,
                "Gust of Wind range/line length is 60 ft (12 squares)",
                $"range={spell.RangeSquares}, line={spell.AoESizeSquares}");
            Assert(spell.AllowsSavingThrow && string.Equals(spell.SavingThrowType, "Fortitude", StringComparison.OrdinalIgnoreCase),
                "Gust of Wind uses Fortitude save");
            Assert(spell.SpellResistanceApplies, "Gust of Wind allows spell resistance");

            var classes = new HashSet<string>(spell.ClassList ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            Assert(classes.Contains("Wizard") && classes.Contains("Sorcerer") && classes.Contains("Druid"),
                "Gust of Wind is available to Wizard, Sorcerer, and Druid",
                $"classes=[{string.Join(", ", classes)}]");
        }

        private static void TestCheckedConditionRepresentsBlockedMovement()
        {
            ConditionDefinition checkedDef = ConditionRules.GetDefinition(CombatConditionType.Checked);
            Assert(checkedDef != null, "Checked condition definition exists");
            if (checkedDef == null)
                return;

            Assert(checkedDef.PreventsMovement,
                "Checked condition blocks movement for Gust of Wind medium-target handling");
            Assert(Mathf.Approximately(checkedDef.MovementMultiplier, 0f),
                "Checked condition movement multiplier is 0");
        }

        private static void TestLineTargetingSnapsToEightDirections()
        {
            GameObject gridObj = null;
            try
            {
                gridObj = new GameObject("GustOfWindRulesTests_Grid");
                SquareGrid grid = gridObj.AddComponent<SquareGrid>();
                grid.Width = 25;
                grid.Height = 25;
                grid.GenerateGrid();

                Vector2Int origin = new Vector2Int(12, 12);

                // Slightly east-leaning aim should snap to EAST.
                HashSet<Vector2Int> eastCells = AoESystem.GetLineCellsFromDirection(origin, new Vector2(20f, 12.3f), 12, grid);
                bool hasEast = eastCells.Contains(new Vector2Int(13, 12));
                bool hasNorthEast = eastCells.Contains(new Vector2Int(13, 13));
                Assert(hasEast && !hasNorthEast,
                    "Line targeting snaps near-east input to EAST lane");

                // Clear NE aim should snap to NORTHEAST diagonal.
                HashSet<Vector2Int> neCells = AoESystem.GetLineCellsFromDirection(origin, new Vector2(20f, 20f), 12, grid);
                bool hasNeStep = neCells.Contains(new Vector2Int(13, 13));
                Assert(hasNeStep,
                    "Line targeting supports diagonal snap (NE)");
            }
            finally
            {
                if (gridObj != null)
                    UnityEngine.Object.DestroyImmediate(gridObj);
            }
        }
    }
}
