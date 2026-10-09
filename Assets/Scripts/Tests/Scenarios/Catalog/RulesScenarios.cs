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
    /// - orc_warrior (checked 2026-10-08 for CHR-072): Warrior 1, Medium, STR 17, scale mail and greataxe; since CRE-004
    ///   (2026-10-08) it spawns with the warrior's BAB +1 (MM p.203), and the goblin with BAB +1 too.
    /// - formian_taskmaster: Outsider 6 HD, Medium, STR 17, DEX 16, IsExceptionallyStable (four legs, MM p.108-110;
    ///   owner decision 2026-10-07, CMB-085).
    /// - barghest: Outsider 6 HD, Medium, STR 17, DEX 15, not stable (only its wolf form would be, and the game models
    ///   no wolf form; owner decision 2026-10-07, CMB-085).
    /// - ethereal_filcher: Aberration 5 HD, Medium, not stable (a single leg, MM p.104; corrected 2026-10-08, CRE-045).
    /// - green_slaad: Outsider 9 HD, Large, STR 19, BAB 9, claws primary and bite secondary, Multiattack (MM p.230;
    ///   checked 2026-10-08, CRE-045). rakshasa: Outsider 7 HD, Medium, STR 12, BAB 7, claws primary and bite
    ///   secondary, no Multiattack (MM p.211).
    /// Stats actors are Human fighters built with named arguments (CHR-032) and BAB set through
    /// BaseAttackBonusOverride (CHR-068); feat strings are the case-sensitive HasFeat names (CHR-030):
    /// "Improved Trip", "Improved Grapple", "Weapon Focus".
    /// Rules checked in the PHB (2026-10-07): p.137 and p.143-144 (movement AoOs, 5-foot step, withdraw, stand up,
    /// crawl), p.140 (casting provokes; casting defensively), p.153 (flanking +2), p.154 (bull rush), p.155 (disarm:
    /// an AoO that deals damage foils it), p.156 (grapple: likewise), p.158 (trip; the defender may trip back),
    /// p.141 Table 8-2 note (trip, disarm and grapple replace a melee attack).
    /// MM p.312 (primary natural attacks at full bonus, secondary at -5) and p.304 (Multiattack: -2) for the
    /// maneuvers that replace a natural attack (owner decision 2026-10-07).
    /// Natural weapons (owner rulings 2026-10-08, CMB-102): a natural attack may sunder when it deals slashing or
    /// bludgeoning damage (PHB p.158; MM p.312: bite B/P/S, claw P/S, gore P); on a disarm roll it is a one-handed weapon
    /// of the creature's own size (PHB p.155: no handedness modifier); a secondary natural attack's penalty applies to a
    /// grapple's touch attack only, not to the opposed grapple check (MM p.312 names the attack roll).
    /// PHB p.239 (Haste: one extra attack on a full attack) for the hasted natural-weapon creature: one extra
    /// natural attack at that attack's normal bonus (owner decision 2026-10-07, CMB-106).
    /// Bull rush AoOs (owner decision 2026-10-07): entering the defender's space is the bull rush's own provocation
    /// (PHB p.154), separate from movement; moving out of squares one opponent threatens counts as one opportunity for
    /// the whole round (PHB p.138, rechecked 2026-10-08: the rule is stated per round; owner ruling 2026-10-08, CMB-128:
    /// per round, across a double move or a move and a bull rush follow), and a creature makes one AoO a round, 1 + DEX
    /// modifier with Combat Reflexes (PHB p.92). The squares followed after a standard bull rush and the move action are
    /// independent in both directions (owner ruling 2026-10-08, CMB-129; PHB p.154 limits the follow only by the
    /// attacker's normal movement).
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
    /// Owner rulings 2026-10-08 (CMB-136; PHB p.158, p.145, p.96, p.139 rechecked): any defender may trip back, an ally
    /// of the tripper too (the AI default declines against an ally); a conscious defender at 0 HP or below (disabled)
    /// may trip back, since it is a reaction, not an action; the Improved Trip attack uses the main weapon when it can
    /// attack the tripped foe, else the off-hand weapon, else an unarmed strike, which provokes the armed foe's AoO
    /// first unless the tripper has Improved Unarmed Strike.
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
    /// Improved Grab size (CMB-126; MM p.310): unless the creature's entry names another maximum, the free grapple works
    /// only against an opponent at least one size category smaller than the grabber's current size. Data checked in the
    /// NPC database files and the MM on 2026-10-08: crocodile (Medium Animal, bite trigger, no size clause, MM p.271) and
    /// lion (Large Animal, pounce, bite trigger, no size clause, MM p.275).
    /// Grapple attacks with natural weapons (CMB-127; PHB p.156 Attack Your Opponent, MM p.314 Rake): each grapple attack
    /// action is one attack with one natural weapon, the attacker's pick, in place of one natural attack of its sequence,
    /// at that attack's normal bonus -4 (the attacker's Grappled condition); a full attack of grapple attacks makes each
    /// natural attack once (one per natural attack: an unconfirmed reading, PHB p.156 ties grapple actions to the BAB
    /// ladder; owner question CMB-146), and the rake's two claws come once a turn. Grapple checks (pin, damage, escape)
    /// stay on the BAB ladder for every creature (PHB p.156). MM lion (p.274-275): 2 claws +7, bite +2,
    /// rake +7, BAB +3; rakes are not subject to the -4 (MM p.314),
    /// which the code does not do yet (CMB-144, an XFail expectation).
    /// Trip (Ex) trigger attack (CMB-125): every MM Trip (Ex) entry ties the free trip to one attack, a hit with the
    /// bite (wolf p.283, dire wolf p.66, worg p.257, hyena p.274, shadow mastiff p.222, yeth hound p.262, werewolf
    /// p.174) or, for the cheetah, the claw or bite (p.271): no touch attack, no AoO, and a failed attempt lets the
    /// opponent make no trip back. A hit with any other natural attack, a weapon or an unarmed strike starts no trip.
    /// Combat end (owner definition 2026-10-07; CORE-011, CORE-034, CORE-037, CORE-001): no RAW rule defines it. A
    /// creature is out when it is dead, dying or unconscious, a regenerating one too, and a petrified creature counts as
    /// unconscious (PHB p.311, DMG p.301); a disabled creature (0 HP, PHB p.145) is still in; a side is out when every
    /// active member of its team is out, allies and summons included.
    /// Class progression (CHR-001, CHR-002, CHR-071; checked 2026-10-08): PHB p.22 Table 3-1 (BAB good = level,
    /// average = 3/4, poor = 1/2) and the class tables (bard d6 and 3/4 BAB p.27-28, ranger d8 and full BAB p.46-47);
    /// p.9, p.23 and p.59 (each Hit Die adds its roll plus the CON modifier, minimum 1; maximum at 1st level); p.309 (HD =
    /// character level for a character, total HD for a creature); p.217 (Daze: one humanoid of 4 HD or less). MM owlbear
    /// skeleton 5 HD (p.226: racial HD kept) and bugbear zombie 6 HD (p.267: racial HD doubled). Data checked
    /// 2026-10-08: neutral_mage_test (Wizard 3, Daze prepared x4), skeleton_owlbear, zombie_bugbear; Quick Start bard
    /// (Half-Elf, CON 12) and ranger (Human, CON 12, no Toughness); ogre (MM p.199: 4d8+11 = 29), ghoul (MM p.119: 2d12 = 13,
    /// stored as CON 0) and orc_warrior (CON 12) for the NPC hit point checks.
    /// </summary>
    public static class RulesScenarios
    {
        /// <summary>The number of definitions <see cref="All"/> yields (docs/TESTING.md 3.4); a short catalog is a load error.</summary>
        public const int Count = 107;

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            // Each definition is built through ScenarioCatalog.Safe, so an invalid one is a load error (run verdict
            // LOADERROR) and the others still load.
            yield return S("movement-aoo-move", () => MovementAoO("rules/movement-aoo-move", "Moving out of a threatened square provokes once (PHB p.137)", Step.Move(14, 10)));
            yield return S("movement-aoo-5ft", () => MovementAoO("rules/movement-aoo-5ft", "A 5-foot step provokes nothing (PHB p.144)", Step.FiveFootStep(12, 10)));
            yield return S("movement-aoo-withdraw", () => MovementAoO("rules/movement-aoo-withdraw", "Withdraw: the first square provokes nothing (PHB p.143)", Step.Withdraw(16, 10)));
            yield return S("movement-aoo-double-move", () => DoubleMoveAoOs(false));
            yield return S("movement-aoo-double-move-ui", () => DoubleMoveAoOs(true));
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
            yield return S("maneuver-trip-counter-ally-ui", () => CounterTripAlly(true));
            yield return S("maneuver-trip-counter-ally-ai", () => CounterTripAlly(false));
            yield return S("maneuver-trip-counter-disabled", CounterTripDisabled);
            yield return S("maneuver-trip-improved-unarmed", () => ImprovedTripFallback("unarmed"));
            yield return S("maneuver-trip-improved-unarmed-ius", () => ImprovedTripFallback("unarmed-ius"));
            yield return S("maneuver-trip-improved-offhand", () => ImprovedTripFallback("offhand"));
            yield return S("maneuver-trip-improved-unarmed-dropped-ui", ImprovedTripUnarmedDropsUi);
            yield return S("maneuver-disarm", Disarm);
            yield return S("maneuver-bullrush", BullRush);
            yield return S("maneuver-bullrush-charge-reflexes", () => BullRushWatcherAoOs(true, true));
            yield return S("maneuver-bullrush-charge-control", () => BullRushWatcherAoOs(true, false));
            yield return S("maneuver-bullrush-reflexes", () => BullRushWatcherAoOs(false, true));
            yield return S("maneuver-bullrush-after-move", BullRushAfterMove);
            yield return S("maneuver-bullrush-then-move", BullRushThenMove);
            yield return S("maneuver-grapple", Grapple);
            yield return S("maneuver-freetrip", FreeTrip);
            yield return S("freetrip-trigger", () => FreeTripTrigger(false));
            yield return S("freetrip-trigger-ui", () => FreeTripTrigger(true));
            yield return S("freetrip-trigger-aoo", FreeTripTriggerAoO);
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
            yield return S("shared-square-move-aoo", () => SharedSquareLeave(false));
            yield return S("shared-square-5ft-no-aoo", () => SharedSquareLeave(true));
            yield return S("shared-square-melee", SharedSquareMelee);
            yield return S("shared-square-coup-de-grace", SharedSquareCoupDeGrace);
            yield return S("grappler-feared-stays", GrapplerFearedStays);
            yield return S("pin-duration-ui", PinDurationUi);
            yield return S("pin-duration-ai", PinDurationAi);
            yield return S("pin-duration-ai-one-attack", PinDurationAiOneAttack);
            yield return S("npc-maneuver-replaces-iterative", NpcManeuverReplacesIterative);
            yield return S("maneuver-replaces-natural", () => ManeuverReplacesNatural(false));
            yield return S("maneuver-replaces-natural-multiattack", () => ManeuverReplacesNatural(true));
            yield return S("maneuver-replaces-natural-ui", ManeuverReplacesNaturalUi);
            yield return S("natural-weapon-maneuvers", NaturalWeaponManeuvers);
            yield return S("natural-sunder-gore-refused", NaturalSunderGoreRefused);
            yield return S("natural-sunder-ui", NaturalSunderUi);
            yield return S("natural-maneuvers-size", NaturalManeuversSize);
            yield return S("haste-natural-extra", () => HasteNaturalExtra(false));
            yield return S("haste-natural-extra-ui", () => HasteNaturalExtra(true));
            yield return S("haste-natural-moved", HasteNaturalMoved);
            yield return S("haste-natural-buttons-ui", HasteNaturalButtonsUi);
            yield return S("haste-natural-full-attack-ui", () => HasteNaturalChooserUi(pounce: false, hasteAttack: "Claw"));
            yield return S("haste-natural-full-attack-ui-default", () => HasteNaturalChooserUi(pounce: false, hasteAttack: null));
            yield return S("haste-natural-pounce-ui", () => HasteNaturalChooserUi(pounce: true, hasteAttack: "Claw"));
            yield return S("ai-maneuver-stopgap-trip", () => AiManeuverStopgap(true));
            yield return S("ai-maneuver-stopgap-trip-fail", () => AiManeuverStopgap(false));
            yield return S("stability-trip", () => StabilityCheck("rules/stability-trip", "A four-legged formian taskmaster resists a trip with +4 stability (PHB p.158, CMB-085)", "formian_taskmaster", SpecialAttackType.Trip, true));
            yield return S("stability-bullrush", () => StabilityCheck("rules/stability-bullrush", "A four-legged formian taskmaster resists a bull rush with +4 stability (PHB p.154, CMB-085)", "formian_taskmaster", SpecialAttackType.BullRushAttack, true));
            yield return S("stability-trip-control", () => StabilityCheck("rules/stability-trip-control", "A barghest in its natural form (the only modelled form) gets no stability against a trip (PHB p.158, CMB-085)", "barghest", SpecialAttackType.Trip, false));
            yield return S("stability-trip-filcher", () => StabilityCheck("rules/stability-trip-filcher", "An ethereal filcher on its single leg gets no stability against a trip (MM p.104, PHB p.158, CRE-045)", "ethereal_filcher", SpecialAttackType.Trip, false));
            yield return S("mm-natural-sequence-multiattack", () => MmNaturalSequence("green_slaad", true, null));
            yield return S("mm-natural-sequence-secondary", () => MmNaturalSequence("rakshasa", false, 8));
            yield return S("weapon-size-damage", WeaponSizeDamage);
            yield return S("combat-log-pool", CombatLogPool);
            yield return S("slot-reuse-traits", SlotReuseTraits);
            yield return S("slot-reuse-plain", SlotReusePlain);
            yield return S("improved-grab-size", ImprovedGrabSize);
            yield return S("improved-grab-size-charge", ImprovedGrabSizeCharge);
            yield return S("improved-grab-size-ui", ImprovedGrabSizeUi);
            yield return S("grapple-natural-attacks", GrappleNaturalAttacksAi);
            yield return S("grapple-natural-attacks-ui", GrappleNaturalAttacksUi);
            yield return S("grapple-natural-checks-ui", GrappleNaturalChecksUi);
            yield return S("combat-end-victory-ai", () => CombatEndVictory(false));
            yield return S("combat-end-victory-ui", () => CombatEndVictory(true));
            yield return S("combat-end-defeat-ally", CombatEndDefeatAlly);
            yield return S("combat-end-petrified-victory", () => CombatEndPetrified(false));
            yield return S("combat-end-petrified-defeat", () => CombatEndPetrified(true));
            yield return S("large-encounter-goblins", LargeEncounterGoblins);
            yield return S("large-encounter-sizes", LargeEncounterSizes);
            yield return S("ring-deflection", RingDeflection);
            yield return S("npc-proficiency", NpcProficiency);
            yield return S("npc-spawn-alignment-gear", NpcSpawnAlignmentGear);
            yield return S("class-progression", ClassProgressionHpBabHd);
            yield return S("creature-progression", CreatureProgression);
            yield return S("spell-save-dc", SpellSaveDc);
            yield return S("spell-durations", SpellDurations);
            yield return S("spell-damage-mitigation", SpellDamageMitigation);
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

        /// <summary>
        /// CMB-128 (owner ruling 2026-10-08; PHB p.138, p.92): moving out of more than one square threatened by the same
        /// opponent in the same round is one opportunity for that opponent, even across two move actions. The mover at
        /// (11,10) moves to (11,11), then on to (14,11) with its second move action. "reflexes" at (10,10) has Combat
        /// Reflexes (DEX 16: 4 AoOs a round) and threatens both (11,10) and (11,11): one movement AoO, in the first move,
        /// and none in the second. "guard" at (12,12) (no Combat Reflexes) threatens (11,11) but not (11,10): its first
        /// opportunity comes in the second move, so it takes one AoO there (the round record is per opponent). Scripted:
        /// an NPC orc through the NPC move executor; Ui: a PC hero through the PC Move button and the AoO prompt.
        /// </summary>
        private static ScenarioDef DoubleMoveAoOs(bool ui)
        {
            string mover = ui ? "hero" : "orc";
            ScenarioBuilder b = Rules(ui ? "rules/movement-aoo-double-move-ui" : "rules/movement-aoo-double-move",
                    "A double move out of one Combat Reflexes opponent's squares provokes it once a round (PHB p.138, CMB-128)"
                    + (ui ? ", through the PC buttons" : ""))
                .Covers("CMB-128", "PHB p.92", "PHB p.137", "PHB p.138", "PC_NPC_PARITY")
                .MaxRounds(1);
            if (ui)
                b.Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6)), 11, 10, Control.Ui)
                 .Npc("reflexes", "orc_berserker", 10, 10, Control.Scripted)
                 .Npc("guard", "orc_berserker", 12, 12, Control.Scripted)
                 .Tweak("hero", c => { StripOffHand(c); SturdyDummy(c); });
            else
                b.Pc("reflexes", ActorSource.Stats(() => Fighter("Reflexes", 4, "Combat Reflexes")), 10, 10, Control.Scripted)
                 .Pc("guard", ActorSource.Stats(() => Fighter("Guard", 4)), 12, 12, Control.Scripted)
                 .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                 .Tweak("orc", SturdyDummy);
            b.Tweak("reflexes", c =>
                {
                    c.Stats.DEX = 16;
                    if (!c.Stats.Feats.Contains("Combat Reflexes"))
                        c.Stats.Feats.Add("Combat Reflexes");
                })
             .Initiative("reflexes", "guard", mover)
             .Turn("reflexes", 1, Step.Pass())
             .Turn("guard", 1, Step.Pass());

            Step fixture = Step.Assert("fixture: the Combat Reflexes opponent has AoOs to spare this round", ctx =>
                ctx.Get("reflexes").Stats.MaxAttacksOfOpportunity >= 2 && ctx.Get("reflexes").Stats.AttacksOfOpportunityUsed == 0);
            if (ui)
                b.Turn("hero", 1, fixture, Step.Move(11, 11), Step.AnswerAoO(AoOAnswer.Proceed), Step.Move(14, 11), Step.AnswerAoO(AoOAnswer.Proceed))
                 .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"));
            else
                b.Turn("orc", 1, fixture, Step.Move(11, 11), Step.Move(14, 11));

            b.Expect("Both moves are done and the mover ends at (14,11)", v =>
                {
                    ExpectResult r = Expect.All(Expect.StepStatus(mover, 1, "Move", 0, "done"), Expect.StepStatus(mover, 1, "Move", 1, "done"), Expect.AssertsPass())(v);
                    if (!r.IsPass) return r;
                    if (ScenarioChecks.IsDownSnapshot(v.Final(mover))) return ExpectResult.Inconclusive("the mover was dropped");
                    Vector2Int? p = Pos(v.Final(mover));
                    return p == new Vector2Int(14, 11) ? ExpectResult.Pass("at (14,11)") : ExpectResult.Fail("at " + p);
                })
             .Expect("One movement AoO from the Combat Reflexes opponent, in the first move only (PHB p.138, CMB-128)", v =>
                {
                    List<TraceEvent> aoos = v.AoOs("reflexes", mover, "movement");
                    TraceEvent firstMove = v.Moves(mover).FirstOrDefault();
                    if (firstMove == null) return ExpectResult.Fail("no move by " + mover);
                    return aoos.Count == 1 && aoos[0].Seq < firstMove.Seq
                        ? ExpectResult.Pass("AoO #" + aoos[0].Seq + " before the first move #" + firstMove.Seq, aoos[0].Seq, firstMove.Seq)
                        : ExpectResult.Fail(aoos.Count + " movement AoOs", aoos.Select(e => e.Seq).Concat(new[] { firstMove.Seq }).ToArray());
                })
             .Expect("One movement AoO from the guard, in the second move: its own first opportunity this round", v =>
                {
                    List<TraceEvent> aoos = v.AoOs("guard", mover, "movement");
                    TraceEvent firstMove = v.Moves(mover).FirstOrDefault();
                    if (firstMove == null) return ExpectResult.Fail("no move by " + mover);
                    return aoos.Count == 1 && aoos[0].Seq > firstMove.Seq
                        ? ExpectResult.Pass("AoO #" + aoos[0].Seq + " after the first move #" + firstMove.Seq, aoos[0].Seq, firstMove.Seq)
                        : ExpectResult.Fail(aoos.Count + " movement AoOs", aoos.Select(e => e.Seq).Concat(new[] { firstMove.Seq }).ToArray());
                })
             .Expect("No other AoO", Expect.Count("aoo", null, 2, 2));
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

        /// <summary>
        /// Any defender may trip back after a failed trip, whatever its side (owner ruling 2026-10-08, CMB-136; PHB p.158
        /// names no side). <paramref name="ui"/>: a scripted fighter trips its controllable ally, the Ui hero, and loses the
        /// opposed check; the hero is asked like any defender, the runner answers Trip Back and the forced counter-trip
        /// lands. Otherwise a scripted orc trips its AI-run orc ally: the rule allows the counter-trip, and the AI default
        /// (AIProfile.DefaultShouldCounterTrip) declines it against an ally, so nothing follows but the decline line.
        /// </summary>
        private static ScenarioDef CounterTripAlly(bool ui)
        {
            ScenarioBuilder b = Rules(ui ? "rules/maneuver-trip-counter-ally-ui" : "rules/maneuver-trip-counter-ally-ai",
                    ui
                        ? "A controllable defender tripped by an ally is asked and trips back (PHB p.158, CMB-136)"
                        : "An AI-run defender tripped by an ally may trip back but declines (PHB p.158, CMB-136)")
                .Covers("CMB-136", "PHB p.158", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Force(20, 20, "Trip touch attack")
                .Force(20, 2, "Trip Strength check")
                .Force(20, 18, "Trip defense check")
                .Force(20, 18, "Counter-trip check")
                .Force(20, 2, "Counter-trip resist check");

            if (ui)
            {
                b.Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6)), 10, 10, Control.Ui)
                    .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6)), 11, 10, Control.Scripted)
                    .Npc("orc", "orc_berserker", 16, 16, Control.Scripted)
                    .Tweak("hero", StripOffHand)
                    .Tweak("fighter", SturdyDummy)
                    .Initiative("fighter", "hero", "orc")
                    .CounterTripAnswer("hero", true)
                    .Turn("fighter", 1, Step.Maneuver(SpecialAttackType.Trip, "hero"))
                    .Turn("orc", 1, Step.Pass())
                    .Expect("The fighter's trip of its ally is attempted", Expect.StepStatus("fighter", 1, "Maneuver", 0, "done"))
                    .Expect("The fighter's trip fails and the hero stays standing", v =>
                    {
                        TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Trip).FirstOrDefault(e => !e.Bool("counter"));
                        if (m == null) return ExpectResult.Fail("no trip by the fighter");
                        return !m.Bool("success") && !HasCond(v.Final("hero"), "Prone") ? ExpectResult.Pass("trip failed", m.Seq) : ExpectResult.Fail("success " + m.Get("success"), m.Seq);
                    })
                    .Expect("The hero is asked although the tripper is its ally, once, answered Trip Back", v =>
                    {
                        List<TraceEvent> notes = v.Of("note").Where(e => e.Str("text").StartsWith("counter-trip prompt for hero", StringComparison.Ordinal)).ToList();
                        if (notes.Count != 1) return ExpectResult.Fail(notes.Count + " counter-trip prompts for the hero", notes.Select(e => e.Seq).ToArray());
                        return notes[0].Str("text").Contains("answered TripBack") ? ExpectResult.Pass(notes[0].Str("text"), notes[0].Seq) : ExpectResult.Fail(notes[0].Str("text"), notes[0].Seq);
                    })
                    .Expect("The hero trips its ally back: no touch attack, fighter prone (PHB p.158)", v =>
                    {
                        TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Trip).FirstOrDefault(e => !e.Bool("counter"));
                        TraceEvent counter = v.Maneuvers("hero", SpecialAttackType.Trip).FirstOrDefault(e => e.Bool("counter"));
                        if (m == null || counter == null) return ExpectResult.Fail("no trip or no counter-trip");
                        if (counter.Str("target") != "fighter") return ExpectResult.Fail("counter-trip target " + counter.Str("target"), counter.Seq);
                        if (v.Of("dice").Count(e => e.Str("ctx") == "Trip touch attack") != 1) return ExpectResult.Fail("the counter-trip rolled a touch attack", counter.Seq);
                        if (!counter.Bool("success")) return ExpectResult.Fail("counter-trip failed with forced dice", counter.Seq);
                        return HasCond(v.Final("fighter"), "Prone") ? ExpectResult.Pass("fighter prone", m.Seq, counter.Seq) : ExpectResult.Fail("fighter not prone", counter.Seq);
                    });
            }
            else
            {
                b.Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6)), 6, 10, Control.Scripted)
                    .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                    .Npc("orc2", "orc_berserker", 12, 10, Control.Scripted)
                    .Tweak("orc", SturdyDummy)
                    .Tweak("orc2", SturdyDummy)
                    .Initiative("orc", "orc2", "fighter")
                    .Turn("orc", 1, Step.Maneuver(SpecialAttackType.Trip, "orc2"))
                    .Turn("orc2", 1, Step.Pass())
                    .Turn("fighter", 1, Step.Pass())
                    .Expect("The orc's trip of its ally is attempted and fails", v =>
                    {
                        TraceEvent m = v.Maneuvers("orc", SpecialAttackType.Trip).FirstOrDefault(e => !e.Bool("counter"));
                        if (m == null) return ExpectResult.Fail("no trip by the orc");
                        if (m.Str("target") != "orc2") return ExpectResult.Fail("trip target " + m.Str("target"), m.Seq);
                        return !m.Bool("success") ? ExpectResult.Pass("trip failed", m.Seq) : ExpectResult.Fail("the trip landed", m.Seq);
                    })
                    .Expect("The ally is offered the counter-trip and declines it (AIProfile default against an ally, CMB-136)", v =>
                    {
                        List<TraceEvent> declines = v.Log("does not try to trip .* back");
                        if (declines.Count != 1) return ExpectResult.Fail(declines.Count + " decline lines", declines.Select(e => e.Seq).ToArray());
                        if (v.Maneuvers("orc2").Any()) return ExpectResult.Fail("orc2 tripped back");
                        return !HasCond(v.Final("orc"), "Prone") ? ExpectResult.Pass(declines[0].Str("text"), declines[0].Seq) : ExpectResult.Fail("orc prone");
                    });
            }
            return b.Build();
        }

        /// <summary>
        /// A disabled defender trips back (owner ruling 2026-10-08, CMB-136): the orc is at 0 HP (disabled, PHB p.145) and
        /// the fighter's Improved Trip (no initiation AoO) loses the opposed check. The counter-trip is a reaction, not an
        /// action, so the AI-run orc trips back, and it stays at 0 HP and disabled (no strenuous-action hit point).
        /// </summary>
        private static ScenarioDef CounterTripDisabled()
        {
            return Rules("rules/maneuver-trip-counter-disabled", "A disabled defender (0 HP) trips back after a failed trip (PHB p.145, p.158; CMB-136)")
                .Covers("CMB-136", "PHB p.145", "PHB p.158")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6, "Improved Trip")), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("fighter", SturdyDummy)
                .Tweak("orc", c => c.Stats.CurrentHP = 0)
                .Initiative("fighter", "orc")
                .Force(20, 20, "Trip touch attack")
                .Force(20, 2, "Trip Strength check")
                .Force(20, 18, "Trip defense check")
                .Force(20, 18, "Counter-trip check")
                .Force(20, 2, "Counter-trip resist check")
                .Turn("fighter", 1, Step.Maneuver(SpecialAttackType.Trip, "orc"))
                .Turn("orc", 1, Step.Pass())
                .Expect("The orc starts the fight disabled at 0 HP (fixture check)", v =>
                {
                    JsonObj snap = v.Snapshot("orc", 1);
                    return snap != null && snap.Get("st") is HPState st && st == HPState.Disabled
                        ? ExpectResult.Pass("orc disabled")
                        : ExpectResult.Fail("orc state " + (snap != null ? snap.Get("st") : "missing"));
                })
                .Expect("The fighter's trip fails", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Trip).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no trip");
                    return !m.Bool("success") ? ExpectResult.Pass("failed", m.Seq) : ExpectResult.Fail("the trip landed", m.Seq);
                })
                .Expect("The disabled orc trips the fighter back and the fighter is prone (PHB p.158, CMB-136)", v =>
                {
                    TraceEvent counter = v.Maneuvers("orc", SpecialAttackType.Trip).FirstOrDefault(e => e.Bool("counter"));
                    if (counter == null) return ExpectResult.Fail("no counter-trip by the disabled orc");
                    if (!counter.Bool("success")) return ExpectResult.Fail("counter-trip failed with forced dice", counter.Seq);
                    return HasCond(v.Final("fighter"), "Prone") ? ExpectResult.Pass("fighter prone", counter.Seq) : ExpectResult.Fail("fighter not prone", counter.Seq);
                })
                .Expect("The counter-trip costs the disabled orc no hit point (a reaction, not a strenuous action)", v =>
                {
                    JsonObj fin = v.Final("orc");
                    object hp = fin != null ? fin.Get("hp") : null;
                    return fin != null && Convert.ToInt32(hp) == 0 && fin.Get("st") is HPState st && st == HPState.Disabled
                        ? ExpectResult.Pass("orc at 0 HP, disabled")
                        : ExpectResult.Fail("orc HP " + hp + ", state " + (fin != null ? fin.Get("st") : "missing"));
                })
                .Build();
        }

        /// <summary>
        /// Improved Trip's attack when the main weapon cannot attack the tripped foe (owner ruling 2026-10-08, CMB-136;
        /// PHB p.96 "a melee attack against that opponent", p.139). The fighter trips an adjacent orc with forced dice and
        /// no initiation AoO (Improved Trip). <paramref name="variant"/> "unarmed": a longspear (reach, cannot attack
        /// adjacent), so an unarmed strike at the trip's bonus, after the armed orc's AoO (no Improved Unarmed Strike);
        /// "unarmed-ius": the same with Improved Unarmed Strike, no AoO; "offhand": a sling (a ranged weapon, no melee
        /// attack) in the main hand and a dagger in the off hand, so the dagger, as an off-hand attack, no AoO.
        /// </summary>
        private static ScenarioDef ImprovedTripFallback(string variant)
        {
            bool offHand = variant == "offhand";
            bool ius = variant == "unarmed-ius";
            string weaponName = offHand ? "Dagger" : "Unarmed strike";
            string title = offHand
                ? "Improved Trip with a sling in hand, adjacent foe: the off-hand dagger attacks (PHB p.96, CMB-136)"
                : ius
                    ? "Improved Trip with a longspear, adjacent foe: an unarmed strike, no AoO with Improved Unarmed Strike (PHB p.96, p.139, CMB-136)"
                    : "Improved Trip with a longspear, adjacent foe: an unarmed strike after the foe's AoO (PHB p.96, p.139, CMB-136)";
            ScenarioBuilder b = Rules("rules/maneuver-trip-improved-" + variant, title)
                .Covers("CMB-136", "PHB p.96", "PHB p.139")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => ius ? Fighter("Fighter", 6, "Improved Trip", "Improved Unarmed Strike") : Fighter("Fighter", 6, "Improved Trip")), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("fighter", c =>
                {
                    StripOffHand(c);
                    InventoryComponent inv = c.GetComponent<InventoryComponent>();
#pragma warning disable CS0618 // the longspear has no ItemID value
                    inv.CharacterInventory.DirectEquip(offHand
                        ? ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponSling)
                        : ItemDatabase.CloneItem(DND35e.Identifiers.ItemIDs.LONGSPEAR), EquipSlot.RightHand);
#pragma warning restore CS0618
                    if (offHand)
                        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponDagger), EquipSlot.LeftHand);
                    inv.CharacterInventory.RecalculateStats();
                    SturdyDummy(c);
                })
                .Tweak("orc", SturdyDummy)
                .Initiative("fighter", "orc")
                .Force(20, 20, "Trip touch attack")
                .Force(20, 18, "Trip Strength check")
                .Force(20, 2, "Trip defense check")
                .Turn("fighter", 1, Step.Maneuver(SpecialAttackType.Trip, "orc"))
                .Turn("orc", 1, Step.Pass())
                .Expect("The trip step is done", Expect.StepStatus("fighter", 1, "Maneuver", 0, "done"))
                .Expect("Improved Trip provokes no AoO for the trip (PHB p.96)", Expect.None("aoo", e => e.Str("trigger") == "maneuver"))
                .Expect("The trip succeeds and the orc is prone", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Trip).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no trip");
                    return m.Bool("success") && HasCond(v.Final("orc"), "Prone") ? ExpectResult.Pass("orc prone", m.Seq) : ExpectResult.Fail("success " + m.Get("success"), m.Seq);
                })
                .Expect("One Improved Trip attack, with the " + weaponName + ", at the trip's bonus (BAB +6, STR +3: mod 9)", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.Trip).FirstOrDefault();
                    List<TraceEvent> attacks = v.Attacks("fighter", "orc", false, 1);
                    int[] seqs = attacks.Select(e => e.Seq).ToArray();
                    if (m == null) return ExpectResult.Fail("no trip");
                    if (attacks.Count != 1) return ExpectResult.Fail(attacks.Count + " attacks by the fighter", seqs);
                    if (attacks[0].Seq < m.Seq) return ExpectResult.Fail("the attack came before the trip", seqs);
                    if (attacks[0].Str("weapon") != weaponName) return ExpectResult.Fail("weapon " + attacks[0].Str("weapon"), seqs);
                    return attacks[0].Int("mod") == 9
                        ? ExpectResult.Pass(weaponName + ", mod 9", seqs)
                        : ExpectResult.Fail("mod " + attacks[0].Int("mod"), seqs);
                });

            if (offHand || ius)
                b.Expect("The " + weaponName.ToLowerInvariant() + " provokes no AoO (" + (ius ? "Improved Unarmed Strike, " : "a weapon, ") + "PHB p.139)",
                    Expect.None("aoo", e => e.Str("by") == "orc"));
            else
                b.Expect("The unarmed strike provokes the armed orc's AoO, which comes before it (PHB p.139)", v =>
                {
                    List<TraceEvent> aoos = v.AoOs("orc", "fighter");
                    TraceEvent strike = v.Attacks("fighter", "orc", false, 1).FirstOrDefault();
                    if (aoos.Count != 1) return ExpectResult.Fail(aoos.Count + " AoOs by the orc", aoos.Select(e => e.Seq).ToArray());
                    if (aoos[0].Str("trigger") != "unarmed") return ExpectResult.Fail("AoO trigger " + aoos[0].Str("trigger"), aoos[0].Seq);
                    if (strike == null) return ExpectResult.Fail("no unarmed strike", aoos[0].Seq);
                    return aoos[0].Seq < strike.Seq
                        ? ExpectResult.Pass("AoO " + aoos[0].Seq + " before the strike " + strike.Seq, aoos[0].Seq, strike.Seq)
                        : ExpectResult.Fail("the AoO came after the strike", aoos[0].Seq, strike.Seq);
                });
            return b.Build();
        }

        /// <summary>
        /// A Ui hero (Improved Trip, no Improved Unarmed Strike, 1 HP) holding a sling, with plain gauntlets on, trips the
        /// adjacent orc. The sling makes no melee attack and the hero has no off-hand weapon, so the Improved Trip attack is
        /// an unarmed strike (a gauntlet attack is an unarmed attack, PHB p.118), which provokes the armed orc's AoO first
        /// (PHB p.139). (The PC trip target list uses the main weapon's reach, so a Ui hero with a longspear cannot pick an
        /// adjacent foe, CMB-104.) The forced AoO hit drops the hero, who then makes no unarmed strike, and its turn ends at
        /// once instead of offering its remaining attack (GameManager.FinalizeSpecialAttackAttackerDropped, CMB-136 review).
        /// </summary>
        private static ScenarioDef ImprovedTripUnarmedDropsUi()
        {
            return Rules("rules/maneuver-trip-improved-unarmed-dropped-ui", "A Ui hero dropped by the AoO before its unarmed Improved Trip attack: no strike, its turn ends (PHB p.139, CMB-136)")
                .Covers("CMB-136", "PHB p.96", "PHB p.139", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6, "Improved Trip")), 10, 10, Control.Ui)
                // A second PC keeps the party fighting when the hero drops (combat ends when a whole side is down).
                .Pc("ally", ActorSource.Stats(() => Fighter("Ally", 6)), 3, 3, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("hero", c =>
                {
                    StripOffHand(c);
                    InventoryComponent inv = c.GetComponent<InventoryComponent>();
                    inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponSling), EquipSlot.RightHand);
#pragma warning disable CS0618 // the plain gauntlet has no ItemID value
                    inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(DND35e.Identifiers.ItemIDs.GAUNTLET), EquipSlot.Hands);
#pragma warning restore CS0618
                    inv.CharacterInventory.RecalculateStats();
                    c.Stats.CurrentHP = 1;
                })
                .Tweak("orc", SturdyDummy)
                .Initiative("hero", "orc", "ally")
                .Force(20, 20, "Trip touch attack")
                .Force(20, 18, "Trip Strength check")
                .Force(20, 2, "Trip defense check")
                .Force(20, 19, null, -1) // every other d20 (initiative, the orc's AoO roll): a hit, no threat (greataxe 20/x3)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Trip, "orc"))
                .Turn("orc", 1, Step.Pass())
                .Turn("ally", 1, Step.Pass())
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The trip step is done", Expect.StepStatus("hero", 1, "Maneuver", 0, "done"))
                .Expect("The trip succeeds and the orc is prone", v =>
                {
                    TraceEvent m = v.Maneuvers("hero", SpecialAttackType.Trip).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no trip");
                    return m.Bool("success") && HasCond(v.Final("orc"), "Prone") ? ExpectResult.Pass("orc prone", m.Seq) : ExpectResult.Fail("success " + m.Get("success"), m.Seq);
                })
                .Expect("The orc's AoO for the unarmed strike drops the hero, who makes no strike (PHB p.139)", v =>
                {
                    List<TraceEvent> aoos = v.AoOs("orc", "hero");
                    if (aoos.Count != 1) return ExpectResult.Fail(aoos.Count + " AoOs by the orc", aoos.Select(e => e.Seq).ToArray());
                    if (aoos[0].Str("trigger") != "unarmed") return ExpectResult.Fail("AoO trigger " + aoos[0].Str("trigger"), aoos[0].Seq);
                    if (!ScenarioChecks.IsDownSnapshot(v.Final("hero"))) return ExpectResult.Fail("the hero is still up", aoos[0].Seq);
                    List<TraceEvent> strikes = v.Attacks("hero", "orc", false, 1);
                    return strikes.Count == 0
                        ? ExpectResult.Pass("hero down, no strike", aoos[0].Seq)
                        : ExpectResult.Fail(strikes.Count + " attacks by the dropped hero", strikes.Select(e => e.Seq).ToArray());
                })
                .Expect("The dropped hero's turn ends at once (no action menu for a downed PC)", v =>
                {
                    List<TraceEvent> lines = v.Log("Hero is incapacitated during the trip");
                    return lines.Count == 1
                        ? ExpectResult.Pass(lines[0].Str("text"), lines[0].Seq)
                        : ExpectResult.Fail(lines.Count + " incapacitation lines", lines.Select(e => e.Seq).ToArray());
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
        /// CRE-045 (MM p.312, p.304): a Monster Manual creature from the NPC database full-attacks a sturdy target dummy
        /// through the NPC executor. Its two primary claws roll at BAB + STR + size and its secondary bite 2 lower with
        /// Multiattack (green slaad, MM p.230) or 5 lower without (rakshasa, MM p.211). The order is the data's: claw,
        /// claw, bite. The expected numbers come from the data's BAB and STR, so the green slaad case checks the order
        /// and the gap only: its data Str 19 gives claws +12 and bite +10, not the MM's +14 and +12, until its Str is
        /// corrected (CRE-045). <paramref name="mmPrimary"/>, when given, also pins the claw bonus to the MM value
        /// (rakshasa: claws +8, bite +3).
        /// </summary>
        private static ScenarioDef MmNaturalSequence(string npcId, bool multiattack, int? mmPrimary)
        {
            string id = multiattack ? "rules/mm-natural-sequence-multiattack" : "rules/mm-natural-sequence-secondary";
            string title = multiattack
                ? "A green slaad's claws are primary and its bite secondary at -2 with Multiattack (MM p.230, p.304, CRE-045)"
                : "A rakshasa's claws are primary and its bite secondary at -5 (MM p.211, p.312, CRE-045)";
            return Rules(id, title)
                .Covers("CRE-045", "MM p.312", multiattack ? "MM p.304" : "MM p.211")
                .MaxRounds(1)
                .Pc("dummy", ActorSource.Stats(() => FighterOfRace("Dummy", "Human")), 10, 10, Control.Scripted)
                .Npc("beast", npcId, 11, 10, Control.Scripted)
                .Tweak("dummy", SturdyDummy)
                .Initiative("beast", "dummy")
                .Turn("beast", 1, Step.Attack("dummy"))
                .Turn("dummy", 0, Step.Pass())
                .Expect("The attack step is done", Expect.StepStatus("beast", 1, "Attack", 0, "done"))
                .Expect(multiattack ? "Claw, claw at BAB + STR + size, then the bite 2 lower (Multiattack)" : "Claw, claw at BAB + STR + size, then the bite 5 lower", v =>
                {
                    NPCDefinition def = NPCDatabase.Get(npcId);
                    if (def == null) return ExpectResult.Fail("no NPC entry " + npcId);
                    if (def.Feats.Contains("Multiattack") != multiattack)
                        return ExpectResult.Fail(npcId + " Multiattack is " + def.Feats.Contains("Multiattack"));
                    List<TraceEvent> attacks = v.Attacks("beast", "dummy", false, 1);
                    int[] m = attacks.Select(e => e.Int("mod")).ToArray();
                    int[] seqs = attacks.Select(e => e.Seq).ToArray();
                    string mods = "mods [" + string.Join(",", m) + "]";
                    int primary = def.BAB + Mathf.FloorToInt((def.STR - 10) / 2f) + def.SizeCategory.GetAttackAndAcModifier();
                    int gap = multiattack ? 2 : 5;
                    if (m.Length != 3) return ExpectResult.Fail(m.Length + " attacks, " + mods, seqs);
                    if (mmPrimary.HasValue && primary != mmPrimary.Value)
                        return ExpectResult.Fail(npcId + " data gives claws +" + primary + ", MM +" + mmPrimary.Value + "; " + mods, seqs);
                    return m[0] == primary && m[1] == primary && m[2] == primary - gap
                        ? ExpectResult.Pass(mods, seqs)
                        : ExpectResult.Fail(mods + ", expected [" + primary + "," + primary + "," + (primary - gap) + "]", seqs);
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

        /// <summary>
        /// CMB-128 (owner ruling 2026-10-08; PHB p.138, p.154, p.92): a creature that moved earlier in the round and then
        /// follows a defender it bull rushed gets no second movement AoO from the same opponent. The goblin watcher at
        /// (10,9) (DEX 16, Combat Reflexes) threatens (9,9), (10,10) and (11,10). The fighter moves from (9,9) to (10,10)
        /// (one movement AoO), bull rushes the orc at (11,10) (entering its space provokes the goblin again: the bull
        /// rush's own provocation, CMB-113) and follows it out of (10,10) and (11,10): no AoO, because the goblin's
        /// movement opportunity this round came during the move. The orc makes no AoO; forced dice give a margin of at
        /// least 15. Before CMB-128 the follow drew a third AoO.
        /// </summary>
        private static ScenarioDef BullRushAfterMove()
        {
            return Rules("rules/maneuver-bullrush-after-move", "Move, then bull rush: one movement AoO from a Combat Reflexes watcher for the move and the follow together (PHB p.138, p.154, CMB-128)")
                .Covers("CMB-128", "CMB-113", "PHB p.92", "PHB p.138", "PHB p.154")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6)), 9, 9, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Npc("goblin", "goblin", 10, 9, Control.Scripted)
                .Tweak("fighter", SturdyDummy)
                .Tweak("orc", SturdyDummyWithoutAoO)
                .Tweak("goblin", c =>
                {
                    c.Stats.DEX = 16;
                    c.Stats.Feats.Add("Combat Reflexes");
                })
                .Initiative("goblin", "orc", "fighter")
                .Force(20, 20, "Bull rush check")
                .Force(20, 1, "Bull rush defense")
                .Turn("goblin", 1, Step.Pass())
                .Turn("orc", 1, Step.Pass())
                .Turn("fighter", 1,
                    Step.Assert("fixture: the goblin has AoOs to spare this round", ctx => ctx.Get("goblin").Stats.MaxAttacksOfOpportunity >= 3),
                    Step.Move(10, 10),
                    Step.Maneuver(SpecialAttackType.BullRushAttack, "orc"))
                .Expect("The move and the bull rush are done", Expect.All(
                    Expect.StepStatus("fighter", 1, "Move", 0, "done"),
                    Expect.StepStatus("fighter", 1, "Maneuver", 0, "done"),
                    Expect.AssertsPass()))
                .Expect("The orc makes no AoO (fixture check)", Expect.None("aoo", e => e.Str("by") == "orc"))
                .Expect("The bull rush succeeds and the fighter moves with the orc (fixture check)", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.BullRushAttack).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no bull rush");
                    if (!m.Bool("success")) return ExpectResult.Fail("bull rush failed with forced dice", m.Seq);
                    if (ScenarioChecks.IsDownSnapshot(v.Final("fighter"))) return ExpectResult.Inconclusive("the fighter was dropped");
                    Vector2Int? o = Pos(v.Final("orc")), f = Pos(v.Final("fighter"));
                    if (o == null || f == null) return ExpectResult.Fail("no final positions");
                    return o.Value.y == 10 && o.Value.x >= 14 && f.Value == new Vector2Int(o.Value.x - 1, 10)
                        ? ExpectResult.Pass("orc at " + o + ", fighter at " + f, m.Seq)
                        : ExpectResult.Fail("orc at " + o + ", fighter at " + f, m.Seq);
                })
                .Expect("The goblin makes one movement AoO during the move, before the bull rush (PHB p.137)", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.BullRushAttack).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no bull rush");
                    List<TraceEvent> moveAoOs = v.AoOs("goblin", null, "movement");
                    return moveAoOs.Count == 1 && moveAoOs[0].Seq < m.Seq
                        ? ExpectResult.Pass("movement AoO #" + moveAoOs[0].Seq + " before #" + m.Seq, moveAoOs[0].Seq, m.Seq)
                        : ExpectResult.Fail(moveAoOs.Count + " movement AoOs", moveAoOs.Select(e => e.Seq).ToArray());
                })
                .Expect("Entering the orc's space provokes the goblin again: the bull rush's own provocation (PHB p.154, CMB-113)", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.BullRushAttack).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no bull rush");
                    List<TraceEvent> entry = v.AoOs("goblin", null, "maneuver");
                    return entry.Count == 1 && entry[0].Seq < m.Seq
                        ? ExpectResult.Pass("entry AoO #" + entry[0].Seq + " before #" + m.Seq, entry[0].Seq, m.Seq)
                        : ExpectResult.Fail(entry.Count + " entry AoOs", entry.Select(e => e.Seq).ToArray());
                })
                .Expect("No AoO from the goblin as the fighter follows: its movement opportunity this round came during the move (PHB p.138, CMB-128)",
                    Expect.None("aoo", e => e.Str("by") == "goblin" && e.Str("trigger") == "bullrush-move"))
                .Build();
        }

        /// <summary>
        /// CMB-129 (owner ruling 2026-10-08; PHB p.154): the squares followed after a standard-action bull rush are
        /// independent of the move action, so the follow does not shrink a later move action. The fighter at (10,10) bull
        /// rushes the orc at (11,10) (forced dice: a margin of at least 15, so the fighter follows it 3 or more squares, to
        /// (13,10) or beyond), then still has its move action and its whole movement budget, and moves to (14,6): 4 squares,
        /// more than the budget would leave if the squares followed counted against the move (a budget of 4 to 6 squares,
        /// less 3 or more followed).
        /// The orc makes no AoO.
        /// </summary>
        private static ScenarioDef BullRushThenMove()
        {
            int budgetBefore = -1;
            return Rules("rules/maneuver-bullrush-then-move", "Standard bull rush, then a move action: the follow does not shrink the move (PHB p.154, CMB-129)")
                .Covers("CMB-129", "PHB p.154")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("orc", SturdyDummyWithoutAoO)
                .Initiative("orc", "fighter")
                .Force(20, 20, "Bull rush check")
                .Force(20, 1, "Bull rush defense")
                .Turn("orc", 1, Step.Pass())
                .Turn("fighter", 1,
                    Step.Assert("fixture: the fighter's movement budget is recorded before the bull rush (4 squares or more)", ctx =>
                    {
                        budgetBefore = GameManager.Instance.GetCurrentMoveRangeSquares(ctx.Get("fighter"));
                        return budgetBefore >= 4;
                    }),
                    Step.Maneuver(SpecialAttackType.BullRushAttack, "orc"),
                    Step.Assert("after following, the fighter still has its move action and the same movement budget", ctx =>
                    {
                        CharacterController f = ctx.Get("fighter");
                        return f.Actions.HasMoveAction && GameManager.Instance.GetCurrentMoveRangeSquares(f) == budgetBefore;
                    }),
                    Step.Move(14, 6))
                .Expect("The bull rush, the move and both checks are done", Expect.All(
                    Expect.StepStatus("fighter", 1, "Maneuver", 0, "done"),
                    Expect.StepStatus("fighter", 1, "Move", 0, "done"),
                    Expect.AssertsPass()))
                .Expect("The bull rush succeeds and the fighter follows the orc 3 squares or more (fixture check)", v =>
                {
                    TraceEvent m = v.Maneuvers("fighter", SpecialAttackType.BullRushAttack).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no bull rush");
                    if (!m.Bool("success")) return ExpectResult.Fail("bull rush failed with forced dice", m.Seq);
                    Vector2Int? o = Pos(v.Final("orc"));
                    return o != null && o.Value.y == 10 && o.Value.x >= 14
                        ? ExpectResult.Pass("orc pushed to " + o, m.Seq)
                        : ExpectResult.Fail("orc at " + o + ", expected (14,10) or beyond", m.Seq);
                })
                .Expect("The later move action takes the fighter 4 squares north to (14,6)", v =>
                {
                    if (ScenarioChecks.IsDownSnapshot(v.Final("fighter"))) return ExpectResult.Inconclusive("the fighter was dropped");
                    Vector2Int? f = Pos(v.Final("fighter"));
                    return f == new Vector2Int(14, 6) ? ExpectResult.Pass("fighter at (14,6)") : ExpectResult.Fail("fighter at " + f);
                })
                .Build();
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

        // ── Trip (Ex) trigger attack (CMB-125) ─────────────────────────

        /// <summary>
        /// A Medium natural-weapon fighter (BAB 4) with Trip (Ex) and no trigger name, so the default bite applies:
        /// bite (primary), claw, claw (secondary). No Improved Trip, so a trip adds no attack.
        /// </summary>
        private static CharacterStats TripBeast(string name)
        {
            CharacterStats s = Fighter(name, 4);
            s.NaturalAttacks.Clear();
            s.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, IsPrimary = true });
            s.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, IsPrimary = false, BonusDamageSource = DamageBonusSource.StrengthHalf });
            s.HasTripAttack = true;
            return s;
        }

        /// <summary>
        /// Every attack d20 forced to 19 (a hit, no threat for a natural weapon) and every free trip lost at the opposed
        /// check (2 against 18), so each hit leaves the target standing and the next hit can show whether it trips.
        /// </summary>
        private static ScenarioBuilder ForceLostFreeTrips(ScenarioBuilder b) => b
            .RecordDice()
            .Force(20, 2, "Trip Strength check", -1)
            .Force(20, 18, "Trip defense check", -1)
            .Force(20, 19, null, -1);

        /// <summary>
        /// The hits of <paramref name="attacker"/> on <paramref name="target"/> (AoOs only when <paramref name="aoo"/>) are the
        /// natural attacks <paramref name="weapons"/> in that order; each one is followed, before the next attack or turn, by
        /// exactly one free trip check when it is the bite and by none otherwise, and no trip touch attack or counter-trip is
        /// rolled anywhere (MM Trip (Ex), CMB-125). The trace's <c>weapon</c> field names every natural attack "Unarmed
        /// strike" (TST-034), so the attacks are told apart by their order, and <paramref name="modCheck"/> (null: none)
        /// checks their modifiers (a secondary claw 5 below the primary bite, MM p.312).
        /// </summary>
        private static Func<TraceView, ExpectResult> FreeTripOnlyAfterBite(string attacker, string target, bool? aoo, Func<int[], bool> modCheck, params string[] weapons)
        {
            return v =>
            {
                List<TraceEvent> attacks = v.Attacks(attacker, target, aoo);
                List<TraceEvent> hits = attacks.Where(e => e.Bool("hit")).ToList();
                int[] mods = hits.Select(e => e.Int("mod")).ToArray();
                string modText = "mods [" + string.Join(",", mods) + "]";
                if (hits.Count != weapons.Length || attacks.Count != weapons.Length)
                    return ExpectResult.Fail(hits.Count + " hits of " + attacks.Count + " attacks, expected " + weapons.Length + " hits (forced 19s), " + modText, attacks.Select(e => e.Seq).ToArray());
                if (modCheck != null && !modCheck(mods))
                    return ExpectResult.Fail(modText + " do not match " + string.Join(",", weapons), hits.Select(e => e.Seq).ToArray());
                if (v.Of("dice").Any(e => e.Str("ctx") == "Trip touch attack"))
                    return ExpectResult.Fail("a trip touch attack was rolled");
                TraceEvent counter = v.Of("dice").FirstOrDefault(e => e.Str("ctx") == "Counter-trip check");
                if (counter != null)
                    return ExpectResult.Fail("a counter-trip followed a free trip", counter.Seq);

                var parts = new List<string>();
                for (int i = 0; i < hits.Count; i++)
                {
                    TraceEvent hit = hits[i];
                    TraceEvent nextAttack = v.Of("attack").FirstOrDefault(e => e.Seq > hit.Seq);
                    TraceEvent nextTurn = v.Of("turn_start").FirstOrDefault(e => e.Seq > hit.Seq);
                    int to = Math.Min(nextAttack != null ? nextAttack.Seq : int.MaxValue, nextTurn != null ? nextTurn.Seq : int.MaxValue);
                    int trips = v.Of("dice").Count(e => e.Seq > hit.Seq && e.Seq < to && e.Str("ctx") == "Trip Strength check");
                    int expected = weapons[i] == "Bite" ? 1 : 0;
                    parts.Add(weapons[i] + ":" + trips);
                    if (trips != expected)
                        return ExpectResult.Fail(weapons[i] + " hit followed by " + trips + " trip checks, expected " + expected + " (" + string.Join(", ", parts) + ")", hit.Seq);
                }
                return ExpectResult.Pass("trip checks per hit " + string.Join(", ", parts) + ", " + modText, hits.Select(e => e.Seq).ToArray());
            };
        }

        /// <summary>
        /// CMB-125: a Trip (Ex) creature with a bite and two claws makes its whole natural attack (the NPC executor, or
        /// the PC natural-attack buttons, claws first) and every attack hits; only the bite hit starts the free trip.
        /// </summary>
        private static ScenarioDef FreeTripTrigger(bool ui)
        {
            string key = ui ? "hero" : "beast";
            ScenarioBuilder b = ForceLostFreeTrips(Rules(ui ? "rules/freetrip-trigger-ui" : "rules/freetrip-trigger",
                    ui ? "Trip (Ex) through the PC natural-attack buttons: claw, claw, bite all hit and only the bite starts the free trip (MM p.283, CMB-125)"
                       : "Trip (Ex) on the NPC natural full attack: bite, claw, claw all hit and only the bite starts the free trip (MM p.283, CMB-125)")
                .Covers("CMB-125", "MM p.283", ui ? "PC_NPC_PARITY" : "AI")
                .MaxRounds(1)
                .Pc(key, ActorSource.Stats(() => TripBeast(ui ? "Hero" : "Beast")), 10, 10, ui ? Control.Ui : Control.Scripted)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak(key, StripAllWeapons)
                .Tweak("dummy", SturdyDummy)
                .Initiative(key, "dummy")
                .Turn("dummy", 0, Step.Pass()));

            Step fixture = Step.Assert("fixture: Trip (Ex), no trigger name (the bite by default), natural weapons only", ctx =>
            {
                CharacterStats s = ctx.Get(key).Stats;
                return s.HasTripAttack && string.IsNullOrEmpty(s.TripTriggerAttackName) && s.IsTripTriggerAttack("Bite") && !s.IsTripTriggerAttack("Claw");
            });
            if (ui)
                b.Turn(key, 1, fixture, Step.NaturalAttack("dummy", "Claw"), Step.NaturalAttack("dummy", "Claw"), Step.NaturalAttack("dummy", "Bite"))
                 .Expect("The hero's turn is a Ui turn", Expect.Controller(key, "ui"))
                 .Expect("Three natural-attack presses are done", Expect.All(
                     Expect.StepStatus(key, 1, "NaturalAttack", 0, "done"),
                     Expect.StepStatus(key, 1, "NaturalAttack", 1, "done"),
                     Expect.StepStatus(key, 1, "NaturalAttack", 2, "done"),
                     Expect.AssertsPass()))
                 .Expect("Claw, claw: no trip; bite: one free trip check (MM p.283, CMB-125)", FreeTripOnlyAfterBite(key, "dummy", false, m => m[0] == m[1] && m[2] - m[0] == 5, "Claw", "Claw", "Bite"));
            else
                b.Turn(key, 1, fixture, Step.Attack("dummy"))
                 .Expect("The beast's turn is a scripted turn", Expect.Controller(key, "scripted"))
                 .Expect("The attack step is done", Expect.All(Expect.StepStatus(key, 1, "Attack", 0, "done"), Expect.AssertsPass()))
                 .Expect("Bite: one free trip check; claw, claw: none (MM p.283, CMB-125)", FreeTripOnlyAfterBite(key, "dummy", false, m => m[1] == m[2] && m[0] - m[1] == 5, "Bite", "Claw", "Claw"));
            return b.Build();
        }

        /// <summary>
        /// CMB-125 on the AoO path (ThreatSystem.ExecuteAoO): the fighter walks away from two wolves. The MM wolf's AoO is
        /// a bite (its data names the bite) and starts the free trip; the second wolf's primary attack is a claw (a test
        /// fixture), so its AoO is a claw hit and starts none.
        /// </summary>
        private static ScenarioDef FreeTripTriggerAoO()
        {
            return ForceLostFreeTrips(Rules("rules/freetrip-trigger-aoo", "Trip (Ex) on an AoO: a wolf's bite AoO starts the free trip, a claw AoO does not (MM p.283, CMB-125)")
                .Covers("CMB-125", "MM p.283", "PHB p.137")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6)), 10, 10, Control.Scripted)
                .Npc("wolf", "wolf", 11, 10, Control.Scripted)
                .Npc("clawwolf", "wolf", 11, 11, Control.Scripted)
                .Tweak("fighter", SturdyDummy)
                .Tweak("clawwolf", c =>
                {
                    c.Stats.NaturalAttacks.Clear();
                    c.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 1, IsPrimary = true });
                    c.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, IsPrimary = false });
                })
                .Initiative("fighter", "wolf", "clawwolf"))
                .Turn("fighter", 1,
                    Step.Assert("fixture: the MM wolf's data names the bite; both wolves have Trip (Ex); the second one's primary attack is a claw", ctx =>
                        ctx.Get("wolf").Stats.TripTriggerAttackName == "Bite" && ctx.Get("wolf").Stats.HasTripAttack
                        && ctx.Get("clawwolf").Stats.HasTripAttack && ctx.Get("clawwolf").Stats.GetPrimaryNaturalAttack().Name == "Claw"),
                    Step.Move(7, 10))
                .Turn("wolf", 0, Step.Pass())
                .Turn("clawwolf", 0, Step.Pass())
                .Expect("The move is done", Expect.All(Expect.StepStatus("fighter", 1, "Move", 0, "done"), Expect.AssertsPass()))
                .Expect("One movement AoO from each wolf (PHB p.137)", Expect.All(
                    Expect.Count("aoo", e => e.Str("by") == "wolf" && e.Str("target") == "fighter", 1, 1),
                    Expect.Count("aoo", e => e.Str("by") == "clawwolf" && e.Str("target") == "fighter", 1, 1)))
                .Expect("The wolf's bite AoO starts one free trip check (MM p.283)", FreeTripOnlyAfterBite("wolf", "fighter", true, null, "Bite"))
                .Expect("The claw AoO starts no free trip (CMB-125)", FreeTripOnlyAfterBite("clawwolf", "fighter", true, null, "Claw"))
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
        /// stayed). The hero is in the orc's reach there (PHB p.149, owner ruling CMB-131), so the AI may attack it from
        /// the shared square or leave first (a move provokes from the hero, a 5-foot step does not); either way it acts
        /// against the hero.
        /// </summary>
        private static ScenarioDef PinReleaseAiStayer()
        {
            return Rules("rules/pin-release-ai-stayer", "The AI orc left in the square shared after a pin release acts against the hero on its own turn (CMB-122, CMB-131; PHB p.148, p.149, p.157)")
                .Covers("CMB-122", "CMB-131", "CMB-089", "PHB p.148", "PHB p.149", "PHB p.157", "PC_NPC_PARITY")
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
                .Expect("The orc attacks the hero or uses a maneuver on it in round 2, from the shared square or after leaving it (CMB-131)", v =>
                {
                    TraceEvent t = v.TurnsOf("orc").FirstOrDefault(e => e.Round == 2);
                    if (t == null) return ExpectResult.Fail("no orc turn in round 2");
                    TraceEvent act = v.Attacks("orc", "hero", aoo: false, round: 2).FirstOrDefault(e => e.Seq > t.Seq)
                        ?? v.Maneuvers("orc").FirstOrDefault(e => e.Round == 2 && e.Seq > t.Seq && e.Str("target") == "hero");
                    if (act != null)
                    {
                        TraceEvent mv = v.Moves("orc", 2).FirstOrDefault(e => e.Seq < act.Seq);
                        return ExpectResult.Pass(act.Ev + " #" + act.Seq + (mv == null ? " from the shared square" : " after " + mv.Str("type") + " " + mv.Get("from") + " -> " + mv.Get("to")), act.Seq);
                    }
                    return ScenarioChecks.IsDownSnapshot(v.Final("orc")) ? ExpectResult.Inconclusive("the orc went down") : ExpectResult.Fail("no attack or maneuver on the hero in round 2", t.Seq);
                })
                .Expect("If the orc leaves the shared square with a move, the hero takes its AoO (CMB-131; PHB p.137); a 5-foot step provokes nothing", v =>
                {
                    TraceEvent mv = v.Moves("orc", 2).FirstOrDefault(e => e.Get("from") is Vector2Int f && f == SharedSquare);
                    if (mv == null) return ExpectResult.Pass("the orc did not leave the shared square");
                    // The move event's type never names a 5-foot step: read it from the orc's round-2 turn-end economy
                    // (ActionEconomy.HasMoved5Ft). A full-round move with no attack or maneuver is a withdraw, whose first
                    // square does not provoke (PHB p.143); a charge is full-round too but attacks, and provokes on leaving.
                    TraceEvent end = v.Of("turn_end").FirstOrDefault(e => e.Round == 2 && e.Str("actor") == "orc" && e.Seq > mv.Seq);
                    JsonObj econ = end != null ? end.Get("econ") as JsonObj : null;
                    if (econ == null) return ExpectResult.Inconclusive("no orc turn end after the move (the orc went down?)");
                    bool fiveFoot = econ.Get("five") is bool five && five;
                    bool fullRound = econ.Get("full") is bool full && full;
                    bool acted = v.Attacks("orc", null, aoo: false, round: 2).Any(e => e.Seq > mv.Seq)
                        || v.Maneuvers("orc").Any(e => e.Round == 2 && e.Seq > mv.Seq);
                    TraceEvent aoo = v.AoOs("hero", "orc", "movement").FirstOrDefault(e => e.Round == 2);
                    if (fiveFoot)
                        return aoo == null ? ExpectResult.Pass("5-foot step, no AoO", mv.Seq) : ExpectResult.Fail("the 5-foot step provoked AoO #" + aoo.Seq, mv.Seq);
                    if (fullRound && !acted)
                        return ExpectResult.Inconclusive("a full-round move without an attack (a withdraw: its first square does not provoke)");
                    return aoo != null && aoo.Seq < mv.Seq ? ExpectResult.Pass(mv.Str("type") + ", AoO #" + aoo.Seq, aoo.Seq) : ExpectResult.Fail("no movement AoO by the hero before " + mv.Str("type") + " #" + mv.Seq, mv.Seq);
                })
                .Build();
        }

        /// <summary>
        /// Owner ruling 2026-10-08 (CMB-131): a creature threatens its own square (PHB p.149, p.137), so after a pin
        /// release the scripted NPC orc that leaves the shared square with a one-square move provokes an AoO from the Ui
        /// hero left in it. Before the ruling that move provoked nothing: the square it ends in is never left, and the
        /// shared square was not threatened. A 5-foot step out of it provokes nothing (PHB p.144).
        /// </summary>
        private static ScenarioDef SharedSquareLeave(bool fiveFoot)
        {
            Step leave = fiveFoot ? Step.FiveFootStep(12, 10) : Step.Move(12, 10);
            string stepLabel = fiveFoot ? "FiveFootStep" : "Move";
            ScenarioBuilder b = Rules(fiveFoot ? "rules/shared-square-5ft-no-aoo" : "rules/shared-square-move-aoo",
                    fiveFoot
                        ? "A 5-foot step out of a square shared after a pin release provokes nothing from the creature left in it (PHB p.144; CMB-131)"
                        : "A one-square move out of a square shared after a pin release provokes from the creature left in it (PHB p.137, p.149; CMB-131)")
                .Covers("CMB-131", "CMB-089", "PHB p.137", "PHB p.149", fiveFoot ? "PHB p.144" : "PHB p.138", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6, "Improved Grapple")), 10, 10, Control.Ui)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Tweak("hero", StripOffHand)
                .Initiative("hero", "orc")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"))
                .Turn("orc", 1, Step.Pass())
                .Turn("hero", 2, Step.GrappleAction("Pin"), Step.GrappleAction("ReleasePin"), Step.EndTurn())
                .Turn("orc", 2, leave)
                .Expect("The pin and the release are done", Expect.All(
                    Expect.StepStatus("hero", 2, "GrappleAction(Pin)", 0, "done"),
                    Expect.StepStatus("hero", 2, "GrappleAction(ReleasePin)", 0, "done")))
                .Expect("Both creatures share the square, free, when the orc's round-2 turn begins", v =>
                {
                    JsonObj h = v.Snapshot("hero", 2, "orc"), o = v.Snapshot("orc", 2, "orc");
                    if (h == null || o == null) return ExpectResult.Fail("no snapshot at the orc's round-2 turn start");
                    bool free = !HasCond(h, "Grappled") && !HasCond(o, "Grappled") && !HasCond(o, "Pinned");
                    return Pos(h) == SharedSquare && Pos(o) == SharedSquare && free
                        ? ExpectResult.Pass("both at " + SharedSquare + ", free") : ExpectResult.Fail("hero at " + Pos(h) + ", orc at " + Pos(o) + ", free " + free);
                })
                .Expect("The orc's " + stepLabel + " is done and takes it from the shared square to (12,10)", v =>
                {
                    ExpectResult r = Expect.StepStatus("orc", 2, stepLabel, 0, "done")(v);
                    if (!r.IsPass) return r;
                    TraceEvent mv = v.Moves("orc", 2).FirstOrDefault();
                    if (mv == null) return ExpectResult.Fail("no orc move in round 2");
                    return mv.Get("from") is Vector2Int f && f == SharedSquare && mv.Get("to") is Vector2Int t && t == new Vector2Int(12, 10)
                        ? ExpectResult.Pass(mv.Str("type") + " " + f + " -> " + t, mv.Seq) : ExpectResult.Fail("move " + mv.Get("from") + " -> " + mv.Get("to"), mv.Seq);
                });
            if (fiveFoot)
                b.Expect("The 5-foot step provokes no AoO (PHB p.144)", Expect.None("aoo", null));
            else
                b.Expect("Exactly one AoO by the hero, for movement, before the orc leaves the shared square (PHB p.137, p.149; CMB-131)", Expect.All(
                        Expect.Count("aoo", e => e.Str("by") == "hero" && e.Str("target") == "orc" && e.Str("trigger") == "movement", 1, 1),
                        Expect.AoOBefore("hero", "orc", e => e.Ev == "move" && e.Str("actor") == "orc")))
                 .Expect("No other AoO", Expect.Count("aoo", null, 1, 1));
            return b.Build();
        }

        /// <summary>
        /// Coup de grace on a helpless square-mate (PHB p.153; own square in reach, PHB p.149, owner ruling CMB-131). The
        /// orc is paralyzed throughout; the Ui hero grapples it, pins it, releases the pin at the start of round 3 (a free
        /// action) and presses Coup de Grace, clicking its own square. The button (CanUseCoupDeGraceAttackOption, which
        /// the AI also asks), the target highlight and the resolver share CharacterController.IsInCoupDeGraceReach, so the
        /// coup de grace resolves; before, the button stayed hidden (no adjacent helpless foe) and the resolver refused
        /// the target as "not adjacent".
        /// </summary>
        private static ScenarioDef SharedSquareCoupDeGrace()
        {
            return Rules("rules/shared-square-coup-de-grace", "A coup de grace on a helpless creature in the attacker's own square after a pin release resolves (PHB p.149, p.153; CMB-131)")
                .Covers("CMB-131", "CMB-089", "PHB p.149", "PHB p.153", "PC_NPC_PARITY")
                .MaxRounds(3)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6, "Improved Grapple")), 10, 10, Control.Ui)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .StartCondition("orc", CombatConditionType.Paralyzed, -1)
                // A far, idle second orc keeps the fight going when the coup de grace kills the first, so the step is traced.
                .Npc("watcher", "orc_grapple_drill", 18, 18, Control.Idle)
                .Tweak("hero", StripOffHand)
                .Initiative("hero", "orc")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"))
                .Turn("orc", 0, Step.Pass())
                .Turn("hero", 2, Step.GrappleAction("Pin"))
                .Turn("hero", 3, Step.GrappleAction("ReleasePin"), Step.Maneuver(SpecialAttackType.CoupDeGrace, "orc"))
                .Expect("The hero's turns are Ui turns", Expect.Controller("hero", "ui"))
                .Expect("The pin, the release and the coup de grace are done", Expect.All(
                    Expect.StepStatus("hero", 2, "GrappleAction(Pin)", 0, "done"),
                    Expect.StepStatus("hero", 3, "GrappleAction(ReleasePin)", 0, "done"),
                    Expect.StepStatus("hero", 3, "Maneuver(CoupDeGrace", 0, "done")))
                .Expect("The hero coup de graces the paralyzed orc in the shared square after the release, without moving (PHB p.153)", v =>
                {
                    TraceEvent release = v.Steps("hero", "GrappleAction(ReleasePin)", 3).FirstOrDefault();
                    if (release == null) return ExpectResult.Fail("no release step");
                    JsonObj h = v.Snapshot("hero", 3, "hero"), o = v.Snapshot("orc", 3, "hero");
                    if (Pos(h) != SharedSquare || Pos(o) != SharedSquare) return ExpectResult.Fail("hero at " + Pos(h) + ", orc at " + Pos(o) + " at the hero's round-3 turn start");
                    TraceEvent cdg = v.Maneuvers("hero", SpecialAttackType.CoupDeGrace).FirstOrDefault(e => e.Round == 3 && e.Seq > release.Seq && e.Str("target") == "orc");
                    if (cdg == null) return ExpectResult.Fail("no coup de grace by the hero on the orc after the release", release.Seq);
                    TraceEvent mv = v.Moves("hero", 3).FirstOrDefault(e => e.Seq < cdg.Seq);
                    if (mv != null) return ExpectResult.Fail("the hero moved first (#" + mv.Seq + ")", mv.Seq);
                    return cdg.Bool("success") ? ExpectResult.Pass("coup de grace #" + cdg.Seq + " from " + SharedSquare, cdg.Seq) : ExpectResult.Fail("the coup de grace was refused", cdg.Seq);
                })
                .Expect("No coup de grace is refused as not adjacent", Expect.None("log", e => (e.Str("text") ?? "").Contains("is not adjacent")))
                .Build();
        }

        /// <summary>
        /// Owner ruling 2026-10-08 (CMB-131): creatures sharing a square may melee each other (PHB p.149). The Ui hero
        /// pins the orc in round 2 and releases it at the start of its round-3 renewal turn (a free action, so the full
        /// round is left), then attacks it in the shared square through the Attack button (a click on its own square
        /// picks the square-mate); the scripted orc then attacks the hero on the NPC path. Neither moves first.
        /// </summary>
        private static ScenarioDef SharedSquareMelee()
        {
            return Rules("rules/shared-square-melee", "Creatures sharing a square after a pin release melee each other without moving: the Ui hero and the NPC orc (PHB p.149; CMB-131)")
                .Covers("CMB-131", "CMB-089", "PHB p.137", "PHB p.149", "PC_NPC_PARITY")
                .MaxRounds(3)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6, "Improved Grapple")), 10, 10, Control.Ui)
                .Npc("orc", "orc_grapple_drill", 11, 10, Control.Scripted)
                .Tweak("hero", StripOffHand)
                .Initiative("hero", "orc")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "orc"))
                .Turn("orc", 0, Step.Pass())
                .Turn("hero", 2, Step.GrappleAction("Pin"))
                .Turn("hero", 3, Step.GrappleAction("ReleasePin"), Step.Attack("orc"))
                .Turn("orc", 3, Step.Attack("hero"))
                .Expect("The hero's turns are Ui turns", Expect.Controller("hero", "ui"))
                .Expect("The release and the hero's attack are done", Expect.All(
                    Expect.StepStatus("hero", 3, "GrappleAction(ReleasePin)", 0, "done"),
                    Expect.StepStatus("hero", 3, "Attack", 0, "done")))
                .Expect("The hero attacks the orc in the shared square after the release, without moving (PHB p.149)", v =>
                {
                    TraceEvent release = v.Steps("hero", "GrappleAction(ReleasePin)", 3).FirstOrDefault();
                    if (release == null) return ExpectResult.Fail("no release step");
                    TraceEvent atk = v.Attacks("hero", "orc", aoo: false, round: 3).FirstOrDefault(e => e.Seq > release.Seq);
                    if (atk == null) return ExpectResult.Fail("no hero attack on the orc after the release", release.Seq);
                    TraceEvent mv = v.Moves("hero", 3).FirstOrDefault(e => e.Seq < atk.Seq);
                    return mv == null ? ExpectResult.Pass("attack #" + atk.Seq + " from " + SharedSquare, atk.Seq) : ExpectResult.Fail("the hero moved first (#" + mv.Seq + ")", mv.Seq);
                })
                .Expect("The NPC orc attacks the hero in the shared square on its round-3 turn, without moving (PHB p.149)", v =>
                {
                    TraceEvent t = v.TurnsOf("orc").FirstOrDefault(e => e.Round == 3);
                    if (t == null)
                        return ScenarioChecks.IsDownSnapshot(v.Final("orc")) ? ExpectResult.Inconclusive("the orc went down") : ExpectResult.Fail("no orc turn in round 3");
                    JsonObj o = v.Snapshot("orc", 3, "orc"), h = v.Snapshot("hero", 3, "orc");
                    if (Pos(o) != SharedSquare || Pos(h) != SharedSquare) return ExpectResult.Fail("orc at " + Pos(o) + ", hero at " + Pos(h) + " at the orc's turn start", t.Seq);
                    TraceEvent atk = v.Attacks("orc", "hero", aoo: false, round: 3).FirstOrDefault(e => e.Seq > t.Seq);
                    if (atk == null) return ExpectResult.Fail("no orc attack on the hero in round 3", t.Seq);
                    TraceEvent mv = v.Moves("orc", 3).FirstOrDefault(e => e.Seq < atk.Seq);
                    return mv == null ? ExpectResult.Pass("attack #" + atk.Seq + " from " + SharedSquare, atk.Seq) : ExpectResult.Fail("the orc moved first (#" + mv.Seq + ")", mv.Seq);
                })
                .Expect("The orc's Attack step is done", Expect.StepStatus("orc", 3, "Attack", 0, "done"))
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

        // ── Natural weapons: sunder, disarm and grapple (owner rulings 2026-10-08, CMB-102) ──

        /// <summary>
        /// A Human fighter 6 (BAB +6, STR 16) fighting with the given natural attacks and no weapon (strip the kit
        /// with <see cref="StripAllWeapons"/>), with Improved Sunder, Improved Disarm and Improved Grapple, so no
        /// maneuver provokes and each adds +4 (PHB p.95-96).
        /// </summary>
        private static CharacterStats NaturalManeuverBeast(string name, params NaturalAttackDefinition[] naturals)
        {
            CharacterStats s = Fighter(name, 6, "Improved Sunder", "Improved Disarm", "Improved Grapple");
            s.NaturalAttacks.Clear();
            foreach (NaturalAttackDefinition natural in naturals)
                s.NaturalAttacks.Add(natural);
            return s;
        }

        private static NaturalAttackDefinition NaturalPrimary(string name) =>
            new NaturalAttackDefinition { Name = name, DamageDice = 6, DamageCount = 1, Count = 1, IsPrimary = true };

        private static NaturalAttackDefinition NaturalSecondaryPair(string name) =>
            new NaturalAttackDefinition { Name = name, DamageDice = 4, DamageCount = 1, Count = 2, IsPrimary = false, BonusDamageSource = DamageBonusSource.StrengthHalf };

        /// <summary>The attacker's modifier on a maneuver's check (the check minus the d20; for a grapple whose touch attack hit, the grapple check).</summary>
        private static int CheckMod(TraceEvent maneuver) => maneuver.Int("check") - maneuver.Int("checkRoll");

        /// <summary>A <see cref="SturdyDummyWithoutAoO"/> holding only its main-hand weapon (no armor or off-hand item), so a PC sunder opens no item chooser.</summary>
        private static void SturdyArmedDummyWeaponOnly(CharacterController c)
        {
            SturdyDummyWithoutAoO(c);
            InventoryComponent inv = c.GetComponent<InventoryComponent>();
            if (inv == null || inv.CharacterInventory == null)
                return;
            inv.CharacterInventory.LeftHandSlot = null;
            inv.CharacterInventory.ArmorRobeSlot = null;
            inv.CharacterInventory.RecalculateStats();
        }

        /// <summary>
        /// A bite/claw/claw beast (BAB +6, STR +3, the three Improved feats) sunders with its bite, disarms with a claw and
        /// grapples with the other claw, against a Medium orc holding a greataxe. Every check is forced to fail (the grapple's
        /// touch attack to hit), so the modifiers can be read: the bite sunder at +6 +3 +4 = 13 with no handedness modifier;
        /// the claw disarm 5 lower (secondary, MM p.312; a natural weapon is one-handed of the creature's size, so no
        /// handedness or size term between two Medium creatures, PHB p.155); the claw grapple's opposed check at the full
        /// BAB, 13 again (the -5 is on its touch attack only, MM p.312). Owner rulings 2026-10-08 (CMB-102).
        /// </summary>
        private static ScenarioDef NaturalWeaponManeuvers()
        {
            return Rules("rules/natural-weapon-maneuvers", "A natural weapon sunders, disarms and grapples by the owner's rulings (PHB p.155-158, MM p.312)")
                .Covers("CMB-102", "PHB p.155", "PHB p.156", "PHB p.158", "MM p.312")
                .MaxRounds(1)
                .Pc("beast", ActorSource.Stats(() => NaturalManeuverBeast("Beast", NaturalPrimary("Bite"), NaturalSecondaryPair("Claw"))), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("beast", StripAllWeapons)
                .Tweak("orc", SturdyDummyWithoutAoO)
                .Initiative("beast", "orc")
                .Force(20, 1, "Sunder attack roll")
                .Force(20, 20, "Sunder defense roll")
                .Force(20, 1, "Disarm attack roll")
                .Force(20, 20, "Disarm defense roll")
                .Force(20, 20, "Touch attack")
                .Force(20, 1, "Grapple check")
                .Force(20, 20, "Grapple check")
                .Turn("orc", 0, Step.Pass())
                .Turn("beast", 1, Step.Maneuver(SpecialAttackType.Sunder, "orc"), Step.Maneuver(SpecialAttackType.Disarm, "orc"),
                    Step.Maneuver(SpecialAttackType.Grapple, "orc"))
                .Expect("Sunder (bite), disarm and grapple (claws) are all done", Expect.All(
                    Expect.StepStatus("beast", 1, "Maneuver", 0, "done"),
                    Expect.StepStatus("beast", 1, "Maneuver", 1, "done"),
                    Expect.StepStatus("beast", 1, "Maneuver", 2, "done")))
                .Expect("A bite may sunder (slashing and bludgeoning, PHB p.158): +6 BAB +3 STR +4 Improved Sunder, no handedness modifier", v =>
                {
                    TraceEvent sunder = v.Maneuvers("beast", SpecialAttackType.Sunder).FirstOrDefault(e => e.Round == 1);
                    if (sunder == null) return ExpectResult.Fail("no sunder by the beast");
                    if (sunder.Int("checkRoll") != 1) return ExpectResult.Fail("the sunder roll was not the forced 1", sunder.Seq);
                    return CheckMod(sunder) == 13
                        ? ExpectResult.Pass("sunder mod 13", sunder.Seq)
                        : ExpectResult.Fail("sunder mod " + CheckMod(sunder), sunder.Seq);
                })
                .Expect("The claw disarm rolls 5 lower than the bite sunder: secondary -5, a natural weapon gets no handedness modifier (PHB p.155)", v =>
                {
                    TraceEvent sunder = v.Maneuvers("beast", SpecialAttackType.Sunder).FirstOrDefault(e => e.Round == 1);
                    TraceEvent disarm = v.Maneuvers("beast", SpecialAttackType.Disarm).FirstOrDefault(e => e.Round == 1);
                    if (sunder == null || disarm == null) return ExpectResult.Fail("sunder " + (sunder != null) + ", disarm " + (disarm != null));
                    if (disarm.Int("checkRoll") != 1) return ExpectResult.Fail("the disarm roll was not the forced 1", disarm.Seq);
                    return CheckMod(sunder) - CheckMod(disarm) == 5
                        ? ExpectResult.Pass("sunder " + CheckMod(sunder) + ", disarm " + CheckMod(disarm), sunder.Seq, disarm.Seq)
                        : ExpectResult.Fail("sunder " + CheckMod(sunder) + ", disarm " + CheckMod(disarm), sunder.Seq, disarm.Seq);
                })
                .Expect("The claw grapple's opposed check uses the full BAB: the same modifier as the bite sunder (MM p.312)", v =>
                {
                    TraceEvent sunder = v.Maneuvers("beast", SpecialAttackType.Sunder).FirstOrDefault(e => e.Round == 1);
                    TraceEvent grapple = v.Maneuvers("beast", SpecialAttackType.Grapple).FirstOrDefault(e => e.Round == 1);
                    if (sunder == null || grapple == null) return ExpectResult.Fail("sunder " + (sunder != null) + ", grapple " + (grapple != null));
                    if (grapple.Int("checkRoll") != 1) return ExpectResult.Fail("the grapple check was not the forced 1 (did the touch attack miss?)", grapple.Seq);
                    return CheckMod(grapple) == CheckMod(sunder)
                        ? ExpectResult.Pass("grapple check mod " + CheckMod(grapple), grapple.Seq, sunder.Seq)
                        : ExpectResult.Fail("grapple check mod " + CheckMod(grapple) + ", sunder mod " + CheckMod(sunder), grapple.Seq, sunder.Seq);
                })
                .Build();
        }

        /// <summary>A beast whose only natural attack is a gore (piercing, MM p.312) cannot sunder; its step attacks instead.</summary>
        private static ScenarioDef NaturalSunderGoreRefused()
        {
            return Rules("rules/natural-sunder-gore-refused", "A gore deals only piercing damage, so it cannot sunder (PHB p.158, MM p.312)")
                .Covers("CMB-102", "PHB p.158", "MM p.312")
                .MaxRounds(1)
                .Pc("beast", ActorSource.Stats(() => NaturalManeuverBeast("Beast", NaturalPrimary("Gore"))), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("beast", StripAllWeapons)
                .Tweak("orc", SturdyDummyWithoutAoO)
                .Initiative("beast", "orc")
                .Turn("orc", 0, Step.Pass())
                .Turn("beast", 1, Step.Maneuver(SpecialAttackType.Sunder, "orc"), Step.Attack("orc"))
                .Expect("The sunder is refused and the gore attacks", Expect.All(
                    Expect.StepStatus("beast", 1, "Maneuver", 0, "refused"),
                    Expect.StepStatus("beast", 1, "Attack", 0, "done")))
                .Expect("No sunder is attempted", Expect.None("maneuver", e => e.Str("by") == "beast"))
                .Build();
        }

        /// <summary>
        /// The PC Sunder button of a gore/claw/claw creature gives up the next unused natural attack, as an NPC gives up the one
        /// at its step (PC_NPC_PARITY; CMB-102 open item 8): refused while the gore (piercing only) is next; after the gore
        /// attacks, the sunder gives up the first claw at the claw's bonus, +6 -5 +3 +4 Improved Sunder = 8.
        /// </summary>
        private static ScenarioDef NaturalSunderUi()
        {
            return Rules("rules/natural-sunder-ui", "A PC's Sunder button needs its next natural attack to sunder: not the gore, then a claw (PHB p.158, MM p.312, PC_NPC_PARITY)")
                .Covers("CMB-102", "PHB p.158", "MM p.312", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => NaturalManeuverBeast("Hero", NaturalPrimary("Gore"), NaturalSecondaryPair("Claw"))), 10, 10, Control.Ui)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("hero", StripAllWeapons)
                .Tweak("orc", SturdyArmedDummyWeaponOnly)
                .Initiative("hero", "orc")
                .Force(20, 1, "Sunder attack roll")
                .Force(20, 20, "Sunder defense roll")
                .Turn("orc", 0, Step.Pass())
                .Turn("hero", 1,
                    Step.Maneuver(SpecialAttackType.Sunder, "orc"),
                    Step.NaturalAttack("orc", "Gore"),
                    Step.Maneuver(SpecialAttackType.Sunder, "orc"),
                    Step.Assert("the gore and the first claw are marked used; the second claw is not", ctx =>
                    {
                        CharacterController hero = ctx.Get("hero");
                        return ctx.Gm.IsNaturalAttackSequenceIndexUsed(hero, 0) && ctx.Gm.IsNaturalAttackSequenceIndexUsed(hero, 1)
                            && !ctx.Gm.IsNaturalAttackSequenceIndexUsed(hero, 2);
                    }))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The first sunder is refused (the gore is next), the gore attacks, the second sunder is done with a claw", Expect.All(
                    Expect.StepStatus("hero", 1, "Maneuver", 0, "refused"),
                    Expect.StepStatus("hero", 1, "NaturalAttack", 0, "done"),
                    Expect.StepStatus("hero", 1, "Maneuver", 1, "done"),
                    Expect.AssertsPass()))
                .Expect("The sunder rolls at the claw's bonus: +6 -5 +3 +4 = 8", v =>
                {
                    List<TraceEvent> sunders = v.Maneuvers("hero", SpecialAttackType.Sunder).Where(e => e.Round == 1).ToList();
                    if (sunders.Count != 1) return ExpectResult.Fail(sunders.Count + " sunders by the hero", sunders.Select(e => e.Seq).ToArray());
                    return CheckMod(sunders[0]) == 8
                        ? ExpectResult.Pass("sunder mod 8", sunders[0].Seq)
                        : ExpectResult.Fail("sunder mod " + CheckMod(sunders[0]), sunders[0].Seq);
                })
                .Build();
        }

        /// <summary>
        /// The size and handedness terms of the disarm and sunder opposed rolls (PHB p.155, p.158): a Medium bite/claw/claw
        /// beast (BAB +6, STR +3, the Improved feats) against a Large ogre (BAB +3, STR +5, size -1) holding its greatclub
        /// (two-handed). Every roll is forced to fail. Only the larger combatant gets +4 per size category; the smaller one
        /// takes no penalty. The bite sunder: 6 +3 +4 = 13 with no handedness or size term (a natural weapon, CMB-141);
        /// the ogre 3 +5 -1 +4 greatclub +4 larger = 15. The claw disarm: 1 +3 +4 = 8 (a one-handed weapon of its own size,
        /// owner ruling 2026-10-08); the ogre 15 again.
        /// </summary>
        /// <summary>
        /// A <see cref="SturdyDummyWithoutAoO"/> ogre holding a plain greatclub (two-handed; <see cref="EquipPlainGreatclub"/>)
        /// and no armor, so the sunder targets the greatclub.
        /// </summary>
        private static void SturdyOgreWithGreatclub(CharacterController c)
        {
            SturdyDummyWithoutAoO(c);
            EquipPlainGreatclub(c);
            InventoryComponent inv = c.GetComponent<InventoryComponent>();
            if (inv == null || inv.CharacterInventory == null)
                return;
            inv.CharacterInventory.ArmorRobeSlot = null;
            inv.CharacterInventory.RecalculateStats();
        }

        private static ScenarioDef NaturalManeuversSize()
        {
            return Rules("rules/natural-maneuvers-size", "Disarm and sunder against a larger foe: only the larger side gets +4, each side its own handedness (PHB p.155, p.158)")
                .Covers("CMB-102", "PHB p.155", "PHB p.158", "MM p.312")
                .MaxRounds(1)
                .Pc("beast", ActorSource.Stats(() => NaturalManeuverBeast("Beast", NaturalPrimary("Bite"), NaturalSecondaryPair("Claw"))), 10, 10, Control.Scripted)
                .Npc("ogre", "ogre", 11, 10, Control.Scripted)
                .Tweak("beast", StripAllWeapons)
                .Tweak("ogre", SturdyOgreWithGreatclub)
                .Initiative("beast", "ogre")
                .Force(20, 1, "Sunder attack roll")
                .Force(20, 20, "Sunder defense roll")
                .Force(20, 1, "Disarm attack roll")
                .Force(20, 20, "Disarm defense roll")
                .Turn("ogre", 0, Step.Pass())
                .Turn("beast", 1, Step.Maneuver(SpecialAttackType.Sunder, "ogre"), Step.Maneuver(SpecialAttackType.Disarm, "ogre"))
                .Expect("Sunder (bite) and disarm (claw) are both done", Expect.All(
                    Expect.StepStatus("beast", 1, "Maneuver", 0, "done"),
                    Expect.StepStatus("beast", 1, "Maneuver", 1, "done")))
                .Expect("Sunder: the Medium bite 13 (no size penalty), the Large ogre's greatclub 15 (+4 two-handed, +4 larger)", v =>
                {
                    TraceEvent sunder = v.Maneuvers("beast", SpecialAttackType.Sunder).FirstOrDefault(e => e.Round == 1);
                    if (sunder == null) return ExpectResult.Fail("no sunder by the beast");
                    if (sunder.Int("checkRoll") != 1 || sunder.Int("opposedRoll") != 20) return ExpectResult.Fail("the sunder rolls were not the forced 1 and 20", sunder.Seq);
                    int opposed = sunder.Int("opposed") - sunder.Int("opposedRoll");
                    return CheckMod(sunder) == 13 && opposed == 15
                        ? ExpectResult.Pass("sunder 13 vs 15", sunder.Seq)
                        : ExpectResult.Fail("sunder " + CheckMod(sunder) + " vs " + opposed, sunder.Seq);
                })
                .Expect("Disarm: the Medium claw 8 (no size penalty), the Large ogre's greatclub 15 (+4 two-handed, +4 larger)", v =>
                {
                    TraceEvent disarm = v.Maneuvers("beast", SpecialAttackType.Disarm).FirstOrDefault(e => e.Round == 1);
                    if (disarm == null) return ExpectResult.Fail("no disarm by the beast");
                    if (disarm.Int("checkRoll") != 1 || disarm.Int("opposedRoll") != 20) return ExpectResult.Fail("the disarm rolls were not the forced 1 and 20", disarm.Seq);
                    int opposed = disarm.Int("opposed") - disarm.Int("opposedRoll");
                    return CheckMod(disarm) == 8 && opposed == 15
                        ? ExpectResult.Pass("disarm 8 vs 15", disarm.Seq)
                        : ExpectResult.Fail("disarm " + CheckMod(disarm) + " vs " + opposed, disarm.Seq);
                })
                .Build();
        }

        // ── Haste's extra attack with natural weapons (PHB p.239; owner decision 2026-10-07, CMB-106) ──

        /// <summary>
        /// The bite/claw/claw beast without weapons, hasted as the Haste spell handler leaves it (GameManager_Spells_H):
        /// the extra attack (ApplyHasteEffect) and the +1 attack, +1 dodge AC and +1 Reflex in the Stats fields the
        /// formulas read (PHB p.239). ApplyHasteEffect alone leaves the +1s out (CMB-147).
        /// </summary>
        private static void HastedBeast(CharacterController c)
        {
            StripAllWeapons(c);
            c.Stats.HasteAttackBonus = 1;
            c.Stats.HasteACBonus = 1;
            c.Stats.HasteReflexBonus = 1;
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

        /// <summary>
        /// The PC Full Attack button and the PC pounce let the player pick Haste's extra natural attack (PHB p.239; owner
        /// decision 2026-10-07, CMB-106: the attacker chooses; CMB-124). The hasted bite/claw/claw hero full attacks the
        /// adjacent dummy (Full Attack button; the coroutine's 5-foot step prompts are skipped), or charges it from three
        /// squares away with Pounce (MM p.313: a full attack at the end of a charge, each attack with the charge's +2).
        /// When the sequence reaches the Haste step the chooser opens with one option per natural-attack type; the step
        /// clicks <paramref name="hasteAttack"/>, or Cancel, which keeps the default (the bite: highest bonus).
        /// </summary>
        private static ScenarioDef HasteNaturalChooserUi(bool pounce, string hasteAttack)
        {
            const string key = "hero";
            bool picked = hasteAttack != null;
            string extra = picked ? hasteAttack : "Bite";
            string id = pounce ? "rules/haste-natural-pounce-ui"
                : picked ? "rules/haste-natural-full-attack-ui" : "rules/haste-natural-full-attack-ui-default";
            string title = pounce
                ? "A hasted PC that pounces picks Haste's extra natural attack: bite, claw, claw, Haste claw, each with the charge's +2 (PHB p.239, MM p.313, CMB-124)"
                : picked
                    ? "The PC Full Attack button lets a hasted PC pick Haste's extra natural attack: bite, claw, claw, Haste claw (PHB p.239, CMB-124)"
                    : "Cancelling the Full Attack button's Haste chooser keeps the default natural attack: bite, claw, claw, Haste bite (CMB-124)";
            string stepLabel = pounce ? "Charge" : "FullAttack";

            ScenarioBuilder b = Rules(id, title)
                .Covers("CMB-124", "CMB-106", "PHB p.239", "MM p.312", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc(key, ActorSource.Stats(() =>
                {
                    CharacterStats s = NaturalBeast("Hero", false);
                    s.HasPounce = pounce;
                    return s;
                }), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", pounce ? 13 : 11, 10, Control.Scripted)
                .Tweak(key, HastedBeast)
                .Tweak("dummy", SturdyDummy)
                .Initiative(key, "dummy")
                .Turn("dummy", 0, Step.Pass())
                .Turn(key, 1, pounce ? Step.Charge("dummy", hasteAttack: hasteAttack) : Step.FullAttack("dummy", hasteAttack))
                .Expect("The hero's turn is a Ui turn", Expect.Controller(key, "ui"))
                .Expect("The " + stepLabel + " step is done and the Haste chooser was answered " + (picked ? "with the " + hasteAttack.ToLowerInvariant() : "with Cancel"), v =>
                {
                    List<TraceEvent> steps = v.Steps(key, stepLabel, 1);
                    if (steps.Count == 0)
                        return ExpectResult.Fail("no " + stepLabel + " step");
                    TraceEvent st = steps[0];
                    string note = st.Str("note") ?? "";
                    string wanted = picked ? "Haste chooser: " + hasteAttack : "Haste chooser cancelled (default)";
                    return st.Str("status") == "done" && note.Contains(wanted)
                        ? ExpectResult.Pass(st.Str("step") + " done; " + note, st.Seq)
                        : ExpectResult.Fail(st.Str("step") + " " + st.Str("status") + " (" + note + "), expected done with '" + wanted + "'", st.Seq);
                })
                // The bite rolls +8 (BAB 4, STR +3, Haste +1, PHB p.239), a claw +3 (secondary -5, MM p.312); a pounce adds
                // the charge's +2 to every attack (PHB p.154). The trace's weapon field is not yet set when the attack event fires, so the
                // Haste attack's weapon is read from its modifier: the claw's or the bite's.
                .Expect("Bite, claw, claw, then the Haste " + extra.ToLowerInvariant() + " at its normal bonus"
                    + (pounce ? ", each with the charge's +2: +10, +5, +5, +5 (PHB p.154, p.239; MM p.312-313)" : ": +8, +3, +3, " + (picked ? "+3" : "+8") + " (PHB p.239; MM p.312)"),
                    pounce ? Expect.ModSequence(key, 1, 10, 5, 5, 5)
                        : Expect.ModSequence(key, 1, 8, 3, 3, picked ? 3 : 8));

            return b.Build();
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

        // ── Improved Grab size (CMB-126; MM p.310) ─────────────────────

        /// <summary>
        /// Every d20 rolls 19 (a hit that is not a crit threat for a bite or claw) except the grab's own "Grapple check",
        /// which rolls 10; the dice are recorded, so a grab attempt shows as "Grapple check" dice after the trigger hit.
        /// </summary>
        private static ScenarioBuilder ForceGrabDice(ScenarioBuilder b) => b
            .RecordDice()
            .Force(20, 10, "Grapple check", -1)
            .Force(20, 19, null, -1);

        /// <summary>
        /// The "Grapple check" dice rolled after <paramref name="attacker"/>'s first hit on <paramref name="target"/> and
        /// before the next turn starts; null when that attacker never hit that target.
        /// </summary>
        private static List<TraceEvent> GrabChecksAfterHit(TraceView v, string attacker, string target, out TraceEvent hit)
        {
            hit = v.Attacks(attacker, target, false).FirstOrDefault(e => e.Bool("hit"));
            if (hit == null)
                return null;
            int from = hit.Seq;
            TraceEvent nextTurn = v.Of("turn_start").FirstOrDefault(e => e.Seq > from);
            int to = nextTurn != null ? nextTurn.Seq : int.MaxValue;
            return v.Of("dice").Where(e => e.Seq > from && e.Seq < to && e.Str("ctx") == "Grapple check").ToList();
        }

        private static Func<TraceView, ExpectResult> NoGrabAfterHit(string attacker, string target)
        {
            return v =>
            {
                List<TraceEvent> checks = GrabChecksAfterHit(v, attacker, target, out TraceEvent hit);
                if (checks == null) return ExpectResult.Fail(attacker + " never hit " + target + " with the forced 19");
                if (checks.Count > 0) return ExpectResult.Fail(checks.Count + " grapple checks after the hit", checks.Select(e => e.Seq).ToArray());
                return !HasCond(v.Final(target), "Grappled")
                    ? ExpectResult.Pass("hit #" + hit.Seq + ", no grapple check, " + target + " not grappled", hit.Seq)
                    : ExpectResult.Fail(target + " ends grappled", hit.Seq);
            };
        }

        /// <summary>
        /// The size gate's combat log line ("No Improved Grab after the ... hit: ... too large ...") between
        /// <paramref name="attacker"/>'s first hit on <paramref name="target"/> and the next turn: the positive control of a
        /// NoGrabAfterHit check, showing the hit was a matched trigger that reached the gate and was refused for size.
        /// </summary>
        private static Func<TraceView, ExpectResult> RefusedForSizeAfterHit(string attacker, string target)
        {
            return v =>
            {
                TraceEvent hit = v.Attacks(attacker, target, false).FirstOrDefault(e => e.Bool("hit"));
                if (hit == null) return ExpectResult.Fail(attacker + " never hit " + target + " with the forced 19");
                TraceEvent nextTurn = v.Of("turn_start").FirstOrDefault(e => e.Seq > hit.Seq);
                int to = nextTurn != null ? nextTurn.Seq : int.MaxValue;
                TraceEvent line = v.Log("No Improved Grab after the .* hit: .* too large for .*Improved Grab")
                    .FirstOrDefault(e => e.Seq > hit.Seq && e.Seq < to);
                return line != null
                    ? ExpectResult.Pass("hit #" + hit.Seq + ", size refusal logged: " + line.Str("text"), hit.Seq, line.Seq)
                    : ExpectResult.Fail("no size-refusal log line after hit #" + hit.Seq, hit.Seq);
            };
        }

        private static Func<TraceView, ExpectResult> GrabAfterHit(string attacker, string target)
        {
            return v =>
            {
                List<TraceEvent> checks = GrabChecksAfterHit(v, attacker, target, out TraceEvent hit);
                if (checks == null) return ExpectResult.Fail(attacker + " never hit " + target + " with the forced 19");
                return checks.Count == 2
                    ? ExpectResult.Pass("hit #" + hit.Seq + ", then both grapple checks", checks.Select(e => e.Seq).ToArray())
                    : ExpectResult.Fail(checks.Count + " grapple checks after the hit #" + hit.Seq, hit.Seq);
            };
        }

        /// <summary>
        /// Two Medium crocodiles (bite trigger, no size clause in MM p.271) attack on the NPC attack-sequence path: the one
        /// that bites the Medium fighter makes no grab, the one that bites the Small halfling does (MM p.310, CMB-126).
        /// </summary>
        private static ScenarioDef ImprovedGrabSize()
        {
            return ForceGrabDice(Rules("rules/improved-grab-size", "Improved Grab needs a target at least one size smaller: a Medium crocodile grabs a Small halfling, not a Medium fighter (MM p.310, CMB-126)")
                .Covers("CMB-126", "MM p.310", "MM p.271")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => FighterOfRace("Fighter", "Human")), 10, 10, Control.Scripted)
                .Pc("halfling", ActorSource.Stats(() => FighterOfRace("Halfling", "Halfling")), 10, 14, Control.Scripted)
                .Npc("croc", "crocodile", 11, 10, Control.Scripted)
                .Npc("croc2", "crocodile", 11, 14, Control.Scripted)
                .Tweak("fighter", SturdyDummy)
                .Tweak("halfling", SturdyDummy)
                .Initiative("croc", "croc2", "fighter", "halfling"))
                .Turn("croc", 1, Step.Attack("fighter"))
                .Turn("croc2", 1, Step.Attack("halfling"))
                .Turn("fighter", 1, Step.Pass())
                .Turn("halfling", 1, Step.Pass())
                .Expect("Sizes: crocodiles and the fighter Medium, the halfling Small (fixture check)", Expect.All(
                    ActorSize("croc", SizeCategory.Medium), ActorSize("croc2", SizeCategory.Medium),
                    ActorSize("fighter", SizeCategory.Medium), ActorSize("halfling", SizeCategory.Small)))
                .Expect("Both crocodile attacks are done", Expect.All(
                    Expect.StepStatus("croc", 1, "Attack", 0, "done"), Expect.StepStatus("croc2", 1, "Attack", 0, "done")))
                .Expect("The bite on the Medium fighter starts no grab: no grapple check, not grappled (MM p.310)", NoGrabAfterHit("croc", "fighter"))
                .Expect("The bite on the Medium fighter reaches the Improved Grab gate and is refused for size (combat log)", RefusedForSizeAfterHit("croc", "fighter"))
                .Expect("The bite on the Small halfling is followed by the free grab's opposed grapple checks", GrabAfterHit("croc2", "halfling"))
                .Build();
        }

        /// <summary>
        /// The charge paths (NPCExecuteCharge): a Medium crocodile charges the Medium fighter (single charge attack, no
        /// grab) and another the Small halfling (grab); a Large lion pounces (MM p.275) on an enlarged human (Large,
        /// Enlarge Person PHB p.226), whose bite starts no grab because the target is not smaller than the lion; a second
        /// lion pounces on an unenlarged Medium human and grabs, the positive control of the pounce path.
        /// </summary>
        private static ScenarioDef ImprovedGrabSizeCharge()
        {
            return ForceGrabDice(Rules("rules/improved-grab-size-charge", "Improved Grab after a charge or pounce needs a smaller target: crocodiles on Medium and Small, a lion on an enlarged human (MM p.310, CMB-126)")
                .Covers("CMB-126", "MM p.310", "MM p.271", "MM p.275", "PHB p.226")
                .MaxRounds(1)
                .Pc("fighter", ActorSource.Stats(() => FighterOfRace("Fighter", "Human")), 4, 3, Control.Scripted)
                .Pc("halfling", ActorSource.Stats(() => FighterOfRace("Halfling", "Halfling")), 4, 8, Control.Scripted)
                .Pc("giant", ActorSource.Stats(() => FighterOfRace("Giant", "Human")), 4, 13, Control.Scripted)
                .Pc("fighter2", ActorSource.Stats(() => FighterOfRace("Fighter2", "Human")), 4, 17, Control.Scripted)
                .Npc("croc", "crocodile", 9, 3, Control.Scripted)
                .Npc("croc2", "crocodile", 9, 8, Control.Scripted)
                .Npc("lion", "lion", 11, 13, Control.Scripted)
                .Npc("lion2", "lion", 11, 17, Control.Scripted)
                .Tweak("fighter", SturdyDummy)
                .Tweak("halfling", SturdyDummy)
                .Tweak("giant", c => { WithSizeSpell(DND35e.Identifiers.SpellNames.ENLARGE_PERSON)(c); SturdyDummy(c); })
                .Tweak("fighter2", SturdyDummy)
                .Initiative("croc", "croc2", "lion", "lion2", "fighter", "halfling", "giant", "fighter2"))
                .Turn("croc", 1, Step.Charge("fighter"))
                .Turn("croc2", 1, Step.Charge("halfling"))
                .Turn("lion", 1, Step.Charge("giant"))
                .Turn("lion2", 1, Step.Charge("fighter2"))
                .Turn("fighter", 1, Step.Pass())
                .Turn("halfling", 1, Step.Pass())
                .Turn("giant", 1, Step.Pass())
                .Turn("fighter2", 1, Step.Pass())
                .Expect("Sizes: crocodiles Medium, lions Large, fighters Medium, halfling Small, enlarged human Large (fixture check)", Expect.All(
                    ActorSize("croc", SizeCategory.Medium), ActorSize("croc2", SizeCategory.Medium), ActorSize("lion", SizeCategory.Large),
                    ActorSize("lion2", SizeCategory.Large), ActorSize("fighter", SizeCategory.Medium), ActorSize("halfling", SizeCategory.Small),
                    ActorSize("giant", SizeCategory.Large), ActorSize("fighter2", SizeCategory.Medium)))
                .Expect("All four charges are done", Expect.All(
                    Expect.StepStatus("croc", 1, "Charge", 0, "done"), Expect.StepStatus("croc2", 1, "Charge", 0, "done"),
                    Expect.StepStatus("lion", 1, "Charge", 0, "done"), Expect.StepStatus("lion2", 1, "Charge", 0, "done")))
                .Expect("The lion pounces: claw, claw, bite and two rakes at the end of its charge, all hitting (MM p.275; forced 19s)", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("lion", "giant", false, 1);
                    return attacks.Count == 5 && attacks.All(e => e.Bool("hit"))
                        ? ExpectResult.Pass(attacks.Count + " hits, mods " + string.Join(",", attacks.Select(e => e.Int("mod"))), attacks.Select(e => e.Seq).ToArray())
                        : ExpectResult.Fail(attacks.Count + " attacks, hits " + attacks.Count(e => e.Bool("hit")), attacks.Select(e => e.Seq).ToArray());
                })
                .Expect("The charging bite on the Medium fighter starts no grab (MM p.310)", NoGrabAfterHit("croc", "fighter"))
                .Expect("The charging bite on the Small halfling is followed by the free grab's grapple checks", GrabAfterHit("croc2", "halfling"))
                .Expect("The pouncing lion's bite on the Large enlarged human starts no grab (MM p.310: not smaller than the Large lion)", NoGrabAfterHit("lion", "giant"))
                .Expect("The lion's trigger bite on the enlarged human reaches the gate and is refused for size (combat log)", RefusedForSizeAfterHit("lion", "giant"))
                .Expect("Positive control: the second lion's pounce on the unenlarged Medium human is followed by the grab's grapple checks", GrabAfterHit("lion2", "fighter2"))
                .Build();
        }

        /// <summary>
        /// The PC path (CombatFlowService single attack -> TryResolveImprovedGrabAfterSingleAttack): a Medium hero with
        /// Improved Grab on its claw hits a Medium orc with the claw button. The orc is too large, so no Improved Grab
        /// prompt opens (the harness answers none, so a prompt would stall the turn) and no grapple check is rolled.
        /// </summary>
        private static ScenarioDef ImprovedGrabSizeUi()
        {
            return ForceGrabDice(Rules("rules/improved-grab-size-ui", "A PC's Improved Grab claw on a Medium orc: too large, so no grab prompt and no grapple check (MM p.310, CMB-126)")
                .Covers("CMB-126", "MM p.310", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => NaturalManeuverBeast("Hero", NaturalPrimary("Claw"))), 10, 10, Control.Ui)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("hero", c =>
                {
                    StripAllWeapons(c);
                    c.Stats.HasImprovedGrab = true;
                    c.Stats.ImprovedGrabTriggerAttackName = "Claw";
                })
                .Tweak("orc", SturdyDummyWithoutAoO)
                .Initiative("hero", "orc"))
                .Turn("orc", 0, Step.Pass())
                .Turn("hero", 1, Step.NaturalAttack("orc", "Claw"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The claw attack is done", Expect.StepStatus("hero", 1, "NaturalAttack", 0, "done"))
                .Expect("The claw hit on the Medium orc starts no grab: no prompt stall, no grapple check, not grappled (MM p.310)", NoGrabAfterHit("hero", "orc"))
                .Expect("The claw hit reaches the PC path's Improved Grab gate and is refused for size (combat log)", RefusedForSizeAfterHit("hero", "orc"))
                .Build();
        }

        // ── Grapple attacks with natural weapons (CMB-127; PHB p.156, MM p.314) ──────────

        /// <summary>
        /// The lion's grapple turns after round 1, each split into its grapple actions: the attacks made before each
        /// "Lion chooses grapple action [X]" log line (the AI logs the action after resolving it), back to the previous
        /// one or the turn start. Only turns that begin with the lion grappling.
        /// </summary>
        private static List<List<KeyValuePair<string, List<TraceEvent>>>> LionGrappleTurns(TraceView v)
        {
            var header = new System.Text.RegularExpressions.Regex(@"Lion chooses grapple action \[(\w+)\]");
            var turns = new List<List<KeyValuePair<string, List<TraceEvent>>>>();
            foreach (TraceEvent turn in v.TurnsOf("lion"))
            {
                if (turn.Round < 2)
                    continue;
                JsonObj self = v.Snapshot("lion", turn.Round, "lion");
                if (self == null || !(self.Get("gr") is bool g && g))
                    continue;
                TraceEvent next = v.Of("turn_start").FirstOrDefault(e => e.Seq > turn.Seq);
                int end = next != null ? next.Seq : int.MaxValue;
                var actions = new List<KeyValuePair<string, List<TraceEvent>>>();
                int from = turn.Seq;
                foreach (TraceEvent log in v.Of("log").Where(e => e.Seq > turn.Seq && e.Seq < end))
                {
                    System.Text.RegularExpressions.Match m = header.Match(log.Str("text") ?? "");
                    if (!m.Success)
                        continue;
                    int lo = from;
                    List<TraceEvent> attacks = v.Attacks("lion", null, false).Where(a => a.Seq > lo && a.Seq < log.Seq).ToList();
                    actions.Add(new KeyValuePair<string, List<TraceEvent>>(m.Groups[1].Value, attacks));
                    from = log.Seq;
                }
                turns.Add(actions);
            }
            return turns;
        }

        /// <summary>
        /// A Large lion (MM p.274-275: 2 claws +7, bite +2, improved grab with the bite, rake +7; BAB +3, so one iterative
        /// attack) grabs a Medium human on round 1 with its bite (forced 19s; the grab's grapple checks forced to 10, the
        /// lion +12 against +7) and fights the grapple with the AI from round 2. Each grapple attack action is one natural
        /// attack (PHB p.156; MM p.314) in place of one natural attack of its sequence, so the lion has up to 3 a turn (the
        /// count is the unconfirmed one-per-natural-attack reading, CMB-146; its grapple checks stay on its one iterative)
        /// (claw, claw, then the bite: the AI picks the highest bonus first, NaturalAttackChoice), each at its normal bonus
        /// -4 for grappling; the rake's 2 claws come once, with the first. The AI may stop early (a 20% chance after each
        /// action, AI_GrappleRestrictedTurn), so a seed range covers 1 to 3 actions.
        /// </summary>
        private static ScenarioDef GrappleNaturalAttacksAi()
        {
            return ForceGrabDice(Rules("rules/grapple-natural-attacks", "Each grapple attack is one natural attack at its own bonus -4, up to one per natural attack (unconfirmed count, CMB-146); grapple checks on the BAB ladder; rake once a turn (PHB p.156, MM p.314, CMB-127)")
                .Covers("CMB-127", "CMB-146", "PHB p.156", "MM p.314", "MM p.312", "MM p.274", "AI")
                .MaxRounds(3)
                .Pc("hero", ActorSource.Stats(() => FighterOfRace("Hero", "Human")), 10, 10, Control.Scripted)
                .Npc("lion", "lion", 11, 10, Control.Scripted)
                .Tweak("hero", SturdyDummy)
                .Initiative("lion", "hero"))
                .Turn("lion", 1, Step.Attack("hero"))
                .Turn("hero", 0, Step.Pass())
                .AiWhenUnscripted("lion")
                .Expect("Round 1: claw, claw, bite at +7, +7, +2 (MM p.274), then the bite's grab holds the hero", v =>
                {
                    List<TraceEvent> r1 = v.Attacks("lion", "hero", false, 1);
                    int[] m = r1.Select(e => e.Int("mod")).ToArray();
                    if (m.Length != 3 || m[0] != m[1] || m[0] - m[2] != 5)
                        return ExpectResult.Fail("round-1 mods [" + string.Join(",", m) + "]", r1.Select(e => e.Seq).ToArray());
                    JsonObj h2 = v.Snapshot("hero", 2, "lion");
                    return HasCond(h2, "Grappled")
                        ? ExpectResult.Pass("mods [" + string.Join(",", m) + "], hero grappled on the lion's round-2 turn", r1.Select(e => e.Seq).ToArray())
                        : ExpectResult.Fail("the hero is not grappled when the lion's round-2 turn starts");
                })
                .Expect("Each grapple turn: 1 to 3 natural-weapon grapple attacks (one per natural attack, though BAB +3 gives one iterative)", v =>
                {
                    var turns = LionGrappleTurns(v);
                    if (turns.Count == 0) return ExpectResult.Inconclusive("the lion never started a turn grappling");
                    foreach (var t in turns)
                    {
                        int natural = t.Count(a => a.Key == "AttackUnarmed");
                        if (natural < 1 || natural > 3)
                            return ExpectResult.Fail(natural + " natural grapple attacks in a turn (actions: " + string.Join(",", t.Select(a => a.Key)) + ")");
                    }
                    return ExpectResult.Pass(turns.Count + " turn(s): " + string.Join(" / ", turns.Select(t => t.Count + " action(s)")));
                })
                .Expect("A grapple check (pin, damage, escape) comes only as the turn's first action: BAB +3 gives one iterative step, and a grapple natural attack uses it (PHB p.156)", v =>
                {
                    var turns = LionGrappleTurns(v);
                    if (turns.Count == 0) return ExpectResult.Inconclusive("the lion never started a turn grappling");
                    int checks = 0;
                    foreach (var t in turns)
                    {
                        for (int i = 0; i < t.Count; i++)
                        {
                            if (Array.IndexOf(new[] { "PinOpponent", "DamageOpponent", "OpposedGrappleEscape", "UseOpponentWeapon", "AttackWithLightWeapon" }, t[i].Key) < 0) continue;
                            checks++;
                            if (i > 0)
                                return ExpectResult.Fail("grapple action " + (i + 1) + " was " + t[i].Key + " (actions: " + string.Join(",", t.Select(a => a.Key)) + ")");
                        }
                    }
                    return ExpectResult.Pass(checks + " grapple check(s), none after a grapple attack");
                })
                .Expect("Each grapple attack is one attack; the first of a turn adds the rake's 2 claws, the others nothing (MM p.314)", v =>
                {
                    var turns = LionGrappleTurns(v);
                    if (turns.Count == 0) return ExpectResult.Inconclusive("the lion never started a turn grappling");
                    foreach (var t in turns)
                    {
                        for (int i = 0; i < t.Count; i++)
                        {
                            if (t[i].Key != "AttackUnarmed") continue;
                            int expected = i == 0 ? 3 : 1;
                            if (t[i].Value.Count != expected)
                                return ExpectResult.Fail("grapple action " + (i + 1) + " made " + t[i].Value.Count + " attacks, expected " + expected,
                                    t[i].Value.Select(e => e.Seq).ToArray());
                        }
                    }
                    return ExpectResult.Pass("one natural attack per action, rake with the first");
                })
                .Expect("The grapple attacks are claw, claw, bite in that order, each at its round-1 bonus -4 (PHB p.156; MM p.312)", v =>
                {
                    List<TraceEvent> r1 = v.Attacks("lion", "hero", false, 1);
                    if (r1.Count != 3) return ExpectResult.Fail(r1.Count + " round-1 attacks");
                    int claw = r1[0].Int("mod"), bite = r1[2].Int("mod");
                    int[] expected = { claw - 4, claw - 4, bite - 4 };
                    var turns = LionGrappleTurns(v);
                    if (turns.Count == 0) return ExpectResult.Inconclusive("the lion never started a turn grappling");
                    var seen = new List<string>();
                    foreach (var t in turns)
                    {
                        int[] mods = t.Where(a => a.Key == "AttackUnarmed" && a.Value.Count > 0).Select(a => a.Value[0].Int("mod")).ToArray();
                        for (int i = 0; i < mods.Length; i++)
                        {
                            if (mods[i] != expected[i])
                                return ExpectResult.Fail("turn mods [" + string.Join(",", mods) + "], expected [" + string.Join(",", expected.Take(mods.Length)) + "]");
                        }
                        seen.Add("[" + string.Join(",", mods) + "]");
                    }
                    return ExpectResult.Pass("mods " + string.Join(" ", seen) + " from claw " + claw + ", bite " + bite);
                })
                .ExpectXFail("CMB-144", "The rake's claws are not subject to the -4 for attacking in a grapple: each rolls at the claw's round-1 bonus (MM p.314, p.275)", v =>
                {
                    List<TraceEvent> r1 = v.Attacks("lion", "hero", false, 1);
                    if (r1.Count != 3) return ExpectResult.Fail(r1.Count + " round-1 attacks");
                    int claw = r1[0].Int("mod");
                    List<TraceEvent> rakes = LionGrappleTurns(v).Where(t => t.Count > 0 && t[0].Value.Count == 3).SelectMany(t => t[0].Value.Skip(1)).ToList();
                    if (rakes.Count == 0) return ExpectResult.Inconclusive("no rake was made");
                    TraceEvent wrong = rakes.FirstOrDefault(e => e.Int("mod") != claw);
                    return wrong == null
                        ? ExpectResult.Pass(rakes.Count + " rakes at " + claw, rakes.Select(e => e.Seq).ToArray())
                        : ExpectResult.Fail("rake at " + wrong.Int("mod") + ", claw " + claw, wrong.Seq);
                })
                .Build();
        }

        /// <summary>
        /// A PC fighting with a bite and two claws (Fighter 6: BAB +6, two iteratives; Improved Grapple, so the grapple
        /// provokes nothing) grapples on round 1 in place of its bite, then presses the grapple attack button and picks
        /// natural attacks in the natural-attack menu (CMB-127): round 1 the two claws (the second is the only option
        /// left, made at once), a fourth grapple attack is refused; round 2 claw, bite, claw, and a fourth is refused.
        /// </summary>
        private static ScenarioDef GrappleNaturalAttacksUi()
        {
            return Rules("rules/grapple-natural-attacks-ui", "A PC's grapple attack is one natural attack it picks, one per natural attack in its sequence (PHB p.156, CMB-127)")
                .Covers("CMB-127", "PHB p.156", "MM p.312", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => NaturalManeuverBeast("Hero", NaturalPrimary("Bite"), NaturalSecondaryPair("Claw"))), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("hero", StripAllWeapons)
                .Tweak("dummy", SturdyDummyWithoutAoO)
                .Initiative("hero", "dummy")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("dummy", 0, Step.Pass())
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "dummy"), Step.GrappleAttack("Claw"), Step.GrappleAttack("Claw"), Step.GrappleAttack("Bite"))
                .Turn("hero", 2, Step.GrappleAttack("Claw"), Step.GrappleAttack("Bite"), Step.GrappleAttack("Claw"), Step.GrappleAttack("Bite"))
                .Expect("The hero's turns are Ui turns", Expect.Controller("hero", "ui"))
                .Expect("Round 1: the grapple and two grapple claw attacks are done; a fourth action finds no natural attack left", Expect.All(
                    Expect.StepStatus("hero", 1, "Maneuver", 0, "done"),
                    Expect.StepStatus("hero", 1, "GrappleAction(Attack", 0, "done"),
                    Expect.StepStatus("hero", 1, "GrappleAction(Attack", 1, "done"),
                    Expect.StepStatus("hero", 1, "GrappleAction(Attack", 2, "refused", "dropped")))
                .Expect("Round 1: one attack per grapple attack, two in all, both at the claw's bonus", v =>
                {
                    List<TraceEvent> r1 = v.Attacks("hero", "dummy", false, 1);
                    return r1.Count == 2 && r1[0].Int("mod") == r1[1].Int("mod")
                        ? ExpectResult.Pass("mods " + r1[0].Int("mod") + ", " + r1[1].Int("mod"), r1.Select(e => e.Seq).ToArray())
                        : ExpectResult.Fail(r1.Count + " attacks, mods [" + string.Join(",", r1.Select(e => e.Int("mod"))) + "]", r1.Select(e => e.Seq).ToArray());
                })
                .Expect("Round 2: three grapple attacks are done; a fourth is refused (BAB +6 gives two iteratives, the sequence three natural attacks)", Expect.All(
                    Expect.StepStatus("hero", 2, "GrappleAction(Attack", 0, "done"),
                    Expect.StepStatus("hero", 2, "GrappleAction(Attack", 1, "done"),
                    Expect.StepStatus("hero", 2, "GrappleAction(Attack", 2, "done"),
                    Expect.StepStatus("hero", 2, "GrappleAction(Attack", 3, "refused", "dropped")))
                .Expect("Round 2: claw, bite, claw: the bite 5 above the claws (MM p.312), at BAB + STR -4 for grappling (PHB p.156)", v =>
                {
                    List<TraceEvent> r2 = v.Attacks("hero", "dummy", false, 2);
                    int[] m = r2.Select(e => e.Int("mod")).ToArray();
                    int bab = ActorInt(v, "hero", "bab");
                    string mods = "mods [" + string.Join(",", m) + "], BAB " + bab;
                    if (m.Length != 3) return ExpectResult.Fail(m.Length + " attacks, " + mods, r2.Select(e => e.Seq).ToArray());
                    return m[1] - m[0] == 5 && m[0] == m[2] && m[1] == bab + 3 - 4
                        ? ExpectResult.Pass(mods, r2.Select(e => e.Seq).ToArray())
                        : ExpectResult.Fail(mods + ", expected claw, bite (+5, = BAB +3 STR -4), claw", r2.Select(e => e.Seq).ToArray());
                })
                .Build();
        }

        /// <summary>
        /// Grapple checks stay on the BAB ladder for a natural-weapon creature (PHB p.156; CMB-127). A PC fighting with a
        /// bite and two claws at BAB +4 (Fighter 4: one iterative; Improved Grapple) grapples on round 1 in place of its
        /// bite. That grapple took its one iterative step, so the Pin button is refused; its two grapple claw attacks are
        /// natural steps and are made (the second is the only option left, made at once); a third grapple attack finds no
        /// natural attack left. The number of grapple natural attacks (one per natural attack) is the unconfirmed
        /// reading of CMB-146.
        /// </summary>
        private static ScenarioDef GrappleNaturalChecksUi()
        {
            return Rules("rules/grapple-natural-checks-ui", "A natural-weapon PC's grapple checks follow its BAB ladder; its grapple attacks follow its natural attacks (PHB p.156, CMB-127, CMB-146)")
                .Covers("CMB-127", "CMB-146", "PHB p.156", "MM p.312", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() =>
                {
                    CharacterStats s = Fighter("Hero", 4, "Improved Grapple");
                    s.NaturalAttacks.Clear();
                    s.NaturalAttacks.Add(NaturalPrimary("Bite"));
                    s.NaturalAttacks.Add(NaturalSecondaryPair("Claw"));
                    return s;
                }), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("hero", StripAllWeapons)
                .Tweak("dummy", SturdyDummyWithoutAoO)
                .Initiative("hero", "dummy")
                .Force(20, 20, "Touch attack")
                .Force(20, 20, "Grapple check", -1)
                .Turn("dummy", 0, Step.Pass())
                .Turn("hero", 1, Step.Maneuver(SpecialAttackType.Grapple, "dummy"), Step.GrappleAction("Pin"), Step.GrappleAttack("Claw"), Step.GrappleAttack("Claw"), Step.GrappleAttack("Bite"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The grapple is done; the pin after it is refused (BAB +4 gives one iterative step, PHB p.156)", Expect.All(
                    Expect.StepStatus("hero", 1, "Maneuver", 0, "done"),
                    Expect.StepStatus("hero", 1, "GrappleAction(Pin", 0, "refused", "dropped")))
                .Expect("Two grapple claw attacks are done; a third grapple attack finds no natural attack left (the bite was given up for the grapple)", Expect.All(
                    Expect.StepStatus("hero", 1, "GrappleAction(Attack", 0, "done"),
                    Expect.StepStatus("hero", 1, "GrappleAction(Attack", 1, "done"),
                    Expect.StepStatus("hero", 1, "GrappleAction(Attack", 2, "refused", "dropped")))
                .Expect("Two attacks in all, both at the claw's bonus (BAB +4 -5 secondary + STR +3 -4 grappling)", v =>
                {
                    List<TraceEvent> r1 = v.Attacks("hero", "dummy", false, 1);
                    int bab = ActorInt(v, "hero", "bab");
                    int[] m = r1.Select(e => e.Int("mod")).ToArray();
                    string mods = "mods [" + string.Join(",", m) + "], BAB " + bab;
                    return m.Length == 2 && m[0] == m[1] && m[0] == bab - 5 + 3 - 4
                        ? ExpectResult.Pass(mods, r1.Select(e => e.Seq).ToArray())
                        : ExpectResult.Fail(mods + ", expected two claws at BAB -5 +3 -4", r1.Select(e => e.Seq).ToArray());
                })
                .Build();
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
        /// Replaces the MM ogre's spawned greatclub with a plain one. The ogre spawns with its greatclub (its NPC data
        /// lists it under EquipSlot.MainHand, ITM-004), but at CR 3 the spawn may roll a masterwork one by seed
        /// (ItemMaterialFactory.GetRandomMaterialWeapon), and a scenario whose expected figures assume a plain weapon
        /// uses this so they do not depend on the seed.
        /// </summary>
        private static void EquipPlainGreatclub(CharacterController c)
        {
            InventoryComponent inv = c.GetComponent<InventoryComponent>();
            if (inv == null || inv.CharacterInventory == null)
                return;
            inv.CharacterInventory.RightHandSlot = null;
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
        /// Small goblin's morningstar 1d6 and the Large ogre's greatclub 2d8, both for the MM ogre, whose data lists the
        /// greatclub under MainHand (ITM-004), and for ogre_brute, whose data lists it under RightHand (CMB-119). Both
        /// spawn holding it.
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
                .Expect("The Large ogre's spawned greatclub (a MainHand entry, ITM-004) rolls 2d8 (MM p.199; DMG Table 2-2)", RollsDice("ogre", "2d8"))
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

        // ── Combat end (CORE-011, CORE-034, CORE-037, CORE-001) ─────────

        private static TraceEvent FirstCombatOver(TraceView v)
            => v.Of("phase").FirstOrDefault(e => e.Get("to") is GameManager.TurnPhase t && t == GameManager.TurnPhase.CombatOver);

        /// <summary>
        /// The last enemy still in the fight is a target dummy at 0 HP (disabled, so still in: PHB p.145); the other two
        /// are out from the start: a troll dying at -3 HP (it has regeneration, CORE-034) and an orc asleep at full HP
        /// (the Unconscious condition, CORE-037). The fighter's first hit drops the dummy below 0 and the game itself
        /// declares the victory from the attack that dropped it (CORE-011): inside the NPC attack path for an AI-run
        /// party member, through the PC menu check after the Attack button for a Ui hero.
        /// </summary>
        private static ScenarioDef CombatEndVictory(bool ui)
        {
            string key = ui ? "hero" : "fighter";
            ScenarioBuilder b = Rules(ui ? "rules/combat-end-victory-ui" : "rules/combat-end-victory-ai",
                    "The attack that drops the last enemy still in ends the fight; dying, regenerating and sleeping enemies are out (CORE-011, CORE-034, CORE-037)"
                    + (ui ? ", through the PC buttons" : ""))
                .Covers("CORE-011", "CORE-034", "CORE-037", "PHB p.145", "PC_NPC_PARITY")
                .MaxRounds(3)
                .Pc(key, ActorSource.Stats(() => Fighter(ui ? "Hero" : "Fighter", 4)), 10, 10, ui ? Control.Ui : Control.Scripted)
                .Npc("dummy", "target_dummy", 11, 10, Control.Idle)
                .Npc("troll", "troll", 16, 3, Control.Idle)
                .Npc("orc", "orc_warrior", 16, 15, Control.Idle)
                .Hp("dummy", 0)
                .Hp("troll", -3)
                .StartCondition("orc", CombatConditionType.Unconscious, -1)
                .Tweak(key, StripOffHand)
                .Initiative(key, "dummy", "troll", "orc");
            if (ui)
                b.Turn(key, 0, Step.Attack("dummy"), Step.EndTurn());
            else
                b.Turn(key, 0, Step.Attack("dummy"));

            b.Expect("The game declares the victory itself", Expect.Outcome(Outcome.Victory))
             .Expect("The fight goes on while the disabled dummy is at 0 HP (PHB p.145): no end before the fighter's first hit", v =>
                {
                    TraceEvent over = FirstCombatOver(v);
                    TraceEvent hit = v.Of("attack").FirstOrDefault(e => e.Str("attacker") == key && e.Bool("hit"));
                    if (over == null) return ExpectResult.Fail("no CombatOver");
                    if (hit == null) return ExpectResult.Fail("no hit by " + key, over.Seq);
                    return v.Before(hit.Seq, over.Seq)
                        ? ExpectResult.Pass("hit #" + hit.Seq + ", CombatOver #" + over.Seq)
                        : ExpectResult.Fail("CombatOver #" + over.Seq + " before the hit #" + hit.Seq, over.Seq);
                })
             .Expect("The victory is declared from the attack that dropped the dummy, not at a later turn boundary (CORE-011)", v =>
                {
                    TraceEvent over = FirstCombatOver(v);
                    if (over == null) return ExpectResult.Fail("no CombatOver");
                    var by = over.Get("by") as List<string>;
                    string text = by != null ? string.Join(" < ", by) : "";
                    if (text.IndexOf("NextInitiativeTurn", StringComparison.Ordinal) >= 0 || text.IndexOf("SingleNPCTurnFromInitiative", StringComparison.Ordinal) >= 0)
                        return ExpectResult.Fail("declared at the turn boundary: " + text, over.Seq);
                    if (text.IndexOf("EvaluateCombatEnd", StringComparison.Ordinal) < 0)
                        return ExpectResult.Fail("not through EvaluateCombatEnd: " + text, over.Seq);
                    // CallerFrames keeps 3 GameManager frames: HandleCombatVictoryDetected < EvaluateCombatEnd < the caller. On the
                    // NPC path that is the melee sequence itself; on the PC path the attack flow's own check
                    // (CombatFlowService -> Combat_CheckCombatVictory -> CheckCombatVictory), not the end-of-turn safety net
                    // (EndActivePCTurn -> EndAttackSequence) and not the menu after the attack (ShowActionChoices).
                    string expected = ui ? "CheckCombatVictory" : "PerformNPCMeleeAttackSequence";
                    return text.IndexOf(expected, StringComparison.Ordinal) >= 0
                        ? ExpectResult.Pass(text, over.Seq)
                        : ExpectResult.Fail("by " + text + ", expected " + expected, over.Seq);
                })
             .Expect("The victory comes before the attacker's turn ends (no end-turn step or turn_end by it first)", v =>
                {
                    TraceEvent over = FirstCombatOver(v);
                    if (over == null) return ExpectResult.Fail("no CombatOver");
                    TraceEvent endStep = v.Of("step").FirstOrDefault(e => e.Str("actor") == key && e.Str("kind") == "EndTurn");
                    TraceEvent turnEnd = v.Of("turn_end").FirstOrDefault(e => e.Str("actor") == key);
                    if (endStep != null && v.Before(endStep.Seq, over.Seq))
                        return ExpectResult.Fail("end-turn step #" + endStep.Seq + " before CombatOver #" + over.Seq, over.Seq);
                    if (turnEnd != null && v.Before(turnEnd.Seq, over.Seq))
                        return ExpectResult.Fail("turn_end #" + turnEnd.Seq + " before CombatOver #" + over.Seq, over.Seq);
                    return ExpectResult.Pass("CombatOver #" + over.Seq + " inside the turn", over.Seq);
                })
             .Expect("The troll is dying, not dead, and the orc still asleep at positive HP when the fight ends (both count as out)", v =>
                {
                    JsonObj troll = v.Final("troll");
                    JsonObj orc = v.Final("orc");
                    if (troll == null || orc == null) return ExpectResult.Fail("missing final snapshot");
                    bool trollOk = troll.Get("st") is HPState ts && ts != HPState.Dead && troll.Get("hp") is int th && th < 0;
                    bool orcOk = orc.Get("hp") is int oh && oh > 0 && HasCond(orc, "Unconscious");
                    string conds = string.Join(",", (orc.Get("conds") as List<string>) ?? new List<string>());
                    return trollOk && orcOk
                        ? ExpectResult.Pass("troll " + troll.Get("hp") + "/" + troll.Get("st") + ", orc " + orc.Get("hp") + " " + conds)
                        : ExpectResult.Fail("troll " + troll.Get("hp") + "/" + troll.Get("st") + ", orc " + orc.Get("hp") + " conds " + conds);
                });
            if (ui)
                b.Expect("The hero's turns are Ui turns", Expect.Controller(key, "ui"));
            return b.Build();
        }

        /// <summary>
        /// Defeat counts the whole party team (CORE-037): the hero at 1 HP and a Player-team goblin ally at 0 HP
        /// (disabled, still in: PHB p.145) away from it, both idle, against an AI orc berserker next to the hero. The
        /// first of them going down (the orc picks) does not end the fight while the other is still in; the defeat comes
        /// when the orc drops the second too, and the game opens its defeat screen (CORE-001), whose combat log line the
        /// trace shows.
        /// </summary>
        private static ScenarioDef CombatEndDefeatAlly()
        {
            return Rules("rules/combat-end-defeat-ally", "Defeat waits until every party-team member is out, a disabled ally included, then opens the defeat screen (CORE-037, CORE-001)")
                .Covers("CORE-037", "CORE-001", "CORE-011", "PHB p.145", "PC_NPC_PARITY")
                .MaxRounds(8)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 1)), 10, 10, Control.Idle)
                .Npc("ally", "goblin", 4, 10, Control.Idle, CharacterTeam.Player)
                .Npc("orc", "orc_berserker", 11, 10, Control.Ai)
                .Hp("hero", 1)
                .Hp("ally", 0)
                .Initiative("orc", "hero", "ally")
                .Expect("The game declares the defeat itself (a stalemate, the orc never dropping both, is inconclusive)", v =>
                {
                    if (v.Outcome == Outcome.Defeat) return ExpectResult.Pass("outcome Defeat");
                    if (v.Outcome == Outcome.Stalemate) return ExpectResult.Inconclusive("stalemate: the orc did not drop both in time");
                    return ExpectResult.Fail("outcome " + v.Outcome + " (" + v.OutcomeReason + ")");
                })
                .Expect("Both party-team members are out when the fight ends", v =>
                {
                    if (v.Outcome != Outcome.Defeat) return ExpectResult.Inconclusive("no defeat");
                    bool hero = ScenarioChecks.IsDownSnapshot(v.Final("hero"));
                    bool ally = ScenarioChecks.IsDownSnapshot(v.Final("ally"));
                    return hero && ally ? ExpectResult.Pass("hero and ally down") : ExpectResult.Fail("hero down " + hero + ", ally down " + ally);
                })
                .Expect("The first party-team member down does not end the fight: a turn starts with one of them down and the other still in", v =>
                {
                    if (v.Outcome != Outcome.Defeat) return ExpectResult.Inconclusive("no defeat");
                    foreach (TraceEvent ts in v.Of("turn_start"))
                    {
                        if (!(ts.Get("snap") is List<JsonObj> snaps)) continue;
                        JsonObj hero = snaps.FirstOrDefault(o => o.Get("k") as string == "hero");
                        JsonObj ally = snaps.FirstOrDefault(o => o.Get("k") as string == "ally");
                        if (hero == null || ally == null) continue;
                        bool heroDown = ScenarioChecks.IsDownSnapshot(hero);
                        bool allyDown = ScenarioChecks.IsDownSnapshot(ally);
                        if (heroDown != allyDown)
                            return ExpectResult.Pass("turn #" + ts.Seq + " (" + ts.Str("actor") + ") starts with " + (heroDown ? "the hero" : "the ally") + " down and the other in", ts.Seq);
                    }
                    return ExpectResult.Fail("no turn started with exactly one of them down");
                })
                .Expect("The defeat screen opened (its combat log line, CORE-001)", v =>
                {
                    if (v.Outcome != Outcome.Defeat) return ExpectResult.Inconclusive("no defeat");
                    return v.Log("DEFEAT! Every hero is dead, dying or unconscious").Count > 0
                        ? ExpectResult.Pass("defeat line logged")
                        : ExpectResult.Fail("no defeat log line");
                })
                .Build();
        }

        /// <summary>
        /// A petrified creature is considered unconscious (PHB glossary p.311, DMG condition summary p.301), so it is out
        /// of the fight at full HP (owner definition 2026-10-07). A hero and an orc, both idle; one of them starts
        /// petrified (the condition the cockatrice, the basilisk and medusa gazes and Flesh to Stone apply). The game
        /// ends the fight at the first check, before anyone attacks: victory when the orc is the petrified one, defeat
        /// with the defeat screen (CORE-001) when the hero is.
        /// </summary>
        private static ScenarioDef CombatEndPetrified(bool partyPetrified)
        {
            string stone = partyPetrified ? "hero" : "orc";
            ScenarioBuilder b = Rules(partyPetrified ? "rules/combat-end-petrified-defeat" : "rules/combat-end-petrified-victory",
                    "A side turned to stone is out of the fight: petrified counts as unconscious (PHB p.311, DMG p.301; CORE-037)")
                .Covers("CORE-037", "CORE-011", "PHB p.311", "DMG p.301", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 4)), 10, 10, Control.Idle)
                .Npc("orc", "orc_warrior", 14, 10, Control.Idle)
                .StartCondition(stone, CombatConditionType.Petrified, -1)
                .Initiative("hero", "orc")
                .Expect("The game declares the " + (partyPetrified ? "defeat" : "victory") + " itself",
                    Expect.Outcome(partyPetrified ? Outcome.Defeat : Outcome.Victory))
                .Expect("Nobody attacks: the fight ends at the first check", Expect.None("attack", null))
                .Expect("The " + stone + " is petrified, alive and at full HP when the fight ends", v =>
                {
                    JsonObj s = v.Final(stone);
                    if (s == null) return ExpectResult.Fail("no final snapshot");
                    bool ok = HasCond(s, "Petrified") && s.Get("hp") is int hp && hp > 0 && !(s.Get("st") is HPState st && st == HPState.Dead);
                    return ok ? ExpectResult.Pass(stone + " " + s.Get("hp") + "/" + s.Get("st") + " petrified")
                              : ExpectResult.Fail(stone + " " + s.Get("hp") + "/" + s.Get("st") + " conds " + string.Join(",", (s.Get("conds") as List<string>) ?? new List<string>()));
                });
            if (partyPetrified)
                b.Expect("The defeat screen opened (its combat log line, CORE-001)", v =>
                    v.Log("DEFEAT! Every hero is dead, dying or unconscious").Count > 0
                        ? ExpectResult.Pass("defeat line logged")
                        : ExpectResult.Fail("no defeat log line"));
            return b.Build();
        }

        // ── Encounter spawning (ENC-001) ────────────────────────────────

        /// <summary>The five squares the default layout gives the first five creatures without a preset square.</summary>
        private static readonly Vector2Int[] DefaultFormationSquares =
        {
            new Vector2Int(16, 6), new Vector2Int(14, 10), new Vector2Int(16, 14), new Vector2Int(13, 8), new Vector2Int(13, 12)
        };

        /// <summary>Four idle level-8 fighters on the left edge: they hold squares the spawn must avoid and outlast two rounds.</summary>
        private static ScenarioBuilder IdleGuards(ScenarioBuilder b)
            => b.Pc("guard1", ActorSource.Stats(() => Fighter("Guard A", 8)), 3, 7, Control.Idle)
                .Pc("guard2", ActorSource.Stats(() => Fighter("Guard B", 8)), 3, 9, Control.Idle)
                .Pc("guard3", ActorSource.Stats(() => Fighter("Guard C", 8)), 3, 11, Control.Idle)
                .Pc("guard4", ActorSource.Stats(() => Fighter("Guard D", 8)), 3, 13, Control.Idle);

        /// <summary>
        /// Twenty goblins placed by the game's own layout (GameManager.SetupEnemyEncounter through
        /// EncounterSpawnPlacement), as a random or DMG encounter spawns them. Before ENC-001 was fixed the sixth and later
        /// goblins spawned at (15 + i, 10), off the 20x20 grid, and could not move, and the 16th to 20th were dropped
        /// (15 pool slots). Now the pool grows to 20, the first five keep the old five squares, the rest spread out from
        /// the enemy side's middle, and every goblin takes its turns.
        /// </summary>
        private static ScenarioDef LargeEncounterGoblins()
        {
            ScenarioBuilder b = IdleGuards(Rules("rules/large-encounter-goblins",
                    "Twenty goblins all spawn on free squares of the grid and all act (ENC-001)")
                .Covers("ENC-001", "PHB p.149")
                .MaxRounds(2));
            var keys = new List<string>();
            for (int i = 1; i <= 20; i++)
            {
                string key = "g" + i;
                keys.Add(key);
                b.NpcPlacedByGame(key, "goblin");
            }
            AddSpawnExpectations(b, keys);
            b.Expect("The first five goblins take the default layout's five squares", v =>
                {
                    for (int i = 0; i < DefaultFormationSquares.Length; i++)
                    {
                        Vector2Int? p = ActorPos(v, keys[i]);
                        if (p != DefaultFormationSquares[i])
                            return ExpectResult.Fail(keys[i] + " at " + p + ", expected " + DefaultFormationSquares[i]);
                    }
                    return ExpectResult.Pass("g1-g5 on the five default squares");
                });
            return b.Build();
        }

        /// <summary>
        /// Seventeen creatures of three sizes placed by the game's layout: a goblin, a Huge giant constrictor snake (3x3,
        /// PHB p.149 Table 8-4), three Large ogres (2x2), ten goblins, an ogre and a second snake. The snake takes the
        /// second default square (14,10) and covers (14-16, 10-12), so the third ogre's default square (13,12), whose
        /// 2x2 footprint would overlap it, gives way to the nearest square that fits. Every footprint must lie on the
        /// grid on squares no other creature holds, and every creature must act.
        /// </summary>
        private static ScenarioDef LargeEncounterSizes()
        {
            ScenarioBuilder b = IdleGuards(Rules("rules/large-encounter-sizes",
                    "Seventeen Medium, Large and Huge creatures spawn with whole footprints on free squares and all act (ENC-001)")
                .Covers("ENC-001", "PHB p.149")
                .MaxRounds(2));
            var spawn = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("g1", "goblin"),
                new KeyValuePair<string, string>("snake1", "giant_constrictor_snake"),
                new KeyValuePair<string, string>("ogre1", "ogre"),
                new KeyValuePair<string, string>("ogre2", "ogre"),
                new KeyValuePair<string, string>("ogre3", "ogre"),
            };
            for (int i = 2; i <= 11; i++)
                spawn.Add(new KeyValuePair<string, string>("g" + i, "goblin"));
            spawn.Add(new KeyValuePair<string, string>("ogre4", "ogre"));
            spawn.Add(new KeyValuePair<string, string>("snake2", "giant_constrictor_snake"));

            var keys = new List<string>();
            foreach (KeyValuePair<string, string> kv in spawn)
            {
                keys.Add(kv.Key);
                b.NpcPlacedByGame(kv.Key, kv.Value);
            }
            AddSpawnExpectations(b, keys);
            b.Expect("The snake keeps the default square (14,10); the third ogre gives way from (13,12), which its footprint would share with the snake", v =>
                {
                    Vector2Int? snake = ActorPos(v, "snake1");
                    Vector2Int? ogre3 = ActorPos(v, "ogre3");
                    if (snake != new Vector2Int(14, 10))
                        return ExpectResult.Fail("snake1 at " + snake);
                    return ogre3.HasValue && ogre3 != new Vector2Int(13, 12)
                        ? ExpectResult.Pass("snake1 at (14,10), ogre3 at " + ogre3)
                        : ExpectResult.Fail("ogre3 at " + ogre3);
                });
            return b.Build();
        }

        /// <summary>
        /// The checks both large-encounter scenarios share: every creature spawned (its actor event exists), its whole
        /// footprint is on the 20x20 grid, no two actors' footprints share a square, every creature takes an AI turn in
        /// round 1, and every creature acts (moves, attacks, makes a maneuver or casts) within the two rounds.
        /// </summary>
        private static void AddSpawnExpectations(ScenarioBuilder b, List<string> keys)
        {
            b.Expect("All " + keys.Count + " creatures spawn (none dropped)", v =>
                {
                    List<string> missing = keys.Where(k => !v.Of("actor").Any(e => e.Str("key") == k)).ToList();
                    return missing.Count == 0
                        ? ExpectResult.Pass(keys.Count + " actor events")
                        : ExpectResult.Fail("missing " + string.Join(",", missing));
                })
             .Expect("Every footprint lies on the grid, and no two actors share a square", v =>
                {
                    var owner = new Dictionary<Vector2Int, string>();
                    foreach (TraceEvent a in v.Of("actor"))
                    {
                        string key = a.Str("key");
                        if (!(a.Get("pos") is Vector2Int p))
                            return ExpectResult.Fail(key + " has no position", a.Seq);
                        int w = FootprintWidth(a.Get("size"));
                        for (int dx = 0; dx < w; dx++)
                        {
                            for (int dy = 0; dy < w; dy++)
                            {
                                var sq = new Vector2Int(p.x + dx, p.y + dy);
                                if (sq.x < 0 || sq.y < 0 || sq.x >= ScenarioLimits.GridWidth || sq.y >= ScenarioLimits.GridHeight)
                                    return ExpectResult.Fail(key + " at " + p + " (" + w + "x" + w + ") leaves the grid at " + sq, a.Seq);
                                if (owner.TryGetValue(sq, out string other))
                                    return ExpectResult.Fail(key + " at " + p + " shares " + sq + " with " + other, a.Seq);
                                owner[sq] = key;
                            }
                        }
                    }
                    return ExpectResult.Pass(owner.Count + " squares held, no overlap");
                })
             .Expect("Every creature takes an AI turn in round 1", v =>
                {
                    List<string> none = keys.Where(k => !v.Of("turn_start").Any(e => e.Round == 1 && e.Str("actor") == k && e.Str("controller") == "ai")).ToList();
                    return none.Count == 0
                        ? ExpectResult.Pass(keys.Count + " AI turns in round 1")
                        : ExpectResult.Fail("no round-1 AI turn: " + string.Join(",", none));
                })
             .Expect("Every creature acts: it moves, attacks, makes a maneuver or casts within two rounds", v =>
                {
                    List<string> idle = keys.Where(k => !(v.Of("move").Any(e => e.Str("actor") == k)
                            || v.Of("attack").Any(e => e.Str("attacker") == k)
                            || v.Of("maneuver").Any(e => e.Str("by") == k)
                            || v.Of("cast").Any(e => e.Str("by") == k))).ToList();
                    return idle.Count == 0
                        ? ExpectResult.Pass("all " + keys.Count + " acted")
                        : ExpectResult.Fail("never acted: " + string.Join(",", idle));
                });
        }

        // ── Item deflection (ITM-001) ───────────────────────────────────

        /// <summary>
        /// Ring of Protection deflection (ITM-001). DMG p.232: the ring grants a deflection bonus to AC; PHB p.171: two
        /// bonuses of the same type do not stack, only the better one applies, while different types (luck and
        /// resistance) do; PHB p.278 Shield of Faith and Shield Other and PHB p.266 Protection from Evil grant deflection
        /// bonuses too, and the ward and Shield Other a resistance bonus on saves; DMG p.253 Cloak of Resistance; DMG
        /// p.267 Stone of Good Luck (luck bonus on saves). A Stats fighter (no shield) wears a Ring of Protection +2 from
        /// setup; an NE goblin attacks it once a round, so its attack event records the AC the attack path uses.
        /// Round 1: unequipping the ring lowers AC and touch AC by exactly 2; re-equipping and five recalculations keep
        /// it at +2 (before the fix every recalculation added the ring again and unequipping removed nothing); a second
        /// ring (+1) changes nothing. Round 2: with only the +1 ring, Shield of Faith (+2 at caster level 1) gives +2,
        /// not +3; when it ends the ring's +1 is still there and spell deflection is back to 0 (before the fix the
        /// spell's end subtracted its bonus twice and took the ring's with it); it is then cast again. Shield Other (+1)
        /// on top changes nothing (spell deflection 2, not 3), ending Shield of Faith under it leaves +1, and Shield of
        /// Faith is cast once more and Shield Other ended. Round 3: with Shield of Faith +2 and the +1 ring, Protection
        /// from Evil (+2 against the evil goblin) adds 0 AC; its resistance bonus adds +1 over a Cloak of Resistance +1,
        /// and a Stone of Good Luck adds +1 luck to Will without cutting the ward's part; with Shield of Faith ended the
        /// ward adds 1 over the ring. Spells are applied with GameManager.Harness_ApplySpellBuff (the shared effect step
        /// of a landed cast).
        /// </summary>
        private static ScenarioDef RingDeflection()
        {
            int bare = int.MinValue, bareTouch = int.MinValue; // AC and touch AC without deflection, measured in round 1 of each job
            return Rules("rules/ring-deflection", "Ring of Protection deflection counts once and does not stack with other deflection (ITM-001)")
                .Covers("ITM-001", "DMG p.232", "PHB p.171", "PHB p.266", "PHB p.278")
                .MaxRounds(3)
                .Pc("wearer", ActorSource.Stats(() => Fighter("Wearer", 3)), 5, 10, Control.Scripted)
                .Npc("goblin", "goblin", 6, 10, Control.Scripted)
                .Tweak("wearer", c => { StripOffHand(c); EquipProtectionRing(c, EquipSlot.LeftRing, 2); })
                .Tweak("goblin", c => c.Stats.CharacterAlignment = Alignment.NeutralEvil)
                .Initiative("wearer", "goblin")
                .Turn("wearer", 1, Step.Assert("a Ring of Protection +2 adds exactly +2 to AC and touch AC, however often stats are recalculated", ctx =>
                {
                    CharacterController w = ctx.Get("wearer");
                    global::Inventory inv = w.GetComponent<InventoryComponent>().CharacterInventory;
                    CharacterStats s = w.Stats;
                    int withRing = s.ArmorClass, touchWith = s.TouchArmorClass;
                    bool ok = AcCheck(ctx, "unequip the +2 ring", inv.Unequip(EquipSlot.LeftRing) ? 1 : 0, 1);
                    bare = s.ArmorClass;
                    bareTouch = s.TouchArmorClass;
                    ok &= AcCheck(ctx, "AC with the ring minus AC without", withRing - bare, 2);
                    ok &= AcCheck(ctx, "touch AC with the ring minus touch AC without", touchWith - bareTouch, 2);
                    EquipProtectionRing(w, EquipSlot.LeftRing, 2);
                    for (int i = 1; i <= 5; i++)
                    {
                        inv.RecalculateStats();
                        ok &= AcCheck(ctx, "AC after recalculation " + i, s.ArmorClass, bare + 2);
                    }
                    EquipProtectionRing(w, EquipSlot.RightRing, 1);
                    ok &= AcCheck(ctx, "rings +2 and +1: only the higher applies", s.ArmorClass, bare + 2);
                    ok &= AcCheck(ctx, "spell deflection stays 0", s.DeflectionBonus, 0);
                    ctx.Note("ring-deflection r1 expect-ac=" + (bare + 2));
                    return ok;
                }))
                .Turn("wearer", 2, Step.Assert("Shield of Faith and a ring: the higher deflection applies, and the ring survives the spell's end", ctx =>
                {
                    CharacterController w = ctx.Get("wearer");
                    global::Inventory inv = w.GetComponent<InventoryComponent>().CharacterInventory;
                    CharacterStats s = w.Stats;
                    bool ok = AcCheck(ctx, "unequip the +2 ring", inv.Unequip(EquipSlot.LeftRing) ? 1 : 0, 1);
                    ok &= AcCheck(ctx, "the +1 ring alone", s.ArmorClass, bare + 1);
                    ok &= AcCheck(ctx, "Shield of Faith applied", ApplySpell(ctx, w, DND35e.Identifiers.SpellNames.SHIELD_OF_FAITH) ? 1 : 0, 1);
                    ok &= AcCheck(ctx, "Shield of Faith +2 with the +1 ring", s.ArmorClass, bare + 2);
                    ok &= AcCheck(ctx, "touch AC, Shield of Faith +2 with the +1 ring", s.TouchArmorClass, bareTouch + 2);
                    w.StatusEffectManager.RemoveEffectsBySpellId(DND35e.Identifiers.SpellNames.SHIELD_OF_FAITH);
                    ok &= AcCheck(ctx, "Shield of Faith ended: the +1 ring remains", s.ArmorClass, bare + 1);
                    ok &= AcCheck(ctx, "Shield of Faith ended: spell deflection back to 0", s.DeflectionBonus, 0);
                    ok &= AcCheck(ctx, "Shield of Faith applied again", ApplySpell(ctx, w, DND35e.Identifiers.SpellNames.SHIELD_OF_FAITH) ? 1 : 0, 1);
                    ok &= AcCheck(ctx, "Shield of Faith +2 with the +1 ring, again", s.ArmorClass, bare + 2);
                    ok &= AcCheck(ctx, "Shield Other applied", ApplySpell(ctx, w, DND35e.Identifiers.SpellNames.SHIELD_OTHER) ? 1 : 0, 1);
                    ok &= AcCheck(ctx, "Shield of Faith +2 and Shield Other +1: spell deflection", s.DeflectionBonus, 2);
                    ok &= AcCheck(ctx, "Shield of Faith +2, Shield Other +1 and the +1 ring", s.ArmorClass, bare + 2);
                    w.StatusEffectManager.RemoveEffectsBySpellId(DND35e.Identifiers.SpellNames.SHIELD_OF_FAITH);
                    ok &= AcCheck(ctx, "Shield of Faith ended under Shield Other: spell deflection", s.DeflectionBonus, 1);
                    ok &= AcCheck(ctx, "Shield of Faith ended under Shield Other: AC", s.ArmorClass, bare + 1);
                    ok &= AcCheck(ctx, "Shield of Faith applied a third time", ApplySpell(ctx, w, DND35e.Identifiers.SpellNames.SHIELD_OF_FAITH) ? 1 : 0, 1);
                    w.StatusEffectManager.RemoveEffectsBySpellId(DND35e.Identifiers.SpellNames.SHIELD_OTHER);
                    ok &= AcCheck(ctx, "Shield Other ended: spell deflection", s.DeflectionBonus, 2);
                    ok &= AcCheck(ctx, "Shield Other ended: AC", s.ArmorClass, bare + 2);
                    ctx.Note("ring-deflection r2 expect-ac=" + (bare + 2));
                    return ok;
                }))
                .Turn("wearer", 3, Step.Assert("Protection from Evil adds only what other deflection and resistance bonuses do not already give", ctx =>
                {
                    CharacterController w = ctx.Get("wearer");
                    global::Inventory inv = w.GetComponent<InventoryComponent>().CharacterInventory;
                    CharacterStats s = w.Stats;
                    bool ok = AcCheck(ctx, "Shield of Faith +2 with the +1 ring", s.ArmorClass, bare + 2);
                    ok &= AcCheck(ctx, "Protection from Evil applied", ApplySpell(ctx, w, DND35e.Identifiers.SpellNames.PROTECTION_FROM_EVIL) ? 1 : 0, 1);
                    AlignmentProtectionBenefits ward = AlignmentProtectionRules.GetBenefitsAgainst(w, ctx.Get("goblin").Stats.CharacterAlignment);
                    ok &= AcCheck(ctx, "the ward's deflection against the goblin", ward.DeflectionAcBonus, 2);
                    ok &= AcCheck(ctx, "the ward's resistance bonus against the goblin", ward.ResistanceSaveBonus, 2);
                    ok &= AcCheck(ctx, "the ward raises AC over Shield of Faith +2 by", AlignmentProtectionRules.DeflectionAcIncrease(ward, s), 0);
                    int willBare = s.WillSave;
                    ok &= AcCheck(ctx, "the ward raises saves with no item resistance by", AlignmentProtectionRules.ResistanceSaveIncrease(ward, s), 2);
                    inv.DirectEquip(WondrousItemFactory.CreateCloakOfResistance(1), EquipSlot.Back);
                    ok &= AcCheck(ctx, "Cloak of Resistance +1 raises Will by", s.WillSave - willBare, 1);
                    ok &= AcCheck(ctx, "the ward raises saves over the +1 cloak by", AlignmentProtectionRules.ResistanceSaveIncrease(ward, s), 1);
                    inv.DirectEquip(WondrousItemFactory.CreateStoneOfGoodLuck(), EquipSlot.Slotless);
                    ok &= AcCheck(ctx, "cloak +1 and Stone of Good Luck (luck +1) raise Will by", s.WillSave - willBare, 2);
                    ok &= AcCheck(ctx, "the ward raises saves over cloak and luckstone by", AlignmentProtectionRules.ResistanceSaveIncrease(ward, s), 1);
                    w.StatusEffectManager.RemoveEffectsBySpellId(DND35e.Identifiers.SpellNames.SHIELD_OF_FAITH);
                    ok &= AcCheck(ctx, "Shield of Faith ended: the +1 ring remains", s.ArmorClass, bare + 1);
                    ok &= AcCheck(ctx, "the ward raises AC over the +1 ring by", AlignmentProtectionRules.DeflectionAcIncrease(ward, s), 1);
                    ctx.Note("ring-deflection r3 expect-ac=" + (bare + 2));
                    return ok;
                }))
                .Turn("goblin", 0, Step.Attack("wearer"))
                .Expect("Every deflection check holds (DMG p.232, PHB p.171)", Expect.AssertsPass())
                .Expect("Each round the goblin's attack meets the AC the wearer's check expects (ring, Shield of Faith, Protection from Evil)", v =>
                {
                    for (int round = 1; round <= 3; round++)
                    {
                        TraceEvent note = v.Of("note").FirstOrDefault(e => (e.Str("text") ?? "").StartsWith("ring-deflection r" + round + " expect-ac=", StringComparison.Ordinal));
                        if (note == null) return ExpectResult.Fail("no round-" + round + " expectation note");
                        int want = int.Parse(note.Str("text").Substring(note.Str("text").IndexOf('=') + 1));
                        List<TraceEvent> attacks = v.Attacks("goblin", "wearer", false, round);
                        if (attacks.Count == 0) return ExpectResult.Fail("no goblin attack in round " + round, note.Seq);
                        TraceEvent wrong = attacks.FirstOrDefault(e => e.Int("ac") != want);
                        if (wrong != null) return ExpectResult.Fail("round " + round + ": attack against AC " + wrong.Int("ac") + ", expected " + want, wrong.Seq);
                    }
                    return ExpectResult.Pass("3 rounds of attacks against the expected AC");
                })
                .Build();
        }

        /// <summary>Equips a new Ring of Protection +<paramref name="bonus"/> (RingFactory, DMG p.232) in <paramref name="slot"/>.</summary>
        private static void EquipProtectionRing(CharacterController c, EquipSlot slot, int bonus)
        {
            InventoryComponent inv = c.GetComponent<InventoryComponent>();
            if (inv == null || inv.CharacterInventory == null)
                return;
            inv.CharacterInventory.DirectEquip(RingFactory.CreateProtectionRing(bonus), slot);
        }

        /// <summary>Applies a clone of <paramref name="spellId"/> to <paramref name="target"/> as a landed self-cast (Harness_ApplySpellBuff).</summary>
        private static bool ApplySpell(ScenarioContext ctx, CharacterController target, string spellId)
        {
            SpellDatabase.Init();
            SpellData spell = SpellDatabase.GetSpell(spellId);
            if (spell == null || ctx.Gm == null)
                return false;
            ctx.Gm.Harness_ApplySpellBuff(target, target, spell.Clone());
            return target.StatusEffectManager != null && target.StatusEffectManager.HasEffect(spellId);
        }

        /// <summary>True when <paramref name="got"/> equals <paramref name="want"/>; otherwise notes the mismatch.</summary>
        private static bool AcCheck(ScenarioContext ctx, string what, int got, int want)
        {
            if (got == want)
                return true;
            ctx.Note("ring-deflection mismatch: " + what + ": got " + got + ", expected " + want);
            return false;
        }

        // ── NPC weapon and armor proficiency (CHR-072; DMG p.109, MM p.310) ──

        /// <summary>
        /// A goblin and an orc warrior, each a 1-HD humanoid whose level is a real warrior level (MM p.310), attack a
        /// sturdy dummy once with their weapons through the AI path. A warrior is proficient with all simple and
        /// martial weapons, armor and shields (DMG p.109), so neither attack carries the -4 non-proficiency penalty
        /// or the armor and shield check penalties: the trace modifiers are the MM's, goblin morningstar +2 (BAB +1,
        /// Small +1, MM p.133; leather armor and a light wooden shield) and the orc's +4 (BAB +1, STR +3). The MM orc
        /// (p.203) attacks with a falchion at +4 in studded leather; orc_warrior's greataxe and scale mail are database
        /// gear, a martial weapon with the same +4 at the same STR and BAB. Both spawn with the MM's BAB +1 (a warrior level,
        /// CRE-004, fixed 2026-10-08; before, the orc spawned at +0 and this check allowed for it). Both are fractional-CR creatures, so their gear is never upgraded to masterwork at spawn. The
        /// two stand side by side east of the dummy, so neither flanks it. Before the fix the goblin was at -3 and the
        /// orc at -5 (-4 for the weapon plus the light shield's or scale mail's check penalty).
        /// </summary>
        private static ScenarioDef NpcProficiency()
        {
            return Rules("rules/npc-proficiency", "Warrior-class goblin and orc attack at their MM bonuses, without non-proficiency penalties (DMG p.109, MM p.310, CHR-072)")
                .Covers("CHR-072", "DMG p.109", "MM p.310", "MM p.133", "MM p.203")
                .MaxRounds(1)
                .Pc("dummy", ActorSource.Stats(() => FighterOfRace("Dummy", "Human")), 10, 10, Control.Scripted)
                .Npc("goblin", "goblin", 11, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 11, 11, Control.Scripted)
                .Tweak("dummy", SturdyDummy)
                .Initiative("goblin", "orc", "dummy")
                .Turn("goblin", 1, Step.Attack("dummy"))
                .Turn("orc", 1, Step.Attack("dummy"))
                .Turn("dummy", 0, Step.Pass())
                .Expect("The goblin's attack step is done", Expect.StepStatus("goblin", 1, "Attack", 0, "done"))
                .Expect("The orc's attack step is done", Expect.StepStatus("orc", 1, "Attack", 0, "done"))
                .Expect("The goblin's morningstar attack is +2 (MM p.133)", v => SingleWeaponAttackMod(v, "goblin", "morningstar", 2, 1))
                .Expect("The orc's greataxe attack is +4 at the MM's BAB (MM p.203 falchion +4; the greataxe is database gear)", v => SingleWeaponAttackMod(v, "orc", "greataxe", 4, 1))
                .Build();
        }

        // ── Spawned alignment and hand-alias gear (CRE-002, ITM-004) ──

        /// <summary>
        /// Spawned NPCs keep their definition's alignment (CRE-002) and the weapons and shields their data lists under the
        /// MainHand, OffHand and Ranged aliases (ITM-004). Round 1: a Stats fighter (the warden, no shield) checks the
        /// spawns and takes Protection from Evil (PHB p.266: +2 deflection to AC against attacks by evil creatures); then
        /// the MM ogre (CE in its data; MM p.199 usually chaotic evil) attacks it with its greatclub, a MainHand entry, and
        /// the elf warrior (CG; MM p.101; RangedKiter AI) shoots it with its longbow, a Ranged entry, while its longsword
        /// is carried. The ogre's attack meets the warden's AC plus the ward's +2, the elf's the plain AC. The fixture
        /// also checks the paladin's longsword and heavy shield (MainHand, OffHand; LG, PHB p.43), the ettin's two
        /// morningstars (MainHand, OffHand; MM p.106), the monk's sling carried in the pack with its hands free (LN,
        /// PHB p.40), and the alignments the templates set: the fiendish wolf neutral evil (the wolf is always neutral,
        /// MM p.283; fiendish creatures always evil, MM p.108) and the wolf skeleton neutral evil (MM p.226). Before the
        /// fix every spawn was Alignment.None, the ogre fought with an unarmed strike and the elf had no weapon.
        /// </summary>
        private static ScenarioDef NpcSpawnAlignmentGear()
        {
            return Rules("rules/npc-spawn-alignment-gear", "Spawned NPCs keep their MM alignment and their MainHand, OffHand and Ranged gear; Protection from Evil sees the evil ogre (CRE-002, ITM-004)")
                .Covers("CRE-002", "ITM-004", "PHB p.266", "MM p.101", "MM p.106", "MM p.108", "MM p.199", "MM p.226", "MM p.283")
                .MaxRounds(1)
                .Pc("warden", ActorSource.Stats(() => Fighter("Warden", 3)), 10, 10, Control.Scripted)
                .Npc("ogre", "ogre", 11, 10, Control.Scripted)
                .Npc("elf", "elf_warrior", 3, 10, Control.Scripted)
                .Npc("paladin", "human_paladin_3", 16, 3, Control.Idle)
                .Npc("ettin", "ettin", 15, 15, Control.Idle)
                .Npc("monk", "human_monk_3", 17, 8, Control.Idle)
                .Npc("fwolf", "fiendish_wolf", 3, 3, Control.Idle)
                .Npc("swolf", "skeleton_wolf", 5, 3, Control.Idle)
                .Tweak("warden", c => { StripOffHand(c); SturdyDummy(c); })
                .Initiative("warden", "ogre", "elf", "paladin", "ettin", "monk", "fwolf", "swolf")
                .Turn("warden", 1, Step.Assert("spawned alignments and hand-alias gear match the NPC data; Protection from Evil is up", ctx =>
                {
                    bool ok = AlignmentIs(ctx, "ogre", Alignment.ChaoticEvil);
                    ok &= AlignmentIs(ctx, "elf", Alignment.ChaoticGood);
                    ok &= AlignmentIs(ctx, "paladin", Alignment.LawfulGood);
                    ok &= AlignmentIs(ctx, "ettin", Alignment.ChaoticEvil);
                    ok &= AlignmentIs(ctx, "monk", Alignment.LawfulNeutral);
                    ok &= AlignmentIs(ctx, "fwolf", Alignment.NeutralEvil);
                    ok &= AlignmentIs(ctx, "swolf", Alignment.NeutralEvil);

                    global::Inventory ogre = InvOf(ctx, "ogre"), elf = InvOf(ctx, "elf"), paladin = InvOf(ctx, "paladin"), ettin = InvOf(ctx, "ettin"), monk = InvOf(ctx, "monk");
                    ok &= SpawnCheck(ctx, "ogre holds its greatclub", IdHas(ogre.RightHandSlot, "greatclub"), IdOf(ogre.RightHandSlot));
                    ok &= SpawnCheck(ctx, "elf holds its longbow", IdHas(elf.RightHandSlot, "longbow"), IdOf(elf.RightHandSlot));
                    ok &= SpawnCheck(ctx, "elf carries its longsword", PackHolds(elf, "longsword"), "not in the pack");
                    ok &= SpawnCheck(ctx, "paladin holds its longsword", IdHas(paladin.RightHandSlot, "longsword"), IdOf(paladin.RightHandSlot));
                    ok &= SpawnCheck(ctx, "paladin holds its shield", paladin.LeftHandSlot != null && paladin.LeftHandSlot.IsShield, IdOf(paladin.LeftHandSlot));
                    ok &= SpawnCheck(ctx, "ettin holds two morningstars", IdHas(ettin.RightHandSlot, "morningstar") && IdHas(ettin.LeftHandSlot, "morningstar"),
                        IdOf(ettin.RightHandSlot) + "/" + IdOf(ettin.LeftHandSlot));
                    ok &= SpawnCheck(ctx, "monk's hands are free and its sling is carried", monk.RightHandSlot == null && monk.LeftHandSlot == null && PackHolds(monk, "sling"),
                        IdOf(monk.RightHandSlot));

                    CharacterController w = ctx.Get("warden");
                    ok &= SpawnCheck(ctx, "Protection from Evil applied", ApplySpell(ctx, w, DND35e.Identifiers.SpellNames.PROTECTION_FROM_EVIL), "not applied");
                    int evilIncrease = AlignmentProtectionRules.DeflectionAcIncrease(AlignmentProtectionRules.GetBenefitsAgainst(w, Alignment.ChaoticEvil), w.Stats);
                    ok &= SpawnCheck(ctx, "the ward adds +2 AC against a chaotic evil attacker", evilIncrease == 2, evilIncrease);
                    ctx.Note("npc-spawn expect-ac-evil=" + (w.Stats.ArmorClass + evilIncrease));
                    ctx.Note("npc-spawn expect-ac-good=" + w.Stats.ArmorClass);
                    return ok;
                }), Step.Pass())
                .Turn("ogre", 1, Step.Attack("warden"))
                .Turn("elf", 1, Step.Attack("warden"))
                .Expect("Every spawn check holds", Expect.AssertsPass())
                .Expect("The ogre attacks with its spawned greatclub against the warden's AC plus the ward's +2 (PHB p.266; MM p.199)",
                    v => SpawnAttackMeetsAc(v, "ogre", "greatclub", false, "npc-spawn expect-ac-evil="))
                .Expect("The elf shoots its spawned longbow against the warden's plain AC (a good attacker; MM p.101)",
                    v => SpawnAttackMeetsAc(v, "elf", "longbow", true, "npc-spawn expect-ac-good="))
                .Build();
        }

        private static global::Inventory InvOf(ScenarioContext ctx, string key) => ctx.Get(key).GetComponent<InventoryComponent>().CharacterInventory;

        private static bool IdHas(ItemData item, string fragment) => item != null && item.Id != null && item.Id.Contains(fragment);

        private static string IdOf(ItemData item) => item != null ? item.Id : "empty";

        private static bool PackHolds(global::Inventory inv, string fragment)
            => inv != null && inv.GeneralSlots != null && inv.GeneralSlots.Any(i => IdHas(i, fragment));

        private static bool AlignmentIs(ScenarioContext ctx, string key, Alignment expected)
        {
            Alignment got = ctx.Get(key).Stats.CharacterAlignment;
            return SpawnCheck(ctx, key + " is " + expected, got == expected, got);
        }

        /// <summary>True when <paramref name="ok"/>; otherwise notes the failed check and what was found.</summary>
        private static bool SpawnCheck(ScenarioContext ctx, string what, bool ok, object got)
        {
            if (!ok)
                ctx.Note("npc-spawn mismatch: " + what + ": got " + got);
            return ok;
        }

        /// <summary>
        /// Passes when <paramref name="key"/>'s round-1 attacks on the warden used <paramref name="weapon"/>, were ranged
        /// or melee as <paramref name="ranged"/> says, and met the AC the note starting with <paramref name="notePrefix"/>
        /// recorded.
        /// </summary>
        private static ExpectResult SpawnAttackMeetsAc(TraceView v, string key, string weapon, bool ranged, string notePrefix)
        {
            TraceEvent note = v.Of("note").FirstOrDefault(e => (e.Str("text") ?? "").StartsWith(notePrefix, StringComparison.Ordinal));
            if (note == null) return ExpectResult.Fail("no expectation note " + notePrefix);
            int want = int.Parse(note.Str("text").Substring(notePrefix.Length));
            List<TraceEvent> attacks = v.Attacks(key, "warden", false, 1);
            int[] seqs = attacks.Select(e => e.Seq).ToArray();
            if (attacks.Count == 0) return ExpectResult.Fail("no round-1 attack by " + key, note.Seq);
            TraceEvent wrongWeapon = attacks.FirstOrDefault(e => (e.Str("weapon") ?? "").IndexOf(weapon, StringComparison.OrdinalIgnoreCase) < 0);
            if (wrongWeapon != null) return ExpectResult.Fail(key + " attacked with " + wrongWeapon.Str("weapon"), wrongWeapon.Seq);
            TraceEvent wrongKind = attacks.FirstOrDefault(e => e.Bool("ranged") != ranged);
            if (wrongKind != null) return ExpectResult.Fail(key + "'s attack ranged=" + wrongKind.Bool("ranged"), wrongKind.Seq);
            TraceEvent wrongAc = attacks.FirstOrDefault(e => e.Int("ac") != want);
            if (wrongAc != null) return ExpectResult.Fail(key + "'s attack against AC " + wrongAc.Int("ac") + ", expected " + want, wrongAc.Seq);
            return ExpectResult.Pass(attacks.Count + " " + weapon + " attack(s) against AC " + want, seqs);
        }

        /// <summary>
        /// Passes when <paramref name="key"/> spawned with the MM's BAB <paramref name="mmBab"/> (CRE-004) and made one
        /// round-1 attack on the dummy with <paramref name="weapon"/> at the MM modifier <paramref name="mmMod"/>.
        /// </summary>
        private static ExpectResult SingleWeaponAttackMod(TraceView v, string key, string weapon, int mmMod, int mmBab)
        {
            int bab = ActorInt(v, key, "bab");
            if (bab == int.MinValue)
                return ExpectResult.Fail("no actor event for " + key);
            if (bab != mmBab)
                return ExpectResult.Fail(key + " spawned with BAB " + bab + ", MM " + mmBab + " (CRE-004)");
            int mod = mmMod;
            List<TraceEvent> attacks = v.Attacks(key, "dummy", false, 1);
            int[] seqs = attacks.Select(e => e.Seq).ToArray();
            if (attacks.Count != 1)
                return ExpectResult.Fail(attacks.Count + " attacks by " + key, seqs);
            string used = attacks[0].Str("weapon") ?? "";
            if (used.IndexOf(weapon, StringComparison.OrdinalIgnoreCase) < 0)
                return ExpectResult.Fail("weapon " + used, seqs);
            int got = attacks[0].Int("mod");
            return got == mod
                ? ExpectResult.Pass(used + ", mod " + got + ", BAB " + bab, seqs)
                : ExpectResult.Fail(used + ", mod " + got + ", expected " + mod + ", BAB " + bab, seqs);
        }

        // ── Class progression: hit points, BAB and Hit Dice (CHR-001, CHR-002, CHR-071) ──

        /// <summary>A Human bard built at 1st level (max d6 + CON 12) and raised by the real level-up path.</summary>
        private static CharacterStats LevelledBard(string name, int finalLevel)
        {
            var s = new CharacterStats(
                name: name, level: 1, characterClass: "Bard",
                str: 10, dex: 12, con: 12, wis: 10, intelligence: 10, cha: 14,
                bab: 0, armorBonus: 0, shieldBonus: 0,
                damageDice: 6, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 6, raceName: "Human");
            s.PendingLevelUps = finalLevel - 1;
            s.EnsureMulticlassDataInitialized();
            for (int i = 1; i < finalLevel; i++)
                s.ApplyPendingLevelUp("Bard");
            return s;
        }

        /// <summary>The note-recording comparison for the class-progression asserts.</summary>
        private static bool ProgressionCheck(ScenarioContext ctx, string what, int got, int want)
        {
            if (got == want)
                return true;
            ctx.Note("class-progression mismatch: " + what + ": got " + got + ", expected " + want);
            return false;
        }

        /// <summary>
        /// Every character with class levels uses the class definitions and the PHB hit point rule, and its Hit Dice follow
        /// its level-ups. Quick Start bard and ranger (level 3 through the real creation path): HP max die at 1st level and
        /// average after, CON once per die (bard 7 + 5 + 5 = 17, ranger 9 + 6 + 6 = 21), BAB +2 and +3 (CHR-001, CHR-002).
        /// Two Human bards built at 1st level and raised to 5 and 4 through ApplyPendingLevelUp: HD 5 and 4, BAB +3,
        /// every new die a d6 with CON +1 and a minimum of 1. A scripted Wizard 3 then casts Daze (4 HD or less) at both:
        /// the HD 5 bard is refused before the slot is spent, the HD 4 bard is a legal target (CHR-071). Templated undead
        /// count their template's HD, not their Level (owlbear skeleton 5; bugbear zombie 6, an XFail while the spawn
        /// applies the zombie template twice, CRE-001). NPC max HP counts CON once (CreatureHitPointChecks).
        /// </summary>
        private static ScenarioDef ClassProgressionHpBabHd()
        {
            return Rules("rules/class-progression", "Class HP, BAB and Hit Dice follow the class tables and every level-up (PHB p.9, p.22-23, p.59, p.309; CHR-001, CHR-002, CHR-071)")
                .Covers("CHR-001", "CHR-002", "CHR-071", "PHB p.9", "PHB p.22", "PHB p.23", "PHB p.59", "PHB p.309", "PHB p.217", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("bard", ActorSource.QuickStart("Bard"), 3, 4, Control.Idle)
                .Pc("ranger", ActorSource.QuickStart("Ranger"), 3, 6, Control.Idle)
                .Pc("hero5", ActorSource.Stats(() => LevelledBard("Bard Five", 5)), 12, 10, Control.Scripted)
                .Pc("hero4", ActorSource.Stats(() => LevelledBard("Bard Four", 4)), 12, 12, Control.Scripted)
                .Npc("mage", "neutral_mage_test", 10, 11, Control.Scripted)
                .Npc("skeleton", "skeleton_owlbear", 16, 4, Control.Idle)
                .Npc("zombie", "zombie_bugbear", 16, 16, Control.Idle)
                .Npc("ogre", "ogre", 6, 14, Control.Idle)
                .Npc("ghoul", "ghoul", 3, 16, Control.Idle)
                .Initiative("hero5", "hero4", "mage", "bard", "ranger", "skeleton", "zombie", "ogre", "ghoul")
                .Turn("hero5", 1, Step.Assert("Levelled bards: HD, BAB and hit points follow every level-up; undead count template HD", ctx =>
                {
                    bool ok = true;
                    foreach (var pair in new[] { new KeyValuePair<string, int>("hero5", 5), new KeyValuePair<string, int>("hero4", 4) })
                    {
                        CharacterStats s = ctx.Get(pair.Key).Stats;
                        int level = pair.Value;
                        ok &= ProgressionCheck(ctx, pair.Key + " level", s.Level, level);
                        ok &= ProgressionCheck(ctx, pair.Key + " GetHitDice", s.GetHitDice(), level);
                        ok &= ProgressionCheck(ctx, pair.Key + " TeamUtility.GetHitDice", TeamUtility.GetHitDice(ctx.Get(pair.Key)), level);
                        ok &= ProgressionCheck(ctx, pair.Key + " BAB (bard 3/4, PHB p.27)", s.BaseAttackBonus, level * 3 / 4);
                        ok &= ProgressionCheck(ctx, pair.Key + " level-up records", s.HitPointGainsByClassLevel.Count, level - 1);
                        int gained = 0;
                        foreach (ClassHitPointEntry e in s.HitPointGainsByClassLevel)
                        {
                            ok &= ProgressionCheck(ctx, pair.Key + " level-up hit die", e.HitDie, 6);
                            ok &= ProgressionCheck(ctx, pair.Key + " level-up gain = max(1, die + CON)", e.TotalGain, Mathf.Max(1, e.Roll + 1));
                            ok &= ProgressionCheck(ctx, pair.Key + " die in range", e.Roll >= 1 && e.Roll <= 6 ? 1 : 0, 1);
                            gained += e.TotalGain;
                        }
                        ok &= ProgressionCheck(ctx, pair.Key + " max HP = 7 at 1st level + gains", s.MaxHP, 7 + gained);
                    }
                    ok &= ProgressionCheck(ctx, "owlbear skeleton HD (MM p.226)", TeamUtility.GetHitDice(ctx.Get("skeleton")), 5);
                    ok &= CreatureHitPointChecks(ctx);
                    // Recorded for the CRE-001 XFail below: the spawn applies the zombie template a second time.
                    ctx.Note("class-progression zombie-hd=" + TeamUtility.GetHitDice(ctx.Get("zombie")));
                    return ok;
                }))
                .Turn("hero4", 1, Step.Pass())
                .Turn("mage", 1, Step.Cast(DND35e.Identifiers.SpellNames.DAZE, "hero5"), Step.Cast(DND35e.Identifiers.SpellNames.DAZE, "hero4"))
                .Expect("Every progression check holds", Expect.AssertsPass())
                .Expect("Quick Start bard 3: BAB +2, HP 17 (d6, CON +1 once per die; PHB p.23, p.27)", v =>
                    ActorIs(v, "bard", 3, 2, 17))
                .Expect("Quick Start ranger 3: BAB +3, HP 21 (d8, CON +1 once per die; PHB p.23, p.46-47)", v =>
                    ActorIs(v, "ranger", 3, 3, 21))
                .Expect("Daze on the HD 5 bard is refused (4 HD or less, PHB p.217); on the HD 4 bard it is cast", Expect.All(
                    Expect.StepStatus("mage", 1, "Cast", 0, "refused"),
                    Expect.StepStatus("mage", 1, "Cast", 1, "done")))
                .ExpectXFail("CRE-001", "The bugbear zombie has 6 HD, its 3 racial HD doubled once (MM p.267)", v =>
                {
                    const string prefix = "class-progression zombie-hd=";
                    TraceEvent note = v.Of("note").FirstOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
                    if (note == null) return ExpectResult.Inconclusive("no zombie HD note");
                    int hd = int.Parse(note.Str("text").Substring(prefix.Length));
                    return hd == 6 ? ExpectResult.Pass("6 HD", note.Seq) : ExpectResult.Fail(hd + " HD (the template applied again at spawn)", note.Seq);
                })
                .Build();
        }

        /// <summary>
        /// NPC max HP counts CON once (CHR-001, CRE-041): an MM total is final (ogre 4d8+11 = 29, MM p.199; Toughness is
        /// still added on top by TotalMaxHP, the rest of CRE-041), a CON 0 undead adds nothing (ghoul 2d12 = 13, MM p.119;
        /// it still spawns dead, CRE-044), and an orc warrior given barbarian 2 through CreatureClassEngine spawns with its
        /// definition total, max(1, die + CON) per barbarian die (the orc's Hit Die is exchanged for the class, MM p.290,
        /// CRE-004), without CON again per level.
        /// </summary>
        private static bool CreatureHitPointChecks(ScenarioContext ctx)
        {
            bool ok = ProgressionCheck(ctx, "ogre MaxHP = MM total 29", ctx.Get("ogre").Stats.MaxHP, 29);
            ok &= ProgressionCheck(ctx, "ghoul (CON 0) MaxHP = MM total 13", ctx.Get("ghoul").Stats.MaxHP, 13);

            NPCDefinition template = NPCDatabase.Get("orc_warrior");
            if (template == null)
            {
                ctx.Note("class-progression mismatch: orc_warrior missing");
                return false;
            }
            NPCDefinition def = template.Clone();
            int conMod = ClassProgression.HitPointConstitutionModifier(def.CON);
            CreatureClassEngine.ApplyClassToDefinition(def, ClassRegistry.GetClass("Barbarian"), 2);
            ok &= ProgressionCheck(ctx, "orc barbarian 2 definition HP (the orc's Hit Die exchanged, MM p.290)", def.BaseHitDieHP,
                CreatureClassEngine.CalculateClassHP(12, conMod, 2));

            var go = new GameObject("ClassProgressionScenario_orc_barbarian");
            CharacterController cc = go.AddComponent<CharacterController>();
            try
            {
                ctx.Gm.InitializeNPCFromDefinition(cc, def, new Vector2Int(-60, -60), null, null);
                ok &= ProgressionCheck(ctx, "orc barbarian 2 spawned MaxHP (CON once)", cc.Stats.MaxHP, def.BaseHitDieHP);
                ok &= ProgressionCheck(ctx, "orc barbarian 2 Hit Dice (MM p.290)", cc.Stats.GetHitDice(), 2);
                ok &= ProgressionCheck(ctx, "orc barbarian 2 BAB (PHB p.25)", cc.Stats.BaseAttackBonus, 2);
            }
            finally
            {
                if (ctx.Gm != null && ctx.Gm.Grid != null)
                    ctx.Gm.Grid.ClearCreatureOccupancy(cc);
                UnityEngine.Object.DestroyImmediate(go);
            }
            return ok;
        }

        // ── Creature BAB and base saves: racial HD plus class levels (CRE-004) ──

        /// <summary>
        /// A creature's BAB and base saves are its racial Hit Dice by its type's progressions plus its class levels by the
        /// class tables (MM p.290, PHB p.59, the same CharacterStats sum a PC uses; CRE-004). Database NPCs spawned by the
        /// game: the orc warrior's Hit Die is a warrior level (MM p.310: BAB +1, Fort +2) and it attacks the dummy at the
        /// MM's +4 (MM p.203; greataxe stand-in for the falchion, as in rules/npc-proficiency); the gnoll (2 humanoid HD,
        /// good Fort: BAB +1, Fort +3, MM p.130), bugbear (3 HD, good Ref: BAB +2, Ref +3, MM p.29) and ogre (4 giant HD
        /// behind its Warrior stand-in: BAB +3, Fort +4, MM p.199) use their racial HD; the human paladin 5 uses the
        /// paladin table (BAB +5, Fort +4, PHB p.43). In the dummy's turn, DMG-table-style class levels are applied with
        /// CreatureClassEngine and spawned through InitializeNPCFromDefinition off the grid: a goblin adept 1 replaces the
        /// goblin's Hit Die (MM p.290: BAB +0, Will +2, DMG p.108), a hobgoblin warrior 3 has 3 HD (BAB +3), and an ogre
        /// barbarian 4 with the MM's ability scores matches its MM p.199 stat block (8 HD, BAB +7, Fort +12, Ref +2,
        /// Will +2); a human given rogue 4 has the same BAB and base saves as a PC rogue 4 (PHB p.50). Before the fix
        /// every spawn used the type's progression over all HD: orc BAB +0 and Fort +0, gnoll Fort +0, bugbear Ref +1,
        /// paladin BAB +3 and Fort +1, ogre barbarian 4 BAB +6 and Fort +5.
        /// </summary>
        private static ScenarioDef CreatureProgression()
        {
            return Rules("rules/creature-progression", "Spawned creatures add racial-HD and class BAB and saves (MM p.290, p.310; PHB p.59; CRE-004)")
                .Covers("CRE-004", "MM p.290", "MM p.310", "MM p.199", "MM p.203", "MM p.130", "MM p.29", "MM p.117", "MM p.250", "MM p.63", "PHB p.59", "PHB p.43", "DMG p.108", "DMG p.97", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("dummy", ActorSource.Stats(() => FighterOfRace("Dummy", "Human")), 10, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Npc("gnoll", "gnoll", 16, 4, Control.Idle)
                .Npc("bugbear", "bugbear", 16, 7, Control.Idle)
                .Npc("ogre", "ogre", 4, 15, Control.Idle)
                .Npc("paladin", "human_paladin", 16, 16, Control.Idle)
                .Tweak("dummy", SturdyDummy)
                .Initiative("orc", "dummy", "gnoll", "bugbear", "ogre", "paladin")
                .Turn("orc", 1, Step.Attack("dummy"))
                .Turn("dummy", 1, Step.Assert("Class levels applied to MM creatures give the MM and class-table BAB and saves", CreatureProgressionChecks))
                .Expect("Every class-level check holds", Expect.AssertsPass())
                .Expect("Orc warrior: 1 HD, all warrior: BAB +1, base Fort +2, Ref +0, Will +0 (MM p.203, p.310)", v => ActorProgression(v, "orc", 1, 0, 1, 2, 0, 0))
                .Expect("The orc's greataxe attack is the MM's +4 at BAB +1 (MM p.203)", v => SingleWeaponAttackMod(v, "orc", "greataxe", 4, 1))
                .Expect("Gnoll: 2 racial HD, good Fort: BAB +1, base Fort +3, Ref +0, Will +0 (MM p.130)", v => ActorProgression(v, "gnoll", 2, 2, 1, 3, 0, 0))
                .Expect("Bugbear: 3 racial HD, good Ref: BAB +2, base Fort +1, Ref +3, Will +1 (MM p.29)", v => ActorProgression(v, "bugbear", 3, 3, 2, 1, 3, 1))
                .Expect("Ogre: 4 giant HD: BAB +3, base Fort +4, Ref +1, Will +1 (MM p.199)", v => ActorProgression(v, "ogre", 4, 4, 3, 4, 1, 1))
                .Expect("Human paladin 5: BAB +5, base Fort +4, Ref +1, Will +1 (PHB p.43)", v => ActorProgression(v, "paladin", 5, 0, 5, 4, 1, 1))
                .Build();
        }

        /// <summary>Passes when the traced actor has the Hit Dice, racial Hit Dice, BAB and base saves given.</summary>
        private static ExpectResult ActorProgression(TraceView v, string key, int hd, int racialHd, int bab, int fort, int reflex, int will)
        {
            TraceEvent a = v.Of("actor").FirstOrDefault(e => e.Str("key") == key);
            if (a == null) return ExpectResult.Fail("no actor event for " + key);
            string got = "HD " + a.Int("hd") + " (racial " + a.Int("racialHd") + "), BAB " + a.Int("bab")
                + ", base F/R/W " + a.Int("baseFort") + "/" + a.Int("baseRef") + "/" + a.Int("baseWill");
            return a.Int("hd") == hd && a.Int("racialHd") == racialHd && a.Int("bab") == bab
                   && a.Int("baseFort") == fort && a.Int("baseRef") == reflex && a.Int("baseWill") == will
                ? ExpectResult.Pass(got, a.Seq)
                : ExpectResult.Fail(got + "; expected HD " + hd + " (racial " + racialHd + "), BAB " + bab + ", base F/R/W " + fort + "/" + reflex + "/" + will, a.Seq);
        }

        /// <summary>Spawns <paramref name="def"/> off the grid through the game's spawn and checks HD, BAB and base and total saves.</summary>
        private static bool SpawnedProgressionCheck(ScenarioContext ctx, NPCDefinition def, string what, int hd, int bab,
            int fort, int reflex, int will, int? totalFort = null, int? totalRef = null, int? totalWill = null)
        {
            var go = new GameObject("CreatureProgressionScenario_" + what.Replace(' ', '_'));
            CharacterController cc = go.AddComponent<CharacterController>();
            try
            {
                ctx.Gm.InitializeNPCFromDefinition(cc, def, new Vector2Int(-60, -60), null, null);
                CharacterStats s = cc.Stats;
                bool ok = ProgressionCheck(ctx, what + " HD", s.GetHitDice(), hd);
                ok &= ProgressionCheck(ctx, what + " BAB", s.BaseAttackBonus, bab);
                ok &= ProgressionCheck(ctx, what + " base Fort", s.ClassFortSave, fort);
                ok &= ProgressionCheck(ctx, what + " base Ref", s.ClassRefSave, reflex);
                ok &= ProgressionCheck(ctx, what + " base Will", s.ClassWillSave, will);
                if (totalFort.HasValue)
                {
                    ok &= ProgressionCheck(ctx, what + " Fort", s.FortitudeSave, totalFort.Value);
                    ok &= ProgressionCheck(ctx, what + " Ref", s.ReflexSave, totalRef.Value);
                    ok &= ProgressionCheck(ctx, what + " Will", s.WillSave, totalWill.Value);
                }
                return ok;
            }
            finally
            {
                if (ctx.Gm != null && ctx.Gm.Grid != null)
                    ctx.Gm.Grid.ClearCreatureOccupancy(cc);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>A clone of <paramref name="npcId"/> given <paramref name="levels"/> levels of a class (the DMG-table spawn path).</summary>
        private static NPCDefinition WithClass(ScenarioContext ctx, string npcId, string className, int levels)
        {
            NPCDefinition template = NPCDatabase.Get(npcId);
            if (template == null)
            {
                ctx.Note("class-progression mismatch: " + npcId + " missing");
                return null;
            }
            NPCDefinition def = template.Clone();
            CreatureClassEngine.ApplyClassToDefinition(def, ClassRegistry.GetClass(className), levels);
            return def;
        }

        private static bool CreatureProgressionChecks(ScenarioContext ctx)
        {
            bool ok = true;
            NPCDefinition adept = WithClass(ctx, "goblin", "Adept", 1);
            ok &= adept != null && SpawnedProgressionCheck(ctx, adept, "goblin adept 1", 1, 0, 0, 0, 2, 1, 1, 1);

            NPCDefinition warrior = WithClass(ctx, "hobgoblin_warrior", "Warrior", 3);
            ok &= warrior != null && SpawnedProgressionCheck(ctx, warrior, "hobgoblin warrior 3", 3, 3, 3, 1, 1);

            NPCDefinition barbarian = WithClass(ctx, "ogre", "Barbarian", 4);
            if (barbarian != null)
            {
                // The MM ogre barbarian's ability scores (MM p.199), so its printed saves can be compared.
                barbarian.STR = 26; barbarian.DEX = 11; barbarian.CON = 18; barbarian.INT = 8; barbarian.WIS = 10; barbarian.CHA = 4;
            }
            ok &= barbarian != null && SpawnedProgressionCheck(ctx, barbarian, "ogre barbarian 4", 8, 7, 8, 2, 2, 12, 2, 2);

            NPCDefinition rogue = WithClass(ctx, "human_warrior", "Rogue", 4);
            ok &= rogue != null && SpawnedProgressionCheck(ctx, rogue, "human rogue 4 (PC rogue 4: BAB +3, +1/+4/+1)", 4, 3, 1, 4, 1);

            // The MM ghost and vampire samples are human fighter 5s under the template (MM p.117, p.250); the DMG row
            // "vampire, 5th-level human fighter" (DMG p.97, CR 7) names that sample and adds no levels.
            NPCDefinition vampire = NPCDatabase.Get("vampire");
            NPCDefinition ghost = NPCDatabase.Get("ghost");
            ok &= vampire != null && SpawnedProgressionCheck(ctx, vampire.Clone(), "vampire fighter 5 (MM p.250)", 5, 5, 4, 1, 1);
            ok &= ghost != null && SpawnedProgressionCheck(ctx, ghost.Clone(), "ghost fighter 5 (MM p.117)", 5, 5, 4, 1, 1);
            if (vampire != null)
            {
                NPCDefinition row = vampire.Clone();
                ok &= ProgressionCheck(ctx, "DMG row vampire, 5th-level fighter: levels added",
                    CreatureClassEngine.ApplyEncounterClassLevel(row, ClassRegistry.GetClass("Fighter"), 5), 0);
                ok &= SpawnedProgressionCheck(ctx, row, "DMG row vampire, 5th-level fighter", 5, 5, 4, 1, 1);
            }

            // Dire animals: good Will beside the animal's good Fort and Ref (MM p.63-65), on the spawn and on the
            // Lion's Shield summon's own stats builder.
            NPCDefinition wolf = NPCDatabase.Get("dire_wolf");
            ok &= wolf != null && SpawnedProgressionCheck(ctx, wolf.Clone(), "dire wolf (MM p.65)", 6, 4, 5, 5, 5, 8, 7, 6);
            NPCDefinition lion = NPCDatabase.Get("dire_lion");
            if (lion != null)
            {
                CharacterStats summon = LionsShieldBehavior.BuildSummonStats(lion.Clone());
                ok &= ProgressionCheck(ctx, "Lion's Shield dire lion BAB (MM p.63)", summon.BaseAttackBonus, 6);
                ok &= ProgressionCheck(ctx, "Lion's Shield dire lion base Will (good)", summon.ClassWillSave, 6);
                ok &= ProgressionCheck(ctx, "Lion's Shield dire lion Will (MM p.63)", summon.WillSave, 7);
            }

            ok &= ProgressionCheck(ctx, "goblin template still 1 HD", NPCDatabase.Get("goblin").HitDice, 1);
            ok &= ProgressionCheck(ctx, "ogre template still 4 HD", NPCDatabase.Get("ogre").HitDice, 4);
            return ok;
        }

        // ── Spell save DCs (SPL-001) ────────────────────────────────────

        private const string SpellDcNotePrefix = "spell-dc expect ";

        /// <summary>
        /// SPL-001: one save DC rule (SpellSaveDCRules) for every cast path. PHB p.177: DC = 10 + the spell's level for
        /// the casting class + INT for a wizard, CHA for a sorcerer or bard, WIS for a cleric; PHB p.100: Spell Focus +1
        /// for its school. Two real casts, whose combat log prints the DC ("... vs DC N"): the Quick Start wizard (Elara,
        /// INT 17; a Ui actor, so the PC pipeline, with Charm Person prepared in place of Enlarge Person) casts Charm Person
        /// (Sor/Wiz/Brd 1, Enchantment, Will negates, PHB p.209) at a goblin, and neutral_mage_test (Wizard 3, INT 16,
        /// WIS 14) casts Daze (Sor/Wiz/Brd 0, PHB p.217) at the Quick Start cleric through the NPC cast executor. The
        /// Quick Start sorcerer (Kael, CHA 17, WIS 13; given Spell Focus (Enchantment), since character creation never
        /// sets the school, CHR-021, and Charm Person in place of Mage Armor) and bard (Lyric, CHA 16, WIS 10) cannot cast
        /// on the NPC executor (SPL-123: SpellcastingComponent.CanCast ignores a spontaneous caster's known spells; an
        /// XFail below), and the Ui driver casts only from prepared slots, so the Assert step resolves their Charm Person,
        /// and the evil_acolyte_test cleric's (Cleric 3, WIS 16, INT 12, CHA 13) Hold Person (Clr 2, Sor/Wiz 3, Will
        /// negates), through SpellCaster.Cast, the resolver the PC and NPC single-target pipelines share. Before SPL-001
        /// SpellCaster.Cast used WIS for every non-wizard: Kael's DC was 13 and Lyric's 11; now 15 (10 + 1 + CHA 3 +
        /// Spell Focus 1) and 14. The Assert step also checks the handler DC helper (GameManager.GetSpellSaveDC, used by
        /// every GameManager spell handler): with the wizard's Hold Person as the pending cast and Heighten to 5th as the
        /// pending metamagic it gives 10 + 5 + INT (PHB p.95), and a spell that is not the pending cast keeps level 3.
        /// </summary>
        private static ScenarioDef SpellSaveDc()
        {
            return Rules("rules/spell-save-dc", "Spell save DCs use the casting class's key ability and Spell Focus on every cast path (PHB p.177, p.100; SPL-001)")
                .Covers("SPL-001", "SPL-123", "PHB p.177", "PHB p.100", "PHB p.209", "PHB p.217", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("sorc", ActorSource.QuickStart("Sorcerer"), 6, 10, Control.Scripted)
                .Pc("bard", ActorSource.QuickStart("Bard"), 6, 12, Control.Idle)
                .Pc("wizard", ActorSource.QuickStart("Wizard"), 5, 11, Control.Ui)
                .Pc("cleric", ActorSource.QuickStart("Cleric"), 4, 13, Control.Idle)
                .Npc("mage", "neutral_mage_test", 9, 11, Control.Scripted)
                .Npc("acolyte", "evil_acolyte_test", 9, 13, Control.Idle)
                .Npc("gob1", "goblin", 8, 10, Control.Idle)
                .Npc("gob2", "goblin", 8, 12, Control.Idle)
                .Tweak("sorc", c =>
                {
                    c.Stats.SpellFocusSchool = "Enchantment";
                    SpontaneousCastingData known = c.Spellcasting != null ? c.Spellcasting.SpontaneousData : null;
                    if (known != null && known.SpellsKnownByLevel[1] != null)
                    {
                        known.SpellsKnownByLevel[1].Remove(DND35e.Identifiers.SpellNames.MAGE_ARMOR);
                        known.LearnSpell(DND35e.Identifiers.SpellNames.CHARM_PERSON, 1);
                    }
                })
                .Tweak("wizard", c =>
                {
                    SpellcastingComponent sc = c.Spellcasting;
                    if (sc == null || sc.SpellSlots == null)
                        return;
                    foreach (SpellSlot slot in sc.SpellSlots)
                        if (slot != null && slot.PreparedSpell != null && slot.PreparedSpell.SpellId == DND35e.Identifiers.SpellNames.ENLARGE_PERSON)
                        {
                            slot.PreparedSpell = SpellDatabase.GetSpell(DND35e.Identifiers.SpellNames.CHARM_PERSON).Clone();
                            break;
                        }
                })
                .Initiative("sorc", "wizard", "mage", "bard", "cleric", "acolyte", "gob1", "gob2")
                .Turn("sorc", 1,
                    Step.Assert("Each class's DC uses its key ability and level (PHB p.177) and Spell Focus (PHB p.100)", SpellDcChecks),
                    Step.Cast(DND35e.Identifiers.SpellNames.CHARM_PERSON, "gob1"))
                .Turn("wizard", 1, Step.Cast(DND35e.Identifiers.SpellNames.CHARM_PERSON, "gob2"))
                .Turn("mage", 1, Step.Cast(DND35e.Identifiers.SpellNames.DAZE, "cleric"))
                .Expect("Every DC check holds", Expect.AssertsPass())
                .Expect("The PC wizard's Charm Person and the NPC wizard's Daze ran", Expect.All(
                    Expect.StepStatus("wizard", 1, "Cast", 0, "done"),
                    Expect.StepStatus("mage", 1, "Cast", 0, "done")))
                .ExpectXFail("SPL-123", "The Quick Start sorcerer casts a known spell through the NPC cast executor", Expect.StepStatus("sorc", 1, "Cast", 0, "done"))
                .Expect("Each logged save rolls against the caster's own DC: 10 + class level + INT for both wizards (PHB p.177)", v =>
                {
                    List<TraceEvent> notes = v.Of("note").Where(e => (e.Str("text") ?? "").StartsWith(SpellDcNotePrefix, StringComparison.Ordinal)).ToList();
                    if (notes.Count == 0)
                        return ExpectResult.Inconclusive("no expected DC was recorded");
                    var got = new List<string>();
                    foreach (TraceEvent note in notes)
                    {
                        string body = note.Str("text").Substring(SpellDcNotePrefix.Length);
                        int eq = body.LastIndexOf('=');
                        int bar = body.IndexOf('|');
                        string name = body.Substring(0, bar);
                        string spellName = body.Substring(bar + 1, eq - bar - 1);
                        int expected = int.Parse(body.Substring(eq + 1));
                        var r = new System.Text.RegularExpressions.Regex(System.Text.RegularExpressions.Regex.Escape(name + " casts " + spellName + "!") + @"[\s\S]*?vs DC (\d+)");
                        TraceEvent log = v.Of("log").FirstOrDefault(e => r.IsMatch(e.Str("text") ?? ""));
                        if (log == null)
                            return ExpectResult.Inconclusive("no " + spellName + " save logged for " + name);
                        int dc = int.Parse(r.Match(log.Str("text")).Groups[1].Value);
                        if (dc != expected)
                            return ExpectResult.Fail(name + " DC " + dc + ", expected " + expected, log.Seq, note.Seq);
                        got.Add(name + " " + dc);
                    }
                    return ExpectResult.Pass(string.Join(", ", got));
                })
                .Build();
        }

        /// <summary>
        /// SPL-002 and SPL-003: spell durations on both cast paths. The Quick Start cleric (Cleric 3, a Ui actor, so the PC
        /// pipeline; Silence prepared in its two non-domain 2nd-level slots) casts Silence (PHB p.279; Will negates) at a goblin
        /// in round 1 and again in round 2, while the first is still on: the Silence handler sets the target's flag and a
        /// tracked effect for 1 round/level (the code's long-standing duration, believed to be the SRD's but not verified; PHB
        /// p.279 prints 1 min./level, an owner question in SPL-003), and the recast keeps the longer duration (the same spell
        /// does not stack) with the flag still on. The goblin gets a -30 save tweak so its Will save fails on every seed.
        /// evil_acolyte_test (Cleric 3; its prepared slots spawn empty, since its Daze is not a cleric spell, so Guidance is put
        /// into an empty orison slot) casts Guidance (PHB p.238: 1 minute) at the goblin beside it through the NPC cast
        /// executor: a generic tracked effect. Before SPL-002 Guidance set only the legacy rounds field and its effect started
        /// at 0 rounds and ended at the first round tick; before SPL-003 nothing ever ended the Silence flag, and the first
        /// version of the fix let a recast clear the flag it had just set. A scripted fighter's Assert step checks both every
        /// round: Guidance has 10 - (round - 1) rounds left, and Silence is on, flag and tracked effect together, for its
        /// duration from the round-2 recast and off after. The Silence flag and handler run on the PC pipeline only (SPL-054).
        /// </summary>
        private static ScenarioDef SpellDurations()
        {
            var def = Rules("rules/spell-durations", "Spells last their PHB duration on the PC and NPC cast paths, and Silence ends (PHB p.176; SPL-002, SPL-003)")
                .Covers("SPL-002", "SPL-003", "PHB p.176", "PHB p.238", "PHB p.279", "PC_NPC_PARITY")
                .MaxRounds(SpellDurationMaxRounds)
                .Pc("cleric", ActorSource.QuickStart("Cleric"), 5, 10, Control.Ui)
                .Pc("fighter", ActorSource.QuickStart("Fighter"), 5, 12, Control.Scripted)
                .Npc("acolyte", "evil_acolyte_test", 12, 10, Control.Scripted)
                .Npc("gob2", "goblin", 13, 10, Control.Idle)
                .Npc("gob", "goblin", 14, 12, Control.Idle)
                .Tweak("cleric", c => PrepareTwice(c, 2, DND35e.Identifiers.SpellNames.SILENCE))
                .Tweak("acolyte", c => ReplacePrepared(c, null, DND35e.Identifiers.SpellNames.GUIDANCE))
                .Tweak("gob", c => c.Stats.MoraleSaveBonus = -30)
                .Initiative("cleric", "acolyte", "fighter", "gob2", "gob")
                .Turn("cleric", 1, Step.Cast(DND35e.Identifiers.SpellNames.SILENCE, "gob"))
                .Turn("acolyte", 1, Step.Cast(DND35e.Identifiers.SpellNames.GUIDANCE, "gob2"))
                .Turn("cleric", SpellDurationRecastRound, Step.Cast(DND35e.Identifiers.SpellNames.SILENCE, "gob"));
            for (int round = 1; round <= SpellDurationMaxRounds; round++)
            {
                int r = round;
                def = def.Turn("fighter", r, Step.Assert("Round " + r + ": Guidance and Silence have the rounds their durations give", ctx => SpellDurationCheck(ctx, r)));
            }

            return def
                .Expect("All three casts ran", Expect.All(
                    Expect.StepStatus("cleric", 1, "Cast", 0, "done"),
                    Expect.StepStatus("acolyte", 1, "Cast", 0, "done"),
                    Expect.StepStatus("cleric", SpellDurationRecastRound, "Cast", 0, "done")))
                .Expect("Every round's duration check holds (Guidance 1 minute through the NPC executor, PHB p.238; Silence 1 round/level through the PC pipeline, recast included)", Expect.AssertsPass())
                .Expect("A Silence recast while the first lasts keeps the flag on (SPL-003)", v =>
                {
                    TraceEvent recast = v.Of("note").FirstOrDefault(e => (e.Str("text") ?? "").StartsWith(SpellDurationNotePrefix + "silence recast", StringComparison.Ordinal));
                    return recast != null ? ExpectResult.Pass(recast.Str("text")) : ExpectResult.Fail("no note says the recast Silence is on");
                })
                .Expect("Silence ends after 1 round/level from the recast: the flag and the tracked effect both go (SPL-003)", v =>
                {
                    TraceEvent ended = v.Of("note").FirstOrDefault(e => (e.Str("text") ?? "").StartsWith(SpellDurationNotePrefix + "silence ended", StringComparison.Ordinal));
                    return ended != null ? ExpectResult.Pass(ended.Str("text")) : ExpectResult.Fail("no note says Silence ended");
                })
                .Build();
        }

        private const int SpellDurationMaxRounds = 5;
        private const int SpellDurationRecastRound = 2;
        private const string SpellDurationNotePrefix = "spell-duration ";

        /// <summary>Prepares <paramref name="spellId"/> in the first two non-domain slots of <paramref name="level"/>.</summary>
        private static void PrepareTwice(CharacterController c, int level, string spellId)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                return;
            int placed = 0;
            foreach (SpellSlot slot in sc.SpellSlots)
            {
                if (placed >= 2)
                    return;
                if (slot != null && slot.Level == level && !slot.IsDomainSlot)
                {
                    slot.PreparedSpell = SpellDatabase.GetSpell(spellId).Clone();
                    placed++;
                }
            }
        }

        /// <summary>Puts a clone of <paramref name="newId"/> into the first prepared slot that holds <paramref name="oldId"/> (null: the first empty slot of the spell's level).</summary>
        private static void ReplacePrepared(CharacterController c, string oldId, string newId)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                return;
            foreach (SpellSlot slot in sc.SpellSlots)
                if (slot != null && (oldId == null
                        ? slot.PreparedSpell == null && slot.Level == SpellDatabase.GetSpell(newId).SpellLevel
                        : slot.PreparedSpell != null && slot.PreparedSpell.SpellId == oldId))
                {
                    slot.PreparedSpell = SpellDatabase.GetSpell(newId).Clone();
                    return;
                }
        }

        /// <summary>The fighter's Assert step of <see cref="SpellDurations"/> in round <paramref name="round"/>.</summary>
        private static bool SpellDurationCheck(ScenarioContext ctx, int round)
        {
            bool ok = true;

            // Guidance through the NPC executor: cast in round 1, ticked at the start of every later round.
            StatusEffectManager gob2Effects = ctx.Get("gob2").StatusEffectManager;
            int guidanceLeft = gob2Effects != null && gob2Effects.HasEffect(DND35e.Identifiers.SpellNames.GUIDANCE)
                ? gob2Effects.GetRemainingRounds(DND35e.Identifiers.SpellNames.GUIDANCE) : 0;
            int guidanceExpected = SpellDurationRules.RoundsPerMinute - (round - 1);
            ctx.Note(SpellDurationNotePrefix + "round " + round + " guidance " + guidanceLeft + " (expected " + guidanceExpected + ")");
            if (guidanceLeft != guidanceExpected)
                ok = false;

            // Silence through the PC pipeline's handler: cast in round 1, recast in round 2 (the goblin's Will save always fails).
            CharacterController cleric = ctx.Get("cleric");
            CharacterController gob = ctx.Get("gob");
            SpellData silence = SpellDatabase.GetSpell(DND35e.Identifiers.SpellNames.SILENCE);
            int silenceRounds = SpellCastingHelper.CalculateDuration(silence, SpellDurationRules.CasterLevelFor(cleric, silence));
            bool flag = gob.Stats.SilenceActive;
            bool tracked = gob.StatusEffectManager != null && gob.StatusEffectManager.HasEffect(DND35e.Identifiers.SpellNames.SILENCE);
            int trackedLeft = tracked ? gob.StatusEffectManager.GetRemainingRounds(DND35e.Identifiers.SpellNames.SILENCE) : 0;

            int lastCastRound = round >= SpellDurationRecastRound ? SpellDurationRecastRound : 1;
            bool expectedOn = round - lastCastRound < silenceRounds;
            ctx.Note(SpellDurationNotePrefix + "round " + round + " silence flag " + flag + " tracked " + tracked + " rounds left "
                + trackedLeft + " / flag " + gob.Stats.SilenceRoundsRemaining + " (duration " + silenceRounds + ", expected " + (expectedOn ? "on" : "off") + ")");
            if (flag != expectedOn || tracked != expectedOn)
                ok = false;

            if (round == SpellDurationRecastRound)
            {
                bool fresh = flag && tracked && trackedLeft == silenceRounds && gob.Stats.SilenceRoundsRemaining == silenceRounds;
                if (fresh)
                    ctx.Note(SpellDurationNotePrefix + "silence recast in round " + round + ": flag and tracked effect on, " + silenceRounds + " rounds");
                else
                    ok = false;
            }

            if (!expectedOn && !flag && !tracked)
                ctx.Note(SpellDurationNotePrefix + "silence ended by round " + round + ", " + silenceRounds + " rounds after the recast");
            return ok;
        }

        /// <summary>
        /// The Assert step of <see cref="SpellSaveDc"/>: the key ability SpellSaveDCRules picks for each live caster; the
        /// sorcerer's, bard's and acolyte's DCs through SpellCaster.Cast; and a note with each real caster's expected DC
        /// ("name|spell=DC") for the log expectation.
        /// </summary>
        private static bool SpellDcChecks(ScenarioContext ctx)
        {
            SpellData daze = SpellDatabase.GetSpell(DND35e.Identifiers.SpellNames.DAZE);
            SpellData charm = SpellDatabase.GetSpell(DND35e.Identifiers.SpellNames.CHARM_PERSON);
            SpellData hold = SpellDatabase.GetSpell(DND35e.Identifiers.SpellNames.HOLD_PERSON);
            if (daze == null || charm == null || hold == null)
            {
                ctx.Note("spell-dc mismatch: Daze, Charm Person or Hold Person missing");
                return false;
            }

            bool ok = true;
            // Key ability and class spell level: arcane casters with Daze (Sor/Wiz/Brd 0), divine casters with
            // Hold Person at its cleric level (PHB p.177: "the spell level applicable to your class").
            var keyAbility = new[]
            {
                ("sorc", daze, "CHA", "Sorcerer"), ("bard", daze, "CHA", "Bard"), ("wizard", daze, "INT", "Wizard"),
                ("cleric", hold, "WIS", "Cleric"), ("mage", daze, "INT", "Wizard"), ("acolyte", hold, "WIS", "Cleric"),
            };
            foreach (var row in keyAbility)
            {
                SpellSaveDCBreakdown dc = SpellSaveDCRules.Explain(ctx.Get(row.Item1), row.Item2);
                int level = row.Item2.GetSpellLevelFor(row.Item4);
                if (dc.Ability != row.Item3 || dc.SpellLevel != level)
                {
                    ctx.Note("spell-dc mismatch: " + row.Item1 + " " + row.Item2.Name + " key ability " + dc.Ability + " level " + dc.SpellLevel
                        + ", expected " + row.Item3 + " level " + level);
                    ok = false;
                }
            }

            // Through the shared single-target resolver: caster, spell, target, key ability modifier, casting class.
            var resolved = new[]
            {
                ("sorc", charm, "gob1", ctx.Get("sorc").Stats.CHAMod, "Sorcerer"),
                ("bard", charm, "gob2", ctx.Get("bard").Stats.CHAMod, "Bard"),
                ("acolyte", hold, "bard", ctx.Get("acolyte").Stats.WISMod, "Cleric"),
            };
            foreach (var row in resolved)
            {
                CharacterController caster = ctx.Get(row.Item1);
                CharacterController target = ctx.Get(row.Item3);
                CharacterStats s = caster.Stats;
                int focus = FeatManager.GetSpellFocusDCBonus(s, row.Item2.School);
                int expected = 10 + row.Item2.GetSpellLevelFor(row.Item5) + row.Item4 + focus;
                SpellResult result = SpellCaster.Cast(row.Item2.Clone(), s, target.Stats, null, false, false, caster, target);
                int got = result != null && result.RequiredSave ? result.SaveDC : -1;
                ctx.Note("spell-dc " + row.Item1 + " " + s.CharacterName + " " + row.Item2.Name + " DC " + got + " (expected " + expected
                    + "; the pre-SPL-001 WIS formula gives " + (10 + row.Item2.SpellLevel + s.WISMod + focus) + ")");
                if (got != expected)
                    ok = false;
            }

            ok &= HandlerHeightenCheck(ctx, hold);

            // The real casts: notes the log expectation reads.
            var real = new[] { ("wizard", charm), ("mage", daze) };
            foreach (var row in real)
            {
                CharacterStats s = ctx.Get(row.Item1).Stats;
                int expected = 10 + row.Item2.SpellLevel + s.INTMod + FeatManager.GetSpellFocusDCBonus(s, row.Item2.School);
                int rule = SpellSaveDCRules.Compute(ctx.Get(row.Item1), row.Item2);
                if (rule != expected)
                {
                    ctx.Note("spell-dc mismatch: " + row.Item1 + " SpellSaveDCRules " + rule + ", expected " + expected);
                    ok = false;
                }
                ctx.Note(SpellDcNotePrefix + s.CharacterName + "|" + row.Item2.Name + "=" + expected);
            }
            return ok;
        }

        /// <summary>
        /// The handler DC helper (private GameManager.GetSpellSaveDC, called by every GameManager spell handler) adds the
        /// pending cast's Heighten (PHB p.95) for the pending spell only. Sets and restores _pendingSpell and
        /// _pendingMetamagic by reflection.
        /// </summary>
        private static bool HandlerHeightenCheck(ScenarioContext ctx, SpellData hold)
        {
            GameManager gm = GameManager.Instance;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            System.Reflection.FieldInfo spellField = typeof(GameManager).GetField("_pendingSpell", flags);
            System.Reflection.FieldInfo mmField = typeof(GameManager).GetField("_pendingMetamagic", flags);
            System.Reflection.MethodInfo helper = typeof(GameManager).GetMethod("GetSpellSaveDC", flags, null,
                new[] { typeof(CharacterController), typeof(SpellData) }, null);
            if (gm == null || spellField == null || mmField == null || helper == null)
            {
                ctx.Note("spell-dc mismatch: GameManager, _pendingSpell, _pendingMetamagic or GetSpellSaveDC not found");
                return false;
            }

            CharacterController wizard = ctx.Get("wizard");
            CharacterStats s = wizard.Stats;
            int focus = FeatManager.GetSpellFocusDCBonus(s, hold.School);
            SpellData pending = hold.Clone();
            SpellData other = hold.Clone();
            var heighten = new MetamagicData();
            heighten.Toggle(MetamagicFeatId.HeightenSpell);
            heighten.HeightenToLevel = 5;

            object oldSpell = spellField.GetValue(gm);
            object oldMm = mmField.GetValue(gm);
            int heightened, plain;
            try
            {
                spellField.SetValue(gm, pending);
                mmField.SetValue(gm, heighten);
                heightened = (int)helper.Invoke(gm, new object[] { wizard, pending });
                plain = (int)helper.Invoke(gm, new object[] { wizard, other });
            }
            finally
            {
                spellField.SetValue(gm, oldSpell);
                mmField.SetValue(gm, oldMm);
            }

            int expectedHeightened = 10 + 5 + s.INTMod + focus;
            int expectedPlain = 10 + hold.GetSpellLevelFor("Wizard") + s.INTMod + focus;
            ctx.Note("spell-dc handler helper: pending Hold Person heightened to 5th DC " + heightened + " (expected " + expectedHeightened
                + "), another Hold Person DC " + plain + " (expected " + expectedPlain + ")");
            return heightened == expectedHeightened && plain == expectedPlain;
        }

        // ── Damage mitigation of spells, breath and special attacks (SPL-004) ──

        private const string DamageNotePrefix = "spl004 ";
        private static int _breathHeroHpBefore;
        private static int _breathHero2HpBefore;

        /// <summary>
        /// SPL-004: every spell, breath weapon and special attack deals its damage through
        /// CharacterStats.ApplyIncomingDamage with its type (GameManager.DealDamage), so immunity and resistance apply
        /// (MM p.314) and damage reduction does not reduce energy damage, spells or supernatural abilities (MM p.307);
        /// the shared concentration and death checks follow. On the NPC side a red dragon wyrmling (breath 2d10 fire,
        /// 20-ft cone, a supernatural ability) breathes on two Stats-built fighters through the AI executor
        /// (GameManager.NPCExecuteBreathWeaponForAI) in its scripted turn: the one with fire resistance 10 takes 10 of
        /// 20, the other 20 (the breath dice are forced to 10, both fighters get -30 on saves). Then a scripted fighter's
        /// Assert step runs the PC-side area handlers on target dummies with every die at its maximum (a local roll
        /// filter) and saves made to fail (-30): Fireball (TryResolveScaledAoEDamageSpell, PHB p.231) cast by
        /// arcane_missile_adept (Wizard 5, so 5d6 = 30 fire) on a plain dummy (30), one with fire resistance 10 (20),
        /// one immune to fire (0), one with DR 10/magic (30: a spell ignores DR) and one at 5 HP (killed, and its death
        /// handled); Ice Storm (TryResolveIceStormAoE, PHB p.243: 3d6 bludgeoning + 2d6 cold = 18 + 12) on a dummy immune
        /// to cold (only the 18 bludgeoning) and one with DR 10/magic (30); a gibbering mouther's acid spittle
        /// (AIService.TryExecuteRangedSpecialAttack, MM p.126: 1d4 acid, an extraordinary attack) on a dummy with acid
        /// resistance 2 (2 of 4); Searing Light on that dummy, where SpellCaster.Cast deals nothing and the handler alone deals its damage (SPL-124); and a Warm Fire Shield's retribution (FireShieldReactionEffect, PHB p.230: 1d6 + CL 5 =
        /// 11 fire) on the fire-immune dummy (0) and the fire-resistant one (1). Before SPL-004 each of these but the
        /// breath (which had its own first-match resistance code) dealt its full rolled damage.
        /// </summary>
        private static ScenarioDef SpellDamageMitigation()
        {
            return Rules("rules/spell-damage-mitigation", "Spell, breath and special-attack damage takes immunity and resistance, not DR (MM p.307, p.314; SPL-004)")
                .Covers("SPL-004", "SPL-124", "AI-006", "AI-040", "CMB-013", "MM p.307", "MM p.314", "PHB p.230", "PHB p.231", "PHB p.243", "PHB p.275", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("dragon", "dragon_red_wyrmling", 10, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => Fighter("Hero", 6)), 11, 10, Control.Idle)
                .Pc("hero2", ActorSource.Stats(() => Fighter("Second", 6)), 12, 10, Control.Scripted)
                .Npc("mage", "arcane_missile_adept", 2, 2, Control.Idle)
                .Npc("mouther", "gibbering_mouther", 15, 7, Control.Idle)
                .Npc("dPlain", "target_dummy", 13, 3, Control.Idle)
                .Npc("dResist", "target_dummy", 14, 3, Control.Idle)
                .Npc("dImmune", "target_dummy", 15, 3, Control.Idle)
                .Npc("dDr", "target_dummy", 16, 3, Control.Idle)
                .Npc("dLow", "target_dummy", 17, 3, Control.Idle)
                .Npc("dCold", "target_dummy", 13, 5, Control.Idle)
                .Npc("dIceDr", "target_dummy", 14, 5, Control.Idle)
                .Npc("dAcid", "target_dummy", 15, 5, Control.Idle)
                .Hp("dLow", 5)
                .Tweak("hero", c => { c.Stats.AddDamageResistance(DamageType.Fire, 10); c.Stats.MoraleSaveBonus = -30; })
                .Tweak("hero2", c => c.Stats.MoraleSaveBonus = -30)
                .Tweak("dPlain", c => c.Stats.MoraleSaveBonus = -30)
                .Tweak("dResist", c => { c.Stats.AddDamageResistance(DamageType.Fire, 10); c.Stats.MoraleSaveBonus = -30; })
                .Tweak("dImmune", c => { c.Stats.AddDamageImmunity(DamageType.Fire); c.Stats.MoraleSaveBonus = -30; })
                .Tweak("dDr", c => { c.Stats.AddDamageReduction(10, DamageBypassTag.Magic); c.Stats.MoraleSaveBonus = -30; })
                .Tweak("dLow", c => c.Stats.MoraleSaveBonus = -30)
                .Tweak("dCold", c => { c.Stats.AddDamageImmunity(DamageType.Cold); c.Stats.MoraleSaveBonus = -30; })
                .Tweak("dIceDr", c => { c.Stats.AddDamageReduction(10, DamageBypassTag.Magic); c.Stats.MoraleSaveBonus = -30; })
                .Tweak("dAcid", c => { c.Stats.AddDamageResistance(DamageType.Acid, 2); c.Stats.MoraleSaveBonus = -30; })
                .Initiative("dragon", "hero2", "hero", "mage", "mouther", "dPlain", "dResist", "dImmune", "dDr", "dLow", "dCold", "dIceDr", "dAcid")
                .Force(10, 10, "Breath weapon damage", -1)
                .Script("dragon", BreathAtHeroes)
                .Turn("hero2", 1,
                    Step.Assert("The breath weapon's fire takes the fighter's fire resistance (MM p.314)", BreathMitigationCheck),
                    Step.Assert("Spell and special-attack damage takes immunity and resistance and ignores DR (MM p.307)", SpellDamageMitigationChecks))
                .Expect("The dragon breathed", v =>
                {
                    TraceEvent n = v.Of("note").FirstOrDefault(e => (e.Str("text") ?? "") == DamageNotePrefix + "breath");
                    return n != null ? ExpectResult.Pass("breath used", n.Seq) : ExpectResult.Fail("no breath note");
                })
                .Expect("Every damage check holds", Expect.AssertsPass())
                .Build();
        }

        /// <summary>Dragon script: breathes once at the heroes (the AI's breath executor), recording their HP first.</summary>
        private static System.Collections.IEnumerator BreathAtHeroes(ScenarioContext ctx, CharacterController dragon)
        {
            CharacterController hero = ctx.Get("hero");
            CharacterController hero2 = ctx.Get("hero2");
            if (dragon == null || hero == null || hero2 == null || !dragon.HasBreathWeapon || !dragon.IsBreathWeaponReady)
            {
                ctx.Note(DamageNotePrefix + "breath not ready");
                yield break;
            }

            _breathHeroHpBefore = hero.Stats.CurrentHP;
            _breathHero2HpBefore = hero2.Stats.CurrentHP;
            ctx.Note(DamageNotePrefix + "breath");
            yield return ctx.Gm.StartCoroutine(ctx.Gm.NPCExecuteBreathWeaponForAI(dragon, hero));
        }

        /// <summary>The fire-resistant fighter took 20 - 10, the other the full 20 (2d10 forced to 10 each, failed saves).</summary>
        private static bool BreathMitigationCheck(ScenarioContext ctx)
        {
            int heroTaken = _breathHeroHpBefore - ctx.Get("hero").Stats.CurrentHP;
            int hero2Taken = _breathHero2HpBefore - ctx.Get("hero2").Stats.CurrentHP;
            ctx.Note(DamageNotePrefix + "breath: resistant fighter took " + heroTaken + " (expected 10), other fighter " + hero2Taken + " (expected 20)");
            return heroTaken == 10 && hero2Taken == 20;
        }

        /// <summary>The Assert step of <see cref="SpellDamageMitigation"/>: the PC-side area handlers, spittle and Fire Shield.</summary>
        private static bool SpellDamageMitigationChecks(ScenarioContext ctx)
        {
            GameManager gm = ctx.Gm;
            CharacterController mage = ctx.Get("mage");
            Func<int, string, int, int> previous = ScenarioHooks.RollFilter;
            // Every die at its maximum while the checks run; saves still fail through the -30 tweaks.
            ScenarioHooks.RollFilter = (sides, c, natural) => sides;
            bool ok = true;
            try
            {
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                // Fireball: 1d6 per caster level, max 10d6 (PHB p.231).
                SpellData fireball = SpellDatabase.GetSpell(DND35e.Identifiers.SpellNames.FIREBALL).Clone();
                int fireballRaw = 6 * Mathf.Clamp(SpellCastingHelper.GetEffectiveCasterLevel(mage, fireball), 1, 10);
                var fireballTargets = new[] { "dPlain", "dResist", "dImmune", "dDr", "dLow" };
                Dictionary<string, int> before = HpOf(ctx, fireballTargets);
                object[] args = { mage, fireball, fireballTargets.Select(ctx.Get).ToList(), null, null };
                System.Reflection.MethodInfo scaled = typeof(GameManager).GetMethod("TryResolveScaledAoEDamageSpell", flags);
                bool handled = scaled != null && (bool)scaled.Invoke(gm, args);
                ok &= Taken(ctx, "Fireball", "dPlain", before, fireballRaw);
                ok &= Taken(ctx, "Fireball", "dResist", before, fireballRaw - 10);
                ok &= Taken(ctx, "Fireball", "dImmune", before, 0);
                ok &= Taken(ctx, "Fireball", "dDr", before, fireballRaw);
                CharacterController low = ctx.Get("dLow");
                bool lowDead = low.Stats.IsDead && low.IsDead;
                ctx.Note(DamageNotePrefix + "Fireball handled " + handled + ", rolled " + fireballRaw + "; dLow dead " + lowDead + " (HP " + low.Stats.CurrentHP + ")");
                ok &= handled && lowDead;

                // Ice Storm: 3d6 bludgeoning + 2d6 cold, no save (PHB p.243); each part mitigated by its own type.
                SpellData iceStorm = SpellDatabase.GetSpell(DND35e.Identifiers.SpellNames.ICE_STORM).Clone();
                var iceTargets = new[] { "dCold", "dIceDr" };
                before = HpOf(ctx, iceTargets);
                args = new object[] { mage, iceStorm, iceTargets.Select(ctx.Get).ToList(), null, null };
                System.Reflection.MethodInfo ice = typeof(GameManager).GetMethod("TryResolveIceStormAoE", flags);
                handled = ice != null && (bool)ice.Invoke(gm, args);
                ctx.Note(DamageNotePrefix + "Ice Storm handled " + handled);
                ok &= handled;
                ok &= Taken(ctx, "Ice Storm", "dCold", before, 18);
                ok &= Taken(ctx, "Ice Storm", "dIceDr", before, 30);

                // Gibbering mouther spittle: ranged touch (forced 20), 1d4 acid (4) vs acid resistance 2.
                AIService ai = gm.GetComponent<AIService>();
                before = HpOf(ctx, "dAcid");
                bool spat = ai != null && ai.TryExecuteRangedSpecialAttack(ctx.Get("mouther"), ctx.Get("dAcid"));
                ctx.Note(DamageNotePrefix + "spittle used " + spat);
                ok &= spat && Taken(ctx, "Spittle", "dAcid", before, 2);

                // Searing Light (PHB p.275): SpellCaster.Cast deals no generic damage (SPL-124), and its handler deals
                // 1d8 per 2 caster levels (max 5d8), halved for a living creature, 1d6 per die for a construct.
                SpellData searing = SpellDatabase.GetSpell(DND35e.Identifiers.SpellNames.SEARING_LIGHT).Clone();
                CharacterController lit = ctx.Get("dAcid");
                before = HpOf(ctx, "dAcid");
                SpellResult searingResult = SpellCaster.Cast(searing, mage.Stats, lit.Stats, null, false, false, mage, lit);
                ok &= Taken(ctx, "Searing Light generic branch", "dAcid", before, 0);
                int searingDice = Mathf.Clamp(SpellCastingHelper.GetEffectiveCasterLevel(mage, searing) / 2, 1, 5);
                int searingExpected = SpellTargetingService.IsUndead(lit) ? 8 * searingDice
                    : SpellTargetingService.IsConstruct(lit) ? 6 * searingDice
                    : Mathf.Max(1, 8 * searingDice / 2);
                before = HpOf(ctx, "dAcid");
                System.Reflection.MethodInfo searingHandler = typeof(GameManager).GetMethod("TryResolveSearingLightSpellEffect", flags);
                bool searingHandled = searingResult != null && searingResult.Success && searingHandler != null
                    && (bool)searingHandler.Invoke(gm, new object[] { mage, lit, searing, searingResult });
                ctx.Note(DamageNotePrefix + "Searing Light hit " + (searingResult != null && searingResult.Success) + ", handled " + searingHandled
                    + ", DamageDealt " + (searingResult != null ? searingResult.DamageDealt : -1));
                ok &= searingHandled && Taken(ctx, "Searing Light handler", "dAcid", before, searingExpected)
                    && searingResult.DamageDealt == searingExpected;

                // Warm Fire Shield retribution: 1d6 + CL 5 = 11 fire, no save (PHB p.230).
                CharacterController shielded = ctx.Get("hero2");
                CharacterStats s = shielded.Stats;
                bool wasActive = s.FireShieldActive, wasWarm = s.FireShieldIsWarm;
                int oldCl = s.FireShieldCasterLevel;
                s.FireShieldActive = true;
                s.FireShieldIsWarm = true;
                s.FireShieldCasterLevel = 5;
                try
                {
                    var shield = new FireShieldReactionEffect(shielded);
                    before = HpOf(ctx, "dImmune", "dResist");
                    shield.OnMeleeAttackHit(ctx.Get("dImmune"), shielded, null);
                    shield.OnMeleeAttackHit(ctx.Get("dResist"), shielded, null);
                    ok &= Taken(ctx, "Fire Shield", "dImmune", before, 0);
                    ok &= Taken(ctx, "Fire Shield", "dResist", before, 1);
                }
                finally
                {
                    s.FireShieldActive = wasActive;
                    s.FireShieldIsWarm = wasWarm;
                    s.FireShieldCasterLevel = oldCl;
                }
            }
            finally
            {
                ScenarioHooks.RollFilter = previous;
            }
            return ok;
        }

        private static Dictionary<string, int> HpOf(ScenarioContext ctx, params string[] keys)
            => keys.ToDictionary(k => k, k => ctx.Get(k).Stats.CurrentHP);

        /// <summary>Notes and checks the HP <paramref name="key"/> lost since <paramref name="before"/>.</summary>
        private static bool Taken(ScenarioContext ctx, string source, string key, Dictionary<string, int> before, int expected)
        {
            int taken = before[key] - ctx.Get(key).Stats.CurrentHP;
            ctx.Note(DamageNotePrefix + source + " " + key + " took " + taken + " (expected " + expected + ")");
            if (taken != expected)
                ctx.Note(DamageNotePrefix + "mismatch: " + source + " " + key);
            return taken == expected;
        }

        /// <summary>Passes when the traced actor has the level, BAB and max HP given.</summary>
        private static ExpectResult ActorIs(TraceView v, string key, int level, int bab, int maxHp)
        {
            TraceEvent a = v.Of("actor").FirstOrDefault(e => e.Str("key") == key);
            if (a == null) return ExpectResult.Fail("no actor event for " + key);
            string got = "level " + a.Int("level") + ", BAB " + a.Int("bab") + ", max HP " + a.Int("maxHp");
            return a.Int("level") == level && a.Int("bab") == bab && a.Int("maxHp") == maxHp
                ? ExpectResult.Pass(got, a.Seq)
                : ExpectResult.Fail(got + "; expected level " + level + ", BAB " + bab + ", max HP " + maxHp, a.Seq);
        }

        private static Vector2Int? ActorPos(TraceView v, string key)
        {
            TraceEvent a = v.Of("actor").FirstOrDefault(e => e.Str("key") == key);
            return a != null && a.Get("pos") is Vector2Int p ? p : (Vector2Int?)null;
        }

        /// <summary>The footprint width in squares of a traced size (PHB p.149 Table 8-4, SizeCategory.GetSpaceWidthSquares).</summary>
        private static int FootprintWidth(object size)
        {
            if (size is SizeCategory sc)
                return Mathf.Max(1, sc.GetSpaceWidthSquares());
            return Enum.TryParse(Convert.ToString(size), out SizeCategory parsed) ? Mathf.Max(1, parsed.GetSpaceWidthSquares()) : 1;
        }
    }
}
#endif
