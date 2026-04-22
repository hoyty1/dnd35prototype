using System.Collections.Generic;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Verifies D&D 3.5e two-weapon fighting penalties for all core cases.
/// Run with DualWieldPenaltyTests.RunAll().
/// </summary>
public static class DualWieldPenaltyTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== DUAL WIELD PENALTY TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();

        TestNoFeatNormalOffHand();
        TestNoFeatLightOffHand();
        TestTWFNormalOffHand();
        TestTWFLightOffHand();

        Debug.Log($"====== Dual Wield Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats MakeFighterStats(string name)
    {
        return new CharacterStats(name, 3, "Fighter",
            16, 14, 14, 10, 10, 10,
            3, 0, 0,
            8, 1, 0,
            6, 1, 24,
            "Human");
    }

    private static CharacterController BuildDualWieldCharacter(string name, bool hasTWF, string mainWeaponId, string offWeaponId)
    {
        var go = new GameObject($"TWF_Test_{name}");
        var controller = go.AddComponent<CharacterController>();
        var inventoryComp = go.AddComponent<InventoryComponent>();

        CharacterStats stats = MakeFighterStats(name);
        if (hasTWF)
            stats.AddFeats(new List<string> { "Two-Weapon Fighting" });

        controller.Stats = stats;

        inventoryComp.Init(stats);
        inventoryComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(mainWeaponId), EquipSlot.RightHand);
        inventoryComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(offWeaponId), EquipSlot.LeftHand);
        inventoryComp.CharacterInventory.RecalculateStats();

        return controller;
    }

    private static void DestroyTestCharacter(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static void ValidateCase(
        string caseName,
        bool hasTWF,
        string mainWeaponId,
        string offWeaponId,
        int expectedMain,
        int expectedOff,
        bool expectedLight)
    {
        CharacterController controller = null;
        try
        {
            controller = BuildDualWieldCharacter(caseName, hasTWF, mainWeaponId, offWeaponId);

            ItemData main = controller.GetDualWieldMainWeapon();
            ItemData off = controller.GetDualWieldOffHandWeapon();
            bool light = controller.IsOffHandWeaponLight();
            var (mainPenalty, offPenalty, lightOffHand) = controller.GetDualWieldPenalties();

            string mainType = (main != null && (main.IsLightWeapon || main.WeaponSize == WeaponSizeCategory.Light)) ? "light" : "normal";
            string offType = light ? "light" : "normal";

            Debug.Log("[DualWield] Calculating penalties");
            Debug.Log($"[DualWield] Main hand weapon: {main?.Name ?? "None"} ({mainType})");
            Debug.Log($"[DualWield] Off-hand weapon: {off?.Name ?? "None"} ({offType})");
            Debug.Log($"[DualWield] TWF feat: {hasTWF}");
            Debug.Log($"[DualWield] Light off-hand: {light}");
            Debug.Log($"[DualWield] Penalties: Main {mainPenalty}, Off-hand {offPenalty}");

            Assert(light == expectedLight,
                $"{caseName}: light off-hand detection",
                $"expected {expectedLight}, got {light}");

            Assert(lightOffHand == expectedLight,
                $"{caseName}: GetDualWieldPenalties light flag",
                $"expected {expectedLight}, got {lightOffHand}");

            Assert(mainPenalty == expectedMain && offPenalty == expectedOff,
                $"{caseName}: penalty values",
                $"expected {expectedMain}/{expectedOff}, got {mainPenalty}/{offPenalty}");
        }
        finally
        {
            DestroyTestCharacter(controller);
        }
    }

    // 1) Fighter without TWF feat, longsword + longsword -> -6/-10
    private static void TestNoFeatNormalOffHand()
    {
        ValidateCase(
            caseName: "No feat, normal off-hand",
            hasTWF: false,
            mainWeaponId: "longsword",
            offWeaponId: "longsword",
            expectedMain: -6,
            expectedOff: -10,
            expectedLight: false);
    }

    // 2) Fighter without TWF feat, longsword + dagger -> -4/-8
    private static void TestNoFeatLightOffHand()
    {
        ValidateCase(
            caseName: "No feat, light off-hand",
            hasTWF: false,
            mainWeaponId: "longsword",
            offWeaponId: "dagger",
            expectedMain: -4,
            expectedOff: -8,
            expectedLight: true);
    }

    // 3) Fighter with TWF feat, longsword + longsword -> -4/-4
    private static void TestTWFNormalOffHand()
    {
        ValidateCase(
            caseName: "TWF feat, normal off-hand",
            hasTWF: true,
            mainWeaponId: "longsword",
            offWeaponId: "longsword",
            expectedMain: -4,
            expectedOff: -4,
            expectedLight: false);
    }

    // 4) Fighter with TWF feat, longsword + dagger -> -2/-2
    private static void TestTWFLightOffHand()
    {
        ValidateCase(
            caseName: "TWF feat, light off-hand",
            hasTWF: true,
            mainWeaponId: "longsword",
            offWeaponId: "dagger",
            expectedMain: -2,
            expectedOff: -2,
            expectedLight: true);
    }
}

}
