using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Sleep spell + Asleep condition integration.
/// Run with SleepSpellRulesTests.RunAll().
/// </summary>
public static class SleepSpellRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SLEEP SPELL RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestSleepDefinitionMatchesCoreRules();
        TestAsleepConditionDefinitionExists();
        TestTryWakeSleepingCharacterRemovesAsleepState();
        TestAidAnotherAvailableForAdjacentSleepingAlly();

        Debug.Log($"====== Sleep Spell Rules Results: {_passed} passed, {_failed} failed ======");
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
            intelligence: 12,
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
        GameObject go = new GameObject($"SleepRules_{stats.CharacterName}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Stats = stats;
        controller.Team = team;
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

    private static void TestSleepDefinitionMatchesCoreRules()
    {
        SpellData sleep = SpellDatabase.GetSpell("sleep");
        Assert(sleep != null, "Sleep spell definition exists");
        if (sleep == null)
            return;

        Assert(!sleep.IsPlaceholder, "Sleep is implemented (not placeholder)");
        Assert(sleep.TargetType == SpellTargetType.Area, "Sleep is area-targeted");
        Assert(sleep.RangeCategory == SpellRangeCategory.Medium, "Sleep uses Medium range");
        Assert(sleep.AoEShapeType == AoEShape.Burst && sleep.AoESizeSquares == 2,
            "Sleep uses 10-ft burst",
            $"shape={sleep.AoEShapeType}, size={sleep.AoESizeSquares}");
        Assert(sleep.AoEFilter == AoETargetFilter.All, "Sleep affects all creatures in area");
        Assert(sleep.AllowsSavingThrow && sleep.SavingThrowType == "Will", "Sleep uses Will save negates");
        Assert(sleep.SpellResistanceApplies, "Sleep allows Spell Resistance");
        Assert(sleep.IsMindAffecting, "Sleep is mind-affecting");
        Assert(sleep.DurationType == DurationType.Minutes && sleep.DurationValue == 1 && sleep.DurationScalesWithLevel,
            "Sleep duration is 1 min/level");
    }

    private static void TestAsleepConditionDefinitionExists()
    {
        ConditionDefinition def = ConditionRules.GetDefinition(CombatConditionType.Asleep);
        Assert(def != null, "Asleep condition definition exists");
        if (def == null)
            return;

        Assert(def.PreventsStandardActions && def.PreventsMovement,
            "Asleep blocks actions and movement");
        Assert(def.CoupDeGraceVulnerable,
            "Asleep is helpless-like and coup de grace vulnerable");
    }

    private static void TestTryWakeSleepingCharacterRemovesAsleepState()
    {
        GameObject gmObject = new GameObject("SleepRules_GameManager");
        GameManager gm = gmObject.AddComponent<GameManager>();
        ConditionService conditionService = gmObject.AddComponent<ConditionService>();

        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Caster", "Wizard", 3, 3), CharacterTeam.Player, new Vector2Int(2, 2));
            target = CreateController(BuildStats("Target", "Fighter", 2, 2), CharacterTeam.Player, new Vector2Int(3, 2));

            gm.PCs.Add(caster);
            gm.PCs.Add(target);

            conditionService.Initialize(() =>
            {
                var all = new List<CharacterController>();
                all.AddRange(gm.PCs);
                all.AddRange(gm.NPCs);
                return all;
            });

            FieldInfo conditionField = typeof(GameManager).GetField("_conditionService", BindingFlags.NonPublic | BindingFlags.Instance);
            conditionField?.SetValue(gm, conditionService);

            var asleepData = new AsleepConditionData
            {
                Caster = caster,
                CasterName = caster.Stats.CharacterName,
                RemainingRounds = 10,
                WakeDC = 14,
                SourceSpellId = "sleep",
                SourceEffectName = "Sleep"
            };

            conditionService.ApplyCondition(
                target,
                CombatConditionType.Asleep,
                10,
                source: caster,
                data: asleepData,
                sourceNameOverride: "Sleep",
                sourceCategory: "Spell",
                sourceId: "sleep");
            conditionService.ApplyCondition(
                target,
                CombatConditionType.Unconscious,
                10,
                source: caster,
                sourceNameOverride: "Sleep",
                sourceCategory: "Spell",
                sourceId: "sleep");

            Assert(conditionService.HasCondition(target, CombatConditionType.Asleep), "Precondition: target starts asleep");

            bool woke = gm.TryWakeSleepingCharacter(target, "unit test", caster, suppressLog: true);
            Assert(woke, "TryWakeSleepingCharacter returns true for sleeping target");
            Assert(!conditionService.HasCondition(target, CombatConditionType.Asleep), "Asleep removed after wake");
            Assert(!conditionService.HasCondition(target, CombatConditionType.Unconscious), "Unconscious removed after wake");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            Object.DestroyImmediate(gmObject);
        }
    }

    private static void TestAidAnotherAvailableForAdjacentSleepingAlly()
    {
        GameObject gmObject = new GameObject("SleepRules_AidAnother_GameManager");
        GameManager gm = gmObject.AddComponent<GameManager>();
        ConditionService conditionService = gmObject.AddComponent<ConditionService>();

        CharacterController aider = null;
        CharacterController sleeper = null;

        try
        {
            aider = CreateController(BuildStats("Aider", "Fighter", 3, 3), CharacterTeam.Player, new Vector2Int(5, 5));
            sleeper = CreateController(BuildStats("Sleeper", "Wizard", 2, 2), CharacterTeam.Player, new Vector2Int(6, 5));

            gm.PCs.Add(aider);
            gm.PCs.Add(sleeper);

            conditionService.Initialize(() =>
            {
                var all = new List<CharacterController>();
                all.AddRange(gm.PCs);
                all.AddRange(gm.NPCs);
                return all;
            });

            FieldInfo conditionField = typeof(GameManager).GetField("_conditionService", BindingFlags.NonPublic | BindingFlags.Instance);
            conditionField?.SetValue(gm, conditionService);

            conditionService.ApplyCondition(
                sleeper,
                CombatConditionType.Asleep,
                10,
                source: aider,
                data: new AsleepConditionData { Caster = aider, CasterName = "Aider", RemainingRounds = 10, WakeDC = 10, SourceSpellId = "sleep", SourceEffectName = "Sleep" },
                sourceNameOverride: "Sleep",
                sourceCategory: "Spell",
                sourceId: "sleep");
            conditionService.ApplyCondition(
                sleeper,
                CombatConditionType.Unconscious,
                10,
                source: aider,
                sourceNameOverride: "Sleep",
                sourceCategory: "Spell",
                sourceId: "sleep");

            bool canAid = gm.CanUseAidAnother(aider, out string reason);
            Assert(canAid, "Aid Another is available when adjacent ally is asleep");
            Assert(string.IsNullOrEmpty(reason), "Aid Another wake path has no failure reason", $"reason='{reason}'");
        }
        finally
        {
            DestroyController(aider);
            DestroyController(sleeper);
            Object.DestroyImmediate(gmObject);
        }
    }
}
}
