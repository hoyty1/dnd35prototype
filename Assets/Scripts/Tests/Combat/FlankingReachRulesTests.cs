using UnityEngine;
using DND35e.Identifiers;
using Tests.Utilities;

namespace Tests.Combat
{
/// <summary>
/// Lightweight runtime checks for reach-aware flanking geometry and threat-distance semantics,
/// plus the PHB p.153 flanking bonus rule (flankers get +2 on melee attacks; the defender
/// takes no AC penalty, CMB-001) and flanking sneak attack eligibility.
/// Attach to any GameObject or call FlankingReachRulesTests.RunAllTests().
/// </summary>
public class FlankingReachRulesTests : MonoBehaviour
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
        AssertThreatBand(ItemIDs.LONGSPEAR, expectedMin: 2, expectedMax: 2, ref passed, ref failed);
        AssertThreatBand(ItemIDs.SPIKED_CHAIN, expectedMin: 1, expectedMax: 2, ref passed, ref failed);
        AssertThreatBand(ItemIDs.WHIP, expectedMin: 2, expectedMax: 3, ref passed, ref failed);
        AssertThreatBand(ItemIDs.LONGSWORD, expectedMin: 1, expectedMax: 1, ref passed, ref failed);

        // PHB p.153 flanking: attacker bonus only, no defender AC penalty (CMB-001).
        TestFlankedConditionHasNoAcPenalty(ref passed, ref failed);
        TestFlankingAttackGetsBonusAndSneakAttack(ref passed, ref failed);
        TestFlankedTagAloneGivesNonFlankerNothing(ref passed, ref failed);

        Debug.Log($"[FlankReachTest] === RESULTS: {passed} passed, {failed} failed ===");
    }

    private static void TestFlankedConditionHasNoAcPenalty(ref int passed, ref int failed)
    {
        ConditionDefinition def = ConditionRules.GetDefinition(CombatConditionType.Flanked);
        Assert(def != null && def.ArmorClassModifier == 0,
            "Flanked condition definition has no AC modifier", ref passed, ref failed);

        CharacterController defender = null;
        try
        {
            defender = TestHelpers.CreateCharacter(name: "FlankedDefender", dex: 14);
            int baseAc = defender.Stats.ArmorClass;
            int baseTouch = defender.Stats.TouchArmorClass;

            defender.ApplyCondition(CombatConditionType.Flanked, -1, "Flanking");

            Assert(defender.HasCondition(CombatConditionType.Flanked) && defender.Stats.IsFlanked,
                "Flanked condition still applies as a display tag", ref passed, ref failed);
            Assert(defender.Stats.ArmorClass == baseAc,
                $"Flanked does not lower AC (base={baseAc}, flanked={defender.Stats.ArmorClass})", ref passed, ref failed);
            Assert(defender.Stats.TouchArmorClass == baseTouch,
                $"Flanked does not lower touch AC (base={baseTouch}, flanked={defender.Stats.TouchArmorClass})", ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(defender != null ? defender.gameObject : null);
        }
    }

    private static void TestFlankingAttackGetsBonusAndSneakAttack(ref int passed, ref int failed)
    {
        CharacterController rogue = null;
        CharacterController defender = null;
        try
        {
            rogue = TestHelpers.CreateRogue("FlankingRogue", level: 5);
            TestHelpers.SetGridPosition(rogue, 0, 0);

            bool sawHit = false;
            bool bonusAlwaysRecorded = true;
            bool sneakOnEveryHit = true;
            Random.InitState(1530);
            for (int i = 0; i < 40; i++)
            {
                // Fresh defender each swing so prior damage (and any unconscious/helpless
                // state, which would itself allow sneak attack) cannot leak into the check.
                TestHelpers.Cleanup(defender != null ? defender.gameObject : null);
                defender = TestHelpers.CreateCharacter(name: "FlankedTarget", dex: 10);
                TestHelpers.SetGridPosition(defender, 1, 0);
                defender.ApplyCondition(CombatConditionType.Flanked, -1, "Flanking");
                CombatResult r = rogue.Attack(defender, true, CombatUtils.FlankingAttackBonus, "Partner");
                if (r == null) { bonusAlwaysRecorded = false; break; }
                if (!r.IsFlanking || r.FlankingBonus != 2) bonusAlwaysRecorded = false;
                if (r.Hit)
                {
                    sawHit = true;
                    if (!r.SneakAttackApplied || !r.SneakAttackByFlanking) sneakOnEveryHit = false;
                }
            }

            Assert(bonusAlwaysRecorded, "Flanking melee attacker gets +2 flanking bonus", ref passed, ref failed);
            Assert(sawHit, "Flanking rogue landed at least one hit in 40 attacks", ref passed, ref failed);
            Assert(sawHit && sneakOnEveryHit, "Flanking rogue hit applies sneak attack (by flanking)", ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(rogue != null ? rogue.gameObject : null, defender != null ? defender.gameObject : null);
        }
    }

    private static void TestFlankedTagAloneGivesNonFlankerNothing(ref int passed, ref int failed)
    {
        // Regression: an attacker who is not itself flanking (e.g. a third ally or an archer)
        // gets no flanking bonus and no flanking sneak attack against a Flanked target.
        CharacterController rogue = null;
        CharacterController defender = null;
        try
        {
            rogue = TestHelpers.CreateRogue("NonFlankingRogue", level: 5);
            TestHelpers.SetGridPosition(rogue, 0, 0);

            bool sawHit = false;
            bool noBonus = true;
            bool noSneak = true;
            Random.InitState(1531);
            for (int i = 0; i < 40; i++)
            {
                // Fresh defender each swing so prior damage (and any unconscious/helpless
                // state, which would itself allow sneak attack) cannot leak into the check.
                TestHelpers.Cleanup(defender != null ? defender.gameObject : null);
                defender = TestHelpers.CreateCharacter(name: "TaggedTarget", dex: 10);
                TestHelpers.SetGridPosition(defender, 1, 0);
                defender.ApplyCondition(CombatConditionType.Flanked, -1, "Flanking");
                CombatResult r = rogue.Attack(defender, false, 0, null);
                if (r == null) { noBonus = false; break; }
                if (r.IsFlanking || r.FlankingBonus != 0) noBonus = false;
                if (r.Hit)
                {
                    sawHit = true;
                    if (r.SneakAttackApplied) noSneak = false;
                }
            }

            Assert(noBonus, "Non-flanking attacker gets no flanking bonus vs Flanked target", ref passed, ref failed);
            Assert(sawHit && noSneak, "Non-flanking rogue gets no sneak attack from the Flanked tag", ref passed, ref failed);
        }
        finally
        {
            TestHelpers.Cleanup(rogue != null ? rogue.gameObject : null, defender != null ? defender.gameObject : null);
        }
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

}
