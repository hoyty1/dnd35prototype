#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 13 step 16 (docs/designs/action_catalog_batch13.md; CMB-201, AI-079, AI-054, CMB-026 narrowed; decisions D9,
    /// D10, design choice C6): the class-ability executors in real fights.
    ///
    /// Turn undead (PHB p.159, D9): a standard action paid first, then the turning check and, only when the check can
    /// affect some undead in range, the turning damage. The undead foes within 60 ft with line of effect are taken closest
    /// first, ties to the lower Hit Dice, and the turning stops at the first that the Hit Dice left cannot cover; one
    /// behind a wall is not affected. The PC's Turn Undead button and the AI run the same executor; an AI cleric turns
    /// when its best check could affect the nearest undead (C6).
    ///
    /// Rage (PHB p.25, D10): no action, only on the barbarian's own turn, once per encounter (fatigue no bar); an AI barbarian rages at its turn start when a
    /// foe is within reach or charge range (C6) and still acts.
    ///
    /// The dice: the cleric fixtures have Charisma 10 and turning level 3; a forced natural 20 on the turning check
    /// affects up to 6 HD (Table 8-9: 19-21 is level + 3), and two forced 1s on the turning damage give a pool of 2 + 3 = 5
    /// HD.
    /// </summary>
    public static class ClassAbilityScenarios
    {
        private const string NotePrefix = "b13s16 ";

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("turn-undead-closest-first-ui", () => TurnUndeadClosestFirst(ui: true));
            yield return S("turn-undead-closest-first-ai", () => TurnUndeadClosestFirst(ui: false));
            yield return S("turn-undead-stops-at-first-misfit", TurnUndeadStopsAtFirstMisfit);
            yield return S("turn-undead-needs-line-of-effect", TurnUndeadNeedsLineOfEffect);
            yield return S("npc-cleric-turns-undead-ai", NpcClericTurnsUndeadAi);
            yield return S("rage-ai", RageAi);
            yield return S("rage-own-turn-only-ui", RageOwnTurnOnlyUi);
            yield return S("turn-undead-skip-turned-ui", () => TurnUndeadAlreadyTurned(include: false));
            yield return S("turn-undead-include-turned-ui", () => TurnUndeadAlreadyTurned(include: true));
            yield return S("greater-turning-sun-ai", GreaterTurningSunAi);
            yield return S("rage-once-per-encounter-ui", RageOncePerEncounterUi);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("ClassAbilityScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        /// <summary>A human cleric of <paramref name="level"/>, Charisma 10 (turning check d20 + 0), neutral good (turns undead).</summary>
        private static CharacterStats Cleric(string name, int level)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Cleric",
                str: 12, dex: 10, con: 14, wis: 14, intelligence: 10, cha: 10,
                bab: 0, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 8 * level, raceName: "Human");
            s.CharacterAlignment = Alignment.NeutralGood;
            s.EnsureMulticlassDataInitialized();
            return s;
        }

        private static CharacterStats Barbarian(string name, int level)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Barbarian",
                str: 16, dex: 12, con: 14, wis: 10, intelligence: 10, cha: 10,
                bab: level, armorBonus: 0, shieldBonus: 0,
                damageDice: 12, damageCount: 1, bonusDamage: 0,
                baseSpeed: 8, atkRange: 1, baseHitDieHP: 12 * level, raceName: "Human");
            s.BaseAttackBonusOverride = level;
            s.EnsureMulticlassDataInitialized();
            return s;
        }

        private static void Note(ScenarioContext ctx, string key, object value) => ctx.Note(NotePrefix + key + "=" + value);

        /// <summary>The last note of <paramref name="key"/> is <paramref name="want"/>.</summary>
        private static Func<TraceView, ExpectResult> Fact(string key, string want)
            => v =>
            {
                string prefix = NotePrefix + key + "=";
                TraceEvent n = v.Of("note").LastOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
                if (n == null)
                    return ExpectResult.Inconclusive("no note " + key);
                string got = n.Str("text").Substring(prefix.Length);
                return got == want ? ExpectResult.Pass(key + "=" + got, n.Seq) : ExpectResult.Fail(key + "=" + got + ", expected " + want, n.Seq);
            };

        private static bool TurnedInRound(TraceView v, string key, int round)
            => v.Conds(key, CombatConditionType.Turned).Any(e => e.Bool("added") && e.Round == round);

        /// <summary><paramref name="key"/> gets Turned in round 1 (<paramref name="want"/>) or not.</summary>
        private static Func<TraceView, ExpectResult> Turned(string key, bool want)
            => v =>
            {
                bool got = TurnedInRound(v, key, 1);
                return got == want ? ExpectResult.Pass(key + (got ? " turned" : " not turned")) : ExpectResult.Fail(key + (got ? " turned" : " not turned") + ", expected " + (want ? "turned" : "not turned"));
            };

        /// <summary>The forced dice: a natural 20 on the turning check (up to level + 3 HD), 1 + 1 on the turning damage.</summary>
        private static ScenarioBuilder ForceTurningDice(ScenarioBuilder b)
            => b.Force(20, 20, "Turning check").Force(6, 1, "Turning damage", 2);

        // ── Turn undead: closest first, the pool stops at the first misfit (PHB p.159; D9) ──

        /// <summary>
        /// A level 3 cleric (pool 5 HD with the forced dice) turns with a skeleton (4 HD) 2 squares away, a zombie (2 HD) at 4
        /// and another zombie at 6. The closest, the skeleton, is turned first (4 of the 5 HD); the next, the nearer zombie,
        /// needs 2 HD and only 1 is left, so the turning stops there and the far zombie is not affected either. The Ui twin
        /// presses the Turn Undead button and confirms; the AI twin runs the same entry through the AI runner.
        /// </summary>
        private static ScenarioDef TurnUndeadClosestFirst(bool ui)
        {
            ScenarioBuilder b = Rules("rules/turn-undead-closest-first-" + (ui ? "ui" : "ai"),
                    "Turn undead takes the closest undead first and stops at the first that does not fit (PHB p.159; D9, CMB-201)")
                .Covers("CMB-201", "AI-054", "D9", "PHB p.159", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("cleric", ActorSource.Stats(() => Cleric("Cleric", 3)), 5, 10, ui ? Control.Ui : Control.Scripted)
                .Tweak("cleric", Sturdy)
                .Npc("skel", "skeleton_warrior", 7, 10, Control.Ai)
                .Npc("mid", "zombie_shambler", 5, 14, Control.Ai)
                .Npc("far", "zombie_shambler", 11, 10, Control.Ai)
                .Initiative("cleric", "skel", "mid", "far")
                .Turn("cleric", 1, Step.TurnUndead());
            b = ForceTurningDice(b);
            if (ui)
                b = b.Expect("The cleric's turn is a Ui turn", Expect.Controller("cleric", "ui"));
            return b
                .Expect("The turning step is done", Expect.StepStatus("cleric", 1, "TurnUndead", 0, "done"))
                .Expect("The cleric invokes Turn Undead in round 1", v =>
                    v.Log("Cleric invokes Turn Undead").Any(e => e.Round == 1) ? ExpectResult.Pass("invoked") : ExpectResult.Fail("no turning"))
                .Expect("The closest undead, the skeleton, is turned", Turned("skel", true))
                .Expect("The nearer zombie does not fit the 1 HD left: the turning stops there", v =>
                    v.Log("needs more than the 1 HD left: the turning stops here").Any(e => e.Round == 1) ? ExpectResult.Pass("stopped at the zombie") : ExpectResult.Fail("no stop line"))
                .Expect("Neither zombie is turned", Expect.All(Turned("mid", false), Turned("far", false)))
                .Expect("The pool's result: 1 turned, 1 HD unspent", v =>
                    v.Log("Results: 0 destroyed, 1 turned, 1 HD turning power unspent").Any() ? ExpectResult.Pass("1 turned, 1 HD left") : ExpectResult.Fail("other result"))
                .Build();
        }

        /// <summary>
        /// A zombie (2 HD) and a skeleton (4 HD) are both 2 squares from the cleric, a second zombie 5 squares away. The tie
        /// goes to the lower Hit Dice, so the zombie is turned first (2 of the 5 HD); the skeleton needs 4 and 3 are left, so
        /// the turning stops there: the farther zombie, which the 3 HD would cover, is not affected (delegated RAW reading
        /// 2026-10-29: only already turned undead may be skipped).
        /// </summary>
        private static ScenarioDef TurnUndeadStopsAtFirstMisfit()
        {
            ScenarioBuilder b = Rules("rules/turn-undead-stops-at-first-misfit",
                    "Turn undead breaks ties to the lower HD and does not skip an undead that does not fit (PHB p.159; D9)")
                .Covers("CMB-201", "D9", "PHB p.159")
                .MaxRounds(1)
                .Pc("cleric", ActorSource.Stats(() => Cleric("Cleric", 3)), 5, 10, Control.Scripted)
                .Tweak("cleric", Sturdy)
                .Npc("zombie", "zombie_shambler", 7, 10, Control.Ai)
                .Npc("skel", "skeleton_warrior", 5, 12, Control.Ai)
                .Npc("zombie2", "zombie_shambler", 3, 14, Control.Ai)
                .Initiative("cleric", "zombie", "skel", "zombie2")
                .Turn("cleric", 1, Step.TurnUndead());
            return ForceTurningDice(b)
                .Expect("The zombie (the tie's lower HD) is turned", Turned("zombie", true))
                .Expect("The skeleton (4 HD, 3 left) is not turned and the turning stops there", v =>
                {
                    bool stop = v.Log("Skeleton Warrior \\(4 HD\\) needs more than the 3 HD left").Any(e => e.Round == 1);
                    bool turned = TurnedInRound(v, "skel", 1);
                    return stop && !turned ? ExpectResult.Pass("stopped at the skeleton") : ExpectResult.Fail("stop line " + stop + ", skeleton turned " + turned);
                })
                .Expect("The farther zombie is not skipped to", Turned("zombie2", false))
                .Expect("The pool's result: 1 turned, 3 HD unspent", v =>
                    v.Log("Results: 0 destroyed, 1 turned, 3 HD turning power unspent").Any() ? ExpectResult.Pass("1 turned, 3 HD left") : ExpectResult.Fail("other result"))
                .Build();
        }

        /// <summary>A Wall of Ice on <paramref name="cells"/> (PHB p.299): a wall that blocks line of effect (GRID-009).</summary>
        private static void PlaceWallOfIce(params Vector2Int[] cells)
        {
            var go = new GameObject("WallOfIce_Test_Area");
            go.transform.position = SquareGridUtils.GridToWorld(cells[0]);
            WallOfIceAreaEffect wall = go.AddComponent<WallOfIceAreaEffect>();
            wall.CenterPosition = go.transform.position;
            wall.CenterCell = cells[0];
            wall.LengthSquares = Math.Max(2, cells.Length);
            wall.RoundsRemaining = 10;
            wall.CasterLevel = 9;
            wall.SetExplicitCells(new HashSet<Vector2Int>(cells));
        }

        /// <summary>
        /// A Wall of Ice (x = 7, y = 6 to 14) stands between the cleric and a skeleton 4 squares away; a zombie 5 squares away
        /// is in the open. The turning needs line of effect (PHB p.159: no total cover), so the skeleton, the closer one, is
        /// not affected and the zombie is turned. A Scripted ally places the wall a turn earlier (the wall joins line of
        /// effect when it starts).
        /// </summary>
        private static ScenarioDef TurnUndeadNeedsLineOfEffect()
        {
            var wallCells = new List<Vector2Int>();
            for (int y = 6; y <= 14; y++)
                wallCells.Add(new Vector2Int(7, y));
            ScenarioBuilder b = Rules("rules/turn-undead-needs-line-of-effect",
                    "Turn undead does not affect an undead behind a wall (PHB p.159; D9, CMB-026)")
                .Covers("CMB-026", "D9", "PHB p.159", "GRID-009")
                .MaxRounds(1)
                .Pc("ally", ActorSource.Stats(() => RulesScenarios.Fighter("Ally", 3)), 1, 1, Control.Scripted)
                .Pc("cleric", ActorSource.Stats(() => Cleric("Cleric", 3)), 5, 10, Control.Scripted)
                .Tweak("cleric", Sturdy)
                .Tweak("ally", Sturdy)
                .Npc("skel", "skeleton_warrior", 9, 10, Control.Ai)
                .Npc("zombie", "zombie_shambler", 5, 15, Control.Ai)
                .Initiative("ally", "cleric", "skel", "zombie")
                .Turn("ally", 1, Step.Assert("a Wall of Ice stands between the cleric and the skeleton (fixture)", ctx => { PlaceWallOfIce(wallCells.ToArray()); return true; }), Step.Pass())
                .Turn("cleric", 1, Step.TurnUndead());
            return ForceTurningDice(b)
                .Expect("The skeleton behind the wall has no line of effect from the cleric", v =>
                    v.Log("Skeleton Warrior has no line of effect from Cleric").Any(e => e.Round == 1) ? ExpectResult.Pass("blocked") : ExpectResult.Fail("no blocked line"))
                .Expect("The skeleton is not turned", Turned("skel", false))
                .Expect("The zombie in the open is turned", Turned("zombie", true))
                .Build();
        }

        // ── Already turned undead: the turner's choice (PHB p.159; D9) ──

        /// <summary>
        /// Cleric A (Scripted) turns first: the zombie (2 HD, 2 squares) is turned and the skeleton (4 HD, 4 squares) does not
        /// fit the 3 HD left. Then Cleric B, a Ui cleric beside A, turns with the same forced dice (pool 5) and is asked once
        /// whether to skip the already turned zombie. Skip: the zombie is passed over and the skeleton (4 of the 5 HD) is
        /// turned. Include: the zombie takes 2 HD again and the skeleton (3 left) stops the turning.
        /// </summary>
        private static ScenarioDef TurnUndeadAlreadyTurned(bool include)
        {
            string id = "rules/turn-undead-" + (include ? "include" : "skip") + "-turned-ui";
            ScenarioBuilder b = Rules(id, "A PC turning chooses whether to skip already turned undead (PHB p.159; D9)")
                .Covers("D9", "PHB p.159", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("a", ActorSource.Stats(() => Cleric("Cleric A", 3)), 5, 10, Control.Scripted)
                .Pc("b", ActorSource.Stats(() => Cleric("Cleric B", 3)), 5, 11, Control.Ui)
                .Tweak("a", Sturdy)
                .Tweak("b", Sturdy)
                .Npc("zombie", "zombie_shambler", 7, 10, Control.Ai)
                .Npc("skel", "skeleton_warrior", 9, 10, Control.Ai)
                .Initiative("a", "b", "zombie", "skel")
                .Turn("a", 1, Step.TurnUndead())
                .Turn("b", 1, Step.TurnUndead(includeTurned: include))
                .Force(20, 20, "Turning check", 2)
                .Force(6, 1, "Turning damage", 4)
                .Expect("Cleric B's turn is a Ui turn", Expect.Controller("b", "ui"))
                .Expect("Cleric B invokes Turn Undead in round 1", v =>
                    v.Log("Cleric B invokes Turn Undead").Any(e => e.Round == 1) ? ExpectResult.Pass("invoked") : ExpectResult.Fail("no turning"))
                .Expect("Cleric B's turning step is done", Expect.StepStatus("b", 1, "TurnUndead", 0, "done"));
            if (include)
                return b
                    .Expect("The already turned zombie is not skipped", v =>
                        v.Log("Zombie is already turned: skipped").Any() ? ExpectResult.Fail("skipped") : ExpectResult.Pass("not skipped"))
                    .Expect("The skeleton is not turned: the zombie took 2 of the 5 HD", Turned("skel", false))
                    .Build();
            return b
                .Expect("The already turned zombie is skipped", v =>
                    v.Log("Zombie is already turned: skipped").Any(e => e.Round == 1) ? ExpectResult.Pass("skipped") : ExpectResult.Fail("no skip line"))
                .Expect("The skeleton is turned (4 of the 5 HD)", Turned("skel", true))
                .Expect("Cleric B's result: 1 turned, 1 HD unspent", v =>
                    v.Log("Results: 0 destroyed, 1 turned, 1 HD turning power unspent").Any() ? ExpectResult.Pass("1 turned, 1 HD left") : ExpectResult.Fail("other result"))
                .Build();
        }

        // ── Sun domain: a greater turning in the same action (PHB p.188) ──

        /// <summary>
        /// A level 3 Sun-domain cleric (Scripted, the AI's runner) uses its domain power: a greater turning in place of a
        /// turning attempt (PHB p.188). With the forced dice (pool 5) two zombies (2 HD each, 2 and 4 squares away) are
        /// affected, and both are destroyed though a level 3 turner destroys only undead of 1 HD or less (PHB p.159): the
        /// greater turning destroys what it would turn. One Sun use and one turning attempt are spent and the flag is off.
        /// Then, with the day's Sun use handed back by the fixture and no standard action left, the Sun core is refused
        /// and spends nothing. A target dummy far away keeps the fight going.
        /// </summary>
        private static ScenarioDef GreaterTurningSunAi()
        {
            ScenarioBuilder b = Rules("rules/greater-turning-sun-ai", "A Sun-domain greater turning destroys the undead it would turn, in one action (PHB p.188; AI-054)")
                .Covers("AI-054", "D9", "PHB p.188", "PHB p.159", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("cleric", ActorSource.Stats(() => Cleric("Cleric", 3)), 5, 10, Control.Scripted)
                .Tweak("cleric", c => { Sturdy(c); c.Stats.ChosenDomains = new List<string> { "Sun", "Good" }; })
                .Npc("z1", "zombie_shambler", 7, 10, Control.Ai)
                .Npc("z2", "zombie_shambler", 9, 10, Control.Ai)
                .Npc("dummy", "target_dummy", 19, 19, Control.Scripted)
                .Tweak("dummy", Sturdy)
                .Initiative("cleric", "z1", "z2", "dummy")
                .Turn("cleric", 1, Step.Action(ActionIds.DomainPower, variant: DomainPowerActionExecutor.SunVariant),
                    Step.Assert("the greater turning's costs, then a refused one", ctx =>
                    {
                        CharacterStats st = ctx.Get("cleric").Stats;
                        Note(ctx, "sun.uses", st.SunDomainUsesToday);
                        Note(ctx, "turn.attempts", st.TurnUndeadAttemptsUsedToday);
                        Note(ctx, "greater.flag", st.GreaterTurningActive);
                        Note(ctx, "standard.left", ctx.Get("cleric").Actions.HasStandardAction);
                        // The fixture hands back the day's Sun use; with the standard action spent the core refuses.
                        st.SunDomainUsesToday = 0;
                        var o = new ActionOutcome();
                        ctx.Gm.ActionCore_DomainPower(ctx.Get("cleric"), DomainPowerActionExecutor.SunVariant, null, false, o);
                        Note(ctx, "refused.performed", o.Performed);
                        Note(ctx, "refused.sun.uses", st.SunDomainUsesToday);
                        Note(ctx, "refused.flag", st.GreaterTurningActive);
                        st.SunDomainUsesToday = 1;
                        return true;
                    }));
            return ForceTurningDice(b)
                .Expect("The domain power step is done", Expect.StepStatus("cleric", 1, "Action", 0, "done"))
                .Expect("The cleric invokes Greater Turning in round 1", v =>
                    v.Log("Cleric invokes Greater Turning").Any(e => e.Round == 1) ? ExpectResult.Pass("invoked") : ExpectResult.Fail("no greater turning"))
                .Expect("Both zombies are destroyed by the greater turning", v =>
                {
                    int n = v.Log("Zombie is destroyed by Greater Turning").Count(e => e.Round == 1);
                    return n == 2 ? ExpectResult.Pass("2 destroyed") : ExpectResult.Fail(n + " destroyed by Greater Turning");
                })
                .Expect("One Sun use and one turning attempt are spent, the flag is off and the standard action used",
                    Expect.All(Fact("sun.uses", "1"), Fact("turn.attempts", "1"), Fact("greater.flag", "False"), Fact("standard.left", "False")))
                .Expect("A refused Sun turning spends nothing",
                    Expect.All(Fact("refused.performed", "False"), Fact("refused.sun.uses", "0"), Fact("refused.flag", "False")))
                .Build();
        }

        // ── An AI cleric turns undead (C6, AI-054) ──

        /// <summary>
        /// An AI-run human cleric (level 5, Charisma 12; MM-style NPC on the party's side) faces two skeletons about 5 squares
        /// away. Its best check could affect the nearest skeleton (C6), so it turns undead in round 1 through the turn-undead
        /// executor; the forced dice give up to 8 HD and a pool of 2 + 5 + 1 = 8, which turns both skeletons.
        /// </summary>
        private static ScenarioDef NpcClericTurnsUndeadAi()
        {
            ScenarioBuilder b = Rules("rules/npc-cleric-turns-undead-ai", "An AI cleric turns undead through the shared executor (PHB p.159; C6, AI-054)")
                .Covers("AI-054", "C6", "D9", "PHB p.159", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("cleric", "human_cleric", 5, 10, Control.Ai, CharacterTeam.Player)
                .Tweak("cleric", Sturdy)
                .Npc("skel1", "skeleton_warrior", 10, 10, Control.Ai)
                .Npc("skel2", "skeleton_warrior", 10, 12, Control.Ai)
                .Initiative("cleric", "skel1", "skel2");
            return ForceTurningDice(b)
                .Expect("The cleric invokes Turn Undead in round 1", v =>
                    v.Log("invokes Turn Undead").Any(e => e.Round == 1) ? ExpectResult.Pass("invoked") : ExpectResult.Fail("no turning"))
                .Expect("Both skeletons are turned", Expect.All(Turned("skel1", true), Turned("skel2", true)))
                .Expect("No skeleton attacks", Expect.None("attack", e => e.Str("attacker") == "skel1" || e.Str("attacker") == "skel2"))
                .Build();
        }

        // ── Rage (PHB p.25; D10, C6) ──

        /// <summary>
        /// Two AI-run barbarians: one 2 squares from a target dummy, one far across the grid with its speed cut to 5 ft by a
        /// fixture (charge range 2 x its speed). The near one rages at its turn start (a foe within charge range, C6) and
        /// still attacks the dummy (a weapon attack or, as its profile may pick, a trip), since the rage took no action; the far one has no foe in reach or charge range and does
        /// not rage. The dummy notes both barbarians' rage at its turn.
        /// </summary>
        private static ScenarioDef RageAi()
        {
            return Rules("rules/rage-ai", "An AI barbarian rages at its turn start when a foe is within charge range, and still acts (PHB p.25; D10, C6)")
                .Covers("AI-079", "AI-054", "D10", "C6", "PHB p.25", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("barb", ActorSource.Stats(() => Barbarian("Barb", 3)), 5, 10, Control.Ai)
                .Pc("far", ActorSource.Stats(() => Barbarian("Far", 3)), 19, 19, Control.Ai)
                .Tweak("far", c => c.Stats.BaseSpeed = 1)
                .Npc("dummy", "target_dummy", 7, 10, Control.Scripted)
                .Tweak("dummy", Sturdy)
                .Initiative("barb", "far", "dummy")
                .Turn("dummy", 1, Step.Assert("the barbarians' rage at the dummy's turn", ctx =>
                {
                    Note(ctx, "barb.raging", ctx.Get("barb").Stats.IsRaging);
                    Note(ctx, "far.raging", ctx.Get("far").Stats.IsRaging);
                    return true;
                }), Step.Pass())
                .Expect("The near barbarian enters a rage in round 1", v =>
                    v.Log("Barb enters a BARBARIAN RAGE").Any(e => e.Round == 1) ? ExpectResult.Pass("raged") : ExpectResult.Fail("no rage"))
                .Expect("The near barbarian still attacks the dummy in round 1, with a weapon or a maneuver (the rage took no action)", v =>
                {
                    int attacks = v.Attacks("barb", "dummy", aoo: false, round: 1).Count;
                    int maneuvers = v.Of("maneuver").Count(e => e.Round == 1 && e.Str("by") == "barb" && e.Str("target") == "dummy");
                    return attacks + maneuvers >= 1 ? ExpectResult.Pass(attacks + " attack(s), " + maneuvers + " maneuver(s)") : ExpectResult.Fail("no attack or maneuver");
                })
                .Expect("The far barbarian does not rage", v =>
                    v.Log("Far enters a BARBARIAN RAGE").Any() ? ExpectResult.Fail("raged") : ExpectResult.Pass("no rage"))
                .Expect("At the dummy's turn the near barbarian rages and the far one does not",
                    Expect.All(Fact("barb.raging", "True"), Fact("far.raging", "False")))
                .Build();
        }

        /// <summary>
        /// A barbarian may rage only once per encounter, and fatigue is no bar (PHB p.25; D10). The Ui barbarian starts its
        /// turn fatigued (a fixture, as Touch of Fatigue would leave it) and has not raged: the Rage button rages. Then the
        /// fixture ends the rage and removes the fatigue it leaves (as a Restoration would): a second rage is refused
        /// ("already raged this encounter") and the AI runner rages nothing.
        /// </summary>
        private static ScenarioDef RageOncePerEncounterUi()
        {
            return Rules("rules/rage-once-per-encounter-ui", "A fatigued barbarian may rage, but only once per encounter (PHB p.25; D10)")
                .Covers("D10", "PHB p.25", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("barb", ActorSource.Stats(() => Barbarian("Barb", 3)), 5, 10, Control.Ui)
                .Tweak("barb", Sturdy)
                .Npc("dummy", "target_dummy", 7, 10, Control.Scripted)
                .Tweak("dummy", Sturdy)
                .Initiative("barb", "dummy")
                .Turn("barb", 1,
                    Step.Assert("the barbarian is fatigued and has not raged (fixture)", ctx =>
                    {
                        CharacterStats st = ctx.Get("barb").Stats;
                        st.IsFatigued = true;
                        Note(ctx, "pre", st.IsFatiguedState + " " + st.RagedThisEncounter);
                        return true;
                    }),
                    Step.Rage(),
                    Step.Assert("after the rage, a second one", ctx =>
                    {
                        CharacterController barb = ctx.Get("barb");
                        CharacterStats st = barb.Stats;
                        Note(ctx, "raging", st.IsRaging);
                        Note(ctx, "rounds", st.RageRoundsRemaining);
                        st.DeactivateRage();
                        st.IsFatigued = false;
                        st.RemoveCondition(CombatConditionType.Fatigued);
                        string refusal = RageActionExecutor.RefusalOf(barb, out LegalityBar bar);
                        Note(ctx, "second.refusal", refusal != null && refusal.Contains("already raged") ? "already raged/" + bar : (refusal ?? "allowed"));
                        ActionOutcome o = ctx.Gm.RunAIActionNow(ActionContext.For(ActionIds.Rage, barb, global::ActorSource.AI));
                        Note(ctx, "second.performed", o.Performed);
                        Note(ctx, "second.raging", st.IsRaging);
                        return true;
                    }))
                .Expect("The barbarian's turn is a Ui turn", Expect.Controller("barb", "ui"))
                .Expect("It starts fatigued and has not raged", Fact("pre", "True False"))
                .Expect("The Rage step is done", Expect.StepStatus("barb", 1, "Rage", 0, "done"))
                // Con 14 + 4 = 18: +4, so 3 + 4 = 7 rounds (PHB p.25: the newly improved modifier).
                .Expect("The fatigued barbarian rages, for 3 + its raged Con modifier rounds", Expect.All(Fact("raging", "True"), Fact("rounds", "7")))
                .Expect("A second rage this encounter is refused on the Resource bar", Fact("second.refusal", "already raged/" + LegalityBar.Resource))
                .Expect("The AI runner rages nothing", Expect.All(Fact("second.performed", "False"), Fact("second.raging", "False")))
                .Build();
        }

        /// <summary>
        /// A barbarian may rage only on its own turn (PHB p.25; D10). The dummy acts first: on its turn the rage executor
        /// refuses the barbarian ("only on its own turn") and an attempt through the AI runner rages nothing. On its own Ui
        /// turn the barbarian presses Rage: it rages, and its standard and move actions are left (no action, D10).
        /// </summary>
        private static ScenarioDef RageOwnTurnOnlyUi()
        {
            return Rules("rules/rage-own-turn-only-ui", "A barbarian rages only on its own turn and the rage takes no action (PHB p.25; D10, CMB-201)")
                .Covers("D10", "PHB p.25", "AI-054", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("dummy", "target_dummy", 7, 10, Control.Scripted)
                .Tweak("dummy", Sturdy)
                .Pc("barb", ActorSource.Stats(() => Barbarian("Barb", 3)), 5, 10, Control.Ui)
                .Tweak("barb", Sturdy)
                .Initiative("dummy", "barb")
                .Turn("dummy", 1, Step.Assert("the barbarian may not rage on the dummy's turn", ctx =>
                {
                    CharacterController barb = ctx.Get("barb");
                    string refusal = RageActionExecutor.RefusalOf(barb, out LegalityBar bar);
                    Note(ctx, "offturn.refusal", refusal != null && refusal.Contains("own turn") ? "own turn/" + bar : (refusal ?? "allowed"));
                    ActionOutcome o = ctx.Gm.RunAIActionNow(ActionContext.For(ActionIds.Rage, barb, global::ActorSource.AI));
                    Note(ctx, "offturn.performed", o.Performed);
                    Note(ctx, "offturn.raging", barb.Stats.IsRaging);
                    return true;
                }), Step.Pass())
                .Turn("barb", 1, Step.Rage(), Step.Assert("after the rage", ctx =>
                {
                    CharacterController barb = ctx.Get("barb");
                    Note(ctx, "own.raging", barb.Stats.IsRaging);
                    Note(ctx, "own.standard", barb.Actions.HasStandardAction);
                    Note(ctx, "own.move", barb.Actions.HasMoveAction);
                    return true;
                }))
                .Expect("The barbarian's turn is a Ui turn", Expect.Controller("barb", "ui"))
                .Expect("Off its turn the rage is refused on the Action bar", Fact("offturn.refusal", "own turn/" + LegalityBar.Action))
                .Expect("Off its turn the AI runner rages nothing", Expect.All(Fact("offturn.performed", "False"), Fact("offturn.raging", "False")))
                .Expect("The Rage step is done", Expect.StepStatus("barb", 1, "Rage", 0, "done"))
                .Expect("On its own turn it rages and keeps its standard and move actions",
                    Expect.All(Fact("own.raging", "True"), Fact("own.standard", "True"), Fact("own.move", "True")))
                .Build();
        }
    }
}
#endif
