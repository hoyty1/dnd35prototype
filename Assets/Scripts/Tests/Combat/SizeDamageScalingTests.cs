using System.Collections.Generic;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Verifies D&D 3.5 size-based damage scaling for manufactured, natural, and resized attacks.
/// Run with SizeDamageScalingTests.RunAll().
/// </summary>
public static class SizeDamageScalingTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SIZE DAMAGE SCALING TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();

        TestWeaponProgressionLongsword();
        TestWeaponProgressionGreatsword();
        TestNaturalAttackScalingFromLargeToMedium();
        TestSizeChangeRecalculatesEquippedWeaponDamage();

        Debug.Log($"====== Size Damage Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController BuildEquippedCharacter(string name, string weaponId)
    {
        var go = new GameObject($"Size_Test_{name}");
        var controller = go.AddComponent<CharacterController>();
        var inventoryComp = go.AddComponent<InventoryComponent>();

        CharacterStats stats = MakeFighterStats(name);
        controller.Stats = stats;

        inventoryComp.Init(stats);
        inventoryComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(weaponId), EquipSlot.RightHand);
        inventoryComp.CharacterInventory.RecalculateStats();

        return controller;
    }

    private static void DestroyTestCharacter(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static void AssertScaled(ItemData weapon, SizeCategory targetSize, int expectedCount, int expectedDice, string label)
    {
        weapon.GetScaledDamageDice(targetSize, out int scaledCount, out int scaledDice);
        Assert(scaledCount == expectedCount && scaledDice == expectedDice,
            label,
            $"expected {expectedCount}d{expectedDice}, got {scaledCount}d{scaledDice}");
    }

    private static void TestWeaponProgressionLongsword()
    {
        ItemData longsword = ItemDatabase.CloneItem("longsword");
        AssertScaled(longsword, SizeCategory.Small, 1, 6, "Longsword Medium->Small = 1d6");
        AssertScaled(longsword, SizeCategory.Medium, 1, 8, "Longsword Medium->Medium = 1d8");
        AssertScaled(longsword, SizeCategory.Large, 2, 6, "Longsword Medium->Large = 2d6");
    }

    private static void TestWeaponProgressionGreatsword()
    {
        ItemData greatsword = ItemDatabase.CloneItem("greatsword");
        AssertScaled(greatsword, SizeCategory.Small, 1, 10, "Greatsword Medium->Small = 1d10");
        AssertScaled(greatsword, SizeCategory.Medium, 2, 6, "Greatsword Medium->Medium = 2d6");
        AssertScaled(greatsword, SizeCategory.Large, 3, 6, "Greatsword Medium->Large = 3d6");
    }

    private static void TestNaturalAttackScalingFromLargeToMedium()
    {
        CharacterStats stats = MakeFighterStats("NaturalScaler");
        stats.SetBaseSizeCategory(SizeCategory.Large);
        stats.SetNaturalAttacks(new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Bite",
                DamageCount = 1,
                DamageDice = 8,
                IsPrimary = true,
                Count = 1
            }
        });

        NaturalAttackDefinition bite = stats.GetPrimaryNaturalAttack();
        stats.GetScaledNaturalAttackDamage(bite, out int largeCount, out int largeDice);
        Assert(largeCount == 1 && largeDice == 8,
            "Natural bite at base Large remains 1d8",
            $"expected 1d8, got {largeCount}d{largeDice}");

        stats.TryShiftCurrentSize(-1); // Large -> Medium
        bite = stats.GetPrimaryNaturalAttack();
        stats.GetScaledNaturalAttackDamage(bite, out int mediumCount, out int mediumDice);
        Assert(mediumCount == 1 && mediumDice == 6,
            "Natural bite Large->Medium becomes 1d6",
            $"expected 1d6, got {mediumCount}d{mediumDice}");
    }

    private static void TestSizeChangeRecalculatesEquippedWeaponDamage()
    {
        CharacterController controller = null;
        try
        {
            controller = BuildEquippedCharacter("ResizeLongsword", "longsword");

            Assert(controller.Stats.BaseDamageCount == 1 && controller.Stats.BaseDamageDice == 8,
                "Initial equipped longsword damage is 1d8",
                $"got {controller.Stats.BaseDamageCount}d{controller.Stats.BaseDamageDice}");

            bool enlarged = controller.ChangeSize(+1);
            Assert(enlarged, "Enlarge size change succeeded");
            Assert(controller.Stats.BaseDamageCount == 2 && controller.Stats.BaseDamageDice == 6,
                "After enlarge, equipped longsword damage is 2d6",
                $"got {controller.Stats.BaseDamageCount}d{controller.Stats.BaseDamageDice}");

            bool reduced = controller.ChangeSize(-2);
            Assert(reduced, "Reduce size change succeeded");
            Assert(controller.Stats.BaseDamageCount == 1 && controller.Stats.BaseDamageDice == 6,
                "After enlarge then reduce to Small, equipped longsword damage is 1d6",
                $"got {controller.Stats.BaseDamageCount}d{controller.Stats.BaseDamageDice}");
        }
        finally
        {
            DestroyTestCharacter(controller);
        }
    }
}
}
