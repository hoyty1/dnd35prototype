#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 12 step 7 (docs/designs/pc_npc_parity_batch12.md, decision D4; CMB-075, CMB-100): a turn is skipped only when
    /// the creature can take no action at all (ConditionRules.DeniesAllActions), and the compulsions run once per turn
    /// for every actor (GameManager.TryRunCompulsionTurn). Before the step the skip gate also took the turn of a
    /// nauseated, pinned, panicked or turned creature, PC or NPC, so none of them moved, escaped or fled.
    /// </summary>
    public static class SkipGateScenarios
    {
        private const string NotePrefix = "b12s7 ";

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("nauseated-move-only-ui", NauseatedMoveOnlyUi);
            yield return S("nauseated-move-only-ai", NauseatedMoveOnlyAi);
            yield return S("pinned-escape-ui", PinnedEscapeUi);
            yield return S("pinned-escape-ai", PinnedEscapeAi);
            yield return S("panicked-pc-flees", PanickedPcFlees);
            yield return S("turned-undead-flees-ai", TurnedUndeadFleesAi);
            yield return S("three-skipped-in-a-row", ThreeSkippedInARow);
            yield return S("rainbow-pattern-fascination-holds", RainbowPatternFascinationHolds);
            yield return S("frightened-pc-unresolved-source-acts", FrightenedPcUnresolvedSourceActs);
            yield return S("frightened-pc-cornered-fights", FrightenedPcCorneredFights);
            yield return S("panicked-pinned-escapes-ai", PanickedPinnedEscapesAi);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("SkipGateScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        private static Vector2Int? Pos(JsonObj snap) => snap != null && snap.Get("pos") is Vector2Int p ? p : (Vector2Int?)null;

        private static bool HasCond(JsonObj snap, string cond) => snap != null && snap.Get("conds") is List<string> l && l.Contains(cond);

        private static int Dist(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        /// <summary>No "X ... cannot act this turn" skip line for <paramref name="name"/> in <paramref name="round"/>.</summary>
        private static Func<TraceView, ExpectResult> NotSkipped(string name, int round)
            => v =>
            {
                TraceEvent skip = v.Log("^⏭? ?" + name + " .*cannot act this turn").FirstOrDefault(e => e.Round == round);
                return skip == null ? ExpectResult.Pass(name + " was not skipped in round " + round) : ExpectResult.Fail("skipped: " + skip.Str("text"), skip.Seq);
            };

        // ── Nauseated: a single move action (PHB glossary p.311; D4) ─────

        /// <summary>
        /// A nauseated Ui fighter next to a dummy: its turn is not skipped; the Attack button is refused (no standard action,
        /// ActionEconomy.MoveActionOnly from batch 12 step 3), one Move is done and a second Move is refused (one move action
        /// only, no conversion of the standard action).
        /// </summary>
        private static ScenarioDef NauseatedMoveOnlyUi()
        {
            return Rules("rules/nauseated-move-only-ui", "A nauseated PC gets its turn with a single move action and no attack (PHB p.311; D4, CMB-075)")
                .Covers("CMB-075", "PHB p.311", "D4", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 8, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 9, 10, Control.Scripted)
                .Tweak("hero", RulesScenarios.StripOffHand)
                .Tweak("dummy", Sturdy)
                .StartCondition("hero", CombatConditionType.Nauseated, 3)
                .Initiative("hero", "dummy")
                .Turn("hero", 1, Step.Attack("dummy"), Step.Move(5, 10), Step.Move(3, 10))
                .Turn("dummy", 0, Step.Pass())
                .Expect("The nauseated hero's turn is not skipped", NotSkipped("Hero", 1))
                .Expect("The Attack step is refused (no standard action)", Expect.StepStatus("hero", 1, "Attack", 0, "refused"))
                .Expect("The first Move is done, the second refused (a single move action)", Expect.All(
                    Expect.StepStatus("hero", 1, "Move", 0, "done"),
                    Expect.StepStatus("hero", 1, "Move", 1, "refused")))
                .Expect("The hero makes no attack and ends at (5,10)", v =>
                {
                    if (v.Attacks("hero").Count > 0) return ExpectResult.Fail("the hero attacked", v.Attacks("hero")[0].Seq);
                    Vector2Int? p = Pos(v.Final("hero"));
                    return p == new Vector2Int(5, 10) ? ExpectResult.Pass("at (5,10)") : ExpectResult.Fail("hero at " + p);
                })
                .Build();
        }

        /// <summary>
        /// A nauseated AI goblin 6 squares from a sturdy Idle fighter: its turn is not skipped, it moves (its single move
        /// action) and makes no attack, cast or charge.
        /// </summary>
        private static ScenarioDef NauseatedMoveOnlyAi()
        {
            return Rules("rules/nauseated-move-only-ai", "A nauseated AI creature moves with its single move action and does not attack (PHB p.311; D4, CMB-075)")
                .Covers("CMB-075", "PHB p.311", "D4", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("gob", "goblin", 13, 10, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 7, 10, Control.Idle)
                .Tweak("hero", Sturdy)
                .StartCondition("gob", CombatConditionType.Nauseated, 3)
                .Initiative("gob", "hero")
                .Expect("The goblin's turn is an AI turn", Expect.Controller("gob", "ai"))
                .Expect("The goblin moves in round 1", v =>
                    v.Moves("gob", 1).Count > 0 ? ExpectResult.Pass(v.Moves("gob", 1).Count + " move segment(s)") : ExpectResult.Fail("no goblin move in round 1"))
                .Expect("The goblin makes no attack and casts nothing", Expect.All(
                    Expect.None("attack", e => e.Str("attacker") == "gob"),
                    Expect.None("cast", e => e.Str("by") == "gob")))
                .Expect("The goblin's turn used its move action only", v =>
                {
                    TraceEvent end = v.Of("turn_end").FirstOrDefault(e => e.Str("actor") == "gob" && e.Round == 1);
                    if (end == null) return ExpectResult.Fail("no goblin turn_end in round 1");
                    JsonObj econ = end.Get("econ") as JsonObj;
                    if (econ == null) return ExpectResult.Inconclusive("no action-economy record");
                    bool std = econ.Get("std") is bool s && s, full = econ.Get("full") is bool f && f;
                    return !std && !full ? ExpectResult.Pass("no standard or full-round action", end.Seq) : ExpectResult.Fail("standard " + std + ", full " + full, end.Seq);
                })
                .Build();
        }

        // ── Pinned: grapple escape only (PHB p.156; D4) ──────────────────

        /// <summary>The pinner's fixture: grapple (the Maneuver step), then pin at once (an Assert, no action spent).</summary>
        private static Step PinFixture(string pinner, string pinned)
            => Step.Assert(pinner + " pins " + pinned + " (fixture: the pin check through ResolveGrappleAction)", ctx =>
            {
                CharacterController a = ctx.Get(pinner), b = ctx.Get(pinned);
                if (a == null || b == null || !a.IsGrappling())
                    return false;
                SpecialAttackResult r = a.ResolveGrappleAction(GrappleActionType.PinOpponent);
                return r != null && r.Success && b.IsPinned();
            });

        /// <summary>
        /// An orc with Improved Grapple grabs and pins a Ui fighter 1 (all grapple d20s 20, ties to the higher modifier:
        /// orc +9, hero +4). On the hero's turn, which is no longer skipped, only the escape options are offered
        /// (GetAvailableGrappleActions), the Attack button is redirected to the grapple menu, and the Escape button makes
        /// the opposed check (lost on the tie).
        /// </summary>
        private static ScenarioDef PinnedEscapeUi()
        {
            return Rules("rules/pinned-escape-ui", "A pinned PC gets its turn with the grapple escape options only (PHB p.156; D4, CMB-075)")
                .Covers("CMB-075", "PHB p.156", "D4", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 1)), 10, 10, Control.Ui)
                .Tweak("hero", RulesScenarios.StripOffHand)
                .Tweak("orc", c => c.Stats.Feats.Add("Improved Grapple"))
                .Initiative("orc", "hero")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("orc", 1, Step.Maneuver(SpecialAttackType.Grapple, "hero"), PinFixture("orc", "hero"))
                .Turn("hero", 1,
                    Step.Assert("only the escape options are offered to the pinned hero", ctx =>
                    {
                        CharacterController h = ctx.Get("hero");
                        List<GrappleActionType> offered = ctx.Gm.GetAvailableGrappleActions(h);
                        ctx.Note(NotePrefix + "hero.offered=" + string.Join(",", offered));
                        return h.IsPinned()
                            && offered.Count == 2 && offered.Contains(GrappleActionType.OpposedGrappleEscape) && offered.Contains(GrappleActionType.EscapeArtist)
                            && !ctx.Gm.CanUseGrappleAction(h, GrappleActionType.DamageOpponent)
                            && !ctx.Gm.CanUseGrappleAction(h, GrappleActionType.PinOpponent);
                    }),
                    Step.Attack("orc"),
                    Step.GrappleAction("Escape"))
                .Expect("The orc grabs and pins the hero", Expect.All(
                    Expect.StepStatus("orc", 1, "Maneuver", 0, "done"),
                    Expect.AssertsPass()))
                .Expect("The pinned hero's turn is not skipped", NotSkipped("Hero", 1))
                .Expect("The Attack step is refused: a pinned creature only tries to escape", Expect.StepStatus("hero", 1, "Attack", 0, "refused"))
                .Expect("The Escape step makes the opposed check and fails on the tie", v =>
                {
                    TraceEvent fail = v.Log("Hero fails to escape").FirstOrDefault(e => e.Round == 1);
                    return fail != null ? ExpectResult.Pass("escape check made", fail.Seq) : ExpectResult.Fail("no failed escape check by the hero in round 1");
                })
                .Expect("The hero makes no attack", Expect.None("attack", e => e.Str("attacker") == "hero"))
                .Build();
        }

        /// <summary>
        /// A Scripted fighter 6 with Improved Grapple grabs and pins an AI orc (hero +13, orc +5; all grapple d20s 20). The
        /// pinned orc's turn is no longer skipped: the AI grapple turn (BuildNPCLegalGrappleActions, the pinned list) tries
        /// to escape, fails on the tie, and the orc neither attacks nor moves.
        /// </summary>
        private static ScenarioDef PinnedEscapeAi()
        {
            return Rules("rules/pinned-escape-ai", "A pinned AI creature tries to escape the pin on its turn (PHB p.156; D4, CMB-075)")
                .Covers("CMB-075", "PHB p.156", "D4", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6, "Improved Grapple")), 10, 10, Control.Scripted)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Ai)
                .Initiative("hero", "orc")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"), PinFixture("hero", "orc"))
                .Expect("The hero grabs and pins the orc", Expect.All(
                    Expect.StepStatus("hero", 1, "Maneuver", 0, "done"),
                    Expect.AssertsPass()))
                .Expect("The pinned orc's turn is an AI turn and is not skipped", Expect.All(
                    Expect.Controller("orc", "ai"),
                    v => v.Log("cannot act this turn").Any(e => e.Round == 1) ? ExpectResult.Fail("a round-1 turn was skipped") : ExpectResult.Pass("no skip")))
                .Expect("The orc's grapple choice is an escape (the pinned options only)", v =>
                {
                    var r = new System.Text.RegularExpressions.Regex(@"chooses grapple action \[(\w+)\]");
                    List<string> choices = v.Of("log").Where(e => e.Round == 1 && r.IsMatch(e.Str("text") ?? ""))
                        .Select(e => r.Match(e.Str("text")).Groups[1].Value).ToList();
                    if (choices.Count == 0) return ExpectResult.Fail("no grapple choice by the orc");
                    string bad = choices.FirstOrDefault(c => c != "OpposedGrappleEscape" && c != "EscapeArtist");
                    return bad == null ? ExpectResult.Pass(string.Join(",", choices)) : ExpectResult.Fail("chose " + bad + " while pinned");
                })
                .Expect("The escape check fails on the tie and the orc stays pinned", v =>
                {
                    bool failed = v.Log("fails to escape|Escape Artist").Any(e => e.Round == 1);
                    return failed && HasCond(v.Final("orc"), "Pinned") ? ExpectResult.Pass("failed, still pinned") : ExpectResult.Fail("failed log " + failed + ", pinned " + HasCond(v.Final("orc"), "Pinned"));
                })
                .Expect("The orc neither attacks nor moves", Expect.All(
                    Expect.None("attack", e => e.Str("attacker") == "orc"),
                    Expect.None("move", e => e.Str("actor") == "orc")))
                .Build();
        }

        // ── Panicked PC: a forced flight (PHB p.311; D4, owner decision Q1) ──

        /// <summary>
        /// A Scripted goblin panics the adjacent Ui fighter (the harness stand-in for a failed save against a fear effect,
        /// with the goblin as the source). On the hero's turn the compulsion pass starts a forced turn (owner decision
        /// 2026-10-23, Q1): the shared flee controller withdraws it away from the goblin, the player's Attack step never
        /// runs (the action menu never opens), and the hero makes no attack.
        /// </summary>
        private static ScenarioDef PanickedPcFlees()
        {
            return Rules("rules/panicked-pc-flees", "A panicked PC flees from the source of its fear as a forced turn (PHB p.311; D4, CMB-100, Q1)")
                .Covers("CMB-075", "CMB-100", "PHB p.311", "D4", "Q1", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("gob", "goblin", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 10, 10, Control.Ui)
                .Tweak("hero", RulesScenarios.StripOffHand)
                .Tweak("gob", Sturdy)
                .Initiative("gob", "hero")
                .Turn("gob", 1, Step.Assert("the goblin panics the hero (fixture)", ctx =>
                {
                    ctx.Gm.ApplyCondition(ctx.Get("hero"), CombatConditionType.Panicked, 3, ctx.Get("gob"), sourceCategory: "Harness");
                    return ctx.Get("hero").HasCondition(CombatConditionType.Panicked);
                }), Step.Pass())
                .Turn("hero", 1, Step.Attack("gob"))
                .Expect("The panicked hero's turn is not skipped", NotSkipped("Hero", 1))
                .Expect("The player's Attack step never runs: the turn is forced (no step done or refused)", v =>
                {
                    TraceEvent ran = v.Steps("hero", "Attack", 1).FirstOrDefault(e => e.Str("status") == "done" || e.Str("status") == "refused");
                    return ran == null ? ExpectResult.Pass("the Attack step never ran") : ExpectResult.Fail("the Attack step " + ran.Str("status"), ran.Seq);
                })
                .Expect("The hero flees: it ends farther from the goblin than it started", v =>
                {
                    JsonObj h0 = v.Snapshot("hero", 1, "hero"), g0 = v.Snapshot("gob", 1, "hero");
                    Vector2Int? hs = Pos(h0), gs = Pos(g0), hf = Pos(v.Final("hero"));
                    if (hs == null || gs == null || hf == null) return ExpectResult.Inconclusive("no positions");
                    int before = Dist(hs.Value, gs.Value), after = Dist(hf.Value, gs.Value);
                    bool logged = v.Log("flees in terror|runs from").Any(e => e.Round == 1);
                    return after > before && logged
                        ? ExpectResult.Pass("distance " + before + " -> " + after)
                        : ExpectResult.Fail("distance " + before + " -> " + after + ", flee log " + logged);
                })
                .Expect("The hero makes no attack", Expect.None("attack", e => e.Str("attacker") == "hero"))
                .Build();
        }

        // ── Turned undead: flee, or cower when cornered (PHB p.159; D4) ──

        /// <summary>
        /// A Scripted cleric turns two AI skeletons 4 squares away (the fixture applies Turned for 10 rounds with the
        /// cleric as the turner, the effect of a successful turning check, PHB p.159). The free skeleton flees from the
        /// cleric by the fastest means (the shared flight: withdraw, a double move), farther than one move action takes
        /// it; the one that cannot move (speed 0, a fixture) cowers: the Cowering condition until its next turn, so its
        /// round-2 turn is not skipped and it cowers again. Neither attacks, and no turn is skipped in either round.
        /// </summary>
        private static ScenarioDef TurnedUndeadFleesAi()
        {
            return Rules("rules/turned-undead-flees-ai", "Turned undead flee from the turner, or cower when they cannot flee (PHB p.159; D4, CMB-075)")
                .Covers("CMB-075", "PHB p.159", "D4", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("cleric", ActorSource.Stats(() => RulesScenarios.Fighter("Cleric", 3)), 5, 10, Control.Scripted)
                .Npc("skel", "skeleton_warrior", 9, 10, Control.Ai)
                .Npc("stuck", "skeleton_warrior", 9, 12, Control.Ai)
                .Tweak("stuck", c => c.Stats.BaseSpeed = 0)
                .Initiative("cleric", "skel", "stuck")
                .Turn("cleric", 1, Step.Assert("the cleric turns both skeletons (fixture)", ctx =>
                {
                    CharacterController cleric = ctx.Get("cleric");
                    foreach (string k in new[] { "skel", "stuck" })
                    {
                        CharacterController u = ctx.Get(k);
                        ctx.Gm.ApplyCondition(u, CombatConditionType.Turned, 10, cleric, sourceCategory: "Harness");
                        ctx.Gm.RegisterTurnUndeadTrackerForAI(u, cleric);
                    }
                    return ctx.Get("skel").HasCondition(CombatConditionType.Turned) && ctx.Get("stuck").HasCondition(CombatConditionType.Turned);
                }), Step.Pass())
                .Turn("cleric", 2, Step.Pass())
                .Expect("No turned skeleton's turn is skipped, in either round", v =>
                    v.Log("cannot act this turn").Any(e => e.Round == 1 || e.Round == 2) ? ExpectResult.Fail("a turn was skipped") : ExpectResult.Pass("no skip"))
                .Expect("The free skeleton flees farther than one move action (speed 30 ft, 6 squares) takes it", v =>
                {
                    Vector2Int? c = Pos(v.Snapshot("cleric", 1, "skel")), s0 = Pos(v.Snapshot("skel", 1, "skel")), s1 = Pos(v.Snapshot("skel", 1, "stuck"));
                    if (c == null || s0 == null || s1 == null) return ExpectResult.Inconclusive("no positions");
                    int before = Dist(s0.Value, c.Value), after = Dist(s1.Value, c.Value);
                    bool logged = v.Log("flees in terror from Cleric|runs from Cleric").Any(e => e.Round == 1);
                    return after > before + 6 && logged ? ExpectResult.Pass("distance " + before + " -> " + after) : ExpectResult.Fail("distance " + before + " -> " + after + ", flee log " + logged);
                })
                .Expect("The skeleton that cannot move cowers in round 1 and acts again (cowers again) in round 2", v =>
                {
                    bool logged1 = v.Log("cannot flee: it cowers").Any(e => e.Round == 1);
                    bool cowering1 = HasCond(v.Snapshot("stuck", 2, "cleric"), "Cowering");
                    int cowers2 = v.Log("cannot flee: it cowers").Count(e => e.Round == 2);
                    return logged1 && cowering1 && cowers2 >= 1 && v.Moves("stuck").Count == 0
                        ? ExpectResult.Pass("cowering after round 1, cowers again in round 2, no move")
                        : ExpectResult.Fail("cower log r1 " + logged1 + ", Cowering at round 2 start " + cowering1 + ", cower logs r2 " + cowers2 + ", moves " + v.Moves("stuck").Count);
                })
                .Expect("No skeleton attacks", Expect.None("attack", e => e.Str("attacker") == "skel" || e.Str("attacker") == "stuck"))
                .Build();
        }

        // ── Three skipped turns in a row (CORE-012 re-entrancy) ──────────

        /// <summary>
        /// A Ui fighter and two AI goblins, all paralyzed, take consecutive initiative counts after a Scripted fighter. Each
        /// skipped turn advances the next one synchronously inside the previous one (CORE-012); three in a row, over three
        /// rounds, must keep the turn structure (one turn per actor per round, rounds in order) and give the acting
        /// fighter its turn every round.
        /// </summary>
        private static ScenarioDef ThreeSkippedInARow()
        {
            return Rules("rules/three-skipped-in-a-row", "Three skipped turns in a row keep the turn order intact (CORE-012; D4)")
                .Covers("CORE-012", "D4", "CMB-075")
                .MaxRounds(3)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 5, 10, Control.Scripted)
                .Pc("frozen", ActorSource.Stats(() => RulesScenarios.Fighter("Frozen", 3)), 5, 12, Control.Ui)
                .Npc("gob1", "goblin", 14, 10, Control.Ai)
                .Npc("gob2", "goblin", 14, 12, Control.Ai)
                .Tweak("frozen", RulesScenarios.StripOffHand)
                .StartCondition("frozen", CombatConditionType.Paralyzed, 10)
                .StartCondition("gob1", CombatConditionType.Paralyzed, 10)
                .StartCondition("gob2", CombatConditionType.Paralyzed, 10)
                .Initiative("hero", "frozen", "gob1", "gob2")
                .Turn("hero", 0, Step.Pass())
                .Expect("Every round: the three paralyzed turns are skipped, in initiative order", v =>
                {
                    for (int round = 1; round <= 3; round++)
                    {
                        List<string> order = v.Of("turn_start").Where(e => e.Round == round).Select(e => e.Str("actor")).ToList();
                        string got = string.Join(",", order);
                        if (got != "hero,frozen,gob1,gob2")
                            return ExpectResult.Fail("round " + round + " turn order " + got);
                        int skips = v.Log("cannot act this turn").Count(e => e.Round == round);
                        if (skips != 3)
                            return ExpectResult.Fail("round " + round + ": " + skips + " skip line(s), expected 3");
                    }
                    return ExpectResult.Pass("3 rounds of hero,frozen,gob1,gob2 with 3 skips each");
                })
                .Expect("The hero acts in each of the 3 rounds", v =>
                {
                    int turns = v.TurnsOf("hero").Count;
                    return turns == 3 ? ExpectResult.Pass("3 hero turns") : ExpectResult.Fail(turns + " hero turn(s)");
                })
                .Build();
        }

        // ── Review fixes (batch 12 step 7) ───────────────────────────────

        /// <summary>
        /// A Scripted wizard stands in for Rainbow Pattern on an AI goblin (the spell's own call: Fascinated with only the
        /// spell's name as its source, GameManager_Spells_R). The fascination's source never resolves to a creature, yet
        /// it holds: the goblin's turn is skipped in rounds 1 and 2 and it is still fascinated at the end. Before the
        /// review fix the turn-start release (the bard's Fascinate only now) ended it at the goblin's first turn.
        /// </summary>
        private static ScenarioDef RainbowPatternFascinationHolds()
        {
            return Rules("rules/rainbow-pattern-fascination-holds", "A spell's fascination with no creature source holds its duration (PHB p.268; D4)")
                .Covers("D4", "CMB-075", "PHB p.268")
                .MaxRounds(2)
                .Pc("wiz", ActorSource.Stats(() => RulesScenarios.Fighter("Wizard", 3)), 5, 10, Control.Scripted)
                .Npc("gob", "goblin", 12, 10, Control.Ai)
                .Tweak("gob", Sturdy)
                .Initiative("wiz", "gob")
                .Turn("wiz", 1, Step.Assert("Rainbow Pattern fascinates the goblin (fixture, the spell's call)", ctx =>
                {
                    ctx.Get("gob").ApplyCondition(CombatConditionType.Fascinated, 5, "Rainbow Pattern");
                    return ctx.Get("gob").HasCondition(CombatConditionType.Fascinated);
                }), Step.Pass())
                .Turn("wiz", 2, Step.Pass())
                .Expect("The goblin's turn is skipped in round 1 and round 2", v =>
                {
                    int r1 = v.Log("cannot act this turn").Count(e => e.Round == 1), r2 = v.Log("cannot act this turn").Count(e => e.Round == 2);
                    return r1 == 1 && r2 == 1 ? ExpectResult.Pass("skipped twice") : ExpectResult.Fail("skip lines r1 " + r1 + ", r2 " + r2);
                })
                .Expect("The fascination is not released for want of a source", Expect.All(
                    v => v.Log("no longer fascinated").Any() ? ExpectResult.Fail("released") : ExpectResult.Pass("not released"),
                    v => HasCond(v.Final("gob"), "Fascinated") ? ExpectResult.Pass("still fascinated") : ExpectResult.Fail("not fascinated at the end")))
                .Expect("The goblin neither moves nor attacks", Expect.All(
                    Expect.None("attack", e => e.Str("attacker") == "gob"),
                    Expect.None("move", e => e.Str("actor") == "gob")))
                .Build();
        }

        /// <summary>
        /// A Ui fighter frightened with a source that is no creature (a Mace of Terror's call records only the item's name,
        /// CMB-178). The turn start ends that fear (the existing shortcut) before the turn is decided, so no forced flight
        /// starts and the player's Attack on the adjacent dummy runs. Before the review fix the forced turn ended at once.
        /// </summary>
        private static ScenarioDef FrightenedPcUnresolvedSourceActs()
        {
            return Rules("rules/frightened-pc-unresolved-source-acts", "A PC whose fear has no creature source gets its turn (CMB-178; Q1, D4)")
                .Covers("CMB-178", "CMB-100", "Q1", "D4")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 8, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 9, 10, Control.Scripted)
                .Tweak("hero", RulesScenarios.StripOffHand)
                .Tweak("dummy", Sturdy)
                .Initiative("dummy", "hero")
                .Turn("dummy", 1, Step.Assert("a Mace of Terror frightens the hero (fixture: the item's name as the only source)", ctx =>
                {
                    ctx.Gm.ApplyCondition(ctx.Get("hero"), CombatConditionType.Frightened, 3, null, sourceNameOverride: "Mace of Terror", sourceCategory: "Harness");
                    return ctx.Get("hero").HasCondition(CombatConditionType.Frightened);
                }), Step.Pass())
                .Turn("hero", 1, Step.Attack("dummy"))
                .Expect("The fear without a source ends at the hero's turn start", v =>
                    v.Log("Hero is no longer frightened").Any(e => e.Round == 1) ? ExpectResult.Pass("ended") : ExpectResult.Fail("no 'no longer frightened' line"))
                .Expect("The player's Attack step runs", Expect.StepStatus("hero", 1, "Attack", 0, "done"))
                .Expect("The hero does not flee", v =>
                    v.Log("flees in terror|runs from").Any(e => e.Round == 1) ? ExpectResult.Fail("the hero fled") : ExpectResult.Pass("no flight"))
                .Build();
        }

        /// <summary>
        /// A Ui fighter with speed 0 (a fixture) is frightened by the adjacent goblin. Its forced flight finds no cell, so it
        /// is cornered: it fights defensively and gets its action menu back (PHB glossary p.310: a frightened creature that
        /// cannot flee may fight), and the player's Attack on the goblin runs.
        /// </summary>
        private static ScenarioDef FrightenedPcCorneredFights()
        {
            return Rules("rules/frightened-pc-cornered-fights", "A cornered frightened PC gets its menu back and may fight (PHB p.310; Q1, CMB-100)")
                .Covers("CMB-100", "PHB p.310", "Q1", "D4")
                .MaxRounds(1)
                .Npc("gob", "goblin", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 10, 10, Control.Ui)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); c.Stats.BaseSpeed = 0; })
                .Tweak("gob", Sturdy)
                .Initiative("gob", "hero")
                .Turn("gob", 1, Step.Assert("the goblin frightens the hero (fixture)", ctx =>
                {
                    ctx.Gm.ApplyCondition(ctx.Get("hero"), CombatConditionType.Frightened, 3, ctx.Get("gob"), sourceCategory: "Harness");
                    return ctx.Get("hero").HasCondition(CombatConditionType.Frightened);
                }), Step.Pass())
                .Turn("hero", 1, Step.Attack("gob"))
                .Expect("The cornered hero fights defensively", v =>
                    v.Log("cornered by fear and fights defensively").Any(e => e.Round == 1) ? ExpectResult.Pass("cornered") : ExpectResult.Fail("no cornered line"))
                .Expect("The player's Attack step runs after the forced part", Expect.StepStatus("hero", 1, "Attack", 0, "done"))
                .Expect("The hero attacks the goblin", v =>
                    v.Attacks("hero").Count > 0 ? ExpectResult.Pass(v.Attacks("hero").Count + " attack(s)") : ExpectResult.Fail("no attack by the hero"))
                .Build();
        }

        /// <summary>
        /// As rules/pinned-escape-ai, and the pinner also panics the pinned AI orc (a fixture, the hero as the source). The
        /// panicked orc flees by trying to escape the pin (FrightenedBehaviorController.Flee: the grapple escape first)
        /// instead of cowering in place: its grapple choice is an escape, it does not cower and makes no attack.
        /// </summary>
        private static ScenarioDef PanickedPinnedEscapesAi()
        {
            return Rules("rules/panicked-pinned-escapes-ai", "A pinned panicked AI creature flees by trying to escape the pin (PHB p.156, p.311; D4)")
                .Covers("PHB p.156", "PHB p.311", "D4", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6, "Improved Grapple")), 10, 10, Control.Scripted)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Ai)
                .Initiative("hero", "orc")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"), PinFixture("hero", "orc"),
                    Step.Assert("the hero panics the pinned orc (fixture)", ctx =>
                    {
                        ctx.Gm.ApplyCondition(ctx.Get("orc"), CombatConditionType.Panicked, 3, ctx.Get("hero"), sourceCategory: "Harness");
                        return ctx.Get("orc").HasCondition(CombatConditionType.Panicked) && ctx.Get("orc").IsPinned();
                    }))
                .Expect("The fixtures pin and panic the orc", Expect.All(
                    Expect.StepStatus("hero", 1, "Maneuver", 0, "done"),
                    Expect.AssertsPass()))
                .Expect("The orc's turn is not skipped and its grapple choice is an escape", v =>
                {
                    if (v.Log("cannot act this turn").Any(e => e.Round == 1)) return ExpectResult.Fail("a round-1 turn was skipped");
                    var r = new System.Text.RegularExpressions.Regex(@"chooses grapple action \[(\w+)\]");
                    List<string> choices = v.Of("log").Where(e => e.Round == 1 && r.IsMatch(e.Str("text") ?? ""))
                        .Select(e => r.Match(e.Str("text")).Groups[1].Value).ToList();
                    if (choices.Count == 0) return ExpectResult.Fail("no grapple choice by the orc");
                    string bad = choices.FirstOrDefault(c => c != "OpposedGrappleEscape" && c != "EscapeArtist");
                    return bad == null ? ExpectResult.Pass(string.Join(",", choices)) : ExpectResult.Fail("chose " + bad);
                })
                .Expect("The orc does not cower and makes no attack", Expect.All(
                    v => HasCond(v.Final("orc"), "Cowering") || v.Log("cowers").Any(e => e.Round == 1) ? ExpectResult.Fail("the orc cowered") : ExpectResult.Pass("no cowering"),
                    Expect.None("attack", e => e.Str("attacker") == "orc")))
                .Build();
        }
    }
}
#endif
