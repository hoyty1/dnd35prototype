#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 12 step 22 (docs/designs/pc_npc_parity_batch12.md; GRID-019, GRID-022): every mover passes the same movement
    /// legality (MovementService.CanStartMovement). The NPC executors refuse a move after a 5-foot step and a 5-foot step
    /// after a move (PHB p.144); a creature limited to a single action withdraws as a standard action at its speed
    /// (Restricted Withdraw, PHB p.144), through the PC button and on its own as an AI; a blinded AI creature does not
    /// withdraw (PHB p.143).
    /// </summary>
    public static class MovementLegalityScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("ai-no-5ft-then-move", NoFiveFootStepThenMove);
            yield return S("restricted-withdraw-disabled-ui", RestrictedWithdrawDisabledUi);
            yield return S("restricted-withdraw-disabled-ai", RestrictedWithdrawDisabledAi);
            yield return S("blinded-no-withdraw-ai", BlindedNoWithdrawAi);
            yield return S("movement-step-action-converts-standard-ui", StepActionMoveConvertsStandardUi);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("MovementLegalityScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title)
            => Scenario.Define(id, title).Tags("rules", "movement").Covers("PC_NPC_PARITY", "batch 12 step 22", "PHB p.143-144");

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(200);
            c.Stats.CurrentHP += 200;
        }

        /// <summary>Empties the backpack (the hobgoblin's javelins and potion), so the AI neither throws nor drinks.</summary>
        private static void EmptyBackpack(CharacterController c)
        {
            InventoryComponent inv = c.GetComponent<InventoryComponent>();
            if (inv == null || inv.CharacterInventory == null || inv.CharacterInventory.GeneralSlots == null)
                return;
            ItemData[] slots = inv.CharacterInventory.GeneralSlots;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = null;
            inv.CharacterInventory.RecalculateStats();
        }

        private static Vector2Int? Pos(JsonObj snap) => snap != null && snap.Get("pos") is Vector2Int p ? p : (Vector2Int?)null;

        /// <summary>The creature's own movement events (move and path-move; a 5-foot step is a reposition) in a round.</summary>
        private static List<TraceEvent> OwnMoves(TraceView v, string key, int round)
            => v.Moves(key, round).Where(e => e.Str("type") == "move" || e.Str("type") == "path-move").ToList();

        private static JsonObj Econ(TraceView v, string key, int round)
        {
            TraceEvent te = v.Of("turn_end").FirstOrDefault(e => e.Str("actor") == key && e.Round == round);
            return te != null ? te.Get("econ") as JsonObj : null;
        }

        private static bool Flag(JsonObj econ, string name) => econ != null && econ.Get(name) is bool b && b;

        // ── GRID-019: no 5-foot step in a round with other movement ────

        /// <summary>
        /// GRID-019 (PHB p.144): a Scripted orc (the NPC turn path and its movement executors) takes a 5-foot step and then
        /// tries to move: the move executor (GameManager.ExecuteMoveAction) refuses it, so the orc stays in
        /// the square it stepped to. In round 2 it moves first and then tries a 5-foot step, which is refused. Before the
        /// fix the executor read only the move range, so an AI ranged kiter could step and then walk. A far-off idle hero
        /// keeps the fight going.
        /// </summary>
        private static ScenarioDef NoFiveFootStepThenMove()
        {
            return Rules("rules/ai-no-5ft-then-move", "An NPC takes no move after a 5-foot step and no 5-foot step after a move (PHB p.144; GRID-019)")
                .Covers("GRID-019", "PHB p.144")
                .MaxRounds(2)
                .Npc("orc", "orc_warrior", 10, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 2)), 2, 2, Control.Idle)
                .Initiative("orc", "hero")
                .Turn("orc", 1,
                    Step.FiveFootStep(11, 10),
                    Step.Assert("after the 5-foot step the shared legality refuses any other movement", ctx =>
                        !MovementService.CanStartMovement(ctx.Get("orc"), MoveKind.Move).Ok
                        && !MovementService.CanStartMovement(ctx.Get("orc"), MoveKind.Withdraw).Ok),
                    Step.Move(14, 10))
                .Turn("orc", 2,
                    Step.Move(13, 10),
                    Step.Assert("after the move the shared legality refuses a 5-foot step", ctx =>
                        !MovementService.CanStartMovement(ctx.Get("orc"), MoveKind.FiveFootStep).Ok),
                    Step.FiveFootStep(12, 10))
                .Expect("Round 1: the 5-foot step is done and the move after it refused", Expect.All(
                    Expect.StepStatus("orc", 1, "FiveFootStep", 0, "done"),
                    Expect.StepStatus("orc", 1, "Move", 0, "refused")))
                .Expect("Round 1: the orc makes no movement of its own after the step", v =>
                {
                    List<TraceEvent> own = OwnMoves(v, "orc", 1);
                    return own.Count == 0 ? ExpectResult.Pass("no own move") : ExpectResult.Fail(own.Count + " own move(s)", own[0].Seq);
                })
                .Expect("Round 2: the move is done and the 5-foot step after it refused", Expect.All(
                    Expect.StepStatus("orc", 2, "Move", 0, "done"),
                    Expect.StepStatus("orc", 2, "FiveFootStep", 0, "refused")))
                .Expect("The orc ends where its round-2 move took it, (13,10)", v =>
                {
                    Vector2Int? p = Pos(v.Final("orc"));
                    return p == new Vector2Int(13, 10) ? ExpectResult.Pass("at (13,10)") : ExpectResult.Fail("at " + p);
                })
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── GRID-022: Restricted Withdraw ──────────────────────────────

        /// <summary>
        /// GRID-022 (PHB p.144, p.145): a disabled Ui fighter (0 hp: a single move or standard action) next to an orc
        /// presses Withdraw. It is a Restricted Withdraw: a standard action up to its speed (not double), so the full-round
        /// withdraw is refused, the budget is one speed, and leaving the start square provokes nothing from the orc it can
        /// see (PHB p.143). Being a standard action it is strenuous, so after it the hero takes 1 damage (PHB p.145).
        /// Before the fix the Withdraw button needed a full-round action, so a disabled or slowed creature could not
        /// withdraw at all. An idle ally far off keeps the party in the fight.
        /// </summary>
        private static ScenarioDef RestrictedWithdrawDisabledUi()
        {
            var destination = new Vector2Int(14, 10);
            return Rules("rules/restricted-withdraw-disabled-ui", "A disabled PC withdraws as a standard action up to its speed: Restricted Withdraw (PHB p.144; GRID-022)")
                .Covers("GRID-022", "PHB p.144", "PHB p.145")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Ui)
                .Npc("orc", "orc_berserker", 9, 10, Control.Scripted)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Ally", 1)), 2, 2, Control.Idle)
                .Hp("hero", 0)
                .Tweak("hero", RulesScenarios.StripOffHand)
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero", "ally")
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 1,
                    Step.Assert("the disabled hero may only make a Restricted Withdraw, up to its speed (4 squares or more)", ctx =>
                    {
                        CharacterController h = ctx.Get("hero");
                        int speed = MovementService.SingleMoveBudgetSquares(h);
                        return h.Actions.SingleActionOnly && speed >= 4
                            && MovementService.WithdrawKindFor(h) == MoveKind.RestrictedWithdraw
                            && !MovementService.CanStartMovement(h, MoveKind.Withdraw).Ok
                            && MovementService.CanStartMovement(h, MoveKind.RestrictedWithdraw, speed).Ok
                            && !MovementService.CanStartMovement(h, MoveKind.RestrictedWithdraw, speed + 1).Ok
                            && ctx.Gm.GetWithdrawMoveRangeSquares(h) == speed
                            && string.IsNullOrEmpty(ctx.Gm.GetWithdrawDisabledReason(h));
                    }),
                    Step.Withdraw(destination.x, destination.y))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The withdraw is done and ends at (14,10)", v =>
                {
                    ExpectResult r = Expect.StepStatus("hero", 1, "Withdraw", 0, "done")(v);
                    if (!r.IsPass) return r;
                    Vector2Int? p = Pos(v.Final("hero"));
                    return p == destination ? ExpectResult.Pass("at (14,10)") : ExpectResult.Fail("at " + p);
                })
                .Expect("It is logged as a Restricted Withdraw", v =>
                    v.Log("Hero begins a Restricted Withdraw").Count == 1 ? ExpectResult.Pass("logged") : ExpectResult.Fail("no Restricted Withdraw line"))
                .Expect("Leaving the start square provokes nothing from the orc it can see (PHB p.143)", Expect.None("aoo", null))
                .Expect("The withdraw spent the standard action, not a full-round action", v =>
                {
                    JsonObj econ = Econ(v, "hero", 1);
                    if (econ == null) return ExpectResult.Fail("no turn end");
                    return Flag(econ, "std") && !Flag(econ, "full")
                        ? ExpectResult.Pass("standard spent, no full-round")
                        : ExpectResult.Fail("std " + Flag(econ, "std") + ", full " + Flag(econ, "full"));
                })
                .Expect("A standard action is strenuous for a disabled creature: 1 damage after it (PHB p.145)", v =>
                    v.Log(@"Hero takes 1 damage for acting while disabled").Count == 1
                        ? ExpectResult.Pass("logged once")
                        : ExpectResult.Fail(v.Log(@"Hero takes 1 damage for acting while disabled").Count + " log line(s)"))
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        /// <summary>
        /// GRID-022 (PHB p.144, p.145): an AI hobgoblin sergeant (DefensiveMelee: it withdraws below 30% of its hit points,
        /// AIService.ExecuteDefensiveMeleeTurn) at 0 hp next to a hero is disabled, limited to a single action. It withdraws
        /// on its own as a Restricted Withdraw: a standard action up to its speed (20 ft, 4 squares), never double, and
        /// leaving its start square provokes nothing. The act is strenuous: 1 damage after it (PHB p.145). Before the fix
        /// the AI's withdraw needed a full-round action, so a disabled or slowed creature never withdrew. Its backpack
        /// (javelins and a potion) is emptied so it neither throws nor drinks. An idle dummy far off keeps the enemy side in
        /// the fight once the hobgoblin is dying, so its turn end is traced.
        /// </summary>
        private static ScenarioDef RestrictedWithdrawDisabledAi()
        {
            return Rules("rules/restricted-withdraw-disabled-ai", "A disabled AI creature withdraws as a standard action up to its speed: Restricted Withdraw (PHB p.144; GRID-022)")
                .Covers("GRID-022", "PHB p.144", "PHB p.145")
                .MaxRounds(1)
                .Npc("hob", "hobgoblin_sergeant", 10, 10, Control.Ai)
                .Npc("far", "target_dummy", 18, 2, Control.Idle)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 9, 10, Control.Scripted)
                .Hp("hob", 0)
                .Tweak("hob", EmptyBackpack)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); })
                .Initiative("hero", "hob", "far")
                .Turn("hero", 1, Step.Pass())
                .Expect("The hobgoblin's turn is an AI turn", Expect.Controller("hob", "ai"))
                .Expect("It withdraws from the hero (log)", v =>
                    v.Log("Hobgoblin Sergeant withdraws from Hero").Count == 1 ? ExpectResult.Pass("logged") : ExpectResult.Fail("no withdraw line"))
                .Expect("It moves at least one square and at most its speed (4 squares), not double", v =>
                {
                    int moved = OwnMoves(v, "hob", 1).Sum(e => e.Int("sq"));
                    return moved >= 1 && moved <= 4 ? ExpectResult.Pass("moved " + moved) : ExpectResult.Fail("moved " + moved);
                })
                .Expect("Leaving the start square provokes nothing (PHB p.143)", Expect.None("aoo", null))
                .Expect("The withdraw spent its single standard action, not a full-round action", v =>
                {
                    JsonObj econ = Econ(v, "hob", 1);
                    if (econ == null) return ExpectResult.Fail("no turn end");
                    return Flag(econ, "std") && !Flag(econ, "full")
                        ? ExpectResult.Pass("standard spent, no full-round")
                        : ExpectResult.Fail("std " + Flag(econ, "std") + ", full " + Flag(econ, "full"));
                })
                .Expect("A standard action is strenuous for a disabled creature: 1 damage after it (PHB p.145)", v =>
                    v.Log(@"Hobgoblin Sergeant takes 1 damage for acting while disabled").Count == 1
                        ? ExpectResult.Pass("logged once")
                        : ExpectResult.Fail(v.Log(@"Hobgoblin Sergeant takes 1 damage for acting while disabled").Count + " log line(s)"))
                .Build();
        }

        // ── Batch 13 step 9: a Ui catalog move runs the PC's rule ──────

        /// <summary>
        /// Batch 13 step 9 (PC/NPC parity; PHB p.141: a standard action may be taken as a move action): a Ui fighter moves
        /// with the Move button (its move action), then runs the catalog's move entry itself (the harness's Ui Step.Action,
        /// with no MoveRequest on the context). The executor builds the PC's request (MoveActionExecutor.UiRequestFor,
        /// MoveRequest.ForUi): the standard action is converted to a second move before the walk. Before the fix a Ui
        /// context with no request ran the AI's request, which pays after the walk with no conversion, so the second move
        /// was free. A far-off idle orc keeps the fight going.
        /// </summary>
        private static ScenarioDef StepActionMoveConvertsStandardUi()
        {
            return Rules("rules/movement-step-action-converts-standard-ui", "A Ui catalog move with no request converts the standard action as the PC's move does (PHB p.141; batch 13 step 9)")
                .Covers("batch 13 step 9", "PHB p.141")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 2)), 10, 10, Control.Ui)
                .Npc("orc", "orc_warrior", 2, 18, Control.Idle)
                .Initiative("hero", "orc")
                .Turn("hero", 1,
                    Step.Move(13, 10),
                    Step.Assert("after the Move button the move action is spent and the standard action is unused", ctx =>
                    {
                        CharacterController h = ctx.Get("hero");
                        return h.Actions.MoveActionUsed && !h.Actions.StandardConvertedToMove && !h.Actions.StandardActionUsed;
                    }),
                    Step.ActionAt(ActionIds.Move, 16, 10))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The catalog move step is done", Expect.StepStatus("hero", 1, "Action(move", 0, "done"))
                .Expect("The hero's turn ends at (16,10), after both moves (the step waits for the walk)", v =>
                {
                    TraceEvent te = v.Of("turn_end").FirstOrDefault(e => e.Str("actor") == "hero" && e.Round == 1);
                    JsonObj hero = te != null && te.Get("diff") is List<JsonObj> diff ? diff.FirstOrDefault(d => d.Get("k") as string == "hero") : null;
                    Vector2Int? p = Pos(hero);
                    return p == new Vector2Int(16, 10) ? ExpectResult.Pass("at (16,10)") : ExpectResult.Fail("at " + p + " at the turn end");
                })
                .Expect("The hero moves 6 squares in its own turn", v =>
                {
                    int moved = v.Of("move").Where(e => e.Str("actor") == "hero" && e.Round == 1 && e.Turn == 1).Sum(e => e.Int("sq"));
                    return moved == 6 ? ExpectResult.Pass("6 squares") : ExpectResult.Fail(moved + " squares");
                })
                .Expect("The catalog move converted the standard action (PHB p.141)", v =>
                {
                    JsonObj econ = Econ(v, "hero", 1);
                    if (econ == null) return ExpectResult.Fail("no turn end");
                    return Flag(econ, "move") && Flag(econ, "conv")
                        ? ExpectResult.Pass("move spent, standard converted")
                        : ExpectResult.Fail("move " + Flag(econ, "move") + ", conv " + Flag(econ, "conv"));
                })
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        // ── GRID-019: no withdraw while blinded ────────────────────────

        /// <summary>
        /// GRID-019 (PHB p.143: no withdraw while blinded): an AI hobgoblin sergeant (DefensiveMelee) at 5 hp, below the
        /// 30% at which it withdraws, is blinded next to a hero. The shared legality refuses its withdraw, so it does not
        /// withdraw and the routine goes on (it fights where it stands or moves). Before the fix neither side checked
        /// Blinded for a withdraw. Its backpack is emptied so it neither throws nor drinks.
        /// </summary>
        private static ScenarioDef BlindedNoWithdrawAi()
        {
            return Rules("rules/blinded-no-withdraw-ai", "A blinded AI creature does not withdraw (PHB p.143; GRID-019)")
                .Covers("GRID-019", "PHB p.143")
                .MaxRounds(1)
                .Npc("hob", "hobgoblin_sergeant", 10, 10, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 9, 10, Control.Scripted)
                .Hp("hob", 5)
                .StartCondition("hob", CombatConditionType.Blinded, -1)
                .Tweak("hob", EmptyBackpack)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); })
                .Initiative("hero", "hob")
                .Turn("hero", 1, Step.Pass())
                .Expect("The hobgoblin's turn is an AI turn", Expect.Controller("hob", "ai"))
                .Expect("At its turn start it is blinded and below 30% of its hit points", v =>
                {
                    JsonObj snap = v.Snapshot("hob", 1, "hob");
                    bool blinded = snap != null && snap.Get("conds") is List<string> l && l.Contains("Blinded");
                    int hp = snap != null && snap.Get("hp") is int h ? h : -999;
                    return blinded && hp > 0 && hp <= 5 ? ExpectResult.Pass("blinded, hp " + hp) : ExpectResult.Fail("blinded " + blinded + ", hp " + hp);
                })
                .Expect("It does not withdraw, and no withdraw is even tried", v =>
                {
                    int withdrew = v.Log("Hobgoblin Sergeant withdraws from").Count;
                    int refused = v.Log("Hobgoblin Sergeant cannot withdraw").Count;
                    return withdrew == 0 && refused == 0
                        ? ExpectResult.Pass("no withdraw")
                        : ExpectResult.Fail(withdrew + " withdraw line(s), " + refused + " refusal(s)");
                })
                .Expect("It reached the withdraw decision and fought on: an attack or maneuver of its own on the hero", v =>
                {
                    // Pins that the DefensiveMelee routine (and its withdraw gate) ran, not the no-target search turn,
                    // which would pass the expectation above without ever asking the withdraw legality.
                    int attacks = v.Attacks(by: "hob", target: "hero", aoo: false, round: 1).Count;
                    int maneuvers = v.Of("maneuver").Count(e => e.Round == 1 && e.Str("by") == "hob" && e.Str("target") == "hero");
                    // A trip provokes; when the hero's attack of opportunity drops the hobgoblin first, the AoO event (its
                    // trigger "maneuver") is the only trace of the attempt.
                    int provoked = v.Of("aoo").Count(e => e.Round == 1 && e.Str("by") == "hero" && e.Str("target") == "hob"
                        && (e.Str("trigger") == "maneuver" || e.Str("trigger") == "attack"));
                    return attacks + maneuvers + provoked > 0
                        ? ExpectResult.Pass(attacks + " attack(s), " + maneuvers + " maneuver(s), " + provoked + " provoked AoO(s)")
                        : ExpectResult.Fail("no attack or maneuver by the hobgoblin on the hero");
                })
                .Build();
        }
    }
}
#endif
