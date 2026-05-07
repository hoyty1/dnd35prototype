using System.Collections.Generic;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// Regression tests for D&D 3.5e multiclass save progression.
/// Multiclass base saves are summed across classes, not chosen by best class.
/// </summary>
public static class MulticlassSaveCalculationTests
{
    private static int _passed;
    private static int _failed;

    public static void multiclass_save_calculation_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== MULTICLASS SAVE CALCULATION TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();

        TestFighter1Rogue1SavesAreSummed();
        TestFighter5Wizard3SavesAreSummed();

        Debug.Log($"====== Multiclass Save Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name, int str, int dex, int con, int wis)
    {
        return new CharacterStats(
            name: name,
            level: 1,
            characterClass: "Fighter",
            str: str,
            dex: dex,
            con: con,
            wis: wis,
            intelligence: 10,
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

    private static void TestFighter1Rogue1SavesAreSummed()
    {
        CharacterStats stats = BuildStats("FtrRog", str: 12, dex: 14, con: 14, wis: 10);
        stats.ClassLevels = new List<ClassLevelEntry>
        {
            new ClassLevelEntry("Fighter", 1),
            new ClassLevelEntry("Rogue", 1)
        };
        stats.EnsureMulticlassDataInitialized();

        Assert(stats.ClassFortSave == 2, "Fighter1/Rogue1 base Fort = +2", $"got {stats.ClassFortSave}");
        Assert(stats.ClassRefSave == 2, "Fighter1/Rogue1 base Ref = +2", $"got {stats.ClassRefSave}");
        Assert(stats.ClassWillSave == 0, "Fighter1/Rogue1 base Will = +0", $"got {stats.ClassWillSave}");

        Assert(stats.FortitudeSave == 4, "Fighter1/Rogue1 total Fort adds CON mod", $"expected 4, got {stats.FortitudeSave}");
        Assert(stats.ReflexSave == 4, "Fighter1/Rogue1 total Ref adds DEX mod", $"expected 4, got {stats.ReflexSave}");
        Assert(stats.WillSave == 0, "Fighter1/Rogue1 total Will adds WIS mod", $"expected 0, got {stats.WillSave}");
    }

    private static void TestFighter5Wizard3SavesAreSummed()
    {
        CharacterStats stats = BuildStats("FtrWiz", str: 12, dex: 10, con: 12, wis: 12);
        stats.ClassLevels = new List<ClassLevelEntry>
        {
            new ClassLevelEntry("Fighter", 5),
            new ClassLevelEntry("Wizard", 3)
        };
        stats.EnsureMulticlassDataInitialized();

        Assert(stats.ClassFortSave == 5, "Fighter5/Wizard3 base Fort = +5", $"got {stats.ClassFortSave}");
        Assert(stats.ClassRefSave == 2, "Fighter5/Wizard3 base Ref = +2", $"got {stats.ClassRefSave}");
        Assert(stats.ClassWillSave == 4, "Fighter5/Wizard3 base Will = +4", $"got {stats.ClassWillSave}");

        Assert(stats.ClassFortSave != 4, "Fighter5/Wizard3 is not best-of Fort (+4)");
        Assert(stats.ClassRefSave != 1, "Fighter5/Wizard3 is not best-of Ref (+1)");
        Assert(stats.ClassWillSave != 3, "Fighter5/Wizard3 is not best-of Will (+3)");
    }
}
}
