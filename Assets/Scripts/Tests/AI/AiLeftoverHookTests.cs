#if UNITY_EDITOR
using System.Collections.Generic;
using DND35.AI;
using DND35.AI.Knowledge;
using DND35.AI.Personality;
using DND35.AI.Profiles;
using UnityEngine;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 11 (AI-077; decision D1, design choice C8; docs/designs/personalities_batch14.md "Leftover hooks"): the
/// move left after a standard-action ability. The default table of AIProfile.DefaultChooseAfterStandardAction (no move:
/// stay; a Melee or Mixed Role approaches a focus out of reach; a Ranged or casting Role keeps its distance from a close
/// or threatening foe when it can still fight from range; a prone
/// creature that wants to move stands up instead, but a threatened one that wants distance stays down; otherwise it stays), the shipped profiles' hook equal to it, and the
/// traits' tilt (AiBlend.AfterStandardAction): no personality keeps the Role's choice; Cowardly backs away where the Role
/// would close in (Retreat .8 against Approach -.5) and Berserker closes in where the Role would back away (Approach .6
/// against Retreat -.8), with the starter scores pinned. Pure: contexts are filled by hand. Either mode.
/// </summary>
public static class AiLeftoverHookTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== AI LEFTOVER HOOK TESTS (batch 14 step 11) ======");

        TestDefaultTable();
        TestShippedProfilesUseTheDefault();
        TestNoPersonalityKeepsRole();
        TestCowardlyAndBerserkerReorder();

        Debug.Log($"====== AI Leftover Hook Results: {_passed} passed, {_failed} failed ======");
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
            Debug.LogError($"  [FAIL] {testName}" + (detail != null ? " - " + detail : ""));
        }
    }

    private static AfterStandardActionContext Ctx(CombatStyle style, bool casting = false, bool canMove = true, bool prone = false,
        bool outOfReach = false, bool near = false, bool threatened = false, bool ranged = false)
        => new AfterStandardActionContext
        {
            Style = style,
            CastingRole = casting,
            CanMove = canMove,
            Prone = prone,
            FocusOutOfReach = outOfReach,
            FoeWithinKeepAway = near,
            Threatened = threatened,
            CanFightFromRange = ranged
        };

    private static void Expect(string name, AfterStandardActionContext ctx, AfterStandardActionChoice expected)
    {
        AfterStandardActionChoice got = AIProfile.DefaultChooseAfterStandardAction(ctx);
        Assert(got == expected, "default: " + name + " -> " + expected, "got " + got);
    }

    private static void TestDefaultTable()
    {
        Expect("melee, no move left, focus out of reach", Ctx(CombatStyle.Melee, canMove: false, outOfReach: true), AfterStandardActionChoice.Stay);
        Expect("melee, focus out of reach", Ctx(CombatStyle.Melee, outOfReach: true), AfterStandardActionChoice.Approach);
        Expect("melee, focus in reach, threatened", Ctx(CombatStyle.Melee, threatened: true, near: true), AfterStandardActionChoice.Stay);
        Expect("Mixed non-casting (dragon), focus out of reach", Ctx(CombatStyle.Mixed, outOfReach: true), AfterStandardActionChoice.Approach);
        Expect("melee-style casting Role (Spellcaster), nobody close, focus out of reach", Ctx(CombatStyle.Melee, casting: true, outOfReach: true), AfterStandardActionChoice.Stay);
        Expect("melee-style casting Role (Spellcaster), a foe close, can fight from range", Ctx(CombatStyle.Melee, casting: true, near: true, ranged: true), AfterStandardActionChoice.KeepDistance);
        Expect("Ranged, foe within keep-away, can fight from range", Ctx(CombatStyle.Ranged, near: true, ranged: true), AfterStandardActionChoice.KeepDistance);
        Expect("Ranged, threatened, no ranged means (AI-039)", Ctx(CombatStyle.Ranged, threatened: true, near: true), AfterStandardActionChoice.Stay);
        Expect("Ranged, nobody close, focus out of reach", Ctx(CombatStyle.Ranged, outOfReach: true, ranged: true), AfterStandardActionChoice.Stay);
        Expect("Mixed casting Role (abjurer), threatened, can fight from range", Ctx(CombatStyle.Mixed, casting: true, threatened: true, ranged: true), AfterStandardActionChoice.KeepDistance);
        Expect("prone melee, focus out of reach", Ctx(CombatStyle.Melee, prone: true, outOfReach: true), AfterStandardActionChoice.StandUp);
        Expect("prone Ranged, foe close, can fight from range", Ctx(CombatStyle.Ranged, prone: true, near: true, ranged: true), AfterStandardActionChoice.StandUp);
        Expect("prone melee, focus in reach", Ctx(CombatStyle.Melee, prone: true), AfterStandardActionChoice.Stay);
        Expect("prone caster, threatened, can fight from range: stays down (standing provokes, gains no distance)",
            Ctx(CombatStyle.Ranged, casting: true, prone: true, near: true, threatened: true, ranged: true), AfterStandardActionChoice.Stay);
    }

    private static void TestShippedProfilesUseTheDefault()
    {
        var profiles = new List<AIProfile>();
        try
        {
            profiles.Add(AIProfile.CreateRuntime<HumanoidAIProfile>());
            profiles.Add(AIProfile.CreateRuntime<HealerAIProfile>());
            profiles.Add(AIProfile.CreateRuntime<EvokerAIProfile>());
            profiles.Add(AIProfile.CreateRuntime<DragonAIProfile>());
            var contexts = new[]
            {
                Ctx(CombatStyle.Melee, outOfReach: true),
                Ctx(CombatStyle.Ranged, near: true, ranged: true),
                Ctx(CombatStyle.Melee, prone: true, outOfReach: true),
                Ctx(CombatStyle.Ranged, canMove: false, near: true, ranged: true)
            };
            foreach (AIProfile p in profiles)
            {
                bool same = true;
                foreach (AfterStandardActionContext c in contexts)
                    same &= p.ChooseAfterStandardAction(null, c) == AIProfile.DefaultChooseAfterStandardAction(c);
                Assert(same, p.GetType().Name + ".ChooseAfterStandardAction equals the default");
            }
        }
        finally
        {
            foreach (AIProfile p in profiles)
                if (p != null)
                    Object.DestroyImmediate(p);
        }
    }

    private static PersonalityProfile P(string spec)
        => PersonalityFactory.Create(spec, CognitionTier.Average, Alignment.None, "leftover_test", _ => { });

    private static void TestNoPersonalityKeepsRole()
    {
        var c = Ctx(CombatStyle.Melee, outOfReach: true, near: true);
        foreach (AfterStandardActionChoice role in new[] { AfterStandardActionChoice.Stay, AfterStandardActionChoice.Approach,
                     AfterStandardActionChoice.KeepDistance, AfterStandardActionChoice.StandUp })
            Assert(AiBlend.AfterStandardAction((PersonalityProfile)null, role, c) == role, "no personality keeps the Role's " + role);
        Assert(AiBlend.AfterStandardAction((CharacterController)null, AfterStandardActionChoice.Approach, c) == AfterStandardActionChoice.Approach,
            "no creature keeps the Role's choice");
        Assert(AiBlend.RolesOf(AfterStandardActionChoice.Approach) == ActionRoles.Approach
               && AiBlend.RolesOf(AfterStandardActionChoice.KeepDistance) == ActionRoles.Retreat
               && AiBlend.RolesOf(AfterStandardActionChoice.Stay) == ActionRoles.None,
            "roles: Approach is Approach, KeepDistance is Retreat, Stay is none");
    }

    private static void TestCowardlyAndBerserkerReorder()
    {
        PersonalityProfile coward = P("Cowardly:1");
        PersonalityProfile berserker = P("Berserker:1");
        Assert(coward != null && berserker != null, "the two test personalities build");
        if (coward == null || berserker == null)
            return;
        Assert(Mathf.Approximately(coward.RoleShift(ActionRoles.Retreat), 0.8f) && Mathf.Approximately(coward.RoleShift(ActionRoles.Approach), -0.5f)
               && Mathf.Approximately(berserker.RoleShift(ActionRoles.Approach), 0.6f) && Mathf.Approximately(berserker.RoleShift(ActionRoles.Retreat), -0.8f),
            "starter desires: Cowardly Retreat .8, Approach -.5; Berserker Approach .6, Retreat -.8");
        Assert(Mathf.Approximately(AiBlend.AfterStandardDesireGain, 2f), "the desire gain is 2 (starter constant)");

        // A melee Role closing on its focus, a foe close: Approach scores 1 - 1 = 0, KeepDistance 1.6, Stay 0.
        var meleeNear = Ctx(CombatStyle.Melee, outOfReach: true, near: true);
        AfterStandardActionChoice role = AIProfile.DefaultChooseAfterStandardAction(meleeNear);
        Assert(role == AfterStandardActionChoice.Approach, "melee Role, focus out of reach and a foe close: the Role approaches");
        Assert(AiBlend.AfterStandardAction(coward, role, meleeNear) == AfterStandardActionChoice.KeepDistance, "Cowardly backs away instead");
        Assert(AiBlend.AfterStandardAction(berserker, role, meleeNear) == AfterStandardActionChoice.Approach, "Berserker approaches as the Role does");

        // Nobody close: Cowardly's Approach (0) ties Stay (0), so the Role's choice is kept.
        var meleeFar = Ctx(CombatStyle.Melee, outOfReach: true);
        Assert(AiBlend.AfterStandardAction(coward, AfterStandardActionChoice.Approach, meleeFar) == AfterStandardActionChoice.Approach,
            "Cowardly with nobody close: a tie keeps the Role's approach");

        // A casting Role keeping its distance, its focus out of reach: KeepDistance 1 - 1.6 = -0.6, Approach 1.2, Stay 0.
        var casterNear = Ctx(CombatStyle.Ranged, casting: true, outOfReach: true, near: true, ranged: true);
        role = AIProfile.DefaultChooseAfterStandardAction(casterNear);
        Assert(role == AfterStandardActionChoice.KeepDistance, "casting Role, a foe close: the Role keeps its distance");
        Assert(AiBlend.AfterStandardAction(berserker, role, casterNear) == AfterStandardActionChoice.Approach, "Berserker closes in instead");
        Assert(AiBlend.AfterStandardAction(coward, role, casterNear) == AfterStandardActionChoice.KeepDistance, "Cowardly keeps its distance as the Role does");

        // The same caster with its focus in reach: no approach is open, so Berserker stays rather than backing away.
        var casterInReach = Ctx(CombatStyle.Ranged, casting: true, near: true, threatened: true, ranged: true);
        Assert(AiBlend.AfterStandardAction(berserker, AfterStandardActionChoice.KeepDistance, casterInReach) == AfterStandardActionChoice.Stay,
            "Berserker with the focus in reach stays instead of backing away");

        // A Role that stays (a Ranged Role with no ranged means, threatened): KeepDistance is closed (AI-039), so Cowardly
        // stays too; with the foe close but not threatening, Cowardly backs away anyway (1.6 > 1).
        var rangedNoMeans = Ctx(CombatStyle.Ranged, threatened: true, near: true);
        Assert(AiBlend.AfterStandardAction(coward, AIProfile.DefaultChooseAfterStandardAction(rangedNoMeans), rangedNoMeans) == AfterStandardActionChoice.Stay,
            "Cowardly, threatened with no ranged means, stays as the Role does (AI-039)");
        var rangedNoMeansNear = Ctx(CombatStyle.Ranged, near: true);
        Assert(AiBlend.AfterStandardAction(coward, AIProfile.DefaultChooseAfterStandardAction(rangedNoMeansNear), rangedNoMeansNear) == AfterStandardActionChoice.KeepDistance,
            "Cowardly backs away from a close foe that does not threaten, where the Role would stay");

        // Prone: the blended move stands the creature up first.
        var proneNear = Ctx(CombatStyle.Melee, prone: true, outOfReach: true, near: true);
        role = AIProfile.DefaultChooseAfterStandardAction(proneNear);
        Assert(role == AfterStandardActionChoice.StandUp && AiBlend.AfterStandardAction(coward, role, proneNear) == AfterStandardActionChoice.StandUp,
            "prone: the Role and Cowardly both stand up first");
        var proneCaster = Ctx(CombatStyle.Ranged, casting: true, prone: true, near: true, threatened: true, ranged: true);
        Assert(AiBlend.AfterStandardAction(berserker, AIProfile.DefaultChooseAfterStandardAction(proneCaster), proneCaster) == AfterStandardActionChoice.Stay,
            "prone caster, focus in reach: Berserker stays prone rather than standing to back away");

        // No move left: every personality stays.
        var noMove = Ctx(CombatStyle.Melee, canMove: false, outOfReach: true, near: true);
        Assert(AiBlend.AfterStandardAction(coward, AfterStandardActionChoice.Stay, noMove) == AfterStandardActionChoice.Stay
               && AiBlend.AfterStandardAction(berserker, AfterStandardActionChoice.Stay, noMove) == AfterStandardActionChoice.Stay,
            "no move left: Cowardly and Berserker stay");
    }
}
}
#endif
