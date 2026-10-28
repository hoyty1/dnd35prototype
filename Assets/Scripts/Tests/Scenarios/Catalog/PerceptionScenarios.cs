#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpellNames = DND35e.Identifiers.SpellNames;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 12 step 8 (docs/designs/pc_npc_parity_batch12.md, CMB-094): every combatant, PC or NPC, has a
    /// LastKnownPositionTracker from slot init and makes the same turn-start perception check
    /// (PerceptionRules.ProcessTurnStart, called by GameManager.BeginActingTurn): it records the squares of the enemies it
    /// sees and makes one Listen check to pinpoint the unseen ones it tracks (DMG p.295). A pinpointed creature can be
    /// attacked in its square, with total concealment's 50% miss chance (PHB p.152); a PC's ranged target list and click
    /// accept it. Before the step only the NPC path ran the check, a PC had no tracker, a PC archer could not target an
    /// invisible creature at all, and an NPC whose first turn came after an enemy turned invisible did not track it.
    /// Every d20 is 19 and the observer has a +10 Listen fixture, so each Listen check (19 + 10 against DC 20) pinpoints;
    /// every d% is 40, inside the 50% miss chance.
    /// </summary>
    public static class PerceptionScenarios
    {
        private const string NotePrefix = "b12s8 ";

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("invisible-pinpoint-ui", InvisiblePinpointUi);
            yield return S("invisible-pinpoint-ai", InvisiblePinpointAi);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("PerceptionScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        /// <summary>Test fixture: +10 on Listen checks, so a forced d20 of 19 beats the pinpoint DC of 20.</summary>
        private static void KeenEars(CharacterController c)
            => c.Stats.Bonuses.Set("scenario_listen", BonusTarget.Skill, BonusType.Circumstance, 10, "Scenario fixture", "Listen");

        /// <summary>Test fixture: the same key set to -30, so a forced d20 of 19 fails the pinpoint DC of 20.</summary>
        private static void DullEars(CharacterController c)
            => c.Stats.Bonuses.Set("scenario_listen", BonusTarget.Skill, BonusType.Circumstance, -30, "Scenario fixture", "Listen");

        /// <summary>The combat log's Listen-check lead line (LastKnownPositionTracker.AttemptListenChecks).</summary>
        private const string ListenLine = "makes Listen check to pinpoint concealed enemies";

        /// <summary>Makes <paramref name="c"/> invisible with the tracked effect a cast would add.</summary>
        private static bool MakeInvisible(CharacterController c)
        {
            ActiveSpellEffect effect = c.StatusEffectManager != null
                ? c.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.INVISIBILITY).Clone(), c.Stats.CharacterName, 10)
                : null;
            if (effect != null)
                c.ApplyInvisibilityEffect(effect.RemainingRounds, c, isMoving: false);
            return c.HasActiveInvisibilityEffect;
        }

        /// <summary>Replaces the Fighter kit's weapons with a longbow and gives it arrows.</summary>
        private static void Archer(CharacterController c)
        {
            RulesScenarios.StripOffHand(c);
            InventoryComponent inv = c.GetComponent<InventoryComponent>();
            if (inv == null || inv.CharacterInventory == null)
                throw new InvalidOperationException(c.Stats.CharacterName + " has no inventory");
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponLongbow), EquipSlot.RightHand);
            if (!inv.CharacterInventory.AddItem(ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.AmmoArrow)))
                throw new InvalidOperationException(c.Stats.CharacterName + " cannot carry arrows");
            inv.CharacterInventory.RecalculateStats();
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

        /// <summary>
        /// The first attack by <paramref name="by"/> on <paramref name="target"/> was rolled (a d20 of 19, not the
        /// empty-square auto-miss, which rolls nothing) and missed on the 50% miss chance (d% 40).
        /// </summary>
        private static Func<TraceView, ExpectResult> RolledIntoConcealment(string by, string target, bool ranged, int? round = null)
            => v =>
            {
                List<TraceEvent> attacks = v.Attacks(by, target, false, round);
                if (attacks.Count == 0)
                    return ExpectResult.Fail("no attack by " + by + " on " + target);
                TraceEvent a = attacks[0];
                string detail = "die " + a.Get("die") + ", hit " + a.Bool("hit") + ", conceal " + a.Bool("conceal") + ", ranged " + a.Bool("ranged");
                return Convert.ToInt32(a.Get("die")) == 19 && a.Bool("conceal") && !a.Bool("hit") && a.Bool("ranged") == ranged
                    ? ExpectResult.Pass(detail, a.Seq)
                    : ExpectResult.Fail(detail + "; expected a rolled " + (ranged ? "ranged" : "melee") + " attack of 19 lost to the 50% miss chance", a.Seq);
            };

        // ── rules/invisible-pinpoint-ui ─────────────────────────────────

        /// <summary>
        /// A Ui fighter 5 with a longbow sees a Scripted goblin 25 ft away at its round-1 turn start, so its
        /// tracker records the goblin's square. On its turn the goblin turns invisible and takes a 5-foot step. At the
        /// fighter's round-2 turn start the shared perception check (DMG p.295) pinpoints the goblin by Listen; the PC
        /// ranged target list and the click accept the pinpointed goblin, and the arrow is rolled against its current square
        /// and lost to the 50% miss chance (PHB p.152). Before batch 12 step 8 the PC had no tracker and no Listen check,
        /// and the ranged click on an unseen creature did nothing.
        /// </summary>
        private static ScenarioDef InvisiblePinpointUi()
        {
            return Rules("rules/invisible-pinpoint-ui", "A PC pinpoints an invisible creature by Listen at turn start and shoots it with a 50% miss chance (DMG p.295; CMB-094)")
                .Covers("CMB-094", "DMG p.295", "PHB p.152", "PC_NPC_PARITY", "batch 12 step 8")
                .MaxRounds(2)
                .Pc("fighter", ActorSource.Stats(() => RulesScenarios.Fighter("Pinpoint Archer", 5)), 5, 10, Control.Ui)
                .Npc("goblin", "goblin", 10, 10, Control.Scripted)
                .Tweak("fighter", c => { Archer(c); KeenEars(c); Sturdy(c); })
                .Tweak("goblin", Sturdy)
                .Initiative("fighter", "goblin")
                .Force(20, 19, null, -1)
                .Force(100, 40, null, -1)
                .Turn("fighter", 1,
                    Step.Assert("tracked", ctx =>
                    {
                        CharacterController f = ctx.Get("fighter");
                        LastKnownPositionTracker t = f.GetComponent<LastKnownPositionTracker>();
                        Note(ctx, "tracked.r1", t != null && t.HasLastKnownPosition(ctx.Get("goblin")));
                        return true;
                    }),
                    Step.EndTurn())
                .Turn("goblin", 1,
                    Step.Assert("invisible", ctx => { Note(ctx, "invisible", MakeInvisible(ctx.Get("goblin"))); return true; }),
                    Step.FiveFootStep(10, 11),
                    Step.Assert("moved", ctx =>
                    {
                        CharacterController f = ctx.Get("fighter");
                        CharacterController g = ctx.Get("goblin");
                        LastKnownPositionTracker t = f.GetComponent<LastKnownPositionTracker>();
                        Vector2Int? last = t != null ? t.GetLastKnownPosition(g) : null;
                        Note(ctx, "moved", last.HasValue && last.Value != g.GridPosition);
                        return true;
                    }),
                    Step.Pass())
                .Turn("fighter", 2,
                    Step.Assert("pinpointed", ctx =>
                    {
                        CharacterController f = ctx.Get("fighter");
                        CharacterController g = ctx.Get("goblin");
                        Note(ctx, "seen.r2", f.CanSee(g, incomingIsRangedAttack: true));
                        Note(ctx, "pinpointed.r2", f.HasPinpointed(g));
                        return true;
                    }),
                    Step.Attack("goblin"),
                    Step.EndTurn())
                .Turn("goblin", 2,
                    Step.FiveFootStep(10, 12),
                    Step.Assert("lapsed", ctx => { Note(ctx, "pinpointed.after-move", ctx.Get("fighter").HasPinpointed(ctx.Get("goblin"))); return true; }),
                    Step.Pass())
                .Expect("Round 1: the fighter's turn-start check records the visible goblin's square (every combatant has a tracker)", Fact("tracked.r1", "True"))
                .Expect("The goblin is invisible and has left the square the fighter last saw it in",
                    Expect.All(Fact("invisible", "True"), Fact("seen.r2", "False"), Fact("moved", "True")))
                .Expect("Round 2: the fighter's turn-start Listen check pinpoints the goblin (DMG p.295)", Fact("pinpointed.r2", "True"))
                .Expect("The PC ranged click accepts the pinpointed goblin", Expect.StepStatus("fighter", 2, "Attack", 0, "done"))
                .Expect("The arrow is rolled against the goblin's square and lost to the 50% miss chance, not an empty-square auto-miss (PHB p.152)",
                    RolledIntoConcealment("fighter", "goblin", ranged: true))
                .Expect("The pinpoint lapses once the goblin leaves the pinpointed square (DMG p.295: a creature that moves is again unlocated)",
                    Fact("pinpointed.after-move", "False"))
                .Build();
        }

        // ── rules/invisible-pinpoint-ai ─────────────────────────────────

        /// <summary>
        /// A Ui wizard 3 casts Invisibility on itself before an AI goblin's first turn (the cast records the wizard's square
        /// in every enemy's tracker, which the goblin has from slot init) and takes a 5-foot step. At the goblin's turn start
        /// the shared perception check pinpoints the wizard by Listen (DMG p.295), and the goblin attacks the wizard's
        /// current square with the 50% miss chance (PHB p.152). In round 2 the goblin's Listen check fails (a -30 fixture),
        /// the wizard has not moved, and the goblin attacks the square it pinpointed: rolled, not an auto-miss. The goblin
        /// makes one Listen check per turn, the turn-start one. Before batch 12 step 8 the goblin had no tracker until its
        /// first turn, so the cast recorded nothing and the goblin could not find the wizard; its target choice rolled Listen
        /// again in the turn (AI-007); and a Listen pinpoint did not update the last-known square.
        /// </summary>
        private static ScenarioDef InvisiblePinpointAi()
        {
            return Rules("rules/invisible-pinpoint-ai", "An AI creature pinpoints an invisible PC by Listen at turn start and attacks its square with a 50% miss chance (DMG p.295; CMB-094)")
                .Covers("CMB-094", "DMG p.295", "PHB p.152", "PC_NPC_PARITY", "batch 12 step 8")
                .MaxRounds(2)
                .Pc("wizard", ActorSource.Stats(() => Wizard("Unseen Wizard", 3)), 5, 10, Control.Ui)
                .Npc("goblin", "goblin", 8, 10, Control.Ai)
                .Tweak("wizard", c => { Prepare(c, 2, SpellNames.INVISIBILITY); Sturdy(c); })
                .Tweak("goblin", c => { KeenEars(c); Sturdy(c); })
                .Initiative("wizard", "goblin")
                .Force(20, 19, null, -1)
                .Force(100, 40, null, -1)
                .Turn("wizard", 1,
                    Step.Cast(SpellNames.INVISIBILITY, "wizard"),
                    Step.FiveFootStep(5, 11),
                    Step.Assert("unseen", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        CharacterController g = ctx.Get("goblin");
                        LastKnownPositionTracker t = g.GetComponent<LastKnownPositionTracker>();
                        Vector2Int? last = t != null ? t.GetLastKnownPosition(w) : null;
                        Note(ctx, "invisible", w.HasActiveInvisibilityEffect);
                        Note(ctx, "tracked", last.HasValue);
                        Note(ctx, "moved", last.HasValue && last.Value != w.GridPosition);
                        return true;
                    }),
                    Step.EndTurn())
                .Turn("wizard", 2,
                    Step.Assert("pinpointed", ctx =>
                    {
                        CharacterController w = ctx.Get("wizard");
                        CharacterController g = ctx.Get("goblin");
                        LastKnownPositionTracker t = g.GetComponent<LastKnownPositionTracker>();
                        Vector2Int? last = t != null ? t.GetLastKnownPosition(w) : null;
                        Note(ctx, "pinpointed.r1", g.HasPinpointed(w));
                        Note(ctx, "lastknown.is-pinpointed-square", last.HasValue && last.Value == w.GridPosition);
                        // The goblin's next Listen check fails (19 - 30 + its own bonus against DC 20); the wizard stays put.
                        DullEars(g);
                        // Test fixture: no maneuver or grapple in place of the attack (CMB-102), so round 2 is a plain weapon attack.
                        if (g.aiProfile != null && g.aiProfile.Maneuvers != null)
                        {
                            g.aiProfile.Maneuvers.AttemptTrip = false;
                            g.aiProfile.Maneuvers.AttemptDisarm = false;
                            g.aiProfile.Maneuvers.AttemptSunder = false;
                            g.aiProfile.Maneuvers.AttemptBullRush = false;
                            g.aiProfile.Maneuvers.AttemptOverrun = false;
                            g.aiProfile.GrappleBehavior = DND35.AI.GrappleBehavior.Avoid;
                        }
                        return true;
                    }),
                    Step.EndTurn())
                .Expect("The wizard is invisible; the goblin, which has not acted yet, tracks the square it vanished in, and the wizard has left it",
                    Expect.All(Fact("invisible", "True"), Fact("tracked", "True"), Fact("moved", "True")))
                .Expect("The goblin pinpoints the wizard (DMG p.295), and the pinpointed square becomes its last-known square",
                    Expect.All(Fact("pinpointed.r1", "True"), Fact("lastknown.is-pinpointed-square", "True")))
                .Expect("Round 1: the goblin makes exactly one Listen check, the turn-start one, before its first attack (DMG p.295: one check per round; AI-007)",
                    v =>
                    {
                        List<TraceEvent> listens = v.Log(ListenLine).Where(e => e.Round == 1).ToList();
                        List<TraceEvent> attacks = v.Attacks("goblin", "wizard", false, 1);
                        if (listens.Count != 1)
                            return ExpectResult.Fail(listens.Count + " Listen checks in round 1, expected 1");
                        if (attacks.Count == 0)
                            return ExpectResult.Fail("the goblin made no attack in round 1");
                        return v.Before(listens[0].Seq, attacks[0].Seq)
                            ? ExpectResult.Pass("one Listen check, before the attack", listens[0].Seq)
                            : ExpectResult.Fail("the Listen check came after the attack", listens[0].Seq);
                    })
                .Expect("Round 2: the goblin's one Listen check fails, and its attack on the wizard, still in the pinpointed square, is rolled into the 50% miss chance, not an empty-square auto-miss (PHB p.152)",
                    v =>
                    {
                        int listens = v.Log(ListenLine).Count(e => e.Round == 2);
                        if (listens != 1)
                            return ExpectResult.Fail(listens + " Listen checks in round 2, expected 1");
                        List<TraceEvent> attacks = v.Attacks("goblin", "wizard", false, 2);
                        return attacks.Count > 0
                            ? RolledIntoConcealment("goblin", "wizard", attacks[0].Bool("ranged"), 2)(v)
                            : ExpectResult.Fail("the goblin made no attack on the wizard in round 2");
                    })
                .Expect("The goblin's attack is rolled against the wizard's square and lost to the 50% miss chance, not an empty-square auto-miss (PHB p.152)",
                    v =>
                    {
                        List<TraceEvent> attacks = v.Attacks("goblin", "wizard", false);
                        return attacks.Count > 0
                            ? RolledIntoConcealment("goblin", "wizard", attacks[0].Bool("ranged"))(v)
                            : ExpectResult.Fail("the goblin made no attack on the wizard");
                    })
                .Build();
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

        /// <summary>Puts a clone of <paramref name="spellId"/> into the first unused regular slot of <paramref name="level"/>.</summary>
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
                sc.SyncPreparedSpellsFromSlots();
                return;
            }
            throw new InvalidOperationException(c.Stats.CharacterName + " has no regular level-" + level + " slot for " + spellId);
        }
    }
}
#endif
