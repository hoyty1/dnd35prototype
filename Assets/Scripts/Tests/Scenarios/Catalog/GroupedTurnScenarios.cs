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
    /// after exposure (DMG p.296: 1 minute; PHB p.138: 10 rounds), at the initiative count of the creature whose turn it was
    /// at exposure, whatever the real time; one still pending when the fight ends resolves at combat end, in initiative
    /// order, after CombatOver and before the loot window (delegated RAW reading 2026-11-16). Step 3: every token renders
    /// through its mirror view (TokenView), which follows its model. Step 4 (CRE-312): a summon slain or expired leaves the
    /// grid, NPCs, the summon lists and the initiative order at once, its despawn line before the next creature's turn,
    /// while its view plays the fade; a summon slain on its own turn does not cost the next creature its turn; the Lesser
    /// Globe centre is read from the caster's grid square, not its token (following the caster is current behaviour, SPL-335).
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
            yield return S("summon-slain-leaves-initiative-at-once", SummonSlainLeavesInitiativeAtOnce);
            yield return S("summon-expires-leaves-at-once", SummonExpiresLeavesAtOnce);
            yield return S("summon-slain-on-own-turn-next-still-acts", SummonSlainOnOwnTurnNextStillActs);
            yield return S("summon-slain-off-its-move-next-still-acts", SummonSlainOffItsMoveNextStillActs);
            yield return S("lesser-globe-follows-caster-grid", LesserGlobeFollowsCasterGrid);
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
        private static bool SummonAndAdopt(ScenarioContext ctx, string casterKey, string key, int x, int y, SummonMonsterOption option, int rounds, int hp = 0)
        {
            CharacterController summon = ctx.Gm.Harness_SummonIntoFight(ctx.Get(casterKey), new Vector2Int(x, y), option, rounds);
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
        /// The Lesser Globe (PHB p.236) is centred on the caster; that it then follows the caster is current behaviour, not
        /// RAW (PHB p.236 calls the sphere immobile; owner question SPL-335), and this scenario pins only where the centre
        /// is read from. Its centre now comes from the caster's grid square (LesserGlobeOfInvulnerabilityAreaEffect.CasterCenter), not from its token, which a step tween moves
        /// at the speed of the pacing: at creation (the token pushed off its square first), after a move with the token
        /// again off its square when the globe's Update runs, and in round 2 after OnRoundStart.
        /// </summary>
        private static ScenarioDef LesserGlobeFollowsCasterGrid()
        {
            return Rules("rules/lesser-globe-follows-caster-grid", "The Lesser Globe centre is read from the caster's grid square, not its token (grouped AI turns step 4; following the caster is current behaviour, SPL-335)")
                .Covers("grouped AI turns step 4", "CRE-312", "PHB p.236", "SPL-141", "SPL-335")
                .MaxRounds(2)
                .Pc("caster", ActorSource.Stats(() => Wizard("Globe Caster", 7)), 5, 10, Control.Scripted)
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
                    Step.Assert("After the move, the globe's Update centres it on the new square while the token is off it", ctx =>
                        ctx.Get("caster").GridPosition == new Vector2Int(8, 10)
                        && GlobeOnSquareWhileTokenOff(ctx, "after the move", g =>
                        {
                            if (g == null)
                                return false;
                            System.Reflection.MethodInfo update = typeof(LesserGlobeOfInvulnerabilityAreaEffect).GetMethod("Update",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                            if (update == null)
                                return false;
                            update.Invoke(g, null);
                            return true;
                        })),
                    Step.EndTurn())
                .Turn("caster", 2,
                    Step.Assert("In round 2 (after OnRoundStart) the globe is still centred on the caster's square", ctx =>
                    {
                        CharacterController c = ctx.Get("caster");
                        LesserGlobeOfInvulnerabilityAreaEffect g = GlobeOf(c);
                        bool on = g != null && (g.CenterPosition - SquareCentre(ctx, c)).sqrMagnitude < 1e-8f;
                        ctx.Note("globe round 2: " + (g != null ? g.CenterPosition.ToString("F3") : "none"));
                        return on;
                    }),
                    Step.EndTurn())
                .Expect("The globe centre checks hold", Expect.AssertsPass())
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
