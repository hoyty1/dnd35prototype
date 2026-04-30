using System.Reflection;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Hypnotism + Fascinated mechanics.
/// Run with HypnotismRulesTests.RunAll().
/// </summary>
public static class HypnotismRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== HYPNOTISM RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestHypnotismDefinitionMatchesCoreRules();
        TestFascinatedConditionDefinitionExists();
        TestFascinatedAppliesListenSpotPenalty();
        TestFascinationBreaksOnHostileActionFromSourceSide();

        Debug.Log($"====== Hypnotism Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name, string className, int level, int bab)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: 12,
            dex: 12,
            con: 12,
            wis: 14,
            intelligence: 14,
            cha: 12,
            bab: bab,
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
        return stats;
    }

    private static CharacterController CreateController(CharacterStats stats, CharacterTeam team, Vector2Int gridPos)
    {
        GameObject go = new GameObject($"HypnotismTest_{stats.CharacterName}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Stats = stats;
        controller.SetTeam(team);
        controller.GridPosition = gridPos;

        InventoryComponent inv = go.AddComponent<InventoryComponent>();
        inv.Init(stats);

        SpellcastingComponent spellComp = go.AddComponent<SpellcastingComponent>();
        spellComp.Init(stats);

        StatusEffectManager statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static void TestHypnotismDefinitionMatchesCoreRules()
    {
        SpellData hypnotism = SpellDatabase.GetSpell("hypnotism");
        Assert(hypnotism != null, "Hypnotism spell definition exists");
        if (hypnotism == null)
            return;

        Assert(!hypnotism.IsPlaceholder, "Hypnotism is implemented (not placeholder)");
        Assert(hypnotism.TargetType == SpellTargetType.Area, "Hypnotism is area-targeted");
        Assert(hypnotism.RangeCategory == SpellRangeCategory.Close, "Hypnotism uses Close range");
        Assert(hypnotism.AoEShapeType == AoEShape.Burst && hypnotism.AoESizeSquares == 3,
            "Hypnotism uses 15-ft burst",
            $"shape={hypnotism.AoEShapeType}, size={hypnotism.AoESizeSquares}");
        Assert(hypnotism.AllowsSavingThrow && hypnotism.SavingThrowType == "Will", "Hypnotism uses Will save negates");
        Assert(hypnotism.SpellResistanceApplies, "Hypnotism allows Spell Resistance");
        Assert(hypnotism.IsMindAffecting, "Hypnotism is mind-affecting");
    }

    private static void TestFascinatedConditionDefinitionExists()
    {
        ConditionDefinition def = ConditionRules.GetDefinition(CombatConditionType.Fascinated);
        Assert(def != null, "Fascinated condition definition exists");
        if (def == null)
            return;

        Assert(def.PreventsStandardActions && def.PreventsMovement, "Fascinated blocks actions and movement");
        Assert(def.PreventsAoO && def.PreventsThreatening, "Fascinated blocks AoOs/threatening");
    }

    private static void TestFascinatedAppliesListenSpotPenalty()
    {
        CharacterStats stats = BuildStats("Scout", "Rogue", 3, 2);
        int baseListen = stats.GetConditionSkillModifier("Listen");
        int baseSpot = stats.GetConditionSkillModifier("Spot");

        stats.ApplyCondition(CombatConditionType.Fascinated, 3, "Hypnotism");

        int fascListen = stats.GetConditionSkillModifier("Listen");
        int fascSpot = stats.GetConditionSkillModifier("Spot");

        Assert(fascListen <= baseListen - 4, "Fascinated applies -4 to Listen checks");
        Assert(fascSpot <= baseSpot - 4, "Fascinated applies -4 to Spot checks");
    }

    private static void TestFascinationBreaksOnHostileActionFromSourceSide()
    {
        GameObject gmObject = new GameObject("HypnotismRulesTest_GameManager");
        GameManager gm = gmObject.AddComponent<GameManager>();
        ConditionService conditionService = gmObject.AddComponent<ConditionService>();

        CharacterController caster = null;
        CharacterController ally = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Caster", "Wizard", 3, 1), CharacterTeam.Player, new Vector2Int(2, 2));
            ally = CreateController(BuildStats("Ally", "Fighter", 3, 3), CharacterTeam.Player, new Vector2Int(3, 2));
            target = CreateController(BuildStats("Target", "Fighter", 3, 3), CharacterTeam.Enemy, new Vector2Int(4, 2));

            gm.PCs.Add(caster);
            gm.PCs.Add(ally);
            gm.NPCs.Add(target);

            conditionService.Initialize(() =>
            {
                var all = new System.Collections.Generic.List<CharacterController>();
                all.AddRange(gm.PCs);
                all.AddRange(gm.NPCs);
                return all;
            });

            FieldInfo conditionField = typeof(GameManager).GetField("_conditionService", BindingFlags.NonPublic | BindingFlags.Instance);
            conditionField?.SetValue(gm, conditionService);

            var data = new FascinatedConditionData
            {
                Caster = caster,
                CasterName = caster.Stats.CharacterName,
                DisturbanceSaveDC = 1,
                SourceSpellId = "hypnotism",
                SourceEffectName = "Hypnotism"
            };

            conditionService.ApplyCondition(
                target,
                CombatConditionType.Fascinated,
                3,
                source: caster,
                data: data,
                sourceNameOverride: "Hypnotism",
                sourceCategory: "Spell",
                sourceId: "hypnotism");

            Assert(conditionService.HasCondition(target, CombatConditionType.Fascinated), "Precondition: target starts fascinated");

            gm.BreakFascinationOnHostileAction(ally, target, "attack");

            Assert(!conditionService.HasCondition(target, CombatConditionType.Fascinated),
                "Fascinated breaks when source-side ally performs hostile action");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(ally);
            DestroyController(target);
            Object.DestroyImmediate(gmObject);
        }
    }
}
}
