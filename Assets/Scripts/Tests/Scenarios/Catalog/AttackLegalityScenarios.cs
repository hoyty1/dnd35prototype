#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpellNames = DND35e.Identifiers.SpellNames;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 12 step 12 (docs/designs/pc_npc_parity_batch12.md; CMB-092, CMB-093, CMB-104, AI-005; decision D6): one
    /// "may this creature attack that one" gate for every attacker, AttackLegality.CanDirectlyAttack, and the ward save
    /// ledger, WardSaveLedger. Otiluke's Resilient Sphere blocks every attack across its boundary for PCs and NPCs (PHB
    /// p.258); the Sanctuary attacker save is rolled once per (attacker, casting) when the attack is attempted, by PCs and
    /// NPCs, and a failure loses that attack (PHB p.274); an intelligent undead's one Hide from Undead save is rolled at its
    /// turn-start perception step, a failure leaves the warded creature unseen by it and a success does not end the spell
    /// (PHB p.241); a maneuver needs its own reach (PHB p.155-158). The wards and the sphere are put in place by a
    /// scripted step with the effect objects a cast would add (a 5-ft sphere on one square).
    /// </summary>
    public static class AttackLegalityScenarios
    {
        private const string NotePrefix = "b12s12 ";

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("sphere-blocks-npc-attack", SphereBlocksNpcAttack);
            yield return S("sphere-blocks-pc-fullattack-ui", SphereBlocksPcFullAttackUi);
            yield return S("sanctuary-ai-fails-loses-attack", SanctuaryAiFailsLosesAttack);
            yield return S("sanctuary-pc-saves-once-ui", SanctuaryPcSavesOnceUi);
            yield return S("hide-from-undead-persists", HideFromUndeadPersists);
            yield return S("hide-from-undead-failed-unseen", HideFromUndeadFailedUnseen);
            yield return S("ai-trip-needs-reach", AiTripNeedsReach);
            yield return S("sanctuary-spell-keeps-slot-ui", SanctuarySpellKeepsSlotUi);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("AttackLegalityScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        private static void Note(ScenarioContext ctx, string key, object value) => ctx.Note(NotePrefix + key + "=" + value);

        private static Func<TraceView, ExpectResult> Fact(string key, string want)
            => v =>
            {
                string prefix = NotePrefix + key + "=";
                TraceEvent note = v.Of("note").LastOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
                if (note == null)
                    return ExpectResult.Inconclusive("no fact " + key);
                string got = note.Str("text").Substring(prefix.Length);
                return got == want ? ExpectResult.Pass(key + "=" + got) : ExpectResult.Fail(key + "=" + got + ", expected " + want);
            };

        private static Func<TraceView, ExpectResult> LogCount(string regex, int want, string what)
            => v =>
            {
                List<TraceEvent> lines = v.Log(regex);
                return lines.Count == want
                    ? ExpectResult.Pass(lines.Count + " " + what, lines.Select(e => e.Seq).ToArray())
                    : ExpectResult.Fail(lines.Count + " " + what + ", expected " + want, lines.Select(e => e.Seq).ToArray());
            };

        private static Func<TraceView, ExpectResult> AttackCount(string by, string target, int want, int? round = null)
            => v =>
            {
                List<TraceEvent> attacks = v.Attacks(by, target, false, round);
                return attacks.Count == want
                    ? ExpectResult.Pass(attacks.Count + " attacks by " + by + " on " + target, attacks.Select(e => e.Seq).ToArray())
                    : ExpectResult.Fail(attacks.Count + " attacks by " + by + " on " + target + ", expected " + want, attacks.Select(e => e.Seq).ToArray());
            };

        /// <summary>A 5-ft Otiluke's Resilient Sphere on <paramref name="cell"/> (PHB p.258), as the spell's handler builds it.</summary>
        private static bool PlaceSphere(Vector2Int cell)
        {
            var go = new GameObject("ResilientSphere_Area");
            go.transform.position = SquareGridUtils.GridToWorld(cell);
            ResilientSphereAreaEffect sphere = go.AddComponent<ResilientSphereAreaEffect>();
            sphere.CenterPosition = go.transform.position;
            sphere.CenterCell = cell;
            sphere.CastFootprint = new HashSet<Vector2Int> { cell };
            sphere.RoundsRemaining = 10;
            sphere.CasterLevel = 7;
            // In force at once (Start recomputes the same footprint on the next frame).
            sphere.AffectedCells.Add(cell);
            AreaEffectManager.Instance.RegisterAreaEffect(sphere);
            return ResilientSphereAreaEffect.IsCellInAnySphere(cell);
        }

        /// <summary>Sanctuary (or Hide from Undead) on <paramref name="c"/> with the effect and fields ApplySpellBuff sets.</summary>
        private static bool Ward(CharacterController c, string spellId, int dc)
        {
            StatusEffectManager sem = c.StatusEffectManager;
            if (sem == null)
            {
                sem = c.gameObject.AddComponent<StatusEffectManager>();
                sem.Init(c.Stats);
            }
            ActiveSpellEffect effect = sem.AddEffect(SpellDatabase.GetSpell(spellId).Clone(), c.Stats.CharacterName, 5);
            if (effect == null)
                return false;
            if (spellId == SpellNames.SANCTUARY)
            {
                c.Stats.SanctuaryActive = true;
                c.Stats.SanctuaryDC = dc;
                c.Stats.SanctuaryCasterLevel = 5;
                c.Stats.ActiveSanctuaryEffect = effect;
            }
            else
            {
                c.Stats.HideFromUndeadActive = true;
                c.Stats.HideFromUndeadDC = dc;
                c.Stats.HideFromUndeadCasterLevel = 5;
                c.Stats.ActiveHideFromUndeadEffect = effect;
            }
            return true;
        }

        // ── rules/sphere-blocks-npc-attack ──────────────────────────────

        /// <summary>
        /// A Scripted fighter stands in a 5-ft Resilient Sphere; an AI orc berserker stands next to it, outside. Nothing
        /// passes the sphere's boundary (PHB p.258), so the orc makes no attack on the fighter in two rounds, and the
        /// fighter's own attack on the orc is refused. Before batch 12 step 12 only the PC attack entry checked the sphere
        /// (CMB-092): the NPC melee sequence hit the sphered PC.
        /// </summary>
        private static ScenarioDef SphereBlocksNpcAttack()
        {
            return Rules("rules/sphere-blocks-npc-attack", "An AI orc cannot attack a PC inside a Resilient Sphere, nor the PC the orc (PHB p.258; CMB-092)")
                .Covers("CMB-092", "PHB p.258", "PC_NPC_PARITY", "batch 12 step 12")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Sphered Hero", 5)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Ai)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); })
                .Tweak("orc", Sturdy)
                .Initiative("hero", "orc")
                .Turn("hero", 1,
                    Step.Assert("sphere", ctx => { Note(ctx, "sphere", PlaceSphere(new Vector2Int(10, 10))); return true; }),
                    Step.Attack("orc"),
                    Step.Pass())
                .Turn("hero", 2, Step.Pass())
                .Expect("The sphere stands on the hero's square", Fact("sphere", "True"))
                .Expect("The AI orc makes no attack on the sphered hero in two rounds (PHB p.258; CMB-092)", AttackCount("orc", "hero", 0))
                .Expect("Nor does the hero's attack reach the orc through the boundary", AttackCount("hero", "orc", 0))
                .Build();
        }

        // ── rules/sphere-blocks-pc-fullattack-ui ────────────────────────

        /// <summary>
        /// A Ui fighter 6 (two attacks) full-attacks an adjacent orc at 1 hp; a second orc stands next to it inside a 5-ft
        /// Resilient Sphere. The first attack drops the first orc; the full attack's retarget list then offers no target,
        /// because nothing passes the sphere (PHB p.258), so the full attack ends with one attack used. Before batch 12 step
        /// 12 the full attack's retarget list and step loop never checked the sphere (CMB-092): the sphered orc was offered
        /// and could be attacked.
        /// </summary>
        private static ScenarioDef SphereBlocksPcFullAttackUi()
        {
            return Rules("rules/sphere-blocks-pc-fullattack-ui", "A PC full attack never retargets into a Resilient Sphere (PHB p.258; CMB-092)")
                .Covers("CMB-092", "PHB p.258", "PC_NPC_PARITY", "batch 12 step 12")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Full Attacker", 6)), 10, 10, Control.Ui)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Npc("sphered", "orc_berserker", 10, 11, Control.Scripted)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); })
                .Hp("orc", 1)
                .Tweak("sphered", Sturdy)
                .Initiative("orc", "sphered", "hero")
                .Force(20, 19, null, -1)
                .Force(8, 8, null, -1) // the longsword's d8: one hit always kills the 1-hp orc (8 + 3 >= 11)
                .Turn("orc", 1,
                    Step.Assert("sphere", ctx => { Note(ctx, "sphere", PlaceSphere(new Vector2Int(10, 11))); return true; }),
                    Step.Pass())
                .Turn("sphered", 1, Step.Pass())
                .Turn("hero", 1, Step.FullAttack("orc"), Step.EndTurn())
                .Expect("The sphere stands on the second orc's square", Fact("sphere", "True"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The full attack's first attack drops the orc outside", AttackCount("hero", "orc", 1))
                .Expect("No attack reaches the orc inside the sphere (PHB p.258; CMB-092)", AttackCount("hero", "sphered", 0))
                .Expect("The full attack ends with one of two attacks used: the sphered orc is no target",
                    LogCount("completes melee full attack \\(1/2 attacks used\\)", 1, "full-attack completion lines"))
                .Build();
        }

        // ── rules/sanctuary-ai-fails-loses-attack ───────────────────────

        /// <summary>
        /// A Scripted cleric (fighter stats) is under Sanctuary (DC 15); an AI orc berserker stands next to it, and it is the
        /// orc's only foe. Every Sanctuary save d20 is 2. The orc's attack attempt rolls its Will save once: it fails, the
        /// attack (the standard action) is lost, and the orc cannot attack the cleric while that casting lasts, so in round 2
        /// it neither attacks nor rolls again (PHB p.274; D6). Before batch 12 step 12 the AI rolled a fresh save every time
        /// it chose a target (several per turn, AI-005) and a failure only took the cleric off that choice.
        /// </summary>
        private static ScenarioDef SanctuaryAiFailsLosesAttack()
        {
            return Rules("rules/sanctuary-ai-fails-loses-attack", "An AI attacker saves once against Sanctuary; the failure loses the attack and bars the target (PHB p.274; AI-005, D6)")
                .Covers("AI-005", "CMB-093", "PHB p.274", "D6", "PC_NPC_PARITY", "batch 12 step 12")
                .MaxRounds(2)
                .Pc("cleric", ActorSource.Stats(() => RulesScenarios.Fighter("Warded Cleric", 5)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 11, 10, Control.Ai)
                .Tweak("cleric", c => { RulesScenarios.StripOffHand(c); Sturdy(c); })
                .Tweak("orc", Sturdy)
                .Initiative("cleric", "orc")
                .Force(20, 2, "save vs Sanctuary", -1)
                .Turn("cleric", 1,
                    Step.Assert("ward", ctx => { Note(ctx, "ward", Ward(ctx.Get("cleric"), SpellNames.SANCTUARY, 15)); return true; }),
                    Step.Pass())
                .Turn("cleric", 2,
                    Step.Assert("after-r1", ctx =>
                    {
                        CharacterController orc = ctx.Get("orc");
                        Note(ctx, "orc.known-failure", WardSaveLedger.HasKnownFailure(orc, ctx.Get("cleric")));
                        Note(ctx, "orc.std-spent-r1", orc.Actions != null && !orc.Actions.HasStandardAction);
                        return true;
                    }),
                    Step.Pass())
                .Expect("Sanctuary is on the cleric", Fact("ward", "True"))
                .Expect("The orc never attacks the warded cleric (PHB p.274)", AttackCount("orc", "cleric", 0))
                .Expect("The orc rolls one Sanctuary save in two rounds (one save per casting; AI-005)",
                    LogCount("save vs Sanctuary|Sanctuary! \\(Will|overcomes .*Sanctuary", 1, "Sanctuary save lines"))
                .Expect("The failed save loses the attempted attack once (D6)", LogCount("cannot follow through against Warded Cleric", 1, "lost-attack lines"))
                .Expect("After round 1 the orc knows it cannot attack the cleric", Fact("orc.known-failure", "True"))
                .Expect("The lost attack spent the orc's standard action in round 1", Fact("orc.std-spent-r1", "True"))
                .Build();
        }

        // ── rules/sanctuary-pc-saves-once-ui ────────────────────────────

        /// <summary>
        /// A Scripted orc is under Sanctuary (DC 15); a Ui fighter 6 attacks it twice in one turn (two clicks of the iterative
        /// sequence). Every Sanctuary save d20 is 20. The first attack's attempt rolls the fighter's one Will save, which
        /// succeeds; the second attack rolls nothing; both attacks are rolled (PHB p.274; D6). Before batch 12 step 12 a PC
        /// never saved against Sanctuary (CMB-093).
        /// </summary>
        private static ScenarioDef SanctuaryPcSavesOnceUi()
        {
            return Rules("rules/sanctuary-pc-saves-once-ui", "A PC saves once against an NPC's Sanctuary and then attacks freely (PHB p.274; CMB-093, D6)")
                .Covers("CMB-093", "PHB p.274", "D6", "PC_NPC_PARITY", "batch 12 step 12")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Sanctuary Breaker", 6)), 10, 10, Control.Ui)
                .Npc("orc", "orc_berserker", 11, 10, Control.Scripted)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Force(20, 20, "save vs Sanctuary", -1)
                .Turn("orc", 1,
                    Step.Assert("ward", ctx => { Note(ctx, "ward", Ward(ctx.Get("orc"), SpellNames.SANCTUARY, 15)); return true; }),
                    Step.Pass())
                .Turn("hero", 1, Step.Attack("orc"), Step.AttackAgain("orc"), Step.EndTurn())
                .Expect("Sanctuary is on the orc", Fact("ward", "True"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The hero rolls one Sanctuary save, which succeeds (PHB p.274; CMB-093)",
                    LogCount("overcomes Sanctuary Breaker|Sanctuary Breaker overcomes", 1, "successful Sanctuary save lines"))
                .Expect("No Sanctuary save fails and none is rolled twice", LogCount("cannot follow through|Sanctuary! \\(Will", 0, "failed Sanctuary save lines"))
                .Expect("Both of the hero's attacks are rolled against the orc", AttackCount("hero", "orc", 2))
                .Build();
        }

        // ── rules/hide-from-undead-persists ─────────────────────────────

        /// <summary>
        /// A Scripted fighter is under Hide from Undead (DC 14); an AI ghoul (intelligent) and an AI zombie (mindless) stand
        /// next to it. Every Hide from Undead save d20 is 20. The ghoul's turn-start perception step rolls its one Will save,
        /// which succeeds, and it attacks; the spell goes on (PHB p.241: an intelligent undead's success does not end it), so the zombie, which never
        /// sees through it, makes no attack. Before batch 12 step 12 a successful save ended the spell for everyone (AI-005),
        /// and the zombie then attacked.
        /// </summary>
        private static ScenarioDef HideFromUndeadPersists()
        {
            return Rules("rules/hide-from-undead-persists", "An intelligent undead that sees through Hide from Undead does not end it; a mindless one never attacks (PHB p.241; AI-005, D6)")
                .Covers("AI-005", "PHB p.241", "D6", "PC_NPC_PARITY", "batch 12 step 12")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hidden Hero", 5)), 10, 10, Control.Scripted)
                .Npc("ghoul", "ghoul", 11, 10, Control.Ai)
                .Npc("zombie", "zombie_shambler", 9, 10, Control.Ai)
                // A +30 save fixture keeps the hero clear of the ghoul's paralysis, so its round-2 turn runs (the save fallback
                // makes every unforced save d20 a 10).
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); c.Stats.Bonuses.Set("scenario:save-tweak", BonusTarget.AllSaves, BonusType.Untyped, 30, "scenario save tweak"); })
                .Tweak("ghoul", Sturdy)
                .Tweak("zombie", Sturdy)
                .Initiative("hero", "ghoul", "zombie")
                .Force(20, 20, "save vs Hide from Undead", -1)
                .Turn("hero", 1,
                    Step.Assert("ward", ctx =>
                    {
                        Note(ctx, "ward", Ward(ctx.Get("hero"), SpellNames.HIDE_FROM_UNDEAD, 14));
                        Note(ctx, "zombie.mindless", ctx.Get("zombie").Stats.IsMindless);
                        Note(ctx, "ghoul.mindless", ctx.Get("ghoul").Stats.IsMindless);
                        return true;
                    }),
                    Step.Pass())
                .Turn("hero", 2,
                    Step.Assert("after-r1", ctx => { Note(ctx, "ward.after-r1", ctx.Get("hero").Stats.HideFromUndeadActive); return true; }),
                    Step.Pass())
                .Expect("Hide from Undead is on the hero; the zombie is mindless, the ghoul is not",
                    Expect.All(Fact("ward", "True"), Fact("zombie.mindless", "True"), Fact("ghoul.mindless", "False")))
                .Expect("The ghoul rolls one save in two rounds and sees through the ward (PHB p.241; one save per casting)", LogCount("sees through Hidden Hero's Hide from Undead", 1, "see-through lines"))
                .Expect("The spell is not ended by the ghoul's success (AI-005)",
                    Expect.All(Fact("ward.after-r1", "True"), LogCount("Hide from Undead on Hidden Hero is broken", 0, "broken-ward lines")))
                .Expect("The ghoul attacks the hero", v => v.Attacks("ghoul", "hero", false).Count >= 1
                    ? ExpectResult.Pass(v.Attacks("ghoul", "hero", false).Count + " attacks")
                    : ExpectResult.Fail("no attack by the ghoul"))
                .Expect("The mindless zombie never perceives the hero, so it makes no attack (PHB p.241)", AttackCount("zombie", "hero", 0))
                .Build();
        }

        // ── rules/hide-from-undead-failed-unseen ───────────────────────

        /// <summary>
        /// As rules/hide-from-undead-persists, but every Hide from Undead save d20 is 1. The ghoul's turn-start perception
        /// step rolls its one Will save, which fails: the hero is unseen by it (it cannot see, hear or smell the hero) for the
        /// rest of the casting, and no second save is rolled in round 2. A failure is not Sanctuary's: no attack is "lost" to
        /// the ward (delegated RAW reading 2026-10-28, D6; PHB p.241: it may still try to strike an unseen foe it suspects),
        /// and the spell stays on the hero.
        /// </summary>
        private static ScenarioDef HideFromUndeadFailedUnseen()
        {
            return Rules("rules/hide-from-undead-failed-unseen", "An intelligent undead that fails its Hide from Undead save cannot perceive the warded creature; it loses no attack to the ward (PHB p.241; D6)")
                .Covers("PHB p.241", "D6", "PC_NPC_PARITY", "batch 12 step 12")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hidden Hero", 5)), 10, 10, Control.Scripted)
                .Npc("ghoul", "ghoul", 11, 10, Control.Ai)
                .Npc("zombie", "zombie_shambler", 9, 10, Control.Ai)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); c.Stats.Bonuses.Set("scenario:save-tweak", BonusTarget.AllSaves, BonusType.Untyped, 30, "scenario save tweak"); })
                .Tweak("ghoul", Sturdy)
                .Tweak("zombie", Sturdy)
                .Initiative("hero", "ghoul", "zombie")
                .Force(20, 1, "save vs Hide from Undead", -1)
                .Turn("hero", 1,
                    Step.Assert("ward", ctx => { Note(ctx, "ward", Ward(ctx.Get("hero"), SpellNames.HIDE_FROM_UNDEAD, 14)); return true; }),
                    Step.Pass())
                .Turn("hero", 2,
                    Step.Assert("after-r1", ctx =>
                    {
                        Note(ctx, "ward.after-r1", ctx.Get("hero").Stats.HideFromUndeadActive);
                        Note(ctx, "ghoul.hidden", WardSaveLedger.IsHiddenFromUndead(ctx.Get("ghoul"), ctx.Get("hero")));
                        Note(ctx, "ghoul.concealment", ctx.Get("hero").GetMissChance(ctx.Get("ghoul")) >= 50);
                        return true;
                    }),
                    Step.Pass())
                .Expect("Hide from Undead is on the hero", Fact("ward", "True"))
                .Expect("The ghoul rolls one save in two rounds and fails it: the hero is unseen by it (PHB p.241)",
                    Expect.All(LogCount("cannot see, hear or smell Hidden Hero", 1, "failed-save lines"), LogCount("sees through Hidden Hero's Hide from Undead", 0, "see-through lines")))
                .Expect("After the failure the hero has total concealment from the ghoul, and the spell goes on",
                    Expect.All(Fact("ghoul.hidden", "True"), Fact("ghoul.concealment", "True"), Fact("ward.after-r1", "True")))
                .Expect("No attack is lost to Hide from Undead by the intelligent ghoul (it is not Sanctuary)", LogCount("cannot perceive Hidden Hero: Hide from Undead", 0, "attack-lost lines"))
                .Expect("The mindless zombie never perceives the hero, so it makes no attack (PHB p.241)", AttackCount("zombie", "hero", 0))
                .Build();
        }

        // ── rules/ai-trip-needs-reach ───────────────────────────────────

        /// <summary>
        /// A Scripted orc berserker (a greataxe, no reach) tries to trip a fighter 10 ft away through the NPC maneuver
        /// executor the AI uses: refused before any cost or AoO, because a trip is an unarmed melee touch attack within the
        /// creature's natural reach (PHB p.158; CMB-104). The orc then moves next to the fighter and trips it. Before batch
        /// 12 step 12 no layer checked a maneuver's reach, so the trip was resolved from 10 ft.
        /// </summary>
        private static ScenarioDef AiTripNeedsReach()
        {
            return Rules("rules/ai-trip-needs-reach", "The NPC maneuver executor refuses a trip out of reach and allows it adjacent (PHB p.158; CMB-104)")
                .Covers("CMB-104", "PHB p.158", "PC_NPC_PARITY", "batch 12 step 12")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Distant Hero", 5)), 10, 10, Control.Scripted)
                .Npc("orc", "orc_berserker", 12, 10, Control.Scripted)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); Sturdy(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "hero")
                .Force(20, 20, "Trip touch attack")
                .Force(20, 18, "Trip Strength check")
                .Force(20, 2, "Trip defense check")
                .Turn("orc", 1,
                    Step.Maneuver(SpecialAttackType.Trip, "hero"),
                    Step.Move(11, 10),
                    Step.Maneuver(SpecialAttackType.Trip, "hero"))
                .Turn("hero", 1, Step.Pass())
                .Expect("The trip from 10 ft is refused (PHB p.158; CMB-104)", Expect.StepStatus("orc", 1, "Maneuver", 0, "refused"))
                .Expect("The adjacent trip is made", Expect.StepStatus("orc", 1, "Maneuver", 1, "done"))
                .Expect("One trip is resolved, after the move, and the hero is prone", v =>
                {
                    List<TraceEvent> trips = v.Maneuvers("orc", SpecialAttackType.Trip);
                    TraceEvent move = v.Moves("orc").FirstOrDefault();
                    if (trips.Count != 1) return ExpectResult.Fail(trips.Count + " trips", trips.Select(e => e.Seq).ToArray());
                    if (move == null || trips[0].Seq < move.Seq) return ExpectResult.Fail("the trip came before the move", trips[0].Seq);
                    return trips[0].Bool("success") ? ExpectResult.Pass("one trip after the move", trips[0].Seq) : ExpectResult.Fail("the trip failed", trips[0].Seq);
                })
                .Build();
        }
        // ── rules/sanctuary-spell-keeps-slot-ui ─────────────────────────

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

        /// <summary>Unused regular slots of <paramref name="level"/> holding <paramref name="spellId"/>.</summary>
        private static int UnusedSlots(CharacterController c, int level, string spellId)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                return -1;
            return sc.SpellSlots.Count(slot => slot != null && slot.Level == level && !slot.IsDomainSlot && !slot.IsUsed
                && slot.PreparedSpell != null && slot.PreparedSpell.SpellId == spellId);
        }

        private static void PrepareMissiles(CharacterController c)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spellcasting");
            foreach (SpellSlot slot in sc.SpellSlots)
            {
                if (slot == null || slot.Level != 1 || slot.IsDomainSlot || slot.IsUsed)
                    continue;
                slot.PreparedSpell = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE).Clone();
                sc.SyncPreparedSpellsFromSlots();
                return;
            }
            throw new InvalidOperationException(c.Stats.CharacterName + " has no level-1 slot");
        }

        /// <summary>
        /// A Ui wizard 3 casts Magic Missile at a Scripted orc 20 ft away that is under Sanctuary (DC 15); every Sanctuary
        /// save d20 is 2. The cast core rolls the wizard's one save after the casting action is paid (its Ward stage): the
        /// save fails, the casting action is lost, but the slot is kept because the spell was never cast, and the orc takes
        /// no damage (PHB p.274; D6). Before batch 12 step 12 a PC never saved against Sanctuary (CMB-093) and NPC spells
        /// ignored it (AI-005).
        /// </summary>
        private static ScenarioDef SanctuarySpellKeepsSlotUi()
        {
            return Rules("rules/sanctuary-spell-keeps-slot-ui", "A spell at a Sanctuaried foe rolls the save once; a failure loses the casting action but keeps the slot (PHB p.274; D6)")
                .Covers("CMB-093", "AI-005", "PHB p.274", "D6", "PC_NPC_PARITY", "batch 12 step 12")
                .MaxRounds(1)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Warded-off Wizard", 3)), 5, 10, Control.Ui)
                .Npc("orc", "orc_berserker", 9, 10, Control.Scripted)
                .Tweak("wizard", c => { PrepareMissiles(c); Sturdy(c); })
                .Tweak("orc", Sturdy)
                .Initiative("orc", "wizard")
                .Force(20, 2, "save vs Sanctuary", -1)
                .Turn("orc", 1,
                    Step.Assert("ward", ctx => { Note(ctx, "ward", Ward(ctx.Get("orc"), SpellNames.SANCTUARY, 15)); return true; }),
                    Step.Pass())
                .Turn("wizard", 1,
                    Step.Assert("before", ctx =>
                    {
                        Note(ctx, "slots.before", UnusedSlots(ctx.Get("wizard"), 1, SpellNames.MAGIC_MISSILE));
                        Note(ctx, "orc.hp.before", ctx.Get("orc").Stats.CurrentHP);
                        return true;
                    }),
                    Step.Cast(SpellNames.MAGIC_MISSILE, "orc"),
                    Step.Assert("after", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        Note(ctx, "slots.after", UnusedSlots(w, 1, SpellNames.MAGIC_MISSILE));
                        Note(ctx, "orc.hp.after", ctx.Get("orc").Stats.CurrentHP);
                        Note(ctx, "std.spent", w.Actions != null && !w.Actions.HasStandardAction);
                        Note(ctx, "known-failure", WardSaveLedger.HasKnownFailure(w, ctx.Get("orc")));
                        return true;
                    }),
                    Step.EndTurn())
                .Expect("Sanctuary is on the orc and the wizard has one Magic Missile prepared",
                    Expect.All(Fact("ward", "True"), Fact("slots.before", "1")))
                .Expect("The wizard rolls one Sanctuary save and fails it (PHB p.274)", LogCount("Sanctuary! \\(Will", 1, "failed Sanctuary save lines"))
                .Expect("The spell is never cast: the slot is kept (D6)", Expect.All(Fact("slots.after", "1"), LogCount("keeps Magic Missile: the spell was never cast", 1, "slot-kept lines")))
                .Expect("The casting action is lost", Fact("std.spent", "True"))
                .Expect("The orc takes no damage", v =>
                {
                    Func<string, string> fact = key =>
                    {
                        string prefix = NotePrefix + key + "=";
                        TraceEvent n = v.Of("note").LastOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
                        return n != null ? n.Str("text").Substring(prefix.Length) : null;
                    };
                    string before = fact("orc.hp.before"), after = fact("orc.hp.after");
                    return before != null && before == after ? ExpectResult.Pass("hp " + after) : ExpectResult.Fail("hp " + before + " -> " + after);
                })
                .Expect("The wizard now knows it cannot target the orc", Fact("known-failure", "True"))
                .Build();
        }
    }
}
#endif
