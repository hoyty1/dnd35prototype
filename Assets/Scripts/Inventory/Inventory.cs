using System;
using UnityEngine;

/// <summary>
/// Per-character inventory with 3 equipment slots and 20 general slots.
/// Manages equipping/unequipping and stat recalculation.
/// </summary>
[System.Serializable]
public class Inventory
{
    public const int GeneralSlotCount = 20;

    // Equipment slots
    public ItemData ArmorSlot;      // Chest armor
    public ItemData LeftHandSlot;   // Shield or weapon
    public ItemData RightHandSlot;  // Weapon

    // General inventory
    public ItemData[] GeneralSlots;

    // Reference to the owning character's stats for recalculation
    [NonSerialized] public CharacterStats OwnerStats;

    public Inventory()
    {
        GeneralSlots = new ItemData[GeneralSlotCount];
    }

    /// <summary>Try to add an item to the first empty general slot. Returns true on success.</summary>
    public bool AddItem(ItemData item)
    {
        if (item == null) return false;
        for (int i = 0; i < GeneralSlots.Length; i++)
        {
            if (GeneralSlots[i] == null)
            {
                GeneralSlots[i] = item;
                return true;
            }
        }
        return false; // Inventory full
    }

    /// <summary>Remove an item from a general slot index. Returns the item.</summary>
    public ItemData RemoveItemAt(int index)
    {
        if (index < 0 || index >= GeneralSlots.Length) return null;
        var item = GeneralSlots[index];
        GeneralSlots[index] = null;
        return item;
    }

    /// <summary>Get the equipped item in a given slot.</summary>
    public ItemData GetEquipped(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Armor: return ArmorSlot;
            case EquipSlot.LeftHand: return LeftHandSlot;
            case EquipSlot.RightHand: return RightHandSlot;
            default: return null;
        }
    }

    /// <summary>
    /// Equip an item from general inventory slot to an equipment slot.
    /// Swaps if something is already equipped.
    /// Returns true on success.
    /// </summary>
    public bool EquipFromInventory(int generalIndex, EquipSlot targetSlot)
    {
        if (generalIndex < 0 || generalIndex >= GeneralSlots.Length) return false;
        var item = GeneralSlots[generalIndex];
        if (item == null) return false;
        if (!item.CanEquipIn(targetSlot)) return false;

        // Get current equipped item in the target slot
        ItemData currentEquipped = GetEquipped(targetSlot);

        // Place the new item in the equipment slot
        SetEquipSlot(targetSlot, item);

        // Put the old equipped item (if any) into the general slot
        GeneralSlots[generalIndex] = currentEquipped; // may be null (empty swap)

        RecalculateStats();
        return true;
    }

    /// <summary>
    /// Unequip an item from an equipment slot back to inventory.
    /// Returns true on success (fails if inventory is full).
    /// </summary>
    public bool Unequip(EquipSlot slot)
    {
        ItemData item = GetEquipped(slot);
        if (item == null) return false;

        // Find an empty general slot
        int emptyIndex = -1;
        for (int i = 0; i < GeneralSlots.Length; i++)
        {
            if (GeneralSlots[i] == null)
            {
                emptyIndex = i;
                break;
            }
        }
        if (emptyIndex == -1) return false; // No room

        GeneralSlots[emptyIndex] = item;
        SetEquipSlot(slot, null);

        RecalculateStats();
        return true;
    }

    /// <summary>
    /// Directly equip an item (used during character setup).
    /// Does NOT put it in general inventory first.
    /// </summary>
    public void DirectEquip(ItemData item, EquipSlot slot)
    {
        if (item == null) return;
        SetEquipSlot(slot, item);
    }

    private void SetEquipSlot(EquipSlot slot, ItemData item)
    {
        switch (slot)
        {
            case EquipSlot.Armor: ArmorSlot = item; break;
            case EquipSlot.LeftHand: LeftHandSlot = item; break;
            case EquipSlot.RightHand: RightHandSlot = item; break;
        }
    }

    /// <summary>
    /// Recalculate the owner's derived stats based on equipped items.
    /// </summary>
    public void RecalculateStats()
    {
        if (OwnerStats == null) return;

        // Armor bonus
        OwnerStats.ArmorBonus = ArmorSlot != null ? ArmorSlot.ArmorBonus : 0;

        // Shield bonus
        OwnerStats.ShieldBonus = 0;
        if (LeftHandSlot != null && LeftHandSlot.IsShield)
            OwnerStats.ShieldBonus = LeftHandSlot.ShieldBonus;

        // Weapon stats from right hand (primary weapon)
        if (RightHandSlot != null && RightHandSlot.IsWeapon)
        {
            OwnerStats.BaseDamageDice = RightHandSlot.DamageDice;
            OwnerStats.BaseDamageCount = RightHandSlot.DamageCount;
            OwnerStats.BonusDamage = RightHandSlot.BonusDamage;
            OwnerStats.AttackRange = RightHandSlot.AttackRange;
            OwnerStats.CritThreatMin = RightHandSlot.CritThreatMin > 0 ? RightHandSlot.CritThreatMin : 20;
            OwnerStats.CritMultiplier = RightHandSlot.CritMultiplier > 0 ? RightHandSlot.CritMultiplier : 2;
        }
        else if (LeftHandSlot != null && LeftHandSlot.IsWeapon)
        {
            // Fallback: weapon in left hand
            OwnerStats.BaseDamageDice = LeftHandSlot.DamageDice;
            OwnerStats.BaseDamageCount = LeftHandSlot.DamageCount;
            OwnerStats.BonusDamage = LeftHandSlot.BonusDamage;
            OwnerStats.AttackRange = LeftHandSlot.AttackRange;
            OwnerStats.CritThreatMin = LeftHandSlot.CritThreatMin > 0 ? LeftHandSlot.CritThreatMin : 20;
            OwnerStats.CritMultiplier = LeftHandSlot.CritMultiplier > 0 ? LeftHandSlot.CritMultiplier : 2;
        }
        else
        {
            // Unarmed: 20/×2
            OwnerStats.BaseDamageDice = 3; // 1d3 unarmed
            OwnerStats.BaseDamageCount = 1;
            OwnerStats.BonusDamage = 0;
            OwnerStats.AttackRange = 1;
            OwnerStats.CritThreatMin = 20;
            OwnerStats.CritMultiplier = 2;
        }
    }

    /// <summary>Count how many general slots are occupied.</summary>
    public int ItemCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < GeneralSlots.Length; i++)
                if (GeneralSlots[i] != null) count++;
            return count;
        }
    }

    /// <summary>Count empty general slots.</summary>
    public int EmptySlots => GeneralSlotCount - ItemCount;
}
