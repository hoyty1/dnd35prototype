using UnityEngine;

namespace Tests.Maneuvers
{
/// <summary>
/// Runtime tests for Coup de Grace availability and core rule guards.
/// Run with CoupDeGraceRulesTests.RunAll().
/// </summary>
public static class CoupDeGraceRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== COUP DE GRACE RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();

        TestCanUseCoupDeGrace_RequiresFullRoundAction();
        TestCanUseCoupDeGrace_RequiresAdjacentHelplessEnemy();
        TestExecuteCoupDeGrace_FailsAgainstCriticalImmuneTarget();
        TestExecuteCoupDeGrace_SucceedsAgainstHelplessNonImmuneTarget();

        Debug.Log($"====== Coup de Grace Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController CreateCharacter(string name, bool isPlayer, Vector2Int gridPos, string creatureType = "Humanoid")
    {
        var go = new GameObject($"CdG_{name}");
        var controller = go.AddComponent<CharacterController>();
        controller.Stats = new CharacterStats(name, 5, "Fighter", 16, 12, 12, 10, 10, 10, 5, 0, 0, 10, 1, 0, 8, 2, 34)
        {
            CreatureType = creatureType
        };
        controller.IsPlayerControlled = isPlayer;
        controller.GridPosition = gridPos;
        return controller;
    }

    private static (GameManager gm, SquareGrid grid, CharacterController attacker, CharacterController defender) BuildScenario(Vector2Int attackerPos, Vector2Int defenderPos, string defenderCreatureType = "Humanoid")
    {
        var gmGo = new GameObject("CdGRules_GameManager");
        var gm = gmGo.AddComponent<GameManager>();

        var gridGo = new GameObject("CdGRules_Grid");
        var grid = gridGo.AddComponent<SquareGrid>();
        grid.Width = 10;
        grid.Height = 10;
        grid.GenerateGrid();

        gm.Grid = grid;

        CharacterController attacker = CreateCharacter("Attacker", true, attackerPos);
        CharacterController defender = CreateCharacter("Defender", false, defenderPos, defenderCreatureType);

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

    private static void TestCanUseCoupDeGrace_RequiresFullRoundAction()
    {
        var setup = BuildScenario(new Vector2Int(2, 2), new Vector2Int(3, 2));
        try
        {
            setup.defender.ApplyCondition(CombatConditionType.Unconscious, -1, "CoupDeGraceRulesTests");
            setup.attacker.Actions.UseStandardAction();

            bool canUse = setup.gm.CanUseCoupDeGraceAttackOption(setup.attacker);
            Assert(!canUse, "Coup de Grace blocked when full-round action is unavailable");
        }
        finally
        {
            Cleanup(setup.attacker.gameObject, setup.defender.gameObject, setup.grid.gameObject, setup.gm.gameObject);
        }
    }

    private static void TestCanUseCoupDeGrace_RequiresAdjacentHelplessEnemy()
    {
        var setup = BuildScenario(new Vector2Int(2, 2), new Vector2Int(3, 2));
        try
        {
            bool canUseWithoutHelpless = setup.gm.CanUseCoupDeGraceAttackOption(setup.attacker);
            Assert(!canUseWithoutHelpless, "Coup de Grace blocked when adjacent enemy is not helpless");

            setup.defender.ApplyCondition(CombatConditionType.Unconscious, -1, "CoupDeGraceRulesTests");
            bool canUseWithHelpless = setup.gm.CanUseCoupDeGraceAttackOption(setup.attacker);
            Assert(canUseWithHelpless, "Coup de Grace enabled when adjacent enemy is helpless");
        }
        finally
        {
            Cleanup(setup.attacker.gameObject, setup.defender.gameObject, setup.grid.gameObject, setup.gm.gameObject);
        }
    }

    private static void TestExecuteCoupDeGrace_FailsAgainstCriticalImmuneTarget()
    {
        var setup = BuildScenario(new Vector2Int(2, 2), new Vector2Int(3, 2), defenderCreatureType: "Undead");
        try
        {
            setup.defender.ApplyCondition(CombatConditionType.Unconscious, -1, "CoupDeGraceRulesTests");

            SpecialAttackResult result = setup.attacker.ExecuteSpecialAttack(SpecialAttackType.CoupDeGrace, setup.defender);
            Assert(!result.Success, "Coup de Grace fails against crit-immune target", $"log='{result.Log}'");
        }
        finally
        {
            Cleanup(setup.attacker.gameObject, setup.defender.gameObject, setup.grid.gameObject, setup.gm.gameObject);
        }
    }

    private static void TestExecuteCoupDeGrace_SucceedsAgainstHelplessNonImmuneTarget()
    {
        var setup = BuildScenario(new Vector2Int(2, 2), new Vector2Int(3, 2), defenderCreatureType: "Humanoid");
        try
        {
            setup.defender.ApplyCondition(CombatConditionType.Unconscious, -1, "CoupDeGraceRulesTests");

            SpecialAttackResult result = setup.attacker.ExecuteSpecialAttack(SpecialAttackType.CoupDeGrace, setup.defender);
            bool passed = result.Success && result.DamageDealt > 0;
            Assert(passed, "Coup de Grace applies automatic critical damage to helpless valid target", $"success={result.Success} damage={result.DamageDealt} log='{result.Log}'");
        }
        finally
        {
            Cleanup(setup.attacker.gameObject, setup.defender.gameObject, setup.grid.gameObject, setup.gm.gameObject);
        }
    }
}
}
