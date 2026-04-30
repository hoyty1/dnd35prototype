using System;
using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
    /// <summary>Modal post-combat loot collection UI.</summary>
    public LootCollectionUI LootCollectionUI;

    /// <summary>Whether the game is waiting for the player to finish loot collection.</summary>
    public bool WaitingForLootCollection { get; private set; }

    private void BeginPostCombatLootCollection()
    {
        EnsurePartyStashInitialized();
        PartyStash?.Unlock();

        EnsureLootCollectionUIInitialized();
        if (LootCollectionUI == null)
        {
            WaitingForLootCollection = false;
            CombatUI?.ShowCombatLog("⚠ Loot window unavailable. Stash unlocked.");
            return;
        }

        List<LootCollectionUI.LootStackEntry> lootEntries = GatherPostCombatLootEntries();
        if (lootEntries.Count == 0)
        {
            WaitingForLootCollection = false;
            CombatUI?.ShowCombatLog("📭 No loot found after combat. Party stash unlocked.");
            return;
        }

        WaitingForLootCollection = true;
        CombatUI?.ShowCombatLog($"💰 Loot available: {CountTotalItems(lootEntries)} item(s). Collect loot before continuing.");

        LootCollectionUI.Open(
            lootEntries,
            onLootSingle: TryTransferLootItemInstanceToStash,
            onClosed: lootedCount =>
            {
                WaitingForLootCollection = false;
                PartyStash?.Unlock();

                if (lootedCount > 0)
                    CombatUI?.ShowCombatLog($"📦 {lootedCount} item(s) looted to party stash. Stash unlocked.");
                else
                    CombatUI?.ShowCombatLog("📦 Loot window closed. Party stash unlocked.");
            });
    }

    private void EnsureLootCollectionUIInitialized()
    {
        if (LootCollectionUI != null)
            return;

        LootCollectionUI = FindObjectOfType<LootCollectionUI>();
        if (LootCollectionUI == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
                LootCollectionUI = canvas.gameObject.AddComponent<LootCollectionUI>();
        }
    }

    private List<LootCollectionUI.LootStackEntry> GatherPostCombatLootEntries()
    {
        Dictionary<string, LootCollectionUI.LootStackEntry> stackMap = new Dictionary<string, LootCollectionUI.LootStackEntry>();
        List<LootCollectionUI.LootStackEntry> ordered = new List<LootCollectionUI.LootStackEntry>();

        GatherLootFromDefeatedEnemies(stackMap, ordered);
        GatherLootFromGround(stackMap, ordered);

        ordered.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int sourceCmp = string.Compare(a.SourceLabel, b.SourceLabel, StringComparison.OrdinalIgnoreCase);
            if (sourceCmp != 0)
                return sourceCmp;

            string aName = a.Prototype != null ? a.Prototype.Name : string.Empty;
            string bName = b.Prototype != null ? b.Prototype.Name : string.Empty;
            return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
        });

        return ordered;
    }

    private void GatherLootFromDefeatedEnemies(
        Dictionary<string, LootCollectionUI.LootStackEntry> stackMap,
        List<LootCollectionUI.LootStackEntry> ordered)
    {
        if (NPCs == null)
            return;

        for (int i = 0; i < NPCs.Count; i++)
        {
            CharacterController enemy = NPCs[i];
            if (enemy == null || enemy.Stats == null || !enemy.Stats.IsDead)
                continue;

            Inventory inventory = GetCharacterInventory(enemy);
            if (inventory == null)
                continue;

            string sourceLabel = $"From {enemy.Stats.CharacterName}";
            string sourceGroupKey = $"enemy:{enemy.GetInstanceID()}";

            foreach (EquipSlot slot in Inventory.AllEquipmentSlots)
            {
                ItemData equipped = inventory.GetEquipped(slot);
                if (!IsValidLootItem(equipped))
                    continue;

                AddLootInstance(stackMap, ordered, sourceGroupKey, sourceLabel, equipped, LootCollectionUI.LootSourceType.Enemy, enemy, Vector2Int.zero);
            }

            if (inventory.GeneralSlots == null)
                continue;

            for (int slotIndex = 0; slotIndex < inventory.GeneralSlots.Length; slotIndex++)
            {
                ItemData item = inventory.GeneralSlots[slotIndex];
                if (!IsValidLootItem(item))
                    continue;

                AddLootInstance(stackMap, ordered, sourceGroupKey, sourceLabel, item, LootCollectionUI.LootSourceType.Enemy, enemy, Vector2Int.zero);
            }
        }
    }

    private void GatherLootFromGround(
        Dictionary<string, LootCollectionUI.LootStackEntry> stackMap,
        List<LootCollectionUI.LootStackEntry> ordered)
    {
        if (Grid == null || Grid.Cells == null)
            return;

        foreach (KeyValuePair<Vector2Int, SquareCell> kvp in Grid.Cells)
        {
            SquareCell cell = kvp.Value;
            if (cell == null || cell.GroundItems == null || cell.GroundItems.Count == 0)
                continue;

            string sourceLabel = $"Items on Ground ({kvp.Key.x},{kvp.Key.y})";
            string sourceGroupKey = $"ground:{kvp.Key.x}:{kvp.Key.y}";

            for (int i = 0; i < cell.GroundItems.Count; i++)
            {
                ItemData item = cell.GroundItems[i];
                if (!IsValidLootItem(item))
                    continue;

                AddLootInstance(stackMap, ordered, sourceGroupKey, sourceLabel, item, LootCollectionUI.LootSourceType.Ground, null, kvp.Key);
            }
        }
    }

    private void AddLootInstance(
        Dictionary<string, LootCollectionUI.LootStackEntry> stackMap,
        List<LootCollectionUI.LootStackEntry> ordered,
        string sourceGroupKey,
        string sourceLabel,
        ItemData item,
        LootCollectionUI.LootSourceType sourceType,
        CharacterController sourceEnemy,
        Vector2Int groundPos)
    {
        if (!IsValidLootItem(item))
            return;

        string itemIdentity = !string.IsNullOrWhiteSpace(item.Id) ? item.Id : item.Name;
        string stackKey = $"{sourceGroupKey}|{itemIdentity}";

        if (!stackMap.TryGetValue(stackKey, out LootCollectionUI.LootStackEntry stack))
        {
            stack = new LootCollectionUI.LootStackEntry
            {
                StackKey = stackKey,
                SourceGroupKey = sourceGroupKey,
                SourceLabel = sourceLabel,
                Prototype = item
            };

            stackMap[stackKey] = stack;
            ordered.Add(stack);
        }

        stack.RemainingInstances.Add(new LootCollectionUI.LootItemInstance
        {
            Item = item,
            SourceType = sourceType,
            SourceEnemy = sourceEnemy,
            GroundPosition = groundPos,
            SourceLabel = sourceLabel
        });
    }

    private void TryTransferLootItemInstanceToStash(LootCollectionUI.LootItemInstance lootInstance, Action<bool> done)
    {
        bool success = false;

        if (lootInstance == null || !lootInstance.IsValid)
        {
            done?.Invoke(false);
            return;
        }

        if (PartyStash == null)
            PartyStash = new PartyStash();

        if (PartyStash.IsLocked)
            PartyStash.Unlock();

        bool removedFromSource = RemoveItemFromLootSource(lootInstance);
        if (!removedFromSource)
        {
            done?.Invoke(false);
            return;
        }

        success = PartyStash.AddItem(lootInstance.Item);
        if (!success)
        {
            RestoreItemToLootSource(lootInstance);
        }

        done?.Invoke(success);
    }

    private bool RemoveItemFromLootSource(LootCollectionUI.LootItemInstance lootInstance)
    {
        if (lootInstance == null || lootInstance.Item == null)
            return false;

        switch (lootInstance.SourceType)
        {
            case LootCollectionUI.LootSourceType.Enemy:
            {
                CharacterController enemy = lootInstance.SourceEnemy;
                Inventory inv = GetCharacterInventory(enemy);
                return inv != null && inv.RemoveItem(lootInstance.Item);
            }

            case LootCollectionUI.LootSourceType.Ground:
            {
                SquareCell cell = Grid != null ? Grid.GetCell(lootInstance.GroundPosition) : null;
                return cell != null && cell.RemoveGroundItem(lootInstance.Item);
            }

            default:
                return false;
        }
    }

    private void RestoreItemToLootSource(LootCollectionUI.LootItemInstance lootInstance)
    {
        if (lootInstance == null || lootInstance.Item == null)
            return;

        switch (lootInstance.SourceType)
        {
            case LootCollectionUI.LootSourceType.Enemy:
            {
                CharacterController enemy = lootInstance.SourceEnemy;
                Inventory inv = GetCharacterInventory(enemy);
                if (inv != null)
                    inv.AddItem(lootInstance.Item);
                break;
            }

            case LootCollectionUI.LootSourceType.Ground:
            {
                SquareCell cell = Grid != null ? Grid.GetCell(lootInstance.GroundPosition) : null;
                cell?.AddGroundItem(lootInstance.Item);
                break;
            }
        }
    }

    private Inventory GetCharacterInventory(CharacterController character)
    {
        if (character == null)
            return null;

        InventoryComponent inventoryComponent = character.GetComponent<InventoryComponent>();
        return inventoryComponent != null ? inventoryComponent.CharacterInventory : null;
    }

    private static bool IsValidLootItem(ItemData item)
    {
        if (item == null)
            return false;

        if (item.IsDestroyed)
            return false;

        return true;
    }

    private static int CountTotalItems(List<LootCollectionUI.LootStackEntry> entries)
    {
        if (entries == null)
            return 0;

        int total = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            LootCollectionUI.LootStackEntry entry = entries[i];
            if (entry == null)
                continue;

            total += Mathf.Max(0, entry.RemainingQuantity);
        }

        return total;
    }
}
