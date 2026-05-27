using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for CombatLogHelper — verifies color wrapping, named
/// constants, and semantic formatting helpers produce correct rich text.
/// Run with CombatLogHelperTests.RunAll().
/// </summary>
public static class CombatLogHelperTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== COMBAT LOG HELPER TESTS ======");

        TestColorWrap();
        TestDamageFormat();
        TestSuccessFormat();
        TestFailureFormat();
        TestInfoFormat();
        TestConditionFadedFormat();
        TestSaveResult_Success();
        TestSaveResult_Failure();
        TestSpellResistedFormat();
        TestNoEffectFormat();

        Debug.Log($"====== CombatLogHelper Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    private static void TestColorWrap()
    {
        string result = CombatLogHelper.Color("hello", "FF0000");
        Assert(result == "<color=#FF0000>hello</color>", "Color wrap", $"got: {result}");
    }

    private static void TestDamageFormat()
    {
        string result = CombatLogHelper.Damage("💥", "Goblin takes 5 fire damage");
        Assert(result.Contains("<color=#FF8888>") && result.Contains("💥"), "Damage format", $"got: {result}");
    }

    private static void TestSuccessFormat()
    {
        string result = CombatLogHelper.Success("✅", "Heal lands!");
        Assert(result.Contains("<color=#88FF88>") && result.Contains("✅"), "Success format", $"got: {result}");
    }

    private static void TestFailureFormat()
    {
        string result = CombatLogHelper.Failure("❌", "Spell fizzles");
        Assert(result.Contains("<color=#FF6666>"), "Failure format", $"got: {result}");
    }

    private static void TestInfoFormat()
    {
        string result = CombatLogHelper.Info("✦", "Nothing happens.");
        Assert(result.Contains("<color=#AAAAAA>"), "Info format", $"got: {result}");
    }

    private static void TestConditionFadedFormat()
    {
        string result = CombatLogHelper.ConditionFaded("🛡", "Alaric", "Shield of Faith");
        Assert(result.Contains("Alaric") && result.Contains("Shield of Faith") && result.Contains("fades"),
            "ConditionFaded format", $"got: {result}");
    }

    private static void TestSaveResult_Success()
    {
        string result = CombatLogHelper.SaveResult("Goblin", true, "Will", 18, 15);
        Assert(result.Contains("<color=#88FF88>") && result.Contains("makes"),
            "SaveResult success", $"got: {result}");
    }

    private static void TestSaveResult_Failure()
    {
        string result = CombatLogHelper.SaveResult("Goblin", false, "Reflex", 8, 15);
        Assert(result.Contains("<color=#FF6666>") && result.Contains("fails"),
            "SaveResult failure", $"got: {result}");
    }

    private static void TestSpellResistedFormat()
    {
        string result = CombatLogHelper.SpellResisted("Orc", "Hold Person");
        Assert(result.Contains("Orc") && result.Contains("Hold Person") && result.Contains("resists"),
            "SpellResisted format", $"got: {result}");
    }

    private static void TestNoEffectFormat()
    {
        string result = CombatLogHelper.NoEffect("✦", "Bless", "Skeleton", "undead");
        Assert(result.Contains("Skeleton") && result.Contains("unaffected") && result.Contains("undead"),
            "NoEffect format", $"got: {result}");
    }
}
}
