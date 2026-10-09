using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;
using Tests.Utilities;

namespace Tests.Combat
{
/// <summary>
/// CMB-006 (with CMB-034 and SPL-032): durations in rounds run relative to the initiative count they began on.
/// PHB p.138 (The Combat Round): an effect that lasts a number of rounds ends just before the same initiative count
/// that it began on; the monk example there has a 1-round stun from the monk's turn last until just before the monk's
/// count in the next round, so the stunned creature loses its next turn whatever the order (most cases below use a
/// 1-round daze as a stand-in, which follows the same timing; DMG p.301, Dazed: typically 1 round). Stunning Fist
/// (PHB p.101) lasts "until just before your next action", the monk's, also when it lands on an attack of opportunity.
///
/// Checks: TurnService reports every initiative count once per round, a dead creature's too, before the turn starts,
/// and sets <see cref="TurnDurations.CurrentAnchor"/> (null at the round boundary); conditions (ConditionManager and
/// the CharacterStats fallback) and spell effects (StatusEffectManager) tick only at their anchor's count, unanchored
/// ones and ones whose anchor left the initiative order at the round boundary; re-application keeps whichever ends
/// later, for conditions and spell effects (StatusEffectManager.AddEffect with a duration, Death Knell); Stunning Fist
/// is timed from the monk; area-sustained effects (Obscuring Mist concealment, Entangle) tick at the round boundary;
/// a creature leaving the initiative order hands its durations to a neighbouring count; and the PHB p.138 example
/// played through TurnService and ConditionService as GameManager wires them.
/// Run with TurnDurationRulesTests.RunAll().
/// </summary>
public static class TurnDurationRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== TURN DURATION RULES TESTS (CMB-006) ======");
        TestHelpers.EnsureCoreDatabasesInitialized();

        CharacterController savedAnchor = TurnDurations.CurrentAnchor;
        try
        {
            TestCountsReachedOncePerRound();
            TestConditionTicksAtAnchorCount();
            TestRoundBoundaryFallback();
            TestRefreshKeepsLaterEnd();
            TestSpellEffectsTickAtAnchorCount();
            TestSpellEffectRefreshWithDuration();
            TestStunningFistTimedFromMonk();
            TestAreaSustainedEffectsTickAtBoundary();
            TestRemovedCountHandsDurationsOn();
            TestDazeFromLaterCountCostsTheTargetItsTurn();
            TestDazeOnLaterCreatureCostsOneTurnOnly();
        }
        finally
        {
            TurnDurations.SetCurrentAnchor(savedAnchor);
        }

        Debug.Log($"====== Turn Duration Rules Results: {_passed} passed, {_failed} failed ======");
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
        host = new GameObject("TurnDurationTest_TurnService");
        return host.AddComponent<TurnService>();
    }

    private static int Remaining(CharacterController c, CombatConditionType type)
    {
        foreach (StatusEffect e in c.GetActiveConditionsDirect())
            if (ConditionRules.Normalize(e.Type) == type)
                return e.RemainingRounds;
        return 0;
    }

    private static StatusEffect Find(CharacterController c, CombatConditionType type)
    {
        foreach (StatusEffect e in c.GetActiveConditionsDirect())
            if (ConditionRules.Normalize(e.Type) == type)
                return e;
        return null;
    }

    // ── TurnService ─────────────────────────────────────────────────

    private static void TestCountsReachedOncePerRound()
    {
        CharacterController a = Make("Count A"), b = Make("Count B"), c = Make("Count C");
        TurnService turns = MakeTurnService(out GameObject host);
        var log = new List<string>();
        try
        {
            turns.OnNewRound += r => log.Add("round" + r + ":" + (TurnDurations.CurrentAnchor == null ? "null" : "set"));
            turns.OnInitiativeCountReached += ch => log.Add("count:" + ch.Stats.CharacterName);
            turns.OnTurnStarted += ch => log.Add("turn:" + ch.Stats.CharacterName + (TurnDurations.CurrentAnchor == ch ? "" : "(anchor wrong)"));

            var all = new List<CharacterController> { a, b, c };
            turns.StartCombat(all, null, _ => true, all);
            b.Stats.CurrentHP = -10; // dead after initiative: its count still comes up, its turn does not
            turns.EndTurn();
            turns.EndTurn();
            turns.StartTurnAtCurrentIndex(); // a repeated start of the same turn reports no second count
            string got = string.Join(" ", log);
            string want = "round1:null count:Count A turn:Count A count:Count B count:Count C turn:Count C round2:null count:Count A turn:Count A turn:Count A";
            Assert(got == want, "TurnService reports each initiative count once per round, a dead creature's too, before its turn, with the anchor set (PHB p.138)", "got: " + got);

            turns.EndCombat();
            Assert(TurnDurations.CurrentAnchor == null, "Ending combat clears the current duration anchor");
        }
        finally
        {
            Destroy(a, b, c, host);
        }
    }

    // ── Conditions ──────────────────────────────────────────────────

    private static void TestConditionTicksAtAnchorCount()
    {
        CharacterController monk = Make("Anchor Monk"), target = Make("Anchor Target");
        try
        {
            TurnDurations.SetCurrentAnchor(monk);
            target.ApplyConditionDirect(CombatConditionType.Dazed, 1, "Daze");
            StatusEffect daze1 = Find(target, CombatConditionType.Dazed);
            Assert(daze1 != null && daze1.DurationAnchor == monk, "A condition records the current initiative count's creature as its anchor (ConditionManager)");

            target.TickConditionsDirect(x => TurnDurations.TicksAtCount(x, target));
            Assert(Remaining(target, CombatConditionType.Dazed) == 1, "The target's own count does not tick a condition anchored to the monk");

            target.TickConditionsDirect(x => TurnDurations.TicksAtRoundBoundary(x, ch => ch == monk || ch == target));
            Assert(Remaining(target, CombatConditionType.Dazed) == 1, "The round boundary does not tick a condition whose anchor is in the initiative order");

            List<StatusEffect> expired = target.TickConditionsDirect(x => TurnDurations.TicksAtCount(x, monk));
            Assert(expired.Count == 1 && Find(target, CombatConditionType.Dazed) == null, "A 1-round daze ends at the monk's next count (PHB p.138)");

            // The CharacterStats fallback (a controller without ConditionManager) keeps the same anchor rules.
            var stats = TestHelpers.CreateStats("Fallback Target");
            stats.ApplyCondition(CombatConditionType.Dazed, 2, "Daze");
            StatusEffect daze = stats.ActiveConditions.Find(e => e.Type == CombatConditionType.Dazed);
            Assert(daze != null && daze.DurationAnchor == monk, "The CharacterStats condition fallback records the anchor too");
            stats.TickConditions(x => TurnDurations.TicksAtCount(x, target));
            stats.TickConditions(x => TurnDurations.TicksAtCount(x, monk));
            Assert(daze != null && daze.RemainingRounds == 1, "The CharacterStats fallback ticks only at the anchor's count");
        }
        finally
        {
            TurnDurations.ClearCurrentAnchor();
            Destroy(monk, target);
        }
    }

    private static void TestRoundBoundaryFallback()
    {
        CharacterController gone = Make("Gone Caster"), target = Make("Boundary Target");
        try
        {
            TurnDurations.ClearCurrentAnchor();
            target.ApplyConditionDirect(CombatConditionType.Shaken, 1, "Out of combat");
            TurnDurations.SetCurrentAnchor(gone);
            target.ApplyConditionDirect(CombatConditionType.Dazzled, 1, "Gone caster");
            TurnDurations.ClearCurrentAnchor();

            System.Func<CharacterController, bool> inInitiative = ch => ch == target; // the caster left the order
            target.TickConditionsDirect(x => TurnDurations.TicksAtCount(x, target));
            Assert(Find(target, CombatConditionType.Shaken) != null, "An unanchored condition does not tick at a creature's count");
            target.TickConditionsDirect(x => TurnDurations.TicksAtRoundBoundary(x, inInitiative));
            Assert(Find(target, CombatConditionType.Shaken) == null, "An unanchored condition (applied out of combat or at the round boundary) ticks at the round boundary");
            Assert(Find(target, CombatConditionType.Dazzled) == null, "A condition whose anchor left the initiative order ticks at the round boundary");
            Assert(TurnDurations.TicksAtRoundBoundary(gone, null), "Without an initiative order every duration ticks at the round boundary");
        }
        finally
        {
            TurnDurations.ClearCurrentAnchor();
            Destroy(gone, target);
        }
    }

    private static void TestRefreshKeepsLaterEnd()
    {
        CharacterController first = Make("Refresh First"), second = Make("Refresh Second"), target = Make("Refresh Target");
        try
        {
            TurnDurations.SetCurrentAnchor(first);
            target.ApplyConditionDirect(CombatConditionType.Shaken, 2, "Fear");
            TurnDurations.SetCurrentAnchor(second);
            target.ApplyConditionDirect(CombatConditionType.Shaken, 2, "Fear");
            StatusEffect shaken = Find(target, CombatConditionType.Shaken);
            Assert(shaken != null && shaken.RemainingRounds == 2 && shaken.DurationAnchor == second,
                "Re-applying an equal count from a later initiative count takes the later anchor (it ends no earlier)");

            TurnDurations.SetCurrentAnchor(first);
            target.ApplyConditionDirect(CombatConditionType.Shaken, 1, "Fear");
            Assert(shaken != null && shaken.RemainingRounds == 2 && shaken.DurationAnchor == second,
                "A shorter re-application leaves the longer condition and its anchor");

            TurnDurations.ClearCurrentAnchor();
            target.ApplyConditionDirect(CombatConditionType.Shaken, 3, "Fear");
            Assert(shaken != null && shaken.RemainingRounds == 3 && shaken.DurationAnchor == null,
                "A longer re-application takes its rounds and its anchor");

            TurnDurations.SetCurrentAnchor(first);
            target.ApplyConditionDirect(CombatConditionType.Prone, -1, "Trip");
            target.ApplyConditionDirect(CombatConditionType.Prone, 1, "Gust of Wind");
            Assert(Remaining(target, CombatConditionType.Prone) == -1, "An indefinite condition stays indefinite when re-applied with a count");
        }
        finally
        {
            TurnDurations.ClearCurrentAnchor();
            Destroy(first, second, target);
        }
    }

    // ── Spell effects (CMB-034, SPL-032) ────────────────────────────

    private static void TestSpellEffectsTickAtAnchorCount()
    {
        CharacterController caster = Make("Bless Caster"), other = Make("Bless Other"), ally = Make("Bless Ally");
        try
        {
            StatusEffectManager mgr = ally.GetComponent<StatusEffectManager>();
            if (mgr == null)
                mgr = ally.gameObject.AddComponent<StatusEffectManager>();
            mgr.Init(ally.Stats);
            TurnDurations.SetCurrentAnchor(caster);
            ActiveSpellEffect bless = mgr.AddEffect(SpellDatabase.GetSpell(SpellNames.BLESS).Clone(), "Bless Caster", 1);
            Assert(bless != null && bless.DurationAnchor == caster, "A spell effect records its caster's count as its anchor");
            if (bless == null)
                return;

            bless.RemainingRounds = 1;
            mgr.TickEffects(x => TurnDurations.TicksAtCount(x, other));
            mgr.TickEffects(x => TurnDurations.TicksAtCount(x, ally));
            Assert(mgr.HasEffect(SpellNames.BLESS), "A 1-round spell effect survives every other creature's count");
            mgr.TickEffects(x => TurnDurations.TicksAtCount(x, caster));
            Assert(!mgr.HasEffect(SpellNames.BLESS), "A 1-round spell effect ends at its caster's next count (PHB p.138)");

            TurnDurations.SetCurrentAnchor(caster);
            ActiveSpellEffect first = mgr.AddEffect(SpellDatabase.GetSpell(SpellNames.BLESS).Clone(), "Bless Caster", 1);
            TurnDurations.SetCurrentAnchor(other);
            ActiveSpellEffect recast = mgr.AddEffect(SpellDatabase.GetSpell(SpellNames.BLESS).Clone(), "Bless Other", 1);
            Assert(first != null && recast == null && first.DurationAnchor == other,
                "An equal-duration recast keeps the existing effect, timed from the later count");
        }
        finally
        {
            TurnDurations.ClearCurrentAnchor();
            Destroy(caster, other, ally);
        }
    }

    private static void TestSpellEffectRefreshWithDuration()
    {
        CharacterController first = Make("Recast First"), second = Make("Recast Second"), target = Make("Recast Target");
        try
        {
            StatusEffectManager mgr = target.GetComponent<StatusEffectManager>();
            if (mgr == null)
                mgr = target.gameObject.AddComponent<StatusEffectManager>();
            mgr.Init(target.Stats);

            TurnDurations.SetCurrentAnchor(first);
            ActiveSpellEffect bless = mgr.AddEffect(SpellDatabase.GetSpell(SpellNames.BLESS).Clone(), "Recast First", 1, 3);
            Assert(bless != null && bless.RemainingRounds == 3 && bless.DurationAnchor == first,
                "AddEffect with a duration sets it before the same-spell comparison");

            TurnDurations.SetCurrentAnchor(second);
            ActiveSpellEffect equal = mgr.AddEffect(SpellDatabase.GetSpell(SpellNames.BLESS).Clone(), "Recast Second", 1, 3);
            Assert(equal == null && bless != null && bless.RemainingRounds == 3 && bless.DurationAnchor == second,
                "An equal-duration recast from a later count keeps the effect, timed from the later count");

            TurnDurations.SetCurrentAnchor(first);
            ActiveSpellEffect shorter = mgr.AddEffect(SpellDatabase.GetSpell(SpellNames.BLESS).Clone(), "Recast First", 1, 2);
            Assert(shorter == null && bless != null && bless.RemainingRounds == 3 && bless.DurationAnchor == second,
                "A shorter recast leaves the effect and its anchor (the caller's duration is compared, not the spell's default)");

            ActiveSpellEffect longer = mgr.AddEffect(SpellDatabase.GetSpell(SpellNames.BLESS).Clone(), "Recast First", 1, 5);
            Assert(longer != null && longer.RemainingRounds == 5 && longer.DurationAnchor == first && !mgr.ActiveEffects.Contains(bless),
                "A longer recast replaces the effect with its own duration and anchor");

            // Death Knell's recast keeps the later end through the same rule (PHB p.217).
            StatusEffectManager firstMgr = first.GetComponent<StatusEffectManager>();
            if (firstMgr == null)
                firstMgr = first.gameObject.AddComponent<StatusEffectManager>();
            firstMgr.Init(first.Stats);
            SpellData knell = SpellDatabase.GetSpell(SpellNames.DEATH_KNELL).Clone();
            TurnDurations.SetCurrentAnchor(first);
            EffectService.ApplyDeathKnellBonus(first, knell, 1, 10, 5);
            ActiveSpellEffect knellEffect = firstMgr.ActiveEffects.Find(e => e.Spell != null && e.Spell.SpellId == SpellNames.DEATH_KNELL);
            TurnDurations.SetCurrentAnchor(second);
            EffectService.ApplyDeathKnellBonus(first, knell, 1, 10, 5);
            Assert(knellEffect != null && knellEffect.RemainingRounds == 10 && knellEffect.DurationAnchor == second && first.Stats.DeathKnellRoundsRemaining == 10,
                "A Death Knell recast of equal duration is timed from the later casting");
        }
        finally
        {
            TurnDurations.ClearCurrentAnchor();
            Destroy(first, second, target);
        }
    }

    private static void TestStunningFistTimedFromMonk()
    {
        CharacterController monk = TestHelpers.CreateCharacter(name: "Fist Monk", characterClass: "Monk", wis: 60, gridPosition: new Vector2Int(16, 16));
        CharacterController provoker = TestHelpers.CreateCharacter(name: "Fist Provoker", con: 1, gridPosition: new Vector2Int(17, 16));
        try
        {
            monk.Stats.Feats.Add("Stunning Fist");
            monk.Stats.StunningFistUsesRemaining = 1;

            // An attack of opportunity during the provoker's turn: the provoker's count is current.
            TurnDurations.SetCurrentAnchor(provoker);
            bool stunned = FeatManager.TryApplyStunningFist(monk.Stats, provoker, monk);
            StatusEffect stun = Find(provoker, CombatConditionType.Stunned);
            Assert(stunned && stun != null && stun.DurationAnchor == monk,
                "Stunning Fist on an attack of opportunity is timed from the monk's count (PHB p.101)",
                "stunned=" + stunned + " anchor=" + (stun != null && stun.DurationAnchor != null ? stun.DurationAnchor.Stats.CharacterName : "null"));
            Assert(TurnDurations.CurrentAnchor == provoker, "The explicit anchor scope gives back the current count");

            provoker.TickConditionsDirect(x => TurnDurations.TicksAtCount(x, provoker));
            Assert(Find(provoker, CombatConditionType.Stunned) != null, "The provoker's own count does not end the stun");
            provoker.TickConditionsDirect(x => TurnDurations.TicksAtCount(x, monk));
            Assert(Find(provoker, CombatConditionType.Stunned) == null, "The stun ends just before the monk's next action (PHB p.101)");
        }
        finally
        {
            TurnDurations.ClearCurrentAnchor();
            Destroy(monk, provoker);
        }
    }

    private static void TestAreaSustainedEffectsTickAtBoundary()
    {
        CharacterController caster = Make("Mist Caster"), inside = Make("Mist Inside");
        var mistHost = new GameObject("TurnDurationTest_Mist");
        var entangleHost = new GameObject("TurnDurationTest_Entangle");
        try
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            ObscuringMistAreaEffect mist = mistHost.AddComponent<ObscuringMistAreaEffect>();
            mist.Caster = caster;

            TurnDurations.SetCurrentAnchor(caster); // cast on the caster's turn
            typeof(ObscuringMistAreaEffect).GetMethod("OnCreatureEntersArea", flags).Invoke(mist, new object[] { inside, true });
            StatusEffectManager mgr = inside.StatusEffectManager;
            ActiveSpellEffect conceal = mgr != null ? mgr.ActiveEffects.Find(e => e.SourceAreaEffect == mist) : null;
            Assert(conceal != null && conceal.DurationAnchor == null, "Obscuring Mist concealment has no anchor: the area renews it at the round boundary");

            if (mgr != null)
                mgr.TickEffects(x => TurnDurations.TicksAtCount(x, caster));
            Assert(conceal != null && mgr.ActiveEffects.Contains(conceal) && inside.GetMissChance() > 0,
                "The concealment holds through the caster's next count (CMB-006)");

            TurnDurations.ClearCurrentAnchor();
            if (mgr != null)
                mgr.TickEffects(x => TurnDurations.TicksAtRoundBoundary(x, ch => true));
            typeof(ObscuringMistAreaEffect).GetMethod("OnCreatureInAreaAtRoundStart", flags).Invoke(mist, new object[] { inside });
            Assert(mgr != null && mgr.ActiveEffects.Exists(e => e.SourceAreaEffect == mist && e.DurationAnchor == null && e.RemainingRounds == 1),
                "At the round boundary the concealment ticks and the area renews it");

            EntangleAreaEffect entangle = entangleHost.AddComponent<EntangleAreaEffect>();
            entangle.Caster = caster;
            entangle.SaveDC = 100; // the Reflex save always fails
            TurnDurations.SetCurrentAnchor(caster);
            typeof(EntangleAreaEffect).GetMethod("OnCreatureEntersArea", flags).Invoke(entangle, new object[] { inside, true });
            StatusEffect entangled = Find(inside, CombatConditionType.Entangled);
            Assert(entangled != null && entangled.DurationAnchor == null && TurnDurations.CurrentAnchor == caster,
                "Entangle's 1-round condition ticks at the round boundary, where the area re-checks it");
        }
        finally
        {
            TurnDurations.ClearCurrentAnchor();
            Destroy(mistHost, entangleHost, caster, inside);
        }
    }

    private static void TestRemovedCountHandsDurationsOn()
    {
        CharacterController a = Make("Order A"), summon = Make("Order Summon"), b = Make("Order B"), target = Make("Order Target");
        try
        {
            var abs = new List<CharacterController> { a, summon, b };
            string got = HeirOf(abs, 2, summon, target, out int moved);
            StatusEffect shaken = Find(target, CombatConditionType.Shaken);
            Assert(got == "Order B" && moved == 1 && shaken != null && shaken.DurationAnchor == b,
                "A count removed after it came up this round hands its durations to the next count, also reached (PHB p.138)", got);
            got = HeirOf(abs, 0, summon, target, out _);
            Assert(got == "Order B", "A count removed before it came up hands its durations to the next count, still to come this round", got);
            got = HeirOf(abs, 1, summon, target, out _);
            Assert(got == "Order A", "A count removed during its own turn hands its durations to the previous count", got);
            got = HeirOf(new List<CharacterController> { a, b, summon }, 0, summon, target, out _);
            Assert(got == "boundary", "The last count removed before it came up hands its durations to the round boundary", got);
        }
        finally
        {
            TurnDurations.ClearCurrentAnchor();
            Destroy(a, summon, b, target);
        }
    }

    /// <summary>
    /// Starts combat with <paramref name="order"/>, ends <paramref name="endTurns"/> turns, gives <paramref name="target"/>
    /// a 2-round Shaken anchored to <paramref name="removed"/>, removes it from the order and re-anchors as GameManager
    /// does. Returns the heir's name, "boundary" or "none".
    /// </summary>
    private static string HeirOf(List<CharacterController> order, int endTurns, CharacterController removed, CharacterController target, out int moved)
    {
        TurnService turns = MakeTurnService(out GameObject host);
        CharacterController heir = null;
        bool raised = false;
        int movedCount = 0;
        try
        {
            turns.OnInitiativeCountRemoved += (gone, h) =>
            {
                raised = true;
                heir = h;
                movedCount = TurnDurations.Reanchor(new List<CharacterController> { target }, gone, h);
            };
            turns.StartCombat(order, null, _ => true, order);
            for (int i = 0; i < endTurns; i++)
                turns.EndTurn();

            target.RemoveConditionDirect(CombatConditionType.Shaken);
            using (TurnDurations.Anchor(removed))
                target.ApplyConditionDirect(CombatConditionType.Shaken, 2, "Summon");
            turns.RemoveFromInitiative(removed);
            turns.EndCombat();
        }
        finally
        {
            Destroy(host);
        }
        moved = movedCount;
        return !raised ? "none" : heir == null ? "boundary" : heir.Stats.CharacterName;
    }

    // ── PHB p.138 example through TurnService and ConditionService ──

    /// <summary>
    /// Plays rounds with <paramref name="order"/> (forced initiative) through TurnService and a ConditionService wired
    /// as GameManager wires them; <paramref name="onTurn"/> runs at each turn start with (character, round) after the
    /// count's ticks. Returns the turn log "round:name:dazed|free" of every turn start.
    /// </summary>
    private static List<string> Play(List<CharacterController> order, int rounds, CharacterController watched, System.Action<ConditionService, CharacterController, int> onTurn)
    {
        var log = new List<string>();
        TurnService turns = MakeTurnService(out GameObject host);
        ConditionService service = host.AddComponent<ConditionService>();
        try
        {
            service.Initialize(() => order);
            turns.OnNewRound += _ => service.OnRoundBoundary(a => TurnDurations.TicksAtRoundBoundary(a, ch => turns.GetInitiative(ch) != null));
            turns.OnInitiativeCountReached += service.OnInitiativeCountReached;
            turns.OnTurnStarted += ch =>
            {
                service.OnTurnStart(ch);
                log.Add(turns.CurrentRound + ":" + ch.Stats.CharacterName + ":" + (watched.HasConditionDirect(CombatConditionType.Dazed) ? "dazed" : "free"));
                onTurn(service, ch, turns.CurrentRound);
            };

            turns.StartCombat(order, null, _ => true, order);
            int guard = 0;
            while (turns.IsCombatActive && turns.CurrentRound <= rounds && guard++ < 50)
                turns.EndTurn();
            turns.EndCombat();
        }
        finally
        {
            Destroy(host);
        }
        return log;
    }

    private static void TestDazeFromLaterCountCostsTheTargetItsTurn()
    {
        CharacterController target = Make("Early Target"), monk = Make("Late Monk");
        try
        {
            List<string> log = Play(new List<CharacterController> { target, monk }, 2, target, (svc, ch, round) =>
            {
                if (ch == monk && round == 1)
                    svc.ApplyCondition(target, CombatConditionType.Dazed, 1, monk, sourceNameOverride: "Daze");
            });
            string got = string.Join(" ", log);
            Assert(log.Contains("2:Early Target:dazed"),
                "A 1-round daze from a later count still holds at the target's next turn: the target loses that turn (PHB p.138, CMB-006)", got);
            Assert(log.Contains("2:Late Monk:free"),
                "The daze ends just before the monk's count in the next round (PHB p.138)", got);
        }
        finally
        {
            Destroy(target, monk);
        }
    }

    private static void TestDazeOnLaterCreatureCostsOneTurnOnly()
    {
        CharacterController monk = Make("Early Monk"), target = Make("Late Target");
        try
        {
            List<string> log = Play(new List<CharacterController> { monk, target }, 2, target, (svc, ch, round) =>
            {
                if (ch == monk && round == 1)
                    svc.ApplyCondition(target, CombatConditionType.Dazed, 1, monk, sourceNameOverride: "Daze");
            });
            string got = string.Join(" ", log);
            Assert(log.Contains("1:Late Target:dazed") && log.Contains("2:Early Monk:free") && log.Contains("2:Late Target:free"),
                "A 1-round daze on a creature later in the order costs it the round-1 turn only (PHB p.138)", got);
        }
        finally
        {
            Destroy(monk, target);
        }
    }
}
}
