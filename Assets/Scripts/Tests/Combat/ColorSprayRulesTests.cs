using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Color Spray spell mechanics and staged HD-based effects.
/// Run with ColorSprayRulesTests.RunAll().
/// </summary>
public static class ColorSprayRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== COLOR SPRAY RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestColorSprayDefinitionMatchesCoreRules();
        TestColorSprayLowHdCascadesAllStages();
        TestColorSprayMediumHdCascadesTwoStages();
        TestColorSprayHighHdOnlyStuns();

        Debug.Log($"====== Color Spray Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name, string className, int level, int hitDice)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: 12,
            dex: 12,
            con: 12,
            wis: 12,
            intelligence: 14,
            cha: 12,
            bab: Mathf.Max(1, level / 2),
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 24,
            raceName: "Human");

        stats.CreatureType = "Humanoid";
        stats.HitDice = hitDice;
        return stats;
    }

    private static CharacterController CreateController(CharacterStats stats, CharacterTeam team, Vector2Int gridPos)
    {
        GameObject go = new GameObject($"ColorSprayRules_{stats.CharacterName}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Stats = stats;
        controller.SetTeam(team);
        controller.GridPosition = gridPos;

        InventoryComponent inv = go.AddComponent<InventoryComponent>();
        inv.Init(stats);

        StatusEffectManager statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static void TestColorSprayDefinitionMatchesCoreRules()
    {
        SpellData colorSpray = SpellDatabase.GetSpell("color_spray");
        Assert(colorSpray != null, "Color Spray spell definition exists");
        if (colorSpray == null)
            return;

        Assert(!colorSpray.IsPlaceholder, "Color Spray is implemented (not placeholder)");
        Assert(colorSpray.TargetType == SpellTargetType.Area, "Color Spray is area-targeted");
        Assert(colorSpray.AoEShapeType == AoEShape.Cone && colorSpray.AoESizeSquares == 3,
            "Color Spray uses 15-ft cone", $"shape={colorSpray.AoEShapeType}, size={colorSpray.AoESizeSquares}");
        Assert(colorSpray.AoEFilter == AoETargetFilter.All, "Color Spray affects all creatures in cone");
        Assert(colorSpray.AllowsSavingThrow && colorSpray.SavingThrowType == "Will", "Color Spray uses Will save negates");
        Assert(colorSpray.SpellResistanceApplies, "Color Spray allows Spell Resistance");
        Assert(colorSpray.IsMindAffecting, "Color Spray is mind-affecting");
        Assert(colorSpray.ClassList != null && System.Array.Exists(colorSpray.ClassList, c => c == "Wizard") && System.Array.Exists(colorSpray.ClassList, c => c == "Sorcerer"),
            "Color Spray is on Wizard/Sorcerer lists");
    }

    private static void TestColorSprayLowHdCascadesAllStages()
    {
        RunTierTransitionScenario(
            testLabel: "Low HD (<=2) tier transitions",
            hdTier: 1,
            expectedStage1Unconscious: true,
            expectedStage1Blinded: true,
            expectedStage1Stunned: true,
            expectedStage2Unconscious: false,
            expectedStage2Blinded: true,
            expectedStage2Stunned: true,
            expectedStage3Unconscious: false,
            expectedStage3Blinded: false,
            expectedStage3Stunned: true,
            hasStage3: true);
    }

    private static void TestColorSprayMediumHdCascadesTwoStages()
    {
        RunTierTransitionScenario(
            testLabel: "Medium HD (3-4) tier transitions",
            hdTier: 2,
            expectedStage1Unconscious: false,
            expectedStage1Blinded: true,
            expectedStage1Stunned: true,
            expectedStage2Unconscious: false,
            expectedStage2Blinded: false,
            expectedStage2Stunned: true,
            expectedStage3Unconscious: false,
            expectedStage3Blinded: false,
            expectedStage3Stunned: false,
            hasStage3: false);
    }

    private static void TestColorSprayHighHdOnlyStuns()
    {
        RunTierTransitionScenario(
            testLabel: "High HD (5+) tier transitions",
            hdTier: 3,
            expectedStage1Unconscious: false,
            expectedStage1Blinded: false,
            expectedStage1Stunned: true,
            expectedStage2Unconscious: false,
            expectedStage2Blinded: false,
            expectedStage2Stunned: false,
            expectedStage3Unconscious: false,
            expectedStage3Blinded: false,
            expectedStage3Stunned: false,
            hasStage3: false);
    }

    private static void RunTierTransitionScenario(
        string testLabel,
        int hdTier,
        bool expectedStage1Unconscious,
        bool expectedStage1Blinded,
        bool expectedStage1Stunned,
        bool expectedStage2Unconscious,
        bool expectedStage2Blinded,
        bool expectedStage2Stunned,
        bool expectedStage3Unconscious,
        bool expectedStage3Blinded,
        bool expectedStage3Stunned,
        bool hasStage3)
    {
        GameObject gmObject = new GameObject($"ColorSprayRules_{testLabel}_GM");
        GameManager gm = gmObject.AddComponent<GameManager>();
        ConditionService conditionService = gmObject.AddComponent<ConditionService>();

        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Caster", "Wizard", 3, 3), CharacterTeam.Player, new Vector2Int(2, 2));
            target = CreateController(BuildStats("Target", "Fighter", 3, hdTier == 1 ? 2 : hdTier == 2 ? 4 : 6), CharacterTeam.Enemy, new Vector2Int(3, 2));

            gm.PCs.Add(caster);
            gm.NPCs.Add(target);

            conditionService.Initialize(() =>
            {
                var all = new List<CharacterController>();
                all.AddRange(gm.PCs);
                all.AddRange(gm.NPCs);
                return all;
            });

            FieldInfo conditionField = typeof(GameManager).GetField("_conditionService", BindingFlags.NonPublic | BindingFlags.Instance);
            conditionField?.SetValue(gm, conditionService);

            SpellData spell = SpellDatabase.GetSpell("color_spray");
            MethodInfo applyStage = typeof(GameManager).GetMethod("ApplyColorSprayStageConditions", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo handleExpiry = typeof(GameManager).GetMethod("TryHandleColorSprayConditionExpiry", BindingFlags.NonPublic | BindingFlags.Instance);

            var data = new ColorSprayEffectData
            {
                Caster = caster,
                CasterName = caster.Stats.CharacterName,
                SourceSpellId = "color_spray",
                SourceEffectName = "Color Spray",
                HdTier = hdTier,
                HitDice = hdTier == 1 ? 2 : hdTier == 2 ? 4 : 6,
                CurrentStage = 1,
                Stage1Duration = 1,
                Stage2Duration = 1,
                Stage3Duration = 1,
                RemainingDuration = 1,
                NextStage = hdTier == 1 || hdTier == 2 ? 2 : 0
            };

            applyStage?.Invoke(gm, new object[] { target, data, spell });

            Assert(conditionService.HasCondition(target, CombatConditionType.Unconscious) == expectedStage1Unconscious,
                $"{testLabel}: stage 1 unconscious state");
            Assert(conditionService.HasCondition(target, CombatConditionType.Blinded) == expectedStage1Blinded,
                $"{testLabel}: stage 1 blinded state");
            Assert(conditionService.HasCondition(target, CombatConditionType.Stunned) == expectedStage1Stunned,
                $"{testLabel}: stage 1 stunned state");

            var expired = new ConditionService.ActiveCondition
            {
                Type = CombatConditionType.Stunned,
                SourceName = "Color Spray",
                SourceId = "color_spray",
                Data = data
            };

            conditionService.RemoveCondition(target, CombatConditionType.Stunned);
            bool handled = handleExpiry != null && (bool)handleExpiry.Invoke(gm, new object[] { target, expired });
            Assert(handled, $"{testLabel}: stage 1 expiry handled");

            Assert(conditionService.HasCondition(target, CombatConditionType.Unconscious) == expectedStage2Unconscious,
                $"{testLabel}: stage 2 unconscious state");
            Assert(conditionService.HasCondition(target, CombatConditionType.Blinded) == expectedStage2Blinded,
                $"{testLabel}: stage 2 blinded state");
            Assert(conditionService.HasCondition(target, CombatConditionType.Stunned) == expectedStage2Stunned,
                $"{testLabel}: stage 2 stunned state");

            if (hasStage3)
            {
                conditionService.RemoveCondition(target, CombatConditionType.Stunned);
                handled = handleExpiry != null && (bool)handleExpiry.Invoke(gm, new object[] { target, expired });
                Assert(handled, $"{testLabel}: stage 2 expiry handled");

                Assert(conditionService.HasCondition(target, CombatConditionType.Unconscious) == expectedStage3Unconscious,
                    $"{testLabel}: stage 3 unconscious state");
                Assert(conditionService.HasCondition(target, CombatConditionType.Blinded) == expectedStage3Blinded,
                    $"{testLabel}: stage 3 blinded state");
                Assert(conditionService.HasCondition(target, CombatConditionType.Stunned) == expectedStage3Stunned,
                    $"{testLabel}: stage 3 stunned state");
            }
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            Object.DestroyImmediate(gmObject);
        }
    }
}
}
