using System;
using System.Collections;
using System.Collections.Generic;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Total defense (PHB p.142; CMB-179, batch 13 step 4, decision D2), the catalog's shared action for every creature:
/// - the entry: a standard action that does not provoke, status New, with its executor and the ShowChoices follow-up;
/// - +4 dodge AC (+6 with 5 or more Tumble ranks, PHB p.85) in ArmorClass and TouchArmorClass, none in
///   ArmorClassWithoutDexAndDodge; the standard action is spent, the move action is not;
/// - the dodge bonus is lost while the Dexterity bonus to AC is denied (flat-footed; PHB p.136);
/// - no attack of opportunity while it lasts (ThreatSystem.CanMakeAoO and CanMakeAoOAgainst);
/// - refused with nothing spent without a standard action, or after an attack made fighting defensively or with Combat
///   Expertise; a fighting-defensively stance with no attack (the cornered frightened creature's) does not bar it and ends;
/// - refused for a turned creature, one that can take no actions (cowering) and a panicked one unless it is cornered;
/// - the strenuous-act test (CharacterController.StrenuousActWouldLeaveDying) the cornered panicked branch reads;
/// - it ends at the creature's next turn start (StartNewTurn, which GameManager.BeginCombatantTurn runs on every turn) and
///   when the slot is reused (ResetForNewCreature, CRE-046);
/// - in Play mode with a scene GameManager, also through GameManager.RunAIAction (the cornered panicked creature's path).
/// Play only: CharacterController.Init needs Awake for its conditions (TST-009).
/// </summary>
public static class TotalDefenseRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("========== TOTAL DEFENSE RULES TESTS (CMB-179, batch 13 step 4) ==========");
        TestHelpers.EnsureCoreDatabasesInitialized();

        Run(TestCatalogEntry);
        Run(TestDodgeBonusInEveryAC);
        Run(TestTumbleSynergy);
        Run(TestLostWhenDexDenied);
        Run(TestNoAttacksOfOpportunity);
        Run(TestRefusals);
        Run(TestUndeclaredStanceEnds);
        Run(TestConditionRefusals);
        Run(TestStrenuousActWouldLeaveDying);
        Run(TestEndsAtTurnStartAndOnSlotReuse);
        if (Application.isPlaying && GameManager.Instance != null)
            Run(TestThroughRunAIAction);
        else
            Debug.Log("  [SKIP] TestThroughRunAIAction (needs Play mode and a scene GameManager)");

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    private static void Run(Action test)
    {
        try
        {
            test();
        }
        catch (Exception ex)
        {
            Assert(false, $"{test.Method.Name} threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static CharacterController Fighter(string name)
    {
        CharacterController c = TestHelpers.CreateCharacter(name: name, characterClass: "Fighter", level: 3, str: 14, dex: 14, bab: 3);
        InventoryComponent inv = c.GetComponent<InventoryComponent>();
        if (inv != null && inv.CharacterInventory != null)
        {
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponShortsword), EquipSlot.RightHand);
            inv.CharacterInventory.RecalculateStats();
        }
        c.StartNewTurn();
        return c;
    }

    private static IActionExecutor Executor() => ActionCatalog.Get(ActionIds.TotalDefense)?.Executor;

    private static ActionOutcome Take(CharacterController c)
    {
        var o = new ActionOutcome();
        Drain(Executor().Execute(ActionContext.For(ActionIds.TotalDefense, c, ActorSource.AI), o));
        return o;
    }

    private static void TestCatalogEntry()
    {
        ActionDefinition d = ActionCatalog.Get(ActionIds.TotalDefense);
        Assert(d != null && d.Cost == ActionCostType.Standard && d.Provokes == ProvokeRule.No && d.Status == ImplStatus.New
               && d.Executor is TotalDefenseActionExecutor && d.UiAfter == ActionUiAfter.ShowChoices && d.IssueIds.Length == 0,
            "PHB p.142, Table 8-2: total defense is a standard action that does not provoke; the entry is New with its executor",
            d != null ? d.ToString() : "no entry");
    }

    private static void TestDodgeBonusInEveryAC()
    {
        CharacterController c = Fighter("TD_AC");
        try
        {
            CharacterStats s = c.Stats;
            int ac = s.ArmorClass, touch = s.TouchArmorClass, noDexDodge = s.ArmorClassWithoutDexAndDodge, dodge = s.DodgeACBonus;
            Assert(Executor().CanStart(c).Ok, "Control: a fighter with its actions may take total defense");
            ActionOutcome o = Take(c);
            Assert(o.Performed && c.IsInTotalDefense && s.TotalDefenseACBonus == 4, "PHB p.142: total defense is taken (+4)", o.ToString());
            Assert(s.ArmorClass == ac + 4 && s.TouchArmorClass == touch + 4 && s.DodgeACBonus == dodge + 4,
                "PHB p.142: +4 dodge bonus to AC and touch AC", $"AC {ac}->{s.ArmorClass}, touch {touch}->{s.TouchArmorClass}");
            Assert(s.ArmorClassWithoutDexAndDodge == noDexDodge, "PHB p.300: the AC without Dexterity and dodge bonuses does not include it",
                $"{noDexDodge}->{s.ArmorClassWithoutDexAndDodge}");
            Assert(c.Actions.StandardActionUsed && !c.Actions.MoveActionUsed, "PHB p.142: the standard action is spent, the move action is not");
        }
        finally
        {
            TestHelpers.Cleanup(c.gameObject);
        }
    }

    private static void TestTumbleSynergy()
    {
        CharacterController c = Fighter("TD_Tumble");
        try
        {
            CharacterStats s = c.Stats;
            s.Skills["Tumble"] = new Skill("Tumble", AbilityType.DEX, false) { Ranks = 4 };
            Assert(TotalDefenseRules.DodgeBonus(s) == 4, "PHB p.85: 4 Tumble ranks give no synergy (+4)");
            s.Skills["Tumble"].Ranks = 5;
            Assert(TotalDefenseRules.DodgeBonus(s) == 6, "PHB p.85: 5 Tumble ranks make total defense +6");
            int ac = s.ArmorClass;
            Take(c);
            Assert(s.ArmorClass == ac + 6, "PHB p.85: the +6 is in the AC", $"{ac}->{s.ArmorClass}");
        }
        finally
        {
            TestHelpers.Cleanup(c.gameObject);
        }
    }

    private static void TestLostWhenDexDenied()
    {
        CharacterController c = Fighter("TD_DexDenied");
        try
        {
            CharacterStats s = c.Stats;
            c.ApplyCondition(CombatConditionType.FlatFooted, 1, "TotalDefenseRulesTests");
            Assert(s.DeniedDexToAcByCondition, "Control: flat-footed denies the Dexterity bonus to AC");
            int flatAc = s.ArmorClass;
            c.BeginTotalDefense(4);
            Assert(s.DodgeACBonus == 0 && s.ArmorClass == flatAc,
                "PHB p.136: a creature denied its Dexterity bonus to AC loses total defense's dodge bonus too", $"{flatAc}->{s.ArmorClass}");
        }
        finally
        {
            TestHelpers.Cleanup(c.gameObject);
        }
    }

    private static void TestNoAttacksOfOpportunity()
    {
        CharacterController c = Fighter("TD_AoO");
        CharacterController foe = Fighter("TD_AoO_Foe");
        try
        {
            foe.SetTeam(CharacterTeam.Enemy);
            Assert(ThreatSystem.CanMakeAoO(c) && ThreatSystem.CanMakeAoOAgainst(c, foe), "Control: an armed fighter can make an AoO");
            Take(c);
            Assert(!ThreatSystem.CanMakeAoO(c) && !ThreatSystem.CanMakeAoOAgainst(c, foe),
                "PHB p.142: a creature in total defense makes no attacks of opportunity");
        }
        finally
        {
            TestHelpers.Cleanup(c.gameObject, foe.gameObject);
        }
    }

    private static void TestRefusals()
    {
        CharacterController c = Fighter("TD_Refusals");
        try
        {
            c.Actions.UseStandardAction();
            ActionOutcome noStandard = Take(c);
            Assert(!noStandard.Performed && noStandard.Refusal.Bar == LegalityBar.Action && !c.IsInTotalDefense && c.Stats.TotalDefenseACBonus == 0,
                "PHB p.142: no total defense without a standard action", noStandard.ToString());

            c.StartNewTurn();
            c.SetFightingDefensively(true);
            c.HasAttackedThisTurn = true;
            ActionOutcome defensive = Take(c);
            Assert(!defensive.Performed && defensive.Refusal.Bar == LegalityBar.Action && !c.Actions.StandardActionUsed && !c.IsInTotalDefense,
                "PHB p.142: total defense cannot be combined with an attack made fighting defensively; nothing is spent", defensive.ToString());

            c.StartNewTurn();
            c.Stats.Feats.Add("Combat Expertise");
            c.Stats.CombatExpertiseValue = 2;
            c.HasAttackedThisTurn = true;
            ActionOutcome expertise = Take(c);
            Assert(!expertise.Performed && expertise.Refusal.Bar == LegalityBar.Action && !c.IsInTotalDefense,
                "PHB p.142: total defense cannot be combined with Combat Expertise", expertise.ToString());
        }
        finally
        {
            TestHelpers.Cleanup(c.gameObject);
        }
    }

    private static void TestUndeclaredStanceEnds()
    {
        CharacterController c = Fighter("TD_Stance");
        try
        {
            CharacterStats s = c.Stats;
            int ac = s.ArmorClass;
            c.SetFightingDefensively(true); // the cornered frightened creature's stance (AI-044): no attack declared
            Assert(Executor().CanStart(c).Ok,
                "PHB p.142: fighting defensively is declared with an attack; a stance with no attack does not bar total defense");
            ActionOutcome o = Take(c);
            Assert(o.Performed && c.IsInTotalDefense && !c.IsFightingDefensively && s.ArmorClass == ac + 4,
                "PHB p.142: total defense ends the undeclared stance, so its +2 does not stack with the +4",
                $"fd={c.IsFightingDefensively} AC {ac}->{s.ArmorClass}");
        }
        finally
        {
            TestHelpers.Cleanup(c.gameObject);
        }
    }

    private static void TestConditionRefusals()
    {
        CharacterController c = Fighter("TD_Conditions");
        try
        {
            c.ApplyCondition(CombatConditionType.Turned, 3, "TotalDefenseRulesTests");
            Legality turned = Executor().CanStart(c);
            Assert(!turned.Ok && turned.Bar == LegalityBar.Condition, "PHB p.159: a turned creature flees, or cowers; no total defense", turned.ToString());
            c.RemoveCondition(CombatConditionType.Turned);

            c.ApplyCondition(CombatConditionType.Cowering, 1, "TotalDefenseRulesTests");
            Legality cowering = Executor().CanStart(c);
            Assert(!cowering.Ok && cowering.Bar == LegalityBar.Condition, "PHB p.307: a cowering creature takes no actions", cowering.ToString());
            c.RemoveCondition(CombatConditionType.Cowering);

            c.ApplyCondition(CombatConditionType.Panicked, 3, "TotalDefenseRulesTests");
            Legality fleeing = Executor().CanStart(c);
            Assert(!fleeing.Ok && fleeing.Bar == LegalityBar.Condition, "PHB p.311: a panicked creature that can flee takes no total defense", fleeing.ToString());
            c.CurrentTurnStart.CorneredInPanic = true;
            Assert(Executor().CanStart(c).Ok, "PHB p.311: a cornered panicked creature cowers, typically using total defense (D2)");
            c.CurrentTurnStart.Clear();
            Assert(!c.CurrentTurnStart.CorneredInPanic && !Executor().CanStart(c).Ok, "The cornered flag lasts one turn (TurnStartState.Clear)");
            c.RemoveCondition(CombatConditionType.Panicked);
            Assert(Executor().CanStart(c).Ok, "Control: with the conditions gone, total defense is allowed again");
        }
        finally
        {
            TestHelpers.Cleanup(c.gameObject);
        }
    }

    private static void TestStrenuousActWouldLeaveDying()
    {
        CharacterController c = Fighter("TD_Strenuous");
        try
        {
            Assert(!c.StrenuousActIsDamaging() && !c.StrenuousActWouldLeaveDying(), "Control: a healthy creature's standard action is not strenuous");
            c.Stats.CurrentHP = 0;
            Assert(c.CurrentHPState == HPState.Disabled && c.StrenuousActIsDamaging() && c.StrenuousActWouldLeaveDying(),
                "PHB p.145: at 0 hp a standard action costs 1 hp and leaves the creature dying", c.CurrentHPState.ToString());
            c.Stats.TempHP = 2;
            Assert(c.StrenuousActIsDamaging() && !c.StrenuousActWouldLeaveDying(), "Temporary hit points absorb the 1 damage: it stays disabled");
            c.Stats.TempHP = 0;
            c.StartNewTurn();
            Assert(c.CommitStandardAction() && c.StrenuousDamagePending && c.Actions.NoFurtherActions,
                "MarkStrenuousAction applies the same test: the damage is owed and the turn has no further actions");
        }
        finally
        {
            TestHelpers.Cleanup(c.gameObject);
        }
    }

    private static void TestEndsAtTurnStartAndOnSlotReuse()
    {
        CharacterController c = Fighter("TD_Lifetime");
        try
        {
            CharacterStats s = c.Stats;
            int ac = s.ArmorClass;
            Take(c);
            Assert(c.IsInTotalDefense && s.ArmorClass == ac + 4, "Control: in total defense");
            c.StartNewTurn();
            Assert(!c.IsInTotalDefense && s.TotalDefenseACBonus == 0 && s.ArmorClass == ac && ThreatSystem.CanMakeAoO(c),
                "PHB p.142: total defense ends at the start of the creature's next turn (AC back, AoOs again)", $"AC {s.ArmorClass}");

            Take(c);
            Assert(c.IsInTotalDefense, "Control: in total defense again");
            c.ResetForNewCreature("TotalDefenseRulesTests");
            Assert(!c.IsInTotalDefense && s.TotalDefenseACBonus == 0, "CRE-046: a reused slot keeps no total defense");
        }
        finally
        {
            TestHelpers.Cleanup(c.gameObject);
        }
    }

    private static void TestThroughRunAIAction()
    {
        GameManager gm = GameManager.Instance;
        CharacterController c = Fighter("TD_Runner");
        try
        {
            var o = new ActionOutcome();
            bool drained = Drain(gm.RunAIAction(ActionContext.For(ActionIds.TotalDefense, c, ActorSource.AI), o));
            Assert(drained && o.Performed && o.Spent == ActionCostType.Standard && c.IsInTotalDefense,
                "GameManager.RunAIAction takes total defense and reports the standard action spent", o.ToString());

            var again = new ActionOutcome();
            drained = Drain(gm.RunAIAction(ActionContext.For(ActionIds.TotalDefense, c, ActorSource.AI), again));
            Assert(drained && !again.Performed && again.Refusal.Bar == LegalityBar.Action && again.Spent == ActionCostType.NotAnAction,
                "RunAIAction pre-checks the New entry: without a standard action it refuses before the executor runs", again.ToString());
        }
        finally
        {
            TestHelpers.Cleanup(c.gameObject);
        }
    }

    /// <summary>Runs <paramref name="routine"/> to its end in one go (only for paths that never wait); false if it waits.</summary>
    private static bool Drain(IEnumerator routine, int maxSteps = 1000)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        for (int i = 0; i < maxSteps && stack.Count > 0; i++)
        {
            IEnumerator top = stack.Peek();
            if (!top.MoveNext())
            {
                stack.Pop();
                continue;
            }
            if (top.Current is IEnumerator nested)
                stack.Push(nested);
            else if (top.Current != null)
                return false;
        }
        return stack.Count == 0;
    }

    private static void Assert(bool condition, string testName, string detail = null)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}" + (string.IsNullOrEmpty(detail) ? "" : " -- " + detail));
        }
    }
}
}
