using System.Collections.Generic;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Verifies D&D 3.5e Improved Shield Bash behavior:
/// - Without feat: shield AC bonus is lost after shield bash until next turn.
/// - With feat: shield AC bonus is retained.
/// Run with ImprovedShieldBashTests.RunAll().
/// </summary>
public static class ImprovedShieldBashTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        RaceDatabase.Init();
        ClassRegistry.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Debug.Log("====== IMPROVED SHIELD BASH TESTS ======");

        TestShieldBashWithoutFeatLosesShieldAcUntilNextTurn();
        TestShieldBashWithFeatKeepsShieldAc();
        TestOffHandShieldBashSingleAttackWithoutFeatLosesShieldAc();
        TestOffHandShieldBashSingleAttackWithFeatKeepsShieldAc();

        Debug.Log($"====== Improved Shield Bash Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController CreateShieldBashFighter(string name, bool hasImprovedShieldBash)
    {
        var go = new GameObject($"ShieldBash_{name}");
        var controller = go.AddComponent<CharacterController>();
        var inventoryComp = go.AddComponent<InventoryComponent>();

        var stats = new CharacterStats(name, 5, "Fighter",
            16, 14, 14, 10, 10, 10,
            5, 0, 0,
            8, 1, 0,
            6, 1, 48,
            "Human");

        stats.InitFeats();
        if (hasImprovedShieldBash)
            stats.AddFeats(new List<string> { "Improved Shield Bash" });

        controller.Init(stats, Vector2Int.zero, null, null);
        inventoryComp.Init(stats);
        inventoryComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("longsword"), EquipSlot.RightHand);
        inventoryComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("shield_heavy_steel"), EquipSlot.LeftHand);
        inventoryComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chain_shirt"), EquipSlot.Armor);
        inventoryComp.CharacterInventory.RecalculateStats();

        return controller;
    }

    private static CharacterController CreateDurableTarget(string name)
    {
        var go = new GameObject($"ShieldBashTarget_{name}");
        var controller = go.AddComponent<CharacterController>();
        var inventoryComp = go.AddComponent<InventoryComponent>();

        var stats = new CharacterStats(name, 8, "Fighter",
            18, 10, 18, 10, 10, 10,
            8, 0, 0,
            8, 1, 0,
            6, 1, 140,
            "Human");

        controller.Init(stats, Vector2Int.right, null, null);
        inventoryComp.Init(stats);
        inventoryComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chainmail"), EquipSlot.Armor);
        inventoryComp.CharacterInventory.RecalculateStats();

        return controller;
    }

    private static void Cleanup(params CharacterController[] controllers)
    {
        if (controllers == null)
            return;

        foreach (CharacterController controller in controllers)
        {
            if (controller != null)
                Object.DestroyImmediate(controller.gameObject);
        }
    }

    private static void TestShieldBashWithoutFeatLosesShieldAcUntilNextTurn()
    {
        CharacterController attacker = null;
        CharacterController target = null;

        try
        {
            attacker = CreateShieldBashFighter("Basher_NoFeat", hasImprovedShieldBash: false);
            target = CreateDurableTarget("Dummy_NoFeat");

            Assert(attacker.CanDualWield(), "Without feat: fighter can dual-wield with weapon + shield");
            Assert(attacker.Stats.ShieldBonus == 2, "Without feat: pre-attack shield bonus is +2", $"expected 2, got {attacker.Stats.ShieldBonus}");

            FullAttackResult result = attacker.DualWieldAttack(target, isFlanking: false, flankingBonus: 0, flankingPartnerName: null);

            Assert(result != null && result.Attacks.Count >= 2,
                "Without feat: dual-wield attack performs off-hand shield bash",
                $"attackCount={(result == null ? -1 : result.Attacks.Count)}");

            Assert(attacker.Stats.ShieldBonus == 0,
                "Without feat: shield bonus suppressed to 0 after shield bash",
                $"expected 0, got {attacker.Stats.ShieldBonus}");

            Assert(attacker.HasCondition(CombatConditionType.LostShieldAC),
                "Without feat: Lost Shield AC condition applied after shield bash");

            attacker.StartNewTurn();

            Assert(attacker.Stats.ShieldBonus == 2,
                "Without feat: shield bonus restored at start of next turn",
                $"expected 2, got {attacker.Stats.ShieldBonus}");

            Assert(!attacker.HasCondition(CombatConditionType.LostShieldAC),
                "Without feat: Lost Shield AC condition removed at start of next turn");
        }
        finally
        {
            Cleanup(attacker, target);
        }
    }

    private static void TestShieldBashWithFeatKeepsShieldAc()
    {
        CharacterController attacker = null;
        CharacterController target = null;

        try
        {
            attacker = CreateShieldBashFighter("Shielder_WithFeat", hasImprovedShieldBash: true);
            target = CreateDurableTarget("Dummy_WithFeat");

            Assert(attacker.Stats.ShieldBonus == 2,
                "With feat: pre-attack shield bonus is +2",
                $"expected 2, got {attacker.Stats.ShieldBonus}");

            FullAttackResult result = attacker.DualWieldAttack(target, isFlanking: false, flankingBonus: 0, flankingPartnerName: null);

            Assert(result != null && result.Attacks.Count >= 2,
                "With feat: dual-wield attack performs off-hand shield bash",
                $"attackCount={(result == null ? -1 : result.Attacks.Count)}");

            Assert(attacker.Stats.ShieldBonus == 2,
                "With feat: shield bonus remains +2 after shield bash",
                $"expected 2, got {attacker.Stats.ShieldBonus}");

            Assert(!attacker.HasCondition(CombatConditionType.LostShieldAC),
                "With feat: Lost Shield AC condition is not applied");
        }
        finally
        {
            Cleanup(attacker, target);
        }
    }

    private static void TestOffHandShieldBashSingleAttackWithoutFeatLosesShieldAc()
    {
        CharacterController attacker = null;
        CharacterController target = null;

        try
        {
            attacker = CreateShieldBashFighter("SingleOffHand_NoFeat", hasImprovedShieldBash: false);
            target = CreateDurableTarget("SingleOffHandTarget_NoFeat");

            var inventory = attacker.GetComponent<InventoryComponent>()?.CharacterInventory;
            ItemData offHandShield = inventory?.LeftHandSlot;

            Assert(offHandShield != null && offHandShield.IsShield,
                "Single off-hand without feat: off-hand shield is equipped");

            CombatResult result = attacker.Attack(
                target,
                isFlanking: false,
                flankingBonus: 0,
                flankingPartnerName: null,
                rangeInfo: null,
                baseAttackBonusOverride: null,
                attackWeaponOverride: offHandShield,
                additionalAttackModifier: 0,
                isOffHandAttack: true);

            Assert(result != null,
                "Single off-hand without feat: shield bash attack resolves");

            Assert(attacker.Stats.ShieldBonus == 0,
                "Single off-hand without feat: shield bonus suppressed to 0",
                $"expected 0, got {attacker.Stats.ShieldBonus}");

            Assert(attacker.HasCondition(CombatConditionType.LostShieldAC),
                "Single off-hand without feat: Lost Shield AC condition applied");
        }
        finally
        {
            Cleanup(attacker, target);
        }
    }

    private static void TestOffHandShieldBashSingleAttackWithFeatKeepsShieldAc()
    {
        CharacterController attacker = null;
        CharacterController target = null;

        try
        {
            attacker = CreateShieldBashFighter("SingleOffHand_WithFeat", hasImprovedShieldBash: true);
            target = CreateDurableTarget("SingleOffHandTarget_WithFeat");

            var inventory = attacker.GetComponent<InventoryComponent>()?.CharacterInventory;
            ItemData offHandShield = inventory?.LeftHandSlot;

            Assert(offHandShield != null && offHandShield.IsShield,
                "Single off-hand with feat: off-hand shield is equipped");

            CombatResult result = attacker.Attack(
                target,
                isFlanking: false,
                flankingBonus: 0,
                flankingPartnerName: null,
                rangeInfo: null,
                baseAttackBonusOverride: null,
                attackWeaponOverride: offHandShield,
                additionalAttackModifier: 0,
                isOffHandAttack: true);

            Assert(result != null,
                "Single off-hand with feat: shield bash attack resolves");

            Assert(attacker.Stats.ShieldBonus == 2,
                "Single off-hand with feat: shield bonus remains +2",
                $"expected 2, got {attacker.Stats.ShieldBonus}");

            Assert(!attacker.HasCondition(CombatConditionType.LostShieldAC),
                "Single off-hand with feat: Lost Shield AC condition not applied");
        }
        finally
        {
            Cleanup(attacker, target);
        }
    }
}
}
