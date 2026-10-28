#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpellNames = DND35e.Identifiers.SpellNames;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 12 step 14 (docs/designs/pc_npc_parity_batch12.md; CMB-097, CMB-169, CMB-170, CMB-090, CMB-019, CMB-096 drop,
    /// CMB-173, CMB-174; decisions D8 and D15): every attack step gets the same after-attack effects through the one
    /// post-attack hook (GameManager.OnAttackResolved). Improved Grab follows each trigger hit on the PC's Full Attack
    /// button, after an AoO and inside a pounce (MM p.310); a charm breaks when the charmer's ally attacks (PHB p.209); an
    /// NPC archer spends its arrows and an NPC thrower drops its javelins (PHB p.113); a Fire Shield that kills the attacker
    /// ends the fight from the hook (CORE-011).
    /// </summary>
    public static class PostAttackScenarios
    {
        private const string NotePrefix = "b12s14 ";

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("improved-grab-pc-full-attack-ui", ImprovedGrabPcFullAttackUi);
            yield return S("improved-grab-aoo", ImprovedGrabAoO);
            yield return S("pounce-grab-mid-sequence", PounceGrabMidSequence);
            yield return S("charm-broken-by-ally-attack", CharmBrokenByAllyAttack);
            yield return S("npc-archer-ammo", NpcArcherAmmo);
            yield return S("npc-throws-javelin", NpcThrowsJavelin);
            yield return S("fire-shield-kills-attacker-ends-combat", FireShieldKillsAttackerEndsCombat);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("PostAttackScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title)
            => Scenario.Define(id, title).Tags("rules", "post-attack").Covers("PC_NPC_PARITY", "batch 12 step 14");

        // ── Helpers ─────────────────────────────────────────────────────

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        private static void SturdyWithoutAoO(CharacterController c)
        {
            Sturdy(c);
            c.Stats.CanMakeAttacksOfOpportunity = false;
        }

        /// <summary>Every d20 rolls 19 (a hit that is not a crit threat for a claw or bite) except the grab's "Grapple check", which rolls 10.</summary>
        private static ScenarioBuilder ForceGrabDice(ScenarioBuilder b) => b
            .RecordDice()
            .Force(20, 10, "Grapple check", -1)
            .Force(20, 19, null, -1);

        private static global::Inventory Inv(CharacterController c)
            => c != null && c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;

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

        private static bool HasCond(JsonObj snap, string cond) => snap != null && snap.Get("conds") is List<string> l && l.Contains(cond);

        /// <summary>The "Grapple check" dice after <paramref name="attacker"/>'s first hit on <paramref name="target"/>, up to the next turn start (null: no hit).</summary>
        private static List<TraceEvent> GrabChecksAfterFirstHit(TraceView v, string attacker, string target, out TraceEvent hit)
        {
            hit = v.Attacks(attacker, target).FirstOrDefault(e => e.Bool("hit"));
            if (hit == null)
                return null;
            int from = hit.Seq;
            TraceEvent nextTurn = v.Of("turn_start").FirstOrDefault(e => e.Seq > from);
            int to = nextTurn != null ? nextTurn.Seq : int.MaxValue;
            return v.Of("dice").Where(e => e.Seq > from && e.Seq < to && e.Str("ctx") == "Grapple check").ToList();
        }

        /// <summary>The grab's opposed grapple checks follow the trigger hit at once, before any other attack by the grabber (MM p.310; D8).</summary>
        private static Func<TraceView, ExpectResult> GrabRightAfterFirstHit(string attacker, string target)
            => v =>
            {
                List<TraceEvent> checks = GrabChecksAfterFirstHit(v, attacker, target, out TraceEvent hit);
                if (checks == null) return ExpectResult.Fail(attacker + " never hit " + target);
                if (checks.Count != 2) return ExpectResult.Fail(checks.Count + " grapple checks after the hit #" + hit.Seq, hit.Seq);
                TraceEvent between = v.Attacks(attacker).FirstOrDefault(e => e.Seq > hit.Seq && e.Seq < checks[0].Seq);
                return between == null
                    ? ExpectResult.Pass("hit #" + hit.Seq + ", then both grapple checks", checks.Select(e => e.Seq).ToArray())
                    : ExpectResult.Fail("attack #" + between.Seq + " came between the hit and the grab", between.Seq);
            };

        private static Func<TraceView, ExpectResult> EndsGrappled(string key)
            => v =>
            {
                JsonObj snap = v.Final(key);
                if (snap == null) return ExpectResult.Fail("no final snapshot of " + key);
                return HasCond(snap, "Grappled")
                    ? ExpectResult.Pass(key + " ends grappled")
                    : ExpectResult.Fail(key + " conds " + string.Join(",", (snap.Get("conds") as List<string>) ?? new List<string>()));
            };

        private static NaturalAttackDefinition Natural(string name, int count, bool primary) => new NaturalAttackDefinition
        {
            Name = name, DamageDice = primary ? 6 : 4, DamageCount = 1, Count = count, IsPrimary = primary,
            BonusDamageSource = primary ? DamageBonusSource.Strength : DamageBonusSource.StrengthHalf
        };

        /// <summary>A Human fighter 6 (BAB +6, STR 16, Improved Grapple) fighting with a claw (Improved Grab) and two bites, no weapons.</summary>
        private static CharacterStats GrabbingBeast(string name)
        {
            CharacterStats s = RulesScenarios.Fighter(name, 6, "Improved Grapple");
            s.NaturalAttacks.Clear();
            s.NaturalAttacks.Add(Natural("Claw", 1, true));
            s.NaturalAttacks.Add(Natural("Bite", 2, false));
            s.HasImprovedGrab = true;
            s.ImprovedGrabTriggerAttackName = "Claw";
            return s;
        }

        /// <summary>A Small goblin with STR 1 and BAB 0 (a weak grappler), 300 more hit points and no AoOs.</summary>
        private static void WeakGrappleGoblin(CharacterController c)
        {
            SturdyWithoutAoO(c);
            c.Stats.STR = 1;
            c.Stats.BaseAttackBonusOverride = 0;
        }

        // ── Improved Grab (D8) ──────────────────────────────────────────

        /// <summary>
        /// CMB-097 (origin C, the PC Full Attack button): a Ui hero with a claw (Improved Grab), then two bites, full-attacks
        /// a Small goblin. The claw hits (forced 19s), the Improved Grab menu opens and the hero grapples (grapple checks
        /// forced to 10: the hero +13 against the goblin's -9); grappling, it makes no more ordinary attacks (PHB p.156).
        /// </summary>
        private static ScenarioDef ImprovedGrabPcFullAttackUi()
        {
            return ForceGrabDice(Rules("rules/improved-grab-pc-full-attack-ui", "Improved Grab after the claw hit of the PC's Full Attack button, then no more ordinary attacks (MM p.310, PHB p.156; D8, CMB-097)")
                .Covers("CMB-097", "MM p.310", "PHB p.156")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => GrabbingBeast("Hero")), 10, 10, Control.Ui)
                .Npc("goblin", "goblin", 11, 10, Control.Scripted)
                .Tweak("hero", RulesScenarios.StripAllWeapons)
                .Tweak("goblin", WeakGrappleGoblin)
                .Initiative("hero", "goblin"))
                .Turn("goblin", 0, Step.Pass())
                .Turn("hero", 1, Step.FullAttack("goblin"), Step.AnswerImprovedGrab("grapple"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The full attack and the grab answer are done", Expect.All(
                    Expect.StepStatus("hero", 1, "FullAttack", 0, "done"),
                    Expect.StepStatus("hero", 1, "AnswerImprovedGrab(grapple)", 0, "done")))
                .Expect("The claw hit is followed at once by the grab's opposed grapple checks (MM p.310)", GrabRightAfterFirstHit("hero", "goblin"))
                .Expect("The goblin ends grappled", EndsGrappled("goblin"))
                .Expect("Grappling, the hero makes no more ordinary attacks: one attack in round 1 (PHB p.156)", v =>
                {
                    List<TraceEvent> r1 = v.Attacks("hero", null, null, 1);
                    return r1.Count == 1
                        ? ExpectResult.Pass("one attack, " + r1[0].Str("weapon"), r1[0].Seq)
                        : ExpectResult.Fail(r1.Count + " attacks: " + string.Join(",", r1.Select(e => e.Str("weapon"))), r1.Select(e => e.Seq).ToArray());
                })
                .Build();
        }

        /// <summary>
        /// CMB-170 (origin I): a Small halfling walks away from a Medium crocodile (bite, Improved Grab; MM p.271) that has
        /// already acted (so it is not flat-footed), provoking its AoO. The bite hits (forced 19) and the grab follows it (the AI decider grapples); the grappled halfling's move
        /// ends there (PHB p.156).
        /// </summary>
        private static ScenarioDef ImprovedGrabAoO()
        {
            return ForceGrabDice(Rules("rules/improved-grab-aoo", "Improved Grab after an AoO's bite hit, and the grabbed mover's move ends (MM p.310, PHB p.156; D8, CMB-170)")
                .Covers("CMB-170", "MM p.310", "MM p.271", "PHB p.137")
                .MaxRounds(1)
                .Pc("halfling", ActorSource.Stats(() => FighterOfRace("Halfling", "Halfling")), 10, 10, Control.Scripted)
                .Npc("croc", "crocodile", 11, 10, Control.Scripted)
                .Tweak("halfling", Sturdy)
                .Tweak("croc", Sturdy)
                .Initiative("croc", "halfling"))
                .Turn("croc", 1, Step.Pass())
                .Turn("halfling", 1, Step.Move(8, 10))
                .Expect("The halfling's move provokes the crocodile's AoO", v =>
                {
                    List<TraceEvent> aoos = v.AoOs("croc", "halfling");
                    return aoos.Count >= 1 ? ExpectResult.Pass(aoos.Count + " AoO(s)", aoos[0].Seq) : ExpectResult.Fail("no AoO by the crocodile");
                })
                .Expect("The AoO's bite hit is followed by the grab's opposed grapple checks (MM p.310; D8)", GrabRightAfterFirstHit("croc", "halfling"))
                .Expect("The halfling ends grappled", EndsGrappled("halfling"))
                .Expect("The grappled halfling does not reach its destination (PHB p.156)", v =>
                {
                    JsonObj snap = v.Final("halfling");
                    if (snap == null || !(snap.Get("pos") is Vector2Int p)) return ExpectResult.Fail("no final position");
                    return p != new Vector2Int(8, 10) ? ExpectResult.Pass("halfling at " + p) : ExpectResult.Fail("the halfling moved on to " + p);
                })
                .Build();
        }

        /// <summary>
        /// CMB-169 (origin J, one step at a time): a Large giant praying mantis (MM p.285: claws, then bite; improved grab
        /// with a claw) given Pounce charges a Medium human. Its first claw hits (forced 19s) and the grab follows that hit at
        /// once (grapple checks forced to 10); grappling, it makes no more ordinary attacks of the pounce (PHB p.156), as the
        /// melee sequence does (rules/improved-grab-mid-sequence).
        /// </summary>
        private static ScenarioDef PounceGrabMidSequence()
        {
            return ForceGrabDice(Rules("rules/pounce-grab-mid-sequence", "Inside a pounce the grab follows its trigger hit at once, and the remaining attacks are not made (MM p.310, MM p.313, PHB p.156; D8, CMB-169)")
                .Covers("CMB-169", "MM p.310", "MM p.313", "MM p.285", "PHB p.156", "AI")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => FighterOfRace("Hero", "Human")), 5, 10, Control.Scripted)
                .Npc("mantis", "giant_praying_mantis", 11, 10, Control.Scripted)
                .Tweak("hero", Sturdy)
                .Tweak("mantis", c => c.Stats.HasPounce = true)
                .Initiative("mantis", "hero"))
                .Turn("mantis", 1, Step.Charge("hero"))
                .Turn("hero", 1, Step.Pass())
                .Expect("The mantis charges", Expect.StepStatus("mantis", 1, "Charge", 0, "done"))
                .Expect("The first claw hit is followed at once by the grab's grapple checks, before any other attack (MM p.310; D8)", GrabRightAfterFirstHit("mantis", "hero"))
                .Expect("The hero ends grappled", EndsGrappled("hero"))
                .Expect("Grappling, the mantis makes no more of the pounce's attacks: one attack in round 1 (PHB p.156)", v =>
                {
                    List<TraceEvent> r1 = v.Attacks("mantis", null, null, 1);
                    return r1.Count == 1 && r1[0].Bool("hit")
                        ? ExpectResult.Pass("one claw, then the grapple", r1[0].Seq)
                        : ExpectResult.Fail(r1.Count + " attacks: " + string.Join(",", r1.Select(e => e.Str("weapon"))), r1.Select(e => e.Seq).ToArray());
                })
                .Build();
        }

        // ── Charm (D15) ─────────────────────────────────────────────────

        /// <summary>
        /// D15 (PHB p.209): the enchanter (Scripted, the NPC turn path) charms the hero (Will save forced to a natural 1);
        /// then the enchanter's ally, an orc, attacks the charmed hero. Any threatening act by the charmer's apparent allies
        /// breaks the charm, so the hero is no longer charmed. A second hero keeps the party in the fight.
        /// </summary>
        private static ScenarioDef CharmBrokenByAllyAttack()
        {
            return Rules("rules/charm-broken-by-ally-attack", "An attack by the charmer's ally breaks Charm Person (PHB p.209; D15, CMB-090)")
                .Covers("CMB-090", "PHB p.209")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 8, 10, Control.Idle)
                .Pc("hero2", ActorSource.Stats(() => RulesScenarios.Fighter("Hero Two", 4)), 3, 16, Control.Idle)
                .Npc("enchanter", "evil_enchanter_test", 13, 10, Control.Scripted)
                .Npc("orc", "orc_warrior", 9, 10, Control.Scripted)
                .Tweak("hero", Sturdy)
                .Force(20, 1, SpellCaster.SavingThrowContext)
                .Initiative("enchanter", "orc", "hero", "hero2")
                .Turn("enchanter", 1, Step.Cast(SpellNames.CHARM_PERSON, "hero"), Step.EndTurn())
                .Turn("orc", 1, Step.Attack("hero"))
                .Expect("The enchanter charms the hero", v =>
                    v.Log("is charmed by").Count > 0 ? ExpectResult.Pass("charm logged") : ExpectResult.Fail("no charm line logged"))
                .Expect("The orc attacks the charmed hero", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("orc", "hero");
                    return attacks.Count > 0 ? ExpectResult.Pass(attacks.Count + " attack(s)", attacks[0].Seq) : ExpectResult.Fail("no attack by the orc");
                })
                .Expect("The orc's attack breaks the charm (PHB p.209: the charmer's apparent allies; D15), on the orc's turn", v =>
                {
                    // The break is sent by the swing itself (NotifyWeaponAttackAgainst), just before the trace's attack event.
                    List<TraceEvent> breaks = v.Log("is no longer charmed after being attacked by Orc");
                    TraceEvent orcTurn = v.TurnsOf("orc").FirstOrDefault();
                    if (breaks.Count == 0) return ExpectResult.Fail("no charm-break line naming the orc");
                    return orcTurn != null && breaks[0].Seq > orcTurn.Seq
                        ? ExpectResult.Pass(breaks[0].Str("text"), breaks[0].Seq)
                        : ExpectResult.Fail("the break line is not in the orc's turn", breaks[0].Seq);
                })
                .Expect("The hero ends the round not charmed", v =>
                {
                    JsonObj snap = v.Final("hero");
                    return snap != null && !HasCond(snap, "Charmed") ? ExpectResult.Pass("not charmed") : ExpectResult.Fail("the hero is still charmed");
                })
                .Build();
        }

        // ── Ammunition and thrown weapons (CMB-019, CMB-174) ────────────

        /// <summary>
        /// CMB-019 (origins G and H; PHB p.113): an NPC archer (Ranger 2, longbow) with 3 arrows shoots a sturdy hero 30 ft
        /// away each round. Each shot spends one arrow, so after the third shot it has none and cannot fire again; the refused shot
        /// spends no action and provokes nothing (the ammunition is checked before the ward, the AoOs and the action).
        /// </summary>
        private static ScenarioDef NpcArcherAmmo()
        {
            return Rules("rules/npc-archer-ammo", "An NPC archer with 3 arrows stops shooting after its third shot (PHB p.113; CMB-019)")
                .Covers("CMB-019", "PHB p.113")
                .MaxRounds(5)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Idle)
                .Npc("archer", "test_archer_gust", 16, 10, Control.Scripted)
                .Tweak("hero", Sturdy)
                .Tweak("archer", c =>
                {
                    global::Inventory inv = Inv(c);
                    if (inv == null || inv.GeneralSlots == null)
                        return;
                    bool kept = false;
                    for (int i = 0; i < inv.GeneralSlots.Length; i++)
                    {
                        ItemData item = inv.GeneralSlots[i];
                        if (item == null || !item.IsAmmunition || item.AmmoType != AmmunitionType.Arrow)
                            continue;
                        if (!kept)
                        {
                            item.Quantity = 3;
                            kept = true;
                        }
                        else
                        {
                            inv.GeneralSlots[i] = null;
                        }
                    }
                    inv.RecalculateStats();
                })
                .Initiative("archer", "hero")
                .Turn("archer", 1, Step.Assert("arrows", ctx => { Note(ctx, "arrows.start", Inv(ctx.Get("archer")).GetTotalAmmoCount(AmmunitionType.Arrow)); return true; }), Step.Attack("hero"))
                .Turn("archer", 2, Step.Attack("hero"))
                .Turn("archer", 3, Step.Attack("hero"))
                .Turn("archer", 4, Step.Attack("hero"),
                    Step.Assert("actions", ctx => { CharacterController a = ctx.Get("archer"); Note(ctx, "r4.fullround", a.Actions != null && a.Actions.HasFullRoundAction); return true; }))
                .Turn("archer", 5, Step.Assert("arrows", ctx => { Note(ctx, "arrows.end", Inv(ctx.Get("archer")).GetTotalAmmoCount(AmmunitionType.Arrow)); return true; }), Step.Attack("hero"))
                .Expect("The archer starts with 3 arrows (fixture check)", Fact("arrows.start", "3"))
                .Expect("It shoots exactly 3 times over 5 rounds (one arrow per shot, PHB p.113)", v =>
                {
                    List<TraceEvent> shots = v.Attacks("archer", "hero");
                    return shots.Count == 3
                        ? ExpectResult.Pass("3 shots in rounds " + string.Join(",", shots.Select(e => e.Round)), shots.Select(e => e.Seq).ToArray())
                        : ExpectResult.Fail(shots.Count + " shots", shots.Select(e => e.Seq).ToArray());
                })
                .Expect("No arrows are left", Fact("arrows.end", "0"))
                .Expect("The refused shot with an empty quiver spends no action and provokes nothing (PHB p.113, p.141; CMB-019 review)", Expect.All(
                    Fact("r4.fullround", "True"),
                    v => v.AoOs(null, "archer").Count == 0 ? ExpectResult.Pass("no AoO on the archer") : ExpectResult.Fail(v.AoOs(null, "archer").Count + " AoO(s) on the archer")))
                .Expect("A later attack is refused for want of arrows (combat log)", v =>
                    v.Log("has no Arrow ammunition").Count > 0 ? ExpectResult.Pass("no-ammunition line logged") : ExpectResult.Fail("no no-ammunition line"))
                .Build();
        }

        /// <summary>
        /// CMB-174 and CMB-096's drop (origins G and H; PHB p.113): an orc holding a javelin, with one more in its pack, throws
        /// at a hero 20 ft away. Each javelin lands in the hero's square, and after the first throw the orc draws the second.
        /// After the second throw it has none left.
        /// </summary>
        private static ScenarioDef NpcThrowsJavelin()
        {
            return Rules("rules/npc-throws-javelin", "An NPC's thrown javelin lands on the ground and the next is drawn (PHB p.113; CMB-174, CMB-096)")
                .Covers("CMB-174", "CMB-096", "CMB-019", "PHB p.113")
                .MaxRounds(3)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Idle)
                .Npc("orc", "orc_warrior", 14, 10, Control.Scripted)
                .Tweak("hero", Sturdy)
                .Tweak("orc", c =>
                {
                    global::Inventory inv = Inv(c);
                    if (inv == null || inv.GeneralSlots == null)
                        return;
                    for (int i = 0; i < inv.GeneralSlots.Length; i++)
                        if (inv.GeneralSlots[i] != null && inv.GeneralSlots[i].IsWeapon)
                            inv.GeneralSlots[i] = null;
                    inv.RightHandSlot = ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponJavelin);
                    inv.LeftHandSlot = null;
                    inv.AddItem(ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponJavelin));
                    inv.RecalculateStats();
                })
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Assert("before", ctx => { Facts(ctx, "r0"); return true; }), Step.Attack("hero"),
                    Step.Assert("after-1", ctx => { Facts(ctx, "r1"); return true; }))
                .Turn("orc", 2, Step.Attack("hero"),
                    Step.Assert("after-2", ctx => { Facts(ctx, "r2"); return true; }))
                .Turn("orc", 3, Step.Pass())
                .Expect("Round 1: one javelin thrown", v => OneThrow(v, 1))
                .Expect("Round 2: one javelin thrown", v => OneThrow(v, 2))
                .Expect("After the first throw: one more javelin on the ground in the hero's square and the second drawn into the hand", Expect.All(
                    GroundGain("r1", 1), Fact("r1.hand", "javelin"), Fact("r1.pack", "0")))
                .Expect("After the second throw: both javelins on the ground and none left to draw", Expect.All(
                    GroundGain("r2", 2), Fact("r2.hand", "none"), Fact("r2.pack", "0")))
                .Build();
        }

        /// <summary>The javelins on the hero's square at <paramref name="key"/> minus those before the first throw (ground items outlive a job in one Play session).</summary>
        private static Func<TraceView, ExpectResult> GroundGain(string key, int want)
            => v =>
            {
                int before = NoteInt(v, "r0.ground"), now = NoteInt(v, key + ".ground");
                if (before < 0 || now < 0) return ExpectResult.Inconclusive("missing ground facts");
                return now - before == want
                    ? ExpectResult.Pass(key + ": " + (now - before) + " javelin(s) landed")
                    : ExpectResult.Fail(key + ": " + (now - before) + " javelin(s) landed, expected " + want);
            };

        private static int NoteInt(TraceView v, string key)
        {
            string prefix = NotePrefix + key + "=";
            TraceEvent note = v.Of("note").LastOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
            return note != null && int.TryParse(note.Str("text").Substring(prefix.Length), out int n) ? n : -1;
        }

        private static ExpectResult OneThrow(TraceView v, int round)
        {
            List<TraceEvent> throws = v.Attacks("orc", "hero", null, round);
            return throws.Count == 1 && (throws[0].Str("weapon") ?? "").IndexOf("Javelin", StringComparison.OrdinalIgnoreCase) >= 0
                ? ExpectResult.Pass("one " + throws[0].Str("weapon"), throws[0].Seq)
                : ExpectResult.Fail(throws.Count + " attacks: " + string.Join(",", throws.Select(e => e.Str("weapon"))), throws.Select(e => e.Seq).ToArray());
        }

        private static void Facts(ScenarioContext ctx, string prefix)
        {
            CharacterController orc = ctx.Get("orc");
            CharacterController hero = ctx.Get("hero");
            global::Inventory inv = Inv(orc);
            SquareGrid grid = SquareGrid.Instance;
            SquareCell cell = grid != null && hero != null ? grid.GetCell(hero.GridPosition) : null;
            int ground = cell != null ? cell.GroundItems.Count(i => i != null && i.Id == DND35e.Identifiers.ItemIDs.JAVELIN) : -1;
            int pack = inv != null && inv.GeneralSlots != null ? inv.GeneralSlots.Count(i => i != null && i.Id == DND35e.Identifiers.ItemIDs.JAVELIN) : -1;
            ItemData hand = inv != null ? inv.RightHandSlot : null;
            Note(ctx, prefix + ".ground", ground);
            Note(ctx, prefix + ".hand", hand == null ? "none" : hand.Id);
            Note(ctx, prefix + ".pack", pack);
        }

        // ── Combat end after reactions ──────────────────────────────────

        /// <summary>
        /// The hook's combat-end check for the attacker (CORE-011): a wizard 7 casts Fire Shield (warm, PHB p.230) on itself;
        /// the last enemy, an orc at 1 hp, hits the wizard in melee (forced 19) and the shield's 1d6+7 fire damage drops it (dying or dead).
        /// The game declares the victory from the post-attack hook of the orc's attack.
        /// </summary>
        private static ScenarioDef FireShieldKillsAttackerEndsCombat()
        {
            return Rules("rules/fire-shield-kills-attacker-ends-combat", "A Fire Shield that kills the attacker ends the fight from that attack's post-attack hook (PHB p.230; CORE-011)")
                .Covers("CORE-011", "PHB p.230")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Shield Wizard", 7)), 10, 10, Control.Ui)
                .Npc("orc", "orc_warrior", 11, 10, Control.Scripted)
                .Tweak("wizard", c => { AddWizardSlot(c, 4, SpellNames.FIRE_SHIELD); Sturdy(c); })
                .Tweak("orc", c => c.Stats.CanMakeAttacksOfOpportunity = false)
                .Hp("orc", 1)
                .Force(20, 19, null, -1)
                .Initiative("wizard", "orc")
                .Turn("wizard", 1, Step.CastChoosing(SpellNames.FIRE_SHIELD, "wizard", "Warm"), Step.EndTurn())
                .Turn("orc", 1, Step.Attack("wizard"))
                .Expect("The wizard's Fire Shield cast is done", Expect.StepStatus("wizard", 1, "Cast", 0, "done"))
                .Expect("The game declares the victory itself", Expect.Outcome(Outcome.Victory))
                .Expect("The orc's hit comes before the end of the fight", v =>
                {
                    TraceEvent hit = v.Attacks("orc", "wizard").FirstOrDefault(e => e.Bool("hit"));
                    TraceEvent over = v.Of("phase").FirstOrDefault(e => e.Get("to") is GameManager.TurnPhase t && t == GameManager.TurnPhase.CombatOver);
                    if (hit == null) return ExpectResult.Fail("the orc never hit");
                    if (over == null) return ExpectResult.Fail("no CombatOver");
                    return v.Before(hit.Seq, over.Seq) ? ExpectResult.Pass("hit #" + hit.Seq + ", CombatOver #" + over.Seq) : ExpectResult.Fail("CombatOver before the hit", over.Seq);
                })
                .Expect("The victory is declared by the post-attack hook of the orc's attack (OnAttackResolved), not at a turn boundary", v =>
                {
                    TraceEvent over = v.Of("phase").FirstOrDefault(e => e.Get("to") is GameManager.TurnPhase t && t == GameManager.TurnPhase.CombatOver);
                    if (over == null) return ExpectResult.Fail("no CombatOver");
                    string text = over.Get("by") is List<string> by ? string.Join(" < ", by) : "";
                    return text.IndexOf("OnAttackResolved", StringComparison.Ordinal) >= 0
                        ? ExpectResult.Pass(text, over.Seq)
                        : ExpectResult.Fail("by " + text, over.Seq);
                })
                .Expect("The shield's damage drops the orc below 0 hp: dying or dead, out of the fight (PHB p.145)", v =>
                {
                    JsonObj orc = v.Final("orc");
                    return orc != null && orc.Get("hp") is int hp && hp < 0
                        ? ExpectResult.Pass("orc " + hp + "/" + orc.Get("st"))
                        : ExpectResult.Fail("orc " + (orc != null ? orc.Get("hp") + "/" + orc.Get("st") : "missing"));
                })
                .Build();
        }

        /// <summary>A fighter 4 of the given race (a halfling is Small, PHB p.19), as RulesScenarios builds one.</summary>
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

        /// <summary>A Human wizard: STR 10, DEX 12, CON 12, INT 16 (+3); Wizard kit.</summary>
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

        private static void AddWizardSlot(CharacterController c, int level, string spellId)
        {
            SpellcastingComponent sc = c.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no spellcasting to add a slot to");
            var slot = new SpellSlot(level, false, "Wizard");
            slot.PrepareWithMetamagic(SpellDatabase.GetSpell(spellId).Clone(), null);
            sc.SpellSlots.Add(slot);
            sc.SyncPreparedSpellsFromSlots();
        }
    }
}
#endif
