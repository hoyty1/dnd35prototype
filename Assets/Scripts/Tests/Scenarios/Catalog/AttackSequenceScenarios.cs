#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 12 step 17 (docs/designs/pc_npc_parity_batch12.md; CMB-091, CMB-045, CMB-096, CMB-173; decision D9): every
    /// attack and full attack runs through the one attack-sequence executor (GameManager.RunAttackSequence), PC and NPC,
    /// melee, ranged and thrown alike. A ranged full attack makes every step of the ladder on the PC's Full Attack button
    /// and on the AI path, each shot provoking from each threatening foe (PHB p.141) and spending its arrow (PHB p.113); a
    /// crossbow full attack fires again only when Rapid Reload reloads it as a free action (PHB p.116, p.99); Rapid Shot is
    /// declared before the first attack and commits the turn to the full attack (PHB p.99, p.143).
    /// </summary>
    public static class AttackSequenceScenarios
    {
        private const string NotePrefix = "b12s17 ";

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("ranged-full-attack-ui", RangedFullAttackUi);
            yield return S("ranged-full-attack-ai", RangedFullAttackAi);
            yield return S("crossbow-full-attack-reload", CrossbowFullAttackReload);
            yield return S("rapid-shot-declared-first-ui", RapidShotDeclaredFirstUi);
            yield return S("npc-throws-dagger", NpcThrowsDagger);
            yield return S("twf-full-attack-ai", TwfFullAttackAi);
            yield return S("rapid-shot-ai", RapidShotAi);
        }

        /// <summary>
        /// CMB-096 (PHB p.113): an orc holding a dagger, with a second one in its pack, attacks a sturdy hero 20 ft away, beyond
        /// its reach and within the dagger's thrown range. Its intent is its own (AttackIntent.ChooseAiMode: Thrown), never the PC
        /// UI's selection: it throws the dagger as a ranged attack, the dagger lands in the hero's square and the second is
        /// drawn. Before the fix NPCs could not throw melee weapons.
        /// </summary>
        private static ScenarioDef NpcThrowsDagger()
        {
            return Rules("rules/npc-throws-dagger", "An NPC throws a dagger at a foe beyond its reach, and the next is drawn (PHB p.113; CMB-096)")
                .Covers("CMB-096", "PHB p.113")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Idle)
                .Npc("orc", "orc_warrior", 14, 10, Control.Scripted)
                .Tweak("hero", Sturdy)
                .Tweak("orc", c =>
                {
                    Arm(c, ItemID.WeaponDagger, null, 0);
                    Inv(c).AddItem(ItemDatabase.CloneItem(ItemID.WeaponDagger));
                    Inv(c).RecalculateStats();
                })
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Attack("hero"),
                    Step.Assert("after", ctx =>
                    {
                        CharacterController orc = ctx.Get("orc");
                        CharacterController hero = ctx.Get("hero");
                        ItemData hand = orc.GetEquippedMainWeapon();
                        SquareCell cell = SquareGrid.Instance != null ? SquareGrid.Instance.GetCell(hero.GridPosition) : null;
                        Note(ctx, "hand", hand != null ? hand.Id : "none");
                        Note(ctx, "ground", cell != null ? cell.GroundItems.Count(i => i != null && i.Id == ItemIDs.DAGGER) : -1);
                        return true;
                    }))
                .Expect("The orc makes one ranged attack with the dagger", v =>
                {
                    List<TraceEvent> throws = v.Attacks("orc", "hero", false, 1);
                    return throws.Count == 1 && throws[0].Bool("ranged") && (throws[0].Str("weapon") ?? "").IndexOf("Dagger", StringComparison.OrdinalIgnoreCase) >= 0
                        ? ExpectResult.Pass("one thrown " + throws[0].Str("weapon"), throws[0].Seq)
                        : ExpectResult.Fail(throws.Count + " attacks: " + string.Join(",", throws.Select(e => e.Str("weapon") + (e.Bool("ranged") ? " (ranged)" : " (melee)"))), throws.Select(e => e.Seq).ToArray());
                })
                .Expect("The thrown dagger lies in the hero's square and the second is in the orc's hand", Expect.All(
                    v => NoteValue(v, "ground") >= 1 ? ExpectResult.Pass("ground daggers " + NoteValue(v, "ground")) : ExpectResult.Fail("ground daggers " + NoteValue(v, "ground")),
                    Fact("hand", ItemIDs.DAGGER)))
                .Build();
        }

        // ── AI attack modes (batch 12 step 18, AI-055) ──────────────────

        /// <summary>
        /// AI-055 (batch 12 step 18; PHB p.160, p.96; owner decision Q2, 2026-10-23): an AI-run fighter 6 with Two-Weapon
        /// Fighting and Improved Two-Weapon Fighting, a longsword and a short sword in the off hand (light: -2/-2), beside a
        /// sturdy dummy (its maneuvers switched off before its turn, a fixture: the profile trips any standing foe, AI-035;
        /// the dummy acts first, and its round-2 turn reads the fighter's round-1 attack pool). The AI stopgap's expected-damage rule (AIProfile.DefaultShouldUseTwoWeaponFighting) chooses
        /// two-weapon fighting, and the one executor makes both hands' steps in BAB order: main +6, off +6, main +1, off +1
        /// (each at -2), so four attacks alternating the weapons, the second pair 5 lower. Before the fix no NPC made an
        /// off-hand attack.
        /// </summary>
        private static ScenarioDef TwfFullAttackAi()
        {
            return Rules("rules/twf-full-attack-ai", "An AI fighter with two weapons full attacks with both, in BAB order (PHB p.160; AI-055)")
                .Covers("AI-055", "PHB p.160", "PHB p.96", "batch 12 step 18")
                .MaxRounds(2)
                .Pc("fighter", ActorSource.Stats(() => RulesScenarios.Fighter("Twin", 6, "Two-Weapon Fighting", "Improved Two-Weapon Fighting")), 10, 10, Control.Ai)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("fighter", c =>
                {
                    Arm(c, ItemID.WeaponLongsword, null, 0);
                    Inv(c).LeftHandSlot = ItemDatabase.CloneItem(ItemID.WeaponShortsword);
                    Inv(c).RecalculateStats();
                })
                .Tweak("dummy", Sturdy)
                .Initiative("dummy", "fighter")
                // Test fixture: no maneuver or grapple in place of an attack (CMB-102; AI-035 trips every standing foe), so the
                // fighter's round-1 steps are weapon attacks.
                .Turn("dummy", 1, Step.Assert("no maneuvers", ctx =>
                {
                    CharacterController f = ctx.Get("fighter");
                    if (f.aiProfile != null && f.aiProfile.Maneuvers != null)
                    {
                        f.aiProfile.Maneuvers.AttemptTrip = false;
                        f.aiProfile.Maneuvers.AttemptDisarm = false;
                        f.aiProfile.Maneuvers.AttemptSunder = false;
                        f.aiProfile.Maneuvers.AttemptBullRush = false;
                        f.aiProfile.Maneuvers.AttemptOverrun = false;
                        f.aiProfile.GrappleBehavior = DND35.AI.GrappleBehavior.Avoid;
                    }
                    Note(ctx, "profile", f.aiProfile != null);
                    return true;
                }))
                .Turn("dummy", 2, Step.Assert("after", ctx =>
                {
                    CharacterController f = ctx.Get("fighter");
                    Note(ctx, "offhand", f.ProgressiveAttackPool.OffHandStepsUsed);
                    Note(ctx, "full", f.ProgressiveAttackPool.IsFullAttack);
                    return true;
                }))
                .Expect("The fighter's turn is an AI turn", Expect.Controller("fighter", "ai"))
                .Expect("Four attacks: main, off, main, off; each pair at one bonus, the second pair 5 lower (PHB p.160, p.96)", v =>
                {
                    List<TraceEvent> a = v.Attacks("fighter", "dummy", false, 1);
                    string seq = string.Join(",", a.Select(e => e.Str("weapon") + " " + e.Int("mod")));
                    int[] seqs = a.Select(e => e.Seq).ToArray();
                    bool ok = a.Count == 4
                              && a[0].Str("weapon") == a[2].Str("weapon") && a[1].Str("weapon") == a[3].Str("weapon")
                              && a[0].Str("weapon") != a[1].Str("weapon")
                              && a[0].Int("mod") == a[1].Int("mod") && a[0].Int("mod") - a[2].Int("mod") == 5 && a[1].Int("mod") - a[3].Int("mod") == 5;
                    return ok ? ExpectResult.Pass(seq, seqs) : ExpectResult.Fail(seq, seqs);
                })
                .Expect("Two off-hand steps in a full attack", Expect.All(Fact("profile", "True"), Fact("offhand", "2"), Fact("full", "True")))
                .Build();
        }

        /// <summary>
        /// AI-055 (batch 12 step 18; PHB p.99; D9): an NPC-turn fighter 6 with Point Blank Shot and Rapid Shot, a longbow and
        /// 20 arrows, its Rapid Shot toggle off, attacks a sturdy dummy 30 ft away (a scripted attack: the turn path's own
        /// choices). The AI stopgap's expected-damage rule (AIProfile.DefaultShouldUseRapidShot) declares Rapid Shot before
        /// the first shot, which makes the attack a full attack of three shots: +6, +6 (Rapid Shot's), +1, each at -2, three
        /// arrows. Before the fix only the PC toggle could declare it.
        /// </summary>
        private static ScenarioDef RapidShotAi()
        {
            return Rules("rules/rapid-shot-ai", "The AI declares Rapid Shot by its expected-damage rule: three shots, a full attack (PHB p.99; AI-055)")
                .Covers("AI-055", "D9", "PHB p.99", "PHB p.143", "batch 12 step 18")
                .MaxRounds(1)
                .Pc("archer", ActorSource.Stats(() => RulesScenarios.Fighter("Archer", 6, "Point Blank Shot", "Rapid Shot")), 8, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 14, 10, Control.Idle)
                .Tweak("archer", c => { Arm(c, ItemID.WeaponLongbow, ItemID.AmmoArrow, 20); c.SetRapidShot(false); })
                .Tweak("dummy", Sturdy)
                .Initiative("archer", "dummy")
                .Turn("archer", 1,
                    Step.Attack("dummy"),
                    Step.Assert("after", ctx =>
                    {
                        CharacterController a = ctx.Get("archer");
                        Note(ctx, "declared", a.IsRapidShotDeclared);
                        Note(ctx, "toggle", a.RapidShotEnabled);
                        Note(ctx, "full", a.ProgressiveAttackPool.IsFullAttack);
                        Note(ctx, "arrows", Inv(a).GetTotalAmmoCount(AmmunitionType.Arrow));
                        return true;
                    }))
                .Expect("Three shots: +6, +6 (Rapid Shot's), then +1 (PHB p.99)", Shots("archer", 1, 3, 0, 5))
                .Expect("The AI declared Rapid Shot with its toggle off; the turn is a full attack; three arrows spent", Expect.All(
                    Fact("declared", "True"), Fact("toggle", "False"), Fact("full", "True"), Fact("arrows", "17")))
                .Build();
        }

        private static int NoteValue(TraceView v, string key)
        {
            string prefix = NotePrefix + key + "=";
            TraceEvent note = v.Of("note").LastOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
            return note != null && int.TryParse(note.Str("text").Substring(prefix.Length), out int n) ? n : -1;
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("AttackSequenceScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title)
            => Scenario.Define(id, title).Tags("rules", "attack-sequence").Covers("PC_NPC_PARITY", "batch 12 step 17");

        // ── Helpers ─────────────────────────────────────────────────────

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        private static global::Inventory Inv(CharacterController c)
            => c != null && c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;

        /// <summary>Empties both hands and the pack's weapons and ammunition, then puts <paramref name="weapon"/> in the right hand and <paramref name="ammoCount"/> pieces of <paramref name="ammo"/> in the pack.</summary>
        private static void Arm(CharacterController c, ItemID weapon, ItemID? ammo, int ammoCount)
        {
            global::Inventory inv = Inv(c);
            if (inv == null)
                return;
            if (inv.GeneralSlots != null)
                for (int i = 0; i < inv.GeneralSlots.Length; i++)
                    if (inv.GeneralSlots[i] != null && (inv.GeneralSlots[i].IsWeapon || inv.GeneralSlots[i].IsAmmunition))
                        inv.GeneralSlots[i] = null;
            inv.RightHandSlot = ItemDatabase.CloneItem(weapon);
            inv.LeftHandSlot = null;
            inv.HandsSlot = null;
            if (ammo.HasValue)
            {
                ItemData pieces = ItemDatabase.CloneItem(ammo.Value);
                pieces.Quantity = ammoCount;
                inv.AddItem(pieces);
            }
            inv.RecalculateStats();
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

        /// <summary><paramref name="by"/>'s attacks (not AoOs) in <paramref name="round"/>: exactly <paramref name="count"/>, each 5 below the one before from the second step on (<paramref name="diffs"/>: the drop from each attack to the next).</summary>
        private static Func<TraceView, ExpectResult> Shots(string by, int round, int count, params int[] diffs)
            => v =>
            {
                List<TraceEvent> shots = v.Attacks(by, null, false, round);
                int[] mods = shots.Select(e => e.Int("mod")).ToArray();
                int[] seqs = shots.Select(e => e.Seq).ToArray();
                if (shots.Count != count)
                    return ExpectResult.Fail(shots.Count + " attacks, mods [" + string.Join(",", mods) + "], expected " + count, seqs);
                for (int i = 0; i + 1 < mods.Length && i < diffs.Length; i++)
                    if (mods[i] - mods[i + 1] != diffs[i])
                        return ExpectResult.Fail("mods [" + string.Join(",", mods) + "]: step " + (i + 2) + " is " + (mods[i] - mods[i + 1]) + " below, expected " + diffs[i], seqs);
                return ExpectResult.Pass(count + " attacks, mods [" + string.Join(",", mods) + "]", seqs);
            };

        // ── Ranged full attacks (CMB-091, CMB-173) ──────────────────────

        /// <summary>
        /// CMB-173, CMB-091 (origin C): a Ui fighter 6 with a longbow and 20 arrows presses Full Attack at a sturdy dummy 30 ft
        /// away. The button runs the one executor: two shots at BAB +6 and +1 (PHB p.143), one arrow each (PHB p.113), and
        /// the full-round action is spent.
        /// </summary>
        private static ScenarioDef RangedFullAttackUi()
        {
            return Rules("rules/ranged-full-attack-ui", "The PC Full Attack button makes a bow's iterative shots, one arrow each (PHB p.113, p.143; CMB-091, CMB-173)")
                .Covers("CMB-091", "CMB-173", "PHB p.113", "PHB p.143")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6)), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 16, 10, Control.Idle)
                .Tweak("hero", c => Arm(c, ItemID.WeaponLongbow, ItemID.AmmoArrow, 20))
                .Tweak("dummy", Sturdy)
                .Initiative("hero", "dummy")
                .Turn("hero", 1,
                    Step.FullAttack("dummy"),
                    Step.Assert("after", ctx =>
                    {
                        CharacterController hero = ctx.Get("hero");
                        Note(ctx, "arrows", Inv(hero).GetTotalAmmoCount(AmmunitionType.Arrow));
                        Note(ctx, "fullround", hero.ProgressiveAttackPool.IsFullAttack && !hero.Actions.HasMoveAction);
                        return true;
                    }))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("Two shots, the second 5 lower (BAB +6/+1, PHB p.143)", Shots("hero", 1, 2, 5))
                .Expect("Each shot spent one arrow (20 to 18, PHB p.113)", Fact("arrows", "18"))
                .Expect("The Full Attack button spent the full-round action", Fact("fullround", "True"))
                .Build();
        }

        /// <summary>
        /// CMB-091, CMB-045 (origins G and F alike): an NPC archer (an orc with BAB +6, a longbow and 20 arrows) full attacks a
        /// sturdy hero 30 ft away while a guard with Combat Reflexes (DEX 12: two AoOs a round) stands next to it. The archer
        /// makes both iterative shots through the shared executor, and each shot provokes on its own (PHB p.141 Table 8-2):
        /// the guard makes one AoO before each shot, two in all. Before the fix the NPC ranged full attack provoked once.
        /// </summary>
        private static ScenarioDef RangedFullAttackAi()
        {
            return Rules("rules/ranged-full-attack-ai", "An NPC ranged full attack makes every shot, each provoking on its own (PHB p.141, p.143; CMB-091, CMB-045)")
                .Covers("CMB-091", "CMB-045", "PHB p.141", "PHB p.143")
                .MaxRounds(1)
                .Pc("guard", ActorSource.Stats(() => RulesScenarios.Fighter("Guard", 4, "Combat Reflexes")), 9, 10, Control.Idle)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 16, 10, Control.Idle)
                .Npc("archer", "orc_warrior", 10, 10, Control.Scripted)
                .Tweak("guard", Sturdy)
                .Tweak("hero", Sturdy)
                .Tweak("archer", c =>
                {
                    Sturdy(c);
                    c.Stats.BaseAttackBonusOverride = 6;
                    Arm(c, ItemID.WeaponLongbow, ItemID.AmmoArrow, 20);
                })
                .Initiative("guard", "archer", "hero")
                .Turn("archer", 1,
                    Step.FullAttack("hero"),
                    Step.Assert("after", ctx => { Note(ctx, "arrows", Inv(ctx.Get("archer")).GetTotalAmmoCount(AmmunitionType.Arrow)); return true; }))
                .Expect("The archer shoots twice, the second 5 lower (BAB +6/+1)", Shots("archer", 1, 2, 5))
                .Expect("Each shot spent one arrow (20 to 18)", Fact("arrows", "18"))
                .Expect("The guard makes one AoO before each shot: two ranged-attack AoOs (PHB p.141; Combat Reflexes)", v =>
                {
                    List<TraceEvent> aoos = v.AoOs("guard", "archer", "ranged");
                    List<TraceEvent> shots = v.Attacks("archer", "hero", false, 1);
                    if (aoos.Count != 2 || shots.Count != 2)
                        return ExpectResult.Fail(aoos.Count + " AoO(s), " + shots.Count + " shot(s)", aoos.Select(e => e.Seq).Concat(shots.Select(e => e.Seq)).ToArray());
                    bool interleaved = v.Before(aoos[0].Seq, shots[0].Seq) && v.Before(shots[0].Seq, aoos[1].Seq) && v.Before(aoos[1].Seq, shots[1].Seq);
                    return interleaved
                        ? ExpectResult.Pass("AoO, shot, AoO, shot", aoos[0].Seq, shots[0].Seq, aoos[1].Seq, shots[1].Seq)
                        : ExpectResult.Fail("not one AoO before each shot", aoos[0].Seq, shots[0].Seq, aoos[1].Seq, shots[1].Seq);
                })
                .Build();
        }

        /// <summary>
        /// CMB-091 (crossbow loading, PHB p.116, p.99): two NPC crossbowmen (orcs with BAB +6, a light crossbow and 20 bolts)
        /// full attack a sturdy hero. The one with Rapid Reload (Light Crossbow) reloads as a free action after each shot and
        /// makes both iterative shots; the other's crossbow is unloaded after its first shot, so the executor's weapon check
        /// stops its sequence there (reloading a light crossbow is a move action) and its move action is left.
        /// </summary>
        private static ScenarioDef CrossbowFullAttackReload()
        {
            return Rules("rules/crossbow-full-attack-reload", "A crossbow full attack fires again only when Rapid Reload reloads it for free (PHB p.99, p.116; CMB-091)")
                .Covers("CMB-091", "PHB p.99", "PHB p.116")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4)), 10, 10, Control.Idle)
                .Npc("rapid", "orc_warrior", 16, 8, Control.Scripted)
                .Npc("plain", "orc_warrior", 16, 12, Control.Scripted)
                .Tweak("hero", Sturdy)
                .Tweak("rapid", c =>
                {
                    Sturdy(c);
                    c.Stats.BaseAttackBonusOverride = 6;
                    if (!c.Stats.HasFeat("Rapid Reload (Light Crossbow)"))
                        c.Stats.Feats.Add("Rapid Reload (Light Crossbow)");
                    Arm(c, ItemID.WeaponCrossbowLight, ItemID.AmmoBolt, 20);
                })
                .Tweak("plain", c =>
                {
                    Sturdy(c);
                    c.Stats.BaseAttackBonusOverride = 6;
                    Arm(c, ItemID.WeaponCrossbowLight, ItemID.AmmoBolt, 20);
                })
                .Initiative("rapid", "plain", "hero")
                .Turn("rapid", 1, Step.FullAttack("hero"))
                .Turn("plain", 1,
                    Step.FullAttack("hero"),
                    Step.Assert("after", ctx =>
                    {
                        CharacterController plain = ctx.Get("plain");
                        ItemData bow = plain.GetEquippedMainWeapon();
                        Note(ctx, "plain.loaded", bow != null && bow.IsLoaded);
                        Note(ctx, "plain.move", plain.Actions.HasMoveAction);
                        return true;
                    }))
                .Expect("With Rapid Reload: both iterative shots (+6/+1)", Shots("rapid", 1, 2, 5))
                .Expect("Without it: one shot, then the unloaded crossbow stops the full attack", Shots("plain", 1, 1))
                .Expect("The plain crossbow is unloaded and its move action is left for the reload (PHB p.116)", Expect.All(
                    Fact("plain.loaded", "False"), Fact("plain.move", "True")))
                .Build();
        }

        // ── Rapid Shot (D9) ─────────────────────────────────────────────

        /// <summary>
        /// D9 (PHB p.99, p.143): two Ui fighters 6 with Rapid Shot, a longbow, 20 arrows and the Rapid Shot toggle on. The
        /// archer presses Attack three times (the Attack button's sequence, origin B): Rapid Shot is declared before the first
        /// shot, so the turn is a full attack of three shots (+6, +6, +1), each at -2. The mover moves first, so it has no full
        /// attack left: its one shot is made without Rapid Shot, 2 higher than the archer's first.
        /// </summary>
        private static ScenarioDef RapidShotDeclaredFirstUi()
        {
            return Rules("rules/rapid-shot-declared-first-ui", "Rapid Shot is declared before the first shot and makes the Attack button's sequence a full attack (PHB p.99, p.143; D9)")
                .Covers("CMB-091", "CMB-173", "D9", "PHB p.99", "PHB p.143")
                .MaxRounds(1)
                .Pc("archer", ActorSource.Stats(() => RulesScenarios.Fighter("Archer", 6, "Point Blank Shot", "Rapid Shot")), 8, 8, Control.Ui)
                .Pc("mover", ActorSource.Stats(() => RulesScenarios.Fighter("Mover", 6, "Point Blank Shot", "Rapid Shot")), 8, 13, Control.Ui)
                .Npc("dummy", "target_dummy", 18, 10, Control.Idle)
                .Tweak("archer", c => { Arm(c, ItemID.WeaponLongbow, ItemID.AmmoArrow, 20); c.SetRapidShot(true); })
                .Tweak("mover", c => { Arm(c, ItemID.WeaponLongbow, ItemID.AmmoArrow, 20); c.SetRapidShot(true); })
                .Tweak("dummy", Sturdy)
                .Initiative("archer", "mover", "dummy")
                .Turn("archer", 1,
                    Step.Attack("dummy"), Step.AttackAgain("dummy"), Step.AttackAgain("dummy"),
                    Step.Assert("after", ctx =>
                    {
                        CharacterController a = ctx.Get("archer");
                        Note(ctx, "archer.declared", a.IsRapidShotDeclared);
                        Note(ctx, "archer.full", a.ProgressiveAttackPool.IsFullAttack);
                        Note(ctx, "archer.arrows", Inv(a).GetTotalAmmoCount(AmmunitionType.Arrow));
                        return true;
                    }))
                .Turn("mover", 1,
                    Step.Move(9, 13), Step.Attack("dummy"),
                    Step.Assert("after", ctx => { Note(ctx, "mover.declared", ctx.Get("mover").IsRapidShotDeclared); return true; }))
                .Expect("The archer's turn is a Ui turn", Expect.Controller("archer", "ui"))
                .Expect("Three shots: +6, +6 (Rapid Shot's), then +1 (PHB p.99)", Shots("archer", 1, 3, 0, 5))
                .Expect("Rapid Shot was declared and the turn is a full attack; three arrows spent", Expect.All(
                    Fact("archer.declared", "True"), Fact("archer.full", "True"), Fact("archer.arrows", "17")))
                .Expect("After a move the mover's one shot is made without Rapid Shot: 2 above the archer's first (-2, PHB p.99)", v =>
                {
                    List<TraceEvent> archer = v.Attacks("archer", null, false, 1);
                    List<TraceEvent> mover = v.Attacks("mover", null, false, 1);
                    if (archer.Count == 0 || mover.Count != 1)
                        return ExpectResult.Fail("archer " + archer.Count + " shot(s), mover " + mover.Count);
                    int diff = mover[0].Int("mod") - archer[0].Int("mod");
                    return diff == 2
                        ? ExpectResult.Pass("mover +" + mover[0].Int("mod") + ", archer +" + archer[0].Int("mod"), mover[0].Seq, archer[0].Seq)
                        : ExpectResult.Fail("mover " + mover[0].Int("mod") + " vs archer " + archer[0].Int("mod") + ": " + diff, mover[0].Seq, archer[0].Seq);
                })
                .Expect("The mover declared no Rapid Shot", Fact("mover.declared", "False"))
                .Build();
        }
    }
}
#endif
