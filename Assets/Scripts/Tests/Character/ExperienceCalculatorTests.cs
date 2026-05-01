using System.Collections.Generic;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// Runtime smoke tests for combat XP award math (DMG 3.5e table).
/// </summary>
public static class ExperienceCalculatorTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== EXPERIENCE CALCULATOR TESTS ======");

        TestLevelThresholdFormula();
        TestCrDifferenceTableLookup();

        Debug.Log($"====== Experience Calculator Results: {_passed} passed, {_failed} failed ======");
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

    private static void TestLevelThresholdFormula()
    {
        Assert(ExperienceCalculator.GetXPForLevel(1) == 0, "Level 1 threshold", "expected 0");
        Assert(ExperienceCalculator.GetXPForLevel(2) == 1000, "Level 2 threshold", "expected 1000");
        Assert(ExperienceCalculator.GetXPForLevel(3) == 3000, "Level 3 threshold", "expected 3000");
        Assert(ExperienceCalculator.GetXPForLevel(4) == 6000, "Level 4 threshold", "expected 6000");
        Assert(ExperienceCalculator.GetXPForLevel(5) == 10000, "Level 5 threshold", "expected 10000");
    }

    private static void TestCrDifferenceTableLookup()
    {
        var go = new GameObject("XPTestCalc");
        var calc = go.AddComponent<ExperienceCalculator>();

        var party = new List<CharacterController>
        {
            BuildCharacter("A", 4),
            BuildCharacter("B", 4),
            BuildCharacter("C", 4)
        };

        var enemy = BuildCharacter("CR4 Enemy", 1, challengeRating: "4");
        var result = calc.CalculateXPForCombat(party, new List<CharacterController> { enemy });

        Assert(result.TotalXPPerCharacter == 100, "CR = APL with 3 PCs gives 100 XP each", $"expected 100, got {result.TotalXPPerCharacter}");

        CleanupCharacter(enemy);
        for (int i = 0; i < party.Count; i++)
            CleanupCharacter(party[i]);

        Object.DestroyImmediate(go);
    }

    private static CharacterController BuildCharacter(string name, int level, string challengeRating = null)
    {
        var go = new GameObject($"XPTest_{name}");
        var cc = go.AddComponent<CharacterController>();

        var stats = new CharacterStats(name, level, "Fighter",
            14, 12, 12, 10, 10, 10,
            level, 0, 0,
            8, 1, 0,
            6, 1, 10 + (level * 4),
            "Human");

        stats.Level = level;
        stats.ChallengeRating = challengeRating;
        cc.Stats = stats;
        cc.ConfigureTeamControl(CharacterTeam.Player, controllable: true);

        return cc;
    }

    private static void CleanupCharacter(CharacterController cc)
    {
        if (cc != null)
            Object.DestroyImmediate(cc.gameObject);
    }
}
}
