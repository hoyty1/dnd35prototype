using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
/// - CombatFlowService.CanPerformWithdraw and GameManager.GetWithdrawMoveRangeSquares read the same rule;
/// - the charge's clear path (PHB p.154; CMB-132, CMB-199): the squares every line between the starting and ending spaces
///   passes through (their convex hull; an edge or corner touch is not a crossing), the straight steps inside it, and which
///   creatures a charge may pass (helpless or dead ones only; ChargeRules.LetsChargePass);
/// - the per-mover square question (MovementTerrain; owner interpretation 2026-10-29): difficult terrain slows every mover
///   but an incorporeal one, for a plain move's path cost as for a charge line; Freedom of Movement ignores only a Web's;
/// - the one move executor's requests (batch 13 step 9, GameManager.ExecuteMoveAction): MoveRequest.ForUi sets no quirk bit
///   and converts the standard action; MoveRequest.ForAI sets only AiSegmentLoop; the builders' modifiers (LoggingOnStart
///   included); a pathless move is refused with MoveAttempt.NoPath and spends nothing, and the spend precedes the first step
///   (D3, batch 13 step 10); and the classification of the AI's move sites (24 move calls) as a source scan.
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
            TestChargeLineGeometry();
            TestChargePassesOnlyHelplessCreatures();
            TestMovementTerrainExemptions();
            TestMoveRequestBuilders();
            TestExecuteMoveActionSpends();
            TestMoveSiteClassification();
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

    /// <summary>
    /// The charge line (PHB p.154, CMB-132): SquareGridUtils.GetSquaresCoveredBySweptSpace (every line between the starting
    /// and ending spaces: their convex hull; an edge or corner touch is not a crossing) and GetStraightLinePath.
    /// </summary>
    private static void TestChargeLineGeometry()
    {
        var straight = new HashSet<Vector2Int>(SquareGridUtils.GetSquaresCoveredBySweptSpace(new Vector2Int(0, 0), new Vector2Int(4, 0), 1));
        Assert(straight.Count == 5 && straight.Contains(new Vector2Int(2, 0)) && !straight.Contains(new Vector2Int(2, 1))
               && !straight.Contains(new Vector2Int(2, -1)),
            $"A straight charge along a row covers the 5 squares of its row only ({straight.Count})");

        var diagonal = new HashSet<Vector2Int>(SquareGridUtils.GetSquaresCoveredBySweptSpace(new Vector2Int(0, 0), new Vector2Int(2, 2), 1));
        Assert(diagonal.Count == 7 && diagonal.Contains(new Vector2Int(1, 1))
               && diagonal.Contains(new Vector2Int(1, 0)) && diagonal.Contains(new Vector2Int(0, 1))
               && !diagonal.Contains(new Vector2Int(2, 0)) && !diagonal.Contains(new Vector2Int(0, 2)),
            $"A diagonal charge covers the squares orthogonally beside the diagonal, not the corners it only touches ({diagonal.Count})");

        var shallow = new HashSet<Vector2Int>(SquareGridUtils.GetSquaresCoveredBySweptSpace(new Vector2Int(0, 0), new Vector2Int(3, 1), 1));
        Assert(shallow.Contains(new Vector2Int(1, 1)) && shallow.Contains(new Vector2Int(2, 0)) && shallow.Count == 8,
            $"A charge from (0,0) to (3,1) covers (1,1) and (2,0), which some line between the two spaces crosses ({shallow.Count})");

        var large = new HashSet<Vector2Int>(SquareGridUtils.GetSquaresCoveredBySweptSpace(new Vector2Int(0, 0), new Vector2Int(3, 1), 2));
        Assert(large.Contains(new Vector2Int(1, 2)) && large.Contains(new Vector2Int(4, 2)) && !large.Contains(new Vector2Int(0, 3)),
            "A 2x2 charger's swept space from anchor (0,0) to (3,1) covers (1,2), beyond its footprint's own rows at the start");

        var largeStraight = new HashSet<Vector2Int>(SquareGridUtils.GetSquaresCoveredBySweptSpace(new Vector2Int(0, 0), new Vector2Int(3, 0), 2));
        Assert(largeStraight.Count == 10 && largeStraight.Contains(new Vector2Int(4, 1)) && !largeStraight.Contains(new Vector2Int(1, 2)),
            $"A 2x2 charger moving along its rows covers those two rows only ({largeStraight.Count})");

        List<Vector2Int> path = SquareGridUtils.GetStraightLinePath(new Vector2Int(10, 10), new Vector2Int(13, 9));
        var swept = new HashSet<Vector2Int>(SquareGridUtils.GetSquaresCoveredBySweptSpace(new Vector2Int(10, 10), new Vector2Int(13, 9), 1));
        Assert(path.Count == 3 && path[path.Count - 1] == new Vector2Int(13, 9) && path.TrueForAll(swept.Contains)
               && SquareGridUtils.CalculatePathCost(new Vector2Int(10, 10), path) == SquareGridUtils.GetDistance(new Vector2Int(10, 10), new Vector2Int(13, 9)),
            $"The straight steps end at the end space, stay in the swept space and cost the open-ground distance ({string.Join(" ", path)})");

        List<Vector2Int> back = SquareGridUtils.GetStraightLinePath(new Vector2Int(5, 5), new Vector2Int(1, 3));
        var backSwept = new HashSet<Vector2Int>(SquareGridUtils.GetSquaresCoveredBySweptSpace(new Vector2Int(5, 5), new Vector2Int(1, 3), 1));
        Assert(back.Count == 4 && back[3] == new Vector2Int(1, 3) && back.TrueForAll(backSwept.Contains),
            $"The steps toward lower coordinates also stay in the swept space ({string.Join(" ", back)})");
    }

    /// <summary>Helpless creatures do not stop a charge, every other one does, an ally too (PHB p.154; ChargeRules.LetsChargePass).</summary>
    private static void TestChargePassesOnlyHelplessCreatures()
    {
        CharacterController awake = Create("ChargeAwake");
        Assert(!ChargeRules.LetsChargePass(awake), "A creature that is not helpless stops a charge, even an ally (PHB p.154)");

        CharacterController paralyzed = Create("ChargeParalyzed");
        paralyzed.ApplyCondition(CombatConditionType.Paralyzed, 10, "test");
        Assert(ChargeRules.LetsChargePass(paralyzed), "A paralyzed (helpless) creature does not stop a charge (PHB p.154)");

        CharacterController asleep = Create("ChargeAsleep");
        asleep.ApplyCondition(CombatConditionType.Asleep, 10, "test");
        Assert(ChargeRules.LetsChargePass(asleep), "A sleeping (helpless) creature does not stop a charge");

        CharacterController dying = Create("ChargeDying");
        dying.Stats.CurrentHP = -3;
        Assert(ChargeRules.LetsChargePass(dying), $"A dying (unconscious, helpless) creature does not stop a charge (hp {dying.Stats.CurrentHP})");

        CharacterController prone = Create("ChargeProne");
        prone.ApplyCondition(CombatConditionType.Prone, 10, "test");
        Assert(!ChargeRules.LetsChargePass(prone), "A prone creature is not helpless and stops a charge");
    }

    /// <summary>
    /// The per-mover square question (MovementTerrain; owner interpretation 2026-10-29; PHB p.148, p.233, MM p.310-311):
    /// a difficult-terrain square slows a fighter and costs it 2 on a path, but not an incorporeal creature; Freedom of
    /// Movement does not lift difficult terrain that is not a Web's; the exemption predicates read the movement abilities.
    /// </summary>
    private static void TestMovementTerrainExemptions()
    {
        CharacterController fighter = Create("MT_Fighter");
        CharacterController ghost = Create("MT_Incorporeal");
        ghost.ConfigureIncorporeal(true);
        CharacterController free = Create("MT_Freedom");
        free.Stats.FreedomOfMovementActive = true;
        CharacterController traveler = Create("MT_Travel");
        traveler.Stats.TravelDomainFreedomRounds = 3;

        Assert(!MovementTerrain.IgnoresDifficultTerrain(fighter) && MovementTerrain.IgnoresDifficultTerrain(ghost)
               && !MovementTerrain.IgnoresDifficultTerrain(null),
            "Only the incorporeal creature ignores difficult terrain (PHB p.148; no fly mode, GRID-027)");
        Assert(!MovementTerrain.PassesSolidObjects(fighter) && MovementTerrain.PassesSolidObjects(ghost),
            "Only the incorporeal creature passes solid objects (MM p.310-311)");
        Assert(!MovementTerrain.IgnoresMagicalImpediment(fighter) && MovementTerrain.IgnoresMagicalImpediment(free)
               && MovementTerrain.IgnoresMagicalImpediment(traveler),
            "Freedom of Movement (the spell or ring flag, or the Travel domain power) ignores magical impediments (PHB p.233)");

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.Grid == null)
        {
            Assert(false, "MovementTerrain: no GameManager grid to test the difficult-terrain set on");
            return;
        }

        var cell = new Vector2Int(1, 1);
        bool wasDifficult = gm.IsDifficultTerrain(cell);
        try
        {
            gm.SetAreaDifficultTerrain(new[] { cell }, true);
            Assert(MovementTerrain.IsDifficultTerrain(fighter, cell)
                   && MovementTerrain.Classify(fighter, cell, out string why) == SquareHindrance.Slows && why == "difficult terrain",
                "A difficult-terrain square slows a fighter and bars its charge line (PHB p.148, p.154)");
            Assert(!MovementTerrain.IsDifficultTerrain(ghost, cell) && MovementTerrain.Classify(ghost, cell, out _) == SquareHindrance.None,
                "The same square does not hamper an incorporeal creature (PHB p.148)");
            Assert(MovementTerrain.IsDifficultTerrain(free, cell),
                "Freedom of Movement does not lift difficult terrain that no Web makes (PHB p.233 names magic, not terrain)");

            var path = new List<Vector2Int> { new Vector2Int(1, 0), cell, new Vector2Int(1, 2) };
            int fighterCost = SquareGridUtils.CalculatePathCost(fighter, new Vector2Int(1, -1), path);
            int ghostCost = SquareGridUtils.CalculatePathCost(ghost, new Vector2Int(1, -1), path);
            int noMoverCost = SquareGridUtils.CalculatePathCost(new Vector2Int(1, -1), path);
            Assert(fighterCost == 4 && noMoverCost == 4 && ghostCost == 3,
                $"A plain move's path cost reads the same question: 4 for the fighter and for no mover, 3 for the incorporeal one ({fighterCost}, {noMoverCost}, {ghostCost})");
        }
        finally
        {
            if (!wasDifficult)
                gm.SetAreaDifficultTerrain(new[] { cell }, false);
        }

        Assert(MovementTerrain.Classify(fighter, new Vector2Int(-1, 5), out string edge) == SquareHindrance.Blocks
               && edge == "the edge of the battlefield",
            "A square off the grid blocks every mover");
    }

    // ── The one move executor (batch 13 steps 9-10) ──────────────────────────────────────────────────────────────────

    /// <summary>The builders' bits: the PC's request has no quirk, the AI's only the segment walker (batch 13 step 10
    /// deleted the spend bits, D3), and the modifiers set only theirs.</summary>
    private static void TestMoveRequestBuilders()
    {
        var path = new List<Vector2Int> { new Vector2Int(1, 0), new Vector2Int(2, 0) };
        MoveRequest ui = MoveRequest.ForUi(null, path, null, false, 0.08f);
        Assert(ui.Quirks == MoveQuirk.None && ui.AllowConvertStandard && ui.Spend == MoveSpend.MoveAction && ui.Kind == MoveKind.Move
               && ui.Destination == new Vector2Int(2, 0) && ui.Path == path && !string.IsNullOrEmpty(ui.CombatEndContext),
            $"ForUi: no quirk bit, the standard action converts, the move action is spent before the walk ({ui.Quirks})");
        MoveRequest uiWithdraw = MoveRequest.ForUi(null, path, null, true, 0.08f);
        Assert(uiWithdraw.IsWithdraw && uiWithdraw.Spend == MoveSpend.ExecutorOwned && uiWithdraw.Quirks == MoveQuirk.None && uiWithdraw.Label == "AoO",
            "ForUi withdraw: the withdraw pays its own action, no quirk bit, the PC's AoO label");

        MoveRequest ai = MoveRequest.ForAI(null, new Vector2Int(5, 5), 0.08f, MoveSpend.MoveAction);
        Assert(ai.Quirks == MoveQuirk.AiSegmentLoop && ai.Spend == MoveSpend.MoveAction,
            $"ForAI: only the segment walker bit, the move action spent by the executor before the first step (D3) ({ai.Quirks})");
        Assert(!ai.AllowConvertStandard && ai.Path == null && ai.Destination == new Vector2Int(5, 5) && ai.Label == "AoO"
               && ai.CombatEndContext == null && ai.StartLogLine == null,
            "ForAI: no conversion, the executor finds the path, no combat-end check of its own, no start line");
        MoveRequest aiWithdraw = MoveRequest.ForAIWithdraw(null, new Vector2Int(5, 5), 0.08f);
        Assert(aiWithdraw.IsWithdraw && aiWithdraw.Spend == MoveSpend.ExecutorOwned && aiWithdraw.Label == "AoO (Withdraw)" && aiWithdraw.Has(MoveQuirk.AiSegmentLoop),
            "ForAIWithdraw: its own action, the segment walker, the withdraw AoO label");

        MoveRequest charmed = MoveRequest.ForAI(null, Vector2Int.zero, 0.08f, MoveSpend.MoveAction).ConvertingStandard();
        Assert(charmed.AllowConvertStandard && charmed.Quirks == MoveQuirk.AiSegmentLoop,
            $"ConvertingStandard: the compulsion moves convert the standard action and keep only the walker bit ({charmed.Quirks})");
        MoveRequest logged = MoveRequest.ForAI(null, Vector2Int.zero, 0.08f, MoveSpend.MoveAction).LoggingOnStart("X moves to touch Y.");
        Assert(logged.StartLogLine == "X moves to touch Y." && logged.Quirks == MoveQuirk.AiSegmentLoop,
            "LoggingOnStart sets only the line shown once the move starts");
    }

    /// <summary>A probe step hook (batch 13 step 10): for its one mover it records whether the move action was already
    /// spent when the first step was asked, and refuses the step, so the per-step walk ends before any coroutine.</summary>
    private sealed class SpendOrderProbe : IMovementStepHook
    {
        public CharacterController Mover;
        public bool Asked;
        public bool SpentWhenAsked;

        public void BeforeStep(CharacterController actor, Vector2Int from, Vector2Int to, MoveKind kind, ref bool stop)
        {
            if (actor == null || actor != Mover)
                return;
            if (!Asked)
                SpentWhenAsked = actor.Actions != null && actor.Actions.MoveActionUsed;
            Asked = true;
            stop = true;
        }

        public void OnStep(CharacterController actor, Vector2Int from, Vector2Int to, MoveKind kind, ref bool stop) { }
    }

    /// <summary>
    /// GameManager.ExecuteMoveAction runs to its end without a walk here (no coroutine may start; RunToEnd stops and fails
    /// one that does). D3 (batch 13 step 10, delegated RAW reading of PHB p.138): an AI move with no path (its own square,
    /// a destination it cannot reach) is refused with <see cref="MoveAttempt.NoPath"/> and spends nothing; the move action
    /// is spent before the first step (a probe step hook sees it spent) and stays spent when the walk stops at that step; a
    /// prone PC's move or withdraw is refused by the shared legality with nothing spent. Each case first checks its
    /// precondition and is skipped without it, so a later rule change cannot start a real walk for a fixture the suite
    /// destroys.
    /// </summary>
    private static void TestExecuteMoveActionSpends()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] TestExecuteMoveActionSpends (needs a scene GameManager)");
            return;
        }

        // A pathless AI move: its own square.
        var nowhere = Create("ML_MoveNowhere");
        MoveRequest req = MoveRequest.ForAI(nowhere, nowhere.GridPosition, 0.01f, MoveSpend.MoveAction);
        bool waited = RunToEnd(gm.ExecuteMoveAction(req));
        Assert(!waited && req.Attempt != null && !req.Attempt.Started && req.Attempt.Refused && req.Attempt.NoPath
               && !nowhere.Actions.MoveActionUsed && !nowhere.Actions.StandardConvertedToMove,
            $"A pathless AI move (its own square) is refused and spends nothing (D3, AI-076) ({req.Attempt?.Reason})");

        // A pathless AI move: a destination no path reaches (off the grid).
        if (gm.Grid != null)
        {
            var unreachable = Create("ML_MoveUnreachable");
            Vector2Int offGrid = new Vector2Int(-500, -500);
            MoveRequest far = MoveRequest.ForAI(unreachable, offGrid, 0.01f, MoveSpend.MoveAction).ConvertingStandard();
            waited = RunToEnd(gm.ExecuteMoveAction(far));
            Assert(!waited && far.Attempt.Refused && far.Attempt.NoPath && !far.Attempt.Started
                   && !unreachable.Actions.MoveActionUsed && !unreachable.Actions.StandardConvertedToMove,
                $"An AI move no path reaches is refused and spends nothing, the standard action not converted either (D3) ({far.Attempt.Reason}, {Why(unreachable, MoveKind.Move)})");
        }
        else
        {
            Debug.Log("  [SKIP] An unreachable AI move (needs the scene grid)");
        }

        var callerPays = Create("ML_MoveNowhereCallerPays");
        var given = new MoveAttempt();
        MoveRequest callerReq = MoveRequest.ForAI(callerPays, callerPays.GridPosition, 0.01f, MoveSpend.None, given);
        RunToEnd(gm.ExecuteMoveAction(callerReq));
        Assert(!callerPays.Actions.MoveActionUsed, "MoveSpend.None spends nothing (the caller pays)");
        Assert(ReferenceEquals(callerReq.Attempt, given), "The request's attempt is the one record the executor fills");

        // The spend precedes the first step, and a walk stopped at that step stays spent (D3). An AI-built request on the
        // per-step walk (its walker bit cleared: the spend does not depend on the walker), the probe refusing the step.
        var payer = Create("ML_MovePaysFirst");
        if (!Ok(payer, MoveKind.Move))
        {
            Debug.Log($"  [SKIP] The spend-before-the-first-step case (the fixture may not move: {Why(payer, MoveKind.Move)})");
        }
        else
        {
            var probe = new SpendOrderProbe { Mover = payer };
            MovementService.RegisterStepHook(probe);
            try
            {
                Vector2Int start = payer.GridPosition;
                MoveRequest first = MoveRequest.ForAI(payer, start + Vector2Int.right, 0.01f, MoveSpend.MoveAction);
                first.Path = new List<Vector2Int> { start + Vector2Int.right };
                first.Quirks = MoveQuirk.None;
                waited = RunToEnd(gm.ExecuteMoveAction(first));
                Assert(probe.Asked && probe.SpentWhenAsked,
                    $"The AI move action is spent before the first step (D3): the step hook saw it spent (asked {probe.Asked}, spent {probe.SpentWhenAsked})");
                Assert(first.Attempt.Started && !first.Attempt.Refused && payer.Actions.MoveActionUsed && payer.GridPosition == start,
                    "A move stopped at its first step (a step hook) stays spent, and the mover stays in its square");
            }
            finally
            {
                MovementService.UnregisterStepHook(probe);
            }
        }

        var pronePc = Create("ML_PronePcMove");
        pronePc.ApplyCondition(CombatConditionType.Prone, -1, "Test");
        if (MovementService.CanStartMovement(pronePc, MoveKind.Move).Ok)
        {
            Debug.Log("  [SKIP] A prone PC's move (the shared legality allows it now: a real walk would start)");
        }
        else
        {
            var path = new List<Vector2Int> { pronePc.GridPosition + Vector2Int.right };
            MoveRequest prone = MoveRequest.ForUi(pronePc, path, null, false, 0.01f);
            waited = RunToEnd(gm.ExecuteMoveAction(prone));
            Assert(!waited && prone.Attempt.Refused && !prone.Attempt.NoPath && !prone.Attempt.Started && !pronePc.Actions.MoveActionUsed && !pronePc.Actions.StandardConvertedToMove,
                $"A prone PC's move is refused by the shared legality with nothing spent ({prone.Attempt.Reason})");
        }

        var proneWithdraw = Create("ML_PronePcWithdraw");
        proneWithdraw.ApplyCondition(CombatConditionType.Prone, -1, "Test");
        if (MovementService.CanStartMovement(proneWithdraw, MovementService.WithdrawKindFor(proneWithdraw)).Ok)
        {
            Debug.Log("  [SKIP] A prone PC's withdraw (the shared legality allows it now: a real walk would start)");
        }
        else
        {
            MoveRequest proneW = MoveRequest.ForUi(proneWithdraw, new List<Vector2Int> { proneWithdraw.GridPosition + Vector2Int.right }, null, true, 0.01f);
            RunToEnd(gm.ExecuteMoveAction(proneW));
            Assert(proneW.Attempt.Refused && !proneWithdraw.Actions.FullRoundActionUsed && !proneWithdraw.IsWithdrawing,
                $"A prone PC's withdraw is refused with nothing spent ({proneW.Attempt.Reason})");
        }
    }

    /// <summary>Runs <paramref name="routine"/> to its end, inlining nested enumerators; true when it yielded anything. A
    /// coroutine or a wait (a walk started on the real GameManager) is stopped and fails the suite: these cases must
    /// return before the walk.</summary>
    private static bool RunToEnd(System.Collections.IEnumerator routine)
    {
        bool yielded = false;
        var stack = new Stack<System.Collections.IEnumerator>();
        stack.Push(routine);
        for (int i = 0; i < 1000 && stack.Count > 0; i++)
        {
            System.Collections.IEnumerator top = stack.Peek();
            if (!top.MoveNext())
            {
                stack.Pop();
                continue;
            }
            yielded = true;
            if (top.Current is System.Collections.IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            if (top.Current is Coroutine started && GameManager.Instance != null)
                GameManager.Instance.StopCoroutine(started);
            Assert(false, $"ExecuteMoveAction started a walk in a static test ({(top.Current != null ? top.Current.GetType().Name : "null")})");
            return true;
        }
        return yielded;
    }

    /// <summary>
    /// The AI's move sites (batch 13 step 9's 24 move calls; batch 13 step 10, AI-076: every move pays before its first
    /// step, D3) are classified as their source reads: every move call builds a MoveRequest (no caller-pays form is left),
    /// each file has its expected number of executor-paid (MoveSpend.MoveAction) and caller-paid (MoveSpend.None) moves,
    /// inside the methods that make a move call no routine spends a move action of its own (the held charge's touch move
    /// and the harness's typed Move step pass MoveSpend.MoveAction since step 10), and no after-walk spend modifier is
    /// left. A move action spent for something other than movement, outside those methods, is not counted. The frightened
    /// run pays its full-round action (MoveSpend.None); AiOutOfReach's catalog pick runs the entry's own request.
    /// </summary>
    private static void TestMoveSiteClassification()
    {
        const string owner = " (pinned by batch 13 step 10, AI-076, D3)";
        // file, executor-paid moves, caller-paid moves, the move routines' own move spends left
        var expected = new (string File, int Paid, int CallerPays, int OwnSpends)[]
        {
            ("Services/AIService.cs", 12, 0, 0),
            ("_Core/GameManager.NPCTurns.cs", 3, 0, 0),
            ("Combat/Behaviors/CharmedBehaviorController.cs", 1, 0, 0),
            ("Combat/Behaviors/ConfusedBehaviorController.cs", 1, 0, 0),
            ("Combat/Behaviors/FrightenedBehaviorController.cs", 1, 1, 0),
            ("Combat/Behaviors/SuggestedBehaviorController.cs", 1, 0, 0),
        };
        string root = Path.Combine(Application.dataPath, "Scripts");
        foreach (var e in expected)
        {
            string src = ReadCode(root, e.File);
            if (src == null)
                continue;
            int calls = Regex.Matches(src, @"MoveCharacterAlongComputedPathForAI\(").Count;
            int requestCalls = Regex.Matches(src, @"MoveCharacterAlongComputedPathForAI\(\s*(MoveRequest\.ForAI|CharmedMove|ConfusedMove)\(").Count;
            int paid = Regex.Matches(src, @"MoveSpend\.MoveAction\b").Count;
            int callerPays = Regex.Matches(src, @"MoveSpend\.None\b").Count;
            var ownIn = new List<string>();
            int own = 0;
            foreach (var m in MethodsContaining(src, @"MoveCharacterAlongComputedPathForAI\("))
            {
                int n = Regex.Matches(m.Body, @"\.UseMoveAction\(\)").Count + Regex.Matches(m.Body, @"\bConsumeMoveAction\(").Count
                        + Regex.Matches(m.Body, @"\.ConvertStandardToMove\(\)").Count;
                own += n;
                if (n > 0)
                    ownIn.Add(m.Name);
            }
            Assert(calls > 0 && calls == requestCalls, $"{e.File}: every AI move call builds a MoveRequest ({requestCalls} of {calls}){owner}");
            Assert(paid == e.Paid && callerPays == e.CallerPays && own == e.OwnSpends,
                $"{e.File}: {e.Paid} executor-paid, {e.CallerPays} caller-paid moves, {e.OwnSpends} own move spends in the move routines (found {paid}, {callerPays}, {own}: {string.Join(", ", ownIn)}){owner}");
            Assert(!Regex.IsMatch(src, @"\.Spending(EvenIfDropped|AfterCombatEnd|OnlyIfStarted)\(") && !src.Contains("ShouldSpendAfterWalk"),
                $"{e.File}: no after-walk spend modifier is left{owner}");
        }

        // The harness's typed Move step pays through the executor too (no caller-pays overload is left).
        string steps = ReadCode(root, "Tests/Scenarios/ScenarioSteps.cs");
        string gmCore = ReadCode(root, "_Core/GameManager.cs");
        if (steps != null)
        {
            // The step's runner case is the "case StepKind.Move:" that builds the attempt (the first one is the step's ToString).
            string moveCase = string.Empty;
            for (int from = steps.IndexOf("case StepKind.Move:", System.StringComparison.Ordinal); from >= 0 && moveCase.Length == 0;
                 from = steps.IndexOf("case StepKind.Move:", from + 1, System.StringComparison.Ordinal))
            {
                int to = steps.IndexOf("case StepKind.Withdraw:", from, System.StringComparison.Ordinal);
                string body = to > from ? steps.Substring(from, to - from) : string.Empty;
                if (body.Contains("MoveCharacterAlongComputedPathForAI("))
                    moveCase = body;
            }
            Assert(moveCase.Contains("MoveSpend.MoveAction") && !moveCase.Contains("UseMoveAction") && !moveCase.Contains("ConvertStandardToMove()"),
                "The harness's typed Move step passes MoveSpend.MoveAction and spends nothing itself" + owner);
        }
        if (gmCore != null)
            Assert(!Regex.IsMatch(gmCore, @"MoveCharacterAlongComputedPathForAI\(\s*CharacterController"),
                "The caller-pays MoveCharacterAlongComputedPathForAI overload is gone" + owner);

        string charmed = ReadCode(root, "Combat/Behaviors/CharmedBehaviorController.cs");
        string confused = ReadCode(root, "Combat/Behaviors/ConfusedBehaviorController.cs");
        string npcTurns = ReadCode(root, "_Core/GameManager.NPCTurns.cs");
        string outOfReach = ReadCode(root, "AI/AiOutOfReach.cs");
        if (charmed != null)
            Assert(Regex.Matches(charmed, @"ForAI\(\s*CharmedMove\(").Count == 3 && charmed.Contains(".ConvertingStandard()"),
                "The charmed creature's three moves convert the standard action" + owner);
        if (confused != null)
            Assert(Regex.Matches(confused, @"ForAI\(\s*ConfusedMove\(").Count == 2 && confused.Contains(".ConvertingStandard()")
                   && confused.Contains(".LoggingOnStart("),
                "The confused creature's two moves convert the standard action; its flight line shows only once it starts" + owner);
        if (npcTurns != null)
            Assert(npcTurns.Contains(".LoggingOnStart("),
                "The search rush's line shows only once its move starts (no line for a refused move)" + owner);
        if (outOfReach != null)
            Assert(outOfReach.Contains("ActionIds.Move") && !outOfReach.Contains("MoveRequest"),
                "AiOutOfReach's move is the catalog pick with no request (the executor's DefaultAiRequest pays it before the first step)" + owner);
    }

    /// <summary>The file's code with comments removed, or null (and a failed assertion) when it is missing.</summary>
    private static string ReadCode(string root, string relative)
    {
        string file = Path.Combine(root, relative);
        if (!File.Exists(file))
        {
            Assert(false, $"{relative} exists for the move-site scan (moved? update TestMoveSiteClassification)");
            return null;
        }
        return StripComments(file);
    }

    /// <summary>The class-member methods (4-space indent) of <paramref name="code"/> whose body matches
    /// <paramref name="pattern"/>: name and body, found by brace matching from the signature.</summary>
    private static List<(string Name, string Body)> MethodsContaining(string code, string pattern)
    {
        var result = new List<(string Name, string Body)>();
        var signature = new Regex(@"^    (?:(?:private|public|internal|protected|static|override|virtual|async|sealed)\s+)+[\w<>\[\],\.\s\?]*?(\w+)\s*\(",
            RegexOptions.Multiline);
        MatchCollection signatures = signature.Matches(code);
        var seen = new HashSet<int>();
        foreach (Match call in Regex.Matches(code, pattern))
        {
            Match owner = null;
            foreach (Match sig in signatures)
            {
                if (sig.Index > call.Index)
                    break;
                owner = sig;
            }
            if (owner == null || !seen.Add(owner.Index))
                continue;
            int open = code.IndexOf('{', owner.Index);
            if (open < 0 || open > call.Index)
                continue;
            int depth = 0;
            int end = open;
            for (; end < code.Length; end++)
            {
                if (code[end] == '{') depth++;
                else if (code[end] == '}' && --depth == 0) break;
            }
            result.Add((owner.Groups[1].Value, code.Substring(open, Mathf.Min(end, code.Length - 1) - open + 1)));
        }
        return result;
    }

    /// <summary>The file's text without comments: block comments, and a line's "//" tail outside a string or char literal.</summary>
    private static string StripComments(string path)
    {
        string text = Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/", " ", RegexOptions.Singleline);
        var code = new System.Text.StringBuilder();
        foreach (string line in text.Split('\n'))
        {
            int cut = -1;
            bool inString = false;
            bool inChar = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inString || inChar)
                {
                    if (ch == '\\') { i++; continue; }
                    if (inString && ch == '"') inString = false;
                    else if (inChar && ch == '\'') inChar = false;
                    continue;
                }
                if (ch == '"') { inString = true; continue; }
                if (ch == '\'') { inChar = true; continue; }
                if (ch == '/' && i + 1 < line.Length && line[i + 1] == '/') { cut = i; break; }
            }
            code.AppendLine(cut >= 0 ? line.Substring(0, cut) : line);
        }
        return code.ToString();
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
