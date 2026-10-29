#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Scenarios
{
    /// <summary>
    /// The owner ruling of 2026-10-29 on the charge ("Charges need a clear path unless they have some ability to bypass an
    /// object in their way, and they cannot end with two creatures spaces overlapping"; docs/systems/RULES_COVERAGE.md 4.1):
    /// one per-mover square question (MovementTerrain) for the charge line, its end space and normal movement, PC and NPC
    /// alike. A charger standing in grease cannot charge out of it (rules/charge-out-of-grease; PHB p.148, p.154); a line
    /// through Solid Fog is refused, unless the charger has Freedom of Movement (rules/charge-through-solid-fog; PHB p.281,
    /// p.233); a line across a Sleet Storm's icy ground is refused too (rules/charge-through-sleet-storm; PHB p.280); an
    /// incorporeal charger crosses greased squares with no Balance check (rules/charge-incorporeal-over-grease, -ui; PHB
    /// p.148) and an Entangle area with no Reflex save (rules/charge-incorporeal-over-entangle; MM p.311); a charge stopped
    /// in a helpless creature's square moves back to the last clear square it passed (rules/charge-interrupted-on-helpless,
    /// -ai); the square question's exemptions on real walls and zones (rules/movement-terrain-exemptions).
    /// </summary>
    public static class ChargeTerrainScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("charge-out-of-grease", ChargeOutOfGrease);
            yield return S("charge-through-solid-fog", ChargeThroughSolidFog);
            yield return S("charge-incorporeal-over-grease", () => ChargeIncorporealOverGrease(ui: false));
            yield return S("charge-incorporeal-over-grease-ui", () => ChargeIncorporealOverGrease(ui: true));
            yield return S("charge-through-sleet-storm", ChargeThroughSleetStorm);
            yield return S("charge-incorporeal-over-entangle", ChargeIncorporealOverEntangle);
            yield return S("movement-terrain-exemptions", MovementTerrainExemptions);
            yield return S("charge-interrupted-on-helpless", () => ChargeInterruptedOnHelpless(ui: true));
            yield return S("charge-interrupted-on-helpless-ai", () => ChargeInterruptedOnHelpless(ui: false));
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("ChargeTerrainScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title)
            => Scenario.Define(id, title).Tags("rules", "charge", "movement").Covers("owner interpretation 2026-10-29", "PHB p.154");

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

        private static Vector2Int? Pos(JsonObj snap) => snap != null && snap.Get("pos") is Vector2Int p ? p : (Vector2Int?)null;

        private static bool MovedInto(TraceView v, string key, int round, Vector2Int square)
            => v.Moves(key, round).Any(e => e.Get("to") is Vector2Int t && t == square);

        private static List<TraceEvent> Dice(TraceView v, string ctx)
            => v.Of("dice").Where(e => (e.Str("ctx") ?? "").Contains(ctx)).ToList();

        /// <summary>A Human wizard 3: the caster of the scenario's grease or fog (it only owns the area).</summary>
        private static CharacterStats Wizard(string name)
        {
            var s = new CharacterStats(
                name: name, level: 3, characterClass: "Wizard",
                str: 10, dex: 12, con: 12, wis: 10, intelligence: 16, cha: 10,
                bab: 0, armorBonus: 0, shieldBonus: 0,
                damageDice: 6, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 18, raceName: "Human");
            s.CharacterAlignment = Alignment.TrueNeutral;
            s.EnsureMulticlassDataInitialized();
            return s;
        }

        private static HashSet<Vector2Int> Rect(int x0, int x1, int y0, int y1)
        {
            var cells = new HashSet<Vector2Int>();
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    cells.Add(new Vector2Int(x, y));
            return cells;
        }

        /// <summary>A Grease area on <paramref name="cells"/> (GameManager.CreateGreaseArea, the cast's builder); in force a frame later.</summary>
        private static bool PlaceGrease(ScenarioContext ctx, string casterKey, HashSet<Vector2Int> cells)
        {
            Vector2Int anchor = cells.First();
            ctx.Gm.CreateGreaseArea(SquareGridUtils.GridToWorld(anchor), 10, 11, ctx.Get(casterKey), SpellDatabase.GetSpell(SpellNames.GREASE), cells);
            return true;
        }

        /// <summary>A Wall of Ice on <paramref name="cells"/> (PHB p.299), intact; in force once its Start has run (a frame later).</summary>
        private static void PlaceWallOfIce(params Vector2Int[] cells)
        {
            var go = new GameObject("WallOfIce_Test_Area");
            go.transform.position = SquareGridUtils.GridToWorld(cells[0]);
            WallOfIceAreaEffect wall = go.AddComponent<WallOfIceAreaEffect>();
            wall.CenterPosition = go.transform.position;
            wall.CenterCell = cells[0];
            wall.LengthSquares = Mathf.Max(2, cells.Length);
            wall.RoundsRemaining = 10;
            wall.CasterLevel = 9;
            wall.SetExplicitCells(new HashSet<Vector2Int>(cells));
        }

        /// <summary>A Wall of Force on <paramref name="cells"/> (PHB p.298); in force once its Start has run (a frame later).</summary>
        private static void PlaceWallOfForce(params Vector2Int[] cells)
        {
            var go = new GameObject("WallOfForce_Test_Area");
            go.transform.position = SquareGridUtils.GridToWorld(cells[0]);
            WallOfForceAreaEffect wall = go.AddComponent<WallOfForceAreaEffect>();
            wall.CenterPosition = go.transform.position;
            wall.CenterCell = cells[0];
            wall.LengthSquares = Mathf.Max(2, cells.Length);
            wall.RoundsRemaining = 10;
            wall.CasterLevel = 9;
            wall.SetExplicitCells(new HashSet<Vector2Int>(cells));
        }

        /// <summary>Runs <paramref name="check"/> with <paramref name="c"/>'s incorporeal flag set to <paramref name="incorporeal"/>, then restores it.</summary>
        private static bool WithIncorporeal(CharacterController c, bool incorporeal, Func<bool> check)
        {
            bool was = c.IsIncorporeal;
            c.ConfigureIncorporeal(incorporeal);
            try { return check(); }
            finally { c.ConfigureIncorporeal(was); }
        }

        /// <summary>Runs <paramref name="check"/> with Freedom of Movement on <paramref name="c"/>, then restores the flag.</summary>
        private static bool WithFreedom(CharacterController c, Func<bool> check)
        {
            bool was = c.Stats.FreedomOfMovementActive;
            c.Stats.FreedomOfMovementActive = true;
            try { return check(); }
            finally { c.Stats.FreedomOfMovementActive = was; }
        }

        private static SquareHindrance H(CharacterController c, int x, int y, out string reason)
            => MovementTerrain.Classify(c, new Vector2Int(x, y), out reason);

        private static SquareHindrance H(CharacterController c, int x, int y) => MovementTerrain.Classify(c, new Vector2Int(x, y), out _);

        private static bool GreaseOn(ScenarioContext ctx, Vector2Int cell)
            => AreaEffectManager.Instance.GetEffectsOfType<GreaseAreaEffect>().Any(g => g != null && g.IsCellInArea(cell))
               && ctx.Gm.IsDifficultTerrain(cell);

        // ── rules/charge-out-of-grease ─────────────────────────────────

        /// <summary>
        /// Owner interpretation 2026-10-29 (PHB p.148: a creature moves only as fast as the most difficult terrain it occupies;
        /// p.154: every charge line starts in the starting space and may not pass a square that slows movement): a Ui fighter
        /// 4 stands at (10,10) in grease laid on its own square only (the area builder takes any footprint; its Reflex save at
        /// the casting, a forced 19, keeps it standing) with a sturdy dummy at (14,10). Every other square of every line is
        /// open, but the starting square is
        /// greased, so there is no charge, for the PC or the AI's query, and the refusal says the charger stands in difficult
        /// terrain. The Charge step is refused with nothing spent; the hero then walks out (its Balance check passes) and
        /// attacks. Before the fix the planner skipped the whole starting space and the charge went ahead.
        /// </summary>
        private static ScenarioDef ChargeOutOfGrease()
        {
            return Rules("rules/charge-out-of-grease", "A charger standing in grease cannot charge out of it: refused, nothing spent (PHB p.148, p.154; owner interpretation 2026-10-29)")
                .Covers("PHB p.148", "PHB p.237")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Ui)
                .Pc("wiz", ActorSource.Stats(() => Wizard("Grease Wizard")), 2, 2, Control.Idle)
                .Npc("dummy", "target_dummy", 14, 10, Control.Idle)
                .Tweak("hero", RulesScenarios.StripOffHand)
                .Tweak("dummy", SturdyNoAoO)
                .Force(20, 19, null, -1)
                .Initiative("wiz", "hero", "dummy")
                .Turn("hero", 1,
                    Step.Assert("the grease is laid under the hero only", ctx => PlaceGrease(ctx, "wiz", Rect(10, 10, 10, 10))),
                    Step.Wait(3),
                    Step.Assert("the hero's square is greased difficult terrain and the hero stands", ctx =>
                        GreaseOn(ctx, new Vector2Int(10, 10)) && !ctx.Gm.IsDifficultTerrain(new Vector2Int(11, 10)) && !ctx.Gm.IsDifficultTerrain(new Vector2Int(10, 9))
                        && !ctx.Get("hero").HasCondition(CombatConditionType.Prone)),
                    Step.Assert("no charge out of the grease for the PC or the AI query, and no Charge highlight", ctx =>
                        !ctx.Gm.CanChargeTargetForAI(ctx.Get("hero"), ctx.Get("dummy"))
                        && !ctx.Gm.HasAnyValidChargeTarget(ctx.Get("hero"))),
                    Step.Assert("the refusal says the charger stands in difficult terrain (PHB p.154)", ctx =>
                    {
                        Legality l = ctx.Gm.CanCharge(ctx.Gm.ChargeRequestForUi(ctx.Get("hero"), ctx.Get("dummy"), false));
                        return !l.Ok && l.Reason.Contains("the charger stands in difficult terrain");
                    }),
                    Step.Charge("dummy"),
                    Step.Assert("nothing was spent: the full-round action is left and the hero has not moved", ctx =>
                        ctx.Get("hero").Actions.HasFullRoundAction && ctx.Get("hero").GridPosition == new Vector2Int(10, 10)),
                    Step.Move(13, 10),
                    Step.Attack("dummy"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The Charge step is refused", Expect.StepStatus("hero", 1, "Charge", 0, "refused"))
                .Expect("No charge is made", v =>
                    v.Log("Hero charges").Count == 0 ? ExpectResult.Pass("no charge") : ExpectResult.Fail(v.Log("Hero charges").Count + " charge line(s)"))
                .Expect("The hero walks out of the grease and attacks the dummy (the turn was whole)", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("hero", "dummy", false, 1);
                    return attacks.Count == 1 && MovedInto(v, "hero", 1, new Vector2Int(13, 10))
                        ? ExpectResult.Pass("moved to (13,10), one attack", attacks[0].Seq)
                        : ExpectResult.Fail(attacks.Count + " attack(s), moves " + v.Moves("hero", 1).Count);
                })
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── rules/charge-through-solid-fog ─────────────────────────────

        /// <summary>
        /// Owner interpretation 2026-10-29 (PHB p.281: any creature moving through Solid Fog progresses at 5 ft; p.154: a line
        /// through a square that slows movement bars the charge; p.233: Freedom of Movement moves normally despite Solid
        /// Fog): a Ui fighter 4 at (8,10), a Solid Fog band at x 10-11, y 7-13, and a sturdy dummy at (14,10). Every line
        /// to a space from which the hero could attack crosses the fog, so the charge is refused for the PC and the AI's
        /// query, naming the fog. With Freedom of Movement the same charge would be legal (the flag is set and cleared inside
        /// one check). The Charge step is refused with nothing spent. Before the fix fog squares were not on the charge's
        /// list and the charge went ahead.
        /// </summary>
        private static ScenarioDef ChargeThroughSolidFog()
        {
            return Rules("rules/charge-through-solid-fog", "A charge line through Solid Fog is refused, but not with Freedom of Movement (PHB p.281, p.154, p.233; owner interpretation 2026-10-29)")
                .Covers("PHB p.281", "PHB p.233")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 8, 10, Control.Ui)
                .Pc("wiz", ActorSource.Stats(() => Wizard("Fog Wizard")), 2, 2, Control.Idle)
                .Npc("dummy", "target_dummy", 14, 10, Control.Idle)
                .Tweak("hero", RulesScenarios.StripOffHand)
                .Tweak("dummy", SturdyNoAoO)
                .Force(20, 19, null, -1)
                .Initiative("wiz", "hero", "dummy")
                .Turn("hero", 1,
                    Step.Assert("the fog band is laid", ctx =>
                    {
                        HashSet<Vector2Int> cells = Rect(10, 11, 7, 13);
                        ctx.Gm.CreateSolidFogArea(SquareGridUtils.GridToWorld(new Vector2Int(10, 10)), 10, 7, ctx.Get("wiz"), cells);
                        return true;
                    }),
                    Step.Wait(3),
                    Step.Assert("the fog is in force on the line and the hero is outside it", ctx =>
                        SolidFogAreaEffect.IsCellInAnySolidFog(new Vector2Int(10, 10)) && SolidFogAreaEffect.IsCellInAnySolidFog(new Vector2Int(11, 12))
                        && !SolidFogAreaEffect.IsCreatureInAnySolidFog(ctx.Get("hero"))),
                    Step.Assert("no charge through the fog for the PC or the AI query, and no Charge highlight", ctx =>
                        !ctx.Gm.CanChargeTargetForAI(ctx.Get("hero"), ctx.Get("dummy"))
                        && !ctx.Gm.HasAnyValidChargeTarget(ctx.Get("hero"))),
                    Step.Assert("the refusal names the fog (PHB p.154)", ctx =>
                    {
                        Legality l = ctx.Gm.CanCharge(ctx.Gm.ChargeRequestForUi(ctx.Get("hero"), ctx.Get("dummy"), false));
                        return !l.Ok && l.Reason.Contains("solid fog is in the way");
                    }),
                    Step.Assert("with Freedom of Movement the same charge is legal (PHB p.233)", ctx =>
                    {
                        CharacterController h = ctx.Get("hero");
                        h.Stats.FreedomOfMovementActive = true;
                        try { return ctx.Gm.CanChargeTargetForAI(h, ctx.Get("dummy")); }
                        finally { h.Stats.FreedomOfMovementActive = false; }
                    }),
                    Step.Charge("dummy"),
                    Step.Assert("nothing was spent: the full-round action is left and the hero has not moved", ctx =>
                        ctx.Get("hero").Actions.HasFullRoundAction && ctx.Get("hero").GridPosition == new Vector2Int(8, 10)))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The Charge step is refused", Expect.StepStatus("hero", 1, "Charge", 0, "refused"))
                .Expect("No charge is made", v =>
                    v.Log("Hero charges").Count == 0 ? ExpectResult.Pass("no charge") : ExpectResult.Fail(v.Log("Hero charges").Count + " charge line(s)"))
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── rules/charge-incorporeal-over-grease, -ui ──────────────────

        /// <summary>
        /// Owner interpretation 2026-10-29 (PHB p.148: incorporeal creatures are not hampered by difficult terrain; MM
        /// p.310-311): a grease band at x 10-11, y 8-12 lies across the line from (8,10) to a sturdy target at (14,10);
        /// sturdy dummies at (13,9) and (13,11) hold the diagonal end spaces, so the charge runs along row 10 to (13,10). A
        /// corporeal charger would be refused (difficult terrain on the line). A fighter made incorporeal charges a dummy:
        /// Scripted (the NPC path, CanChargeTargetForAI and NPCExecuteChargeForAI) or Ui (the Charge button); the MM's
        /// incorporeal entries spawn dead and helpless today (CRE-044). Either way the charge is legal, crosses both greased squares with no Balance check
        /// (the grease's step hook asks the same exemption; delegated RAW reading 2026-10-29), ends at (13,10) and makes its
        /// attack. Before the fix the planner refused every charge over grease, incorporeal or not.
        /// </summary>
        private static ScenarioDef ChargeIncorporealOverGrease(bool ui)
        {
            string id = ui ? "rules/charge-incorporeal-over-grease-ui" : "rules/charge-incorporeal-over-grease";
            ScenarioBuilder b = Rules(id, (ui ? "A Ui PC" : "An AI-path creature") + " that is incorporeal charges across greased squares with no Balance check (PHB p.148; owner interpretation 2026-10-29)")
                .Covers("PHB p.148", "MM p.310", "PHB p.237")
                .MaxRounds(1)
                .RecordDice()
                .Pc("wiz", ActorSource.Stats(() => Wizard("Grease Wizard")), 2, 2, Control.Idle)
                .Npc("north", "target_dummy", 13, 9, Control.Idle)
                .Npc("south", "target_dummy", 13, 11, Control.Idle)
                .Tweak("north", SturdyNoAoO)
                .Tweak("south", SturdyNoAoO)
                .Force(20, 19, null, -1);

            // The MM's incorporeal entries (shadow, allip, wraith) store their missing Strength and Constitution as 0 and spawn
            // dead and helpless (CRE-044), so the charger is a fighter made incorporeal: Ui through the Charge button, or a
            // Scripted party member, which takes the NPC turn path.
            const string charger = "hero";
            const string target = "dummy";
            b = b.Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 8, 10, ui ? Control.Ui : Control.Scripted)
                .Npc("dummy", "target_dummy", 14, 10, Control.Idle)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); RulesScenarios.StripArmor(c); c.ConfigureIncorporeal(true); })
                .Tweak("dummy", SturdyNoAoO)
                .Initiative("wiz", "hero", "dummy", "north", "south");

            return b.Turn(charger, 1,
                    Step.Assert("the grease band is laid", ctx => PlaceGrease(ctx, "wiz", Rect(10, 11, 8, 12))),
                    Step.Wait(3),
                    Step.Assert("the band is greased difficult terrain on the line", ctx => GreaseOn(ctx, new Vector2Int(10, 10)) && GreaseOn(ctx, new Vector2Int(11, 10))),
                    Step.Assert("the charger is incorporeal and the greased squares do not hamper it", ctx =>
                        ctx.Get(charger).IsIncorporeal && !MovementTerrain.IsDifficultTerrain(ctx.Get(charger), new Vector2Int(10, 10))),
                    Step.Assert("the exemption is what makes the charge legal: the same charger made corporeal is refused (difficult terrain)", ctx =>
                        WithIncorporeal(ctx.Get(charger), false, () => !ctx.Gm.CanChargeTargetForAI(ctx.Get(charger), ctx.Get(target))
                            && MovementTerrain.IsDifficultTerrain(ctx.Get(charger), new Vector2Int(10, 10)))),
                    Step.Assert("the charge over the grease is legal, straight along row 10 to (13,10)", ctx =>
                    {
                        if (!ctx.Gm.CanChargeTargetForAI(ctx.Get(charger), ctx.Get(target)))
                            return false;
                        List<Vector2Int> path = ctx.Gm.GetChargePath(ctx.Get(charger), ctx.Get(target));
                        return path.Count == 5 && path.Contains(new Vector2Int(10, 10)) && path.Contains(new Vector2Int(11, 10)) && path[4] == new Vector2Int(13, 10);
                    }),
                    Step.Charge(target))
                .Expect("The charger's turn is " + (ui ? "a Ui turn" : "a scripted turn"), Expect.Controller(charger, ui ? "ui" : "scripted"))
                .Expect("The Charge step is done", Expect.StepStatus(charger, 1, "Charge", 0, "done"))
                .Expect("The charge crosses both greased squares and is not interrupted", v =>
                    MovedInto(v, charger, 1, new Vector2Int(10, 10)) && MovedInto(v, charger, 1, new Vector2Int(11, 10)) && v.Log("charge is interrupted").Count == 0
                        ? ExpectResult.Pass("stepped through (10,10) and (11,10)")
                        : ExpectResult.Fail("(10,10) " + MovedInto(v, charger, 1, new Vector2Int(10, 10)) + ", (11,10) " + MovedInto(v, charger, 1, new Vector2Int(11, 10)) + ", interrupted " + v.Log("charge is interrupted").Count))
                .Expect("No Balance check: the grease does not hamper an incorporeal mover", v =>
                {
                    int n = Dice(v, GreaseAreaEffect.BalanceDiceContext).Count;
                    return n == 0 ? ExpectResult.Pass("no Balance check") : ExpectResult.Fail(n + " Balance check(s)");
                })
                .Expect("The charge ends at (13,10) and makes its attack", v =>
                {
                    Vector2Int? p = Pos(v.Final(charger));
                    TraceEvent atk = v.Attacks(charger, target, false, 1).FirstOrDefault();
                    return p == new Vector2Int(13, 10) && atk != null
                        ? ExpectResult.Pass("at (13,10), attack " + (atk.Bool("hit") ? "hit" : "miss"), atk.Seq)
                        : ExpectResult.Fail("at " + p + ", attack " + (atk != null));
                })
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── rules/charge-through-sleet-storm ───────────────────────────

        /// <summary>
        /// Owner interpretation 2026-10-29 (PHB p.280: a Sleet Storm makes the ground icy, and a creature walks in it at half
        /// speed with a DC 10 Balance check; p.154: a line through a square that slows movement bars the charge): a Ui
        /// fighter 4 at (8,10), a Sleet Storm band at x 10-11, y 7-13, and a sturdy dummy at (14,10). Every line to a space
        /// from which the hero could attack crosses the storm, so the charge is refused for the PC and the AI's query,
        /// naming the icy ground; with Freedom of Movement (PHB p.233), or made incorporeal (PHB p.148: it does not walk on
        /// the ground), the same charge would be legal (each flag set and restored inside one check). The storm's squares are
        /// not difficult terrain for a plain move (its Balance check is SPL-206). The Charge step is refused with nothing
        /// spent. Before the fix the storm's squares were not on the charge's list and the charge went ahead.
        /// </summary>
        private static ScenarioDef ChargeThroughSleetStorm()
        {
            return Rules("rules/charge-through-sleet-storm", "A charge line across a Sleet Storm's icy ground is refused, but not with Freedom of Movement or for an incorporeal charger (PHB p.280, p.154, p.233; owner interpretation 2026-10-29)")
                .Covers("PHB p.280", "PHB p.233", "PHB p.148")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 8, 10, Control.Ui)
                .Pc("wiz", ActorSource.Stats(() => Wizard("Sleet Wizard")), 2, 2, Control.Idle)
                .Npc("dummy", "target_dummy", 14, 10, Control.Idle)
                .Tweak("hero", RulesScenarios.StripOffHand)
                .Tweak("dummy", SturdyNoAoO)
                .Force(20, 19, null, -1)
                .Initiative("wiz", "hero", "dummy")
                .Turn("hero", 1,
                    Step.Assert("the storm band is laid", ctx =>
                    {
                        ctx.Gm.CreateSleetStormArea(SquareGridUtils.GridToWorld(new Vector2Int(10, 10)), 10, 5, ctx.Get("wiz"), Rect(10, 11, 7, 13));
                        return true;
                    }),
                    Step.Wait(3),
                    Step.Assert("the storm is in force on the line, the hero is outside it, and its squares are not difficult terrain", ctx =>
                        AreaEffectManager.Instance.GetEffectsOfType<SleetStormAreaEffect>().Any(z => z != null && z.IsCellInArea(new Vector2Int(10, 10)) && z.IsCellInArea(new Vector2Int(11, 12)))
                        && !SleetStormAreaEffect.IsCreatureInAnySleetStorm(ctx.Get("hero"))
                        && !ctx.Gm.IsDifficultTerrain(new Vector2Int(10, 10))),
                    Step.Assert("no charge across the storm for the PC or the AI query, and no Charge highlight", ctx =>
                        !ctx.Gm.CanChargeTargetForAI(ctx.Get("hero"), ctx.Get("dummy"))
                        && !ctx.Gm.HasAnyValidChargeTarget(ctx.Get("hero"))),
                    Step.Assert("the refusal names the icy ground (PHB p.154)", ctx =>
                    {
                        Legality l = ctx.Gm.CanCharge(ctx.Gm.ChargeRequestForUi(ctx.Get("hero"), ctx.Get("dummy"), false));
                        return !l.Ok && l.Reason.Contains("a sleet storm's icy ground is in the way");
                    }),
                    Step.Assert("with Freedom of Movement the same charge is legal (PHB p.233)", ctx =>
                        WithFreedom(ctx.Get("hero"), () => ctx.Gm.CanChargeTargetForAI(ctx.Get("hero"), ctx.Get("dummy")))),
                    Step.Assert("made incorporeal the same charger may charge (PHB p.148)", ctx =>
                        WithIncorporeal(ctx.Get("hero"), true, () => ctx.Gm.CanChargeTargetForAI(ctx.Get("hero"), ctx.Get("dummy")))),
                    Step.Charge("dummy"),
                    Step.Assert("nothing was spent: the full-round action is left and the hero has not moved", ctx =>
                        ctx.Get("hero").Actions.HasFullRoundAction && ctx.Get("hero").GridPosition == new Vector2Int(8, 10)))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The Charge step is refused", Expect.StepStatus("hero", 1, "Charge", 0, "refused"))
                .Expect("No charge is made", v =>
                    v.Log("Hero charges").Count == 0 ? ExpectResult.Pass("no charge") : ExpectResult.Fail(v.Log("Hero charges").Count + " charge line(s)"))
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── rules/charge-incorporeal-over-entangle ─────────────────────

        /// <summary>
        /// Owner interpretation 2026-10-29 and its delegated RAW readings (PHB p.148: incorporeal creatures are not hampered
        /// by difficult terrain; MM p.311: an incorporeal creature is not subject to physical actions that move or
        /// manipulate it; PHB p.233: Freedom of Movement): an Entangle band at x 10-11, y 8-12 lies across the line from
        /// (8,10) to a sturdy dummy at (14,10); dummies at (13,9) and (13,11) hold the diagonal end spaces. A Scripted
        /// fighter 4 (the NPC path) made incorporeal charges: the planner allows it, and the executor agrees, because the
        /// plants ask the same exemption (MovementTerrain.ZoneHindersMovement), so the charger makes no Reflex save on
        /// entering (every d20 a 2, which would fail it), is not entangled and ends at (13,10) with its attack. Made
        /// corporeal inside one check the charge is refused, and with Freedom of Movement too it is legal again. Before the
        /// fix the entry save could entangle the charger mid-line although the planner had allowed the charge.
        /// </summary>
        private static ScenarioDef ChargeIncorporealOverEntangle()
        {
            return Rules("rules/charge-incorporeal-over-entangle", "An AI-path incorporeal charger crosses an Entangle area with no Reflex save and is not stopped (PHB p.148, MM p.311; owner interpretation 2026-10-29)")
                .Covers("PHB p.148", "MM p.311", "PHB p.227", "PHB p.233")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 8, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 14, 10, Control.Idle)
                .Npc("north", "target_dummy", 13, 9, Control.Idle)
                .Npc("south", "target_dummy", 13, 11, Control.Idle)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); RulesScenarios.StripArmor(c); c.ConfigureIncorporeal(true); })
                .Tweak("dummy", SturdyNoAoO)
                .Tweak("north", SturdyNoAoO)
                .Tweak("south", SturdyNoAoO)
                .Force(20, 2, null, -1)
                .Initiative("hero", "dummy", "north", "south")
                .Turn("hero", 1,
                    Step.Assert("the entangle band is laid", ctx =>
                    {
                        ctx.Gm.CreateEntangleArea(SquareGridUtils.GridToWorld(new Vector2Int(10, 10)), 10, 14, 1, Rect(10, 11, 8, 12));
                        return true;
                    }),
                    Step.Wait(3),
                    Step.Assert("the band is entangling difficult terrain on the line", ctx =>
                        AreaEffectManager.Instance.GetEffectsOfType<EntangleAreaEffect>().Any(z => z != null && z.IsCellInArea(new Vector2Int(10, 10)) && z.IsCellInArea(new Vector2Int(11, 10)))
                        && ctx.Gm.IsDifficultTerrain(new Vector2Int(10, 10)) && ctx.Gm.IsDifficultTerrain(new Vector2Int(11, 10))),
                    Step.Assert("made corporeal, the charger is refused (difficult terrain on the line)", ctx =>
                        WithIncorporeal(ctx.Get("hero"), false, () => !ctx.Gm.CanChargeTargetForAI(ctx.Get("hero"), ctx.Get("dummy")))),
                    Step.Assert("made corporeal with Freedom of Movement, the charge is legal (PHB p.233: Entangle is magic that impedes movement)", ctx =>
                        WithIncorporeal(ctx.Get("hero"), false, () => WithFreedom(ctx.Get("hero"), () => ctx.Gm.CanChargeTargetForAI(ctx.Get("hero"), ctx.Get("dummy"))))),
                    Step.Assert("the incorporeal charge over the plants is legal, straight along row 10 to (13,10)", ctx =>
                    {
                        if (!ctx.Gm.CanChargeTargetForAI(ctx.Get("hero"), ctx.Get("dummy")))
                            return false;
                        List<Vector2Int> path = ctx.Gm.GetChargePath(ctx.Get("hero"), ctx.Get("dummy"));
                        return path.Count == 5 && path.Contains(new Vector2Int(10, 10)) && path.Contains(new Vector2Int(11, 10)) && path[4] == new Vector2Int(13, 10);
                    }),
                    Step.Charge("dummy"))
                .Expect("The charger's turn is a scripted turn", Expect.Controller("hero", "scripted"))
                .Expect("The Charge step is done", Expect.StepStatus("hero", 1, "Charge", 0, "done"))
                .Expect("The charge crosses both entangling squares and is not interrupted", v =>
                    MovedInto(v, "hero", 1, new Vector2Int(10, 10)) && MovedInto(v, "hero", 1, new Vector2Int(11, 10)) && v.Log("charge is interrupted").Count == 0
                        ? ExpectResult.Pass("stepped through (10,10) and (11,10)")
                        : ExpectResult.Fail("(10,10) " + MovedInto(v, "hero", 1, new Vector2Int(10, 10)) + ", (11,10) " + MovedInto(v, "hero", 1, new Vector2Int(11, 10)) + ", interrupted " + v.Log("charge is interrupted").Count))
                .Expect("No Entangle save and no entanglement: the plants do not grasp an incorporeal mover", v =>
                {
                    int saves = v.Log("Hero enters Entangle").Count + v.Log("Hero is in Entangle").Count;
                    int caught = v.Log("Hero is entangled").Count;
                    return saves == 0 && caught == 0 ? ExpectResult.Pass("no save, not entangled") : ExpectResult.Fail(saves + " save(s), " + caught + " entanglement(s)");
                })
                .Expect("The charge ends at (13,10) and makes its attack", v =>
                {
                    Vector2Int? p = Pos(v.Final("hero"));
                    TraceEvent atk = v.Attacks("hero", "dummy", false, 1).FirstOrDefault();
                    return p == new Vector2Int(13, 10) && atk != null
                        ? ExpectResult.Pass("at (13,10), attack " + (atk.Bool("hit") ? "hit" : "miss"), atk.Seq)
                        : ExpectResult.Fail("at " + p + ", attack " + (atk != null));
                })
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── rules/movement-terrain-exemptions ──────────────────────────

        /// <summary>
        /// The per-mover square question's exemptions on real walls and zones (MovementTerrain; owner interpretation and
        /// delegated RAW readings 2026-10-29): a fighter, a fighter made incorporeal and one under Freedom of Movement. A
        /// Wall of Ice at (12,2)-(12,3) blocks the fighter's square, step, diagonal past its corner and path cost but not the
        /// incorporeal one's (MM p.311: it passes solid objects); a Wall of Force at (14,2)-(14,3) blocks both (force); a Web
        /// at x 16-17, y 2-3 slows the fighter but neither the incorporeal mover (strands) nor the free one (PHB p.233), on
        /// the square question and a plain move's path cost; an Entangle at x 16-17, y 6-7 the same; Solid Fog at x 12-13,
        /// y 6-7 slows the fighter and the incorporeal one ("any creature", PHB p.281) but not the free one; a Sleet Storm at
        /// x 14-15, y 6-7 slows the fighter only, on the square question, and costs a plain move nothing extra (SPL-206).
        /// Difficult terrain that no zone makes (a cell marked by hand at (2,16)) still slows the free fighter. No turn is
        /// taken but the checks.
        /// </summary>
        private static ScenarioDef MovementTerrainExemptions()
        {
            return Rules("rules/movement-terrain-exemptions", "Walls and zones block or slow each mover by its movement abilities: incorporeal, Freedom of Movement (MovementTerrain; owner interpretation 2026-10-29)")
                .Covers("PHB p.148", "PHB p.233", "PHB p.281", "PHB p.280", "PHB p.298", "PHB p.299", "MM p.311")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 4, 10, Control.Ui)
                .Pc("ghost", ActorSource.Stats(() => RulesScenarios.Fighter("Ghost", 4)), 4, 12, Control.Idle)
                .Pc("free", ActorSource.Stats(() => RulesScenarios.Fighter("Free", 4)), 4, 14, Control.Idle)
                .Pc("wiz", ActorSource.Stats(() => Wizard("Zone Wizard")), 2, 2, Control.Idle)
                .Npc("dummy", "target_dummy", 9, 18, Control.Idle)
                .Tweak("ghost", c => c.ConfigureIncorporeal(true))
                .Tweak("free", c => c.Stats.FreedomOfMovementActive = true)
                .Tweak("dummy", SturdyNoAoO)
                .Force(20, 19, null, -1)
                .Initiative("hero", "ghost", "free", "wiz", "dummy")
                .Turn("hero", 1,
                    Step.Assert("the walls and zones are laid", ctx =>
                    {
                        PlaceWallOfIce(new Vector2Int(12, 2), new Vector2Int(12, 3));
                        PlaceWallOfForce(new Vector2Int(14, 2), new Vector2Int(14, 3));
                        CharacterController wiz = ctx.Get("wiz");
                        ctx.Gm.CreateWebArea(SquareGridUtils.GridToWorld(new Vector2Int(16, 2)), 10, 5, 14, wiz, null, Rect(16, 17, 2, 3));
                        ctx.Gm.CreateEntangleArea(SquareGridUtils.GridToWorld(new Vector2Int(16, 6)), 10, 14, 1, Rect(16, 17, 6, 7));
                        ctx.Gm.CreateSolidFogArea(SquareGridUtils.GridToWorld(new Vector2Int(12, 6)), 10, 7, wiz, Rect(12, 13, 6, 7));
                        ctx.Gm.CreateSleetStormArea(SquareGridUtils.GridToWorld(new Vector2Int(14, 6)), 10, 5, wiz, Rect(14, 15, 6, 7));
                        ctx.Gm.SetAreaDifficultTerrain(new[] { new Vector2Int(2, 16) }, true);
                        return true;
                    }),
                    Step.Wait(3),
                    Step.Assert("the walls stand and the zones are in force", ctx =>
                        WallOfIceAreaEffect.DoesCellBlockMovement(new Vector2Int(12, 2)) && WallOfForceAreaEffect.DoesCellBlockMovement(new Vector2Int(14, 3))
                        && ctx.Gm.IsDifficultTerrain(new Vector2Int(16, 2)) && ctx.Gm.IsDifficultTerrain(new Vector2Int(17, 7))
                        && SolidFogAreaEffect.IsCellInAnySolidFog(new Vector2Int(13, 7))
                        && AreaEffectManager.Instance.GetEffectsOfType<SleetStormAreaEffect>().Any(z => z != null && z.IsCellInArea(new Vector2Int(15, 6)))),
                    Step.Assert("Wall of Ice: blocks the fighter's square, step, corner diagonal and path, not the incorporeal mover's (MM p.311)", ctx =>
                    {
                        CharacterController f = ctx.Get("hero"), g = ctx.Get("ghost");
                        var from = new Vector2Int(11, 2);
                        var path = new List<Vector2Int> { new Vector2Int(12, 2), new Vector2Int(13, 2) };
                        return H(f, 12, 2) == SquareHindrance.Blocks && H(g, 12, 2) == SquareHindrance.None
                            && SquareGridUtils.IsCellBlockedByWall(f, new Vector2Int(12, 2)) && !SquareGridUtils.IsCellBlockedByWall(g, new Vector2Int(12, 2))
                            && SquareGridUtils.IsStepBlockedByBarrier(f, from, new Vector2Int(12, 2)) && !SquareGridUtils.IsStepBlockedByBarrier(g, from, new Vector2Int(12, 2))
                            && SquareGridUtils.IsStepBlockedByBarrier(f, new Vector2Int(11, 3), new Vector2Int(12, 4)) && !SquareGridUtils.IsStepBlockedByBarrier(g, new Vector2Int(11, 3), new Vector2Int(12, 4))
                            && SquareGridUtils.CalculatePathCost(f, from, path) >= SquareGridUtils.BlockedPathCost && SquareGridUtils.CalculatePathCost(g, from, path) == 2;
                    }),
                    Step.Assert("Wall of Force: blocks both movers' square, step, corner diagonal and path (force, MM p.311)", ctx =>
                    {
                        CharacterController f = ctx.Get("hero"), g = ctx.Get("ghost");
                        var from = new Vector2Int(13, 2);
                        var path = new List<Vector2Int> { new Vector2Int(14, 2), new Vector2Int(15, 2) };
                        return H(f, 14, 2) == SquareHindrance.Blocks && H(g, 14, 2) == SquareHindrance.Blocks
                            && SquareGridUtils.IsStepBlockedByBarrier(f, from, new Vector2Int(14, 2)) && SquareGridUtils.IsStepBlockedByBarrier(g, from, new Vector2Int(14, 2))
                            && SquareGridUtils.IsStepBlockedByBarrier(g, new Vector2Int(13, 3), new Vector2Int(14, 4))
                            && SquareGridUtils.CalculatePathCost(f, from, path) >= SquareGridUtils.BlockedPathCost && SquareGridUtils.CalculatePathCost(g, from, path) >= SquareGridUtils.BlockedPathCost;
                    }),
                    Step.Assert("Web: slows the fighter (difficult terrain), not the incorporeal or the free mover, on the square question and the path cost (MM p.311, PHB p.233)", ctx =>
                        H(ctx.Get("hero"), 16, 2, out string why) == SquareHindrance.Slows && why == "difficult terrain"
                        && H(ctx.Get("ghost"), 16, 2) == SquareHindrance.None && H(ctx.Get("free"), 17, 3) == SquareHindrance.None
                        && SquareGridUtils.CalculatePathCost(ctx.Get("free"), new Vector2Int(15, 2), new List<Vector2Int> { new Vector2Int(16, 2), new Vector2Int(17, 2) }) == 2
                        && SquareGridUtils.CalculatePathCost(ctx.Get("hero"), new Vector2Int(15, 2), new List<Vector2Int> { new Vector2Int(16, 2), new Vector2Int(17, 2) }) == 4),
                    Step.Assert("Entangle: slows the fighter, not the incorporeal or the free mover", ctx =>
                        H(ctx.Get("hero"), 16, 6) == SquareHindrance.Slows && H(ctx.Get("ghost"), 16, 6) == SquareHindrance.None && H(ctx.Get("free"), 17, 7) == SquareHindrance.None),
                    Step.Assert("Solid Fog: slows the fighter and the incorporeal mover (any creature, PHB p.281), not the free one", ctx =>
                        H(ctx.Get("hero"), 12, 6, out string why) == SquareHindrance.Slows && why == "solid fog"
                        && H(ctx.Get("ghost"), 13, 7) == SquareHindrance.Slows && H(ctx.Get("free"), 12, 7) == SquareHindrance.None),
                    Step.Assert("Sleet Storm: slows the fighter on the square question only, not the incorporeal or the free mover, and costs a plain move nothing extra", ctx =>
                        H(ctx.Get("hero"), 14, 6, out string why) == SquareHindrance.Slows && why == "a sleet storm's icy ground"
                        && H(ctx.Get("ghost"), 15, 7) == SquareHindrance.None && H(ctx.Get("free"), 14, 7) == SquareHindrance.None
                        && SquareGridUtils.CalculatePathCost(ctx.Get("hero"), new Vector2Int(14, 8), new List<Vector2Int> { new Vector2Int(14, 7), new Vector2Int(14, 6) }) == 2),
                    Step.Assert("Difficult terrain that no zone makes slows the free fighter, not the incorporeal one", ctx =>
                    {
                        try
                        {
                            return H(ctx.Get("free"), 2, 16) == SquareHindrance.Slows && H(ctx.Get("ghost"), 2, 16) == SquareHindrance.None;
                        }
                        finally { ctx.Gm.SetAreaDifficultTerrain(new[] { new Vector2Int(2, 16) }, false); }
                    }),
                    Step.Assert("the zone exemption itself: a Web holds the fighter, not the incorporeal or the free mover; Solid Fog holds the incorporeal one, not the free one; no zone holds nobody", ctx =>
                    {
                        WebAreaEffect web = AreaEffectManager.Instance.GetEffectsOfType<WebAreaEffect>().FirstOrDefault();
                        SolidFogAreaEffect fog = AreaEffectManager.Instance.GetEffectsOfType<SolidFogAreaEffect>().FirstOrDefault();
                        return web != null && fog != null
                            && MovementTerrain.ZoneHindersMovement(ctx.Get("hero"), web) && !MovementTerrain.ZoneHindersMovement(ctx.Get("ghost"), web)
                            && !MovementTerrain.ZoneHindersMovement(ctx.Get("free"), web)
                            && MovementTerrain.ZoneHindersMovement(ctx.Get("ghost"), fog) && !MovementTerrain.ZoneHindersMovement(ctx.Get("free"), fog)
                            && !MovementTerrain.ZoneHindersMovement(ctx.Get("hero"), null);
                    }))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── rules/charge-interrupted-on-helpless, -ai ──────────────────

        /// <summary>
        /// Owner interpretation 2026-10-29 (a charge never ends with two creatures' spaces overlapping) and its delegated RAW
        /// reading (an interrupted charge moves back to the last clear square it passed, as a failed overrun moves 5 ft back
        /// the way it came, PHB p.157): a fighter 4 with 1 hp at (8,10) charges a sturdy dummy at (14,10) along row 10,
        /// past a sleeping goblin at (10,10) (helpless: it does not stop a charge, PHB p.154); dummies at (13,9) and (13,11)
        /// hold the diagonal end spaces. An orc warrior at (11,11) threatens (10,10) but not (9,10), so leaving the goblin's
        /// square provokes its attack of opportunity (the orc acts first, so it is not flat-footed, PHB p.137), which hits
        /// (every d20 a 19) and drops the hero there. The charge is
        /// interrupted in the goblin's square, and the hero is moved back to (9,10). Ui: the Charge button; ai: a Scripted
        /// party member (the NPC path). An idle ally far away keeps the party in the fight. Before the fix the hero stayed
        /// in the goblin's square.
        /// </summary>
        private static ScenarioDef ChargeInterruptedOnHelpless(bool ui)
        {
            string id = ui ? "rules/charge-interrupted-on-helpless" : "rules/charge-interrupted-on-helpless-ai";
            return Rules(id, (ui ? "A Ui PC's" : "An AI-path") + " charge stopped in a helpless creature's square moves back to the last clear square (owner interpretation 2026-10-29; PHB p.154, p.157)")
                .Covers("PHB p.157", "CMB-199")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 8, 10, ui ? Control.Ui : Control.Scripted)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Ally", 1)), 2, 2, Control.Idle)
                .Npc("goblin", "goblin", 10, 10, Control.Idle)
                .Npc("orc", "orc_warrior", 11, 11, Control.Idle)
                .Npc("dummy", "target_dummy", 14, 10, Control.Idle)
                .Npc("north", "target_dummy", 13, 9, Control.Idle)
                .Npc("south", "target_dummy", 13, 11, Control.Idle)
                .Hp("hero", 1)
                .Tweak("hero", RulesScenarios.StripOffHand)
                .Tweak("ally", c => { Sturdy(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Tweak("goblin", Sturdy)
                .Tweak("orc", Sturdy)
                .Tweak("dummy", SturdyNoAoO)
                .Tweak("north", SturdyNoAoO)
                .Tweak("south", SturdyNoAoO)
                .StartCondition("goblin", CombatConditionType.Asleep, 10)
                .Force(20, 19, null, -1)
                .Initiative("orc", "hero", "ally", "goblin", "dummy", "north", "south")
                .Turn("hero", 1,
                    Step.Assert("the sleeping goblin is helpless, and the charge past it is legal along row 10 to (13,10)", ctx =>
                    {
                        if (!ChargeRules.LetsChargePass(ctx.Get("goblin")) || !ctx.Gm.CanChargeTargetForAI(ctx.Get("hero"), ctx.Get("dummy")))
                            return false;
                        List<Vector2Int> path = ctx.Gm.GetChargePath(ctx.Get("hero"), ctx.Get("dummy"));
                        return path.Count == 5 && path[1] == new Vector2Int(10, 10) && path[4] == new Vector2Int(13, 10);
                    }),
                    Step.Charge("dummy"))
                .Expect("The hero's turn is " + (ui ? "a Ui turn" : "a scripted turn"), Expect.Controller("hero", ui ? "ui" : "scripted"))
                .Expect("The charge enters the goblin's square and the orc's attack of opportunity drops the hero there", v =>
                {
                    bool entered = MovedInto(v, "hero", 1, new Vector2Int(10, 10));
                    int aoos = v.Attacks("orc", "hero", true, 1).Count;
                    return entered && aoos == 1 && v.Log("charge is interrupted").Count == 1
                        ? ExpectResult.Pass("entered (10,10), one AoO, interrupted")
                        : ExpectResult.Fail("entered " + entered + ", AoOs " + aoos + ", interrupted " + v.Log("charge is interrupted").Count);
                })
                .Expect("The hero is moved back to (9,10), the last clear square it passed (no overlap)", v =>
                {
                    Vector2Int? p = Pos(v.Final("hero"));
                    return p == new Vector2Int(9, 10) && v.Log(@"moves back to \(9,10\)").Count == 1
                        ? ExpectResult.Pass("at (9,10)")
                        : ExpectResult.Fail("at " + p + ", move-back log " + v.Log(@"moves back to \(9,10\)").Count);
                })
                .Expect("The sleeping goblin keeps its square", v =>
                {
                    Vector2Int? p = Pos(v.Final("goblin"));
                    return p == new Vector2Int(10, 10) ? ExpectResult.Pass("at (10,10)") : ExpectResult.Fail("at " + p);
                })
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }
    }
}
#endif
