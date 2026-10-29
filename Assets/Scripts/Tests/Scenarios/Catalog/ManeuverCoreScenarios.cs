#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 12 step 19 (docs/designs/pc_npc_parity_batch12.md; CMB-102, CMB-098, CMB-103, CMB-101; decisions D10, D15): one
    /// maneuver core (ManeuverRequest, the cost table ManeuverActionCost.For, GameManager.CanStartManeuver and
    /// ExecuteManeuver) and one maneuver decider (IManeuverDecider) for PCs and NPCs. The NPC picks the item it disarms
    /// (PHB p.155; D15), feints with the PC's cost row (PHB p.155, p.95), really moves a grapple it won the move check for
    /// (PHB p.156; CMB-103) and picks its escape square (PHB p.156-157; D10); a controllable grappler may let an escape go
    /// (D10); a PC's trip is one attack of its full attack (PHB p.141 Table 8-2 note 7). The grapple steps of a Scripted
    /// actor run the AI grapple turn's own executor (GameManager.RunGrappleActionForAI).
    /// </summary>
    public static class ManeuverCoreScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("npc-disarm-best-item", NpcDisarmBestItem);
            yield return S("npc-feint", () => NpcFeint(true));
            yield return S("npc-feint-standard", () => NpcFeint(false));
            yield return S("maneuver-cost-table-ui", ManeuverCostTableUi);
            yield return S("ai-grapple-move", AiGrappleMove);
            yield return S("grapple-escape-square-choice-ui", GrappleEscapeSquareChoiceUi);
            yield return S("grapple-escape-square-choice-ai", GrappleEscapeSquareChoiceAi);
            yield return S("grapple-escape-not-opposed-ui", () => GrappleEscapeOppositionUi(false));
            yield return S("grapple-escape-opposed-ui", () => GrappleEscapeOppositionUi(true));
            yield return S("grapple-escape-artist-not-opposed-ui", () => GrappleEscapeOppositionUi(false, escapeArtist: true));
            yield return S("grapple-escape-artist-opposed-ui", () => GrappleEscapeOppositionUi(true, escapeArtist: true));
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("ManeuverCoreScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title)
            => Scenario.Define(id, title).Tags("rules", "maneuver").Covers("PC_NPC_PARITY", "batch 12 step 19");

        // ── Helpers ─────────────────────────────────────────────────────

        private static Vector2Int? Pos(JsonObj snap) => snap != null && snap.Get("pos") is Vector2Int p ? p : (Vector2Int?)null;

        private static bool Grappling(JsonObj snap) => snap != null && snap.Get("gr") is bool g && g;

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

        private static global::Inventory Inv(CharacterController c)
            => c != null && c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;

        private const string HeirloomName = "Heirloom Shortsword";

        /// <summary>A plain dagger in the main hand and a +1 shortsword (renamed, for the log) in the off hand.</summary>
        private static void DaggerAndHeirloom(CharacterController c)
        {
            SturdyNoAoO(c);
            global::Inventory inv = Inv(c);
            if (inv == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no inventory");
            inv.RightHandSlot = null;
            inv.LeftHandSlot = null;
            inv.HandsSlot = null;
            inv.DirectEquip(ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponDagger), EquipSlot.RightHand);
            ItemData heirloom = ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponShortswordPlus1);
            heirloom.Name = HeirloomName;
            heirloom.EnhancementBonus = Mathf.Max(1, heirloom.EnhancementBonus);
            inv.DirectEquip(heirloom, EquipSlot.LeftHand);
            inv.RecalculateStats();
        }

        /// <summary>A rogue (Human, STR 12, DEX 16, CHA 14) with BAB 3, the given level and feats.</summary>
        private static CharacterStats Rogue(string name, int level, params string[] feats)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Rogue",
                str: 12, dex: 16, con: 12, wis: 10, intelligence: 12, cha: 14,
                bab: 3, armorBonus: 0, shieldBonus: 0,
                damageDice: 6, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 6 * level, raceName: "Human");
            s.BaseAttackBonusOverride = 3;
            foreach (string f in feats)
                s.Feats.Add(f);
            return s;
        }

        // ── D15: the attacker picks the item ────────────────────────────

        /// <summary>
        /// D15 (PHB p.155): a Scripted fighter 6 with Improved Disarm (no AoO) disarms an orc holding a plain dagger in its
        /// main hand and a +1 shortsword in its off hand. The NPC executor's request carries the item the AI decider picks
        /// (AIProfile.DefaultChooseManeuverItem: the better weapon), not the first held item, so the shortsword is knocked
        /// away and the dagger stays in hand.
        /// </summary>
        private static ScenarioDef NpcDisarmBestItem()
        {
            return Rules("rules/npc-disarm-best-item", "An NPC's disarm aims at the item its decider picks, the better weapon (PHB p.155; D15, CMB-098)")
                .Covers("CMB-098", "D15", "PHB p.155")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => RulesScenarios.Fighter("Fighter", 6, "Improved Disarm")), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("fighter", RulesScenarios.StripOffHand)
                .Tweak("orc", DaggerAndHeirloom)
                .Initiative("fighter", "orc")
                .Force(20, 20, "Disarm attack roll")
                .Force(20, 1, "Disarm defense roll")
                .Turn("fighter", 1, Step.Maneuver(SpecialAttackType.Disarm, "orc"))
                .Turn("orc", 1, Step.Assert("the heirloom shortsword is gone and the dagger kept", ctx =>
                {
                    global::Inventory inv = Inv(ctx.Get("orc"));
                    return inv != null && inv.RightHandSlot != null && inv.RightHandSlot.Name != HeirloomName
                        && (inv.LeftHandSlot == null || inv.LeftHandSlot.Name != HeirloomName);
                }))
                .Expect("The disarm step is done", Expect.StepStatus("fighter", 1, "Maneuver", 0, "done"))
                .Expect("The disarm succeeds", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Disarm).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no disarm");
                    return m.Bool("success") ? ExpectResult.Pass("disarmed", m.Seq) : ExpectResult.Fail("failed with forced dice", m.Seq);
                })
                .Expect("The disarm names the heirloom shortsword, not the dagger in the main hand", v =>
                {
                    TraceEvent line = v.Of("log").FirstOrDefault(e => (e.Str("text") ?? "").Contains(HeirloomName));
                    return line != null ? ExpectResult.Pass(line.Str("text"), line.Seq) : ExpectResult.Fail("no log line names " + HeirloomName);
                })
                .Expect("The orc keeps the dagger and has lost the shortsword", Expect.AssertsPass())
                .Build();
        }

        // ── CMB-102: the NPC feint row ──────────────────────────────────

        /// <summary>
        /// CMB-102 item 4 (PHB p.155, p.95): a Scripted rogue 4 feints an orc and then attacks it. With Improved Feint the
        /// feint is a move action, so the attack (a standard action) follows; without it the feint takes the standard
        /// action and the attack is refused. The cost is the PC's row of the one table (ManeuverActionCost.For).
        /// </summary>
        private static ScenarioDef NpcFeint(bool improved)
        {
            string id = improved ? "rules/npc-feint" : "rules/npc-feint-standard";
            string title = improved
                ? "An NPC with Improved Feint feints as a move action and still attacks (PHB p.95, p.155; CMB-102)"
                : "An NPC feint without Improved Feint takes the standard action (PHB p.155; CMB-102)";
            ScenarioBuilder b = Rules(id, title)
                .Covers("CMB-102", "PHB p.95", "PHB p.155")
                .MaxRounds(1)
                .Pc("rogue", ActorSource.Stats(() => improved ? Rogue("Rogue", 4, "Combat Expertise", "Improved Feint") : Rogue("Rogue", 4)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("orc", SturdyNoAoO)
                .Initiative("rogue", "orc")
                .Force(20, 20, "Feint Bluff check")
                .Force(20, 1, "Feint opposed check")
                .Turn("rogue", 1, Step.Maneuver(SpecialAttackType.Feint, "orc"), Step.Attack("orc"))
                .Turn("orc", 1, Step.Pass())
                .Expect("The feint is made and succeeds", v =>
                {
                    TraceEvent m = v.Maneuvers("rogue", SpecialAttackType.Feint).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no feint");
                    return m.Bool("success") ? ExpectResult.Pass("feinted", m.Seq) : ExpectResult.Fail("failed with forced dice", m.Seq);
                });
            if (improved)
                b = b.Expect("The attack after the move-action feint is made", Expect.All(
                    Expect.StepStatus("rogue", 1, "Attack", 0, "done"),
                    Expect.Count("attack", e => e.Str("attacker") == "rogue" && !e.Bool("aoo"), 1, 1)));
            else
                b = b.Expect("The feint took the standard action: the attack is refused", Expect.All(
                    Expect.StepStatus("rogue", 1, "Attack", 0, "refused"),
                    Expect.None("attack", e => e.Str("attacker") == "rogue" && !e.Bool("aoo"))));
            return b.Build();
        }

        // ── CMB-102: the PC reads the table ─────────────────────────────

        /// <summary>
        /// CMB-102 (PHB p.141 Table 8-2 note 7, p.143): a Ui fighter 6 (BAB +6/+1, STR 16) attacks a dummy with the Attack
        /// button and then trips it from the Special Attack menu: the trip is the second attack of its full attack, at the
        /// +1 iterative (touch +1 + 3 Strength = +4); a third action is refused.
        /// </summary>
        private static ScenarioDef ManeuverCostTableUi()
        {
            const string TouchMod = @"Touch attack: d20 \d+ ([+-]\d+) =";
            return Rules("rules/maneuver-cost-table-ui", "A PC trip as the second attack of a full attack, at the second iterative (PHB p.141; CMB-102)")
                .Covers("CMB-102", "PHB p.141", "PHB p.143", "PHB p.158")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6)), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("hero", RulesScenarios.StripOffHand)
                .Tweak("dummy", SturdyNoAoO)
                .Initiative("hero", "dummy")
                // A natural 1 misses the touch attack, so the dummy stays standing and nothing else follows.
                .Force(20, 1, "Trip touch attack", -1)
                .Turn("hero", 1, Step.Attack("dummy"), Step.Maneuver(SpecialAttackType.Trip, "dummy"), Step.Maneuver(SpecialAttackType.Trip, "dummy"))
                .Turn("dummy", 1, Step.Pass())
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The attack and the first trip are done; a third action is refused", Expect.All(
                    Expect.StepStatus("hero", 1, "Attack", 0, "done"),
                    Expect.StepStatus("hero", 1, "Maneuver", 0, "done"),
                    Expect.StepStatus("hero", 1, "Maneuver", 1, "refused")))
                .Expect("The trip's touch attack is at the second iterative: +1 BAB + 3 Strength = +4", v =>
                {
                    List<KeyValuePair<TraceEvent, int>> mods = v.LogInts(TouchMod, 1);
                    if (mods.Count != 1) return ExpectResult.Fail(mods.Count + " trip touch attacks (expected 1)");
                    return mods[0].Value == 4 ? ExpectResult.Pass("touch +4", mods[0].Key.Seq) : ExpectResult.Fail("touch " + mods[0].Value, mods[0].Key.Seq);
                })
                .Build();
        }

        // ── CMB-103: the AI moves the grapple ───────────────────────────

        /// <summary>
        /// CMB-103 (PHB p.156): a Scripted grapple-drill orc grapples a Scripted fighter in round 1 (it moves into the
        /// fighter's square, 10,10) and in round 2 wins the move check (forced) through the AI grapple turn's executor
        /// (RunGrappleActionForAI): the AI decider takes the grapple toward its goblin ally at (14,10) and the fighter is
        /// dragged along, still grappled. Before, the AI spent the action, won the check and never moved.
        /// </summary>
        private static ScenarioDef AiGrappleMove()
        {
            return Rules("rules/ai-grapple-move", "An AI grappler that wins the move check moves the grapple, dragging its opponent (PHB p.156; CMB-103)")
                .Covers("CMB-103", "PHB p.156")
                .MaxRounds(2)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Npc("goblin", "goblin", 14, 10, Control.Idle)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6)), 10, 10, Control.Scripted)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); SturdyNoAoO(c); })
                .Tweak("orc", SturdyNoAoO)
                .Tweak("goblin", SturdyNoAoO)
                .Initiative("orc", "hero", "goblin")
                .RecordDice()
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", 1)
                .Force(20, 1, "Grapple check", 1)
                .Force(20, 20, "(attacker)", 1)
                .Force(20, 1, "(defender)", 1)
                .Turn("orc", 1, Step.Maneuver(SpecialAttackType.Grapple, "hero"))
                .Turn("orc", 2, Step.GrappleAction("Move"))
                .Turn("hero", 0, Step.Pass())
                .Expect("Round 1: the grapple holds (both grappling at the hero's round-1 turn)", v =>
                {
                    JsonObj o = v.Snapshot("orc", 1, "hero"), h = v.Snapshot("hero", 1, "hero");
                    return Grappling(o) && Grappling(h) ? ExpectResult.Pass("grappled at " + Pos(o)) : ExpectResult.Fail("orc grappling " + Grappling(o) + ", hero " + Grappling(h));
                })
                .Expect("Round 2: the move step is done", Expect.StepStatus("orc", 2, "GrappleAction(Move", 0, "done"))
                .Expect("The orc took the grapple toward its ally and the hero was dragged along, still grappled", v =>
                {
                    JsonObj o1 = v.Snapshot("orc", 1, "hero"), o2 = v.Snapshot("orc", 2, "hero"), h2 = v.Snapshot("hero", 2, "hero");
                    Vector2Int? before = Pos(o1), after = Pos(o2), hero = Pos(h2);
                    if (!before.HasValue || !after.HasValue || !hero.HasValue) return ExpectResult.Inconclusive("missing snapshots");
                    var goblin = new Vector2Int(14, 10);
                    int dBefore = SquareGridUtils.GetDistance(before.Value, goblin), dAfter = SquareGridUtils.GetDistance(after.Value, goblin);
                    bool beside = SquareGridUtils.GetDistance(after.Value, hero.Value) <= 1;
                    return dAfter < dBefore && beside && Grappling(h2)
                        ? ExpectResult.Pass("orc " + before + " -> " + after + ", hero at " + hero)
                        : ExpectResult.Fail("orc " + before + " -> " + after + " (goblin distance " + dBefore + " -> " + dAfter + "), hero at " + hero + ", grappled " + Grappling(h2));
                })
                .Build();
        }

        // ── D10: the escaper picks the square ───────────────────────────

        /// <summary>
        /// D10 (PHB p.156-157): a Scripted grapple-drill orc grapples a Ui fighter in round 1 (it moves into the fighter's
        /// square, 10,10). The fighter escapes on its turn (checks forced) and picks the square (9,11) from the selection.
        /// </summary>
        private static ScenarioDef GrappleEscapeSquareChoiceUi()
        {
            return Rules("rules/grapple-escape-square-choice-ui", "A PC that escapes a grapple picks the square it moves into (PHB p.156-157; D10)")
                .Covers("D10", "CMB-098", "PHB p.156", "PHB p.157")
                .MaxRounds(1)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6)), 10, 10, Control.Ui)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Tweak("orc", SturdyNoAoO)
                .Initiative("orc", "hero")
                .RecordDice()
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", 1)
                .Force(20, 1, "Grapple check", 1)
                .Force(20, 20, "Grapple check", 1)
                .Force(20, 1, "Grapple check", 1)
                .Turn("orc", 1, Step.Maneuver(SpecialAttackType.Grapple, "hero"))
                .Turn("hero", 1, Step.GrappleEscape(9, 11), Step.EndTurn())
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The escape (and its square selection) is done", Expect.StepStatus("hero", 1, "GrappleAction(Escape", 0, "done"))
                .Expect("The hero ends free of the grapple on the square it picked, (9,11)", v =>
                {
                    JsonObj h = v.Final("hero");
                    TraceEvent mv = v.Moves("hero", 1).FirstOrDefault(e => e.Get("to") is Vector2Int to && to == new Vector2Int(9, 11));
                    return mv != null && !Grappling(h) ? ExpectResult.Pass("moved to (9,11)", mv.Seq) : ExpectResult.Fail("at " + Pos(h) + ", grappling " + Grappling(h));
                })
                .Build();
        }

        /// <summary>
        /// D10, the AI side (PHB p.156-157): a Scripted fighter 6 with Improved Grapple grapples a grapple-drill orc in round
        /// 1 (it moves into the orc's square, 11,10); its ally stands at (9,10), the orc's guard at (14,10). The orc escapes
        /// (checks forced) through the AI grapple turn's executor, and its decider picks the square (no random pick):
        /// AIProfile.DefaultChooseGrappleEscapeSquare, the fewest threatening foes (east of the shared square only the
        /// fighter threatens; west, both), then the nearest ally, then the lowest square: (12,9).
        /// </summary>
        private static ScenarioDef GrappleEscapeSquareChoiceAi()
        {
            return Rules("rules/grapple-escape-square-choice-ai", "An NPC that escapes a grapple picks its square by its decider, not at random (PHB p.156-157; D10)")
                .Covers("D10", "CMB-098", "PHB p.156", "PHB p.157")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6, "Improved Grapple")), 10, 10, Control.Scripted)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Ally", 6)), 9, 10, Control.Scripted)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Npc("guard", "orc_berserker", 14, 10, Control.Idle)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); c.Stats.CanMakeAttacksOfOpportunity = false; SturdyNoAoO(c); })
                .Tweak("ally", c => { RulesScenarios.StripOffHand(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Tweak("orc", SturdyNoAoO)
                .Tweak("guard", SturdyNoAoO)
                .Initiative("hero", "orc", "ally", "guard")
                .RecordDice()
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", 1)
                .Force(20, 1, "Grapple check", 1)
                .Force(20, 20, "Grapple check", 1)
                .Force(20, 1, "Grapple check", 1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"))
                .Turn("orc", 1, Step.GrappleAction("Escape"))
                .Turn("ally", 1, Step.Pass())
                .Expect("The escape step is done", Expect.StepStatus("orc", 1, "GrappleAction(Escape", 0, "done"))
                .Expect("The orc ends free, east of the shared square, on (12,9)", v =>
                {
                    JsonObj o = v.Final("orc");
                    Vector2Int? p = Pos(o);
                    return p == new Vector2Int(12, 9) && !Grappling(o) ? ExpectResult.Pass("orc at (12,9)") : ExpectResult.Fail("orc at " + p + ", grappling " + Grappling(o));
                })
                .Build();
        }

        // ── D10: a grappler may let the escaper go ──────────────────────

        /// <summary>
        /// D10 (PHB p.156: an opponent need not try to hold an escaping creature): a Ui fighter 6 with Improved Grapple
        /// grapples a grapple-drill orc in round 1 (it moves into the orc's square, 11,10). The orc then tries to escape
        /// through the AI grapple turn's executor; the fighter's decider is the prompt, which the runner answers. Allow
        /// (<paramref name="oppose"/> false): the orc escapes with no grapple check. Oppose: the opposed check is rolled and
        /// the orc (forced to roll 1) stays grappled. With <paramref name="escapeArtist"/> the orc escapes with an Escape Artist
        /// check instead (a standard action, PHB p.156), opposed by the hero's grapple check (PHB p.73), and the hero's decider is
        /// asked the same way.
        /// </summary>
        private static ScenarioDef GrappleEscapeOppositionUi(bool oppose, bool escapeArtist = false)
        {
            string id = "rules/grapple-escape-" + (escapeArtist ? "artist-" : "") + (oppose ? "opposed-ui" : "not-opposed-ui");
            string how = escapeArtist ? "Escape Artist escape" : "escape";
            string title = oppose
                ? $"A controllable grappler opposes an NPC's {how}: the opposed check is rolled (PHB p.156{(escapeArtist ? ", p.73" : "")}; D10)"
                : $"A controllable grappler lets an NPC's {how} succeed: no check, the NPC is free (PHB p.156; D10)";
            ScenarioBuilder b = Rules(id, title)
                .Covers("D10", "CMB-098", "PHB p.156")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6, "Improved Grapple")), 10, 10, Control.Ui)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); c.Stats.CanMakeAttacksOfOpportunity = false; SturdyNoAoO(c); })
                .Tweak("orc", SturdyNoAoO)
                .Initiative("hero", "orc")
                .EscapeOppositionAnswer("hero", oppose)
                .RecordDice()
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", 1)
                .Force(20, 1, "Grapple check", 1)
                .Force(20, 1, escapeArtist ? "Escape Artist check" : "Grapple check", 1)
                .Force(20, 20, "Grapple check", 1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"))
                .Turn("orc", 1, Step.GrappleAction(escapeArtist ? "EscapeArtist" : "Escape"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The orc's escape step is done", Expect.StepStatus("orc", 1, "GrappleAction(" + (escapeArtist ? "EscapeArtist" : "Escape"), 0, "done"))
                .Expect("The hero's escape-opposition prompt was answered " + (oppose ? "Oppose" : "Allow"), v =>
                {
                    List<TraceEvent> notes = v.Of("note").Where(e => (e.Str("text") ?? "").StartsWith("escape-opposition prompt for hero", StringComparison.Ordinal)).ToList();
                    if (notes.Count != 1) return ExpectResult.Fail(notes.Count + " escape-opposition prompts for the hero", notes.Select(e => e.Seq).ToArray());
                    string expected = oppose ? "answered Oppose" : "answered Allow";
                    return notes[0].Str("text").Contains(expected) ? ExpectResult.Pass(notes[0].Str("text"), notes[0].Seq) : ExpectResult.Fail(notes[0].Str("text"), notes[0].Seq);
                });
            if (oppose)
                b = b.Expect("Opposed: the orc loses the check and stays grappled", v =>
                {
                    JsonObj o = v.Final("orc");
                    return Grappling(o) ? ExpectResult.Pass("still grappled") : ExpectResult.Fail("the orc escaped an opposed check it was forced to lose");
                });
            else
                b = b.Expect("Allowed: the orc escapes with no check and is free", Expect.All(
                    v =>
                    {
                        JsonObj o = v.Final("orc");
                        return !Grappling(o) ? ExpectResult.Pass("free at " + Pos(o)) : ExpectResult.Fail("still grappled");
                    },
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("chooses not to oppose the escape"), 1, int.MaxValue)));
            if (escapeArtist && oppose)
                b = b.Expect("Opposed: the Escape Artist check is opposed by the hero's grapple check, not a fixed DC (PHB p.73)", Expect.All(
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("attempts Escape Artist against"), 1, int.MaxValue),
                    Expect.Count("log", e => (e.Str("text") ?? "").Contains("Escape DC: 20"), 0, 0)));
            return b.Build();
        }
    }
}
#endif
