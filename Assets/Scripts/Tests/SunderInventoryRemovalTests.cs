using UnityEngine;

/// <summary>
/// Verifies that sunder-destroyed items are removed from inventory completely
/// (equipped slot and any general-slot references).
/// Run with SunderInventoryRemovalTests.RunAll().
/// </summary>
public static class SunderInventoryRemovalTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SUNDER INVENTORY REMOVAL TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();

        TestDestroyedMainHandWeaponRemoved();
        TestDestroyedArmorRemoved();
        TestDestroyedShieldRemoved();

        Debug.Log($"====== Sunder Inventory Removal Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats MakeStats(string name, int bab, int str, int dex)
    {
        return new CharacterStats(name, 3, "Fighter",
            str, dex, 14, 10, 10, 10,
            bab, 0, 0,
            8, 1, 0,
            6, 1, 24,
            "Human");
    }

    private static CharacterController BuildCharacter(string objectName, CharacterStats stats)
    {
        var go = new GameObject(objectName);
        var controller = go.AddComponent<CharacterController>();
        var inventoryComp = go.AddComponent<InventoryComponent>();

        controller.Stats = stats;
        inventoryComp.Init(stats);
        return controller;
    }

    private static void DestroyCharacter(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static ItemData CreateFragileItem(string id)
    {
        ItemData item = ItemDatabase.CloneItem(id);
        item.Hardness = 0;
        item.MaxHitPoints = 1;
        item.CurrentHitPoints = 1;
        item.IsBroken = false;
        item.IsDestroyed = false;
        return item;
    }

    private static bool InventoryContainsReference(Inventory inv, ItemData item)
    {
        if (item == null || inv == null)
            return false;

        foreach (EquipSlot slot in Inventory.AllEquipmentSlots)
        {
            if (ReferenceEquals(inv.GetEquipped(slot), item))
                return true;
        }

        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            if (ReferenceEquals(inv.GeneralSlots[i], item))
                return true;
        }

        return false;
    }

    private static void ValidateDestroyedItemRemoval(EquipSlot targetSlot, string targetItemId, string testName)
    {
        CharacterController attacker = null;
        CharacterController target = null;

        try
        {
            attacker = BuildCharacter("Sunder_Attacker", MakeStats("Attacker", bab: 10, str: 20, dex: 12));
            target = BuildCharacter("Sunder_Target", MakeStats("Target", bab: 1, str: 10, dex: 10));

            var attackerInv = attacker.GetComponent<InventoryComponent>().CharacterInventory;
            var targetInv = target.GetComponent<InventoryComponent>().CharacterInventory;

            ItemData attackerWeapon = ItemDatabase.CloneItem("greatsword");
            attackerInv.DirectEquip(attackerWeapon, EquipSlot.RightHand);

            ItemData destroyedItem = CreateFragileItem(targetItemId);
            targetInv.DirectEquip(destroyedItem, targetSlot);

            // Simulate an accidental duplicate reference in general inventory to verify complete removal.
            targetInv.AddItem(destroyedItem);

            SpecialAttackResult result = attacker.ExecuteSpecialAttack(
                SpecialAttackType.Sunder,
                target,
                sunderTargetSlot: targetSlot,
                sunderAttackBonusOverride: 100,
                sunderAttackerWeaponOverride: attackerWeapon,
                sunderUsedOffHand: false,
                sunderDualWieldPenaltyForLog: 0);

            bool slotCleared = targetInv.GetEquipped(targetSlot) == null;
            bool removedEverywhere = !InventoryContainsReference(targetInv, destroyedItem);

            Assert(result.Success, testName + " - sunder succeeds", result.Log);
            Assert(destroyedItem.IsDestroyed, testName + " - item marked destroyed");
            Assert(slotCleared, testName + " - equip slot cleared");
            Assert(removedEverywhere, testName + " - removed from all inventory locations");
        }
        finally
        {
            DestroyCharacter(attacker);
            DestroyCharacter(target);
        }
    }

    private static void TestDestroyedMainHandWeaponRemoved()
    {
        ValidateDestroyedItemRemoval(EquipSlot.RightHand, "morningstar", "Main-hand weapon destroyed");
    }

    private static void TestDestroyedArmorRemoved()
    {
        ValidateDestroyedItemRemoval(EquipSlot.ArmorRobe, "leather_armor", "Armor destroyed");
    }

    private static void TestDestroyedShieldRemoved()
    {
        ValidateDestroyedItemRemoval(EquipSlot.LeftHand, "shield_light_wooden", "Shield destroyed");
    }
}
