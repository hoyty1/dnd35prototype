#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Rules scenarios (docs/TESTING.md 3.4): attacks of opportunity from movement, flanking, prone, NPC casting
    /// while threatened, the four standard-action maneuvers plus the free trip, single vs full attack, pin release
    /// and the NPC maneuver that replaces an iterative attack (CMB-102).
    ///
    /// NPC data checked through NPCDatabase.Get on 2026-10-07 (edit mode):
    /// - orc_berserker: Barbarian 3, Medium, STR 17, BAB 2, feats Power Attack and Cleave, greataxe.
    /// - orc_grapple_drill: Warrior 3, Medium, STR 16, BAB 2, no feats, dagger.
    /// - arcane_missile_adept: Wizard 5, Medium, prepared Magic Missile x7, AI tag MagicMissileOnly, RangedKiter.
    /// - target_dummy: Commoner 1, Medium, base hit-die HP 50, natural armor -4, no feats, no weapon.
    /// - wolf: Animal 2, Medium, STR 13, BAB 1, HasTripAttack (MM p.283), Weapon Focus and Track.
    /// - goblin: the 1-HD MM goblin of the smoke scenarios.
    /// - formian_taskmaster: Outsider 6 HD, Medium, STR 17, DEX 16, IsExceptionallyStable (four legs, MM p.108-110;
    ///   owner decision 2026-10-07, CMB-085).
    /// - barghest: Outsider 6 HD, Medium, STR 17, DEX 15, not stable (only its wolf form would be, and the game models
    ///   no wolf form; owner decision 2026-10-07, CMB-085).
    /// Stats actors are Human fighters built with named arguments (CHR-032) and BAB set through
    /// BaseAttackBonusOverride (CHR-068); feat strings are the case-sensitive HasFeat names (CHR-030):
    /// "Improved Trip", "Improved Grapple", "Weapon Focus".
    /// Rules checked in the PHB (2026-10-07): p.137 and p.143-144 (movement AoOs, 5-foot step, withdraw, stand up,
    /// crawl), p.140 (casting provokes; casting defensively), p.153 (flanking +2), p.154 (bull rush), p.155 (disarm:
    /// an AoO that deals damage foils it), p.156 (grapple: likewise), p.158 (trip; the defender may trip back),
    /// p.141 Table 8-2 note (trip, disarm and grapple replace a melee attack).
    /// MM p.312 (primary natural attacks at full bonus, secondary at -5) and p.304 (Multiattack: -2) for the
    /// maneuvers that replace a natural attack (owner decision 2026-10-07).
    /// PHB p.239 (Haste: one extra attack on a full attack) for the hasted natural-weapon creature: one extra
    /// natural attack at that attack's normal bonus (owner decision 2026-10-07, CMB-106).
    /// Bull rush AoOs (owner decision 2026-10-07): entering the defender's space is the bull rush's own provocation
    /// (PHB p.154), separate from movement; moving out of squares one opponent threatens counts as one opportunity for
    /// the whole round (PHB p.138), and a creature makes one AoO a round, 1 + DEX modifier with Combat Reflexes (PHB p.92).
    /// The AI maneuver stopgap (owner decision 2026-10-07, AI-060) is an AI decision limit, not a rule: after a
    /// maneuver lands the AI attacks with its remaining steps, and it does not retry a failed maneuver type against
    /// the same target that turn.
    /// </summary>
    public static class RulesScenarios
    {
        /// <summary>The number of definitions <see cref="All"/> yields (docs/TESTING.md 3.4); a short catalog is a load error.</summary>
        public const int Count = 38;

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            // Each definition is built through ScenarioCatalog.Safe, so an invalid one is a load error (run verdict
            // LOADERROR) and the others still load.
            yield return S("movement-aoo-move", () => MovementAoO("rules/movement-aoo-move", "Moving out of a threatened square provokes once (PHB p.137)", Step.Move(14, 10)));
            yield return S("movement-aoo-5ft", () => MovementAoO("rules/movement-aoo-5ft", "A 5-foot step provokes nothing (PHB p.144)", Step.FiveFootStep(12, 10)));
            yield return S("movement-aoo-withdraw", () => MovementAoO("rules/movement-aoo-withdraw", "Withdraw: the first square provokes nothing (PHB p.143)", Step.Withdraw(16, 10)));
            yield return S("flanking", () => Flanking(true));
            yield return S("flanking-control", () => Flanking(false));
            yield return S("prone-ai-stands", ProneAiStandsUp);
            yield return S("prone-ui-crawl", ProneUiCrawl);
            yield return S("prone-scripted-move", ProneScriptedMove);
            yield return S("npc-cast-threatened", () => NpcCast(true, false));
            yield return S("npc-cast-threatened-normal", () => NpcCast(true, true));
            yield return S("npc-cast-unthreatened", () => NpcCast(false, false));
            yield return S("maneuver-trip", () => Trip("rules/maneuver-trip", "Trip without Improved Trip: AoO first, defender prone (PHB p.158)", false, true));
            yield return S("maneuver-trip-improved", () => Trip("rules/maneuver-trip-improved", "Improved Trip: no AoO, prone, free attack (PHB p.96, p.158)", true, true));
            yield return S("maneuver-trip-fail", () => Trip("rules/maneuver-trip-fail", "A failed trip: the defender may trip back (PHB p.158)", true, false));
            yield return S("maneuver-trip-ui", TripUi);
            yield return S("maneuver-disarm", Disarm);
            yield return S("maneuver-bullrush", BullRush);
            yield return S("maneuver-bullrush-charge-reflexes", () => BullRushWatcherAoOs(true, true));
            yield return S("maneuver-bullrush-charge-control", () => BullRushWatcherAoOs(true, false));
            yield return S("maneuver-bullrush-reflexes", () => BullRushWatcherAoOs(false, true));
            yield return S("maneuver-grapple", Grapple);
            yield return S("maneuver-freetrip", FreeTrip);
            yield return S("single-vs-full-attack", () => SingleVsFull(false));
            yield return S("single-vs-full-attack-ui", () => SingleVsFull(true));
            yield return S("pin-release-ends-grapple", PinRelease);
            yield return S("pin-release-5ft", PinReleaseFiveFootStep);
            yield return S("npc-maneuver-replaces-iterative", NpcManeuverReplacesIterative);
            yield return S("maneuver-replaces-natural", () => ManeuverReplacesNatural(false));
            yield return S("maneuver-replaces-natural-multiattack", () => ManeuverReplacesNatural(true));
            yield return S("maneuver-replaces-natural-ui", ManeuverReplacesNaturalUi);
            yield return S("haste-natural-extra", () => HasteNaturalExtra(false));
            yield return S("haste-natural-extra-ui", () => HasteNaturalExtra(true));
            yield return S("haste-natural-moved", HasteNaturalMoved);
            yield return S("haste-natural-buttons-ui", HasteNaturalButtonsUi);
            yield return S("ai-maneuver-stopgap-trip", () => AiManeuverStopgap(true));
            yield return S("ai-maneuver-stopgap-trip-fail", () => AiManeuverStopgap(false));
            yield return S("stability-trip", () => StabilityCheck("rules/stability-trip", "A four-legged formian taskmaster resists a trip with +4 stability (PHB p.158, CMB-085)", "formian_taskmaster", SpecialAttackType.Trip, true));
            yield return S("stability-bullrush", () => StabilityCheck("rules/stability-bullrush", "A four-legged formian taskmaster resists a bull rush with +4 stability (PHB p.154, CMB-085)", "formian_taskmaster", SpecialAttackType.BullRushAttack, true));
            yield return S("stability-trip-control", () => StabilityCheck("rules/stability-trip-control", "A barghest in its natural form (the only modelled form) gets no stability against a trip (PHB p.158, CMB-085)", "barghest", SpecialAttackType.Trip, false));
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("RulesScenarios rules/" + name, build);

        // ── Builders ────────────────────────────────────────────────────

        /// <summary>A Human fighter: STR 16 (+3), DEX 12, CON 14; BAB through BaseAttackBonusOverride; Fighter kit.</summary>
        internal static CharacterStats Fighter(string name, int level, params string[] feats)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Fighter",
                str: 16, dex: 12, con: 14, wis: 10, intelligence: 10, cha: 10,
                bab: level, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 8 * level, raceName: "Human");
            s.BaseAttackBonusOverride = level;
            foreach (string f in feats)
                s.Feats.Add(f);
            return s;
        }

        /// <summary>Removes the shield and spiked gauntlet of the Fighter kit, so the PC menu shows no dual-wield prompt.</summary>
        internal static void StripOffHand(CharacterController c)
        {
            InventoryComponent inv = c.GetComponent<InventoryComponent>();
            if (inv == null || inv.CharacterInventory == null)
                return;
            inv.CharacterInventory.LeftHandSlot = null;
            inv.CharacterInventory.HandsSlot = null;
            inv.CharacterInventory.RecalculateStats();
        }

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static Vector2Int? Pos(JsonObj snap) => snap != null && snap.Get("pos") is Vector2Int p ? p : (Vector2Int?)null;

        private static bool HasCond(JsonObj snap, string cond) => snap != null && snap.Get("conds") is List<string> l && l.Contains(cond);

        private static int ActorInt(TraceView v, string key, string field)
        {
            TraceEvent a = v.Of("actor").FirstOrDefault(e => e.Str("key") == key);
            return a != null ? a.Int(field, int.MinValue) : int.MinValue;
        }

        /// <summary>The damage of the attack an 'aoo' event made (0 on a miss or when the attack line is missing).</summary>
        private static int AoODamage(TraceView v, TraceEvent aoo)
        {
            int seq = aoo.Int("attackSeq");
            TraceEvent atk = v.Of("attack").FirstOrDefault(e => e.Seq == seq);
            return atk != null && atk.Bool("hit") ? atk.Int("dmg") : 0;
        }

        // ── Movement AoOs (CMB-005, CMB-073, CMB-076) ──────────────────

        private static ScenarioDef MovementAoO(string id, string title, Step orcStep)
        {
            ScenarioBuilder b = Rules(id, title)
                .Covers("CMB-005", "CMB-073", "PHB p.137", "PHB p.143-144")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 4)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Initiative("fighter", "orc")
                .Turn("fighter", 1, Step.Pass())
                .Turn("orc", 1, orcStep);

            if (orcStep.Kind == StepKind.Move)
            {
                b.Expect("The orc's move is done", Expect.StepStatus("orc", 1, "Move", 0, "done"))
                 .Expect("Exactly one AoO by the fighter, for movement (PHB p.137)",
                     Expect.Count("aoo", e => e.Str("by") == "fighter" && e.Str("target") == "orc" && e.Str("trigger") == "movement", 1, 1))
                 .Expect("The AoO comes before the orc leaves its square (CMB-005)",
                     Expect.AoOBefore("fighter", "orc", e => e.Ev == "move" && e.Str("actor") == "orc"))
                 .Expect("No other AoO", Expect.Count("aoo", null, 1, 1));
            }
            else if (orcStep.Kind == StepKind.FiveFootStep)
            {
                b.Expect("The 5-foot step is done and ends at (12,10)", v =>
                    {
                        ExpectResult r = Expect.StepStatus("orc", 1, "FiveFootStep", 0, "done")(v);
                        if (!r.IsPass) return r;
                        Vector2Int? p = Pos(v.Final("orc"));
                        return p == new Vector2Int(12, 10) ? ExpectResult.Pass("orc at (12,10)") : ExpectResult.Fail("orc at " + p);
                    })
                 .Expect("A 5-foot step provokes no AoO (PHB p.144)", Expect.None("aoo", null));
            }
            else
            {
                b.Expect("The withdraw is done and ends at (16,10)", v =>
                    {
                        ExpectResult r = Expect.StepStatus("orc", 1, "Withdraw", 0, "done")(v);
                        if (!r.IsPass) return r;
                        Vector2Int? p = Pos(v.Final("orc"));
                        return p == new Vector2Int(16, 10) ? ExpectResult.Pass("orc at (16,10)") : ExpectResult.Fail("orc at " + p);
                    })
                 .Expect("Withdrawing from the only threatened square provokes nothing (PHB p.143)", Expect.None("aoo", null));
            }
            return b.Build();
        }

        // ── Flanking (CMB-001) ──────────────────────────────────────────

        private static ScenarioDef Flanking(bool flanked)
        {
            string id = flanked ? "rules/flanking" : "rules/flanking-control";
            ScenarioBuilder b = Rules(id, flanked
                    ? "Opposite allies flank: +2 on attacks, AC unchanged, sneak attack (PHB p.153, p.50)"
                    : "Allies not opposite do not flank (control)")
                .Covers("CMB-001", "PHB p.153")
                .MaxRounds(1)
                .Pc("rogue", ActorSource.QuickStart("Rogue"), 8, 10, Control.Scripted)
                .Pc("fighter", ActorSource.QuickStart("Fighter"), flanked ? 10 : 9, flanked ? 10 : 11, Control.Scripted)
                .Npc("orc", "orc_berserker", 9, 10, Control.Scripted)
                // The orc acts first, so it is not flat-footed (no sneak attack from that).
                .Initiative("orc", "rogue", "fighter")
                .Turn("orc", 1, Step.Pass())
                .Turn("rogue", 1, Step.Attack("orc"))
                .Turn("fighter", 1, Step.Attack("orc"));

            b.Expect("Both attack the orc", v =>
            {
                bool rogue = v.Attacks("rogue", "orc", false).Count > 0, fighter = v.Attacks("fighter", "orc", false).Count > 0;
                return rogue && fighter ? ExpectResult.Pass("both attacked") : ExpectResult.Fail("rogue " + rogue + ", fighter " + fighter);
            });
            b.Expect("Every attack on the orc uses its setup AC (flanking changes no AC, CMB-001)", v =>
            {
                int ac = ActorInt(v, "orc", "ac");
                TraceEvent bad = v.Attacks(null, "orc", false).FirstOrDefault(e => e.Int("ac") != ac);
                return bad == null ? ExpectResult.Pass("AC " + ac) : ExpectResult.Fail("AC " + bad.Int("ac") + ", setup AC " + ac, bad.Seq);
            });
            if (flanked)
            {
                b.Expect("Every attack on the orc flanks with +2 (PHB p.153)", v =>
                {
                    List<TraceEvent> atk = v.Attacks(null, "orc", false);
                    TraceEvent bad = atk.FirstOrDefault(e => !e.Bool("flank") || e.Int("flankBonus") != 2);
                    return bad == null ? ExpectResult.Pass(atk.Count + " flanking attacks") : ExpectResult.Fail("flank " + bad.Get("flank") + " bonus " + bad.Get("flankBonus"), bad.Seq);
                });
                b.Expect("The rogue's hits add sneak attack (PHB p.50)", v =>
                {
                    List<TraceEvent> hits = v.Attacks("rogue", "orc", false).Where(e => e.Bool("hit")).ToList();
                    if (hits.Count == 0)
                        return ExpectResult.Inconclusive("the rogue did not hit");
                    TraceEvent bad = hits.FirstOrDefault(e => !e.Bool("sneak"));
                    return bad == null ? ExpectResult.Pass(hits.Count + " sneak hits") : ExpectResult.Fail("a hit without sneak attack", bad.Seq);
                });
            }
            else
            {
                b.Expect("No attack on the orc flanks", Expect.None("attack", e => e.Str("target") == "orc" && (e.Bool("flank") || e.Int("flankBonus") != 0)))
                 .Expect("No sneak attack without flanking or a flat-footed target", Expect.None("attack", e => e.Str("attacker") == "rogue" && e.Bool("sneak")));
            }
            return b.Build();
        }

        // ── Prone (CMB-074) ─────────────────────────────────────────────

        private static ScenarioDef ProneAiStandsUp()
        {
            return Rules("rules/prone-ai-stands", "A prone AI creature stands up as a move action and provokes (PHB p.144)")
                .Covers("CMB-074", "PHB p.144")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 4)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Ai)
                .StartCondition("orc", CombatConditionType.Prone, -1)
                .Initiative("fighter", "orc")
                .Turn("fighter", 1, Step.Pass())
                .Expect("The orc's turn is an AI turn", Expect.Controller("orc", "ai"))
                .Expect("The fighter's AoO for standing up comes before Prone is removed (PHB p.144)", v =>
                {
                    TraceEvent aoo = v.AoOs("fighter", "orc", "standup").FirstOrDefault();
                    TraceEvent stand = v.Conds("orc", CombatConditionType.Prone).FirstOrDefault(e => !e.Bool("added"));
                    if (aoo == null)
                        return ExpectResult.Fail("no standup AoO by the fighter");
                    if (stand == null)
                        return ScenarioChecks.IsDownSnapshot(v.Final("orc")) ? ExpectResult.Inconclusive("the orc was dropped") : ExpectResult.Fail("the orc never stood up", aoo.Seq);
                    return aoo.Seq < stand.Seq ? ExpectResult.Pass("AoO #" + aoo.Seq + " before stand #" + stand.Seq, aoo.Seq, stand.Seq) : ExpectResult.Fail("AoO after the stand", aoo.Seq, stand.Seq);
                })
                .Build();
        }

        private static ScenarioDef ProneUiCrawl()
        {
            return Rules("rules/prone-ui-crawl", "A prone PC cannot move, crawls 5 ft as a move action and provokes (PHB p.143)")
                .Covers("CMB-074", "PHB p.143")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6)), 10, 10, Control.Ui)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .StartCondition("hero", CombatConditionType.Prone, -1)
                .Tweak("hero", StripOffHand)
                .Initiative("hero", "orc")
                .Turn("hero", 1, Step.Move(10, 13), Step.Crawl(9, 10), Step.AnswerAoO(AoOAnswer.Proceed))
                .Turn("orc", 1, Step.Pass())
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("Move while prone is refused", Expect.StepStatus("hero", 1, "Move", 0, "refused"))
                .Expect("The crawl is done", Expect.StepStatus("hero", 1, "Crawl", 0, "done"))
                .Expect("Exactly one move of 1 square by the hero", Expect.Count("move", e => e.Str("actor") == "hero" && e.Int("sq") == 1, 1, 1))
                .Expect("No other move by the hero", Expect.Count("move", e => e.Str("actor") == "hero", 1, 1))
                .Expect("The orc's movement AoO comes before the crawl (PHB p.143)",
                    v =>
                    {
                        TraceEvent aoo = v.AoOs("orc", "hero", "movement").FirstOrDefault();
                        TraceEvent mv = v.Moves("hero").FirstOrDefault();
                        if (aoo == null) return ExpectResult.Fail("no movement AoO by the orc");
                        if (mv == null) return ExpectResult.Inconclusive("no crawl move");
                        return aoo.Seq < mv.Seq ? ExpectResult.Pass("AoO before the crawl", aoo.Seq, mv.Seq) : ExpectResult.Fail("AoO after the crawl", aoo.Seq, mv.Seq);
                    })
                .Expect("The hero is still prone at the end of its turn, with the move action spent", v =>
                {
                    TraceEvent end = v.Of("turn_end").FirstOrDefault(e => e.Str("actor") == "hero");
                    if (end == null) return ExpectResult.Fail("no turn_end for the hero");
                    JsonObj econ = end.Get("econ") as JsonObj;
                    bool moveUsed = econ != null && econ.Get("move") is bool m && m;
                    JsonObj snap = (end.Get("diff") as List<JsonObj>)?.FirstOrDefault(o => o.Get("k") as string == "hero") ?? v.Final("hero");
                    if (!HasCond(snap, "Prone")) return ExpectResult.Fail("the hero is not prone", end.Seq);
                    return moveUsed ? ExpectResult.Pass("prone, move used", end.Seq) : ExpectResult.Fail("move action not spent", end.Seq);
                })
                .Build();
        }

        private static ScenarioDef ProneScriptedMove()
        {
            return Rules("rules/prone-scripted-move", "A prone creature that does not stand cannot take ordinary movement (PHB p.143)")
                .Covers("CMB-074", "PHB p.143")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 4)), 5, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .StartCondition("orc", CombatConditionType.Prone, -1)
                .Initiative("fighter", "orc")
                .Turn("fighter", 1, Step.Pass())
                .Turn("orc", 1, Step.Move(15, 10))
                .Expect("The move is refused", Expect.StepStatus("orc", 1, "Move", 0, "refused"))
                .Expect("The orc does not move", Expect.None("move", e => e.Str("actor") == "orc"))
                .Expect("The orc is still prone", v => HasCond(v.Final("orc"), "Prone") ? ExpectResult.Pass("prone") : ExpectResult.Fail("not prone"))
                .Build();
        }

        // ── NPC casting while threatened (SPL-006) ──────────────────────

        /// <summary>
        /// The adept casting Magic Missile next to (or away from) a fighter. Without <paramref name="forceNormal"/> the
        /// AI runs it and (Concentration +5 against DC 16, 50%) casts defensively. <paramref name="forceNormal"/> lowers
        /// its CON to 6, so its Concentration (caster level + CON, CharacterStats.GetSpellcastingConcentrationBonus) is
        /// +3, a 40% chance, below the 50% AISpellcastingStrategist.ShouldCastDefensively wants, so the cast is a
        /// normal one and must provoke. Left to the AI that adept retreats first (Play mode 2026-10-07: it withdrew
        /// on every seed), so in that variant it is Scripted and casts in round 1 through the NPC cast executor
        /// (TryNPCPerformSpellCast, the same ResolveNPCSpellcastProvocation decision).
        /// </summary>
        private static ScenarioDef NpcCast(bool adjacent, bool forceNormal)
        {
            string id = !adjacent ? "rules/npc-cast-unthreatened" : forceNormal ? "rules/npc-cast-threatened-normal" : "rules/npc-cast-threatened";
            string title = !adjacent ? "An unthreatened NPC cast provokes nothing (control)"
                : forceNormal ? "An NPC that does not cast defensively provokes an AoO before its spell (PHB p.140)"
                : "An NPC casting while threatened provokes unless it casts defensively (PHB p.140)";
            ScenarioBuilder b = Rules(id, title)
                .Covers("SPL-006", "PHB p.140", "PHB p.69-70")
                .MaxRounds(2)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 4)), adjacent ? 10 : 4, 10, Control.Scripted)
                .Npc("adept", "arcane_missile_adept", 11, 10, forceNormal ? Control.Scripted : Control.Ai)
                .Initiative("fighter", "adept")
                .Turn("fighter", 0, Step.Pass());
            if (forceNormal)
                b.Tweak("adept", c => c.Stats.CON = 6)
                 .MaxRounds(1)
                 .Turn("adept", 1, Step.Cast(DND35e.Identifiers.SpellNames.MAGIC_MISSILE, "fighter"))
                 .Expect("The cast step ran", Expect.StepStatus("adept", 1, "Cast", 0, "done"));

            if (adjacent)
            {
                if (forceNormal)
                    b.Expect("A normal threatened cast happened (the branch this variant exists for)", v =>
                    {
                        TraceEvent normal = v.Casts("adept").FirstOrDefault(e => e.Int("threateners") >= 1 && !e.Bool("defensive"));
                        if (normal != null)
                            return ExpectResult.Pass("normal cast #" + normal.Seq, normal.Seq);
                        return v.Casts("adept").Any(e => e.Int("threateners") >= 1)
                            ? ExpectResult.Fail("every threatened cast was defensive (the CON tweak no longer forces the normal branch)")
                            : ExpectResult.Inconclusive("the adept never cast while threatened");
                    });
                b.Expect("Each threatened cast: an AoO for the spell before it unless cast defensively, none when defensive", v =>
                {
                    List<TraceEvent> casts = v.Casts("adept").Where(e => e.Int("threateners") >= 1).ToList();
                    if (casts.Count == 0)
                        return ExpectResult.Inconclusive("the adept never cast while threatened (it may have moved away first)");
                    var branches = new List<string>();
                    foreach (TraceEvent c in casts)
                    {
                        TraceEvent turn = v.Of("turn_start").LastOrDefault(e => e.Seq < c.Seq);
                        int from = turn != null ? turn.Seq : 0;
                        List<TraceEvent> aoos = v.AoOs("fighter", "adept", "spellcast").Where(e => e.Seq > from && e.Seq < c.Seq).ToList();
                        bool defensive = c.Bool("defensive");
                        branches.Add(defensive ? "defensive" : "normal");
                        if (defensive && aoos.Count > 0)
                            return ExpectResult.Fail("a defensive cast provoked", aoos[0].Seq, c.Seq);
                        if (!defensive && aoos.Count == 0)
                            return ExpectResult.Fail("a normal threatened cast provoked no spellcast AoO", c.Seq);
                    }
                    return ExpectResult.Pass(string.Join(",", branches));
                });
            }
            else
            {
                b.Expect("No threatened cast", Expect.None("threatened_cast", e => e.Int("threateners") >= 1))
                 .Expect("No spellcast AoO", Expect.None("aoo", e => e.Str("trigger") == "spellcast"));
            }
            return b.Build();
        }

        // ── Maneuvers (CMB-014, CMB-076, CMB-079, CMB-098, CMB-102) ─────

        private static ScenarioDef Trip(string id, string title, bool improved, bool succeed)
        {
            ScenarioBuilder b = Rules(id, title)
                .Covers("CMB-014", "CMB-076", "CMB-079", "PHB p.158")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => improved ? Fighter("Fighter", 6, "Improved Trip") : Fighter("Fighter", 6)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Initiative("fighter", "orc")
                .Force(20, 20, "Trip touch attack")
                .Force(20, succeed ? 18 : 2, "Trip Strength check")
                .Force(20, succeed ? 2 : 18, "Trip defense check")
                .Turn("fighter", 1, Step.Maneuver(SpecialAttackType.Trip, "orc"))
                .Turn("orc", 1, Step.Pass())
                .Expect("The trip step is done", Expect.StepStatus("fighter", 1, "Maneuver", 0, "done"));

            if (improved)
                b.Expect("Improved Trip provokes no AoO (PHB p.96)", Expect.None("aoo", e => e.Str("trigger") == "maneuver"));
            else
                b.Expect("The trip provokes the orc's AoO first (PHB p.158, CMB-076)",
                    Expect.AoOBefore("orc", "fighter", e => e.Ev == "maneuver" && e.Str("by") == "fighter"));

            if (succeed)
            {
                b.Expect("The trip succeeds and the orc is prone", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Trip).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no trip");
                    if (!m.Bool("success")) return ExpectResult.Fail("trip failed", m.Seq);
                    return HasCond(v.Final("orc"), "Prone") ? ExpectResult.Pass("orc prone", m.Seq) : ExpectResult.Fail("orc not prone", m.Seq);
                });
                if (improved)
                    b.ExpectXFail("CMB-079", "Improved Trip gives an immediate melee attack on the tripped foe (PHB p.96)",
                        v =>
                        {
                            TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Trip).FirstOrDefault();
                            if (m == null) return ExpectResult.Inconclusive("no trip");
                            TraceEvent atk = v.Attacks("fighter", "orc", false).FirstOrDefault(e => e.Seq > m.Seq);
                            return atk != null ? ExpectResult.Pass("attack #" + atk.Seq) : ExpectResult.Fail("no attack after the trip", m.Seq);
                        });
            }
            else
            {
                b.Expect("The trip fails and the orc stays standing", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Trip).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no trip");
                    return !m.Bool("success") && !HasCond(v.Final("orc"), "Prone") ? ExpectResult.Pass("failed", m.Seq) : ExpectResult.Fail("success " + m.Get("success"), m.Seq);
                });
                b.ExpectXFail("CMB-079", "After a failed trip the defender may try to trip the attacker (PHB p.158)",
                    v => v.Log("trip").Any(e => e.Str("text").IndexOf("Orc", StringComparison.Ordinal) >= 0 && e.Str("text").IndexOf("counter", StringComparison.OrdinalIgnoreCase) >= 0)
                        ? ExpectResult.Pass("counter-trip logged")
                        : ExpectResult.Fail("no counter-trip"));
            }
            return b.Build();
        }

        private static ScenarioDef TripUi()
        {
            return Rules("rules/maneuver-trip-ui", "A PC trip through the Special Attack menu: AoO first, defender prone (PHB p.158)")
                .Covers("CMB-014", "CMB-076", "PHB p.158", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6)), 10, 10, Control.Ui)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("hero", StripOffHand)
                .Initiative("hero", "orc")
                .Force(20, 20, "Trip touch attack")
                .Force(20, 18, "Trip Strength check")
                .Force(20, 2, "Trip defense check")
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Trip, "orc"), Step.AnswerAoO(AoOAnswer.Proceed))
                .Turn("orc", 1, Step.Pass())
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The trip step is done", Expect.StepStatus("hero", 1, "Maneuver", 0, "done"))
                .Expect("The trip provokes the orc's AoO first (PHB p.158, CMB-076)",
                    Expect.AoOBefore("orc", "hero", e => e.Ev == "maneuver" && e.Str("by") == "hero"))
                .Expect("The trip succeeds and the orc is prone", v =>
                {
                    TraceEvent m = v.Maneuvers("hero", SpecialAttackType.Trip).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no trip");
                    return m.Bool("success") && HasCond(v.Final("orc"), "Prone") ? ExpectResult.Pass("orc prone", m.Seq) : ExpectResult.Fail("success " + m.Get("success"), m.Seq);
                })
                .Build();
        }

        private static ScenarioDef Disarm()
        {
            return Rules("rules/maneuver-disarm", "Disarm: the defender's AoO comes first and foils it if it deals damage (PHB p.155)")
                .Covers("CMB-014", "CMB-076", "PHB p.155")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Initiative("fighter", "orc")
                .Force(20, 20, "Disarm attack roll")
                .Force(20, 1, "Disarm defense roll")
                .Turn("fighter", 1, Step.Maneuver(SpecialAttackType.Disarm, "orc"))
                .Turn("orc", 1, Step.Pass())
                .Expect("The disarm step is done", Expect.StepStatus("fighter", 1, "Maneuver", 0, "done"))
                .Expect("The orc's AoO comes first; a damaging AoO foils the disarm, otherwise the orc is disarmed", v =>
                {
                    TraceEvent aoo = v.AoOs("orc", "fighter", "maneuver").FirstOrDefault();
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Disarm).FirstOrDefault();
                    if (aoo == null) return ExpectResult.Fail("no AoO by the orc");
                    int dmg = AoODamage(v, aoo);
                    if (dmg > 0)
                        return m == null || !m.Bool("success") ? ExpectResult.Pass("AoO dealt " + dmg + ": disarm foiled", aoo.Seq) : ExpectResult.Fail("disarm succeeded after a damaging AoO", aoo.Seq, m.Seq);
                    if (m == null) return ExpectResult.Fail("no disarm after a harmless AoO", aoo.Seq);
                    if (m.Seq < aoo.Seq) return ExpectResult.Fail("AoO after the disarm", aoo.Seq, m.Seq);
                    return m.Bool("success") && HasCond(v.Final("orc"), "Disarmed")
                        ? ExpectResult.Pass("harmless AoO, orc disarmed", aoo.Seq, m.Seq)
                        : ExpectResult.Fail("success " + m.Get("success") + ", disarmed " + HasCond(v.Final("orc"), "Disarmed"), m.Seq);
                })
                .Build();
        }

        private static ScenarioDef BullRush()
        {
            return Rules("rules/maneuver-bullrush", "Bull rush: AoOs from every threatener, push 5 ft + 5 ft per 5 points, a standard action (PHB p.154)")
                .Covers("CMB-098", "CMB-102", "CMB-109", "CMB-110", "PHB p.154")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Npc("goblin", "goblin", 10, 11, Control.Scripted)
                .Initiative("fighter", "orc", "goblin")
                .Force(20, 20, "Bull rush check")
                .Force(20, 1, "Bull rush defense")
                .Turn("fighter", 1, Step.Maneuver(SpecialAttackType.BullRushAttack, "orc"), Step.Maneuver(SpecialAttackType.BullRushAttack, "orc"))
                .Turn("orc", 1, Step.Pass())
                .Turn("goblin", 1, Step.Pass())
                .Expect("The first bull rush is done", Expect.StepStatus("fighter", 1, "Maneuver", 0, "done"))
                .Expect("A second bull rush in the turn is refused (a standard action, CMB-102)", Expect.StepStatus("fighter", 1, "Maneuver", 1, "refused"))
                .Expect("The defender and the other threatener each make an AoO before the bull rush resolves (PHB p.154)", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.BullRushAttack).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no bull rush");
                    TraceEvent byOrc = v.AoOs("orc", null, "maneuver").FirstOrDefault(e => e.Seq < m.Seq);
                    TraceEvent byGoblin = v.AoOs("goblin", null, "maneuver").FirstOrDefault(e => e.Seq < m.Seq);
                    if (byOrc == null || byGoblin == null)
                        return ExpectResult.Fail("orc AoO " + (byOrc != null) + ", goblin AoO " + (byGoblin != null), m.Seq);
                    return ExpectResult.Pass("both AoOs before #" + m.Seq, byOrc.Seq, byGoblin.Seq, m.Seq);
                })
                .Expect("The push is 5 ft plus 5 ft per 5 points of margin at most, straight back, and the attacker follows or stays", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.BullRushAttack).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no bull rush");
                    if (!m.Bool("success")) return ExpectResult.Fail("bull rush failed with forced dice", m.Seq);
                    if (ScenarioChecks.IsDownSnapshot(v.Final("fighter"))) return ExpectResult.Inconclusive("the fighter was dropped");
                    Vector2Int? o = Pos(v.Final("orc")), f = Pos(v.Final("fighter"));
                    if (o == null || f == null) return ExpectResult.Fail("no final positions");
                    int margin = m.Int("check") - m.Int("opposed");
                    int d = o.Value.x - 11;
                    if (o.Value.y != 10 || d < 1 || d > 1 + margin / 5)
                        return ExpectResult.Fail("orc at " + o + " (margin " + margin + ")", m.Seq);
                    bool followed = f.Value == new Vector2Int(10 + d, 10);
                    bool stayed = f.Value == new Vector2Int(10, 10) && d == 1;
                    return followed || stayed
                        ? ExpectResult.Pass("pushed " + d + " (margin " + margin + "), attacker " + (followed ? "followed" : "stayed"), m.Seq)
                        : ExpectResult.Fail("fighter at " + f + " after a push of " + d, m.Seq);
                })
                .Build();
        }

        /// <summary>
        /// CMB-085 (owner decision 2026-10-07): the stability flag from the NPC data reaches the defender's check in a
        /// played trip or bull rush. A 6th-level fighter with Improved Trip and Improved Bull Rush (no AoO from those)
        /// acts against <paramref name="npcId"/>, which makes no AoO either; forced dice (touch attack 20, attacker 20,
        /// defender 1) make the maneuver succeed. The defender's modifier in the trace (opposed total minus its d20)
        /// must be the PHB one: trip (p.158) the better of STR and DEX modifier, bull rush (p.154) the STR modifier,
        /// plus the special size modifier, plus 4 only when the creature is exceptionally stable. The expected
        /// numbers come from the shared database entry, read only (never mutated).
        /// </summary>
        private static ScenarioDef StabilityCheck(string id, string title, string npcId, SpecialAttackType type, bool expectStable)
        {
            bool trip = type == SpecialAttackType.Trip;
            return Rules(id, title)
                .Covers("CMB-085", trip ? "PHB p.158" : "PHB p.154")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6, "Improved Trip", "Improved Bull Rush")), 10, 10, Control.Scripted)
                .Npc("foe", npcId, 11, 10, Control.Scripted)
                .Tweak("foe", SturdyDummyWithoutAoO)
                .Initiative("fighter", "foe")
                .Force(20, 20, "Trip touch attack")
                .Force(20, 20, trip ? "Trip Strength check" : "Bull rush check")
                .Force(20, 1, trip ? "Trip defense check" : "Bull rush defense")
                .Turn("fighter", 1, Step.Maneuver(type, "foe"))
                .Turn("foe", 1, Step.Pass())
                .Expect("The maneuver step is done", Expect.StepStatus("fighter", 1, "Maneuver", 0, "done"))
                .Expect(expectStable ? "The defender's check includes +4 stability" : "The defender's check has no stability bonus", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", type).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no " + type);
                    NPCDefinition def = NPCDatabase.Get(npcId);
                    if (def == null) return ExpectResult.Fail("no NPC entry " + npcId);
                    if (def.IsExceptionallyStable != expectStable)
                        return ExpectResult.Fail(npcId + " IsExceptionallyStable is " + def.IsExceptionallyStable, m.Seq);
                    int strMod = Mathf.FloorToInt((def.STR - 10) / 2f);
                    int dexMod = Mathf.FloorToInt((def.DEX - 10) / 2f);
                    int expected = (trip ? Math.Max(strMod, dexMod) : strMod) + def.SizeCategory.GetGrappleModifier() + (expectStable ? 4 : 0);
                    int actual = m.Int("opposed") - m.Int("opposedRoll");
                    if (m.Int("opposedRoll") != 1)
                        return ExpectResult.Inconclusive("defender d20 " + m.Int("opposedRoll") + " (a tie reroll?)");
                    return actual == expected
                        ? ExpectResult.Pass("defender modifier " + actual + (expectStable ? " with +4 stability" : ", no stability"), m.Seq)
                        : ExpectResult.Fail("defender modifier " + actual + ", expected " + expected, m.Seq);
                })
                .Build();
        }

        /// <summary>
        /// A watching goblin's AoOs during a bull rush (PHB p.154, p.138, p.92; owner decision 2026-10-07). The goblin
        /// (DEX 16; with <paramref name="combatReflexes"/> 4 AoOs a round, else 1) stands at (10,9), so it threatens the
        /// squares (9,10), (10,10) and (11,10). The orc defender at (11,10) makes no AoO, to keep the count to the goblin.
        /// With <paramref name="charge"/> the fighter charges from (4,10) and leaves (9,10) on the way (one movement AoO),
        /// enters the orc's space from (10,10) (the bull rush's own provocation: a second AoO only with Combat Reflexes)
        /// and follows out of (10,10) and (11,10) (movement in the same round: no further AoO). Without a charge the
        /// fighter starts at (10,10): the entry provokes, and the follow is the round's first movement opportunity, so the
        /// goblin with Combat Reflexes takes one more. Forced dice give a margin of at least 15, so the push and follow
        /// run at least 3 squares east.
        /// </summary>
        private static ScenarioDef BullRushWatcherAoOs(bool charge, bool combatReflexes)
        {
            string id = charge
                ? (combatReflexes ? "rules/maneuver-bullrush-charge-reflexes" : "rules/maneuver-bullrush-charge-control")
                : "rules/maneuver-bullrush-reflexes";
            string title = charge
                ? (combatReflexes
                    ? "Charge bull rush: a Combat Reflexes watcher takes the charge-move AoO and the entry AoO, none on the follow (PHB p.154, p.138)"
                    : "Charge bull rush: a watcher without Combat Reflexes spends its one AoO on the charge move (PHB p.137, p.154)")
                : "Bull rush: a Combat Reflexes watcher takes the entry AoO and one follow AoO (PHB p.154, p.138)";

            ScenarioBuilder b = Rules(id, title)
                .Covers("CMB-113", "CMB-128", "PHB p.92", "PHB p.137", "PHB p.138", "PHB p.154")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6)), charge ? 4 : 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Npc("goblin", "goblin", 10, 9, Control.Scripted)
                .Tweak("orc", SturdyDummyWithoutAoO)
                .Tweak("goblin", c =>
                {
                    c.Stats.DEX = 16;
                    if (combatReflexes)
                        c.Stats.Feats.Add("Combat Reflexes");
                })
                .Initiative("goblin", "orc", "fighter")
                .Force(20, 20, "Bull rush check")
                .Force(20, 1, "Bull rush defense")
                .Turn("goblin", 1, Step.Pass())
                .Turn("orc", 1, Step.Pass())
                .Turn("fighter", 1, charge ? Step.Charge("orc", bullRush: true) : Step.Maneuver(SpecialAttackType.BullRushAttack, "orc"))
                .Expect("The orc makes no AoO (fixture check)", Expect.None("aoo", e => e.Str("by") == "orc"))
                .Expect("The bull rush succeeds and the fighter moves with the orc (fixture check)", v =>
                {
                    SpecialAttackType type = charge ? SpecialAttackType.BullRushCharge : SpecialAttackType.BullRushAttack;
                    TraceEvent m = v.Maneuvers("fighter", type).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no bull rush");
                    if (!m.Bool("success")) return ExpectResult.Fail("bull rush failed with forced dice", m.Seq);
                    if (ScenarioChecks.IsDownSnapshot(v.Final("fighter"))) return ExpectResult.Inconclusive("the fighter was dropped");
                    Vector2Int? o = Pos(v.Final("orc")), f = Pos(v.Final("fighter"));
                    if (o == null || f == null) return ExpectResult.Fail("no final positions");
                    return o.Value.y == 10 && o.Value.x >= 14 && f.Value == new Vector2Int(o.Value.x - 1, 10)
                        ? ExpectResult.Pass("orc at " + o + ", fighter at " + f, m.Seq)
                        : ExpectResult.Fail("orc at " + o + ", fighter at " + f, m.Seq);
                });

            if (charge)
            {
                b.Expect("The goblin makes one movement AoO during the charge, before the bull rush (PHB p.137)", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.BullRushCharge).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no bull rush");
                    List<TraceEvent> moveAoOs = v.AoOs("goblin", null, "movement");
                    return moveAoOs.Count == 1 && moveAoOs[0].Seq < m.Seq
                        ? ExpectResult.Pass("movement AoO #" + moveAoOs[0].Seq + " before #" + m.Seq, moveAoOs[0].Seq, m.Seq)
                        : ExpectResult.Fail(moveAoOs.Count + " movement AoOs", moveAoOs.Select(e => e.Seq).ToArray());
                });
                b.Expect(combatReflexes
                    ? "Entering the orc's space provokes a second AoO from the goblin with Combat Reflexes: the bull rush's own provocation (PHB p.154)"
                    : "Without Combat Reflexes the goblin has no AoO left for the entry (one AoO a round, PHB p.137)", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.BullRushCharge).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no bull rush");
                    List<TraceEvent> entry = v.AoOs("goblin", null, "maneuver");
                    if (!combatReflexes)
                        return entry.Count == 0
                            ? ExpectResult.Pass("no entry AoO", m.Seq)
                            : ExpectResult.Fail(entry.Count + " entry AoOs", entry.Select(e => e.Seq).ToArray());
                    return entry.Count == 1 && entry[0].Seq < m.Seq
                        ? ExpectResult.Pass("entry AoO #" + entry[0].Seq + " (at " + entry[0].Str("target") + ") before #" + m.Seq, entry[0].Seq, m.Seq)
                        : ExpectResult.Fail(entry.Count + " entry AoOs", entry.Select(e => e.Seq).ToArray());
                });
                b.Expect("No AoO from the goblin as the fighter follows: its movement opportunity this round went on the charge (PHB p.138)",
                    Expect.None("aoo", e => e.Str("by") == "goblin" && e.Str("trigger") == "bullrush-move"));
            }
            else
            {
                b.Expect("Entering the orc's space provokes the goblin's AoO before the bull rush resolves (PHB p.154)", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.BullRushAttack).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no bull rush");
                    List<TraceEvent> entry = v.AoOs("goblin", null, "maneuver");
                    return entry.Count == 1 && entry[0].Seq < m.Seq
                        ? ExpectResult.Pass("entry AoO #" + entry[0].Seq + " before #" + m.Seq, entry[0].Seq, m.Seq)
                        : ExpectResult.Fail(entry.Count + " entry AoOs", entry.Select(e => e.Seq).ToArray());
                });
                b.Expect("The follow out of the goblin's squares provokes one more AoO: the entry AoO is not a movement opportunity (PHB p.154, p.138)", v =>
                {
                    List<TraceEvent> follow = v.AoOs("goblin", null, "bullrush-move");
                    List<TraceEvent> entry = v.AoOs("goblin", null, "maneuver");
                    return follow.Count == 1 && entry.Count == 1 && follow[0].Seq > entry[0].Seq
                        ? ExpectResult.Pass("follow AoO #" + follow[0].Seq, follow[0].Seq)
                        : ExpectResult.Fail(follow.Count + " follow AoOs, " + entry.Count + " entry AoOs", follow.Concat(entry).Select(e => e.Seq).ToArray());
                });
            }

            return b.Build();
        }

        private static ScenarioDef Grapple()
        {
            return Rules("rules/maneuver-grapple", "Grapple: AoO first (foiled if it deals damage), touch attack, opposed check, both grappled (PHB p.156)")
                .Covers("CMB-014", "CMB-076", "PHB p.156")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Initiative("fighter", "orc")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("fighter", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"))
                .Turn("orc", 1, Step.Pass())
                .Expect("The grapple step is done", Expect.StepStatus("fighter", 1, "Maneuver", 0, "done"))
                .Expect("The orc's AoO comes first; a damaging AoO foils the grapple, otherwise both are grappled", v =>
                {
                    TraceEvent aoo = v.AoOs("orc", "fighter", "maneuver").FirstOrDefault();
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Grapple).FirstOrDefault();
                    if (aoo == null) return ExpectResult.Fail("no AoO by the orc");
                    int dmg = AoODamage(v, aoo);
                    if (dmg > 0)
                        return m == null || !m.Bool("success") ? ExpectResult.Pass("AoO dealt " + dmg + ": grapple foiled", aoo.Seq) : ExpectResult.Fail("grapple succeeded after a damaging AoO", aoo.Seq, m.Seq);
                    if (m == null) return ExpectResult.Fail("no grapple after a harmless AoO", aoo.Seq);
                    if (m.Seq < aoo.Seq) return ExpectResult.Fail("AoO after the grapple", aoo.Seq, m.Seq);
                    bool both = HasCond(v.Final("fighter"), "Grappled") && HasCond(v.Final("orc"), "Grappled");
                    return m.Bool("success") && both ? ExpectResult.Pass("both grappled", aoo.Seq, m.Seq) : ExpectResult.Fail("success " + m.Get("success") + ", both grappled " + both, m.Seq);
                })
                .Build();
        }

        private static ScenarioDef FreeTrip()
        {
            return Rules("rules/maneuver-freetrip", "A wolf's bite that hits trips for free: no touch attack, no AoO (MM p.283, PHB p.158)")
                .Covers("CMB-014", "MM p.283")
                // The wolf's bite (+2 against AC 17 here) hits about one time in four, so give it four rounds and run a seed range.
                .MaxRounds(4)
                .RecordDice()
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6)), 10, 10, Control.Scripted)
                .Npc("wolf", "wolf", 11, 10, Control.Ai)
                .Initiative("wolf", "fighter")
                .Turn("fighter", 0, Step.Pass())
                .Expect("A hit is followed by a trip attempt with no touch attack and no AoO", v =>
                {
                    List<TraceEvent> hits = v.Attacks("wolf", "fighter", false).Where(e => e.Bool("hit")).ToList();
                    if (hits.Count == 0) return ExpectResult.Inconclusive("the wolf never hit");
                    if (v.Of("dice").Any(e => e.Str("ctx") == "Trip touch attack"))
                        return ExpectResult.Fail("a trip touch attack was rolled");
                    if (v.AoOs("fighter", "wolf", "maneuver").Count > 0)
                        return ExpectResult.Fail("the free trip provoked");
                    bool strengthRolled = v.Of("dice").Any(e => e.Str("ctx") == "Trip Strength check" && e.Seq > hits[0].Seq);
                    return strengthRolled ? ExpectResult.Pass(hits.Count + " hits, trip check rolled") : ExpectResult.Fail("no trip check after a hit", hits[0].Seq);
                })
                .Build();
        }

        // ── Single vs full attack (CMB-043, CMB-102) ────────────────────

        private static ScenarioDef SingleVsFull(bool ui)
        {
            Control c = ui ? Control.Ui : Control.Scripted;
            string key = ui ? "hero" : "fighter";
            ScenarioBuilder b = Rules(ui ? "rules/single-vs-full-attack-ui" : "rules/single-vs-full-attack",
                    "One attack after a move, two iteratives on a full attack: +10, then +10/+5 (PHB p.143, Table 8-2)" + (ui ? ", through the PC buttons" : ""))
                .Covers("CMB-043", "CMB-102", "PHB p.143", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc(key, ActorSource.Stats(() =>
                {
                    CharacterStats s = Fighter(ui ? "Hero" : "Fighter", 6, "Weapon Focus");
                    s.WeaponFocusChoice = "Longsword";
                    return s;
                }), 10, 10, c)
                .Npc("dummy", "target_dummy", 12, 10, Control.Scripted)
                .Tweak(key, StripOffHand)
                .Initiative(key, "dummy")
                .Turn("dummy", 0, Step.Pass());
            if (ui)
                b.Turn(key, 1, Step.Move(11, 10), Step.Attack("dummy"))
                 .Turn(key, 2, Step.Attack("dummy"), Step.AttackAgain("dummy"))
                 .Expect("The hero's turns are Ui turns", Expect.Controller(key, "ui"));
            else
                b.Turn(key, 1, Step.Move(11, 10), Step.Attack("dummy"))
                 .Turn(key, 2, Step.FullAttack("dummy"))
                 .Expect("The fighter's turns are scripted turns", Expect.Controller(key, "scripted"));
            // BAB 6 + STR 3 + Weapon Focus 1 = +10; the second iterative is 5 lower.
            b.Expect("Round 1, after a move: one attack at +10", Expect.ModSequence(key, 1, 10))
             .Expect("Round 2, full attack: +10 then +5", Expect.ModSequence(key, 2, 10, 5));
            return b.Build();
        }

        // ── Pin release (CMB-089) ───────────────────────────────────────

        private static ScenarioDef PinRelease()
        {
            return Rules("rules/pin-release-ends-grapple", "Releasing a pin ends the grapple for both (owner decision CMB-089; PHB p.156-157)")
                .Covers("CMB-089", "CMB-122", "PHB p.156-157")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6, "Improved Grapple")), 10, 10, Control.Ui)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Tweak("hero", StripOffHand)
                .Initiative("hero", "orc")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"))
                .Turn("orc", 1, Step.Pass())
                .AiWhenUnscripted("orc")
                .Turn("hero", 2, Step.GrappleAction("Pin"), Step.GrappleAction("ReleasePin"), Step.Move(9, 12))
                .Expect("The hero's turns are Ui turns", Expect.Controller("hero", "ui"))
                .Expect("Round 1: Improved Grapple, no AoO, both grappled", v =>
                {
                    if (v.AoOs(null, "hero", "maneuver").Count > 0) return ExpectResult.Fail("the grapple provoked");
                    TraceEvent m = v.Maneuvers("hero", SpecialAttackType.Grapple).FirstOrDefault();
                    if (m == null || !m.Bool("success")) return ExpectResult.Fail("no successful grapple");
                    JsonObj h = v.Snapshot("hero", 2, "hero"), o = v.Snapshot("orc", 2, "hero");
                    return HasCond(h, "Grappled") && HasCond(o, "Grappled") ? ExpectResult.Pass("both grappled at round 2", m.Seq) : ExpectResult.Fail("not both grappled at round 2", m.Seq);
                })
                .Expect("Round 2: the pin and the release are done", Expect.All(
                    Expect.StepStatus("hero", 2, "GrappleAction(Pin)", 0, "done"),
                    Expect.StepStatus("hero", 2, "GrappleAction(ReleasePin)", 0, "done")))
                .Expect("After the release neither creature is grappled or pinned", v =>
                {
                    TraceEvent release = v.Steps("hero", "GrappleAction(ReleasePin)", 2).FirstOrDefault();
                    if (release == null) return ExpectResult.Fail("no release step");
                    JsonObj h = v.Final("hero"), o = v.Final("orc");
                    foreach (JsonObj s in new[] { h, o })
                        if (HasCond(s, "Grappled") || HasCond(s, "Pinned") || (s != null && s.Get("gr") is bool g && g))
                            return ExpectResult.Fail(s.Get("k") + " still grappled or pinned", release.Seq);
                    return ExpectResult.Pass("grapple ended", release.Seq);
                })
                // Play mode 2026-10-07: after the release both creatures stay in one square and neither can take a
                // move action out of it (no path; a 5-foot step still works), so the move is refused (CMB-122).
                .Waive("CMB-122", ScenarioChecks.GridInv, "(orc and hero|hero and orc) share")
                .ExpectXFail("CMB-122", "The hero moves after the release (the grapple is over; CMB-089 leaves it to the releaser's own move)", v =>
                {
                    TraceEvent release = v.Steps("hero", "GrappleAction(ReleasePin)", 2).FirstOrDefault();
                    if (release == null) return ExpectResult.Fail("no release step");
                    TraceEvent mv = v.Moves("hero", 2).FirstOrDefault(e => e.Seq > release.Seq);
                    return mv != null ? ExpectResult.Pass("move #" + mv.Seq, mv.Seq) : ExpectResult.Fail("no move after the release", release.Seq);
                })
                .Expect("The orc's round-2 turn is an ordinary AI turn, not grappling", v =>
                {
                    TraceEvent t = v.TurnsOf("orc").FirstOrDefault(e => e.Round == 2);
                    if (t == null) return ExpectResult.Inconclusive("no orc turn in round 2");
                    JsonObj o = v.Snapshot("orc", 2, "orc");
                    bool gr = o != null && o.Get("gr") is bool g && g;
                    return t.Str("controller") == "ai" && !gr ? ExpectResult.Pass("ai, free", t.Seq) : ExpectResult.Fail("controller " + t.Str("controller") + ", grappling " + gr, t.Seq);
                })
                .Build();
        }

        private static ScenarioDef PinReleaseFiveFootStep()
        {
            return Rules("rules/pin-release-5ft", "After releasing a pin the releaser can leave the shared square with a 5-foot step (CMB-089)")
                .Covers("CMB-089", "CMB-122", "PHB p.144")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6, "Improved Grapple")), 10, 10, Control.Ui)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Tweak("hero", StripOffHand)
                .Initiative("hero", "orc")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"))
                .Turn("orc", 0, Step.Pass())
                .Turn("hero", 2, Step.GrappleAction("Pin"), Step.GrappleAction("ReleasePin"), Step.FiveFootStep(10, 10))
                .Expect("The pin, the release and the 5-foot step are done", Expect.All(
                    Expect.StepStatus("hero", 2, "GrappleAction(Pin)", 0, "done"),
                    Expect.StepStatus("hero", 2, "GrappleAction(ReleasePin)", 0, "done"),
                    Expect.StepStatus("hero", 2, "FiveFootStep", 0, "done")))
                .Expect("The two creatures end apart and free", v =>
                {
                    JsonObj h = v.Final("hero"), o = v.Final("orc");
                    if (Pos(h) == Pos(o)) return ExpectResult.Fail("both at " + Pos(h));
                    foreach (JsonObj s in new[] { h, o })
                        if (HasCond(s, "Grappled") || HasCond(s, "Pinned"))
                            return ExpectResult.Fail(s.Get("k") + " still grappled or pinned");
                    return ExpectResult.Pass("hero at " + Pos(h) + ", orc at " + Pos(o));
                })
                .Build();
        }

        // ── NPC maneuver replaces an iterative (CMB-102) ────────────────

        private static ScenarioDef NpcManeuverReplacesIterative()
        {
            const string TouchMod = @"Touch attack: d20 \d+ ([+-]\d+) =";
            return Rules("rules/npc-maneuver-replaces-iterative", "An NPC's trip replaces one attack of its sequence at that attack's BAB (PHB p.141 Table 8-2, p.158)")
                .Covers("CMB-102", "PHB p.141", "PHB p.158")
                .MaxRounds(3)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6, "Improved Trip")), 10, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Initiative("fighter", "dummy")
                // A natural 1 always misses the touch attack, so no trip lands and nothing else changes.
                .Force(20, 1, "Trip touch attack", -1)
                .Turn("dummy", 0, Step.Pass())
                .Turn("fighter", 1, Step.Maneuver(SpecialAttackType.Trip, "dummy"), Step.Attack("dummy"), Step.Maneuver(SpecialAttackType.Trip, "dummy"))
                .Turn("fighter", 2, Step.FullAttack("dummy"), Step.Move(10, 13))
                .Turn("fighter", 3, Step.Maneuver(SpecialAttackType.Trip, "dummy"), Step.Maneuver(SpecialAttackType.Trip, "dummy"), Step.Maneuver(SpecialAttackType.Trip, "dummy"))
                .Expect("Round 1: a trip and one attack use both iteratives; a third action is refused", Expect.All(
                    Expect.Count("maneuver", e => e.Round == 1 && e.Str("by") == "fighter", 1, 1),
                    Expect.Count("attack", e => e.Round == 1 && e.Str("attacker") == "fighter" && !e.Bool("aoo"), 1, 1),
                    Expect.StepStatus("fighter", 1, "Maneuver", 1, "refused")))
                .Expect("The attack after the trip is the second iterative (same modifier as the full attack's second)", v =>
                {
                    List<TraceEvent> r1 = v.Attacks("fighter", null, false, 1), r2 = v.Attacks("fighter", null, false, 2);
                    if (r1.Count != 1 || r2.Count != 2) return ExpectResult.Fail("round 1 " + r1.Count + " attacks, round 2 " + r2.Count);
                    return r1[0].Int("mod") == r2[1].Int("mod") && r2[0].Int("mod") - r2[1].Int("mod") == 5
                        ? ExpectResult.Pass("mods " + r1[0].Int("mod") + " vs [" + r2[0].Int("mod") + "," + r2[1].Int("mod") + "]", r1[0].Seq, r2[1].Seq)
                        : ExpectResult.Fail("mods " + r1[0].Int("mod") + " vs [" + r2[0].Int("mod") + "," + r2[1].Int("mod") + "]", r1[0].Seq, r2[1].Seq);
                })
                .Expect("No move after a full attack (PHB p.143)", Expect.StepStatus("fighter", 2, "Move", 0, "refused"))
                .Expect("Round 3: two trips at BAB +6 then +1 (touch modifiers 5 apart); a third is refused", v =>
                {
                    List<KeyValuePair<TraceEvent, int>> mods = v.LogInts(TouchMod, 3);
                    ExpectResult third = Expect.StepStatus("fighter", 3, "Maneuver", 2, "refused")(v);
                    if (!third.IsPass) return third;
                    int trips = v.Maneuvers("fighter", SpecialAttackType.Trip).Count(e => e.Round == 3);
                    // The modifiers come from the combat log line (CharacterController's touch line); a wording change
                    // must not read as a rules failure.
                    if (mods.Count == 0 && trips > 0)
                        return ExpectResult.Fail("touch-attack log line not found (format changed?): " + trips + " trips in round 3 but no line matches " + TouchMod);
                    if (mods.Count != 2) return ExpectResult.Fail(mods.Count + " trip touch attacks in round 3 (" + trips + " trip events)");
                    return mods[0].Value - mods[1].Value == 5
                        ? ExpectResult.Pass("touch mods " + mods[0].Value + ", " + mods[1].Value, mods[0].Key.Seq, mods[1].Key.Seq)
                        : ExpectResult.Fail("touch mods " + mods[0].Value + ", " + mods[1].Value, mods[0].Key.Seq, mods[1].Key.Seq);
                })
                .Build();
        }

        // ── A maneuver replaces any natural attack at that attack's bonus (CMB-102) ──

        /// <summary>
        /// A Human fighter 4 (BAB +4, STR 16) fighting with a primary bite and two secondary claws and no
        /// weapon (strip the kit with <see cref="StripAllWeapons"/>), with Improved Trip so no trip provokes.
        /// </summary>
        private static CharacterStats NaturalBeast(string name, bool multiattack)
        {
            CharacterStats s = multiattack ? Fighter(name, 4, "Improved Trip", "Multiattack") : Fighter(name, 4, "Improved Trip");
            s.NaturalAttacks.Clear();
            s.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, IsPrimary = true });
            s.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, IsPrimary = false, BonusDamageSource = DamageBonusSource.StrengthHalf });
            return s;
        }

        /// <summary>Empties both hands and the gauntlet slot, so the creature fights with its natural attacks.</summary>
        internal static void StripAllWeapons(CharacterController c)
        {
            InventoryComponent inv = c.GetComponent<InventoryComponent>();
            if (inv == null || inv.CharacterInventory == null)
                return;
            inv.CharacterInventory.RightHandSlot = null;
            inv.CharacterInventory.LeftHandSlot = null;
            inv.CharacterInventory.HandsSlot = null;
            inv.CharacterInventory.RecalculateStats();
        }

        /// <summary>The trip touch-attack modifier from a 'maneuver' event whose touch attack missed (check minus the d20).</summary>
        private static int TouchMod(TraceEvent trip) => trip.Int("check") - trip.Int("checkRoll");

        private static ScenarioDef ManeuverReplacesNatural(bool multiattack)
        {
            int secondaryPenalty = multiattack ? 2 : 5;
            string id = multiattack ? "rules/maneuver-replaces-natural-multiattack" : "rules/maneuver-replaces-natural";
            string title = multiattack
                ? "With Multiattack, a trip replacing a secondary natural attack rolls at -2 (MM p.304, p.312)"
                : "A trip replaces any natural attack at that attack's bonus: primary full, secondary -5 (MM p.312, PHB p.141)";
            return Rules(id, title)
                .Covers("CMB-102", "PHB p.141", "MM p.312", "MM p.304")
                .MaxRounds(1)
                .Pc("beast", ActorSource.Stats(() => NaturalBeast("Beast", multiattack)), 10, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("beast", StripAllWeapons)
                .Initiative("beast", "dummy")
                // A natural 1 always misses the touch attack, so no trip lands and nothing else changes.
                .Force(20, 1, "Trip touch attack", -1)
                .Turn("dummy", 0, Step.Pass())
                // BAB +4 is one iterative, yet the trips take the bite and the first claw, the attack the
                // second claw, and a fourth action finds no natural attack left.
                .Turn("beast", 1, Step.Maneuver(SpecialAttackType.Trip, "dummy"), Step.Maneuver(SpecialAttackType.Trip, "dummy"),
                    Step.Attack("dummy"), Step.Maneuver(SpecialAttackType.Trip, "dummy"))
                .Expect("Two trips and one attack are done; the fourth action is refused", Expect.All(
                    Expect.StepStatus("beast", 1, "Maneuver", 0, "done"),
                    Expect.StepStatus("beast", 1, "Maneuver", 1, "done"),
                    Expect.StepStatus("beast", 1, "Attack", 0, "done"),
                    Expect.StepStatus("beast", 1, "Maneuver", 2, "refused")))
                .Expect("The second trip (in place of a secondary claw) rolls " + secondaryPenalty + " lower than the first (in place of the primary bite)", v =>
                {
                    List<TraceEvent> trips = v.Maneuvers("beast", SpecialAttackType.Trip).Where(e => e.Round == 1).ToList();
                    if (trips.Count != 2) return ExpectResult.Fail(trips.Count + " trips");
                    if (trips.Any(t => t.Int("checkRoll") != 1)) return ExpectResult.Fail("a touch attack was not the forced natural 1");
                    int m0 = TouchMod(trips[0]), m1 = TouchMod(trips[1]);
                    return m0 - m1 == secondaryPenalty
                        ? ExpectResult.Pass("touch mods " + m0 + ", " + m1, trips[0].Seq, trips[1].Seq)
                        : ExpectResult.Fail("touch mods " + m0 + ", " + m1, trips[0].Seq, trips[1].Seq);
                })
                // The trace's 'weapon' is recorded inside PerformSingleAttackWithCrit, before FullAttack names the
                // natural attack, so it reads "Unarmed strike"; the claw is told from the bite by its modifier.
                .Expect("The attack is the remaining claw, at the same bonus as the trip that replaced the other claw", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("beast", null, false, 1);
                    TraceEvent clawTrip = v.Maneuvers("beast", SpecialAttackType.Trip).Where(e => e.Round == 1).Skip(1).FirstOrDefault();
                    if (attacks.Count != 1 || clawTrip == null) return ExpectResult.Fail(attacks.Count + " attacks");
                    return attacks[0].Int("mod") == TouchMod(clawTrip)
                        ? ExpectResult.Pass("claw +" + attacks[0].Int("mod"), attacks[0].Seq, clawTrip.Seq)
                        : ExpectResult.Fail("claw mod " + attacks[0].Int("mod") + " vs trip touch mod " + TouchMod(clawTrip), attacks[0].Seq, clawTrip.Seq);
                })
                .Build();
        }

        private static ScenarioDef ManeuverReplacesNaturalUi()
        {
            return Rules("rules/maneuver-replaces-natural-ui", "A PC's Trip button replaces each natural attack in turn at that attack's bonus (MM p.312, PHB p.141)")
                .Covers("CMB-102", "PHB p.141", "MM p.312", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => NaturalBeast("Hero", false)), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("hero", StripAllWeapons)
                .Initiative("hero", "dummy")
                .Force(20, 1, "Trip touch attack", -1)
                .Turn("dummy", 0, Step.Pass())
                .Turn("hero", 1,
                    Step.Maneuver(SpecialAttackType.Trip, "dummy"),
                    Step.Maneuver(SpecialAttackType.Trip, "dummy"),
                    Step.Assert("the bite and the first claw are marked used; the second claw is not", ctx =>
                    {
                        CharacterController hero = ctx.Get("hero");
                        return ctx.Gm.IsNaturalAttackSequenceIndexUsed(hero, 0)
                            && ctx.Gm.IsNaturalAttackSequenceIndexUsed(hero, 1)
                            && !ctx.Gm.IsNaturalAttackSequenceIndexUsed(hero, 2);
                    }),
                    Step.Maneuver(SpecialAttackType.Trip, "dummy"),
                    Step.Maneuver(SpecialAttackType.Trip, "dummy"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("Three trips are done and the used natural attacks are marked", Expect.All(
                    Expect.StepStatus("hero", 1, "Maneuver", 0, "done"),
                    Expect.StepStatus("hero", 1, "Maneuver", 1, "done"),
                    Expect.StepStatus("hero", 1, "Maneuver", 2, "done"),
                    Expect.AssertsPass()))
                .Expect("A fourth trip finds no natural attack left", Expect.StepStatus("hero", 1, "Maneuver", 3, "refused", "dropped"))
                .Expect("Trips at the bite, claw, claw bonuses: the second 5 lower than the first, the third equal to the second", v =>
                {
                    List<TraceEvent> trips = v.Maneuvers("hero", SpecialAttackType.Trip).Where(e => e.Round == 1).ToList();
                    if (trips.Count != 3) return ExpectResult.Fail(trips.Count + " trips");
                    int m0 = TouchMod(trips[0]), m1 = TouchMod(trips[1]), m2 = TouchMod(trips[2]);
                    return m0 - m1 == 5 && m1 == m2
                        ? ExpectResult.Pass("touch mods " + m0 + ", " + m1 + ", " + m2, trips[0].Seq, trips[2].Seq)
                        : ExpectResult.Fail("touch mods " + m0 + ", " + m1 + ", " + m2, trips[0].Seq, trips[2].Seq);
                })
                .Build();
        }

        // ── Haste's extra attack with natural weapons (PHB p.239; owner decision 2026-10-07, CMB-106) ──

        /// <summary>The bite/claw/claw beast without weapons, hasted (Haste's effect only: the extra attack, not the +1).</summary>
        private static void HastedBeast(CharacterController c)
        {
            StripAllWeapons(c);
            c.ApplyHasteEffect(10, null);
        }

        /// <summary>A target dummy with 200 more hit points, so four natural attacks never drop it.</summary>
        private static void SturdyDummy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(200);
            c.Stats.CurrentHP += 200;
        }

        /// <summary>A <see cref="SturdyDummy"/> that makes no attacks of opportunity, so a maneuver against it provokes nothing.</summary>
        private static void SturdyDummyWithoutAoO(CharacterController c)
        {
            SturdyDummy(c);
            c.Stats.CanMakeAttacksOfOpportunity = false;
        }

        /// <summary>Round-1 attack modifiers of <paramref name="key"/> read bite, claw, claw, bite: one Haste attack at the bite's bonus.</summary>
        private static ExpectResult BiteClawClawHasteBite(TraceView v, string key)
        {
            List<TraceEvent> attacks = v.Attacks(key, null, false, 1);
            int[] m = attacks.Select(e => e.Int("mod")).ToArray();
            string mods = "mods [" + string.Join(",", m) + "]";
            int[] seqs = attacks.Select(e => e.Seq).ToArray();
            if (m.Length != 4)
                return ExpectResult.Fail(m.Length + " attacks, " + mods, seqs);
            return m[0] - m[1] == 5 && m[1] == m[2] && m[3] == m[0]
                ? ExpectResult.Pass(mods, seqs)
                : ExpectResult.Fail(mods + ", expected bite, claw (-5), claw, bite", seqs);
        }

        private static ScenarioDef HasteNaturalExtra(bool ui)
        {
            string id = ui ? "rules/haste-natural-extra-ui" : "rules/haste-natural-extra";
            string title = ui
                ? "A hasted PC fighting with natural weapons: the iterative Attack path (OnAttackButtonPressed) makes bite, claw, claw and one Haste bite (PHB p.239, CMB-106)"
                : "A hasted creature's natural full attack adds one natural attack at its normal bonus, the AI's pick (PHB p.239, CMB-106)";
            string key = ui ? "hero" : "beast";
            ScenarioBuilder b = Rules(id, title)
                .Covers("CMB-106", "PHB p.239", "MM p.312", ui ? "PC_NPC_PARITY" : "AI")
                .MaxRounds(1)
                .Pc(key, ActorSource.Stats(() => NaturalBeast(ui ? "Hero" : "Beast", false)), 10, 10, ui ? Control.Ui : Control.Scripted)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak(key, HastedBeast)
                .Tweak("dummy", SturdyDummy)
                .Initiative(key, "dummy")
                .Turn("dummy", 0, Step.Pass());

            if (ui)
            {
                // Each Attack click is one natural step (PerformIterativeSequenceAttack): bite, claw, claw, then
                // with every natural attack used the Haste step with the default natural attack (the bite).
                b.Turn(key, 1, Step.Attack("dummy"), Step.AttackAgain("dummy"), Step.AttackAgain("dummy"),
                        Step.AttackAgain("dummy"), Step.AttackAgain("dummy"))
                 .Expect("The hero's turn is a Ui turn", Expect.Controller(key, "ui"))
                 .Expect("Four attack clicks are done; a fifth finds no attack left", Expect.All(
                     Expect.StepStatus(key, 1, "Attack", 0, "done"),
                     Expect.StepStatus(key, 1, "AttackAgain", 0, "done"),
                     Expect.StepStatus(key, 1, "AttackAgain", 1, "done"),
                     Expect.StepStatus(key, 1, "AttackAgain", 2, "done"),
                     Expect.StepStatus(key, 1, "AttackAgain", 3, "refused", "dropped")));
            }
            else
            {
                // The NPC executor's whole sequence: the AI picks the bite for Haste (no riders, highest bonus).
                b.Turn(key, 1, Step.Attack("dummy"))
                 .Expect("The beast's turn is a scripted turn", Expect.Controller(key, "scripted"))
                 .Expect("The attack step is done", Expect.StepStatus(key, 1, "Attack", 0, "done"));
            }

            return b.Expect("Bite, claw, claw, then one Haste attack at the bite's bonus (PHB p.239; MM p.312; CMB-106)",
                    v => BiteClawClawHasteBite(v, key))
                .Build();
        }

        private static ScenarioDef HasteNaturalButtonsUi()
        {
            // The natural-attack buttons a PC with natural attacks actually has (ActionButtonPanel ->
            // OnNaturalAttackButtonPressed -> CombatFlowService.PerformSingleAttack). After the bite, pressing the
            // bite again makes the Haste bite while both claws are still unused: the attacker picks the weapon and
            // the moment. A third claw press finds nothing left.
            const string key = "hero";
            return Rules("rules/haste-natural-buttons-ui", "A hasted PC picks Haste's extra natural attack with the natural-attack buttons: bite, Haste bite, claw, claw (PHB p.239, CMB-106)")
                .Covers("CMB-106", "PHB p.239", "MM p.312", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc(key, ActorSource.Stats(() => NaturalBeast("Hero", false)), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak(key, HastedBeast)
                .Tweak("dummy", SturdyDummy)
                .Initiative(key, "dummy")
                .Turn("dummy", 0, Step.Pass())
                .Turn(key, 1,
                    Step.NaturalAttack("dummy", "Bite"),
                    Step.NaturalAttack("dummy", "Bite"),
                    Step.Assert("the second bite was the Haste attack; both claws are still unused", ctx =>
                    {
                        CharacterController hero = ctx.Get(key);
                        return hero.ProgressiveAttackPool.HasteExtraNaturalAttackUsed
                            && hero.ProgressiveAttackPool.MainHandStepsUsed == 2
                            && !ctx.Gm.IsNaturalAttackSequenceIndexUsed(hero, 1)
                            && !ctx.Gm.IsNaturalAttackSequenceIndexUsed(hero, 2);
                    }),
                    Step.NaturalAttack("dummy", "Claw"),
                    Step.NaturalAttack("dummy", "Claw"),
                    Step.NaturalAttack("dummy", "Claw"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller(key, "ui"))
                .Expect("Four presses are done and the Haste bite came before the claws", Expect.All(
                    Expect.StepStatus(key, 1, "NaturalAttack", 0, "done"),
                    Expect.StepStatus(key, 1, "NaturalAttack", 1, "done"),
                    Expect.StepStatus(key, 1, "NaturalAttack", 2, "done"),
                    Expect.StepStatus(key, 1, "NaturalAttack", 3, "done"),
                    Expect.AssertsPass()))
                .Expect("A fifth press finds no natural attack left", Expect.StepStatus(key, 1, "NaturalAttack", 4, "refused", "dropped"))
                .Expect("Bite, Haste bite at the bite's bonus, then claw, claw 5 lower (PHB p.239; MM p.312)", v =>
                {
                    List<TraceEvent> attacks = v.Attacks(key, null, false, 1);
                    int[] m = attacks.Select(e => e.Int("mod")).ToArray();
                    string mods = "mods [" + string.Join(",", m) + "]";
                    int[] seqs = attacks.Select(e => e.Seq).ToArray();
                    if (m.Length != 4)
                        return ExpectResult.Fail(m.Length + " attacks, " + mods, seqs);
                    return m[0] == m[1] && m[0] - m[2] == 5 && m[2] == m[3]
                        ? ExpectResult.Pass(mods, seqs)
                        : ExpectResult.Fail(mods + ", expected bite, bite, claw (-5), claw", seqs);
                })
                .Build();
        }

        private static ScenarioDef HasteNaturalMoved()
        {
            return Rules("rules/haste-natural-moved", "A hasted natural-weapon creature that moved makes one attack: Haste's extra attack needs a full attack (PHB p.143, p.239)")
                .Covers("CMB-106", "PHB p.143", "PHB p.239")
                .MaxRounds(1)
                .Pc("beast", ActorSource.Stats(() => NaturalBeast("Beast", false)), 10, 11, Control.Scripted)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("beast", HastedBeast)
                .Tweak("dummy", SturdyDummy)
                .Initiative("beast", "dummy")
                .Turn("dummy", 0, Step.Pass())
                .Turn("beast", 1, Step.Move(10, 10), Step.Attack("dummy"))
                .Expect("The move and the attack step are done", Expect.All(
                    Expect.StepStatus("beast", 1, "Move", 0, "done"),
                    Expect.StepStatus("beast", 1, "Attack", 0, "done")))
                .Expect("One attack, the bite, after the move", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("beast", null, false, 1);
                    return attacks.Count == 1
                        ? ExpectResult.Pass("1 attack, mod " + attacks[0].Int("mod"), attacks[0].Seq)
                        : ExpectResult.Fail(attacks.Count + " attacks", attacks.Select(e => e.Seq).ToArray());
                })
                .Build();
        }

        // ── AI maneuver stopgap (owner decision 2026-10-07, CMB-102 open item 7, AI-060) ──

        /// <summary>
        /// An AI-run fighter 11 (+11/+6/+1, Humanoid profile: trip, then disarm an armed target) against a sturdy,
        /// scripted orc holding a greataxe that makes no attacks of opportunity, so the trip provokes nothing (PHB
        /// p.158) without the fighter needing Improved Trip. The feat is left out on purpose: its free attack after a
        /// trip that lands (PHB p.96) is not built yet (CMB-079) and would add an attack to the count checked here.
        /// The AI chooses every step itself (no typed steps). With the trip forced to land, the stopgap makes the
        /// remaining two steps attacks on the prone orc, where the evaluation alone would disarm next; with every
        /// trip touch attack a natural 1, the failed trip is not retried and the remaining two steps are attacks.
        /// An AI decision limit, not a rule (AI-060).
        /// </summary>
        private static ScenarioDef AiManeuverStopgap(bool tripLands)
        {
            ScenarioBuilder b = Rules(tripLands ? "rules/ai-maneuver-stopgap-trip" : "rules/ai-maneuver-stopgap-trip-fail",
                    tripLands
                        ? "AI stopgap: after its trip lands, an AI fighter attacks the prone target with its remaining iteratives (AI-060)"
                        : "AI stopgap: after its trip fails, an AI fighter does not retry the trip that turn and attacks instead (AI-060)")
                .Covers("CMB-102", "AI-060", "PHB p.141", "PHB p.158")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 11)), 10, 10, Control.Ai)
                .Profile("fighter", () => ScriptableObject.CreateInstance<DND35.AI.Profiles.HumanoidAIProfile>())
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("fighter", StripOffHand)
                .Tweak("orc", SturdyDummyWithoutAoO)
                .Initiative("fighter", "orc")
                .Turn("orc", 0, Step.Pass());
            if (tripLands)
                b.Force(20, 20, "Trip touch attack", -1)
                 .Force(20, 18, "Trip Strength check", -1)
                 .Force(20, 2, "Trip defense check", -1);
            else
                b.Force(20, 1, "Trip touch attack", -1);

            b.Expect("The fighter's turn is an AI turn", Expect.Controller("fighter", "ai"))
             .Expect("The orc makes no AoO, so the trip needs no Improved Trip (fixture check)", Expect.None("aoo", e => e.Str("by") == "orc"))
             .Expect(tripLands
                    ? "One maneuver in the turn: a trip that lands; no disarm or grapple follows (AI-060)"
                    : "One maneuver in the turn: a trip that fails; it is not retried (AI-060)", v =>
             {
                 List<TraceEvent> ms = v.Maneuvers("fighter").Where(e => e.Round == 1).ToList();
                 if (ms.Count == 0) return ExpectResult.Fail("no maneuver: the AI did not trip");
                 TraceEvent first = ms[0];
                 string kinds = string.Join(",", ms.Select(e => Convert.ToString(e.Get("type"))));
                 if (ms.Count != 1) return ExpectResult.Fail(ms.Count + " maneuvers (" + kinds + ")", ms.Select(e => e.Seq).ToArray());
                 if (!(first.Get("type") is SpecialAttackType t) || t != SpecialAttackType.Trip) return ExpectResult.Fail("first maneuver " + kinds, first.Seq);
                 if (first.Bool("success") != tripLands) return ExpectResult.Fail("trip success " + first.Get("success"), first.Seq);
                 bool prone = HasCond(v.Final("orc"), "Prone");
                 return prone == tripLands
                     ? ExpectResult.Pass("1 trip, success " + tripLands + ", orc prone " + prone, first.Seq)
                     : ExpectResult.Fail("orc prone " + prone, first.Seq);
             })
             .Expect("The remaining two iteratives are attacks after the trip, 5 apart (+6, +1; PHB p.143)", v =>
             {
                 TraceEvent trip = v.Maneuvers("fighter", SpecialAttackType.Trip).FirstOrDefault(e => e.Round == 1);
                 if (trip == null) return ExpectResult.Fail("no trip");
                 List<TraceEvent> attacks = v.Attacks("fighter", "orc", false, 1);
                 if (attacks.Count != 2) return ExpectResult.Fail(attacks.Count + " attacks", attacks.Select(e => e.Seq).ToArray());
                 if (attacks.Any(e => e.Seq < trip.Seq)) return ExpectResult.Fail("an attack came before the trip", trip.Seq);
                 return attacks[0].Int("mod") - attacks[1].Int("mod") == 5
                     ? ExpectResult.Pass("mods " + attacks[0].Int("mod") + ", " + attacks[1].Int("mod"), attacks[0].Seq, attacks[1].Seq)
                     : ExpectResult.Fail("mods " + attacks[0].Int("mod") + ", " + attacks[1].Int("mod"), attacks[0].Seq, attacks[1].Seq);
             });
            return b.Build();
        }
    }
}
#endif
