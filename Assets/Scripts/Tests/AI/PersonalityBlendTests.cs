#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DND35.AI;
using DND35.AI.Knowledge;
using DND35.AI.Personality;
using UnityEngine;
using StubView = Tests.AI.ConsiderationTests.StubView;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 3 (docs/designs/personalities_batch14.md C2-C8; design sections 4.2, 4.5 and 8): PersonalityProfile and
/// AiBlend on fixed views.
/// <list type="bullet">
/// <item>One trait at 1.0 equals that trait; the folded weights equal the per-trait sum (the orc's, design 4.5) and the
/// term is linear in the weights and bounded by K = 20; design 4.5's fighter, wizard and cleric terms.</item>
/// <item>The vote: the quorum at .5 plus or minus epsilon, an exact Forbid/Insist tie falling through, the legacy
/// fallback inside plus or minus .15, the data override winning, the conditions, Easy skipping the sadistic coup de
/// grace (Hard equals Normal), partial weights (C2: the quorum uses the given weights).</item>
/// <item>The AoO price (C6) for each verdict and the flat 1000 penalty; RiskAversion and rho (the adept's .72, the
/// orc's .12, C7); the out-of-reach order by RoleDesire (C8); RoleShift.</item>
/// <item>A null difficulty is read from AiBlend.SettingsDifficulty (an unset Easy still blocks the sadistic row).</item>
/// <item>No personality: every member returns its legacy argument; a member with an actor in an open turn writes one
/// trace row, none with trace off.</item>
/// <item>Batch 14 step 5: the layout of rules/ai-captain-focuses-softest, fixed here before the scenario (the plan's
/// "Captain layout" risk): the Humanoid Role's score, worked from HumanoidAIProfile's terms, favours the near, healthy
/// fighter, and Smart .5's term turns the captain to the soft, wounded wizard, each with a margin; the creature members
/// with a personality (the sadist keeps downed foes and insists on a coup de grace at Normal, not on Easy, the data
/// override wins; an IgnoreUnconscious For turns the retarget on, Forbid leaves the Role's answer; the summary row).</item>
/// <item>Batch 14 step 6: the movement members with a personality (charge, AoO price, range adjust, keep-away distance,
/// risk factor) and the out-of-reach orders of the step's two scenarios.</item>
/// </list>
/// Either mode (static data and fixed views).
/// </summary>
public static class PersonalityBlendTests
{
    private static int _passed;
    private static int _failed;

    private static readonly JudgeContext Normal = new JudgeContext();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== PERSONALITY BLEND TESTS (batch 14 step 3) ======");

        // In Play mode AIService points the source at its Difficulty (batch 14 step 4): the suite runs with no source and
        // gives the live one back afterwards.
        Func<AIService.AIDifficultyLevel> liveSource = AiBlend.SettingsDifficulty;
        AiBlend.SettingsDifficulty = null;
        try
        {
            TestSingleTraitEqualsTrait();
            TestFoldedWeights();
            TestDesignTerms();
            TestQuorum();
            TestLegacyFallback();
            TestDataOverride();
            TestConditions();
            TestDifficulty();
            TestPartialWeights();
            TestAoOCost();
            TestRisk();
            TestOutOfReachOrder();
            TestNullPersonality();
            TestTrace();
            TestCaptainLayout();
            TestCreatureMembers();
            TestMovementMembers();
        }
        finally
        {
            AiBlend.SettingsDifficulty = liveSource;
            AiDecisionTrace.OverrideEnabled(null);
            AiDecisionTrace.Clear();
        }

        Debug.Log($"====== Personality Blend Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = null)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}" + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
        }
    }

    private static bool Near(float a, float b, float eps = 1e-4f) => Mathf.Abs(a - b) < eps;

    private static PersonalityProfile P(string spec, CognitionTier tier = CognitionTier.Average)
        => PersonalityFactory.Create(spec, tier, Alignment.None, "blend_test", _ => { });

    private static PersonalityProfile Orc => P("Sadistic:0.7,Berserker:0.3");
    private static PersonalityProfile Adept => P("Cowardly:0.6,Healer:0.4");

    private static JudgeContext Ctx(WoundBand self = WoundBand.Unhurt, bool threatened = false, bool standingInReach = false,
        AIService.AIDifficultyLevel? difficulty = null)
        => new JudgeContext { SelfWound = self, EnemyThreatensSelf = threatened, StandingThreatInReach = standingInReach, Difficulty = difficulty };

    private static Verdict V(PersonalityProfile p, BoolDecision d, JudgeContext ctx) => p.Judge(d, ctx).Verdict;

    /// <summary>A view where every consideration a trait might read has a value.</summary>
    private static StubView Rich()
        => new StubView().Band(WoundBand.Hurt).Armor(WornArmor.Light).Size(SizeCategory.Medium).Number(FactKey.Distance, 3)
            .Number(FactKey.FoesNear, 2).Number(FactKey.HpCurrent, 8).Flag(FactKey.Caster).Flag(FactKey.Healer, false)
            .Flag(FactKey.ThreatensObserver).Flag(FactKey.EngagedByAlly).Flag(FactKey.FlankingNow, false).Flag(FactKey.Fleeing)
            .Flag(FactKey.ThreatensAllyInNeed).Flag(FactKey.DamagedObserver).Flag(FactKey.WieldsMelee).Flag(FactKey.Down, false)
            .Flag(FactKey.Helpless, false);

    private static float TraitTerm(TraitDefinition t, ITargetView view)
    {
        float s = 0f;
        foreach (TargetWeight w in t.Target)
            s += w.Weight * Considerations.Evaluate(w.Consideration, view);
        return PersonalityProfile.K * s;
    }

    private static void TestSingleTraitEqualsTrait()
    {
        StubView view = Rich();
        foreach (TraitDefinition t in TraitDatabase.All)
        {
            PersonalityProfile p = P(t.Id + ":1", CognitionTier.Brilliant);
            bool folded = true;
            foreach (ConsiderationId id in System.Enum.GetValues(typeof(ConsiderationId)))
                folded &= Near(p.FoldedWeight(id), t.TargetWeightOf(id));
            Assert(folded, t.Id + ":1 folds to the trait's own target weights");
            Assert(Near(p.TargetTerm(view), TraitTerm(t, view)), t.Id + ":1's term equals the trait's", p.TargetTerm(view) + " vs " + TraitTerm(t, view));
            Assert(Near(p.RiskAversion, t.RiskAversion), t.Id + ":1's RiskAversion is the trait's");
        }
        Assert(V(P("Cowardly:1"), BoolDecision.Charge, Normal) == Verdict.Forbid, "Cowardly:1 forbids a charge");
        Assert(V(P("Berserker:1"), BoolDecision.Charge, Normal) == Verdict.Insist, "Berserker:1 insists on a charge");
        Assert(V(P("Smart:1"), BoolDecision.ProvokeAoO, Normal) == Verdict.Against, "Smart:1 is against provoking");
        Assert(V(P("Healer:1"), BoolDecision.CoupDeGrace, Normal) == Verdict.Against, "Healer:1 is against a coup de grace");
        Assert(V(P("Mindless:1"), BoolDecision.Charge, Normal) == Verdict.Neutral, "Mindless:1 has no opinion (the Role decides)");
    }

    private static void TestFoldedWeights()
    {
        PersonalityProfile orc = Orc;
        var expected = new Dictionary<ConsiderationId, float>
        {
            { ConsiderationId.WoundSeverity, 0.21f }, { ConsiderationId.IsDown, 0.21f }, { ConsiderationId.Helpless, 0.14f },
            { ConsiderationId.Proximity, 0.12f }, { ConsiderationId.HurtMe, 0.12f }, { ConsiderationId.Softness, 0.07f },
            { ConsiderationId.Fleeing, 0.07f }, { ConsiderationId.LooksDangerous, 0.06f }
        };
        bool ok = true;
        var detail = new List<string>();
        foreach (ConsiderationId id in System.Enum.GetValues(typeof(ConsiderationId)))
        {
            float want = expected.TryGetValue(id, out float w) ? w : 0f;
            if (!Near(orc.FoldedWeight(id), want))
            {
                ok = false;
                detail.Add(id + " " + orc.FoldedWeight(id));
            }
        }
        Assert(ok, "the orc's folded weights are design 4.5's (WoundSeverity .21, IsDown .21, Helpless .14, Proximity .12, HurtMe .12, Softness .07, Fleeing .07, LooksDangerous .06)",
            string.Join(", ", detail));

        StubView view = Rich();
        float linear = 0.7f * TraitTerm(TraitDatabase.Get("Sadistic"), view) + 0.3f * TraitTerm(TraitDatabase.Get("Berserker"), view);
        Assert(Near(orc.TargetTerm(view), linear), "the folded term equals the per-trait sum", orc.TargetTerm(view) + " vs " + linear);

        StubView adjacent = new StubView().Number(FactKey.Distance, 1);
        Assert(Near(P("Mindless").TargetTerm(adjacent), 20f), "the term reaches +K (Mindless, adjacent)");
        StubView scary = new StubView().Number(FactKey.Distance, 1).Flag(FactKey.ThreatensObserver).Size(SizeCategory.Large).Flag(FactKey.EngagedByAlly, false);
        Assert(Near(P("Cowardly:1").TargetTerm(scary), -14f), "Cowardly against an adjacent, threatening, larger foe: -.7 x 20 = -14");
        Assert(P("Cowardly:1").TargetTerm(null) == 0f, "no view: no term");
    }

    private static void TestDesignTerms()
    {
        PersonalityProfile orc = Orc;
        StubView fighter = new StubView().Armor(WornArmor.Heavy).Flag(FactKey.WieldsMelee).Size(SizeCategory.Medium).Band(WoundBand.Unhurt)
            .Number(FactKey.Distance, 1).Flag(FactKey.DamagedObserver).Flag(FactKey.Down, false).Flag(FactKey.Helpless, false);
        Assert(Near(orc.TargetTerm(fighter), 6.0f), "design 4.5: the orc's term for the heavy, unhurt, adjacent fighter who hit it is +6.0",
            orc.TargetTerm(fighter).ToString());
        StubView wizard = new StubView().Armor(WornArmor.Unarmored).Size(SizeCategory.Medium).Band(WoundBand.BadlyHurt)
            .Number(FactKey.Distance, 4).Flag(FactKey.Down, false);
        float wizardWant = 20f * (0.21f * 0.66f + 0.07f * 1f + 0.12f * 8f / 11f);
        Assert(Near(orc.TargetTerm(wizard), wizardWant), "design 4.5: the badly hurt, unarmored wizard 4 squares away is about +5.9 (design: +5.8 with Proximity rounded)",
            orc.TargetTerm(wizard).ToString());
        StubView cleric = new StubView().Armor(WornArmor.Medium).Size(SizeCategory.Medium).Band(WoundBand.Down).Number(FactKey.Distance, 1)
            .Flag(FactKey.Down).Flag(FactKey.Helpless);
        float clericWant = 20f * (0.21f + 0.14f + 0.12f + 0.07f * 0.33f);
        Assert(Near(orc.TargetTerm(cleric), clericWant), "the Down, helpless, adjacent cleric is about +9.9 (design 4.5's +7.1 left out Helpless)",
            orc.TargetTerm(cleric).ToString());
        Assert(orc.TargetTerm(cleric) > orc.TargetTerm(fighter) && orc.TargetTerm(fighter) > orc.TargetTerm(wizard),
            "the sadistic orc ranks the downed cleric first");
    }

    private static void TestQuorum()
    {
        JudgeContext hurt = Ctx(WoundBand.Hurt);
        Assert(V(P("Cowardly:0.5"), BoolDecision.Charge, Normal) == Verdict.Forbid, "a Forbid at weight .5 meets the quorum");
        Assert(V(P("Cowardly:0.501"), BoolDecision.Charge, Normal) == Verdict.Forbid, "a Forbid at .5 + e meets the quorum");
        Assert(V(P("Cowardly:0.499"), BoolDecision.Charge, Normal) == Verdict.Against, "a Forbid at .5 - e is a vote (-2 x .499: Against)");
        Assert(AiBlend.AoOCost(P("Cowardly:0.5"), 2f, hurt) == 1000f && AiBlend.AoOCost(P("Cowardly:0.499"), 2f, hurt) == 6f,
            "at the AoO price the quorum shows: 1000 at .5, 6 at .499");
        Assert(V(P("Berserker:0.5"), BoolDecision.Charge, Normal) == Verdict.Insist, "an Insist at .5 meets the quorum");
        Assert(V(P("Berserker:0.499"), BoolDecision.Charge, Normal) == Verdict.For, "an Insist at .5 - e is a vote (For)");
        PersonalityProfile tie = P("Cowardly:0.5,Berserker:0.5");
        Assert(V(tie, BoolDecision.Charge, Normal) == Verdict.Neutral, "an exact .5 Forbid / .5 Insist tie falls through");
        Assert(AiBlend.Judge(tie, BoolDecision.Charge, true, Normal) && !AiBlend.Judge(tie, BoolDecision.Charge, false, Normal),
            "to the Role's legacy answer, either way");
        Assert(!AiBlend.Judge(Adept, BoolDecision.Charge, true, Normal), "the adept never charges (Cowardly Forbid at .6)");
        Assert(AiBlend.Judge(Orc, BoolDecision.CoupDeGrace, false, Normal), "the sadistic orc insists on a coup de grace (.7)");
        Assert(!AiBlend.Judge(Orc, BoolDecision.IgnoreUnconscious, true, Normal), "and never ignores the downed (Forbid .7)");
    }

    private static void TestLegacyFallback()
    {
        Assert(V(P("Smart:0.15"), BoolDecision.ProvokeAoO, Normal) == Verdict.Neutral, "v = -.15 is inside the band: Neutral");
        Assert(AiBlend.Judge(P("Smart:0.1"), BoolDecision.ProvokeAoO, true, Normal), "v = -.1: the legacy yes stands");
        Assert(!AiBlend.Judge(P("Smart:0.2"), BoolDecision.ProvokeAoO, true, Normal), "v = -.2: no, over the legacy yes");
        Assert(!AiBlend.Judge(P("Healer:0.14"), BoolDecision.CoupDeGrace, false, Normal) && AiBlend.Judge(P("Healer:0.14"), BoolDecision.CoupDeGrace, true, Normal),
            "Healer .14 against a coup de grace: the legacy answer either way");
        Assert(V(P("Sadistic:0.07"), BoolDecision.CoupDeGrace, Normal) == Verdict.Neutral, "an Insist below the quorum counts +2: .07 gives v .14, Neutral");
        Assert(V(P("Sadistic:0.08"), BoolDecision.CoupDeGrace, Normal) == Verdict.For, "and .08 gives v .16, For");
        Assert(AiBlend.Judge(P("Smart:0.5"), BoolDecision.Charge, true, Normal) && !AiBlend.Judge(P("Smart:0.5"), BoolDecision.Charge, false, Normal),
            "a decision no trait has an opinion on keeps the legacy answer");
    }

    private static void TestDataOverride()
    {
        Assert(!AiBlend.Judge(Orc, BoolDecision.CoupDeGrace, true, Normal, dataOverride: false), "the data override (false) beats the sadist's Insist");
        Assert(AiBlend.Judge(P("Healer:1"), BoolDecision.CoupDeGrace, false, Normal, dataOverride: true), "the data override (true) beats the healer's Against");
        Assert(AiBlend.Judge(P("Cowardly:1"), BoolDecision.Charge, false, Normal, dataOverride: true), "the data override beats a quorum Forbid");
    }

    private static void TestConditions()
    {
        PersonalityProfile coward = P("Cowardly:1");
        Assert(V(coward, BoolDecision.ProvokeAoO, Ctx(WoundBand.Unhurt)) == Verdict.Against, "Cowardly unhurt: Against provoking");
        Assert(V(coward, BoolDecision.ProvokeAoO, Ctx(WoundBand.Hurt)) == Verdict.Forbid, "Cowardly hurt: Forbid");
        Assert(V(coward, BoolDecision.ProvokeAoO, Ctx(WoundBand.BadlyHurt)) == Verdict.Forbid, "Cowardly badly hurt: Forbid");
        PersonalityProfile smart = P("Smart:1");
        Assert(V(smart, BoolDecision.CoupDeGrace, Ctx(threatened: false)) == Verdict.For, "Smart, no foe threatens it: For a coup de grace");
        Assert(V(smart, BoolDecision.CoupDeGrace, Ctx(threatened: true)) == Verdict.Against, "Smart, threatened: Against");
        Assert(V(smart, BoolDecision.IgnoreUnconscious, Ctx(standingInReach: true)) == Verdict.For, "Smart, a standing foe in reach: ignore the downed");
        Assert(V(smart, BoolDecision.IgnoreUnconscious, Ctx(standingInReach: false)) == Verdict.Neutral, "Smart, none in reach: the Role decides");
    }

    private static void TestDifficulty()
    {
        PersonalityProfile orc = Orc;
        Assert(V(orc, BoolDecision.CoupDeGrace, Ctx(difficulty: AIService.AIDifficultyLevel.Normal)) == Verdict.Insist, "Normal: the sadist insists");
        Assert(V(orc, BoolDecision.CoupDeGrace, Ctx(difficulty: AIService.AIDifficultyLevel.Hard)) == Verdict.Insist, "Hard equals Normal");
        Assert(V(orc, BoolDecision.CoupDeGrace, Ctx()) == Verdict.Insist, "an unset difficulty is Normal");
        Assert(V(orc, BoolDecision.CoupDeGrace, Ctx(difficulty: AIService.AIDifficultyLevel.Easy)) == Verdict.Neutral, "Easy skips the sadistic row");
        Assert(!AiBlend.Judge(orc, BoolDecision.CoupDeGrace, false, Ctx(difficulty: AIService.AIDifficultyLevel.Easy))
               && AiBlend.Judge(orc, BoolDecision.CoupDeGrace, true, Ctx(difficulty: AIService.AIDifficultyLevel.Easy)),
            "on Easy the Role's own coup de grace answer stands (Q6: only the trait-driven one is blocked)");
        Assert(V(orc, BoolDecision.IgnoreUnconscious, Ctx(difficulty: AIService.AIDifficultyLevel.Easy)) == Verdict.Forbid,
            "Easy skips only the marked row (IgnoreUnconscious Forbid stays)");

        Func<AIService.AIDifficultyLevel> saved = AiBlend.SettingsDifficulty;
        try
        {
            AiBlend.SettingsDifficulty = () => AIService.AIDifficultyLevel.Easy;
            Assert(!AiBlend.Judge(orc, BoolDecision.CoupDeGrace, false, Ctx()) && AiBlend.Judge(orc, BoolDecision.CoupDeGrace, true, Ctx()),
                "a context with no difficulty under an Easy setting: the sadistic Insist is skipped (the Role's answer stands)");
            Assert(AiBlend.Judge(orc, BoolDecision.CoupDeGrace, false, Ctx(difficulty: AIService.AIDifficultyLevel.Normal)),
                "an explicit difficulty in the context wins over the setting (Normal: the sadist insists)");
            AiBlend.SettingsDifficulty = () => AIService.AIDifficultyLevel.Normal;
            Assert(AiBlend.Judge(orc, BoolDecision.CoupDeGrace, false, Ctx()), "under a Normal setting the unset context insists");
            AiBlend.SettingsDifficulty = null;
            Assert(AiBlend.Judge(orc, BoolDecision.CoupDeGrace, false, Ctx()), "with no setting source, the default Normal");
        }
        finally
        {
            AiBlend.SettingsDifficulty = saved;
        }
    }

    private static void TestPartialWeights()
    {
        PersonalityProfile half = P("Smart:0.5");
        Assert(Near(half.FoldedWeight(ConsiderationId.KillChance), 0.225f), "Smart:0.5 is Smart at half strength (KillChance .225)");
        Assert(Near(half.RiskAversion, 0.55f), "the Role's .5 share holds RiskAversion at .5: .5 x .6 + .5 x .5 = .55");
        Assert(V(P("Cowardly:0.4"), BoolDecision.Charge, Normal) == Verdict.Against, "the quorum uses the given weights: Cowardly .4 is no quorum");
        Assert(AiBlend.AoOCost(P("Cowardly:0.4"), 2f, Ctx(WoundBand.Hurt)) == 6f, "so Cowardly .4 hurt prices a provoker 6, not 1000");
    }

    private static void TestAoOCost()
    {
        PersonalityProfile insist = PersonalityProfile.Build(new List<TraitWeight>
        {
            new TraitWeight(new TraitDefinition
            {
                Id = "TestReckless",
                Target = new[] { new TargetWeight(ConsiderationId.Proximity, 1f) },
                Opinions = new[] { new OpinionRule(BoolDecision.ProvokeAoO, Opinion.Insist) }
            }, 1f)
        }, CognitionTier.Average, "TestReckless:1");
        Assert(AiBlend.AoOCost(insist, 1000f, Normal) == 0f, "Insist: a provoker costs 0");
        Assert(AiBlend.AoOCost(P("Berserker:1"), 1000f, Normal) == 2f && AiBlend.AoOCost(P("Berserker:1"), 1f, Normal) == 1f,
            "For: min(legacy, 2)");
        Assert(AiBlend.AoOCost(P("Smart:1"), 2f, Normal) == 6f && AiBlend.AoOCost(P("Smart:1"), 1000f, Normal) == 1000f,
            "Against: max(legacy, 6)");
        Assert(AiBlend.AoOCost(P("Cowardly:1"), 2f, Ctx(WoundBand.Hurt)) == 1000f, "Forbid: 1000");
        Assert(AiBlend.AoOCost(P("Healer:1"), 2f, Normal) == 2f && AiBlend.AoOCost(P("Healer:1"), 1000f, Normal) == 1000f,
            "Neutral: the legacy price");
        Assert(AiBlend.AoOCost(Adept, 2f, Ctx(WoundBand.Unhurt)) == 6f && AiBlend.AoOCost(Adept, 2f, Ctx(WoundBand.Hurt)) == 1000f,
            "the adept: 6 a provoker while unhurt, 1000 once hurt (Cowardly .6)");
        Assert(AiBlend.AoOPenalty(1000f, 3) == 1000f && AiBlend.AoOPenalty(2f, 3) == 6f,
            "the penalty reproduces the legacy cost: avoiding 1000 flat, else 2 per provoker");
        Assert(AiBlend.AoOPenalty(6f, 2) == 12f && AiBlend.AoOPenalty(0f, 4) == 0f && AiBlend.AoOPenalty(1000f, 0) == 0f,
            "6 per provoker; a free price; no provoker");
    }

    private static void TestRisk()
    {
        PersonalityProfile adept = Adept;
        Assert(Near(adept.RiskAversion, 0.72f), "the adept's RiskAversion: .6 x 1.0 + .4 x .3 = .72 (design 4.5)", adept.RiskAversion.ToString());
        Assert(Near(adept.Rho, 1.264f), "its rho: lerp(.4, 1.6, .72) = 1.264");
        Assert(Near(AiBlend.RiskToleranceFactor(adept, 1f), 0.736f), "its risk tolerance: 1 x (2 - 1.264) = .736 (design 4.5: about .74)");
        Assert(Near(Orc.RiskAversion, 0.12f), "the orc's RiskAversion: .7 x .3 + .3 x -.3 = .12");
        Assert(Near(P("Berserker:1").Rho, 0.4f), "a RiskAversion below 0 clamps (Berserker: rho .4)");
        Assert(Near(P("Mindless").Rho, 0.4f) && Near(AiBlend.RiskToleranceFactor(P("Mindless"), 1f), 1.6f), "RiskAversion 0: rho .4, tolerance x 1.6");
        Assert(Near(AiBlend.RiskToleranceFactor(P("Cowardly:0.5,Healer:0.5", CognitionTier.Average), 1f), 2f - Mathf.Lerp(0.4f, 1.6f, 0.65f)),
            "the factor multiplies the Role's own multiplier by 2 - rho");
        Assert(Near(AiBlend.RiskToleranceFactor(P("Smart:0.5"), 1.5f), 1.5f * (2f - Mathf.Lerp(0.4f, 1.6f, 0.55f))),
            "and keeps the Role's multiplier (1.5)");
    }

    private static void TestOutOfReachOrder()
    {
        var ranged = new[] { OutOfReachAlternative.AttackOtherTarget, OutOfReachAlternative.PreferredRange, OutOfReachAlternative.Approach };
        IReadOnlyList<OutOfReachAlternative> adept = AiBlend.OrderOutOfReach(Adept, ranged);
        Assert(adept.SequenceEqual(new[] { OutOfReachAlternative.PreferredRange, OutOfReachAlternative.AttackOtherTarget, OutOfReachAlternative.Approach }),
            "the adept's ranged list: its preferred range first (Retreat .48 lifts it past another target, Attack -.12)", string.Join(">", adept));
        var melee = new[] { OutOfReachAlternative.AttackOtherTarget, OutOfReachAlternative.Approach };
        Assert(ReferenceEquals(AiBlend.OrderOutOfReach(P("Berserker:1"), melee), melee), "Berserker keeps the melee list (and its instance)");
        Assert(ReferenceEquals(AiBlend.OrderOutOfReach(P("Smart:1"), ranged), ranged), "a trait with no RoleDesire keeps the Role's list");
        var caster = new[] { OutOfReachAlternative.AttackOtherTarget, OutOfReachAlternative.PreferredRange, OutOfReachAlternative.TotalDefense };
        IReadOnlyList<OutOfReachAlternative> coward = AiBlend.OrderOutOfReach(P("Cowardly:1"), caster);
        Assert(coward.SequenceEqual(new[] { OutOfReachAlternative.PreferredRange, OutOfReachAlternative.AttackOtherTarget, OutOfReachAlternative.TotalDefense }),
            "a cowardly caster: the preferred range first, total defense still last", string.Join(">", coward));
        Assert(coward.Count == caster.Length && caster.All(coward.Contains), "the order never adds or removes an entry");
        var single = new[] { OutOfReachAlternative.Approach };
        Assert(ReferenceEquals(AiBlend.OrderOutOfReach(Adept, single), single), "a one-entry list is kept");

        PersonalityProfile a = Adept;
        Assert(Near(a.RoleShift(ActionRoles.Retreat), 0.48f) && Near(a.RoleShift(ActionRoles.Defend), 0.34f) && Near(a.RoleShift(ActionRoles.Heal), 0.32f)
               && Near(a.RoleShift(ActionRoles.Support), 0.24f) && Near(a.RoleShift(ActionRoles.Approach), -0.3f),
            "the adept's RoleShift: Retreat .48, Defend .34, Heal .32, Support .24, Approach -.3");
        Assert(Near(a.RoleShift(ActionRoles.Approach | ActionRoles.Retreat), 0.48f) && a.RoleShift(ActionRoles.None) == 0f
               && a.RoleShift(ActionRoles.Escape) == 0f, "several roles take the best; None and an unnamed role give 0");
        PersonalityProfile berserker = P("Berserker:1");
        Assert(Near(berserker.RoleShift(ActionRoles.Retreat | ActionRoles.Escape), -0.8f),
            "an unnamed flag abstains: Berserker's withdraw (Retreat and Escape) shifts by Retreat -.8, not 0",
            berserker.RoleShift(ActionRoles.Retreat | ActionRoles.Escape).ToString());
        var defendOrWithdraw = new[] { OutOfReachAlternative.TotalDefense, OutOfReachAlternative.Withdraw };
        Assert(ReferenceEquals(AiBlend.OrderOutOfReach(berserker, defendOrWithdraw), defendOrWithdraw),
            "so a Berserker never lifts a withdraw over total defense (-2.6 against -1.2): the Role's order is kept");
        Assert(AiBlend.RolesOf(OutOfReachAlternative.Withdraw) == (ActionRoles.Retreat | ActionRoles.Escape)
               && AiBlend.RolesOf(OutOfReachAlternative.TotalDefense) == ActionRoles.Defend, "out-of-reach roles follow the catalog's entries");
    }

    private static void TestNullPersonality()
    {
        StubView view = Rich();
        var list = new[] { OutOfReachAlternative.AttackOtherTarget, OutOfReachAlternative.Approach };
        Assert(AiBlend.Judge(null, BoolDecision.Charge, true, Normal) && !AiBlend.Judge(null, BoolDecision.Charge, false, Normal),
            "no personality: Judge returns the legacy answer");
        Assert(AiBlend.Judge(null, BoolDecision.CoupDeGrace, true, Normal, dataOverride: false),
            "no personality: the legacy answer as given (the legacy sites apply the override themselves)");
        Assert(AiBlend.AoOCost(null, 1000f, Ctx(WoundBand.Hurt)) == 1000f && AiBlend.AoOCost(null, 2f, Normal) == 2f, "no personality: the legacy AoO price");
        Assert(AiBlend.RiskToleranceFactor((PersonalityProfile)null, 1.3f) == 1.3f, "no personality: the legacy risk multiplier");
        Assert(ReferenceEquals(AiBlend.OrderOutOfReach((PersonalityProfile)null, list), list), "no personality: the Role's list itself");
        Assert(AiBlend.ScoreTarget(null, 12.5f, view) == 12.5f, "no personality: the legacy target score");
        Assert(Near(AiBlend.ScoreTarget(Orc, 12.5f, view), 12.5f + Orc.TargetTerm(view)), "a personality adds its term to the legacy score");
        Assert(AiBlend.ScoreTarget(Orc, float.MinValue, view) == float.MinValue, "an invalid target stays invalid");
        Assert(AiBlend.ScoreTarget(Orc, 3f, view) == AiBlend.ScoreTarget(Orc, 3f, view), "the blend is deterministic");
    }

    private static void TestTrace()
    {
        AiDecisionTrace.OverrideEnabled(true);
        AiDecisionTrace.Clear();
        var actor = new object();
        AiDecisionTrace.BeginTurn(actor, "Orc 1", 1, "Humanoid", Orc.Describe());
        AiBlend.Judge(Orc, BoolDecision.CoupDeGrace, false, Normal, actor: actor);
        AiBlend.Judge(null, BoolDecision.Charge, false, Normal, actor: actor);
        AiDecisionTrace.TurnRecord rec = AiDecisionTrace.EndTurn(actor);
        Assert(rec != null && rec.Decisions.Count == 1 && rec.Decisions[0].Site == "Personality.CoupDeGrace"
               && rec.Decisions[0].Chosen == "yes" && rec.Decisions[0].Legacy == "no",
            "a member with an actor writes one trace row; none without a personality",
            rec != null ? rec.Collapsed() : "no record");
        Assert(rec != null && rec.Decisions.Count == 1 && rec.Decisions[0].Reason.Contains("difficulty from settings"),
            "a context with no difficulty says it read the setting", rec != null && rec.Decisions.Count > 0 ? rec.Decisions[0].Reason : "no row");
        AiBlend.Judge(Orc, BoolDecision.CoupDeGrace, false, Normal, actor: actor);
        Assert(AiDecisionTrace.OpenTurns == 0 && !AiDecisionTrace.HasOpenTurn(actor), "a row with no open turn is dropped");

        StubView view = Rich();
        AiDecisionTrace.BeginTurn(actor, "Orc 1", 2, "Humanoid", Orc.Describe());
        Assert(AiDecisionTrace.HasOpenTurn(actor), "HasOpenTurn sees the open turn");
        float silent = AiBlend.ScoreTarget(Orc, 10f, view, actor, trace: false);
        float price = AiBlend.AoOCost(Adept, 2f, Normal, actor, trace: false);
        AiBlend.RiskToleranceFactor(Adept, 1f, actor);
        AiDecisionTrace.TurnRecord rec2 = AiDecisionTrace.EndTurn(actor);
        Assert(rec2 != null && rec2.Decisions.Count == 1 && rec2.Decisions[0].Site == "Personality.RiskTolerance",
            "ScoreTarget and AoOCost with trace off write no row (a per-candidate site records one summary row itself)",
            rec2 != null ? rec2.Collapsed() : "no record");
        Assert(Near(silent, 10f + Orc.TargetTerm(view)) && price == 6f, "and still return their values");
    }

    // ── Batch 14 step 5 ─────────────────────────────────────────

    /// <summary>
    /// The Humanoid Role's target score as HumanoidAIProfile builds it on AIProfile.ScoreTarget (10; the fact tags,
    /// unarmored +3 and heavy armor -2; wounded (1 - hp share) x 8; isolated +4, else -1.25 per ally beside the target;
    /// melee proximity (6 - d) x .7), for the captain layout's two foes. A retune of those terms shows here.
    /// </summary>
    private static float HumanoidLegacy(bool unarmored, bool heavy, int hp, int maxHp, int alliesBeside, int distance)
    {
        float s = 10f + (unarmored ? 3f : 0f) - (heavy ? 2f : 0f);
        s += (1f - Mathf.Clamp01((float)hp / maxHp)) * 8f;
        s += alliesBeside == 0 ? 4f : -1.25f * alliesBeside;
        s += Mathf.Max(0, 6 - distance) * 0.7f;
        return s;
    }

    /// <summary>The trait term of <paramref name="view"/> with KillChance read at <paramref name="myExpectedDamage"/> (a stub view has no observer).</summary>
    private static float TermWithKill(PersonalityProfile p, ITargetView view, float myExpectedDamage)
    {
        float sum = 0f;
        foreach (ConsiderationId id in Enum.GetValues(typeof(ConsiderationId)))
        {
            float e = p.FoldedWeight(id);
            if (e == 0f)
                continue;
            float v = id == ConsiderationId.KillChance ? Considerations.KillChance(view, myExpectedDamage) : Considerations.Evaluate(id, view);
            sum += e * v;
        }
        return Mathf.Clamp(PersonalityProfile.K * sum, -PersonalityProfile.K, PersonalityProfile.K);
    }

    /// <summary>
    /// rules/ai-captain-focuses-softest, fixed before the scenario: the hobgoblin captain (Smart .5, Keen) has in its reach
    /// a Fighter 3 in medium armor at full hit points (30) with nobody beside it, and a wizard 2, unarmored, at 10 of 14 hit
    /// points with two fighters beside it. The captain's longsword (+6, 1d8+2) expects about 5.2 damage a round against
    /// the wizard's AC 11 and at most 4 against the fighter; the layout holds from 4 up. The plain captain takes the
    /// fighter by more than 1; the Smart one takes the wizard by more than 1.
    /// </summary>
    private static void TestCaptainLayout()
    {
        PersonalityProfile smart = P("Smart:0.5", CognitionTier.Keen);
        StubView fighter = new StubView().Armor(WornArmor.Medium).Band(WoundBand.Unhurt).Size(SizeCategory.Medium).Number(FactKey.Distance, 1)
            .Number(FactKey.HpCurrent, 30).Flag(FactKey.Caster, false).Flag(FactKey.Healer, false).Flag(FactKey.FlankingNow, false)
            .Flag(FactKey.Down, false).Flag(FactKey.Helpless, false).Flag(FactKey.WieldsMelee);
        StubView wizard = new StubView().Armor(WornArmor.Unarmored).Band(WoundBand.Hurt).Size(SizeCategory.Medium).Number(FactKey.Distance, 1)
            .Number(FactKey.HpCurrent, 10).Flag(FactKey.Caster).Flag(FactKey.Healer, false).Flag(FactKey.FlankingNow, false)
            .Flag(FactKey.Down, false).Flag(FactKey.Helpless, false);

        Assert(Near(TermWithKill(smart, wizard, 0f), smart.TargetTerm(wizard)) && Near(TermWithKill(smart, fighter, 0f), smart.TargetTerm(fighter)),
            "the layout helper's term equals TargetTerm when no damage is expected (a stub view has no observer)");

        float legacyFighter = HumanoidLegacy(unarmored: false, heavy: false, hp: 30, maxHp: 30, alliesBeside: 0, distance: 1);
        float legacyWizard = HumanoidLegacy(unarmored: true, heavy: false, hp: 10, maxHp: 14, alliesBeside: 2, distance: 1);
        Assert(legacyFighter - legacyWizard >= 1f, "the plain captain takes the fighter: the Role's 17.5 against the wizard's 16.3",
            legacyFighter.ToString("0.00") + " vs " + legacyWizard.ToString("0.00"));

        float fighterTerm = TermWithKill(smart, fighter, 4f);
        bool smartWins = true;
        var detail = new List<string>();
        foreach (float dmg in new[] { 4f, 5.2f, 6.5f })
        {
            float wizardTotal = legacyWizard + TermWithKill(smart, wizard, dmg);
            float fighterTotal = legacyFighter + fighterTerm;
            detail.Add(dmg + ": " + wizardTotal.ToString("0.00") + " vs " + fighterTotal.ToString("0.00"));
            smartWins &= wizardTotal - fighterTotal >= 1f;
        }
        Assert(smartWins, "the Smart captain takes the wizard by more than 1 at an expected 4 to 6.5 damage (KillChance and KnownCaster)",
            string.Join("; ", detail));
        Assert(Near(TermWithKill(smart, wizard, 5.2f), 20f * 0.5f * (0.45f * 0.52f + 0.15f)), "the wizard's Smart term at 5.2 damage: 10 x (.45 x .52 + .15) = 3.84");
        Assert(fighterTerm < 0.5f, "the fighter's Smart term stays below .5 (30 hit points, no caster)", fighterTerm.ToString("0.000"));
    }

    /// <summary>The creature-based members of step 5 with a personality, on bare controllers (no stats: the vote context is the default).</summary>
    private static void TestCreatureMembers()
    {
        var go = new GameObject("PersonalityBlendTests creature");
        var other = new GameObject("PersonalityBlendTests target");
        Func<AIService.AIDifficultyLevel> saved = AiBlend.SettingsDifficulty;
        try
        {
            CharacterController self = go.AddComponent<CharacterController>();
            CharacterController target = other.AddComponent<CharacterController>();

            self.Personality = Orc;
            Assert(!AiBlend.IgnoresUnconscious(self, true) && !AiBlend.IgnoresUnconscious(self, false),
                "the sadistic orc never skips downed foes (Forbid .7 over the Role's yes)");
            Assert(!AiBlend.Adaptive(self, false) && AiBlend.Adaptive(self, true),
                "a Forbid on IgnoreUnconscious leaves the Role's retarget answer");
            AiBlend.SettingsDifficulty = () => AIService.AIDifficultyLevel.Normal;
            Assert(AiBlend.UsesCoupDeGrace(self, false, null), "at Normal the sadist takes a coup de grace the Role refuses (Insist .7)");
            Assert(!AiBlend.UsesCoupDeGrace(self, false, false) && AiBlend.UsesCoupDeGrace(self, false, true), "the data override wins either way");
            AiBlend.SettingsDifficulty = () => AIService.AIDifficultyLevel.Easy;
            Assert(!AiBlend.UsesCoupDeGrace(self, false, null) && AiBlend.UsesCoupDeGrace(self, true, null),
                "on Easy the sadistic row is skipped: the Role's answer stands (owner, 2026-10-30)");
            AiBlend.SettingsDifficulty = null;
            Assert(AiBlend.ScoreTarget(self, target, float.MinValue) == float.MinValue && AiBlend.ScoreTarget(self, null, 4f) == 4f,
                "an invalid target stays invalid; no target keeps the legacy score");

            self.Personality = PersonalityProfile.Build(new List<TraitWeight>
            {
                new TraitWeight(new TraitDefinition
                {
                    Id = "TestFinisher",
                    Target = new[] { new TargetWeight(ConsiderationId.Proximity, 1f) },
                    Opinions = new[] { new OpinionRule(BoolDecision.IgnoreUnconscious, Opinion.For) }
                }, 1f)
            }, CognitionTier.Average, "TestFinisher:1");
            Assert(AiBlend.IgnoresUnconscious(self, false) && AiBlend.Adaptive(self, false),
                "an IgnoreUnconscious For skips downed foes and turns the mid-sequence retarget on (the Role said no)");
            self.Personality = Adept;
            Assert(!AiBlend.Adaptive(self, false) && AiBlend.IgnoresUnconscious(self, true) && !AiBlend.IgnoresUnconscious(self, false),
                "a personality with no IgnoreUnconscious opinion leaves both Role answers");

            self.Personality = Orc;
            AiDecisionTrace.OverrideEnabled(true);
            AiDecisionTrace.Clear();
            AiDecisionTrace.BeginTurn(self, "Orc", 1, "Humanoid", Orc.Describe());
            Assert(AiBlend.TracesTargets(self), "TracesTargets with a personality and an open turn");
            AiBlend.TraceTarget(self, "Target", "Soft Wizard", "Hero Fighter", 3.84f);
            AiDecisionTrace.TurnRecord rec = AiDecisionTrace.EndTurn(self);
            Assert(rec != null && rec.Decisions.Count == 1 && rec.Decisions[0].Site == "Personality.Target"
                   && rec.Decisions[0].Verdict == "changed" && Near(rec.Decisions[0].Bonus, 3.84f),
                "the target site's summary row: the blend's choice, the Role's choice and the chosen target's term",
                rec != null ? rec.Collapsed() : "no record");
            Assert(!AiBlend.TracesTargets(self), "no open turn: no summary row");
        }
        finally
        {
            AiBlend.SettingsDifficulty = saved;
            AiDecisionTrace.OverrideEnabled(null);
            AiDecisionTrace.Clear();
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(other);
        }
    }

    /// <summary>
    /// Batch 14 step 6: the creature members of the movement and out-of-reach sites with a personality, on a bare
    /// controller (no stats: the vote context is the default, Unhurt and unthreatened). The charge vote; the AoO price by
    /// verdict; the range adjust (Cowardly +2 on a Ranged, Mixed or casting Role only, never weighted); the keep-away
    /// distance; the risk factor; the out-of-reach orders of the step's two scenarios; one trace row per member call.
    /// </summary>
    private static void TestMovementMembers()
    {
        var go = new GameObject("PersonalityBlendTests mover");
        var made = new List<AIProfile>();
        try
        {
            CharacterController self = go.AddComponent<CharacterController>();

            self.Personality = Adept;
            Assert(!AiBlend.PrefersCharge(self, true), "the adept refuses a legal charge the Role takes (Cowardly .6 Forbid, a quorum)");
            self.Personality = P("Cowardly:0.4,Smart:0.4");
            Assert(!AiBlend.PrefersCharge(self, true), "Cowardly below the quorum still votes the charge down (v -.8)");
            self.Personality = Orc;
            Assert(AiBlend.PrefersCharge(self, false), "the orc's Berserker .3 Insist, below the quorum, still votes a charge in (v +.6)");
            self.Personality = P("Healer:1");
            Assert(!AiBlend.PrefersCharge(self, true), "a Healer is against a charge");
            self.Personality = P("Smart:1");
            Assert(AiBlend.PrefersCharge(self, true) && !AiBlend.PrefersCharge(self, false), "a trait with no charge opinion leaves the Role's answer");

            self.Personality = Adept;
            Assert(AiBlend.AoOCost(self, 2f) == 6f && AiBlend.AoOCost(self, 1000f) == 1000f,
                "an unhurt cowardly creature prices a provoker at least 6 (Against): a Berserk Role's 2 becomes 6, an avoiding Role's 1000 stays");
            self.Personality = P("Berserker:0.6");
            Assert(AiBlend.AoOCost(self, 1000f) == 2f && AiBlend.AoOCost(self, 2f) == 2f,
                "a berserker prices a provoker at most 2 (For): a Humanoid Role's 1000 becomes 2");
            Assert(Near(AiBlend.RiskToleranceFactor(self, 1f), 2f - Mathf.Lerp(0.4f, 1.6f, 0.6f * -0.3f + 0.4f * 0.5f)),
                "the creature's risk factor is its profile's (2 - rho)");

            self.Personality = Adept;
            Assert(Adept.RangeAdjust(true) == 2 && Adept.RangeAdjust(false) == 0,
                "the adept's range adjust: Cowardly's +2 in full at weight .6 (never averaged), on a Ranged, Mixed or casting Role only");
            Assert(P("Cowardly:0.2").RangeAdjust(true) == 2 && Orc.RangeAdjust(true) == 0, "any weight gives the full adjust; a trait without one adds 0");
            Assert(AiBlend.PreferredRange(self, 4, true) == 6 && AiBlend.PreferredRange(self, 4, false) == 4 && AiBlend.PreferredRange(self, 0, true) == 2,
                "the preferred range: the Healer Role's 4 becomes 6; a melee Role keeps its range");
            Assert(AiBlend.KeepAwayDistance(self, AiBlend.LegacyKeepAwaySquares, true) == 4 && AiBlend.KeepAwayDistance(self, AiBlend.LegacyKeepAwaySquares, false) == 2,
                "the cowardly kiter backs off from a foe within 4 squares, not 2, on a Ranged or casting Role; on a melee Role it keeps 2");
            self.Personality = Orc;
            Assert(AiBlend.KeepAwayDistance(self, 2, true) == 2 && AiBlend.PreferredRange(self, 6, true) == 6, "no range adjust: the legacy ranges");

            AIProfile humanoid = AIProfile.CreateRuntime<DND35.AI.Profiles.HumanoidAIProfile>();
            AIProfile ranged = AIProfile.CreateRuntime<DND35.AI.Profiles.RangedAIProfile>();
            AIProfile healer = AIProfile.CreateRuntime<DND35.AI.Profiles.HealerAIProfile>();
            AIProfile meleeCaster = AIProfile.CreateRuntime<DND35.AI.Profiles.SpellcasterAIProfile>();
            meleeCaster.CombatStyle = CombatStyle.Melee;
            made.Add(humanoid);
            made.Add(ranged);
            made.Add(healer);
            made.Add(meleeCaster);
            Assert(!AiBlend.IsRangedOrCastingRole(humanoid) && AiBlend.IsRangedOrCastingRole(ranged) && AiBlend.IsRangedOrCastingRole(healer)
                   && AiBlend.IsRangedOrCastingRole(meleeCaster) && !AiBlend.IsRangedOrCastingRole(null),
                "Ranged and casting Roles fight from range (a casting Role of melee style too); a Humanoid Role and no Role do not");

            var coward = new[] { OutOfReachAlternative.AttackOtherTarget, OutOfReachAlternative.Approach, OutOfReachAlternative.TotalDefense };
            self.Personality = Adept;
            IReadOnlyList<OutOfReachAlternative> c = AiBlend.OrderOutOfReach(self, coward);
            Assert(c.SequenceEqual(new[] { OutOfReachAlternative.AttackOtherTarget, OutOfReachAlternative.TotalDefense, OutOfReachAlternative.Approach }),
                "rules/ai-cowardly-caster-defends-before-approach: the adept puts total defense (-1.32) before the approach (-1.6)", string.Join(">", c));
            var berserk = new[] { OutOfReachAlternative.AttackOtherTarget, OutOfReachAlternative.TotalDefense, OutOfReachAlternative.Approach };
            self.Personality = P("Berserker:0.6");
            IReadOnlyList<OutOfReachAlternative> d = AiBlend.OrderOutOfReach(self, berserk);
            Assert(d.SequenceEqual(new[] { OutOfReachAlternative.AttackOtherTarget, OutOfReachAlternative.Approach, OutOfReachAlternative.TotalDefense }),
                "rules/ai-berserker-approaches-first: Berserker .6 puts the approach (-1.28) before total defense (-1.72)", string.Join(">", d));

            AiDecisionTrace.OverrideEnabled(true);
            AiDecisionTrace.Clear();
            self.Personality = Adept;
            AiDecisionTrace.BeginTurn(self, "Adept", 1, "Healer", Adept.Describe());
            AiBlend.PrefersCharge(self, true);
            AiBlend.AoOCost(self, 2f);
            AiBlend.KeepAwayDistance(self, 2, true);
            AiBlend.PreferredRange(self, 4, true);
            AiBlend.PreferredRange(self, 4, false);
            AiBlend.RiskToleranceFactor(self, 1f);
            AiBlend.OrderOutOfReach(self, coward);
            AiDecisionTrace.TurnRecord rec = AiDecisionTrace.EndTurn(self);
            string sites = rec != null ? string.Join(",", rec.Decisions.Select(x => x.Site)) : "no record";
            Assert(sites == "Personality.Charge,Personality.AoOCost,Personality.KeepAway,Personality.PreferredRange,Personality.RiskTolerance,Personality.OutOfReach",
                "one trace row per member call; an unchanged range (a melee Role) writes none", sites);
        }
        finally
        {
            AiDecisionTrace.OverrideEnabled(null);
            AiDecisionTrace.Clear();
            foreach (AIProfile p in made)
                if (p != null)
                    UnityEngine.Object.DestroyImmediate(p);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
}
#endif
