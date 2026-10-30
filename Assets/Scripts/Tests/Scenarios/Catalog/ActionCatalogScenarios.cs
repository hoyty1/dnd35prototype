#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DND35e.Identifiers;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 13 (docs/designs/action_catalog_batch13.md): the action catalog's shared executors in real fights.
    ///
    /// Step 4 (CMB-179, decision D2): total defense for both sides (PHB p.142): a standard action, +4 dodge AC from the
    /// moment it is taken until the creature's next turn starts, no attacks of opportunity meanwhile; the PC's Total
    /// Defense button and the AI's GameManager.RunAIAction run the same executor; a cornered panicked creature, PC or
    /// NPC, cowers and takes it (PHB p.311; delegated RAW reading: the action, not the Cowering condition).
    ///
    /// Step 5 (AI-075, AI-045, AI-039, AI-073 narrowed; decision D1, design choice C1): the AI attack guard
    /// (AiAttackGuard): an AI-run creature, NPC or party member, declares a weapon attack only on a target in the reach or
    /// range of the weapon in hand, with ammunition (PHB p.112-113, p.134, p.137); with none, its profile's out-of-reach
    /// alternatives (AiOutOfReach), the attack after an approach move included.
    ///
    /// Step 7 (AI-078, AI-053, AI-021; tags v2): AI target scoring reads typed facts through the knowledge provider
    /// (DND35.AI.Knowledge): exact matching, the "HP State: Staggered" rows as the BadlyHurt wound band, the armor-priority
    /// switch ahead of the profile, and a flanking term that tests a flank.
    ///
    /// Step 8 (AI-004, AI-003, AI-002; owner question Q4's default): every archetype gets a profile (Brute the plain
    /// BruteAIProfile, Caster without spells too), the Ranged behaviour runs the ranged routine and shoots, and a
    /// creature with a combat spell on a melee routine tries it first.
    ///
    /// Step 12 (AI-057, CMB-074; CMB-032 and AI-054 narrowed; decisions D4, D5): the reload, crawl and drop-prone shared
    /// executors (PHB p.115, p.99, p.141-143): a load provokes, Rapid Reload's free load too, an NPC reloads with its move
    /// action and fires with its standard action; a crawl provokes from every foe threatening a square of it, dropping prone is free; a prone confused
    /// creature that must attack the nearest creature stands first unless it already threatens it (delegated RAW reading).
    ///
    /// Step 13 (AI-054 narrowed, CORE-040; decision D6): aid another and waking an ally are shared executors (PHB p.154,
    /// p.280): a standard action paid first, then an attack roll against AC 10 that gives the ally +2; waking a sleeper
    /// is a standard action; neither provokes. The attack-wall executor serves the PC's wall buttons and the NPC's wall
    /// branch alike, and continues no UI, so an NPC's Wall of Ice attack advances the turn once.
    ///
    /// Step 15 (ITM-069, AI-073, AI-074, CMB-181; CHR-021 narrowed; decision D8): the equipment entries for both sides (PHB
    /// p.141-143, Table 8-2, Quick Draw p.98): drawing is a move action that does not provoke, free with Quick Draw or with a
    /// regular move at BAB +1; sheathing and picking up are move actions that provoke; readying a shield is a move action
    /// that does not; dropping is free. An archer with no arrows draws its melee weapon, the AI keeps its last melee weapon,
    /// and a panicked creature, PC or NPC, drops what it holds before it flees.
    /// </summary>
    public static class ActionCatalogScenarios
    {
        private const string NotePrefix = "b13s4 ";

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("total-defense-ui", TotalDefenseUi);
            yield return S("total-defense-no-aoo-ui", TotalDefenseNoAoOUi);
            yield return S("total-defense-ends-at-turn-start", TotalDefenseEndsAtTurnStart);
            yield return S("total-defense-cornered-panicked-ai", () => TotalDefenseCorneredPanicked(ui: false));
            yield return S("total-defense-cornered-panicked-ui", () => TotalDefenseCorneredPanicked(ui: true));
            yield return S("total-defense-cornered-frightened-ui", TotalDefenseCorneredFrightenedUi);
            yield return S("total-defense-cornered-panicked-disabled-ai", TotalDefenseCorneredPanickedDisabledAi);
            yield return S("ai-attack-only-in-reach-npc", () => AiAttackOnlyInReach(party: false));
            yield return S("ai-attack-only-in-reach-party", () => AiAttackOnlyInReach(party: true));
            yield return S("ai-kiter-approach-then-attack", AiKiterApproachThenAttack);
            yield return S("ai-kiter-no-ranged-no-retreat", () => AiKiterNoRangedNoRetreat(rangedProfile: false));
            yield return S("ai-kiter-no-ranged-no-retreat-ranged-profile", () => AiKiterNoRangedNoRetreat(rangedProfile: true));
            yield return S("ai-reach-weapon-dead-zone", AiReachWeaponDeadZone);
            yield return S("ai-archer-no-ammo-no-shot", AiArcherNoAmmoNoShot);
            yield return S("ai-melee-thrower-no-throw", AiMeleeThrowerNoThrow);
            yield return S("ai-wound-band-priority", AiWoundBandPriority);
            yield return S("ai-exact-race-match", AiExactRaceMatch);
            yield return S("ai-armor-priority-reachable", AiArmorPriorityReachable);
            yield return S("ai-flanking-priority", AiFlankingPriority);
            yield return S("brute-profile-melee-ai", BruteProfileMelee);
            yield return S("caster-archetype-without-spells-fights-ai", CasterArchetypeWithoutSpellsFights);
            yield return S("ranged-behaviour-shoots-ai", () => RangedBehaviourShoots(withProfile: true));
            yield return S("ranged-behaviour-shoots-no-profile-ai", () => RangedBehaviourShoots(withProfile: false));
            yield return S("melee-caster-casts-first-ai", MeleeCasterCastsFirst);
            yield return S("melee-caster-casts-first-party-ai", MeleeCasterCastsFirstParty);
            yield return S("melee-caster-skips-orisons-ai", MeleeCasterSkipsOrisons);
            yield return S("defensive-caster-withdraws-when-hurt-ai", DefensiveCasterWithdrawsWhenHurt);
            yield return S("ranged-behaviour-melee-only-approaches-ai", RangedBehaviourMeleeOnlyApproaches);
            yield return S("reload-provokes-ui", () => ReloadProvokes(ui: true));
            yield return S("reload-provokes-ai", () => ReloadProvokes(ui: false));
            yield return S("npc-reload-then-fire-ai", NpcReloadThenFire);
            yield return S("rapid-reload-free-ai", RapidReloadFree);
            yield return S("confused-prone-stands-ai", ConfusedProneStandsAi);
            yield return S("confused-prone-stands-ui", ConfusedProneStandsUi);
            yield return S("crawl-ui", () => Crawl(ui: true));
            yield return S("crawl-ai", () => Crawl(ui: false));
            yield return S("drop-prone-ui", DropProneUi);
            yield return S("reload-cancel-ui", ReloadCancelUi);
            yield return S("npc-heavy-reload-no-shot-ai", NpcHeavyReloadNoShot);
            yield return S("crawl-into-threat-ui", () => CrawlIntoThreat(ui: true));
            yield return S("crawl-into-threat-ai", () => CrawlIntoThreat(ui: false));
            yield return S("confused-prone-flee-stands-ai", ConfusedProneFleeStands);
            yield return S("confused-prone-crossbow-ai", ConfusedProneCrossbow);
            yield return S("aid-another-ui", () => AidAnother(ui: true));
            yield return S("aid-another-ai", () => AidAnother(ui: false));
            yield return S("wake-sleeping-ally-ui", () => WakeSleepingAlly(ui: true));
            yield return S("wake-sleeping-ally-ai", () => WakeSleepingAlly(ui: false));
            yield return S("npc-wall-of-ice-attack-ai", NpcWallOfIceAttack);
            yield return S("aid-another-defense-ai", AidAnotherDefense);
            yield return S("aid-another-expires-ai", AidAnotherExpires);
            yield return S("npc-wall-of-ice-break-ai", NpcWallOfIceBreak);
            yield return S("pc-wall-of-ice-attack-ui", () => PcWallAttack(ranged: true));
            yield return S("pc-wall-of-ice-reach-ui", () => PcWallAttack(ranged: false));
            yield return S("pc-break-wall-ui", PcBreakWall);
            yield return S("draw-weapon-move-ui", () => DrawWeaponMove(ui: true));
            yield return S("draw-weapon-move-ai", () => DrawWeaponMove(ui: false));
            yield return S("draw-with-move-bab1-ui", DrawWithMoveBab1Ui);
            yield return S("quick-draw-free-ui", () => QuickDrawFree(ui: true));
            yield return S("quick-draw-free-ai", () => QuickDrawFree(ui: false));
            yield return S("sheathe-provokes-ui", SheatheProvokesUi);
            yield return S("ready-shield-ui", ReadyShieldUi);
            yield return S("loose-shield-ui", LooseShieldUi);
            yield return S("twf-paired-draw-ui", TwfPairedDrawUi);
            yield return S("inventory-equip-locked-ui", InventoryEquipLockedUi);
            yield return S("pick-up-provokes-ui", () => PickUpProvokes(ui: true));
            yield return S("pick-up-provokes-ai", () => PickUpProvokes(ui: false));
            yield return S("archer-no-ammo-draws-melee-ai", ArcherNoAmmoDrawsMeleeAi);
            yield return S("npc-keeps-last-melee-weapon-ai", NpcKeepsLastMeleeWeaponAi);
            yield return S("panicked-drops-held-ai", PanickedDropsHeldAi);
            yield return S("panicked-drops-held-pc", PanickedDropsHeldPc);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("ActionCatalogScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        private static bool NoteDefense(ScenarioContext ctx, string key, string tag)
        {
            CharacterController c = ctx.Get(key);
            ctx.Note(NotePrefix + key + "." + tag + "=td=" + c.IsInTotalDefense + " bonus=" + c.Stats.TotalDefenseACBonus + " ac=" + c.Stats.ArmorClass);
            return true;
        }

        /// <summary>The last note "<paramref name="key"/>=..." as its value, or null.</summary>
        private static string NoteValue(TraceView v, string key, out int seq)
        {
            string prefix = NotePrefix + key + "=";
            TraceEvent n = v.Of("note").LastOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
            seq = n != null ? n.Seq : -1;
            return n != null ? n.Str("text").Substring(prefix.Length) : null;
        }

        private static int NotedAC(string noted)
        {
            int i = noted != null ? noted.IndexOf("ac=", StringComparison.Ordinal) : -1;
            return i >= 0 && int.TryParse(noted.Substring(i + 3), out int ac) ? ac : int.MinValue;
        }

        /// <summary>The noted state starts with "td=<paramref name="td"/> bonus=<paramref name="bonus"/>".</summary>
        private static Func<TraceView, ExpectResult> Noted(string key, bool td, int bonus)
            => v =>
            {
                string got = NoteValue(v, key, out int seq);
                if (got == null)
                    return ExpectResult.Inconclusive("no note " + key);
                string want = "td=" + td + " bonus=" + bonus + " ";
                return got.StartsWith(want, StringComparison.Ordinal)
                    ? ExpectResult.Pass(key + ": " + got, seq)
                    : ExpectResult.Fail(key + ": " + got + ", expected " + want.Trim(), seq);
            };

        /// <summary>
        /// The <paramref name="nth"/> attack of <paramref name="by"/> on <paramref name="target"/> in <paramref name="round"/>
        /// is against the AC noted under <paramref name="noteKey"/> just before it.
        /// </summary>
        private static Func<TraceView, ExpectResult> AttackAgainstNotedAC(string by, string target, int round, string noteKey)
            => v =>
            {
                int noted = NotedAC(NoteValue(v, noteKey, out _));
                if (noted == int.MinValue)
                    return ExpectResult.Inconclusive("no noted AC " + noteKey);
                TraceEvent attack = v.Attacks(by, target, aoo: false, round: round).FirstOrDefault();
                if (attack == null)
                    return ExpectResult.Fail("no round-" + round + " attack by " + by + " on " + target);
                int ac = attack.Int("ac");
                return ac == noted ? ExpectResult.Pass("attack vs AC " + ac, attack.Seq) : ExpectResult.Fail("attack vs AC " + ac + ", noted " + noted, attack.Seq);
            };

        // ── +4 AC against an attacker (PHB p.142) ───────────────────────

        /// <summary>
        /// A Ui fighter presses Total Defense (the catalog's shared executor through GameManager.RunPlayerAction): it
        /// spends the standard action and logs the action. The Scripted orc then attacks it: the attack is against the
        /// fighter's AC at its turn start plus 4 (a dodge bonus, PHB p.142).
        /// </summary>
        private static ScenarioDef TotalDefenseUi()
        {
            return Rules("rules/total-defense-ui", "A PC's total defense gives +4 dodge AC against the next attacks (PHB p.142; CMB-179, D2)")
                .Covers("CMB-179", "PHB p.142", "D2", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("def", ActorSource.Stats(() => RulesScenarios.Fighter("Defender", 3)), 10, 10, Control.Ui)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Tweak("def", RulesScenarios.StripOffHand)
                .Tweak("def", Sturdy)
                .Tweak("orc", Sturdy)
                .Initiative("def", "orc")
                .Turn("def", 1, Step.Assert("note the defender before total defense", ctx => NoteDefense(ctx, "def", "before")), Step.TotalDefense())
                .Turn("orc", 1, Step.Assert("note the defender in total defense", ctx => NoteDefense(ctx, "def", "orc1")), Step.Attack("def"))
                .Expect("The Total Defense step is done and logged", Expect.All(
                    Expect.StepStatus("def", 1, "TotalDefense", 0, "done"),
                    v => v.Log("Defender takes total defense: \\+4 dodge AC").Any(e => e.Round == 1)
                        ? ExpectResult.Pass("logged") : ExpectResult.Fail("no total defense line")))
                .Expect("Before: not in total defense", Noted("def.before", false, 0))
                .Expect("At the orc's turn: in total defense, +4", Noted("def.orc1", true, 4))
                .Expect("The defender's AC is 4 higher than before", v =>
                {
                    int before = NotedAC(NoteValue(v, "def.before", out _)), after = NotedAC(NoteValue(v, "def.orc1", out int seq));
                    if (before == int.MinValue || after == int.MinValue) return ExpectResult.Inconclusive("no noted AC");
                    return after == before + 4 ? ExpectResult.Pass("AC " + before + " -> " + after, seq) : ExpectResult.Fail("AC " + before + " -> " + after, seq);
                })
                .Expect("The orc's attack is against the raised AC", AttackAgainstNotedAC("orc", "def", 1, "def.orc1"))
                .Build();
        }

        // ── No attacks of opportunity (PHB p.142) ───────────────────────

        /// <summary>
        /// As rules/movement-aoo-move (a Scripted orc leaves the fighter's threatened square, which provokes, PHB p.137),
        /// but the Ui fighter took total defense first: it makes no attack of opportunity.
        /// </summary>
        private static ScenarioDef TotalDefenseNoAoOUi()
        {
            return Rules("rules/total-defense-no-aoo-ui", "A PC in total defense makes no attacks of opportunity (PHB p.142; CMB-179, D2)")
                .Covers("CMB-179", "PHB p.142", "PHB p.137", "D2", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("def", ActorSource.Stats(() => RulesScenarios.Fighter("Defender", 4)), 10, 10, Control.Ui)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("def", RulesScenarios.StripOffHand)
                .Initiative("def", "orc")
                .Turn("def", 1, Step.TotalDefense())
                .Turn("orc", 1, Step.Assert("note the defender in total defense", ctx => NoteDefense(ctx, "def", "orc1")), Step.Move(14, 10))
                .Expect("The defender is in total defense when the orc moves", Expect.All(
                    Expect.StepStatus("def", 1, "TotalDefense", 0, "done"),
                    Noted("def.orc1", true, 4)))
                .Expect("The orc's move out of the threatened square is done", Expect.StepStatus("orc", 1, "Move", 0, "done"))
                .Expect("No attack of opportunity by the defender", Expect.None("aoo", e => e.Str("by") == "def"))
                .Build();
        }

        // ── It ends at the creature's next turn (PHB p.142) ─────────────

        /// <summary>
        /// A Scripted fighter takes total defense through the AI runner (GameManager.RunAIAction, the NPC turn path). The
        /// orc's round-1 attack is against the raised AC; at the fighter's round-2 turn start total defense ends
        /// (CharacterController.StartNewTurn from GameManager.BeginCombatantTurn), so the orc's round-2 attack is against
        /// the fighter's AC from before.
        /// </summary>
        private static ScenarioDef TotalDefenseEndsAtTurnStart()
        {
            return Rules("rules/total-defense-ends-at-turn-start", "Total defense lasts until the creature's next turn starts (PHB p.142; CMB-179, D2)")
                .Covers("CMB-179", "PHB p.142", "D2", "D3", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("def", ActorSource.Stats(() => RulesScenarios.Fighter("Defender", 3)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Tweak("def", RulesScenarios.StripOffHand)
                .Tweak("def", Sturdy)
                .Tweak("orc", Sturdy)
                .Initiative("def", "orc")
                .Turn("def", 1, Step.Assert("note the defender before total defense", ctx => NoteDefense(ctx, "def", "before")), Step.TotalDefense())
                .Turn("orc", 1, Step.Assert("note the defender in total defense", ctx => NoteDefense(ctx, "def", "orc1")), Step.Attack("def"))
                .Turn("def", 2, Step.Assert("note the defender at its next turn", ctx => NoteDefense(ctx, "def", "def2")), Step.Pass())
                .Turn("orc", 2, Step.Assert("note the defender after its turn start", ctx => NoteDefense(ctx, "def", "orc2")), Step.Attack("def"))
                .Expect("The AI-path Total Defense step is done", Expect.StepStatus("def", 1, "TotalDefense", 0, "done"))
                .Expect("Round 1: in total defense at the orc's turn", Noted("def.orc1", true, 4))
                .Expect("Round 1: the orc's attack is against the raised AC", AttackAgainstNotedAC("orc", "def", 1, "def.orc1"))
                .Expect("Round 2: total defense ended at the defender's turn start", Noted("def.def2", false, 0))
                .Expect("Round 2: the AC is back to its value before total defense", v =>
                {
                    int before = NotedAC(NoteValue(v, "def.before", out _)), after = NotedAC(NoteValue(v, "def.orc2", out int seq));
                    if (before == int.MinValue || after == int.MinValue) return ExpectResult.Inconclusive("no noted AC");
                    return after == before ? ExpectResult.Pass("AC " + after, seq) : ExpectResult.Fail("AC " + before + " before, " + after + " in round 2", seq);
                })
                .Expect("Round 2: the orc's attack is against the AC from before", AttackAgainstNotedAC("orc", "def", 2, "def.orc2"))
                .Build();
        }

        // ── The cornered panicked creature (PHB p.311; D2) ──────────────

        /// <summary>
        /// The source panics the adjacent victim (a fixture), whose speed is 0, so it cannot flee: cornered, it cowers and
        /// does not attack, and takes total defense through the shared action (FrightenedBehaviorController.ExecuteDecision,
        /// GameManager.RunAIAction), not the Cowering condition. The AI twin is an AI goblin with a Scripted fighter as the
        /// source; the Ui twin is a panicked Ui fighter (its forced turn, the same controller) with a Scripted goblin as the
        /// source. In round 2 the source notes the victim (still in total defense: it ends at the victim's own turn start)
        /// and attacks it against the raised AC; the victim's round-2 turn takes total defense again.
        /// </summary>
        private static ScenarioDef TotalDefenseCorneredPanicked(bool ui)
        {
            ScenarioBuilder b = Rules(ui ? "rules/total-defense-cornered-panicked-ui" : "rules/total-defense-cornered-panicked-ai",
                    (ui ? "A cornered panicked PC" : "A cornered panicked creature") + " cowers and takes total defense (PHB p.311, p.142; CMB-179, D2)")
                .Covers("CMB-179", "PHB p.311", "PHB p.142", "D2", "PC_NPC_PARITY")
                .MaxRounds(2);
            if (ui)
                b = b.Npc("src", "goblin", 11, 10, Control.Scripted)
                     .Pc("vic", ActorSource.Stats(() => RulesScenarios.Fighter("Victim", 3)), 10, 10, Control.Ui)
                     .Tweak("vic", RulesScenarios.StripOffHand);
            else
                b = b.Pc("src", ActorSource.Stats(() => RulesScenarios.Fighter("Source", 3)), 10, 10, Control.Scripted)
                     .Npc("vic", "goblin", 11, 10, Control.Ai)
                     .Tweak("src", RulesScenarios.StripOffHand);
            return b.Tweak("vic", c => { Sturdy(c); c.Stats.BaseSpeed = 0; })
                .Tweak("src", Sturdy)
                .Initiative("src", "vic")
                .Turn("src", 1, Step.Assert("the source panics the victim (fixture)", ctx =>
                {
                    ctx.Gm.ApplyCondition(ctx.Get("vic"), CombatConditionType.Panicked, 3, ctx.Get("src"), sourceCategory: "Harness");
                    return ctx.Get("vic").HasCondition(CombatConditionType.Panicked);
                }), Step.Pass())
                .Turn("src", 2, Step.Assert("note the victim before the attack", ctx => NoteDefense(ctx, "vic", "src2")), Step.Attack("vic"))
                .Expect("Round 1: the victim is cornered and cowers", v =>
                    v.Log("is cornered and cowers in panic").Any(e => e.Round == 1) ? ExpectResult.Pass("cowers") : ExpectResult.Fail("no cower line"))
                .Expect("Round 1: it takes total defense (+4), not the Cowering condition", Expect.All(
                    v => v.Log("takes total defense: \\+4 dodge AC").Any(e => e.Round == 1) ? ExpectResult.Pass("total defense") : ExpectResult.Fail("no total defense line"),
                    v => v.Conds("vic", CombatConditionType.Cowering).Count == 0 ? ExpectResult.Pass("no Cowering") : ExpectResult.Fail("Cowering applied")))
                .Expect("Round 2: still in total defense at the source's turn", Noted("vic.src2", true, 4))
                .Expect("Round 2: the source's attack is against the raised AC", AttackAgainstNotedAC("src", "vic", 2, "vic.src2"))
                .Expect("Round 2: the victim takes total defense again", v =>
                    v.Log("takes total defense").Any(e => e.Round == 2) ? ExpectResult.Pass("again") : ExpectResult.Fail("not in round 2"))
                .Expect("The victim neither moves nor attacks", Expect.All(
                    Expect.None("attack", e => e.Str("attacker") == "vic"),
                    Expect.None("move", e => e.Str("actor") == "vic")))
                .Build();
        }

        // -- The cornered frightened PC (PHB p.310, p.142; Q1) --------------

        /// <summary>
        /// As rules/frightened-pc-cornered-fights: a frightened Ui fighter with speed 0 is cornered, so its forced turn sets
        /// the fighting-defensively stance (AI-044) and gives its menu back (owner decision Q1; PHB p.310). The stance was
        /// declared with no attack, so it does not bar total defense (PHB p.142: fighting defensively is declared with an
        /// attack); the player presses Total Defense, which ends the stance, so the goblin's round-2 attack is against the
        /// fighter's AC plus exactly 4, with no fighting-defensively +2 on top.
        /// </summary>
        private static ScenarioDef TotalDefenseCorneredFrightenedUi()
        {
            return Rules("rules/total-defense-cornered-frightened-ui", "A cornered frightened PC may take total defense, +4 and no more (PHB p.310, p.142; CMB-179, D2)")
                .Covers("CMB-179", "PHB p.310", "PHB p.142", "D2", "Q1", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Npc("gob", "goblin", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 10, 10, Control.Ui)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); c.Stats.BaseSpeed = 0; })
                .Tweak("gob", Sturdy)
                .Initiative("gob", "hero")
                .Turn("gob", 1, Step.Assert("the goblin frightens the hero (fixture)", ctx =>
                {
                    ctx.Gm.ApplyCondition(ctx.Get("hero"), CombatConditionType.Frightened, 3, ctx.Get("gob"), sourceCategory: "Harness");
                    return ctx.Get("hero").HasCondition(CombatConditionType.Frightened);
                }), Step.Pass())
                .Turn("hero", 1,
                    Step.Assert("note the cornered hero before total defense", ctx =>
                    {
                        ctx.Note(NotePrefix + "hero.fd1=" + ctx.Get("hero").IsFightingDefensively);
                        return NoteDefense(ctx, "hero", "before");
                    }),
                    Step.TotalDefense())
                .Turn("gob", 2,
                    Step.Assert("note the hero in total defense", ctx =>
                    {
                        ctx.Note(NotePrefix + "hero.fd2=" + ctx.Get("hero").IsFightingDefensively);
                        return NoteDefense(ctx, "hero", "gob2");
                    }),
                    Step.Attack("hero"))
                .Expect("The cornered hero's forced turn set the stance and gave its menu back", Expect.All(
                    v => v.Log("cornered by fear and fights defensively").Any(e => e.Round == 1) ? ExpectResult.Pass("cornered") : ExpectResult.Fail("no cornered line"),
                    v => NoteValue(v, "hero.fd1", out int s1) == "True" ? ExpectResult.Pass("stance set", s1) : ExpectResult.Fail("stance not set", s1)))
                .Expect("The player's Total Defense step runs", Expect.StepStatus("hero", 1, "TotalDefense", 0, "done"))
                .Expect("Round 2: in total defense (+4), the stance ended", Expect.All(
                    Noted("hero.gob2", true, 4),
                    v => NoteValue(v, "hero.fd2", out int s2) == "False" ? ExpectResult.Pass("stance ended", s2) : ExpectResult.Fail("stance still on", s2)))
                .Expect("Round 2: the AC is exactly 4 higher", v =>
                {
                    int before = NotedAC(NoteValue(v, "hero.before", out _)), after = NotedAC(NoteValue(v, "hero.gob2", out int seq));
                    if (before == int.MinValue || after == int.MinValue) return ExpectResult.Inconclusive("no noted AC");
                    return after == before + 4 ? ExpectResult.Pass("AC " + before + " -> " + after, seq) : ExpectResult.Fail("AC " + before + " -> " + after, seq);
                })
                .Expect("Round 2: the goblin's attack is against that AC", AttackAgainstNotedAC("gob", "hero", 2, "hero.gob2"))
                .Build();
        }

        // -- The cornered panicked creature at 0 hp (PHB p.145, p.311; D2) --

        /// <summary>
        /// As rules/total-defense-cornered-panicked-ai, but the AI goblin is disabled at 0 hp. Total defense is a standard
        /// action, strenuous for it (PHB p.145): the 1 damage would leave it dying, and a dying creature loses the dodge bonus
        /// with its Dexterity bonus at once. The panicked creature only "typically" takes total defense (PHB p.311), so it
        /// cowers without it (delegated RAW reading 2026-10-29, D2): no total defense, still at 0 hp and disabled.
        /// </summary>
        private static ScenarioDef TotalDefenseCorneredPanickedDisabledAi()
        {
            return Rules("rules/total-defense-cornered-panicked-disabled-ai", "A cornered panicked creature at 0 hp cowers without total defense (PHB p.145, p.311; CMB-179, D2)")
                .Covers("CMB-179", "PHB p.311", "PHB p.145", "D2", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("src", ActorSource.Stats(() => RulesScenarios.Fighter("Source", 3)), 10, 10, Control.Scripted)
                .Npc("vic", "goblin", 11, 10, Control.Ai)
                .Tweak("src", c => { RulesScenarios.StripOffHand(c); Sturdy(c); })
                .Tweak("vic", c => { c.Stats.BaseSpeed = 0; c.Stats.CurrentHP = 0; })
                .Initiative("src", "vic")
                .Turn("src", 1, Step.Assert("the source panics the disabled victim (fixture)", ctx =>
                {
                    ctx.Gm.ApplyCondition(ctx.Get("vic"), CombatConditionType.Panicked, 3, ctx.Get("src"), sourceCategory: "Harness");
                    return ctx.Get("vic").HasCondition(CombatConditionType.Panicked) && ctx.Get("vic").CurrentHPState == HPState.Disabled;
                }), Step.Pass())
                .Turn("src", 2, Step.Assert("note the victim after its turn", ctx =>
                {
                    CharacterController vic = ctx.Get("vic");
                    ctx.Note(NotePrefix + "vic.hp2=" + vic.Stats.CurrentHP + " " + vic.CurrentHPState);
                    return NoteDefense(ctx, "vic", "src2");
                }), Step.Pass())
                .Expect("Round 1: the victim is cornered and cowers", v =>
                    v.Log("is cornered and cowers in panic").Any(e => e.Round == 1) ? ExpectResult.Pass("cowers") : ExpectResult.Fail("no cower line"))
                .Expect("Round 1: it takes no total defense", v =>
                    v.Log("takes total defense").Any(e => e.Round == 1) ? ExpectResult.Fail("took total defense") : ExpectResult.Pass("none"))
                .Expect("Round 2: not in total defense", Noted("vic.src2", false, 0))
                .Expect("Round 2: still at 0 hp and disabled", v =>
                {
                    string got = NoteValue(v, "vic.hp2", out int seq);
                    if (got == null) return ExpectResult.Inconclusive("no hp note");
                    return got == "0 Disabled" ? ExpectResult.Pass(got, seq) : ExpectResult.Fail(got + ", expected 0 Disabled", seq);
                })
                .Build();
        }
        // ── Step 5: the AI attack guard (D1, C1) ────────────────────────

        private static global::Inventory Inv(CharacterController c)
            => c != null && c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;

        /// <summary>Every spell slot spent: nothing is castable (the fixture of AI-075, a caster that casts nothing).</summary>
        private static void SpendAllSlots(CharacterController c)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                return;
            foreach (SpellSlot slot in sc.SpellSlots)
                if (slot != null)
                    slot.IsUsed = true;
            sc.SyncPreparedSpellsFromSlots();
        }

        private static void Wield(CharacterController c, ItemData weapon)
        {
            global::Inventory inv = Inv(c);
            if (inv == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no inventory");
            inv.LeftHandSlot = null;
            inv.DirectEquip(weapon, EquipSlot.RightHand);
            inv.RecalculateStats();
        }

        /// <summary>Every arrow stack removed from the inventory (an empty quiver, PHB p.113).</summary>
        private static void EmptyQuiver(CharacterController c)
        {
            global::Inventory inv = Inv(c);
            if (inv == null || inv.GeneralSlots == null)
                return;
            for (int i = 0; i < inv.GeneralSlots.Length; i++)
            {
                ItemData item = inv.GeneralSlots[i];
                if (item != null && item.IsAmmunition && item.AmmoType == AmmunitionType.Arrow)
                    inv.GeneralSlots[i] = null;
            }
            inv.RecalculateStats();
        }

        private static CharacterStats StaffWizard(string name, int level)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Wizard",
                str: 10, dex: 12, con: 12, wis: 10, intelligence: 16, cha: 10,
                bab: 2, armorBonus: 0, shieldBonus: 0,
                damageDice: 6, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 6 * level, raceName: "Human");
            s.CharacterAlignment = Alignment.TrueNeutral;
            s.EnsureMulticlassDataInitialized();
            return s;
        }

        /// <summary>Where <paramref name="key"/> stood just before trace event <paramref name="seq"/>: its last move's square, else <paramref name="start"/>.</summary>
        private static UnityEngine.Vector2Int PosBefore(TraceView v, string key, UnityEngine.Vector2Int start, int seq)
        {
            TraceEvent move = v.Moves(key).LastOrDefault(e => e.Seq < seq);
            return move != null && move.Get("to") is UnityEngine.Vector2Int to ? to : start;
        }

        private static Func<TraceView, ExpectResult> NoLog(string regex, string what)
            => v =>
            {
                List<TraceEvent> lines = v.Log(regex);
                return lines.Count == 0 ? ExpectResult.Pass("no " + what) : ExpectResult.Fail(lines.Count + " " + what + " line(s)", lines.Select(e => e.Seq).ToArray());
            };

        /// <summary>
        /// AI-075 (seen in the batch 12 soak): an AI-run caster with nothing castable and a quarterstaff, one foe adjacent and
        /// one far away. The guard (D1) lets it attack only the adjacent foe, the one in its staff's reach (PHB p.137); it
        /// never declares an attack on the far one and never logs "makes no attack: target is out of reach". As an Enemy
        /// NPC (mist_wizard: the kiter routine, the Evoker profile) and as an AI-run party wizard (the Evoker profile):
        /// the same guard for both sides. The caster does not back off either: it has no ranged attack or ranged spell
        /// (AI-039).
        /// </summary>
        private static ScenarioDef AiAttackOnlyInReach(bool party)
        {
            string id = party ? "rules/ai-attack-only-in-reach-party" : "rules/ai-attack-only-in-reach-npc";
            ScenarioBuilder b = Rules(id, (party ? "An AI-run party wizard" : "An AI-run Enemy wizard")
                    + " with nothing to cast attacks the adjacent foe, never the far one (AI-075; D1; PHB p.137)")
                .Covers("AI-075", "AI-039", "D1", "PHB p.137", "PC_NPC_PARITY")
                .MaxRounds(2);
            if (party)
            {
                b = b.Pc("wiz", ActorSource.Stats(() => StaffWizard("Staff Wizard", 5)), 10, 10, Control.Ai)
                    .Profile("wiz", () => DND35.AI.AIProfile.CreateRuntime<DND35.AI.Profiles.EvokerAIProfile>())
                    .Npc("near", "target_dummy", 11, 10, Control.Idle)
                    .Npc("far", "target_dummy", 17, 10, Control.Idle)
#pragma warning disable CS0618 // the quarterstaff's string id
                    .Tweak("wiz", c => { Wield(c, ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF)); SpendAllSlots(c); Sturdy(c); });
#pragma warning restore CS0618
            }
            else
            {
                b = b.Npc("wiz", "mist_wizard", 10, 10, Control.Ai)
                    .Pc("near", ActorSource.Stats(() => RulesScenarios.Fighter("Near Hero", 3)), 11, 10, Control.Idle)
                    .Pc("far", ActorSource.Stats(() => StaffWizard("Far Hero", 1)), 17, 10, Control.Idle)
                    .Tweak("wiz", c => { SpendAllSlots(c); Sturdy(c); });
            }
            return b
                .Tweak("near", Sturdy)
                .Tweak("far", Sturdy)
                .Initiative("wiz", "near", "far")
                .Expect("The wizard never attacks the far foe", v =>
                {
                    List<TraceEvent> far = v.Attacks("wiz", "far", aoo: false);
                    return far.Count == 0 ? ExpectResult.Pass("no attack on the far foe") : ExpectResult.Fail(far.Count + " attack(s) on the far foe", far.Select(e => e.Seq).ToArray());
                })
                .Expect("Round 1: it attacks the adjacent foe with its staff", v =>
                {
                    List<TraceEvent> near = v.Attacks("wiz", "near", aoo: false, round: 1);
                    return near.Count > 0 ? ExpectResult.Pass(near.Count + " attack(s)", near.Select(e => e.Seq).ToArray()) : ExpectResult.Fail("no round-1 attack on the adjacent foe");
                })
                .Expect("No attack is declared out of reach", NoLog("makes no attack: target is out of reach", "out-of-reach"))
                .Expect("It does not back off without a ranged attack (AI-039)", NoLog("retreats to maintain distance", "retreat"))
                .Build();
        }

        /// <summary>
        /// AI-045: an AI-run Enemy wizard (mist_wizard, the kiter routine, the Evoker profile) with nothing castable holds a
        /// dagger (thrown: five 10-ft. increments, 50 ft., PHB p.134) and its foe is 70 ft. away. No target passes the guard,
        /// so its profile moves it to its preferred range (the C1 ranged list: another target, the preferred range, an
        /// approach); the move brings the foe within 50 ft., and the standard action left throws the dagger the same turn
        /// (before step 5 the turn ended after the move). It carries a second dagger: since batch 13 step 15 the AI never
        /// throws its last weapon (AI-074).
        /// </summary>
        private static ScenarioDef AiKiterApproachThenAttack()
        {
            return Rules("rules/ai-kiter-approach-then-attack", "An AI kiter that moves into range attacks with its standard action left (AI-045; C1; PHB p.134)")
                .Covers("AI-045", "C1", "D1", "PHB p.134")
                .MaxRounds(1)
                .Npc("wiz", "mist_wizard", 2, 10, Control.Ai)
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Distant Hero", 3)), 16, 10, Control.Idle)
                .Tweak("wiz", c =>
                {
                    Wield(c, ItemDatabase.CloneItem(ItemID.WeaponDagger));
                    Inv(c).AddItem(ItemDatabase.CloneItem(ItemID.WeaponDagger)); // a second to draw, so the throw keeps it a weapon (AI-074)
                    Inv(c).RecalculateStats();
                    SpendAllSlots(c);
                    Sturdy(c);
                })
                .Tweak("foe", Sturdy)
                .Initiative("wiz", "foe")
                .Expect("Round 1: the wizard moves, then throws the dagger at the foe", v =>
                {
                    TraceEvent move = v.Moves("wiz", 1).FirstOrDefault();
                    if (move == null)
                        return ExpectResult.Fail("no round-1 move");
                    TraceEvent attack = v.Attacks("wiz", "foe", aoo: false, round: 1).FirstOrDefault();
                    if (attack == null)
                        return ExpectResult.Fail("moved to " + move.Get("to") + " but made no attack", move.Seq);
                    return attack.Seq > move.Seq && attack.Bool("ranged")
                        ? ExpectResult.Pass("moved to " + move.Get("to") + ", then a ranged attack", move.Seq, attack.Seq)
                        : ExpectResult.Fail("attack seq " + attack.Seq + " ranged=" + attack.Bool("ranged") + ", move seq " + move.Seq, move.Seq, attack.Seq);
                })
                .Build();
        }

        /// <summary>
        /// AI-039: an AI-run Enemy archer (test_archer_gust, the kiter routine) with an empty quiver and a foe adjacent. It
        /// has no ranged attack it can use (PHB p.113) and no spell, so it does not retreat to keep its distance, takes no
        /// tactical 5-foot step to avoid provoking with a shot it cannot fire, and declares no shot. The
        /// <paramref name="rangedProfile"/> variant gives it the Ranged profile (RangedAIProfile, the profile ranged NPCs
        /// get: C1's ranged list, another target, the preferred range, an approach): its preferred-range move is skipped too,
        /// since it has no ranged means (AiOutOfReach, AIService.HasRangedMeans), so it never leaves the threatened square
        /// and provokes no attack of opportunity (PHB p.137).
        /// </summary>
        private static ScenarioDef AiKiterNoRangedNoRetreat(bool rangedProfile)
        {
            string id = rangedProfile ? "rules/ai-kiter-no-ranged-no-retreat-ranged-profile" : "rules/ai-kiter-no-ranged-no-retreat";
            ScenarioBuilder b = Rules(id, "An AI kiter with no usable ranged attack does not retreat"
                    + (rangedProfile ? ", nor move to its preferred range" : "") + " (AI-039; D1; PHB p.113)")
                .Covers("AI-039", "AI-073", "D1", "C1", "PHB p.113")
                .MaxRounds(1)
                .Npc("archer", "test_archer_gust", 10, 10, Control.Ai);
            if (rangedProfile)
                b = b.Profile("archer", () => DND35.AI.AIProfile.CreateRuntime<DND35.AI.Profiles.RangedAIProfile>());
            return b
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Adjacent Hero", 3)), 11, 10, Control.Idle)
                .Tweak("archer", c => { EmptyQuiver(c); Sturdy(c); })
                .Tweak("foe", Sturdy)
                .Initiative("archer", "foe")
                .Expect("It does not retreat or step away", v =>
                {
                    List<TraceEvent> moves = v.Moves("archer", 1);
                    return moves.Count == 0 ? ExpectResult.Pass("no move") : ExpectResult.Fail(moves.Count + " move(s), to " + moves[0].Get("to"), moves.Select(e => e.Seq).ToArray());
                })
                .Expect("It provokes no attack of opportunity", v =>
                {
                    List<TraceEvent> aoos = v.AoOs("foe", "archer");
                    return aoos.Count == 0 ? ExpectResult.Pass("no AoO") : ExpectResult.Fail(aoos.Count + " AoO(s)", aoos.Select(e => e.Seq).ToArray());
                })
                .Expect("No preferred-range move", NoLog("moves to get a better shot", "preferred-range move"))
                .Expect("No retreat line", NoLog("retreats to maintain distance|repositions to avoid|tactical 5-foot step", "retreat"))
                .Expect("It declares no shot (it may fight with the longsword it draws, AI-073)", NoBowShot("archer"))
                .Build();
        }

        /// <summary>
        /// A reach weapon's dead zone (PHB p.112, p.137): an AI-run orc (orc_warrior, the melee routine) wields a longspear
        /// (10-ft. reach, cannot attack an adjacent foe); one foe stands adjacent and one 10 ft. away. Every attack it
        /// declares is on a foe that is not adjacent to it where it stands, and it attacks in round 1 (the guard lets it
        /// pick the foe in reach, C1's "another target").
        /// </summary>
        private static ScenarioDef AiReachWeaponDeadZone()
        {
            var start = new UnityEngine.Vector2Int(10, 10);
            var adjStart = new UnityEngine.Vector2Int(11, 10);
            var reachStart = new UnityEngine.Vector2Int(10, 12);
            return Rules("rules/ai-reach-weapon-dead-zone", "An AI spearman never attacks into its longspear's dead zone (D1; PHB p.112, p.137)")
                .Covers("D1", "C1", "PHB p.112", "PHB p.137")
                .MaxRounds(2)
                .Npc("orc", "orc_warrior", start.x, start.y, Control.Ai)
                .Pc("adj", ActorSource.Stats(() => RulesScenarios.Fighter("Adjacent Hero", 3)), adjStart.x, adjStart.y, Control.Idle)
                .Pc("reach", ActorSource.Stats(() => RulesScenarios.Fighter("Reach Hero", 3)), reachStart.x, reachStart.y, Control.Idle)
#pragma warning disable CS0618 // the longspear's string id
                .Tweak("orc", c => { Wield(c, ItemDatabase.CloneItem(ItemIDs.LONGSPEAR)); Sturdy(c); })
#pragma warning restore CS0618
                .Tweak("adj", Sturdy)
                .Tweak("reach", Sturdy)
                .Initiative("orc", "adj", "reach")
                .Expect("Every attack of the spearman is on a foe outside its dead zone", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("orc", null, aoo: false);
                    if (attacks.Count == 0)
                        return ExpectResult.Inconclusive("no attack");
                    foreach (TraceEvent a in attacks)
                    {
                        UnityEngine.Vector2Int at = PosBefore(v, "orc", start, a.Seq);
                        UnityEngine.Vector2Int foe = PosBefore(v, a.Str("target"), a.Str("target") == "adj" ? adjStart : reachStart, a.Seq);
                        int d = Math.Max(Math.Abs(at.x - foe.x), Math.Abs(at.y - foe.y));
                        if (d <= 1)
                            return ExpectResult.Fail("attack on " + a.Str("target") + " from " + at + " at " + foe + " (adjacent)", a.Seq);
                    }
                    return ExpectResult.Pass(attacks.Count + " attack(s), none adjacent", attacks.Select(e => e.Seq).ToArray());
                })
                .Expect("Round 1: it attacks", v =>
                {
                    List<TraceEvent> r1 = v.Attacks("orc", null, aoo: false, round: 1);
                    return r1.Count > 0 ? ExpectResult.Pass(r1.Count + " attack(s) on " + string.Join(",", r1.Select(e => e.Str("target"))), r1.Select(e => e.Seq).ToArray())
                        : ExpectResult.Fail("no round-1 attack");
                })
                .Expect("No attack is declared out of reach", NoLog("makes no attack: target is out of reach", "out-of-reach"))
                .Build();
        }

        /// <summary>
        /// Design choice C1 (2026-10-29): the melee routines ask the attack guard for melee reach only, so an AI-run orc
        /// (orc_warrior, the melee routine) holding a dagger, one foe adjacent and one 20 ft. away (inside the dagger's 50-ft.
        /// thrown range, PHB p.134), attacks only in melee: every attack it makes is from a square adjacent to its target
        /// (positions rebuilt from the trace's moves; it may charge the far foe, a melee attack, PHB p.154), and it never
        /// throws its only weapon. The trace's "ranged" flag cannot tell: a dagger's melee strike is flagged ranged (CMB-105).
        /// </summary>
        private static ScenarioDef AiMeleeThrowerNoThrow()
        {
            var start = new UnityEngine.Vector2Int(10, 10);
            var adjStart = new UnityEngine.Vector2Int(11, 10);
            var farStart = new UnityEngine.Vector2Int(10, 14);
            return Rules("rules/ai-melee-thrower-no-throw", "An AI melee creature with a dagger attacks in melee and does not throw it at a far foe (C1; PHB p.113, p.134)")
                .Covers("C1", "D1", "PHB p.113", "PHB p.134")
                .MaxRounds(2)
                .Npc("orc", "orc_warrior", 10, 10, Control.Ai)
                .Pc("adj", ActorSource.Stats(() => RulesScenarios.Fighter("Adjacent Hero", 3)), 11, 10, Control.Idle)
                .Pc("far", ActorSource.Stats(() => StaffWizard("Far Hero", 1)), 10, 14, Control.Idle)
                .Tweak("orc", c => { Wield(c, ItemDatabase.CloneItem(ItemID.WeaponDagger)); Sturdy(c); })
                .Tweak("adj", Sturdy)
                .Tweak("far", Sturdy)
                .Initiative("orc", "adj", "far")
                .Expect("Every attack is a melee attack from an adjacent square (no throw)", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("orc", null, aoo: false);
                    if (attacks.Count == 0)
                        return ExpectResult.Fail("no attack");
                    foreach (TraceEvent a in attacks)
                    {
                        UnityEngine.Vector2Int at = PosBefore(v, "orc", start, a.Seq + 1);
                        UnityEngine.Vector2Int foe = PosBefore(v, a.Str("target"), a.Str("target") == "adj" ? adjStart : farStart, a.Seq);
                        int d = Math.Max(Math.Abs(at.x - foe.x), Math.Abs(at.y - foe.y));
                        if (d > 1)
                            return ExpectResult.Fail("attack on " + a.Str("target") + " from " + at + " at " + foe + " (" + d + " squares: a throw)", a.Seq);
                    }
                    return ExpectResult.Pass(attacks.Count + " attack(s), all adjacent", attacks.Select(e => e.Seq).ToArray());
                })
                .Expect("Round 1: it attacks", v =>
                {
                    List<TraceEvent> r1 = v.Attacks("orc", null, aoo: false, round: 1);
                    return r1.Count > 0 ? ExpectResult.Pass(r1.Count + " attack(s) on " + string.Join(",", r1.Select(e => e.Str("target"))), r1.Select(e => e.Seq).ToArray())
                        : ExpectResult.Fail("no round-1 attack");
                })
                .Build();
        }

        /// <summary>
        /// AI-073: an AI-run Enemy archer (test_archer_gust) with an empty quiver and a foe 30 ft. away declares no shot (the
        /// guard refuses a projectile weapon without ammunition, PHB p.113), so the refused shot's "has no Arrow ammunition"
        /// line never appears, and it does not walk to its preferred range for a shot it can never take (AiOutOfReach skips
        /// the move without ranged means). Since batch 13 step 15 it draws its stowed longsword instead
        /// (rules/archer-no-ammo-draws-melee-ai), so attacks with the sword may follow; none is a bow shot.
        /// </summary>
        private static ScenarioDef AiArcherNoAmmoNoShot()
        {
            return Rules("rules/ai-archer-no-ammo-no-shot", "An AI archer with an empty quiver declares no shot (AI-073; D1; PHB p.113)")
                .Covers("AI-073", "D1", "PHB p.113")
                .MaxRounds(2)
                .Npc("archer", "test_archer_gust", 16, 10, Control.Ai)
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 10, 10, Control.Idle)
                .Tweak("archer", c => { EmptyQuiver(c); Sturdy(c); })
                .Tweak("foe", Sturdy)
                .Initiative("archer", "foe")
                .Expect("It declares no shot", NoBowShot("archer"))
                .Expect("No refused shot for want of arrows", NoLog("has no Arrow ammunition", "no-ammunition"))
                .Expect("No move to a better shot", NoLog("moves to get a better shot", "preferred-range move"))
                .Build();
        }

        /// <summary>No attack of <paramref name="by"/>'s with a bow (an empty quiver: no shot, PHB p.113).</summary>
        private static Func<TraceView, ExpectResult> NoBowShot(string by)
            => v =>
            {
                List<TraceEvent> attacks = v.Attacks(by, null, aoo: false);
                List<TraceEvent> shots = attacks.Where(e => (e.Str("weapon") ?? "").IndexOf("bow", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                return shots.Count == 0 ? ExpectResult.Pass("no bow shot (" + attacks.Count + " other attack(s): " + string.Join(",", attacks.Select(e => e.Str("weapon"))) + ")")
                    : ExpectResult.Fail(shots.Count + " bow shot(s)", shots.Select(e => e.Seq).ToArray());
            };

        // ── batch 13 step 7: typed facts in AI target scoring ──────────

        /// <summary>A plain melee profile that scores only the given fact priorities (no wounded or isolation terms).</summary>
        private static Func<DND35.AI.AIProfile> FactProfile(params DND35.AI.TagPriority[] priorities)
            => () =>
            {
                DND35.AI.AIProfile p = DND35.AI.AIProfile.CreateRuntime<DND35.AI.AIProfile>();
                p.CombatStyle = DND35.AI.CombatStyle.Melee;
                p.PrioritizeWounded = false;
                p.PrioritizeIsolated = false;
                p.TagPriorities = new List<DND35.AI.TagPriority>(priorities);
                return p;
            };

        /// <summary>The attacker's first round-1 attack that is not an attack of opportunity is on <paramref name="want"/>.</summary>
        private static Func<TraceView, ExpectResult> FirstAttackOn(string by, string want)
            => v =>
            {
                TraceEvent first = v.Attacks(by, null, aoo: false, round: 1).FirstOrDefault();
                if (first == null)
                    return ExpectResult.Inconclusive("no round-1 attack");
                return first.Str("target") == want
                    ? ExpectResult.Pass("first attack on " + want, first.Seq)
                    : ExpectResult.Fail("first attack on " + first.Str("target") + ", expected " + want, first.Seq);
            };

        /// <summary>
        /// AI-078 (wound bands): an AI-run orc whose profile weights "HP State: Staggered" (+10), as seven shipped profiles
        /// do to finish off a wounded foe, and "Armor: Unarmored" (+3), has two adjacent foes: an armored fighter at 6 hit
        /// points (below half: BadlyHurt) and an unarmored one at full hit points. Staggered is nonlethal damage equal to
        /// current hit points (PHB p.313), which neither has, so before batch 13 step 7 the row never fired and the orc took
        /// the unarmored foe; the row now reads the BadlyHurt wound band, and the orc attacks the wounded fighter.
        /// </summary>
        private static ScenarioDef AiWoundBandPriority()
        {
            return Rules("rules/ai-wound-band-priority", "An AI profile's 'HP State: Staggered' weight picks the badly hurt foe (AI-078; PHB p.313)")
                .Covers("AI-078", "PHB p.313", "batch 13 step 7")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 10, 10, Control.Ai)
                .Profile("orc", FactProfile(new DND35.AI.TagPriority("HP State: Staggered", 10f), new DND35.AI.TagPriority("Armor: Unarmored", 3f)))
                .Pc("hurt", ActorSource.Stats(() => RulesScenarios.Fighter("Hurt Hero", 3)), 11, 10, Control.Idle)
                .Pc("fresh", ActorSource.Stats(() => RulesScenarios.Fighter("Fresh Hero", 3)), 10, 11, Control.Idle)
                .Hp("hurt", 6)
                .Tweak("orc", Sturdy)
                .Tweak("fresh", c => { RulesScenarios.StripArmor(c); Sturdy(c); })
                .Initiative("orc", "hurt", "fresh")
                .Expect("The hurt fighter starts badly hurt (below half its hit points), not staggered", v =>
                {
                    TraceEvent a = v.Of("actor").FirstOrDefault(e => e.Str("key") == "hurt");
                    if (a == null)
                        return ExpectResult.Inconclusive("no actor event");
                    int hp = a.Int("hp"), max = a.Int("maxHp");
                    DND35.AI.Knowledge.WoundBand band = DND35.AI.Knowledge.KnowledgeFacts.WoundBandOf(hp, max);
                    return band == DND35.AI.Knowledge.WoundBand.BadlyHurt
                        ? ExpectResult.Pass(hp + "/" + max + " " + band, a.Seq)
                        : ExpectResult.Fail(hp + "/" + max + " " + band, a.Seq);
                })
                .Expect("Round 1: the orc attacks the badly hurt fighter", FirstAttackOn("orc", "hurt"))
                .Build();
        }

        /// <summary>
        /// AI-078 (exact matching): an AI-run orc whose profile weights "Elf" (+10, a bare species) and "Armor: Unarmored"
        /// (+3) has two adjacent foes: an armored elf and an unarmored half-elf. The old two-way substring match counted
        /// "Elf" in "Race: Half-Elf", so both got the +10 and the orc took the unarmored half-elf; the species fact is now
        /// matched whole, and the orc attacks the elf.
        /// </summary>
        private static ScenarioDef AiExactRaceMatch()
        {
            return Rules("rules/ai-exact-race-match", "An AI profile's 'Elf' weight matches an elf, not a half-elf (AI-078)")
                .Covers("AI-078", "batch 13 step 7")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 10, 10, Control.Ai)
                .Profile("orc", FactProfile(new DND35.AI.TagPriority("Elf", 10f), new DND35.AI.TagPriority("Armor: Unarmored", 3f)))
                .Pc("elf", ActorSource.Stats(() => RulesScenarios.Fighter("Elf Hero", 3)), 11, 10, Control.Idle)
                .Pc("halfelf", ActorSource.Stats(() => RulesScenarios.Fighter("Half-Elf Hero", 3)), 10, 11, Control.Idle)
                .Tweak("orc", Sturdy)
                .Tweak("elf", c => { c.SetDisplayedRace("Elf"); Sturdy(c); })
                .Tweak("halfelf", c => { c.SetDisplayedRace("Half-Elf"); RulesScenarios.StripArmor(c); Sturdy(c); })
                .Initiative("orc", "elf", "halfelf")
                .Expect("Round 1: the orc attacks the elf", FirstAttackOn("orc", "elf"))
                .Build();
        }

        /// <summary>
        /// AI-053 (armor priority): an AI-run orc with the armor-priority switch (the armor-targeting preset's "Uses
        /// Armor-Based Targeting", CharacterController.AIControl) and a profile that prefers medium armor (+20) has two
        /// adjacent foes: one in the Fighter kit's scale mail (medium) and one unarmored. Before batch 13 step 7 the
        /// profile path ran first and the switch was unreachable, so the orc took the armored foe; the switch now comes
        /// first and picks the least armored foe (unarmored 100, medium 50).
        /// </summary>
        private static ScenarioDef AiArmorPriorityReachable()
        {
            return Rules("rules/ai-armor-priority-reachable", "The AI armor-priority switch is reachable for a creature with a profile (AI-053)")
                .Covers("AI-053", "batch 13 step 7")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 10, 10, Control.Ai)
                .Profile("orc", FactProfile(new DND35.AI.TagPriority("Armor: Medium Armor", 20f)))
                .Pc("armored", ActorSource.Stats(() => RulesScenarios.Fighter("Armored Hero", 3)), 11, 10, Control.Idle)
                .Pc("bare", ActorSource.Stats(() => RulesScenarios.Fighter("Bare Hero", 3)), 10, 11, Control.Idle)
                .Tweak("orc", c => { c.AIControl |= DND35.AI.Knowledge.AIControlFlags.ArmorPriorityTargeting; Sturdy(c); })
                .Tweak("armored", Sturdy)
                .Tweak("bare", c => { RulesScenarios.StripArmor(c); Sturdy(c); })
                .Initiative("orc", "armored", "bare")
                .Expect("Round 1: the orc attacks the unarmored foe", FirstAttackOn("orc", "bare"))
                .Build();
        }

        /// <summary>
        /// AI-053 (flanking term): an AI-run orc without a profile (the default score, AIService.GetTargetPriority) has two
        /// adjacent foes: a fighter 3 that an idle goblin ally threatens from the opposite side, so the orc flanks it
        /// (PHB p.153), and a fighter 4, which scores 0.7 more as a threat. The old "flanking" term only tested that the orc
        /// threatens the target, true for both, so the orc took the fighter 4; the term now tests a flank (+2.5), and the
        /// orc goes for the flanked fighter. A profile-less orc opens with a trip on a standing foe (AI-035), so the check
        /// reads its first round-1 attack or maneuver, whichever comes first.
        /// </summary>
        private static ScenarioDef AiFlankingPriority()
        {
            return Rules("rules/ai-flanking-priority", "The default AI target score's flanking term needs a flank, not just threat (AI-053; PHB p.153)")
                .Covers("AI-053", "PHB p.153", "batch 13 step 7")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 10, 10, Control.Ai)
                .Profile("orc", () => null)
                .Npc("ally", "goblin", 12, 10, Control.Idle)
                .Pc("flanked", ActorSource.Stats(() => RulesScenarios.Fighter("Flanked Hero", 3)), 11, 10, Control.Idle)
                .Pc("other", ActorSource.Stats(() => RulesScenarios.Fighter("Veteran Hero", 4)), 10, 11, Control.Idle)
                .Tweak("orc", Sturdy)
                .Tweak("ally", Sturdy)
                .Tweak("flanked", Sturdy)
                .Tweak("other", Sturdy)
                .Initiative("orc", "ally", "flanked", "other")
                .Expect("Round 1: the orc's first attack or maneuver is on the flanked fighter", v =>
                {
                    TraceEvent first = v.Attacks("orc", null, aoo: false, round: 1)
                        .Concat(v.Maneuvers("orc").Where(e => e.Round == 1 && !e.Bool("counter")))
                        .OrderBy(e => e.Seq)
                        .FirstOrDefault();
                    if (first == null)
                        return ExpectResult.Inconclusive("no round-1 attack or maneuver");
                    return first.Str("target") == "flanked"
                        ? ExpectResult.Pass(first.Ev + " on flanked", first.Seq)
                        : ExpectResult.Fail(first.Ev + " on " + first.Str("target") + ", expected flanked", first.Seq);
                })
                .Build();
        }

        // ── batch 13 step 8: archetype profiles, the Ranged behaviour, melee casters ──

        /// <summary>The spawned actor's AI profile type (the trace's actor event) is <paramref name="typeName"/>.</summary>
        private static Func<TraceView, ExpectResult> ProfileIs(string key, string typeName)
            => v =>
            {
                TraceEvent a = v.Of("actor").FirstOrDefault(e => e.Str("key") == key);
                if (a == null)
                    return ExpectResult.Inconclusive("no actor event for " + key);
                string got = a.Str("profile");
                return got == typeName
                    ? ExpectResult.Pass(key + " profile " + got, a.Seq)
                    : ExpectResult.Fail(key + " profile " + (got ?? "null") + ", expected " + typeName, a.Seq);
            };

        /// <summary><paramref name="by"/> attacks <paramref name="target"/> (not an AoO) by round <paramref name="round"/>.</summary>
        private static Func<TraceView, ExpectResult> AttacksBy(string by, string target, int round)
            => v =>
            {
                TraceEvent a = v.Attacks(by, target, aoo: false).FirstOrDefault(e => e.Round <= round);
                return a != null
                    ? ExpectResult.Pass("round-" + a.Round + " attack on " + target, a.Seq)
                    : ExpectResult.Fail("no attack on " + target + " by round " + round);
            };

        /// <summary>
        /// <paramref name="by"/> fights <paramref name="target"/> in melee by round <paramref name="round"/>: a melee attack
        /// (not an AoO) or a combat maneuver of its own (not a counter), the profile-less and Humanoid trip included.
        /// </summary>
        private static Func<TraceView, ExpectResult> FightsInMeleeBy(string by, string target, int round)
            => v =>
            {
                TraceEvent a = v.Attacks(by, target, aoo: false).FirstOrDefault(e => e.Round <= round && !e.Bool("ranged"));
                TraceEvent m = v.Maneuvers(by).FirstOrDefault(e => e.Round <= round && !e.Bool("counter") && e.Str("target") == target);
                TraceEvent first = a == null ? m : (m == null || a.Seq < m.Seq ? a : m);
                return first != null
                    ? ExpectResult.Pass("round-" + first.Round + " " + first.Ev + " on " + target, first.Seq)
                    : ExpectResult.Fail("no melee attack or maneuver on " + target + " by round " + round);
            };

        private static Func<TraceView, ExpectResult> NoManeuver(string by)
            => v =>
            {
                List<TraceEvent> m = v.Maneuvers(by).Where(e => !e.Bool("counter")).ToList();
                return m.Count == 0
                    ? ExpectResult.Pass("no maneuver")
                    : ExpectResult.Fail(m.Count + " maneuver(s), first " + m[0].Get("type"), m.Select(e => e.Seq).ToArray());
            };

        /// <summary>
        /// AI-004 (Q4's default): an ogre (a Brute definition) spawns with the plain BruteAIProfile, where it had no profile
        /// before. It closes on the fighter and attacks in melee, and it tries no maneuver: the profile-less ogre opened with
        /// a trip on a standing foe (AI-035), and BerserkAIProfile (Q4's alternative) trips too.
        /// </summary>
        private static ScenarioDef BruteProfileMelee()
        {
            return Rules("rules/brute-profile-melee-ai", "A Brute creature gets the plain Brute profile and fights in melee without maneuvers (AI-004; Q4)")
                .Covers("AI-004", "Q4", "batch 13 step 8")
                .MaxRounds(2)
                .Npc("ogre", "ogre", 10, 10, Control.Ai)
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 13, 10, Control.Idle)
                .Tweak("ogre", Sturdy)
                .Tweak("foe", Sturdy)
                .Initiative("ogre", "foe")
                .Expect("The ogre has the Brute profile", ProfileIs("ogre", "BruteAIProfile"))
                .Expect("By round 2 it attacks the fighter", AttacksBy("ogre", "foe", 2))
                .Expect("It tries no combat maneuver", NoManeuver("ogre"))
                .Build();
        }

        /// <summary>
        /// AI-004: the couatl is a Caster definition, but this build gives it no spells (its sorcerer casting and spell-likes
        /// wait for batch 16), and SpellcasterAIProfile without spells would hold at its 6-square casting distance and
        /// never fight (AI-038). It gets BruteAIProfile and closes in to attack the fighter.
        /// </summary>
        private static ScenarioDef CasterArchetypeWithoutSpellsFights()
        {
            return Rules("rules/caster-archetype-without-spells-fights-ai", "A Caster creature without spells gets the Brute profile and fights (AI-004; AI-038)")
                .Covers("AI-004", "AI-038", "Q4", "batch 13 step 8")
                .MaxRounds(2)
                .Npc("couatl", "couatl", 10, 10, Control.Ai)
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 15, 10, Control.Idle)
                .Tweak("couatl", Sturdy)
                .Tweak("foe", Sturdy)
                .Initiative("couatl", "foe")
                .Expect("The couatl has the Brute profile", ProfileIs("couatl", "BruteAIProfile"))
                .Expect("By round 2 it attacks the fighter", AttacksBy("couatl", "foe", 2))
                .Build();
        }

        /// <summary>
        /// AI-003: the skeleton archer's behaviour is Ranged ("prefer ranged attacks"), which was never routed, so it closed
        /// to melee. It now starts with its shortbow in hand (the spawn's wield test is the routing test) and shoots the
        /// fighter 35 ft. away in round 1, with its UndeadMindless profile and, in the twin without a profile, through the
        /// profile-less switch.
        /// </summary>
        private static ScenarioDef RangedBehaviourShoots(bool withProfile)
        {
            string id = withProfile ? "rules/ranged-behaviour-shoots-ai" : "rules/ranged-behaviour-shoots-no-profile-ai";
            ScenarioBuilder b = Rules(id, "A creature with the Ranged behaviour shoots" + (withProfile ? "" : " without a profile") + " (AI-003; PHB p.113)")
                .Covers("AI-003", "PHB p.113", "batch 13 step 8")
                .MaxRounds(1)
                .Npc("archer", "skeleton_archer", 10, 10, Control.Ai);
            if (!withProfile)
                b = b.Profile("archer", () => null);
            return b
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 17, 10, Control.Idle)
                .Tweak("archer", Sturdy)
                .Tweak("foe", Sturdy)
                .Initiative("archer", "foe")
                .Expect("The archer holds its shortbow", v =>
                {
                    TraceEvent a = v.Of("actor").FirstOrDefault(e => e.Str("key") == "archer");
                    if (a == null)
                        return ExpectResult.Inconclusive("no actor event");
                    string w = a.Str("weapon") ?? "";
                    return w.IndexOf("bow", StringComparison.OrdinalIgnoreCase) >= 0
                        ? ExpectResult.Pass("weapon " + w, a.Seq)
                        : ExpectResult.Fail("weapon " + w, a.Seq);
                })
                .Expect("Round 1: it shoots the fighter", v =>
                {
                    TraceEvent shot = v.Attacks("archer", "foe", aoo: false, round: 1).FirstOrDefault();
                    if (shot == null)
                        return ExpectResult.Fail("no round-1 attack");
                    return shot.Bool("ranged") ? ExpectResult.Pass("a ranged attack", shot.Seq) : ExpectResult.Fail("a melee attack", shot.Seq);
                })
                .Expect("It shoots from where it stands: no move toward the fighter before the round-1 shot (the ranged routine)", v =>
                {
                    TraceEvent shot = v.Attacks("archer", "foe", aoo: false, round: 1).FirstOrDefault();
                    if (shot == null)
                        return ExpectResult.Inconclusive("no round-1 attack");
                    TraceEvent move = v.Moves("archer", 1).FirstOrDefault(e => e.Seq < shot.Seq);
                    return move == null ? ExpectResult.Pass("no move before the shot", shot.Seq) : ExpectResult.Fail("moved before the shot", move.Seq, shot.Seq);
                })
                .Build();
        }

        /// <summary>The first round-1 action of <paramref name="by"/> is a cast, made before any attack of its own.</summary>
        private static Func<TraceView, ExpectResult> CastsBeforeAttacking(string by)
            => v =>
            {
                TraceEvent cast = v.Of("cast").FirstOrDefault(e => e.Str("by") == by && e.Round == 1);
                if (cast == null)
                    return ExpectResult.Fail("no round-1 cast");
                if (!IsCombatSpell(cast.Str("spell")))
                    return ExpectResult.Fail("round 1: casts " + cast.Str("spell") + ", a spell with no combat effect (AI-069)", cast.Seq);
                TraceEvent attack = v.Attacks(by, null, aoo: false, round: 1).FirstOrDefault();
                return attack == null || attack.Seq > cast.Seq
                    ? ExpectResult.Pass("round 1: casts " + cast.Str("spell"), cast.Seq)
                    : ExpectResult.Fail("attacked before casting", attack.Seq, cast.Seq);
            };

        /// <summary>A spell id the melee routines' cast-first step may cast (AIProfile.IsCastFirstCandidate, AI-069).</summary>
        private static bool IsCombatSpell(string spellId)
        {
            SpellData spell = string.IsNullOrEmpty(spellId) ? null : SpellDatabase.GetSpell(spellId);
            return spell != null && DND35.AI.AIProfile.IsCastFirstCandidate(spell);
        }

        /// <summary>Every cast of <paramref name="by"/> is a spell with a combat effect (no Light, Detect Magic or the like).</summary>
        private static Func<TraceView, ExpectResult> CastsOnlyCombatSpells(string by)
            => v =>
            {
                List<TraceEvent> bad = v.Of("cast").Where(e => e.Str("by") == by && !IsCombatSpell(e.Str("spell"))).ToList();
                int all = v.Of("cast").Count(e => e.Str("by") == by);
                return bad.Count == 0
                    ? ExpectResult.Pass(all + " cast(s), all combat spells")
                    : ExpectResult.Fail(bad.Count + " cast(s) without a combat effect, first " + bad[0].Str("spell"), bad.Select(e => e.Seq).ToArray());
            };

        /// <summary>
        /// AI-002 (the defensive melee routine): the human cleric (DMG p.114's sample cleric: the Humanoid profile, the
        /// DefensiveMelee behaviour) has its prepared spells. Before the change the melee routines never cast, so it walked
        /// up and swung its mace; now it tries the turn's spell first and casts in round 1 before any attack. Over three
        /// rounds, once its buffs are up, it casts only spells with a combat effect: never Light or Detect Magic, which it
        /// has prepared (AI-069; the cast-first step and its gate share AIProfile.IsCastFirstCandidate).
        /// </summary>
        private static ScenarioDef MeleeCasterCastsFirst()
        {
            return Rules("rules/melee-caster-casts-first-ai", "An AI cleric on the defensive melee routine casts before it melees (AI-002)")
                .Covers("AI-002", "AI-069", "batch 13 step 8")
                .MaxRounds(3)
                .Npc("cleric", "human_cleric", 10, 10, Control.Ai)
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 13, 10, Control.Idle)
                .Tweak("cleric", Sturdy)
                .Tweak("foe", Sturdy)
                .Initiative("cleric", "foe")
                .Expect("The cleric has the Humanoid profile", ProfileIs("cleric", "HumanoidAIProfile"))
                .Expect("Round 1: it casts before it attacks", CastsBeforeAttacking("cleric"))
                .Expect("Rounds 1-3: it casts no spell without a combat effect (AI-069)", CastsOnlyCombatSpells("cleric"))
                .Build();
        }

        /// <summary>
        /// AI-002 (the aggressive melee routine) for an AI-run party member, the same rule as for an NPC: a 5th-level party
        /// wizard with its spells and a melee profile (Humanoid: the aggressive melee routine) casts in round 1 instead of
        /// walking up to the foe with its staff.
        /// </summary>
        private static ScenarioDef MeleeCasterCastsFirstParty()
        {
            return Rules("rules/melee-caster-casts-first-party-ai", "An AI-run party wizard on the aggressive melee routine casts before it melees (AI-002)")
                .Covers("AI-002", "PC_NPC_PARITY", "batch 13 step 8")
                .MaxRounds(1)
                .Pc("wiz", ActorSource.Stats(() => StaffWizard("Melee Wizard", 5)), 10, 10, Control.Ai)
                .Profile("wiz", () => DND35.AI.AIProfile.CreateRuntime<DND35.AI.Profiles.HumanoidAIProfile>())
                .Npc("foe", "orc_warrior", 13, 10, Control.Idle)
#pragma warning disable CS0618 // the quarterstaff's string id
                .Tweak("wiz", c => { Wield(c, ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF)); Sturdy(c); })
#pragma warning restore CS0618
                .Tweak("foe", Sturdy)
                .Initiative("wiz", "foe")
                .Expect("Round 1: it casts before it attacks", CastsBeforeAttacking("wiz"))
                .Build();
        }

        /// <summary>
        /// AI-002 with AI-069: an AI-run party sorcerer 1 on the aggressive melee routine (Humanoid profile) who knows only
        /// Light, Detect Magic, Prestidigitation and Read Magic, a foe next to it. None of these has a combat effect, so the
        /// cast-first gate (AIProfile.IsCastFirstCandidate) stays shut: it casts nothing and fights in melee in round 1 (its staff or its profile's trip).
        /// (With the first gate, Light, which the classifier files as Buff, opened it and the sorcerer cast it.)
        /// </summary>
        private static ScenarioDef MeleeCasterSkipsOrisons()
        {
            return Rules("rules/melee-caster-skips-orisons-ai", "A melee-routine caster with only out-of-combat cantrips fights instead of casting (AI-002, AI-069)")
                .Covers("AI-002", "AI-069", "batch 13 step 8")
                .MaxRounds(2)
                .Pc("sorc", ActorSource.Stats(() => StaffSorcerer("Cantrip Sorcerer", 1)), 10, 10, Control.Ai)
                .Profile("sorc", () => DND35.AI.AIProfile.CreateRuntime<DND35.AI.Profiles.HumanoidAIProfile>())
                .Npc("foe", "orc_warrior", 11, 10, Control.Idle)
#pragma warning disable CS0618 // the quarterstaff's string id
                .Tweak("sorc", c =>
                {
                    KnowsOnlyCantrips(c, SpellNames.LIGHT, SpellNames.DETECT_MAGIC, SpellNames.PRESTIDIGITATION, SpellNames.READ_MAGIC);
                    Wield(c, ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF));
                    Sturdy(c);
                })
#pragma warning restore CS0618
                .Tweak("foe", Sturdy)
                .Initiative("sorc", "foe")
                .Expect("It casts nothing (AI-069)", v =>
                {
                    List<TraceEvent> casts = v.Of("cast").Where(e => e.Str("by") == "sorc").ToList();
                    return casts.Count == 0 ? ExpectResult.Pass("no cast") : ExpectResult.Fail("cast " + casts[0].Str("spell"), casts.Select(e => e.Seq).ToArray());
                })
                .Expect("Round 1: it fights the orc in melee (an attack or its profile's trip)", FightsInMeleeBy("sorc", "foe", 1))
                .Build();
        }

        private static CharacterStats StaffSorcerer(string name, int level)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Sorcerer",
                str: 10, dex: 12, con: 12, wis: 10, intelligence: 10, cha: 16,
                bab: level / 2, armorBonus: 0, shieldBonus: 0,
                damageDice: 6, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 4 * level, raceName: "Human");
            s.CharacterAlignment = Alignment.TrueNeutral;
            s.EnsureMulticlassDataInitialized();
            return s;
        }

        /// <summary>Clears every spell the sorcerer knows and makes <paramref name="ids"/> its known 0-level spells.</summary>
        private static void KnowsOnlyCantrips(CharacterController c, params string[] ids)
        {
            SpontaneousCastingData known = c.Spellcasting != null ? c.Spellcasting.SpontaneousData : null;
            if (known == null || !c.Spellcasting.IsSpontaneousCaster)
                throw new InvalidOperationException(c.Stats.CharacterName + " is not a spontaneous caster");
            for (int l = 0; l < known.SpellsKnownByLevel.Length; l++)
                known.SpellsKnownByLevel[l]?.Clear();
            foreach (string id in ids)
            {
                if (!known.LearnSpell(id, 0))
                    throw new InvalidOperationException(c.Stats.CharacterName + " could not learn " + id);
            }
        }

        /// <summary>
        /// AI-002 must not displace the defensive melee routine's withdraw (below 30% HP, PHB p.143, AI.md 5.4): the human
        /// cleric, at a fifth of its hit points next to a fighter, withdraws in round 1 instead of casting, although it has
        /// combat spells prepared.
        /// </summary>
        private static ScenarioDef DefensiveCasterWithdrawsWhenHurt()
        {
            return Rules("rules/defensive-caster-withdraws-when-hurt-ai", "A badly hurt defensive melee caster withdraws before it casts (AI-002; PHB p.143)")
                .Covers("AI-002", "PHB p.143", "batch 13 step 8")
                .MaxRounds(1)
                .Npc("cleric", "human_cleric", 10, 10, Control.Ai)
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 11, 10, Control.Idle)
                .Tweak("cleric", c => c.Stats.CurrentHP = Math.Max(1, c.Stats.TotalMaxHP / 5))
                .Tweak("foe", Sturdy)
                .Initiative("cleric", "foe")
                .Expect("Round 1: it withdraws from the fighter", v =>
                    v.Log("withdraws from").Any(e => e.Round == 1) ? ExpectResult.Pass("withdraws") : ExpectResult.Fail("no withdraw line"))
                .Expect("Round 1: it casts nothing", v =>
                {
                    TraceEvent cast = v.Of("cast").FirstOrDefault(e => e.Str("by") == "cleric" && e.Round == 1);
                    return cast == null ? ExpectResult.Pass("no cast") : ExpectResult.Fail("cast " + cast.Str("spell"), cast.Seq);
                })
                .Build();
        }

        /// <summary>
        /// AI-003 for a creature with no ranged means: the gauth's behaviour is Ranged, but its eye rays are text only
        /// (CRE-017) and it has only its bite, so "prefers ranged attacks" gives it nothing to shoot; it keeps the aggressive
        /// melee routine (AIService.RunsRangedRoutine), closes on the fighter two squares away and fights it in melee (its bite,
        /// or the profile-less trip, AI-035). It takes no total defense.
        /// </summary>
        private static ScenarioDef RangedBehaviourMeleeOnlyApproaches()
        {
            return Rules("rules/ranged-behaviour-melee-only-approaches-ai", "A Ranged-behaviour creature with no ranged means closes and bites (AI-003)")
                .Covers("AI-003", "batch 13 step 8")
                .MaxRounds(3)
                .Npc("gauth", "gauth", 10, 10, Control.Ai)
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 12, 10, Control.Idle)
                .Tweak("gauth", Sturdy)
                .Tweak("foe", Sturdy)
                .Initiative("gauth", "foe")
                .Expect("By round 2 it closes and fights the fighter in melee (an attack or the profile-less trip, AI-035)", FightsInMeleeBy("gauth", "foe", 2))
                .Expect("It takes no total defense", NoLog("Gauth takes total defense", "total defense"))
                .Build();
        }

        // ── Step 12: posture and reload (D4, D5; AI-057, CMB-074, CMB-032, AI-054) ──────────

        /// <summary>
        /// Puts <paramref name="crossbow"/> in <paramref name="c"/>'s hands (both: the off-hand kit is stripped), loaded or
        /// not, with <paramref name="bolts"/> bolts in the pack and, when named, the Rapid Reload feat for it (PHB p.99).
        /// </summary>
        private static void ArmCrossbow(CharacterController c, ItemID crossbow, bool loaded, int bolts, string rapidReload = null)
        {
            RulesScenarios.StripOffHand(c);
            ItemData bow = ItemDatabase.CloneItem(crossbow);
            bow.IsLoaded = loaded;
            Wield(c, bow);
            global::Inventory inv = Inv(c);
            if (bolts > 0)
            {
                ItemData pieces = ItemDatabase.CloneItem(ItemID.AmmoBolt);
                pieces.Quantity = bolts;
                inv.AddItem(pieces);
            }
            inv.RecalculateStats();
            if (rapidReload != null && !c.Stats.HasFeat(rapidReload))
                c.Stats.Feats.Add(rapidReload);
        }

        private static void Note12(ScenarioContext ctx, string key, object value) => ctx.Note(NotePrefix + key + "=" + value);

        /// <summary>The last note of <paramref name="key"/> is <paramref name="want"/>.</summary>
        private static Func<TraceView, ExpectResult> Fact12(string key, string want)
            => v =>
            {
                string got = NoteValue(v, key, out int seq);
                if (got == null)
                    return ExpectResult.Inconclusive("no note " + key);
                return got == want ? ExpectResult.Pass(key + "=" + got, seq) : ExpectResult.Fail(key + "=" + got + ", expected " + want, seq);
            };

        /// <summary>Notes <paramref name="key"/>'s main weapon load and the actions it has left, under "<paramref name="tag"/>.*".</summary>
        private static bool NoteLoadAndActions(ScenarioContext ctx, string key, string tag)
        {
            CharacterController c = ctx.Get(key);
            ItemData w = c.GetEquippedMainWeapon();
            Note12(ctx, tag + ".loaded", w != null && w.IsLoaded);
            Note12(ctx, tag + ".move", c.Actions.HasMoveAction);
            Note12(ctx, tag + ".standard", c.Actions.HasStandardAction);
            return true;
        }

        private static bool HasProne(JsonObj snap) => snap != null && snap.Get("conds") is List<string> l && l.Contains("Prone");

        /// <summary>
        /// <paramref name="by"/>'s attack of opportunity on <paramref name="target"/> for the reload (trigger "reload",
        /// GameManager.ResolveActionProvocation) comes before the reload's log line (D4: the load provokes, PHB p.115, p.99).
        /// </summary>
        private static Func<TraceView, ExpectResult> ReloadAoOBeforeLoad(string by, string target)
            => v =>
            {
                TraceEvent aoo = v.AoOs(by, target, "reload").FirstOrDefault();
                TraceEvent load = v.Log(" reloads .*Crossbow").FirstOrDefault();
                if (aoo == null)
                    return ExpectResult.Fail("no reload AoO by " + by + " on " + target);
                if (load == null)
                    return ExpectResult.Fail("no reload line", aoo.Seq);
                return aoo.Seq < load.Seq ? ExpectResult.Pass("AoO #" + aoo.Seq + " before the load #" + load.Seq, aoo.Seq, load.Seq)
                    : ExpectResult.Fail("AoO after the load", aoo.Seq, load.Seq);
            };

        /// <summary>
        /// D4 (PHB p.115, Table 8-2 p.141, CMB-032): a fighter with an unloaded light crossbow and bolts, next to an orc,
        /// reloads: a move action that provokes, so the orc's attack of opportunity comes first, then the crossbow is loaded
        /// and the standard action is left. The Ui twin presses the Reload button (its attack-of-opportunity prompt answered
        /// Proceed), the AI twin runs the same executor through RunAIAction (Step.Reload, Scripted).
        /// </summary>
        private static ScenarioDef ReloadProvokes(bool ui)
        {
            ScenarioBuilder b = Rules(ui ? "rules/reload-provokes-ui" : "rules/reload-provokes-ai",
                    "Loading a light crossbow is a move action that provokes (PHB p.115, p.141; D4, CMB-032)")
                .Covers("CMB-032", "D4", "PHB p.115", "PHB p.141", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, ui ? Control.Ui : Control.Scripted)
                .Tweak("hero", c => { Sturdy(c); ArmCrossbow(c, ItemID.WeaponCrossbowLight, false, 10); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero") // the orc acts first, so it is not flat-footed (PHB p.137)
                .Turn("orc", 1, Step.Pass());
            Step after = Step.Assert("after the reload", ctx => NoteLoadAndActions(ctx, "hero", "hero"));
            b = ui
                ? b.Turn("hero", 1, Step.Reload(), Step.AnswerAoO(AoOAnswer.Proceed), after)
                : b.Turn("hero", 1, Step.Reload(), after);
            if (ui)
                b = b.Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"));
            return b
                .Expect("The reload step is done", Expect.StepStatus("hero", 1, "Reload", 0, "done"))
                .Expect("The orc's attack of opportunity for the reload comes before the load (PHB p.115, p.141)", ReloadAoOBeforeLoad("orc", "hero"))
                .Expect("The crossbow is loaded, the move action spent and the standard action left (PHB p.115)", Expect.All(
                    Fact12("hero.loaded", "True"), Fact12("hero.move", "False"), Fact12("hero.standard", "True")))
                .Build();
        }

        /// <summary>
        /// AI-057 (PHB p.115, D4): an orc with an unloaded light crossbow and bolts attacks the fighter next to it. Its attack
        /// prelude reloads through the catalog's reload entry (a move action; the fighter's attack of opportunity comes
        /// first), then the standard action left makes the shot. Before the fix the NPC reloaded and ended its turn.
        /// </summary>
        private static ScenarioDef NpcReloadThenFire()
        {
            return Rules("rules/npc-reload-then-fire-ai", "An NPC reloads a light crossbow with its move action and fires with its standard action (PHB p.115; AI-057, D4)")
                .Covers("AI-057", "CMB-032", "D4", "PHB p.115", "PHB p.141", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Tweak("hero", Sturdy)
                .Tweak("orc", c => { Sturdy(c); ArmCrossbow(c, ItemID.WeaponCrossbowLight, false, 10); })
                .Initiative("hero", "orc") // the hero acts first, so it is not flat-footed when the orc reloads (PHB p.137)
                .Turn("hero", 1, Step.Pass())
                .Turn("orc", 1, Step.Attack("hero"), Step.Assert("after the attack", ctx => NoteLoadAndActions(ctx, "orc", "orc")))
                .Expect("The hero's attack of opportunity for the reload comes before the load (PHB p.115, p.141)", ReloadAoOBeforeLoad("hero", "orc"))
                .Expect("Then one shot at the hero, after the load (AI-057)", v =>
                {
                    List<TraceEvent> shots = v.Attacks("orc", "hero", false, 1);
                    TraceEvent load = v.Log(" reloads .*Crossbow").FirstOrDefault();
                    if (shots.Count != 1)
                        return ExpectResult.Fail(shots.Count + " shots, expected 1", shots.Select(e => e.Seq).ToArray());
                    if (load == null)
                        return ExpectResult.Fail("no reload line", shots[0].Seq);
                    return load.Seq < shots[0].Seq ? ExpectResult.Pass("load #" + load.Seq + ", then the shot #" + shots[0].Seq, load.Seq, shots[0].Seq)
                        : ExpectResult.Fail("the shot came before the load", shots[0].Seq, load.Seq);
                })
                .Expect("The move action reloaded and the standard action shot: neither is left, the crossbow is empty again", Expect.All(
                    Fact12("orc.move", "False"), Fact12("orc.standard", "False"), Fact12("orc.loaded", "False")))
                .Build();
        }

        /// <summary>
        /// D4 (PHB p.99, p.115): an orc with Rapid Reload (Light Crossbow), BAB +6 and an unloaded light crossbow full
        /// attacks the fighter next to it. Its load is a free action that still provokes (the fighter's attack of
        /// opportunity comes first), so the full-round action is whole: two shots (+6/+1), Rapid Reload reloading between
        /// them.
        /// </summary>
        private static ScenarioDef RapidReloadFree()
        {
            return Rules("rules/rapid-reload-free-ai", "Rapid Reload makes a light crossbow's load free, and it still provokes (PHB p.99, p.115; D4)")
                .Covers("CMB-032", "D4", "PHB p.99", "PHB p.115", "PHB p.141")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Tweak("hero", Sturdy)
                .Tweak("orc", c =>
                {
                    Sturdy(c);
                    c.Stats.BaseAttackBonusOverride = 6;
                    ArmCrossbow(c, ItemID.WeaponCrossbowLight, false, 20, "Rapid Reload (Light Crossbow)");
                })
                .Initiative("hero", "orc")
                .Turn("hero", 1, Step.Pass())
                .Turn("orc", 1, Step.FullAttack("hero"))
                .Expect("The hero's attack of opportunity for the free load comes before it (PHB p.99: it still provokes)", ReloadAoOBeforeLoad("hero", "orc"))
                .Expect("The load was a free action: the line says so", v =>
                    v.Log(" reloads Crossbow, Light \\(Free action\\)").Any() ? ExpectResult.Pass("free load") : ExpectResult.Fail("no free load line"))
                .Expect("Both iterative shots follow (+6/+1)", v =>
                {
                    List<TraceEvent> shots = v.Attacks("orc", "hero", false, 1);
                    int[] mods = shots.Select(e => e.Int("mod")).ToArray();
                    if (shots.Count != 2)
                        return ExpectResult.Fail(shots.Count + " shots, mods [" + string.Join(",", mods) + "]", shots.Select(e => e.Seq).ToArray());
                    return mods[0] - mods[1] == 5 ? ExpectResult.Pass("mods [" + string.Join(",", mods) + "]", shots[0].Seq, shots[1].Seq)
                        : ExpectResult.Fail("mods [" + string.Join(",", mods) + "]", shots[0].Seq, shots[1].Seq);
                })
                .Build();
        }

        /// <summary>The first removal of <paramref name="key"/>'s Prone comes before its first move in round 1.</summary>
        private static Func<TraceView, ExpectResult> StandsThenMoves(string key)
            => v =>
            {
                TraceEvent stand = v.Conds(key, CombatConditionType.Prone).FirstOrDefault(e => !e.Bool("added"));
                TraceEvent move = v.Moves(key, 1).FirstOrDefault();
                if (stand == null)
                    return ExpectResult.Fail(key + " never stood up");
                if (move == null)
                    return ExpectResult.Fail(key + " did not close in", stand.Seq);
                return stand.Seq < move.Seq ? ExpectResult.Pass("stand #" + stand.Seq + ", move #" + move.Seq, stand.Seq, move.Seq)
                    : ExpectResult.Fail("moved before standing", move.Seq, stand.Seq);
            };

        /// <summary>
        /// CMB-074, D5 (delegated RAW reading of PHB p.212, silent on prone): two prone, confused orcs both roll "attack the
        /// nearest creature" (d% 90, forced). The far one does not threaten the fighter, so it stands up first (its move
        /// action) and closes in with its standard action, which leaves it no attack; the near one already threatens the
        /// fighter, so it stays prone and attacks it at once (-4 prone, PHB p.311). Before the fix neither ever stood up.
        /// </summary>
        private static ScenarioDef ConfusedProneStandsAi()
        {
            return Rules("rules/confused-prone-stands-ai", "A prone confused creature told to attack the nearest creature stands first unless it threatens it (CMB-074, D5)")
                .Covers("CMB-074", "D5", "PHB p.212", "PHB p.143", "PHB p.311")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Idle)
                .Npc("near", "orc_warrior", 11, 10, Control.Ai)
                .Npc("far", "orc_warrior", 8, 10, Control.Ai)
                .StartCondition("near", CombatConditionType.Prone, -1)
                .StartCondition("far", CombatConditionType.Prone, -1)
                .StartCondition("near", CombatConditionType.Confused, 3)
                .StartCondition("far", CombatConditionType.Confused, 3)
                .Tweak("hero", Sturdy)
                .Tweak("near", Sturdy)
                .Tweak("far", Sturdy)
                .Force(100, 90, null, 2) // both confusion rolls: attack the nearest creature (PHB p.212)
                .Initiative("near", "far", "hero")
                .Expect("Both orcs' turns are AI turns", Expect.All(Expect.Controller("near", "ai"), Expect.Controller("far", "ai")))
                .Expect("Both rolled 'attack the nearest creature' (d% 90)", v =>
                {
                    int rolls = v.Log("is confused \\(d% 90\\)").Count;
                    return rolls >= 2 ? ExpectResult.Pass(rolls + " rolls of 90") : ExpectResult.Fail(rolls + " rolls of 90");
                })
                .Expect("The far orc stands up, then closes in (PHB p.143)", StandsThenMoves("far"))
                .Expect("The near orc, which threatens the fighter, stays prone and attacks it", Expect.All(
                    Expect.None("cond", e => e.Str("actor") == "near" && e.Get("type") is CombatConditionType t && t == CombatConditionType.Prone && !e.Bool("added")),
                    v => v.Attacks("near", "hero", false, 1).Count >= 1 ? ExpectResult.Pass("attacked") : ExpectResult.Fail("no attack by the near orc")))
                .Expect("The far orc's standard action went to the approach: no attack", Expect.None("attack", e => e.Str("attacker") == "far" && !e.Bool("aoo")))
                .Expect("Only the near orc declares an attack; the far one stumbles toward the fighter (no attack action left)", v =>
                {
                    int declared = v.Log("attacks the nearest creature").Count;
                    int stumbles = v.Log("stumbles toward .* but cannot attack this turn").Count;
                    return declared == 1 && stumbles == 1 ? ExpectResult.Pass("1 declaration, 1 stumble")
                        : ExpectResult.Fail(declared + " declarations, " + stumbles + " stumbles");
                })
                .Build();
        }

        /// <summary>
        /// The PC twin of rules/confused-prone-stands-ai (CMB-074, D5): a prone, confused Ui fighter rolls "attack the
        /// nearest creature" (d% 90, forced). Its forced turn runs the same controller: it does not threaten the orc two
        /// squares away, so it stands up first, then closes in.
        /// </summary>
        private static ScenarioDef ConfusedProneStandsUi()
        {
            return Rules("rules/confused-prone-stands-ui", "A prone confused PC told to attack the nearest creature stands first (CMB-074, D5)")
                .Covers("CMB-074", "D5", "PHB p.212", "PHB p.143", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 8, 10, Control.Ui)
                .Npc("orc", "orc_warrior", 10, 10, Control.Idle)
                .StartCondition("hero", CombatConditionType.Prone, -1)
                .StartCondition("hero", CombatConditionType.Confused, 3)
                .Tweak("hero", c => { Sturdy(c); RulesScenarios.StripOffHand(c); })
                .Tweak("orc", Sturdy)
                .Force(100, 90, null, 1)
                .Initiative("hero", "orc")
                .Expect("The hero rolled 'attack the nearest creature' (d% 90)", v =>
                    v.Log("Hero is confused \\(d% 90\\)").Count > 0 ? ExpectResult.Pass("rolled 90") : ExpectResult.Fail("no roll of 90"))
                .Expect("The hero stands up, then closes in (PHB p.143)", StandsThenMoves("hero"))
                .Expect("With no attack action left it declares no attack (PHB p.139)", v =>
                    !v.Log("attacks the nearest creature").Any() && v.Log("stumbles toward .* but cannot attack this turn").Any()
                        ? ExpectResult.Pass("stumbles, no declaration") : ExpectResult.Fail("an attack declared, or no stumble line"))
                .Build();
        }

        /// <summary>
        /// D5 (PHB p.142-143, p.137): a prone fighter next to an orc crawls 5 ft away: a move action that provokes as
        /// movement, so the orc's movement attack of opportunity comes before the crawl, the fighter ends 1 square away,
        /// still prone, its move action spent. The Ui twin presses Crawl and clicks the square; the AI twin runs the same
        /// executor through RunAIAction (Step.Crawl, Scripted).
        /// </summary>
        private static ScenarioDef Crawl(bool ui)
        {
            ScenarioBuilder b = Rules(ui ? "rules/crawl-ui" : "rules/crawl-ai", "Crawling 5 ft is a move action that provokes from a foe threatening the square left (PHB p.142-143; D5, AI-054)")
                .Covers("AI-054", "D5", "PHB p.142", "PHB p.143", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, ui ? Control.Ui : Control.Scripted)
                .StartCondition("hero", CombatConditionType.Prone, -1)
                .Tweak("hero", c => { Sturdy(c); RulesScenarios.StripOffHand(c); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1, Step.Crawl(9, 10), Step.Assert("after the crawl", ctx => NoteLoadAndActions(ctx, "hero", "hero")));
            if (ui)
                b = b.Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"));
            return b
                .Expect("The crawl step is done", Expect.StepStatus("hero", 1, "Crawl", 0, "done"))
                .Expect("Exactly one move of 1 square by the hero", Expect.Count("move", e => e.Str("actor") == "hero" && e.Int("sq") == 1, 1, 1))
                .Expect("The orc's movement attack of opportunity comes before the crawl (PHB p.137, p.143)", v =>
                {
                    TraceEvent aoo = v.AoOs("orc", "hero", "movement").FirstOrDefault();
                    TraceEvent mv = v.Moves("hero").FirstOrDefault();
                    if (aoo == null) return ExpectResult.Fail("no movement AoO by the orc");
                    if (mv == null) return ExpectResult.Inconclusive("no crawl move");
                    return aoo.Seq < mv.Seq ? ExpectResult.Pass("AoO before the crawl", aoo.Seq, mv.Seq) : ExpectResult.Fail("AoO after the crawl", aoo.Seq, mv.Seq);
                })
                .Expect("The hero is still prone, its move action spent and its standard action left", Expect.All(
                    v => HasProne(v.Final("hero")) ? ExpectResult.Pass("prone") : ExpectResult.Fail("not prone"),
                    Fact12("hero.move", "False"), Fact12("hero.standard", "True")))
                .Build();
        }

        /// <summary>
        /// D5 (PHB p.141 Table 8-2, p.311): a Ui fighter next to an orc presses Drop Prone: a free action that provokes
        /// nothing, so it is prone with its move and standard actions left, and the orc took no attack of opportunity.
        /// </summary>
        private static ScenarioDef DropProneUi()
        {
            return Rules("rules/drop-prone-ui", "Dropping prone is a free action that provokes nothing (PHB p.141; D5, AI-054)")
                .Covers("AI-054", "D5", "PHB p.141", "PHB p.311", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Ui)
                .Tweak("hero", c => { Sturdy(c); RulesScenarios.StripOffHand(c); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1, Step.DropProne(), Step.Assert("after dropping prone", ctx => NoteLoadAndActions(ctx, "hero", "hero")))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The drop step is done", Expect.StepStatus("hero", 1, "DropProne", 0, "done"))
                .Expect("The hero becomes prone in round 1", v =>
                    v.Conds("hero", CombatConditionType.Prone).Any(e => e.Bool("added") && e.Round == 1) ? ExpectResult.Pass("prone") : ExpectResult.Fail("no Prone added"))
                .Expect("No attack of opportunity on the hero (Table 8-2: a free action)", Expect.None("aoo", e => e.Str("target") == "hero"))
                .Expect("Its move and standard actions are left", Expect.All(Fact12("hero.move", "True"), Fact12("hero.standard", "True")))
                .Build();
        }

        /// <summary>
        /// D4 with the payOnProceed hook (GameManager.ResolveActionProvocation): a Ui fighter with an unloaded light crossbow
        /// next to an orc presses Reload and cancels at the attack-of-opportunity prompt. Nothing is spent and nothing
        /// provoked: the crossbow stays empty, the move and standard actions are left, and the orc makes no attack.
        /// </summary>
        private static ScenarioDef ReloadCancelUi()
        {
            return Rules("rules/reload-cancel-ui", "A reload cancelled at its attack-of-opportunity prompt spends nothing (D4, D3)")
                .Covers("CMB-032", "D4", "D3", "PHB p.115")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Ui)
                .Tweak("hero", c => { Sturdy(c); ArmCrossbow(c, ItemID.WeaponCrossbowLight, false, 10); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1, Step.Reload(), Step.AnswerAoO(AoOAnswer.Cancel),
                    Step.Assert("after the cancel", ctx => NoteLoadAndActions(ctx, "hero", "hero")))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("No attack of opportunity on the hero", Expect.None("aoo", e => e.Str("target") == "hero"))
                .Expect("No reload line", v => v.Log(" reloads .*Crossbow").Any() ? ExpectResult.Fail("reloaded") : ExpectResult.Pass("no reload"))
                .Expect("The crossbow is still empty and both actions are left", Expect.All(
                    Fact12("hero.loaded", "False"), Fact12("hero.move", "True"), Fact12("hero.standard", "True")))
                .Build();
        }

        /// <summary>
        /// D4 (PHB p.115): an orc with an unloaded heavy crossbow attacks the fighter next to it. Its attack prelude loads
        /// with the full-round action (the fighter's attack of opportunity first), which leaves no attack this turn: no shot,
        /// and no "cannot attack" or "makes no attack" line.
        /// </summary>
        private static ScenarioDef NpcHeavyReloadNoShot()
        {
            return Rules("rules/npc-heavy-reload-no-shot-ai", "An NPC loading a heavy crossbow spends its full round and does not shoot (PHB p.115; D4)")
                .Covers("AI-057", "CMB-032", "D4", "PHB p.115")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Tweak("hero", Sturdy)
                .Tweak("orc", c => { Sturdy(c); ArmCrossbow(c, ItemID.WeaponCrossbowHeavy, false, 10); })
                .Initiative("hero", "orc")
                .Turn("hero", 1, Step.Pass())
                .Turn("orc", 1, Step.Attack("hero"), Step.Assert("after the attack", ctx => NoteLoadAndActions(ctx, "orc", "orc")))
                .Expect("The hero's attack of opportunity for the load comes before it (PHB p.115, p.141)", ReloadAoOBeforeLoad("hero", "orc"))
                .Expect("The load is a full-round action", v =>
                    v.Log(" reloads Crossbow, Heavy \\(Full-round action\\)").Any() ? ExpectResult.Pass("full-round load") : ExpectResult.Fail("no full-round load line"))
                .Expect("No shot, and no 'cannot attack' or 'makes no attack' line", Expect.All(
                    v => v.Attacks("orc", "hero", false, 1).Count == 0 ? ExpectResult.Pass("no shot") : ExpectResult.Fail("the orc shot"),
                    v => v.Log("cannot attack|makes no attack").Any() ? ExpectResult.Fail("a no-attack line") : ExpectResult.Pass("no such line")))
                .Expect("Loaded, with no action left", Expect.All(Fact12("orc.loaded", "True"), Fact12("orc.move", "False"), Fact12("orc.standard", "False")))
                .Build();
        }

        /// <summary>
        /// D5 (PHB p.143: crawling provokes from any attacker that threatens the creature at any point of the crawl): a prone
        /// fighter crawls from (10,10) to (9,10), into the reach of an orc at (8,10) that does not threaten the square it
        /// left. The orc's attack of opportunity comes on arrival, after the one-square move; the fighter stays prone with its
        /// move action spent. The Ui twin presses Crawl and clicks the square; the AI twin runs the executor (Step.Crawl).
        /// </summary>
        private static ScenarioDef CrawlIntoThreat(bool ui)
        {
            ScenarioBuilder b = Rules(ui ? "rules/crawl-into-threat-ui" : "rules/crawl-into-threat-ai",
                    "A crawl into a foe's reach provokes from that foe on arrival (PHB p.143; D5)")
                .Covers("AI-054", "D5", "PHB p.143", "PHB p.138", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 8, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, ui ? Control.Ui : Control.Scripted)
                .StartCondition("hero", CombatConditionType.Prone, -1)
                .Tweak("hero", c => { Sturdy(c); RulesScenarios.StripOffHand(c); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1, Step.Crawl(9, 10), Step.Assert("after the crawl", ctx => NoteLoadAndActions(ctx, "hero", "hero")));
            if (ui)
                b = b.Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"));
            return b
                .Expect("Exactly one move of 1 square by the hero", Expect.Count("move", e => e.Str("actor") == "hero" && e.Int("sq") == 1, 1, 1))
                .Expect("One attack of opportunity by the orc, after the crawl's move (PHB p.143)", v =>
                {
                    List<TraceEvent> aoos = v.AoOs("orc", "hero");
                    TraceEvent mv = v.Moves("hero").FirstOrDefault();
                    if (aoos.Count != 1) return ExpectResult.Fail(aoos.Count + " AoOs by the orc, expected 1", aoos.Select(e => e.Seq).ToArray());
                    if (mv == null) return ExpectResult.Inconclusive("no crawl move");
                    return aoos[0].Seq > mv.Seq ? ExpectResult.Pass("move #" + mv.Seq + ", then the AoO #" + aoos[0].Seq, mv.Seq, aoos[0].Seq)
                        : ExpectResult.Fail("the AoO came before the move", aoos[0].Seq, mv.Seq);
                })
                .Expect("The hero is still prone, its move action spent and its standard action left", Expect.All(
                    v => HasProne(v.Final("hero")) ? ExpectResult.Pass("prone") : ExpectResult.Fail("not prone"),
                    Fact12("hero.move", "False"), Fact12("hero.standard", "True")))
                .Build();
        }

        /// <summary>
        /// D5 as revised 2026-10-29 (delegated RAW reading of PHB p.212, silent on prone): a prone, confused orc next to the
        /// fighter rolls "flee" (d% 60, forced): it flees at its top speed, so it stands up first (a move action that
        /// provokes, PHB p.143), then flees with its converted standard action. A second prone, confused orc far away rolls
        /// "babble" (d% 30, forced) and stays prone.
        /// </summary>
        private static ScenarioDef ConfusedProneFleeStands()
        {
            return Rules("rules/confused-prone-flee-stands-ai", "A prone confused creature that rolls flee stands up, then flees; one that babbles stays prone (D5)")
                .Covers("CMB-074", "D5", "PHB p.212", "PHB p.143")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Idle)
                .Npc("fleer", "orc_warrior", 11, 10, Control.Ai)
                .Npc("babbler", "orc_warrior", 15, 15, Control.Ai)
                .StartCondition("fleer", CombatConditionType.Prone, -1)
                .StartCondition("babbler", CombatConditionType.Prone, -1)
                .StartCondition("fleer", CombatConditionType.Confused, 3)
                .StartCondition("babbler", CombatConditionType.Confused, 3)
                .Tweak("hero", Sturdy)
                .Tweak("fleer", Sturdy)
                .Tweak("babbler", Sturdy)
                .Force(100, 60, null, 1) // the fleer's confusion roll: flee from the caster (PHB p.212)
                .Force(100, 30, null, 1) // the babbler's: babble
                .Initiative("fleer", "babbler", "hero")
                .Expect("The rolls were 60 and 30", v =>
                    v.Log("is confused \\(d% 60\\)").Count >= 1 && v.Log("is confused \\(d% 30\\)").Count >= 1
                        ? ExpectResult.Pass("60 and 30") : ExpectResult.Fail("rolls not forced"))
                .Expect("The fleer stands up, then flees (PHB p.212, p.143)", StandsThenMoves("fleer"))
                .Expect("No 'cannot move' line for the fleer", v =>
                    v.Log("tries to flee .* but cannot move").Any() ? ExpectResult.Fail("it could not move") : ExpectResult.Pass("it moved"))
                .Expect("The babbler stays prone and does not move", Expect.All(
                    Expect.None("cond", e => e.Str("actor") == "babbler" && e.Get("type") is CombatConditionType t && t == CombatConditionType.Prone && !e.Bool("added")),
                    Expect.None("move", e => e.Str("actor") == "babbler")))
                .Build();
        }

        /// <summary>
        /// D5 (review 2026-10-29): a prone, confused orc with a loaded light crossbow, 3 squares from the fighter, rolls
        /// "attack the nearest creature" (d% 90, forced). It can already shoot as it lies (a prone attacker may use a
        /// crossbow, PHB p.311), so it does not stand up: it stays prone and shoots.
        /// </summary>
        private static ScenarioDef ConfusedProneCrossbow()
        {
            return Rules("rules/confused-prone-crossbow-ai", "A prone confused crossbowman told to attack the nearest creature shoots without standing (D5, PHB p.311)")
                .Covers("CMB-074", "D5", "PHB p.212", "PHB p.311")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Idle)
                .Npc("orc", "orc_warrior", 13, 10, Control.Ai)
                .StartCondition("orc", CombatConditionType.Prone, -1)
                .StartCondition("orc", CombatConditionType.Confused, 3)
                .Tweak("hero", Sturdy)
                .Tweak("orc", c => { Sturdy(c); ArmCrossbow(c, ItemID.WeaponCrossbowLight, true, 10); })
                .Force(100, 90, null, 1)
                .Initiative("orc", "hero")
                .Expect("The orc rolled 'attack the nearest creature' (d% 90)", v =>
                    v.Log("is confused \\(d% 90\\)").Count > 0 ? ExpectResult.Pass("rolled 90") : ExpectResult.Fail("no roll of 90"))
                .Expect("The orc stays prone and does not move", Expect.All(
                    Expect.None("cond", e => e.Str("actor") == "orc" && e.Get("type") is CombatConditionType t && t == CombatConditionType.Prone && !e.Bool("added")),
                    Expect.None("move", e => e.Str("actor") == "orc")))
                .Expect("It shoots the hero", v => v.Attacks("orc", "hero", false, 1).Count >= 1 ? ExpectResult.Pass("shot") : ExpectResult.Fail("no shot"))
                .Build();
        }

        // ── Step 13: aid another, waking an ally, the NPC wall attack (D6, AI-054, CORE-040) ──────────────────────

        /// <summary>Notes the +2 aid bonuses <paramref name="allyKey"/> holds on its attacks against <paramref name="foeKey"/> (GameManager.GetAidAnotherAttackBonuses).</summary>
        private static bool NoteAidBonus(ScenarioContext ctx, string tag, string allyKey, string foeKey)
        {
            int total = 0;
            foreach (GameManager.AidBonus b in ctx.Gm.GetAidAnotherAttackBonuses(ctx.Get(allyKey), ctx.Get(foeKey)))
                total += b.Bonus;
            Note12(ctx, tag, total);
            return true;
        }

        /// <summary>
        /// D6 (PHB p.154, Table 8-2 p.141 note 2): a fighter beside an orc aids its ally's attack on that orc: a standard
        /// action (its move action is left), an attack roll against AC 10 (every d20 forced to 15, so it hits), and the
        /// ally gains +2 on its next attack roll against the orc, which its attack uses up. Nothing provokes. The Ui twin
        /// walks the PC aid menu (the opponent, Aid Offense, the ally); the AI twin runs the same executor through
        /// RunAIAction (Step.ActionAgainst, Scripted).
        /// </summary>
        private static ScenarioDef AidAnother(bool ui)
        {
            ScenarioBuilder b = Rules(ui ? "rules/aid-another-ui" : "rules/aid-another-ai",
                    "Aid another is a standard action, an attack roll against AC 10 giving an ally +2 on its next attack (PHB p.154; D6, AI-054)")
                .Covers("AI-054", "D6", "PHB p.154", "PHB p.141", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Aldric", 4)), 10, 10, ui ? Control.Ui : Control.Scripted)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Brena", 4)), 12, 10, Control.Scripted)
                .Tweak("hero", c => { Sturdy(c); RulesScenarios.StripOffHand(c); })
                .Tweak("ally", c => { Sturdy(c); RulesScenarios.StripOffHand(c); })
                .Tweak("orc", Sturdy)
                .Force(20, 15, null, -1)
                .Initiative("orc", "hero", "ally")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1,
                    ui ? Step.AidAnother("ally", "orc") : Step.ActionAgainst(ActionIds.AidAnother, "ally", "orc", AidAnotherActionExecutor.OffenseVariant),
                    Step.Assert("after the aid", ctx => NoteLoadAndActions(ctx, "hero", "hero") && NoteAidBonus(ctx, "aid.before", "ally", "orc")))
                .Turn("ally", 1, Step.Attack("orc"), Step.Assert("after the ally's attack", ctx => NoteAidBonus(ctx, "aid.after", "ally", "orc")));
            if (ui)
                b = b.Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"));
            return b
                .Expect("The aid step is done", Expect.StepStatus("hero", 1, ui ? "AidAnother" : "Action", 0, "done"))
                .Expect("The attack roll against AC 10 hits (PHB p.154)", v =>
                    v.Log("Aid Another success!").Count == 1 ? ExpectResult.Pass("success") : ExpectResult.Fail(v.Log("Aid Another").Count + " aid lines"))
                .Expect("The ally holds +2 on its next attack against the orc, and its attack uses it up", Expect.All(Fact12("aid.before", "2"), Fact12("aid.after", "0")))
                .Expect("Aid another is a standard action: the move action is left", Expect.All(Fact12("hero.standard", "False"), Fact12("hero.move", "True")))
                .Expect("Nothing provokes (Table 8-2 note 2)", Expect.None("aoo", e => e.Str("target") == "hero"))
                .Expect("The ally attacks the orc in round 1", v => v.Attacks("ally", "orc", false, 1).Count >= 1 ? ExpectResult.Pass("attacked") : ExpectResult.Fail("no attack"))
                .Build();
        }

        /// <summary>
        /// D6 (PHB p.280, p.154): a fighter shakes the sleeping ally beside it awake: a standard action (its move action is
        /// left) that provokes nothing, and the ally is no longer asleep. The Ui twin walks the PC aid menu (the sleeping
        /// ally); the AI twin runs the same executor through RunAIAction (Step.Action, Scripted).
        /// </summary>
        private static ScenarioDef WakeSleepingAlly(bool ui)
        {
            ScenarioBuilder b = Rules(ui ? "rules/wake-sleeping-ally-ui" : "rules/wake-sleeping-ally-ai",
                    "Shaking a sleeping ally awake is a standard action that provokes nothing (PHB p.280; D6, AI-054)")
                .Covers("AI-054", "D6", "PHB p.280", "PHB p.154", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Aldric", 4)), 10, 10, ui ? Control.Ui : Control.Scripted)
                .Pc("sleeper", ActorSource.Stats(() => RulesScenarios.Fighter("Brena", 4)), 10, 11, Control.Scripted)
                .Npc("orc", "orc_warrior", 16, 10, Control.Scripted)
                .StartCondition("sleeper", CombatConditionType.Asleep, 10)
                .Tweak("hero", c => { Sturdy(c); RulesScenarios.StripOffHand(c); })
                .Tweak("sleeper", Sturdy)
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero", "sleeper")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1,
                    ui ? Step.WakeAlly("sleeper") : Step.Action(ActionIds.WakeAlly, "sleeper"),
                    Step.Assert("after the wake", ctx =>
                    {
                        Note12(ctx, "sleeper.asleep", ctx.Get("sleeper").HasCondition(CombatConditionType.Asleep));
                        return NoteLoadAndActions(ctx, "hero", "hero");
                    }));
            if (ui)
                b = b.Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"));
            return b
                .Expect("The wake step is done", Expect.StepStatus("hero", 1, ui ? "WakeAlly" : "Action", 0, "done"))
                .Expect("The ally is awake (PHB p.280)", Expect.All(Fact12("sleeper.asleep", "False"),
                    v => v.Log("shakes Brena awake").Count == 1 ? ExpectResult.Pass("woken") : ExpectResult.Fail("no wake line")))
                .Expect("Waking is a standard action: the move action is left", Expect.All(Fact12("hero.standard", "False"), Fact12("hero.move", "True")))
                .Expect("Nothing provokes", Expect.None("aoo", e => e.Str("target") == "hero"))
                .Build();
        }

        /// <summary>A Wall of Ice on <paramref name="cells"/> (PHB p.299), caster level 9, intact; in force once its Start has run (a frame later).</summary>
        private static void PlaceWallOfIce(params UnityEngine.Vector2Int[] cells) => PlaceWallOfIce(9, cells);

        /// <summary>A Wall of Ice of caster level <paramref name="casterLevel"/> on <paramref name="cells"/> (PHB p.299; break DC 15 + CL).</summary>
        private static void PlaceWallOfIce(int casterLevel, params UnityEngine.Vector2Int[] cells)
        {
            var go = new UnityEngine.GameObject("WallOfIce_Test_Area");
            go.transform.position = SquareGridUtils.GridToWorld(cells[0]);
            WallOfIceAreaEffect wall = go.AddComponent<WallOfIceAreaEffect>();
            wall.CenterPosition = go.transform.position;
            wall.CenterCell = cells[0];
            wall.LengthSquares = System.Math.Max(2, cells.Length);
            wall.RoundsRemaining = 10;
            wall.CasterLevel = casterLevel;
            wall.SetExplicitCells(new HashSet<UnityEngine.Vector2Int>(cells));
        }

        /// <summary>
        /// CORE-040 (CORE-012): an AI-run orc boxed in by a Wall of Ice cannot reach the heroes, so its wall branch attacks an
        /// adjacent wall square with its weapon (NPCAttackWallForAI, the attack-wall executor through RunAIAction). The
        /// executor continues no PC UI, so the turn advances exactly once: round 1 is the hero, the orc, then the Ui ally,
        /// each once, and the ally's End Turn ends the round. Before the fix the PC attack follow-up (AfterAttackDelay) ran
        /// inside the NPC coroutine and could end or redraw the next turn.
        /// </summary>
        private static ScenarioDef NpcWallOfIceAttack()
        {
            var ring = new[]
            {
                new UnityEngine.Vector2Int(9, 9), new UnityEngine.Vector2Int(10, 9), new UnityEngine.Vector2Int(11, 9),
                new UnityEngine.Vector2Int(9, 10), new UnityEngine.Vector2Int(11, 10),
                new UnityEngine.Vector2Int(9, 11), new UnityEngine.Vector2Int(10, 11), new UnityEngine.Vector2Int(11, 11),
            };
            return Rules("rules/npc-wall-of-ice-attack-ai", "An NPC's Wall of Ice attack advances the turn exactly once (CORE-040, CORE-012; PHB p.299)")
                .Covers("CORE-040", "CORE-012", "AI-054", "PHB p.299", "PHB p.165")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Aldric", 4)), 15, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 10, 10, Control.Ai)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Brena", 4)), 15, 12, Control.Ui)
                .Tweak("hero", Sturdy)
                .Tweak("ally", Sturdy)
                .Tweak("orc", Sturdy)
                .Initiative("hero", "orc", "ally")
                .Turn("hero", 1, Step.Assert("a Wall of Ice boxes the orc in (fixture)", ctx => { PlaceWallOfIce(ring); return true; }), Step.Pass())
                .Turn("ally", 1, Step.Pass())
                .Expect("The orc attacks the wall in round 1 (its wall branch)", v =>
                    v.Log("attacks Wall of Ice").Count >= 1 ? ExpectResult.Pass("wall attacked") : ExpectResult.Fail("no wall attack"))
                .Expect("Round 1 is the hero, the orc, then the ally, each once: the turn advanced once after the orc (CORE-040)", v =>
                {
                    List<string> order = v.Of("turn_start").Where(e => e.Round == 1).Select(e => e.Str("actor")).ToList();
                    string got = string.Join(",", order);
                    return got == "hero,orc,ally" ? ExpectResult.Pass(got) : ExpectResult.Fail(got + ", expected hero,orc,ally");
                })
                .Expect("The ally's Ui turn ran its step (no stray turn end took it)", Expect.StepStatus("ally", 1, "Pass", 0, "done"))
                .Build();
        }

        // ── Step 13 review: the defensive aid, the aid's expiry, the NPC Strength check and the PC wall buttons ──────

        /// <summary>Notes the +2 AC aid bonuses <paramref name="allyKey"/> holds against <paramref name="foeKey"/>'s attacks (GameManager.GetAidAnotherDefenseBonuses).</summary>
        private static bool NoteAidDefense(ScenarioContext ctx, string tag, string allyKey, string foeKey)
        {
            int total = 0;
            foreach (GameManager.AidBonus b in ctx.Gm.GetAidAnotherDefenseBonuses(ctx.Get(allyKey), ctx.Get(foeKey)))
                total += b.Bonus;
            Note12(ctx, tag, total);
            return true;
        }

        /// <summary>
        /// D6 (PHB p.154): the defensive aid. A fighter beside an orc aids its ally's AC against that orc (every d20 forced to
        /// 15, so the attack roll against AC 10 hits); the ally holds +2 AC against the orc, and the orc's attack on the ally
        /// uses it up. The aid-another executor through RunAIAction (Step.AidAnother, Scripted).
        /// </summary>
        private static ScenarioDef AidAnotherDefense()
        {
            return Rules("rules/aid-another-defense-ai", "Aid another's defense gives the ally +2 AC against the opponent's next attack (PHB p.154; D6)")
                .Covers("AI-054", "D6", "PHB p.154")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Aldric", 4)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Brena", 4)), 12, 10, Control.Scripted)
                .Tweak("hero", c => { Sturdy(c); RulesScenarios.StripOffHand(c); })
                .Tweak("ally", c => { Sturdy(c); RulesScenarios.StripOffHand(c); })
                .Tweak("orc", Sturdy)
                .Force(20, 15, null, -1)
                .Initiative("hero", "orc", "ally")
                .Turn("hero", 1, Step.AidAnother("ally", "orc", defense: true),
                    Step.Assert("after the aid", ctx => NoteAidDefense(ctx, "aidac.before", "ally", "orc")))
                .Turn("orc", 1, Step.Attack("ally"), Step.Assert("after the orc's attack", ctx => NoteAidDefense(ctx, "aidac.after", "ally", "orc")))
                .Turn("ally", 1, Step.Pass())
                .Expect("The aid step is done", Expect.StepStatus("hero", 1, "AidAnother", 0, "done"))
                .Expect("The attack roll against AC 10 hits (PHB p.154)", v =>
                    v.Log("Aid Another success!").Count == 1 ? ExpectResult.Pass("success") : ExpectResult.Fail(v.Log("Aid Another").Count + " aid lines"))
                .Expect("The ally holds +2 AC against the orc, and the orc's attack uses it up", Expect.All(Fact12("aidac.before", "2"), Fact12("aidac.after", "0"),
                    v => v.Log("Aid defense consumed").Count == 1 ? ExpectResult.Pass("consumed") : ExpectResult.Fail(v.Log("Aid defense consumed").Count + " consumed lines")))
                .Build();
        }

        /// <summary>
        /// PHB p.154: the aid's +2 lasts only until the beginning of the aider's next turn. The fighter aids its ally's attack
        /// on the orc in round 1; the ally does not attack; the bonus is still there on the ally's round-1 turn and gone when
        /// the aider's round-2 turn starts (before the batch 13 step 13 review it lasted until the start of the ally's second
        /// turn after the grant, so it was still there).
        /// </summary>
        private static ScenarioDef AidAnotherExpires()
        {
            return Rules("rules/aid-another-expires-ai", "An unused aid bonus ends at the start of the aider's next turn (PHB p.154; D6)")
                .Covers("AI-054", "D6", "PHB p.154")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Aldric", 4)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Brena", 4)), 12, 10, Control.Scripted)
                .Tweak("hero", c => { Sturdy(c); RulesScenarios.StripOffHand(c); })
                .Tweak("ally", c => { Sturdy(c); RulesScenarios.StripOffHand(c); })
                .Tweak("orc", Sturdy)
                .Force(20, 15, null, -1)
                .Initiative("hero", "ally", "orc")
                .Turn("hero", 1, Step.AidAnother("ally", "orc"))
                .Turn("ally", 1, Step.Assert("the ally's turn after the aid", ctx => NoteAidBonus(ctx, "aid.r1", "ally", "orc")), Step.Pass())
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 2, Step.Assert("the aider's next turn", ctx => NoteAidBonus(ctx, "aid.r2", "ally", "orc")), Step.Pass())
                .Turn("ally", 2, Step.Pass())
                .Turn("orc", 2, Step.Pass())
                .Expect("The aid hits (PHB p.154)", v =>
                    v.Log("Aid Another success!").Count == 1 ? ExpectResult.Pass("success") : ExpectResult.Fail(v.Log("Aid Another").Count + " aid lines"))
                .Expect("The +2 holds through the ally's round-1 turn and ends at the start of the aider's next turn",
                    Expect.All(Fact12("aid.r1", "2"), Fact12("aid.r2", "0"),
                        v => v.Log("Aid offense bonus expired").Count == 1 ? ExpectResult.Pass("expired") : ExpectResult.Fail(v.Log("Aid offense bonus expired").Count + " expiry lines")))
                .Build();
        }

        /// <summary>The ring of eight Wall of Ice squares around (10, 10).</summary>
        private static UnityEngine.Vector2Int[] RingAround10x10() => new[]
        {
            new UnityEngine.Vector2Int(9, 9), new UnityEngine.Vector2Int(10, 9), new UnityEngine.Vector2Int(11, 9),
            new UnityEngine.Vector2Int(9, 10), new UnityEngine.Vector2Int(11, 10),
            new UnityEngine.Vector2Int(9, 11), new UnityEngine.Vector2Int(10, 11), new UnityEngine.Vector2Int(11, 11),
        };

        /// <summary>
        /// CORE-040, the Strength variant (PHB p.299): an AI-run orc with no weapon and Strength 22, boxed in by a caster level
        /// 1 Wall of Ice (DC 16), takes its wall branch's Strength check (NPCBreakWallForAI, the attack-wall executor's strength
        /// variant through RunAIAction) on an adjacent square. The turn advances exactly once: hero, orc, then the Ui ally.
        /// </summary>
        private static ScenarioDef NpcWallOfIceBreak()
        {
            UnityEngine.Vector2Int[] ring = RingAround10x10();
            return Rules("rules/npc-wall-of-ice-break-ai", "An unarmed NPC's Strength check on a Wall of Ice advances the turn exactly once (CORE-040; PHB p.299)")
                .Covers("CORE-040", "CORE-012", "AI-054", "PHB p.299")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Aldric", 4)), 15, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 10, 10, Control.Ai)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Brena", 4)), 15, 12, Control.Ui)
                .Tweak("hero", Sturdy)
                .Tweak("ally", Sturdy)
                .Tweak("orc", c =>
                {
                    Sturdy(c);
                    c.Stats.STR = 22;
                    global::Inventory inv = Inv(c);
                    if (inv != null)
                    {
                        inv.RightHandSlot = null;
                        inv.LeftHandSlot = null;
                        // No weapon in the pack either: since batch 13 step 15 an AI creature with empty hands draws one (AI-073).
                        if (inv.GeneralSlots != null)
                            for (int i = 0; i < inv.GeneralSlots.Length; i++)
                                if (inv.GeneralSlots[i] != null && inv.GeneralSlots[i].IsWeapon)
                                    inv.GeneralSlots[i] = null;
                        inv.RecalculateStats();
                    }
                })
                .Initiative("hero", "orc", "ally")
                .Turn("hero", 1, Step.Assert("a Wall of Ice (CL 1) boxes the orc in (fixture)", ctx => { PlaceWallOfIce(1, ring); return true; }), Step.Pass())
                .Turn("ally", 1, Step.Pass())
                .Expect("The unarmed orc tries to break the wall in round 1 (its wall branch's Strength check)", v =>
                    v.Log("attempts to break through Wall of Ice").Count >= 1 ? ExpectResult.Pass("strength check") : ExpectResult.Fail("no Strength check"))
                .Expect("Round 1 is the hero, the orc, then the ally, each once: the turn advanced once after the orc (CORE-040)", v =>
                {
                    List<string> order = v.Of("turn_start").Where(e => e.Round == 1).Select(e => e.Str("actor")).ToList();
                    string got = string.Join(",", order);
                    return got == "hero,orc,ally" ? ExpectResult.Pass(got) : ExpectResult.Fail(got + ", expected hero,orc,ally");
                })
                .Expect("The ally's Ui turn ran its step (no stray turn end took it)", Expect.StepStatus("ally", 1, "Pass", 0, "done"))
                .Build();
        }

        /// <summary>The number of arrows <paramref name="key"/> carries, noted under "<paramref name="tag"/>".</summary>
        private static bool NoteArrows(ScenarioContext ctx, string key, string tag)
        {
            global::Inventory inv = Inv(ctx.Get(key));
            Note12(ctx, tag, inv != null ? inv.GetTotalAmmoCount(AmmunitionType.Arrow) : -1);
            return true;
        }

        /// <summary>
        /// The PC's Attack button on a Wall of Ice square out of plain adjacency (batch 13 step 13 review; PHB p.165-166: any
        /// attack can target an object). <paramref name="ranged"/>: a Ui fighter with a longbow and 20 more arrows shoots the wall
        /// 15 ft away: the shot is made (the attack step the button paid), deals half damage to the object (PHB p.165),
        /// spends one arrow. Else a Ui fighter with a longspear strikes the wall 10 ft away (its reach band; it could not
        /// strike an adjacent square). Both squares are the Attack button's own highlights (IsWallCellInWeaponAttackReach),
        /// which the executor's pre-check now accepts; before the review it refused every square that was not adjacent.
        /// </summary>
        private static ScenarioDef PcWallAttack(bool ranged)
        {
            int wallX = ranged ? 13 : 12;
            var wall = new[] { new UnityEngine.Vector2Int(wallX, 9), new UnityEngine.Vector2Int(wallX, 10), new UnityEngine.Vector2Int(wallX, 11) };
            ScenarioBuilder b = Rules(ranged ? "rules/pc-wall-of-ice-attack-ui" : "rules/pc-wall-of-ice-reach-ui",
                    ranged ? "The PC's Attack button shoots a Wall of Ice square at range: half damage, one arrow (PHB p.165-166; CORE-040)"
                           : "The PC's Attack button strikes a Wall of Ice square at a reach weapon's 10 ft (PHB p.113, p.165-166; CORE-040)")
                .Covers("CORE-040", "AI-054", "PHB p.165", "PHB p.299", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Aldric", 4)), 10, 10, Control.Ui)
                .Npc("orc", "orc_warrior", 18, 10, Control.Scripted)
                .Tweak("hero", c =>
                {
                    Sturdy(c);
                    RulesScenarios.StripOffHand(c);
                    if (ranged)
                    {
                        Wield(c, ItemDatabase.CloneItem(ItemID.WeaponLongbow));
                        ItemData arrows = ItemDatabase.CloneItem(ItemID.AmmoArrow);
                        arrows.Quantity = 20;
                        Inv(c).AddItem(arrows);
                        Inv(c).RecalculateStats();
                    }
                    else
                    {
#pragma warning disable CS0618 // no ItemID enum value for the longspear; the string id is the only one
                        Wield(c, ItemDatabase.CloneItem(ItemIDs.LONGSPEAR));
#pragma warning restore CS0618
                    }
                })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Assert("a Wall of Ice stands between them (fixture)", ctx => { PlaceWallOfIce(wall); return true; }), Step.Pass())
                .Turn("hero", 1,
                    Step.Assert("before the wall attack", ctx => NoteArrows(ctx, "hero", "arrows.before")),
                    Step.AttackWall(wallX, 10),
                    Step.Assert("after the wall attack", ctx => NoteArrows(ctx, "hero", "arrows.after") && NoteLoadAndActions(ctx, "hero", "hero")));
            b = b.Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The wall step is done", Expect.StepStatus("hero", 1, "AttackWall", 0, "done"))
                .Expect("The hero attacks the wall square at " + (ranged ? "15 ft" : "10 ft"), v =>
                    v.Log("attacks Wall of Ice at \\(" + wallX + ",10\\)").Count == 1 ? ExpectResult.Pass("wall attacked") : ExpectResult.Fail(v.Log("attacks Wall of Ice").Count + " wall attack lines"))
                .Expect("The attack was the Attack button's paid step: the standard action is spent", Fact12("hero.standard", "False"))
                .Expect("No refusal line", v => v.Log("out of .*(reach|range)|must be adjacent").Count == 0 ? ExpectResult.Pass("none") : ExpectResult.Fail("refused"));
            if (ranged)
                b = b.Expect("A ranged attack deals half damage to an object (PHB p.165)", v =>
                        v.Log("Ranged weapon damage to an object is halved").Count == 1 ? ExpectResult.Pass("halved") : ExpectResult.Fail("no halving line"))
                    .Expect("The shot spends one arrow", v =>
                    {
                        // Relative: the Fighter preset carries its own arrows beside the fixture's 20.
                        string before = NoteValue(v, "arrows.before", out int seqBefore);
                        string after = NoteValue(v, "arrows.after", out int seqAfter);
                        if (!int.TryParse(before, out int nBefore) || !int.TryParse(after, out int nAfter))
                            return ExpectResult.Inconclusive("no arrow notes");
                        string detail = "arrows " + nBefore + " -> " + nAfter;
                        return nBefore >= 20 && nAfter == nBefore - 1 ? ExpectResult.Pass(detail, seqAfter) : ExpectResult.Fail(detail + ", expected one spent", seqAfter);
                    });
            else
                b = b.Expect("A melee attack's damage is not halved", v =>
                    v.Log("Ranged weapon damage to an object is halved").Count == 0 ? ExpectResult.Pass("not halved") : ExpectResult.Fail("halved"));
            return b.Build();
        }

        /// <summary>
        /// The PC's Break Wall button (PHB p.299: a Strength check, DC 15 + caster level, a standard action): a Ui fighter next
        /// to a Wall of Ice presses Break Wall and picks the adjacent square; the attack-wall executor's strength variant
        /// pays the standard action and rolls the check. The move action is left.
        /// </summary>
        private static ScenarioDef PcBreakWall()
        {
            var wall = new[] { new UnityEngine.Vector2Int(11, 9), new UnityEngine.Vector2Int(11, 10), new UnityEngine.Vector2Int(11, 11) };
            return Rules("rules/pc-break-wall-ui", "The PC's Break Wall button makes the Strength check against an adjacent Wall of Ice square (PHB p.299; CORE-040)")
                .Covers("CORE-040", "AI-054", "PHB p.299", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Aldric", 4)), 10, 10, Control.Ui)
                .Npc("orc", "orc_warrior", 16, 10, Control.Scripted)
                .Tweak("hero", c => { Sturdy(c); RulesScenarios.StripOffHand(c); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Assert("a Wall of Ice beside the hero (fixture)", ctx => { PlaceWallOfIce(wall); return true; }), Step.Pass())
                .Turn("hero", 1, Step.AttackWall(11, 10, strength: true),
                    Step.Assert("after the Strength check", ctx => NoteLoadAndActions(ctx, "hero", "hero")))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The Break Wall step is done", Expect.StepStatus("hero", 1, "AttackWall", 0, "done"))
                .Expect("The hero makes the Strength check on the adjacent square", v =>
                    v.Log("attempts to break through Wall of Ice at \\(11,10\\)").Count == 1 ? ExpectResult.Pass("checked") : ExpectResult.Fail("no Strength check line"))
                .Expect("The check is a standard action: the move action is left", Expect.All(Fact12("hero.standard", "False"), Fact12("hero.move", "True")))
                .Build();
        }

        // ── Step 15: equipment actions (D8; ITM-069, AI-073, AI-074, CMB-181) ──

        /// <summary>
        /// Empties <paramref name="c"/>'s hands and every weapon and shield from its pack, then holds <paramref name="right"/>
        /// and <paramref name="left"/> (null: empty) and stows <paramref name="pack"/>.
        /// </summary>
        private static void Gear(CharacterController c, ItemID? right, ItemID? left, params ItemID[] pack)
        {
            global::Inventory inv = Inv(c);
            if (inv == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no inventory");
            if (inv.GeneralSlots != null)
                for (int i = 0; i < inv.GeneralSlots.Length; i++)
                    if (inv.GeneralSlots[i] != null && (inv.GeneralSlots[i].IsWeapon || inv.GeneralSlots[i].IsShield))
                        inv.GeneralSlots[i] = null;
            inv.RightHandSlot = right.HasValue ? ItemDatabase.CloneItem(right.Value) : null;
            inv.LeftHandSlot = left.HasValue ? ItemDatabase.CloneItem(left.Value) : null;
            inv.HandsSlot = null;
            foreach (ItemID id in pack)
                inv.AddItem(ItemDatabase.CloneItem(id));
            inv.RecalculateStats();
        }

        /// <summary>Notes <paramref name="key"/>'s hands, its pack's weapons and shields and the actions it has left, under "<paramref name="tag"/>.*".</summary>
        private static bool NoteGear(ScenarioContext ctx, string key, string tag)
        {
            CharacterController c = ctx.Get(key);
            global::Inventory inv = Inv(c);
            Note12(ctx, tag + ".right", inv != null && inv.RightHandSlot != null ? inv.RightHandSlot.Id : "none");
            Note12(ctx, tag + ".left", inv != null && inv.LeftHandSlot != null ? inv.LeftHandSlot.Id : "none");
            Note12(ctx, tag + ".pack", inv != null && inv.GeneralSlots != null
                ? string.Join("+", inv.GeneralSlots.Where(i => i != null && (i.IsWeapon || i.IsShield)).Select(i => i.Id).OrderBy(i => i, StringComparer.Ordinal))
                : "?");
            Note12(ctx, tag + ".move", c.Actions.HasMoveAction);
            Note12(ctx, tag + ".standard", c.Actions.HasStandardAction);
            return true;
        }

        /// <summary><paramref name="by"/>'s attacks of opportunity on <paramref name="target"/>: exactly <paramref name="want"/>, each with trigger <paramref name="trigger"/> (null: any).</summary>
        private static Func<TraceView, ExpectResult> AoOCount(string by, string target, string trigger, int want)
            => v =>
            {
                List<TraceEvent> all = v.AoOs(by, target);
                int matching = all.Count(e => trigger == null || e.Str("trigger") == trigger);
                return all.Count == want && matching == want
                    ? ExpectResult.Pass(want + " AoO(s)" + (trigger != null ? " for " + trigger : ""), all.Select(e => e.Seq).ToArray())
                    : ExpectResult.Fail(all.Count + " AoO(s) (" + string.Join(",", all.Select(e => e.Str("trigger"))) + "), expected " + want
                        + (trigger != null ? " for " + trigger : ""), all.Select(e => e.Seq).ToArray());
            };

        private static Func<TraceView, ExpectResult> HasLog(string regex, string what)
            => v =>
            {
                List<TraceEvent> lines = v.Log(regex);
                return lines.Count > 0 ? ExpectResult.Pass(what, lines[0].Seq) : ExpectResult.Fail("no " + what + " line");
            };

        /// <summary>
        /// D8 (PHB p.142, Table 8-2): drawing a weapon is a move action that does not provoke. A fighter 4 with empty hands
        /// and a longsword in its pack, beside an orc that has acted (so it is not flat-footed and threatens), draws the
        /// longsword without moving first: the orc takes no attack of opportunity, the sword is in the main hand, the move
        /// action is spent and the standard action left. The Ui twin runs the PC runner (no prompt opens), the AI twin
        /// the same executor through RunAIAction.
        /// </summary>
        private static ScenarioDef DrawWeaponMove(bool ui)
        {
            ScenarioBuilder b = Rules(ui ? "rules/draw-weapon-move-ui" : "rules/draw-weapon-move-ai",
                    "Drawing a weapon is a move action that does not provoke (PHB p.142, Table 8-2; D8, ITM-069)")
                .Covers("ITM-069", "D8", "PHB p.142", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, ui ? Control.Ui : Control.Scripted)
                .Tweak("hero", c => { Sturdy(c); Gear(c, null, null, ItemID.WeaponLongsword); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1, Step.ActionItem(ActionIds.DrawWeapon, ItemIDs.LONGSWORD),
                    Step.Assert("after the draw", ctx => NoteGear(ctx, "hero", "hero")));
            if (ui)
                b = b.Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"));
            return b
                .Expect("The draw step is done", Expect.StepStatus("hero", 1, "Action", 0, "done"))
                .Expect("Drawing provokes no attack of opportunity (PHB p.142, Table 8-2)", AoOCount("orc", "hero", null, 0))
                .Expect("The longsword is in hand, out of the pack", Expect.All(Fact12("hero.right", ItemIDs.LONGSWORD), Fact12("hero.pack", "")))
                .Expect("The draw spent the move action; the standard action is left", Expect.All(Fact12("hero.move", "False"), Fact12("hero.standard", "True")))
                .Expect("The log names the move action", HasLog("draws Longsword [(]move action[)]", "draw"))
                .Build();
        }

        /// <summary>
        /// Table 8-2 note 4 (PHB p.142): with a base attack bonus of +1 or higher a creature may draw a weapon as a free
        /// action combined with a regular move (delegated RAW reading 2026-10-29: one draw for a move action spent moving,
        /// not with a 5-foot step or a charge). A Ui fighter 1 (BAB +1) with its longsword stowed moves 10 ft., then draws:
        /// the draw is free, so the standard action is still left.
        /// </summary>
        private static ScenarioDef DrawWithMoveBab1Ui()
        {
            return Rules("rules/draw-with-move-bab1-ui", "At BAB +1 a draw is free with a regular move (PHB p.142, Table 8-2 note 4; D8)")
                .Covers("ITM-069", "D8", "PHB p.142")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 1)), 10, 10, Control.Ui)
                .Npc("orc", "orc_warrior", 18, 10, Control.Idle)
                .Tweak("hero", c => { Sturdy(c); Gear(c, null, null, ItemID.WeaponLongsword); })
                .Tweak("orc", Sturdy)
                .Initiative("hero", "orc")
                .Turn("hero", 1, Step.Move(12, 10), Step.ActionItem(ActionIds.DrawWeapon, ItemIDs.LONGSWORD),
                    Step.Assert("after the draw", ctx => NoteGear(ctx, "hero", "hero")))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The draw step is done", Expect.StepStatus("hero", 1, "Action", 0, "done"))
                .Expect("The longsword is in hand", Fact12("hero.right", ItemIDs.LONGSWORD))
                .Expect("The move spent the move action; the draw spent nothing: the standard action is left", Expect.All(Fact12("hero.move", "False"), Fact12("hero.standard", "True")))
                .Expect("The log names the free draw with the move", HasLog("draws Longsword [(]free action with its move[)]", "free draw"))
                .Build();
        }

        /// <summary>
        /// Quick Draw (PHB p.98): a creature with the feat draws a weapon as a free action. A fighter 4 with Quick Draw, empty
        /// hands and a stowed longsword, beside an orc, draws it: no attack of opportunity, and both its move and standard
        /// actions are left. Ui and AI twins run the same executor.
        /// </summary>
        private static ScenarioDef QuickDrawFree(bool ui)
        {
            ScenarioBuilder b = Rules(ui ? "rules/quick-draw-free-ui" : "rules/quick-draw-free-ai",
                    "Quick Draw draws a weapon as a free action (PHB p.98; D8, CHR-021)")
                .Covers("CHR-021", "ITM-069", "D8", "PHB p.98", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4, "Quick Draw")), 10, 10, ui ? Control.Ui : Control.Scripted)
                .Tweak("hero", c => { Sturdy(c); Gear(c, null, null, ItemID.WeaponLongsword); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1, Step.ActionItem(ActionIds.DrawWeapon, ItemIDs.LONGSWORD),
                    Step.Assert("after the draw", ctx => NoteGear(ctx, "hero", "hero")));
            if (ui)
                b = b.Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"));
            return b
                .Expect("The draw step is done", Expect.StepStatus("hero", 1, "Action", 0, "done"))
                .Expect("No attack of opportunity", AoOCount("orc", "hero", null, 0))
                .Expect("The longsword is in hand", Fact12("hero.right", ItemIDs.LONGSWORD))
                .Expect("The draw is free: the move and standard actions are left (PHB p.98)", Expect.All(Fact12("hero.move", "True"), Fact12("hero.standard", "True")))
                .Expect("The log names Quick Draw", HasLog("draws Longsword [(]free action, Quick Draw[)]", "Quick Draw"))
                .Build();
        }

        /// <summary>
        /// D8 (PHB p.142, Table 8-2): sheathing a weapon is a move action that provokes. A Ui fighter 4 holding a longsword
        /// beside an orc sheathes it, answering the attack-of-opportunity prompt Proceed: the orc takes one attack of
        /// opportunity for the sheathe, the sword goes to the pack, the move action is spent and the standard action left.
        /// </summary>
        private static ScenarioDef SheatheProvokesUi()
        {
            return Rules("rules/sheathe-provokes-ui", "Sheathing a weapon is a move action that provokes (PHB p.142, Table 8-2; D8, ITM-069)")
                .Covers("ITM-069", "D8", "PHB p.142")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Ui)
                .Tweak("hero", c => { Sturdy(c); Gear(c, ItemID.WeaponLongsword, null); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1, Step.ActionItem(ActionIds.SheatheWeapon, ItemIDs.LONGSWORD), Step.AnswerAoO(AoOAnswer.Proceed),
                    Step.Assert("after the sheathe", ctx => NoteGear(ctx, "hero", "hero")))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The sheathe step is done", Expect.StepStatus("hero", 1, "Action", 0, "done"))
                .Expect("The orc takes one attack of opportunity for the sheathe (PHB p.142)", AoOCount("orc", "hero", "sheathe Longsword", 1))
                .Expect("The longsword is in the pack, the hand empty", Expect.All(Fact12("hero.right", "none"), Fact12("hero.pack", ItemIDs.LONGSWORD)))
                .Expect("The move action is spent; the standard action is left", Expect.All(Fact12("hero.move", "False"), Fact12("hero.standard", "True")))
                .Build();
        }

        /// <summary>
        /// D8 (PHB p.142-143, Table 8-2): readying a shield is a move action that does not provoke. A Ui fighter 4 with a
        /// longsword in its main hand and a heavy wooden shield in its pack, beside an orc, readies the shield: no attack
        /// of opportunity, the shield is on the off-hand arm, the move action spent and the standard action left.
        /// </summary>
        private static ScenarioDef ReadyShieldUi()
        {
            return Rules("rules/ready-shield-ui", "Readying a shield is a move action that does not provoke (PHB p.142-143, Table 8-2; D8, ITM-069)")
                .Covers("ITM-069", "D8", "PHB p.142", "PHB p.143")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Ui)
                .Tweak("hero", c => { Sturdy(c); Gear(c, ItemID.WeaponLongsword, null, ItemID.ShieldHeavyWooden); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1, Step.ActionItem(ActionIds.ReadyShield, ItemIDs.SHIELD_HEAVY_WOODEN),
                    Step.Assert("after the shield", ctx => NoteGear(ctx, "hero", "hero")))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The shield step is done", Expect.StepStatus("hero", 1, "Action", 0, "done"))
                .Expect("No attack of opportunity", AoOCount("orc", "hero", null, 0))
                .Expect("The shield is readied on the arm, the sword still in hand", Expect.All(
                    Fact12("hero.left", ItemIDs.SHIELD_HEAVY_WOODEN), Fact12("hero.right", ItemIDs.LONGSWORD), Fact12("hero.pack", "")))
                .Expect("The move action is spent; the standard action is left", Expect.All(Fact12("hero.move", "False"), Fact12("hero.standard", "True")))
                .Build();
        }

        /// <summary>
        /// D8 (PHB p.143, Table 8-2): loosing a shield (unstrapping and dropping it) is a move action that does not provoke.
        /// A Ui fighter 4 with a longsword and a heavy wooden shield readied, beside an orc, looses the shield: no attack of
        /// opportunity, the shield lies in the hero's square, the move action is spent and the standard action left.
        /// </summary>
        private static ScenarioDef LooseShieldUi()
        {
            return Rules("rules/loose-shield-ui", "Loosing a shield is a move action that does not provoke; the shield drops (PHB p.143, Table 8-2; D8, ITM-069)")
                .Covers("ITM-069", "D8", "PHB p.143")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Ui)
                .Tweak("hero", c => { Sturdy(c); Gear(c, ItemID.WeaponLongsword, ItemID.ShieldHeavyWooden); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1, Step.ActionItem(ActionIds.ReadyShield, ItemIDs.SHIELD_HEAVY_WOODEN),
                    Step.Assert("after the loose", ctx =>
                    {
                        CharacterController hero = ctx.Get("hero");
                        SquareCell cell = SquareGrid.Instance != null ? SquareGrid.Instance.GetCell(hero.GridPosition) : null;
                        // At least one: the grid is shared by the seeds of a run, so earlier seeds' shields may lie there too.
                        Note12(ctx, "hero.ground", cell != null && cell.GroundItems.Any(i => i != null && i.Id == ItemIDs.SHIELD_HEAVY_WOODEN));
                        return NoteGear(ctx, "hero", "hero");
                    }))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The loose step is done", Expect.StepStatus("hero", 1, "Action", 0, "done"))
                .Expect("No attack of opportunity", AoOCount("orc", "hero", null, 0))
                .Expect("The shield lies in the hero's square; the sword is still in hand", Expect.All(
                    Fact12("hero.left", "none"), Fact12("hero.right", ItemIDs.LONGSWORD), Fact12("hero.ground", "True")))
                .Expect("The move action is spent; the standard action is left", Expect.All(Fact12("hero.move", "False"), Fact12("hero.standard", "True")))
                .Expect("The log names the loose", HasLog("looses Shield, Heavy Wooden and drops it [(]move action[)]", "loose"))
                .Build();
        }

        /// <summary>
        /// D8 (PHB p.142, Table 8-2 note 4; delegated RAW reading 2026-10-29): a Two-Weapon Fighting creature draws two light
        /// or one-handed weapons in the time of one. A Ui fighter 4 with the feat, empty hands and a dagger and a shortsword
        /// stowed draws the dagger (a move action), then at once the shortsword (free, into the other hand): the standard
        /// action is left.
        /// </summary>
        private static ScenarioDef TwfPairedDrawUi()
        {
            return Rules("rules/twf-paired-draw-ui", "Two-Weapon Fighting draws two weapons in the time of one (PHB p.142, Table 8-2 note 4; D8)")
                .Covers("ITM-069", "D8", "PHB p.142")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4, "Two-Weapon Fighting")), 10, 10, Control.Ui)
                .Tweak("hero", c => { Sturdy(c); Gear(c, null, null, ItemID.WeaponDagger, ItemID.WeaponShortsword); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1, Step.ActionItem(ActionIds.DrawWeapon, ItemIDs.DAGGER), Step.ActionItem(ActionIds.DrawWeapon, ItemIDs.SHORT_SWORD),
                    Step.Assert("after the draws", ctx => NoteGear(ctx, "hero", "hero")))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("Both draw steps are done", Expect.All(Expect.StepStatus("hero", 1, "Action", 0, "done"), Expect.StepStatus("hero", 1, "Action", 1, "done")))
                .Expect("A weapon in each hand", Expect.All(Fact12("hero.right", ItemIDs.DAGGER), Fact12("hero.left", ItemIDs.SHORT_SWORD), Fact12("hero.pack", "")))
                .Expect("One move action for both draws; the standard action is left", Expect.All(Fact12("hero.move", "False"), Fact12("hero.standard", "True")))
                .Expect("The log names the paired draw", HasLog("draws Shortsword [(]free, drawn with the first by Two-Weapon Fighting[)]", "paired draw"))
                .Build();
        }

        /// <summary>
        /// Owner question Q5's default (batch 13 step 15; ITM-095): the character sheet's inventory in combat calls the
        /// equipment entries, armor stays locked, and a hand slot the PC names is strict. A Ui fighter 4 holding a longsword
        /// with a chain shirt and a dagger stowed tries, on its turn, to don the chain shirt (refused with the ITM-095 reason)
        /// and to draw the dagger into its full right hand (refused: no silent move to the left hand). Nothing is spent.
        /// </summary>
        private static ScenarioDef InventoryEquipLockedUi()
        {
            return Rules("rules/inventory-equip-locked-ui", "In combat the inventory locks armor and refuses a draw into a full hand, spending nothing (Q5 default; ITM-095, D8)")
                .Covers("ITM-069", "ITM-095", "Q5", "D8", "PHB p.123")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Ui)
                .Tweak("hero", c =>
                {
                    Sturdy(c);
                    Gear(c, ItemID.WeaponLongsword, null, ItemID.WeaponDagger);
                    Inv(c).AddItem(ItemDatabase.CloneItem(ItemID.ArmorChainShirt));
                })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1, Step.Assert("the inventory's equip attempts", ctx =>
                {
                    CharacterController hero = ctx.Get("hero");
                    global::Inventory inv = Inv(hero);
                    int armor = Array.FindIndex(inv.GeneralSlots, i => i != null && i.Id == ItemIDs.CHAIN_SHIRT);
                    int dagger = Array.FindIndex(inv.GeneralSlots, i => i != null && i.Id == ItemIDs.DAGGER);
                    bool armorOk = ctx.Gm.TryInventoryEquip(hero, armor, EquipSlot.Armor, out string armorWhy);
                    bool daggerOk = ctx.Gm.TryInventoryEquip(hero, dagger, EquipSlot.RightHand, out string daggerWhy);
                    Note12(ctx, "armor.ok", armorOk);
                    Note12(ctx, "armor.itm095", (armorWhy ?? "").Contains("ITM-095"));
                    Note12(ctx, "dagger.ok", daggerOk);
                    Note12(ctx, "dagger.fullhand", (daggerWhy ?? "").Contains("is in that hand"));
                    ctx.Note(NotePrefix + "why=" + armorWhy + " | " + daggerWhy);
                    return NoteGear(ctx, "hero", "hero");
                }), Step.Pass())
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("Donning armor is refused with the ITM-095 reason (PHB Table 7-7, p.123)", Expect.All(Fact12("armor.ok", "False"), Fact12("armor.itm095", "True")))
                .Expect("A draw into the full right hand is refused", Expect.All(Fact12("dagger.ok", "False"), Fact12("dagger.fullhand", "True")))
                .Expect("Nothing changed and nothing was spent", Expect.All(Fact12("hero.right", ItemIDs.LONGSWORD), Fact12("hero.left", "none"),
                    Fact12("hero.move", "True"), Fact12("hero.standard", "True")))
                .Build();
        }

        /// <summary>
        /// D8 (PHB p.142, Table 8-2): picking up an item is a move action that provokes. A fighter 4 with empty hands and a
        /// dagger on its square, beside an orc, picks the dagger up: the orc takes one attack of opportunity for it, the
        /// dagger is in the hero's hand (a free hand; delegated RAW reading 2026-10-29), the move action is spent and the
        /// standard action left. The Ui twin answers the prompt Proceed; the AI twin's decider goes on. One executor.
        /// </summary>
        private static ScenarioDef PickUpProvokes(bool ui)
        {
            var steps = new List<Step>
            {
                Step.Assert("a dagger on the hero's square (fixture)", ctx =>
                {
                    CharacterController hero = ctx.Get("hero");
                    SquareCell cell = SquareGrid.Instance != null ? SquareGrid.Instance.GetCell(hero.GridPosition) : null;
                    if (cell == null)
                        return false;
                    cell.AddGroundItem(ItemDatabase.CloneItem(ItemID.WeaponDagger));
                    return true;
                }),
                Step.Action(ActionIds.PickUpItem, null, ItemIDs.DAGGER),
            };
            if (ui)
                steps.Add(Step.AnswerAoO(AoOAnswer.Proceed));
            steps.Add(Step.Assert("after the pick-up", ctx => NoteGear(ctx, "hero", "hero")));
            ScenarioBuilder b = Rules(ui ? "rules/pick-up-provokes-ui" : "rules/pick-up-provokes-ai",
                    "Picking up an item is a move action that provokes (PHB p.142, Table 8-2; D8, AI-054)")
                .Covers("AI-054", "D8", "PHB p.142", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, ui ? Control.Ui : Control.Scripted)
                .Tweak("hero", c => { Sturdy(c); Gear(c, null, null); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1, steps.ToArray());
            if (ui)
                b = b.Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"));
            return b
                .Expect("The pick-up step is done", Expect.StepStatus("hero", 1, "Action", 0, "done"))
                .Expect("The orc takes one attack of opportunity for the pick-up (PHB p.142)", AoOCount("orc", "hero", "pick up Dagger", 1))
                .Expect("The dagger is in the hero's hand", Fact12("hero.right", ItemIDs.DAGGER))
                .Expect("The move action is spent; the standard action is left", Expect.All(Fact12("hero.move", "False"), Fact12("hero.standard", "True")))
                .Build();
        }

        /// <summary>
        /// AI-073 (batch 13 step 15, D8): an AI-run Enemy archer (test_archer_gust: a longbow in hand, its longsword stowed)
        /// with an empty quiver and a foe adjacent drops the useless bow (a free action, PHB p.141) and draws the longsword
        /// through the draw entry (a move action that does not provoke, PHB p.142), then fights the foe with it in round 1
        /// (an attack, or a maneuver such as a trip, the profile's choice). It never shoots.
        /// </summary>
        private static ScenarioDef ArcherNoAmmoDrawsMeleeAi()
        {
            return Rules("rules/archer-no-ammo-draws-melee-ai", "An AI archer with an empty quiver drops its bow and draws its melee weapon (PHB p.141-142; AI-073, D8)")
                .Covers("AI-073", "ITM-069", "D8", "PHB p.141", "PHB p.142")
                .MaxRounds(1)
                .Npc("archer", "test_archer_gust", 10, 10, Control.Ai)
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Adjacent Hero", 3)), 11, 10, Control.Idle)
                .Tweak("archer", c => { EmptyQuiver(c); Sturdy(c); })
                .Tweak("foe", Sturdy)
                .Initiative("archer", "foe")
                .Expect("It drops the bow (free) before it draws the longsword (move action)", v =>
                {
                    TraceEvent drop = v.Log("drops Longbow [(]free action").FirstOrDefault();
                    TraceEvent draw = v.Log("draws Longsword [(]move action[)]").FirstOrDefault();
                    if (drop == null || draw == null)
                        return ExpectResult.Fail("drop line " + (drop != null) + ", draw line " + (draw != null));
                    return drop.Seq < draw.Seq ? ExpectResult.Pass("drop #" + drop.Seq + " then draw #" + draw.Seq, drop.Seq, draw.Seq)
                        : ExpectResult.Fail("the draw came first", drop.Seq, draw.Seq);
                })
                .Expect("Drawing provokes nothing", AoOCount("foe", "archer", null, 0))
                .Expect("Round 1: it fights the foe in melee (an attack or a maneuver with the drawn sword)", FightsInMeleeBy("archer", "foe", 1))
                .Expect("Any weapon attack it makes is with the longsword", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("archer", null, aoo: false);
                    List<TraceEvent> other = attacks.Where(e => (e.Str("weapon") ?? "").IndexOf("Longsword", StringComparison.OrdinalIgnoreCase) < 0).ToList();
                    return other.Count == 0 ? ExpectResult.Pass(attacks.Count + " longsword attack(s)")
                        : ExpectResult.Fail(other.Count + " attack(s) with " + string.Join(",", other.Select(e => e.Str("weapon"))), other.Select(e => e.Seq).ToArray());
                })
                .Expect("It never shoots", NoBowShot("archer"))
                .Build();
        }

        /// <summary>
        /// AI-074 (design choice C2's interim rule, batch 13 step 15): the AI's attack-mode chooser
        /// (AttackIntent.ChooseAiMode) never throws a creature's last melee weapon unless it has another weapon to draw. A
        /// Scripted orc whose only weapon is a dagger is told to attack a hero 20 ft. away (inside the dagger's thrown range,
        /// PHB p.113): it does not throw, so it makes no ranged attack and still holds the dagger.
        /// </summary>
        private static ScenarioDef NpcKeepsLastMeleeWeaponAi()
        {
            return Rules("rules/npc-keeps-last-melee-weapon-ai", "An AI creature does not throw its last melee weapon (AI-074; C2)")
                .Covers("AI-074", "C2", "PHB p.113")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Idle)
                .Npc("orc", "orc_warrior", 14, 10, Control.Scripted)
                .Tweak("hero", Sturdy)
                .Tweak("orc", c => { Sturdy(c); Gear(c, ItemID.WeaponDagger, null); })
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Attack("hero"), Step.Assert("after", ctx => NoteGear(ctx, "orc", "orc")))
                .Expect("It makes no ranged attack (no throw)", v =>
                {
                    List<TraceEvent> throws = v.Attacks("orc", null, aoo: false).Where(e => e.Bool("ranged")).ToList();
                    return throws.Count == 0 ? ExpectResult.Pass("no throw") : ExpectResult.Fail(throws.Count + " ranged attack(s)", throws.Select(e => e.Seq).ToArray());
                })
                .Expect("It still holds its dagger", Fact12("orc.right", ItemIDs.DAGGER))
                .Build();
        }

        /// <summary>
        /// CMB-181 (PHB p.311: a panicked creature drops anything it holds and flees): an AI-run orc holding a longsword
        /// with a heavy wooden shield readied is panicked by the Scripted hero beside it (the harness stand-in for a failed
        /// save). When Panicked is applied (and, as a backstop, at the start of its panicked turn in the shared flee
        /// controller) it drops the sword (PHB p.141, p.311; delegated RAW reading 2026-10-29) before it flees; the readied shield is strapped on, not held, and stays (delegated RAW reading 2026-10-29).
        /// </summary>
        private static ScenarioDef PanickedDropsHeldAi()
        {
            return Rules("rules/panicked-drops-held-ai", "A panicked AI creature drops what it holds before it flees (PHB p.311, p.141; CMB-181)")
                .Covers("CMB-181", "PHB p.311", "PHB p.141", "D8", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 11, 10, Control.Ai)
                .Tweak("hero", Sturdy)
                .Tweak("orc", c => { Sturdy(c); Gear(c, ItemID.WeaponLongsword, ItemID.ShieldHeavyWooden); })
                .Initiative("hero", "orc")
                .Turn("hero", 1, Step.Assert("the hero panics the orc (fixture)", ctx =>
                {
                    ctx.Gm.ApplyCondition(ctx.Get("orc"), CombatConditionType.Panicked, 3, ctx.Get("hero"), sourceCategory: "Harness");
                    return ctx.Get("orc").HasCondition(CombatConditionType.Panicked);
                }), Step.Pass())
                .Turn("hero", 2, Step.Assert("after the orc's turn", ctx => NoteGear(ctx, "orc", "orc")), Step.Pass())
                .Expect("Round 1: the orc drops the longsword before it moves", DropBeforeMove("orc", "Longsword", 1))
                .Expect("The sword is gone from its hands; the readied shield stays", Expect.All(
                    Fact12("orc.right", "none"), Fact12("orc.left", ItemIDs.SHIELD_HEAVY_WOODEN)))
                .Expect("The orc makes no attack", Expect.None("attack", e => e.Str("attacker") == "orc"))
                .Build();
        }

        /// <summary>
        /// CMB-181 for a PC (the same flee controller, PC_NPC_PARITY): a Ui fighter holding a longsword with a heavy wooden
        /// shield readied is panicked by the Scripted goblin beside it; on its forced turn it drops the sword (a free action)
        /// before it flees, and keeps the readied shield.
        /// </summary>
        private static ScenarioDef PanickedDropsHeldPc()
        {
            return Rules("rules/panicked-drops-held-pc", "A panicked PC drops what it holds before it flees (PHB p.311, p.141; CMB-181)")
                .Covers("CMB-181", "PHB p.311", "PHB p.141", "D8", "Q1", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Npc("gob", "goblin", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 10, 10, Control.Ui)
                .Tweak("hero", c => { Sturdy(c); Gear(c, ItemID.WeaponLongsword, ItemID.ShieldHeavyWooden); })
                .Tweak("gob", Sturdy)
                .Initiative("gob", "hero")
                .Turn("gob", 1, Step.Assert("the goblin panics the hero (fixture)", ctx =>
                {
                    ctx.Gm.ApplyCondition(ctx.Get("hero"), CombatConditionType.Panicked, 3, ctx.Get("gob"), sourceCategory: "Harness");
                    return ctx.Get("hero").HasCondition(CombatConditionType.Panicked);
                }), Step.Pass())
                .Turn("hero", 1, Step.Attack("gob"))
                .Turn("gob", 2, Step.Assert("after the hero's turn", ctx => NoteGear(ctx, "hero", "hero")), Step.Pass())
                .Expect("Round 1: the hero drops the longsword before it moves", DropBeforeMove("hero", "Longsword", 1))
                .Expect("The sword is gone from its hands; the readied shield stays", Expect.All(
                    Fact12("hero.right", "none"), Fact12("hero.left", ItemIDs.SHIELD_HEAVY_WOODEN)))
                .Expect("The hero makes no attack", Expect.None("attack", e => e.Str("attacker") == "hero"))
                .Build();
        }

        /// <summary>The panicked drop of <paramref name="item"/> (DropItemNow's "everything it holds" line, when Panicked is applied or at the panicked turn's start) comes before <paramref name="key"/>'s first move in <paramref name="round"/>.</summary>
        private static Func<TraceView, ExpectResult> DropBeforeMove(string key, string item, int round)
            => v =>
            {
                TraceEvent drop = v.Log("drops " + item + " [(](panicked: )?everything it holds; a free action").FirstOrDefault(e => e.Round == round);
                if (drop == null)
                    return ExpectResult.Fail("no panicked drop line in round " + round);
                TraceEvent move = v.Moves(key, round).FirstOrDefault();
                if (move == null)
                    return ExpectResult.Pass("dropped #" + drop.Seq + " (no move)", drop.Seq);
                return drop.Seq < move.Seq ? ExpectResult.Pass("drop #" + drop.Seq + " before the move #" + move.Seq, drop.Seq, move.Seq)
                    : ExpectResult.Fail("the move came first", drop.Seq, move.Seq);
            };
    }
}
#endif
