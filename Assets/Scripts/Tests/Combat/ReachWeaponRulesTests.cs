using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Lightweight runtime checks for melee reach metadata and D&D 3.5 reach-ring semantics.
/// Attach to a GameObject or call ReachWeaponRulesTests.RunAllTests().
/// </summary>
public class ReachWeaponRulesTests : MonoBehaviour
{
    private void Start()
    {
        RunAllTests();
    }

    public static void RunAllTests()
    {
        ItemDatabase.Init();

        int passed = 0;
        int failed = 0;

        AssertReach("longspear", expectedReach: 2, expectedAdjacent: false, expectedReachWeapon: true, ref passed, ref failed);
        AssertReach("glaive", expectedReach: 2, expectedAdjacent: false, expectedReachWeapon: true, ref passed, ref failed);
        AssertReach("halberd", expectedReach: 2, expectedAdjacent: false, expectedReachWeapon: true, ref passed, ref failed);
        AssertReach("spiked_chain", expectedReach: 2, expectedAdjacent: true, expectedReachWeapon: true, ref passed, ref failed);
        AssertReach("whip", expectedReach: 3, expectedAdjacent: false, expectedReachWeapon: true, ref passed, ref failed);
        AssertReach("longsword", expectedReach: 1, expectedAdjacent: true, expectedReachWeapon: false, ref passed, ref failed);

        ItemData whip = ItemDatabase.Get("whip");
        if (whip != null && whip.DealsNonlethalDamage && whip.WhipLikeArmorRestriction)
        {
            passed++;
            Debug.Log("[ReachTest] PASS: whip nonlethal + armor restriction flags");
        }
        else
        {
            failed++;
            Debug.LogError("[ReachTest] FAIL: whip nonlethal/armor restriction flags missing");
        }

        if (whip != null && whip.GetReachDescription() == "15 ft")
        {
            passed++;
            Debug.Log("[ReachTest] PASS: whip reach description is 15 ft");
        }
        else
        {
            failed++;
            Debug.LogError($"[ReachTest] FAIL: whip reach description mismatch: {(whip == null ? "<null>" : whip.GetReachDescription())}");
        }

        Debug.Log($"[ReachTest] === RESULTS: {passed} passed, {failed} failed ===");
    }

    private static void AssertReach(string itemId, int expectedReach, bool expectedAdjacent, bool expectedReachWeapon, ref int passed, ref int failed)
    {
        ItemData item = ItemDatabase.Get(itemId);
        if (item == null)
        {
            failed++;
            Debug.LogError($"[ReachTest] FAIL: {itemId} not found");
            return;
        }

        bool ok = item.ReachSquares == expectedReach
            && item.CanAttackAdjacent == expectedAdjacent
            && item.IsReachWeapon == expectedReachWeapon
            && item.AttackRange == expectedReach;

        if (ok)
        {
            passed++;
            Debug.Log($"[ReachTest] PASS: {item.Name} reach={item.ReachSquares}, adjacent={item.CanAttackAdjacent}, isReach={item.IsReachWeapon}");
        }
        else
        {
            failed++;
            Debug.LogError($"[ReachTest] FAIL: {item.Name} got reach={item.ReachSquares}, adjacent={item.CanAttackAdjacent}, isReach={item.IsReachWeapon}, atkRange={item.AttackRange} | expected reach={expectedReach}, adjacent={expectedAdjacent}, isReach={expectedReachWeapon}");
        }
    }
}

}
