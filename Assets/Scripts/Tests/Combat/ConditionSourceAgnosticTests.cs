using System.Collections.Generic;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Validates that one condition type can be applied from multiple source categories
/// (poisons, spells, abilities) through ConditionService.
/// Run with ConditionSourceAgnosticTests.RunAll().
/// </summary>
public static class ConditionSourceAgnosticTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== CONDITION SOURCE-AGNOSTIC TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();

        TestMultipleSourcesRefreshSameCondition();

        Debug.Log($"====== Condition Source-Agnostic Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats MakeStats(string name)
    {
        return new CharacterStats(name, 5, "Cleric",
            14, 12, 14, 10, 12, 16,
            4, 1, 4,
            8, 1, 0,
            6, 1, 30,
            "Human");
    }

    private static CharacterController BuildCharacter(string name)
    {
        var go = new GameObject($"ConditionSourceTest_{name}");
        var controller = go.AddComponent<CharacterController>();
        controller.Stats = MakeStats(name);
        return controller;
    }

    private static void TestMultipleSourcesRefreshSameCondition()
    {
        CharacterController target = null;
        CharacterController caster = null;
        ConditionService service = null;
        GameObject serviceObject = null;

        try
        {
            target = BuildCharacter("Target");
            caster = BuildCharacter("Caster");

            List<CharacterController> characters = new List<CharacterController> { target, caster };

            serviceObject = new GameObject("ConditionServiceTestHost");
            service = serviceObject.AddComponent<ConditionService>();
            service.Initialize(() => characters);

            service.ApplyCondition(
                target,
                CombatConditionType.Paralyzed,
                2,
                source: null,
                sourceNameOverride: "Carrion Crawler Poison",
                sourceCategory: "Poison",
                sourceId: "carrion_crawler_poison");

            Assert(service.HasCondition(target, CombatConditionType.Paralyzed),
                "Poison can apply Paralyzed through ConditionService");

            ConditionService.ActiveCondition poisonApplied = FindCondition(service, target, CombatConditionType.Paralyzed);
            Assert(poisonApplied != null && poisonApplied.SourceCategory == "Poison",
                "Poison source metadata tracked",
                $"expected category Poison, got {poisonApplied?.SourceCategory ?? "null"}");

            service.ApplyCondition(
                target,
                CombatConditionType.Paralyzed,
                5,
                source: caster,
                sourceNameOverride: "Hold Person",
                sourceCategory: "Spell",
                sourceId: "hold_person");

            ConditionService.ActiveCondition spellApplied = FindCondition(service, target, CombatConditionType.Paralyzed);
            Assert(spellApplied != null && spellApplied.Source == caster,
                "Spell source character tracked on same condition");
            Assert(spellApplied != null && spellApplied.SourceCategory == "Spell",
                "Spell source category tracked on same condition",
                $"expected Spell, got {spellApplied?.SourceCategory ?? "null"}");
            Assert(service.GetConditionDuration(target, CombatConditionType.Paralyzed) == 5,
                "Reapplying same condition from new source refreshes to stronger duration",
                $"expected 5, got {service.GetConditionDuration(target, CombatConditionType.Paralyzed)}");

            int paralyzedCount = CountCondition(service, target, CombatConditionType.Paralyzed);
            Assert(paralyzedCount == 1,
                "Same condition type remains a single active entry",
                $"expected 1, got {paralyzedCount}");

            service.ApplyCondition(
                target,
                CombatConditionType.Paralyzed,
                1,
                source: caster,
                sourceNameOverride: "Stunning Fist",
                sourceCategory: "Ability",
                sourceId: "stunning_fist");

            Assert(service.GetConditionDuration(target, CombatConditionType.Paralyzed) == 5,
                "Shorter reapply from another source does not reduce existing duration",
                $"expected 5, got {service.GetConditionDuration(target, CombatConditionType.Paralyzed)}");

            ConditionService.ActiveCondition abilityApplied = FindCondition(service, target, CombatConditionType.Paralyzed);
            Assert(abilityApplied != null && abilityApplied.SourceCategory == "Ability",
                "Ability source metadata can also drive the same condition",
                $"expected Ability, got {abilityApplied?.SourceCategory ?? "null"}");
        }
        finally
        {
            if (target != null)
                Object.DestroyImmediate(target.gameObject);
            if (caster != null)
                Object.DestroyImmediate(caster.gameObject);
            if (serviceObject != null)
                Object.DestroyImmediate(serviceObject);
        }
    }

    private static ConditionService.ActiveCondition FindCondition(ConditionService service, CharacterController target, CombatConditionType type)
    {
        if (service == null || target == null)
            return null;

        List<ConditionService.ActiveCondition> active = service.GetActiveConditions(target);
        CombatConditionType normalized = ConditionRules.Normalize(type);
        for (int i = 0; i < active.Count; i++)
        {
            if (ConditionRules.Normalize(active[i].Type) == normalized)
                return active[i];
        }

        return null;
    }

    private static int CountCondition(ConditionService service, CharacterController target, CombatConditionType type)
    {
        if (service == null || target == null)
            return 0;

        List<ConditionService.ActiveCondition> active = service.GetActiveConditions(target);
        CombatConditionType normalized = ConditionRules.Normalize(type);

        int count = 0;
        for (int i = 0; i < active.Count; i++)
        {
            if (ConditionRules.Normalize(active[i].Type) == normalized)
                count++;
        }

        return count;
    }
}
}
