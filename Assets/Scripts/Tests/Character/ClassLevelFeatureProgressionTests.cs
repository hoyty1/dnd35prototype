using System.Collections.Generic;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// Regression tests for class-specific progression rules.
/// Ensures class abilities (bonus feats, class-level prerequisites) use class level, not total character level.
/// </summary>
public static class ClassLevelFeatureProgressionTests
{
    private static int _passed;
    private static int _failed;

    public static void class_level_feature_progression_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== CLASS LEVEL FEATURE PROGRESSION TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();

        TestWizard2Fighter1GetsGeneralAndFighterBonusFeatSelections();
        TestClassLevelPrerequisiteUsesSpecificClassLevelNotPrimaryClass();
        TestCasterLevelPrerequisiteUsesBestCasterClassLevel();

        Debug.Log($"====== Class Level Feature Progression Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name, int totalLevel, string primaryClass)
    {
        return new CharacterStats(
            name: name,
            level: totalLevel,
            characterClass: primaryClass,
            str: 14,
            dex: 12,
            con: 12,
            wis: 12,
            intelligence: 14,
            cha: 10,
            bab: 1,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 10,
            raceName: "Human");
    }

    private static void TestWizard2Fighter1GetsGeneralAndFighterBonusFeatSelections()
    {
        var go = new GameObject("ClassLevelProgressionTestChar");
        var controller = go.AddComponent<CharacterController>();

        CharacterStats stats = BuildStats("WizFtr", totalLevel: 3, primaryClass: "Wizard");
        stats.ClassLevels = new List<ClassLevelEntry>
        {
            new ClassLevelEntry("Wizard", 2)
        };
        stats.PendingLevelUps = 1;
        stats.EnsureMulticlassDataInitialized();

        controller.Stats = stats;

        LevelUpData data = LevelUpCalculator.CalculateLevelUp(controller, oldLevel: 2, newLevel: 3);
        LevelUpCalculator.RecalculateForSelectedClass(data, "Fighter");

        Assert(data.GeneralFeatsToSelect == 1,
            "Wizard2/Fighter1 grants 1 general feat at total level 3",
            $"expected 1, got {data.GeneralFeatsToSelect}");
        Assert(data.FighterBonusFeatsToSelect == 1,
            "Wizard2/Fighter1 grants Fighter bonus feat at Fighter class level 1",
            $"expected 1, got {data.FighterBonusFeatsToSelect}");
        Assert(data.TotalFeatsToSelect == 2,
            "Wizard2/Fighter1 requires 2 feat selections this level-up",
            $"expected 2, got {data.TotalFeatsToSelect}");

        Object.DestroyImmediate(go);
    }

    private static void TestClassLevelPrerequisiteUsesSpecificClassLevelNotPrimaryClass()
    {
        CharacterStats stats = BuildStats("PrereqClassLevel", totalLevel: 3, primaryClass: "Wizard");
        stats.ClassLevels = new List<ClassLevelEntry>
        {
            new ClassLevelEntry("Wizard", 2),
            new ClassLevelEntry("Fighter", 1)
        };
        stats.EnsureMulticlassDataInitialized();

        FeatPrerequisite fighterLevelOne = new FeatPrerequisite(PrerequisiteType.ClassLevel, "Fighter", 1);
        FeatPrerequisite fighterLevelTwo = new FeatPrerequisite(PrerequisiteType.ClassLevel, "Fighter", 2);

        Assert(fighterLevelOne.IsMet(stats),
            "Class-level prerequisite succeeds when that class level is present");
        Assert(!fighterLevelTwo.IsMet(stats),
            "Class-level prerequisite fails when required class level is not reached");
    }

    private static void TestCasterLevelPrerequisiteUsesBestCasterClassLevel()
    {
        CharacterStats stats = BuildStats("CasterLevelPrereq", totalLevel: 5, primaryClass: "Fighter");
        stats.ClassLevels = new List<ClassLevelEntry>
        {
            new ClassLevelEntry("Fighter", 3),
            new ClassLevelEntry("Wizard", 2)
        };
        stats.EnsureMulticlassDataInitialized();

        FeatPrerequisite casterLevelTwo = new FeatPrerequisite(PrerequisiteType.CasterLevel, "", 2);
        FeatPrerequisite casterLevelThree = new FeatPrerequisite(PrerequisiteType.CasterLevel, "", 3);

        Assert(casterLevelTwo.IsMet(stats),
            "Caster-level prerequisite checks highest caster class level (Wizard 2 meets CL 2)");
        Assert(!casterLevelThree.IsMet(stats),
            "Caster-level prerequisite fails when no class reaches required caster level");
    }
}
}
