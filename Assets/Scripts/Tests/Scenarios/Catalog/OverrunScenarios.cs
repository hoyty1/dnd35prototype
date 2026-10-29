#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 12 step 20 (docs/designs/pc_npc_parity_batch12.md; CMB-015, CMB-086, CMB-116, CMB-151; decision D11): one
    /// overrun for PCs and NPCs, a move through the defender's space (GameManager.RunOverrunMove, PHB p.157-158). The AI
    /// plans it (GameManager.TryPlanOverrunPathForAI); the defender's decider chooses to avoid or block (the UI prompt for
    /// a controllable defender, IManeuverDecider.ChooseOverrunResponse); Improved Overrun forbids avoiding and adds +4 but
    /// does not stop the defender's AoO (PHB p.96); a winner knocks the defender prone and moves on, the defender staying in
    /// its square, a loser stays where it came from; an avoided overrun costs no action; a single-action creature cannot
    /// overrun; a helpless opponent's square is passed with no attempt (PHB p.147).
    /// </summary>
    public static class OverrunScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("overrun-ai-through", OverrunAiThrough);
            yield return S("overrun-pc-defender-chooses-ui", () => OverrunPcDefenderUi(block: false));
            yield return S("overrun-pc-defender-blocks-ui", () => OverrunPcDefenderUi(block: true));
            yield return S("overrun-improved-no-avoid", OverrunImprovedNoAvoid);
            yield return S("overrun-defender-aoo-improved", OverrunDefenderAoOImproved);
            yield return S("overrun-blocked-loses", OverrunBlockedLoses);
            yield return S("overrun-single-action-refused", OverrunSingleActionRefused);
            yield return S("overrun-helpless-pass", OverrunHelplessPass);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("OverrunScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title)
            => Scenario.Define(id, title).Tags("rules", "maneuver", "overrun").Covers("PC_NPC_PARITY", "batch 12 step 20", "D11", "PHB p.157");

        private const string AttackRoll = "Overrun attack roll";
        private const string DefenseRoll = "Overrun defense roll";

        private static Vector2Int? Pos(JsonObj snap) => snap != null && snap.Get("pos") is Vector2Int p ? p : (Vector2Int?)null;

        private static bool HasCond(JsonObj snap, string cond) => snap != null && snap.Get("conds") is List<string> l && l.Contains(cond);

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(200);
            c.Stats.CurrentHP += 200;
        }

        private static void SturdyNoAoO(CharacterController c)
        {
            Sturdy(c);
            c.Stats.CanMakeAttacksOfOpportunity = false;
        }

        private static IEnumerable<TraceEvent> ResponseNotes(TraceView v, string key)
            => v.Of("note").Where(e => (e.Str("text") ?? "").StartsWith("overrun-response prompt for " + key, StringComparison.Ordinal));

        private static Func<TraceView, ExpectResult> EndsAt(string key, int x, int y)
            => v =>
            {
                Vector2Int? p = Pos(v.Final(key));
                return p == new Vector2Int(x, y) ? ExpectResult.Pass(key + " at " + p) : ExpectResult.Fail(key + " at " + p + " (expected (" + x + "," + y + "))");
            };

        // ── The AI plans a move through the defender's space ───────────

        /// <summary>
        /// CMB-015, D11 (PHB p.157): a Scripted orc at (8,10) overruns a Scripted fighter at (9,10) with the AI's plan
        /// (Step.Overrun: TryPlanOverrunPathForAI picks the square straight past the fighter, (10,10)). The fighter's AI decider
        /// blocks; the checks are forced (20 against 1), so the orc wins: the fighter is knocked prone and stays in its square
        /// (no push, CMB-116), the orc moves on to (10,10), and the block took its standard action.
        /// </summary>
        private static ScenarioDef OverrunAiThrough()
        {
            return Rules("rules/overrun-ai-through", "An AI overrun is a move through the defender's space: prone defender, the attacker moves on (PHB p.157; CMB-015)")
                .Covers("CMB-015", "CMB-116")
                .MaxRounds(1)
                .Npc("orc", "orc_berserker", 8, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6)), 9, 10, Control.Scripted)
                .Tweak("orc", Sturdy)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); SturdyNoAoO(c); })
                .Initiative("orc", "hero")
                .Force(20, 20, AttackRoll)
                .Force(20, 1, DefenseRoll)
                .Turn("orc", 1, Step.Overrun("hero"))
                .Turn("hero", 1, Step.Pass())
                .Expect("The overrun step is done", Expect.StepStatus("orc", 1, "Overrun", 0, "done"))
                .Expect("The fighter blocks and the orc wins the opposed check, spending its standard action", v =>
                {
                    TraceEvent m = v.Maneuvers("orc", SpecialAttackType.Overrun).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no overrun check");
                    return m.Bool("success") && m.Bool("consumed") && m.Str("target") == "hero"
                        ? ExpectResult.Pass("won " + m.Int("check") + " vs " + m.Int("opposed"), m.Seq)
                        : ExpectResult.Fail("success " + m.Bool("success") + ", consumed " + m.Bool("consumed"), m.Seq);
                })
                .Expect("The orc moved through to (10,10)", EndsAt("orc", 10, 10))
                .Expect("The fighter stays in its square, prone (no push; PHB p.157, CMB-116)", v =>
                {
                    JsonObj h = v.Final("hero");
                    return Pos(h) == new Vector2Int(9, 10) && HasCond(h, "Prone")
                        ? ExpectResult.Pass("prone at (9,10)")
                        : ExpectResult.Fail("hero at " + Pos(h) + ", prone " + HasCond(h, "Prone"));
                })
                .Build();
        }

        // ── D11: the defender's decider chooses ────────────────────────

        /// <summary>
        /// D11 (PHB p.157 step 2): a Scripted orc at (8,10) overruns a Ui fighter at (9,10). The fighter's decider is the
        /// prompt, which the runner answers. Avoid (<paramref name="block"/> false): no check is rolled, the fighter is not
        /// prone, the orc passes to (10,10) and, the attempt costing no action, still attacks with its standard action.
        /// Block: the opposed check is rolled (forced 20 against 1) and the fighter is knocked prone; the standard action is
        /// spent, so the attack is refused.
        /// </summary>
        private static ScenarioDef OverrunPcDefenderUi(bool block)
        {
            string id = block ? "rules/overrun-pc-defender-blocks-ui" : "rules/overrun-pc-defender-chooses-ui";
            string title = block
                ? "A controllable defender blocks an NPC's overrun: the check is rolled and costs the standard action (PHB p.157; D11)"
                : "A controllable defender avoids an NPC's overrun: no check, and the attempt costs no action (PHB p.157; D11)";
            ScenarioBuilder b = Rules(id, title)
                .Covers("CMB-015", "CMB-098")
                .MaxRounds(1)
                .Npc("orc", "orc_berserker", 8, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6)), 9, 10, Control.Ui)
                .Tweak("orc", Sturdy)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); SturdyNoAoO(c); })
                .Initiative("orc", "hero")
                .OverrunResponseAnswer("hero", block)
                .Force(20, 20, AttackRoll)
                .Force(20, 1, DefenseRoll)
                .Turn("orc", 1, Step.Overrun("hero"), Step.Attack("hero"))
                .Turn("hero", 1, Step.Pass())
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The overrun step is done", Expect.StepStatus("orc", 1, "Overrun", 0, "done"))
                .Expect("The hero's overrun prompt was answered " + (block ? "Block" : "Avoid"), v =>
                {
                    List<TraceEvent> notes = ResponseNotes(v, "hero").ToList();
                    if (notes.Count != 1) return ExpectResult.Fail(notes.Count + " overrun prompts for the hero", notes.Select(e => e.Seq).ToArray());
                    string expected = block ? "answered Block" : "answered Avoid";
                    return notes[0].Str("text").Contains(expected) ? ExpectResult.Pass(notes[0].Str("text"), notes[0].Seq) : ExpectResult.Fail(notes[0].Str("text"), notes[0].Seq);
                })
                .Expect("The orc moved through to (10,10)", EndsAt("orc", 10, 10));
            if (block)
                b = b.Expect("Blocked: the check is rolled and the hero is prone", Expect.All(
                        Expect.Count("maneuver", e => e.Str("by") == "orc" && e.Get("type") is SpecialAttackType t && t == SpecialAttackType.Overrun && e.Bool("success"), 1, 1),
                        v => HasCond(v.Final("hero"), "Prone") ? ExpectResult.Pass("prone") : ExpectResult.Fail("not prone")))
                    .Expect("The block took the standard action: the attack is refused", Expect.All(
                        Expect.StepStatus("orc", 1, "Attack", 0, "refused"),
                        Expect.None("attack", e => e.Str("attacker") == "orc" && !e.Bool("aoo"))));
            else
                b = b.Expect("Avoided: no check is rolled and the hero is not prone", Expect.All(
                        Expect.None("maneuver", e => e.Str("by") == "orc"),
                        v => !HasCond(v.Final("hero"), "Prone") ? ExpectResult.Pass("standing") : ExpectResult.Fail("prone")))
                    .Expect("The avoided overrun cost no action: the orc still attacks", Expect.All(
                        Expect.StepStatus("orc", 1, "Attack", 0, "done"),
                        Expect.Count("attack", e => e.Str("attacker") == "orc" && !e.Bool("aoo"), 1, int.MaxValue)));
            return b.Build();
        }

        // ── PHB p.96: Improved Overrun ──────────────────────────────────

        /// <summary>
        /// PHB p.96 (Improved Overrun): a Scripted orc with Improved Overrun (STR set to 16, +3) overruns a Ui fighter whose
        /// scripted answer would be Avoid. The target may not avoid: no prompt opens, the opposed check is rolled, and the
        /// orc's check modifier is +7 (Strength +3, Improved Overrun +4).
        /// </summary>
        private static ScenarioDef OverrunImprovedNoAvoid()
        {
            return Rules("rules/overrun-improved-no-avoid", "Improved Overrun: the defender may not avoid, and the check gains +4 (PHB p.96; D11)")
                .Covers("CMB-015", "PHB p.96")
                .MaxRounds(1)
                .Npc("orc", "orc_berserker", 8, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6)), 9, 10, Control.Ui)
                .Tweak("orc", c => { Sturdy(c); c.Stats.STR = 16; if (!c.Stats.Feats.Contains("Improved Overrun")) c.Stats.Feats.Add("Improved Overrun"); })
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); SturdyNoAoO(c); })
                .Initiative("orc", "hero")
                .OverrunResponseAnswer("hero", false)
                .Force(20, 10, AttackRoll)
                .Force(20, 10, DefenseRoll)
                .Turn("orc", 1,
                    Step.Assert("the orc's overrun check modifier is +7 (STR +3, Improved Overrun +4)", ctx => ctx.Get("orc").GetOverrunAttackerCheckModifier() == 7),
                    Step.Overrun("hero"))
                .Turn("hero", 1, Step.Pass())
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The modifier assert holds", Expect.AssertsPass())
                .Expect("No avoid-or-block prompt opens for the hero", v =>
                {
                    List<TraceEvent> notes = ResponseNotes(v, "hero").ToList();
                    return notes.Count == 0 ? ExpectResult.Pass("no prompt") : ExpectResult.Fail(notes.Count + " prompts", notes.Select(e => e.Seq).ToArray());
                })
                .Expect("The check is rolled with the +4: check minus roll is +7", v =>
                {
                    TraceEvent m = v.Maneuvers("orc", SpecialAttackType.Overrun).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no overrun check (the hero avoided?)");
                    int mod = m.Int("check") - m.Int("checkRoll");
                    return mod == 7 ? ExpectResult.Pass("modifier +7", m.Seq) : ExpectResult.Fail("modifier " + mod, m.Seq);
                })
                .Expect("The log says the hero cannot avoid", Expect.Count("log", e => (e.Str("text") ?? "").Contains("cannot avoid"), 1, int.MaxValue))
                .Build();
        }

        /// <summary>
        /// CMB-086 (PHB p.157 step 1, p.96), D11 reading (1): a Ui fighter with Improved Overrun at (8,10) overruns an orc at
        /// (10,10) through the Special Attack menu, to (11,10) (the "Overrun or Normal Move" dialog is answered Overrun); the
        /// orc acted first, so it is not flat-footed, and has Combat Reflexes (DEX 14: three AoOs a round). Leaving (9,10)
        /// provokes the orc's movement AoO (CMB-151); moving into its space then provokes its overrun AoO, Improved Overrun or
        /// not (trigger "maneuver"), a separate provocation that a defender with an AoO left may also take; leaving the orc's
        /// own space provokes no third (one movement opportunity a round, PHB p.138). The check is forced (20 against 1) and
        /// the fighter moves on.
        /// </summary>
        private static ScenarioDef OverrunDefenderAoOImproved()
        {
            return Rules("rules/overrun-defender-aoo-improved", "Improved Overrun keeps the defender's AoO for entering its space (PHB p.157, p.96; CMB-086)")
                .Covers("CMB-086", "CMB-151", "PHB p.96", "PHB p.138")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6, "Power Attack", "Improved Overrun")), 8, 10, Control.Ui)
                .Npc("orc", "orc_berserker", 10, 10, Control.Scripted)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); SturdyNoAoO(c); })
                .Tweak("orc", c => { Sturdy(c); c.Stats.DEX = 14; if (!c.Stats.Feats.Contains("Combat Reflexes")) c.Stats.Feats.Add("Combat Reflexes"); })
                // The orc acts first, so it is no longer flat-footed and can make the AoO (PHB p.137).
                .Initiative("orc", "hero")
                .Force(20, 20, AttackRoll)
                .Force(20, 1, DefenseRoll)
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1, Step.OverrunTo(11, 10))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The overrun step is done", Expect.StepStatus("hero", 1, "Overrun", 0, "done"))
                .Expect("The orc makes two AoOs: the movement one for leaving (9,10), then the overrun's (trigger maneuver)", v =>
                {
                    List<TraceEvent> aoos = v.AoOs("orc", "hero");
                    if (aoos.Count != 2) return ExpectResult.Fail(aoos.Count + " AoOs by the orc", aoos.Select(e => e.Seq).ToArray());
                    return aoos[0].Str("trigger") == "movement" && aoos[1].Str("trigger") == "maneuver"
                        ? ExpectResult.Pass("movement, then maneuver", aoos[0].Seq, aoos[1].Seq)
                        : ExpectResult.Fail("triggers " + aoos[0].Str("trigger") + ", " + aoos[1].Str("trigger"), aoos[0].Seq, aoos[1].Seq);
                })
                .Expect("Both AoOs come before the overrun check", v =>
                {
                    TraceEvent a = v.AoOs("orc", "hero").LastOrDefault();
                    TraceEvent m = v.Maneuvers("hero", SpecialAttackType.Overrun).FirstOrDefault();
                    if (a == null || m == null) return ExpectResult.Fail("AoO " + (a != null) + ", check " + (m != null));
                    return v.Before(a.Seq, m.Seq) ? ExpectResult.Pass("AoOs first", a.Seq, m.Seq) : ExpectResult.Fail("check first", a.Seq, m.Seq);
                })
                .Expect("The hero wins, the orc is prone in its square and the hero ends at (11,10)", Expect.All(
                    EndsAt("hero", 11, 10),
                    v =>
                    {
                        JsonObj o = v.Final("orc");
                        return Pos(o) == new Vector2Int(10, 10) && HasCond(o, "Prone") ? ExpectResult.Pass("orc prone at (10,10)") : ExpectResult.Fail("orc at " + Pos(o) + ", prone " + HasCond(o, "Prone"));
                    }))
                .Build();
        }

        // ── PHB p.157 step 4: a lost block ─────────────────────────────

        /// <summary>
        /// PHB p.157 steps 3-4: a Scripted orc at (8,10) overruns a Scripted fighter at (9,10) (the AI plan); the fighter
        /// blocks and the checks are forced the other way (1 against 20), so the orc loses: its movement ends in the square
        /// it came from, (8,10), the fighter is not prone, and the block spent the standard action, so the orc's attack is
        /// refused. (The defender's knock-prone counter is not rolled, CMB-117.)
        /// </summary>
        private static ScenarioDef OverrunBlockedLoses()
        {
            return Rules("rules/overrun-blocked-loses", "A lost overrun block ends the attacker's movement where it came from and spends its standard action (PHB p.157)")
                .Covers("CMB-015", "CMB-117")
                .MaxRounds(1)
                .Npc("orc", "orc_berserker", 8, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6)), 9, 10, Control.Scripted)
                .Tweak("orc", Sturdy)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); SturdyNoAoO(c); })
                .Initiative("orc", "hero")
                .Force(20, 1, AttackRoll)
                .Force(20, 20, DefenseRoll)
                .Turn("orc", 1, Step.Overrun("hero"), Step.Attack("hero"))
                .Turn("hero", 1, Step.Pass())
                .Expect("The overrun step is done", Expect.StepStatus("orc", 1, "Overrun", 0, "done"))
                .Expect("The check is rolled and lost", v =>
                {
                    TraceEvent m = v.Maneuvers("orc", SpecialAttackType.Overrun).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no overrun check");
                    return !m.Bool("success") && m.Str("target") == "hero"
                        ? ExpectResult.Pass("lost " + m.Int("check") + " vs " + m.Int("opposed"), m.Seq)
                        : ExpectResult.Fail("success " + m.Bool("success"), m.Seq);
                })
                .Expect("The orc stays in the square it came from, (8,10)", EndsAt("orc", 8, 10))
                .Expect("The fighter is standing in its square", v =>
                {
                    JsonObj h = v.Final("hero");
                    return Pos(h) == new Vector2Int(9, 10) && !HasCond(h, "Prone") ? ExpectResult.Pass("standing at (9,10)") : ExpectResult.Fail("hero at " + Pos(h) + ", prone " + HasCond(h, "Prone"));
                })
                .Expect("The block took the standard action: the attack is refused", Expect.All(
                    Expect.StepStatus("orc", 1, "Attack", 0, "refused"),
                    Expect.None("attack", e => e.Str("attacker") == "orc" && !e.Bool("aoo"))))
                .Build();
        }

        // ── PHB p.145-146, p.280: a single action is not enough ─────────

        /// <summary>
        /// PHB p.157 with p.145-146 and p.280: an overrun is a standard action taken during a move, so a creature limited to a
        /// single action (here an orc at (8,10) with the zombie's IsSingleActionsOnly) cannot overrun: CanUseOverrun refuses
        /// ("Single action only"), the AI plan finds no path, and the orc neither moves nor rolls a check.
        /// </summary>
        private static ScenarioDef OverrunSingleActionRefused()
        {
            return Rules("rules/overrun-single-action-refused", "A creature limited to a single action cannot overrun (PHB p.157, p.145-146, p.280)")
                .Covers("CMB-015", "PHB p.280")
                .MaxRounds(1)
                .Npc("orc", "orc_berserker", 8, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6)), 9, 10, Control.Scripted)
                .Tweak("orc", c => { Sturdy(c); c.Stats.IsSingleActionsOnly = true; })
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); SturdyNoAoO(c); })
                .Initiative("orc", "hero")
                .Turn("orc", 1,
                    Step.Assert("the orc is limited to a single action and CanUseOverrun refuses it", ctx =>
                    {
                        CharacterController orc = ctx.Get("orc");
                        bool can = GameManager.Instance.CanUseOverrun(orc, out string why);
                        return orc.Actions.SingleActionOnly && !can && why == "Single action only";
                    }),
                    Step.Overrun("hero"))
                .Turn("hero", 1, Step.Pass())
                .Expect("The assert holds", Expect.AssertsPass())
                .Expect("The overrun step is refused", Expect.StepStatus("orc", 1, "Overrun", 0, "refused"))
                .Expect("No overrun check is rolled", Expect.None("maneuver", e => e.Str("by") == "orc"))
                .Expect("The orc did not move", EndsAt("orc", 8, 10))
                .Build();
        }

        // ── PHB p.147: a helpless opponent's square ─────────────────────

        /// <summary>
        /// PHB p.147: a square occupied by a helpless opponent may be moved through freely, so it is no overrun attempt. A
        /// Scripted orc at (8,10) overruns to (11,10) through a sleeping fighter at (9,10) and a waking one at (10,10): the
        /// sleeper gets no attempt and no check (and does not use up the round's one attempt); the attempt is against the
        /// waking fighter (forced 20 against 1), who is knocked prone, and the orc ends at (11,10).
        /// </summary>
        private static ScenarioDef OverrunHelplessPass()
        {
            return Rules("rules/overrun-helpless-pass", "A helpless opponent's square is passed with no overrun attempt (PHB p.147)")
                .Covers("CMB-015", "PHB p.147")
                .MaxRounds(1)
                .Npc("orc", "orc_berserker", 8, 10, Control.Scripted)
                .Pc("sleeper", ActorSource.Stats(() => RulesScenarios.Fighter("Sleeper", 6)), 9, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6)), 10, 10, Control.Scripted)
                .Tweak("orc", Sturdy)
                .Tweak("sleeper", c => { RulesScenarios.StripOffHand(c); SturdyNoAoO(c); })
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); SturdyNoAoO(c); })
                .StartCondition("sleeper", CombatConditionType.Asleep, 10)
                .Initiative("orc", "sleeper", "hero")
                .Force(20, 20, AttackRoll)
                .Force(20, 1, DefenseRoll)
                .Turn("orc", 1,
                    Step.Assert("the sleeper is helpless", ctx => ctx.Get("sleeper").IsHelplessForCoupDeGrace()),
                    Step.OverrunTo(11, 10))
                .Turn("hero", 1, Step.Pass())
                .Expect("The assert holds", Expect.AssertsPass())
                .Expect("The overrun step is done", Expect.StepStatus("orc", 1, "Overrun", 0, "done"))
                .Expect("One overrun check, against the waking fighter, won", Expect.All(
                    Expect.Count("maneuver", e => e.Str("by") == "orc", 1, 1),
                    v =>
                    {
                        TraceEvent m = v.Maneuvers("orc", SpecialAttackType.Overrun).FirstOrDefault();
                        return m != null && m.Str("target") == "hero" && m.Bool("success")
                            ? ExpectResult.Pass("won against hero", m.Seq)
                            : ExpectResult.Fail(m == null ? "no check" : "target " + m.Str("target") + ", success " + m.Bool("success"));
                    }))
                .Expect("The sleeper is not prone and the hero is", v =>
                    !HasCond(v.Final("sleeper"), "Prone") && HasCond(v.Final("hero"), "Prone")
                        ? ExpectResult.Pass("sleeper untouched, hero prone")
                        : ExpectResult.Fail("sleeper prone " + HasCond(v.Final("sleeper"), "Prone") + ", hero prone " + HasCond(v.Final("hero"), "Prone")))
                .Expect("The orc ends at (11,10)", EndsAt("orc", 11, 10))
                .Build();
        }
    }
}
#endif
