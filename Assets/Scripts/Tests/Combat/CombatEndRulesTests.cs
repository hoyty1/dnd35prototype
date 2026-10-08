using System.Collections.Generic;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// The combat-end predicate, case by case (CombatEndRules; owner definition 2026-10-07; CORE-011, CORE-034, CORE-037).
/// Written from the definition, not from the code, so that it pins the predicate independently of the scenario harness
/// (whose checks call the same predicate). A creature is out of the fight when it is dead, dying or unconscious:
/// stable, nonlethal damage above its HP, the Unconscious condition, asleep and petrified (PHB p.311, DMG p.301) count;
/// regeneration or fast healing does not keep a downed creature in. A disabled creature is in: at 0 HP (PHB p.145), and
/// with Diehard at negative HP, also after healing there (PHB p.93). A side is out when it has, or had in this combat,
/// a member and none is in; Neutral creatures belong to neither side.
/// Play mode only: CharacterController.Init needs Awake (TST-009).
/// </summary>
public static class CombatEndRulesTests
{
    private static int _passed;
    private static int _failed;
    private static readonly List<CharacterController> _created = new List<CharacterController>();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== COMBAT END RULES TESTS ==========");

        try
        {
            TestHealthyIsIn();
            TestDisabledAtZeroIsIn();
            TestDyingIsOut();
            TestDyingWithFastHealingIsOut();
            TestDeadIsOut();
            TestDiehardNegativeIsIn();
            TestDiehardHealedAtNegativeStaysIn();
            TestStableIsOut();
            TestNonlethalAboveHpIsOut();
            TestUnconsciousConditionIsOut();
            TestAsleepOnlyIsOut();
            TestPetrifiedIsOut();
            TestMissingCreatureIsOut();
            TestCountSidesAndNeutral();
            TestEmptySide();
        }
        finally
        {
            foreach (CharacterController c in _created)
                if (c != null)
                    Object.DestroyImmediate(c.gameObject);
            _created.Clear();
        }

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    private static void Assert(bool condition, string testName)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}");
        }
    }

    /// <summary>A level 2 fighter with 12 max HP, on <paramref name="team"/>.</summary>
    private static CharacterController Create(string name, CharacterTeam team = CharacterTeam.Player, params string[] feats)
    {
        var go = new GameObject($"{name}_GO");
        var controller = go.AddComponent<CharacterController>();
        var stats = new CharacterStats(name, 2, "Fighter", 14, 12, 12, 10, 10, 10, 2, 0, 0, 8, 1, 0, 4, 1, 12);
        foreach (string f in feats)
            stats.Feats.Add(f);
        controller.Init(stats, Vector2Int.zero, null, null);
        controller.SetTeam(team);
        _created.Add(controller);
        return controller;
    }

    private static string State(CharacterController c) => $"HP {c.Stats.CurrentHP}, state {c.CurrentHPState}";

    private static void TestHealthyIsIn()
    {
        var c = Create("CE_Healthy");
        Assert(!CombatEndRules.IsOutOfFight(c), $"A healthy creature is in ({State(c)})");
    }

    private static void TestDisabledAtZeroIsIn()
    {
        var c = Create("CE_Disabled");
        c.Stats.CurrentHP = 0;
        Assert(c.CurrentHPState == HPState.Disabled, $"0 HP is disabled (PHB p.145) ({State(c)})");
        Assert(!CombatEndRules.IsOutOfFight(c), $"A disabled creature at 0 HP is still in ({State(c)})");
    }

    private static void TestDyingIsOut()
    {
        var c = Create("CE_Dying");
        c.Stats.CurrentHP = -1;
        Assert(CombatEndRules.IsOutOfFight(c), $"A dying creature at -1 HP is out ({State(c)})");
    }

    private static void TestDyingWithFastHealingIsOut()
    {
        var c = Create("CE_FastHealing", CharacterTeam.Enemy);
        c.Stats.SpecialAbilities.Add("Fast Healing 2");
        c.Stats.CurrentHP = -3;
        Assert(CombatEndRules.IsOutOfFight(c), $"A downed creature with fast healing is out (CORE-034) ({State(c)})");
    }

    private static void TestDeadIsOut()
    {
        var c = Create("CE_Dead");
        c.Stats.CurrentHP = -10;
        Assert(CombatEndRules.IsOutOfFight(c), $"A dead creature at -10 HP is out ({State(c)})");
    }

    private static void TestDiehardNegativeIsIn()
    {
        var c = Create("CE_Diehard", CharacterTeam.Player, "Diehard");
        c.Stats.CurrentHP = -4;
        Assert(c.CurrentHPState == HPState.Disabled, $"Diehard at -4 HP acts as disabled (PHB p.93) ({State(c)})");
        Assert(!CombatEndRules.IsOutOfFight(c), $"Diehard at negative HP is still in ({State(c)})");
    }

    private static void TestDiehardHealedAtNegativeStaysIn()
    {
        var c = Create("CE_DiehardHealed", CharacterTeam.Player, "Diehard");
        c.Stats.CurrentHP = -5;
        c.Stats.CurrentHP = -2; // healed, still below 0
        Assert(c.CurrentHPState == HPState.Disabled, $"Diehard healed from -5 to -2 stays disabled, not stable (PHB p.93) ({State(c)})");
        Assert(!CombatEndRules.IsOutOfFight(c), $"Diehard healed at negative HP is still in ({State(c)})");
    }

    private static void TestStableIsOut()
    {
        var c = Create("CE_Stable");
        c.Stats.CurrentHP = -5;
        c.Stats.CurrentHP = -3; // healed, still below 0: stable
        Assert(c.CurrentHPState == HPState.Stable, $"Healed at negative HP without Diehard is stable ({State(c)})");
        Assert(CombatEndRules.IsOutOfFight(c), $"A stable creature is out (stable is unconscious, PHB p.145) ({State(c)})");
    }

    private static void TestNonlethalAboveHpIsOut()
    {
        var c = Create("CE_Nonlethal");
        c.Stats.ApplyNonlethalDamage(c.Stats.CurrentHP + 1);
        Assert(c.CurrentHPState == HPState.Unconscious, $"Nonlethal damage above HP knocks out (PHB p.146) ({State(c)})");
        Assert(CombatEndRules.IsOutOfFight(c), $"A creature knocked out by nonlethal damage is out ({State(c)})");
    }

    private static void TestUnconsciousConditionIsOut()
    {
        var c = Create("CE_UnconsciousCond", CharacterTeam.Enemy);
        c.ApplyCondition(CombatConditionType.Unconscious, -1, "test");
        Assert(c.Stats.CurrentHP > 0, $"fixture: full HP ({State(c)})");
        Assert(CombatEndRules.IsOutOfFight(c), $"The Unconscious condition at full HP is out ({State(c)})");
    }

    private static void TestAsleepOnlyIsOut()
    {
        var c = Create("CE_Asleep", CharacterTeam.Enemy);
        c.ApplyCondition(CombatConditionType.Asleep, 100, "test"); // a sleep arrow applies Asleep alone (DMG p.228)
        Assert(c.HasConditionDirect(CombatConditionType.Asleep) && !c.HasConditionDirect(CombatConditionType.Unconscious),
            "fixture: Asleep without Unconscious");
        Assert(CombatEndRules.IsOutOfFight(c), $"A creature asleep (Asleep only) is out ({State(c)})");
    }

    private static void TestPetrifiedIsOut()
    {
        var c = Create("CE_Petrified");
        c.ApplyCondition(CombatConditionType.Petrified, -1, "test");
        Assert(c.HasConditionDirect(CombatConditionType.Petrified), "fixture: Petrified applied");
        Assert(CombatEndRules.IsOutOfFight(c), $"A petrified creature is out: considered unconscious (PHB p.311, DMG p.301) ({State(c)})");
    }

    private static void TestMissingCreatureIsOut()
    {
        Assert(CombatEndRules.IsOutOfFight(null), "A missing creature is out");
    }

    private static void TestCountSidesAndNeutral()
    {
        var hero = Create("CE_CountHero", CharacterTeam.Player);
        var ally = Create("CE_CountAlly", CharacterTeam.Player);
        var foe = Create("CE_CountFoe", CharacterTeam.Enemy);
        var bystander = Create("CE_CountNeutral", CharacterTeam.Neutral);
        hero.Stats.CurrentHP = -2;  // dying: out
        ally.Stats.CurrentHP = 0;   // disabled: in
        foe.Stats.CurrentHP = -1;   // dying: out

        var all = new List<CharacterController> { hero, ally, foe, bystander };
        CombatEndRules.SideCounts s = CombatEndRules.Count(all);
        Assert(s.PlayersAll == 2 && s.PlayersIn == 1, $"Party side: 2 members, 1 in (got {s.PlayersAll}/{s.PlayersIn})");
        Assert(s.EnemiesAll == 1 && s.EnemiesIn == 0, $"Hostile side: 1 member, 0 in; the Neutral creature is ignored (got {s.EnemiesAll}/{s.EnemiesIn})");
        Assert(!s.PlayersOut, "A disabled ally keeps the party side in");
        Assert(s.EnemiesOut, "The hostile side is out");

        ally.Stats.CurrentHP = -1;
        s = CombatEndRules.Count(all);
        Assert(s.PlayersOut && s.EnemiesOut, "Both sides out when the ally drops too (EvaluateCombatEnd treats this as a defeat, CORE-038)");

        bystander.Stats.CurrentHP = -1;
        s = CombatEndRules.Count(new List<CharacterController> { bystander });
        Assert(!s.PlayersOut && !s.EnemiesOut, "A Neutral creature alone makes no side");
    }

    private static void TestEmptySide()
    {
        var hero = Create("CE_EmptyHero", CharacterTeam.Player);
        var foe = Create("CE_EmptyFoe", CharacterTeam.Enemy);
        foe.gameObject.SetActive(false); // trapped, destroyed or removed during the combat

        CombatEndRules.SideCounts s = CombatEndRules.Count(new List<CharacterController> { hero, foe });
        Assert(s.EnemiesAll == 0, $"An inactive creature is not counted (got {s.EnemiesAll})");
        Assert(!s.EnemiesOut, "A side that never had a member is not out");
        s.EnemiesHadMembers = true;
        Assert(s.EnemiesOut, "A side that had a member earlier in the combat and has none left is out");
        Assert(!s.PlayersOut, "The party side with a healthy hero is in");
    }
}
}
