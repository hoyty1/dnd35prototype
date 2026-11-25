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
    /// Step 9 (gt-run-ahead): the rules/group-* scenarios run as group=pair (ScenarioBuilder.Grouping): rep 1 with grouping
    /// off, rep 2 with it on, and the runner compares the trace hash (RepeatCheck) and the presentation sidecar
    /// (PresentationCheck); their group expectations (ExpectGroups) check rep 2's group record: which AI-decided turns
    /// joined a group as its lanes, and what closed it (the control beat, combat end, the harness halt's reset).
    /// Step 10 (gt-stop-points): a player-run creature's AoO or automatic acid spray inside a group is a window stop on the
    /// group record (S2; the owner's ruling, C1), and a defender prompt to a player-run creature closes the group as a
    /// prompt and opens only after the playback has shown every earlier beat (S3, C3); the rules never pause for playback.
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
            yield return S("group-three-orcs-before-ui-fighter", GroupThreeOrcsBeforeUiFighter);
            yield return S("group-confused-pc-inside", GroupConfusedPcInside);
            yield return S("group-dying-pc-skipped-inside", GroupDyingPcSkippedInside);
            yield return S("group-cornered-frightened-pc-gets-menu", GroupCorneredFrightenedPcGetsMenu);
            yield return S("group-charmed-pc-ends-group", GroupCharmedPcEndsGroup);
            yield return S("group-summon-slain-inside", GroupSummonSlainInside);
            yield return S("group-spans-round-wrap", GroupSpansRoundWrap);
            yield return S("group-victory-mid-group", GroupVictoryMidGroup);
            yield return S("group-defeat-mid-group", GroupDefeatMidGroup);
            yield return S("group-stalemate-halt", GroupStalemateHalt);
            yield return S("group-lesser-globe-caster-moves", GroupLesserGlobeCasterMoves);
            yield return S("group-stops-at-fighter-aoo", GroupStopsAtFighterAoO);
            yield return S("group-counter-trip-prompt-plays-out-first", GroupCounterTripPromptPlaysOutFirst);
            yield return S("group-overrun-response-prompt", GroupOverrunResponsePrompt);
            yield return S("group-escape-opposition-prompt", GroupEscapeOppositionPrompt);
            yield return S("group-beetle-acid-spray-prompt", GroupBeetleAcidSprayPrompt);
            yield return S("group-beetle-acid-spray-auto", GroupBeetleAcidSprayAuto);
            yield return S("group-web-cast-inside", GroupWebCastInside);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("GroupedTurnScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        private static string SecondaryLine(string name, string poisonName = VenomName) => "^☣ " + name + " makes secondary save vs " + poisonName;

        // ── Step 9: grouped AI turns, run ahead and played back (group=pair) ─────────────

        private static void Named(CharacterController c, string name) => c.Stats.CharacterName = name;

        private static void SturdyNamed(CharacterController c, string name)
        {
            Sturdy(c);
            Named(c, name);
        }

        private static string Describe(GroupRecord g) =>
            g == null ? "none" : "[" + string.Join(", ", g.Lanes) + "] " + (g.Stop.HasValue ? g.Stop.Value.ToString() : "open");

        private static string DescribeAll(IReadOnlyList<GroupRecord> groups) =>
            groups.Count + " groups: " + string.Join("; ", groups.Select(Describe));

        /// <summary>Null when group <paramref name="index"/> (from 0) has exactly <paramref name="lanes"/> and closed by <paramref name="stop"/>.</summary>
        private static string GroupIs(IReadOnlyList<GroupRecord> groups, int index, GroupStop stop, params string[] lanes)
        {
            GroupRecord g = index < groups.Count ? groups[index] : null;
            if (g != null && g.Stop == stop && g.Lanes.SequenceEqual(lanes))
                return null;
            return "group " + (index + 1) + " is " + Describe(g) + ", expected [" + string.Join(", ", lanes) + "] " + stop
                   + " (" + DescribeAll(groups) + ")";
        }

        /// <summary>The playback has caught up when a player-run creature's menu is up (S1; the Ui driver waits for it).</summary>
        private static Step CaughtUp(string what) =>
            Step.Assert(what, ctx => !CombatPresentation.Holding && !CombatPresentation.BlocksInput);

        /// <summary>
        /// Three orcs act before the Ui fighter: their three AI turns run ahead as one group, which the fighter's menu closes
        /// (S1); the menu comes up only once the playback has caught up. Round 2 is a second group of the same three lanes.
        /// </summary>
        private static ScenarioDef GroupThreeOrcsBeforeUiFighter()
        {
            return Rules("rules/group-three-orcs-before-ui-fighter", "Three orcs' turns run ahead as one group that the Ui fighter's menu closes (grouped AI turns step 9, S1)")
                .Covers("grouped AI turns step 9", "S1", "PC_NPC_PARITY")
                .Grouping(GroupMode.Pair)
                .MaxRounds(2)
                .Npc("o1", "orc_warrior", 13, 9, Control.Ai)
                .Npc("o2", "orc_warrior", 13, 10, Control.Ai)
                .Npc("o3", "orc_warrior", 13, 11, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Group Fighter", 3)), 10, 10, Control.Ui)
                .Tweak("o1", c => SturdyNamed(c, "Orc A"))
                .Tweak("o2", c => SturdyNamed(c, "Orc B"))
                .Tweak("o3", c => SturdyNamed(c, "Orc C"))
                .Tweak("hero", Sturdy)
                .Initiative("o1", "o2", "o3", "hero")
                .Turn("hero", 0, CaughtUp("The fighter's menu comes up after the playback caught up"), Step.EndTurn())
                .Expect("The menu checks hold", Expect.AssertsPass())
                .Expect("The fight runs to the round cap", Expect.Outcome(Outcome.Stalemate))
                .ExpectGroups("Rounds 1 and 2: the three orcs are one group each, closed by the fighter's menu", g =>
                    GroupIs(g, 0, GroupStop.Control, "1:Orc A", "1:Orc B", "1:Orc C")
                    ?? GroupIs(g, 1, GroupStop.Control, "2:Orc A", "2:Orc B", "2:Orc C"))
                .Build();
        }

        /// <summary>
        /// A confused Ui PC whose d% (90: attack the nearest creature, PHB p.212) takes its turn over acts as a forced turn
        /// inside the group (BeginForcedPCTurn is an AI-decided turn's mark), so the group runs on through it to the orc
        /// after it and closes at the second PC's menu.
        /// </summary>
        private static ScenarioDef GroupConfusedPcInside()
        {
            return Rules("rules/group-confused-pc-inside", "A confused PC's forced turn is a lane of the group around it (grouped AI turns step 9; PHB p.212)")
                .Covers("grouped AI turns step 9", "PHB p.212", "PC_NPC_PARITY")
                .Grouping(GroupMode.Pair)
                .MaxRounds(1)
                .Npc("o1", "orc_warrior", 12, 10, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Confused Hero", 3)), 10, 10, Control.Ui)
                .Npc("o2", "orc_warrior", 12, 12, Control.Ai)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Clear Ally", 3)), 10, 12, Control.Ui)
                .StartCondition("hero", CombatConditionType.Confused, 3)
                .Tweak("o1", c => SturdyNamed(c, "Orc A"))
                .Tweak("o2", c => SturdyNamed(c, "Orc B"))
                .Tweak("hero", Sturdy)
                .Tweak("ally", Sturdy)
                .Force(100, 90, null, 1)
                .Initiative("o1", "hero", "o2", "ally")
                .Turn("ally", 0, CaughtUp("The ally's menu comes up after the playback caught up"), Step.EndTurn())
                .Expect("The hero rolled 'attack the nearest creature' (d% 90)", v =>
                    v.Log("Confused Hero is confused \\(d% 90\\)").Count > 0 ? ExpectResult.Pass("rolled 90") : ExpectResult.Fail("no roll of 90"))
                .Expect("The menu checks hold", Expect.AssertsPass())
                .ExpectGroups("Round 1: the orc, the confused hero's forced turn and the second orc are one group, closed by the ally's menu", g =>
                    GroupIs(g, 0, GroupStop.Control, "1:Orc A", "1:Confused Hero", "1:Orc B"))
                .Build();
        }

        /// <summary>
        /// The first orc (Scripted) drops the Ui hero to -3 HP; the dying hero's turn is skipped (StartPCTurn's skip gate,
        /// no menu, PHB p.145), so it is no lane and does not close the group: both orcs are one group, closed by the
        /// ally's menu.
        /// </summary>
        private static ScenarioDef GroupDyingPcSkippedInside()
        {
            return Rules("rules/group-dying-pc-skipped-inside", "A dying PC's skipped turn neither joins nor closes the group (grouped AI turns step 9; PHB p.145)")
                .Covers("grouped AI turns step 9", "PHB p.145", "PC_NPC_PARITY")
                .Grouping(GroupMode.Pair)
                .MaxRounds(1)
                .Npc("o1", "orc_warrior", 12, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Dying Hero", 3)), 10, 10, Control.Ui)
                .Npc("o2", "orc_warrior", 15, 15, Control.Idle)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Standing Ally", 3)), 10, 12, Control.Ui)
                .Tweak("o1", c => SturdyNamed(c, "Orc A"))
                .Tweak("o2", c => SturdyNamed(c, "Orc B"))
                .Tweak("ally", Sturdy)
                .Force(100, 50, CharacterController.StabilizationCheckContext, -1)
                .Initiative("o1", "hero", "o2", "ally")
                .Turn("o1", 1, Step.Assert("The hero drops to -3 (fixture)", ctx =>
                {
                    ctx.Get("hero").Stats.CurrentHP = -3;
                    return ctx.Get("hero").Stats.CurrentHP < 0;
                }), Step.EndTurn())
                .Turn("ally", 0, CaughtUp("The ally's menu comes up after the playback caught up"), Step.EndTurn())
                .Expect("The fixture and the menu checks hold", Expect.AssertsPass())
                .Expect("The dying hero cannot act this turn", v =>
                    v.Log("Dying Hero .* cannot act this turn").Count > 0 ? ExpectResult.Pass("skipped") : ExpectResult.Fail("no skip line"))
                .ExpectGroups("Round 1: both orcs are one group across the skipped turn, closed by the ally's menu", g =>
                    GroupIs(g, 0, GroupStop.Control, "1:Orc A", "1:Orc B"))
                .Build();
        }

        /// <summary>
        /// The goblin frightens the cornered Ui hero (speed 0, PHB p.310: cornered, it may fight); the hero's turn starts as
        /// a forced turn (a lane of the goblin's group) that hands the menu back, which closes the group (S1). The player
        /// then takes total defense.
        /// </summary>
        private static ScenarioDef GroupCorneredFrightenedPcGetsMenu()
        {
            return Rules("rules/group-cornered-frightened-pc-gets-menu", "A cornered frightened PC's forced turn joins the group, and its menu closes it (grouped AI turns step 9; PHB p.310)")
                .Covers("grouped AI turns step 9", "PHB p.310", "D15", "PC_NPC_PARITY")
                .Grouping(GroupMode.Pair)
                .MaxRounds(1)
                .Npc("gob", "goblin", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Cornered Hero", 3)), 10, 10, Control.Ui)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); c.Stats.BaseSpeed = 0; })
                .Tweak("gob", c => SturdyNamed(c, "Fear Goblin"))
                .Initiative("gob", "hero")
                .Turn("gob", 1, Step.Assert("The goblin frightens the hero (fixture)", ctx =>
                {
                    ctx.Gm.ApplyCondition(ctx.Get("hero"), CombatConditionType.Frightened, 3, ctx.Get("gob"), sourceCategory: "Harness");
                    return ctx.Get("hero").HasCondition(CombatConditionType.Frightened);
                }), Step.Pass())
                .Turn("hero", 1, CaughtUp("The hero's menu comes up after the playback caught up"), Step.TotalDefense())
                .Expect("The fixture and the menu checks hold", Expect.AssertsPass())
                .Expect("The cornered hero's forced turn ran", v =>
                    v.Log("cornered by fear").Any(e => e.Round == 1) ? ExpectResult.Pass("cornered") : ExpectResult.Fail("no cornered line"))
                .Expect("The player's Total Defense step runs", Expect.StepStatus("hero", 1, "TotalDefense", 0, "done"))
                .ExpectGroups("Round 1: the goblin and the hero's forced turn are one group, closed by the hero's menu", g =>
                    GroupIs(g, 0, GroupStop.Control, "1:Fear Goblin", "1:Cornered Hero"))
                .Build();
        }

        /// <summary>
        /// The goblin charms the Ui hero (a fixture standing for a failed Will save, PHB p.209). Charm is no turn compulsion
        /// (D14): the charmed hero's turn is a player-run turn, so its menu ends the goblin's group. A second, uncharmed Ui
        /// PC keeps the party under its own control, or the charm alone would end the fight as a defeat (CORE-038's
        /// control predicate) and the group would close at combat end instead.
        /// </summary>
        private static ScenarioDef GroupCharmedPcEndsGroup()
        {
            return Rules("rules/group-charmed-pc-ends-group", "A charmed PC's turn is player-run and ends the group (grouped AI turns step 9; PHB p.209, D14)")
                .Covers("grouped AI turns step 9", "PHB p.209", "D14", "PC_NPC_PARITY")
                .Grouping(GroupMode.Pair)
                .MaxRounds(1)
                .Npc("gob", "goblin", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Charmed Hero", 3)), 10, 10, Control.Ui)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Free Ally", 3)), 8, 10, Control.Ui)
                .Tweak("hero", Sturdy)
                .Tweak("ally", Sturdy)
                .Tweak("gob", c => SturdyNamed(c, "Charm Goblin"))
                .Initiative("gob", "hero", "ally")
                .Turn("gob", 1, Step.Assert("The goblin charms the hero (fixture)", ctx =>
                {
                    CharacterController hero = ctx.Get("hero"), gob = ctx.Get("gob");
                    var data = new CharmedConditionData { Caster = gob, CasterName = gob.Stats.CharacterName, RemainingRounds = 10 };
                    ctx.Gm.ApplyCondition(hero, CombatConditionType.Charmed, 10, gob, data, sourceNameOverride: "Charm Person", sourceCategory: "Harness");
                    return CharmRules.IsCharmedBy(hero, gob);
                }), Step.Pass())
                .Turn("hero", 1, CaughtUp("The charmed hero's menu comes up after the playback caught up"), Step.EndTurn())
                .Expect("The fixture and the menu checks hold", Expect.AssertsPass())
                .ExpectGroups("Round 1: the goblin is one group, closed by the charmed hero's menu", g =>
                    GroupIs(g, 0, GroupStop.Control, "1:Charm Goblin"))
                .Build();
        }

        /// <summary>
        /// rules/summon-slain-leaves-initiative-at-once as a pair: the summon is slain inside a group (every turn is
        /// AI-decided, so the whole fight is one group, which the halt resets); its view keeps its last picture and goes at
        /// its Removed beat, and both reps show the same.
        /// </summary>
        private static ScenarioDef GroupSummonSlainInside()
        {
            return AsPair(SummonSlainLeavesInitiativeAtOnce(), "rules/group-summon-slain-inside",
                    "A summon slain inside a group leaves at once in the rules and at its beat on screen (grouped AI turns step 9; CRE-312)")
                .ExpectGroups("Every turn is AI-decided: one group from the first turn, reset by the halt", g =>
                    g.Count == 1 && g[0].Stop == GroupStop.Reset && g[0].Lanes.Count >= 3 && g[0].Lanes[0].StartsWith("1:", StringComparison.Ordinal)
                        ? null : DescribeAll(g))
                .Build();
        }

        /// <summary>
        /// The orcs' turns after the Ui hero's and the first orc's turn of the next round are one group across the round
        /// wrap (S5's window cap comes with step 10).
        /// </summary>
        private static ScenarioDef GroupSpansRoundWrap()
        {
            return Rules("rules/group-spans-round-wrap", "A group spans the round wrap (grouped AI turns step 9)")
                .Covers("grouped AI turns step 9", "PC_NPC_PARITY")
                .Grouping(GroupMode.Pair)
                .MaxRounds(2)
                .Npc("o1", "orc_warrior", 13, 9, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Wrap Fighter", 3)), 10, 10, Control.Ui)
                .Npc("o2", "orc_warrior", 13, 10, Control.Ai)
                .Npc("o3", "orc_warrior", 13, 11, Control.Ai)
                .Tweak("o1", c => SturdyNamed(c, "Orc A"))
                .Tweak("o2", c => SturdyNamed(c, "Orc B"))
                .Tweak("o3", c => SturdyNamed(c, "Orc C"))
                .Tweak("hero", Sturdy)
                .Initiative("o1", "hero", "o2", "o3")
                .Turn("hero", 0, CaughtUp("The fighter's menu comes up after the playback caught up"), Step.EndTurn())
                .Expect("The menu checks hold", Expect.AssertsPass())
                .ExpectGroups("Round 1's Orc A, then Orc B and Orc C of round 1 with Orc A of round 2 as one group", g =>
                    GroupIs(g, 0, GroupStop.Control, "1:Orc A")
                    ?? GroupIs(g, 1, GroupStop.Control, "1:Orc B", "1:Orc C", "2:Orc A"))
                .Build();
        }

        /// <summary>
        /// The AI ally drops the last goblin inside a group (every d20 a 20): the combat ends mid-group, the group closes at
        /// combat end (S4) and its beats still play; the loot window waits for them (WhenDrained).
        /// </summary>
        private static ScenarioDef GroupVictoryMidGroup()
        {
            return Rules("rules/group-victory-mid-group", "A victory mid-group closes the group at combat end (grouped AI turns step 9, S4)")
                .Covers("grouped AI turns step 9", "S4", "PC_NPC_PARITY")
                .Grouping(GroupMode.Pair)
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Watching Hero", 3)), 5, 10, Control.Ui)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Victor Ally", 5)), 10, 10, Control.Ai)
                .Npc("gob", "goblin", 11, 10, Control.Ai)
                .Tweak("hero", Sturdy)
                .Tweak("ally", Sturdy)
                .Tweak("gob", c => { Named(c, "Last Goblin"); c.Stats.CurrentHP = 1; })
                .Force(20, 20, null, -1)
                .Initiative("hero", "ally", "gob")
                .Turn("hero", 0, Step.EndTurn())
                .Expect("The party wins in round 1", Expect.All(Expect.Outcome(Outcome.Victory), Expect.RoundsAtMost(1)))
                .ExpectGroups("The ally's turn is one group, closed at combat end", g =>
                    GroupIs(g, 0, GroupStop.CombatEnd, "1:Victor Ally"))
                .Build();
        }

        /// <summary>
        /// The orc drops the last PC inside its group (every d20 a 20): the defeat closes the group at combat end, and the
        /// defeat panel is an interactive beat behind the group's beats (S4).
        /// </summary>
        private static ScenarioDef GroupDefeatMidGroup()
        {
            return Rules("rules/group-defeat-mid-group", "A defeat mid-group closes the group at combat end (grouped AI turns step 9, S4)")
                .Covers("grouped AI turns step 9", "S4", "PC_NPC_PARITY")
                .Grouping(GroupMode.Pair)
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Fallen Hero", 1)), 10, 10, Control.Ui)
                .Npc("orc", "orc_warrior", 11, 10, Control.Ai)
                .Tweak("hero", c => c.Stats.CurrentHP = 1)
                .Tweak("orc", c => SturdyNamed(c, "Orc Killer"))
                .Force(20, 20, null, -1)
                .Initiative("hero", "orc")
                .Turn("hero", 0, Step.EndTurn())
                .Expect("The party loses in round 1", Expect.All(Expect.Outcome(Outcome.Defeat), Expect.RoundsAtMost(1)))
                .ExpectGroups("The orc's turn is one group, closed at combat end", g =>
                    GroupIs(g, 0, GroupStop.CombatEnd, "1:Orc Killer"))
                .Build();
        }

        /// <summary>
        /// Two sturdy AI sides at the round cap: the harness halts the fight while the group still records (every turn is
        /// AI-decided); it catches the playback up first (ScenarioRunner.HaltCaughtUp), so both reps show the same, and the
        /// halt's Clear closes the group as a reset.
        /// </summary>
        private static ScenarioDef GroupStalemateHalt()
        {
            return Rules("rules/group-stalemate-halt", "The harness halt catches an open group up, then resets it (grouped AI turns step 9, S6)")
                .Covers("grouped AI turns step 9", "S6", "PC_NPC_PARITY")
                .Grouping(GroupMode.Pair)
                .MaxRounds(1)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Halted Ally", 3)), 5, 10, Control.Ai)
                .Npc("orc", "orc_warrior", 15, 10, Control.Ai)
                .Tweak("ally", Sturdy)
                .Tweak("orc", c => SturdyNamed(c, "Halted Orc"))
                .Initiative("ally", "orc")
                .Expect("The fight is halted as a stalemate", Expect.Outcome(Outcome.Stalemate))
                .ExpectGroups("One group from the first turn, reset by the halt", g =>
                    g.Count == 1 && g[0].Stop == GroupStop.Reset && g[0].Lanes.Count >= 2
                    && g[0].Lanes[0] == "1:Halted Ally" && g[0].Lanes[1] == "1:Halted Orc"
                        ? null : DescribeAll(g))
                .Build();
        }

        /// <summary>
        /// rules/lesser-globe-stays-put as a pair: the caster casts the Lesser Globe and walks out of it inside one group;
        /// the globe's indicator shows at its beat and stays where it was cast in both reps.
        /// </summary>
        private static ScenarioDef GroupLesserGlobeCasterMoves()
        {
            return AsPair(LesserGlobeStaysPut(), "rules/group-lesser-globe-caster-moves",
                    "The Lesser Globe stays put while its caster moves inside a group (grouped AI turns step 9; PHB p.236, SPL-335)")
                .ExpectGroups("Every turn is AI-decided: one group from the first turn, reset by the halt", g =>
                    g.Count == 1 && g[0].Stop == GroupStop.Reset && g[0].Lanes.Count >= 2 ? null : DescribeAll(g))
                .Build();
        }

        // ── Step 10: the stopping points (group=pair) ─────────────

        private static string StopsOf(GroupRecord g) => g == null ? "none" : "[" + string.Join(", ", g.Stops) + "]";

        /// <summary>
        /// Null when group <paramref name="index"/> closed at a prompt (S3) with beats still to play (so the dialog opened
        /// only after them), has exactly <paramref name="lanes"/> lanes, all from round 1, and no window stop.
        /// </summary>
        private static string PromptGroupIs(IReadOnlyList<GroupRecord> groups, int index, int lanes)
        {
            GroupRecord g = index < groups.Count ? groups[index] : null;
            if (g != null && g.Stop == GroupStop.Prompt && g.PendingAtClose > 0 && g.Lanes.Count == lanes
                && g.Lanes.All(l => l.StartsWith("1:", StringComparison.Ordinal)) && g.Stops.Count == 0)
                return null;
            return "group " + (index + 1) + " is " + Describe(g) + " pending " + (g != null ? g.PendingAtClose : 0) + " stops " + StopsOf(g)
                   + ", expected a Prompt close with beats to play and " + lanes + " round-1 lane(s) (" + DescribeAll(groups) + ")";
        }

        /// <summary>
        /// Null when the first group is the orc's round-1 turn closed at the prompt (<see cref="PromptGroupIs"/>) and the
        /// only other group is the next round's first AI turn, reset by the harness halt at the round cap.
        /// </summary>
        private static string PromptThenHalt(IReadOnlyList<GroupRecord> groups)
        {
            string first = PromptGroupIs(groups, 0, 1);
            if (first != null)
                return first;
            if (groups.Count == 1)
                return null;
            GroupRecord last = groups[1];
            return groups.Count == 2 && last.Stop == GroupStop.Reset && last.Lanes.All(l => l.StartsWith("2:", StringComparison.Ordinal))
                ? null
                : "expected only the round-2 group the halt resets after the prompt group (" + DescribeAll(groups) + ")";
        }

        /// <summary>The runner's notes that start with <paramref name="prefix"/>.</summary>
        private static List<TraceEvent> NotesStarting(TraceView v, string prefix) =>
            v.Of("note").Where(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal)).ToList();

        /// <summary>
        /// The owner's ruling (a player-run creature's AoO stops a group; C1, S2): the Ui fighter acts first (it is not
        /// flat-footed), then the Scripted guard (an AI-run ally) and the Scripted orc run as one group. The orc walks out of
        /// the squares both threaten: two movement AoOs, resolved at once in the rules as with grouping off. Only the
        /// fighter's is a window stop (it is player-run: its decider is the prompting one); the guard's is not. The group
        /// stays open across the stop and closes at the fighter's round-2 menu.
        /// </summary>
        private static ScenarioDef GroupStopsAtFighterAoO()
        {
            return Rules("rules/group-stops-at-fighter-aoo", "A player-run fighter's AoO inside a group is a window stop; an AI-run guard's is not (grouped AI turns step 10, S2, C1)")
                .Covers("grouped AI turns step 10", "S2", "C1", "PHB p.137", "PC_NPC_PARITY")
                .Grouping(GroupMode.Pair)
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Group Fighter", 3)), 10, 10, Control.Ui)
                .Npc("guard", "orc_warrior", 12, 11, Control.Scripted, CharacterTeam.Player)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Tweak("hero", Sturdy)
                .Tweak("guard", c => SturdyNamed(c, "Guard"))
                .Tweak("orc", c => SturdyNamed(c, "Orc A"))
                .Initiative("hero", "guard", "orc")
                .Turn("hero", 0, CaughtUp("The fighter's menu comes up after the playback caught up"), Step.EndTurn())
                .Turn("guard", 0, Step.Pass())
                .Turn("orc", 1, Step.Move(14, 10))
                .Turn("orc", 2, Step.Pass())
                .Expect("The menu checks hold", Expect.AssertsPass())
                .Expect("The fighter's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The orc's move is done", Expect.StepStatus("orc", 1, "Move", 0, "done"))
                .Expect("The orc's move provokes one movement AoO from the fighter and one from the guard (PHB p.137)", v =>
                {
                    List<TraceEvent> hero = v.AoOs("hero", "orc", "movement");
                    List<TraceEvent> guard = v.AoOs("guard", "orc", "movement");
                    return hero.Count == 1 && guard.Count == 1
                        ? ExpectResult.Pass("one each", hero[0].Seq, guard[0].Seq)
                        : ExpectResult.Fail(hero.Count + " by the fighter, " + guard.Count + " by the guard");
                })
                .ExpectGroups("Round 1: the guard and the orc are one group with one window stop, the fighter's AoO in the orc's lane, closed by the fighter's menu", g =>
                    GroupIs(g, 0, GroupStop.Control, "1:Guard", "1:Orc A")
                    ?? (g[0].Stops.Count == 1 && g[0].Stops[0].Kind == StopKind.AoO && g[0].Stops[0].Actor == "Group Fighter" && g[0].Stops[0].Lane == "1:Orc A"
                        ? null : "group 1 stops " + StopsOf(g[0]) + ", expected [AoO:Group Fighter] in lane 1:Orc A"))
                .Build();
        }

        /// <summary>
        /// S3 (C3): the counter-trip prompt (PHB p.158, CMB-079) to the Ui hero inside the Scripted orc's group: the dialog
        /// is asked for while the group holds, so it closes the group and opens only after the playback has shown the
        /// orc's turn so far; the rules wait on the prompt's flag as with grouping off, and the runner answers once the
        /// dialog shows (Trip Back). The rules checks are those of rules/maneuver-trip-counter-ui.
        /// </summary>
        private static ScenarioDef GroupCounterTripPromptPlaysOutFirst()
        {
            return AsPair(RulesScenarios.CounterTripUi(true), "rules/group-counter-trip-prompt-plays-out-first",
                    "The counter-trip prompt inside a group opens after the playback caught up (grouped AI turns step 10, S3, C3; PHB p.158)", 10)
                .ExpectGroups("The orc's round-1 turn is one group, closed by the hero's prompt with beats still to play; then only the halt's reset", g =>
                    PromptThenHalt(g))
                .Build();
        }

        /// <summary>
        /// S3: the overrun avoid-or-block prompt (PHB p.157, D11) to the Ui fighter inside the Scripted orc's group closes
        /// the group and opens after the playback caught up; the runner answers Block. The rules checks are those of
        /// rules/overrun-pc-defender-blocks-ui.
        /// </summary>
        private static ScenarioDef GroupOverrunResponsePrompt()
        {
            return AsPair(OverrunScenarios.OverrunPcDefenderUi(true), "rules/group-overrun-response-prompt",
                    "The overrun response prompt inside a group opens after the playback caught up (grouped AI turns step 10, S3; PHB p.157)", 10)
                .ExpectGroups("The orc's round-1 turn is one group, closed by the hero's prompt with beats still to play; then only the halt's reset", g =>
                    PromptThenHalt(g))
                .Build();
        }

        /// <summary>
        /// S3: the escape-opposition prompt (PHB p.156, D10) to the Ui grappler: the hero grapples the orc on its own turn,
        /// then the orc's escape attempt inside its group asks the hero, which closes the group and opens after the
        /// playback caught up; the runner answers Oppose. The rules checks are those of rules/grapple-escape-opposed-ui.
        /// </summary>
        private static ScenarioDef GroupEscapeOppositionPrompt()
        {
            return AsPair(ManeuverCoreScenarios.GrappleEscapeOppositionUi(true), "rules/group-escape-opposition-prompt",
                    "The escape-opposition prompt inside a group opens after the playback caught up (grouped AI turns step 10, S3; PHB p.156)", 10)
                .ExpectGroups("The orc's round-1 turn is one group, closed by the hero's prompt with beats still to play; then only the halt's reset", g =>
                    PromptThenHalt(g))
                .Build();
        }

        /// <summary>
        /// S3: a Ui-controlled giant bombardier beetle on the party's side (a Player-team pool creature, controllable like a
        /// player's summon) is attacked by the Scripted orc inside its group. The NPC attack sequence can wait, so the
        /// beetle's decider is its acid-spray menu (MM p.285, D1; GameManager.PromptAttackedReaction): it closes the group,
        /// waits for the playback, then shows; the runner answers Spray through the menu's own option button
        /// (HandleAttackedReactionPrompt). One spray; no window stop (a prompt, not an automatic reaction).
        /// </summary>
        private static ScenarioDef GroupBeetleAcidSprayPrompt()
        {
            return Rules("rules/group-beetle-acid-spray-prompt", "A player-run beetle's acid-spray menu inside a group opens after the playback caught up (grouped AI turns step 10, S3; MM p.285, D1)")
                .Covers("grouped AI turns step 10", "S3", "CRE-040", "MM p.285", "PC_NPC_PARITY")
                .Grouping(GroupMode.Pair)
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Npc("beetle", "giant_bombardier_beetle", 10, 10, Control.Ui, CharacterTeam.Player)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Far Fighter", 3)), 3, 3, Control.Ui)
                .Tweak("orc", c => SturdyNamed(c, "Orc A"))
                .Tweak("beetle", c => SturdyNamed(c, "Group Beetle"))
                .Tweak("hero", Sturdy)
                .Initiative("orc", "beetle", "hero")
                .AttackedReactionAnswer("beetle", true)
                .Turn("orc", 1, Step.Attack("beetle"))
                .Turn("beetle", 0, CaughtUp("The beetle's menu comes up after the playback caught up"), Step.EndTurn())
                .Turn("hero", 0, Step.EndTurn())
                .Expect("The menu checks hold", Expect.AssertsPass())
                .Expect("The beetle's turn is a Ui turn", Expect.Controller("beetle", "ui"))
                .Expect("The orc attacked the beetle", Expect.StepStatus("orc", 1, "Attack", 0, "done"))
                .Expect("The beetle's acid-spray menu was asked once and answered Spray", v =>
                {
                    List<TraceEvent> notes = NotesStarting(v, "attacked-reaction prompt for beetle");
                    return notes.Count == 1 && (notes[0].Str("text") ?? "").Contains("answered Spray")
                        ? ExpectResult.Pass(notes[0].Str("text"), notes[0].Seq)
                        : ExpectResult.Fail(notes.Count + " prompts: " + string.Join("; ", notes.Select(e => e.Str("text"))));
                })
                .Expect("One spray, after the attack (MM p.285)", Expect.Count("log", e => (e.Str("text") ?? "").Contains("unleashes Acid Spray"), 1, 1))
                .ExpectGroups("The orc's round-1 turn is one group, closed by the beetle's prompt with beats still to play; then only the halt's reset", g =>
                    PromptThenHalt(g))
                .Build();
        }

        /// <summary>
        /// S2 for the acid spray: the same Ui beetle is disturbed inside the Scripted orc's group by the orc's effect
        /// (a fixture: GameManager.DealDamage with the orc as the packet's source creature, MM p.285: damaged by a foe). The
        /// damage path cannot wait on a prompt, so the AI fallback answers for the player-run beetle (CRE-075) and sprays
        /// at once in the rules; that automatic reaction is a window stop on the group record. No prompt; the group closes at
        /// the beetle's menu.
        /// </summary>
        private static ScenarioDef GroupBeetleAcidSprayAuto()
        {
            return Rules("rules/group-beetle-acid-spray-auto", "A player-run beetle's automatic acid spray inside a group is a window stop (grouped AI turns step 10, S2, C1; MM p.285, CRE-075)")
                .Covers("grouped AI turns step 10", "S2", "C1", "CRE-075", "MM p.285", "PC_NPC_PARITY")
                .Grouping(GroupMode.Pair)
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Npc("beetle", "giant_bombardier_beetle", 10, 10, Control.Ui, CharacterTeam.Player)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Far Fighter", 3)), 3, 3, Control.Ui)
                .Tweak("orc", c => SturdyNamed(c, "Orc A"))
                .Tweak("beetle", c => SturdyNamed(c, "Group Beetle"))
                .Tweak("hero", Sturdy)
                .Initiative("orc", "beetle", "hero")
                .Turn("orc", 1, Step.Assert("fixture: the orc's effect deals the beetle 1 damage (it is disturbed by a foe)", ctx =>
                {
                    DamagePacket packet = DamagePackets.CreatureAttack("Scenario blow", DamageType.Untyped);
                    packet.SourceCreature = ctx.Get("orc");
                    ctx.Gm.DealDamage(ctx.Get("beetle"), 1, packet);
                    return true;
                }), Step.EndTurn())
                .Turn("beetle", 0, CaughtUp("The beetle's menu comes up after the playback caught up"), Step.EndTurn())
                .Turn("hero", 0, Step.EndTurn())
                .Expect("The fixture and the menu checks hold", Expect.AssertsPass())
                .Expect("The beetle's turn is a Ui turn", Expect.Controller("beetle", "ui"))
                .Expect("No acid-spray menu: the damage path cannot wait, so the AI fallback answers (CRE-075)", v =>
                    NotesStarting(v, "attacked-reaction prompt").Count == 0 ? ExpectResult.Pass("no prompt") : ExpectResult.Fail("a prompt was answered"))
                .Expect("One spray (MM p.285)", Expect.Count("log", e => (e.Str("text") ?? "").Contains("unleashes Acid Spray"), 1, 1))
                .ExpectGroups("The orc's turn is one group with the beetle's spray as its window stop, closed by the beetle's menu", g =>
                    GroupIs(g, 0, GroupStop.Control, "1:Orc A")
                    ?? (g[0].Stops.Count == 1 && g[0].Stops[0].Kind == StopKind.AutomaticReaction && g[0].Stops[0].Actor == "Group Beetle"
                        ? null : "group 1 stops " + StopsOf(g[0]) + ", expected [AutomaticReaction:Group Beetle]"))
                .Build();
        }

        /// <summary>
        /// A cast inside a group with no stop: the Scripted wizard (an AI-run turn) casts Web (PHB p.301: a 20-ft.-radius
        /// spread, Reflex negates) over the Ui hero inside its group. The cast's area, the hero's save and any entangle
        /// resolve at once in the rules; nothing prompts the hero (an area spell's save asks no player), so the cast is no
        /// window stop and no prompt: the group closes at the hero's menu (Control), which comes up after the playback.
        /// </summary>
        private static ScenarioDef GroupWebCastInside()
        {
            return Rules("rules/group-web-cast-inside", "A Scripted wizard's Web over the Ui hero inside a group closes at the hero's menu, with no stop (grouped AI turns step 10)")
                .Covers("grouped AI turns step 10", "S1", "PHB p.301", "PC_NPC_PARITY")
                .Grouping(GroupMode.Pair)
                .MaxRounds(1)
                .Npc("wizard", "mist_wizard", 3, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Webbed Fighter", 3)), 10, 10, Control.Ui)
                .Tweak("wizard", c => { SturdyNamed(c, "Web Wizard"); PrepareOnly(c, 2, SpellNames.WEB); })
                .Tweak("hero", Sturdy)
                .Initiative("wizard", "hero")
                .Turn("wizard", 1, Step.Cast(SpellNames.WEB, "hero"))
                .Turn("hero", 0, CaughtUp("The hero's menu comes up after the playback caught up"), Step.EndTurn())
                .Expect("The menu checks hold", Expect.AssertsPass())
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The wizard's cast is done", Expect.StepStatus("wizard", 1, "Cast", 0, "done"))
                .Expect("The wizard casts Web once", Expect.Count("cast", e => e.Str("by") == "wizard" && e.Str("spell") == SpellNames.WEB, 1, 1))
                .ExpectGroups("The wizard's round-1 turn is one group with no window stop, closed by the hero's menu", g =>
                    GroupIs(g, 0, GroupStop.Control, "1:Web Wizard")
                    ?? (g.All(x => x.Stops.Count == 0) ? null : "stops " + string.Join("; ", g.Select(StopsOf)) + ", expected none"))
                .Build();
        }

        /// <summary>
        /// Prepares <paramref name="spellId"/> (a clone, never the template) in the first unused regular slot of
        /// <paramref name="level"/> and spends every other unused slot, so it is the caster's only spell. Throws (the job
        /// ends SetupFailed) when there is no such slot.
        /// </summary>
        private static void PrepareOnly(CharacterController c, int level, string spellId)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spellcasting to prepare " + spellId + " in");
            bool placed = false;
            foreach (SpellSlot slot in sc.SpellSlots)
            {
                if (slot == null || slot.IsUsed)
                    continue;
                if (!placed && slot.Level == level && !slot.IsDomainSlot)
                {
                    slot.PreparedSpell = SpellDatabase.GetSpell(spellId).Clone();
                    placed = true;
                    continue;
                }
                slot.IsUsed = true;
            }
            if (!placed)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no unused regular level-" + level + " slot for " + spellId);
            sc.SyncPreparedSpellsFromSlots();
        }

        /// <summary>A copy of a built scenario as a group=pair scenario of its own id and title (its rules checks kept).</summary>
        private static PairBuilder AsPair(ScenarioDef def, string id, string title, int step = 9)
        {
            def.Id = id;
            def.Title = title;
            def.Grouping = GroupMode.Pair;
            string covers = "grouped AI turns step " + step;
            if (!def.Covers.Contains(covers))
                def.Covers.Add(covers);
            return new PairBuilder(def);
        }

        private sealed class PairBuilder
        {
            private readonly ScenarioDef _def;
            public PairBuilder(ScenarioDef def) { _def = def; }
            public PairBuilder ExpectGroups(string name, Func<IReadOnlyList<GroupRecord>, string> check)
            {
                _def.GroupExpectations.Add(new GroupExpectation { Name = name, Check = check });
                return this;
            }
            public ScenarioDef Build() => _def;
        }

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
                .Grouping(GroupMode.Off) // reads live views mid-turn (grouped AI turns step 9)
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
                        // No frame numbers in the hashed detail: they vary with the AI speed and grouping (grouped AI turns step 9).
                        ? ExpectResult.Pass("expiry #" + lines[0].Seq + ", then the orc's turn #" + next.Seq, lines[0].Seq, next.Seq)
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
