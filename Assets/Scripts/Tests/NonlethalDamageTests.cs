using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Focused checks for nonlethal damage tracking and HP-state transitions.
/// Run via NonlethalDamageTests.RunAll() from any runtime MonoBehaviour test hook.
/// </summary>
public static class NonlethalDamageTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== NONLETHAL DAMAGE TESTS ==========");

        TestNonlethalEqualHPBecomesStaggered();
        TestNonlethalExceedsHPBecomesUnconscious();
        TestHealRemovesNonlethalBeforeLethal();

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    private static void Assert(bool condition, string testName)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}");
        }
    }

    private static CharacterController CreateTestCharacter(string name, int hp)
    {
        var go = new GameObject($"{name}_GO");
        var controller = go.AddComponent<CharacterController>();
        var stats = new CharacterStats(name, 2, "Fighter", 14, 12, 12, 10, 10, 10, 2, 0, 0, 8, 1, 0, 4, 1, 12);
        stats.CurrentHP = hp;
        controller.Init(stats, Vector2Int.zero, null, null);
        return controller;
    }

    private static void Cleanup(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static void TestNonlethalEqualHPBecomesStaggered()
    {
        Debug.Log("--- Test: Nonlethal == current HP -> Staggered ---");
        var c = CreateTestCharacter("StaggerTest", 10);

        var packet = new DamagePacket
        {
            RawDamage = 10,
            Types = new HashSet<DamageType> { DamageType.Bludgeoning },
            IsRanged = false,
            IsNonlethal = true,
            Source = AttackSource.Weapon,
            SourceName = "sap"
        };

        c.Stats.ApplyIncomingDamage(10, packet);

        Assert(c.Stats.CurrentHP == 10, "Nonlethal damage should not reduce lethal HP");
        Assert(c.Stats.NonlethalDamage == 10, "Nonlethal pool should increase by final nonlethal damage");
        Assert(c.CurrentHPState == HPState.Staggered, "Equal nonlethal/current HP should set Staggered state");

        Cleanup(c);
    }

    private static void TestNonlethalExceedsHPBecomesUnconscious()
    {
        Debug.Log("--- Test: Nonlethal > current HP -> Unconscious ---");
        var c = CreateTestCharacter("UnconsciousTest", 10);

        var packet = new DamagePacket
        {
            RawDamage = 11,
            Types = new HashSet<DamageType> { DamageType.Bludgeoning },
            IsRanged = false,
            IsNonlethal = true,
            Source = AttackSource.Weapon,
            SourceName = "club"
        };

        c.Stats.ApplyIncomingDamage(11, packet);

        Assert(c.Stats.CurrentHP == 10, "Unconscious from nonlethal should still keep lethal HP unchanged");
        Assert(c.Stats.NonlethalDamage == 11, "Nonlethal pool should exceed current HP");
        Assert(c.CurrentHPState == HPState.Unconscious, "Nonlethal exceeding HP should set Unconscious state");
        Assert(!c.CanTakeTurnActions(), "Unconscious character should not be able to take turn actions");

        Cleanup(c);
    }

    private static void TestHealRemovesNonlethalBeforeLethal()
    {
        Debug.Log("--- Test: Healing removes nonlethal first ---");
        var c = CreateTestCharacter("HealOrderTest", 10);

        c.Stats.TakeDamage(4);         // 10 -> 6 lethal HP
        c.Stats.ApplyNonlethalDamage(3);

        int nonlethalHealed;
        int hpHealed = c.Stats.HealDamage(5, out nonlethalHealed);

        Assert(nonlethalHealed == 3, "Healing should remove nonlethal damage first");
        Assert(hpHealed == 2, "Remaining healing should restore lethal HP");
        Assert(c.Stats.NonlethalDamage == 0, "Nonlethal pool should be reduced to 0");
        Assert(c.Stats.CurrentHP == 8, "Current HP should increase only after nonlethal is cleared");

        Cleanup(c);
    }
}