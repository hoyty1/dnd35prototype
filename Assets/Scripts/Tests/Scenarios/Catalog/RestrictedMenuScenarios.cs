#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 13 step 20 (docs/designs/action_catalog_batch13.md; CMB-100, decisions D14 and D15, inherited owner decision
    /// Q1): the charm link is one legality bit of every attack, maneuver, attack of opportunity and hostile cast, for both
    /// sides (PHB p.209), and a frightened or turned PC with a way to flee chooses its flight from a restricted menu (PHB
    /// p.309, p.159) instead of the forced flight; a panicked PC (rules/panicked-pc-flees) and a cornered frightened one
    /// (rules/frightened-pc-cornered-fights) keep their batch 12 turns.
    /// </summary>
    public static class RestrictedMenuScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("charmed-pc-spares-charmer-ui", CharmedPcSparesCharmerUi);
            yield return S("charmed-npc-spares-charmer-ai", CharmedNpcSparesCharmerAi);
            yield return S("charmed-no-aoo-on-charmer", CharmedNoAoOOnCharmer);
            yield return S("frightened-pc-chooses-flight-ui", FrightenedPcChoosesFlightUi);
            yield return S("turned-pc-flight-menu-ui", TurnedPcFlightMenuUi);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("RestrictedMenuScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        private static Vector2Int? Pos(JsonObj snap) => snap != null && snap.Get("pos") is Vector2Int p ? p : (Vector2Int?)null;

        private static bool HasCond(JsonObj snap, string cond) => snap != null && snap.Get("conds") is List<string> l && l.Contains(cond);

        private static int Dist(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        /// <summary>Charms <paramref name="charmedKey"/> with <paramref name="charmerKey"/> as its charmer (the effect of a failed save against Charm Person).</summary>
        private static bool Charm(ScenarioContext ctx, string charmedKey, string charmerKey)
        {
            CharacterController charmed = ctx.Get(charmedKey), charmer = ctx.Get(charmerKey);
            var data = new CharmedConditionData { Caster = charmer, CasterName = charmer.Stats.CharacterName, RemainingRounds = 10 };
            ctx.Gm.ApplyCondition(charmed, CombatConditionType.Charmed, 10, charmer, data, sourceNameOverride: "Charm Person", sourceCategory: "Harness");
            return CharmRules.IsCharmedBy(charmed, charmer);
        }

        /// <summary>The flee lines of the shared flee controller (the forced flight), which a chosen flight never logs.</summary>
        private const string ForcedFlightLines = "flees in terror|runs from|is turned and cannot flee";

        // ── Charm (D14, PHB p.209) ─────────────────────────────────────

        /// <summary>
        /// The Scripted goblin has charmed the Ui hero (a fixture standing for a failed Will save against its Charm Person).
        /// On the hero's turn the player's Attack on the goblin chooses nothing (the shared attack gate's Charm bit: the
        /// goblin is no highlighted target, so the click is cancelled and nothing is spent), and the hero's Attack on the
        /// other goblin runs. An idle ally keeps the party in the
        /// fight (a hero charmed by the hostile side is out of the party side, CORE-038).
        /// </summary>
        private static ScenarioDef CharmedPcSparesCharmerUi()
        {
            return Rules("rules/charmed-pc-spares-charmer-ui", "A charmed PC makes no attack on its charmer and may attack another foe (PHB p.209; D14, CMB-100)")
                .Covers("CMB-100", "PHB p.209", "D14", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("gob", "goblin", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 10, 10, Control.Ui)
                .Npc("gob2", "goblin", 9, 10, Control.Idle)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Ally", 1)), 2, 2, Control.Idle)
                .Tweak("hero", RulesScenarios.StripOffHand)
                .Tweak("gob", Sturdy)
                .Tweak("gob2", Sturdy)
                .Initiative("gob", "hero", "gob2", "ally")
                .Turn("gob", 1, Step.Assert("the goblin charms the hero (fixture)", ctx => Charm(ctx, "hero", "gob")), Step.Pass())
                .Turn("hero", 1,
                    Step.Attack("gob"),
                    // The Attack button pays the attack action when it opens the sequence (GameManager.StartAttackSequence);
                    // the refused click on the charmer uses no attack step, so the paid attack is still there for gob2.
                    Step.Assert("the click on the charmer used no attack (the paid attack is still open, the move action left)", ctx =>
                    {
                        CharacterController h = ctx.Get("hero");
                        ctx.Note("b13s20 afterCharmerClick steps=" + h.ProgressiveAttackPool.MainHandStepsUsed + " move=" + h.Actions.HasMoveAction);
                        return h.ProgressiveAttackPool.MainHandStepsUsed == 0 && h.Actions.HasMoveAction;
                    }),
                    Step.Attack("gob2"))
                .Expect("The fixture charms the hero, and the click on the charmer uses no attack", Expect.AssertsPass())
                .Expect("The hero's Attack on its charmer chooses no target", Expect.StepStatus("hero", 1, "Attack(gob)", 0, "done", "refused"))
                .Expect("The hero makes no attack on its charmer", Expect.None("attack", e => e.Str("attacker") == "hero" && e.Str("target") == "gob"))
                .Expect("The hero's Attack on the other goblin runs", Expect.All(
                    Expect.StepStatus("hero", 1, "Attack(gob2)", 0, "done"),
                    v => v.Attacks("hero", "gob2").Count > 0 ? ExpectResult.Pass(v.Attacks("hero", "gob2").Count + " attack(s)") : ExpectResult.Fail("no attack on gob2")))
                .Build();
        }

        /// <summary>
        /// The Scripted hero charms two AI orcs (a fixture). A charmed NPC's turn is the shared charm controller's for any
        /// controller (the compulsion pass, batch 12 step 7), so no script can make it attack; the hero's own asserts check
        /// the shared gate an NPC attack meets (AttackLegality.CanDirectlyAttack with the AI's intent: the Charm bar for a
        /// weapon attack, a maneuver and an attack of opportunity), and the orcs' turns spare the hero (the AI's target
        /// choice). An idle second hero stands next to the first orc as another target, and an idle goblin keeps the
        /// hostile side in the fight (a foe charmed by the party is out of the hostile side, CORE-038). Both orcs are
        /// sturdy: a charmed NPC may attack its own ally (AI-042).
        /// </summary>
        private static ScenarioDef CharmedNpcSparesCharmerAi()
        {
            return Rules("rules/charmed-npc-spares-charmer-ai", "Charmed NPCs make no attack on their charmer (PHB p.209; D14, CMB-100)")
                .Covers("CMB-100", "PHB p.209", "D14", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 11, 10, Control.Ai)
                .Npc("orc2", "orc_warrior", 10, 11, Control.Ai)
                .Pc("hero2", ActorSource.Stats(() => RulesScenarios.Fighter("Hero Two", 3)), 12, 10, Control.Idle)
                .Npc("gob", "goblin", 18, 18, Control.Idle)
                .Tweak("hero", Sturdy)
                .Tweak("hero2", Sturdy)
                .Tweak("orc", Sturdy)
                .Tweak("orc2", Sturdy)
                .Initiative("hero", "orc", "orc2", "hero2", "gob")
                .Turn("hero", 1,
                    Step.Assert("the hero charms both orcs (fixture)", ctx => Charm(ctx, "orc", "hero") && Charm(ctx, "orc2", "hero")),
                    Step.Assert("the shared gate refuses the charmed orc's attacks on the hero", ctx =>
                    {
                        CharacterController orc2 = ctx.Get("orc2"), hero = ctx.Get("hero"), hero2 = ctx.Get("hero2");
                        Legality weapon = AttackLegality.CanDirectlyAttack(orc2, hero, DirectAttackKind.Weapon, AttackIntent.ForAI(ctx.Gm, orc2, hero));
                        Legality trip = AttackLegality.CanDirectlyAttack(orc2, hero, DirectAttackKind.Maneuver, AttackIntent.ForManeuver(SpecialAttackType.Trip));
                        Legality aoo = AttackLegality.CanDirectlyAttack(orc2, hero, DirectAttackKind.AoO);
                        Legality other = AttackLegality.CanDirectlyAttack(ctx.Get("orc"), hero2, DirectAttackKind.Weapon, AttackIntent.ForAI(ctx.Gm, ctx.Get("orc"), hero2));
                        return !weapon.Ok && weapon.Bar == LegalityBar.Charm && !trip.Ok && trip.Bar == LegalityBar.Charm
                            && !aoo.Ok && aoo.Bar == LegalityBar.Charm && other.Bar != LegalityBar.Charm;
                    }),
                    Step.Pass())
                .Expect("The fixture charms both orcs and the gate refuses their attacks on the hero", Expect.AssertsPass())
                .Expect("The orcs' turns are not skipped", v =>
                    v.TurnsOf("orc").Any(e => e.Round == 1) && v.TurnsOf("orc2").Any(e => e.Round == 1)
                        ? ExpectResult.Pass("both orcs had their turns") : ExpectResult.Fail("an orc turn is missing"))
                .Expect("Neither orc attacks the hero", Expect.None("attack", e => (e.Str("attacker") == "orc" || e.Str("attacker") == "orc2") && e.Str("target") == "hero"))
                .Build();
        }

        /// <summary>
        /// The Scripted hero stands between two idle orcs and has charmed one of them (a fixture). It moves away, leaving
        /// both orcs' threatened squares: the uncharmed orc takes its attack of opportunity, the charmed one does not
        /// (an attack of opportunity is an attack: delegated RAW reading 2026-10-29, D14). Both orcs act first, so neither
        /// is flat-footed (a flat-footed creature makes no AoO, PHB p.137), and the charm comes after their idle turns.
        /// </summary>
        private static ScenarioDef CharmedNoAoOOnCharmer()
        {
            return Rules("rules/charmed-no-aoo-on-charmer", "A charmed creature makes no attack of opportunity on its charmer (PHB p.209; D14, CMB-100)")
                .Covers("CMB-100", "PHB p.209", "PHB p.137", "D14", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 10, 10, Control.Scripted)
                .Npc("charmed", "orc_warrior", 11, 10, Control.Idle)
                .Npc("orc", "orc_warrior", 10, 11, Control.Idle)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); })
                .Initiative("charmed", "orc", "hero")
                .Turn("hero", 1,
                    Step.Assert("the hero has charmed one orc (fixture)", ctx => Charm(ctx, "charmed", "hero")),
                    Step.Move(10, 7))
                .Expect("The fixture charms the orc", Expect.AssertsPass())
                .Expect("The hero moves away", Expect.StepStatus("hero", 1, "Move(10,7)", 0, "done"))
                .Expect("The uncharmed orc takes its attack of opportunity", v =>
                    v.AoOs("orc", "hero").Count > 0 ? ExpectResult.Pass(v.AoOs("orc", "hero").Count + " AoO(s)") : ExpectResult.Fail("no AoO by the uncharmed orc"))
                .Expect("The charmed orc takes none", v =>
                    v.AoOs("charmed", "hero").Count == 0 && v.Attacks("charmed", "hero").Count == 0
                        ? ExpectResult.Pass("no AoO by the charmed orc")
                        : ExpectResult.Fail(v.AoOs("charmed", "hero").Count + " AoO(s) by the charmed orc"))
                .Build();
        }

        // ── The chosen flight (D15, PHB p.309, p.159; Q1) ──────────────

        /// <summary>
        /// The Scripted goblin frightens the adjacent Ui hero (a fixture). On the hero's turn the compulsion pass gives it the
        /// restricted menu (it can flee, so no forced flight): End Turn is refused while it can flee (the End Turn button's
        /// handler is pressed and the turn stays the hero's), the Attack on the goblin is refused, a 5-foot step away is
        /// refused while a full move is open (it would cut the flight short), a move that ends no farther from the goblin
        /// is refused with nothing spent, and the two moves away are done (the second with the standard action converted);
        /// then End Turn is allowed and the turn ends.
        /// </summary>
        private static ScenarioDef FrightenedPcChoosesFlightUi()
        {
            return Rules("rules/frightened-pc-chooses-flight-ui", "A frightened PC chooses its flight from a restricted menu (PHB p.309; D15, Q1, CMB-100)")
                .Covers("CMB-100", "PHB p.309", "D15", "Q1", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("gob", "goblin", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 10, 10, Control.Ui)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); })
                .Tweak("gob", Sturdy)
                .Initiative("gob", "hero")
                .Turn("gob", 1, Step.Assert("the goblin frightens the hero (fixture)", ctx =>
                {
                    ctx.Gm.ApplyCondition(ctx.Get("hero"), CombatConditionType.Frightened, 3, ctx.Get("gob"), sourceCategory: "Harness");
                    return ctx.Get("hero").HasCondition(CombatConditionType.Frightened);
                }), Step.Pass())
                .Turn("hero", 1,
                    Step.Assert("the turn is a chosen flight and End Turn is refused while the hero can flee", ctx =>
                    {
                        CharacterController h = ctx.Get("hero");
                        TurnCompulsion c = h.CurrentTurnStart.Compulsion;
                        string refusal = FlightRules.EndTurnRefusal(ctx.Gm, h);
                        ctx.Note("b13s20 endTurnRefusal=" + (refusal ?? "none"));
                        return c != null && c.ChoosesFlight && !c.Cornered && refusal != null;
                    }),
                    Step.Assert("pressing End Turn while the hero can flee leaves the turn the hero's", ctx =>
                    {
                        CharacterController h = ctx.Get("hero");
                        ctx.Gm.OnEndTurnButtonPressed();
                        return ctx.Gm.ActivePC == h && ctx.Gm.IsPlayerTurn && h.Actions.HasMoveAction && h.Actions.HasStandardAction;
                    }),
                    Step.Assert("a 5-foot step away would cut the flight short while a full move is open", ctx =>
                    {
                        string refusal = FlightRules.DestinationRefusal(ctx.Get("hero"), new Vector2Int(9, 10), shortMove: true);
                        ctx.Note("b13s20 shortStepRefusal=" + (refusal ?? "none"));
                        return refusal != null && FlightRules.DestinationRefusal(ctx.Get("hero"), new Vector2Int(9, 10)) == null;
                    }),
                    Step.Attack("gob"),
                    Step.FiveFootStep(9, 10),
                    Step.Move(11, 11),
                    Step.Move(7, 10),
                    Step.Move(4, 10),
                    Step.Assert("no flight option is left, so End Turn is allowed", ctx => FlightRules.EndTurnRefusal(ctx.Gm, ctx.Get("hero")) == null))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The fixtures hold: a chosen flight, End Turn refused, then allowed", Expect.AssertsPass())
                .Expect("No forced flight: the menu opened (the restricted-menu line, no controller flee line)", v =>
                {
                    bool menu = v.Log("Hero must flee from").Any(e => e.Round == 1);
                    bool forced = v.Log(ForcedFlightLines).Any(e => e.Round == 1);
                    return menu && !forced ? ExpectResult.Pass("restricted menu") : ExpectResult.Fail("menu line " + menu + ", forced line " + forced);
                })
                .Expect("The Attack on the goblin is refused", Expect.StepStatus("hero", 1, "Attack(gob)", 0, "refused"))
                .Expect("A 5-foot step away is refused while a full move is open", Expect.StepStatus("hero", 1, "FiveFootStep(9,10)", 0, "refused"))
                .Expect("A move that ends no farther from the goblin is refused", Expect.StepStatus("hero", 1, "Move(11,11)", 0, "refused"))
                .Expect("Both moves away are done", Expect.All(
                    Expect.StepStatus("hero", 1, "Move(7,10)", 0, "done"),
                    Expect.StepStatus("hero", 1, "Move(4,10)", 0, "done")))
                .Expect("The hero ends farther from the goblin", v =>
                {
                    Vector2Int? h = Pos(v.Final("hero")), g = Pos(v.Final("gob"));
                    if (h == null || g == null) return ExpectResult.Inconclusive("no positions");
                    return Dist(h.Value, g.Value) >= 7 ? ExpectResult.Pass("distance " + Dist(h.Value, g.Value)) : ExpectResult.Fail("distance " + Dist(h.Value, g.Value));
                })
                .Expect("The hero makes no attack", Expect.None("attack", e => e.Str("attacker") == "hero"))
                .Build();
        }

        /// <summary>
        /// A Ui-controlled undead (a fighter whose creature type is Undead) is turned by the Scripted goblin two squares away
        /// (a fixture: the Turned condition with the goblin as its turner, the effect of a successful turning check, PHB
        /// p.159). On its turn it gets the restricted menu: its Attack is refused, a move toward the turner is refused, and
        /// its withdraw away is done. It does not cower and makes no attack.
        /// </summary>
        private static ScenarioDef TurnedPcFlightMenuUi()
        {
            return Rules("rules/turned-pc-flight-menu-ui", "A turned PC chooses its flight from a restricted menu (PHB p.159; D15, Q1, CMB-100)")
                .Covers("CMB-100", "PHB p.159", "D15", "Q1", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("turner", "goblin", 12, 10, Control.Scripted)
                .Pc("undead", ActorSource.Stats(() =>
                {
                    CharacterStats s = RulesScenarios.Fighter("Dead Knight", 3);
                    s.CreatureType = "Undead";
                    return s;
                }), 10, 10, Control.Ui)
                .Tweak("undead", c => { RulesScenarios.StripOffHand(c); Sturdy(c); })
                .Tweak("turner", Sturdy)
                .Initiative("turner", "undead")
                .Turn("turner", 1, Step.Assert("the goblin turns the undead (fixture)", ctx =>
                {
                    CharacterController u = ctx.Get("undead"), t = ctx.Get("turner");
                    ctx.Gm.ApplyCondition(u, CombatConditionType.Turned, 10, t, sourceCategory: "Harness");
                    ctx.Gm.RegisterTurnUndeadTrackerForAI(u, t);
                    return u.HasCondition(CombatConditionType.Turned);
                }), Step.Pass())
                .Turn("undead", 1,
                    Step.Assert("the turn is a chosen flight from the turner", ctx =>
                    {
                        TurnCompulsion c = ctx.Get("undead").CurrentTurnStart.Compulsion;
                        return c != null && c.Turned && c.ChoosesFlight && c.FlightSources.Contains(ctx.Get("turner"));
                    }),
                    Step.Attack("turner"),
                    Step.Move(11, 10),
                    Step.Withdraw(6, 10))
                .Expect("The undead's turn is a Ui turn", Expect.Controller("undead", "ui"))
                .Expect("The fixtures hold: turned, a chosen flight", Expect.AssertsPass())
                .Expect("No forced flight and no cowering", v =>
                {
                    bool forced = v.Log(ForcedFlightLines).Any(e => e.Round == 1);
                    bool cowering = HasCond(v.Final("undead"), "Cowering");
                    return !forced && !cowering ? ExpectResult.Pass("restricted menu") : ExpectResult.Fail("forced line " + forced + ", cowering " + cowering);
                })
                .Expect("The Attack is refused", Expect.StepStatus("undead", 1, "Attack(turner)", 0, "refused"))
                .Expect("A move toward the turner is refused", Expect.StepStatus("undead", 1, "Move(11,10)", 0, "refused"))
                .Expect("The withdraw away is done and ends at (6,10)", v =>
                {
                    ExpectResult r = Expect.StepStatus("undead", 1, "Withdraw(6,10)", 0, "done")(v);
                    if (!r.IsPass) return r;
                    Vector2Int? p = Pos(v.Final("undead"));
                    return p == new Vector2Int(6, 10) ? ExpectResult.Pass("at (6,10)") : ExpectResult.Fail("at " + p);
                })
                .Expect("The undead makes no attack", Expect.None("attack", e => e.Str("attacker") == "undead"))
                .Build();
        }
    }
}
#endif
