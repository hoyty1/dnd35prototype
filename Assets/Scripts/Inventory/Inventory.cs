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
    /// Handles D&D 3.5 armor properties: Max Dex Bonus, Armor Check Penalty, Arcane Spell Failure.
    /// Also enforces two-handed weapon restrictions (clears off-hand if main weapon is two-handed).
    /// </summary>
    public void RecalculateStats()
    {
        if (OwnerStats == null) return;

        // --- Two-Handed Weapon Enforcement ---
        // If the right hand weapon is two-handed, the left hand must be empty
        if (RightHandSlot != null && RightHandSlot.IsWeapon && RightHandSlot.IsTwoHanded && LeftHandSlot != null)
        {
            // Move left hand item to inventory
            AddItem(LeftHandSlot);
            LeftHandSlot = null;
        }
        // If the left hand weapon is two-handed, the right hand must be empty
        if (LeftHandSlot != null && LeftHandSlot.IsWeapon && LeftHandSlot.IsTwoHanded && RightHandSlot != null)
        {
            AddItem(RightHandSlot);
            RightHandSlot = null;
        }

        // --- Armor Bonus & Properties ---
        OwnerStats.ArmorBonus = ArmorSlot != null ? ArmorSlot.ArmorBonus : 0;

        // Max Dex Bonus: use the most restrictive (lowest non-negative) from armor
        // -1 means no limit; armor sets a cap; shield doesn't usually limit max dex (except tower shield)
        int armorMaxDex = -1;
        int shieldMaxDex = -1;

        if (ArmorSlot != null)
        {
            armorMaxDex = ArmorSlot.MaxDexBonus; // already -1 if no limit
        }

        // --- Shield Bonus & Properties ---
        OwnerStats.ShieldBonus = 0;
        if (LeftHandSlot != null && LeftHandSlot.IsShield)
        {
            OwnerStats.ShieldBonus = LeftHandSlot.ShieldBonus;
            shieldMaxDex = LeftHandSlot.MaxDexBonus;
        }

        // Compute effective Max Dex Bonus (most restrictive / lowest non-negative)
        if (armorMaxDex >= 0 && shieldMaxDex >= 0)
            OwnerStats.MaxDexBonus = Mathf.Min(armorMaxDex, shieldMaxDex);
        else if (armorMaxDex >= 0)
            OwnerStats.MaxDexBonus = armorMaxDex;
        else if (shieldMaxDex >= 0)
            OwnerStats.MaxDexBonus = shieldMaxDex;
        else
            OwnerStats.MaxDexBonus = -1; // No limit

        // --- Armor Check Penalty (sum of armor + shield) ---
        int totalACP = 0;
        if (ArmorSlot != null)
            totalACP += ArmorSlot.ArmorCheckPenalty;
        if (LeftHandSlot != null && LeftHandSlot.IsShield)
            totalACP += LeftHandSlot.ArmorCheckPenalty;
        OwnerStats.ArmorCheckPenalty = totalACP;

        // --- Arcane Spell Failure (sum of armor + shield) ---
        int totalASF = 0;
        if (ArmorSlot != null)
            totalASF += ArmorSlot.ArcaneSpellFailure;
        if (LeftHandSlot != null && LeftHandSlot.IsShield)
            totalASF += LeftHandSlot.ArcaneSpellFailure;
        OwnerStats.ArcaneSpellFailure = totalASF;

        // --- Weapon Stats ---
        // Primary weapon from right hand
        if (RightHandSlot != null && RightHandSlot.IsWeapon)
        {
            ApplyWeaponStats(RightHandSlot);
        }
        else if (LeftHandSlot != null && LeftHandSlot.IsWeapon)
        {
            // Fallback: weapon in left hand only
            ApplyWeaponStats(LeftHandSlot);
        }
        else
        {
            // Unarmed: 1d3, 20/×2, bludgeoning
            OwnerStats.BaseDamageDice = 3;
            OwnerStats.BaseDamageCount = 1;
            OwnerStats.BonusDamage = 0;
            OwnerStats.AttackRange = 1;
            OwnerStats.CritThreatMin = 20;
            OwnerStats.CritMultiplier = 2;
        }
    }

    /// <summary>Apply weapon stats from an ItemData to OwnerStats.</summary>
    private void ApplyWeaponStats(ItemData weapon)
    {
        OwnerStats.BaseDamageDice = weapon.DamageDice;
        OwnerStats.BaseDamageCount = weapon.DamageCount;
        OwnerStats.BonusDamage = weapon.BonusDamage;
        OwnerStats.AttackRange = weapon.AttackRange;
        OwnerStats.CritThreatMin = weapon.CritThreatMin > 0 ? weapon.CritThreatMin : 20;
        OwnerStats.CritMultiplier = weapon.CritMultiplier > 0 ? weapon.CritMultiplier : 2;
    }

    /// <summary>
    /// Check if dual wielding is possible with current equipment.
    /// Two-handed weapons cannot be dual-wielded.
    /// </summary>
    public bool CanDualWield()
    {
        if (RightHandSlot == null || !RightHandSlot.IsWeapon) return false;
        if (LeftHandSlot == null || !LeftHandSlot.IsWeapon) return false;
        if (RightHandSlot.IsTwoHanded || LeftHandSlot.IsTwoHanded) return false;
        return true;
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
