#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 13 (docs/designs/action_catalog_batch13.md): the action catalog's shared executors in real fights.
    ///
    /// Step 4 (CMB-179, decision D2): total defense for both sides (PHB p.142): a standard action, +4 dodge AC from the
    /// moment it is taken until the creature's next turn starts, no attacks of opportunity meanwhile; the PC's Total
    /// Defense button and the AI's GameManager.RunAIAction run the same executor; a cornered panicked creature, PC or
    /// NPC, cowers and takes it (PHB p.311; delegated RAW reading: the action, not the Cowering condition).
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
    }
}
#endif
