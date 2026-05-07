using System.Collections.Generic;
using UnityEngine;

namespace Tests.Character
{
    /// <summary>
    /// Regression checks for deity alignment compatibility.
    /// D&D 3.5e: any character can only worship a deity within one step of their alignment.
    /// Run manually via DeityAlignmentRestrictionTests.RunAll().
    /// </summary>
    public static class DeityAlignmentRestrictionTests
    {
        private static int _passed;
        private static int _failed;

        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            Debug.Log("====== DEITY ALIGNMENT RESTRICTION TESTS ======");

            DeityDatabase.Init();

            TestLawfulGoodCompatibleDeities();
            TestChaoticEvilCompatibleDeities();
            TestTrueNeutralCanChooseAnyDeity();

            Debug.Log($"====== Deity Alignment Restriction Results: {_passed} passed, {_failed} failed ======");
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

        private static bool ContainsDeity(List<DeityData> list, string deityId)
        {
            if (list == null) return false;
            foreach (DeityData deity in list)
            {
                if (deity != null && deity.DeityId == deityId)
                    return true;
            }

            return false;
        }

        private static void TestLawfulGoodCompatibleDeities()
        {
            List<DeityData> deities = DeityDatabase.GetCompatibleDeities(Alignment.LawfulGood);

            Assert(ContainsDeity(deities, "heironeous"), "LG can choose LG deity (Heironeous)");
            Assert(ContainsDeity(deities, "st_cuthbert"), "LG can choose LN deity (St. Cuthbert)");
            Assert(ContainsDeity(deities, "pelor"), "LG can choose NG deity (Pelor)");
            Assert(!ContainsDeity(deities, "kord"), "LG cannot choose CG deity (Kord)");
            Assert(!ContainsDeity(deities, "hextor"), "LG cannot choose LE deity (Hextor)");
        }

        private static void TestChaoticEvilCompatibleDeities()
        {
            List<DeityData> deities = DeityDatabase.GetCompatibleDeities(Alignment.ChaoticEvil);

            Assert(ContainsDeity(deities, "erythnul"), "CE can choose CE deity (Erythnul)");
            Assert(ContainsDeity(deities, "olidammara"), "CE can choose CN deity (Olidammara)");
            Assert(ContainsDeity(deities, "nerull"), "CE can choose NE deity (Nerull)");
            Assert(!ContainsDeity(deities, "hextor"), "CE cannot choose LE deity (Hextor)");
            Assert(!ContainsDeity(deities, "moradin"), "CE cannot choose LG deity (Moradin)");
        }

        private static void TestTrueNeutralCanChooseAnyDeity()
        {
            List<DeityData> compatible = DeityDatabase.GetCompatibleDeities(Alignment.TrueNeutral);
            List<DeityData> all = DeityDatabase.GetAllDeities();

            Assert(compatible.Count == all.Count,
                "True Neutral can choose every deity (one-step coverage includes all alignments)",
                $"expected {all.Count}, got {compatible.Count}");
        }
    }
}
