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
    /// Pin duration (owner decision 2026-10-07, CMB-120; PHB p.156): a pin holds the opponent for 1 round; on the
    /// pinner's next turn it may pin again (an opposed check in place of an attack) to hold it another round, and
    /// a pin not renewed that turn ends while the grapple goes on. The AI renews only when that pays (a grapple attack
    /// left for damage, or an able ally and a caster or adjacent ally); a lone one-attack pinner deals damage instead.
    /// Pin release (PHB p.157, CMB-089, CMB-122): the release ends the grapple and moves neither creature; either one
    /// then leaves the shared square with ordinary movement (PHB p.148 forbids only ending a move in an occupied
    /// square), and the one that stays is not moved until it moves itself.
    /// Trip (CMB-079; PHB p.158, p.96): only a creature at most one size category larger can be tripped; a trip lost at
    /// the opposed check lets the defender react at once with a Strength check against the tripper's better of
    /// Strength and Dexterity (the defender's choice: the AI decides for an AI-run defender, a controllable one gets a
    /// prompt, answered here by the runner as each scenario says); after a trip that lands, Improved Trip gives an
    /// immediate melee attack at the bonus of the attack the trip used, which spends no step of the sequence.
    /// Weapon damage by size (CMB-119): DMG Tables 2-2 and 2-3 (p.28) scale a Medium weapon's damage one row per
    /// size category; PHB Table 7-5 (p.116) gives a Small longsword 1d6; Enlarge Person (PHB p.226) and Reduce Person
    /// (p.269) resize the wielder's weapon with it; MM goblin (p.133, morningstar 1d6) and ogre (p.199, greatclub 2d8).
    /// The AI maneuver stopgap (owner decision 2026-10-07, AI-060) is an AI decision limit, not a rule: after a
    /// maneuver lands the AI attacks with its remaining steps, and it does not retry a failed maneuver type against
    /// the same target that turn.
    /// rules/combat-log-pool is not a rules check: it guards the combat log's line pool against the UI-001 leak.
    /// rules/slot-reuse-traits and rules/slot-reuse-plain are not rules checks either: run in that order in one Play
    /// session (filter rules/slot-reuse-*), they fight two encounters in the same enemy pool slots, the first with
    /// trait creatures and the second with plain orc warriors, and check that no orc keeps a trait (CRE-046). Data
    /// checked in the NPC database files on 2026-10-08: allip (incorporeal, Babble aura; spawns dead, CRE-044),
    /// hell_hound (breath weapon), ghast (stench), gibbering_mouther (gibbering aura, spittle, ground manipulation,
    /// blood drain, engulf), troll (regeneration, Large), orc_warrior (Warrior, greataxe, no special trait).
    /// </summary>
    public static class RulesScenarios
    {
        /// <summary>The number of definitions <see cref="All"/> yields (docs/TESTING.md 3.4); a short catalog is a load error.</summary>
        public const int Count = 58;

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
            yield return S("maneuver-trip-counter-ui", () => CounterTripUi(true));
            yield return S("maneuver-trip-counter-ui-decline", () => CounterTripUi(false));
            yield return S("maneuver-trip-counter-ui-ai", () => CounterTripUiAi(true));
            yield return S("maneuver-trip-counter-ui-ai-decline", () => CounterTripUiAi(false));
            yield return S("maneuver-trip-size", TripSize);
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
            yield return S("pin-release-ui-withdraw", () => PinReleaseLeave(true, "withdraw"));
            yield return S("pin-release-ui-charge", () => PinReleaseLeave(true, "charge"));
            yield return S("pin-release-npc-move", () => PinReleaseLeave(false, "move"));
            yield return S("pin-release-npc-withdraw", () => PinReleaseLeave(false, "withdraw"));
            yield return S("pin-release-npc-charge", () => PinReleaseLeave(false, "charge"));
            yield return S("pin-release-ai-stayer", PinReleaseAiStayer);
            yield return S("grappler-feared-stays", GrapplerFearedStays);
            yield return S("pin-duration-ui", PinDurationUi);
            yield return S("pin-duration-ai", PinDurationAi);
            yield return S("pin-duration-ai-one-attack", PinDurationAiOneAttack);
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
            yield return S("weapon-size-damage", WeaponSizeDamage);
            yield return S("combat-log-pool", CombatLogPool);
            yield return S("slot-reuse-traits", SlotReuseTraits);
            yield return S("slot-reuse-plain", SlotReusePlain);
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

        /// <summary>
        /// A scripted fighter 6 (+6/+1) trips an orc berserker. Without Improved Trip the orc's AoO comes first (PHB
        /// p.158). <paramref name="improved"/> with <paramref name="succeed"/>: the trip is followed by the fighter's
        /// Attack step, so the Improved Trip attack (PHB p.96) must come right after the trip at the trip's bonus
        /// (+6), and the Attack step's one attack is the second iterative (+1): 2 attacks, 5 apart, so the follow-up
        /// spent no step. A failed trip (opposed check 2 against 18) lets the AI-run orc trip back; the forced
        /// counter-trip dice (18 against 2) make it land, so the fighter ends prone (CMB-079).
        /// </summary>
        private static ScenarioDef Trip(string id, string title, bool improved, bool succeed)
        {
            ScenarioBuilder b = Rules(id, title)
                .Covers("CMB-014", "CMB-076", "CMB-079", "PHB p.158")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => improved ? Fighter("Fighter", 6, "Improved Trip") : Fighter("Fighter", 6)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("orc", SturdyDummy)
                .Initiative("fighter", "orc")
                .Force(20, 20, "Trip touch attack")
                .Force(20, succeed ? 18 : 2, "Trip Strength check")
                .Force(20, succeed ? 2 : 18, "Trip defense check")
                .Force(20, 18, "Counter-trip check")
                .Force(20, 2, "Counter-trip resist check")
                .Turn("orc", 1, Step.Pass())
                .Expect("The trip step is done", Expect.StepStatus("fighter", 1, "Maneuver", 0, "done"));

            if (improved && succeed)
                b.Turn("fighter", 1, Step.Maneuver(SpecialAttackType.Trip, "orc"), Step.Attack("orc"));
            else
                b.Turn("fighter", 1, Step.Maneuver(SpecialAttackType.Trip, "orc"));

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
                b.Expect("No counter-trip after a trip that lands (PHB p.158)", Expect.None("maneuver", e => e.Bool("counter")));
                if (improved)
                {
                    b.Expect("Improved Trip: an immediate attack on the tripped orc at the trip's bonus, and the next iterative still follows (PHB p.96)", v =>
                    {
                        TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Trip).FirstOrDefault();
                        if (m == null) return ExpectResult.Fail("no trip");
                        List<TraceEvent> attacks = v.Attacks("fighter", "orc", false, 1);
                        int[] seqs = attacks.Select(e => e.Seq).ToArray();
                        if (attacks.Count != 2) return ExpectResult.Fail(attacks.Count + " attacks (expected the follow-up and the +1 iterative)", seqs);
                        if (attacks[0].Seq < m.Seq) return ExpectResult.Fail("an attack came before the trip", seqs);
                        TraceEvent stepDone = v.Steps("fighter", "Attack", 1).FirstOrDefault();
                        if (stepDone != null && stepDone.Seq < attacks[0].Seq) return ExpectResult.Fail("the follow-up came after the Attack step", seqs);
                        int gap = attacks[0].Int("mod") - attacks[1].Int("mod");
                        return gap == 5
                            ? ExpectResult.Pass("follow-up mod " + attacks[0].Int("mod") + ", next iterative " + attacks[1].Int("mod"), seqs)
                            : ExpectResult.Fail("mods " + attacks[0].Int("mod") + ", " + attacks[1].Int("mod") + " (gap " + gap + ", expected 5)", seqs);
                    });
                    b.Expect("The Attack step after the trip is done", Expect.StepStatus("fighter", 1, "Attack", 0, "done"));
                }
                else
                {
                    b.Expect("Without Improved Trip no attack follows the trip", Expect.None("attack", e => e.Str("by") == "fighter"));
                }
            }
            else
            {
                b.Expect("The trip fails and the orc stays standing", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Trip).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no trip");
                    return !m.Bool("success") && !HasCond(v.Final("orc"), "Prone") ? ExpectResult.Pass("failed", m.Seq) : ExpectResult.Fail("success " + m.Get("success"), m.Seq);
                });
                b.Expect("After the failed trip the AI-run orc trips the fighter back at once: no touch attack, no AoO, fighter prone (PHB p.158)", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Trip).FirstOrDefault();
                    TraceEvent counter = v.Maneuvers("orc", SpecialAttackType.Trip).FirstOrDefault(e => e.Bool("counter"));
                    if (m == null) return ExpectResult.Fail("no trip");
                    if (counter == null) return ExpectResult.Fail("no counter-trip by the orc", m.Seq);
                    if (counter.Seq < m.Seq) return ExpectResult.Fail("counter-trip before the trip", m.Seq, counter.Seq);
                    if (counter.Str("target") != "fighter") return ExpectResult.Fail("counter-trip target " + counter.Str("target"), counter.Seq);
                    if (counter.Bool("consumed")) return ExpectResult.Fail("the counter-trip spent an action", counter.Seq);
                    if (v.AoOs("fighter", "orc").Any(e => e.Seq > m.Seq)) return ExpectResult.Fail("the counter-trip provoked an AoO", counter.Seq);
                    if (v.Of("dice").Any(e => e.Str("ctx") == "Trip touch attack" && e.Seq > m.Seq)) return ExpectResult.Fail("the counter-trip rolled a touch attack", counter.Seq);
                    if (!counter.Bool("success")) return ExpectResult.Fail("counter-trip failed with forced dice", counter.Seq);
                    return HasCond(v.Final("fighter"), "Prone")
                        ? ExpectResult.Pass("orc trips back, fighter prone", m.Seq, counter.Seq)
                        : ExpectResult.Fail("fighter not prone", counter.Seq);
                });
                b.Expect("No Improved Trip attack after a failed trip", Expect.None("attack", e => e.Str("by") == "fighter"));
            }
            return b.Build();
        }

        /// <summary>
        /// The counter-trip prompt (PHB p.158, CMB-079): a scripted orc trips a Ui hero (controllable) and loses the opposed
        /// check, so the hero is asked whether it trips back. The runner answers as the scenario says (Trip Back, or
        /// Decline with <paramref name="tripBack"/> false); the orc's Maneuver step settles only after the answer. With
        /// Trip Back the forced dice make the hero's counter-trip land and the orc ends prone; with Decline nothing follows.
        /// </summary>
        private static ScenarioDef CounterTripUi(bool tripBack)
        {
            ScenarioBuilder b = Rules(tripBack ? "rules/maneuver-trip-counter-ui" : "rules/maneuver-trip-counter-ui-decline",
                    tripBack
                        ? "A controllable defender is asked after a failed trip and trips back (PHB p.158, CMB-079)"
                        : "A controllable defender is asked after a failed trip and declines (PHB p.158, CMB-079)")
                .Covers("CMB-079", "PHB p.158", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6)), 10, 10, Control.Ui)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("hero", StripOffHand)
                .Tweak("orc", SturdyDummy)
                .Initiative("orc", "hero")
                .CounterTripAnswer("hero", tripBack)
                .Force(20, 20, "Trip touch attack")
                .Force(20, 2, "Trip Strength check")
                .Force(20, 18, "Trip defense check")
                .Force(20, 18, "Counter-trip check")
                .Force(20, 2, "Counter-trip resist check")
                .Turn("orc", 1, Step.Maneuver(SpecialAttackType.Trip, "hero"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The orc's trip step is done", Expect.StepStatus("orc", 1, "Maneuver", 0, "done"))
                .Expect("The orc's trip fails and the hero stays standing", v =>
                {
                    TraceEvent m = v.Maneuvers("orc", SpecialAttackType.Trip).FirstOrDefault(e => !e.Bool("counter"));
                    if (m == null) return ExpectResult.Fail("no trip by the orc");
                    return !m.Bool("success") && !HasCond(v.Final("hero"), "Prone") ? ExpectResult.Pass("trip failed", m.Seq) : ExpectResult.Fail("success " + m.Get("success"), m.Seq);
                })
                .Expect("The hero's counter-trip prompt was answered " + (tripBack ? "Trip Back" : "Decline"), v =>
                {
                    List<TraceEvent> notes = v.Of("note").Where(e => e.Str("text").StartsWith("counter-trip prompt for hero", StringComparison.Ordinal)).ToList();
                    if (notes.Count != 1) return ExpectResult.Fail(notes.Count + " counter-trip prompts for the hero", notes.Select(e => e.Seq).ToArray());
                    string expected = tripBack ? "answered TripBack" : "answered Decline";
                    return notes[0].Str("text").Contains(expected) ? ExpectResult.Pass(notes[0].Str("text"), notes[0].Seq) : ExpectResult.Fail(notes[0].Str("text"), notes[0].Seq);
                });

            if (tripBack)
                b.Expect("The hero trips the orc back before the orc's step settles: orc prone (PHB p.158)", v =>
                {
                    TraceEvent counter = v.Maneuvers("hero", SpecialAttackType.Trip).FirstOrDefault(e => e.Bool("counter"));
                    TraceEvent step = v.Steps("orc", "Maneuver", 1).FirstOrDefault();
                    if (counter == null) return ExpectResult.Fail("no counter-trip by the hero");
                    if (step != null && step.Seq < counter.Seq) return ExpectResult.Fail("the orc's step settled before the answer", step.Seq, counter.Seq);
                    if (!counter.Bool("success")) return ExpectResult.Fail("counter-trip failed with forced dice", counter.Seq);
                    return HasCond(v.Final("orc"), "Prone") ? ExpectResult.Pass("orc prone", counter.Seq) : ExpectResult.Fail("orc not prone", counter.Seq);
                });
            else
                b.Expect("Declined: no counter-trip, the orc stays standing", v =>
                {
                    if (v.Maneuvers("hero", SpecialAttackType.Trip).Any())
                        return ExpectResult.Fail("the hero tripped after declining");
                    return !HasCond(v.Final("orc"), "Prone") ? ExpectResult.Pass("no counter-trip") : ExpectResult.Fail("orc prone");
                });
            return b.Build();
        }

        /// <summary>
        /// The counter-trip prompt during an AI turn (PHB p.158, CMB-079): an AI-run orc (Humanoid profile, BAB raised to
        /// +6 so it has +6/+1 iteratives, no typed steps) trips a Ui hero who makes no attacks of opportunity, so the trip
        /// provokes nothing without Improved Trip. The trip loses the opposed check, the NPC attack loop suspends while the
        /// hero's prompt is open, and the runner answers it (Trip Back, or Decline with <paramref name="tripBack"/> false).
        /// The loop then resumes: the orc makes its remaining +1 iterative (not a second trip, AI-060) and its turn ends.
        /// </summary>
        private static ScenarioDef CounterTripUiAi(bool tripBack)
        {
            ScenarioBuilder b = Rules(tripBack ? "rules/maneuver-trip-counter-ui-ai" : "rules/maneuver-trip-counter-ui-ai-decline",
                    tripBack
                        ? "An AI tripper's attack loop waits for the hero's counter-trip answer (Trip Back), then resumes (PHB p.158, CMB-079)"
                        : "An AI tripper's attack loop waits for the hero's counter-trip answer (Decline), then resumes (PHB p.158, CMB-079)")
                .Covers("CMB-079", "PHB p.158", "PC_NPC_PARITY", "AI-060")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6)), 10, 10, Control.Ui)
                .Npc("orc", "orc_berserker", 11, 10, Control.Ai)
                .Profile("orc", () => ScriptableObject.CreateInstance<DND35.AI.Profiles.HumanoidAIProfile>())
                .Tweak("hero", c => { StripOffHand(c); SturdyDummyWithoutAoO(c); })
                .Tweak("orc", c => { SturdyDummy(c); c.Stats.BaseAttackBonusOverride = 6; })
                .Initiative("orc", "hero")
                .CounterTripAnswer("hero", tripBack)
                .Force(20, 20, "Trip touch attack")
                .Force(20, 2, "Trip Strength check")
                .Force(20, 18, "Trip defense check")
                .Force(20, 18, "Counter-trip check")
                .Force(20, 2, "Counter-trip resist check")
                .Expect("The orc's turn is an AI turn", Expect.Controller("orc", "ai"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The hero makes no AoO, so the trip provokes nothing (fixture check)", Expect.None("aoo", e => e.Str("by") == "hero"))
                .Expect("The orc trips once and the trip fails; it is not retried (AI-060)", v =>
                {
                    List<TraceEvent> ms = v.Maneuvers("orc").Where(e => e.Round == 1 && !e.Bool("counter")).ToList();
                    if (ms.Count != 1) return ExpectResult.Fail(ms.Count + " maneuvers by the orc", ms.Select(e => e.Seq).ToArray());
                    if (!(ms[0].Get("type") is SpecialAttackType t) || t != SpecialAttackType.Trip) return ExpectResult.Fail("maneuver " + ms[0].Get("type"), ms[0].Seq);
                    return !ms[0].Bool("success") ? ExpectResult.Pass("one failed trip", ms[0].Seq) : ExpectResult.Fail("the trip landed", ms[0].Seq);
                })
                .Expect("Exactly one counter-trip prompt, answered " + (tripBack ? "Trip Back" : "Decline"), v =>
                {
                    List<TraceEvent> notes = v.Of("note").Where(e => e.Str("text").StartsWith("counter-trip prompt for hero", StringComparison.Ordinal)).ToList();
                    if (notes.Count != 1) return ExpectResult.Fail(notes.Count + " counter-trip prompts for the hero", notes.Select(e => e.Seq).ToArray());
                    string expected = tripBack ? "answered TripBack" : "answered Decline";
                    return notes[0].Str("text").Contains(expected) ? ExpectResult.Pass(notes[0].Str("text"), notes[0].Seq) : ExpectResult.Fail(notes[0].Str("text"), notes[0].Seq);
                })
                .Expect("No orc attack while the prompt is open; after the answer the orc makes its remaining iterative, and its turn ends after that", v =>
                {
                    TraceEvent trip = v.Maneuvers("orc", SpecialAttackType.Trip).FirstOrDefault(e => !e.Bool("counter"));
                    TraceEvent note = v.Of("note").FirstOrDefault(e => e.Str("text").StartsWith("counter-trip prompt for hero", StringComparison.Ordinal));
                    if (trip == null || note == null) return ExpectResult.Fail("no trip or no prompt");
                    List<TraceEvent> attacks = v.Attacks("orc", "hero", false, 1);
                    int[] seqs = attacks.Select(e => e.Seq).ToArray();
                    if (attacks.Any(e => e.Seq > trip.Seq && e.Seq < note.Seq)) return ExpectResult.Fail("the orc attacked while the prompt was open", seqs);
                    List<TraceEvent> after = attacks.Where(e => e.Seq > note.Seq).ToList();
                    if (after.Count != 1) return ExpectResult.Fail(after.Count + " orc attacks after the answer (expected the +1 iterative)", seqs);
                    TraceEvent end = v.Of("turn_end").FirstOrDefault(e => e.Str("actor") == "orc" && e.Round == 1);
                    if (end == null) return ExpectResult.Fail("the orc's turn never ended", seqs);
                    return end.Seq > after[0].Seq
                        ? ExpectResult.Pass("trip " + trip.Seq + ", answer " + note.Seq + ", attack " + after[0].Seq + ", turn end " + end.Seq, trip.Seq, note.Seq, after[0].Seq, end.Seq)
                        : ExpectResult.Fail("the turn ended before the attack", after[0].Seq, end.Seq);
                })
                .Expect("The hero's own turn follows the orc's", v =>
                    v.Of("turn_start").Any(e => e.Str("actor") == "hero" && e.Round == 1)
                        ? ExpectResult.Pass("hero turn started")
                        : ExpectResult.Fail("no hero turn"));

            if (tripBack)
                b.Expect("The hero trips the orc back before the orc's next attack: orc prone (PHB p.158)", v =>
                {
                    List<TraceEvent> counters = v.Maneuvers("hero", SpecialAttackType.Trip).Where(e => e.Bool("counter")).ToList();
                    if (counters.Count != 1) return ExpectResult.Fail(counters.Count + " counter-trips by the hero", counters.Select(e => e.Seq).ToArray());
                    TraceEvent counter = counters[0];
                    if (!counter.Bool("success")) return ExpectResult.Fail("counter-trip failed with forced dice", counter.Seq);
                    if (v.Attacks("orc", "hero", false, 1).Any(e => e.Seq < counter.Seq && e.Seq > v.Maneuvers("orc", SpecialAttackType.Trip).First().Seq))
                        return ExpectResult.Fail("an orc attack came between its trip and the counter-trip", counter.Seq);
                    return HasCond(v.Final("orc"), "Prone") ? ExpectResult.Pass("orc prone", counter.Seq) : ExpectResult.Fail("orc not prone", counter.Seq);
                });
            else
                b.Expect("Declined: no counter-trip, the orc stays standing", v =>
                {
                    if (v.Maneuvers("hero", SpecialAttackType.Trip).Any())
                        return ExpectResult.Fail("the hero tripped after declining");
                    return !HasCond(v.Final("orc"), "Prone") ? ExpectResult.Pass("no counter-trip") : ExpectResult.Fail("orc prone");
                });
            return b.Build();
        }

        /// <summary>
        /// The trip size limit (PHB p.158, CMB-079): a Small halfling fighter cannot trip a Large ogre (two categories
        /// larger); the attempt is refused before any cost or AoO. The same fighter may trip a Medium orc (one larger);
        /// that touch attack is forced to a natural 1, so only the attempt is checked.
        /// </summary>
        private static ScenarioDef TripSize()
        {
            return Rules("rules/maneuver-trip-size", "Only a creature at most one size category larger can be tripped (PHB p.158, CMB-079)")
                .Covers("CMB-079", "PHB p.158")
                .MaxRounds(1)
                .Pc("halfling", ActorSource.Stats(() => FighterOfRace("Halfling", "Halfling")), 10, 10, Control.Scripted)
                .Npc("ogre", "ogre", 11, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 9, 10, Control.Scripted)
                .Tweak("ogre", SturdyDummy)
                .Tweak("orc", SturdyDummy)
                .Initiative("halfling", "ogre", "orc")
                .Force(20, 1, "Trip touch attack", -1)
                .Turn("halfling", 1, Step.Maneuver(SpecialAttackType.Trip, "ogre"), Step.Maneuver(SpecialAttackType.Trip, "orc"))
                .Turn("ogre", 1, Step.Pass())
                .Turn("orc", 1, Step.Pass())
                .Expect("The halfling is Small and the ogre Large (fixture check)", v =>
                    v.Of("actor").Any(e => e.Str("key") == "halfling" && Convert.ToString(e.Get("size")) == "Small")
                    && v.Of("actor").Any(e => e.Str("key") == "ogre" && Convert.ToString(e.Get("size")) == "Large")
                        ? ExpectResult.Pass("Small vs Large")
                        : ExpectResult.Fail("sizes " + string.Join(", ", v.Of("actor").Select(e => e.Str("key") + "=" + Convert.ToString(e.Get("size"))))))
                .Expect("The trip of the Large ogre is refused", Expect.StepStatus("halfling", 1, "Maneuver", 0, "refused"))
                .Expect("No trip of the ogre and no AoO by it", v =>
                    !v.Maneuvers("halfling").Any(e => e.Str("target") == "ogre") && !v.AoOs("ogre").Any()
                        ? ExpectResult.Pass("refused before any cost or AoO")
                        : ExpectResult.Fail("the ogre was tripped at or made an AoO"))
                .Expect("The trip of the Medium orc is attempted", Expect.StepStatus("halfling", 1, "Maneuver", 1, "done"))
                .Expect("The orc trip is the halfling's only maneuver, and it spent the standard action", v =>
                {
                    List<TraceEvent> ms = v.Maneuvers("halfling");
                    return ms.Count == 1 && ms[0].Str("target") == "orc"
                        ? ExpectResult.Pass("one trip, at the orc", ms[0].Seq)
                        : ExpectResult.Fail(ms.Count + " maneuvers", ms.Select(e => e.Seq).ToArray());
                })
                .Build();
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

        // ── Pin release (CMB-089, CMB-122) ──────────────────────────────

        /// <summary>The orc's square, which the hero moves into when it grapples (PHB p.156) and both share after the release.</summary>
        private static readonly Vector2Int SharedSquare = new Vector2Int(11, 10);

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
                .Expect("The hero leaves the shared square with a move action after the release and ends at (9,12) (CMB-122; PHB p.148, p.157)", v =>
                {
                    TraceEvent release = v.Steps("hero", "GrappleAction(ReleasePin)", 2).FirstOrDefault();
                    if (release == null) return ExpectResult.Fail("no release step");
                    TraceEvent mv = v.Moves("hero", 2).FirstOrDefault(e => e.Seq > release.Seq);
                    if (mv == null) return ExpectResult.Fail("no move after the release", release.Seq);
                    if (!(mv.Get("from") is Vector2Int start) || start != SharedSquare) return ExpectResult.Fail("the first move starts at " + mv.Get("from") + ", not the shared square " + SharedSquare, mv.Seq);
                    // Read the square at the orc's round-2 turn start: the AI orc may grapple or bull rush the hero later.
                    JsonObj hs = v.Snapshot("hero", 2, "orc");
                    if (ScenarioChecks.IsDownSnapshot(hs)) return ExpectResult.Inconclusive("the hero was dropped by an AoO");
                    Vector2Int? h = Pos(hs);
                    return h == new Vector2Int(9, 12) ? ExpectResult.Pass("move #" + mv.Seq + " from " + start + ", hero at " + h, mv.Seq) : ExpectResult.Fail("hero ends at " + h, mv.Seq);
                })
                .Expect("The Move step is done", Expect.StepStatus("hero", 2, "Move", 0, "done"))
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

        /// <summary>
        /// After a pin release both creatures stay in one square (CMB-089). Either one leaves it with ordinary movement
        /// through the shared path-finding and movement executors (CMB-122): a withdraw or a charge by the Ui hero
        /// (released at the start of its round-3 renewal turn, a free action, so the full round is left), or a move,
        /// withdraw or charge by the scripted orc on the NPC path. The creature that stays is not moved: it remains in
        /// the square until it moves itself (PHB p.148 forbids only ending a move there; PHB p.157 moves only an escaper).
        /// </summary>
        private static ScenarioDef PinReleaseLeave(bool heroLeaves, string how)
        {
            string leaver = heroLeaves ? "hero" : "orc";
            string stayer = heroLeaves ? "orc" : "hero";
            string id = "rules/pin-release-" + (heroLeaves ? "ui-" : "npc-") + how;
            int leaveRound = heroLeaves ? 3 : 2;
            string stepLabel = how == "move" ? "Move" : how == "withdraw" ? "Withdraw" : "Charge";
            Step leave = how == "move" ? Step.Move(13, 10)
                : how == "withdraw" ? (heroLeaves ? Step.Withdraw(7, 10) : Step.Withdraw(14, 10))
                : Step.Charge(heroLeaves ? "goblin" : "ally");
            string title = (heroLeaves ? "The Ui releaser" : "The released NPC") + " leaves the square shared after a pin release with a "
                + how + "; the other creature stays (CMB-122; PHB p.148, p.157" + (how == "charge" ? ", p.154" : how == "withdraw" ? ", p.143" : "") + ")";

            ScenarioBuilder b = Rules(id, title)
                .Covers("CMB-122", "CMB-089", "PHB p.148", "PHB p.157", "PC_NPC_PARITY")
                .MaxRounds(leaveRound)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6, "Improved Grapple")), 10, 10, Control.Ui)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Tweak("hero", StripOffHand)
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"));
            if (how == "charge" && heroLeaves)
                b.Npc("goblin", "goblin", 16, 10, Control.Idle).Initiative("hero", "orc", "goblin");
            else if (how == "charge")
                b.Pc("ally", ActorSource.Stats(() => Fighter("Ally", 6)), 16, 10, Control.Idle).Initiative("hero", "orc", "ally");
            else
                b.Initiative("hero", "orc");

            if (heroLeaves)
            {
                // Round 2 pins; round 3 is the pinner's renewal turn, where the release is free (PHB p.157, CMB-120).
                b.Turn("orc", 0, Step.Pass())
                 .Turn("hero", 2, Step.GrappleAction("Pin"))
                 .Turn("hero", 3, Step.GrappleAction("ReleasePin"), leave);
            }
            else
            {
                b.Turn("orc", 1, Step.Pass())
                 .Turn("hero", 2, Step.GrappleAction("Pin"), Step.GrappleAction("ReleasePin"))
                 .Turn("orc", 2, leave);
            }

            return b
                .Expect("The release is done", Expect.StepStatus("hero", leaveRound, "GrappleAction(ReleasePin)", 0, "done"))
                .Expect("Both creatures share the square when the " + leaver + "'s turn begins (no forced separation)", v =>
                {
                    JsonObj h = v.Snapshot("hero", leaveRound, leaver), o = v.Snapshot("orc", leaveRound, leaver);
                    if (h == null || o == null) return ExpectResult.Fail("no snapshot at the " + leaver + "'s round-" + leaveRound + " turn start");
                    if (heroLeaves)
                        return Pos(h) == SharedSquare && Pos(o) == SharedSquare && HasCond(o, "Pinned")
                            ? ExpectResult.Pass("both at " + SharedSquare + ", orc pinned until the release") : ExpectResult.Fail("hero at " + Pos(h) + ", orc at " + Pos(o));
                    bool free = !HasCond(h, "Grappled") && !HasCond(o, "Grappled") && !HasCond(o, "Pinned");
                    return Pos(h) == SharedSquare && Pos(o) == SharedSquare && free
                        ? ExpectResult.Pass("both at " + SharedSquare + ", free") : ExpectResult.Fail("hero at " + Pos(h) + ", orc at " + Pos(o) + ", free " + free);
                })
                .Expect("The " + stepLabel + " step is done", Expect.StepStatus(leaver, leaveRound, stepLabel, 0, "done"))
                .Expect("The " + leaver + "'s first move after the release starts in the shared square and leaves it (CMB-122)", v =>
                {
                    TraceEvent release = v.Steps("hero", "GrappleAction(ReleasePin)").FirstOrDefault();
                    if (release == null) return ExpectResult.Fail("no release step");
                    TraceEvent mv = v.Moves(leaver, leaveRound).FirstOrDefault(e => e.Seq > release.Seq);
                    if (mv == null) return ExpectResult.Fail("no move after the release", release.Seq);
                    return mv.Get("from") is Vector2Int start && start == SharedSquare && mv.Get("to") is Vector2Int to && to != SharedSquare
                        ? ExpectResult.Pass(mv.Str("type") + " " + start + " -> " + to, mv.Seq)
                        : ExpectResult.Fail("first move " + mv.Get("from") + " -> " + mv.Get("to"), mv.Seq);
                })
                .Expect("The " + stayer + " stays in the shared square; the " + leaver + " ends elsewhere", v =>
                {
                    JsonObj st = v.Final(stayer), lv = v.Final(leaver);
                    if (ScenarioChecks.IsDownSnapshot(lv)) return ExpectResult.Inconclusive("the " + leaver + " was dropped by an AoO");
                    return Pos(st) == SharedSquare && Pos(lv) != SharedSquare
                        ? ExpectResult.Pass(stayer + " at " + Pos(st) + ", " + leaver + " at " + Pos(lv))
                        : ExpectResult.Fail(stayer + " at " + Pos(st) + ", " + leaver + " at " + Pos(lv));
                })
                .Build();
        }

        /// <summary>
        /// The Ui hero pins and releases the orc, then ends its turn in the shared square. The orc's own AI turn starts in
        /// that square with an enemy at distance 0 (CMB-122's NPC symptom: an AI that advanced spent its move action and
        /// stayed). The AI must leave the square by a move or a 5-foot step and then act against the hero.
        /// </summary>
        private static ScenarioDef PinReleaseAiStayer()
        {
            return Rules("rules/pin-release-ai-stayer", "The AI orc left in the square shared after a pin release leaves it on its own turn and acts (CMB-122; PHB p.148, p.157)")
                .Covers("CMB-122", "CMB-089", "PHB p.148", "PHB p.157", "PC_NPC_PARITY")
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
                .Turn("hero", 2, Step.GrappleAction("Pin"), Step.GrappleAction("ReleasePin"), Step.EndTurn())
                .Expect("The pin and the release are done", Expect.All(
                    Expect.StepStatus("hero", 2, "GrappleAction(Pin)", 0, "done"),
                    Expect.StepStatus("hero", 2, "GrappleAction(ReleasePin)", 0, "done")))
                .Expect("The orc's round-2 turn is an AI turn that starts free in the shared square", v =>
                {
                    TraceEvent t = v.TurnsOf("orc").FirstOrDefault(e => e.Round == 2);
                    if (t == null) return ExpectResult.Fail("no orc turn in round 2");
                    JsonObj o = v.Snapshot("orc", 2, "orc"), h = v.Snapshot("hero", 2, "orc");
                    bool free = o != null && !HasCond(o, "Grappled") && !HasCond(o, "Pinned") && !(o.Get("gr") is bool g && g);
                    return t.Str("controller") == "ai" && free && Pos(o) == SharedSquare && Pos(h) == SharedSquare
                        ? ExpectResult.Pass("ai, free, both at " + SharedSquare, t.Seq)
                        : ExpectResult.Fail("controller " + t.Str("controller") + ", free " + free + ", orc at " + Pos(o) + ", hero at " + Pos(h), t.Seq);
                })
                .Expect("The orc leaves the shared square on its round-2 turn (a move or a 5-foot step)", v =>
                {
                    TraceEvent mv = v.Moves("orc", 2).FirstOrDefault();
                    JsonObj o = v.Final("orc");
                    if (mv == null)
                        return ScenarioChecks.IsDownSnapshot(o) ? ExpectResult.Inconclusive("the orc went down before moving") : ExpectResult.Fail("the orc did not move in round 2; it ends at " + Pos(o));
                    return mv.Get("from") is Vector2Int start && start == SharedSquare && mv.Get("to") is Vector2Int to && to != SharedSquare
                        ? ExpectResult.Pass(mv.Str("type") + " " + start + " -> " + to, mv.Seq)
                        : ExpectResult.Fail("first move " + mv.Get("from") + " -> " + mv.Get("to"), mv.Seq);
                })
                .Expect("After leaving, the orc attacks the hero or uses a maneuver on it in round 2", v =>
                {
                    TraceEvent mv = v.Moves("orc", 2).FirstOrDefault();
                    if (mv == null) return ExpectResult.Inconclusive("no orc move in round 2 (see the previous expectation)");
                    if (ScenarioChecks.IsDownSnapshot(v.Final("orc"))) return ExpectResult.Inconclusive("the orc went down");
                    TraceEvent act = v.Attacks("orc", "hero", aoo: false, round: 2).FirstOrDefault(e => e.Seq > mv.Seq)
                        ?? v.Maneuvers("orc").FirstOrDefault(e => e.Round == 2 && e.Seq > mv.Seq && e.Str("target") == "hero");
                    return act != null ? ExpectResult.Pass(act.Ev + " #" + act.Seq, act.Seq) : ExpectResult.Fail("no attack or maneuver on the hero after the move", mv.Seq);
                })
                .Build();
        }

        /// <summary>
        /// A grappling creature takes no ordinary movement (PHB p.156). Grapplers share a square and path-finding accepts a
        /// shared start (CMB-122), so the movement budget keeps a frightened grappler in the grapple: the fear compulsion
        /// finds no square to flee to and falls to its cornered branch. The harness applies the Frightened condition (the
        /// effect of a failed save against Cause Fear) from the priest after the hero grapples the orc.
        /// </summary>
        private static ScenarioDef GrapplerFearedStays()
        {
            return Rules("rules/grappler-feared-stays", "A frightened grappler does not flee out of the grapple (PHB p.156; CMB-122)")
                .Covers("CMB-122", "PHB p.156", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6, "Improved Grapple")), 10, 10, Control.Ui)
                .Pc("priest", ActorSource.Stats(() => Fighter("Priest", 4)), 5, 10, Control.Scripted)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Tweak("hero", StripOffHand)
                .Initiative("hero", "priest", "orc")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"))
                .Script("priest", FrightenOrc)
                .Expect("The orc is frightened and grappling at its round-1 turn", v =>
                {
                    JsonObj o = v.Snapshot("orc", 1, "orc");
                    if (o == null) return ExpectResult.Fail("no orc snapshot at its round-1 turn");
                    bool gr = o.Get("gr") is bool g && g;
                    return HasCond(o, "Frightened") && gr ? ExpectResult.Pass("frightened, grappling") : ExpectResult.Fail("frightened " + HasCond(o, "Frightened") + ", grappling " + gr);
                })
                .Expect("The orc's turns are AI turns (fear compulsions stay with the AI)", v =>
                {
                    List<TraceEvent> turns = v.TurnsOf("orc");
                    if (turns.Count == 0) return ExpectResult.Fail("no orc turn");
                    TraceEvent other = turns.FirstOrDefault(e => e.Str("controller") != "ai");
                    return other == null ? ExpectResult.Pass(turns.Count + " ai turns") : ExpectResult.Fail("controller " + other.Str("controller"), other.Seq);
                })
                .Expect("Neither grappler moves after the grapple, and both end in the shared square still grappling", v =>
                {
                    TraceEvent grapple = v.Maneuvers("hero", SpecialAttackType.Grapple).FirstOrDefault();
                    if (grapple == null || !grapple.Bool("success")) return ExpectResult.Fail("no successful grapple");
                    TraceEvent mv = v.Moves("orc").FirstOrDefault(e => e.Seq > grapple.Seq) ?? v.Moves("hero").FirstOrDefault(e => e.Seq > grapple.Seq);
                    if (mv != null) return ExpectResult.Fail(mv.Str("actor") + " moved " + mv.Get("from") + " -> " + mv.Get("to") + " (" + mv.Str("type") + ")", mv.Seq);
                    JsonObj o = v.Final("orc"), h = v.Final("hero");
                    bool og = o != null && o.Get("gr") is bool a && a, hg = h != null && h.Get("gr") is bool b && b;
                    return Pos(o) == SharedSquare && Pos(h) == SharedSquare && og && hg
                        ? ExpectResult.Pass("both at " + SharedSquare + ", grappling", grapple.Seq)
                        : ExpectResult.Fail("orc at " + Pos(o) + " grappling " + og + ", hero at " + Pos(h) + " grappling " + hg, grapple.Seq);
                })
                .Build();
        }

        /// <summary>Priest script: frightens the orc once (the harness stand-in for a failed save against Cause Fear).</summary>
        private static System.Collections.IEnumerator FrightenOrc(ScenarioContext ctx, CharacterController priest)
        {
            CharacterController orc = ctx.Get("orc");
            if (orc != null && orc.Stats != null && !orc.Stats.IsDead && orc.IsGrappling() && !orc.HasCondition(CombatConditionType.Frightened))
            {
                ctx.Gm.ApplyCondition(orc, CombatConditionType.Frightened, 3, priest, sourceCategory: "Harness");
                ctx.Note("priest frightens the orc");
            }
            yield break;
        }

        // ── Pin duration (CMB-120) ──────────────────────────────────────

        private static ScenarioDef PinDurationUi()
        {
            return Rules("rules/pin-duration-ui", "A pin lasts 1 round; pinning again on the next turn holds it another round, otherwise it ends and the grapple goes on (PHB p.156, CMB-120)")
                .Covers("CMB-120", "CMB-089", "PHB p.156", "PC_NPC_PARITY")
                .MaxRounds(4)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6, "Improved Grapple")), 10, 10, Control.Ui)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Tweak("hero", StripOffHand)
                .Initiative("hero", "orc")
                // Every grapple check is a 20 for both sides; ties go to the higher modifier (the hero's +13).
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"))
                .Turn("orc", 0, Step.Pass())
                .Turn("hero", 2, Step.GrappleAction("Pin"))
                .Turn("hero", 3, Step.GrappleAction("Pin"))
                // Round 4: no steps, so the hero ends the turn without pinning again.
                .Expect("The hero's turns are Ui turns", Expect.Controller("hero", "ui"))
                .Expect("Round 2 pins, round 3 pins again", Expect.All(
                    Expect.StepStatus("hero", 2, "GrappleAction(Pin)", 0, "done"),
                    Expect.StepStatus("hero", 3, "GrappleAction(Pin)", 0, "done")))
                .Expect("The orc is pinned on its round-2 turn and still pinned when the hero's round-3 turn starts", v =>
                {
                    JsonObj o2 = v.Snapshot("orc", 2, "orc"), o3 = v.Snapshot("orc", 3, "hero");
                    if (!HasCond(o2, "Pinned")) return ExpectResult.Fail("orc not pinned on its round-2 turn");
                    return HasCond(o3, "Pinned") ? ExpectResult.Pass("pinned at both") : ExpectResult.Fail("the pin ended before the hero could pin again");
                })
                .Expect("Pinning again on the next turn holds the orc another round (PHB p.156)", v =>
                {
                    List<TraceEvent> renewed = v.Log("stays pinned for another round").Where(e => e.Round == 3).ToList();
                    if (renewed.Count == 0) return ExpectResult.Fail("no renewal log in round 3");
                    JsonObj o3 = v.Snapshot("orc", 3, "orc");
                    return HasCond(o3, "Pinned") ? ExpectResult.Pass("orc pinned on its round-3 turn", renewed[0].Seq) : ExpectResult.Fail("orc not pinned on its round-3 turn", renewed[0].Seq);
                })
                .Expect("Without a new pin the pin ends at the end of the hero's round-4 turn; the grapple goes on", v =>
                {
                    List<TraceEvent> ended = v.Log("ends after 1 round").Where(e => e.Round == 4).ToList();
                    if (ended.Count == 0) return ExpectResult.Fail("no pin-end log in round 4");
                    JsonObj h4 = v.Snapshot("orc", 4, "hero"), o4 = v.Snapshot("orc", 4, "orc");
                    if (!HasCond(h4, "Pinned")) return ExpectResult.Fail("the pin had already ended when the hero's round-4 turn started", ended[0].Seq);
                    if (o4 == null) return ExpectResult.Inconclusive("no orc turn in round 4");
                    return !HasCond(o4, "Pinned") && HasCond(o4, "Grappled")
                        ? ExpectResult.Pass("orc grappled, not pinned, on its round-4 turn", ended[0].Seq)
                        : ExpectResult.Fail("orc round 4: pinned " + HasCond(o4, "Pinned") + ", grappled " + HasCond(o4, "Grappled"), ended[0].Seq);
                })
                .Build();
        }

        private static ScenarioDef PinDurationAi()
        {
            return Rules("rules/pin-duration-ai", "An AI pinner with a second attack this turn pins again on its next turn before anything else (PHB p.156, CMB-120)")
                .Covers("CMB-120", "PHB p.156", "PC_NPC_PARITY")
                .MaxRounds(5)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6, "Improved Grapple")), 10, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Initiative("hero", "dummy")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "dummy"))
                .Turn("dummy", 0, Step.Pass())
                .AiWhenUnscripted("hero")
                // The AI pins at its own discretion (ChooseNPCGrappleAction), so a seed range is needed.
                .Expect("Each hero turn that starts with the dummy pinned begins with Pin Opponent, which holds the pin", v =>
                {
                    var r = new System.Text.RegularExpressions.Regex(@"Hero chooses grapple action \[(\w+)\]");
                    int checkedRounds = 0;
                    foreach (TraceEvent turn in v.TurnsOf("hero"))
                    {
                        int round = turn.Round;
                        if (!HasCond(v.Snapshot("dummy", round, "hero"), "Pinned"))
                            continue;
                        TraceEvent first = v.Of("log").FirstOrDefault(e => e.Round == round && e.Seq > turn.Seq && r.IsMatch(e.Str("text") ?? ""));
                        if (first == null) return ExpectResult.Fail("no grapple choice in round " + round, turn.Seq);
                        string action = r.Match(first.Str("text")).Groups[1].Value;
                        if (action != "PinOpponent") return ExpectResult.Fail("round " + round + " began with " + action, first.Seq);
                        JsonObj d = v.Snapshot("dummy", round, "dummy");
                        if (d != null && !HasCond(d, "Pinned")) return ExpectResult.Fail("the won renewal of round " + round + " did not hold the pin", first.Seq);
                        checkedRounds++;
                    }
                    return checkedRounds > 0 ? ExpectResult.Pass(checkedRounds + " renewal turn(s)") : ExpectResult.Inconclusive("the AI never started a turn with the dummy pinned");
                })
                .Build();
        }

        private static ScenarioDef PinDurationAiOneAttack()
        {
            return Rules("rules/pin-duration-ai-one-attack", "A lone AI pinner with one attack deals grapple damage on its next turn instead of renewing, so the pin ends (PHB p.156, CMB-120)")
                .Covers("CMB-120", "CMB-075", "PHB p.156", "PC_NPC_PARITY")
                .MaxRounds(8)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 4, "Improved Grapple")), 10, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Initiative("hero", "dummy")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "dummy"))
                .Turn("dummy", 0, Step.Pass())
                .AiWhenUnscripted("hero")
                // BAB +4 gives one attack a round (PHB p.141); no ally and a non-caster target, so renewing would only
                // trade grapple damage for a turn the pinned dummy loses anyway (CMB-075). The AI pins at its own
                // discretion (ChooseNPCGrappleAction), so a seed range is needed.
                .Expect("Each hero turn that starts with the dummy pinned deals grapple damage, and the pin ends", v =>
                {
                    var r = new System.Text.RegularExpressions.Regex(@"Hero chooses grapple action \[(\w+)\]");
                    int checkedRounds = 0;
                    foreach (TraceEvent turn in v.TurnsOf("hero"))
                    {
                        int round = turn.Round;
                        if (!HasCond(v.Snapshot("dummy", round, "hero"), "Pinned"))
                            continue;
                        TraceEvent first = v.Of("log").FirstOrDefault(e => e.Round == round && e.Seq > turn.Seq && r.IsMatch(e.Str("text") ?? ""));
                        if (first == null) return ExpectResult.Fail("no grapple choice in round " + round, turn.Seq);
                        string action = r.Match(first.Str("text")).Groups[1].Value;
                        if (action != "DamageOpponent") return ExpectResult.Fail("round " + round + " began with " + action, first.Seq);
                        if (!v.Log("does not pin").Any(e => e.Round == round)) return ExpectResult.Fail("no pin-lapse log in round " + round, first.Seq);
                        if (!v.Log("attempts to damage").Any(e => e.Round == round)) return ExpectResult.Fail("no grapple damage attempt in round " + round, first.Seq);
                        JsonObj d = v.Snapshot("dummy", round, "dummy");
                        if (d != null && HasCond(d, "Pinned")) return ExpectResult.Fail("the dummy is still pinned on its round-" + round + " turn", first.Seq);
                        checkedRounds++;
                    }
                    return checkedRounds > 0 ? ExpectResult.Pass(checkedRounds + " lapse turn(s)") : ExpectResult.Inconclusive("the AI never started a turn with the dummy pinned");
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
        /// p.158) without the fighter needing Improved Trip. The feat is left out on purpose: its attack after a trip
        /// that lands (PHB p.96, CMB-079) would add an attack to the count checked here. A trip that fails at the touch
        /// attack gives no counter-trip (PHB p.158: only a lost opposed check does).
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

        // ── Weapon damage by size (CMB-119; DMG p.28 Tables 2-2 and 2-3) ──────────

        /// <summary>A fighter 4 like <see cref="Fighter"/> but of the given race (a halfling is Small, PHB p.19).</summary>
        private static CharacterStats FighterOfRace(string name, string race)
        {
            var s = new CharacterStats(
                name: name, level: 4, characterClass: "Fighter",
                str: 16, dex: 12, con: 14, wis: 10, intelligence: 10, cha: 10,
                bab: 4, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 32, raceName: race);
            s.BaseAttackBonusOverride = 4;
            return s;
        }

        /// <summary>
        /// Strips the off-hand kit and applies the real Enlarge Person or Reduce Person effect through the target's
        /// StatusEffectManager (the size shift and inventory recalculation of the spell's buff path); a harness
        /// stand-in for the cast itself, which no NPC caster in the catalog prepares.
        /// </summary>
        private static Action<CharacterController> WithSizeSpell(string spellId) => c =>
        {
            StripOffHand(c);
            SpellDatabase.Init();
            SpellData spell = SpellDatabase.GetSpell(spellId);
            if (spell != null && c.StatusEffectManager != null)
                c.StatusEffectManager.AddEffect(spell.Clone(), "ScenarioHarness", 1);
        };

        /// <summary>Every attack by <paramref name="key"/> rolls <paramref name="dice"/>; a hit that is not a crit rolls within those dice.</summary>
        private static Func<TraceView, ExpectResult> RollsDice(string key, string dice)
        {
            return v =>
            {
                List<TraceEvent> attacks = v.Attacks(key, null, false);
                if (attacks.Count == 0) return ExpectResult.Fail("no attack by " + key);
                TraceEvent wrong = attacks.FirstOrDefault(e => e.Str("dice") != dice);
                if (wrong != null) return ExpectResult.Fail(key + " rolled " + wrong.Str("dice") + " with " + wrong.Str("weapon"), wrong.Seq);
                string[] parts = dice.Split('d');
                int count = int.Parse(parts[0]), sides = int.Parse(parts[1]);
                TraceEvent outOfRange = attacks.FirstOrDefault(e => e.Bool("hit") && !e.Bool("confirmed")
                    && (e.Int("baseRoll") < count || e.Int("baseRoll") > count * sides));
                if (outOfRange != null) return ExpectResult.Fail("base roll " + outOfRange.Int("baseRoll") + " outside " + dice, outOfRange.Seq);
                return ExpectResult.Pass(attacks.Count + " attacks with " + attacks[0].Str("weapon") + ", " + dice, attacks.Select(e => e.Seq).ToArray());
            };
        }

        /// <summary>
        /// Puts a greatclub in the MM ogre's right hand. Its NPC data lists the greatclub under EquipSlot.MainHand, which
        /// DirectEquip drops (ITM-004), so the ogre otherwise fights with an unarmed strike. Large NPCs whose weapon is
        /// listed under RightHand (ogre_brute, test_ogre_gust) spawn armed; the scenario's "brute" covers that path.
        /// </summary>
        private static void EquipGreatclub(CharacterController c)
        {
            InventoryComponent inv = c.GetComponent<InventoryComponent>();
            if (inv == null || inv.CharacterInventory == null || inv.CharacterInventory.RightHandSlot != null)
                return;
#pragma warning disable CS0618
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(DND35e.Identifiers.ItemIDs.GREATCLUB), EquipSlot.RightHand);
#pragma warning restore CS0618
            inv.CharacterInventory.RecalculateStats();
        }

        private static Func<TraceView, ExpectResult> ActorSize(string key, SizeCategory size)
        {
            return v =>
            {
                TraceEvent a = v.Of("actor").FirstOrDefault(e => e.Str("key") == key);
                object got = a != null ? a.Get("size") : null;
                return got is SizeCategory s && s == size
                    ? ExpectResult.Pass(key + " is " + size, a.Seq)
                    : ExpectResult.Fail(key + " is " + (got ?? "missing"));
            };
        }

        /// <summary>
        /// Each attacker makes one scripted attack with its weapon; the trace's attack dice must match the DMG tables
        /// for its size: a Medium longsword 1d8, a halfling's 1d6, an enlarged human's 2d6, a reduced human's 1d6, the
        /// Small goblin's morningstar 1d6 and the Large ogre's greatclub 2d8, both for the MM ogre given its greatclub by
        /// a tweak and for ogre_brute, which spawns with its greatclub (CMB-119).
        /// </summary>
        private static ScenarioDef WeaponSizeDamage()
        {
            return Rules("rules/weapon-size-damage", "Weapon damage dice follow the wielder's size: DMG Tables 2-2 and 2-3 (p.28), PHB Table 7-5, MM goblin and ogre (CMB-119)")
                .Covers("CMB-119", "DMG p.28", "PHB p.116", "PHB p.226", "PHB p.269", "MM p.133", "MM p.199")
                .MaxRounds(1)
                .Pc("medium", ActorSource.Stats(() => FighterOfRace("Medium", "Human")), 3, 3, Control.Scripted)
                .Pc("halfling", ActorSource.Stats(() => FighterOfRace("Halfling", "Halfling")), 3, 7, Control.Scripted)
                .Pc("enlarged", ActorSource.Stats(() => FighterOfRace("Enlarged", "Human")), 3, 11, Control.Scripted)
                .Pc("reduced", ActorSource.Stats(() => FighterOfRace("Reduced", "Human")), 3, 15, Control.Scripted)
                .Npc("d1", "target_dummy", 4, 3, Control.Idle)
                .Npc("d2", "target_dummy", 4, 7, Control.Idle)
                .Npc("d3", "target_dummy", 5, 11, Control.Idle)
                .Npc("d4", "target_dummy", 4, 15, Control.Idle)
                .Npc("goblin", "goblin", 2, 3, Control.Scripted)
                .Npc("ogre", "ogre", 1, 7, Control.Scripted)
                .Npc("brute", "ogre_brute", 1, 14, Control.Scripted)
                .Tweak("medium", StripOffHand)
                .Tweak("halfling", StripOffHand)
                .Tweak("enlarged", WithSizeSpell(DND35e.Identifiers.SpellNames.ENLARGE_PERSON))
                .Tweak("reduced", WithSizeSpell(DND35e.Identifiers.SpellNames.REDUCE_PERSON))
                .Tweak("ogre", EquipGreatclub)
                .Initiative("medium", "halfling", "enlarged", "reduced", "goblin", "ogre", "brute")
                .Turn("medium", 1, Step.Attack("d1"))
                .Turn("halfling", 1, Step.Attack("d2"))
                .Turn("enlarged", 1, Step.Attack("d3"))
                .Turn("reduced", 1, Step.Attack("d4"))
                .Turn("goblin", 1, Step.Attack("medium"))
                .Turn("ogre", 1, Step.Attack("halfling"))
                .Turn("brute", 1, Step.Attack("reduced"))
                .Expect("Sizes: halfling and reduced human Small, enlarged human Large, goblin Small, ogre Large (PHB p.226, p.269)", Expect.All(
                    ActorSize("medium", SizeCategory.Medium), ActorSize("halfling", SizeCategory.Small),
                    ActorSize("enlarged", SizeCategory.Large), ActorSize("reduced", SizeCategory.Small),
                    ActorSize("goblin", SizeCategory.Small), ActorSize("ogre", SizeCategory.Large),
                    ActorSize("brute", SizeCategory.Large)))
                .Expect("A Medium longsword rolls 1d8 (PHB Table 7-5)", RollsDice("medium", "1d8"))
                .Expect("A halfling's longsword rolls 1d6 (PHB Table 7-5; DMG Table 2-3)", RollsDice("halfling", "1d6"))
                .Expect("An enlarged human's longsword rolls 2d6 (Enlarge Person, PHB p.226; DMG Table 2-2)", RollsDice("enlarged", "2d6"))
                .Expect("A reduced human's longsword rolls 1d6 (Reduce Person, PHB p.269; DMG Table 2-3)", RollsDice("reduced", "1d6"))
                .Expect("The Small goblin's morningstar rolls 1d6 (MM p.133; DMG Table 2-3)", RollsDice("goblin", "1d6"))
                .Expect("The Large ogre's greatclub rolls 2d8 (MM p.199; DMG Table 2-2)", RollsDice("ogre", "2d8"))
                .Expect("The Large ogre_brute's spawned greatclub rolls 2d8 (DMG Table 2-2)", RollsDice("brute", "2d8"))
                .Build();
        }

        // ── Combat log pool (UI-001) ────────────────────────────────────

        /// <summary>Combat log lines the scribe writes on each of its turns (more than the log keeps, so lines are trimmed and reused).</summary>
        private const int LogPoolLinesPerTurn = CombatLogPanel.MaxMessages + 20;

        /// <summary>
        /// Not a rules check: the combat log's line objects stay bounded (UI-001). A scripted scribe writes 520 lines
        /// through CombatUI.ShowCombatLog on each of its two turns and notes, in deterministic terms, what it finds
        /// right after: no new inactive PooledLogMsg root object; the panel has exactly one CombatLogPool holder; the
        /// scrollable log keeps at most 500 lines; the combat-log line objects anywhere in the scene (counted by
        /// FindObjectsByType, so lines left under an old holder or anywhere else count too) number at most
        /// CombatLogPanel.MaxLineObjects (501); and the second turn adds no line object. Before the fix each log call
        /// built a new pool and orphaned its 50 prewarmed lines; a regression that rebuilt the pool under a new holder
        /// would fail the holder, bound and turn-2 checks. The leak sweep (every 30 frames) cannot hide a leak here:
        /// the scribe writes and counts within one frame.
        /// </summary>
        private static ScenarioDef CombatLogPool()
        {
            return Rules("rules/combat-log-pool", "The combat log reuses its pooled lines: no orphans, one pool, bounded object count (UI-001)")
                .Covers("UI-001")
                .MaxRounds(2)
                .Pc("scribe", ActorSource.Stats(() => Fighter("Scribe", 1)), 5, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 15, 10, Control.Idle)
                .Initiative("scribe", "dummy")
                .Script("scribe", WriteLogLines)
                .Expect("The fight is halted as a stalemate when round 3 begins", Expect.Outcome(Outcome.Stalemate))
                .Expect("Both turns note their counts", v => LogPoolNotes(v).Count == 2
                    ? ExpectResult.Pass("2 notes") : ExpectResult.Fail(LogPoolNotes(v).Count + " log-pool notes, expected 2"))
                .Expect("Writing 520 lines leaves no inactive PooledLogMsg object at the scene root (UI-001)", v => LogPoolCheck(v, "roots+0"))
                .Expect("The panel keeps exactly one line pool (one CombatLogPool holder)", v => LogPoolCheck(v, "holders=1"))
                .Expect("The scrollable log keeps at most 500 lines and the scene holds at most 501 combat-log line objects", v => LogPoolCheck(v, "visible<=max", "lines<=max"))
                .Expect("The second turn reuses pooled lines and adds no line object", v =>
                {
                    TraceEvent n = LogPoolNotes(v).FirstOrDefault(e => e.Str("text").StartsWith("log-pool turn 2", StringComparison.Ordinal));
                    if (n == null) return ExpectResult.Fail("no turn-2 note");
                    return n.Str("text").Contains("lines+0") ? ExpectResult.Pass(n.Str("text"), n.Seq) : ExpectResult.Fail(n.Str("text"), n.Seq);
                })
                .Build();
        }

        private static List<TraceEvent> LogPoolNotes(TraceView v)
            => v.Of("note").Where(e => (e.Str("text") ?? "").StartsWith("log-pool turn", StringComparison.Ordinal)).ToList();

        private static ExpectResult LogPoolCheck(TraceView v, params string[] needles)
        {
            List<TraceEvent> notes = LogPoolNotes(v);
            if (notes.Count == 0) return ExpectResult.Fail("no log-pool note");
            foreach (TraceEvent n in notes)
                foreach (string needle in needles)
                    if (!n.Str("text").Contains(needle))
                        return ExpectResult.Fail(n.Str("text"), n.Seq);
            return ExpectResult.Pass(string.Join(" | ", notes.Select(n => n.Str("text"))));
        }

        private static int _logPoolLinesAfterTurn1;

        /// <summary>
        /// Scribe script: writes <see cref="LogPoolLinesPerTurn"/> log lines and notes what it finds. Absolute counts
        /// below the bound depend on what earlier jobs in the Play session logged, so the note holds only deltas,
        /// bounds and the holder count; an out-of-bound count is written out, since a failing run has no hash to keep.
        /// </summary>
        private static System.Collections.IEnumerator WriteLogLines(ScenarioContext ctx, CharacterController scribe)
        {
            CombatUI ui = ctx.Gm != null ? ctx.Gm.CombatUI : null;
            int round = ctx.Gm != null ? ctx.Gm.CurrentRound : 0;
            int turn = round <= 1 ? 1 : 2;
            if (ui == null || ui.CombatLogContent == null)
            {
                ctx.Note("log-pool turn " + turn + ": no CombatUI or log content");
                yield break;
            }

            int rootsBefore = CountLogPoolRoots();
            for (int i = 1; i <= LogPoolLinesPerTurn; i++)
                ui.ShowCombatLog("Scribe log line " + turn + "." + i);

            CombatLogPanel panel = ui.GetComponent<CombatLogPanel>();
            int roots = CountLogPoolRoots() - rootsBefore;
            int visible = ui.CombatLogContent.transform.childCount;
            int holders = -1;
            if (panel != null)
                panel.CountLineObjects(out holders);
            int lines = CountLogLineObjects();
            string note = "log-pool turn " + turn + ": roots" + (roots >= 0 ? "+" : "") + roots
                + (panel != null ? " holders=" + holders : " (no panel)")
                + (visible <= CombatLogPanel.MaxMessages ? " visible<=max" : " visible=" + visible)
                + (lines <= CombatLogPanel.MaxLineObjects ? " lines<=max" : " lines=" + lines);
            if (turn == 1)
                _logPoolLinesAfterTurn1 = lines;
            else
                note += " lines" + (lines - _logPoolLinesAfterTurn1 >= 0 ? "+" : "") + (lines - _logPoolLinesAfterTurn1);
            ctx.Note(note);
        }

        private static int CountLogPoolRoots()
        {
            int n = 0;
            foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (go != null && !go.activeSelf && go.name == CombatLogPanel.PooledLineName)
                    n++;
            return n;
        }

        /// <summary>
        /// Every combat-log line object in the loaded scenes, active or not and wherever it is parented: a GameObject
        /// with a Text and a LayoutElement named PooledLogMsg (free) or LogMsg_N (visible), as CombatLogPanel makes them.
        /// </summary>
        private static int CountLogLineObjects()
        {
            int n = 0;
            foreach (UnityEngine.UI.LayoutElement le in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.LayoutElement>(FindObjectsInactive.Include))
            {
                if (le == null) continue;
                string name = le.gameObject.name;
                if ((name == CombatLogPanel.PooledLineName || name.StartsWith("LogMsg_", StringComparison.Ordinal))
                    && le.GetComponent<UnityEngine.UI.Text>() != null)
                    n++;
            }
            return n;
        }

        // ── Enemy pool slot reuse (CRE-046) ─────────────────────────────

        /// <summary>The trait creatures of rules/slot-reuse-traits, in pool-slot order, and the squares both scenarios use.</summary>
        private static readonly string[] SlotReuseTraitIds = { "allip", "hell_hound", "ghast", "gibbering_mouther", "troll" };
        private static readonly Vector2Int[] SlotReuseSquares =
        {
            new Vector2Int(15, 2), new Vector2Int(15, 5), new Vector2Int(15, 8), new Vector2Int(15, 11), new Vector2Int(15, 14)
        };

        /// <summary>The innate traits a controller carries, as names (empty for a creature without any).</summary>
        private static List<string> SlotTraits(CharacterController c)
        {
            var t = new List<string>();
            if (c == null) return t;
            if (c.IsIncorporeal) t.Add("incorporeal");
            if (c.HasAuraAbility) t.Add("aura");
            if (c.HasBreathWeapon) t.Add("breath");
            if (c.HasSecondaryBreathWeapon) t.Add("secondary-breath");
            if (c.HasFrightfulPresence) t.Add("frightful-presence");
            if (c.HasEngulf) t.Add("engulf");
            if (c.HasRangedSpecialAttack) t.Add("ranged-special");
            if (c.HasBloodDrain) t.Add("blood-drain");
            if (c.HasTerrainManipulation) t.Add("terrain");
            if (c.HasStenchAura) t.Add("stench");
            if (c.HasRegeneration) t.Add("regeneration");
            return t;
        }

        /// <summary>The traits each creature of <see cref="SlotReuseTraitIds"/> must show (the positive control).</summary>
        private static bool HoldsExpectedTraits(string id, CharacterController c)
        {
            List<string> t = SlotTraits(c);
            switch (id)
            {
                case "allip": return t.Contains("incorporeal") && t.Contains("aura");
                case "hell_hound": return t.Contains("breath");
                case "ghast": return t.Contains("stench");
                case "gibbering_mouther": return t.Contains("aura") && t.Contains("ranged-special") && t.Contains("terrain") && t.Contains("blood-drain") && t.Contains("engulf");
                case "troll": return t.Contains("regeneration");
                default: return false;
            }
        }

        /// <summary>
        /// First of the CRE-046 pair (not a rules check): a fighter against five trait creatures in pool slots 0-4
        /// (allip, hell hound, ghast, gibbering mouther, troll), all AI-run for one round so their abilities run. The
        /// fighter's assert confirms each slot carries its creature's traits, so rules/slot-reuse-plain, which follows
        /// it in the same Play session, starts from slots that held them.
        /// </summary>
        private static ScenarioDef SlotReuseTraits()
        {
            ScenarioBuilder b = Rules("rules/slot-reuse-traits", "Trait creatures fill enemy pool slots 0-4 before rules/slot-reuse-plain (CRE-046)")
                .Covers("CRE-046")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 4)), 3, 8, Control.Scripted);
            for (int i = 0; i < SlotReuseTraitIds.Length; i++)
                b.Npc("t" + i, SlotReuseTraitIds[i], SlotReuseSquares[i].x, SlotReuseSquares[i].y, Control.Ai);
            return b
                .Initiative("fighter")
                .Turn("fighter", 1, Step.Assert("each pool slot carries its trait creature's traits (positive control)", ctx =>
                {
                    bool ok = true;
                    for (int i = 0; i < SlotReuseTraitIds.Length; i++)
                    {
                        CharacterController c = ctx.Get("t" + i);
                        if (!HoldsExpectedTraits(SlotReuseTraitIds[i], c))
                        {
                            ok = false;
                            ctx.Note("slot " + i + " (" + SlotReuseTraitIds[i] + ") traits: " + string.Join(",", SlotTraits(c)));
                        }
                    }
                    return ok;
                }))
                .Expect("Every trait creature spawns with its traits (the control for rules/slot-reuse-plain)", Expect.AssertsPass())
                .Build();
        }

        /// <summary>
        /// Second of the CRE-046 pair: five plain orc warriors in pool slots 0-4, AI-run for one round. Run right after
        /// rules/slot-reuse-traits in one Play session, each orc takes the slot a trait creature held in the previous
        /// encounter. Run alone (or after other jobs), its setup first spawns the trait creatures into those slots
        /// through the same spawn path, with the RNG state saved and restored, so the job starts from the same slots
        /// either way and its trace does not depend on which happened. Before the fix the orc in slot 0 kept the allip's
        /// incorporeality and Babble aura (soak seeds 25-30, docs/TESTING.md 3.5), and the others their breath weapon,
        /// stench, mouther abilities and regeneration.
        /// </summary>
        private static ScenarioDef SlotReusePlain()
        {
            ScenarioBuilder b = Rules("rules/slot-reuse-plain", "Plain orcs in slots that held trait creatures keep none of their traits (CRE-046)")
                .Covers("CRE-046")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 4)), 3, 8, Control.Scripted);
            for (int i = 0; i < SlotReuseTraitIds.Length; i++)
                b.Npc("orc" + i, "orc_warrior", SlotReuseSquares[i].x, SlotReuseSquares[i].y, Control.Ai);
            return b
                .GenerateActors(StageTraitCreaturesInPool)
                .Initiative("fighter")
                .Turn("fighter", 1, Step.Assert("no orc keeps a trait of its slot's earlier creature", ctx =>
                {
                    bool ok = true;
                    for (int i = 0; i < SlotReuseTraitIds.Length; i++)
                    {
                        List<string> left = SlotTraits(ctx.Get("orc" + i));
                        if (left.Count > 0)
                        {
                            ok = false;
                            ctx.Note("orc" + i + " (slot of " + SlotReuseTraitIds[i] + ") keeps: " + string.Join(",", left));
                        }
                    }
                    return ok;
                }))
                .Expect("No orc warrior keeps incorporeality, an aura, a breath weapon, stench, the mouther's abilities or regeneration (CRE-046)", Expect.AssertsPass())
                .Expect("No orc uses Babble (the allip's aura, MM p.10)", Expect.Count("log", e => (e.Str("text") ?? "").IndexOf("Babble", StringComparison.OrdinalIgnoreCase) >= 0, 0, 0))
                .Build();
        }

        /// <summary>
        /// GenerateActors of rules/slot-reuse-plain: unless pool slots 0-4 already hold the trait creatures (the job ran
        /// right after rules/slot-reuse-traits), spawn them there through GameManager.Harness_SpawnEnemies, the spawn
        /// path the job then reuses. The RNG state is saved and restored around it, and the trace info is the same
        /// either way, so the job's trace does not depend on it. Adds no actors.
        /// </summary>
        private static GeneratedActors StageTraitCreaturesInPool(ScenarioContext ctx)
        {
            GameManager gm = ctx.Gm;
            bool held = gm != null && gm.NPCs != null && gm.NPCs.Count >= SlotReuseTraitIds.Length;
            for (int i = 0; held && i < SlotReuseTraitIds.Length; i++)
            {
                CharacterController c = gm.NPCs[i];
                held = c != null && c.Stats != null && c.Stats.SourceNpcDefinitionId == SlotReuseTraitIds[i]
                    && HoldsExpectedTraits(SlotReuseTraitIds[i], c);
            }

            if (!held && gm != null)
            {
                UnityEngine.Random.State saved = UnityEngine.Random.state;
                try
                {
                    gm.Harness_SpawnEnemies(new List<string>(SlotReuseTraitIds), SlotReuseSquares);
                }
                finally
                {
                    UnityEngine.Random.state = saved;
                }
            }
            Debug.Log("[Scenario] rules/slot-reuse-plain: pool slots 0-4 " + (held ? "held the trait creatures of the previous job" : "were staged with the trait creatures"));

            var gen = new GeneratedActors();
            gen.Info.Set("slots", "0-4").Set("earlier", string.Join(",", SlotReuseTraitIds));
            return gen;
        }
    }
}
#endif
