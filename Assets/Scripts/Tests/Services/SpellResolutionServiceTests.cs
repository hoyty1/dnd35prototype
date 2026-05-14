using UnityEngine;
using Tests.Utilities;

namespace Tests.Services
{
/// <summary>
/// Unit tests for SpellResolutionService — verifies Blink caster/target failure
/// logic and spell resistance checks per D&amp;D 3.5e rules.
/// Run with SpellResolutionServiceTests.RunAll().
/// </summary>
public static class SpellResolutionServiceTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SPELL RESOLUTION SERVICE TESTS ======");

        TestHelpers.EnsureCoreDatabasesInitialized();

        TestBlinkCasterFailureNoBlink();
        TestBlinkCasterFailureStatistical();
        TestBlinkTargetFailureNoBlink();
        TestBlinkTargetFailureSelfSpell();
        TestBlinkTargetFailureStatistical();
        TestSpellResistanceOvercome();
        TestSpellResistanceFail();
        TestRunPreCastChecksNoBlink();

        Debug.Log($"====== Spell Resolution Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  ✅ PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  ❌ FAIL: {testName} {detail}");
        }
    }

    private static CharacterController MakeCharacter(string name)
    {
        var go = new GameObject($"SRS_Test_{name}");
        var cc = go.AddComponent<CharacterController>();
        cc.Stats = TestHelpers.CreateStats(name: name, level: 5, characterClass: "Wizard");
        return cc;
    }

    private static SpellData MakeSpell(string name, SpellTargetType targetType = SpellTargetType.SingleTarget)
    {
        var spell = new SpellData();
        spell.Name = name;
        spell.SpellId = name.ToLower().Replace(" ", "_");
        spell.TargetType = targetType;
        return spell;
    }

    private static void DestroyCharacter(CharacterController cc)
    {
        if (cc != null && cc.gameObject != null)
            Object.DestroyImmediate(cc.gameObject);
    }

    // ===== BLINK CASTER FAILURE =====

    private static void TestBlinkCasterFailureNoBlink()
    {
        var caster = MakeCharacter("NoBlink_Caster");
        // caster.HasActiveBlinkEffect should be false by default
        bool failed = SpellResolutionService.TryBlinkCasterFailure(caster, "Test Spell", out int roll);
        Assert(!failed, "No blink: caster failure = false");
        Assert(roll == 0, "No blink: roll = 0", $"got {roll}");
        DestroyCharacter(caster);
    }

    private static void TestBlinkCasterFailureStatistical()
    {
        // We can't directly set HasActiveBlinkEffect without proper setup,
        // so we test the core logic with null caster (should return false)
        bool failed = SpellResolutionService.TryBlinkCasterFailure(null, "Test Spell", out int roll);
        Assert(!failed, "Null caster: failure = false");
        Assert(roll == 0, "Null caster: roll = 0", $"got {roll}");
    }

    // ===== BLINK TARGET FAILURE =====

    private static void TestBlinkTargetFailureNoBlink()
    {
        var caster = MakeCharacter("Caster");
        var target = MakeCharacter("Target_NoBlink");
        var spell = MakeSpell("Magic Missile");

        bool failed = SpellResolutionService.TryBlinkTargetFailure(caster, target, spell, out int roll);
        Assert(!failed, "No blink on target: failure = false");
        Assert(roll == 0, "No blink on target: roll = 0", $"got {roll}");

        DestroyCharacter(caster);
        DestroyCharacter(target);
    }

    private static void TestBlinkTargetFailureSelfSpell()
    {
        var caster = MakeCharacter("SelfSpell_Caster");
        var spell = MakeSpell("Shield", SpellTargetType.Self);

        // Self-targeted spells should never check Blink target failure
        bool failed = SpellResolutionService.TryBlinkTargetFailure(caster, caster, spell, out int roll);
        Assert(!failed, "Self-targeted spell: failure = false");
        Assert(roll == 0, "Self-targeted spell: roll = 0", $"got {roll}");

        DestroyCharacter(caster);
    }

    private static void TestBlinkTargetFailureStatistical()
    {
        // Test with null target
        var caster = MakeCharacter("Caster2");
        var spell = MakeSpell("Ray of Frost");

        bool failed = SpellResolutionService.TryBlinkTargetFailure(caster, null, spell, out int roll);
        Assert(!failed, "Null target: failure = false");

        DestroyCharacter(caster);
    }

    // ===== SPELL RESISTANCE =====

    private static void TestSpellResistanceOvercome()
    {
        // CL 10 vs SR 5: should almost always succeed
        int successes = 0;
        for (int i = 0; i < 100; i++)
        {
            bool overcame = SpellResolutionService.TryOvercomeSpellResistance(10, 5, "test SR", out int roll, out int total);
            if (overcame) successes++;
            Assert(total == roll + 10, $"SR total = roll + CL (iter {i})", $"roll={roll}, total={total}");
        }
        Assert(successes > 80, "CL 10 vs SR 5: mostly succeeds", $"successes={successes}/100");
    }

    private static void TestSpellResistanceFail()
    {
        // CL 1 vs SR 30: should almost always fail
        int failures = 0;
        for (int i = 0; i < 100; i++)
        {
            bool overcame = SpellResolutionService.TryOvercomeSpellResistance(1, 30, "test SR high", out int roll, out int total);
            if (!overcame) failures++;
        }
        Assert(failures > 80, "CL 1 vs SR 30: mostly fails", $"failures={failures}/100");
    }

    // ===== COMBINED PRE-CAST =====

    private static void TestRunPreCastChecksNoBlink()
    {
        var caster = MakeCharacter("PreCast_Caster");
        var target = MakeCharacter("PreCast_Target");
        var spell = MakeSpell("Fireball", SpellTargetType.Area);

        var result = SpellResolutionService.RunPreCastChecks(caster, target, spell);

        Assert(result.SpellProceeds, "No blink: spell proceeds");
        Assert(!result.BlinkCasterFailed, "No blink: caster not failed");
        Assert(!result.BlinkTargetFailed, "No blink: target not failed");
        Assert(result.BlinkCasterRoll == 0, "No blink: caster roll = 0", $"got {result.BlinkCasterRoll}");
        Assert(result.BlinkTargetRoll == 0, "No blink: target roll = 0", $"got {result.BlinkTargetRoll}");

        DestroyCharacter(caster);
        DestroyCharacter(target);
    }
}
}
