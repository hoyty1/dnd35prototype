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
            TestLineTargetingUsesClickedEndpoint();

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

        private static void TestLineTargetingUsesClickedEndpoint()
        {
            GameObject gridObj = null;
            try
            {
                gridObj = new GameObject("GustOfWindRulesTests_Grid");
                SquareGrid grid = gridObj.AddComponent<SquareGrid>();
                grid.Width = 35;
                grid.Height = 35;
                grid.GenerateGrid();

                Vector2Int origin = new Vector2Int(12, 12);

                Vector2Int endpoint = new Vector2Int(20, 13);
                HashSet<Vector2Int> lineCells = AoESystem.GetLineCellsToTarget(origin, endpoint, 12, grid);

                Assert(lineCells.Contains(endpoint),
                    "Line targeting includes clicked endpoint cell");

                bool hasEastOnlySnap = lineCells.Contains(new Vector2Int(13, 12)) && !lineCells.Contains(new Vector2Int(13, 13));
                Assert(!hasEastOnlySnap,
                    "Line targeting no longer snaps to 8 fixed directions");

                int furthestDistance = 0;
                foreach (Vector2Int cell in lineCells)
                    furthestDistance = Mathf.Max(furthestDistance, SquareGridUtils.GetDistance(origin, cell));

                Assert(furthestDistance == 12,
                    "Line targeting extends to the full 60-ft length from trajectory",
                    $"furthestDistance={furthestDistance}");

                int clickedDistance = SquareGridUtils.GetDistance(origin, endpoint);
                bool hasCellsBeyondClickedPoint = false;
                foreach (Vector2Int cell in lineCells)
                {
                    if (SquareGridUtils.GetDistance(origin, cell) > clickedDistance)
                    {
                        hasCellsBeyondClickedPoint = true;
                        break;
                    }
                }

                Assert(hasCellsBeyondClickedPoint,
                    "Line targeting affects cells beyond the clicked square when endpoint is closer than 60 ft");

                Vector2Int outOfRangeEndpoint = new Vector2Int(30, 12);
                HashSet<Vector2Int> outOfRangeCells = AoESystem.GetLineCellsToTarget(origin, outOfRangeEndpoint, 12, grid);
                Assert(outOfRangeCells.Count == 0,
                    "Line targeting rejects endpoints outside 60-ft range");
            }
            finally
            {
                if (gridObj != null)
                    UnityEngine.Object.DestroyImmediate(gridObj);
            }
        }
    }
}
