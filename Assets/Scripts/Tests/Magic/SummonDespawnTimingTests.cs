using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using Tests.Utilities;
using Object = UnityEngine.Object;

namespace Tests.Magic
{
/// <summary>
/// CRE-312 (grouped AI turns step 4, docs/designs/grouped_ai_turns.md "Rules off wall time and token lifetime"): a
/// summon leaves play at once. Before the step GameManager.DespawnSummonWithEffect removed the summon from the grid,
/// GameManager.NPCs, the summon lists and the initiative order only after the 0.45 s despawn tween, so with any other
/// pacing the removal landed at another point of the turn sequence. Checks:
/// - TurnService: a creature removed during its own turn (slain by an AoO on its own move, by a reaction to its own
///   attack, or in its end-of-turn check) leaves the index on its slot, now the next creature's, and marks the turn
///   vacated (TurnService.VacatedTurnOwner, IsCurrentTurnOf), so the next creature still takes its turn (in the middle,
///   at the first and at the last slot), also when a second creature leaves in the same turn, in either order; a
///   creature removed at its own count (a tick in OnInitiativeCountReached) takes no turn and the next one acts; a
///   removal at the round boundary (the expired summon of TickSummonDurations) and one of another creature keep their
///   old index rules; a creature inserted during a vacated turn at the next creature's index acts next;
/// - through the scene GameManager (Play mode): HandleSummonDeathCleanup takes the summon out of the grid, NPCs and the
///   parallel AI behaviour list together (AI-015), the summon lists, the active summons and the initiative order, and
///   logs its despawn line, all before the call returns (so before any later turn-start line); the removed creature
///   cannot come back onto the grid in that frame (CharacterController.IsRemovedFromPlay); its mirror view
///   (TokenView) lingers outside the live views, carries no controller and plays the summon visual's despawn fade;
///   a source scan that DespawnSummonWithEffect yields nothing before its rules work;
/// - a source scan that every PC tail ends a vacated player-run turn by reference (GameManager.IsVacatedPlayerTurn:
///   ShowActionChoices, EndCurrentTurn, AfterAttackDelay, DelayedEndActivePCTurn; IsCurrentTurnOf in the grapple moves)
///   and that the harness's summon removal marks the summon out of play;
/// - the Lesser Globe centre (PHB p.236: centred on the caster; that it follows the caster is current behaviour,
///   owner question SPL-335) comes from the caster's grid
///   square (LesserGlobeOfInvulnerabilityAreaEffect.CasterCenter), not from its token's transform, at creation and in
///   Update and OnRoundStart (a source scan).
/// Needs Play mode (CharacterController.Init needs Awake, TST-009; the despawn checks use the scene GameManager).
/// </summary>
public static class SummonDespawnTimingTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("========== SUMMON DESPAWN TIMING TESTS (CRE-312, grouped AI turns step 4) ==========");

        if (!Application.isPlaying)
        {
            Assert(false, "Summon despawn timing tests need Play mode (CharacterController.Init needs Awake)");
        }
        else
        {
            TestHelpers.EnsureCoreDatabasesInitialized();
            Run(TestCurrentTurnRemovalLetsNextAct);
            Run(TestOtherRemovalsKeepTheirIndex);
            Run(TestTwoRemovalsInOneTurn);
            Run(TestRemovalAtCountReached);
            Run(TestInsertDuringVacatedTurn);
            if (GameManager.Instance != null && GameManager.Instance.Grid != null)
            {
                Run(TestDespawnIsSynchronous);
                Run(TestLesserGlobeCentreFromGrid);
            }
            else
            {
                Assert(false, "The despawn and globe checks need the scene GameManager and grid");
            }
            Run(TestDespawnSourceHasNoEarlyYield);
            Run(TestGlobeSourcesReadTheGrid);
            Run(TestForcedTurnTailsAskTheTurnOwner);
            Run(TestVacatedPlayerTurnTailsEndTheTurn);
        }

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    // ── TurnService ─────────────────────────────────────────────────

    private static int _nextX;

    private static CharacterController Make(string name)
    {
        _nextX += 2;
        return TestHelpers.CreateCharacter(name: name, gridPosition: new Vector2Int(-40 - (_nextX % 18), -40));
    }

    private static TurnService MakeTurnService(out GameObject host)
    {
        host = new GameObject("SummonDespawnTest_TurnService");
        return host.AddComponent<TurnService>();
    }

    /// <summary>
    /// Plays <paramref name="order"/> (forced initiative) through a TurnService, ends <paramref name="endTurns"/> turns,
    /// removes <paramref name="removed"/> in that order during the current creature's turn and ends
    /// <paramref name="endsAfter"/> more turns; returns the turn log "round:name" of every turn start. Reports the index,
    /// the vacated turn's owner, whether the turn is still the first removed creature's, and the next creature, right
    /// after the removals.
    /// </summary>
    private static List<string> PlayRemovals(List<CharacterController> order, int endTurns, CharacterController[] removed, int endsAfter,
        out int indexAfterRemoval, out CharacterController vacatedOwner, out bool stillFirstsTurn, out CharacterController nextAfterRemoval)
    {
        var log = new List<string>();
        TurnService turns = MakeTurnService(out GameObject host);
        indexAfterRemoval = int.MinValue;
        vacatedOwner = null;
        stillFirstsTurn = false;
        nextAfterRemoval = null;
        try
        {
            turns.OnTurnStarted += c => log.Add(turns.CurrentRound + ":" + c.Stats.CharacterName);
            turns.StartCombat(order, null, _ => true, order);
            for (int i = 0; i < endTurns; i++)
                turns.EndTurn();
            foreach (CharacterController r in removed)
                turns.RemoveFromInitiative(r);
            indexAfterRemoval = turns.CurrentInitiativeIndex;
            vacatedOwner = turns.VacatedTurnOwner;
            stillFirstsTurn = removed.Length > 0 && turns.IsCurrentTurnOf(removed[0]);
            nextAfterRemoval = turns.GetNextCharacter();
            for (int i = 0; i < endsAfter; i++)
                turns.EndTurn();
            turns.EndCombat();
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
        return log;
    }

    private static string Name(CharacterController c) => c != null && c.Stats != null ? c.Stats.CharacterName : "none";

    private static void TestCurrentTurnRemovalLetsNextAct()
    {
        CharacterController a = Make("Order A"), s = Make("Order Summon"), b = Make("Order B"), c = Make("Order C");
        try
        {
            // The summon is slain during its own turn (round 1, slot 1): B, which now fills slot 1, still acts.
            List<string> log = PlayRemovals(new List<CharacterController> { a, s, b, c }, 1, new[] { s }, 2, out int idx, out CharacterController owner, out bool turnOf, out CharacterController next);
            string got = string.Join(" ", log);
            Assert(idx == 1 && owner == s && turnOf && next == b,
                "A creature removed during its own turn leaves the index on its slot, now the next creature's, and the turn stays its own until it ends (CRE-312)",
                "idx " + idx + ", owner " + Name(owner) + ", its turn " + turnOf + ", next " + Name(next));
            Assert(got == "1:Order A 1:Order Summon 1:Order B 1:Order C",
                "The next creature still takes its turn in the same round (CRE-312)", got);

            // At slot 0: the creature that fills the slot acts next, in the same round.
            log = PlayRemovals(new List<CharacterController> { s, a, b }, 0, new[] { s }, 2, out idx, out owner, out turnOf, out next);
            got = string.Join(" ", log);
            Assert(idx == 0 && owner == s && turnOf && next == a, "Removed during its own turn at slot 0, the index stays at 0",
                "idx " + idx + ", owner " + Name(owner) + ", its turn " + turnOf + ", next " + Name(next));
            Assert(got == "1:Order Summon 1:Order A 1:Order B",
                "Removed at slot 0, the creature that fills the slot acts next, in the same round", got);

            // At the last slot: the index is past the end, so the advance wraps to the next round, whose first creature acts.
            log = PlayRemovals(new List<CharacterController> { a, b, s }, 2, new[] { s }, 2, out idx, out owner, out turnOf, out next);
            got = string.Join(" ", log);
            Assert(idx == 2 && owner == s && turnOf && next == a, "Removed during its own turn at the last slot, the index is past the end and the next creature is the first",
                "idx " + idx + ", owner " + Name(owner) + ", its turn " + turnOf + ", next " + Name(next));
            Assert(got == "1:Order A 1:Order B 1:Order Summon 2:Order A 2:Order B",
                "Removed at the last slot, the next round starts with its first creature (none skipped, the round counted)", got);
        }
        finally
        {
            DestroyAll(a, s, b, c);
        }
    }

    private static void TestOtherRemovalsKeepTheirIndex()
    {
        CharacterController a = Make("Keep A"), s = Make("Keep Summon"), b = Make("Keep B"), c = Make("Keep C");
        try
        {
            // Another creature's turn: a summon earlier in the order leaves, the index follows the current creature.
            List<string> log = PlayRemovals(new List<CharacterController> { s, a, b, c }, 2, new[] { s }, 2, out int idx, out CharacterController owner, out _, out _);
            string got = string.Join(" ", log);
            Assert(idx == 1 && owner == null && got == "1:Keep Summon 1:Keep A 1:Keep B 1:Keep C 2:Keep A",
                "A creature earlier in the order removed during another's turn: the index follows the current creature, no turn vacated",
                "idx " + idx + ", owner " + Name(owner) + ": " + got);

            // The round boundary (TickSummonDurations in OnNewRound): index 0, the turn's creature another; slot 0 acts.
            var log2 = new List<string>();
            TurnService turns = MakeTurnService(out GameObject host);
            try
            {
                var order = new List<CharacterController> { s, a, b };
                bool removeAtBoundary = false;
                turns.OnTurnStarted += ch => log2.Add(turns.CurrentRound + ":" + ch.Stats.CharacterName);
                turns.OnNewRound += r =>
                {
                    if (removeAtBoundary)
                    {
                        removeAtBoundary = false;
                        turns.RemoveFromInitiative(s);
                        log2.Add("removed@" + turns.CurrentInitiativeIndex + (turns.VacatedTurnOwner != null ? "+vacated" : ""));
                    }
                };
                turns.StartCombat(order, null, _ => true, order);
                turns.EndTurn();
                turns.EndTurn();
                removeAtBoundary = true;
                turns.EndTurn(); // the round wraps: the summon expires at the boundary
                turns.EndTurn();
                turns.EndCombat();
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
            got = string.Join(" ", log2);
            Assert(got == "1:Keep Summon 1:Keep A 1:Keep B removed@0 2:Keep A 2:Keep B",
                "A summon removed at the round boundary (index 0, another creature's turn just ended): the new round starts at slot 0", got);
        }
        finally
        {
            DestroyAll(a, s, b, c);
        }
    }

    /// <summary>
    /// Two creatures leave during one turn, the turn's own creature among them (an AoO slays the moving summon and its
    /// Cleave, PHB p.92, or a readied area slays another): in either order, nobody who has not acted loses a turn.
    /// </summary>
    private static void TestTwoRemovalsInOneTurn()
    {
        CharacterController a = Make("Two A"), s = Make("Two Summon"), b = Make("Two B"), c = Make("Two C");
        try
        {
            // A and S are summons; S acts at slot 1. S is slain, then A (slot 0): B acts next.
            string got = string.Join(" ", PlayRemovals(new List<CharacterController> { a, s, b, c }, 1, new[] { s, a }, 2, out int idx, out _, out _, out CharacterController next));
            Assert(got == "1:Two A 1:Two Summon 1:Two B 1:Two C" && idx == 0 && next == b,
                "The turn's creature removed, then one before it: the next creature still acts", "idx " + idx + ", next " + Name(next) + ": " + got);

            // The other order: A first (slot 0), then S (now at slot 0, the turn's creature): B acts next.
            got = string.Join(" ", PlayRemovals(new List<CharacterController> { a, s, b, c }, 1, new[] { a, s }, 2, out idx, out _, out _, out next));
            Assert(got == "1:Two A 1:Two Summon 1:Two B 1:Two C" && idx == 0 && next == b,
                "One before it removed, then the turn's creature: the next creature still acts", "idx " + idx + ", next " + Name(next) + ": " + got);

            // At slot 0: S then the creature that slid into its slot (A, not yet acted): B acts next, in the same round.
            got = string.Join(" ", PlayRemovals(new List<CharacterController> { s, a, b }, 0, new[] { s, a }, 2, out idx, out _, out _, out next));
            Assert(got == "1:Two Summon 1:Two B 2:Two B" && idx == 0 && next == b,
                "At slot 0, the turn's creature and the one that slid into its slot removed: the one after them acts next", "idx " + idx + ", next " + Name(next) + ": " + got);

            // At the last slot: S, then B before it: the round wraps and A acts first.
            got = string.Join(" ", PlayRemovals(new List<CharacterController> { a, b, s }, 2, new[] { s, b }, 2, out idx, out _, out _, out next));
            Assert(got == "1:Two A 1:Two B 1:Two Summon 2:Two A 3:Two A" && idx == 1 && next == a,
                "At the last slot, the turn's creature and one before it removed: the round wraps (counted) and the first creature acts", "idx " + idx + ", next " + Name(next) + ": " + got);
        }
        finally
        {
            DestroyAll(a, s, b, c);
        }
    }

    /// <summary>
    /// A tick at a count (a duration, a poison secondary, concentration; TurnService.OnInitiativeCountReached) takes that
    /// count's own creature out of play before its turn starts: it takes no turn, and the creature after it takes its
    /// turn in the same round (the last slot: the round wraps and is counted).
    /// </summary>
    private static void TestRemovalAtCountReached()
    {
        CharacterController a = Make("Count A"), s = Make("Count Summon"), b = Make("Count B");
        try
        {
            string got = PlayCountRemoval(new List<CharacterController> { a, s, b }, s, 2);
            Assert(got == "1:Count A 1:Count B 2:Count A",
                "A creature removed at its own count takes no turn, and the next creature acts in the same round", got);
            got = PlayCountRemoval(new List<CharacterController> { a, b, s }, s, 3);
            Assert(got == "1:Count A 1:Count B 2:Count A 2:Count B",
                "Removed at its own count at the last slot: the round wraps (counted) and the first creature acts", got);
        }
        finally
        {
            DestroyAll(a, s, b);
        }
    }

    /// <summary>
    /// A creature inserted during a vacated turn (CRE-312) at the index that already holds the next creature comes before
    /// it and acts next (TurnService.AddToInitiative keeps the index); one inserted there during an ordinary turn comes
    /// after the current creature and the index moves past it, as before.
    /// </summary>
    private static void TestInsertDuringVacatedTurn()
    {
        CharacterController a = Make("Insert A"), s = Make("Insert Summon"), b = Make("Insert B"), x = Make("Insert X"), y = Make("Insert Y");
        try
        {
            var log = new List<string>();
            TurnService turns = MakeTurnService(out GameObject host);
            int idxAfterInsert = int.MinValue;
            CharacterController next = null;
            try
            {
                var order = new List<CharacterController> { a, s, b };
                turns.OnTurnStarted += c => log.Add(turns.CurrentRound + ":" + c.Stats.CharacterName);
                turns.StartCombat(order, null, _ => true, order);
                // Distinct totals, so the insertion after A lands right after A (ResolveInsertIndex skips equal totals).
                for (int i = 0; i < turns.InitiativeOrder.Count; i++)
                    turns.InitiativeOrder[i].Total = 30 - 10 * i;
                turns.EndTurn();                     // the summon's turn (slot 1)
                turns.RemoveFromInitiative(s);       // slain on it: B now fills slot 1, the turn vacated
                turns.AddToInitiative(x, false, a);  // inserted after A: slot 1, before B
                idxAfterInsert = turns.CurrentInitiativeIndex;
                next = turns.GetNextCharacter();
                turns.EndTurn();
                turns.EndTurn();
                // An ordinary turn: B's. One inserted after X (slot 2, B's own slot) comes after B, the index moves past it.
                turns.AddToInitiative(y, false, x);
                turns.EndTurn();
                turns.EndCombat();
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
            string got = string.Join(" ", log);
            Assert(idxAfterInsert == 1 && next == x, "Inserted during a vacated turn at the next creature's index: the index stays and the inserted creature acts next",
                "idx " + idxAfterInsert + ", next " + Name(next));
            Assert(got == "1:Insert A 1:Insert Summon 1:Insert X 1:Insert B 2:Insert A",
                "Nobody loses a turn: the inserted creature, then the next one, in the same round; one inserted at the current creature's index in an ordinary turn waits for the next round", got);
        }
        finally
        {
            DestroyAll(a, s, b, x, y);
        }
    }

    /// <summary>
    /// A player-run creature that leaves play during its own turn (CRE-312) leaves CurrentCharacter and so ActivePC null:
    /// every PC tail ends that turn by reference (GameManager.IsVacatedPlayerTurn), never through Unity's
    /// destroyed-object null. ShowActionChoices (the tail of most PC paths: a grapple-escape move, a provoking cast)
    /// ends it; EndCurrentTurn sends it through EndActivePCTurn's cleanup (the dismiss confirm, the forced-turn tails);
    /// AfterAttackDelay and DelayedEndActivePCTurn end it before they read ActivePC; the grapple moves ask
    /// IsCurrentTurnOf; and the harness's summon removal marks the summon out of play as the game's does. The scenario
    /// rules/summon-slain-off-its-move-next-still-acts plays the attack tail and the ShowActionChoices tail.
    /// </summary>
    private static void TestVacatedPlayerTurnTailsEndTheTurn()
    {
        string gm = Regex.Replace(Source("_Core/GameManager.cs"), @"//[^\n]*", "");
        string show = Body(gm, "private void ShowActionChoices()") ?? "";
        int guard = show.IndexOf("if (pc == null)", StringComparison.Ordinal);
        int ends = show.IndexOf("IsVacatedPlayerTurn", StringComparison.Ordinal);
        int endCall = show.IndexOf("EndActivePCTurn()", StringComparison.Ordinal);
        int combatEnd = show.IndexOf("EvaluateCombatEnd(", StringComparison.Ordinal);
        Assert(guard >= 0 && ends > guard && endCall > ends && combatEnd > endCall,
            "ShowActionChoices ends a vacated player turn (IsVacatedPlayerTurn -> EndActivePCTurn) instead of returning with the turn open",
            $"guard {guard}, vacated {ends}, end {endCall}, combat end {combatEnd}");
        string endCurrent = Body(gm, "public void EndCurrentTurn()") ?? "";
        Assert(endCurrent.Contains("if (IsPlayerTurn || IsVacatedPlayerTurn)"),
            "EndCurrentTurn sends a vacated player turn through EndActivePCTurn (highlights, buttons, attack-sequence state)");
        string dismiss = gm.Substring(Math.Max(0, gm.IndexOf("ReferenceEquals(_turnService.VacatedTurnOwner, dismissed)", StringComparison.Ordinal)));
        Assert(dismiss.Length > 0 && Regex.IsMatch(dismiss.Substring(0, Math.Min(400, dismiss.Length)), @"EndCurrentTurn\(\)"),
            "The dismiss confirm ends the dismissed summon's own turn through EndCurrentTurn, so through EndActivePCTurn");

        string actions = Regex.Replace(Source("_Core/GameManager.CombatActions.cs"), @"//[^\n]*", "");
        string after = Body(actions, "private IEnumerator AfterAttackDelay(") ?? "";
        string delayed = Body(actions, "private IEnumerator DelayedEndActivePCTurn(") ?? "";
        int afterVacated = after.IndexOf("IsVacatedPlayerTurn", StringComparison.Ordinal);
        int afterAuto = after.IndexOf("ShouldAutoEndTurn(", StringComparison.Ordinal);
        Assert(afterVacated >= 0 && afterAuto > afterVacated && after.Contains("IsCurrentTurnOf(pc)") && after.Contains("pc.IsRemovedFromPlay")
               && !after.Contains("pc == ActivePC"),
            "AfterAttackDelay ends a vacated turn, or an attacker out of play or out of the fight, by reference before ShouldAutoEndTurn");
        int delayedVacated = delayed.IndexOf("IsVacatedPlayerTurn", StringComparison.Ordinal);
        int delayedActive = delayed.IndexOf("CharacterController pc = ActivePC", StringComparison.Ordinal);
        Assert(delayedVacated >= 0 && delayedActive > delayedVacated, "DelayedEndActivePCTurn ends a vacated turn before it reads ActivePC");

        string grapple = Regex.Replace(Source("Combat/Maneuvers/GrappleSystem.cs"), @"//[^\n]*", "");
        int asks = Regex.Matches(grapple, @"IsMoverIncapacitated\(actor\) && IsCurrentTurnOf\(actor\)").Count;
        Assert(asks == 3 && !grapple.Contains("IsMoverIncapacitated(actor) && actor == ActivePC"),
            "The three grapple moves end an AoO-dropped mover's turn on IsCurrentTurnOf, never on actor == ActivePC", "found " + asks);

        string harness = Body(Regex.Replace(Source("_Core/GameManager.ScenarioHarness.cs"), @"//[^\n]*", ""), "internal void Harness_DespawnSummon(") ?? "";
        int mark = harness.IndexOf("MarkRemovedFromPlay()", StringComparison.Ordinal);
        int release = harness.IndexOf("PlayDespawnAndRelease()", StringComparison.Ordinal);
        int remove = harness.IndexOf("Harness_RemoveSpawnedController(", StringComparison.Ordinal);
        Assert(release >= 0 && mark > release && remove > mark,
            "The harness's summon removal releases the view and marks the summon out of play before it removes it, as DespawnSummonWithEffect does");
    }

    private static string PlayCountRemoval(List<CharacterController> order, CharacterController victim, int ends)
    {
        var log = new List<string>();
        TurnService turns = MakeTurnService(out GameObject host);
        try
        {
            bool armed = true;
            turns.OnTurnStarted += c => log.Add(turns.CurrentRound + ":" + c.Stats.CharacterName);
            turns.OnInitiativeCountReached += c =>
            {
                if (armed && ReferenceEquals(c, victim))
                {
                    armed = false;
                    turns.RemoveFromInitiative(victim); // alive: an expiry, not a death, so only the order keeps it from acting
                }
            };
            turns.StartCombat(order, null, _ => true, order);
            for (int i = 0; i < ends; i++)
                turns.EndTurn();
            turns.EndCombat();
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
        return string.Join(" ", log);
    }

    // ── GameManager despawn (Play mode, scene GameManager) ──────────

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void TestDespawnIsSynchronous()
    {
        GameManager gm = GameManager.Instance;
        TurnService turns = gm.GetComponent<TurnService>();
        MethodInfo spawn = typeof(GameManager).GetMethod("SpawnSummonedCreature", Private);
        MethodInfo register = typeof(GameManager).GetMethod("RegisterActiveSummon", Private);
        MethodInfo cleanup = typeof(GameManager).GetMethod("HandleSummonDeathCleanup", Private);
        MethodInfo profiles = typeof(GameManager).GetMethod("DestroyUnusedSummonAIProfiles", Private);
        FieldInfo behaviorsField = typeof(GameManager).GetField("_npcAIBehaviors", Private);
        FieldInfo alliesField = typeof(GameManager).GetField("_summonedAllies", Private);
        FieldInfo enemiesField = typeof(GameManager).GetField("_summonedEnemies", Private);
        Assert(turns != null && spawn != null && register != null && cleanup != null && behaviorsField != null && alliesField != null && enemiesField != null,
            "The GameManager members the despawn check reflects on exist (TST-006)");
        if (turns == null || spawn == null || register == null || cleanup == null || behaviorsField == null || alliesField == null || enemiesField == null)
            return;
        if (turns.IsCombatActive)
        {
            Assert(false, "The despawn check needs a scene with no fight running");
            return;
        }

        // A free square of the scene grid, so the occupancy is real.
        SquareCell cell = null;
        for (int y = gm.Grid.Height - 1; y >= 0 && cell == null; y--)
            for (int x = gm.Grid.Width - 1; x >= 0 && cell == null; x--)
            {
                SquareCell c = gm.Grid.GetCell(new Vector2Int(x, y));
                if (c != null && !c.IsOccupied)
                    cell = c;
            }
        Assert(cell != null, "The scene grid has a free square for the summon");
        if (cell == null)
            return;

        CharacterController caster = Make("Despawn Caster");
        caster.ConfigureTeamControl(CharacterTeam.Player, false);
        var option = new SummonMonsterOption { DisplayName = "Dog", NpcDefinitionId = "dog", TemplateId = "celestial", SummonedCreatureAlignment = Alignment.LawfulGood };
        CharacterController summon = null;
        TokenView view = null;
        var lines = new List<string>();
        bool returned = false;
        // The static runner captures Debug.Log by swapping Debug.unityLogger.logHandler, so Application.logMessageReceived
        // never sees a suite's Debug.Log line there: wrap the current handler instead (and forward every line to it).
        ILogHandler previousHandler = Debug.unityLogger.logHandler;
        var capture = new DespawnLineCapture(previousHandler, line => lines.Add((returned ? "after:" : "inside:") + line));
        // Outside an encounter the enemy pool slots in NPCs have no AI behaviour entries (SetupEnemyEncounter adds them),
        // so the two lists are index-parallel only in a fight (AI-015). Pad the list as an encounter would, restore it after.
        var behaviors = (IList)behaviorsField.GetValue(gm);
        var savedBehaviors = new List<object>();
        foreach (object b in behaviors)
            savedBehaviors.Add(b);
        while (behaviors.Count < gm.NPCs.Count)
            behaviors.Add(NPCAIBehavior.AggressiveMelee);
        try
        {
            summon = (CharacterController)spawn.Invoke(gm, new object[] { caster, cell.Coords, option, false });
            Assert(summon != null, "The game's summon spawn brings the celestial dog");
            if (summon == null)
                return;
            register.Invoke(gm, new object[] { summon, caster, null, 5, false, false });
            turns.AddToInitiative(summon, false);
            view = summon.View;

            var allies = (IEnumerable<CharacterController>)alliesField.GetValue(gm); // a HashSet in GameManager
            var enemies = (IEnumerable<CharacterController>)enemiesField.GetValue(gm);
            int npcsBefore = gm.NPCs.Count, behaviorsBefore = behaviors.Count;
            Assert(Contains(gm.NPCs, summon) && Contains(allies, summon) && gm.Harness_IsActiveSummon(summon)
                   && turns.GetInitiative(summon) != null && cell.ContainsOccupant(summon) && view != null && view.SummonVisual != null,
                "Before: the summon is in NPCs, the allied summons, the active summons, the initiative order and its square, with its summon visual on its view");

            Debug.unityLogger.logHandler = capture;
            summon.Stats.CurrentHP = -10;
            cleanup.Invoke(gm, new object[] { summon }); // GameManager.HandleSummonDeathCleanup -> DespawnSummonWithEffect
            returned = true;
            Debug.unityLogger.logHandler = previousHandler;

            Assert(!Contains(gm.NPCs, summon) && gm.NPCs.Count == npcsBefore - 1, "At once: the summon left NPCs");
            Assert(behaviors.Count == behaviorsBefore - 1 && behaviors.Count == gm.NPCs.Count - (npcsBefore - behaviorsBefore),
                "At once: its AI behaviour entry left with it, so the two lists stay index-parallel (AI-015)");
            Assert(!Contains(allies, summon) && !Contains(enemies, summon), "At once: it left the summon lists");
            Assert(!gm.Harness_IsActiveSummon(summon), "At once: it left the active summons");
            Assert(turns.GetInitiative(summon) == null, "At once: it left the initiative order");
            Assert(!cell.ContainsOccupant(summon), "At once: its square is free");
            Assert(lines.Count == 1 && lines[0].StartsWith("inside:", StringComparison.Ordinal),
                "Its despawn line is logged before the call returns, so before any later turn-start line", string.Join(" | ", lines));
            Assert(summon.IsRemovedFromPlay, "The creature is marked out of play until Unity destroys it at the end of the frame");
            Assert(!CombatEndRules.IsActiveCombatant(summon),
                "The combatant check skips it in that frame (so do the scene-wide area scans, which read IsRemovedFromPlay)");
            gm.Grid.SetCreatureOccupancy(summon, cell.Coords, 1);
            Assert(!cell.ContainsOccupant(summon), "A movement unwinding in the same frame cannot put it back on the grid (SquareCell.AddOccupant)");

            Assert(view != null && view.IsDespawning && view.IsReleased, "Its mirror view lingers, released from its model, to play the despawn");
            Assert(view != null && !ContainsView(view), "The lingering view is no live view (the clean check and the tests never wait for it)");
            Assert(view != null && view.Model == null, "The lingering view mirrors nothing any more");
            Assert(view != null && view.GetComponentInChildren<CharacterController>(true) == null,
                "The lingering view carries no controller, so area tracking (FindObjectsOfType<CharacterController>) and IsActiveCombatant never see it");
            Assert(view != null && view.SummonVisual != null && view.gameObject != null, "The summon visual stays on the view and plays its despawn fade");
        }
        finally
        {
            Debug.unityLogger.logHandler = previousHandler;
            if (summon != null && gm.NPCs.Contains(summon))
                gm.NPCs.Remove(summon);
            behaviors.Clear();
            foreach (object b in savedBehaviors)
                behaviors.Add(b);
            if (view != null)
                Object.DestroyImmediate(view.gameObject);
            if (summon != null)
            {
                turns.RemoveFromInitiative(summon);
                Object.DestroyImmediate(summon.gameObject);
            }
            DestroyAll(caster);
            profiles?.Invoke(gm, null);
        }
    }

    private static void TestLesserGlobeCentreFromGrid()
    {
        GameManager gm = GameManager.Instance;
        CharacterController caster = TestHelpers.CreateCharacter(name: "Globe Caster", gridPosition: new Vector2Int(4, 5));
        try
        {
            Vector3 square = gm.Grid.GetCenteredWorldPosition(caster.GridPosition, caster.GetVisualSquaresOccupied());
            caster.transform.position = square + new Vector3(0.6f, 0.2f, 0f); // the token mid-way through a step tween
            Vector3 centre = LesserGlobeOfInvulnerabilityAreaEffect.CasterCenter(caster);
            Assert((centre - square).sqrMagnitude < 1e-8f, "The globe centre is the caster's grid square, not its token mid-tween (PHB p.236)",
                centre + " vs " + square);
            caster.Stats.CurrentSizeCategory = SizeCategory.Large;
            Vector3 large = gm.Grid.GetCenteredWorldPosition(caster.GridPosition, caster.GetVisualSquaresOccupied());
            Assert(caster.GetVisualSquaresOccupied() == 2 && (LesserGlobeOfInvulnerabilityAreaEffect.CasterCenter(caster) - large).sqrMagnitude < 1e-8f,
                "For a Large caster the centre is the middle of its 2x2 space");
        }
        finally
        {
            DestroyAll(caster);
        }
    }

    // ── Source scans ────────────────────────────────────────────────

    private static string Source(string relative) => File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", relative));

    private static string Body(string src, string signature)
    {
        int at = src.IndexOf(signature, StringComparison.Ordinal);
        if (at < 0)
            return null;
        int start = src.IndexOf('{', at), depth = 0;
        for (int i = start; i >= 0 && i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}' && --depth == 0)
                return src.Substring(start, i - start + 1);
        }
        return null;
    }

    private static void TestDespawnSourceHasNoEarlyYield()
    {
        string body = Body(Source("Spell/Resolution/GameManager.SpellCasting.cs"), "private IEnumerator DespawnSummonWithEffect(");
        Assert(body != null, "GameManager.DespawnSummonWithEffect keeps its name and coroutine shape (TST-006)");
        if (body == null)
            return;
        string code = Regex.Replace(body, @"//[^\n]*", "");
        Assert(!Regex.IsMatch(code, @"\byield\s+return\b"), "DespawnSummonWithEffect yields nothing: every rules change runs at its StartCoroutine call");
        int view = code.IndexOf("PlayDespawnAndRelease()", StringComparison.Ordinal);
        int npcs = code.IndexOf("NPCs.RemoveAt(", StringComparison.Ordinal);
        int behaviors = code.IndexOf("_npcAIBehaviors.RemoveAt(", StringComparison.Ordinal);
        int initiative = code.IndexOf("RemoveFromInitiative(", StringComparison.Ordinal);
        int destroy = code.IndexOf("Destroy(cc.gameObject)", StringComparison.Ordinal);
        Assert(view >= 0 && npcs > view && behaviors > npcs && initiative > behaviors && destroy > initiative,
            "It detaches the view first, removes NPCs and the AI behaviour together, then the initiative entry, and destroys the model last",
            $"view {view}, NPCs {npcs}, behaviours {behaviors}, initiative {initiative}, destroy {destroy}");
    }

    private static void TestGlobeSourcesReadTheGrid()
    {
        string globe = Regex.Replace(Source("Spell/AreaEffects/LesserGlobeOfInvulnerabilityAreaEffect.cs"), @"//[^\n]*", "");
        Assert(!globe.Contains("Caster.transform"), "LesserGlobeOfInvulnerabilityAreaEffect never reads its caster's transform");
        string update = Body(globe, "private void Update()") ?? "";
        string roundStart = Body(globe, "public override void OnRoundStart()") ?? "";
        Assert(update.Contains("CasterCenter(Caster)") && roundStart.Contains("CasterCenter(Caster)"),
            "Update and OnRoundStart take the centre from CasterCenter (the caster's grid square)");
        string spellsL = Body(Source("Spell/Resolution/GameManager_Spells_L.cs"), "TryResolveLesserGlobeSpellEffect(") ?? "";
        string phase1 = Regex.Replace(Source("Spell/Resolution/GameManager_Spells_Phase1.cs"), @"//[^\n]*", "");
        Assert(spellsL.Contains("LesserGlobeOfInvulnerabilityAreaEffect.CasterCenter(caster)") && !spellsL.Contains("caster.transform.position"),
            "Lesser Globe's creation (GameManager_Spells_L) centres on the caster's grid square");
        Assert(phase1.Contains("globe.CenterPosition = LesserGlobeOfInvulnerabilityAreaEffect.CasterCenter(caster)") && !phase1.Contains("globe.CenterPosition = caster.transform.position"),
            "Globe of Invulnerability's creation (GameManager_Spells_Phase1) centres on the caster's grid square");
    }

    /// <summary>
    /// A player-run summon's forced turn (fleeing, confused, suggested) that ends with the summon slain on its own move
    /// still ends the turn: each tail asks GameManager.IsCurrentTurnOf (the turn's owner kept by TurnService after the
    /// removal), never Unity's null-or-destroyed equality on CurrentCharacter, which answers differently in the frame of
    /// the removal and after it (CRE-312).
    /// </summary>
    private static void TestForcedTurnTailsAskTheTurnOwner()
    {
        var tails = new[]
        {
            ("_Core/GameManager.TurnStart.cs", "private IEnumerator ExecuteFleeingForcedPCTurn(", 2),
            ("_Core/GameManager.cs", "private IEnumerator ExecuteConfusedForcedPCTurn(", 1),
            ("Combat/Special/BardicMusicSystem.cs", "private IEnumerator ExecuteSuggestedForcedPCTurn(", 1),
        };
        foreach ((string file, string signature, int ends) in tails)
        {
            string body = Body(Source(file), signature);
            string code = body == null ? "" : Regex.Replace(body, @"//[^\n]*", "");
            int asks = Regex.Matches(code, @"IsCurrentTurnOf\(pc\)\)\s*EndCurrentTurn\(\)").Count;
            Assert(body != null && asks == ends && !code.Contains("CurrentCharacter == pc"),
                $"{signature.Substring(20).TrimEnd('(')} ends the turn on IsCurrentTurnOf(pc) ({ends}x), never on CurrentCharacter == pc", "found " + asks);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static bool Contains(IEnumerable<CharacterController> list, CharacterController c)
    {
        if (list == null)
            return false;
        foreach (CharacterController x in list)
            if (ReferenceEquals(x, c))
                return true;
        return false;
    }

    /// <summary>Forwards every log line to the handler it wraps and reports the [Summon] Despawned lines.</summary>
    private sealed class DespawnLineCapture : ILogHandler
    {
        private readonly ILogHandler _inner;
        private readonly Action<string> _onDespawnLine;

        public DespawnLineCapture(ILogHandler inner, Action<string> onDespawnLine)
        {
            _inner = inner;
            _onDespawnLine = onDespawnLine;
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            string line = args == null || args.Length == 0 ? format : string.Format(format, args);
            if (line != null && line.StartsWith("[Summon] Despawned", StringComparison.Ordinal))
                _onDespawnLine(line);
            _inner?.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, Object context)
        {
            _inner?.LogException(exception, context);
        }
    }

    private static bool ContainsView(TokenView view)
    {
        foreach (TokenView v in TokenView.Live)
            if (ReferenceEquals(v, view))
                return true;
        return false;
    }

    private static void DestroyAll(params CharacterController[] controllers)
    {
        GameManager gm = GameManager.Instance;
        foreach (CharacterController c in controllers)
        {
            if (c == null)
                continue;
            if (gm != null && gm.Grid != null)
                gm.Grid.ClearCreatureOccupancy(c);
            Object.DestroyImmediate(c.gameObject);
        }
    }

    private static void Run(Action test)
    {
        try
        {
            test();
        }
        catch (Exception ex)
        {
            Exception inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
            Assert(false, $"{test.Method.Name} threw {inner.GetType().Name}: {inner.Message}");
        }
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
