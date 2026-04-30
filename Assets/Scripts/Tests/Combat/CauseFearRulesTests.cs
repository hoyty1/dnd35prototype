using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Cause Fear + Frightened mechanics.
/// Run with CauseFearRulesTests.RunAll().
/// </summary>
public static class CauseFearRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== CAUSE FEAR RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestCauseFearDefinitionMatchesCoreRules();
        TestFrightenedConditionPenaltiesIncludeAbilityChecks();
        TestCauseFearLowHdFailedSaveAppliesFrightened();
        TestCauseFearSuccessfulSaveAppliesShaken();
        TestCauseFearHighHdIsTooPowerful();
        TestCauseFearUndeadIsImmune();
        TestFrightenedBehaviorDecisionTracksFearSource();

        Debug.Log($"====== Cause Fear Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name, string className, int level, int hitDice, string creatureType = "Humanoid")
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

        stats.CreatureType = creatureType;
        stats.HitDice = hitDice;
        return stats;
    }

    private static CharacterController CreateController(CharacterStats stats, CharacterTeam team, Vector2Int gridPos)
    {
        GameObject go = new GameObject($"CauseFearRules_{stats.CharacterName}");
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

    private static GameManager BuildGameManagerWithConditionService(out ConditionService conditionService)
    {
        GameObject gmObject = new GameObject("CauseFearRules_GameManager");
        GameManager gm = gmObject.AddComponent<GameManager>();
        conditionService = gmObject.AddComponent<ConditionService>();

        conditionService.Initialize(() =>
        {
            var all = new List<CharacterController>();
            all.AddRange(gm.PCs);
            all.AddRange(gm.NPCs);
            return all;
        });

        FieldInfo conditionField = typeof(GameManager).GetField("_conditionService", BindingFlags.NonPublic | BindingFlags.Instance);
        conditionField?.SetValue(gm, conditionService);

        return gm;
    }

    private static bool InvokeResolveCauseFear(GameManager gm, CharacterController caster, CharacterController target, SpellData spell, SpellResult result)
    {
        MethodInfo resolve = typeof(GameManager).GetMethod("TryResolveCauseFearSpellEffect", BindingFlags.NonPublic | BindingFlags.Instance);
        if (resolve == null)
            return false;

        object handled = resolve.Invoke(gm, new object[] { caster, target, spell, result });
        return handled is bool b && b;
    }

    private static void TestCauseFearDefinitionMatchesCoreRules()
    {
        SpellData causeFear = SpellDatabase.GetSpell("cause_fear");
        Assert(causeFear != null, "Cause Fear spell definition exists");
        if (causeFear == null)
            return;

        Assert(causeFear.TargetType == SpellTargetType.SingleEnemy, "Cause Fear targets one creature");
        Assert(causeFear.RangeCategory == SpellRangeCategory.Close, "Cause Fear uses Close range");
        Assert(causeFear.AllowsSavingThrow && causeFear.SavingThrowType == "Will", "Cause Fear uses Will save");
        Assert(causeFear.SpellResistanceApplies, "Cause Fear allows spell resistance");
        Assert(causeFear.IsMindAffecting, "Cause Fear is mind-affecting");
        Assert(causeFear.School == "Necromancy", "Cause Fear school is Necromancy");
    }

    private static void TestFrightenedConditionPenaltiesIncludeAbilityChecks()
    {
        ConditionDefinition def = ConditionRules.GetDefinition(CombatConditionType.Frightened);
        Assert(def != null, "Frightened condition definition exists");
        if (def == null)
            return;

        Assert(def.AttackModifier <= -2, "Frightened applies -2 attack penalty", $"attack={def.AttackModifier}");
        Assert(def.FortitudeModifier <= -2 && def.ReflexModifier <= -2 && def.WillModifier <= -2,
            "Frightened applies -2 save penalties",
            $"fort={def.FortitudeModifier}, ref={def.ReflexModifier}, will={def.WillModifier}");
        Assert(def.SkillCheckModifier <= -2, "Frightened applies -2 skill penalties", $"skill={def.SkillCheckModifier}");
        Assert(def.AbilityCheckModifier <= -2, "Frightened applies -2 ability-check penalties", $"ability={def.AbilityCheckModifier}");

        CharacterStats stats = BuildStats("PenaltyProbe", "Fighter", 3, 3);
        stats.ApplyCondition(CombatConditionType.Frightened, 2, "Cause Fear");
        Assert(stats.ConditionAttackPenalty <= -2, "Frightened propagates into attack penalty");
        Assert(stats.ConditionWillModifier <= -2, "Frightened propagates into Will penalty");
        Assert(stats.GetConditionSkillModifier("Spot") <= -2, "Frightened propagates into skill penalty");
        Assert(stats.ConditionAbilityCheckModifier <= -2, "Frightened propagates into ability-check penalty");
    }

    private static void TestCauseFearLowHdFailedSaveAppliesFrightened()
    {
        ConditionService service;
        GameManager gm = BuildGameManagerWithConditionService(out service);
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Wizard", "Wizard", 3, 3), CharacterTeam.Player, new Vector2Int(1, 1));
            target = CreateController(BuildStats("Goblin", "Fighter", 2, 2), CharacterTeam.Enemy, new Vector2Int(3, 1));
            gm.PCs.Add(caster);
            gm.NPCs.Add(target);

            SpellData spell = SpellDatabase.GetSpell("cause_fear");
            SpellResult result = new SpellResult
            {
                Spell = spell,
                CasterName = caster.Stats.CharacterName,
                TargetName = target.Stats.CharacterName,
                RequiredSave = true,
                SaveSucceeded = false,
                Success = true
            };

            bool handled = InvokeResolveCauseFear(gm, caster, target, spell, result);
            Assert(handled, "Cause Fear low-HD failed save path handled");
            Assert(service.HasCondition(target, CombatConditionType.Frightened), "Low-HD target becomes frightened on failed save");

            List<ConditionService.ActiveCondition> active = service.GetActiveConditions(target);
            FrightenedConditionData fearData = null;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] != null && ConditionRules.Normalize(active[i].Type) == CombatConditionType.Frightened)
                {
                    fearData = active[i].Data as FrightenedConditionData;
                    break;
                }
            }

            Assert(fearData != null, "Frightened condition stores runtime data payload");
            if (fearData != null)
            {
                Assert(fearData.Caster == caster, "Frightened source caster tracked");
                Assert(fearData.RemainingRounds >= 1 && fearData.RemainingRounds <= 4,
                    "Frightened duration rolled in 1d4 range",
                    $"rounds={fearData.RemainingRounds}");
            }
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            Object.DestroyImmediate(gm.gameObject);
        }
    }

    private static void TestCauseFearSuccessfulSaveAppliesShaken()
    {
        ConditionService service;
        GameManager gm = BuildGameManagerWithConditionService(out service);
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Wizard", "Wizard", 3, 3), CharacterTeam.Player, new Vector2Int(1, 1));
            target = CreateController(BuildStats("Guard", "Fighter", 3, 3), CharacterTeam.Enemy, new Vector2Int(3, 1));
            gm.PCs.Add(caster);
            gm.NPCs.Add(target);

            SpellData spell = SpellDatabase.GetSpell("cause_fear");
            SpellResult result = new SpellResult
            {
                Spell = spell,
                CasterName = caster.Stats.CharacterName,
                TargetName = target.Stats.CharacterName,
                RequiredSave = true,
                SaveSucceeded = true,
                Success = true
            };

            bool handled = InvokeResolveCauseFear(gm, caster, target, spell, result);
            Assert(handled, "Cause Fear successful-save path handled");
            Assert(service.HasCondition(target, CombatConditionType.Shaken), "Successful Will save reduces Cause Fear to shaken");
            Assert(!service.HasCondition(target, CombatConditionType.Frightened), "Successful save prevents frightened condition");
            Assert(service.GetConditionDuration(target, CombatConditionType.Shaken) == 1,
                "Successful save applies shaken for exactly 1 round",
                $"duration={service.GetConditionDuration(target, CombatConditionType.Shaken)}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            Object.DestroyImmediate(gm.gameObject);
        }
    }

    private static void TestCauseFearHighHdIsTooPowerful()
    {
        ConditionService service;
        GameManager gm = BuildGameManagerWithConditionService(out service);
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Wizard", "Wizard", 5, 5), CharacterTeam.Player, new Vector2Int(1, 1));
            target = CreateController(BuildStats("Ogre", "Fighter", 8, 8), CharacterTeam.Enemy, new Vector2Int(3, 1));
            gm.PCs.Add(caster);
            gm.NPCs.Add(target);

            SpellData spell = SpellDatabase.GetSpell("cause_fear");
            SpellResult result = new SpellResult
            {
                Spell = spell,
                CasterName = caster.Stats.CharacterName,
                TargetName = target.Stats.CharacterName,
                RequiredSave = true,
                SaveSucceeded = false,
                Success = true
            };

            bool handled = InvokeResolveCauseFear(gm, caster, target, spell, result);
            Assert(handled, "Cause Fear high-HD path handled");
            Assert(!service.HasCondition(target, CombatConditionType.Frightened), "High-HD target is not frightened");
            Assert(!string.IsNullOrWhiteSpace(result.NoEffectReason) && result.NoEffectReason.ToLowerInvariant().Contains("too powerful"),
                "High-HD target reports too-powerful failure",
                $"reason={result.NoEffectReason}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            Object.DestroyImmediate(gm.gameObject);
        }
    }

    private static void TestCauseFearUndeadIsImmune()
    {
        ConditionService service;
        GameManager gm = BuildGameManagerWithConditionService(out service);
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Cleric", "Cleric", 3, 3), CharacterTeam.Player, new Vector2Int(1, 1));
            target = CreateController(BuildStats("Skeleton", "Warrior", 2, 2, creatureType: "Undead"), CharacterTeam.Enemy, new Vector2Int(3, 1));
            gm.PCs.Add(caster);
            gm.NPCs.Add(target);

            SpellData spell = SpellDatabase.GetSpell("cause_fear");
            SpellResult result = new SpellResult
            {
                Spell = spell,
                CasterName = caster.Stats.CharacterName,
                TargetName = target.Stats.CharacterName,
                RequiredSave = false,
                SaveSucceeded = false,
                Success = true
            };

            bool handled = InvokeResolveCauseFear(gm, caster, target, spell, result);
            Assert(handled, "Cause Fear undead-immunity path handled");
            Assert(!service.HasCondition(target, CombatConditionType.Frightened), "Undead target is immune to frightened from Cause Fear");
            Assert(!string.IsNullOrWhiteSpace(result.NoEffectReason) && result.NoEffectReason.ToLowerInvariant().Contains("immune"),
                "Undead immunity sets explicit no-effect reason",
                $"reason={result.NoEffectReason}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            Object.DestroyImmediate(gm.gameObject);
        }
    }

    private static void TestFrightenedBehaviorDecisionTracksFearSource()
    {
        ConditionService service;
        GameManager gm = BuildGameManagerWithConditionService(out service);
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Wizard", "Wizard", 3, 3), CharacterTeam.Player, new Vector2Int(1, 1));
            target = CreateController(BuildStats("Bandit", "Rogue", 3, 3), CharacterTeam.Enemy, new Vector2Int(3, 1));
            gm.PCs.Add(caster);
            gm.NPCs.Add(target);

            var fearData = new FrightenedConditionData
            {
                Caster = caster,
                CasterName = caster.Stats.CharacterName,
                RemainingRounds = 3,
                SourceSpellId = "cause_fear",
                SourceEffectName = "Cause Fear"
            };

            service.ApplyCondition(
                target,
                CombatConditionType.Frightened,
                3,
                source: caster,
                data: fearData,
                sourceNameOverride: "Cause Fear",
                sourceCategory: "Spell",
                sourceId: "cause_fear");

            var controller = new FrightenedBehaviorController();
            bool hasDecision = controller.TryBuildDecision(gm, target, out FrightenedBehaviorController.FrightenedTurnDecision decision);
            Assert(hasDecision, "Frightened behavior builds a decision for frightened actor");
            Assert(decision != null && decision.CasterSource == caster, "Frightened behavior decision keeps the fear source");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            Object.DestroyImmediate(gm.gameObject);
        }
    }
}
}
