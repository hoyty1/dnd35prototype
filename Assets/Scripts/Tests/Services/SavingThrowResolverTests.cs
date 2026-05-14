using UnityEngine;
using Tests.Utilities;

namespace Tests.Services
{
/// <summary>
/// Unit tests for SavingThrowResolver — verifies saving throw calculations,
/// specialized saves (poison, disease, coup de grace), and utility methods.
/// Run with SavingThrowResolverTests.RunAll().
/// </summary>
public static class SavingThrowResolverTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SAVING THROW RESOLVER TESTS ======");

        TestHelpers.EnsureCoreDatabasesInitialized();

        TestGetSaveModifierFortitude();
        TestGetSaveModifierReflex();
        TestGetSaveModifierWill();
        TestGetSaveModifierNullStats();
        TestResolveSaveSuccess();
        TestResolveSaveFailure();
        TestResolveFortitudeSave();
        TestResolveReflexSave();
        TestResolveWillSave();
        TestResolvePoisonSave();
        TestResolveDiseaseSave();
        TestResolveCoupDeGraceSave();
        TestQuickSaveStatistical();
        TestParseSaveType();
        TestGetSaveTypeName();
        TestSaveResultStructure();

        Debug.Log($"====== Saving Throw Resolver Results: {_passed} passed, {_failed} failed ======");
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

    // ===== SAVE MODIFIER CALCULATION =====

    private static void TestGetSaveModifierFortitude()
    {
        var stats = TestHelpers.CreateStats(name: "Fort_Test", con: 16);
        int mod = SavingThrowResolver.GetSaveModifier(stats, SavingThrowResolver.SaveType.Fortitude);
        Assert(mod == stats.FortitudeSave, "Fort modifier matches stats.FortitudeSave", $"got {mod}, expected {stats.FortitudeSave}");
    }

    private static void TestGetSaveModifierReflex()
    {
        var stats = TestHelpers.CreateStats(name: "Ref_Test", dex: 18);
        int mod = SavingThrowResolver.GetSaveModifier(stats, SavingThrowResolver.SaveType.Reflex);
        Assert(mod == stats.ReflexSave, "Reflex modifier matches stats.ReflexSave", $"got {mod}, expected {stats.ReflexSave}");
    }

    private static void TestGetSaveModifierWill()
    {
        var stats = TestHelpers.CreateStats(name: "Will_Test", wis: 16);
        int mod = SavingThrowResolver.GetSaveModifier(stats, SavingThrowResolver.SaveType.Will);
        Assert(mod == stats.WillSave, "Will modifier matches stats.WillSave", $"got {mod}, expected {stats.WillSave}");
    }

    private static void TestGetSaveModifierNullStats()
    {
        int mod = SavingThrowResolver.GetSaveModifier(null, SavingThrowResolver.SaveType.Fortitude);
        Assert(mod == 0, "Null stats returns 0", $"got {mod}");
    }

    // ===== RESOLVE SAVE =====

    private static void TestResolveSaveSuccess()
    {
        // DC 1 with any stats should almost always succeed
        var stats = TestHelpers.CreateStats(name: "Save_Easy", con: 16);
        int successes = 0;
        for (int i = 0; i < 100; i++)
        {
            var result = SavingThrowResolver.ResolveSave(stats, SavingThrowResolver.SaveType.Fortitude, 1, "easy save");
            if (result.Succeeded) successes++;
        }
        Assert(successes == 100, "DC 1 save always succeeds", $"successes={successes}/100");
    }

    private static void TestResolveSaveFailure()
    {
        // DC 100 should almost always fail
        var stats = TestHelpers.CreateStats(name: "Save_Hard");
        int failures = 0;
        for (int i = 0; i < 100; i++)
        {
            var result = SavingThrowResolver.ResolveSave(stats, SavingThrowResolver.SaveType.Will, 100, "impossible save");
            if (!result.Succeeded) failures++;
        }
        Assert(failures == 100, "DC 100 save always fails", $"failures={failures}/100");
    }

    private static void TestResolveFortitudeSave()
    {
        var stats = TestHelpers.CreateStats(name: "Fort_Resolve", con: 14);
        var result = SavingThrowResolver.ResolveFortitudeSave(stats, 15, "poison");

        Assert(result.Type == SavingThrowResolver.SaveType.Fortitude, "Fort save type correct");
        Assert(result.DC == 15, "Fort save DC = 15", $"got {result.DC}");
        Assert(result.Roll >= 1 && result.Roll <= 20, "Fort save roll in range", $"got {result.Roll}");
        Assert(result.Total == result.Roll + result.Modifier, "Fort total = roll + mod", $"total={result.Total}, roll={result.Roll}, mod={result.Modifier}");
        Assert(result.Succeeded == (result.Total >= result.DC), "Fort succeeded logic correct");
        Assert(!string.IsNullOrEmpty(result.LogMessage), "Fort log message not empty");
    }

    private static void TestResolveReflexSave()
    {
        var stats = TestHelpers.CreateStats(name: "Ref_Resolve", dex: 16);
        var result = SavingThrowResolver.ResolveReflexSave(stats, 12, "fireball");

        Assert(result.Type == SavingThrowResolver.SaveType.Reflex, "Reflex save type correct");
        Assert(result.Roll >= 1 && result.Roll <= 20, "Reflex roll in range", $"got {result.Roll}");
    }

    private static void TestResolveWillSave()
    {
        var stats = TestHelpers.CreateStats(name: "Will_Resolve", wis: 18);
        var result = SavingThrowResolver.ResolveWillSave(stats, 14, "charm person");

        Assert(result.Type == SavingThrowResolver.SaveType.Will, "Will save type correct");
        Assert(result.Roll >= 1 && result.Roll <= 20, "Will roll in range", $"got {result.Roll}");
    }

    // ===== SPECIALIZED SAVES =====

    private static void TestResolvePoisonSave()
    {
        var stats = TestHelpers.CreateStats(name: "Poison_Test", con: 14);

        // Primary save
        var primary = SavingThrowResolver.ResolvePoisonSave(stats, 16, "Medium Spider Venom", isSecondary: false);
        Assert(primary.EffectName == "Medium Spider Venom (initial)", "Poison primary label correct", $"got '{primary.EffectName}'");

        // Secondary save
        var secondary = SavingThrowResolver.ResolvePoisonSave(stats, 16, "Medium Spider Venom", isSecondary: true);
        Assert(secondary.EffectName == "Medium Spider Venom (secondary)", "Poison secondary label correct", $"got '{secondary.EffectName}'");
    }

    private static void TestResolveDiseaseSave()
    {
        var stats = TestHelpers.CreateStats(name: "Disease_Test", con: 12);

        var exposure = SavingThrowResolver.ResolveDiseaseSave(stats, 14, "Filth Fever", isDaily: false);
        Assert(exposure.EffectName == "Filth Fever (exposure)", "Disease exposure label correct", $"got '{exposure.EffectName}'");

        var daily = SavingThrowResolver.ResolveDiseaseSave(stats, 14, "Filth Fever", isDaily: true);
        Assert(daily.EffectName == "Filth Fever (daily)", "Disease daily label correct", $"got '{daily.EffectName}'");
    }

    private static void TestResolveCoupDeGraceSave()
    {
        var stats = TestHelpers.CreateStats(name: "CdG_Test", con: 14);
        int damage = 25;

        var result = SavingThrowResolver.ResolveCoupDeGraceSave(stats, damage);
        Assert(result.DC == 10 + damage, $"CdG DC = 10 + {damage} = {10 + damage}", $"got {result.DC}");
        Assert(result.EffectName == "Coup de Grace", "CdG effect name correct", $"got '{result.EffectName}'");
    }

    // ===== QUICK SAVE =====

    private static void TestQuickSaveStatistical()
    {
        var stats = TestHelpers.CreateStats(name: "Quick_Test", con: 14);
        int successes = 0;
        for (int i = 0; i < 200; i++)
        {
            if (SavingThrowResolver.QuickSave(stats, SavingThrowResolver.SaveType.Fortitude, 10))
                successes++;
        }
        // With reasonable stats, we should see a mix of successes and failures for DC 10
        Assert(successes > 20, "QuickSave has some successes", $"successes={successes}/200");
        Assert(successes < 200, "QuickSave has some failures", $"successes={successes}/200");
    }

    // ===== PARSE SAVE TYPE =====

    private static void TestParseSaveType()
    {
        Assert(SavingThrowResolver.ParseSaveType("Fortitude") == SavingThrowResolver.SaveType.Fortitude, "Parse 'Fortitude'");
        Assert(SavingThrowResolver.ParseSaveType("Fort") == SavingThrowResolver.SaveType.Fortitude, "Parse 'Fort'");
        Assert(SavingThrowResolver.ParseSaveType("fortitude") == SavingThrowResolver.SaveType.Fortitude, "Parse 'fortitude' (lowercase)");
        Assert(SavingThrowResolver.ParseSaveType("Reflex") == SavingThrowResolver.SaveType.Reflex, "Parse 'Reflex'");
        Assert(SavingThrowResolver.ParseSaveType("ref") == SavingThrowResolver.SaveType.Reflex, "Parse 'ref'");
        Assert(SavingThrowResolver.ParseSaveType("Will") == SavingThrowResolver.SaveType.Will, "Parse 'Will'");
        Assert(SavingThrowResolver.ParseSaveType("") == SavingThrowResolver.SaveType.Will, "Parse empty defaults to Will");
        Assert(SavingThrowResolver.ParseSaveType(null) == SavingThrowResolver.SaveType.Will, "Parse null defaults to Will");
    }

    // ===== GET SAVE TYPE NAME =====

    private static void TestGetSaveTypeName()
    {
        Assert(SavingThrowResolver.GetSaveTypeName(SavingThrowResolver.SaveType.Fortitude) == "Fortitude", "Name for Fortitude");
        Assert(SavingThrowResolver.GetSaveTypeName(SavingThrowResolver.SaveType.Reflex) == "Reflex", "Name for Reflex");
        Assert(SavingThrowResolver.GetSaveTypeName(SavingThrowResolver.SaveType.Will) == "Will", "Name for Will");
    }

    // ===== SAVE RESULT STRUCTURE =====

    private static void TestSaveResultStructure()
    {
        var stats = TestHelpers.CreateStats(name: "Struct_Test");
        var result = SavingThrowResolver.ResolveSave(stats, SavingThrowResolver.SaveType.Will, 15, "test effect");

        Assert(result.Type == SavingThrowResolver.SaveType.Will, "Result.Type correct");
        Assert(result.DC == 15, "Result.DC = 15", $"got {result.DC}");
        Assert(result.EffectName == "test effect", "Result.EffectName correct", $"got '{result.EffectName}'");
        Assert(result.Modifier == stats.WillSave, "Result.Modifier = WillSave", $"got {result.Modifier}, expected {stats.WillSave}");
        Assert(result.Total == result.Roll + result.Modifier, "Result.Total = Roll + Modifier");
        Assert(result.Succeeded == (result.Total >= 15), "Result.Succeeded matches Total >= DC");
        Assert(!string.IsNullOrEmpty(result.LogMessage), "Result.LogMessage populated");
    }
}
}
