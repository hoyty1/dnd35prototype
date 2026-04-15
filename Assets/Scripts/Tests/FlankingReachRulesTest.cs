using UnityEngine;

/// <summary>
/// Lightweight runtime checks for reach-aware flanking geometry and threat-distance semantics.
/// Attach to any GameObject or call FlankingReachRulesTest.RunAllTests().
/// </summary>
public class FlankingReachRulesTest : MonoBehaviour
{
    private void Start()
    {
        RunAllTests();
    }

    public static void RunAllTests()
    {
        int passed = 0;
        int failed = 0;

        // Geometric opposite-side checks (independent from adjacency/reach).
        Assert(CombatUtils.IsFlanking(new Vector2Int(1, 1), new Vector2Int(3, 3), new Vector2Int(2, 2)),
            "Adjacent opposite diagonal flanks", ref passed, ref failed);

        Assert(CombatUtils.IsFlanking(new Vector2Int(0, 0), new Vector2Int(4, 4), new Vector2Int(2, 2)),
            "Reach-distance opposite diagonal still counts as opposite sides", ref passed, ref failed);

        Assert(!CombatUtils.IsFlanking(new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2)),
            "Same-side positions do not flank", ref passed, ref failed);

        // Weapon reach semantics for threat ring ranges.
        ItemDatabase.Init();
        AssertThreatBand("longspear", expectedMin: 2, expectedMax: 2, ref passed, ref failed);
        AssertThreatBand("spiked_chain", expectedMin: 1, expectedMax: 2, ref passed, ref failed);
        AssertThreatBand("whip", expectedMin: 2, expectedMax: 3, ref passed, ref failed);
        AssertThreatBand("longsword", expectedMin: 1, expectedMax: 1, ref passed, ref failed);

        Debug.Log($"[FlankReachTest] === RESULTS: {passed} passed, {failed} failed ===");
    }

    private static void AssertThreatBand(string itemId, int expectedMin, int expectedMax, ref int passed, ref int failed)
    {
        ItemData item = ItemDatabase.Get(itemId);
        if (item == null)
        {
            failed++;
            Debug.LogError($"[FlankReachTest] FAIL: missing item {itemId}");
            return;
        }

        int maxReach = Mathf.Max(1, item.ReachSquares > 0 ? item.ReachSquares : item.AttackRange);
        int minReach = item.CanAttackAdjacent ? 1 : (maxReach >= 2 ? 2 : 1);

        bool ok = minReach == expectedMin && maxReach == expectedMax;
        Assert(ok, $"{item.Name} threat band {minReach}-{maxReach} (expected {expectedMin}-{expectedMax})", ref passed, ref failed);
    }

    private static void Assert(bool condition, string label, ref int passed, ref int failed)
    {
        if (condition)
        {
            passed++;
            Debug.Log($"[FlankReachTest] PASS: {label}");
        }
        else
        {
            failed++;
            Debug.LogError($"[FlankReachTest] FAIL: {label}");
        }
    }
}
