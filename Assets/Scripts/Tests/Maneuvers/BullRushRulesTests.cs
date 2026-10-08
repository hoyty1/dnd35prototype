using System.Collections.Generic;
using System.Reflection;
using DND35.AI;
using Tests.Utilities;
using UnityEngine;

namespace Tests.Maneuvers
{
/// <summary>
/// Bull rush push and follow (PHB p.154, CMB-098).
/// Pure rules (BullRushRules) run anywhere; the scenarios place Stats-built actors on the scene
/// grid and call GameManager.ResolveBullRushPushAndFollow with a fixed check margin, so they need
/// Play mode (they log [SKIP] otherwise). Run via BullRushRulesTests.RunAll().
/// </summary>
public static class BullRushRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        TestHelpers.EnsureCoreDatabasesInitialized();

        Debug.Log("========== BULL RUSH RULES TESTS ==========");

        TestMaxPushSquares();
        TestMovementLimit();
        TestPushDirection();
        TestScenarioMediumFollowsLargeEast();
        TestScenarioLargeAttackerFollowsEast();
        TestScenarioAllyAoOOnPushedDefender();
        TestMisdirection();
        TestScenarioNotFollowingPushesOneSquare();
        TestScenarioBlockedFollowerEndsPushAtOneSquare();
        TestScenarioProneAttackerCannotFollow();
        TestPartnerNeverTakesAnAoO();
        TestScenarioAlreadyProvokedOpponentsSkipFollowAoO();

        Debug.Log($"========== BULL RUSH RESULTS: {_passed} passed, {_failed} failed ==========");
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

    // ------------------------------------------------------------------
    // Pure rules
    // ------------------------------------------------------------------

    private static void TestMaxPushSquares()
    {
        Assert(BullRushRules.GetMaxPushSquares(12, false, 6) == 1, "Not following: push 5 ft only, whatever the margin (PHB p.154)");
        Assert(BullRushRules.GetMaxPushSquares(12, true, 6) == 3, "Following, margin 12, limit 6: 1 + 12/5 = 3 squares");
        Assert(BullRushRules.GetMaxPushSquares(30, true, 4) == 4, "Following: the movement limit caps the push (margin 30, limit 4 -> 4)");
        Assert(BullRushRules.GetMaxPushSquares(4, true, 6) == 1, "Following, margin 4: 1 square");
        Assert(BullRushRules.GetMaxPushSquares(10, true, 0) == 0,
            "Following with no movement left is impossible: 0 follow squares, so only push 5 ft and stay (PHB p.154 movement limit)");
        Assert(BullRushRules.GetMaxPushSquares(-1, true, 6) == 0 && BullRushRules.GetMaxPushSquares(-1, false, 6) == 0,
            "Failure (margin below 0) pushes nothing");
        Assert(BullRushRules.GetMaxPushSquares(20, true, 4, diagonal: true) == 3,
            "Diagonal follow pays 5-10-5 ft: limit 4 squares allows 3 diagonal squares (PHB p.148)");
        Assert(BullRushRules.GetMaxPushSquares(20, true, 4, diagonal: true, previousDiagonals: 1) == 2,
            "Diagonal follow after an odd number of charge diagonals starts at 10 ft: limit 4 allows 2 (costs 2, 1)");
        Assert(BullRushRules.GetMaxPushSquares(20, true, 1, diagonal: true, previousDiagonals: 1) == 0,
            "A 10-ft first diagonal does not fit a 1-square limit: no follow");
    }

    private static void TestMovementLimit()
    {
        CharacterController actor = null;
        try
        {
            actor = TestHelpers.CreateCharacter(name: "BullRushLimitActor");
            int speed = BullRushRules.GetSpeedSquares(actor);
            Assert(speed > 0, $"Test actor has a speed ({speed} squares)");
            Assert(BullRushRules.GetMovementLimitSquares(actor, false, 0) == speed,
                "Standard bull rush: movement limit is the speed (earlier move not subtracted; owner decision 2026-10-07)");
            actor.HasMovedThisTurn = true;
            bool moveSpent = actor.Actions != null && actor.Actions.UseMoveAction();
            Assert(moveSpent && BullRushRules.GetMovementLimitSquares(actor, false, 0) == speed,
                "Standard bull rush after a move action this turn: the limit is still the speed (owner decision 2026-10-07)");
            actor.HasMovedThisTurn = false;
            actor.Actions?.Reset();
            Assert(BullRushRules.GetMovementLimitSquares(actor, true, 0) == speed * 2,
                "Charge bull rush with no movement: twice the speed");
            Assert(BullRushRules.GetMovementLimitSquares(actor, true, 3) == speed * 2 - 3,
                "Charge bull rush: twice the speed minus the charge path cost");
            Assert(BullRushRules.GetMovementLimitSquares(actor, true, speed * 2 + 5) == 0,
                "Charge bull rush that used all its movement: limit 0");

            actor.HasTakenFiveFootStep = true;
            Assert(BullRushRules.GetMovementLimitSquares(actor, false, 0) == 0,
                "After a 5-foot step the attacker cannot move with the defender (PHB p.144)");
            actor.HasTakenFiveFootStep = false;

            actor.ApplyCondition(CombatConditionType.Prone, -1, "Test");
            Assert(BullRushRules.GetMovementLimitSquares(actor, false, 0) == 0,
                "A prone attacker has no movement to follow with");
            actor.RemoveCondition(CombatConditionType.Prone);
        }
        finally
        {
            TestHelpers.Cleanup(actor != null ? actor.gameObject : null);
        }
    }

    private static void TestPushDirection()
    {
        Assert(BullRushRules.GetPushDirection(new Vector2Int(0, 0), 1, new Vector2Int(1, 0), 2) == Vector2Int.right,
            "Medium west of a Large's lower row pushes it east");
        Assert(BullRushRules.GetPushDirection(new Vector2Int(0, 1), 1, new Vector2Int(1, 0), 2) == Vector2Int.right,
            "Medium west of a Large's upper row pushes it east");
        Assert(BullRushRules.GetPushDirection(new Vector2Int(3, 1), 1, new Vector2Int(1, 0), 2) == Vector2Int.left,
            "Medium east of a Large pushes it west");
        Assert(BullRushRules.GetPushDirection(new Vector2Int(0, 0), 2, new Vector2Int(2, 1), 1) == Vector2Int.right,
            "Large attacker west of a Medium beside its upper row pushes it east");
        Assert(BullRushRules.GetPushDirection(new Vector2Int(0, 0), 1, new Vector2Int(1, 1), 1) == new Vector2Int(1, 1),
            "Footprints touching at a corner push diagonally");
        Assert(BullRushRules.GetPushDirection(new Vector2Int(0, 0), 1, new Vector2Int(0, 0), 1) == Vector2Int.right,
            "Coincident footprints fall back to (1, 0)");
    }

    // ------------------------------------------------------------------
    // Play-mode scenarios
    // ------------------------------------------------------------------

    /// <summary>AI profile that always pushes 5 ft and stays (exercises the profile hook).</summary>
    private sealed class StayBullRushProfile : AIProfile
    {
        public override int ChooseBullRushPush(CharacterController self, CharacterController target, int maxIfFollowing, out bool follow)
        {
            follow = false;
            return 1;
        }
    }

    private sealed class Scenario
    {
        public GameManager Gm;
        public readonly List<CharacterController> Actors = new List<CharacterController>();
        public readonly List<CharacterController> AddedToNpcs = new List<CharacterController>();
        public readonly List<Object> Extra = new List<Object>();

        public void Dispose()
        {
            GameManager.BullRushMisdirectionRollOverride = null;
            if (Gm != null)
            {
                for (int i = 0; i < AddedToNpcs.Count; i++)
                    Gm.NPCs.Remove(AddedToNpcs[i]);
            }

            for (int i = 0; i < Actors.Count; i++)
            {
                if (Actors[i] == null)
                    continue;
                // Free the grid squares first, or later scenarios find them occupied.
                if (Gm != null && Gm.Grid != null)
                    Gm.Grid.ClearCreatureOccupancy(Actors[i]);
                Object.DestroyImmediate(Actors[i].gameObject);
            }

            for (int i = 0; i < Extra.Count; i++)
            {
                if (Extra[i] != null)
                    Object.DestroyImmediate(Extra[i]);
            }
        }
    }

    private static bool TryBeginScenario(string name, out Scenario scenario)
    {
        scenario = null;
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.Grid == null)
        {
            Debug.Log($"  [SKIP] {name}: needs Play mode (GameManager and grid)");
            return false;
        }

        scenario = new Scenario { Gm = gm };
        return true;
    }

    /// <summary>
    /// A Stats-built, non-controllable actor on the scene grid, registered as a combatant (GameManager.NPCs,
    /// removed again by Scenario.Dispose) so the AoO helpers see it. Plenty of HP so AoOs never drop it.
    /// </summary>
    private static CharacterController AddActor(Scenario scenario, string name, CharacterTeam team, Vector2Int anchor, SizeCategory size)
    {
        CharacterController actor = TestHelpers.CreateCharacter(name: name);
        scenario.Actors.Add(actor);
        actor.ConfigureTeamControl(team, false);
        actor.Stats.SetBaseSizeCategory(size);
        actor.Stats.AdjustMaxHP(500);
        actor.Stats.CurrentHP += 500;
        ThreatSystem.ResetAoOForTurn(actor);

        SquareCell cell = scenario.Gm.Grid.GetCell(anchor);
        if (cell != null)
            actor.MoveToCell(cell, markAsMoved: false);

        scenario.Gm.NPCs.Add(actor);
        scenario.AddedToNpcs.Add(actor);
        return actor;
    }

    private static void ResolvePush(GameManager gm, CharacterController attacker, CharacterController target, int margin,
        bool isCharge = false, int squaresMoved = 0, ICollection<CharacterController> alreadyProvoked = null)
    {
        var result = new SpecialAttackResult { Success = true, CheckTotal = 20 + margin, OpposedTotal = 20 };
        MethodInfo method = typeof(GameManager).GetMethod("ResolveBullRushPushAndFollow", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(gm, new object[] { attacker, target, result, isCharge, squaresMoved, null, 0, alreadyProvoked });
    }

    private static void TestScenarioMediumFollowsLargeEast()
    {
        // (a) A Medium attacker west of a Large defender's lower row wins by 3 (one square when
        // following); the AI default follows. Both anchors move one square east.
        if (!TryBeginScenario("Medium follows Large", out Scenario s))
            return;
        try
        {
            var attacker = AddActor(s, "BRMediumAttacker", CharacterTeam.Player, new Vector2Int(4, 14), SizeCategory.Medium);
            var target = AddActor(s, "BRLargeDefender", CharacterTeam.Enemy, new Vector2Int(5, 14), SizeCategory.Large);
            Assert(attacker.GridPosition == new Vector2Int(4, 14) && target.GridPosition == new Vector2Int(5, 14),
                "Scenario (a) setup: attacker (4,14), Large defender (5,14)");

            ResolvePush(s.Gm, attacker, target, margin: 3);
            Assert(target.GridPosition == new Vector2Int(6, 14),
                $"(a) Large defender pushed 1 square east (at {target.GridPosition})");
            Assert(attacker.GridPosition == new Vector2Int(5, 14),
                $"(a) Medium attacker follows 1 square east into the vacated square (at {attacker.GridPosition})");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"(a) threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            s.Dispose();
        }
    }

    private static void TestScenarioLargeAttackerFollowsEast()
    {
        // (b) A Large attacker wins by 7 against a Medium beside its upper row: following allows
        // 2 squares; the defender moves 2 east and the attacker follows 2.
        if (!TryBeginScenario("Large attacker follows", out Scenario s))
            return;
        try
        {
            var attacker = AddActor(s, "BRLargeAttacker", CharacterTeam.Enemy, new Vector2Int(3, 10), SizeCategory.Large);
            var target = AddActor(s, "BRMediumDefender", CharacterTeam.Player, new Vector2Int(5, 11), SizeCategory.Medium);

            ResolvePush(s.Gm, attacker, target, margin: 7);
            Assert(target.GridPosition == new Vector2Int(7, 11),
                $"(b) Medium defender pushed 2 squares east (at {target.GridPosition})");
            Assert(attacker.GridPosition == new Vector2Int(5, 10),
                $"(b) Large attacker follows 2 squares east (at {attacker.GridPosition})");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"(b) threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            s.Dispose();
        }
    }

    private static void TestScenarioAllyAoOOnPushedDefender()
    {
        // (c) The attacker's ally threatens the square the defender leaves and takes an AoO against
        // it; the attacker gets none (PHB p.154). The d100 is fixed at 100 so nothing strays.
        if (!TryBeginScenario("Ally AoO on pushed defender", out Scenario s))
            return;
        try
        {
            var attacker = AddActor(s, "BRAoOAttacker", CharacterTeam.Player, new Vector2Int(4, 17), SizeCategory.Medium);
            var target = AddActor(s, "BRAoODefender", CharacterTeam.Enemy, new Vector2Int(5, 17), SizeCategory.Medium);
            var ally = AddActor(s, "BRAoOAlly", CharacterTeam.Player, new Vector2Int(5, 18), SizeCategory.Medium);
            GameManager.BullRushMisdirectionRollOverride = () => 100;

            ResolvePush(s.Gm, attacker, target, margin: 0);
            Assert(target.GridPosition == new Vector2Int(6, 17) && attacker.GridPosition == new Vector2Int(5, 17),
                $"(c) defender pushed to (6,17), attacker followed to (5,17) (at {target.GridPosition}, {attacker.GridPosition})");
            Assert(ally.Stats.AttacksOfOpportunityUsed == 1,
                $"(c) the attacker's ally took one AoO as the defender left its threatened square (used {ally.Stats.AttacksOfOpportunityUsed})");
            Assert(attacker.Stats.AttacksOfOpportunityUsed == 0,
                "(c) the attacker takes no AoO against the defender it pushes");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"(c) threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            s.Dispose();
        }
    }

    private static void TestMisdirection()
    {
        // (c, misdirection) PHB p.154: an AoO against one participant by anyone but the other one
        // strikes the other one on d100 1-25.
        if (!TryBeginScenario("Misdirection", out Scenario s))
            return;
        try
        {
            var attacker = AddActor(s, "BRMisAttacker", CharacterTeam.Player, new Vector2Int(10, 17), SizeCategory.Medium);
            var target = AddActor(s, "BRMisDefender", CharacterTeam.Enemy, new Vector2Int(11, 17), SizeCategory.Medium);
            var ally = AddActor(s, "BRMisAlly", CharacterTeam.Player, new Vector2Int(11, 18), SizeCategory.Medium);
            ally.Stats.MaxAttacksOfOpportunity = 10;
            target.Stats.MaxAttacksOfOpportunity = 10;

            MethodInfo method = typeof(GameManager).GetMethod("ResolveBullRushAoO", BindingFlags.Instance | BindingFlags.NonPublic);
            CombatResult Fire(CharacterController provoker, CharacterController intended, CharacterController other)
                => (CombatResult)method.Invoke(s.Gm, new object[] { provoker, intended, other, false, "Test" });

            GameManager.BullRushMisdirectionRollOverride = () => 25;
            CombatResult strayed = Fire(ally, target, attacker);
            Assert(strayed != null && strayed.Defender == attacker, "d100 25: the ally's AoO at the defender strikes the attacker instead");

            GameManager.BullRushMisdirectionRollOverride = () => 26;
            CombatResult onTarget = Fire(ally, target, attacker);
            Assert(onTarget != null && onTarget.Defender == target, "d100 26: the ally's AoO strikes the defender");

            GameManager.BullRushMisdirectionRollOverride = () => 1;
            CombatResult fromDefender = Fire(target, attacker, target);
            Assert(fromDefender != null && fromDefender.Defender == attacker, "The defender's own AoO at the attacker never strays");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Misdirection threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            s.Dispose();
        }
    }

    private static void TestScenarioNotFollowingPushesOneSquare()
    {
        // (d) The attacker's AI profile stays: even with margin 10 the defender moves exactly 1 square
        // and the attacker does not move (the extra 5 ft per 5 points needs following, PHB p.154).
        if (!TryBeginScenario("Not following", out Scenario s))
            return;
        try
        {
            var attacker = AddActor(s, "BRStayAttacker", CharacterTeam.Enemy, new Vector2Int(4, 6), SizeCategory.Medium);
            var target = AddActor(s, "BRStayDefender", CharacterTeam.Player, new Vector2Int(5, 6), SizeCategory.Medium);
            var profile = ScriptableObject.CreateInstance<StayBullRushProfile>();
            s.Extra.Add(profile);
            attacker.aiProfile = profile;

            ResolvePush(s.Gm, attacker, target, margin: 10);
            Assert(target.GridPosition == new Vector2Int(6, 6),
                $"(d) not following, margin 10: defender pushed exactly 1 square (at {target.GridPosition})");
            Assert(attacker.GridPosition == new Vector2Int(4, 6),
                $"(d) not following: attacker stays (at {attacker.GridPosition})");

            // Same margin with the default AI (follow for the maximum): 1 + 10/5 = 3 squares.
            attacker.aiProfile = null;
            attacker.MoveToCell(s.Gm.Grid.GetCell(new Vector2Int(5, 6)), markAsMoved: false);
            ResolvePush(s.Gm, attacker, target, margin: 10);
            Assert(target.GridPosition == new Vector2Int(9, 6) && attacker.GridPosition == new Vector2Int(8, 6),
                $"(d) default AI follows: margin 10 pushes 3 squares and the attacker follows 3 (at {target.GridPosition}, {attacker.GridPosition})");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"(d) threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            s.Dispose();
        }
    }

    private static void TestScenarioBlockedFollowerEndsPushAtOneSquare()
    {
        // A Large attacker wins by 10 (3 squares if following) and the default AI follows, but a
        // creature stands where the attacker's footprint would go after the first square. The extra
        // 5 ft per 5 points needs the attacker to move with the defender (PHB p.154), so the defender
        // moves exactly the base square and the attacker stays.
        if (!TryBeginScenario("Blocked follower", out Scenario s))
            return;
        try
        {
            var attacker = AddActor(s, "BRBlockedAttacker", CharacterTeam.Enemy, new Vector2Int(3, 2), SizeCategory.Large);
            var target = AddActor(s, "BRBlockedDefender", CharacterTeam.Player, new Vector2Int(5, 3), SizeCategory.Medium);
            var blocker = AddActor(s, "BRBlocker", CharacterTeam.Player, new Vector2Int(5, 2), SizeCategory.Medium);
            GameManager.BullRushMisdirectionRollOverride = () => 100;

            ResolvePush(s.Gm, attacker, target, margin: 10);
            Assert(target.GridPosition == new Vector2Int(6, 3),
                $"Blocked follower: defender pushed exactly 1 square (at {target.GridPosition})");
            Assert(attacker.GridPosition == new Vector2Int(3, 2),
                $"Blocked follower: Large attacker stays (at {attacker.GridPosition})");
            Assert(blocker.GridPosition == new Vector2Int(5, 2), "Blocked follower: the blocker is untouched");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Blocked follower threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            s.Dispose();
        }
    }

    private static void TestScenarioProneAttackerCannotFollow()
    {
        // A prone attacker (no movement, PHB p.142) wins by 10 with the default follow-for-the-maximum
        // AI: it cannot move with the defender, so the push is 5 ft and it stays.
        if (!TryBeginScenario("Prone attacker", out Scenario s))
            return;
        try
        {
            var attacker = AddActor(s, "BRProneAttacker", CharacterTeam.Enemy, new Vector2Int(4, 8), SizeCategory.Medium);
            var target = AddActor(s, "BRProneDefender", CharacterTeam.Player, new Vector2Int(5, 8), SizeCategory.Medium);
            attacker.ApplyCondition(CombatConditionType.Prone, -1, "Test");

            ResolvePush(s.Gm, attacker, target, margin: 10);
            Assert(target.GridPosition == new Vector2Int(6, 8),
                $"Prone attacker: defender pushed exactly 1 square (at {target.GridPosition})");
            Assert(attacker.GridPosition == new Vector2Int(4, 8),
                $"Prone attacker: attacker does not slide after it (at {attacker.GridPosition})");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Prone attacker threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            s.Dispose();
        }
    }

    private static void TestPartnerNeverTakesAnAoO()
    {
        // PHB p.154: the bull rush participants do not provoke from each other. The defender stands
        // beside the attacker's departure square, so it threatens it; as the partner it gets no AoO.
        // Control: the same step without the partner exclusion gives it one.
        if (!TryBeginScenario("Partner exclusion", out Scenario s))
            return;
        try
        {
            var attacker = AddActor(s, "BRPartnerAttacker", CharacterTeam.Player, new Vector2Int(13, 4), SizeCategory.Medium);
            var target = AddActor(s, "BRPartnerDefender", CharacterTeam.Enemy, new Vector2Int(13, 5), SizeCategory.Medium);
            Assert(attacker.GridPosition == new Vector2Int(13, 4) && target.GridPosition == new Vector2Int(13, 5),
                "Partner exclusion setup: attacker (13,4), defender beside it at (13,5)");
            GameManager.BullRushMisdirectionRollOverride = () => 100;

            MethodInfo step = typeof(GameManager).GetMethod("ResolveBullRushStepAoOs", BindingFlags.Instance | BindingFlags.NonPublic);
            var next = new Vector2Int(14, 4);
            step.Invoke(s.Gm, new object[] { attacker, target, next, new HashSet<CharacterController>(), "following" });
            Assert(target.Stats.AttacksOfOpportunityUsed == 0,
                $"The defender takes no AoO against the attacker moving with it, though it threatens the square left (used {target.Stats.AttacksOfOpportunityUsed})");

            step.Invoke(s.Gm, new object[] { attacker, null, next, new HashSet<CharacterController>(), "control" });
            Assert(target.Stats.AttacksOfOpportunityUsed == 1,
                $"Control: without the partner exclusion the same creature takes the AoO (used {target.Stats.AttacksOfOpportunityUsed})");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Partner exclusion threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            s.Dispose();
        }
    }

    private static void TestScenarioAlreadyProvokedOpponentsSkipFollowAoO()
    {
        // An opponent that already had its movement opportunity against the attacker this round (on
        // the charge path) gets no second AoO as the attacker follows (PHB p.138). An AoO at the bull
        // rush's start is not a movement opportunity and does not seed this set (follows from the
        // owner decision 2026-10-07 that the entry is the bull rush's own provocation; checked by
        // rules/maneuver-bullrush-reflexes). Control: without the seed, the same opponent takes one.
        if (!TryBeginScenario("Already provoked", out Scenario s))
            return;
        try
        {
            var attacker = AddActor(s, "BRSeedAttacker", CharacterTeam.Player, new Vector2Int(12, 12), SizeCategory.Medium);
            var target = AddActor(s, "BRSeedDefender", CharacterTeam.Enemy, new Vector2Int(13, 12), SizeCategory.Medium);
            var watcher = AddActor(s, "BRSeedWatcher", CharacterTeam.Enemy, new Vector2Int(12, 13), SizeCategory.Medium);
            GameManager.BullRushMisdirectionRollOverride = () => 100;

            ResolvePush(s.Gm, attacker, target, margin: 0, alreadyProvoked: new List<CharacterController> { watcher });
            Assert(attacker.GridPosition == new Vector2Int(13, 12) && target.GridPosition == new Vector2Int(14, 12),
                $"Already provoked: push 1 and follow 1 (at {target.GridPosition}, {attacker.GridPosition})");
            Assert(watcher.Stats.AttacksOfOpportunityUsed == 0,
                $"An opponent that already had a movement AoO this round takes no AoO on the follow (used {watcher.Stats.AttacksOfOpportunityUsed})");

            ResolvePush(s.Gm, attacker, target, margin: 0);
            Assert(attacker.GridPosition == new Vector2Int(14, 12),
                $"Control push: attacker follows again (at {attacker.GridPosition})");
            Assert(watcher.Stats.AttacksOfOpportunityUsed == 1,
                $"Control: the same opponent takes the follow AoO when not already provoked (used {watcher.Stats.AttacksOfOpportunityUsed})");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Already provoked threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            s.Dispose();
        }
    }
}
}
