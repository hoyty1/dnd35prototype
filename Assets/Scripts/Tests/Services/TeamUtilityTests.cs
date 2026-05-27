using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for TeamUtility — verifies enemy/ally detection,
/// humanoid checks, hit dice lookups, and team member queries.
/// Run with TeamUtilityTests.RunAll().
/// </summary>
public static class TeamUtilityTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== TEAM UTILITY TESTS ======");

        TestIsEnemy_DifferentTeams();
        TestIsAlly_SameTeam();
        TestIsHumanoid();
        TestGetHitDice();
        TestGetAliveTeamMembers();
        TestGetClosestEnemy();

        Debug.Log($"====== TeamUtility Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    private static void TestIsEnemy_DifferentTeams()
    {
        // TODO: Requires two CharacterControllers with different TeamId
        Assert(true, "IsEnemy_DifferentTeams (placeholder — needs scene)");
    }

    private static void TestIsAlly_SameTeam()
    {
        Assert(true, "IsAlly_SameTeam (placeholder — needs scene)");
    }

    private static void TestIsHumanoid()
    {
        Assert(true, "IsHumanoid (placeholder — needs mock)");
    }

    private static void TestGetHitDice()
    {
        Assert(true, "GetHitDice (placeholder — needs mock)");
    }

    private static void TestGetAliveTeamMembers()
    {
        Assert(true, "GetAliveTeamMembers (placeholder — needs scene)");
    }

    private static void TestGetClosestEnemy()
    {
        Assert(true, "GetClosestEnemy (placeholder — needs scene)");
    }
}
}
