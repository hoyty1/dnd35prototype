#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 12 step 6 (docs/designs/pc_npc_parity_batch12.md, decisions D3 and D14): every creature's turn-start effects,
    /// cooldowns and per-turn resets run once, from GameManager.OnTurnStarted through GameManager.BeginCombatantTurn,
    /// before the turn-skip gate and the PC/NPC fork; rage passes at the barbarian's initiative count; breath weapons
    /// recharge as their MM entries say. Before the step a skipped NPC missed regeneration, Melf's Acid Arrow and the
    /// per-turn resets (CMB-075), the NPC turn-start combat-end check ran before the acid tick (CORE-003), rage passed
    /// only on an acting PC's turn (CHR-069) and no breath weapon recharged (AI-032, CRE-073).
    /// </summary>
    public static class TurnStartScenarios
    {
        private const string NotePrefix = "b12s6 ";

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("turn-start-regeneration-skipped-npc", RegenerationSkippedNpc);
            yield return S("turn-start-acid-arrow-last-enemy", AcidArrowLastEnemy);
            yield return S("dragon-breath-recharges", DragonBreathRecharges);
            yield return S("rage-expires-ui", () => RageExpires(ui: true));
            yield return S("rage-expires-ai", () => RageExpires(ui: false));
            yield return S("fight-defensively-ends-skipped", () => FightDefensivelyEndsSkipped(ui: false));
            yield return S("fight-defensively-ends-skipped-ui", () => FightDefensivelyEndsSkipped(ui: true));
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("TurnStartScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        /// <summary>A Human barbarian: STR 16, DEX 12, CON 14 (rage would last 3 + 2 rounds; the scenario sets 2).</summary>
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

        // ── Regeneration on a skipped NPC turn (D3, CMB-075) ─────────────

        /// <summary>
        /// A paralyzed AI troll (MM p.247: regeneration 5) at 30 of 63 hp: its turns are skipped, but regeneration works every
        /// round (MM p.314; D3), so it is at 35 hp after its round-1 turn and 40 after round 2. Before batch 12 step 6 the NPC
        /// regeneration tick ran after the skip gate and the troll stayed at 30.
        /// </summary>
        private static ScenarioDef RegenerationSkippedNpc()
        {
            return Rules("rules/turn-start-regeneration-skipped-npc", "A paralyzed troll still regenerates at its turn start (MM p.314; D3, CMB-075)")
                .Covers("CMB-075", "MM p.314", "MM p.247", "D3", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Npc("troll", "troll", 12, 10, Control.Ai)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 9, 10, Control.Scripted)
                .Hp("troll", 30)
                .StartCondition("troll", CombatConditionType.Paralyzed, 10)
                .Initiative("troll", "hero")
                .Turn("hero", 1, Step.Assert("note the troll's hp after round 1", ctx => NoteHp(ctx, "troll", "r1")), Step.Pass())
                .Turn("hero", 2, Step.Assert("note the troll's hp after round 2", ctx => NoteHp(ctx, "troll", "r2")), Step.Pass())
                .Expect("The troll's turns are skipped (paralyzed) and it takes no action",
                    Expect.None("attack", e => e.Str("by") == "troll"))
                .Expect("Regeneration 5 at each of its turn starts: 35 hp after round 1, 40 after round 2 (MM p.314, D3)",
                    Expect.All(Fact("troll.hp.r1", "35"), Fact("troll.hp.r2", "40")))
                .Build();
        }

        private static bool NoteHp(ScenarioContext ctx, string key, string tag)
        {
            CharacterController c = ctx.Get(key);
            ctx.Note(NotePrefix + key + ".hp." + tag + "=" + (c != null && c.Stats != null ? c.Stats.CurrentHP.ToString() : "none"));
            return true;
        }

        // ── Melf's Acid Arrow ends the fight at the turn start (CORE-003, SPL-200) ──

        /// <summary>
        /// The last enemy, an AI goblin at 1 hp, carries Melf's Acid Arrow (2 rounds left, put on it by the Scripted hero's
        /// round-1 step). At the goblin's turn start the acid deals 2d4 (PHB p.253) through the shared damage path and drops it
        /// to -1 or lower, dying, so the one turn-start check ends the fight at once (BeginCombatantTurn, called from
        /// OnTurnStarted) and the goblin never acts. Before batch 12 step 6 the NPC turn-start check ran before the acid tick,
        /// so the fight ended only after the goblin's turn (CORE-003). The harness's global waiver for a phase set after
        /// CombatOver is gone, so any such phase change fails this job.
        /// </summary>
        private static ScenarioDef AcidArrowLastEnemy()
        {
            return Rules("rules/turn-start-acid-arrow-last-enemy", "A Melf's Acid Arrow tick that drops the last enemy ends the fight at its turn start (PHB p.253; CORE-003, SPL-200)")
                .Covers("CORE-003", "SPL-200", "CMB-075", "PHB p.253", "D3")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 3)), 6, 10, Control.Scripted)
                .Npc("gob", "goblin", 12, 10, Control.Ai)
                .Hp("gob", 1)
                .Initiative("hero", "gob")
                .Turn("hero", 1, Step.Assert("put Melf's Acid Arrow (2 rounds) on the goblin", ctx =>
                {
                    CharacterController gob = ctx.Get("gob");
                    gob.ApplyMelfsAcidArrowEffect(2, ctx.Get("hero"));
                    return gob.HasActiveMelfsAcidArrowEffect;
                }), Step.Pass())
                .Expect("The acid tick drops the goblin and the game declares the victory itself", Expect.Outcome(Outcome.Victory))
                .Expect("The fight ends at the goblin's turn-start check, in round 1 (BeginCombatantTurn; CORE-003)", v =>
                {
                    TraceEvent end = v.Of("phase").FirstOrDefault(e => e.Get("to")?.ToString() == "CombatOver");
                    if (end == null)
                        return ExpectResult.Fail("no CombatOver phase");
                    List<string> by = ByFrames(end);
                    string text = string.Join(" < ", by);
                    return end.Round == 1 && by.Contains("BeginCombatantTurn")
                        ? ExpectResult.Pass("round " + end.Round + " by " + text, end.Seq)
                        : ExpectResult.Fail("round " + end.Round + " by " + text, end.Seq);
                })
                .Expect("The goblin never acts: no attack, move or cast", Expect.All(
                    Expect.None("attack", e => e.Str("by") == "gob"),
                    Expect.None("move", e => e.Str("by") == "gob" || e.Str("actor") == "gob"),
                    Expect.None("cast", e => e.Str("by") == "gob")))
                .Expect("The acid tick is logged", v =>
                    v.Log("Acid continues to burn").Count > 0 ? ExpectResult.Pass("logged") : ExpectResult.Fail("no acid tick log"))
                .Build();
        }

        private static List<string> ByFrames(TraceEvent e)
        {
            var result = new List<string>();
            if (e.Get("by") is IEnumerable list && !(e.Get("by") is string))
                foreach (object o in list)
                    result.Add(o != null ? o.ToString() : "");
            else if (e.Get("by") is string s)
                result.Add(s);
            return result;
        }

        // ── Breath weapon recharge (D14, AI-032, CRE-073) ────────────────

        /// <summary>
        /// A Scripted young red dragon breathes whenever its breath is ready (the AI's breath executor,
        /// NPCExecuteBreathWeaponForAI) at two sturdy Idle fighters. MM p.69: once a dragon breathes, it cannot breathe again
        /// until 1d4 rounds later; every recharge d4 is forced to 2 (RollFilter through Force), and the rounds pass at the
        /// dragon's turn start (TickTurnCooldowns), so it breathes in rounds 1 and 3 only, over 4 rounds. The round-1 script
        /// also audits every breath weapon and ranged special in the bestiary against its MM page (D14).
        /// </summary>
        private static ScenarioDef DragonBreathRecharges()
        {
            return Rules("rules/dragon-breath-recharges", "A dragon's breath recharges in 1d4 rounds, and every breath entry recharges as its MM page says (MM p.69; D14, AI-032, CRE-073)")
                .Covers("AI-032", "CRE-073", "MM p.69", "D14")
                .MaxRounds(4)
                .RecordDice()
                .Npc("dragon", "dragon_red_young", 12, 10, Control.Scripted)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 5)), 9, 10, Control.Idle)
                .Pc("hero2", ActorSource.Stats(() => RulesScenarios.Fighter("Hero2", 5)), 9, 11, Control.Idle)
                .Tweak("hero", Sturdy)
                .Tweak("hero2", Sturdy)
                .Tweak("dragon", Sturdy)
                .Initiative("dragon", "hero", "hero2")
                .Force(4, 2, "breath weapon recharge", -1)
                .Script("dragon", BreatheWhenReady)
                .Expect("The dragon breathes in rounds 1 and 3, never in rounds 2 and 4 (1d4 = 2 rounds; MM p.69)", v =>
                {
                    List<TraceEvent> breaths = v.Log(@"unleashes a \d+-ft (cone|line) of");
                    string rounds = string.Join(",", breaths.Select(e => e.Round));
                    return rounds == "1,3"
                        ? ExpectResult.Pass("breaths in rounds " + rounds)
                        : ExpectResult.Fail("breaths in rounds [" + rounds + "], expected 1,3");
                })
                .Expect("The dragon's breath is ready only in rounds 1 and 3, with 2 rounds left after each breath",
                    Expect.All(Fact("dragon.ready.r1", "True"), Fact("dragon.ready.r2", "False"), Fact("dragon.ready.r3", "True"),
                        Fact("dragon.ready.r4", "False"), Fact("dragon.cooldown.after.r1", "2"), Fact("dragon.cooldown.after.r3", "2")))
                .Expect("Each breath rolls one recharge d4 (two breaths)",
                    Expect.Count("dice", e => (e.Str("ctx") ?? "").Contains("breath weapon recharge"), 2, 2))
                .Expect("Every bestiary breath weapon and ranged special recharges as its MM entry says (D14)",
                    Expect.All(
                        Fact("audit.ankheg", "fixed 3600"),
                        Fact("audit.behir", "fixed 10"),
                        Fact("audit.chimera", "1d4"),
                        Fact("audit.digester", "1d4"),
                        Fact("audit.gorgon", "1d4 max 5/day"),
                        Fact("audit.hell_hound", "2d4"),
                        Fact("audit.giant_bombardier_beetle", "once per round"),
                        Fact("audit.dragons", "all 1d4"),
                        Fact("audit.unaudited", "none"),
                        Fact("audit.ranged.gibbering_mouther", "free action, no recharge")))
                .Build();
        }

        /// <summary>The dragon's script: note readiness, breathe when ready, note the cooldown; round 1 also runs the audit.</summary>
        private static IEnumerator BreatheWhenReady(ScenarioContext ctx, CharacterController dragon)
        {
            int round = ctx.Gm.CurrentRound;
            if (round == 1)
                AuditRecharges(ctx);
            bool ready = dragon != null && dragon.HasBreathWeapon && dragon.IsBreathWeaponReady;
            ctx.Note(NotePrefix + "dragon.ready.r" + round + "=" + ready);
            if (!ready)
                yield break;
            yield return ctx.Gm.StartCoroutine(ctx.Gm.NPCExecuteBreathWeaponForAI(dragon, ctx.Get("hero")));
            ctx.Note(NotePrefix + "dragon.cooldown.after.r" + round + "=" + dragon.BreathWeaponCooldownRemaining);
        }

        private static string Recharge(BreathWeaponDefinition d)
        {
            if (d.OncePerRound)
                return "once per round";
            string r = string.IsNullOrEmpty(d.RechargeDice) ? "fixed " + d.RechargeRounds : d.RechargeDice;
            return d.UsesPerDay > 0 ? r + " max " + d.UsesPerDay + "/day" : r;
        }

        /// <summary>Notes the recharge of every bestiary entry with a breath weapon or a ranged special (MM pages in D14).</summary>
        private static void AuditRecharges(ScenarioContext ctx)
        {
            var known = new HashSet<string> { "ankheg", "behir", "chimera", "digester", "gorgon", "hell_hound", "giant_bombardier_beetle" };
            var unaudited = new List<string>();
            var dragonsOff = new List<string>();
            foreach (NPCDefinition def in NPCDatabase.AllNPCs)
            {
                if (def == null)
                    continue;
                if (def.BreathWeapon != null)
                {
                    string r = Recharge(def.BreathWeapon);
                    if (known.Contains(def.Id))
                        ctx.Note(NotePrefix + "audit." + def.Id + "=" + r);
                    else if (def.Id != null && def.Id.StartsWith("dragon_", StringComparison.Ordinal))
                    {
                        if (r != "1d4")
                            dragonsOff.Add(def.Id + ":" + r);
                    }
                    else
                        unaudited.Add(def.Id + ":" + r);
                }
                if (def.RangedSpecialAttack != null)
                {
                    RangedSpecialAttackDefinition a = def.RangedSpecialAttack;
                    string r = a.IsFreeAction && a.CooldownRounds == 0 && string.IsNullOrEmpty(a.RechargeDice)
                        ? "free action, no recharge"
                        : (string.IsNullOrEmpty(a.RechargeDice) ? "fixed " + a.CooldownRounds : a.RechargeDice);
                    if (def.Id == "gibbering_mouther")
                        ctx.Note(NotePrefix + "audit.ranged." + def.Id + "=" + r);
                    else
                        unaudited.Add(def.Id + ":ranged " + r);
                }
            }
            ctx.Note(NotePrefix + "audit.dragons=" + (dragonsOff.Count == 0 ? "all 1d4" : string.Join(" ", dragonsOff)));
            ctx.Note(NotePrefix + "audit.unaudited=" + (unaudited.Count == 0 ? "none" : string.Join(" ", unaudited)));
        }

        // ── Rage at the barbarian's initiative count (D3, CHR-069) ───────

        /// <summary>
        /// A barbarian 1 rages (activated by the Scripted dummy's round-1 step, set to 2 rounds left). A round of rage passes at
        /// the barbarian's initiative count, PC or NPC, acting or not (PHB p.25; D3): 1 round left in round 2, ended and
        /// fatigued by round 3. The Ui barbarian is paralyzed for its round-2 turn, so its rage also passes on a turn it
        /// cannot use; the AI barbarian (the NPC turn path) acts normally. Before batch 12 step 6 rage ticked only on an
        /// acting PC's turn (CHR-069): the paralyzed Ui barbarian and the AI one were still raging in round 3.
        /// </summary>
        private static ScenarioDef RageExpires(bool ui)
        {
            string id = ui ? "rules/rage-expires-ui" : "rules/rage-expires-ai";
            ScenarioBuilder b = Rules(id, ui
                    ? "A PC barbarian's rage runs out at its initiative count, also on a paralyzed turn (PHB p.25; D3, CHR-069)"
                    : "An AI barbarian's rage runs out at its initiative count (PHB p.25; D3, CHR-069)")
                .Covers("CHR-069", "PHB p.25", "D3", "PC_NPC_PARITY")
                .MaxRounds(3)
                .Pc("barb", ActorSource.Stats(() => Barbarian("Barb", 1)), 9, 10, ui ? Control.Ui : Control.Ai)
                .Npc("dummy", "target_dummy", 12, 10, Control.Scripted)
                .Tweak("dummy", Sturdy)
                .Initiative("dummy", "barb")
                .Turn("dummy", 1, Step.Assert("the barbarian rages, 2 rounds left", ctx =>
                {
                    CharacterStats s = ctx.Get("barb").Stats;
                    bool ok = s.ActivateRage();
                    s.RageRoundsRemaining = 2;
                    return ok && s.IsRaging;
                }), Step.Pass())
                .Turn("dummy", 2, Step.Assert("note the rage in round 2" + (ui ? " and paralyze the barbarian for 1 round" : ""), ctx =>
                {
                    NoteRage(ctx, "r2");
                    if (ui)
                        ctx.Get("barb").ApplyCondition(CombatConditionType.Paralyzed, 1, "Scenario fixture");
                    return true;
                }), Step.Pass())
                .Turn("dummy", 3, Step.Assert("note the rage in round 3", ctx => NoteRage(ctx, "r3")), Step.Pass())
                .Expect("Round 2: still raging with 1 round left (one tick at the barbarian's round-1 count)", Fact("rage.r2", "True 1 False"))
                .Expect("Round 3: the rage ended at the barbarian's round-2 count and it is fatigued (PHB p.25)", Fact("rage.r3", "False 0 True"));
            if (ui)
                b = b.Turn("barb", 1, Step.Pass()).Turn("barb", 3, Step.Pass())
                    .Expect("The paralyzed barbarian's round-2 turn is skipped", v =>
                        v.Log("cannot act this turn").Any(e => e.Round == 2)
                            ? ExpectResult.Pass("skipped in round 2")
                            : ExpectResult.Fail("no skip log in round 2: the paralyzed turn was not skipped, so D3 is unproven"));
            return b.Build();
        }

        private static bool NoteRage(ScenarioContext ctx, string tag)
        {
            CharacterStats s = ctx.Get("barb").Stats;
            ctx.Note(NotePrefix + "rage." + tag + "=" + s.IsRaging + " " + s.RageRoundsRemaining + " " + s.IsFatigued);
            return true;
        }

        // ── Fighting defensively ends on a skipped turn (D3, PHB p.142) ──

        /// <summary>
        /// A fighter fights defensively in round 1 (+2 dodge AC until its next turn, PHB p.142; set through
        /// SetFightingDefensively as the attack step would) and is then paralyzed by the dummy's step. Its round-2 turn is
        /// skipped, but its turn start still runs the per-turn resets (StartNewTurn, D3), so the AC bonus is gone. Run as a
        /// Scripted fighter (the NPC turn path) and as a Ui fighter (the PC turn path): before batch 12 step 6 a skipped turn
        /// on either path ran no StartNewTurn and the bonus stayed.
        /// </summary>
        private static ScenarioDef FightDefensivelyEndsSkipped(bool ui)
        {
            ScenarioBuilder b = Rules(ui ? "rules/fight-defensively-ends-skipped-ui" : "rules/fight-defensively-ends-skipped",
                    (ui ? "A PC's fighting defensively" : "Fighting defensively")
                    + " ends at the creature's next turn start, also on a skipped turn (PHB p.142; D3, CMB-075)")
                .Covers("CMB-075", "PHB p.142", "D3", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("def", ActorSource.Stats(() => RulesScenarios.Fighter("Defender", 3)), 9, 10, ui ? Control.Ui : Control.Scripted)
                .Npc("dummy", "target_dummy", 12, 10, Control.Scripted)
                .Tweak("dummy", Sturdy)
                .Initiative("def", "dummy")
                .Turn("def", 1, Step.Assert("the fighter fights defensively", ctx =>
                {
                    ctx.Get("def").SetFightingDefensively(true);
                    return ctx.Get("def").IsFightingDefensively;
                }), Step.Pass())
                .Turn("dummy", 1, Step.Assert("note the stance and paralyze the fighter for 2 rounds", ctx =>
                {
                    CharacterController d = ctx.Get("def");
                    ctx.Note(NotePrefix + "def.fd.r1=" + d.IsFightingDefensively);
                    d.ApplyCondition(CombatConditionType.Paralyzed, 2, "Scenario fixture");
                    return true;
                }), Step.Pass())
                .Turn("dummy", 2, Step.Assert("note the stance after the skipped turn", ctx =>
                {
                    CharacterController d = ctx.Get("def");
                    ctx.Note(NotePrefix + "def.fd.r2=" + d.IsFightingDefensively + " paralyzed=" + d.HasCondition(CombatConditionType.Paralyzed));
                    return true;
                }), Step.Pass())
                .Expect("Round 1: fighting defensively after its turn", Fact("def.fd.r1", "True"))
                .Expect("Round 2: the skipped, still paralyzed fighter's turn start ended the stance (PHB p.142, D3)", Fact("def.fd.r2", "False paralyzed=True"));
            if (ui)
                b = b.Expect("The paralyzed Ui fighter's round-2 turn is skipped", v =>
                    v.Log("cannot act this turn").Any(e => e.Round == 2)
                        ? ExpectResult.Pass("skipped in round 2")
                        : ExpectResult.Fail("no skip log in round 2: the paralyzed turn was not skipped"));
            return b.Build();
        }

        /// <summary>The last note <paramref name="key"/> equals <paramref name="want"/>.</summary>
        private static Func<TraceView, ExpectResult> Fact(string key, string want)
            => v =>
            {
                string prefix = NotePrefix + key + "=";
                TraceEvent n = v.Of("note").LastOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
                if (n == null)
                    return ExpectResult.Inconclusive("no fact " + key);
                string got = n.Str("text").Substring(prefix.Length);
                return got == want ? ExpectResult.Pass(key + "=" + got, n.Seq) : ExpectResult.Fail(key + "=" + got + ", expected " + want, n.Seq);
            };
    }
}
#endif
