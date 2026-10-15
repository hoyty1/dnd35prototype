#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Scenarios
{
    /// <summary>
    /// Paladin class features on one PC/NPC rules path (CHR-020; PHB p.44): Smite Evil through SmiteRules (declared
    /// before the attack by the PC Smite button, a scripted step or the AI; +Charisma bonus to the attack and +paladin
    /// level to damage against an evil creature; used up with no effect against any other) and Lay on Hands through
    /// LayOnHandsRules (a standard action: heals itself or a creature within touch, or damages an undead creature after a
    /// melee touch attack). The PC paladin is a Human paladin built from stats (STR 16, CHA 14) with the class kit's
    /// longsword (the shield is removed, so no off-hand prompt): BAB +3 at 3rd and +4 at 4th, STR +3, so +6 / +7.
    /// NPC data checked through the NPC database files on 2026-10-14: human_paladin_3 (Paladin 3, STR 16, CHA 15: smite
    /// +2/+3, a 6-point Lay on Hands pool, longsword), target_dummy (Commoner 1, true neutral, 50 base HP, AC 6),
    /// zombie_shambler (undead).
    /// </summary>
    public static class PaladinScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("paladin-smite-ai", SmiteAi);
            yield return S("paladin-smite-ui", SmiteUi);
            yield return S("paladin-smite-wasted-ui", SmiteWastedUi);
            yield return S("paladin-smite-npc", SmiteNpc);
            yield return S("paladin-lay-on-hands-ui", LayOnHandsUi);
            yield return S("paladin-lay-on-hands-ai", LayOnHandsAi);
            yield return S("paladin-lay-on-hands-undead", LayOnHandsUndead);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("PaladinScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        /// <summary>A Human paladin: STR 16 (+3), CHA 14 (+2); BAB from the paladin table.</summary>
        internal static CharacterStats Paladin(string name, int level)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Paladin",
                str: 16, dex: 10, con: 14, wis: 12, intelligence: 10, cha: 14,
                bab: 0, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 10 * level, raceName: "Human");
            s.CharacterAlignment = Alignment.LawfulGood;
            s.EnsureMulticlassDataInitialized();
            return s;
        }

        private static void Sturdy(CharacterController c, Alignment alignment)
        {
            c.Stats.CharacterAlignment = alignment;
            c.Stats.AdjustMaxHP(200);
            c.Stats.CurrentHP += 200;
        }

        private static bool HasTerm(TraceEvent a, string term) => (a.Str("dmgTerms") ?? string.Empty).IndexOf(term, StringComparison.Ordinal) >= 0;

        /// <summary>The first non-AoO attack by <paramref name="by"/> in <paramref name="round"/>: its mod and whether it carries the smite term.</summary>
        private static Func<TraceView, ExpectResult> FirstAttack(string by, int round, int mod, string smiteTerm, bool smiteExpected)
        {
            return v =>
            {
                TraceEvent a = v.Attacks(by, aoo: false, round: round).FirstOrDefault();
                if (a == null)
                    return ExpectResult.Fail("no attack by " + by + " in round " + round);
                if (a.Int("mod") != mod)
                    return ExpectResult.Fail("attack mod " + a.Int("mod") + ", expected " + mod + " (terms " + a.Str("dmgTerms") + ")", a.Seq);
                if (HasTerm(a, smiteTerm) != smiteExpected)
                    return ExpectResult.Fail("damage terms '" + a.Str("dmgTerms") + "' " + (smiteExpected ? "lack " : "have ") + smiteTerm, a.Seq);
                return ExpectResult.Pass("mod " + CharacterStats.FormatMod(mod) + (smiteExpected ? ", " + smiteTerm : ", no smite"), a.Seq);
            };
        }

        /// <summary>
        /// The AI path: a scripted Attack runs NPCPerformAttackForAI, where the AI declares Smite Evil against the evil dummy
        /// (AIProfile.DefaultShouldDeclareSmite) before its melee sequence. Paladin 3: +6 +2 = +8 and +3 damage in round 1;
        /// the day's only smite is gone in round 2 (+6, no smite term).
        /// </summary>
        private static ScenarioDef SmiteAi()
        {
            return Rules("rules/paladin-smite-ai", "The AI paladin 3 smites an evil foe: +2 attack, +3 damage, once a day (PHB p.44; CHR-020)")
                .Covers("CHR-020", "PHB p.44", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("pal", ActorSource.Stats(() => Paladin("Paladin", 3)), 10, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("pal", RulesScenarios.StripOffHand)
                .Tweak("dummy", c => Sturdy(c, Alignment.ChaoticEvil))
                .Initiative("pal", "dummy")
                .Turn("pal", 1, Step.Attack("dummy"))
                .Turn("pal", 2, Step.Attack("dummy"))
                .Turn("dummy", 0, Step.Pass())
                .Expect("Round 1: the smiting attack at +8 (BAB 3, STR 3, CHA 2) with Smite Evil +3 damage", FirstAttack("pal", 1, 8, "Smite Evil:3", true))
                .Expect("Round 2: no smite left (1/day at 3rd): +6, no smite damage", FirstAttack("pal", 2, 6, "Smite Evil", false))
                .Build();
        }

        /// <summary>The PC path: the Smite button declares, the Attack button and the click make the smiting attack.</summary>
        private static ScenarioDef SmiteUi()
        {
            return Rules("rules/paladin-smite-ui", "A PC paladin 3 presses Smite, then attacks an evil foe: +8, +3 damage (PHB p.44; CHR-020)")
                .Covers("CHR-020", "PHB p.44", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("pal", ActorSource.Stats(() => Paladin("Paladin", 3)), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("pal", RulesScenarios.StripOffHand)
                .Tweak("dummy", c => Sturdy(c, Alignment.LawfulEvil))
                .Initiative("pal", "dummy")
                .Turn("pal", 1, Step.Smite("dummy"),
                    Step.Assert("the day's smite is spent", ctx => ctx.Get("pal").Stats.GetPaladinSmiteEvil().RemainingUses == 0
                        && ctx.Get("pal").Stats.DeclaredSmite == SmiteSource.None))
                .Turn("dummy", 1, Step.Pass())
                .Expect("The Smite step is done", Expect.StepStatus("pal", 1, "Smite", 0, "done"))
                .Expect("The smiting attack at +8 with Smite Evil +3 damage", FirstAttack("pal", 1, 8, "Smite Evil:3", true))
                .Expect("The smite is used up", Expect.AssertsPass())
                .Build();
        }

        /// <summary>A smite against a creature that is not evil has no effect and is still used up (PHB p.44).</summary>
        private static ScenarioDef SmiteWastedUi()
        {
            return Rules("rules/paladin-smite-wasted-ui", "A PC paladin smites a neutral dummy: no bonus, the smite is still used (PHB p.44; CHR-020)")
                .Covers("CHR-020", "PHB p.44")
                .MaxRounds(1)
                .Pc("pal", ActorSource.Stats(() => Paladin("Paladin", 3)), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("pal", RulesScenarios.StripOffHand)
                .Tweak("dummy", c => Sturdy(c, Alignment.TrueNeutral))
                .Initiative("pal", "dummy")
                .Turn("pal", 1, Step.Smite("dummy"),
                    Step.Assert("the wasted smite is spent", ctx => ctx.Get("pal").Stats.GetPaladinSmiteEvil().RemainingUses == 0))
                .Turn("dummy", 1, Step.Pass())
                .Expect("The attack at +6 with no smite term", FirstAttack("pal", 1, 6, "Smite Evil", false))
                .Expect("The smite is used up", Expect.AssertsPass())
                .Build();
        }

        /// <summary>
        /// An NPC paladin from the database (human_paladin_3: Paladin 3, CHA 15) smites an evil PC through the same AI
        /// declaration: the damage terms carry Smite Evil +3. The attack mod is not pinned (the AI may Power Attack).
        /// </summary>
        private static ScenarioDef SmiteNpc()
        {
            return Rules("rules/paladin-smite-npc", "An NPC paladin 3 smites an evil PC with paladin level to damage (PHB p.44; CHR-020)")
                .Covers("CHR-020", "PHB p.44", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Foe", 4)), 10, 10, Control.Scripted)
                .Npc("pal", "human_paladin_3", 11, 10, Control.Scripted)
                .Tweak("foe", c => Sturdy(c, Alignment.NeutralEvil))
                .Initiative("pal", "foe")
                .Turn("pal", 1, Step.Attack("foe"))
                .Turn("foe", 1, Step.Pass())
                .Expect("The NPC paladin's first attack carries Smite Evil +3", v =>
                {
                    TraceEvent a = v.Attacks("pal", aoo: false, round: 1).FirstOrDefault();
                    if (a == null) return ExpectResult.Fail("no attack");
                    return HasTerm(a, "Smite Evil:3")
                        ? ExpectResult.Pass("terms " + a.Str("dmgTerms"), a.Seq)
                        : ExpectResult.Fail("terms '" + a.Str("dmgTerms") + "' lack Smite Evil:3", a.Seq);
                })
                .Build();
        }

        /// <summary>
        /// The PC Lay on Hands button: a paladin 4 (CHA 14: 8 points a day) 6 hit points down heals itself 6 with its
        /// standard action; 2 points are left.
        /// </summary>
        private static ScenarioDef LayOnHandsUi()
        {
            return Rules("rules/paladin-lay-on-hands-ui", "A PC paladin 4 lays on hands on itself: heals 6 of its 8 points (PHB p.44; CHR-020)")
                .Covers("CHR-020", "PHB p.44", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("pal", ActorSource.Stats(() => Paladin("Paladin", 4)), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 14, 10, Control.Scripted)
                .Tweak("pal", c => { RulesScenarios.StripOffHand(c); c.Stats.CurrentHP -= 6; })
                .Initiative("pal", "dummy")
                .Turn("pal", 1, Step.LayOnHands("pal"),
                    Step.Assert("healed to full, 2 points left, standard action spent", ctx =>
                    {
                        CharacterController p = ctx.Get("pal");
                        return p.Stats.CurrentHP == p.Stats.TotalMaxHP && LayOnHandsRules.RemainingPool(p.Stats) == 2
                            && !p.Actions.HasStandardAction;
                    }))
                .Turn("dummy", 1, Step.Pass())
                .Expect("The Lay on Hands step is done", Expect.StepStatus("pal", 1, "LayOnHands", 0, "done"))
                .Expect("Healed 6 with 2 points left", Expect.AssertsPass())
                .Build();
        }

        /// <summary>
        /// The AI lays on hands on its own turn (AIProfile.DefaultLayOnHandsTarget): the NPC paladin 3 (6 points) at a third
        /// of its hit points heals itself 6 before fighting.
        /// </summary>
        private static ScenarioDef LayOnHandsAi()
        {
            return Rules("rules/paladin-lay-on-hands-ai", "The AI paladin 3 at low hit points lays on hands on itself for 6 (PHB p.44; CHR-020)")
                .Covers("CHR-020", "PHB p.44", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("foe", ActorSource.Stats(() => RulesScenarios.Fighter("Foe", 4)), 18, 10, Control.Scripted)
                .Npc("pal", "human_paladin_3", 10, 10, Control.Ai)
                .Tweak("pal", c => c.Stats.CurrentHP = c.Stats.TotalMaxHP / 3)
                .Initiative("pal", "foe")
                .Turn("foe", 1, Step.Pass())
                .Expect("The paladin heals itself 6 in round 1", v =>
                {
                    var heals = v.Of("hp").Where(e => e.Round == 1 && e.Str("actor") == "pal" && e.Int("new") - e.Int("old") == 6).ToList();
                    return heals.Count > 0
                        ? ExpectResult.Pass("hp " + heals[0].Int("old") + " -> " + heals[0].Int("new"), heals[0].Seq)
                        : ExpectResult.Fail("no +6 hp change on the paladin in round 1");
                })
                .Build();
        }

        /// <summary>
        /// Lay on Hands against an undead creature (scripted, the AI-path executor): a melee touch attack (forced 20), then
        /// the points the zombie has left, up to the 8-point pool, as damage with no save.
        /// </summary>
        private static ScenarioDef LayOnHandsUndead()
        {
            return Rules("rules/paladin-lay-on-hands-undead", "A paladin 4 lays on hands on a zombie: touch attack, then up to 8 damage (PHB p.44; CHR-020)")
                .Covers("CHR-020", "PHB p.44", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("pal", ActorSource.Stats(() => Paladin("Paladin", 4)), 10, 10, Control.Scripted)
                .Npc("zombie", "zombie_shambler", 11, 10, Control.Scripted)
                .Tweak("pal", RulesScenarios.StripOffHand)
                .Tweak("zombie", c => c.Stats.CurrentHP = Math.Max(1, Math.Min(c.Stats.CurrentHP, 20)))
                .Force(20, 20, LayOnHandsRules.TouchAttackRollContext)
                .Initiative("pal", "zombie")
                .Turn("pal", 1, Step.LayOnHands("zombie"))
                .Turn("zombie", 1, Step.Pass())
                .Expect("The Lay on Hands step is done", Expect.StepStatus("pal", 1, "LayOnHands", 0, "done"))
                .Expect("The zombie takes min(its hit points, 8) damage", v =>
                {
                    TraceEvent hp = v.Of("hp").FirstOrDefault(e => e.Round == 1 && e.Str("actor") == "zombie");
                    if (hp == null) return ExpectResult.Fail("no hp change on the zombie");
                    int old = hp.Int("old");
                    int want = Math.Min(old, 8);
                    return old - hp.Int("new") == want
                        ? ExpectResult.Pass("hp " + old + " -> " + hp.Int("new"), hp.Seq)
                        : ExpectResult.Fail("hp " + old + " -> " + hp.Int("new") + ", expected -" + want, hp.Seq);
                })
                .Build();
        }
    }
}
#endif
