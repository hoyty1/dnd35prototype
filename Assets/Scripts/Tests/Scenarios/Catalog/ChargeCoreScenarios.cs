#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 12 step 21 (docs/designs/pc_npc_parity_batch12.md; CMB-018, CMB-108, CMB-013; decision D2): one charge core
    /// for PCs and NPCs (GameManager.ExecuteCharge(ChargeRequest, ChargeOutcome), Combat/Maneuvers/ChargeCore.cs). A
    /// creature limited to one action makes a partial charge, a standard action moving up to its speed (PHB p.154, p.280;
    /// D2): a disabled PC through the Charge button, a slowed AI orc on its own. Spirited Charge multiplies the weapon damage
    /// roll, so damage reduction applies once (PHB p.100, p.134; CMB-013). An AI charge that ends opposite an ally flanks
    /// (PHB p.153), as the PC's does (rules/charge-flanking-ui).
    /// </summary>
    public static class ChargeCoreScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("charge-partial-disabled", ChargePartialDisabled);
            yield return S("charge-partial-slowed-ai", ChargePartialSlowedAi);
            yield return S("charge-partial-bullrush", ChargePartialBullRush);
            yield return S("spirited-charge-dr", () => SpiritedChargeDr(false));
            yield return S("spirited-charge-crit", () => SpiritedChargeDr(true));
            yield return S("charge-flanking-ai", ChargeFlankingAi);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("ChargeCoreScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title)
            => Scenario.Define(id, title).Tags("rules", "charge").Covers("PC_NPC_PARITY", "batch 12 step 21", "PHB p.154");

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

        /// <summary>Squares <paramref name="key"/> moved in <paramref name="round"/> (every move event's "sq").</summary>
        private static int SquaresMoved(TraceView v, string key, int round) => v.Moves(key, round).Sum(e => e.Int("sq"));

        /// <summary>The Slow spell's effect as its apply site sets it (GameManager_Spells_S: the penalties and half speed, then ApplySlowEffect).</summary>
        private static void Slow(CharacterController c)
        {
            c.Stats.SlowAttackPenalty = -1;
            c.Stats.SlowACPenalty = -1;
            c.Stats.SlowReflexPenalty = -1;
            c.Stats.SlowSpeedMultiplier = 0.5f;
            c.ApplySlowEffect(10, null);
        }

        private static Vector2Int? Pos(JsonObj snap) => snap != null && snap.Get("pos") is Vector2Int p ? p : (Vector2Int?)null;

        /// <summary>The HP <paramref name="key"/> lost in <paramref name="round"/> (the 'hp' events).</summary>
        private static int HpLost(TraceView v, string key, int round)
            => v.Of("hp").Where(e => e.Round == round && e.Str("actor") == key).Sum(e => e.Int("old") - e.Int("new"));

        // ── D2: the partial charge ─────────────────────────────────────

        /// <summary>
        /// CMB-108, D2 (PHB p.154, p.145): a disabled Ui fighter 4 (0 hp: one move or standard action) charges a sturdy dummy
        /// four squares away (a 3-square path) with the Charge button. It is a partial charge: a standard action, so the
        /// charge path may be at most the speed (6 squares), and a second dummy seven squares away, in reach of a full
        /// charge (12) but not of a partial one, cannot be charged. The attack carries the charge's +2 (every d20 a 19:
        /// a hit), and the act is strenuous, so once it is complete the hero is at -1 hp and dying (PHB p.145). An idle
        /// ally far off keeps the party in the fight, so the hero's turn end is traced.
        /// </summary>
        private static ScenarioDef ChargePartialDisabled()
        {
            return Rules("rules/charge-partial-disabled", "A disabled PC makes a partial charge: a standard action, up to its speed, then the strenuous 1 hp (PHB p.154, p.145; CMB-108, D2)")
                .Covers("CMB-108", "D2", "PHB p.145")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 14, 10, Control.Idle)
                .Npc("far", "target_dummy", 10, 18, Control.Idle)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Ally", 1)), 2, 2, Control.Idle)
                .Hp("hero", 0)
                .Tweak("hero", RulesScenarios.StripOffHand)
                .Tweak("dummy", SturdyNoAoO)
                .Tweak("far", SturdyNoAoO)
                .Force(20, 19, null, -1)
                .Initiative("hero", "dummy", "far", "ally")
                .Turn("hero", 1,
                    Step.Assert("the disabled hero is limited to one action and may only make a partial charge", ctx =>
                        ctx.Get("hero").Actions.SingleActionOnly && !ctx.Get("hero").Actions.HasFullRoundAction
                        && ChargeRules.HasChargeAction(ctx.Get("hero"), out bool partial, out _) && partial),
                    Step.Assert("the near dummy can be charged, the far one (7 squares, beyond the speed) cannot", ctx =>
                        ctx.Gm.CanChargeTargetForAI(ctx.Get("hero"), ctx.Get("dummy"))
                        && !ctx.Gm.CanChargeTargetForAI(ctx.Get("hero"), ctx.Get("far"))),
                    Step.Charge("dummy"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The Charge step is done", Expect.StepStatus("hero", 1, "Charge", 0, "done"))
                .Expect("It is logged as a partial charge", v =>
                    v.Log("Hero makes a partial charge at").Count == 1 ? ExpectResult.Pass("logged") : ExpectResult.Fail("no partial-charge line"))
                .Expect("The hero moves 3 squares and makes one charge attack with the +2 (BAB 4, Str 3, charge 2: at least +9)", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("hero", "dummy", false, 1);
                    int moved = SquaresMoved(v, "hero", 1);
                    if (attacks.Count != 1)
                        return ExpectResult.Fail(attacks.Count + " attack(s), moved " + moved);
                    return moved == 3 && attacks[0].Int("mod") >= 9
                        ? ExpectResult.Pass("moved 3, mod " + attacks[0].Int("mod"), attacks[0].Seq)
                        : ExpectResult.Fail("moved " + moved + ", mod " + attacks[0].Int("mod"), attacks[0].Seq);
                })
                // The single-action limit itself is asserted before the charge: by the turn end the 1 hp has left the hero
                // dying, which lifts the limit (RefreshActionLimits).
                .Expect("The partial charge spent the standard action, not a full-round action", v =>
                {
                    TraceEvent te = v.Of("turn_end").FirstOrDefault(e => e.Str("actor") == "hero");
                    if (te == null || !(te.Get("econ") is JsonObj econ))
                        return ExpectResult.Fail("no turn end");
                    bool std = econ.Get("std") is bool su && su;
                    bool full = econ.Get("full") is bool fu && fu;
                    return std && !full ? ExpectResult.Pass("standard spent, no full-round") : ExpectResult.Fail("std " + std + ", full " + full);
                })
                .Expect("The strenuous act drops it to -1 hp once it is complete (PHB p.145)", v =>
                    v.Log(@"Hero takes 1 damage for acting while disabled \(0 → -1 HP\)").Count == 1
                        ? ExpectResult.Pass("logged once")
                        : ExpectResult.Fail(v.Log(@"Hero takes 1 damage for acting while disabled").Count + " log line(s)"))
                .Expect("The hero ends dying, out of the fight (-1 hp after the act, then the dying loss)", v =>
                {
                    JsonObj hero = v.Final("hero");
                    int hp = hero != null && hero.Get("hp") is int h ? h : int.MinValue;
                    return hp <= -1 && hp > -10 && ScenarioChecks.IsDownSnapshot(hero) ? ExpectResult.Pass("hp " + hp + ", down") : ExpectResult.Fail("hp " + hp);
                })
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        /// <summary>
        /// CMB-108, D2 (PHB p.280, p.154): an AI orc warrior slowed by the tweak (<see cref="Slow"/>, what the Slow spell's
        /// apply site sets) is limited to one action and has half its speed (3 squares). Four squares from an idle hero
        /// it charges on its own: a partial charge (the AI's charge choice, AIService.ShouldNPCCharge, reads the charge
        /// core's legality), a 3-square move and one attack with the charge's +2 (BAB 1, Str 3, charge 2, Slow -1: at least +5;
        /// every d20 a 19).
        /// </summary>
        private static ScenarioDef ChargePartialSlowedAi()
        {
            return Rules("rules/charge-partial-slowed-ai", "A slowed AI creature makes a partial charge: a standard action, up to its halved speed (PHB p.280, p.154; CMB-108, D2)")
                .Covers("CMB-108", "D2", "PHB p.280")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 14, 10, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Idle)
                .Tweak("orc", c => { Sturdy(c); Slow(c); })
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); SturdyNoAoO(c); })
                .Force(20, 19, null, -1)
                .Initiative("orc", "hero")
                .Expect("The orc's turn is an AI turn", Expect.Controller("orc", "ai"))
                .Expect("The slowed orc charges: a partial charge (log)", v =>
                    v.Log("Orc Warrior makes a partial charge at Hero").Count == 1
                        ? ExpectResult.Pass("logged")
                        : ExpectResult.Fail("no partial-charge line"))
                .Expect("It moves at most its halved speed (3 squares) and makes one attack with the charge's +2 and the Slow -1 (at least +5)", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("orc", "hero", false, 1);
                    int moved = SquaresMoved(v, "orc", 1);
                    if (attacks.Count != 1)
                        return ExpectResult.Fail(attacks.Count + " attack(s), moved " + moved);
                    return moved >= 2 && moved <= 3 && attacks[0].Int("mod") >= 5
                        ? ExpectResult.Pass("moved " + moved + ", mod " + attacks[0].Int("mod"), attacks[0].Seq)
                        : ExpectResult.Fail("moved " + moved + ", mod " + attacks[0].Int("mod"), attacks[0].Seq);
                })
                .Expect("The turn ends under the single-action limit with the standard action spent", v =>
                {
                    TraceEvent te = v.Of("turn_end").FirstOrDefault(e => e.Str("actor") == "orc");
                    if (te == null || !(te.Get("econ") is JsonObj econ))
                        return ExpectResult.Fail("no turn end");
                    bool single = econ.Get("single") is bool s && s;
                    bool std = econ.Get("std") is bool su && su;
                    bool full = econ.Get("full") is bool fu && fu;
                    return single && std && !full ? ExpectResult.Pass("single, standard spent, no full-round") : ExpectResult.Fail("single " + single + ", std " + std + ", full " + full);
                })
                .Build();
        }

        /// <summary>
        /// D2 (PHB p.154-155): a slowed Scripted fighter 6 (the NPC turn path; speed 6 squares halved to 3, one action) makes
        /// a bull rush on a partial charge against a sturdy orc three squares east: a 2-square charge (the path picked ends
        /// diagonally next to the orc, 5 + 5 ft). The charge's movement budget is the speed, so the follow limit is 3 - 2 = 1
        /// square (BullRushRules.GetMovementLimitSquares): with a forced margin of at least 15 the orc is pushed one square
        /// (5 ft and stay, or one square followed) and the fighter ends at most 3 squares from its start. Before the fix the
        /// follow limit was twice the speed less the charge (4 squares), so the orc was pushed two squares or more.
        /// </summary>
        private static ScenarioDef ChargePartialBullRush()
        {
            return Rules("rules/charge-partial-bullrush", "A bull rush on a partial charge follows the push only up to the speed less the charge distance (PHB p.154-155, D2)")
                .Covers("D2", "PHB p.155", "CMB-108")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => RulesScenarios.Fighter("Fighter", 6)), 7, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 10, 10, Control.Idle)
                // Unarmored: its 30-ft speed halves to 3 squares (the kit's scale mail would make it 20 ft, PHB p.123; ITM-010).
                .Tweak("fighter", c => { RulesScenarios.StripArmor(c); Sturdy(c); Slow(c); })
                .Tweak("orc", SturdyNoAoO)
                .Initiative("fighter", "orc")
                .Force(20, 20, "Bull rush check")
                .Force(20, 1, "Bull rush defense")
                .Turn("fighter", 1,
                    Step.Assert("the slowed fighter is limited to one action with a 3-square speed", ctx =>
                        ctx.Get("fighter").Actions.SingleActionOnly && ctx.Gm.GetCurrentMoveRangeSquares(ctx.Get("fighter")) == 3),
                    Step.Charge("orc", bullRush: true))
                .Expect("The Charge step is done", Expect.StepStatus("fighter", 1, "Charge", 0, "done"))
                .Expect("It is logged as a partial charge", v =>
                    v.Log("Fighter makes a partial charge and attempts a bull rush on").Count == 1 ? ExpectResult.Pass("logged") : ExpectResult.Fail("no partial-charge line"))
                .Expect("The bull rush succeeds, and the push and follow stop at the partial charge's budget: the orc pushed one square, the fighter at most 3 squares from its start", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.BullRushCharge).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no bull rush");
                    if (!m.Bool("success")) return ExpectResult.Fail("bull rush failed with forced dice", m.Seq);
                    Vector2Int? o = Pos(v.Final("orc")), f = Pos(v.Final("fighter"));
                    if (o == null || f == null) return ExpectResult.Fail("no final positions");
                    int orcPushed = Math.Max(Math.Abs(o.Value.x - 10), Math.Abs(o.Value.y - 10));
                    int fighterMoved = Math.Max(Math.Abs(f.Value.x - 7), Math.Abs(f.Value.y - 10));
                    return orcPushed == 1 && fighterMoved <= 3
                        ? ExpectResult.Pass("orc at " + o + ", fighter at " + f, m.Seq)
                        : ExpectResult.Fail("orc at " + o + ", fighter at " + f, m.Seq);
                })
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── CMB-013: Spirited Charge in the weapon damage roll ─────────

        /// <summary>
        /// CMB-013, CMB-018 (PHB p.100, p.134): a Ui fighter 4 with Mounted Combat, Ride-By Attack and Spirited Charge, mounted
        /// on a heavy warhorse by the tweak (MountSystem.TryMount; mounted combat is otherwise unreachable, CMB-031), charges a
        /// sturdy dummy with damage reduction 5/adamantine. Every d20 is 15: a hit and no threat. Spirited Charge doubles the
        /// weapon damage, so the weapon damage is two rolls of the weapon's dice and static modifier (dmgRolls 2), and the
        /// dummy's damage reduction comes off once: it loses the weapon damage less 5. Before the charge core the doubled part
        /// was dealt after mitigation as a second, unmitigated hit. With <paramref name="crit"/> (rules/spirited-charge-crit)
        /// every d20 is 20: a confirmed critical with the x2 longsword, and the multipliers add (PHB p.134: x2 and x2 are x3),
        /// so the weapon damage is three rolls, and damage reduction still comes off once.
        /// </summary>
        private static ScenarioDef SpiritedChargeDr(bool crit)
        {
            int rolls = crit ? 3 : 2;
            return Rules(crit ? "rules/spirited-charge-crit" : "rules/spirited-charge-dr", crit
                    ? "Spirited Charge and a x2 critical add to three weapon damage rolls, and damage reduction applies once (PHB p.100, p.134; CMB-013)"
                    : "Spirited Charge doubles the weapon damage roll, and damage reduction applies once (PHB p.100, p.134; CMB-013)")
                .Covers("CMB-013", "CMB-018", "PHB p.100", "PHB p.134")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Rider", 4, "Mounted Combat", "Ride-By Attack", "Spirited Charge")), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 14, 10, Control.Idle)
                .Tweak("hero", c =>
                {
                    RulesScenarios.StripOffHand(c);
                    MountSystem.TryMount(c, MountSystem.CreateMount(MountType.HeavyWarhorse));
                })
                .Tweak("dummy", c => { SturdyNoAoO(c); c.Stats.AddDamageReduction(5, DamageBypassTag.Adamantine); })
                .Force(20, crit ? 20 : 15, null, -1)
                .Initiative("hero", "dummy")
                .Turn("hero", 1,
                    Step.Assert("the hero is mounted", ctx => MountSystem.IsMounted(ctx.Get("hero"))),
                    Step.Assert("the hero's weapon is a x2 critical weapon", ctx =>
                        ctx.Get("hero").GetEquippedMainWeapon() != null && ctx.Get("hero").GetEquippedMainWeapon().CritMultiplier == 2),
                    Step.Charge("dummy"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The Charge step is done", Expect.StepStatus("hero", 1, "Charge", 0, "done"))
                .Expect(crit
                    ? "The charge attack is a confirmed critical with three weapon damage rolls (x2 critical + x2 Spirited Charge = x3, PHB p.134)"
                    : "The charge attack hits with two weapon damage rolls and no critical (Spirited Charge x2, PHB p.100)", v =>
                {
                    TraceEvent atk = v.Attacks("hero", "dummy", false, 1).FirstOrDefault();
                    if (atk == null)
                        return ExpectResult.Fail("no charge attack");
                    bool ok = atk.Bool("hit") && atk.Bool("confirmed") == crit && atk.Int("dmgRolls") == rolls
                        && atk.Int("weaponDmg") == Math.Max(1, atk.Int("baseRoll") + rolls * atk.Int("dmgMod"));
                    return ok
                        ? ExpectResult.Pass("rolls " + rolls + ", dice " + atk.Int("baseRoll") + ", mod " + atk.Int("dmgMod") + ", weapon damage " + atk.Int("weaponDmg"), atk.Seq)
                        : ExpectResult.Fail("hit " + atk.Bool("hit") + ", confirmed " + atk.Bool("confirmed") + ", rolls " + atk.Int("dmgRolls")
                            + ", dice " + atk.Int("baseRoll") + ", mod " + atk.Int("dmgMod") + ", weapon damage " + atk.Int("weaponDmg"), atk.Seq);
                })
                .Expect("Damage reduction 5 comes off the multiplied damage once", v =>
                {
                    TraceEvent atk = v.Attacks("hero", "dummy", false, 1).FirstOrDefault();
                    if (atk == null)
                        return ExpectResult.Fail("no charge attack");
                    int expected = Math.Max(0, atk.Int("weaponDmg") + atk.Int("sneakDmg") - 5);
                    int lost = HpLost(v, "dummy", 1);
                    return lost == expected
                        ? ExpectResult.Pass("lost " + lost + " = " + atk.Int("weaponDmg") + " - 5")
                        : ExpectResult.Fail("lost " + lost + ", expected " + expected);
                })
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── CMB-018: the AI's charge flanks ────────────────────────────

        /// <summary>
        /// CMB-018 (PHB p.153-154): the AI side of rules/charge-flanking-ui, the same squares. An AI orc warrior charges an
        /// idle hero from three squares away and ends opposite an idle orc ally that threatens the hero: the charge attack
        /// gets the flanking +2 as well as the charge's +2 (BAB 1, Str 3, charge 2, flanking 2: at least +8). Both sides'
        /// charges compute flanking from the square where the charge ends (the charge core's attack sequence).
        /// </summary>
        private static ScenarioDef ChargeFlankingAi()
        {
            return Rules("rules/charge-flanking-ai", "An AI charge that ends opposite an ally gets the flanking +2 on top of the charge's +2 (PHB p.153-154, CMB-018)")
                .Covers("CMB-018", "PHB p.153")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 10, 10, Control.Ai)
                .Npc("ally", "orc_warrior", 14, 10, Control.Idle)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 13, 10, Control.Idle)
                .Tweak("orc", Sturdy)
                .Tweak("ally", Sturdy)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); SturdyNoAoO(c); })
                .Initiative("orc", "ally", "hero")
                .Expect("The orc's turn is an AI turn", Expect.Controller("orc", "ai"))
                .Expect("The orc charges the hero (log)", v =>
                    v.Log("Orc Warrior charges Hero!").Count >= 1 ? ExpectResult.Pass("logged") : ExpectResult.Fail("no charge line"))
                .Expect("The charge attack flanks the hero with the ally: +2 flanking (PHB p.153)", v =>
                {
                    TraceEvent atk = v.Attacks("orc", "hero", false, 1).FirstOrDefault();
                    if (atk == null)
                        return ExpectResult.Fail("no charge attack");
                    return atk.Bool("flank") && atk.Int("flankBonus") == 2 && atk.Int("mod") >= 8
                        ? ExpectResult.Pass("flank true, flankBonus 2, mod " + atk.Int("mod"), atk.Seq)
                        : ExpectResult.Fail("flank " + atk.Bool("flank") + ", flankBonus " + atk.Int("flankBonus") + ", mod " + atk.Int("mod"), atk.Seq);
                })
                .Build();
        }
    }
}
#endif
