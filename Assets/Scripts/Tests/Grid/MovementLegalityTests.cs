using System.Collections.Generic;
using UnityEngine;

namespace Tests.Grid
{
/// <summary>
/// The one movement legality check (MovementService.CanStartMovement; batch 12 step 22, GRID-019, GRID-022), for PCs and
/// NPCs alike (the PC buttons, the NPC movement executors, the AI's 5-foot steps and withdraws and the compulsion
/// controllers all ask it):
/// - a 5-foot step: not in a round with other movement, only one a round, not with a speed of 5 ft or less (PHB p.144);
/// - no other movement after a 5-foot step (PHB p.144; p.143 for the withdraw);
/// - no movement while prone (PHB p.142-143), pinned or held by a condition that prevents movement;
/// - withdraw: a full-round action at double speed, never while blinded (PHB p.143);
/// - Restricted Withdraw: a creature limited to a single action (slowed, disabled) withdraws as a standard action at its
///   speed, and never with the full-round withdraw (PHB p.144);
/// - a nauseated creature (a move action only) may move but never double move, run, withdraw or charge;
/// - the budgets: a move its speed, a double move or withdraw double, a run four times (three times in heavy armor or
///   with a heavy load, PHB p.144, p.162);
/// - no run or charge while entangled (PHB p.309), fatigued (p.308) or exhausted;
/// - CombatFlowService.CanPerformWithdraw and GameManager.GetWithdrawMoveRangeSquares read the same rule.
/// Play mode only: CharacterController.Init needs Awake (TST-009).
/// </summary>
public static class MovementLegalityTests
{
    private static int _passed;
    private static int _failed;
    private static readonly List<CharacterController> _created = new List<CharacterController>();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== MOVEMENT LEGALITY TESTS ==========");

        try
        {
            TestHealthyMayStartEveryFullTurnMovement();
            TestBudgets();
            TestNoMovementAfterFiveFootStep();
            TestNoFiveFootStepAfterMoving();
            TestOneFiveFootStepOnly();
            TestSpeedFiveFeetHasNoFiveFootStep();
            TestProneRefusesMovement();
            TestPinnedRefusesMovement();
            TestParalyzedRefusesMovement();
            TestBlindedCannotWithdraw();
            TestSlowedRestrictedWithdraw();
            TestDisabledRestrictedWithdraw();
            TestNauseatedMoveOnly();
            TestNoMoveActionLeft();
            TestEntangledNoRunOrCharge();
            TestFatiguedNoRunOrCharge();
            TestRunMultiplierHeavyArmorAndLoad();
            TestWithdrawServiceAgrees();
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

    /// <summary>A level 2 fighter (speed 30 ft, 6 squares) at the start of its turn.</summary>
    private static CharacterController Create(string name)
    {
        var go = new GameObject($"{name}_GO");
        var controller = go.AddComponent<CharacterController>();
        var stats = new CharacterStats(name, 2, "Fighter", 14, 12, 12, 10, 10, 10, 2, 0, 0, 8, 1, 0, 6, 1, 12);
        controller.Init(stats, new Vector2Int(40, 40), null, null);
        controller.SetTeam(CharacterTeam.Player);
        _created.Add(controller);
        controller.StartNewTurn();
        return controller;
    }

    private static bool Ok(CharacterController c, MoveKind kind, int squares = 0) => MovementService.CanStartMovement(c, kind, squares).Ok;

    private static string Why(CharacterController c, MoveKind kind, int squares = 0) => MovementService.CanStartMovement(c, kind, squares).ToString();

    private static int Speed(CharacterController c) => MovementService.SingleMoveBudgetSquares(c);

    private static void TestHealthyMayStartEveryFullTurnMovement()
    {
        var c = Create("ML_Healthy");
        Assert(Speed(c) == 6, $"Fixture: speed 30 ft is 6 squares ({Speed(c)})");
        Assert(Ok(c, MoveKind.Move) && Ok(c, MoveKind.DoubleMove) && Ok(c, MoveKind.Run) && Ok(c, MoveKind.Withdraw)
               && Ok(c, MoveKind.FiveFootStep) && Ok(c, MoveKind.Charge),
            $"A healthy creature may move, double move, run, withdraw, 5-foot step and charge ({Why(c, MoveKind.Withdraw)})");
        Assert(!Ok(c, MoveKind.RestrictedWithdraw),
            $"Restricted Withdraw is only for a creature limited to a single action ({Why(c, MoveKind.RestrictedWithdraw)})");
        Assert(MovementService.WithdrawKindFor(c) == MoveKind.Withdraw, "A healthy creature withdraws with the full-round withdraw");
    }

    private static void TestBudgets()
    {
        var c = Create("ML_Budgets");
        int s = Speed(c);
        Assert(Ok(c, MoveKind.Move, s) && !Ok(c, MoveKind.Move, s + 1), $"A move covers up to the speed ({Why(c, MoveKind.Move, s + 1)})");
        Assert(Ok(c, MoveKind.DoubleMove, 2 * s) && !Ok(c, MoveKind.DoubleMove, 2 * s + 1), "A double move covers up to double speed");
        Assert(Ok(c, MoveKind.Withdraw, 2 * s) && !Ok(c, MoveKind.Withdraw, 2 * s + 1), "A withdraw covers up to double speed (PHB p.143)");
        Assert(Ok(c, MoveKind.Run, 4 * s) && !Ok(c, MoveKind.Run, 4 * s + 1), "A run covers up to four times the speed (PHB p.144)");
        Assert(Ok(c, MoveKind.FiveFootStep, 1) && !Ok(c, MoveKind.FiveFootStep, 2), "A 5-foot step moves one square");
        Assert(Ok(c, MoveKind.Charge, 2 * s) && !Ok(c, MoveKind.Charge, 2 * s + 1), "A charge covers up to double speed (PHB p.154)");
        Assert(MovementService.WithdrawBudgetSquares(c) == 2 * s, "The withdraw budget is double speed");
        if (GameManager.Instance != null)
            Assert(GameManager.Instance.GetWithdrawMoveRangeSquares(c) == 2 * s, "GameManager.GetWithdrawMoveRangeSquares reads the same budget");
    }

    private static void TestNoMovementAfterFiveFootStep()
    {
        var c = Create("ML_After5ft");
        c.HasTakenFiveFootStep = true;
        Assert(!Ok(c, MoveKind.Move) && !Ok(c, MoveKind.DoubleMove) && !Ok(c, MoveKind.Run) && !Ok(c, MoveKind.Withdraw) && !Ok(c, MoveKind.Charge),
            $"No move, double move, run, withdraw or charge after a 5-foot step (PHB p.144) ({Why(c, MoveKind.Move)}; {Why(c, MoveKind.Withdraw)})");
    }

    private static void TestNoFiveFootStepAfterMoving()
    {
        var c = Create("ML_AfterMove");
        c.Actions.UseMoveAction();
        c.HasMovedThisTurn = true;
        Assert(!Ok(c, MoveKind.FiveFootStep), $"No 5-foot step in a round with other movement (PHB p.144) ({Why(c, MoveKind.FiveFootStep)})");
        Assert(Ok(c, MoveKind.Move), $"A second move (the standard action converted) is still legal ({Why(c, MoveKind.Move)})");
    }

    private static void TestOneFiveFootStepOnly()
    {
        var c = Create("ML_Second5ft");
        c.HasTakenFiveFootStep = true;
        Assert(!Ok(c, MoveKind.FiveFootStep), $"Only one 5-foot step a round (PHB p.144) ({Why(c, MoveKind.FiveFootStep)})");
    }

    private static void TestSpeedFiveFeetHasNoFiveFootStep()
    {
        var c = Create("ML_Speed5");
        c.Stats.BaseSpeed = 1;
        Assert(Speed(c) == 1, $"Fixture: speed 5 ft ({Speed(c)} square)");
        Assert(!Ok(c, MoveKind.FiveFootStep) && Ok(c, MoveKind.Move),
            $"A creature with a speed of 5 ft takes no 5-foot step but may move (PHB p.144) ({Why(c, MoveKind.FiveFootStep)})");
    }

    private static void TestProneRefusesMovement()
    {
        var c = Create("ML_Prone");
        c.ApplyCondition(CombatConditionType.Prone, -1, "Test");
        Assert(!Ok(c, MoveKind.Move) && !Ok(c, MoveKind.Withdraw) && !Ok(c, MoveKind.FiveFootStep) && !Ok(c, MoveKind.Charge),
            $"A prone creature takes no ordinary movement or 5-foot step (PHB p.142-143) ({Why(c, MoveKind.Move)})");
    }

    private static void TestPinnedRefusesMovement()
    {
        var c = Create("ML_Pinned");
        c.ApplyCondition(CombatConditionType.Pinned, -1, "Test");
        Assert(!Ok(c, MoveKind.Move) && !Ok(c, MoveKind.Withdraw) && !Ok(c, MoveKind.FiveFootStep),
            $"A pinned creature takes no movement (PHB p.156) ({Why(c, MoveKind.Withdraw)})");
    }

    private static void TestParalyzedRefusesMovement()
    {
        var c = Create("ML_Paralyzed");
        c.ApplyCondition(CombatConditionType.Paralyzed, 2, "Test");
        Assert(!Ok(c, MoveKind.Move) && !Ok(c, MoveKind.FiveFootStep) && !Ok(c, MoveKind.Withdraw),
            $"A condition that prevents movement refuses every movement ({Why(c, MoveKind.Move)})");
    }

    private static void TestBlindedCannotWithdraw()
    {
        var c = Create("ML_Blinded");
        c.ApplyCondition(CombatConditionType.Blinded, 3, "Test");
        Assert(!Ok(c, MoveKind.Withdraw), $"A blinded creature cannot withdraw (PHB p.143) ({Why(c, MoveKind.Withdraw)})");
        Assert(Ok(c, MoveKind.Move) && Ok(c, MoveKind.FiveFootStep), $"A blinded creature may still move or 5-foot step ({Why(c, MoveKind.Move)})");

        var slowed = Create("ML_BlindedSlowed");
        slowed.ApplyCondition(CombatConditionType.Blinded, 3, "Test");
        slowed.ApplySlowEffect(3, null);
        Assert(!Ok(slowed, MoveKind.RestrictedWithdraw), $"Nor a Restricted Withdraw ({Why(slowed, MoveKind.RestrictedWithdraw)})");
    }

    private static void TestSlowedRestrictedWithdraw()
    {
        var c = Create("ML_Slowed");
        c.ApplySlowEffect(3, null);
        int s = Speed(c);
        Assert(c.Actions.SingleActionOnly, "Fixture: a slowed creature is limited to a single action (PHB p.280)");
        Assert(!Ok(c, MoveKind.Withdraw), $"A slowed creature has no full-round withdraw ({Why(c, MoveKind.Withdraw)})");
        Assert(MovementService.WithdrawKindFor(c) == MoveKind.RestrictedWithdraw, "Its withdraw is a Restricted Withdraw");
        Assert(Ok(c, MoveKind.RestrictedWithdraw, s) && !Ok(c, MoveKind.RestrictedWithdraw, s + 1),
            $"A Restricted Withdraw moves up to the speed, not double (PHB p.144) ({Why(c, MoveKind.RestrictedWithdraw, s + 1)})");
        Assert(MovementService.WithdrawBudgetSquares(c) == s, $"The withdraw budget is the speed ({MovementService.WithdrawBudgetSquares(c)} vs {s})");
        Assert(!Ok(c, MoveKind.DoubleMove) && !Ok(c, MoveKind.Run), "A single-action creature neither double moves nor runs");
        Assert(Ok(c, MoveKind.Move) && Ok(c, MoveKind.Charge, s) && !Ok(c, MoveKind.Charge, s + 1),
            "It may move, and its charge is a partial charge at speed (PHB p.154, D2)");

        c.CommitStandardAction();
        Assert(!Ok(c, MoveKind.RestrictedWithdraw) && !Ok(c, MoveKind.Move),
            $"After its single standard action no withdraw or move is left ({Why(c, MoveKind.RestrictedWithdraw)})");
    }

    private static void TestDisabledRestrictedWithdraw()
    {
        var c = Create("ML_Disabled");
        c.Stats.CurrentHP = 0;
        Assert(c.CurrentHPState == HPState.Disabled && c.Actions.SingleActionOnly, "Fixture: 0 hp is disabled, a single action (PHB p.145)");
        Assert(Ok(c, MoveKind.RestrictedWithdraw, Speed(c)) && !Ok(c, MoveKind.Withdraw),
            $"A disabled creature may make a Restricted Withdraw, not the full-round withdraw ({Why(c, MoveKind.RestrictedWithdraw)})");
        if (GameManager.Instance != null)
            Assert(GameManager.Instance.GetWithdrawMoveRangeSquares(c) == Speed(c), "GameManager's withdraw budget is the speed for it");
    }

    private static void TestNauseatedMoveOnly()
    {
        var c = Create("ML_Nauseated");
        c.ApplyCondition(CombatConditionType.Nauseated, 2, "Test");
        Assert(c.Actions.MoveActionOnly, "Fixture: nauseated is a move action only");
        Assert(Ok(c, MoveKind.Move), $"A nauseated creature may move ({Why(c, MoveKind.Move)})");
        Assert(!Ok(c, MoveKind.DoubleMove) && !Ok(c, MoveKind.Run) && !Ok(c, MoveKind.Withdraw)
               && !Ok(c, MoveKind.RestrictedWithdraw) && !Ok(c, MoveKind.Charge),
            $"but never double moves, runs, withdraws or charges ({Why(c, MoveKind.Withdraw)}; {Why(c, MoveKind.Charge)})");
    }

    private static void TestNoMoveActionLeft()
    {
        var c = Create("ML_NoActions");
        c.Actions.UseMoveAction();
        c.Actions.UseStandardAction();
        Assert(!Ok(c, MoveKind.Move) && !Ok(c, MoveKind.Withdraw), $"No move or withdraw with the actions spent ({Why(c, MoveKind.Move)})");
        Assert(Ok(c, MoveKind.FiveFootStep), $"A 5-foot step needs no action, only no other movement this round ({Why(c, MoveKind.FiveFootStep)})");
    }

    private static void TestEntangledNoRunOrCharge()
    {
        var c = Create("ML_Entangled");
        c.ApplyCondition(CombatConditionType.Entangled, 3, "Test");
        Assert(c.Stats.IsEntangledState, "Fixture: the creature is entangled");
        Assert(!Ok(c, MoveKind.Run) && !Ok(c, MoveKind.Charge),
            $"An entangled creature can neither run nor charge (PHB p.309) ({Why(c, MoveKind.Run)}; {Why(c, MoveKind.Charge)})");
        Assert(Ok(c, MoveKind.Move), $"It may still move ({Why(c, MoveKind.Move)})");
    }

    private static void TestFatiguedNoRunOrCharge()
    {
        var c = Create("ML_Fatigued");
        c.ApplyCondition(CombatConditionType.Fatigued, 10, "Test");
        Assert(c.Stats.IsFatiguedOrExhausted, "Fixture: the creature is fatigued");
        Assert(!Ok(c, MoveKind.Run) && !Ok(c, MoveKind.Charge),
            $"A fatigued creature can neither run nor charge (PHB p.308) ({Why(c, MoveKind.Run)}; {Why(c, MoveKind.Charge)})");
        Assert(Ok(c, MoveKind.Move) && Ok(c, MoveKind.DoubleMove) && Ok(c, MoveKind.Withdraw),
            $"It may move, double move and withdraw ({Why(c, MoveKind.DoubleMove)})");

        var e = Create("ML_Exhausted");
        e.ApplyCondition(CombatConditionType.Exhausted, 10, "Test");
        Assert(!Ok(e, MoveKind.Run) && !Ok(e, MoveKind.Charge),
            $"An exhausted creature can neither run nor charge ({Why(e, MoveKind.Run)}; {Why(e, MoveKind.Charge)})");
    }

    private static void TestRunMultiplierHeavyArmorAndLoad()
    {
        var c = Create("ML_RunLight");
        Assert(MovementService.RunMultiplier(c) == 4, "Unarmored with a light load, a run is four times the speed (PHB p.144)");

        var loaded = Create("ML_RunHeavyLoad");
        loaded.Stats.CurrentEncumbrance = EncumbranceLevel.Heavy;
        int s = Speed(loaded);
        Assert(MovementService.RunMultiplier(loaded) == 3 && Ok(loaded, MoveKind.Run, 3 * s) && !Ok(loaded, MoveKind.Run, 3 * s + 1),
            $"With a heavy load a run is three times the speed (PHB p.162) ({Why(loaded, MoveKind.Run, 3 * s + 1)})");

        var armored = Create("ML_RunHeavyArmor");
        armored.Stats.EquippedArmorItem = ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.ArmorPlate);
        Assert(armored.Stats.EquippedArmorItem != null && MovementService.RunMultiplier(armored) == 3,
            "In heavy armor (full plate) a run is three times the speed (PHB p.144)");
    }

    private static void TestWithdrawServiceAgrees()
    {
        var go = new GameObject("ML_CombatFlowService_GO");
        try
        {
            var service = go.AddComponent<CombatFlowService>();
            var healthy = Create("ML_ServiceHealthy");
            var blinded = Create("ML_ServiceBlinded");
            blinded.ApplyCondition(CombatConditionType.Blinded, 3, "Test");
            var slowed = Create("ML_ServiceSlowed");
            slowed.ApplySlowEffect(3, null);
            var stepped = Create("ML_ServiceStepped");
            stepped.HasTakenFiveFootStep = true;

            Assert(service.CanPerformWithdraw(healthy, out _), "CombatFlowService.CanPerformWithdraw: a healthy creature may withdraw");
            Assert(!service.CanPerformWithdraw(blinded, out string blindReason) && blindReason.Contains("blind"),
                $"CanPerformWithdraw refuses a blinded creature ({blindReason})");
            Assert(service.CanPerformWithdraw(slowed, out string slowReason),
                $"CanPerformWithdraw allows a slowed creature its Restricted Withdraw ({slowReason})");
            Assert(!service.CanPerformWithdraw(stepped, out string stepReason),
                $"CanPerformWithdraw refuses a withdraw after a 5-foot step ({stepReason})");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
}
