using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using Tests.Utilities;
using Object = UnityEngine.Object;

namespace Tests.Combat
{
/// <summary>
/// CMB-007 (grouped AI turns step 2, docs/designs/grouped_ai_turns.md): a poison's secondary save comes 1 minute after
/// exposure (DMG p.296-297), 10 rounds (PHB p.138), counted at the initiative count of the creature whose turn it was at
/// exposure, like every duration (TurnDurations, CMB-006), and never in real time (PoisonTiming).
///
/// Checks: the delay is 10 rounds for every delivery type, injury, contact, inhaled and ingested alike (DMG p.296-297;
/// owner ruling 2026-11-24, CMB-307, Q6); played through TurnService as GameManager wires it, the secondary rolls at the exposure turn's count
/// 10 rounds later, before that creature acts, and not at the victim's count; an anchor that leaves the order hands the
/// poison to the same heir as its durations, and an unanchored poison ticks at the round boundary only; nothing reads
/// frames or real time (no time field, no per-frame loop in GameManager.Update, ticks only at counts); a secondary still
/// pending at combat end resolves then, in the order given (initiative order), a dead victim making no save, and the three
/// combat-end routes call the helper after CombatOver and before the loot window or the defeat screen, the victory routes
/// re-checking the party side afterwards; the secondary save uses the DC figured at exposure (DMG p.296, CHR-020); the
/// initiative order saved at combat end holds only the fight that just ended.
/// Run with PoisonTimingTests.RunAll().
/// </summary>
public static class PoisonTimingTests
{
    private static int _passed;
    private static int _failed;

    private const string InjuryPoison = "medium_spider_poison"; // MM p.289, DMG p.296: injury, Fort DC 12, 1d3 Str / 1d3 Str
    private const string IngestedPoison = "arsenic";            // DMG p.297: ingested

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== POISON TIMING TESTS (CMB-007) ======");
        TestHelpers.EnsureCoreDatabasesInitialized();
        PoisonDatabase.Initialize();

        CharacterController savedAnchor = TurnDurations.CurrentAnchor;
        try
        {
            TestDelayIsTenRounds();
            TestSecondaryAtExposureCountTenRoundsLater();
            TestRemovedAnchorHandsPoisonToHeir();
            TestUnanchoredPoisonTicksAtRoundBoundary();
            TestFramesAndMenusChangeNothing();
            TestCombatEndResolvesPendingInOrder();
            TestCombatEndRoutesCallTheHelper();
            TestSecondaryUsesTheExposureDc();
            TestSavedOrderHoldsOnlyTheLastFight();
        }
        finally
        {
            TurnDurations.SetCurrentAnchor(savedAnchor);
        }

        Debug.Log($"====== Poison Timing Results: {_passed} passed, {_failed} failed ======");
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

    private static int _nextX;

    private static CharacterController Make(string name)
    {
        _nextX += 2;
        return TestHelpers.CreateCharacter(name: name, gridPosition: new Vector2Int(_nextX % 18, 2 + (_nextX / 18) * 2));
    }

    private static void Destroy(params Object[] objects)
    {
        foreach (Object o in objects)
        {
            if (o == null) continue;
            if (o is Component c) Object.DestroyImmediate(c.gameObject);
            else Object.DestroyImmediate(o);
        }
    }

    private static TurnService MakeTurnService(out GameObject host)
    {
        host = new GameObject("PoisonTimingTest_TurnService");
        return host.AddComponent<TurnService>();
    }

    /// <summary>Exposes <paramref name="victim"/> to <paramref name="poisonId"/> with <paramref name="anchor"/>'s count current (null: no count).</summary>
    private static ActivePoison Expose(CharacterController victim, string poisonId, CharacterController anchor)
    {
        victim.ActivePoisons.Clear();
        using (anchor != null ? TurnDurations.Anchor(anchor) : TurnDurations.AtRoundBoundary())
            victim.ApplyPoison(poisonId);
        return victim.ActivePoisons.Count > 0 ? victim.ActivePoisons[victim.ActivePoisons.Count - 1] : null;
    }

    /// <summary>Wires <paramref name="turns"/> to PoisonTiming as GameManager does (count reached, removal, round boundary).</summary>
    private static void Wire(TurnService turns, List<CharacterController> all)
    {
        turns.OnInitiativeCountReached += count => PoisonTiming.TickAtCount(all, count);
        turns.OnInitiativeCountRemoved += (gone, heir) => PoisonTiming.Reanchor(all, gone, heir);
        turns.OnNewRound += _ => PoisonTiming.TickAtRoundBoundary(all, a => TurnDurations.TicksAtRoundBoundary(a, ch => turns.GetInitiative(ch) != null));
    }

    // ── The delay ───────────────────────────────────────────────────

    private static void TestDelayIsTenRounds()
    {
        PoisonData injury = PoisonDatabase.GetPoison(InjuryPoison);
        PoisonData ingested = PoisonDatabase.GetPoison(IngestedPoison);
        Assert(injury != null && injury.Type == PoisonType.Injury && injury.GetSecondaryDamageDelayRounds() == 10,
            "An injury poison's secondary comes 10 rounds (1 minute) after exposure (DMG p.296)",
            injury == null ? "(no poison)" : "(rounds=" + injury.GetSecondaryDamageDelayRounds() + ")");
        Assert(new PoisonData { Type = PoisonType.Contact }.GetSecondaryDamageDelayRounds() == 10
               && new PoisonData { Type = PoisonType.Inhaled }.GetSecondaryDamageDelayRounds() == 10,
            "Contact and inhaled poisons also wait 10 rounds (DMG p.296)");
        Assert(ingested != null && ingested.Type == PoisonType.Ingested && ingested.GetSecondaryDamageDelayRounds() == 10,
            "An ingested poison's secondary also comes 10 rounds (1 minute) after exposure (DMG p.296-297; owner ruling 2026-11-24, CMB-307)",
            ingested == null ? "(no poison)" : "(rounds=" + ingested.GetSecondaryDamageDelayRounds() + ")");
        Assert(new PoisonData { Type = PoisonType.Ingested }.GetSecondaryDamageDelayRounds() == PoisonTiming.SecondaryDelayRounds
               && typeof(PoisonTiming).GetField("IngestedSecondaryDelayRounds", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) == null,
            "No delivery type has its own delay: the ingested constant is gone (CMB-307)");

        CharacterController victim = Make("Delay Victim");
        try
        {
            ActivePoison p = Expose(victim, InjuryPoison, null);
            Assert(p != null && p.RoundsUntilSecondary == 10 && p.DurationAnchor == null && !p.SecondaryResolved,
                "A new exposure counts 10 rounds from the current count (none here)",
                p == null ? "(no poison)" : "(rounds=" + p.RoundsUntilSecondary + ")");
            Assert(p != null && p.GetStatusSummary().Contains("secondary in 10 rounds"),
                "The status summary shows rounds, not seconds", p == null ? "" : p.GetStatusSummary());
            if (p != null)
            {
                p.RoundsUntilSecondary = 1;
                Assert(p.GetStatusSummary().Contains("secondary in 1 round)"), "One round left reads in the singular", p.GetStatusSummary());
            }
            ActivePoison swallowed = Expose(victim, IngestedPoison, null);
            Assert(swallowed != null && swallowed.RoundsUntilSecondary == 10 && !swallowed.SecondaryResolved,
                "An ingested exposure (arsenic) also counts 10 rounds (DMG p.296-297, CMB-307)",
                swallowed == null ? "(no poison)" : "(rounds=" + swallowed.RoundsUntilSecondary + ")");
        }
        finally
        {
            Destroy(victim);
        }
    }

    // ── The count ───────────────────────────────────────────────────

    private static void TestSecondaryAtExposureCountTenRoundsLater()
    {
        CharacterController victim = Make("Count Victim"), middle = Make("Count Middle"), spider = Make("Count Spider");
        TurnService turns = MakeTurnService(out GameObject host);
        var all = new List<CharacterController> { victim, middle, spider };
        var log = new List<string>();
        ActivePoison poison = null;
        try
        {
            Wire(turns, all);
            turns.OnTurnStarted += ch =>
            {
                if (turns.CurrentRound == 1 && ch == spider)
                    poison = Expose(victim, InjuryPoison, TurnDurations.CurrentAnchor); // the spider bites on its own turn
                if (poison != null && (turns.CurrentRound == 10 || turns.CurrentRound == 11))
                    log.Add(turns.CurrentRound + ":" + ch.Stats.CharacterName.Replace("Count ", "") + ":"
                            + (poison.SecondaryResolved ? "resolved" : poison.RoundsUntilSecondary.ToString()));
            };

            turns.StartCombat(all, null, _ => true, all);
            int guard = 0;
            while (turns.IsCombatActive && turns.CurrentRound <= 11 && guard++ < 100)
                turns.EndTurn();
            turns.EndCombat();

            Assert(poison != null && poison.DurationAnchor == spider,
                "The poison takes the exposure turn's count (the spider's) as its anchor, not the victim's (CMB-006)");
            string got = string.Join(" ", log);
            string want = "10:Victim:2 10:Middle:2 10:Spider:1 11:Victim:1 11:Middle:1 11:Spider:resolved";
            Assert(got == want,
                "The secondary rolls at the spider's count 10 rounds after exposure, before the spider acts, and not earlier (DMG p.296, PHB p.138)",
                "got: " + got);
            Assert(victim.ActivePoisons.Count == 0, "A resolved secondary leaves the victim's poison list",
                "(count=" + victim.ActivePoisons.Count + ")");
        }
        finally
        {
            TurnDurations.ClearCurrentAnchor();
            Destroy(host);
            Destroy(victim, middle, spider);
        }
    }

    private static void TestRemovedAnchorHandsPoisonToHeir()
    {
        CharacterController a = Make("Heir A"), summon = Make("Heir Summon"), b = Make("Heir B"), victim = Make("Heir Victim");
        TurnService turns = MakeTurnService(out GameObject host);
        var order = new List<CharacterController> { a, summon, b };
        var all = new List<CharacterController> { a, summon, b, victim };
        try
        {
            Wire(turns, all);
            turns.StartCombat(order, null, _ => true, order);
            // Round 1, the summon's turn: the summon poisons the victim, then is slain during A's turn of round 2,
            // before its count came up again: its durations, and so its poison, pass to the next count (B's).
            turns.EndTurn();
            ActivePoison p = Expose(victim, InjuryPoison, TurnDurations.CurrentAnchor);
            Assert(p != null && p.DurationAnchor == summon, "The poison is anchored to the summon's count");
            turns.EndTurn(); // B
            turns.EndTurn(); // round 2, A
            int before = p != null ? p.RoundsUntilSecondary : -1;
            turns.RemoveFromInitiative(summon);
            Assert(p != null && p.DurationAnchor == b && p.RoundsUntilSecondary == before,
                "A removed anchor hands its pending poison to the same heir as its durations (B, the next count), rounds unchanged (CMB-006)",
                p == null ? "" : "(anchor=" + (p.DurationAnchor != null ? p.DurationAnchor.Stats.CharacterName : "none") + " rounds=" + p.RoundsUntilSecondary + ")");
            turns.EndTurn(); // round 2, B: the poison loses the round the summon's count would have taken
            Assert(p != null && p.RoundsUntilSecondary == before - 1, "The heir's count ticks it once this round",
                p == null ? "" : "(rounds=" + p.RoundsUntilSecondary + ", before " + before + ")");

            CharacterController other = Make("Heir Other");
            try
            {
                Assert(PoisonTiming.Reanchor(new List<CharacterController> { victim }, other, a) == 0 && p.DurationAnchor == b,
                    "A creature that anchors nothing moves nothing");
            }
            finally
            {
                Destroy(other);
            }
            turns.EndCombat();
        }
        finally
        {
            TurnDurations.ClearCurrentAnchor();
            Destroy(host);
            Destroy(a, summon, b, victim);
        }
    }

    private static void TestUnanchoredPoisonTicksAtRoundBoundary()
    {
        CharacterController a = Make("Boundary A"), victim = Make("Boundary Victim");
        var victims = new List<CharacterController> { victim };
        try
        {
            ActivePoison p = Expose(victim, InjuryPoison, null);
            TurnDurations.ClearCurrentAnchor();
            ActivePoison loose = p;
            PoisonTiming.TickAtCount(victims, a);
            PoisonTiming.TickAtCount(victims, victim);
            Assert(loose != null && loose.RoundsUntilSecondary == 10, "An unanchored poison does not tick at any creature's count");
            Func<CharacterController, bool> boundary = anchor => TurnDurations.TicksAtRoundBoundary(anchor, ch => ch == a);
            PoisonTiming.TickAtRoundBoundary(victims, boundary);
            Assert(loose != null && loose.RoundsUntilSecondary == 9, "It ticks at the round boundary (CMB-006)",
                loose == null ? "" : "(rounds=" + loose.RoundsUntilSecondary + ")");

            ActivePoison anchored = Expose(victim, InjuryPoison, a);
            PoisonTiming.TickAtRoundBoundary(victims, boundary);
            Assert(anchored != null && anchored.RoundsUntilSecondary == 10,
                "A poison anchored to a creature in the order does not tick at the round boundary");
            PoisonTiming.TickAtRoundBoundary(victims, anchor => TurnDurations.TicksAtRoundBoundary(anchor, ch => false));
            Assert(anchored != null && anchored.RoundsUntilSecondary == 9,
                "One whose anchor is no longer in the order ticks there instead");
        }
        finally
        {
            Destroy(a, victim);
        }
    }

    // ── No real time ────────────────────────────────────────────────

    private static void TestFramesAndMenusChangeNothing()
    {
        bool timeField = false;
        foreach (FieldInfo f in typeof(ActivePoison).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (f.FieldType == typeof(float) || f.FieldType == typeof(double))
                timeField = true;
        Assert(!timeField, "ActivePoison keeps no time in seconds (no float field)");
        Assert(typeof(GameManager).GetMethod("UpdatePoisonTimers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null,
            "GameManager has no per-frame poison timer any more (UpdatePoisonTimers removed)");
        Assert(typeof(PoisonData).GetMethod("GetSecondaryDamageDelaySeconds") == null, "The delay has no seconds getter any more");

        string gm = Path.Combine(Application.dataPath, "Scripts/_Core/GameManager.cs");
        string update = MethodBody(StripComments(gm), "private void Update()");
        Assert(update != null && update.IndexOf("Poison", StringComparison.Ordinal) < 0,
            "GameManager.Update, which runs every frame whatever menu is open, touches no poison", update == null ? "(no Update found)" : "");

        string scripts = Path.Combine(Application.dataPath, "Scripts");
        var readers = new List<string>();
        foreach (string file in new[] { "Effects/Poison.cs", "Effects/PoisonTiming.cs" })
        {
            string code = StripComments(Path.Combine(scripts, file));
            if (code.Contains("Time.") || code.Contains("deltaTime") || code.Contains("frameCount"))
                readers.Add(file);
        }
        Assert(readers.Count == 0, "The poison timing reads no Unity time or frame count", string.Join(", ", readers));

        // Extra "frames": any number of calls that are not this poison's count leave it alone.
        CharacterController anchor = Make("Frames Anchor"), victim = Make("Frames Victim");
        try
        {
            ActivePoison p = Expose(victim, InjuryPoison, anchor);
            var victims = new List<CharacterController> { victim };
            for (int i = 0; i < 600; i++)
            {
                PoisonTiming.TickAtCount(victims, victim);
                PoisonTiming.TickAtRoundBoundary(victims, a => TurnDurations.TicksAtRoundBoundary(a, ch => ch == anchor));
            }
            Assert(p != null && p.RoundsUntilSecondary == 10 && !p.SecondaryResolved,
                "600 updates that are not the anchor's count change nothing",
                p == null ? "" : "(rounds=" + p.RoundsUntilSecondary + ")");
        }
        finally
        {
            Destroy(anchor, victim);
        }
    }

    // ── Combat end ──────────────────────────────────────────────────

    private static void TestCombatEndResolvesPendingInOrder()
    {
        CharacterController first = Make("End First"), second = Make("End Second"), dead = Make("End Dead");
        try
        {
            // Exposed in the reverse of the initiative order: the order given decides.
            ActivePoison ps = Expose(second, InjuryPoison, first);
            ActivePoison pf = Expose(first, InjuryPoison, second);
            ActivePoison pd = Expose(dead, InjuryPoison, first);
            dead.Stats.CurrentHP = -10;
            Assert(dead.IsDead, "Setup: the third victim is dead");

            var resolved = new List<CharacterController>();
            int n = PoisonTiming.ResolvePendingAtCombatEnd(new List<CharacterController> { first, second, dead, first }, resolved);
            string got = string.Join(",", resolved.ConvertAll(c => c.Stats.CharacterName));
            Assert(n == 2 && got == "End First,End Second",
                "Pending secondaries resolve at combat end in the initiative order given, each victim once (delegated RAW reading 2026-11-16)",
                "(n=" + n + " order=" + got + ")");
            Assert(pf != null && pf.SecondaryResolved && ps != null && ps.SecondaryResolved && first.ActivePoisons.Count == 0 && second.ActivePoisons.Count == 0,
                "Both secondaries are resolved and leave the lists");
            Assert(pd != null && pd.SecondaryResolved && dead.ActivePoisons.Count == 0,
                "A dead victim makes no save: its poison is dropped");
            Assert(PoisonTiming.ResolvePendingAtCombatEnd(new List<CharacterController> { first, second, dead }) == 0,
                "A second combat-end call finds nothing left");
            Assert(!PoisonTiming.HasPending(first) && !PoisonTiming.HasPending(dead), "Nothing is pending afterwards");
        }
        finally
        {
            Destroy(first, second, dead);
        }
    }

    private static void TestCombatEndRoutesCallTheHelper()
    {
        const string helper = "ResolvePendingPoisonSecondariesAtCombatEnd(";
        string core = Path.Combine(Application.dataPath, "Scripts/_Core");
        string end = StripComments(Path.Combine(core, "GameManager.CombatEnd.cs"));
        string gm = StripComments(Path.Combine(core, "GameManager.cs"));

        CheckOrder(MethodBody(end, "private void HandleCombatVictoryDetected("), "HandleCombatVictoryDetected",
            "CurrentPhase = TurnPhase.CombatOver;", helper, "BeginPostCombatLootCollection();");
        CheckOrder(MethodBody(end, "private void HandleCombatDefeatDetected("), "HandleCombatDefeatDetected",
            "CurrentPhase = TurnPhase.CombatOver;", helper, "ShowDefeatScreen();");
        CheckOrder(MethodBody(gm, "private void OnCombatEnded()"), "OnCombatEnded",
            "CurrentPhase = TurnPhase.CombatOver;", helper, "BeginPostCombatLootCollection();");

        // A combat-end secondary can drop the last PC: both victory routes re-check the party side before the loot window.
        const string recheck = "TurnVictoryIntoDefeatIfPartyOut(";
        CheckOrder(MethodBody(end, "private void HandleCombatVictoryDetected("), "HandleCombatVictoryDetected",
            helper, recheck, "BeginPostCombatLootCollection();");
        string onEnded = MethodBody(gm, "private void OnCombatEnded()");
        int iHelper = onEnded != null ? onEnded.IndexOf(helper, StringComparison.Ordinal) : -1;
        int iRecheck = onEnded != null ? onEnded.IndexOf(recheck, StringComparison.Ordinal) : -1;
        Assert(iHelper >= 0 && iRecheck > iHelper && onEnded.Contains("isVictory = false;"),
            "OnCombatEnded re-checks the party side after the combat-end secondaries and drops the victory when the party is out",
            "(helper=" + iHelper + " recheck=" + iRecheck + ")");
        string turnBody = MethodBody(end, "private bool TurnVictoryIntoDefeatIfPartyOut(");
        Assert(turnBody != null && turnBody.Contains("PlayersOut") && turnBody.Contains("ShowDefeatScreen();"),
            "The re-check reads the party side (CombatEndRules) and opens the defeat screen");
    }

    // ── The DC ──────────────────────────────────────────────────────

    private static void TestSecondaryUsesTheExposureDc()
    {
        PoisonData injury = PoisonDatabase.GetPoison(InjuryPoison);
        CharacterController victim = Make("Dc Victim");
        try
        {
            ActivePoison plain = Expose(victim, InjuryPoison, null);
            Assert(injury != null && plain != null && plain.SaveDC == injury.FortitudeDC,
                "Without a modifier both saves use the poison's DC",
                plain == null || injury == null ? "" : "(dc=" + plain.SaveDC + " poison=" + injury.FortitudeDC + ")");

            victim.ActivePoisons.Clear();
            using (TurnDurations.AtRoundBoundary())
                victim.ApplyPoison(InjuryPoison, 3); // a wild-shaped druid's DC figured for her (WildShapeRules, CHR-020)
            ActivePoison shifted = victim.ActivePoisons.Count > 0 ? victim.ActivePoisons[victim.ActivePoisons.Count - 1] : null;
            Assert(injury != null && shifted != null && shifted.SaveDC == injury.FortitudeDC + 3,
                "The secondary save keeps the DC figured at exposure, modifier included (DMG p.296: one DC for both saves)",
                shifted == null || injury == null ? "" : "(dc=" + shifted.SaveDC + " poison=" + injury.FortitudeDC + ")");
        }
        finally
        {
            Destroy(victim);
        }
    }

    // ── The saved order ─────────────────────────────────────────────

    private static void TestSavedOrderHoldsOnlyTheLastFight()
    {
        CharacterController a = Make("Order A");
        TurnService turns = MakeTurnService(out GameObject host);
        try
        {
            var all = new List<CharacterController> { a };
            turns.StartCombat(all, null, _ => true, all);
            turns.EndCombat();
            Assert(turns.OrderAtLastCombatEnd.Count == 1 && turns.OrderAtLastCombatEnd[0] == a,
                "EndCombat saves the order of the fight that ended", "(count=" + turns.OrderAtLastCombatEnd.Count + ")");

            turns.StartCombat(all, null, _ => true, all);
            Assert(turns.OrderAtLastCombatEnd.Count == 0,
                "A new fight clears the saved order of the last one (reused slots, CRE-046)", "(count=" + turns.OrderAtLastCombatEnd.Count + ")");
            turns.EndCombat();
            turns.ForceResetWithoutCallbacks("PoisonTimingTests");
            Assert(turns.OrderAtLastCombatEnd.Count == 0,
                "A forced reset clears the saved order too", "(count=" + turns.OrderAtLastCombatEnd.Count + ")");
        }
        finally
        {
            Destroy(a);
            Object.DestroyImmediate(host);
        }
    }

    private static void CheckOrder(string body, string method, string phase, string helper, string after)
    {
        int iPhase = body != null ? body.IndexOf(phase, StringComparison.Ordinal) : -1;
        int iHelper = body != null ? body.IndexOf(helper, StringComparison.Ordinal) : -1;
        int iAfter = body != null ? body.IndexOf(after, StringComparison.Ordinal) : -1;
        Assert(iPhase >= 0 && iHelper > iPhase && iAfter > iHelper,
            method + " resolves pending poison secondaries after CombatOver and before " + after.TrimEnd(';', ')', '('),
            "(phase=" + iPhase + " helper=" + iHelper + " after=" + iAfter + ")");
    }

    /// <summary>The text of the method whose declaration starts with <paramref name="signature"/>, up to its closing brace at 4 spaces.</summary>
    private static string MethodBody(string code, string signature)
    {
        int start = code.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0)
            return null;
        int close = code.IndexOf("\n    }", start, StringComparison.Ordinal);
        return close < 0 ? code.Substring(start) : code.Substring(start, close - start);
    }

    private static string StripComments(string path)
    {
        string text = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/", " ",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        var code = new StringBuilder();
        foreach (string line in text.Split('\n'))
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            code.Append(comment >= 0 ? line.Substring(0, comment) : line.TrimEnd('\r')).Append('\n');
        }
        return code.ToString();
    }
}
}
