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
/// a member and none is in; Neutral creatures belong to neither side. Owner interpretations 2026-10-11 (CORE-038): a
/// hostile creature charmed by or under the control of a party-side creature (Charm Person PHB p.209, Command Undead
/// p.211, Dominate Person p.224, a domain command) counts as out of the hostile side; a party member charmed by the
/// hostile side still counts for the party. When both sides are out at once, the game rolls the dying PCs out
/// (PHB p.145) before deciding; that path is covered by the rules/combat-end-tie-* scenarios.
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
            TestControlledByParty();
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
        Assert(s.PlayersOut && s.EnemiesOut, "Both sides out when the ally drops too (EvaluateCombatEnd then rolls the dying PCs out and the party wins if one survives, CORE-038)");

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

    private static ConditionService.ActiveCondition Condition(CombatConditionType type, CharacterController source, object data = null)
        => new ConditionService.ActiveCondition { Type = type, RemainingRounds = 10, Source = source, Data = data };

    private static void TestControlledByParty()
    {
        var wizard = Create("CE_CtlWizard", CharacterTeam.Player);
        var shaman = Create("CE_CtlShaman", CharacterTeam.Enemy);
        var foe = Create("CE_CtlFoe", CharacterTeam.Enemy);
        var hero = Create("CE_CtlHero", CharacterTeam.Player);

        var charmedByParty = new List<ConditionService.ActiveCondition> { Condition(CombatConditionType.Charmed, wizard) };
        var charmedByData = new List<ConditionService.ActiveCondition>
        {
            Condition(CombatConditionType.Charmed, null, new CharmedConditionData { Caster = wizard, CasterName = "CE_CtlWizard" })
        };
        var commandedByParty = new List<ConditionService.ActiveCondition> { Condition(CombatConditionType.Commanded, wizard) };
        var charmedByFoe = new List<ConditionService.ActiveCondition> { Condition(CombatConditionType.Charmed, shaman) };
        var shaken = new List<ConditionService.ActiveCondition> { Condition(CombatConditionType.Shaken, wizard) };

        Assert(CombatEndRules.IsControlledByTeam(foe, CharacterTeam.Player, charmedByParty), "A foe charmed by a party member is party-controlled (PHB p.209)");
        Assert(CombatEndRules.IsControlledByTeam(foe, CharacterTeam.Player, charmedByData), "The charm data's caster counts when the condition has no tracked source");
        Assert(CombatEndRules.IsControlledByTeam(foe, CharacterTeam.Player, commandedByParty), "A foe commanded by a party cleric (domain command) is party-controlled");
        Assert(!CombatEndRules.IsControlledByTeam(foe, CharacterTeam.Player, charmedByFoe), "A foe charmed by its own side is not party-controlled");
        Assert(!CombatEndRules.IsControlledByTeam(foe, CharacterTeam.Player, shaken), "Another condition from a party member is not control");
        Assert(!CombatEndRules.IsControlledByTeam(foe, CharacterTeam.Player, null), "No condition, no controller: not controlled");
        var suggestedByParty = new List<ConditionService.ActiveCondition>
        {
            new ConditionService.ActiveCondition { Type = CombatConditionType.Charmed, RemainingRounds = 10, Source = wizard, SourceId = DND35e.Identifiers.SpellNames.MASS_SUGGESTION }
        };
        Assert(!CombatEndRules.IsControlledByTeam(foe, CharacterTeam.Player, suggestedByParty),
            "A foe under the party's Mass Suggestion (Charmed in the game, a compulsion by RAW, PHB p.173, p.285) is not party-controlled (delegated RAW reading 2026-10-14, CORE-038)");

        // The same predicate for the party side (delegated RAW reading 2026-10-14, CORE-038): a hero charmed by a foe is
        // controlled by the hostile side; one under a foe's Mass Suggestion is not.
        var charmedByShaman = new List<ConditionService.ActiveCondition> { Condition(CombatConditionType.Charmed, shaman) };
        var suggestedByShaman = new List<ConditionService.ActiveCondition>
        {
            new ConditionService.ActiveCondition { Type = CombatConditionType.Charmed, RemainingRounds = 10, Source = shaman, SourceId = DND35e.Identifiers.SpellNames.MASS_SUGGESTION }
        };
        Assert(CombatEndRules.IsControlledByTeam(hero, CharacterTeam.Enemy, charmedByShaman), "A hero charmed by a foe is controlled by the hostile side (PHB p.209)");
        Assert(!CombatEndRules.IsControlledByTeam(hero, CharacterTeam.Enemy, charmedByParty), "A hero charmed by a party member is not controlled by the hostile side");
        Assert(!CombatEndRules.IsControlledByTeam(hero, CharacterTeam.Enemy, suggestedByShaman), "A hero under a foe's Mass Suggestion is not controlled (a compulsion, PHB p.285; CORE-038)");

        var command = new CommandUndeadEffectData { IsActive = true, DurationRemainingRounds = 10, CasterLevel = 5 };
        command.SetCaster(wizard);
        command.SetControlledUndead(foe);
        foe.ApplyCommandUndeadEffect(command);
        Assert(CombatEndRules.IsControlledByTeam(foe, CharacterTeam.Player, null), "A foe under a party member's Command Undead or Dominate Person is party-controlled (PHB p.211, p.224)");

        // Counting: the controlled foe is a member of the hostile side that is out; the healthy shaman keeps it in.
        var all = new List<CharacterController> { wizard, hero, shaman, foe };
        CombatEndRules.SideCounts s = CombatEndRules.Count(all, c => CombatEndRules.IsControlledByTeam(c, CharacterTeam.Player, null));
        Assert(s.EnemiesAll == 2 && s.EnemiesIn == 1 && !s.EnemiesOut, $"The commanded foe counts as out; the shaman keeps the hostile side in (got {s.EnemiesAll}/{s.EnemiesIn})");
        shaman.Stats.CurrentHP = -3;
        s = CombatEndRules.Count(all, c => CombatEndRules.IsControlledByTeam(c, CharacterTeam.Player, null));
        Assert(s.EnemiesOut && !s.PlayersOut, "With the shaman dying, the party-controlled foe at full HP does not keep the hostile side in (owner interpretation 2026-10-11, CORE-038)");
        s = CombatEndRules.Count(all);
        Assert(!s.EnemiesOut, "Without the control predicate the controlled foe still counts as in");

        // A party member controlled by the hostile side is out of the party side, by the same predicate (delegated RAW
        // reading 2026-10-14, CORE-038): with the wizard dying, the dominated hero does not keep the party side in.
        var heroCommand = new CommandUndeadEffectData { IsActive = true, DurationRemainingRounds = 10, CasterLevel = 5 };
        heroCommand.SetCaster(shaman);
        heroCommand.SetControlledUndead(hero);
        hero.ApplyCommandUndeadEffect(heroCommand);
        wizard.Stats.CurrentHP = -2;
        foe.RemoveCommandUndeadEffect(); // the foe, no longer party-controlled, keeps the hostile side in
        System.Func<CharacterController, bool> partyControls = c => CombatEndRules.IsControlledByTeam(c, CharacterTeam.Player, null);
        System.Func<CharacterController, bool> hostileControls = c => CombatEndRules.IsControlledByTeam(c, CharacterTeam.Enemy, null);
        s = CombatEndRules.Count(all, partyControls, hostileControls);
        Assert(s.PlayersOut && s.PlayersAll == 2 && s.PlayersIn == 0 && !s.EnemiesOut,
            $"A hero dominated by the hostile side is out of the party side; the party is out (got {s.PlayersIn}/{s.PlayersAll} in)");
        s = CombatEndRules.Count(all, partyControls);
        Assert(!s.PlayersOut && s.PlayersIn == 1, $"Without the party-side predicate the dominated hero still counts as in (got {s.PlayersIn} in)");
        // It leaves its own side without joining the other: the hostile side's counts are unchanged.
        Assert(s.EnemiesAll == 2, $"The dominated hero is not counted on the hostile side (got {s.EnemiesAll} hostile members)");
        foe.RemoveCommandUndeadEffect();
        hero.RemoveCommandUndeadEffect();
    }
}
}
