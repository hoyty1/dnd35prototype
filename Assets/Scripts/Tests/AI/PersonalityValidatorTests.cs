#if UNITY_EDITOR
using System.Collections.Generic;
using DND35.AI.Knowledge;
using DND35.AI.Personality;
using UnityEngine;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 3 (docs/designs/personalities_batch14.md "The seven starter traits", C2; design 4.1): the cognition
/// tiers, PersonalityValidator and PersonalityFactory.
/// <list type="bullet">
/// <item>The tier of an Intelligence score (Mindless without one, Animal 1-2, Low 3-7, Average 8-12, Keen 13-16,
/// Brilliant 17+), of CharacterStats (IsMindless, no INT score).</item>
/// <item>Smart on a mindless creature is dropped, leaving a null personality, with one warning.</item>
/// <item>"Mindless, Smart:0.5" keeps only Mindless (the lock); Smart on an Int 7 creature is dropped.</item>
/// <item>Sadistic on a lawful good creature only warns (owner, 2026-10-30).</item>
/// <item>Unknown ids, bad weights and repeats are dropped; all problems make one warning per definition.</item>
/// <item>A sum above 1 is scaled to 1; a shortfall is kept (C2). Parsing: ',' or ';', ':' or '=', a bare id weighs 1.</item>
/// </list>
/// Either mode (bare CharacterStats, no creature).
/// </summary>
public static class PersonalityValidatorTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== PERSONALITY VALIDATOR TESTS (batch 14 step 3) ======");

        TestTiers();
        TestSmartOnMindless();
        TestMindlessLock();
        TestTierDrop();
        TestAlignmentWarnsOnly();
        TestBadEntries();
        TestScaling();
        TestParsing();
        TestDefaultWarningOnce();

        Debug.Log($"====== Personality Validator Results: {_passed} passed, {_failed} failed ======");
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

    private static PersonalityProfile Make(string spec, CognitionTier tier, Alignment alignment, List<string> warnings)
        => PersonalityFactory.Create(spec, tier, alignment, "test_creature", warnings.Add);

    private static void TestTiers()
    {
        var cases = new (int score, CognitionTier tier)[]
        {
            (CharacterStats.NO_SCORE, CognitionTier.Mindless), (0, CognitionTier.Animal), (1, CognitionTier.Animal), (2, CognitionTier.Animal),
            (3, CognitionTier.Low), (7, CognitionTier.Low), (8, CognitionTier.Average), (12, CognitionTier.Average),
            (13, CognitionTier.Keen), (16, CognitionTier.Keen), (17, CognitionTier.Brilliant), (25, CognitionTier.Brilliant)
        };
        bool ok = true;
        var detail = new List<string>();
        foreach (var (score, tier) in cases)
        {
            CognitionTier got = CognitionTiers.FromIntelligence(score, mindless: false);
            if (got != tier)
            {
                ok = false;
                detail.Add(score + "->" + got);
            }
        }
        Assert(ok, "Intelligence to tier: none Mindless, 0-2 Animal, 3-7 Low, 8-12 Average, 13-16 Keen, 17+ Brilliant", string.Join(", ", detail));
        Assert(CognitionTiers.FromIntelligence(10, mindless: true) == CognitionTier.Mindless, "the mindless flag wins over a score");
        Assert(CognitionTier.Mindless < CognitionTier.Animal && CognitionTier.Average < CognitionTier.Keen, "tiers are ordered");

        Assert(CognitionTiers.Of(new CharacterStats { INT = CharacterStats.NO_SCORE }) == CognitionTier.Mindless, "CharacterStats with no INT score: Mindless");
        Assert(CognitionTiers.Of(new CharacterStats { INT = 10, IsMindless = true }) == CognitionTier.Mindless, "CharacterStats.IsMindless: Mindless");
        Assert(CognitionTiers.Of(new CharacterStats { INT = 13 }) == CognitionTier.Keen, "CharacterStats Int 13: Keen (the hobgoblin captain)");
        Assert(CognitionTiers.Of(new CharacterStats { INT = 7 }) == CognitionTier.Low, "CharacterStats Int 7: Low");
        Assert(CognitionTiers.Of(null) == CognitionTier.Mindless, "no stats: Mindless");
    }

    private static void TestSmartOnMindless()
    {
        var warnings = new List<string>();
        PersonalityProfile p = Make("Smart:0.5", CognitionTier.Mindless, Alignment.None, warnings);
        Assert(p == null, "Smart on a mindless creature is dropped, leaving a null personality");
        Assert(warnings.Count == 1 && warnings[0].Contains("Smart dropped: requires Average tier") && warnings[0].Contains("test_creature"),
            "one warning names the creature and the tier", warnings.Count > 0 ? warnings[0] : "none");

        var stats = new CharacterStats { INT = CharacterStats.NO_SCORE, IsMindless = true };
        var w2 = new List<string>();
        Assert(PersonalityFactory.Create("Smart:0.5", stats, "zombie_shambler", w2.Add) == null && w2.Count == 1,
            "through CharacterStats: a zombie's Smart:0.5 gives null and one warning");
    }

    private static void TestMindlessLock()
    {
        var warnings = new List<string>();
        PersonalityProfile p = Make("Mindless, Smart:0.5", CognitionTier.Average, Alignment.None, warnings);
        Assert(p != null && p.Traits.Count == 1 && p.Traits[0].Trait.Id == "Mindless" && Near(p.Traits[0].Weight, 1f),
            "'Mindless, Smart:0.5' keeps only Mindless at weight 1", p != null ? p.Describe() : "null");
        Assert(warnings.Count == 1 && warnings[0].Contains("Mindless lock: Smart dropped"), "the lock warns once", warnings.Count > 0 ? warnings[0] : "none");

        var w2 = new List<string>();
        PersonalityProfile z = Make("Mindless, Smart:0.5", CognitionTier.Mindless, Alignment.None, w2);
        Assert(z != null && z.Traits.Count == 1 && z.Traits[0].Trait.Id == "Mindless" && w2.Count == 1 && w2[0].Contains("Smart dropped: requires"),
            "on a mindless creature Smart is dropped by its tier, Mindless kept, one warning", w2.Count > 0 ? w2[0] : "none");

        var w3 = new List<string>();
        PersonalityProfile m = Make("Mindless", CognitionTier.Mindless, Alignment.None, w3);
        Assert(m != null && w3.Count == 0 && Near(m.FoldedWeight(ConsiderationId.Proximity), 1f), "Mindless alone: Proximity 1.0, no warning");
    }

    private static void TestTierDrop()
    {
        var warnings = new List<string>();
        PersonalityProfile p = Make("Smart:1", CognitionTiers.FromIntelligence(7, false), Alignment.None, warnings);
        Assert(p == null && warnings.Count == 1 && warnings[0].Contains("the creature is Low"), "Smart on an Int 7 creature is dropped",
            warnings.Count > 0 ? warnings[0] : "none");

        var w2 = new List<string>();
        PersonalityProfile q = Make("Smart:0.5,Cowardly:0.5", CognitionTier.Low, Alignment.None, w2);
        Assert(q != null && q.Traits.Count == 1 && q.Traits[0].Trait.Id == "Cowardly" && Near(q.Traits[0].Weight, 0.5f) && w2.Count == 1,
            "a dropped trait's weight is not handed to the rest (Cowardly stays .5)", q != null ? q.Describe() : "null");

        var w3 = new List<string>();
        Assert(Make("Cowardly:0.6,Berserker:0.4", CognitionTier.Animal, Alignment.None, w3) != null && w3.Count == 0,
            "Animal-tier traits on an Animal-tier creature: kept, no warning");
    }

    private static void TestAlignmentWarnsOnly()
    {
        var warnings = new List<string>();
        PersonalityProfile p = Make("Sadistic:0.7", CognitionTier.Average, Alignment.LawfulGood, warnings);
        Assert(p != null && Near(p.WeightOf("Sadistic"), 0.7f), "Sadistic on a lawful good creature is kept");
        Assert(warnings.Count == 1 && warnings[0].Contains("Sadistic hints at Evil") && warnings[0].Contains("LG"),
            "and only warns", warnings.Count > 0 ? warnings[0] : "none");

        var w2 = new List<string>();
        Assert(Make("Sadistic:0.7", CognitionTier.Average, Alignment.ChaoticEvil, w2) != null && w2.Count == 0, "Sadistic on a chaotic evil creature: no warning");
        var w3 = new List<string>();
        Assert(Make("Sadistic:0.7", CognitionTier.Average, Alignment.None, w3) != null && w3.Count == 0, "an unset alignment: no warning");
        Assert(PersonalityValidator.Fits(AlignmentHint.None, Alignment.LawfulGood) && !PersonalityValidator.Fits(AlignmentHint.Evil, Alignment.TrueNeutral),
            "a trait with no hint fits every alignment; Evil does not fit neutral");
    }

    private static void TestBadEntries()
    {
        var warnings = new List<string>();
        PersonalityProfile p = Make("Grumpy:0.3, Cowardly:abc, Healer:-0.2, Smart:0.4, smart:0.2, Cowardly:0.3",
            CognitionTier.Average, Alignment.None, warnings);
        Assert(p != null && p.Traits.Count == 2 && Near(p.WeightOf("Smart"), 0.4f) && Near(p.WeightOf("Cowardly"), 0.3f),
            "unknown ids, non-numeric and negative weights and a repeat are dropped; the rest kept", p != null ? p.Describe() : "null");
        Assert(warnings.Count == 1, "every problem goes in one warning per definition", warnings.Count + " warnings");
        string w = warnings.Count > 0 ? warnings[0] : "";
        Assert(w.Contains("unknown trait 'Grumpy'") && w.Contains("'Cowardly:abc' dropped") && w.Contains("'Healer:-0.2' dropped")
               && w.Contains("Smart repeated"), "the warning names each problem", w);

        var w2 = new List<string>();
        Assert(Make("", CognitionTier.Average, Alignment.None, w2) == null && Make(null, CognitionTier.Average, Alignment.None, w2) == null
               && Make("  ", CognitionTier.Average, Alignment.None, w2) == null && w2.Count == 0,
            "an empty spec gives null and no warning");
        var w3 = new List<string>();
        Assert(Make("Nope:1", CognitionTier.Average, Alignment.None, w3) == null && w3.Count == 1, "only unknown ids: null, one warning");
    }

    private static void TestScaling()
    {
        var warnings = new List<string>();
        PersonalityProfile p = Make("Cowardly:0.9,Healer:0.6", CognitionTier.Average, Alignment.None, warnings);
        Assert(p != null && Near(p.WeightOf("Cowardly"), 0.6f) && Near(p.WeightOf("Healer"), 0.4f) && Near(p.TotalWeight, 1f),
            "a sum of 1.5 is scaled to 1 (.6 and .4)", p != null ? p.Describe() : "null");
        Assert(warnings.Count == 1 && warnings[0].Contains("scaled to 1"), "the scaling is reported", warnings.Count > 0 ? warnings[0] : "none");

        var w2 = new List<string>();
        PersonalityProfile q = Make("Smart:0.5", CognitionTier.Average, Alignment.None, w2);
        Assert(q != null && Near(q.WeightOf("Smart"), 0.5f) && Near(q.TotalWeight, 0.5f) && w2.Count == 0,
            "a shortfall is kept (Smart at half strength, C2), no warning");

        var w3 = new List<string>();
        PersonalityProfile r = Make("Cowardly:0.6,Healer:0.4", CognitionTier.Average, Alignment.None, w3);
        Assert(r != null && Near(r.TotalWeight, 1f) && w3.Count == 0, "a sum of exactly 1: unchanged, no warning");
    }

    private static void TestParsing()
    {
        var warnings = new List<string>();
        PersonalityProfile p = Make(" cowardly = 0.6 ; HEALER:0.4 ", CognitionTier.Average, Alignment.None, warnings);
        Assert(p != null && p.Traits.Count == 2 && p.Traits[0].Trait.Id == "Cowardly" && p.Traits[1].Trait.Id == "Healer"
               && Near(p.Traits[0].Weight, 0.6f) && warnings.Count == 0,
            "';' and '=' separators, spaces and case are accepted; the order is kept", p != null ? p.Describe() : "null");
        Assert(p != null && p.Describe() == "Cowardly .60, Healer .40", "Describe gives the trace form", p != null ? p.Describe() : "null");
        Assert(p != null && p.Source == "cowardly = 0.6 ; HEALER:0.4" && p.Tier == CognitionTier.Average, "the profile keeps its trimmed spec and tier");

        var w2 = new List<string>();
        PersonalityProfile b = Make("Berserker", CognitionTier.Animal, Alignment.None, w2);
        Assert(b != null && Near(b.WeightOf("Berserker"), 1f) && w2.Count == 0, "a bare id weighs 1");
    }

    /// <summary>
    /// Counts the [Personality] warnings that reach Debug.unityLogger and passes every other line on. It wraps the log
    /// handler, not Application.logMessageReceived, because the static runner swaps the handler for its own capture.
    /// </summary>
    private sealed class WarningCounter : ILogHandler
    {
        private readonly ILogHandler _inner;
        public readonly List<string> Seen = new List<string>();

        public WarningCounter(ILogHandler inner) { _inner = inner; }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            string message = args != null && args.Length > 0 ? string.Format(format, args) : format;
            if (logType == LogType.Warning && message != null && message.Contains("[Personality]"))
            {
                Seen.Add(message);
                return;
            }
            _inner.LogFormat(logType, context, format, args);
        }

        public void LogException(System.Exception exception, Object context) => _inner.LogException(exception, context);
    }

    /// <summary>The default sink (step 4's spawn path): Debug.LogWarning once per definition, spec, tier and alignment.</summary>
    private static void TestDefaultWarningOnce()
    {
        ILogHandler original = Debug.unityLogger.logHandler;
        var counter = new WarningCounter(original);
        List<string> seen = counter.Seen;
        PersonalityFactory.ResetWarnings();
        Debug.unityLogger.logHandler = counter;
        try
        {
            PersonalityFactory.Create("Smart:0.5", CognitionTier.Mindless, Alignment.None, "validator_default_sink");
            PersonalityFactory.Create("Smart:0.5", CognitionTier.Mindless, Alignment.None, "validator_default_sink");
            Assert(seen.Count == 1, "with no sink a definition warns once, through Debug.LogWarning", seen.Count + " warnings");
            PersonalityFactory.Create("Smart:0.5", CognitionTier.Low, Alignment.None, "validator_default_sink");
            Assert(seen.Count == 2, "the same definition at another tier (a template variant) warns for itself", seen.Count + " warnings");
            PersonalityFactory.Create("Smart:0.5", CognitionTier.Mindless, Alignment.None, null);
            PersonalityFactory.Create("Cowardly:2,Bogus", CognitionTier.Average, Alignment.None, null);
            Assert(seen.Count == 4, "two different specs with no owner id each warn (no shared empty key)", seen.Count + " warnings");
            PersonalityFactory.ResetWarnings();
            PersonalityFactory.Create("Smart:0.5", CognitionTier.Mindless, Alignment.None, "validator_default_sink");
            Assert(seen.Count == 5, "ResetWarnings forgets the definitions that warned", seen.Count + " warnings");
        }
        finally
        {
            Debug.unityLogger.logHandler = original;
            PersonalityFactory.ResetWarnings();
        }
    }
}
}
#endif
