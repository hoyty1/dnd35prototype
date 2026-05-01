using System;
using System.Collections.Generic;
using System.Linq;
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
        Debug.Log("[LootFlow] Beginning post-combat loot collection");

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
                Debug.Log($"[LootFlow] Loot collection complete, {lootedCount} items looted");

                if (lootedCount > 0)
                    CombatUI?.ShowCombatLog($"📦 {lootedCount} item(s) looted to party stash. Stash unlocked.");
                else
                    CombatUI?.ShowCombatLog("📦 Loot window closed. Party stash unlocked.");

                Debug.Log($"[LootFlow] Transitioning from loot to XP flow | lootedCount={lootedCount} | waitingLoot={WaitingForLootCollection} | phase={CurrentPhase} | subPhase={CurrentSubPhase}");
                ShowPostCombatXPFlow(lootedCount);
            },
            onExitLoop: ExitCombatLoopToMenu);
    }

    private void ShowPostCombatXPFlow(int lootedCount)
    {
        Debug.Log("[LootFlow] Starting XP flow");
        RegisterCombatLoopCompletion(lootedCount);

        CaptureDefeatedEnemiesSnapshotForXP("PostLoot.XPFlow");

        List<CharacterController> partyMembers = new List<CharacterController>();
        if (PCs != null)
        {
            for (int i = 0; i < PCs.Count; i++)
            {
                CharacterController pc = PCs[i];
                if (pc == null || pc.Stats == null || !IsActiveCombatant(pc))
                    continue;

                partyMembers.Add(pc);
            }
        }

        List<CharacterController> defeatedEnemies = GetDefeatedEnemiesForXP();
        Debug.Log($"[LootFlow] Found {defeatedEnemies.Count} defeated enemies");
        Debug.Log($"[XP] Preparing post-combat XP flow | party={partyMembers.Count} | defeatedTracked={defeatedEnemies.Count}");

        if (partyMembers.Count == 0 || defeatedEnemies.Count == 0)
        {
            Debug.Log("[XP] Skipping XP UI because party or defeated-enemy list is empty.");
            ContinueToRestAndNextCombat();
            return;
        }

        ExperienceCalculator.CombatXPResult xpResult = ExperienceCalculator.Instance.CalculateXPForCombat(partyMembers, defeatedEnemies);
        Debug.Log($"[LootFlow] XP calculated: {xpResult.TotalXPPerCharacter} per character");

        CombatEndXPUI xpUi = FindOrCreateXPUI();
        if (xpUi == null)
        {
            Debug.LogWarning("[XP UI] Unable to create/find XP display. Continuing without XP panel.");
            ContinueToRestAndNextCombat();
            return;
        }

        xpUi.ShowXPAwards(xpResult, () =>
        {
            Debug.Log("[LootFlow] XP UI complete, checking for level-ups");
            int leveledUpCount = xpResult != null && xpResult.CharacterLeveledUp != null
                ? xpResult.CharacterLeveledUp.Count(kvp => kvp.Value)
                : 0;
            Debug.Log($"[LootFlow] Characters who leveled up: {leveledUpCount}");

            if (xpResult != null && xpResult.CharacterLeveledUp != null)
            {
                foreach (KeyValuePair<CharacterController, bool> kvp in xpResult.CharacterLeveledUp)
                {
                    string charName = kvp.Key != null && kvp.Key.Stats != null && !string.IsNullOrWhiteSpace(kvp.Key.Stats.CharacterName)
                        ? kvp.Key.Stats.CharacterName
                        : "Unknown";
                    Debug.Log($"[LootFlow] {charName}: LeveledUp={kvp.Value}");
                }
            }

            CheckAndShowLevelUps(xpResult, () =>
            {
                Debug.Log("[LootFlow] Level-ups complete, continuing to rest");
                ContinueToRestAndNextCombat();
            });
        });
    }

    private CombatEndXPUI FindOrCreateXPUI()
    {
        CombatEndXPUI xpUi = FindObjectOfType<CombatEndXPUI>();
        if (xpUi != null)
        {
            Debug.Log($"[GameManager][XP UI] Using existing active CombatEndXPUI on '{xpUi.gameObject.name}' (parent={xpUi.transform.parent?.name})");
            return xpUi;
        }

        Debug.Log("[GameManager][XP UI] No active CombatEndXPUI found. Creating or recovering one.");

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[GameManager][XP UI] No Canvas found!");
            return null;
        }

        Debug.Log($"[GameManager][XP UI] Canvas found: {canvas.name}, active={canvas.gameObject.activeInHierarchy}, enabled={canvas.enabled}, renderMode={canvas.renderMode}, sortingOrder={canvas.sortingOrder}");

        xpUi = canvas.GetComponentInChildren<CombatEndXPUI>(true);
        if (xpUi != null)
        {
            Debug.Log($"[GameManager][XP UI] Recovered existing inactive CombatEndXPUI on '{xpUi.gameObject.name}'");
            return xpUi;
        }

        GameObject uiObj = new GameObject("CombatEndXPUI", typeof(RectTransform));
        uiObj.transform.SetParent(canvas.transform, false);
        uiObj.transform.SetAsLastSibling();

        RectTransform uiRect = uiObj.GetComponent<RectTransform>();
        if (uiRect != null)
        {
            uiRect.anchorMin = Vector2.zero;
            uiRect.anchorMax = Vector2.one;
            uiRect.offsetMin = Vector2.zero;
            uiRect.offsetMax = Vector2.zero;
            uiRect.localScale = Vector3.one;
        }

        xpUi = uiObj.AddComponent<CombatEndXPUI>();
        Debug.Log($"[GameManager][XP UI] CombatEndXPUI created and parented to {canvas.name}");
        return xpUi;
    }

    private void ContinueToRestAndNextCombat()
    {
        RestorePartyAfterCombat();
        ReturnToEncounterSelection();
    }

    private void CheckAndShowLevelUps(ExperienceCalculator.CombatXPResult xpResult, Action onComplete)
    {
        Debug.Log("[LevelUp] CheckAndShowLevelUps called");

        List<CharacterController> leveledUpCharacters = new List<CharacterController>();

        if (xpResult != null && xpResult.CharacterLeveledUp != null)
        {
            foreach (KeyValuePair<CharacterController, bool> kvp in xpResult.CharacterLeveledUp)
            {
                CharacterController character = kvp.Key;
                string charName = character != null && character.Stats != null && !string.IsNullOrWhiteSpace(character.Stats.CharacterName)
                    ? character.Stats.CharacterName
                    : "Unknown";

                if (!kvp.Value)
                {
                    Debug.Log($"[LevelUp] {charName} did not level up.");
                    continue;
                }

                if (character == null || character.Stats == null)
                {
                    Debug.LogWarning("[LevelUp] Encountered null character/stats in leveled-up list entry.");
                    continue;
                }

                leveledUpCharacters.Add(character);
                Debug.Log($"[LevelUp] {charName} leveled up!");
            }
        }
        else
        {
            Debug.LogWarning("[LevelUp] XP result or CharacterLeveledUp map was null.");
        }

        Debug.Log($"[LevelUp] Total characters who leveled up: {leveledUpCharacters.Count}");

        if (leveledUpCharacters.Count == 0)
        {
            Debug.Log("[LevelUp] No level-ups, proceeding to complete callback");
            onComplete?.Invoke();
            return;
        }

        ShowLevelUpUISequence(leveledUpCharacters, 0, onComplete);
    }

    private void ShowLevelUpUISequence(List<CharacterController> characters, int index, Action onComplete)
    {
        if (index >= characters.Count)
        {
            Debug.Log("[LevelUp] All level-ups processed");
            onComplete?.Invoke();
            return;
        }

        CharacterController character = characters[index];
        string charName = character != null && character.Stats != null && !string.IsNullOrWhiteSpace(character.Stats.CharacterName)
            ? character.Stats.CharacterName
            : "Unknown";

        Debug.Log($"[LevelUp] Showing level-up UI for {charName} ({index + 1}/{characters.Count})");

        LevelUpUI levelUpUI = FindOrCreateLevelUpUI();
        if (levelUpUI == null)
        {
            Debug.LogError("[LevelUp] Failed to find/create LevelUpUI!");
            onComplete?.Invoke();
            return;
        }

        levelUpUI.ShowForCharacter(character, () =>
        {
            Debug.Log($"[LevelUp] Level-up complete for {charName}");
            ShowLevelUpUISequence(characters, index + 1, onComplete);
        });
    }

    private LevelUpUI FindOrCreateLevelUpUI()
    {
        Debug.Log("[LevelUp] Finding or creating LevelUpUI");

        LevelUpUI levelUpUI = FindObjectOfType<LevelUpUI>();

        if (levelUpUI == null)
        {
            Debug.Log("[LevelUp] LevelUpUI not found, creating new one");

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[LevelUp] No Canvas found!");
                return null;
            }

            GameObject uiObj = new GameObject("LevelUpUI", typeof(RectTransform));
            uiObj.transform.SetParent(canvas.transform, false);
            uiObj.transform.SetAsLastSibling();

            RectTransform uiRect = uiObj.GetComponent<RectTransform>();
            if (uiRect != null)
            {
                uiRect.anchorMin = Vector2.zero;
                uiRect.anchorMax = Vector2.one;
                uiRect.offsetMin = Vector2.zero;
                uiRect.offsetMax = Vector2.zero;
                uiRect.localScale = Vector3.one;
            }

            levelUpUI = uiObj.AddComponent<LevelUpUI>();
            Debug.Log("[LevelUp] LevelUpUI created");
        }
        else
        {
            Debug.Log("[LevelUp] Using existing LevelUpUI");
        }

        return levelUpUI;
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

        Debug.Log($"[LootCollection] Gathering loot from battlefield | npcCount={NPCs.Count}");

        int defeatedEnemyCount = 0;
        int foundItemCount = 0;
        HashSet<int> lootedEnemyIds = new HashSet<int>();

        for (int i = 0; i < NPCs.Count; i++)
        {
            CharacterController enemy = NPCs[i];
            if (enemy == null)
            {
                Debug.Log($"[Loot] Skipping NPC index {i}: null reference.");
                continue;
            }

            var stats = enemy.Stats;
            if (stats == null)
            {
                Debug.Log($"[Loot] Skipping '{enemy.name}': missing stats.");
                continue;
            }

            bool isEnemy = enemy.Team == CharacterTeam.Enemy;
            int hp = stats.CurrentHP;
            bool isUnconscious = stats.IsUnconscious;
            bool isDead = stats.IsDead;
            bool willLoot = isEnemy && hp <= 0;

            Debug.Log($"[Loot] Checking {stats.CharacterName}\n  - IsEnemy: {isEnemy}\n  - HP: {hp}\n  - IsUnconscious: {isUnconscious}\n  - IsDead: {isDead}\n  - Will Loot: {willLoot}");

            if (!willLoot)
                continue;

            int enemyInstanceId = enemy.GetInstanceID();
            if (!lootedEnemyIds.Add(enemyInstanceId))
            {
                Debug.Log($"[Loot] {stats.CharacterName} already processed for loot in this pass, skipping duplicate entry.");
                continue;
            }

            defeatedEnemyCount++;

            Inventory inventory = GetCharacterInventory(enemy);
            if (inventory == null)
            {
                Debug.LogWarning($"[LootFlow] Defeated enemy '{enemy.name}' has no inventory component.");
                continue;
            }

            string sourceLabel = $"From {stats.CharacterName}";
            string sourceGroupKey = $"enemy:{enemyInstanceId}";
            int enemyItemCount = 0;

            foreach (EquipSlot slot in Inventory.AllEquipmentSlots)
            {
                ItemData equipped = inventory.GetEquipped(slot);
                if (!IsValidLootItem(equipped))
                    continue;

                enemyItemCount++;
                foundItemCount++;
                Debug.Log($"[Loot] + {equipped.Name} from {stats.CharacterName}'s equipment ({slot})");
                AddLootInstance(ordered, sourceGroupKey, sourceLabel, equipped, LootCollectionUI.LootSourceType.Enemy, enemy, Vector2Int.zero);
            }

            if (inventory.GeneralSlots != null)
            {
                for (int slotIndex = 0; slotIndex < inventory.GeneralSlots.Length; slotIndex++)
                {
                    ItemData item = inventory.GeneralSlots[slotIndex];
                    if (!IsValidLootItem(item))
                        continue;

                    enemyItemCount++;
                    foundItemCount++;
                    Debug.Log($"[Loot] + {item.Name} from {stats.CharacterName}'s inventory slot {slotIndex}");
                    AddLootInstance(ordered, sourceGroupKey, sourceLabel, item, LootCollectionUI.LootSourceType.Enemy, enemy, Vector2Int.zero);
                }
            }

            Debug.Log($"[Loot] Collected {enemyItemCount} item(s) from {stats.CharacterName}");
        }

        Debug.Log($"[LootCollection] GatherLootFromDefeatedEnemies complete | defeatedEnemies={defeatedEnemyCount} | itemInstances={foundItemCount}");
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
                Debug.Log($"[Loot] Collecting ground item: {item.Name} at ({kvp.Key.x},{kvp.Key.y})");
                AddLootInstance(ordered, sourceGroupKey, sourceLabel, item, LootCollectionUI.LootSourceType.Ground, null, kvp.Key);
            }
        }

        Debug.Log($"[LootCollection] GatherLootFromGround complete | occupiedCells={occupiedGroundCells} | itemInstances={foundGroundItemCount}");
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
