#if UNITY_EDITOR
using System.Collections.Generic;
using DND35.AI;
using DND35.AI.Personality;
using DND35.AI.Profiles;
using UnityEngine;
using StubView = Tests.AI.ConsiderationTests.StubView;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 5 (docs/designs/personalities_batch14.md "The blend": each member returns its legacy argument when the
/// personality is null): every AiBlend member, the profile-based ones and the creature-based ones the target sites call
/// (ScoreTarget, IgnoresUnconscious, Adaptive, UsesCoupDeGrace, TracesTargets), returns its legacy argument for a creature
/// with no traits (and for no creature), for both values of each boolean and several scores, and writes no decision-trace
/// row though the creature's AI turn is open. AIService.UsesCoupDeGrace, the merged copy of the two coup de grace choices,
/// equals the old formula (the data override, else the profile's ShouldUseCoupDeGrace, else no) for every combination.
/// This is the unit side of the step's neutrality: a creature with no traits plays exactly as before. Bare controllers
/// with no stats; either mode.
/// </summary>
public static class AiBlendPassThroughTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== AI BLEND PASS-THROUGH TESTS (batch 14 step 5) ======");

        var go = new GameObject("AiBlendPassThroughTests creature");
        var other = new GameObject("AiBlendPassThroughTests target");
        var profiles = new List<AIProfile>();
        try
        {
            CharacterController self = go.AddComponent<CharacterController>();
            CharacterController target = other.AddComponent<CharacterController>();
            self.Personality = null;
            AiDecisionTrace.OverrideEnabled(true);
            AiDecisionTrace.Clear();
            AiDecisionTrace.BeginTurn(self, "Plain", 1, "Test", "");

            TestProfileMembers();
            TestCreatureMembers(self, target);
            TestNoCreature(target);

            AiDecisionTrace.TurnRecord rec = AiDecisionTrace.EndTurn(self);
            Assert(rec != null && rec.Decisions.Count == 0 && rec.Overflow == 0,
                "no member wrote a trace row for the creature with no traits, though its turn was open",
                rec != null ? rec.Collapsed() : "no record");

            TestUsesCoupDeGraceFormula(self, profiles);
        }
        finally
        {
            AiDecisionTrace.OverrideEnabled(null);
            AiDecisionTrace.Clear();
            foreach (AIProfile p in profiles)
                if (p != null)
                    Object.DestroyImmediate(p);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(other);
        }

        Debug.Log($"====== AI Blend Pass-Through Results: {_passed} passed, {_failed} failed ======");
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

    private static readonly float[] Scores = { 0f, 12.5f, -3.25f, 40f, float.MinValue, float.NegativeInfinity };
    private static readonly bool[] Bools = { false, true };

    /// <summary>The members that take a profile: a null profile returns the legacy argument.</summary>
    private static void TestProfileMembers()
    {
        var view = new StubView().Number(DND35.AI.Knowledge.FactKey.Distance, 1).Flag(DND35.AI.Knowledge.FactKey.Down);
        var ctx = new JudgeContext { SelfWound = DND35.AI.Knowledge.WoundBand.BadlyHurt, EnemyThreatensSelf = true, StandingThreatInReach = true };
        bool scores = true, judges = true, prices = true, risks = true;
        foreach (float s in Scores)
            scores &= AiBlend.ScoreTarget(null, s, view).Equals(s);
        foreach (BoolDecision d in System.Enum.GetValues(typeof(BoolDecision)))
            foreach (bool legacy in Bools)
            {
                judges &= AiBlend.Judge(null, d, legacy, ctx) == legacy;
                judges &= AiBlend.Judge(null, d, legacy, ctx, dataOverride: !legacy) == legacy;
            }
        foreach (float price in new[] { 0f, 1f, 2f, 6f, 1000f })
            prices &= AiBlend.AoOCost(null, price, ctx) == price;
        foreach (float m in new[] { 0.25f, 1f, 1.5f })
            risks &= AiBlend.RiskToleranceFactor(null, m) == m;
        var list = new[] { OutOfReachAlternative.PreferredRange, OutOfReachAlternative.AttackOtherTarget, OutOfReachAlternative.Withdraw };
        Assert(scores, "ScoreTarget(null profile) returns every legacy score, invalid ones included");
        Assert(judges, "Judge(null profile) returns the legacy answer for every decision, with or without a data override");
        Assert(prices, "AoOCost(null profile) returns the legacy price");
        Assert(risks, "RiskToleranceFactor(null profile) returns the legacy multiplier");
        Assert(ReferenceEquals(AiBlend.OrderOutOfReach(null, list), list), "OrderOutOfReach(null profile) returns the Role's list itself");
    }

    /// <summary>The members the target sites call, for a creature with no traits.</summary>
    private static void TestCreatureMembers(CharacterController self, CharacterController target)
    {
        bool scores = true;
        foreach (float s in Scores)
            scores &= AiBlend.ScoreTarget(self, target, s).Equals(s) && AiBlend.ScoreTarget(self, null, s).Equals(s);
        Assert(scores, "ScoreTarget(creature with no traits) returns every legacy score");

        bool ignores = true, adaptive = true;
        foreach (bool legacy in Bools)
        {
            ignores &= AiBlend.IgnoresUnconscious(self, legacy) == legacy;
            adaptive &= AiBlend.Adaptive(self, legacy) == legacy;
        }
        Assert(ignores, "IgnoresUnconscious(creature with no traits) returns the Role's answer");
        Assert(adaptive, "Adaptive(creature with no traits) returns the Role's answer");

        bool coup = true;
        foreach (bool role in Bools)
        {
            coup &= AiBlend.UsesCoupDeGrace(self, role, null) == role;
            coup &= AiBlend.UsesCoupDeGrace(self, role, true);
            coup &= !AiBlend.UsesCoupDeGrace(self, role, false);
        }
        Assert(coup, "UsesCoupDeGrace(creature with no traits): the data override, else the Role's answer");
        Assert(!AiBlend.TracesTargets(self), "TracesTargets is false for a creature with no traits (no summary row is built)");
        AiBlend.TraceTarget(self, "Target", "a", "b", 3f);
    }

    /// <summary>No creature at all: the same pass-through.</summary>
    private static void TestNoCreature(CharacterController target)
    {
        Assert(AiBlend.ScoreTarget((CharacterController)null, target, 7f) == 7f && AiBlend.IgnoresUnconscious(null, true)
               && !AiBlend.Adaptive(null, false) && AiBlend.UsesCoupDeGrace(null, true, null) && !AiBlend.TracesTargets(null),
            "with no creature every member returns its legacy argument");
    }

    /// <summary>
    /// AIService.UsesCoupDeGrace equals the two old copies (AIService.ShouldNPCUseCoupDeGrace and the test of
    /// GameManager.HasNPCCoupDeGraceOption: the override, else the profile's answer, else no) for every override and
    /// for no profile, a profile that refuses (Humanoid) and one that takes it (Berserk).
    /// </summary>
    private static void TestUsesCoupDeGraceFormula(CharacterController npc, List<AIProfile> made)
    {
        AIProfile humanoid = AIProfile.CreateRuntime<HumanoidAIProfile>();
        AIProfile berserk = AIProfile.CreateRuntime<BerserkAIProfile>();
        made.Add(humanoid);
        made.Add(berserk);
        bool ok = true;
        var detail = new List<string>();
        foreach (AIProfile profile in new[] { null, humanoid, berserk })
            foreach (bool? data in new bool?[] { null, false, true })
            {
                npc.aiProfile = profile;
                npc.EnemyUseCoupDeGraceOverride = data;
                bool old = data ?? (profile != null && profile.ShouldUseCoupDeGrace(npc));
                bool merged = AIService.UsesCoupDeGrace(npc);
                if (merged != old)
                {
                    ok = false;
                    detail.Add((profile != null ? profile.ProfileName : "none") + "/" + (data.HasValue ? data.Value.ToString() : "null") + ": " + merged);
                }
            }
        npc.aiProfile = null;
        npc.EnemyUseCoupDeGraceOverride = null;
        Assert(ok, "AIService.UsesCoupDeGrace equals the old formula for every profile and override (no personality)", string.Join(", ", detail));
        Assert(!AIService.UsesCoupDeGrace(null), "no creature: no coup de grace");
    }
}
}
#endif
