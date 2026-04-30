using System.Collections.Generic;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// Runtime smoke/regression tests for DMG Chapter 3 random encounter math and generation.
/// Run with RandomEncounterSystemTests.RunAll().
/// </summary>
public static class RandomEncounterSystemTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== RANDOM ENCOUNTER SYSTEM TESTS ======");

        NPCDatabase.Init();

        TestAplCalculationRules();
        TestSameCrElRules();
        TestMixedCrGroupUsesXpConversion();
        TestDifficultyTargetElOffsets();
        TestGenerationAcrossAplRangeAndDifficulties();

        Debug.Log($"====== Random Encounter Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  FAIL: {testName} {detail}");
        }
    }

    private static void TestAplCalculationRules()
    {
        int aplStandard = ChallengeRatingUtils.CalculateAPL(new List<int> { 5, 5, 5, 5 }, 4);
        Assert(aplStandard == 5, "APL baseline (4 PCs, no size adjustment)", $"expected 5, got {aplStandard}");

        int aplSmallParty = ChallengeRatingUtils.CalculateAPL(new List<int> { 5, 5, 5 }, 3);
        Assert(aplSmallParty == 4, "APL small party adjustment (<4 subtracts 1)", $"expected 4, got {aplSmallParty}");

        int aplLargeParty = ChallengeRatingUtils.CalculateAPL(new List<int> { 5, 5, 5, 5, 5, 5, 5 }, 7);
        Assert(aplLargeParty == 6, "APL large party adjustment (>6 adds 1)", $"expected 6, got {aplLargeParty}");
    }

    private static void TestSameCrElRules()
    {
        Assert(Mathf.Abs(ChallengeRatingUtils.GetELForSameCrGroup(3f, 1) - 3f) < 0.001f, "EL same-CR: 1 creature", "CR3 should be EL3");
        Assert(Mathf.Abs(ChallengeRatingUtils.GetELForSameCrGroup(3f, 2) - 5f) < 0.001f, "EL same-CR: 2 creatures", "CR3x2 should be EL5");
        Assert(Mathf.Abs(ChallengeRatingUtils.GetELForSameCrGroup(3f, 4) - 6f) < 0.001f, "EL same-CR: 3-4 creatures", "CR3x4 should be EL6");
        Assert(Mathf.Abs(ChallengeRatingUtils.GetELForSameCrGroup(3f, 8) - 7f) < 0.001f, "EL same-CR: 5-8 creatures", "CR3x8 should be EL7");
        Assert(Mathf.Abs(ChallengeRatingUtils.GetELForSameCrGroup(3f, 16) - 8f) < 0.001f, "EL same-CR: 9-16 creatures", "CR3x16 should be EL8");
    }

    private static void TestMixedCrGroupUsesXpConversion()
    {
        // CR3 + CR1 + CR1 => 900 + 300 + 300 = 1500 XP => equivalent EL 5 (1600 XP threshold).
        float el = ChallengeRatingUtils.CalculateEncounterEL(new List<float> { 3f, 1f, 1f });
        Assert(Mathf.Abs(el - 5f) < 0.001f, "EL mixed CR uses XP conversion", $"expected 5, got {el}");
    }

    private static void TestDifficultyTargetElOffsets()
    {
        int apl = 8;
        Assert(ChallengeRatingUtils.GetTargetELForDifficulty(apl, RandomEncounterDifficulty.Easy) == 7, "Difficulty offset Easy", "expected EL7");
        Assert(ChallengeRatingUtils.GetTargetELForDifficulty(apl, RandomEncounterDifficulty.Average) == 8, "Difficulty offset Average", "expected EL8");
        Assert(ChallengeRatingUtils.GetTargetELForDifficulty(apl, RandomEncounterDifficulty.Challenging) == 9, "Difficulty offset Challenging", "expected EL9");
        Assert(ChallengeRatingUtils.GetTargetELForDifficulty(apl, RandomEncounterDifficulty.Hard) == 10, "Difficulty offset Hard", "expected EL10");
        Assert(ChallengeRatingUtils.GetTargetELForDifficulty(apl, RandomEncounterDifficulty.Epic) == 11, "Difficulty offset Epic", "expected EL11");
    }

    private static void TestGenerationAcrossAplRangeAndDifficulties()
    {
        RandomEncounterSystem generator = new RandomEncounterSystem();

        RandomEncounterDifficulty[] allDifficulties =
        {
            RandomEncounterDifficulty.Easy,
            RandomEncounterDifficulty.Average,
            RandomEncounterDifficulty.Challenging,
            RandomEncounterDifficulty.Hard,
            RandomEncounterDifficulty.Epic
        };

        for (int apl = 1; apl <= 20; apl++)
        {
            for (int d = 0; d < allDifficulties.Length; d++)
            {
                RandomEncounterRequest request = new RandomEncounterRequest
                {
                    PartyLevels = new List<int> { apl, apl, apl, apl },
                    PartySize = 4,
                    Difficulty = allDifficulties[d]
                };

                GeneratedRandomEncounter encounter = generator.Generate(request);
                string caseLabel = $"APL {apl}, Difficulty {allDifficulties[d]}";

                Assert(encounter != null, $"Generator returns encounter ({caseLabel})");
                if (encounter == null)
                    continue;

                Assert(encounter.TotalCreatureCount > 0, $"Encounter has creatures ({caseLabel})");
                Assert(encounter.TotalXP > 0, $"Encounter has XP ({caseLabel})");
                Assert(encounter.NpcIds.Count == encounter.TotalCreatureCount, $"Encounter NPC ID count matches creature count ({caseLabel})");

                bool foundSummoned = false;
                for (int i = 0; i < encounter.NpcIds.Count; i++)
                {
                    NPCDefinition def = NPCDatabase.Get(encounter.NpcIds[i]);
                    if (def == null)
                        continue;

                    if (def.CreatureTags != null)
                    {
                        for (int t = 0; t < def.CreatureTags.Count; t++)
                        {
                            string tag = def.CreatureTags[t];
                            if (string.Equals(tag, "SummonBase", System.StringComparison.OrdinalIgnoreCase)
                                || string.Equals(tag, "SummonAlias", System.StringComparison.OrdinalIgnoreCase)
                                || string.Equals(tag, "Summoned", System.StringComparison.OrdinalIgnoreCase))
                            {
                                foundSummoned = true;
                                break;
                            }
                        }
                    }

                    if (foundSummoned)
                        break;
                }

                Assert(!foundSummoned, $"Encounter excludes summoned creatures ({caseLabel})");
            }
        }
    }
}
}
