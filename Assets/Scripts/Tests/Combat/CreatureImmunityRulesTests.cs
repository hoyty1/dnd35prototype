using UnityEngine;
using Tests.Utilities;

namespace Tests.Combat
{
/// <summary>
/// Regression tests for reusable creature immunity/mindless rules (D&D 3.5e).
///
/// Sources:
/// - Monster Manual (3.5): Lemure (devil traits: poison + fire immunity)
/// - SRD/Monster Manual glossary: Mindless creatures are immune to mind-affecting effects.
/// </summary>
public static class CreatureImmunityRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== CREATURE IMMUNITY RULES TESTS ======");

        TestHelpers.EnsureCoreDatabasesInitialized();
        NPCDatabase.Init();

        TestLemureDefinitionCarriesMindlessAndImmunityFlags();
        TestLemurePoisonImmunityPreventsPoisonApplication();
        TestLemureFireImmunityBlocksFireDamage();
        TestLemureMindAffectingImmunityBlocksCharmPerson();
        TestNonImmuneTargetCanStillReceivePoison();

        Debug.Log($"====== Creature Immunity Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildLemureLikeStats()
    {
        CharacterStats stats = TestHelpers.CreateStats(
            name: "Lemure",
            level: 2,
            characterClass: "Warrior",
            str: 11,
            dex: 10,
            con: 12,
            wis: 11,
            intelligence: 0,
            cha: 5,
            bab: 2,
            raceName: "Human");

        stats.CreatureType = "Outsider";
        stats.Immunities = ImmunityPresets.Combine(
            ImmunityPresets.DevilImmunities(),
            ImmunityPresets.MindlessImmunities());
        stats.AddDamageImmunity(DamageType.Fire);
        stats.ApplyMindlessTrait(true);
        return stats;
    }

    private static void TestLemureDefinitionCarriesMindlessAndImmunityFlags()
    {
        NPCDefinition lemure = NPCDatabase.Get("lemure");
        bool ok = lemure != null
                  && lemure.IsMindless
                  && lemure.Immunities != null
                  && lemure.Immunities.immuneToPoison
                  && lemure.Immunities.immuneToFire
                  && lemure.Immunities.immuneToMindAffecting;

        Assert(ok,
            "Lemure definition has mindless + poison/fire/mind-affecting immunities",
            lemure == null
                ? "(lemure missing)"
                : $"(mindless={lemure.IsMindless}, poison={lemure.Immunities?.immuneToPoison}, fire={lemure.Immunities?.immuneToFire}, mind={lemure.Immunities?.immuneToMindAffecting})");
    }

    private static void TestLemurePoisonImmunityPreventsPoisonApplication()
    {
        CharacterController lemureController = null;
        try
        {
            GameObject go = new GameObject("Lemure_Poison_Test");
            lemureController = go.AddComponent<CharacterController>();
            CharacterStats lemureStats = BuildLemureLikeStats();
            lemureController.Init(lemureStats, Vector2Int.zero, null, null);

            lemureController.ApplyPoison("black_adder_venom");

            Assert(lemureController.ActivePoisons.Count == 0,
                "Poison immunity prevents poison from being applied to Lemure",
                $"(activePoisons={lemureController.ActivePoisons.Count})");
        }
        finally
        {
            TestHelpers.Cleanup(lemureController != null ? lemureController.gameObject : null);
        }
    }

    private static void TestLemureFireImmunityBlocksFireDamage()
    {
        CharacterStats lemure = BuildLemureLikeStats();
        int hpBefore = lemure.CurrentHP;

        DamagePacket packet = new DamagePacket
        {
            RawDamage = 12,
            Types = new System.Collections.Generic.HashSet<DamageType> { DamageType.Fire },
            Source = AttackSource.Spell,
            SourceName = "Test Fire"
        };

        DamageResolutionResult result = lemure.ApplyIncomingDamage(12, packet);

        bool ok = result.ImmunityTriggered && result.FinalDamage == 0 && lemure.CurrentHP == hpBefore;
        Assert(ok,
            "Fire immunity blocks fire damage on Lemure",
            $"(immunityTriggered={result.ImmunityTriggered}, final={result.FinalDamage}, hpBefore={hpBefore}, hpAfter={lemure.CurrentHP})");
    }

    private static void TestLemureMindAffectingImmunityBlocksCharmPerson()
    {
        CharacterStats caster = TestHelpers.CreateStats(name: "Wizard", characterClass: "Wizard", level: 5, intelligence: 18, bab: 2);
        CharacterStats lemure = BuildLemureLikeStats();
        SpellData charm = SpellDatabase.GetSpell(DND35e.Identifiers.SpellNames.CHARM_PERSON);

        SpellResult result = SpellCaster.Cast(charm, caster, lemure);

        Assert(result != null && result.MindAffectingImmunityBlocked,
            "Mindless Lemure blocks mind-affecting Charm Person",
            result == null ? "(no result)" : $"(blocked={result.MindAffectingImmunityBlocked}, reason={result.NoEffectReason})");
    }

    private static void TestNonImmuneTargetCanStillReceivePoison()
    {
        CharacterController target = null;
        try
        {
            target = TestHelpers.CreateCharacter(name: "Non-Immune Target", characterClass: "Fighter", level: 3, intelligence: 10);
            target.ApplyPoison("black_adder_venom");

            Assert(target.ActivePoisons.Count >= 1,
                "Non-immune target can still receive poison",
                $"(activePoisons={target.ActivePoisons.Count})");
        }
        finally
        {
            TestHelpers.Cleanup(target != null ? target.gameObject : null);
        }
    }
}
}
