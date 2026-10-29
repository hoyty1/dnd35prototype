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
        /// (before step 5 the turn ended after the move).
        /// </summary>
        private static ScenarioDef AiKiterApproachThenAttack()
        {
            return Rules("rules/ai-kiter-approach-then-attack", "An AI kiter that moves into range attacks with its standard action left (AI-045; C1; PHB p.134)")
                .Covers("AI-045", "C1", "D1", "PHB p.134")
                .MaxRounds(1)
                .Npc("wiz", "mist_wizard", 2, 10, Control.Ai)
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Distant Hero", 3)), 16, 10, Control.Idle)
                .Tweak("wiz", c => { Wield(c, ItemDatabase.CloneItem(ItemID.WeaponDagger)); SpendAllSlots(c); Sturdy(c); })
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
                .Expect("It declares no shot", v =>
                {
                    List<TraceEvent> shots = v.Attacks("archer", null, aoo: false);
                    return shots.Count == 0 ? ExpectResult.Pass("no attack") : ExpectResult.Fail(shots.Count + " attack(s)", shots.Select(e => e.Seq).ToArray());
                })
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
        /// AI-073 (narrowed): an AI-run Enemy archer (test_archer_gust) with an empty quiver and a foe 30 ft. away declares
        /// no shot (the guard refuses a projectile weapon without ammunition, PHB p.113), so the refused shot's
        /// "has no Arrow ammunition" line never appears, and it does not walk to its preferred range for a shot it can never
        /// take (AiOutOfReach skips the move without ranged means). It cannot draw its stowed weapon yet (ITM-069, batch 13
        /// step 15).
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
                .Expect("It declares no shot", v =>
                {
                    List<TraceEvent> shots = v.Attacks("archer", null, aoo: false);
                    return shots.Count == 0 ? ExpectResult.Pass("no attack") : ExpectResult.Fail(shots.Count + " attack(s)", shots.Select(e => e.Seq).ToArray());
                })
                .Expect("No refused shot for want of arrows", NoLog("has no Arrow ammunition", "no-ammunition"))
                .Expect("No move to a better shot", NoLog("moves to get a better shot", "preferred-range move"))
                .Build();
        }

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
    }
}
#endif
