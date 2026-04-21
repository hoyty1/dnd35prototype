using UnityEngine;

/// <summary>
/// Runtime tests for D&D 3.5e overrun targeting availability rules.
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

        TestCanUseOverrun_AllowsAdjacentLegalTargetWithoutWeapon();
        TestCanUseOverrun_RejectsTargetMoreThanOneSizeLarger();
        TestCanUseOverrun_RequiresAdjacentLegalTarget();

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

        // Register occupancy so space-validation checks are realistic.
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

    private static void TestCanUseOverrun_AllowsAdjacentLegalTargetWithoutWeapon()
    {
        var setup = BuildScenario(new Vector2Int(2, 2), new Vector2Int(3, 2));
        try
        {
            bool canUse = setup.gm.CanUseOverrun(setup.attacker, out string reason);
            Assert(canUse, "Overrun allowed with adjacent legal target (weapon not required)", $"reason='{reason}'");
        }
        finally
        {
            Cleanup(setup.attacker.gameObject, setup.defender.gameObject, setup.grid.gameObject, setup.gm.gameObject);
        }
    }

    private static void TestCanUseOverrun_RejectsTargetMoreThanOneSizeLarger()
    {
        var setup = BuildScenario(new Vector2Int(2, 2), new Vector2Int(3, 2));
        try
        {
            setup.attacker.Stats.SetBaseSizeCategory(SizeCategory.Small);
            setup.defender.Stats.SetBaseSizeCategory(SizeCategory.Huge);

            bool canUse = setup.gm.CanUseOverrun(setup.attacker, out string reason);
            Assert(!canUse, "Overrun blocked when only target is more than one size larger", $"reason='{reason}'");
        }
        finally
        {
            Cleanup(setup.attacker.gameObject, setup.defender.gameObject, setup.grid.gameObject, setup.gm.gameObject);
        }
    }

    private static void TestCanUseOverrun_RequiresAdjacentLegalTarget()
    {
        var setup = BuildScenario(new Vector2Int(2, 2), new Vector2Int(6, 2));
        try
        {
            bool canUse = setup.gm.CanUseOverrun(setup.attacker, out string reason);
            Assert(!canUse, "Overrun blocked when no adjacent legal target exists", $"reason='{reason}'");
        }
        finally
        {
            Cleanup(setup.attacker.gameObject, setup.defender.gameObject, setup.grid.gameObject, setup.gm.gameObject);
        }
    }
}
