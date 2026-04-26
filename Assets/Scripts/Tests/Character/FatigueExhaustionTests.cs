using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// Runtime checks for D&D 3.5 fatigue/exhaustion behavior.
/// Run via FatigueExhaustionTests.RunAll().
/// </summary>
public static class FatigueExhaustionTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== FATIGUE / EXHAUSTION TESTS ======");

        RaceDatabase.Init();

        TestFatiguedAppliesMinusTwoStrengthAndDexterity();
        TestExhaustedAppliesMinusSixAndHalfSpeed();
        TestApplyingFatigueWhileFatiguedEscalatesToExhausted();
        TestRestRecoveryFromExhaustedToFatiguedToClear();

        Debug.Log($"====== Fatigue/Exhaustion Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats MakeChar(string name, int str = 16, int dex = 14)
    {
        return new CharacterStats(name, 5, "Fighter",
            str, dex, 14, 10, 10, 10,
            5, 0, 0,
            8, 1, 0,
            6, 1, 40,
            "Human");
    }

    private static void TestFatiguedAppliesMinusTwoStrengthAndDexterity()
    {
        CharacterStats stats = MakeChar("FatigueTarget", str: 16, dex: 14);

        int baseStrMod = CharacterStats.GetModifier(stats.STR);
        int baseDexMod = CharacterStats.GetModifier(stats.DEX);

        stats.ApplyCondition(CombatConditionType.Fatigued, 5, "Test");

        Assert(stats.IsFatiguedOrExhausted && !stats.IsExhaustedState,
            "Fatigued condition sets fatigued state without exhausted state");
        Assert(stats.STRMod == baseStrMod - 1,
            "Fatigued applies -2 STR (modifier drops by 1)",
            $"expected {baseStrMod - 1}, got {stats.STRMod}");
        Assert(stats.DEXMod == baseDexMod - 1,
            "Fatigued applies -2 DEX (modifier drops by 1)",
            $"expected {baseDexMod - 1}, got {stats.DEXMod}");
        Assert(!stats.CanCharge && !stats.CanRun,
            "Fatigued blocks charge and run");
    }

    private static void TestExhaustedAppliesMinusSixAndHalfSpeed()
    {
        CharacterStats stats = MakeChar("ExhaustedTarget", str: 16, dex: 14);

        int baseSpeedFeet = stats.EffectiveSpeedFeet;

        stats.ApplyCondition(CombatConditionType.Exhausted, 5, "Test");

        Assert(stats.IsExhaustedState,
            "Exhausted condition sets exhausted state");
        Assert(stats.STRMod == CharacterStats.GetModifier(stats.STR - 6),
            "Exhausted applies -6 STR");
        Assert(stats.DEXMod == CharacterStats.GetModifier(stats.DEX - 6),
            "Exhausted applies -6 DEX");
        Assert(stats.EffectiveSpeedFeet == baseSpeedFeet / 2,
            "Exhausted halves movement speed",
            $"expected {baseSpeedFeet / 2}, got {stats.EffectiveSpeedFeet}");
        Assert(!stats.CanCharge && !stats.CanRun,
            "Exhausted blocks charge and run");
    }

    private static void TestApplyingFatigueWhileFatiguedEscalatesToExhausted()
    {
        CharacterStats stats = MakeChar("EscalationTarget", str: 16, dex: 14);

        stats.ApplyCondition(CombatConditionType.Fatigued, 3, "First");
        stats.ApplyCondition(CombatConditionType.Fatigued, 3, "Second");

        Assert(stats.IsExhaustedState,
            "Second fatigued application escalates to exhausted");
        Assert(!stats.HasFatiguedCondition,
            "Exhausted supersedes fatigued condition");
    }

    private static void TestRestRecoveryFromExhaustedToFatiguedToClear()
    {
        CharacterStats stats = MakeChar("RestTarget", str: 16, dex: 14);
        stats.ApplyCondition(CombatConditionType.Exhausted, -1, "Test");

        stats.ApplyCompleteRest(1);
        Assert(!stats.IsExhaustedState && stats.IsFatiguedState,
            "1 hour rest converts exhausted to fatigued");

        stats.ApplyCompleteRest(8);
        Assert(!stats.IsFatiguedState && !stats.IsExhaustedState,
            "8 hours complete rest clears fatigued state");
    }
}

}
