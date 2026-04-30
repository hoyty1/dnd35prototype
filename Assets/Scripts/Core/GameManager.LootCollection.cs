using System;
using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
    /// <summary>Modal post-combat loot collection UI.</summary>
    public LootCollectionUI LootCollectionUI;

    /// <summary>Whether the game is waiting for the player to finish loot collection.</summary>
    public bool WaitingForLootCollection { get; private set; }

    /// <summary>Tracks whether loot collection was already triggered for the current combat.</summary>
    private bool _postCombatLootCollectionTriggered;

    private void ResetPostCombatLootCollectionState(string context)
    {
        _postCombatLootCollectionTriggered = false;
        WaitingForLootCollection = false;
        Debug.Log($"[LootFlow] Reset post-combat loot state | context={context} | frame={Time.frameCount}");
    }

    private void BeginPostCombatLootCollection()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        Debug.Log($"[LootFlow] BeginPostCombatLootCollection START | frame={Time.frameCount} | phase={CurrentPhase} | lootUiAssigned={LootCollectionUI != null} | partyStashAssigned={PartyStash != null} | partyStashLocked={(PartyStash != null && PartyStash.IsLocked)} | canvasFound={canvas != null}");

        if (_postCombatLootCollectionTriggered)
        {
            Debug.Log($"[LootFlow] BeginPostCombatLootCollection SKIP | alreadyTriggered=true | waiting={WaitingForLootCollection}");
            return;
        }

        _postCombatLootCollectionTriggered = true;

        EnsurePartyStashInitialized();
        PartyStash?.Unlock();

        EnsureLootCollectionUIInitialized();
        if (LootCollectionUI == null)
        {
            WaitingForLootCollection = false;
            Debug.LogError("[LootFlow] LootCollectionUI is null after initialization attempt. Cannot open loot window.");
            CombatUI?.ShowCombatLog("⚠ Loot window unavailable. Stash unlocked.");
            return;
        }

        List<LootCollectionUI.LootStackEntry> lootEntries = GatherPostCombatLootEntries();
        int totalItems = CountTotalItems(lootEntries);
        Debug.Log($"[LootFlow] Gather complete | stacks={lootEntries.Count} | totalItems={totalItems}");

        WaitingForLootCollection = true;
        if (totalItems > 0)
            CombatUI?.ShowCombatLog($"💰 Loot available: {totalItems} item(s). Collect loot before continuing.");
        else
            CombatUI?.ShowCombatLog("📭 No loot found. Review results and close loot window to continue.");

        Debug.Log($"[LootFlow] Opening loot window | waitingForLootCollection={WaitingForLootCollection}");
        LootCollectionUI.Open(
            lootEntries,
            onLootSingle: TryTransferLootItemInstanceToStash,
            onClosed: lootedCount =>
            {
                WaitingForLootCollection = false;
                PartyStash?.Unlock();

                Debug.Log($"[LootFlow] Loot window closed | lootedCount={lootedCount} | stashLocked={PartyStash != null && PartyStash.IsLocked}");

                if (lootedCount > 0)
                    CombatUI?.ShowCombatLog($"📦 {lootedCount} item(s) looted to party stash. Stash unlocked.");
                else
                    CombatUI?.ShowCombatLog("📦 Loot window closed. Party stash unlocked.");

                RegisterCombatLoopCompletion(lootedCount);
                RestorePartyAfterCombat();
                ReturnToEncounterSelection();
            },
            onExitLoop: ExitCombatLoopToMenu);
    }

    private void EnsureLootCollectionUIInitialized()
    {
        if (LootCollectionUI != null)
        {
            Debug.Log($"[LootFlow] LootCollectionUI already assigned on GameManager | object='{LootCollectionUI.gameObject.name}'");
            return;
        }

        LootCollectionUI = FindObjectOfType<LootCollectionUI>();
        if (LootCollectionUI != null)
        {
            Debug.Log($"[LootFlow] Found existing LootCollectionUI in scene | object='{LootCollectionUI.gameObject.name}'");
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[LootFlow] Unable to initialize LootCollectionUI because no Canvas was found.");
            return;
        }

        LootCollectionUI = canvas.gameObject.AddComponent<LootCollectionUI>();
        Debug.Log($"[LootFlow] Created LootCollectionUI on Canvas '{canvas.name}'");
    }

    private List<LootCollectionUI.LootStackEntry> GatherPostCombatLootEntries()
    {
        Debug.Log("[LootFlow] GatherPostCombatLootEntries START");

        List<LootCollectionUI.LootStackEntry> ordered = new List<LootCollectionUI.LootStackEntry>();

        GatherLootFromDefeatedEnemies(ordered);
        GatherLootFromGround(ordered);

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

        Debug.Log($"[LootFlow] GatherPostCombatLootEntries END | stackCount={ordered.Count} | itemCount={CountTotalItems(ordered)}");
        return ordered;
    }

    private void GatherLootFromDefeatedEnemies(List<LootCollectionUI.LootStackEntry> ordered)
    {
        if (NPCs == null)
        {
            Debug.Log("[LootFlow] GatherLootFromDefeatedEnemies skipped: NPC list is null.");
            return;
        }

        int defeatedEnemyCount = 0;
        int foundItemCount = 0;

        for (int i = 0; i < NPCs.Count; i++)
        {
            CharacterController enemy = NPCs[i];
            if (enemy == null || enemy.Stats == null || !enemy.Stats.IsDead)
                continue;

            defeatedEnemyCount++;

            Inventory inventory = GetCharacterInventory(enemy);
            if (inventory == null)
            {
                Debug.LogWarning($"[LootFlow] Defeated enemy '{enemy.name}' has no inventory component.");
                continue;
            }

            string sourceLabel = $"From {enemy.Stats.CharacterName}";
            string sourceGroupKey = $"enemy:{enemy.GetInstanceID()}";

            foreach (EquipSlot slot in Inventory.AllEquipmentSlots)
            {
                ItemData equipped = inventory.GetEquipped(slot);
                if (!IsValidLootItem(equipped))
                    continue;

                foundItemCount++;
                AddLootInstance(ordered, sourceGroupKey, sourceLabel, equipped, LootCollectionUI.LootSourceType.Enemy, enemy, Vector2Int.zero);
            }

            if (inventory.GeneralSlots == null)
                continue;

            for (int slotIndex = 0; slotIndex < inventory.GeneralSlots.Length; slotIndex++)
            {
                ItemData item = inventory.GeneralSlots[slotIndex];
                if (!IsValidLootItem(item))
                    continue;

                foundItemCount++;
                AddLootInstance(ordered, sourceGroupKey, sourceLabel, item, LootCollectionUI.LootSourceType.Enemy, enemy, Vector2Int.zero);
            }
        }

        Debug.Log($"[LootFlow] GatherLootFromDefeatedEnemies complete | defeatedEnemies={defeatedEnemyCount} | itemInstances={foundItemCount}");
    }

    private void GatherLootFromGround(List<LootCollectionUI.LootStackEntry> ordered)
    {
        if (Grid == null || Grid.Cells == null)
        {
            Debug.Log("[LootFlow] GatherLootFromGround skipped: grid/cells unavailable.");
            return;
        }

        int occupiedGroundCells = 0;
        int foundGroundItemCount = 0;

        foreach (KeyValuePair<Vector2Int, SquareCell> kvp in Grid.Cells)
        {
            SquareCell cell = kvp.Value;
            if (cell == null || cell.GroundItems == null || cell.GroundItems.Count == 0)
                continue;

            occupiedGroundCells++;

            string sourceLabel = $"Items on Ground ({kvp.Key.x},{kvp.Key.y})";
            string sourceGroupKey = $"ground:{kvp.Key.x}:{kvp.Key.y}";

            for (int i = 0; i < cell.GroundItems.Count; i++)
            {
                ItemData item = cell.GroundItems[i];
                if (!IsValidLootItem(item))
                    continue;

                foundGroundItemCount++;
                AddLootInstance(ordered, sourceGroupKey, sourceLabel, item, LootCollectionUI.LootSourceType.Ground, null, kvp.Key);
            }
        }

        Debug.Log($"[LootFlow] GatherLootFromGround complete | occupiedCells={occupiedGroundCells} | itemInstances={foundGroundItemCount}");
    }

    private void AddLootInstance(
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
        string stackKey = $"{sourceGroupKey}|{itemIdentity}|{ordered.Count}";

        LootCollectionUI.LootStackEntry stack = new LootCollectionUI.LootStackEntry
        {
            StackKey = stackKey,
            SourceGroupKey = sourceGroupKey,
            SourceLabel = sourceLabel,
            Prototype = item
        };

        stack.RemainingInstances.Add(new LootCollectionUI.LootItemInstance
        {
            Item = item,
            SourceType = sourceType,
            SourceEnemy = sourceEnemy,
            GroundPosition = groundPos,
            SourceLabel = sourceLabel
        });

        ordered.Add(stack);
    }

    private void TryTransferLootItemInstanceToStash(LootCollectionUI.LootItemInstance lootInstance, Action<bool> done)
    {
        bool success = false;

        if (lootInstance == null || !lootInstance.IsValid)
        {
            Debug.LogWarning("[LootFlow] Rejecting loot transfer: loot instance is null/invalid.");
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
            Debug.LogWarning($"[LootFlow] Failed removing '{lootInstance.Item.Name}' from source '{lootInstance.SourceLabel}'.");
            done?.Invoke(false);
            return;
        }

        success = PartyStash.AddItem(lootInstance.Item);
        if (!success)
        {
            Debug.LogWarning($"[LootFlow] Failed adding '{lootInstance.Item.Name}' to stash. Restoring item to source.");
            RestoreItemToLootSource(lootInstance);
        }
        else
        {
            Debug.Log($"[LootFlow] Looted '{lootInstance.Item.Name}' from '{lootInstance.SourceLabel}' into stash.");
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
