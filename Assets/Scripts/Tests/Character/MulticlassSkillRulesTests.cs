using System.Collections.Generic;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// Regression tests for multiclass skill rules:
/// - Max ranks are based on whether ANY class has the skill as class skill.
/// - House rule: cost is 1 if ANY class has the skill as a class skill; otherwise 2.
/// </summary>
public static class MulticlassSkillRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void multiclass_skill_rules_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== MULTICLASS SKILL RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();

        TestMaxRanksUseAnyClassSkill();
        TestSkillCostUsesAnyClassHouseRule();
        TestCrossClassSkillCapsAndCosts();
        TestClassSpecificSkillPointPools();

        Debug.Log($"====== Multiclass Skill Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildFighter3Cleric2()
    {
        CharacterStats stats = new CharacterStats(
            name: "SkillTester",
            level: 1,
            characterClass: "Fighter",
            str: 14,
            dex: 12,
            con: 12,
            wis: 12,
            intelligence: 12,
            cha: 12,
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

        stats.ClassLevels = new List<ClassLevelEntry>
        {
            new ClassLevelEntry("Fighter", 3),
            new ClassLevelEntry("Cleric", 2)
        };

        stats.EnsureMulticlassDataInitialized();
        stats.InitializeSkills("Fighter", stats.Level);
        return stats;
    }

    private static void TestMaxRanksUseAnyClassSkill()
    {
        CharacterStats stats = BuildFighter3Cleric2();

        // Prototype note: Cleric has Diplomacy as class skill, Fighter does not.
        int diplomacyMax = stats.GetSkillMaxRanks("Diplomacy");

        Assert(stats.Level == 5, "Fighter3/Cleric2 total level is 5", $"got {stats.Level}");
        Assert(stats.IsSkillClassSkillForAnyClass("Diplomacy"),
            "Diplomacy is class skill for at least one class (Cleric)");
        Assert(diplomacyMax == 8,
            "Max rank uses ANY class skill rule (Lv+3)",
            $"expected 8, got {diplomacyMax}");
    }

    private static void TestSkillCostUsesAnyClassHouseRule()
    {
        CharacterStats stats = BuildFighter3Cleric2();

        int clericCost = stats.GetSkillPointCost("Diplomacy", "Cleric");
        int fighterCost = stats.GetSkillPointCost("Diplomacy", "Fighter");

        Assert(clericCost == 1,
            "Diplomacy costs 1 when advancing Cleric",
            $"got {clericCost}");
        Assert(fighterCost == 1,
            "House rule: Diplomacy costs 1 when advancing Fighter if any class has it",
            $"got {fighterCost}");

        stats.AvailableSkillPoints = 10;
        int before = stats.AvailableSkillPoints;
        bool addedAsFighter = stats.AddSkillRank("Diplomacy", "Fighter");
        Assert(addedAsFighter, "Can add Diplomacy rank while advancing Fighter");
        Assert(stats.AvailableSkillPoints == before - 1,
            "House rule: Fighter advancement spends 1 point for Diplomacy",
            $"expected {before - 1}, got {stats.AvailableSkillPoints}");

        bool removedAsFighter = stats.RemoveSkillRank("Diplomacy", "Fighter");
        Assert(removedAsFighter, "Can remove Diplomacy rank with Fighter context");
        Assert(stats.AvailableSkillPoints == before,
            "House rule: removing rank refunds 1 when any class grants class-skill status",
            $"expected {before}, got {stats.AvailableSkillPoints}");

        bool addedAsCleric = stats.AddSkillRank("Diplomacy", "Cleric");
        Assert(addedAsCleric, "Can add Diplomacy rank while advancing Cleric");
        Assert(stats.AvailableSkillPoints == before - 1,
            "Cleric advancement spends 1 point for Diplomacy",
            $"expected {before - 1}, got {stats.AvailableSkillPoints}");
    }

    private static void TestCrossClassSkillCapsAndCosts()
    {
        CharacterStats stats = BuildFighter3Cleric2();

        // Disable Device is not a class skill for Fighter or Cleric in this prototype.
        int max = stats.GetSkillMaxRanks("Disable Device");
        int fighterCost = stats.GetSkillPointCost("Disable Device", "Fighter");
        int clericCost = stats.GetSkillPointCost("Disable Device", "Cleric");

        Assert(!stats.IsSkillClassSkillForAnyClass("Disable Device"),
            "Disable Device is cross-class for all current classes");
        Assert(max == 4,
            "Cross-class max rank uses (Lv+3)/2",
            $"expected 4, got {max}");
        Assert(fighterCost == 2 && clericCost == 2,
            "Cross-class skill costs 2 regardless of advancing class",
            $"fighter {fighterCost}, cleric {clericCost}");
    }

    private static void TestClassSpecificSkillPointPools()
    {
        CharacterStats stats = BuildFighter3Cleric2();

        stats.SetClassSkillPointPool("Fighter", 5);
        stats.SetClassSkillPointPool("Wizard", 2);

        int fighterNew = Mathf.Max(1, ClassSkillDefinitions.GetBaseSkillPointsPerLevel("Fighter") + stats.INTMod);
        int wizardFirstLevelNew = Mathf.Max(1, ClassSkillDefinitions.GetBaseSkillPointsPerLevel("Wizard") + stats.INTMod) * 4;

        int fighterAvailable = fighterNew + stats.GetClassSkillPointPool("Fighter");
        int wizardAvailable = wizardFirstLevelNew + stats.GetClassSkillPointPool("Wizard");

        Assert(fighterAvailable == fighterNew + 5,
            "Fighter level-up uses Fighter pool only",
            $"expected {fighterNew + 5}, got {fighterAvailable}");
        Assert(wizardAvailable == wizardFirstLevelNew + 2,
            "Wizard first level uses Wizard pool only",
            $"expected {wizardFirstLevelNew + 2}, got {wizardAvailable}");
        Assert(stats.GetClassSkillPointPool("Cleric") == 0,
            "Uninitialized class pools default to 0");

        stats.SetClassSkillPointPool("Fighter", 2);
        Assert(stats.GetClassSkillPointPool("Wizard") == 2,
            "Updating Fighter pool does not affect Wizard pool",
            $"wizard pool changed to {stats.GetClassSkillPointPool("Wizard")}");
    }
}
}
