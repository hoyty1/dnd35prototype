#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpellNames = DND35e.Identifiers.SpellNames;

namespace Tests.Scenarios
{
    /// <summary>
    /// Grouped AI turns (docs/designs/grouped_ai_turns.md). Step 2 (CMB-007): a poison's secondary save comes 10 rounds
    /// after exposure (DMG p.296-297: 1 minute for every type, ingested ones included since CMB-307; PHB p.138: 10 rounds), at the initiative count of the creature whose turn it was
    /// at exposure, whatever the real time; one still pending when the fight ends resolves at combat end, in initiative
    /// order, after CombatOver and before the loot window (delegated RAW reading 2026-11-16). Step 3: every token renders
    /// through its mirror view (TokenView), which follows its model. Step 4 (CRE-312): a summon slain or expired leaves the
    /// grid, NPCs, the summon lists and the initiative order at once, its despawn line before the next creature's turn,
    /// while its view plays the fade; a summon slain on its own turn does not cost the next creature its turn; the Lesser
    /// Globe is centred on the caster's grid square when cast, not its token, and stays there (PHB p.236, SPL-335).
    /// CRE-311: a summon that expires while it grapples ends the grapple for its partner in the despawn itself, before the
    /// partner's turn starts (PHB p.173; the partner freed, a delegated RAW reading of 2026-11-25, PHB p.309).
    /// Step 5 (CMB-308): a forced PC turn's grapple escape takes the AI decider's escape square and opens no square selection
    /// (batch 13 C7). Step 6: the AI speed factor follows the turn kind: Ai on an NPC's, a Scripted ally's and a forced PC
    /// turn, Player on a player-run turn from its menu on, through its attack hand-off (C2); the trace is the same at any
    /// aiSpeed= (the scenario notes no speed).
    /// </summary>
    public static class GroupedTurnScenarios
    {
        private const string Venom = "medium_spider_poison"; // MM p.289: Medium monstrous spider, injury, Fort DC 12, 1d3 Str / 1d3 Str
        private const string VenomName = "Medium Spider Venom";
        private const string Ingested = "arsenic"; // DMG p.296-297: an ingested poison (PoisonType.Ingested)
        private const string IngestedName = "Arsenic";

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("poison-secondary-after-ten-rounds", PoisonSecondaryAfterTenRounds);
            yield return S("poison-ingested-secondary-after-ten-rounds", PoisonIngestedSecondaryAfterTenRounds);
            yield return S("poison-secondary-pending-at-combat-end", PoisonSecondaryPendingAtCombatEnd);
            yield return S("token-view-mirrors-tokens", TokenViewMirrorsTokens);
            yield return S("summon-slain-leaves-initiative-at-once", SummonSlainLeavesInitiativeAtOnce);
            yield return S("summon-expires-leaves-at-once", SummonExpiresLeavesAtOnce);
            yield return S("summon-expiry-ends-grapple-at-once", SummonExpiryEndsGrappleAtOnce);
            yield return S("summon-slain-on-own-turn-next-still-acts", SummonSlainOnOwnTurnNextStillActs);
            yield return S("summon-slain-off-its-move-next-still-acts", SummonSlainOffItsMoveNextStillActs);
            yield return S("lesser-globe-stays-put", LesserGlobeStaysPut);
            yield return S("panicked-grappled-pc-escape-square-ai", PanickedGrappledPcEscapeSquareAi);
            yield return S("dismiss-summon-caster-standard", DismissSummonCasterStandard);
            yield return S("ai-speed-pacing-kind-follows-turns", AiSpeedPacingKindFollowsTurns);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("GroupedTurnScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        private static string SecondaryLine(string name, string poisonName = VenomName) => "^☣ " + name + " makes secondary save vs " + poisonName;

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
        /// CMB-307 (owner ruling 2026-11-24, grouped AI turns Q6): an ingested poison's secondary save also comes 1 minute,
        /// 10 rounds, after exposure (DMG p.296-297 give every delivery type the same 1 minute; PHB p.138), not the 100
        /// rounds (10 minutes) the game used before. The orc's (Scripted) step makes the hero swallow arsenic on the orc's
        /// turn of round 1, a scripted exposure (no routine in play delivers an ingested poison); the secondary save rolls at
        /// the orc's count of round 11, after the hero's round-11 turn and before the orc's.
        /// </summary>
        private static ScenarioDef PoisonIngestedSecondaryAfterTenRounds()
        {
            return Rules("rules/poison-ingested-secondary-after-ten-rounds", "An ingested poison's secondary save also comes 10 rounds after exposure, not 100 (DMG p.296-297, PHB p.138; CMB-307)")
                .Covers("CMB-307", "CMB-007", "DMG p.296", "DMG p.297", "PHB p.138", "PC_NPC_PARITY")
                .MaxRounds(11)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Poisoned Hero", 3)), 10, 10, Control.Idle)
                .Npc("orc", "orc_warrior", 15, 10, Control.Scripted)
                .Tweak("hero", Sturdy)
                .Tweak("orc", Sturdy)
                .Initiative("hero", "orc")
                .Turn("orc", 1,
                    Step.Assert("The hero swallows arsenic on the orc's turn: an ingested poison, 10 rounds to go, anchored to the orc's count", ctx =>
                    {
                        CharacterController hero = ctx.Get("hero");
                        hero.ApplyPoison(Ingested);
                        ActivePoison p = hero.ActivePoisons.LastOrDefault();
                        ctx.Note("ingested exposure rounds=" + (p != null ? p.RoundsUntilSecondary : -1)
                                 + " type=" + (p != null && p.PoisonData != null ? p.PoisonData.Type.ToString() : "none"));
                        return p != null && p.PoisonData != null && p.PoisonData.Type == PoisonType.Ingested
                               && p.RoundsUntilSecondary == 10 && p.DurationAnchor == ctx.Get("orc");
                    }),
                    Step.EndTurn())
                .Turn("orc", 10,
                    Step.Assert("At the orc's turn of round 10 one round is left", ctx =>
                    {
                        ActivePoison p = ctx.Get("hero").ActivePoisons.LastOrDefault();
                        ctx.Note("ingested round 10 rounds=" + (p != null ? p.RoundsUntilSecondary : -1));
                        return p != null && !p.SecondaryResolved && p.RoundsUntilSecondary == 1;
                    }),
                    Step.EndTurn())
                .Expect("The exposure and round-10 checks hold", Expect.AssertsPass())
                .Expect("Exactly one secondary save, in round 11 (DMG p.296-297: 1 minute for every type = 10 rounds, PHB p.138)", v =>
                {
                    List<TraceEvent> lines = v.Log(SecondaryLine("Poisoned Hero", IngestedName));
                    if (lines.Count != 1)
                        return ExpectResult.Fail(lines.Count + " secondary save lines (rounds " + string.Join(",", lines.Select(e => e.Round)) + ")", lines.Select(e => e.Seq).ToArray());
                    return lines[0].Round == 11
                        ? ExpectResult.Pass(lines[0].Str("text"), lines[0].Seq)
                        : ExpectResult.Fail("secondary save in round " + lines[0].Round, lines[0].Seq);
                })
                .Expect("It rolls at the orc's count: after the hero's round-11 turn, before the orc's (CMB-006)", v =>
                {
                    TraceEvent line = v.Log(SecondaryLine("Poisoned Hero", IngestedName)).FirstOrDefault();
                    TraceEvent hero = v.TurnsOf("hero").FirstOrDefault(e => e.Round == 11);
                    TraceEvent orc = v.TurnsOf("orc").FirstOrDefault(e => e.Round == 11);
                    if (line == null || hero == null || orc == null)
                        return ExpectResult.Fail("missing: line " + (line != null) + ", hero " + (hero != null) + ", orc " + (orc != null));
                    return v.Before(hero.Seq, line.Seq) && v.Before(line.Seq, orc.Seq)
                        ? ExpectResult.Pass("hero #" + hero.Seq + ", save #" + line.Seq + ", orc #" + orc.Seq, line.Seq)
                        : ExpectResult.Fail("hero #" + hero.Seq + ", save #" + line.Seq + ", orc #" + orc.Seq, line.Seq);
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

        // ── Step 4: a summon leaves play at once (CRE-312) ─────────────

        private const string SlainLine = @"is slain! \(Summoning ended early\)";
        private const string ExpiredLine = "disappears as the summoning ends";

        private static SummonMonsterOption CelestialDog()
            => new SummonMonsterOption { DisplayName = "Dog", NpcDefinitionId = "dog", TemplateId = "celestial", SummonedCreatureAlignment = Alignment.LawfulGood };

        private static SummonMonsterOption FiendishDireRat()
            => new SummonMonsterOption { DisplayName = "Dire Rat", NpcDefinitionId = "dire_rat", TemplateId = "fiendish", SummonedCreatureAlignment = Alignment.LawfulEvil };

        /// <summary>
        /// Brings a summon of <paramref name="casterKey"/> into the fight at (x, y) the way the game's summon spells do
        /// (GameManager.Harness_SummonIntoFight: the game's spawn, the initiative order right after the caster, the active
        /// summons for <paramref name="rounds"/> rounds) and adopts it as <paramref name="key"/>; <paramref name="hp"/> above 0
        /// caps its hit points.
        /// </summary>
        private static bool SummonAndAdopt(ScenarioContext ctx, string casterKey, string key, int x, int y, SummonMonsterOption option, int rounds, int hp = 0, string spellId = null)
        {
            CharacterController summon = ctx.Gm.Harness_SummonIntoFight(ctx.Get(casterKey), new Vector2Int(x, y), option, rounds, spellId);
            if (summon == null)
            {
                ctx.Note(key + ": the summon spawn failed");
                return false;
            }
            if (hp > 0)
                summon.Stats.CurrentHP = Mathf.Min(hp, summon.Stats.CurrentHP);
            bool adopted = ctx.Adopt(key, summon);
            ctx.Note(key + " summoned: " + summon.Stats.CharacterName + " at " + summon.GridPosition + ", hp " + summon.Stats.CurrentHP
                     + ", controllable " + summon.IsControllable + ", adopted " + adopted);
            return adopted;
        }

        private static bool HasCond(JsonObj snap, string cond) => snap != null && snap.Get("conds") is List<string> l && l.Contains(cond);

        private static TurnService Turns(ScenarioContext ctx) => ctx.Gm != null ? ctx.Gm.GetComponent<TurnService>() : null;

        /// <summary>What still holds <paramref name="summon"/> in play (null: nothing): NPCs, the initiative order, the active summons, its square.</summary>
        private static string StillInPlay(ScenarioContext ctx, CharacterController summon, Vector2Int square)
        {
            var held = new List<string>();
            if (ctx.Gm.NPCs.Any(n => ReferenceEquals(n, summon))) held.Add("NPCs");
            TurnService ts = Turns(ctx);
            if (ts != null && ts.InitiativeOrder.Any(e => e != null && ReferenceEquals(e.Character, summon))) held.Add("the initiative order");
            if (ctx.Gm.Harness_IsActiveSummon(summon)) held.Add("the active summons");
            SquareCell cell = ctx.Gm.Grid != null ? ctx.Gm.Grid.GetCell(square) : null;
            if (cell != null && cell.ContainsOccupant(summon)) held.Add("its square " + square);
            return held.Count == 0 ? null : string.Join(", ", held);
        }

        /// <summary>The first turn_start after <paramref name="seq"/>, or null.</summary>
        private static TraceEvent NextTurnStart(TraceView v, int seq) => v.Of("turn_start").FirstOrDefault(e => e.Seq > seq);

        /// <summary>
        /// CRE-312. The summoner (Scripted) brings a celestial dog (MM p.271, Summon Monster I) into the fight in round 1,
        /// right after itself in the order; the dog takes its round-1 turn. In round 2, on the summoner's own turn, a
        /// blast kills the dog: before the next statement it has left NPCs, the initiative order, the active summons and
        /// its square, is marked out of play, and its view plays the fade (GameManager.DespawnSummonWithEffect runs every
        /// rules change at its call; before the step they waited 0.45 s on the tween). Its despawn line comes before the
        /// orc's round-2 turn, which follows the summoner's (the dog's slot is gone, nobody is skipped).
        /// </summary>
        private static ScenarioDef SummonSlainLeavesInitiativeAtOnce()
        {
            Vector2Int square = new Vector2Int(6, 10);
            return Rules("rules/summon-slain-leaves-initiative-at-once", "A slain summon leaves NPCs, the initiative order and the grid at once, its despawn line before the next turn (CRE-312)")
                .Covers("CRE-312", "grouped AI turns step 4", "AI-015", "PHB p.285", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("summoner", ActorSource.Stats(() => RulesScenarios.Fighter("Summoner", 3)), 5, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 16, 10, Control.Scripted)
                .Adopted("pet", Control.Scripted, CharacterTeam.Player)
                .Tweak("summoner", Sturdy)
                .Tweak("orc", Sturdy)
                .Initiative("summoner", "orc")
                .Turn("summoner", 1, Step.Assert("Summon the celestial dog next to the summoner", ctx =>
                    SummonAndAdopt(ctx, "summoner", "pet", square.x, square.y, CelestialDog(), 10)))
                .Turn("pet", 1, Step.Pass())
                .Turn("orc", 1, Step.Pass())
                .Turn("orc", 2, Step.Pass())
                .Turn("summoner", 2,
                    Step.Assert("The slain dog is out of play before the next statement; its view lingers and fades", ctx =>
                    {
                        CharacterController pet = ctx.Get("pet");
                        TokenView view = pet != null ? pet.View : null;
                        TurnService ts = Turns(ctx);
                        if (pet == null || ts == null)
                            return false;
                        ctx.Gm.DealDamage(pet, 200, DamagePackets.Spell("Scenario blast", DamageType.Untyped));
                        string held = StillInPlay(ctx, pet, square);
                        bool current = ReferenceEquals(ctx.Gm.CurrentCharacter, ctx.Get("summoner")) && ts.CurrentInitiativeIndex == 0;
                        bool fading = view != null && view.IsDespawning && view.Model == null;
                        ctx.Note("slain pet: held by " + (held ?? "nothing") + "; removed " + pet.IsRemovedFromPlay
                                 + "; summoner still current at index 0 " + current + "; view fading " + fading + "; order " + ts.GetInitiativeOrderString());
                        return held == null && pet.IsRemovedFromPlay && current && fading;
                    }))
                .Expect("The kill step's checks hold", Expect.AssertsPass())
                .Expect("One despawn line, in round 2, before the orc's round-2 turn, which comes next (CRE-312)", v =>
                {
                    List<TraceEvent> lines = v.Log(SlainLine);
                    if (lines.Count != 1)
                        return ExpectResult.Fail(lines.Count + " despawn lines", lines.Select(e => e.Seq).ToArray());
                    TraceEvent next = NextTurnStart(v, lines[0].Seq);
                    TraceEvent orc = v.TurnsOf("orc").FirstOrDefault(e => e.Round == 2);
                    if (orc == null || next == null)
                        return ExpectResult.Fail("no turn after the despawn line (orc round 2 " + (orc != null) + ")", lines[0].Seq);
                    return lines[0].Round == 2 && next.Seq == orc.Seq
                        ? ExpectResult.Pass("despawn #" + lines[0].Seq + ", then the orc's turn #" + orc.Seq, lines[0].Seq, orc.Seq)
                        : ExpectResult.Fail("despawn #" + lines[0].Seq + " in round " + lines[0].Round + ", next turn #" + next.Seq + " (" + next.Str("actor") + ")", lines[0].Seq, next.Seq);
                })
                .Expect("The dog took its round-1 turn and none after it was slain", v =>
                {
                    List<TraceEvent> turns = v.TurnsOf("pet");
                    return turns.Count == 1 && turns[0].Round == 1
                        ? ExpectResult.Pass("one turn, round 1", turns[0].Seq)
                        : ExpectResult.Fail(turns.Count + " turns (rounds " + string.Join(",", turns.Select(e => e.Round)) + ")", turns.Select(e => e.Seq).ToArray());
                })
                .Build();
        }

        /// <summary>
        /// CRE-312. The summoner (Scripted) brings a celestial dog into the fight for 1 round. Its duration runs out at the
        /// round boundary (GameManager.TickSummonDurations): the expiry line comes before the next turn's start, the dog
        /// takes no turn after it, and on the summoner's next turn it is out of NPCs, the initiative order, the active
        /// summons and its square. Before the step the line and the removal came after the 0.45 s tween, so with fast
        /// pacing after the next creature's turn had begun.
        /// </summary>
        private static ScenarioDef SummonExpiresLeavesAtOnce()
        {
            Vector2Int square = new Vector2Int(6, 10);
            Step goneIfExpired = Step.Assert("An expired dog is out of play by the summoner's next turn", ctx =>
            {
                CharacterController pet = ctx.Get("pet");
                // By the summoner's next turn Unity has usually destroyed the expired dog (its Destroy runs at the end of the
                // expiry frame), so `pet == null` is true here: test the reference, then read only managed state.
                if (ReferenceEquals(pet, null))
                    return false;
                if (ctx.Gm.Harness_IsActiveSummon(pet))
                {
                    ctx.Note("pet still summoned in round " + ctx.Gm.CurrentRound);
                    return true;
                }
                string held = StillInPlay(ctx, pet, square);
                ctx.Note("pet expired by round " + ctx.Gm.CurrentRound + ": held by " + (held ?? "nothing") + ", removed " + pet.IsRemovedFromPlay
                         + ", destroyed " + (pet == null));
                return held == null && pet.IsRemovedFromPlay;
            });
            return Rules("rules/summon-expires-leaves-at-once", "An expired summon leaves play at the round boundary, its line before the next turn (CRE-312)")
                .Covers("CRE-312", "grouped AI turns step 4", "PHB p.285", "PC_NPC_PARITY")
                .MaxRounds(3)
                .Pc("summoner", ActorSource.Stats(() => RulesScenarios.Fighter("Summoner", 3)), 5, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 16, 10, Control.Scripted)
                .Adopted("pet", Control.Scripted, CharacterTeam.Player)
                .Tweak("summoner", Sturdy)
                .Tweak("orc", Sturdy)
                .Initiative("summoner", "orc")
                .Turn("summoner", 1, Step.Assert("Summon the celestial dog for 1 round", ctx =>
                    SummonAndAdopt(ctx, "summoner", "pet", square.x, square.y, CelestialDog(), 1)))
                .Turn("summoner", 2, goneIfExpired)
                .Turn("summoner", 3, goneIfExpired)
                .Turn("pet", 1, Step.Pass())
                .Turn("pet", 2, Step.Pass())
                .Turn("pet", 3, Step.Pass())
                .Turn("orc", 1, Step.Pass())
                .Turn("orc", 2, Step.Pass())
                .Turn("orc", 3, Step.Pass())
                .Expect("The summoner's checks hold", Expect.AssertsPass())
                .Expect("One expiry line, before the next turn's start, and no turn of the dog after it (CRE-312)", v =>
                {
                    List<TraceEvent> lines = v.Log(ExpiredLine);
                    if (lines.Count != 1)
                        return ExpectResult.Fail(lines.Count + " expiry lines", lines.Select(e => e.Seq).ToArray());
                    TraceEvent next = NextTurnStart(v, lines[0].Seq);
                    TraceEvent late = v.TurnsOf("pet").FirstOrDefault(e => e.Seq > lines[0].Seq);
                    if (next == null)
                        return ExpectResult.Fail("no turn after the expiry line", lines[0].Seq);
                    if (late != null)
                        return ExpectResult.Fail("the dog took a turn (#" + late.Seq + ") after its expiry line #" + lines[0].Seq, lines[0].Seq, late.Seq);
                    return next.Str("actor") == "summoner"
                        ? ExpectResult.Pass("expiry #" + lines[0].Seq + " (round " + lines[0].Round + "), then the summoner's turn #" + next.Seq, lines[0].Seq, next.Seq)
                        : ExpectResult.Fail("expiry #" + lines[0].Seq + ", next turn #" + next.Seq + " is " + next.Str("actor"), lines[0].Seq, next.Seq);
                })
                .Expect("The dog took its round-1 turn", v =>
                    v.TurnsOf("pet").Any(e => e.Round == 1) ? ExpectResult.Pass("round 1 turn") : ExpectResult.Fail("no round-1 turn of the dog"))
                .Build();
        }

        /// <summary>
        /// CRE-311. The grapple drill orc is first in the order; the summoner (Scripted) brings a celestial dog (MM p.271)
        /// into the fight for 1 round next to it, and the dog grapples the orc on its round-1 turn (forced dice; the orc
        /// makes no attacks of opportunity). At the round-2 boundary the dog's duration runs out
        /// (GameManager.TickSummonDurations), just before the orc's turn starts, as the werewolf's in soak seed 48: a
        /// summoned creature is sent back instantly (PHB p.173), so the grapple ends for the orc (a delegated RAW reading
        /// of 2026-11-25: grappling is a struggle with an opponent, PHB p.309, and none is left) in the despawn itself:
        /// both Grappled removals come after the expiry line and before the orc's round-2 turn_start, whose snapshot shows
        /// neither creature grappled, and the orc (the AI from round 2) does nothing against the vanished dog. Before the
        /// fix the release waited for the end-of-frame CharacterController.OnDestroy, so a turn start in the despawn's frame
        /// still saw the grapple, and an AI decision in that frame could target the vanished dog. The expiry point is the
        /// round-boundary summon tick of SPL-032 (PHB p.138 would end a 1-round summon just before its summoner's count in
        /// round 2, after the orc's turn): once SPL-032 is fixed, rework the order so the expiry still falls right before
        /// the partner's turn start, the same-frame case this scenario pins.
        /// </summary>
        private static ScenarioDef SummonExpiryEndsGrappleAtOnce()
        {
            Vector2Int square = new Vector2Int(7, 10);
            return Rules("rules/summon-expiry-ends-grapple-at-once", "A summon that expires while grappling frees its partner in the despawn, before the partner's turn (PHB p.173, PHB p.309 delegated RAW reading; CRE-311)")
                .Covers("CRE-311", "CRE-312", "grouped AI turns step 9", "PHB p.173", "PHB p.309", "SPL-032", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Npc("orc", "orc_grapple_drill", 8, 10, Control.Scripted)
                .Pc("summoner", ActorSource.Stats(() => RulesScenarios.Fighter("Summoner", 3)), 5, 10, Control.Scripted)
                .Adopted("pet", Control.Scripted, CharacterTeam.Player)
                .Tweak("summoner", Sturdy)
                .Tweak("orc", c => { Sturdy(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Initiative("orc", "summoner")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", 1)
                .Force(20, 1, "Grapple check", 1)
                .Turn("orc", 1, Step.Pass())
                .AiWhenUnscripted("orc")
                .Turn("summoner", 1, Step.Assert("Summon the celestial dog next to the orc for 1 round", ctx =>
                    SummonAndAdopt(ctx, "summoner", "pet", square.x, square.y, CelestialDog(), 1, hp: 0)))
                .Turn("pet", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"))
                .Turn("summoner", 2, Step.Pass())
                .Expect("The summoner's checks hold", Expect.AssertsPass())
                .Expect("Round 1: the dog grapples the orc, both Grappled at the end of its turn", v =>
                {
                    TraceEvent m = v.Maneuvers("pet", SpecialAttackType.Grapple).FirstOrDefault();
                    if (m == null || !m.Bool("success"))
                        return ExpectResult.Fail("no successful grapple by the dog");
                    bool petGr = v.Conds("pet", CombatConditionType.Grappled).Any(e => e.Bool("added") && e.Round == 1);
                    bool orcGr = v.Conds("orc", CombatConditionType.Grappled).Any(e => e.Bool("added") && e.Round == 1);
                    return petGr && orcGr ? ExpectResult.Pass("grapple #" + m.Seq + ", both Grappled", m.Seq) : ExpectResult.Fail("Grappled added: dog " + petGr + ", orc " + orcGr, m.Seq);
                })
                .Expect("The dog expires at the round-2 boundary, right before the orc's turn (CRE-312)", v =>
                {
                    List<TraceEvent> lines = v.Log(ExpiredLine);
                    if (lines.Count != 1)
                        return ExpectResult.Fail(lines.Count + " expiry lines", lines.Select(e => e.Seq).ToArray());
                    TraceEvent next = NextTurnStart(v, lines[0].Seq);
                    if (next == null)
                        return ExpectResult.Fail("no turn after the expiry line", lines[0].Seq);
                    return lines[0].Round == 2 && next.Str("actor") == "orc"
                        ? ExpectResult.Pass("expiry #" + lines[0].Seq + " (frame " + lines[0].Frame + "), then the orc's turn #" + next.Seq + " (frame " + next.Frame + ")", lines[0].Seq, next.Seq)
                        : ExpectResult.Fail("expiry #" + lines[0].Seq + " in round " + lines[0].Round + ", next turn #" + next.Seq + " is " + next.Str("actor"), lines[0].Seq, next.Seq);
                })
                .Expect("Both Grappled conditions are removed after the expiry line and before the orc's turn starts (PHB p.173, p.309; CRE-311)", v =>
                {
                    TraceEvent line = v.Log(ExpiredLine).FirstOrDefault();
                    TraceEvent turn = v.TurnsOf("orc").FirstOrDefault(e => e.Round == 2);
                    if (line == null || turn == null)
                        return ExpectResult.Fail("expiry line " + (line != null) + ", orc round-2 turn " + (turn != null));
                    TraceEvent petOff = v.Conds("pet", CombatConditionType.Grappled).FirstOrDefault(e => !e.Bool("added"));
                    TraceEvent orcOff = v.Conds("orc", CombatConditionType.Grappled).FirstOrDefault(e => !e.Bool("added"));
                    if (petOff == null || orcOff == null)
                        return ExpectResult.Fail("Grappled removed: dog " + (petOff != null) + ", orc " + (orcOff != null), line.Seq, turn.Seq);
                    bool inWindow = petOff.Seq > line.Seq && orcOff.Seq > line.Seq && petOff.Seq < turn.Seq && orcOff.Seq < turn.Seq;
                    return inWindow
                        ? ExpectResult.Pass("expiry #" + line.Seq + ", removals #" + petOff.Seq + " and #" + orcOff.Seq + ", orc turn #" + turn.Seq, line.Seq, petOff.Seq, orcOff.Seq, turn.Seq)
                        : ExpectResult.Fail("expiry #" + line.Seq + ", removals #" + petOff.Seq + " and #" + orcOff.Seq + ", orc turn #" + turn.Seq, line.Seq, petOff.Seq, orcOff.Seq, turn.Seq);
                })
                .Expect("The orc's round-2 turn_start snapshot shows neither the orc nor the vanished dog grappled (CRE-311)", v =>
                {
                    JsonObj o = v.Snapshot("orc", 2, "orc");
                    if (o == null)
                        return ExpectResult.Fail("no orc snapshot at its round-2 turn");
                    JsonObj d = v.Snapshot("pet", 2, "orc"); // the dog may or may not still be listed
                    bool orcGr = (o.Get("gr") is bool g && g) || HasCond(o, "Grappled");
                    bool petGr = d != null && ((d.Get("gr") is bool h && h) || HasCond(d, "Grappled"));
                    return !orcGr && !petGr
                        ? ExpectResult.Pass("orc free" + (d != null ? ", dog listed and free" : ", dog not listed"))
                        : ExpectResult.Fail("orc grappled " + orcGr + ", dog grappled " + petGr);
                })
                .Expect("The orc's round-2 turn is an AI turn and nothing in it targets the vanished dog", v =>
                {
                    TraceEvent t = v.TurnsOf("orc").FirstOrDefault(e => e.Round == 2);
                    if (t == null)
                        return ExpectResult.Fail("no orc turn in round 2");
                    if (t.Str("controller") != "ai")
                        return ExpectResult.Fail("controller " + t.Str("controller"), t.Seq);
                    var against = v.Attacks("orc", "pet").Where(e => e.Seq > t.Seq).Select(e => e.Seq)
                        .Concat(v.Maneuvers("orc").Where(e => e.Seq > t.Seq && e.Str("target") == "pet").Select(e => e.Seq)).ToArray();
                    return against.Length == 0
                        ? ExpectResult.Pass("ai turn #" + t.Seq + ", no attack or maneuver against the dog", t.Seq)
                        : ExpectResult.Fail(against.Length + " attack or maneuver event(s) against the vanished dog", against);
                })
                .Expect("The dog took its round-1 turn and none after it expired", v =>
                {
                    List<TraceEvent> turns = v.TurnsOf("pet");
                    return turns.Count == 1 && turns[0].Round == 1
                        ? ExpectResult.Pass("one turn, round 1", turns[0].Seq)
                        : ExpectResult.Fail(turns.Count + " turns (rounds " + string.Join(",", turns.Select(e => e.Round)) + ")", turns.Select(e => e.Seq).ToArray());
                })
                .Build();
        }

        private static void Deadly(CharacterController c)
        {
            Sturdy(c);
            c.Stats.Bonuses.Set("scenario:sure-hit", BonusTarget.AttackRoll, BonusType.Untyped, 30, "scenario attack tweak");
            c.Stats.Bonuses.Set("scenario:sure-kill", BonusTarget.WeaponDamage, BonusType.Untyped, 30, "scenario damage tweak");
        }

        /// <summary>
        /// CRE-312, the bug the step removes: a summon slain during its own turn (an AoO on its own move) was removed from
        /// the initiative order one tween later, when the order had not advanced yet, so the advance skipped the creature
        /// after it. Now TurnService.RemoveFromInitiative marks the current turn vacated (VacatedTurnOwner) instead. The Ui
        /// summoner's celestial dog (MM p.271; player-run, the PC turn path) and the Scripted goblin's fiendish dire rat
        /// (MM p.64; the NPC turn path) each walk out of a threatened square on their round-2 turn and die to the AoO (PHB
        /// p.137; in round 1 the orc and the guard, whose turns come after the summons', are still flat-footed and make no
        /// attacks of opportunity, PHB p.137). The orc after the dog and the guard after the rat still take their turns,
        /// each after the summon's despawn line.
        /// </summary>
        private static ScenarioDef SummonSlainOnOwnTurnNextStillActs()
        {
            return Rules("rules/summon-slain-on-own-turn-next-still-acts", "A summon slain on its own turn does not cost the next creature its turn, on the PC and NPC paths (CRE-312)")
                .Covers("CRE-312", "grouped AI turns step 4", "PHB p.137", "PHB p.285", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("summoner", ActorSource.Stats(() => RulesScenarios.Fighter("Summoner", 3)), 3, 3, Control.Ui)
                .Npc("orc", "orc_warrior", 8, 8, Control.Scripted)
                .Npc("goblin", "goblin", 16, 3, Control.Scripted)
                .Pc("guard", ActorSource.Stats(() => RulesScenarios.Fighter("Guard", 3)), 8, 14, Control.Scripted)
                .Adopted("dog", Control.Ui, CharacterTeam.Player)
                .Adopted("rat", Control.Scripted, CharacterTeam.Enemy)
                .Tweak("summoner", Sturdy)
                .Tweak("goblin", Sturdy)
                .Tweak("orc", Deadly)
                .Tweak("guard", Deadly)
                .Initiative("summoner", "orc", "goblin", "guard")
                .Turn("summoner", 1, Step.Assert("The summoner's dog appears by the orc, the goblin's dire rat by the guard, each with 1 hp", ctx =>
                {
                    bool dog = SummonAndAdopt(ctx, "summoner", "dog", 9, 8, CelestialDog(), 10, 1);
                    bool rat = SummonAndAdopt(ctx, "goblin", "rat", 9, 14, FiendishDireRat(), 10, 1);
                    return dog && rat && ctx.Get("dog").IsControllable && !ctx.Get("rat").IsControllable;
                }))
                .Turn("dog", 1, Step.Pass())
                .Turn("orc", 1, Step.Pass())
                .Turn("goblin", 1, Step.Pass())
                .Turn("rat", 1, Step.Pass())
                .Turn("guard", 1, Step.Pass())
                .Turn("summoner", 2, Step.Pass())
                .Turn("dog", 2, Step.Move(12, 8))
                .Turn("orc", 2, Step.Pass())
                .Turn("goblin", 2, Step.Pass())
                .Turn("rat", 2, Step.Move(12, 14))
                .Turn("guard", 2, Step.Pass())
                .Expect("The summon step ran", Expect.AssertsPass())
                .Expect("The dog's turn is player-run (the PC path), the rat's scripted (the NPC path)",
                    Expect.All(Expect.Controller("dog", "ui"), Expect.Controller("rat", "scripted")))
                .Expect("Each summon provokes and takes the AoO on its own move (PHB p.137)", v =>
                {
                    TraceEvent dog = v.AoOs("orc", "dog", "movement").FirstOrDefault();
                    TraceEvent rat = v.AoOs("guard", "rat", "movement").FirstOrDefault();
                    return dog != null && rat != null && dog.Bool("hit") && rat.Bool("hit")
                        ? ExpectResult.Pass("orc on the dog #" + dog.Seq + ", guard on the rat #" + rat.Seq, dog.Seq, rat.Seq)
                        : ExpectResult.Fail("orc on the dog " + (dog != null ? "#" + dog.Seq + " hit " + dog.Bool("hit") : "none")
                                            + ", guard on the rat " + (rat != null ? "#" + rat.Seq + " hit " + rat.Bool("hit") : "none"));
                })
                .Expect("Each summon's despawn line comes in its own turn, and the creature after it takes the next turn (CRE-312)", v =>
                {
                    List<TraceEvent> lines = v.Log(SlainLine);
                    TraceEvent dogTurn = v.TurnsOf("dog").FirstOrDefault(e => e.Round == 2);
                    TraceEvent ratTurn = v.TurnsOf("rat").FirstOrDefault(e => e.Round == 2);
                    if (lines.Count != 2 || dogTurn == null || ratTurn == null)
                        return ExpectResult.Fail(lines.Count + " despawn lines; dog turn " + (dogTurn != null) + ", rat turn " + (ratTurn != null), lines.Select(e => e.Seq).ToArray());
                    TraceEvent afterDog = NextTurnStart(v, lines[0].Seq), afterRat = NextTurnStart(v, lines[1].Seq);
                    bool ok = v.Before(dogTurn.Seq, lines[0].Seq) && afterDog != null && afterDog.Str("actor") == "orc"
                              && v.Before(ratTurn.Seq, lines[1].Seq) && afterRat != null && afterRat.Str("actor") == "guard";
                    string detail = "dog #" + dogTurn.Seq + ", despawn #" + lines[0].Seq + ", next " + (afterDog != null ? afterDog.Str("actor") + " #" + afterDog.Seq : "none")
                                    + "; rat #" + ratTurn.Seq + ", despawn #" + lines[1].Seq + ", next " + (afterRat != null ? afterRat.Str("actor") + " #" + afterRat.Seq : "none");
                    return ok ? ExpectResult.Pass(detail, lines[0].Seq, lines[1].Seq) : ExpectResult.Fail(detail, lines[0].Seq, lines[1].Seq);
                })
                .Expect("Every creature of the order takes its turn once in round 1 and in round 2: summoner, dog, orc, goblin, rat, guard", v =>
                {
                    string one = string.Join(" ", v.Of("turn_start").Where(e => e.Round == 1).Select(e => e.Str("actor")));
                    string two = string.Join(" ", v.Of("turn_start").Where(e => e.Round == 2).Select(e => e.Str("actor")));
                    const string want = "summoner dog orc goblin rat guard";
                    return one == want && two == want ? ExpectResult.Pass("round 1: " + one + "; round 2: " + two) : ExpectResult.Fail("round 1: " + one + "; round 2: " + two);
                })
                .Build();
        }

        /// <summary>
        /// CRE-312, the player-run tails other than the catalog move (review of grouped AI turns step 4): a player-run
        /// summon that leaves play during its own turn has a null CurrentCharacter, so ActivePC is null and the PC tails
        /// that return to the action panel used to leave the turn open (ShowActionChoices returned at once, and End Turn
        /// refuses without a player turn). The Ui summoner's two celestial dogs (MM p.271) each die on their own round-1 turn.
        /// The first bites the orc, whose warm Fire Shield (PHB p.230; caster level 15: 1d6+15 fire, no save) slays it
        /// (OnAttackResolved's HandleSummonDeathCleanup(attacker), then AfterAttackDelay). The second is slain from an
        /// Assert step (HandleSummonDeathCleanup, as a grapple-escape move's or a provoking cast's AoO would slay it) and the
        /// step then runs the PC tail every such path ends in, ShowActionChoices. Each turn ends by reference
        /// (GameManager.IsVacatedPlayerTurn), and the creature after each dog still takes its turn.
        /// </summary>
        private static ScenarioDef SummonSlainOffItsMoveNextStillActs()
        {
            return Rules("rules/summon-slain-off-its-move-next-still-acts", "A player-run summon slain on its own turn off the move path still ends its turn, and the next creature acts (CRE-312)")
                .Covers("CRE-312", "grouped AI turns step 4", "PHB p.230", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("summoner", ActorSource.Stats(() => RulesScenarios.Fighter("Summoner", 3)), 3, 3, Control.Ui)
                .Npc("orc", "orc_warrior", 9, 8, Control.Scripted)
                .Adopted("dog", Control.Ui, CharacterTeam.Player)
                .Adopted("pup", Control.Ui, CharacterTeam.Player)
                .Tweak("summoner", Sturdy)
                .Tweak("orc", c => { Sturdy(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Initiative("summoner", "orc")
                .Turn("summoner", 1, Step.Assert("Two player-run dogs appear with 1 hp, one by the orc; the orc wears a warm Fire Shield of caster level 15", ctx =>
                {
                    bool dog = SummonAndAdopt(ctx, "summoner", "dog", 10, 8, CelestialDog(), 10, 1);
                    bool pup = SummonAndAdopt(ctx, "summoner", "pup", 3, 5, CelestialDog(), 10, 1);
                    if (!dog || !pup)
                        return false;
                    ctx.Get("dog").Stats.Bonuses.Set("scenario:sure-hit", BonusTarget.AttackRoll, BonusType.Untyped, 30, "scenario attack tweak");
                    CharacterStats orc = ctx.Get("orc").Stats;
                    orc.FireShieldActive = true;
                    orc.FireShieldIsWarm = true;
                    orc.FireShieldCasterLevel = 15;
                    orc.FireShieldDurationRounds = 10;
                    MeleeReactionService.Register(new FireShieldReactionEffect(ctx.Get("orc")));
                    return ctx.Get("dog").IsControllable && ctx.Get("pup").IsControllable;
                }), Step.EndTurn())
                .Turn("dog", 1, Step.Attack("orc"))
                .Turn("pup", 1, Step.Assert("The pup is slain on its own turn and the PC tail's ShowActionChoices ends the vacated turn", ctx =>
                {
                    GameManager gm = ctx.Gm;
                    CharacterController pup = ctx.Get("pup");
                    TurnService turns = Turns(ctx);
                    const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    System.Reflection.MethodInfo cleanup = typeof(GameManager).GetMethod("HandleSummonDeathCleanup", flags);
                    System.Reflection.MethodInfo choices = typeof(GameManager).GetMethod("ShowActionChoices", flags);
                    if (turns == null || cleanup == null || choices == null)
                    {
                        ctx.Note("pup: a reflected member is missing (TST-006)");
                        return false;
                    }
                    pup.Stats.CurrentHP = -10;
                    cleanup.Invoke(gm, new object[] { pup });
                    bool vacated = ReferenceEquals(turns.VacatedTurnOwner, pup) && gm.ActivePC == null;
                    choices.Invoke(gm, null);
                    bool ended = ReferenceEquals(turns.VacatedTurnOwner, null) && !ReferenceEquals(gm.CurrentCharacter, pup);
                    ctx.Note("pup: vacated " + vacated + ", ended by ShowActionChoices " + ended + ", now " + (gm.CurrentCharacter != null ? gm.CurrentCharacter.Stats.CharacterName : "none"));
                    return vacated && ended;
                }))
                .Turn("orc", 1, Step.Pass())
                .Expect("The setup and the pup's checks hold", Expect.AssertsPass())
                .Expect("Both dogs' turns are player-run (the PC path)", Expect.All(Expect.Controller("dog", "ui"), Expect.Controller("pup", "ui")))
                .Expect("The dog's bite hits the orc, and the shield's retribution slays it", v =>
                {
                    TraceEvent bite = v.Attacks("dog", "orc").FirstOrDefault(e => e.Bool("hit"));
                    List<TraceEvent> burned = v.Log("slain by Fire Shield retribution");
                    return bite != null && burned.Count == 1 && v.Before(bite.Seq, burned[0].Seq)
                        ? ExpectResult.Pass("bite #" + bite.Seq + ", retribution kill #" + burned[0].Seq, bite.Seq, burned[0].Seq)
                        : ExpectResult.Fail("bite " + (bite != null ? "#" + bite.Seq : "none") + ", " + burned.Count + " retribution kill lines");
                })
                .Expect("Each dog's despawn line comes in its own turn, and the creature after it takes the next turn (CRE-312)", v =>
                {
                    List<TraceEvent> lines = v.Log(SlainLine);
                    TraceEvent dogTurn = v.TurnsOf("dog").FirstOrDefault(), pupTurn = v.TurnsOf("pup").FirstOrDefault();
                    if (lines.Count != 2 || dogTurn == null || pupTurn == null)
                        return ExpectResult.Fail(lines.Count + " despawn lines; dog turn " + (dogTurn != null) + ", pup turn " + (pupTurn != null), lines.Select(e => e.Seq).ToArray());
                    TraceEvent afterDog = NextTurnStart(v, lines[0].Seq), afterPup = NextTurnStart(v, lines[1].Seq);
                    bool ok = v.Before(dogTurn.Seq, lines[0].Seq) && afterDog != null && afterDog.Str("actor") == "pup"
                              && v.Before(pupTurn.Seq, lines[1].Seq) && afterPup != null && afterPup.Str("actor") == "orc";
                    string detail = "dog #" + dogTurn.Seq + ", despawn #" + lines[0].Seq + ", next " + (afterDog != null ? afterDog.Str("actor") + " #" + afterDog.Seq : "none")
                                    + "; pup #" + pupTurn.Seq + ", despawn #" + lines[1].Seq + ", next " + (afterPup != null ? afterPup.Str("actor") + " #" + afterPup.Seq : "none");
                    return ok ? ExpectResult.Pass(detail, lines[0].Seq, lines[1].Seq) : ExpectResult.Fail(detail, lines[0].Seq, lines[1].Seq);
                })
                .Expect("Every creature of the order takes its round-1 turn once: summoner, dog, pup, orc", v =>
                {
                    string one = string.Join(" ", v.Of("turn_start").Where(e => e.Round == 1).Select(e => e.Str("actor")));
                    return one == "summoner dog pup orc" ? ExpectResult.Pass("round 1: " + one) : ExpectResult.Fail("round 1: " + one);
                })
                .Build();
        }

        private static CharacterStats Wizard(string name, int level)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Wizard",
                str: 10, dex: 12, con: 12, wis: 10, intelligence: 16, cha: 10,
                bab: 0, armorBonus: 0, shieldBonus: 0,
                damageDice: 6, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 6 * level, raceName: "Human");
            s.CharacterAlignment = Alignment.TrueNeutral;
            s.EnsureMulticlassDataInitialized();
            return s;
        }

        private static LesserGlobeOfInvulnerabilityAreaEffect GlobeOf(CharacterController caster)
        {
            foreach (LesserGlobeOfInvulnerabilityAreaEffect g in UnityEngine.Object.FindObjectsByType<LesserGlobeOfInvulnerabilityAreaEffect>(FindObjectsInactive.Exclude))
                if (g != null && g.Caster == caster)
                    return g;
            return null;
        }

        private static Vector3 SquareCentre(ScenarioContext ctx, CharacterController c)
            => ctx.Gm.Grid.GetCenteredWorldPosition(c.GridPosition, c.GetVisualSquaresOccupied());

        /// <summary>Checks the globe's centre against the caster's square while the caster's token is off it (a step tween in flight).</summary>
        private static bool GlobeOnSquareWhileTokenOff(ScenarioContext ctx, string when, Func<LesserGlobeOfInvulnerabilityAreaEffect, bool> act)
        {
            CharacterController caster = ctx.Get("caster");
            Vector3 token = caster.transform.position;
            Vector3 square = SquareCentre(ctx, caster);
            caster.transform.position = square + new Vector3(0.6f, 0.3f, 0f);
            LesserGlobeOfInvulnerabilityAreaEffect globe;
            bool ran;
            try
            {
                globe = GlobeOf(caster);
                ran = act(globe);
                globe = globe ?? GlobeOf(caster);
            }
            finally
            {
                caster.transform.position = token;
            }
            bool on = globe != null && (globe.CenterPosition - square).sqrMagnitude < 1e-8f;
            ctx.Note("globe " + when + ": ran " + ran + ", centre " + (globe != null ? globe.CenterPosition.ToString("F3") : "none") + ", square " + square.ToString("F3"));
            return ran && on;
        }

        /// <summary>
        /// The Lesser Globe (PHB p.236) is centred on its caster when cast and is immobile: the caster can leave it and come
        /// back (SPL-335, owner ruling 2026-11-24). Its centre comes from the caster's grid square
        /// (LesserGlobeOfInvulnerabilityAreaEffect.CasterCenter), not from its token, which a step tween moves at the
        /// speed of the pacing (grouped AI turns step 4): at creation the token is pushed off its square first. The caster
        /// then walks 3 squares east, out of the 10-ft radius; the globe's Update (run with the token again off its square)
        /// and round 2's OnRoundStart leave the centre on the casting square, and the caster is no longer inside it.
        /// </summary>
        private static ScenarioDef LesserGlobeStaysPut()
        {
            var castSquare = new Vector2Int(5, 10);
            return Rules("rules/lesser-globe-stays-put", "The Lesser Globe stays where it was cast, centred on the caster's square then, and the caster can walk out of it (PHB p.236; SPL-335)")
                .Covers("grouped AI turns step 4", "CRE-312", "PHB p.236", "SPL-141", "SPL-335")
                .MaxRounds(2)
                .Pc("caster", ActorSource.Stats(() => Wizard("Globe Caster", 7)), castSquare.x, castSquare.y, Control.Scripted)
                .Npc("orc", "orc_warrior", 17, 17, Control.Idle)
                .Initiative("caster", "orc")
                .Turn("caster", 1,
                    Step.Assert("Raised with the token off its square, the globe is centred on the caster's square", ctx =>
                        GlobeOnSquareWhileTokenOff(ctx, "at creation", _ =>
                        {
                            CharacterController c = ctx.Get("caster");
                            bool raised = ctx.Gm.Harness_RunSingleTargetHandler(c, c, SpellDatabase.GetSpell(SpellNames.LESSER_GLOBE_OF_INVULNERABILITY).Clone());
                            LesserGlobeOfInvulnerabilityAreaEffect g = GlobeOf(c);
                            if (g != null)
                                g.RoundsRemaining = 10;
                            return raised && g != null;
                        })),
                    Step.Move(8, 10),
                    Step.Assert("After the caster walks out, the globe's Update leaves it on the casting square and the caster is outside it", ctx =>
                    {
                        CharacterController c = ctx.Get("caster");
                        LesserGlobeOfInvulnerabilityAreaEffect g = GlobeOf(c);
                        if (c.GridPosition != new Vector2Int(8, 10) || g == null)
                            return false;
                        Vector3 token = c.transform.position;
                        c.transform.position = SquareCentre(ctx, c) + new Vector3(0.6f, 0.3f, 0f);
                        try
                        {
                            System.Reflection.MethodInfo update = typeof(LesserGlobeOfInvulnerabilityAreaEffect).GetMethod("Update",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                            if (update == null)
                                return false;
                            update.Invoke(g, null);
                        }
                        finally
                        {
                            c.transform.position = token;
                        }
                        return GlobeStaysOn(ctx, g, castSquare, "after the move") && !g.IsCharacterInArea(c);
                    }),
                    Step.EndTurn())
                .Turn("caster", 2,
                    Step.Assert("In round 2 (after OnRoundStart) the globe is still on the casting square", ctx =>
                        GlobeStaysOn(ctx, GlobeOf(ctx.Get("caster")), castSquare, "round 2")),
                    Step.EndTurn())
                .Expect("The globe centre checks hold", Expect.AssertsPass())
                .Build();
        }

        /// <summary>True when <paramref name="g"/> is centred on <paramref name="square"/>'s centre (a Medium caster's square).</summary>
        private static bool GlobeStaysOn(ScenarioContext ctx, LesserGlobeOfInvulnerabilityAreaEffect g, Vector2Int square, string when)
        {
            Vector3 centre = ctx.Gm.Grid.GetCenteredWorldPosition(square, 1);
            bool on = g != null && (g.CenterPosition - centre).sqrMagnitude < 1e-8f;
            ctx.Note("globe " + when + ": " + (g != null ? g.CenterPosition.ToString("F3") : "none") + ", casting square " + centre.ToString("F3"));
            return on;
        }

        // ── SPL-336: dismissing a summon (PHB p.176) ─────────────

        /// <summary>
        /// Dismissing a summon is its caster's standard action, within the spell's range (PHB p.176, (D) Dismissible;
        /// SPL-336, owner ruling 2026-11-24). A Scripted wizard 3 (Close range 25 ft. + 5 ft. per 2 levels = 30 ft., 6
        /// squares) has two Summon Monster I dogs: one adjacent, one 9 squares away. Its fighter ally cannot dismiss either
        /// (not their caster); the far dog is out of range; the caster dismisses the near dog through the catalog action
        /// (the shared executor, RunAIAction for a Scripted actor): the dog leaves play at once, the caster's standard
        /// action is spent, and the far dog is still summoned.
        /// </summary>
        private static ScenarioDef DismissSummonCasterStandard()
        {
            var near = new Vector2Int(6, 10);
            return Rules("rules/dismiss-summon-caster-standard", "Only the caster dismisses its summon, for a standard action, within the spell's range (PHB p.176; SPL-336)")
                .Covers("SPL-336", "PHB p.176", "PHB p.285", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("caster", ActorSource.Stats(() => Wizard("Summoner Wizard", 3)), 5, 10, Control.Scripted)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Ally", 3)), 5, 12, Control.Scripted)
                .Npc("orc", "orc_warrior", 17, 17, Control.Idle)
                .Adopted("dog", Control.Scripted, CharacterTeam.Player)
                .Adopted("far", Control.Scripted, CharacterTeam.Player)
                .Initiative("caster", "ally", "orc")
                .Turn("caster", 1,
                    Step.Assert("Summon two dogs with Summon Monster I: one adjacent, one 9 squares away", ctx =>
                        SummonAndAdopt(ctx, "caster", "dog", near.x, near.y, CelestialDog(), 10, 0, SpellNames.SUMMON_MONSTER_1)
                        && SummonAndAdopt(ctx, "caster", "far", 14, 10, CelestialDog(), 10, 0, SpellNames.SUMMON_MONSTER_1)),
                    Step.Assert("The ally is refused (not the caster); the far dog is refused (out of range); the near dog is allowed", ctx =>
                    {
                        CharacterController caster = ctx.Get("caster"), ally = ctx.Get("ally");
                        Legality byAlly = ctx.Gm.CheckDismissSummon(ally, ctx.Get("dog"));
                        Legality farDog = ctx.Gm.CheckDismissSummon(caster, ctx.Get("far"));
                        Legality nearDog = ctx.Gm.CheckDismissSummon(caster, ctx.Get("dog"));
                        ctx.Note("ally: " + byAlly.Bar + " " + byAlly.Reason + "; far: " + farDog.Bar + " " + farDog.Reason + "; near: " + nearDog.Ok);
                        return !byAlly.Ok && byAlly.Bar == LegalityBar.Target && !farDog.Ok && farDog.Bar == LegalityBar.Range && nearDog.Ok;
                    }),
                    Step.Action(ActionIds.DismissSummon, "dog"),
                    Step.Assert("The near dog left play at once, the standard action is spent, the far dog is still summoned", ctx =>
                    {
                        CharacterController caster = ctx.Get("caster"), dog = ctx.Get("dog"), far = ctx.Get("far");
                        string held = StillInPlay(ctx, dog, near);
                        bool standardSpent = caster.Actions != null && !caster.Actions.HasStandardAction;
                        bool farActive = ctx.Gm.Harness_IsActiveSummon(far);
                        ctx.Note("dismissed dog held by " + (held ?? "nothing") + "; standard spent " + standardSpent + "; far still summoned " + farActive);
                        return held == null && dog.IsRemovedFromPlay && standardSpent && farActive;
                    }),
                    Step.EndTurn())
                .Turn("ally", 1, Step.Pass())
                .Turn("far", 1, Step.Pass())
                .Expect("The dismissal checks hold", Expect.AssertsPass())
                .Build();
        }

        // ── Step 5: the escape square of a forced grappling PC (batch 13 C7) ─────────────

        private const string EscapeSquarePromptLine = @"finishes the escape by moving to an adjacent square \(PHB p\.156\): choose one";

        /// <summary>
        /// A forced PC turn is AI-decided (batch 13 C7), the escape square of its grapple escape included (PHB p.156-157;
        /// D10). The Scripted grapple-drill orc grapples the Ui hero in round 1 (it moves into the hero's square, 10,10) and
        /// panics it (the harness stand-in for a failed save). On its forced turn the panicked hero flees by escaping the
        /// grapple (FrightenedBehaviorController.Flee, GameManager.RunGrappleEscapeForFlight; checks forced) and moves into
        /// the square its AI decider picks, AIProfile.DefaultChooseGrappleEscapeSquare (fewest threats, then nearest able
        /// ally, then lowest x and y): the orc in the shared square threatens every escape square and the guard at (8,10) adds
        /// a second threat on the west ones, which only reinforces the pick; the ally distance decides it, the east squares
        /// (11,*) being 2 from the ally at (13,10), and the y tie-break gives (11,9).
        /// No square selection opens. Before the fix the escape asked ManeuverDeciders.For(hero) with no source, and the Ui
        /// decider opened the selection on the forced turn.
        /// </summary>
        private static ScenarioDef PanickedGrappledPcEscapeSquareAi()
        {
            var aiSquare = new Vector2Int(11, 9);
            return Rules("rules/panicked-grappled-pc-escape-square-ai", "A panicked grappling PC's escape square is the AI decider's, with no square prompt (PHB p.156-157, p.311; batch 13 C7)")
                .Covers("grouped AI turns step 5", "CMB-308", "D10", "C7", "PHB p.156", "PHB p.157", "PHB p.311", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6)), 10, 10, Control.Ui)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Ally", 6)), 13, 10, Control.Scripted)
                .Npc("guard", "orc_berserker", 8, 10, Control.Idle)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Tweak("ally", c => { RulesScenarios.StripOffHand(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Tweak("orc", c => { Sturdy(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Tweak("guard", c => { Sturdy(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Initiative("orc", "hero", "ally", "guard")
                .RecordDice()
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", 1)
                .Force(20, 1, "Grapple check", 1)
                .Force(20, 20, "Grapple check", 1)
                .Force(20, 1, "Grapple check", 1)
                .Turn("orc", 1, Step.Maneuver(SpecialAttackType.Grapple, "hero"),
                    Step.Assert("the orc panics the grappled hero (fixture)", ctx =>
                    {
                        CharacterController hero = ctx.Get("hero");
                        ctx.Gm.ApplyCondition(hero, CombatConditionType.Panicked, 3, ctx.Get("orc"), sourceCategory: "Harness");
                        return hero.HasCondition(CombatConditionType.Panicked) && hero.IsGrappling();
                    }))
                .Turn("ally", 1, Step.Assert("after the hero's forced turn no escape-square selection is open and the hero is free", ctx =>
                {
                    CharacterController hero = ctx.Get("hero");
                    bool open = ctx.Gm.Harness_IsEscapeSquareSelectionOpen();
                    ctx.Note("escape square: selection open " + open + ", hero at " + hero.GridPosition + ", grappling " + hero.IsGrappling());
                    return !open && !hero.IsGrappling();
                }), Step.Pass())
                .Expect("The hero is panicked and grappling at its round-1 turn", v =>
                {
                    JsonObj h = v.Snapshot("hero", 1, "hero");
                    if (h == null) return ExpectResult.Fail("no hero snapshot at its round-1 turn");
                    bool gr = h.Get("gr") is bool g && g;
                    bool panicked = v.Conds("hero", CombatConditionType.Panicked).Any(e => e.Bool("added"));
                    return gr && panicked ? ExpectResult.Pass("panicked, grappling") : ExpectResult.Fail("panicked " + panicked + ", grappling " + gr);
                })
                .Expect("No escape-square selection opens (the forced turn's decider is the AI's, batch 13 C7)", Expect.None("log",
                    e => System.Text.RegularExpressions.Regex.IsMatch(e.Str("text") ?? "", EscapeSquarePromptLine)))
                .Expect("The hero's escape move goes to the AI decider's square, (11,9)", v =>
                {
                    TraceEvent line = v.Log(@"^Hero moves to \(\d+,\d+\) \(free action after ending grapple\)").FirstOrDefault(e => e.Round == 1);
                    if (line == null) return ExpectResult.Fail("no escape move in round 1");
                    string want = "Hero moves to (" + aiSquare.x + "," + aiSquare.y + ")";
                    return (line.Str("text") ?? "").Contains(want) ? ExpectResult.Pass(want, line.Seq) : ExpectResult.Fail(line.Str("text"), line.Seq);
                })
                .Expect("The no-prompt checks hold", Expect.AssertsPass())
                .Build();
        }

        // ── Step 6: the AI speed factor follows the turn kind (C2) ─────────────

        /// <summary>
        /// The pacing kind that switches the AI speed factor on (CombatPacing.Kind, grouped AI turns step 6, C2), read at
        /// each step: Ai on the Scripted orc's turn (the NPC path) and the Scripted ally's (a party member on the NPC path);
        /// Player on the Ui hero's turn once its menu shows, and Player on every hero log line of its attack, which is the
        /// kind in force when AfterAttackDelay starts (nothing writes the kind between the attack's last line and that
        /// wait), so the hand-off wait runs at 1x. In round 2 a Ui lead acts just before the hero, so its ShowActionChoices
        /// leaves the kind at Player; the ally has panicked the hero (PHB p.311), and a log watcher started on the lead's
        /// turn must find the hero's forced flight paced as Ai from its first Ai line on (no Player line after it), which
        /// only BeginForcedPCTurn's mark can give. The notes name kinds, never the speed, so the trace is the same at any
        /// aiSpeed= (the step's equality runs).
        /// </summary>
        private static ScenarioDef AiSpeedPacingKindFollowsTurns()
        {
            var start = new Vector2Int(10, 10);
            return Rules("rules/ai-speed-pacing-kind-follows-turns", "The AI speed factor follows the turn kind: AI-decided turns, forced PC turns included, but not a player-run turn (grouped AI turns step 6, C2)")
                .Covers("grouped AI turns step 6", "C2", "Q4", "PHB p.311", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("lead", ActorSource.Stats(() => RulesScenarios.Fighter("Pacing Lead", 3)), 16, 14, Control.Ui)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Paced Hero", 3)), start.x, start.y, Control.Ui)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Paced Ally", 3)), 10, 14, Control.Scripted)
                .Tweak("lead", c => { Sturdy(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Tweak("ally", c => { Sturdy(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Tweak("orc", c => { Sturdy(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Initiative("orc", "lead", "hero", "ally")
                .OnSetup(ctx => PacingKindWatch.Stop())
                .Turn("orc", 1, KindIs(PaceKind.Ai, "the Scripted orc's turn (the NPC path)"), Step.Pass())
                .Turn("lead", 1, Step.EndTurn())
                .Turn("hero", 1,
                    KindIs(PaceKind.Player, "the Ui hero's turn once its menu shows (ShowActionChoices)"),
                    Step.Assert("watch the kind of every log line of the hero's attack", ctx =>
                    {
                        PacingKindWatch.Start();
                        return true;
                    }),
                    Step.Attack("orc"),
                    Step.Assert("every hero log line of the attack, the kind AfterAttackDelay starts under, was paced as Player", ctx =>
                    {
                        PacingKindWatch w = PacingKindWatch.Stop();
                        CharacterController hero = ctx.Get("hero");
                        List<PacingKindWatch.Line> lines = w != null ? w.Logs.Where(l => l.Actor == hero).ToList() : new List<PacingKindWatch.Line>();
                        int notPlayer = lines.Count(l => l.Kind != PaceKind.Player);
                        ctx.Note("attack: " + lines.Count + " lines, " + notPlayer + " not Player");
                        return lines.Count > 0 && notPlayer == 0;
                    }),
                    KindIs(PaceKind.Player, "the Ui hero's turn after its attack's hand-off (ShowActionChoices again)"),
                    Step.EndTurn())
                .Turn("ally", 1,
                    KindIs(PaceKind.Ai, "the Scripted ally's turn (a party member on the NPC path)"),
                    Step.Assert("the ally panics the hero (fixture)", ctx =>
                    {
                        CharacterController hero = ctx.Get("hero");
                        ctx.Gm.ApplyCondition(hero, CombatConditionType.Panicked, 3, ctx.Get("orc"), sourceCategory: "Harness");
                        return hero.HasCondition(CombatConditionType.Panicked);
                    }),
                    Step.Pass())
                .Turn("orc", 2, KindIs(PaceKind.Ai, "the orc's round-2 turn"), Step.Pass())
                .Turn("lead", 2,
                    Step.Assert("the Ui lead's round-2 turn is Player (ShowActionChoices); watch the kind of every log line", ctx =>
                    {
                        PacingKindWatch.Start();
                        return CombatPacing.Kind == PaceKind.Player;
                    }),
                    Step.EndTurn())
                .Turn("ally", 2,
                    Step.Assert("the hero's forced flight was paced as Ai from its first Ai line on, and the hero fled", ctx =>
                    {
                        PacingKindWatch w = PacingKindWatch.Stop();
                        CharacterController hero = ctx.Get("hero");
                        List<PacingKindWatch.Line> lines = w != null ? w.Logs.Where(l => l.Actor == hero).ToList() : new List<PacingKindWatch.Line>();
                        int firstAi = lines.FindIndex(l => l.Kind == PaceKind.Ai);
                        int playerAfter = firstAi < 0 ? 0 : lines.Skip(firstAi).Count(l => l.Kind != PaceKind.Ai);
                        int ai = lines.Count(l => l.Kind == PaceKind.Ai);
                        bool fled = hero.GridPosition != start;
                        ctx.Note("forced flight: " + lines.Count + " lines, " + ai + " Ai, " + playerAfter + " not Ai after the first Ai, fled " + fled);
                        return ai > 0 && playerAfter == 0 && fled;
                    }),
                    Step.Pass())
                .Expect("The pacing kind follows every turn", Expect.AssertsPass())
                .Build();
        }

        private static Step KindIs(PaceKind kind, string when)
            => Step.Assert("the pacing kind is " + kind + " on " + when, ctx => CombatPacing.Kind == kind);

        /// <summary>
        /// Records the pacing kind and the creature whose turn it is with every combat log line, by chaining the trace's
        /// CombatLog hook; Stop puts it back. The scenario's OnSetup stops a watcher an aborted job left behind.
        /// </summary>
        private sealed class PacingKindWatch
        {
            public struct Line
            {
                public PaceKind Kind;
                public CharacterController Actor;
            }

            private static PacingKindWatch _current;

            public readonly List<Line> Logs = new List<Line>();
            private Action<string> _prevLog, _log;

            public static void Start()
            {
                Stop();
                var w = new PacingKindWatch();
                w._prevLog = ScenarioHooks.CombatLog;
                w._log = ScenarioHooks.Safe<string>("pacing-kind-watch.log", text =>
                {
                    w._prevLog?.Invoke(text);
                    GameManager gm = GameManager.Instance;
                    w.Logs.Add(new Line { Kind = CombatPacing.Kind, Actor = gm != null ? gm.CurrentCharacter : null });
                });
                ScenarioHooks.CombatLog = w._log;
                _current = w;
            }

            public static PacingKindWatch Stop()
            {
                PacingKindWatch w = _current;
                _current = null;
                if (w == null)
                    return null;
                if (ScenarioHooks.CombatLog == w._log) ScenarioHooks.CombatLog = w._prevLog;
                return w;
            }
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
