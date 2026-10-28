using System.Collections.Generic;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// The one action-limit rule (CharacterController.RefreshActionLimits; CMB-095, batch 12 step 3, decision D5), for PCs
/// and NPCs alike:
/// - slowed: one move or one standard action each turn and no full-round action (PHB p.280);
/// - disabled (0 hp, or below 0 with Diehard, PHB p.145) and staggered (PHB p.146): the same single action; a standard
///   action or a quickened spell costs a disabled creature 1 hp after the act is complete (PHB p.145; PHB p.93
///   Diehard), temporary hit points first, a move action costs nothing; a creature the damage will leave dying takes
///   nothing further that turn (ActionEconomy.NoFurtherActions);
/// - nauseated: a single move action and nothing else (PHB p.310 glossary);
/// - lifting a limit mid-turn (Haste ends Slow, PHB p.239) never gives back an action already spent.
/// Also pins that a disabled mover is not incapacitated (ThreatSystem.IsMoverIncapacitated, D5): an AoO that leaves it
/// at 0 hp does not end its move. Play mode only: CharacterController.Init needs Awake (TST-009).
/// </summary>
public static class ActionLimitRulesTests
{
    private static int _passed;
    private static int _failed;
    private static readonly List<CharacterController> _created = new List<CharacterController>();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== ACTION LIMIT RULES TESTS ==========");

        try
        {
            TestHealthyHasFullTurn();
            TestSlowGivesSingleAction();
            TestSlowAtTurnStartKeepsSingleAction();
            TestSlowRefusesStandardAfterMove();
            TestHasteRemovingSlowAfterStandardGrantsNothing();
            TestHasteRemovingSlowAfterMoveGrantsNothing();
            TestNauseatedGivesMoveOnly();
            TestNauseatedAtTurnStartAndRemoval();
            TestNauseatedRemovedAfterMoveGrantsNothing();
            TestDisabledGivesSingleAction();
            TestDisabledStandardActionCostsOneHp();
            TestDisabledMoveActionCostsNothing();
            TestDiehardStandardActionCostsOneHp();
            TestHealthyStandardActionOwesNothing();
            TestDisabledStandardActionEndsFurtherActions();
            TestDiehardStandardActionKeepsFurtherActions();
            TestTempHpAbsorbsStrenuousDamage();
            TestQuickenedSpellIsStrenuous();
            TestDisabledMoverIsNotIncapacitated();
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

    /// <summary>A level 2 fighter with 12 max HP at the start of its turn.</summary>
    private static CharacterController Create(string name, params string[] feats)
    {
        var go = new GameObject($"{name}_GO");
        var controller = go.AddComponent<CharacterController>();
        var stats = new CharacterStats(name, 2, "Fighter", 14, 12, 12, 10, 10, 10, 2, 0, 0, 8, 1, 0, 4, 1, 12);
        foreach (string f in feats)
            stats.Feats.Add(f);
        controller.Init(stats, Vector2Int.zero, null, null);
        controller.SetTeam(CharacterTeam.Player);
        _created.Add(controller);
        controller.StartNewTurn();
        return controller;
    }

    private static string Econ(CharacterController c)
        => $"single={c.Actions.SingleActionOnly} moveOnly={c.Actions.MoveActionOnly} move={c.Actions.HasMoveAction} "
           + $"std={c.Actions.HasStandardAction} full={c.Actions.HasFullRoundAction} hp={c.Stats.CurrentHP} state={c.CurrentHPState}";

    private static void TestHealthyHasFullTurn()
    {
        var c = Create("AL_Healthy");
        Assert(!c.Actions.SingleActionOnly && !c.Actions.MoveActionOnly && c.Actions.HasFullRoundAction
               && c.Actions.HasStandardAction && c.Actions.HasMoveAction,
            $"A healthy creature has a full turn ({Econ(c)})");
    }

    private static void TestSlowGivesSingleAction()
    {
        var c = Create("AL_Slow");
        c.ApplySlowEffect(3, null);
        Assert(c.Actions.SingleActionOnly && !c.Actions.HasFullRoundAction,
            $"Slow applied mid-turn: single action, no full-round action (PHB p.280) ({Econ(c)})");

        bool std = c.CommitStandardAction();
        Assert(std && !c.Actions.HasMoveAction && !c.Actions.HasAnyActionLeft,
            $"A slowed creature's standard action is its whole turn (PHB p.280) ({Econ(c)})");
    }

    private static void TestSlowAtTurnStartKeepsSingleAction()
    {
        var c = Create("AL_SlowTurnStart");
        c.ApplySlowEffect(3, null);
        c.StartNewTurn();
        Assert(c.Actions.SingleActionOnly && !c.Actions.HasFullRoundAction && !c.CanStillReachFullAttack(),
            $"A slowed creature starts its turn limited to a single action ({Econ(c)})");
    }

    private static void TestSlowRefusesStandardAfterMove()
    {
        var c = Create("AL_SlowMove");
        c.ApplySlowEffect(3, null);
        bool moved = c.Actions.UseMoveAction();
        Assert(moved && !c.Actions.HasStandardAction && !c.Actions.CanConvertStandardToMove,
            $"A slowed creature that moved has no standard action and no second move (PHB p.280) ({Econ(c)})");
    }

    private static void TestHasteRemovingSlowAfterStandardGrantsNothing()
    {
        var c = Create("AL_HasteAfterStd");
        c.ApplySlowEffect(3, null);
        c.CommitStandardAction();
        c.ApplyHasteEffect(3, null); // Haste counters and dispels Slow (PHB p.239)
        Assert(!c.HasActiveSlowEffect && !c.Actions.SingleActionOnly,
            $"Haste ends Slow and lifts the single-action limit ({Econ(c)})");
        Assert(!c.Actions.HasMoveAction && !c.Actions.HasStandardAction && !c.Actions.HasFullRoundAction,
            $"Lifting Slow after the standard action gives no action back ({Econ(c)})");
    }

    private static void TestHasteRemovingSlowAfterMoveGrantsNothing()
    {
        var c = Create("AL_HasteAfterMove");
        c.ApplySlowEffect(3, null);
        c.Actions.UseMoveAction();
        c.ClearSlowEffect();
        Assert(!c.Actions.SingleActionOnly && !c.Actions.HasMoveAction && !c.Actions.HasStandardAction,
            $"Lifting Slow after the move action gives no action back ({Econ(c)})");
    }

    private static void TestNauseatedGivesMoveOnly()
    {
        var c = Create("AL_Nauseated");
        c.ApplyCondition(CombatConditionType.Nauseated, 2, "Test");
        Assert(c.Actions.MoveActionOnly && c.Actions.HasMoveAction && !c.Actions.HasStandardAction
               && !c.Actions.HasFullRoundAction && !c.CommitStandardAction(),
            $"Nauseated: a move action only, no standard or full-round action (PHB p.310) ({Econ(c)})");

        bool moved = c.Actions.UseMoveAction();
        Assert(moved && !c.Actions.CanConvertStandardToMove && !c.Actions.HasAnyActionLeft && c.Actions.AllMainActionsSpent,
            $"A nauseated creature's move action is its whole turn: no second move ({Econ(c)})");
    }

    private static void TestNauseatedAtTurnStartAndRemoval()
    {
        var c = Create("AL_NauseatedTurn");
        c.ApplyCondition(CombatConditionType.Nauseated, 3, "Test");
        c.StartNewTurn();
        Assert(c.Actions.MoveActionOnly, $"A nauseated creature starts its turn with a move action only ({Econ(c)})");

        c.RemoveCondition(CombatConditionType.Nauseated);
        Assert(!c.Actions.MoveActionOnly && c.Actions.HasStandardAction && c.Actions.HasFullRoundAction,
            $"Removing Nauseated before acting lifts the move-only limit ({Econ(c)})");
    }

    private static void TestNauseatedRemovedAfterMoveGrantsNothing()
    {
        var c = Create("AL_NauseatedMoveThenRemoved");
        c.ApplyCondition(CombatConditionType.Nauseated, 2, "Test");
        c.Actions.UseMoveAction();
        c.RemoveCondition(CombatConditionType.Nauseated);
        Assert(!c.Actions.MoveActionOnly && !c.Actions.HasStandardAction && !c.Actions.HasFullRoundAction
               && !c.Actions.CanConvertStandardToMove && c.Actions.AllMainActionsSpent,
            $"Removing Nauseated after its one move action gives no standard action back ({Econ(c)})");
    }

    private static void TestDisabledGivesSingleAction()
    {
        var c = Create("AL_Disabled");
        c.Stats.CurrentHP = 0;
        Assert(c.CurrentHPState == HPState.Disabled && c.Actions.SingleActionOnly && !c.Actions.HasFullRoundAction,
            $"0 hp is disabled: a single action, no full-round action (PHB p.145) ({Econ(c)})");
        Assert(c.CanTakeTurnActions() && !CombatEndRules.IsOutOfFight(c),
            $"A disabled creature can act and is in the fight ({Econ(c)})");

        c.StartNewTurn();
        Assert(c.Actions.SingleActionOnly, $"A disabled creature starts its turn limited to a single action ({Econ(c)})");
    }

    private static void TestDisabledStandardActionCostsOneHp()
    {
        var c = Create("AL_DisabledStd");
        c.Stats.CurrentHP = 0;
        bool std = c.CommitStandardAction();
        Assert(std && c.StrenuousDamagePending && c.Stats.CurrentHP == 0 && c.CurrentHPState == HPState.Disabled,
            $"The 1 damage of a disabled creature's standard action waits until the act is complete (PHB p.145) ({Econ(c)})");
        Assert(!c.Actions.HasMoveAction, $"The standard action is the disabled creature's whole turn ({Econ(c)})");

        bool dealt = c.ResolvePendingStrenuousDamage();
        Assert(dealt && !c.StrenuousDamagePending && c.Stats.CurrentHP == -1 && c.CurrentHPState == HPState.Dying,
            $"After the act it is at -1 hp and dying (PHB p.145) ({Econ(c)})");
        Assert(!c.ResolvePendingStrenuousDamage() && c.Stats.CurrentHP == -1, $"The damage is dealt once ({Econ(c)})");
    }

    private static void TestDisabledMoveActionCostsNothing()
    {
        var c = Create("AL_DisabledMove");
        c.Stats.CurrentHP = 0;
        bool moved = c.Actions.UseMoveAction();
        c.ResolvePendingStrenuousDamage();
        Assert(moved && c.Stats.CurrentHP == 0 && c.CurrentHPState == HPState.Disabled && !c.Actions.HasStandardAction,
            $"A disabled creature's move action costs no hp and is its whole turn (PHB p.145) ({Econ(c)})");
    }

    private static void TestDiehardStandardActionCostsOneHp()
    {
        var c = Create("AL_Diehard", "Diehard");
        c.Stats.CurrentHP = -3;
        Assert(c.CurrentHPState == HPState.Disabled && c.Actions.SingleActionOnly,
            $"Diehard at -3 hp is disabled with a single action (PHB p.93) ({Econ(c)})");

        bool std = c.CommitStandardAction();
        c.ResolvePendingStrenuousDamage();
        Assert(std && c.Stats.CurrentHP == -4 && c.CurrentHPState == HPState.Disabled,
            $"A Diehard creature below 0 takes 1 damage for its standard action and stays disabled (PHB p.93) ({Econ(c)})");
    }

    private static void TestHealthyStandardActionOwesNothing()
    {
        var c = Create("AL_HealthyStd");
        c.CommitStandardAction();
        Assert(!c.StrenuousDamagePending && !c.ResolvePendingStrenuousDamage() && c.Stats.CurrentHP == c.Stats.TotalMaxHP,
            $"A healthy creature's standard action costs no hp ({Econ(c)})");
    }

    private static void TestDisabledStandardActionEndsFurtherActions()
    {
        var c = Create("AL_DisabledNoFurther");
        c.Stats.CurrentHP = 0;
        Assert(c.Actions.HasSwiftAction && !c.Actions.NoFurtherActions, $"A disabled creature that has not acted has its swift action ({Econ(c)})");
        c.CommitStandardAction();
        Assert(c.Actions.NoFurtherActions && !c.Actions.HasSwiftAction && !c.Actions.HasAnyActionLeft && c.Actions.AllMainActionsSpent,
            $"After a strenuous act that will leave it dying, a disabled creature takes nothing further: no swift action, no 5-ft step (PHB p.145) ({Econ(c)})");
        c.StartNewTurn();
        Assert(!c.Actions.NoFurtherActions && !c.StrenuousDamagePending, $"The limit ends with the turn ({Econ(c)})");
    }

    private static void TestDiehardStandardActionKeepsFurtherActions()
    {
        var c = Create("AL_DiehardFurther", "Diehard");
        c.Stats.CurrentHP = -3;
        c.CommitStandardAction();
        Assert(c.StrenuousDamagePending && !c.Actions.NoFurtherActions && c.Actions.HasSwiftAction,
            $"A Diehard creature stays disabled after its strenuous act, so only its single action is spent (PHB p.93) ({Econ(c)})");
    }

    private static void TestTempHpAbsorbsStrenuousDamage()
    {
        var c = Create("AL_DisabledTempHp");
        c.Stats.CurrentHP = 0;
        c.Stats.TempHP = 3;
        c.CommitStandardAction();
        Assert(c.StrenuousDamagePending && !c.Actions.NoFurtherActions,
            $"A disabled creature with temporary hit points owes the damage but will not drop ({Econ(c)})");
        c.ResolvePendingStrenuousDamage();
        Assert(c.Stats.CurrentHP == 0 && c.Stats.TempHP == 2 && c.CurrentHPState == HPState.Disabled,
            $"Temporary hit points absorb the strenuous damage first (tempHp={c.Stats.TempHP}) ({Econ(c)})");
        c.Stats.TempHP = 0;
    }

    private static void TestQuickenedSpellIsStrenuous()
    {
        // The cast core's free-action cost calls MarkStrenuousAction for a quickened spell (GameManager.CastCore.cs).
        var c = Create("AL_DisabledQuickened");
        c.Stats.CurrentHP = 0;
        c.MarkStrenuousAction();
        Assert(c.StrenuousDamagePending && c.Actions.NoFurtherActions && !c.Actions.HasStandardAction,
            $"A quickened spell is strenuous for a disabled caster: 1 damage owed and nothing further (PHB p.145, p.93) ({Econ(c)})");
        c.ResolvePendingStrenuousDamage();
        Assert(c.Stats.CurrentHP == -1 && c.CurrentHPState == HPState.Dying, $"After the quickened spell it is at -1 and dying ({Econ(c)})");

        var h = Create("AL_HealthyQuickened");
        h.MarkStrenuousAction();
        Assert(!h.StrenuousDamagePending && !h.Actions.NoFurtherActions && h.Actions.HasStandardAction,
            $"A quickened spell costs a healthy caster nothing ({Econ(h)})");
    }

    private static void TestDisabledMoverIsNotIncapacitated()
    {
        var c = Create("AL_DisabledMover");
        c.Stats.CurrentHP = 0;
        Assert(!ThreatSystem.IsMoverIncapacitated(c),
            $"A mover left at 0 hp is not incapacitated, so its move goes on (D5) ({Econ(c)})");

        c.Stats.CurrentHP = -1;
        Assert(ThreatSystem.IsMoverIncapacitated(c),
            $"A mover dropped below 0 hp is incapacitated ({Econ(c)})");
    }
}
}
