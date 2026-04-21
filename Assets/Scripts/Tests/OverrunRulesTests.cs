using UnityEngine;

/// <summary>
/// Runtime tests for destination-based overrun prerequisites.
/// Run with OverrunRulesTests.RunAll().
/// </summary>
public static class OverrunRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== OVERRUN RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();

        TestCanUseOverrun_RequiresBothMoveAndStandardAction();
        TestCanUseOverrun_DoesNotRequireAdjacentEnemyTarget();
        TestCanUseOverrun_BlockedWhileProne();

        Debug.Log($"====== Overrun Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController CreateCharacter(string name, bool isPlayer, Vector2Int gridPos)
    {
        var go = new GameObject($"Overrun_{name}");
        var controller = go.AddComponent<CharacterController>();
        controller.Stats = new CharacterStats(name, 3, "Fighter", 16, 12, 12, 10, 10, 10, 3, 0, 0, 8, 1, 0, 8, 1, 22);
        controller.IsPlayerControlled = isPlayer;
        controller.GridPosition = gridPos;
        return controller;
    }

    private static (GameManager gm, SquareGrid grid, CharacterController attacker, CharacterController defender) BuildScenario(Vector2Int attackerPos, Vector2Int defenderPos)
    {
        var gmGo = new GameObject("OverrunRules_GameManager");
        var gm = gmGo.AddComponent<GameManager>();

        var gridGo = new GameObject("OverrunRules_Grid");
        var grid = gridGo.AddComponent<SquareGrid>();
        grid.Width = 10;
        grid.Height = 10;
        grid.GenerateGrid();

        gm.Grid = grid;

        CharacterController attacker = CreateCharacter("Attacker", true, attackerPos);
        CharacterController defender = CreateCharacter("Defender", false, defenderPos);

        gm.PCs.Add(attacker);
        gm.NPCs.Add(defender);

        grid.SetCreatureOccupancy(attacker, attacker.GridPosition, attacker.GetVisualSquaresOccupied());
        grid.SetCreatureOccupancy(defender, defender.GridPosition, defender.GetVisualSquaresOccupied());

        return (gm, grid, attacker, defender);
    }

    private static void Cleanup(params Object[] objects)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                Object.DestroyImmediate(objects[i]);
        }
    }

    private static void TestCanUseOverrun_RequiresBothMoveAndStandardAction()
    {
        var setup = BuildScenario(new Vector2Int(2, 2), new Vector2Int(5, 2));
        try
        {
            setup.attacker.Actions.UseMoveAction();
            bool canUseAfterMove = setup.gm.CanUseOverrun(setup.attacker, out string reasonAfterMove);
            Assert(!canUseAfterMove, "Overrun blocked when move action is already spent", $"reason='{reasonAfterMove}'");

            setup.attacker.Actions.Reset();
            setup.attacker.Actions.UseStandardAction();
            bool canUseAfterStandard = setup.gm.CanUseOverrun(setup.attacker, out string reasonAfterStandard);
            Assert(!canUseAfterStandard, "Overrun blocked when standard action is already spent", $"reason='{reasonAfterStandard}'");
        }
        finally
        {
            Cleanup(setup.attacker.gameObject, setup.defender.gameObject, setup.grid.gameObject, setup.gm.gameObject);
        }
    }

    private static void TestCanUseOverrun_DoesNotRequireAdjacentEnemyTarget()
    {
        var setup = BuildScenario(new Vector2Int(2, 2), new Vector2Int(8, 8));
        try
        {
            bool canUse = setup.gm.CanUseOverrun(setup.attacker, out string reason);
            Assert(canUse, "Overrun available without adjacent enemy target", $"reason='{reason}'");
        }
        finally
        {
            Cleanup(setup.attacker.gameObject, setup.defender.gameObject, setup.grid.gameObject, setup.gm.gameObject);
        }
    }

    private static void TestCanUseOverrun_BlockedWhileProne()
    {
        var setup = BuildScenario(new Vector2Int(2, 2), new Vector2Int(3, 2));
        try
        {
            setup.attacker.ApplyCondition(CombatConditionType.Prone, -1, "OverrunRulesTests");
            bool canUse = setup.gm.CanUseOverrun(setup.attacker, out string reason);
            Assert(!canUse, "Overrun blocked while prone", $"reason='{reason}'");
        }
        finally
        {
            Cleanup(setup.attacker.gameObject, setup.defender.gameObject, setup.grid.gameObject, setup.gm.gameObject);
        }
    }
}
