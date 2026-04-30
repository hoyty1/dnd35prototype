using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tests.Inventory
{
/// <summary>
/// Smoke tests for post-combat loot gathering + transfer.
/// Run manually by calling PostCombatLootCollectionTests.RunAll().
/// </summary>
public static class PostCombatLootCollectionTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== POST-COMBAT LOOT COLLECTION TESTS ======");

        ItemDatabase.Init();
        TestGatherLootFromDeadEnemyInventoryAndEquipment();
        TestGatherLootFromGroundItems();
        TestDestroyedItemsExcluded();
        TestLootTransferMovesItemToStash();

        Debug.Log($"====== Post-Combat Loot Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  FAIL: {testName} {details}");
        }
    }

    private static CharacterStats MakeEnemyStats(string name)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: 2,
            characterClass: "Warrior",
            str: 13,
            dex: 12,
            con: 12,
            wis: 10,
            intelligence: 10,
            cha: 8,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 30,
            atkRange: 1,
            baseHitDieHP: 14,
            raceName: "Humanoid");

        stats.CurrentHP = -10; // Dead per D&D 3.5 threshold.
        return stats;
    }

    private static GameManager BuildTestGameManager(out CharacterController enemy, out SquareGrid grid)
    {
        GameObject gmGO = new GameObject("LootTest_GameManager");
        GameManager gm = gmGO.AddComponent<GameManager>();

        GameObject gridGO = new GameObject("LootTest_Grid");
        grid = gridGO.AddComponent<SquareGrid>();
        grid.Width = 4;
        grid.Height = 4;
        grid.GenerateGrid();

        gm.Grid = grid;
        gm.PartyStash = new PartyStash();
        gm.PartyStash.Unlock();
        gm.PCs = new List<CharacterController>();
        gm.NPCs = new List<CharacterController>();

        GameObject enemyGO = new GameObject("LootTest_Enemy");
        enemy = enemyGO.AddComponent<CharacterController>();
        enemy.ConfigureTeamControl(CharacterTeam.Enemy, controllable: false);
        enemy.Stats = MakeEnemyStats("Loot Goblin");

        InventoryComponent invComponent = enemyGO.AddComponent<InventoryComponent>();
        invComponent.Init(enemy.Stats);

        gm.NPCs.Add(enemy);
        return gm;
    }

    private static List<LootCollectionUI.LootStackEntry> InvokeGather(GameManager gm)
    {
        MethodInfo gather = typeof(GameManager).GetMethod("GatherPostCombatLootEntries", BindingFlags.Instance | BindingFlags.NonPublic);
        return gather != null
            ? (List<LootCollectionUI.LootStackEntry>)gather.Invoke(gm, null)
            : new List<LootCollectionUI.LootStackEntry>();
    }

    private static bool InvokeTransfer(GameManager gm, LootCollectionUI.LootItemInstance instance)
    {
        MethodInfo transfer = typeof(GameManager).GetMethod("TryTransferLootItemInstanceToStash", BindingFlags.Instance | BindingFlags.NonPublic);
        if (transfer == null)
            return false;

        bool result = false;
        Action<bool> callback = success => result = success;
        transfer.Invoke(gm, new object[] { instance, callback });
        return result;
    }

    private static void Cleanup(params UnityEngine.Object[] objects)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                UnityEngine.Object.DestroyImmediate(objects[i]);
        }
    }

    private static void TestGatherLootFromDeadEnemyInventoryAndEquipment()
    {
        GameManager gm = BuildTestGameManager(out CharacterController enemy, out SquareGrid grid);
        Inventory inv = enemy.GetComponent<InventoryComponent>().CharacterInventory;

        inv.DirectEquip(ItemDatabase.CloneItem("longsword"), EquipSlot.RightHand);
        inv.AddItem(ItemDatabase.CloneItem("potion_cure_light_wounds"));

        List<LootCollectionUI.LootStackEntry> entries = InvokeGather(gm);
        int totalItems = 0;
        for (int i = 0; i < entries.Count; i++)
            totalItems += entries[i].RemainingQuantity;

        Assert(totalItems >= 2, "Gather includes equipped + inventory items", $"(got {totalItems})");

        Cleanup(enemy.gameObject, grid.gameObject, gm.gameObject);
    }

    private static void TestGatherLootFromGroundItems()
    {
        GameManager gm = BuildTestGameManager(out CharacterController enemy, out SquareGrid grid);
        SquareCell cell = grid.GetCell(1, 1);
        cell.AddGroundItem(ItemDatabase.CloneItem("dagger"));

        List<LootCollectionUI.LootStackEntry> entries = InvokeGather(gm);
        bool foundGround = false;
        for (int i = 0; i < entries.Count; i++)
        {
            LootCollectionUI.LootStackEntry entry = entries[i];
            if (entry != null && entry.SourceLabel != null && entry.SourceLabel.Contains("Items on Ground"))
            {
                foundGround = true;
                break;
            }
        }

        Assert(foundGround, "Gather includes ground items");
        Cleanup(enemy.gameObject, grid.gameObject, gm.gameObject);
    }

    private static void TestDestroyedItemsExcluded()
    {
        GameManager gm = BuildTestGameManager(out CharacterController enemy, out SquareGrid grid);
        Inventory inv = enemy.GetComponent<InventoryComponent>().CharacterInventory;

        ItemData sword = ItemDatabase.CloneItem("longsword");
        sword.IsDestroyed = true;
        inv.AddItem(sword);

        List<LootCollectionUI.LootStackEntry> entries = InvokeGather(gm);
        bool hasDestroyed = false;
        for (int i = 0; i < entries.Count; i++)
        {
            LootCollectionUI.LootStackEntry entry = entries[i];
            if (entry != null && ReferenceEquals(entry.Prototype, sword))
            {
                hasDestroyed = true;
                break;
            }
        }

        Assert(!hasDestroyed, "Destroyed items are excluded from loot list");
        Cleanup(enemy.gameObject, grid.gameObject, gm.gameObject);
    }

    private static void TestLootTransferMovesItemToStash()
    {
        GameManager gm = BuildTestGameManager(out CharacterController enemy, out SquareGrid grid);
        Inventory inv = enemy.GetComponent<InventoryComponent>().CharacterInventory;

        ItemData item = ItemDatabase.CloneItem("dagger");
        inv.AddItem(item);

        LootCollectionUI.LootItemInstance instance = new LootCollectionUI.LootItemInstance
        {
            Item = item,
            SourceType = LootCollectionUI.LootSourceType.Enemy,
            SourceEnemy = enemy,
            SourceLabel = "From Loot Goblin"
        };

        bool success = InvokeTransfer(gm, instance);

        bool inStash = false;
        IReadOnlyList<ItemData> stashItems = gm.PartyStash.GetItems();
        for (int i = 0; i < stashItems.Count; i++)
        {
            if (ReferenceEquals(stashItems[i], item))
            {
                inStash = true;
                break;
            }
        }

        bool removedFromEnemy = true;
        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            if (ReferenceEquals(inv.GeneralSlots[i], item))
            {
                removedFromEnemy = false;
                break;
            }
        }

        Assert(success, "Loot transfer callback returns success");
        Assert(inStash, "Loot transfer adds item to stash");
        Assert(removedFromEnemy, "Loot transfer removes item from enemy source");

        Cleanup(enemy.gameObject, grid.gameObject, gm.gameObject);
    }
}
}
