#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 12 step 15 (docs/designs/pc_npc_parity_batch12.md; CMB-017, CRE-040; decisions D7 and D1): Cleave and Great
    /// Cleave after every melee attack origin through the one post-attack hook (PHB p.92; GameManager.OnAttackResolved,
    /// CleaveRules), and the giant bombardier beetle's acid spray as a reaction to being attacked (MM p.285;
    /// GameManager.AttackedReactions.cs). Every d20 rolls 15 (a hit that threatens no critical with these weapons), so
    /// each fighter hits every goblin; the goblins have 1 hit point unless a scenario says otherwise, and in the Cleave
    /// scenarios every d8 rolls 8, so a hit kills a goblin outright (no dying goblin draws the next attack).
    /// </summary>
    public static class CleaveScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("cleave-full-attack-ai", CleaveFullAttackAi);
            yield return S("cleave-reach-ui", CleaveReachUi);
            yield return S("cleave-once-per-round", CleaveOncePerRound);
            yield return S("cleave-after-aoo", CleaveAfterAoO);
            yield return S("cleave-disabled-target-no-trigger", CleaveDisabledTargetNoTrigger);
            yield return S("great-cleave-chain", GreatCleaveChain);
            yield return S("cleave-two-weapon-offhand-drop", CleaveTwoWeaponOffHandDrop);
            yield return S("bombardier-spray-when-attacked", BombardierSprayWhenAttacked);
            yield return S("bombardier-spray-ends-combat", BombardierSprayEndsCombat);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("CleaveScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title)
            => Scenario.Define(id, title).Tags("rules", "cleave").Covers("PC_NPC_PARITY", "batch 12 step 15");

        // ── Helpers ─────────────────────────────────────────────────────

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        /// <summary>A goblin with 1 hit point that makes no AoOs (any hit drops it).</summary>
        private static void OneHp(CharacterController c)
        {
            c.Stats.CurrentHP = 1;
            c.Stats.CanMakeAttacksOfOpportunity = false;
        }

        /// <summary>A <see cref="OneHp"/> goblin with its own name (the Cleave menu lists the candidates by name).</summary>
        private static Action<CharacterController> OneHpNamed(string name)
            => c => { OneHp(c); c.Stats.CharacterName = name; };

        private static global::Inventory Inv(CharacterController c)
            => c != null && c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;

        /// <summary>Empties both hands and wields <paramref name="weapon"/> in the main hand.</summary>
        private static void Wield(CharacterController c, ItemData weapon)
        {
            global::Inventory inv = Inv(c);
            if (inv == null)
                return;
            inv.RightHandSlot = null;
            inv.LeftHandSlot = null;
            inv.HandsSlot = null;
            inv.DirectEquip(weapon, EquipSlot.RightHand);
            inv.RecalculateStats();
        }

        private static void Longsword(CharacterController c)
        {
            Sturdy(c);
            Wield(c, ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponLongsword));
        }

        private static void Longspear(CharacterController c)
        {
            Sturdy(c);
#pragma warning disable CS0618 // the longspear has no ItemID value; the string id is the only key
            Wield(c, ItemDatabase.CloneItem(DND35e.Identifiers.ItemIDs.LONGSPEAR));
#pragma warning restore CS0618
        }

        /// <summary>
        /// An adaptive AI profile (AIProfile.ShouldSwitchTargetsMidFullAttack: the animal profile's), so the Scripted hero's
        /// NPC-path full attack goes on against another goblin after a kill instead of stopping (an AI choice, not a rule).
        /// </summary>
        private static DND35.AI.AIProfile Adaptive() => DND35.AI.AIProfile.CreateRuntime<DND35.AI.Profiles.AnimalAIProfile>();

        private static ScenarioBuilder ForceHits(ScenarioBuilder b) => b.RecordDice().Force(20, 15, null, -1);

        /// <summary><see cref="ForceHits"/>, and every d8 rolls 8: a longsword or longspear hit (8 + 3 Strength) kills a 1-hp goblin outright.</summary>
        private static ScenarioBuilder ForceKills(ScenarioBuilder b) => ForceHits(b).Force(8, 8, null, -1);

        private static Func<TraceView, ExpectResult> CleaveLogs(int round, int want, string feat = "Cleave!")
            => Expect.Count("log", e => e.Round == round && (e.Str("text") ?? "").Contains(feat), want, want);

        private static Func<TraceView, ExpectResult> Dead(params string[] keys)
            => v =>
            {
                var alive = new List<string>();
                foreach (string k in keys)
                {
                    JsonObj snap = v.Final(k);
                    if (snap == null || !(snap.Get("hp") is int hp) || hp > -10)
                        alive.Add(k + "(" + (snap != null ? snap.Get("hp") : "?") + ")");
                }
                return alive.Count == 0 ? ExpectResult.Pass(string.Join(",", keys) + " dead") : ExpectResult.Fail("not dead: " + string.Join(",", alive));
            };

        private static Func<TraceView, ExpectResult> Mods(string key, int round, params int[] offsets)
            => v =>
            {
                List<TraceEvent> a = v.Attacks(key, null, null, round);
                if (a.Count != offsets.Length)
                    return ExpectResult.Fail(a.Count + " attacks (" + string.Join(",", a.Select(e => e.Str("target") + " " + e.Int("mod"))) + "), expected " + offsets.Length, a.Select(e => e.Seq).ToArray());
                int first = a[0].Int("mod");
                for (int i = 0; i < a.Count; i++)
                    if (a[i].Int("mod") != first + offsets[i])
                        return ExpectResult.Fail("attack " + (i + 1) + " mod " + a[i].Int("mod") + ", expected " + (first + offsets[i]), a[i].Seq);
                return ExpectResult.Pass("mods " + string.Join(",", a.Select(e => e.Int("mod"))), a.Select(e => e.Seq).ToArray());
            };

        // ── Cleave (D7) ─────────────────────────────────────────────────

        /// <summary>
        /// CMB-017 (origin F, the NPC melee sequence, AI decider): a Scripted fighter 6 (BAB +6/+1) with Cleave, a
        /// longsword and an adaptive AI profile (<see cref="Adaptive"/>) full-attacks one of three adjacent 1-hp goblins. Its first attack drops that goblin, so it makes one
        /// Cleave attack at the same bonus against another adjacent goblin (the AI's pick), then its +1 iterative attack
        /// against the last one: three attacks, the first two at the same modifier (PHB p.92).
        /// </summary>
        private static ScenarioDef CleaveFullAttackAi()
        {
            return ForceKills(Rules("rules/cleave-full-attack-ai", "Cleave inside an NPC-path full attack: the AI picks the target, the cleave is at the same bonus (PHB p.92; D7, CMB-017)")
                .Covers("CMB-017", "PHB p.92", "D7")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6, "Cleave")), 10, 10, Control.Scripted)
                .Npc("a", "goblin", 11, 10, Control.Scripted)
                .Npc("b", "goblin", 10, 11, Control.Scripted)
                .Npc("c", "goblin", 9, 10, Control.Scripted)
                .Tweak("hero", Longsword)
                .Profile("hero", Adaptive)
                .Tweak("a", OneHp).Tweak("b", OneHp).Tweak("c", OneHp)
                .Initiative("hero", "a", "b", "c"))
                .Turn("hero", 1, Step.FullAttack("a"))
                .Expect("Three attacks: the first, the Cleave attack at the same bonus, then the +1 iterative attack", Mods("hero", 1, 0, 0, -5))
                .Expect("The first attack drops goblin a, the Cleave attack goes at another goblin", v =>
                {
                    List<TraceEvent> a = v.Attacks("hero", null, null, 1);
                    if (a.Count < 2) return ExpectResult.Fail(a.Count + " attacks");
                    return a[0].Str("target") == "a" && a[1].Str("target") != "a"
                        ? ExpectResult.Pass("a, then " + a[1].Str("target"), a[0].Seq, a[1].Seq)
                        : ExpectResult.Fail(a[0].Str("target") + ", then " + a[1].Str("target"), a[0].Seq, a[1].Seq);
                })
                .Expect("One Cleave in the round", CleaveLogs(1, 1))
                .Expect("Every goblin is dead and the game ends the fight", Expect.All(Dead("a", "b", "c"), Expect.Outcome(Outcome.Victory)))
                .Build();
        }

        /// <summary>
        /// CMB-017 (reach, the UI prompt): a Ui fighter 4 with Cleave and a longspear (reach 10 ft, no adjacent attack)
        /// attacks goblin a 10 ft away. Goblins b and c stand 10 ft away on other sides, out of FindAdjacentEnemy's old
        /// reach; with two candidates the Cleave menu opens and the player picks c (PHB p.92, p.113).
        /// </summary>
        private static ScenarioDef CleaveReachUi()
        {
            return ForceKills(Rules("rules/cleave-reach-ui", "Cleave with a reach weapon: the PC picks among the foes 10 ft away (PHB p.92, p.113; D7, CMB-017)")
                .Covers("CMB-017", "PHB p.92", "PHB p.113", "D7")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4, "Cleave")), 10, 10, Control.Ui)
                .Npc("a", "goblin", 12, 10, Control.Scripted)
                .Npc("b", "goblin", 10, 12, Control.Scripted)
                .Npc("c", "goblin", 8, 10, Control.Scripted)
                .Tweak("hero", Longspear)
                .Tweak("a", OneHpNamed("Goblin A")).Tweak("b", OneHpNamed("Goblin B")).Tweak("c", OneHpNamed("Goblin C"))
                .Initiative("hero", "a", "b", "c"))
                .Turn("hero", 1, Step.Attack("a"), Step.AnswerCleave("c"))
                .Turn("a", 1, Step.Pass())
                .Turn("b", 1, Step.Pass())
                .Turn("c", 1, Step.Pass())
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("The attack and the Cleave answer are done", Expect.All(
                    Expect.StepStatus("hero", 1, "Attack", 0, "done"),
                    Expect.StepStatus("hero", 1, "AnswerCleave(c)", 0, "done")))
                .Expect("The hero attacks a, then cleaves into c 10 ft away at the same bonus, and never attacks b", v =>
                {
                    List<TraceEvent> a = v.Attacks("hero", null, null, 1);
                    string seq = string.Join(",", a.Select(e => e.Str("target") + " " + e.Int("mod")));
                    return a.Count == 2 && a[0].Str("target") == "a" && a[1].Str("target") == "c" && a[0].Int("mod") == a[1].Int("mod")
                        ? ExpectResult.Pass(seq, a.Select(e => e.Seq).ToArray())
                        : ExpectResult.Fail(seq, a.Select(e => e.Seq).ToArray());
                })
                .Expect("One Cleave in the round", CleaveLogs(1, 1))
                .Expect("Goblins a and c are dead", Dead("a", "c"))
                .Build();
        }

        /// <summary>
        /// PHB p.92 (Cleave once per round; D7): a Scripted fighter 6 with Cleave and an adaptive AI profile next to four
        /// 1-hp goblins (a, b, c, f); goblin g starts 10 ft away. Round 1: the first attack drops a, the Cleave attack drops
        /// a second goblin, the +1 attack drops a third, and no second Cleave follows although the fourth is adjacent. Goblin
        /// g then steps next to the fighter. Round 2: the flag cleared at the fighter's turn start, its first attack drops g
        /// and it cleaves the last goblin.
        /// </summary>
        private static ScenarioDef CleaveOncePerRound()
        {
            return ForceKills(Rules("rules/cleave-once-per-round", "Cleave once per round, again the next round (PHB p.92; D7, CMB-017)")
                .Covers("CMB-017", "PHB p.92", "D7")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6, "Cleave")), 10, 10, Control.Scripted)
                .Npc("a", "goblin", 11, 10, Control.Scripted)
                .Npc("b", "goblin", 10, 11, Control.Scripted)
                .Npc("c", "goblin", 9, 10, Control.Scripted)
                .Npc("f", "goblin", 10, 9, Control.Scripted)
                .Npc("g", "goblin", 12, 8, Control.Scripted)
                .Tweak("hero", Longsword)
                .Profile("hero", Adaptive)
                .Tweak("a", OneHp).Tweak("b", OneHp).Tweak("c", OneHp).Tweak("f", OneHp).Tweak("g", OneHp)
                .Initiative("hero", "a", "b", "c", "f", "g"))
                .Turn("hero", 1, Step.FullAttack("a"))
                .Turn("a", 1, Step.Pass())
                .Turn("b", 1, Step.Pass())
                .Turn("c", 1, Step.Pass())
                .Turn("f", 1, Step.Pass())
                .Turn("g", 1, Step.Move(11, 9))
                .Turn("hero", 2,
                    Step.Assert("the Cleave flag is clear at the turn start", ctx => !ctx.Get("hero").CleaveUsedThisRound),
                    Step.FullAttack("g"))
                .Expect("Round 1: three attacks, the Cleave at the same bonus, the +1 attack after it", Mods("hero", 1, 0, 0, -5))
                .Expect("Round 1: one Cleave only (the +1 attack's drop makes none, with a goblin still adjacent)", CleaveLogs(1, 1))
                .Expect("Round 2: the flag is clear at the hero's turn start", Expect.AssertsPass())
                .Expect("Round 2: the first attack drops g and the Cleave the last goblin", Expect.All(Mods("hero", 2, 0, 0), CleaveLogs(2, 1)))
                .Expect("Every goblin is dead and the game ends the fight", Expect.All(Dead("a", "b", "c", "f", "g"), Expect.Outcome(Outcome.Victory)))
                .Build();
        }

        /// <summary>
        /// D7 (Cleave after any melee attack, an AoO included; CMB-017 origin I): a Scripted fighter 4 with Cleave and a
        /// longsword, which has acted (so it is not flat-footed), threatens two 1-hp goblins. Goblin a walks away and
        /// provokes its AoO; the AoO drops a, and the fighter cleaves goblin b on a's turn.
        /// </summary>
        private static ScenarioDef CleaveAfterAoO()
        {
            return ForceKills(Rules("rules/cleave-after-aoo", "Cleave after an attack of opportunity that drops the mover (PHB p.92, p.137; D7, CMB-017)")
                .Covers("CMB-017", "PHB p.92", "PHB p.137", "D7")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4, "Cleave")), 10, 10, Control.Scripted)
                .Npc("a", "goblin", 11, 10, Control.Scripted)
                .Npc("b", "goblin", 10, 11, Control.Scripted)
                .Tweak("hero", Longsword)
                .Tweak("a", OneHp).Tweak("b", OneHp)
                .Initiative("hero", "a", "b"))
                .Turn("hero", 1, Step.Pass())
                .Turn("a", 1, Step.Move(14, 10))
                .Turn("b", 1, Step.Pass())
                .Expect("Goblin a's move provokes the hero's AoO", v =>
                {
                    List<TraceEvent> aoos = v.AoOs("hero", "a");
                    return aoos.Count >= 1 ? ExpectResult.Pass(aoos.Count + " AoO(s)", aoos[0].Seq) : ExpectResult.Fail("no AoO by the hero");
                })
                .Expect("The AoO drops a and the hero cleaves b on a's turn, at the AoO's bonus", v =>
                {
                    List<TraceEvent> a = v.Attacks("hero", null, null, 1);
                    string seq = string.Join(",", a.Select(e => e.Str("target") + (e.Bool("aoo") ? " aoo " : " ") + e.Int("mod")));
                    return a.Count == 2 && a[0].Str("target") == "a" && a[0].Bool("aoo") && a[1].Str("target") == "b" && !a[1].Bool("aoo")
                           && a[0].Int("mod") == a[1].Int("mod")
                        ? ExpectResult.Pass(seq, a.Select(e => e.Seq).ToArray())
                        : ExpectResult.Fail(seq, a.Select(e => e.Seq).ToArray());
                })
                .Expect("One Cleave in the round", CleaveLogs(1, 1))
                .Expect("Both goblins are dead and the game ends the fight", Expect.All(Dead("a", "b"), Expect.Outcome(Outcome.Victory)))
                .Build();
        }

        /// <summary>
        /// D7 (a disabled foe has not dropped): a Scripted fighter 5 (one attack) with Cleave hits goblin a, whose hit
        /// points are set at the turn start to the damage the forced dice deal (each d8 rolls 2: 2 + 3 Strength = 5), so a
        /// is left disabled at exactly 0. No Cleave follows, so goblin b is never attacked.
        /// </summary>
        private static ScenarioDef CleaveDisabledTargetNoTrigger()
        {
            return ForceHits(Rules("rules/cleave-disabled-target-no-trigger", "A foe left disabled at 0 hp has not dropped: no Cleave (PHB p.92, p.145; D7)")
                .Covers("PHB p.92", "PHB p.145", "D7")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 5, "Cleave")), 10, 10, Control.Scripted)
                .Npc("a", "goblin", 11, 10, Control.Scripted)
                .Npc("b", "goblin", 10, 11, Control.Scripted)
                .Tweak("hero", Longsword)
                .Tweak("a", c => { c.Stats.AdjustMaxHP(10); c.Stats.CurrentHP = 5; c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Tweak("b", OneHp)
                .Force(8, 2, null, -1)
                .Initiative("hero", "a", "b"))
                .Turn("hero", 1, Step.FullAttack("a"))
                .Turn("a", 1, Step.Pass())
                .Turn("b", 1, Step.Pass())
                .Expect("One attack, a hit for 5 on goblin a", v =>
                {
                    List<TraceEvent> a = v.Attacks("hero", null, null, 1);
                    return a.Count == 1 && a[0].Str("target") == "a" && a[0].Bool("hit") && a[0].Int("dmg") == 5
                        ? ExpectResult.Pass("one hit for 5", a[0].Seq)
                        : ExpectResult.Fail(string.Join(",", a.Select(e => e.Str("target") + " hit=" + e.Bool("hit") + " dmg=" + e.Int("dmg"))));
                })
                .Expect("Goblin a is disabled at exactly 0 and b untouched", v =>
                {
                    JsonObj a = v.Snapshot("a", 1, "a") ?? v.Final("a");
                    JsonObj b = v.Final("b");
                    object ahp = a != null ? a.Get("hp") : null;
                    object bhp = b != null ? b.Get("hp") : null;
                    return ahp is int x && x == 0 && bhp is int y && y == 1
                        ? ExpectResult.Pass("a 0 hp, b 1 hp")
                        : ExpectResult.Fail("a " + ahp + " hp, b " + bhp + " hp");
                })
                .Expect("No Cleave", CleaveLogs(1, 0))
                .Build();
        }

        /// <summary>
        /// PHB p.92 (Great Cleave: no limit; D7): a Scripted fighter 5 (one attack) with Cleave and Great Cleave and four
        /// adjacent 1-hp goblins. Its attack drops the first; each Great Cleave attack drops the next, all at the same bonus.
        /// </summary>
        private static ScenarioDef GreatCleaveChain()
        {
            return ForceKills(Rules("rules/great-cleave-chain", "Great Cleave chains through every adjacent foe at the same bonus (PHB p.92; D7)")
                .Covers("PHB p.92", "D7")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 5, "Cleave", "Great Cleave")), 10, 10, Control.Scripted)
                .Npc("a", "goblin", 11, 10, Control.Scripted)
                .Npc("b", "goblin", 10, 11, Control.Scripted)
                .Npc("c", "goblin", 9, 10, Control.Scripted)
                .Npc("d", "goblin", 10, 9, Control.Scripted)
                .Tweak("hero", Longsword)
                .Tweak("a", OneHp).Tweak("b", OneHp).Tweak("c", OneHp).Tweak("d", OneHp)
                .Initiative("hero", "a", "b", "c", "d"))
                .Turn("hero", 1, Step.FullAttack("a"))
                .Expect("Four attacks, all at the same bonus", Mods("hero", 1, 0, 0, 0, 0))
                .Expect("Three Great Cleave attacks", CleaveLogs(1, 3, "Great Cleave!"))
                .Expect("Every goblin is dead and the game ends the fight", Expect.All(Dead("a", "b", "c", "d"), Expect.Outcome(Outcome.Victory)))
                .Build();
        }

        /// <summary>
        /// D7 (the drop is judged per attack; the step 15 review): a Ui fighter 4 with Cleave (no Two-Weapon Fighting), a
        /// longsword and a short sword in the off hand (light: -4 main, -8 off, PHB p.160) makes the legacy two-weapon full
        /// attack (origin D, one resolution for both results) against goblin a (6 hp). Each d8 rolls 1 and each d6 rolls 4:
        /// the main hand hits for 4 (6 to 2), the off hand for 5 (2 to -3), so only the off hand drops a. The Cleave attack on
        /// goblin b is made with the short sword at the off-hand bonus (PHB p.92: the same weapon and bonus). Not a dagger:
        /// a weapon with a range increment counts as a ranged attack even in melee (CMB-105), and no ranged attack cleaves.
        /// </summary>
        private static ScenarioDef CleaveTwoWeaponOffHandDrop()
        {
            return ForceHits(Rules("rules/cleave-two-weapon-offhand-drop", "Cleave after a two-weapon attack whose off hand drops the foe: the off-hand weapon and bonus (PHB p.92; D7)")
                .Covers("CMB-017", "PHB p.92", "PHB p.160", "D7")
                .MaxRounds(1)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 4, "Cleave")), 10, 10, Control.Ui)
                .Npc("a", "goblin", 11, 10, Control.Scripted)
                .Npc("b", "goblin", 10, 11, Control.Scripted)
                .Tweak("hero", c =>
                {
                    Longsword(c);
                    global::Inventory inv = Inv(c);
                    if (inv != null)
                    {
                        inv.DirectEquip(ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponShortsword), EquipSlot.LeftHand);
                        inv.RecalculateStats();
                    }
                })
                .Tweak("a", c => { c.Stats.AdjustMaxHP(10); c.Stats.CurrentHP = 6; c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Tweak("b", OneHp)
                .Force(8, 1, null, -1)
                .Force(6, 4, null, -1)
                .Initiative("hero", "a", "b"))
                .Turn("hero", 1, Step.DualWield("a"))
                .Turn("a", 1, Step.Pass())
                .Turn("b", 1, Step.Pass())
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("Main hand on a, off hand on a, then the Cleave on b with the off-hand weapon at the off-hand bonus", v =>
                {
                    List<TraceEvent> a = v.Attacks("hero", null, null, 1);
                    string seq = string.Join(",", a.Select(e => e.Str("target") + " " + e.Str("weapon") + " " + e.Int("mod") + " dmg " + e.Int("dmg")));
                    bool ok = a.Count == 3 && a[0].Str("target") == "a" && a[1].Str("target") == "a" && a[2].Str("target") == "b"
                              && a[0].Int("mod") != a[1].Int("mod")
                              && a[2].Int("mod") == a[1].Int("mod") && a[2].Str("weapon") == a[1].Str("weapon")
                              && a[0].Int("dmg") == 4 && a[1].Int("dmg") == 5;
                    return ok ? ExpectResult.Pass(seq, a.Select(e => e.Seq).ToArray()) : ExpectResult.Fail(seq, a.Select(e => e.Seq).ToArray());
                })
                .Expect("One Cleave in the round", CleaveLogs(1, 1))
                .Expect("Goblin b is down (1 hp to -4) and the game ends the fight", Expect.All(v =>
                {
                    JsonObj snap = v.Final("b");
                    object hp = snap != null ? snap.Get("hp") : null;
                    return hp is int x && x < 0 ? ExpectResult.Pass("b " + x + " hp") : ExpectResult.Fail("b " + hp + " hp");
                }, Expect.Outcome(Outcome.Victory)))
                .Build();
        }

        // ── The acid spray as a reaction (D1) ───────────────────────────

        /// <summary>
        /// CRE-040 (D1): a Ui fighter 6 full-attacks an adjacent giant bombardier beetle (raised by 300 hp) on the fighter's
        /// own turn. The beetle sprays right after the first attack, before the second, and only once in the round
        /// (MM p.285: when attacked, once per round). Its own turn (it acted first) sprays nothing.
        /// </summary>
        private static ScenarioDef BombardierSprayWhenAttacked()
        {
            return ForceHits(Rules("rules/bombardier-spray-when-attacked", "The bombardier beetle sprays when attacked, after the attack, once in the round, on the attacker's turn (MM p.285; D1, CRE-040)")
                .Covers("CRE-040", "MM p.285", "MM p.315", "PHB p.142", "D1")
                .MaxRounds(1)
                .Npc("beetle", "giant_bombardier_beetle", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6)), 10, 10, Control.Ui)
                .Tweak("hero", Longsword)
                .Tweak("beetle", Sturdy)
                .Initiative("beetle", "hero"))
                .Turn("beetle", 1, Step.Pass())
                .Turn("hero", 1, Step.FullAttack("beetle"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("No spray on the beetle's own turn; one spray in the round", Expect.Count("log", e => e.Round == 1 && (e.Str("text") ?? "").Contains("unleashes Acid Spray"), 1, 1))
                .Expect("The spray comes after the hero's first attack and before the second, on the hero's turn", v =>
                {
                    List<TraceEvent> a = v.Attacks("hero", "beetle", null, 1);
                    TraceEvent spray = v.Of("log").FirstOrDefault(e => (e.Str("text") ?? "").Contains("unleashes Acid Spray"));
                    TraceEvent heroTurn = v.Of("turn_start").FirstOrDefault(e => e.Round == 1 && e.Str("actor") == "hero");
                    if (a.Count != 2 || spray == null || heroTurn == null)
                        return ExpectResult.Fail(a.Count + " attacks, spray " + (spray != null ? "#" + spray.Seq : "none"));
                    return heroTurn.Seq < a[0].Seq && a[0].Seq < spray.Seq && spray.Seq < a[1].Seq
                        ? ExpectResult.Pass("turn #" + heroTurn.Seq + ", attack #" + a[0].Seq + ", spray #" + spray.Seq + ", attack #" + a[1].Seq, a[0].Seq, spray.Seq, a[1].Seq)
                        : ExpectResult.Fail("turn #" + heroTurn.Seq + ", attack #" + a[0].Seq + ", spray #" + spray.Seq + ", attack #" + a[1].Seq, a[0].Seq, spray.Seq, a[1].Seq);
                })
                .Expect("The hero rolls one Fortitude save against the spray", Expect.Count("dice", e => (e.Str("ctx") ?? "").Contains("Fortitude save vs Acid Spray"), 1, 1))
                .Build();
        }

        /// <summary>
        /// D1 (a spray that ends the combat ends the triggering action; the step 15 review): a Ui fighter 6, the party's
        /// only member, at 3 hit points, full-attacks a giant bombardier beetle (raised by 300 hp). The first attack hits
        /// and the beetle sprays; the fighter's Fortitude save rolls 2 and fails, and the spray's d4 rolls 4 (4 + 2 = 6
        /// acid), so the fighter drops to -3 and the party is out: defeat, and the full attack's second attack is never made.
        /// </summary>
        private static ScenarioDef BombardierSprayEndsCombat()
        {
            // The save force comes first: the first matching force wins (ScenarioModel.DiceForce).
            return ForceHits(Rules("rules/bombardier-spray-ends-combat", "A spray that drops the last of a side ends the combat and the attacker's full attack (MM p.285; D1, CRE-040)")
                .Force(20, 2, "Fortitude save vs Acid Spray", -1)
                .Covers("CRE-040", "MM p.285", "D1")
                .MaxRounds(1)
                .Npc("beetle", "giant_bombardier_beetle", 11, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 6)), 10, 10, Control.Ui)
                .Tweak("hero", c => { Wield(c, ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.WeaponLongsword)); c.Stats.CurrentHP = 3; })
                .Tweak("beetle", Sturdy)
                .Initiative("beetle", "hero"))
                .Force(4, 4, null, -1)
                .Turn("beetle", 1, Step.Pass())
                .Turn("hero", 1, Step.FullAttack("beetle"))
                .Expect("The hero's turn is a Ui turn", Expect.Controller("hero", "ui"))
                .Expect("One spray, then no second attack by the hero", Expect.All(
                    Expect.Count("log", e => e.Round == 1 && (e.Str("text") ?? "").Contains("unleashes Acid Spray"), 1, 1),
                    Expect.Count("attack", e => e.Str("attacker") == "hero", 1, 1)))
                .Expect("The party is out: defeat", Expect.Outcome(Outcome.Defeat))
                .Build();
        }
    }
}
#endif
