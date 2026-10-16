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

                // A 60-ft. line starts at the corner of the caster's square nearest the square aimed at, runs through that
                // square's centre and extends to its full length (PHB p.175; batch 11 step 24, decision D5).
                Vector2Int caster = new Vector2Int(12, 12);
                Vector2Int endpoint = new Vector2Int(20, 13);
                bool aimed = AoESystem.TryAimLine(caster, 1, new Vector2(endpoint.x, endpoint.y), 12, grid,
                    out GridIntersection start, out HashSet<Vector2Int> lineCells);

                Assert(aimed && start == new GridIntersection(13, 13),
                    "Line starts at the caster's corner nearest the aimed square", $"aimed={aimed} start={start}");
                Assert(lineCells.Contains(endpoint),
                    "Line targeting includes clicked endpoint cell");
                Assert(!lineCells.Contains(caster),
                    "Line never covers the caster's own square");

                int furthestCount = 0;
                foreach (Vector2Int cell in lineCells)
                    furthestCount = Mathf.Max(furthestCount, AoESystem.CountToFarCorner(start, cell));
                Assert(furthestCount == 12,
                    "Line targeting extends to the full 60-ft length from trajectory",
                    $"furthestCount={furthestCount}");

                bool hasCellsBeyondClickedPoint = false;
                foreach (Vector2Int cell in lineCells)
                {
                    if (cell.x > endpoint.x)
                    {
                        hasCellsBeyondClickedPoint = true;
                        break;
                    }
                }
                Assert(hasCellsBeyondClickedPoint,
                    "Line targeting affects cells beyond the clicked square when endpoint is closer than 60 ft");

                // Aimed at a square 90 ft. away the line still covers its first 60 ft.: 12 squares of row 12.
                HashSet<Vector2Int> farCells = AoESystem.GetLineCells(new GridIntersection(13, 13), new Vector2(30f, 12f), 12, grid);
                bool allRow12 = true;
                foreach (Vector2Int cell in farCells)
                    allRow12 &= cell.y == 12 && cell.x >= 13 && cell.x <= 24;
                Assert(farCells.Count == 12 && allRow12,
                    "A line aimed beyond 60 ft. covers its full 60 ft. (12 squares)", $"count={farCells.Count}");

                Assert(!AoESystem.TryAimLine(caster, 1, new Vector2(12.2f, 11.9f), 12, grid, out _, out _),
                    "A line cannot be aimed at a point inside the caster's square");
            }
            finally
            {
                if (gridObj != null)
                    UnityEngine.Object.DestroyImmediate(gridObj);
            }
        }
    }
}
