#if UNITY_EDITOR
using System.Collections.Generic;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.AI
{
/// <summary>
/// Batch 12 step 8 (docs/designs/pc_npc_parity_batch12.md, CMB-094): the pinpoint rules of the LastKnownPositionTracker
/// every combatant has, PC or NPC, and the shared turn-start check (PerceptionRules.ProcessTurnStart).
/// <list type="bullet">
/// <item>A Listen pinpoint records the square: the creature is pinpointed while it stays there, the pinpointed square
/// becomes the last-known square, and the pinpoint lapses once it moves (DMG p.295).</item>
/// <item>A deafened creature automatically fails Listen checks (PHB p.307, DMG p.300): even a +50 Listen pinpoints
/// nothing; the same creature hearing again pinpoints.</item>
/// <item>The turn-start check of a PC-side creature pinpoints a tracked invisible enemy, as it does for an NPC.</item>
/// </list>
/// Each observer has a +50 Listen fixture, so the unseeded d20 always beats DC 20. Play mode (CharacterController.Init
/// needs its Awake, TST-009); no grid.
/// </summary>
public static class LastKnownPositionTrackerTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== LAST KNOWN POSITION TRACKER TESTS (batch 12 step 8, CMB-094) ======");

        TestHelpers.EnsureCoreDatabasesInitialized();
        SpellDatabase.Init();

        var made = new List<CharacterController>();
        try
        {
            TestPinpointRecordsSquareAndLapsesOnMove(made);
            TestDeafenedPinpointsNothing(made);
            TestTurnStartCheckForPcSide(made);
        }
        finally
        {
            foreach (CharacterController c in made)
                if (c != null)
                    Object.DestroyImmediate(c.gameObject);
        }

        Debug.Log($"====== Last Known Position Tracker Results: {_passed} passed, {_failed} failed ======");
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
            Debug.LogError($"  [FAIL] {testName}" + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
        }
    }

    private static CharacterController Make(List<CharacterController> made, string name, CharacterTeam team, int x, int y)
    {
        CharacterController c = TestHelpers.CreateCharacter(name: name, characterClass: "Fighter", level: 1);
        c.SetTeam(team);
        TestHelpers.SetGridPosition(c, x, y);
        made.Add(c);
        return c;
    }

    /// <summary>Test fixture: +50 on Listen checks, so any d20 beats the pinpoint DC of 20.</summary>
    private static void KeenEars(CharacterController c)
        => c.Stats.Bonuses.Set("test_listen", BonusTarget.Skill, BonusType.Circumstance, 50, "Test fixture", "Listen");

    private static bool MakeInvisible(CharacterController c)
    {
        // As the Invisibility cast does (GameManager.SpellCasting): the recipient's StatusEffectManager, added when missing.
        StatusEffectManager statusMgr = c.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
            statusMgr = c.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(c.Stats);
        ActiveSpellEffect effect = statusMgr.AddEffect(SpellDatabase.GetSpell(SpellNames.INVISIBILITY).Clone(), c.Stats.CharacterName, 10);
        if (effect != null)
            c.ApplyInvisibilityEffect(effect.RemainingRounds, c, isMoving: false);
        return c.HasActiveInvisibilityEffect;
    }

    private static void TestPinpointRecordsSquareAndLapsesOnMove(List<CharacterController> made)
    {
        CharacterController observer = Make(made, "Pinpoint Listener", CharacterTeam.Player, 0, 0);
        CharacterController target = Make(made, "Pinpoint Sneak", CharacterTeam.Enemy, 2, 0);
        KeenEars(observer);
        LastKnownPositionTracker tracker = observer.GetComponent<LastKnownPositionTracker>();
        Assert(tracker != null, "the observer has a tracker from Init (CMB-094)");
        if (tracker == null)
            return;

        tracker.UpdateLastKnownPosition(target);
        Assert(MakeInvisible(target) && !observer.CanSee(target), "control: the target is invisible and unseen");
        TestHelpers.SetGridPosition(target, 3, 0);

        tracker.AttemptListenChecks(new List<CharacterController> { target }, null);
        Assert(tracker.IsPinpointedThisRound(target) && observer.HasPinpointed(target),
            "a Listen check of +50 against DC 20 pinpoints the moved target (DMG p.295)");
        Vector2Int? last = tracker.GetLastKnownPosition(target);
        Assert(last.HasValue && last.Value == new Vector2Int(3, 0),
            "the pinpointed square becomes the last-known square", last.HasValue ? last.Value.ToString() : "none");

        TestHelpers.SetGridPosition(target, 4, 0);
        Assert(!tracker.IsPinpointedThisRound(target) && !observer.HasPinpointed(target),
            "the pinpoint lapses once the target leaves the pinpointed square (DMG p.295)");
        last = tracker.GetLastKnownPosition(target);
        Assert(last.HasValue && last.Value == new Vector2Int(3, 0),
            "the last-known square stays the pinpointed one after the target moves", last.HasValue ? last.Value.ToString() : "none");

        TestHelpers.SetGridPosition(target, 3, 0);
        Assert(tracker.IsPinpointedThisRound(target), "back in the pinpointed square before the next check, it is still pinpointed");
    }

    private static void TestDeafenedPinpointsNothing(List<CharacterController> made)
    {
        CharacterController observer = Make(made, "Deaf Listener", CharacterTeam.Player, 0, 5);
        CharacterController target = Make(made, "Deaf Sneak", CharacterTeam.Enemy, 2, 5);
        KeenEars(observer);
        LastKnownPositionTracker tracker = observer.GetComponent<LastKnownPositionTracker>();
        if (tracker == null)
        {
            Assert(false, "the deaf observer has a tracker from Init");
            return;
        }

        tracker.UpdateLastKnownPosition(target);
        MakeInvisible(target);
        observer.ApplyCondition(CombatConditionType.Deafened, 3, "UnitTest");
        Assert(observer.IsDeaf(), "control: the observer is deafened");

        tracker.AttemptListenChecks(new List<CharacterController> { target }, null);
        Assert(!tracker.IsPinpointedThisRound(target), "a deafened creature fails its Listen check automatically and pinpoints nothing (PHB p.307)");

        observer.Stats.ActiveConditions.RemoveAll(c => c != null && c.Type == CombatConditionType.Deafened);
        Assert(!observer.IsDeaf(), "control: the deafness is gone");
        tracker.AttemptListenChecks(new List<CharacterController> { target }, null);
        Assert(tracker.IsPinpointedThisRound(target), "hearing again, the same creature pinpoints the target");
    }

    private static void TestTurnStartCheckForPcSide(List<CharacterController> made)
    {
        CharacterController pc = Make(made, "Turn Start Listener", CharacterTeam.Player, 0, 10);
        CharacterController enemy = Make(made, "Turn Start Sneak", CharacterTeam.Enemy, 2, 10);
        KeenEars(pc);
        var all = new List<CharacterController> { pc, enemy };

        PerceptionRules.ProcessTurnStart(pc, all);
        LastKnownPositionTracker tracker = pc.GetComponent<LastKnownPositionTracker>();
        Assert(tracker != null && tracker.HasLastKnownPosition(enemy) && !tracker.IsPinpointedThisRound(enemy),
            "the turn-start check records a visible enemy's square and pinpoints nothing");

        MakeInvisible(enemy);
        TestHelpers.SetGridPosition(enemy, 2, 11);
        PerceptionRules.ProcessTurnStart(pc, all);
        Assert(tracker != null && tracker.IsPinpointedThisRound(enemy) && pc.CanLocateForRangedAttack(enemy),
            "a PC-side creature's turn-start check pinpoints the tracked invisible enemy, which it can then aim at (DMG p.295)");
    }
}
}
#endif
