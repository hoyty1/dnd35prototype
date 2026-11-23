#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DND35.AI.Knowledge;
using DND35.AI.Personality;
using UnityEngine;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 3 (docs/designs/personalities_batch14.md "The seven starter traits"; design 4.4): pins every number of
/// the seven starter rows of TraitDatabase: the minimum tier, RiskAversion, the alignment hint, the target weights, the
/// opinions with their conditions and difficulty mark, the range adjustment, the RoleDesire column and the Mindless
/// lock. Every trait's target weights sum to 1 in absolute value; ids match case-insensitively and give the one shared
/// row; the shared rows are read-only (init-only members, read-only lists) and every RoleDesire maps to one role bit.
/// Either mode (static data).
/// </summary>
public static class TraitDatabaseTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== TRAIT DATABASE TESTS (batch 14 step 3) ======");

        TestCatalogue();
        TestWeightSums();
        TestReadOnlyRows();
        TestSadistic();
        TestCowardly();
        TestSmart();
        TestCaster();
        TestHealer();
        TestBerserker();
        TestMindless();

        Debug.Log($"====== Trait Database Results: {_passed} passed, {_failed} failed ======");
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

    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 1e-4f;

    private static void TestCatalogue()
    {
        string[] ids = TraitDatabase.All.Select(t => t.Id).ToArray();
        Assert(ids.SequenceEqual(new[] { "Sadistic", "Cowardly", "Smart", "Caster", "Healer", "Berserker", "Mindless" }),
            "the seven starter traits, in table order", string.Join(", ", ids));
        Assert(TraitDatabase.Get("cowardly") == TraitDatabase.Get("Cowardly") && TraitDatabase.Get(" SMART ") == TraitDatabase.Get("Smart"),
            "ids match case-insensitively and give the one shared row");
        Assert(TraitDatabase.Get("Protector") == null && TraitDatabase.Get("") == null && TraitDatabase.Get(null) == null,
            "an unknown or empty id gives null (Protector waits for a later batch)");

        var allowed = ActionRoles.Attack | ActionRoles.Approach | ActionRoles.Retreat | ActionRoles.Defend | ActionRoles.Support | ActionRoles.Heal;
        var bad = new List<string>();
        foreach (TraitDefinition t in TraitDatabase.All)
        {
            foreach (RoleDesire d in t.RoleDesires)
            {
                int v = (int)d.Role;
                if (v == 0 || (v & (v - 1)) != 0 || (d.Role & ~allowed) != 0)
                    bad.Add(t.Id + ":" + d.Role);
            }
            if (t.Target.Select(w => w.Consideration).Distinct().Count() != t.Target.Count)
                bad.Add(t.Id + ": a consideration twice");
            if (t.Opinions.Select(o => o.Decision).Distinct().Count() != t.Opinions.Count)
                bad.Add(t.Id + ": a decision twice");
        }
        Assert(bad.Count == 0, "RoleDesire names single Attack/Approach/Retreat/Defend/Support/Heal flags; no consideration or decision twice",
            string.Join("; ", bad));
    }

    /// <summary>The shared rows cannot be changed by a caller (the database-template rule of CLAUDE.md).</summary>
    private static void TestReadOnlyRows()
    {
        var problems = new List<string>();
        foreach (Type type in new[] { typeof(TraitDefinition), typeof(OpinionRule) })
        {
            foreach (System.Reflection.FieldInfo f in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                problems.Add(type.Name + "." + f.Name + " is a public field");
            foreach (System.Reflection.PropertyInfo prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                System.Reflection.MethodInfo set = prop.GetSetMethod();
                if (set != null && !set.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.Name == "IsExternalInit"))
                    problems.Add(type.Name + "." + prop.Name + " has a public setter");
            }
        }
        Assert(problems.Count == 0, "TraitDefinition and OpinionRule have no public field and no public setter (init only)", string.Join("; ", problems));

        bool listsReadOnly = TraitDatabase.All.All(t => !(t.Target is Array) && !(t.Opinions is Array && t.Opinions.Count > 0)
                                                         && !(t.RoleDesires is Array && t.RoleDesires.Count > 0))
                             && !(TraitDatabase.All is List<TraitDefinition>);
        Assert(listsReadOnly, "the rows' lists and TraitDatabase.All are read-only wrappers, not the backing arrays or list");

        var source = new[] { new TargetWeight(ConsiderationId.Proximity, 1f) };
        var probe = new TraitDefinition { Id = "Probe", Target = source };
        source[0] = new TargetWeight(ConsiderationId.HurtMe, 1f);
        Assert(probe.TargetWeightOf(ConsiderationId.Proximity) == 1f && probe.TargetWeightOf(ConsiderationId.HurtMe) == 0f,
            "a row copies the array it is given, so changing that array later does not change the row");

        var badRoles = new List<string>();
        foreach (TraitDefinition t in TraitDatabase.All)
            foreach (RoleDesire d in t.RoleDesires)
                if (!PersonalityProfile.IsSingleRole(d.Role))
                    badRoles.Add(t.Id + ":" + d.Role);
        Assert(badRoles.Count == 0, "every RoleDesire maps to one PersonalityProfile role bit", string.Join("; ", badRoles));
        int highest = Enum.GetValues(typeof(ActionRoles)).Cast<int>().Max();
        Assert(PersonalityProfile.RoleBits >= 1 && (1 << (PersonalityProfile.RoleBits - 1)) == highest,
            "RoleBits covers ActionRoles up to its highest flag", "RoleBits " + PersonalityProfile.RoleBits + ", highest " + highest);
        Assert(!PersonalityProfile.IsSingleRole(ActionRoles.Approach | ActionRoles.Retreat) && !PersonalityProfile.IsSingleRole(ActionRoles.None)
               && PersonalityProfile.IsSingleRole(ActionRoles.Item), "a combined flag or None is not one role; the highest flag is");
    }

    private static void TestWeightSums()
    {
        foreach (TraitDefinition t in TraitDatabase.All)
        {
            float sum = t.Target.Sum(w => Mathf.Abs(w.Weight));
            Assert(Near(sum, 1f), t.Id + "'s target weights sum to 1 in absolute value", "sum " + sum);
        }
    }

    private static void ExpectTargets(TraitDefinition t, params (ConsiderationId id, float w)[] expected)
    {
        bool ok = t.Target.Count == expected.Length;
        foreach (var (id, w) in expected)
            ok &= Near(t.TargetWeightOf(id), w);
        Assert(ok, t.Id + "'s target weights are design 4.4's",
            string.Join(", ", t.Target.Select(x => x.Consideration + " " + x.Weight)));
    }

    private static void ExpectDesires(TraitDefinition t, params (ActionRoles role, float d)[] expected)
    {
        bool ok = t.RoleDesires.Count == expected.Length;
        foreach (var (role, d) in expected)
            ok &= Near(t.DesireFor(role), d);
        Assert(ok, t.Id + "'s RoleDesire column", string.Join(", ", t.RoleDesires.Select(x => x.Role + " " + x.Desire)));
    }

    private static OpinionRule Row(TraitDefinition t, BoolDecision d) => t.Opinions.FirstOrDefault(o => o.Decision == d);

    private static void ExpectRow(TraitDefinition t, BoolDecision d, Opinion when, OpinionCondition cond = OpinionCondition.Always,
        Opinion otherwise = Opinion.Neutral, WoundBand band = WoundBand.Hurt, AIService.AIDifficultyLevel? min = null)
    {
        OpinionRule r = Row(t, d);
        bool ok = r != null && r.WhenTrue == when && r.Condition == cond && r.Otherwise == otherwise && r.MinDifficulty == min
                  && (cond != OpinionCondition.SelfWoundAtLeast || r.Band == band);
        Assert(ok, t.Id + ": " + d + " " + when + (cond != OpinionCondition.Always ? " if " + cond + ", else " + otherwise : "")
                   + (min.HasValue ? " (" + min + "+)" : ""), r != null ? r.ToString() : "no row");
    }

    private static void ExpectHeader(TraitDefinition t, CognitionTier tier, float ra, AlignmentHint hint, int range = 0, bool rangedOnly = false, bool exclusive = false)
    {
        Assert(t != null && t.MinTier == tier && Near(t.RiskAversion, ra) && t.Alignment == hint && t.PreferredRangeAdjust == range
               && t.RangeAdjustOnlyRangedOrCasting == rangedOnly && t.ExclusiveLock == exclusive,
            (t != null ? t.Id : "?") + ": min tier " + tier + ", RA " + ra + ", alignment hint " + hint + ", range +" + range
            + (exclusive ? ", exclusive lock" : ""));
    }

    private static void TestSadistic()
    {
        TraitDefinition t = TraitDatabase.Get("Sadistic");
        ExpectHeader(t, CognitionTier.Low, 0.3f, AlignmentHint.Evil);
        ExpectTargets(t, (ConsiderationId.WoundSeverity, 0.3f), (ConsiderationId.IsDown, 0.3f), (ConsiderationId.Helpless, 0.2f),
            (ConsiderationId.Softness, 0.1f), (ConsiderationId.Fleeing, 0.1f));
        Assert(t.Opinions.Count == 2, "Sadistic has two opinions (Withdraw waits for batch 18)");
        ExpectRow(t, BoolDecision.CoupDeGrace, Opinion.Insist, min: AIService.AIDifficultyLevel.Normal);
        ExpectRow(t, BoolDecision.IgnoreUnconscious, Opinion.Forbid);
        ExpectDesires(t, (ActionRoles.Attack, 0.4f), (ActionRoles.Retreat, -0.4f));
    }

    private static void TestCowardly()
    {
        TraitDefinition t = TraitDatabase.Get("Cowardly");
        ExpectHeader(t, CognitionTier.Animal, 1.0f, AlignmentHint.None, range: 2, rangedOnly: true);
        ExpectTargets(t, (ConsiderationId.ThreatensMe, -0.3f), (ConsiderationId.EngagedByAlly, 0.3f), (ConsiderationId.LooksDangerous, -0.2f),
            (ConsiderationId.Proximity, -0.2f));
        Assert(t.Opinions.Count == 2, "Cowardly has two opinions (IgnoreAoO is ProvokeAoO; Withdraw waits for batch 18)");
        ExpectRow(t, BoolDecision.Charge, Opinion.Forbid);
        ExpectRow(t, BoolDecision.ProvokeAoO, Opinion.Forbid, OpinionCondition.SelfWoundAtLeast, Opinion.Against, WoundBand.Hurt);
        ExpectDesires(t, (ActionRoles.Retreat, 0.8f), (ActionRoles.Defend, 0.5f), (ActionRoles.Approach, -0.5f), (ActionRoles.Attack, -0.2f));
    }

    private static void TestSmart()
    {
        TraitDefinition t = TraitDatabase.Get("Smart");
        ExpectHeader(t, CognitionTier.Average, 0.6f, AlignmentHint.None);
        ExpectTargets(t, (ConsiderationId.KillChance, 0.45f), (ConsiderationId.KnownHealer, 0.15f), (ConsiderationId.KnownCaster, 0.15f),
            (ConsiderationId.FlankOpportunity, 0.15f), (ConsiderationId.IsDown, -0.1f));
        Assert(t.Opinions.Count == 3, "Smart has three opinions");
        ExpectRow(t, BoolDecision.IgnoreUnconscious, Opinion.For, OpinionCondition.StandingThreatInReach);
        ExpectRow(t, BoolDecision.CoupDeGrace, Opinion.For, OpinionCondition.NoEnemyThreatensSelf, Opinion.Against);
        ExpectRow(t, BoolDecision.ProvokeAoO, Opinion.Against);
        ExpectDesires(t);
    }

    private static void TestCaster()
    {
        TraitDefinition t = TraitDatabase.Get("Caster");
        ExpectHeader(t, CognitionTier.Low, 0.7f, AlignmentHint.None);
        ExpectTargets(t, (ConsiderationId.Clustered, 0.4f), (ConsiderationId.KnownCaster, 0.3f), (ConsiderationId.ThreatensMe, -0.3f));
        Assert(t.Opinions.Count == 2, "Caster has two opinions");
        ExpectRow(t, BoolDecision.Charge, Opinion.Forbid);
        ExpectRow(t, BoolDecision.ProvokeAoO, Opinion.Against);
        ExpectDesires(t, (ActionRoles.Retreat, 0.4f), (ActionRoles.Approach, -0.2f), (ActionRoles.Support, 0.2f));
    }

    private static void TestHealer()
    {
        TraitDefinition t = TraitDatabase.Get("Healer");
        ExpectHeader(t, CognitionTier.Low, 0.3f, AlignmentHint.None);
        ExpectTargets(t, (ConsiderationId.ThreatToWard, 0.4f), (ConsiderationId.EngagedByAlly, 0.3f), (ConsiderationId.ThreatensMe, -0.3f));
        Assert(t.Opinions.Count == 2, "Healer has two opinions");
        ExpectRow(t, BoolDecision.CoupDeGrace, Opinion.Against);
        ExpectRow(t, BoolDecision.Charge, Opinion.Against);
        ExpectDesires(t, (ActionRoles.Heal, 0.8f), (ActionRoles.Support, 0.6f), (ActionRoles.Defend, 0.1f));
    }

    private static void TestBerserker()
    {
        TraitDefinition t = TraitDatabase.Get("Berserker");
        ExpectHeader(t, CognitionTier.Animal, -0.3f, AlignmentHint.None);
        ExpectTargets(t, (ConsiderationId.Proximity, 0.4f), (ConsiderationId.HurtMe, 0.4f), (ConsiderationId.LooksDangerous, 0.2f));
        Assert(t.Opinions.Count == 2, "Berserker has two opinions (Withdraw waits for batch 18)");
        ExpectRow(t, BoolDecision.Charge, Opinion.Insist);
        ExpectRow(t, BoolDecision.ProvokeAoO, Opinion.For);
        ExpectDesires(t, (ActionRoles.Attack, 0.6f), (ActionRoles.Approach, 0.6f), (ActionRoles.Retreat, -0.8f), (ActionRoles.Defend, -0.6f));
    }

    private static void TestMindless()
    {
        TraitDefinition t = TraitDatabase.Get("Mindless");
        ExpectHeader(t, CognitionTier.Mindless, 0f, AlignmentHint.None, exclusive: true);
        ExpectTargets(t, (ConsiderationId.Proximity, 1.0f));
        Assert(t.Opinions.Count == 0, "Mindless has no opinion (the Role's defaults)");
        ExpectDesires(t);
        Assert(TraitDatabase.All.Count(x => x.ExclusiveLock) == 1, "Mindless is the only exclusive trait");
    }
}
}
#endif
