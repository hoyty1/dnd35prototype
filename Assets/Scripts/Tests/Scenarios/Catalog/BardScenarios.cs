#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Scenarios
{
    /// <summary>
    /// The bard's bardic music on one PC/NPC rules path (CHR-020, CHR-026; PHB p.29-30): BardicMusicRules runs the PC
    /// Bardic Music button and its lists, the scripted steps (GameManager.TryBardicMusicForAI and its siblings) and the AI
    /// (AIProfile.DefaultBardicMusicChoice). The bards are Human bards built from stats (CHA 18, Perform at the class-skill
    /// maximum, level + 3); the foes are target dummies (bestiary entry target_dummy, "Arcane Target Dummy", 50 base HP, AC 6, Int 1;
    /// checked 2026-10-15). Fascinate's Will DC is the bard's Perform check, so the fascinate scenarios give the bard a +20
    /// bonus on Perform checks (a test fixture, Tweak) to make the dummies' saves fail whatever the seed.
    /// </summary>
    public static class BardScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("bard-inspire-courage-ui", InspireCourageUi);
            yield return S("bard-inspire-courage-ai", InspireCourageAi);
            yield return S("bard-fascinate-scripted", FascinateScripted);
            yield return S("bard-fascinate-ai", FascinateAi);
            yield return S("bard-suggestion-ui", SuggestionUi);
            yield return S("bard-fascinate-full-attack-ui", () => FascinateFullAttack(ai: false));
            yield return S("bard-fascinate-full-attack-ai", () => FascinateFullAttack(ai: true));
            yield return S("bard-fascinate-trip-scripted", FascinateTripScripted);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("BardScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        /// <summary>A Human bard: STR 10, DEX 14, CON 12, CHA 18, Perform ranks level + 3.</summary>
        internal static CharacterStats Bard(string name, int level)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Bard",
                str: 10, dex: 14, con: 12, wis: 10, intelligence: 12, cha: 18,
                bab: 0, armorBonus: 0, shieldBonus: 0,
                damageDice: 6, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 6 * level, raceName: "Human");
            s.CharacterAlignment = Alignment.ChaoticGood;
            s.EnsureMulticlassDataInitialized();
            s.Skills["Perform"] = new Skill("Perform", AbilityType.CHA, isClassSkill: true) { Ranks = level + 3 };
            s.EnsureBardicMusicInitialized();
            return s;
        }

        /// <summary>A Human fighter ally: STR 16.</summary>
        internal static CharacterStats Fighter(string name, int level)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Fighter",
                str: 16, dex: 12, con: 14, wis: 10, intelligence: 10, cha: 10,
                bab: 0, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 10 * level, raceName: "Human");
            s.EnsureMulticlassDataInitialized();
            return s;
        }

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        /// <summary>Test fixture: +20 on the bard's Perform checks, so fascinate's DC beats any dummy's save.</summary>
        private static void LoudBard(CharacterController c)
            => c.Stats.Bonuses.Set("scenario_perform", BonusTarget.Skill, BonusType.Circumstance, 20, "Scenario fixture", "Perform");

        /// <summary>
        /// The PC path: a bard 2 presses Bardic Music and picks Inspire Courage (round 1): the bard and his fighter ally
        /// get +1 morale on attack and weapon damage rolls (the fighter's attack and weapon damage totals), the dummy does
        /// not; one use of two is spent; the fighter then attacks.
        /// </summary>
        private static ScenarioDef InspireCourageUi()
        {
            return Rules("rules/bard-inspire-courage-ui", "A PC bard 2 sings Inspire Courage: +1 morale for the bard and the ally (PHB p.29; CHR-026)")
                .Covers("CHR-020", "CHR-026", "PHB p.29", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("bard", ActorSource.Stats(() => Bard("Bard", 2)), 8, 10, Control.Ui)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 2)), 10, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("dummy", Sturdy)
                .Initiative("bard", "fighter", "dummy")
                .Turn("bard", 1, Step.BardicMusic(BardicAbility.InspireCourage),
                    Step.Assert("the bard and the ally are inspired (+1), the dummy is not; 1 of 2 uses left", ctx =>
                    {
                        CharacterController b = ctx.Get("bard"), f = ctx.Get("fighter"), d = ctx.Get("dummy");
                        return b.Stats.HasInspireCourageBonus && f.Stats.AppliedInspireCourageValue == 1 && !d.Stats.HasInspireCourageBonus
                            && f.Stats.Bonuses.Total(BonusTarget.AttackRoll) == 1 && f.Stats.EffectWeaponDamageBonus == 1
                            && BardicMusicRules.UsesLeft(b.Stats) == 1 && BardicMusicRules.ActiveAbility(b.Stats) == BardicAbility.InspireCourage;
                    }))
                .Turn("fighter", 1, Step.Attack("dummy"))
                .Turn("dummy", 0, Step.Pass())
                .Expect("The Bardic Music step is done", Expect.StepStatus("bard", 1, "BardicMusic", 0, "done"))
                .Expect("Inspire Courage is on as the rules say", Expect.AssertsPass())
                .Build();
        }

        /// <summary>
        /// The AI path: an AI-run bard 2 next to a fight sings Inspire Courage on its own (AIProfile.DefaultBardicMusicChoice)
        /// and the ally is inspired; in round 2 the song goes on without a second use.
        /// </summary>
        private static ScenarioDef InspireCourageAi()
        {
            return Rules("rules/bard-inspire-courage-ai", "The AI bard 2 sings Inspire Courage for itself and its ally (PHB p.29; CHR-026)")
                .Covers("CHR-020", "CHR-026", "PHB p.29", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("bard", ActorSource.Stats(() => Bard("Bard", 2)), 8, 10, Control.Ai)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 2)), 10, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("dummy", Sturdy)
                .Initiative("bard", "fighter", "dummy")
                .Turn("fighter", 1, Step.Assert("the AI bard sang Inspire Courage: the ally has +1", ctx =>
                    ctx.Get("fighter").Stats.AppliedInspireCourageValue == 1
                    && BardicMusicRules.ActiveAbility(ctx.Get("bard").Stats) == BardicAbility.InspireCourage), Step.Pass())
                .Turn("fighter", 2, Step.Assert("the song goes on in round 2 on the same use (1 of 2 left)", ctx =>
                    ctx.Get("fighter").Stats.HasInspireCourageBonus && BardicMusicRules.UsesLeft(ctx.Get("bard").Stats) == 1), Step.Pass())
                .Turn("dummy", 0, Step.Pass())
                .Expect("Inspire Courage from the AI bard, one use", Expect.AssertsPass())
                .Build();
        }

        /// <summary>
        /// The NPC executor: a scripted bard 4 fascinates two dummies 55 ft away (round 1); in round 2 it concentrates and
        /// its ally's attack on one dummy breaks that fascination at once (an obvious threat); in round 3 the bard does not
        /// concentrate and the other fascination ends with the song.
        /// </summary>
        private static ScenarioDef FascinateScripted()
        {
            return Rules("rules/bard-fascinate-scripted", "A scripted bard 4 fascinates two foes; an attack breaks one, no concentration ends the other (PHB p.29; CHR-026)")
                .Covers("CHR-020", "CHR-026", "PHB p.29", "DMG p.301", "PC_NPC_PARITY")
                .MaxRounds(3)
                .Pc("bard", ActorSource.Stats(() => Bard("Bard", 4)), 3, 10, Control.Scripted)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 4)), 3, 11, Control.Scripted)
                .Npc("dummyA", "target_dummy", 14, 10, Control.Scripted)
                .Npc("dummyB", "target_dummy", 14, 13, Control.Scripted)
                .Tweak("bard", LoudBard)
                .Tweak("dummyA", Sturdy)
                .Tweak("dummyB", Sturdy)
                // Unarmored, so its two 30-ft moves reach dummyA (the kit's scale mail would make them 20 ft, PHB p.123; ITM-010).
                .Tweak("fighter", RulesScenarios.StripArmor)
                .Initiative("bard", "fighter", "dummyA", "dummyB")
                .Turn("bard", 1, Step.BardicMusic(BardicAbility.Fascinate, "dummyA", "dummyB"),
                    Step.Assert("both dummies are fascinated; one use spent; no standard action left", ctx =>
                        ctx.Get("dummyA").HasCondition(CombatConditionType.Fascinated) && ctx.Get("dummyB").HasCondition(CombatConditionType.Fascinated)
                        && BardicMusicRules.UsesLeft(ctx.Get("bard").Stats) == 3 && !ctx.Get("bard").Actions.HasStandardAction))
                .Turn("fighter", 1, Step.Move(9, 10))
                .Turn("bard", 2, Step.BardicConcentrate())
                .Turn("fighter", 2, Step.Move(13, 10), Step.Attack("dummyA"),
                    Step.Assert("the attack broke dummyA's fascination; dummyB is still fascinated", ctx =>
                        !ctx.Get("dummyA").HasCondition(CombatConditionType.Fascinated) && ctx.Get("dummyB").HasCondition(CombatConditionType.Fascinated)))
                .Turn("bard", 3, Step.Pass())
                .Turn("fighter", 3, Step.Assert("without concentration the song and dummyB's fascination ended", ctx =>
                    !BardicMusicRules.IsPerforming(ctx.Get("bard").Stats) && !ctx.Get("dummyB").HasCondition(CombatConditionType.Fascinated)), Step.Pass())
                .Turn("dummyA", 0, Step.Pass())
                .Turn("dummyB", 0, Step.Pass())
                .Expect("The Fascinate step is done", Expect.StepStatus("bard", 1, "BardicMusic", 0, "done"))
                .Expect("The Concentrate step is done", Expect.StepStatus("bard", 2, "BardicConcentrate", 0, "done"))
                .Expect("Fascination, its break and its end follow the rules", Expect.AssertsPass())
                .Build();
        }

        /// <summary>
        /// The AI path: an AI-run bard 4 with an ally beside it and two dummies 55 ft away, far from any fight, fascinates
        /// both (AIProfile.DefaultBardicMusicChoice: two foes it can fascinate) and in round 2 concentrates to keep them.
        /// </summary>
        private static ScenarioDef FascinateAi()
        {
            return Rules("rules/bard-fascinate-ai", "The AI bard 4 fascinates two distant foes and concentrates (PHB p.29; CHR-026)")
                .Covers("CHR-020", "CHR-026", "PHB p.29", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("bard", ActorSource.Stats(() => Bard("Bard", 4)), 3, 10, Control.Ai)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 4)), 3, 11, Control.Scripted)
                .Npc("dummyA", "target_dummy", 14, 10, Control.Scripted)
                .Npc("dummyB", "target_dummy", 14, 13, Control.Scripted)
                .Tweak("bard", LoudBard)
                .Tweak("dummyA", Sturdy)
                .Tweak("dummyB", Sturdy)
                .Initiative("bard", "fighter", "dummyA", "dummyB")
                .Turn("fighter", 1, Step.Assert("the AI bard fascinated both dummies", ctx =>
                    ctx.Get("dummyA").HasCondition(CombatConditionType.Fascinated) && ctx.Get("dummyB").HasCondition(CombatConditionType.Fascinated)
                    && BardicMusicRules.ActiveAbility(ctx.Get("bard").Stats) == BardicAbility.Fascinate), Step.Pass())
                .Turn("fighter", 2, Step.Assert("in round 2 it concentrated: still fascinating both, its standard action spent", ctx =>
                    ctx.Get("dummyA").HasCondition(CombatConditionType.Fascinated) && ctx.Get("dummyB").HasCondition(CombatConditionType.Fascinated)
                    && ctx.Get("bard").Stats.BardBardicMusic.ConcentratedThisTurn), Step.Pass())
                .Turn("dummyA", 0, Step.Pass())
                .Turn("dummyB", 0, Step.Pass())
                .Expect("The AI bard fascinates and keeps the song up", Expect.AssertsPass())
                .Build();
        }

        /// <summary>
        /// The PC path for Suggestion: a PC bard 6 fascinates an AI-run dummy (round 1, through the Fascinate list), makes it
        /// a suggestion (round 2; no use spent; the fascination goes on; Will DC 17 against a dummy of Wisdom 1, so the save
        /// fails whatever the seed; the dummy's Int is raised from 1 to 10, since a suggestion is language-dependent and a
        /// creature of Int 2 or less cannot understand it), and stops the song (round 3, no action): the dummy then follows
        /// the course and moves away from the bard on its own turn (SuggestedBehaviorController), and never attacks.
        /// </summary>
        private static ScenarioDef SuggestionUi()
        {
            return Rules("rules/bard-suggestion-ui", "A PC bard 6 fascinates a foe, makes it a suggestion, and it leaves (PHB p.29, p.285; CHR-026)")
                .Covers("CHR-020", "CHR-026", "PHB p.29", "PHB p.285", "PC_NPC_PARITY")
                .MaxRounds(4)
                .Pc("bard", ActorSource.Stats(() => Bard("Bard", 6)), 3, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 13, 10, Control.Ai)
                .Tweak("bard", LoudBard)
                .Tweak("dummy", c => { Sturdy(c); c.Stats.WIS = 1; c.Stats.INT = 10; })
                .Initiative("bard", "dummy")
                .Turn("bard", 1, Step.BardicMusic(BardicAbility.Fascinate, "dummy"),
                    Step.Assert("the dummy is fascinated", ctx => ctx.Get("dummy").HasCondition(CombatConditionType.Fascinated)))
                .Turn("bard", 2, Step.BardicSuggestion("dummy"),
                    Step.Assert("a suggestion: no use spent (5 of 6 left), the fascination goes on", ctx =>
                        BardicMusicRules.UsesLeft(ctx.Get("bard").Stats) == 5 && ctx.Get("dummy").HasCondition(CombatConditionType.Fascinated)))
                .Turn("bard", 3, Step.BardicStop(),
                    Step.Assert("the song stopped and released the dummy, which still follows the suggestion", ctx =>
                        !BardicMusicRules.IsPerforming(ctx.Get("bard").Stats) && !ctx.Get("dummy").HasCondition(CombatConditionType.Fascinated)
                        && ctx.Get("dummy").HasCondition(CombatConditionType.Suggested)))
                .Turn("bard", 4, Step.Assert("on its turn the dummy moved away from the bard (more than 50 ft now)", ctx =>
                    ctx.Get("bard").GetMinimumDistanceToTarget(ctx.Get("dummy"), chebyshev: false) > 10))
                .Expect("The Fascinate step is done", Expect.StepStatus("bard", 1, "BardicMusic", 0, "done"))
                .Expect("The Suggestion step is done", Expect.StepStatus("bard", 2, "BardicSuggestion", 0, "done"))
                .Expect("The Stop step is done", Expect.StepStatus("bard", 3, "BardicStop", 0, "done"))
                .Expect("The fascination and the suggestion follow the rules", Expect.AssertsPass())
                .Expect("The suggested dummy never attacks", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("dummy", aoo: false);
                    return attacks.Count == 0 ? ExpectResult.Pass("no attack by the dummy") : ExpectResult.Fail("the dummy attacked", attacks[0].Seq);
                })
                .Build();
        }
        /// <summary>
        /// Every weapon attack path is an obvious threat (PHB p.29; CHR-026 review): a scripted bard 4 fascinates a dummy
        /// 55 ft away (round 1) while a fighter 6 (BAB +6, two attacks) moves next to it; in round 2 the bard concentrates
        /// and the fighter full-attacks the dummy, through the PC Full Attack button (<paramref name="ai"/> false, a Ui
        /// actor) or its own AI choice (an AI-run turn, the NPC full attack). The first swing breaks the fascination, which
        /// is gone when the bard's round 3 begins. The AI fighter's full attack may trip with its first step (the NPC
        /// executor's attack-replacing trip, PHB p.158), which breaks the fascination through the shared maneuver core; its
        /// second step is a weapon attack.
        /// </summary>
        private static ScenarioDef FascinateFullAttack(bool ai)
        {
            string id = ai ? "rules/bard-fascinate-full-attack-ai" : "rules/bard-fascinate-full-attack-ui";
            ScenarioBuilder b = Rules(id, (ai ? "The AI fighter's" : "A PC fighter's") + " full attack breaks a bard's fascination (PHB p.29; CHR-026)")
                .Covers("CHR-020", "CHR-026", "PHB p.29", "DMG p.301", "PC_NPC_PARITY")
                .MaxRounds(3)
                .Pc("bard", ActorSource.Stats(() => Bard("Bard", 4)), 3, 10, Control.Scripted)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 6)), 7, 10, ai ? Control.Scripted : Control.Ui)
                .Npc("dummy", "target_dummy", 14, 10, Control.Scripted)
                .Tweak("bard", LoudBard)
                .Tweak("dummy", Sturdy)
                // Unarmored, so its 30-ft move reaches the dummy (the kit's scale mail would make it 20 ft, PHB p.123; ITM-010).
                .Tweak("fighter", RulesScenarios.StripArmor)
                .Initiative("bard", "fighter", "dummy")
                .Turn("bard", 1, Step.BardicMusic(BardicAbility.Fascinate, "dummy"),
                    Step.Assert("the dummy is fascinated", ctx => ctx.Get("dummy").HasCondition(CombatConditionType.Fascinated)))
                .Turn("fighter", 1, Step.Move(13, 10))
                .Turn("bard", 2, Step.BardicConcentrate())
                .Turn("bard", 3, Step.Assert("the full attack broke the fascination", ctx =>
                    !ctx.Get("dummy").HasCondition(CombatConditionType.Fascinated)), Step.Pass())
                .Turn("dummy", 0, Step.Pass())
                .Expect("The Fascinate step is done", Expect.StepStatus("bard", 1, "BardicMusic", 0, "done"))
                .Expect("The fighter made a full attack in round 2 (two steps: attacks, or a trip in place of one)", v =>
                {
                    int swings = v.Attacks("fighter", "dummy", aoo: false, round: 2).Count;
                    int trips = v.Maneuvers("fighter").Count(e => e.Round == 2);
                    return swings + trips >= 2 && swings >= 1
                        ? ExpectResult.Pass(swings + " swing(s), " + trips + " maneuver(s)")
                        : ExpectResult.Fail(swings + " swing(s), " + trips + " maneuver(s) in round 2");
                })
                .Expect("The fascination broke as the rules say", Expect.AssertsPass());
            if (ai)
                b = b.AiWhenUnscripted("fighter");
            else
                b = b.Turn("fighter", 2, Step.FullAttack("dummy"));
            return b.Build();
        }

        /// <summary>
        /// A combat maneuver is an obvious threat, on the shared maneuver core (CharacterController.ExecuteSpecialAttack)
        /// that the PC wrapper and the NPC executor both reach: a scripted fighter (the NPC executor,
        /// TryNPCSpecialAttackByTypeForAI) trips a dummy a bard 4 fascinates, and the fascination breaks at once.
        /// </summary>
        private static ScenarioDef FascinateTripScripted()
        {
            return Rules("rules/bard-fascinate-trip-scripted", "A trip on a bard-fascinated foe breaks the fascination (PHB p.29; CHR-026)")
                .Covers("CHR-020", "CHR-026", "PHB p.29", "DMG p.301", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("bard", ActorSource.Stats(() => Bard("Bard", 4)), 3, 10, Control.Scripted)
                .Pc("fighter", ActorSource.Stats(() => Fighter("Fighter", 4)), 7, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 14, 10, Control.Scripted)
                .Tweak("bard", LoudBard)
                .Tweak("dummy", Sturdy)
                // Unarmored, so its 30-ft move reaches the dummy (the kit's scale mail would make it 20 ft, PHB p.123; ITM-010).
                .Tweak("fighter", RulesScenarios.StripArmor)
                .Initiative("bard", "fighter", "dummy")
                .Turn("bard", 1, Step.BardicMusic(BardicAbility.Fascinate, "dummy"),
                    Step.Assert("the dummy is fascinated", ctx => ctx.Get("dummy").HasCondition(CombatConditionType.Fascinated)))
                .Turn("fighter", 1, Step.Move(13, 10))
                .Turn("bard", 2, Step.BardicConcentrate())
                .Turn("fighter", 2, Step.Maneuver(SpecialAttackType.Trip, "dummy"),
                    Step.Assert("the trip broke the fascination", ctx => !ctx.Get("dummy").HasCondition(CombatConditionType.Fascinated)))
                .Turn("dummy", 0, Step.Pass())
                .Expect("The fighter tried a trip", v => v.Maneuvers("fighter", SpecialAttackType.Trip).Count > 0
                    ? ExpectResult.Pass("trip traced") : ExpectResult.Fail("no trip"))
                .Expect("The fascination broke as the rules say", Expect.AssertsPass())
                .Build();
        }
    }
}
#endif
