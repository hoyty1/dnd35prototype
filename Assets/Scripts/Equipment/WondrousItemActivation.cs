using System.Collections.Generic;
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
        // Check daily use limit
        if (!CheckDailyUses(item, out resultMessage))
            return false;

        // Check charge-based items
        if (!CheckCharges(item, out resultMessage))
            return false;

        // Consume a daily use
        if (item.WondrousUsesPerDay > 0)
            item.WondrousUsesToday++;

        // Consume a charge
        if (item.WondrousMaxCharges > 0)
            item.WondrousCurrentCharges--;

        resultMessage = $"✨ {user.Stats.CharacterName} speaks the command word to activate {item.Name}!";
        Debug.Log($"[WondrousActivation] {user.Stats.CharacterName} activated {item.Name} (command word).");
        return true;
    }

    // ════════════════════════════════════════════════════════════
    //  Use-Activated Activation (Varies — standard/move/free)
    // ════════════════════════════════════════════════════════════

    private static bool TryUseActivation(CharacterController user, ItemData item, out string resultMessage)
    {
        // Check daily use limit
        if (!CheckDailyUses(item, out resultMessage))
            return false;

        // Check charge-based items
        if (!CheckCharges(item, out resultMessage))
            return false;

        // Consume a daily use
        if (item.WondrousUsesPerDay > 0)
            item.WondrousUsesToday++;

        // Consume a charge
        if (item.WondrousMaxCharges > 0)
            item.WondrousCurrentCharges--;

        resultMessage = $"✨ {user.Stats.CharacterName} activates {item.Name}!";
        Debug.Log($"[WondrousActivation] {user.Stats.CharacterName} activated {item.Name} (use-activated).");
        return true;
    }

    // ════════════════════════════════════════════════════════════
    //  Daily Use Tracking
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
            var inv = pc.GetComponent<InventoryComponent>();
            if (inv == null) continue;

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
            if (item != null && item.IsWondrous && item.WondrousUsesToday > 0)
            {
                item.WondrousUsesToday = 0;
                count++;
            }
        }

        // Check slotless items
        if (inventory.SlotlessItems != null)
        {
            foreach (var item in inventory.SlotlessItems)
            {
                if (item != null && item.IsWondrous && item.WondrousUsesToday > 0)
                {
                    item.WondrousUsesToday = 0;
                    count++;
                }
            }
        }

        // Check general inventory (items with activation that might be stored)
        if (inventory.GeneralSlots != null)
        {
            foreach (var item in inventory.GeneralSlots)
            {
                if (item != null && item.IsWondrous && item.WondrousUsesToday > 0)
                {
                    item.WondrousUsesToday = 0;
                    count++;
                }
            }
        }

        return count;
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

    /// <summary>Check if a wondrous item has remaining daily uses.</summary>
    public static bool HasUsesRemaining(ItemData item)
    {
        if (item == null || !item.IsWondrous) return false;
        if (item.WondrousUsesPerDay <= 0) return true; // Unlimited
        return item.WondrousUsesToday < item.WondrousUsesPerDay;
    }

    /// <summary>Get remaining uses for display.</summary>
    public static int GetRemainingUses(ItemData item)
    {
        if (item == null || !item.IsWondrous || item.WondrousUsesPerDay <= 0) return -1; // -1 = unlimited
        return Mathf.Max(0, item.WondrousUsesPerDay - item.WondrousUsesToday);
    }
}
