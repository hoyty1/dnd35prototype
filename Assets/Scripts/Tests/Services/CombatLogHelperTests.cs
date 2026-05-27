using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for CombatLogHelper — verifies color wrapping, named
/// constants, and semantic formatting helpers produce correct rich text.
/// Run with CombatLogHelperTests.RunAll().
///
/// All methods are pure static string formatters — no mocks needed.
/// Verifies Unity rich-text <color=#{hex}> tags and content composition.
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

        // Core formatting
        TestColorWrap();
        TestColorWrap_EmptyText();

        // Semantic formatters (positive)
        TestSuccessFormat();
        TestHealingFormat();
        TestSpellResistedFormat();
        TestBuffFormat();
        TestSpecialFormat();

        // Semantic formatters (negative)
        TestDamageFormat();
        TestDamageWithHPFormat();
        TestFailureFormat();
        TestCriticalFailureFormat();
        TestDeathFormat();
        TestCurseFormat();

        // Neutral/info
        TestInfoFormat();
        TestNoEffectFormat();
        TestWarningFormat();

        // Conditions
        TestConditionAppliedFormat();
        TestConditionFadedFormat();
        TestExpiredFormat();
        TestStatusEndFormat();

        // Save results
        TestSaveResult_Success();
        TestSaveResult_Failure();

        // Spell-specific
        TestSpellEffectFormat();
        TestBuffAppliedFormat();
        TestSpellResistanceFormat();
        TestImmuneFormat();
        TestInterruptedFormat();
        TestDebuffFormat();
        TestDefensiveFormat();

        // Summon
        TestSummonFormat();
        TestSummonRawFormat();

        // Color variants
        TestPaleBlueFormat();

        // Constants verification
        TestColorConstants();

        Debug.Log($"====== CombatLogHelper Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    // ──────────────────────────────────────────────
    //  Core Color() wrapper
    // ──────────────────────────────────────────────

    private static void TestColorWrap()
    {
        string result = CombatLogHelper.Color("hello", "FF0000");
        Assert(result == "<color=#FF0000>hello</color>", "Color wrap", $"got: {result}");
    }

    private static void TestColorWrap_EmptyText()
    {
        string result = CombatLogHelper.Color("", "AAAAAA");
        Assert(result == "<color=#AAAAAA></color>", "Color wrap empty", $"got: {result}");
    }

    // ──────────────────────────────────────────────
    //  Positive outcomes (green tones)
    // ──────────────────────────────────────────────

    private static void TestSuccessFormat()
    {
        string result = CombatLogHelper.Success("✅", "Heal lands!");
        Assert(result.Contains("<color=#88FF88>") && result.Contains("✅") && result.Contains("Heal lands!"),
            "Success format", $"got: {result}");
    }

    private static void TestHealingFormat()
    {
        string result = CombatLogHelper.Healing("Alaric", 10, 15, 25);
        Assert(result.Contains("Alaric") && result.Contains("10") && result.Contains("15") && result.Contains("25"),
            "Healing format with HP", $"got: {result}");
    }

    private static void TestSpellResistedFormat()
    {
        string result = CombatLogHelper.SpellResisted("Orc", "Hold Person");
        Assert(result.Contains("Orc") && result.Contains("Hold Person") && result.Contains("resists"),
            "SpellResisted format", $"got: {result}");
    }

    private static void TestBuffFormat()
    {
        string result = CombatLogHelper.Buff("🛡", "Shield of Faith +2 AC");
        Assert(result.Contains("<color=#FFCC66>") && result.Contains("🛡"),
            "Buff format (yellow)", $"got: {result}");
    }

    private static void TestSpecialFormat()
    {
        string result = CombatLogHelper.Special("⭐", "Critical hit!");
        Assert(result.Contains("<color=#FFD700>") && result.Contains("⭐"),
            "Special format (gold)", $"got: {result}");
    }

    // ──────────────────────────────────────────────
    //  Negative outcomes (red tones)
    // ──────────────────────────────────────────────

    private static void TestDamageFormat()
    {
        string result = CombatLogHelper.Damage("💥", "Goblin takes 5 fire damage");
        Assert(result.Contains("<color=#FF8888>") && result.Contains("💥"),
            "Damage format (bright red)", $"got: {result}");
    }

    private static void TestDamageWithHPFormat()
    {
        string result = CombatLogHelper.DamageWithHP("💥", "Goblin", 8, "fire", 20, 12);
        Assert(result.Contains("Goblin") && result.Contains("8") && result.Contains("fire")
            && result.Contains("20") && result.Contains("12"),
            "DamageWithHP format", $"got: {result}");
    }

    private static void TestFailureFormat()
    {
        string result = CombatLogHelper.Failure("❌", "Spell fizzles");
        Assert(result.Contains("<color=#FF6666>") && result.Contains("Spell fizzles"),
            "Failure format (red)", $"got: {result}");
    }

    private static void TestCriticalFailureFormat()
    {
        string result = CombatLogHelper.CriticalFailure("💀", "Catastrophic misfire!");
        Assert(result.Contains("<color=#FF4444>") && result.Contains("Catastrophic"),
            "CriticalFailure format (dark red)", $"got: {result}");
    }

    private static void TestDeathFormat()
    {
        string result = CombatLogHelper.Death("☠", "Goblin is slain!");
        Assert(result.Contains("<color=#FF0000>") && result.Contains("slain"),
            "Death format (deep red)", $"got: {result}");
    }

    private static void TestCurseFormat()
    {
        string result = CombatLogHelper.Curse("🌑", "Bestow Curse: -6 STR");
        Assert(result.Contains("<color=#8B0000>") && result.Contains("Bestow Curse"),
            "Curse format (dark crimson)", $"got: {result}");
    }

    // ──────────────────────────────────────────────
    //  Neutral / info
    // ──────────────────────────────────────────────

    private static void TestInfoFormat()
    {
        string result = CombatLogHelper.Info("✦", "Nothing happens.");
        Assert(result.Contains("<color=#AAAAAA>") && result.Contains("Nothing happens"),
            "Info format (gray)", $"got: {result}");
    }

    private static void TestNoEffectFormat()
    {
        string result = CombatLogHelper.NoEffect("✦", "Bless", "Skeleton", "undead");
        Assert(result.Contains("Skeleton") && result.Contains("unaffected") && result.Contains("undead"),
            "NoEffect format", $"got: {result}");
    }

    private static void TestWarningFormat()
    {
        string result = CombatLogHelper.Warning("⚠", "Contested grapple check!");
        Assert(result.Contains("<color=#FFAA44>") && result.Contains("⚠"),
            "Warning format (orange)", $"got: {result}");
    }

    // ──────────────────────────────────────────────
    //  Conditions / status
    // ──────────────────────────────────────────────

    private static void TestConditionAppliedFormat()
    {
        string result = CombatLogHelper.ConditionApplied("😵", "Goblin", "Stunned", "1 round");
        Assert(result.Contains("Goblin") && result.Contains("Stunned") && result.Contains("1 round"),
            "ConditionApplied format", $"got: {result}");
    }

    private static void TestConditionFadedFormat()
    {
        string result = CombatLogHelper.ConditionFaded("🛡", "Alaric", "Shield of Faith");
        Assert(result.Contains("Alaric") && result.Contains("Shield of Faith") && result.Contains("fades"),
            "ConditionFaded format", $"got: {result}");
    }

    private static void TestExpiredFormat()
    {
        string result = CombatLogHelper.Expired("⏳", "Haste has expired.");
        Assert(result.Contains("<color=#FFAA44>") && result.Contains("Haste"),
            "Expired format (orange)", $"got: {result}");
    }

    private static void TestStatusEndFormat()
    {
        string result = CombatLogHelper.StatusEnd("Blindness wears off.");
        Assert(result.Contains("<color=#99CCFF>") && result.Contains("Blindness"),
            "StatusEnd format (steel blue)", $"got: {result}");
    }

    // ──────────────────────────────────────────────
    //  Save results
    // ──────────────────────────────────────────────

    private static void TestSaveResult_Success()
    {
        string result = CombatLogHelper.SaveResult("Goblin", true, "Will", 18, 15);
        Assert(result.Contains("<color=#88FF88>") && result.Contains("makes"),
            "SaveResult success (green)", $"got: {result}");
    }

    private static void TestSaveResult_Failure()
    {
        string result = CombatLogHelper.SaveResult("Goblin", false, "Reflex", 8, 15);
        Assert(result.Contains("<color=#FF6666>") && result.Contains("fails"),
            "SaveResult failure (red)", $"got: {result}");
    }

    // ──────────────────────────────────────────────
    //  Spell-specific formatters
    // ──────────────────────────────────────────────

    private static void TestSpellEffectFormat()
    {
        string result = CombatLogHelper.SpellEffect("✨", "Fireball explodes for 30 damage!");
        Assert(result.Contains("<color=#88FFEE>") && result.Contains("Fireball"),
            "SpellEffect format (cyan)", $"got: {result}");
    }

    private static void TestBuffAppliedFormat()
    {
        string result = CombatLogHelper.BuffApplied("✨", "Alaric", "gains +4 STR");
        Assert(result.Contains("Alaric") && result.Contains("+4 STR"),
            "BuffApplied format", $"got: {result}");
    }

    private static void TestSpellResistanceFormat()
    {
        string result = CombatLogHelper.SpellResistance("🔮", "SR check: 15 vs SR 22 — blocked!");
        Assert(result.Contains("<color=#AAAAFF>"),
            "SpellResistance format (lavender)", $"got: {result}");
    }

    private static void TestImmuneFormat()
    {
        string result = CombatLogHelper.Immune("🛡", "Skeleton is immune to Sleep");
        Assert(result.Contains("<color=#66CC66>") && result.Contains("immune"),
            "Immune format (soft green)", $"got: {result}");
    }

    private static void TestInterruptedFormat()
    {
        string result = CombatLogHelper.Interrupted("⚡", "Casting interrupted by AoO!");
        Assert(result.Contains("<color=#FF6644>") && result.Contains("interrupted"),
            "Interrupted format (dim red)", $"got: {result}");
    }

    private static void TestDebuffFormat()
    {
        string result = CombatLogHelper.Debuff("😵", "Slow: half speed, -1 AC");
        Assert(result.Contains("<color=#FF9966>") && result.Contains("Slow"),
            "Debuff format (amber)", $"got: {result}");
    }

    private static void TestDefensiveFormat()
    {
        string result = CombatLogHelper.Defensive("🛡", "Casting defensively (DC 18)");
        Assert(result.Contains("<color=#88CCFF>") && result.Contains("DC 18"),
            "Defensive format (light blue)", $"got: {result}");
    }

    // ──────────────────────────────────────────────
    //  Summon
    // ──────────────────────────────────────────────

    private static void TestSummonFormat()
    {
        string result = CombatLogHelper.Summon("🐺", "Wolf appears!");
        Assert(result.Contains("<color=#66E8FF>") && result.Contains("Wolf"),
            "Summon format (sky blue)", $"got: {result}");
    }

    private static void TestSummonRawFormat()
    {
        string result = CombatLogHelper.SummonRaw("Fiendish Dire Wolf appears!");
        Assert(result.Contains("<color=#66E8FF>") && result.Contains("Fiendish"),
            "SummonRaw format (sky blue, no emoji)", $"got: {result}");
    }

    // ──────────────────────────────────────────────
    //  Color variants
    // ──────────────────────────────────────────────

    private static void TestPaleBlueFormat()
    {
        string result = CombatLogHelper.PaleBlue("ℹ", "Aura of courage active");
        Assert(result.Contains("<color=#AADDFF>"),
            "PaleBlue format", $"got: {result}");
    }

    // ──────────────────────────────────────────────
    //  Color constant verification
    // ──────────────────────────────────────────────

    private static void TestColorConstants()
    {
        // Verify key constants match expected hex values
        Assert(CombatLogHelper.ColorGold == "FFD700", "ColorGold==FFD700");
        Assert(CombatLogHelper.ColorGray == "AAAAAA", "ColorGray==AAAAAA");
        Assert(CombatLogHelper.ColorRed == "FF6666", "ColorRed==FF6666");
        Assert(CombatLogHelper.ColorGreen == "88FF88", "ColorGreen==88FF88");
        Assert(CombatLogHelper.ColorCyan == "88FFEE", "ColorCyan==88FFEE");
        Assert(CombatLogHelper.ColorOrange == "FFAA44", "ColorOrange==FFAA44");
        Assert(CombatLogHelper.ColorBrightRed == "FF8888", "ColorBrightRed==FF8888");
        Assert(CombatLogHelper.ColorDeepRed == "FF0000", "ColorDeepRed==FF0000");
        Assert(CombatLogHelper.ColorDarkCrimson == "8B0000", "ColorDarkCrimson==8B0000");
        Assert(CombatLogHelper.ColorSteelBlue == "99CCFF", "ColorSteelBlue==99CCFF");
    }
}
}
