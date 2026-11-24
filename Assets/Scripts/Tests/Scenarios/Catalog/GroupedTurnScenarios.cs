#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Grouped AI turns (docs/designs/grouped_ai_turns.md). Step 2 (CMB-007): a poison's secondary save comes 10 rounds
    /// after exposure (DMG p.296: 1 minute; PHB p.138: 10 rounds), at the initiative count of the creature whose turn it was
    /// at exposure, whatever the real time; one still pending when the fight ends resolves at combat end, in initiative
    /// order, after CombatOver and before the loot window (delegated RAW reading 2026-11-16). Step 3: every token renders
    /// through its mirror view (TokenView), which follows its model.
    /// </summary>
    public static class GroupedTurnScenarios
    {
        private const string Venom = "medium_spider_poison"; // MM p.289: Medium monstrous spider, injury, Fort DC 12, 1d3 Str / 1d3 Str
        private const string VenomName = "Medium Spider Venom";

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("poison-secondary-after-ten-rounds", PoisonSecondaryAfterTenRounds);
            yield return S("poison-secondary-pending-at-combat-end", PoisonSecondaryPendingAtCombatEnd);
            yield return S("token-view-mirrors-tokens", TokenViewMirrorsTokens);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("GroupedTurnScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        private static string SecondaryLine(string name) => "^☣ " + name + " makes secondary save vs " + VenomName;

        // ── Step 2: poison secondaries in rounds (CMB-007) ─────────────

        /// <summary>
        /// The spider (Scripted) poisons the hero on its own turn in round 1 (CharacterController.ApplyPoison, the venom of
        /// its bite), with the hero and a bystander before it in the order. The secondary save comes at the spider's count of
        /// round 11, after the hero's and the bystander's turns of that round and before the spider's turn: 10 rounds after
        /// exposure at the exposure turn's count (PoisonTiming, the TurnDurations rule of CMB-006), not at the victim's
        /// count and not after 60 frames or seconds. Before the step the old real-time timer fired 60 game seconds after
        /// exposure, which fast mode made 60 frames.
        /// </summary>
        private static ScenarioDef PoisonSecondaryAfterTenRounds()
        {
            return Rules("rules/poison-secondary-after-ten-rounds", "A poison's secondary save comes 10 rounds after exposure, at the exposure turn's count (DMG p.296, PHB p.138; CMB-007)")
                .Covers("CMB-007", "CMB-006", "DMG p.296", "PHB p.138", "MM p.289", "PC_NPC_PARITY")
                .MaxRounds(11)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Poisoned Hero", 3)), 10, 10, Control.Idle)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Bystander", 3)), 10, 12, Control.Idle)
                .Npc("spider", "monstrous_spider_medium", 15, 10, Control.Scripted)
                .Tweak("hero", Sturdy)
                .Tweak("ally", Sturdy)
                .Tweak("spider", Sturdy)
                .Initiative("hero", "ally", "spider")
                .Turn("spider", 1,
                    Step.Assert("The spider's venom reaches the hero on the spider's turn: 10 rounds to go, anchored to the spider's count", ctx =>
                    {
                        CharacterController hero = ctx.Get("hero");
                        hero.ApplyPoison(Venom);
                        ActivePoison p = hero.ActivePoisons.LastOrDefault();
                        ctx.Note("poison exposure rounds=" + (p != null ? p.RoundsUntilSecondary : -1)
                                 + " anchor=" + (p != null && p.DurationAnchor != null ? p.DurationAnchor.Stats.CharacterName : "none"));
                        return p != null && p.RoundsUntilSecondary == 10 && p.DurationAnchor == ctx.Get("spider");
                    }),
                    Step.EndTurn())
                .Turn("spider", 10,
                    Step.Assert("At the spider's turn of round 10 one round is left", ctx =>
                    {
                        ActivePoison p = ctx.Get("hero").ActivePoisons.LastOrDefault();
                        ctx.Note("poison round 10 rounds=" + (p != null ? p.RoundsUntilSecondary : -1));
                        return p != null && !p.SecondaryResolved && p.RoundsUntilSecondary == 1;
                    }),
                    Step.EndTurn())
                .Expect("The exposure and round-10 checks hold", Expect.AssertsPass())
                .Expect("Exactly one secondary save, in round 11 (DMG p.296: 1 minute = 10 rounds, PHB p.138)", v =>
                {
                    List<TraceEvent> lines = v.Log(SecondaryLine("Poisoned Hero"));
                    if (lines.Count != 1)
                        return ExpectResult.Fail(lines.Count + " secondary save lines (rounds " + string.Join(",", lines.Select(e => e.Round)) + ")", lines.Select(e => e.Seq).ToArray());
                    return lines[0].Round == 11
                        ? ExpectResult.Pass(lines[0].Str("text"), lines[0].Seq)
                        : ExpectResult.Fail("secondary save in round " + lines[0].Round, lines[0].Seq);
                })
                .Expect("It rolls at the spider's count: after the hero's and the bystander's round-11 turns, before the spider's (CMB-006)", v =>
                {
                    TraceEvent line = v.Log(SecondaryLine("Poisoned Hero")).FirstOrDefault();
                    TraceEvent hero = v.TurnsOf("hero").FirstOrDefault(e => e.Round == 11);
                    TraceEvent ally = v.TurnsOf("ally").FirstOrDefault(e => e.Round == 11);
                    TraceEvent spider = v.TurnsOf("spider").FirstOrDefault(e => e.Round == 11);
                    if (line == null || hero == null || ally == null || spider == null)
                        return ExpectResult.Fail("missing: line " + (line != null) + ", hero " + (hero != null) + ", ally " + (ally != null) + ", spider " + (spider != null));
                    return v.Before(hero.Seq, line.Seq) && v.Before(ally.Seq, line.Seq) && v.Before(line.Seq, spider.Seq)
                        ? ExpectResult.Pass("hero #" + hero.Seq + ", bystander #" + ally.Seq + ", save #" + line.Seq + ", spider #" + spider.Seq, line.Seq)
                        : ExpectResult.Fail("hero #" + hero.Seq + ", bystander #" + ally.Seq + ", save #" + line.Seq + ", spider #" + spider.Seq, line.Seq);
                })
                .Build();
        }

        /// <summary>
        /// The fight ends with two secondaries pending: the hero (Scripted, first in the order) poisons the bystander and then
        /// himself in round 1 (the bystander first, so exposure order and initiative order differ) and kills the 1-hp orc.
        /// HandleCombatVictoryDetected sets CombatOver, then both secondaries resolve, the hero's first (initiative order),
        /// before the loot window. The trace closes at the CombatOver decision, so a watcher started by the hero's first
        /// step chains the trace's phase and log hooks and records what follows (the pattern of CastPipelineScenarios'
        /// AfterCombatEndWatch). Both secondary saves are forced to fail, so the Str damage lands.
        /// </summary>
        private static ScenarioDef PoisonSecondaryPendingAtCombatEnd()
        {
            return Rules("rules/poison-secondary-pending-at-combat-end", "A secondary still pending at the end of the fight resolves at combat end, in initiative order, before the loot window (DMG p.296; CMB-007)")
                .Covers("CMB-007", "DMG p.296", "PHB p.138", "delegated RAW reading 2026-11-16", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Poisoned Hero", 3)), 10, 10, Control.Scripted)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Bystander", 3)), 9, 12, Control.Idle)
                .Npc("orc", "orc_warrior", 11, 10, Control.Idle)
                .Tweak("hero", Sturdy)
                .Tweak("ally", Sturdy)
                .Tweak("orc", c => c.Stats.CurrentHP = 1)
                .Force(20, 2, "Poison secondary Fort save", -1)
                .Force(20, 19, null, -1)
                .Initiative("hero", "orc", "ally")
                .OnSetup(ctx => AfterCombatEndWatch.Stop())
                .Turn("hero", 1,
                    Step.Assert("Watch the combat end; the bystander, then the hero, are exposed: both have 10 rounds to go", ctx =>
                    {
                        AfterCombatEndWatch.Start();
                        CharacterController ally = ctx.Get("ally"), hero = ctx.Get("hero");
                        ally.ApplyPoison(Venom);
                        hero.ApplyPoison(Venom);
                        ActivePoison pa = ally.ActivePoisons.LastOrDefault(), ph = hero.ActivePoisons.LastOrDefault();
                        return pa != null && ph != null && pa.RoundsUntilSecondary == 10 && ph.RoundsUntilSecondary == 10;
                    }),
                    Step.Attack("orc"), Step.EndTurn())
                .Expect("The exposure step ran", Expect.AssertsPass())
                .Expect("The game declares the victory itself", Expect.Outcome(Outcome.Victory))
                .Expect("No secondary save before the combat end", v =>
                {
                    List<TraceEvent> early = v.Log("makes secondary save vs " + VenomName);
                    return early.Count == 0 ? ExpectResult.Pass("none in the trace") : ExpectResult.Fail(early.Count + " in the fight", early[0].Seq);
                })
                .Expect("After CombatOver both secondaries resolve, the hero's then the bystander's (initiative order, not exposure order), before the loot window (delegated RAW reading 2026-11-16)", v =>
                {
                    AfterCombatEndWatch w = AfterCombatEndWatch.Stop();
                    if (w == null) return ExpectResult.Inconclusive("no watcher (the hero's first step did not run)");
                    if (!w.SawCombatOver) return ExpectResult.Fail("the watcher saw no CombatOver phase");
                    var saves = w.Logs.Where(l => l.Text.Contains("makes secondary save vs " + VenomName)).ToList();
                    string seen = string.Join(" | ", saves.Select(l => l.Text + (l.LootOpen ? " [loot open]" : "")));
                    if (saves.Count != 2) return ExpectResult.Fail(saves.Count + " secondary saves after the end: " + seen);
                    if (!saves[0].Text.Contains("Poisoned Hero") || !saves[1].Text.Contains("Bystander"))
                        return ExpectResult.Fail("order: " + seen);
                    if (saves.Any(l => l.LootOpen)) return ExpectResult.Fail("a save came with the loot window open: " + seen);
                    int fails = w.Logs.Count(l => l.Text.Contains("Secondary save: d20(2)") && l.Text.Contains("FAIL"));
                    return fails == 2 ? ExpectResult.Pass(seen) : ExpectResult.Fail(fails + " forced-fail lines; " + seen);
                })
                .Build();
        }

        // ── Step 3: tokens render through a mirror view (presentation only) ─────────────

        /// <summary>
        /// Every combatant renders through its TokenView (UI/Presentation/TokenView.cs) in a real fight: its own root
        /// GameObject, never a CharacterController (area tracking and the clean check find only the models), the model's
        /// renderer not drawn, and the view at the model's square and sprite after the hero's move. Presentation only: the
        /// fight's trace is that of any two-round walk, and the clean check after the job checks every view again. The
        /// comparisons follow the view's own copy (SyncFromModel, what its LateUpdate runs), so they check the wiring and
        /// the copy, not frame timing: grouped resolution (step 4) and run-ahead views (step 9) may run a move and the
        /// next turn in one frame, and step 9's Held mode must revisit this scenario and the clean check's view sync.
        /// </summary>
        private static ScenarioDef TokenViewMirrorsTokens()
        {
            return Rules("rules/token-view-mirrors-tokens", "Every token renders through its mirror view, which follows its model (grouped AI turns step 3)")
                .Covers("grouped AI turns step 3", "CRE-046", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Viewed Hero", 3)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 17, 10, Control.Idle)
                .Initiative("hero", "orc")
                .Turn("hero", 1,
                    Step.Assert("Each combatant has its own view, not a controller, and only the view is drawn", ctx =>
                        ViewsMirror(ctx, "round 1", "hero", "orc")),
                    Step.Move(12, 10),
                    Step.EndTurn())
                .Turn("hero", 2,
                    Step.Assert("After the move the hero's view stands on the hero's new square", ctx =>
                        ViewsMirror(ctx, "round 2", "hero", "orc")
                        && (ctx.Get("hero").View.Body.position - ctx.Get("hero").transform.position).sqrMagnitude < 1e-8f
                        && ctx.Get("hero").GridPosition == new Vector2Int(12, 10)),
                    Step.EndTurn())
                .Expect("The view checks hold", Expect.AssertsPass())
                .Build();
        }

        private static bool ViewsMirror(ScenarioContext ctx, string when, params string[] names)
        {
            var problems = new List<string>();
            foreach (string name in names)
            {
                CharacterController cc = ctx.Get(name);
                TokenView view = cc != null ? cc.View : null;
                SpriteRenderer model = cc != null ? cc.GetComponent<SpriteRenderer>() : null;
                if (view == null) { problems.Add(name + ": no view"); continue; }
                view.SyncFromModel();
                if (view.gameObject == cc.gameObject || view.transform.parent != null) problems.Add(name + ": the view is not its own root");
                if (view.GetComponentInChildren<CharacterController>(true) != null) problems.Add(name + ": the view carries a controller");
                if (model == null || !model.forceRenderingOff) problems.Add(name + ": the model renderer is drawn");
                if (view.Renderer == null || view.Renderer.forceRenderingOff) problems.Add(name + ": the view renderer is not drawn");
                if (view.Renderer != null && model != null && view.Renderer.sprite != model.sprite) problems.Add(name + ": sprite differs");
                if (view.Body.gameObject.activeSelf != cc.gameObject.activeInHierarchy) problems.Add(name + ": active state differs");
                if ((view.Body.position - cc.transform.position).sqrMagnitude > 1e-8f) problems.Add(name + ": position " + view.Body.position + " vs " + cc.transform.position);
                if (cc.transform.childCount != 0) problems.Add(name + ": " + cc.transform.childCount + " objects under the model");
            }
            ctx.Note("token views " + when + ": " + (problems.Count == 0 ? "ok" : string.Join("; ", problems)));
            return problems.Count == 0;
        }

        /// <summary>
        /// Records the combat log lines that follow the CombatOver phase change (the trace closes at that decision), each
        /// with whether the loot window was already waiting. Chains the trace's PhaseChanged and CombatLog hooks; Stop puts
        /// them back. The scenario's OnSetup stops a watcher an aborted job left behind.
        /// </summary>
        private sealed class AfterCombatEndWatch
        {
            public struct Line
            {
                public string Text;
                public bool LootOpen;
            }

            private static AfterCombatEndWatch _current;

            public readonly List<Line> Logs = new List<Line>();
            public bool SawCombatOver;

            private Action<GameManager.TurnPhase, GameManager.TurnPhase> _prevPhase, _phase;
            private Action<string> _prevLog, _log;

            public static void Start()
            {
                Stop();
                var w = new AfterCombatEndWatch();
                w._prevPhase = ScenarioHooks.PhaseChanged;
                w._prevLog = ScenarioHooks.CombatLog;
                w._phase = ScenarioHooks.Safe<GameManager.TurnPhase, GameManager.TurnPhase>("poison-end-watch.phase", (from, to) =>
                {
                    w._prevPhase?.Invoke(from, to);
                    if (to == GameManager.TurnPhase.CombatOver)
                        w.SawCombatOver = true;
                });
                w._log = ScenarioHooks.Safe<string>("poison-end-watch.log", text =>
                {
                    w._prevLog?.Invoke(text);
                    if (!w.SawCombatOver)
                        return;
                    GameManager gm = GameManager.Instance;
                    bool loot = gm != null && (gm.WaitingForLootCollection || (gm.LootCollectionUI != null && gm.LootCollectionUI.IsOpen));
                    w.Logs.Add(new Line { Text = text ?? "", LootOpen = loot });
                });
                ScenarioHooks.PhaseChanged = w._phase;
                ScenarioHooks.CombatLog = w._log;
                _current = w;
            }

            public static AfterCombatEndWatch Stop()
            {
                AfterCombatEndWatch w = _current;
                _current = null;
                if (w == null)
                    return null;
                if (ScenarioHooks.PhaseChanged == w._phase) ScenarioHooks.PhaseChanged = w._prevPhase;
                if (ScenarioHooks.CombatLog == w._log) ScenarioHooks.CombatLog = w._prevLog;
                return w;
            }
        }
    }
}
#endif
