using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Activation framework for D&D 3.5e wondrous items (DMG pp. 248–271).
/// Supports four activation types: Passive, Command Word, Use-Activated, Continuous.
/// Handles daily use tracking, rest resets, and spell-like ability integration.
/// </summary>
public static class WondrousItemActivation
{
    // ════════════════════════════════════════════════════════════
    //  Activation Type Constants
    // ════════════════════════════════════════════════════════════
    public const string PASSIVE = "passive";
    public const string COMMAND_WORD = "command_word";
    public const string USE_ACTIVATED = "use_activated";
    public const string CONTINUOUS = "continuous";

    // ════════════════════════════════════════════════════════════
    //  Activation Entry Point
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Attempt to activate a wondrous item's ability.
    /// Returns true if activation succeeded, false otherwise.
    /// </summary>
    public static bool TryActivate(CharacterController user, ItemData item, out string resultMessage)
    {
        resultMessage = "";

        if (item == null || !item.IsWondrous)
        {
            resultMessage = "Not a wondrous item.";
            return false;
        }

        if (!item.WondrousHasActivation)
        {
            resultMessage = $"{item.Name} is a passive item with no activation.";
            return false;
        }

        string activationType = item.WondrousActivationType ?? PASSIVE;

        switch (activationType)
        {
            case PASSIVE:
            case CONTINUOUS:
                resultMessage = $"{item.Name} is always active.";
                return false;

            case COMMAND_WORD:
                return TryCommandWordActivation(user, item, out resultMessage);

            case USE_ACTIVATED:
                // Boots of Speed: route to haste activation
                if (item.WondrousGrantsHaste)
                    return TryActivateHaste(user, item, out resultMessage);
                return TryUseActivation(user, item, out resultMessage);

            default:
                resultMessage = $"Unknown activation type: {activationType}";
                return false;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Command Word Activation (Standard Action)
    // ════════════════════════════════════════════════════════════

    private static bool TryCommandWordActivation(CharacterController user, ItemData item, out string resultMessage)
    {
        // Check all use limits (daily, weekly, monthly, charges)
        if (!CheckAllUseLimits(item, out resultMessage))
            return false;

        // Consume uses
        ConsumeUse(item);

        resultMessage = $"✨ {user.Stats.CharacterName} speaks the command word to activate {item.Name}!";
        Debug.Log($"[WondrousActivation] {user.Stats.CharacterName} activated {item.Name} (command word).");
        return true;
    }

    // ════════════════════════════════════════════════════════════
    //  Use-Activated Activation (Varies — standard/move/free)
    // ════════════════════════════════════════════════════════════

    private static bool TryUseActivation(CharacterController user, ItemData item, out string resultMessage)
    {
        // Check all use limits (daily, weekly, monthly, charges)
        if (!CheckAllUseLimits(item, out resultMessage))
            return false;

        // Consume uses
        ConsumeUse(item);

        resultMessage = $"✨ {user.Stats.CharacterName} activates {item.Name}!";
        Debug.Log($"[WondrousActivation] {user.Stats.CharacterName} activated {item.Name} (use-activated).");
        return true;
    }

    // ════════════════════════════════════════════════════════════
    //  Daily / Weekly / Monthly Use Tracking
    // ════════════════════════════════════════════════════════════

    private static bool CheckDailyUses(ItemData item, out string message)
    {
        message = "";
        if (item.WondrousUsesPerDay <= 0) return true; // Unlimited or not daily-tracked

        if (item.WondrousUsesToday >= item.WondrousUsesPerDay)
        {
            message = $"{item.Name} has no uses remaining today ({item.WondrousUsesToday}/{item.WondrousUsesPerDay}).";
            return false;
        }
        return true;
    }

    /// <summary>Check if item has weekly uses remaining (Phase 7/8: Figurines, etc.).</summary>
    private static bool CheckWeeklyUses(ItemData item, out string message)
    {
        message = "";
        if (item.WondrousUsesPerWeek <= 0) return true; // Not weekly-tracked

        if (item.WondrousUsesThisWeek >= item.WondrousUsesPerWeek)
        {
            message = $"{item.Name} has no uses remaining this week ({item.WondrousUsesThisWeek}/{item.WondrousUsesPerWeek}).";
            return false;
        }
        return true;
    }

    /// <summary>Check if item has monthly uses remaining (Phase 8: Marble Elephant, etc.).</summary>
    private static bool CheckMonthlyUses(ItemData item, out string message)
    {
        message = "";
        if (item.WondrousUsesPerMonth <= 0) return true; // Not monthly-tracked

        if (item.WondrousUsesThisMonth >= item.WondrousUsesPerMonth)
        {
            message = $"{item.Name} has no uses remaining this month ({item.WondrousUsesThisMonth}/{item.WondrousUsesPerMonth}).";
            return false;
        }
        return true;
    }

    private static bool CheckCharges(ItemData item, out string message)
    {
        message = "";
        if (item.WondrousMaxCharges <= 0) return true; // Not charge-based

        if (item.WondrousCurrentCharges <= 0)
        {
            message = $"{item.Name} has no charges remaining.";
            return false;
        }
        return true;
    }

    /// <summary>Check all use limits (daily, weekly, monthly, charges) in order. Returns false if any limit is hit.</summary>
    private static bool CheckAllUseLimits(ItemData item, out string message)
    {
        if (!CheckDailyUses(item, out message)) return false;
        if (!CheckWeeklyUses(item, out message)) return false;
        if (!CheckMonthlyUses(item, out message)) return false;
        if (!CheckCharges(item, out message)) return false;
        return true;
    }

    /// <summary>Consume one use from the appropriate tracking pool (daily, weekly, monthly, charges).</summary>
    private static void ConsumeUse(ItemData item)
    {
        if (item.WondrousUsesPerDay > 0)
            item.WondrousUsesToday++;
        if (item.WondrousUsesPerWeek > 0)
            item.WondrousUsesThisWeek++;
        if (item.WondrousUsesPerMonth > 0)
            item.WondrousUsesThisMonth++;
        if (item.WondrousMaxCharges > 0)
            item.WondrousCurrentCharges--;
    }

    // ════════════════════════════════════════════════════════════
    //  Rest Reset — Reset daily uses for all wondrous items
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Reset daily uses for all wondrous items equipped by party members.
    /// Called during rest handler.
    /// </summary>
    public static void OnRest(List<CharacterController> partyMembers)
    {
        if (partyMembers == null) return;

        int resetCount = 0;
        foreach (var pc in partyMembers)
        {
            if (pc == null) continue;
            var inv = pc.InventoryComp;
            if (inv == null) continue;

            // Reset haste state on character stats
            pc.Stats.WondrousHasteActive = false;
            pc.Stats.WondrousHasteRoundsRemaining = 0;

            resetCount += ResetDailyUsesForInventory(inv.CharacterInventory);
        }

        if (resetCount > 0)
            Debug.Log($"[WondrousActivation] Reset daily uses for {resetCount} wondrous items on rest.");
    }

    /// <summary>Reset daily uses for all wondrous items in a single inventory.</summary>
    private static int ResetDailyUsesForInventory(Inventory inventory)
    {
        if (inventory == null) return 0;

        int count = 0;

        // Check all equipment slots
        foreach (EquipSlot slot in Inventory.AllEquipmentSlots)
        {
            ItemData item = inventory.GetEquipped(slot);
            if (item != null && item.IsWondrous)
                count += ResetSingleItem(item);
        }

        // Check slotless items
        if (inventory.SlotlessItems != null)
        {
            foreach (var item in inventory.SlotlessItems)
            {
                if (item != null && item.IsWondrous)
                    count += ResetSingleItem(item);
            }
        }

        // Check general inventory (items with activation that might be stored)
        if (inventory.GeneralSlots != null)
        {
            foreach (var item in inventory.GeneralSlots)
            {
                if (item != null && item.IsWondrous)
                    count += ResetSingleItem(item);
            }
        }

        return count;
    }

    /// <summary>Reset daily uses, haste rounds, and flight uses for a single wondrous item.</summary>
    private static int ResetSingleItem(ItemData item)
    {
        int resets = 0;

        // Reset standard daily uses
        if (item.WondrousUsesToday > 0)
        {
            item.WondrousUsesToday = 0;
            resets++;
        }

        // Reset haste tracking (Boots of Speed)
        if (item.WondrousGrantsHaste)
        {
            item.WondrousHasteRoundsUsedToday = 0;
            item.WondrousHasteCurrentlyActive = false;
            resets++;
        }

        // Reset flight duration tracking (Winged Boots)
        if (item.WondrousFlightRoundsRemaining > 0)
        {
            item.WondrousFlightRoundsRemaining = 0;
            resets++;
        }

        // Reset spell-like ability daily uses (Mantle of Faith, etc.)
        if (!string.IsNullOrEmpty(item.WondrousSpellLikeAbilities) && !string.IsNullOrEmpty(item.WondrousSpellLikeUsesToday))
        {
            string[] spells = item.WondrousSpellLikeAbilities.Split(',');
            string zeros = string.Join(",", new string[spells.Length].Select(s => "0"));
            if (item.WondrousSpellLikeUsesToday != zeros)
            {
                item.WondrousSpellLikeUsesToday = zeros;
                resets++;
            }
        }

        return resets;
    }

    // ════════════════════════════════════════════════════════════
    //  Weekly/Monthly Rest Resets
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Reset weekly uses for all wondrous items. Called when a full week of in-game time passes
    /// (typically tracked by the game calendar or after 7 rests).
    /// </summary>
    public static void OnWeeklyReset(List<CharacterController> partyMembers)
    {
        if (partyMembers == null) return;

        int resetCount = 0;
        foreach (var pc in partyMembers)
        {
            if (pc == null) continue;
            var inv = pc.InventoryComp;
            if (inv == null) continue;
            resetCount += ResetWeeklyUsesForInventory(inv.CharacterInventory);
        }

        if (resetCount > 0)
            Debug.Log($"[WondrousActivation] Reset weekly uses for {resetCount} wondrous items.");
    }

    /// <summary>
    /// Reset monthly uses for all wondrous items. Called when a full month of in-game time passes.
    /// </summary>
    public static void OnMonthlyReset(List<CharacterController> partyMembers)
    {
        if (partyMembers == null) return;

        int resetCount = 0;
        foreach (var pc in partyMembers)
        {
            if (pc == null) continue;
            var inv = pc.InventoryComp;
            if (inv == null) continue;
            resetCount += ResetMonthlyUsesForInventory(inv.CharacterInventory);
        }

        if (resetCount > 0)
            Debug.Log($"[WondrousActivation] Reset monthly uses for {resetCount} wondrous items.");
    }

    private static int ResetWeeklyUsesForInventory(Inventory inventory)
    {
        if (inventory == null) return 0;
        int count = 0;
        foreach (EquipSlot slot in Inventory.AllEquipmentSlots)
        {
            ItemData item = inventory.GetEquipped(slot);
            if (item != null && item.IsWondrous)
            {
                if (item.WondrousUsesThisWeek > 0) { item.WondrousUsesThisWeek = 0; count++; }
                count += ResetCubicGateWeeklyUses(item);
            }
        }
        if (inventory.SlotlessItems != null)
            foreach (var item in inventory.SlotlessItems)
                if (item != null && item.IsWondrous)
                {
                    if (item.WondrousUsesThisWeek > 0) { item.WondrousUsesThisWeek = 0; count++; }
                    count += ResetCubicGateWeeklyUses(item);
                }
        if (inventory.GeneralSlots != null)
            foreach (var item in inventory.GeneralSlots)
                if (item != null && item.IsWondrous)
                {
                    if (item.WondrousUsesThisWeek > 0) { item.WondrousUsesThisWeek = 0; count++; }
                    count += ResetCubicGateWeeklyUses(item);
                }
        return count;
    }

    /// <summary>Reset Cubic Gate per-side weekly uses. Returns 1 if any side was reset, 0 otherwise.</summary>
    private static int ResetCubicGateWeeklyUses(ItemData item)
    {
        if (item.WondrousCubicGateUsesThisWeek == null) return 0;
        bool anyUsed = false;
        for (int i = 0; i < item.WondrousCubicGateUsesThisWeek.Length; i++)
        {
            if (item.WondrousCubicGateUsesThisWeek[i] > 0)
            {
                item.WondrousCubicGateUsesThisWeek[i] = 0;
                anyUsed = true;
            }
        }
        return anyUsed ? 1 : 0;
    }

    private static int ResetMonthlyUsesForInventory(Inventory inventory)
    {
        if (inventory == null) return 0;
        int count = 0;
        foreach (EquipSlot slot in Inventory.AllEquipmentSlots)
        {
            ItemData item = inventory.GetEquipped(slot);
            if (item != null && item.IsWondrous && item.WondrousUsesThisMonth > 0)
            {
                item.WondrousUsesThisMonth = 0;
                count++;
            }
        }
        if (inventory.SlotlessItems != null)
            foreach (var item in inventory.SlotlessItems)
                if (item != null && item.IsWondrous && item.WondrousUsesThisMonth > 0)
                {
                    item.WondrousUsesThisMonth = 0;
                    count++;
                }
        if (inventory.GeneralSlots != null)
            foreach (var item in inventory.GeneralSlots)
                if (item != null && item.IsWondrous && item.WondrousUsesThisMonth > 0)
                {
                    item.WondrousUsesThisMonth = 0;
                    count++;
                }
        return count;
    }

    // ════════════════════════════════════════════════════════════
    //  Haste Effect (Boots of Speed) — D&D 3.5e PHB p.239
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Activate Boots of Speed haste effect.
    /// Haste: +1 dodge AC, +1 attack, +30 ft speed, extra attack at full BAB.
    /// Duration: up to 10 rounds per day, need not be consecutive.
    /// Activation: Free action (click boot heels together).
    /// </summary>
    public static bool TryActivateHaste(CharacterController user, ItemData item, out string resultMessage)
    {
        resultMessage = "";

        if (item == null || !item.WondrousGrantsHaste)
        {
            resultMessage = "This item does not grant haste.";
            return false;
        }

        // Already active — toggle off
        if (item.WondrousHasteCurrentlyActive)
        {
            DeactivateHaste(user, item);
            resultMessage = $"⏹ {user.Stats.CharacterName} deactivates {item.Name}. Haste ends.";
            return true;
        }

        // Check rounds remaining
        int remaining = item.WondrousHasteMaxRounds - item.WondrousHasteRoundsUsedToday;
        if (remaining <= 0)
        {
            resultMessage = $"{item.Name} has no haste rounds remaining today (0/{item.WondrousHasteMaxRounds}).";
            return false;
        }

        // Activate haste
        item.WondrousHasteCurrentlyActive = true;
        user.Stats.WondrousHasteActive = true;
        user.Stats.WondrousHasteRoundsRemaining = remaining;

        resultMessage = $"⚡ {user.Stats.CharacterName} clicks the heels of the {item.Name}! Haste active ({remaining} rounds remaining).\n+1 dodge AC, +1 attack, +30 ft speed, extra attack at full BAB.";
        Debug.Log($"[WondrousActivation] Haste activated: {user.Stats.CharacterName}, {remaining} rounds remaining.");
        return true;
    }

    /// <summary>Deactivate haste effect from Boots of Speed.</summary>
    public static void DeactivateHaste(CharacterController user, ItemData item)
    {
        if (item != null)
            item.WondrousHasteCurrentlyActive = false;
        if (user != null)
        {
            user.Stats.WondrousHasteActive = false;
            user.Stats.WondrousHasteRoundsRemaining = 0;
        }
    }

    /// <summary>
    /// Called at the start of a character's turn to tick down haste rounds.
    /// Returns true if haste is still active, false if it expired.
    /// </summary>
    public static bool TickHasteRound(CharacterController user, ItemData bootsOfSpeed)
    {
        if (user == null || bootsOfSpeed == null || !bootsOfSpeed.WondrousHasteCurrentlyActive)
            return false;

        bootsOfSpeed.WondrousHasteRoundsUsedToday++;
        user.Stats.WondrousHasteRoundsRemaining--;

        if (user.Stats.WondrousHasteRoundsRemaining <= 0 ||
            bootsOfSpeed.WondrousHasteRoundsUsedToday >= bootsOfSpeed.WondrousHasteMaxRounds)
        {
            DeactivateHaste(user, bootsOfSpeed);
            Debug.Log($"[WondrousActivation] Haste expired for {user.Stats.CharacterName} (Boots of Speed).");
            return false;
        }

        return true;
    }

    /// <summary>Find the Boots of Speed item equipped by a character (if any).</summary>
    public static ItemData FindEquippedBootsOfSpeed(CharacterController character)
    {
        if (character == null) return null;
        var inv = character.InventoryComp;
        if (inv == null || inv.CharacterInventory == null) return null;

        ItemData feetItem = inv.CharacterInventory.GetEquipped(EquipSlot.Feet);
        if (feetItem != null && feetItem.WondrousGrantsHaste)
            return feetItem;
        return null;
    }

    // ════════════════════════════════════════════════════════════
    //  Equip/Unequip Hooks (for continuous/passive effects)
    // ════════════════════════════════════════════════════════════

    /// <summary>Called when a wondrous item is equipped. Apply continuous/passive effects.</summary>
    public static void OnWondrousEquipped(CharacterController character, ItemData item)
    {
        if (character == null || item == null || !item.IsWondrous) return;

        // Ensure instance ID is set
        if (string.IsNullOrEmpty(item.WondrousInstanceId))
            item.WondrousInstanceId = System.Guid.NewGuid().ToString("N").Substring(0, 8);

        Debug.Log($"[WondrousActivation] {character.Stats.CharacterName} equipped {item.Name} ({item.WondrousActivationType ?? "passive"}).");
    }

    /// <summary>Called when a wondrous item is unequipped. Remove continuous/passive effects.</summary>
    public static void OnWondrousUnequipped(CharacterController character, ItemData item)
    {
        if (character == null || item == null || !item.IsWondrous) return;

        Debug.Log($"[WondrousActivation] {character.Stats.CharacterName} unequipped {item.Name}.");
    }

    // ════════════════════════════════════════════════════════════
    //  Utility
    // ════════════════════════════════════════════════════════════

    /// <summary>Get or generate a unique instance ID for a wondrous item.</summary>
    public static string GetWondrousInstanceId(ItemData item)
    {
        if (item == null) return "";
        if (string.IsNullOrEmpty(item.WondrousInstanceId))
            item.WondrousInstanceId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        return item.WondrousInstanceId;
    }

    /// <summary>Check if a wondrous item has remaining uses (daily, weekly, monthly, or charges).</summary>
    public static bool HasUsesRemaining(ItemData item)
    {
        if (item == null || !item.IsWondrous) return false;
        if (item.WondrousUsesPerDay > 0 && item.WondrousUsesToday >= item.WondrousUsesPerDay) return false;
        if (item.WondrousUsesPerWeek > 0 && item.WondrousUsesThisWeek >= item.WondrousUsesPerWeek) return false;
        if (item.WondrousUsesPerMonth > 0 && item.WondrousUsesThisMonth >= item.WondrousUsesPerMonth) return false;
        if (item.WondrousMaxCharges > 0 && item.WondrousCurrentCharges <= 0) return false;
        return true;
    }

    /// <summary>Get remaining uses for display (most restrictive limit).</summary>
    public static int GetRemainingUses(ItemData item)
    {
        if (item == null || !item.IsWondrous) return -1;

        int remaining = int.MaxValue;
        bool hasAnyLimit = false;

        if (item.WondrousUsesPerDay > 0)
        {
            remaining = Mathf.Min(remaining, Mathf.Max(0, item.WondrousUsesPerDay - item.WondrousUsesToday));
            hasAnyLimit = true;
        }
        if (item.WondrousUsesPerWeek > 0)
        {
            remaining = Mathf.Min(remaining, Mathf.Max(0, item.WondrousUsesPerWeek - item.WondrousUsesThisWeek));
            hasAnyLimit = true;
        }
        if (item.WondrousUsesPerMonth > 0)
        {
            remaining = Mathf.Min(remaining, Mathf.Max(0, item.WondrousUsesPerMonth - item.WondrousUsesThisMonth));
            hasAnyLimit = true;
        }
        if (item.WondrousMaxCharges > 0)
        {
            remaining = Mathf.Min(remaining, Mathf.Max(0, item.WondrousCurrentCharges));
            hasAnyLimit = true;
        }

        return hasAnyLimit ? remaining : -1; // -1 = unlimited
    }
}
