using System.Collections.Generic;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// Runtime smoke tests for combat XP award math (DMG p.36-39, Table 2-6; CHR-004).
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
        TestAwardTableLookup();
        TestAwardTableEdges();
        TestEqualLevelParty();
        TestDmgMixedLevelExample();

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

    private static void AssertAward(int level, float cr, float expected, string note = "")
    {
        float got = ExperienceCalculator.GetBaseXPAward(level, cr);
        Assert(Mathf.Abs(got - expected) < 0.01f, $"Table 2-6: level {level}, CR {ChallengeRatingUtils.Format(cr)} = {expected} {note}".TrimEnd(), $"got {got}");
    }

    // DMG Table 2-6 (p.38): the award scales with the character's level (CHR-004).
    private static void TestAwardTableLookup()
    {
        AssertAward(1, 1f, 300f, "(levels 1-3 share a row)");
        AssertAward(3, 3f, 900f);
        AssertAward(1, 3f, 900f);
        AssertAward(4, 4f, 1200f);
        AssertAward(5, 5f, 1500f, "(300 x level for CR = level)");
        AssertAward(8, 8f, 2400f);
        AssertAward(7, 1f, 263f);
        AssertAward(6, 2f, 450f);
        AssertAward(1, 10f, 10800f);
        AssertAward(13, 20f, 46800f);
    }

    private static void TestAwardTableEdges()
    {
        // DMG p.37: below CR 1, the CR 1 award times the fraction.
        AssertAward(1, 0.5f, 150f, "(CR 1/2: half the CR 1 award)");
        AssertAward(8, 0.25f, 50f, "(CR 1/4)");
        // DMG p.38 note: above CR 20, double the award for a CR two lower.
        AssertAward(14, 21f, 50400f, "(double CR 19)");
        AssertAward(20, 22f, 12000f, "(double CR 20)");
        // Blank cells (open owner question, CHR-004; recommendation until the owner rules): "*" gives 0 and "**" caps
        // each monster at the award for a CR 7 above the row's level. The other reading of DMG p.39 (clamp the
        // encounter's total to the row's minimum and maximum) is not applied.
        AssertAward(9, 1f, 0f, "(* cell)");
        AssertAward(9, 0.5f, 0f, "(* cell, fraction)");
        AssertAward(1, 11f, 10800f, "(** cell capped at CR 10)");
        AssertAward(4, 14f, 12800f, "(** cell capped at CR 11)");
        AssertAward(14, 22f, 50400f, "(** cell capped at CR 21, the doubling note past CR 20)");
    }

    // Three 4th-level characters defeat a CR 4 monster: 1,200 / 3 = 400 each (the old CR - APL table gave 100).
    private static void TestEqualLevelParty()
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

        int a = result.CharacterXPAward.TryGetValue(party[0], out int v) ? v : -1;
        Assert(a == 400, "CR 4 vs three 4th-level PCs gives 400 XP each (1,200 / 3)", $"expected 400, got {a}");
        Assert(result.CharacterXPGained.TryGetValue(party[0], out int g) && g == 400, "The single-class PC gains its full share", $"got {g}");
        Assert(party[0].Stats.ExperiencePoints == ExperienceCalculator.GetXPForLevel(4) + 400, "AddExperience applied the share", $"XP {party[0].Stats.ExperiencePoints}");

        CleanupCharacter(enemy);
        for (int i = 0; i < party.Count; i++)
            CleanupCharacter(party[i]);

        Object.DestroyImmediate(go);
    }

    // The DMG p.37 example: levels 3, 4, 4, 4, 5 defeat two CR 2 monsters and a CR 3 monster.
    private static void TestDmgMixedLevelExample()
    {
        var party = new List<CharacterController>
        {
            BuildCharacter("L3", 3),
            BuildCharacter("L4a", 4),
            BuildCharacter("L4b", 4),
            BuildCharacter("L4c", 4),
            BuildCharacter("L5", 5)
        };
        var enemies = new List<CharacterController>
        {
            BuildCharacter("CR2a", 1, challengeRating: "2"),
            BuildCharacter("CR2b", 1, challengeRating: "2"),
            BuildCharacter("CR3", 1, challengeRating: "3")
        };

        ExperienceCalculator.CombatXPResult result = ExperienceCalculator.PreviewXPForCombat(party, enemies);
        int[] expected = { 420, 400, 400, 400, 350 };
        for (int i = 0; i < party.Count; i++)
        {
            int got = result.CharacterXPAward.TryGetValue(party[i], out int v) ? v : -1;
            Assert(got == expected[i], $"DMG p.37 example: {party[i].Stats.CharacterName} earns {expected[i]} XP", $"got {got}");
        }
        Assert(result.CharacterBaseXP.TryGetValue(party[0], out int b) && b == 2100, "DMG p.37 example: the 3rd-level base total is 2,100", $"got {b}");
        Assert(result.PartySize == 5 && result.CharacterXPGained.Count == 0, "A preview divides by 5 and awards nothing", $"party {result.PartySize}, gained {result.CharacterXPGained.Count}");

        for (int i = 0; i < party.Count; i++)
            CleanupCharacter(party[i]);
        for (int i = 0; i < enemies.Count; i++)
            CleanupCharacter(enemies[i]);
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
