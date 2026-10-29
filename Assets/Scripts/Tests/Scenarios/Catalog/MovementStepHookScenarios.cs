#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 12 step 23 (docs/designs/pc_npc_parity_batch12.md; GRID-018, GRID-013, decision D12): Grease, zones and path
    /// cost on every movement step. Grease's DC 10 Balance check runs for every mover through the movement step hook
    /// (GreaseStepHook, run by CharacterController.MoveAlongPath and the 5-foot step), once per round, half speed on a
    /// success, no movement and the Reflex save (or a fall, by 5 or more) on a failure; the Reflex save repeats on the
    /// caster's turns (PHB p.237, p.67). Walls and their corners block the path cost, the move highlight and the 5-foot
    /// step as they block the A* (SquareGridUtils.IsStepBlockedByBarrier, PHB p.298-299).
    /// </summary>
    public static class MovementStepHookScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("grease-crossing-ai", GreaseCrossingAi);
            yield return S("grease-balance-half-speed-ui", GreaseBalanceHalfSpeedUi);
            yield return S("grease-caster-turn-save", GreaseCasterTurnSave);
            yield return S("five-foot-wall", FiveFootWall);
            yield return S("wall-of-force-path-cost", WallOfForcePathCost);
            yield return S("five-foot-step-out-of-grease", FiveFootStepOutOfGrease);
            yield return S("grease-balance-before-aoo", GreaseBalanceBeforeAoO);
            yield return S("zone-entry-stops-mover", ZoneEntryStopsMover);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("MovementStepHookScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title)
            => Scenario.Define(id, title).Tags("rules", "movement").Covers("PC_NPC_PARITY", "batch 12 step 23");

        private const string FactPrefix = "fact:";

        private static void Fact(ScenarioContext ctx, string key, object value) => ctx.Note(FactPrefix + key + "=" + value);

        private static string FactOf(TraceView v, string key)
        {
            string prefix = FactPrefix + key + "=";
            TraceEvent note = v.Of("note").LastOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
            return note != null ? note.Str("text").Substring(prefix.Length) : null;
        }

        private static Func<TraceView, ExpectResult> FactIs(string key, object want)
            => v =>
            {
                string got = FactOf(v, key);
                string w = Convert.ToString(want, System.Globalization.CultureInfo.InvariantCulture);
                if (got == null)
                    return ExpectResult.Inconclusive("no fact " + key);
                return got == w ? ExpectResult.Pass(key + "=" + got) : ExpectResult.Fail(key + "=" + got + ", expected " + w);
            };

        private static Func<TraceView, ExpectResult> FactsAre(params (string key, object want)[] facts)
            => Expect.All(facts.Select(f => FactIs(f.key, f.want)).ToArray());

        /// <summary>The d20 events whose context contains <paramref name="ctx"/>, in <paramref name="round"/> (null: any).</summary>
        private static List<TraceEvent> Dice(TraceView v, string ctx, int? round = null)
            => v.Of("dice").Where(e => (e.Str("ctx") ?? "").Contains(ctx) && (round == null || e.Round == round.Value)).ToList();

        private static Func<TraceView, ExpectResult> DiceCount(string ctx, int? round, int want, string what)
            => v =>
            {
                List<TraceEvent> d = Dice(v, ctx, round);
                return d.Count == want
                    ? ExpectResult.Pass(d.Count + " " + what, d.Select(e => e.Seq).ToArray())
                    : ExpectResult.Fail(d.Count + " " + what + ", expected " + want, d.Select(e => e.Seq).ToArray());
            };

        private static string P(Vector2Int p) => p.x + "," + p.y;

        /// <summary>A Human wizard 3 (INT 16) with its kit; the caster of the scenario's grease.</summary>
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

        /// <summary>Puts a clone of <paramref name="spellId"/> into the first unused non-domain slot of <paramref name="level"/>.</summary>
        private static void Prepare(CharacterController c, int level, string spellId)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spellcasting to prepare " + spellId + " in");
            foreach (SpellSlot slot in sc.SpellSlots)
            {
                if (slot == null || slot.Level != level || slot.IsDomainSlot || slot.IsUsed)
                    continue;
                slot.PreparedSpell = SpellDatabase.GetSpell(spellId).Clone();
                return;
            }
            throw new InvalidOperationException(c.Stats.CharacterName + " has no free level-" + level + " slot for " + spellId);
        }

        /// <summary>
        /// A grease band two squares wide (x 12-13) and six tall (y 8-13), so the only short way east from (10,10) crosses it:
        /// one Grease area with that footprint, cast by <paramref name="casterKey"/> (GameManager.CreateGreaseArea, the
        /// cast's own builder). It takes effect at its Start, a frame later: wait before moving.
        /// </summary>
        private static bool PlaceGreaseBand(ScenarioContext ctx, string casterKey)
        {
            var cells = new HashSet<Vector2Int>();
            for (int x = 12; x <= 13; x++)
                for (int y = 8; y <= 13; y++)
                    cells.Add(new Vector2Int(x, y));
            CharacterController caster = ctx.Get(casterKey);
            ctx.Gm.CreateGreaseArea(SquareGridUtils.GridToWorld(new Vector2Int(12, 10)), 10, 11, caster,
                SpellDatabase.GetSpell(SpellNames.GREASE), cells);
            return true;
        }

        private static bool GreaseBandActive(ScenarioContext ctx)
        {
            List<GreaseAreaEffect> greases = AreaEffectManager.Instance.GetEffectsOfType<GreaseAreaEffect>();
            return greases.Count == 1 && greases[0].IsCellInArea(new Vector2Int(12, 10)) && greases[0].IsCellInArea(new Vector2Int(13, 13))
                && ctx.Gm.IsDifficultTerrain(new Vector2Int(13, 10));
        }

        /// <summary>
        /// The square a move east from (10,10) along y 10 reaches with <paramref name="speed"/> squares when (12,10) and
        /// (13,10) are greased (difficult terrain: 2 squares each, PHB p.148, p.237): costs 1, 3, 5, 6, 7 to x 11-15.
        /// </summary>
        private static int ReachEastThroughGrease(int speed)
            => speed >= 7 ? 15 : speed >= 6 ? 14 : speed >= 5 ? 13 : speed >= 3 ? 12 : speed >= 1 ? 11 : 10;

        /// <summary>
        /// A Wall of Force on <paramref name="cells"/> (PHB p.298), as the spell's handler builds it, in force at once (Start
        /// recomputes the same squares on the next frame).
        /// </summary>
        private static bool PlaceWallOfForce(params Vector2Int[] cells)
        {
            var go = new GameObject("WallOfForce_Line_Area");
            go.transform.position = SquareGridUtils.GridToWorld(cells[0]);
            WallOfForceAreaEffect wall = go.AddComponent<WallOfForceAreaEffect>();
            var set = new HashSet<Vector2Int>(cells);
            wall.CenterPosition = go.transform.position;
            wall.CenterCell = cells[0];
            wall.LengthSquares = Mathf.Max(2, cells.Length);
            wall.RoundsRemaining = 10;
            wall.CasterLevel = 9;
            wall.SetExplicitCells(set);
            wall.AffectedCells.UnionWith(set);
            AreaEffectManager.Instance.RegisterAreaEffect(wall);
            return cells.All(WallOfForceAreaEffect.DoesCellBlockMovement);
        }

        private static List<Vector2Int> Straight(Vector2Int from, Vector2Int to)
        {
            var path = new List<Vector2Int>();
            Vector2Int p = from;
            while (p != to)
            {
                p = new Vector2Int(p.x + Math.Sign(to.x - p.x), p.y + Math.Sign(to.y - p.y));
                path.Add(p);
            }
            return path;
        }

        // ── rules/grease-crossing-ai ───────────────────────────────────

        /// <summary>
        /// GRID-018, D12 (PHB p.237, p.67): a Scripted orc (the NPC movement executor, ExecutePathWithMovementAoOs) moves
        /// east across a grease band. Round 1: its Balance check (forced 18) succeeds on its first greased step, and the two
        /// greased squares cost 2 each (half speed), so it reaches (14,10), not (15,10). Round 2 it moves back: a new round's
        /// check, forced to a natural 1, fails by 5 or more, so it cannot move, does not enter the grease and falls prone.
        /// Before the fix an AI mover crossed grease with no check and at full speed (only the PC Move had a hook).
        /// </summary>
        private static ScenarioDef GreaseCrossingAi()
        {
            return Rules("rules/grease-crossing-ai", "An AI mover makes Grease's Balance check, moves at half speed in it, and a failure by 5 or more stops and fells it (PHB p.237; GRID-018, D12)")
                .Covers("GRID-018", "D12", "PHB p.237", "PHB p.67")
                .MaxRounds(2)
                .RecordDice()
                .Npc("orc", "orc_warrior", 10, 10, Control.Scripted)
                .Pc("wiz", ActorSource.Stats(() => Wizard("Grease Wizard", 3)), 2, 2, Control.Idle)
                .Force(20, 18, GreaseAreaEffect.BalanceDiceContext, 1)
                .Force(20, 1, GreaseAreaEffect.BalanceDiceContext, 1)
                .Initiative("orc", "wiz")
                .Turn("orc", 1,
                    Step.Assert("the grease band is laid", ctx => PlaceGreaseBand(ctx, "wiz")),
                    Step.Wait(3),
                    Step.Assert("the grease band is in force and difficult terrain", GreaseBandActive),
                    Step.Assert("speed", ctx =>
                    {
                        int speed = MovementService.SingleMoveBudgetSquares(ctx.Get("orc"));
                        Fact(ctx, "expect.x", ReachEastThroughGrease(speed));
                        return speed >= 1;
                    }),
                    Step.Move(15, 10),
                    Step.Assert("after the crossing", ctx =>
                    {
                        CharacterController o = ctx.Get("orc");
                        Fact(ctx, "r1.pos", P(o.GridPosition));
                        Fact(ctx, "r1.prone", o.HasCondition(CombatConditionType.Prone));
                        return true;
                    }))
                .Turn("orc", 2,
                    Step.Move(10, 10),
                    Step.Assert("after the failed check", ctx =>
                    {
                        CharacterController o = ctx.Get("orc");
                        Fact(ctx, "r2.pos", P(o.GridPosition));
                        Fact(ctx, "r2.prone", o.HasCondition(CombatConditionType.Prone));
                        return true;
                    }))
                .Expect("Round 1: one Balance check, on the first greased step (PHB p.237)", DiceCount(GreaseAreaEffect.BalanceDiceContext, 1, 1, "Balance checks"))
                .Expect("Round 1: the orc crosses at half speed in the grease and stands", v =>
                {
                    string want = FactOf(v, "expect.x");
                    return FactsAre(("r1.pos", want + ",10"), ("r1.prone", false))(v);
                })
                .Expect("Round 2: a new round's check (natural 1) fails by 5 or more: no movement and a fall (PHB p.237)", Expect.All(
                    DiceCount(GreaseAreaEffect.BalanceDiceContext, 2, 1, "Balance checks"),
                    v => FactsAre(("r2.pos", FactOf(v, "r1.pos")), ("r2.prone", true))(v)))
                .Expect("The fall needs no Reflex save (failure by 5 or more)", DiceCount(GreaseAreaEffect.ReflexDiceContext, null, 0, "Grease Reflex saves"))
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── rules/grease-balance-half-speed-ui ─────────────────────────

        /// <summary>
        /// D12, GRID-013 (PHB p.237, p.148): a Ui fighter (speed 30 ft) clicks (15,10) across the grease band. Its Balance
        /// check (forced 18) succeeds, and the PC move executor counts the greased squares at 2 each through the one step cost
        /// (SquareGridUtils.StepCost), so the move ends at (14,10). Its second move this round, back into the grease to
        /// (12,10), makes no second check: the check covers the round. Before the fix the PC rolled a Reflex save on entering
        /// the grease (no such save in RAW) and a second one at the next round start.
        /// </summary>
        private static ScenarioDef GreaseBalanceHalfSpeedUi()
        {
            return Rules("rules/grease-balance-half-speed-ui", "A PC's Balance check in grease covers the round and greased squares cost double (PHB p.237, p.148; D12, GRID-013)")
                .Covers("D12", "GRID-013", "GRID-018", "PHB p.237", "PHB p.148")
                .MaxRounds(1)
                .RecordDice()
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 2)), 10, 10, Control.Ui)
                .Pc("wiz", ActorSource.Stats(() => Wizard("Grease Wizard", 3)), 2, 2, Control.Idle)
                .Npc("orc", "orc_warrior", 2, 18, Control.Idle)
                .Force(20, 18, GreaseAreaEffect.BalanceDiceContext, 1)
                // The grease's caster acts first, so its turn (with Grease's repeated save, D12) comes before the band exists.
                .Initiative("wiz", "hero", "orc")
                .Turn("hero", 1,
                    Step.Assert("the grease band is laid", ctx => PlaceGreaseBand(ctx, "wiz")),
                    Step.Wait(3),
                    Step.Assert("the grease band is in force and difficult terrain", GreaseBandActive),
                    Step.Assert("the hero's speed is 6 squares", ctx => MovementService.SingleMoveBudgetSquares(ctx.Get("hero")) == 6),
                    Step.Assert("the one path cost counts the greased squares double", ctx =>
                        SquareGridUtils.CalculatePathCost(new Vector2Int(10, 10), Straight(new Vector2Int(10, 10), new Vector2Int(15, 10))) == 7),
                    Step.Move(15, 10),
                    Step.Assert("after the first move", ctx => { Fact(ctx, "m1.pos", P(ctx.Get("hero").GridPosition)); return true; }),
                    Step.Move(12, 10),
                    Step.Assert("after the second move", ctx =>
                    {
                        CharacterController h = ctx.Get("hero");
                        Fact(ctx, "m2.pos", P(h.GridPosition));
                        Fact(ctx, "prone", h.HasCondition(CombatConditionType.Prone));
                        return true;
                    }))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The first move ends at (14,10): the greased squares cost 2 each (PHB p.148)", FactIs("m1.pos", "14,10"))
                .Expect("The second move goes back into the grease with no new check", FactsAre(("m2.pos", "12,10"), ("prone", false)))
                .Expect("One Balance check covers the round's movement (PHB p.67, p.237)", DiceCount(GreaseAreaEffect.BalanceDiceContext, 1, 1, "Balance checks"))
                .Expect("No Reflex save for entering the grease (D12)", DiceCount(GreaseAreaEffect.ReflexDiceContext, null, 0, "Grease Reflex saves"))
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── rules/grease-caster-turn-save ──────────────────────────────

        /// <summary>
        /// D12 (PHB p.237: "repeated on your turn", read as the caster's): a Ui wizard greases the 10-ft square (9-10,10-11)
        /// on an idle goblin. The goblin saves at casting (forced natural 20). A Scripted orc then walks into the grease to
        /// (10,11) (its Balance check forced to 18). At the start of the wizard's round-2 turn the goblin saves again (forced
        /// natural 1) and falls prone; the orc, which was not in the area at casting, rolls no save (PHB p.237: the repeat is
        /// for the creature that was in the area when the spell was cast). Nothing is rolled at the round start or on the
        /// goblin's own turn: standing in grease takes no Balance check (the old round-start check is gone).
        /// </summary>
        private static ScenarioDef GreaseCasterTurnSave()
        {
            return Rules("rules/grease-caster-turn-save", "Grease's Reflex save repeats on the caster's turn, and standing in it takes no Balance check (PHB p.237; D12)")
                .Covers("D12", "GRID-018", "PHB p.237")
                .MaxRounds(2)
                .RecordDice()
                .Pc("wizard", ActorSource.Stats(() => Wizard("Grease Wizard", 3)), 5, 10, Control.Ui)
                .Npc("gob", "goblin", 9, 10, Control.Idle)
                .Npc("orc", "orc_warrior", 12, 11, Control.Scripted)
                .Tweak("wizard", c => Prepare(c, 1, SpellNames.GREASE))
                .Force(20, 20, GreaseAreaEffect.ReflexDiceContext, 1)
                .Force(20, 1, GreaseAreaEffect.ReflexDiceContext, 1)
                .Force(20, 18, GreaseAreaEffect.BalanceDiceContext, 1)
                .Initiative("wizard", "orc", "gob")
                .Turn("wizard", 1,
                    Step.CastAt(SpellNames.GREASE, 9, 10, "Grease Area"),
                    Step.Wait(3),
                    Step.Assert("after the cast", ctx => { Fact(ctx, "r1.prone", ctx.Get("gob").HasCondition(CombatConditionType.Prone)); return true; }))
                .Turn("orc", 1,
                    Step.Move(10, 11),
                    Step.Assert("the orc stands in the grease", ctx =>
                    {
                        CharacterController o = ctx.Get("orc");
                        List<GreaseAreaEffect> g = AreaEffectManager.Instance.GetEffectsOfType<GreaseAreaEffect>();
                        Fact(ctx, "orc.in", g.Count == 1 && g[0].IsCharacterInArea(o));
                        return true;
                    }))
                .Turn("wizard", 2,
                    Step.Assert("at the wizard's round-2 turn", ctx =>
                    {
                        Fact(ctx, "r2.prone", ctx.Get("gob").HasCondition(CombatConditionType.Prone));
                        Fact(ctx, "r2.orc.prone", ctx.Get("orc").HasCondition(CombatConditionType.Prone));
                        return true;
                    }),
                    Step.Pass())
                .Turn("orc", 2, Step.Pass())
                .Expect("The goblin saves at casting (natural 20) and stands", Expect.All(
                    DiceCount(GreaseAreaEffect.ReflexDiceContext, 1, 1, "Grease Reflex saves in round 1"),
                    FactIs("r1.prone", false)))
                .Expect("It saves again at the start of the caster's round-2 turn (natural 1) and falls", Expect.All(
                    DiceCount(GreaseAreaEffect.ReflexDiceContext, 2, 1, "Grease Reflex saves in round 2"),
                    FactIs("r2.prone", true)))
                .Expect("The orc that walked in after the cast makes its Balance check and no repeated save (D12)", Expect.All(
                    FactsAre(("orc.in", true), ("r2.orc.prone", false)),
                    DiceCount(GreaseAreaEffect.BalanceDiceContext, 1, 1, "Balance checks in round 1")))
                .Expect("Standing in grease takes no Balance check (D12)", DiceCount(GreaseAreaEffect.BalanceDiceContext, 2, 0, "Balance checks in round 2"))
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── rules/five-foot-wall ───────────────────────────────────────

        /// <summary>
        /// GRID-013 (PHB p.144, p.298): a Wall of Force square stands east of a Scripted orc and of a Ui fighter. The 5-foot
        /// step into the wall square and the diagonal one past its corner are refused for both (the shared step rule,
        /// SquareGridUtils.IsStepBlockedByBarrier, in MovementService.IsValidAdjacentStepDestination and
        /// CharacterController.FiveFootStep); a step away from the wall is taken. Before the fix the 5-foot step checked
        /// only Wall of Ice squares, so a creature could step into a Wall of Force.
        /// </summary>
        private static ScenarioDef FiveFootWall()
        {
            return Rules("rules/five-foot-wall", "No 5-foot step into a wall square or past its corner, PC or NPC (PHB p.144, p.298; GRID-013)")
                .Covers("GRID-013", "PHB p.144", "PHB p.298")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 10, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 2)), 10, 4, Control.Ui)
                .Initiative("orc", "hero")
                .Turn("orc", 1,
                    Step.Assert("the walls stand", ctx => PlaceWallOfForce(new Vector2Int(11, 10), new Vector2Int(11, 4))),
                    Step.Assert("the shared step rule refuses the wall square and its corner", ctx =>
                    {
                        CharacterController o = ctx.Get("orc");
                        return !ctx.Gm.CanTakeFiveFootStepToForAI(o, new Vector2Int(11, 10))
                            && !ctx.Gm.CanTakeFiveFootStepToForAI(o, new Vector2Int(11, 11))
                            && ctx.Gm.CanTakeFiveFootStepToForAI(o, new Vector2Int(9, 10));
                    }),
                    Step.FiveFootStep(11, 10),
                    Step.FiveFootStep(11, 11),
                    Step.FiveFootStep(9, 10))
                .Turn("hero", 1,
                    Step.FiveFootStep(11, 4),
                    Step.FiveFootStep(11, 5),
                    Step.FiveFootStep(9, 4))
                .Expect("The orc's steps into the wall and past its corner are refused, the step away taken", Expect.All(
                    Expect.StepStatus("orc", 1, "FiveFootStep", 0, "refused"),
                    Expect.StepStatus("orc", 1, "FiveFootStep", 1, "refused"),
                    Expect.StepStatus("orc", 1, "FiveFootStep", 2, "done")))
                .Expect("The hero's steps into the wall and past its corner are refused, the step away taken", Expect.All(
                    Expect.StepStatus("hero", 1, "FiveFootStep", 0, "refused"),
                    Expect.StepStatus("hero", 1, "FiveFootStep", 1, "refused"),
                    Expect.StepStatus("hero", 1, "FiveFootStep", 2, "done")))
                .Expect("Both end west of their wall", v =>
                {
                    Vector2Int? o = v.Final("orc") != null && v.Final("orc").Get("pos") is Vector2Int a ? a : (Vector2Int?)null;
                    Vector2Int? h = v.Final("hero") != null && v.Final("hero").Get("pos") is Vector2Int b ? b : (Vector2Int?)null;
                    return o == new Vector2Int(9, 10) && h == new Vector2Int(9, 4)
                        ? ExpectResult.Pass("orc (9,10), hero (9,4)") : ExpectResult.Fail("orc " + o + ", hero " + h);
                })
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── rules/wall-of-force-path-cost ──────────────────────────────

        /// <summary>
        /// GRID-013 (PHB p.298, p.147-148): a Wall of Force line at x 12, y 4-16, stands between a Scripted orc at (10,10)
        /// and (14,10). The one path cost (SquareGridUtils.CalculatePathCost, the A* trim, MovementService.GetMovementCost)
        /// gives the straight path through the wall the blocked cost, the move highlight leaves the wall squares out, and the
        /// path the orc is given, and its move, never enter a wall square or cut a wall's corner. Before the fix the path cost
        /// counted the straight line at 4 and the highlight left out only Wall of Ice squares.
        /// </summary>
        private static ScenarioDef WallOfForcePathCost()
        {
            Vector2Int[] wallCells = Enumerable.Range(4, 13).Select(y => new Vector2Int(12, y)).ToArray();
            return Rules("rules/wall-of-force-path-cost", "Path cost, the move highlight and the move itself respect a Wall of Force (PHB p.298; GRID-013)")
                .Covers("GRID-013", "PHB p.298", "PHB p.147-148")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 10, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 2)), 2, 2, Control.Idle)
                .Initiative("orc", "hero")
                .Turn("orc", 1,
                    Step.Assert("the wall stands", ctx => PlaceWallOfForce(wallCells)),
                    Step.Assert("the path cost through the wall is the blocked cost", ctx =>
                        SquareGridUtils.CalculatePathCost(new Vector2Int(10, 10), Straight(new Vector2Int(10, 10), new Vector2Int(14, 10))) == SquareGridUtils.BlockedPathCost
                        && ctx.Gm.GetMovementCost(new Vector2Int(10, 10), Straight(new Vector2Int(10, 10), new Vector2Int(14, 10))) == SquareGridUtils.BlockedPathCost),
                    Step.Assert("the path cost of a diagonal past the wall's corner is the blocked cost", ctx =>
                        SquareGridUtils.CalculatePathCost(new Vector2Int(11, 3), new List<Vector2Int> { new Vector2Int(12, 2) }) == 1
                        && SquareGridUtils.CalculatePathCost(new Vector2Int(11, 4), new List<Vector2Int> { new Vector2Int(12, 3) }) == SquareGridUtils.BlockedPathCost),
                    Step.Assert("the path never enters or cuts the wall", ctx =>
                    {
                        CharacterController o = ctx.Get("orc");
                        AoOPathResult r = ctx.Gm.FindPath(o, new Vector2Int(14, 10), avoidThreats: false, maxRangeOverride: 20);
                        if (r == null || r.Path == null || r.Path.Count == 0)
                            return false;
                        Vector2Int prev = o.GridPosition;
                        foreach (Vector2Int step in r.Path)
                        {
                            if (SquareGridUtils.IsStepBlockedByBarrier(prev, step))
                                return false;
                            prev = step;
                        }
                        Fact(ctx, "path.end", P(r.Path[r.Path.Count - 1]));
                        return true;
                    }),
                    Step.Assert("the step executor walks a path through the wall", ctx =>
                    {
                        CharacterController o = ctx.Get("orc");
                        ctx.Gm.StartCoroutine(o.MoveAlongPath(Straight(new Vector2Int(10, 10), new Vector2Int(14, 10)), 0.01f, markAsMoved: false));
                        return true;
                    }),
                    Step.Wait(20),
                    Step.Assert("after the walk", ctx => { Fact(ctx, "walk.pos", P(ctx.Get("orc").GridPosition)); return true; }),
                    Step.Move(14, 10),
                    Step.Assert("after the move", ctx =>
                    {
                        Vector2Int p = ctx.Get("orc").GridPosition;
                        Fact(ctx, "pos.wall", wallCells.Contains(p));
                        Fact(ctx, "pos.east", p.x > 12);
                        return true;
                    }))
                .Expect("The step executor (MoveAlongPath) stops at the wall: the walk east ends at (11,10)", FactIs("walk.pos", "11,10"))
                .Expect("The orc never stands in the wall and cannot get round it in one move (speed 30 ft)", FactsAre(("pos.wall", false), ("pos.east", false)))
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── rules/five-foot-step-out-of-grease ─────────────────────────

        /// <summary>
        /// PHB p.144, p.148 (review of batch 12 step 23): a Scripted orc at (12,10) and a Ui fighter at (13,12) stand in the
        /// grease band (difficult terrain; their saves at casting forced to natural 20). Neither may take a 5-foot step out
        /// of it: the step needs movement unhampered by difficult terrain, and a creature moves only as fast as the most
        /// difficult terrain it occupies (MovementService.IsValidAdjacentStepDestination, CharacterController.FiveFootStep).
        /// The refused step makes no Balance check.
        /// </summary>
        private static ScenarioDef FiveFootStepOutOfGrease()
        {
            return Rules("rules/five-foot-step-out-of-grease", "No 5-foot step out of grease, PC or NPC, and no Balance check for it (PHB p.144, p.148, p.237)")
                .Covers("GRID-013", "D12", "PHB p.144", "PHB p.148")
                .MaxRounds(1)
                .RecordDice()
                .Npc("orc", "orc_warrior", 12, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 2)), 13, 12, Control.Ui)
                .Pc("wiz", ActorSource.Stats(() => Wizard("Grease Wizard", 3)), 2, 2, Control.Idle)
                .Force(20, 20, GreaseAreaEffect.ReflexDiceContext, 2)
                .Initiative("orc", "hero", "wiz")
                .Turn("orc", 1,
                    Step.Assert("the grease band is laid", ctx => PlaceGreaseBand(ctx, "wiz")),
                    Step.Wait(3),
                    Step.Assert("the grease band is in force and difficult terrain", GreaseBandActive),
                    Step.Assert("the shared step rule refuses a step out of the grease", ctx =>
                        !ctx.Gm.CanTakeFiveFootStepToForAI(ctx.Get("orc"), new Vector2Int(11, 10))),
                    Step.FiveFootStep(11, 10))
                .Turn("hero", 1,
                    Step.FiveFootStep(14, 12))
                .Expect("The orc's 5-foot step out of the grease is refused", Expect.StepStatus("orc", 1, "FiveFootStep", 0, "refused"))
                .Expect("The hero's 5-foot step out of the grease is refused", Expect.StepStatus("hero", 1, "FiveFootStep", 0, "refused"))
                .Expect("Both stay in the grease", v =>
                {
                    Vector2Int? o = v.Final("orc") != null && v.Final("orc").Get("pos") is Vector2Int a ? a : (Vector2Int?)null;
                    Vector2Int? h = v.Final("hero") != null && v.Final("hero").Get("pos") is Vector2Int b ? b : (Vector2Int?)null;
                    return o == new Vector2Int(12, 10) && h == new Vector2Int(13, 12)
                        ? ExpectResult.Pass("orc (12,10), hero (13,12)") : ExpectResult.Fail("orc " + o + ", hero " + h);
                })
                .Expect("No Balance check for a refused step", DiceCount(GreaseAreaEffect.BalanceDiceContext, null, 0, "Balance checks"))
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── rules/grease-balance-before-aoo ────────────────────────────

        /// <summary>
        /// PHB p.137, p.237 (review of batch 12 step 23): a Scripted orc at (11,10), threatened by an idle fighter at (10,10),
        /// and a Ui fighter at (11,12), threatened by an idle orc at (10,12), each move east into the grease band. Their
        /// Balance checks (forced natural 1) fail, so neither can move and neither leaves its threatened square: no attack
        /// of opportunity. The step is refused before its AoOs resolve (MovementService.IsStepRefused in every mover loop).
        /// Before the fix the AoO came first and then the Balance check stopped the mover.
        /// </summary>
        private static ScenarioDef GreaseBalanceBeforeAoO()
        {
            return Rules("rules/grease-balance-before-aoo", "A failed Balance check in grease stops the mover before it provokes, PC or NPC (PHB p.137, p.237; D12)")
                .Covers("D12", "GRID-018", "PHB p.137", "PHB p.237")
                .MaxRounds(1)
                .RecordDice()
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Pc("guard", ActorSource.Stats(() => RulesScenarios.Fighter("Guard", 2)), 10, 10, Control.Idle)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 2)), 11, 12, Control.Ui)
                .Npc("brute", "orc_warrior", 10, 12, Control.Idle)
                .Pc("wiz", ActorSource.Stats(() => Wizard("Grease Wizard", 3)), 2, 2, Control.Idle)
                .Force(20, 1, GreaseAreaEffect.BalanceDiceContext, 2)
                .Initiative("orc", "hero", "guard", "brute", "wiz")
                .Turn("orc", 1,
                    Step.Assert("the grease band is laid", ctx => PlaceGreaseBand(ctx, "wiz")),
                    Step.Wait(3),
                    Step.Assert("the grease band is in force and difficult terrain", GreaseBandActive),
                    Step.Assert("the orc's move east would provoke from the guard", ctx =>
                        ctx.Gm.CheckForAoO(ctx.Get("orc"), Straight(new Vector2Int(11, 10), new Vector2Int(14, 10))).Count > 0),
                    Step.Move(14, 10),
                    Step.Assert("after the orc's move", ctx => { Fact(ctx, "orc.pos", P(ctx.Get("orc").GridPosition)); return true; }))
                .Turn("hero", 1,
                    Step.Assert("the hero's move east would provoke from the brute", ctx =>
                        ctx.Gm.CheckForAoO(ctx.Get("hero"), Straight(new Vector2Int(11, 12), new Vector2Int(14, 12))).Count > 0),
                    Step.Move(14, 12),
                    Step.Assert("after the hero's move", ctx => { Fact(ctx, "hero.pos", P(ctx.Get("hero").GridPosition)); return true; }))
                .Expect("Each mover makes one Balance check, which fails", DiceCount(GreaseAreaEffect.BalanceDiceContext, 1, 2, "Balance checks"))
                .Expect("Neither leaves its square", FactsAre(("orc.pos", "11,10"), ("hero.pos", "11,12")))
                .Expect("No attack of opportunity: neither mover left a threatened square (PHB p.137)", Expect.None("aoo", e => true))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── rules/zone-entry-stops-mover ───────────────────────────────

        /// <summary>
        /// GRID-018 (PHB p.228; review of batch 12 step 23): Evard's Black Tentacles on x 16-19, y 9-13. A Scripted bugbear
        /// at (12,10) and a Ui fighter at (12,13) each move 30 ft east, to x 18. The tentacles seize each on its first square
        /// of the area (their grapple checks forced: tentacles 20, creature 1), and the move ends there (x 16): a mover held
        /// in place takes no further step (MovementHaltStepHook, MovementService.StepHaltReason), whether the PC move
        /// executor walks one square per call or the NPC executor walks the whole path, and whether the area's own per-frame
        /// tracking or the step's zone entry ran the grapple. Before the fix both walked on out through the area.
        /// </summary>
        private static ScenarioDef ZoneEntryStopsMover()
        {
            var cells = new HashSet<Vector2Int>();
            for (int x = 16; x <= 19; x++)
                for (int y = 9; y <= 13; y++)
                    cells.Add(new Vector2Int(x, y));
            return Rules("rules/zone-entry-stops-mover", "A creature seized by an area as it enters stops there, PC or NPC (PHB p.228; GRID-018)")
                .Covers("GRID-018", "PHB p.228")
                .MaxRounds(1)
                .RecordDice()
                .Npc("brute", "bugbear", 12, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 12, 13, Control.Ui)
                .Pc("wiz", ActorSource.Stats(() => Wizard("Tentacle Wizard", 7)), 2, 2, Control.Idle)
                .Force(20, 20, BlackTentaclesAreaEffect.TentacleGrappleDiceContext, 2)
                .Force(20, 1, BlackTentaclesAreaEffect.CreatureGrappleDiceContext, 2)
                .Initiative("brute", "hero", "wiz")
                .Turn("brute", 1,
                    Step.Assert("the tentacles rise", ctx =>
                    {
                        var go = new GameObject("BlackTentacles_Area");
                        Vector3 center = SquareGridUtils.GridToWorld(new Vector2Int(17, 11));
                        go.transform.position = center;
                        BlackTentaclesAreaEffect t = go.AddComponent<BlackTentaclesAreaEffect>();
                        t.CenterPosition = center;
                        t.CastFootprint = new HashSet<Vector2Int>(cells);
                        t.RoundsRemaining = 7;
                        t.CasterLevel = 7;
                        t.Caster = ctx.Get("wiz");
                        return true;
                    }),
                    Step.Wait(3),
                    Step.Assert("the tentacles are in force", ctx =>
                    {
                        List<BlackTentaclesAreaEffect> t = AreaEffectManager.Instance.GetEffectsOfType<BlackTentaclesAreaEffect>();
                        return t.Count == 1 && t[0].IsCellInArea(new Vector2Int(16, 10)) && !t[0].IsCellInArea(new Vector2Int(15, 10));
                    }),
                    Step.Assert("speed 30 ft", ctx => MovementService.SingleMoveBudgetSquares(ctx.Get("brute")) >= 6),
                    Step.Move(18, 10),
                    Step.Assert("after the bugbear's move", ctx =>
                    {
                        CharacterController b = ctx.Get("brute");
                        Fact(ctx, "brute.pos", P(b.GridPosition));
                        Fact(ctx, "brute.held", BlackTentaclesAreaEffect.IsCharacterGrappledByAnyTentacles(b));
                        return true;
                    }))
                .Turn("hero", 1,
                    Step.Move(18, 13),
                    Step.Assert("after the hero's move", ctx =>
                    {
                        CharacterController h = ctx.Get("hero");
                        Fact(ctx, "hero.pos", P(h.GridPosition));
                        Fact(ctx, "hero.held", BlackTentaclesAreaEffect.IsCharacterGrappledByAnyTentacles(h));
                        return true;
                    }))
                .Expect("The bugbear (NPC executor) stops on its first tentacle square, held", FactsAre(("brute.pos", "16,10"), ("brute.held", true)))
                .Expect("The hero (PC executor) stops on its first tentacle square, held", FactsAre(("hero.pos", "16,13"), ("hero.held", true)))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }
    }
}
#endif
