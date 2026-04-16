using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Per-character inventory with D&D 3.5e equipment slots and 20 general slots.
/// Manages equipping/unequipping and stat recalculation.
/// </summary>
[System.Serializable]
public class Inventory
{
    public const int GeneralSlotCount = 20;

    // Equipment slots (D&D 3.5e + combat hand slots)
    public ItemData HeadSlot;
    public ItemData FaceEyesSlot;
    public ItemData NeckSlot;
    public ItemData TorsoSlot;

    [FormerlySerializedAs("ArmorSlot")]
    public ItemData ArmorRobeSlot;

    public ItemData WaistSlot;
    public ItemData BackSlot;
    public ItemData WristsSlot;
    public ItemData HandsSlot;
    public ItemData LeftRingSlot;
    public ItemData RightRingSlot;
    public ItemData FeetSlot;

    // Combat hand slots kept for weapon/shield systems.
    public ItemData LeftHandSlot;
    public ItemData RightHandSlot;

    /// <summary>
    /// Legacy alias used across older systems/tests.
    /// Kept to avoid breaking callers while internally using ArmorRobeSlot.
    /// </summary>
    public ItemData ArmorSlot
    {
        get => ArmorRobeSlot;
        set => ArmorRobeSlot = value;
    }

    public static readonly EquipSlot[] AllEquipmentSlots =
    {
        EquipSlot.Head,
        EquipSlot.FaceEyes,
        EquipSlot.Neck,
        EquipSlot.Torso,
        EquipSlot.ArmorRobe,
        EquipSlot.Waist,
        EquipSlot.Back,
        EquipSlot.Wrists,
        EquipSlot.Hands,
        EquipSlot.LeftRing,
        EquipSlot.RightRing,
        EquipSlot.Feet,
        EquipSlot.LeftHand,
        EquipSlot.RightHand
    };

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
            case EquipSlot.Head: return HeadSlot;
            case EquipSlot.FaceEyes: return FaceEyesSlot;
            case EquipSlot.Neck: return NeckSlot;
            case EquipSlot.Torso: return TorsoSlot;
            case EquipSlot.Armor:
            case EquipSlot.ArmorRobe: return ArmorRobeSlot;
            case EquipSlot.Waist: return WaistSlot;
            case EquipSlot.Back: return BackSlot;
            case EquipSlot.Wrists: return WristsSlot;
            case EquipSlot.Hands: return HandsSlot;
            case EquipSlot.LeftRing: return LeftRingSlot;
            case EquipSlot.RightRing: return RightRingSlot;
            case EquipSlot.Feet: return FeetSlot;
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
        if (!item.CanEquipIn(slot)) return;
        SetEquipSlot(slot, item);
    }

    private void SetEquipSlot(EquipSlot slot, ItemData item)
    {
        switch (slot)
        {
            case EquipSlot.Head: HeadSlot = item; break;
            case EquipSlot.FaceEyes: FaceEyesSlot = item; break;
            case EquipSlot.Neck: NeckSlot = item; break;
            case EquipSlot.Torso: TorsoSlot = item; break;
            case EquipSlot.Armor:
            case EquipSlot.ArmorRobe: ArmorRobeSlot = item; break;
            case EquipSlot.Waist: WaistSlot = item; break;
            case EquipSlot.Back: BackSlot = item; break;
            case EquipSlot.Wrists: WristsSlot = item; break;
            case EquipSlot.Hands: HandsSlot = item; break;
            case EquipSlot.LeftRing: LeftRingSlot = item; break;
            case EquipSlot.RightRing: RightRingSlot = item; break;
            case EquipSlot.Feet: FeetSlot = item; break;
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
        OwnerStats.ArmorBonus = ArmorRobeSlot != null ? ArmorRobeSlot.ArmorBonus : 0;

        // Max Dex Bonus: only armor limits DEX-to-AC in this prototype's D&D 3.5 implementation.
        // -1 means no limit (no armor equipped).
        int armorMaxDex = -1;
        if (ArmorRobeSlot != null)
            armorMaxDex = ArmorRobeSlot.MaxDexBonus;

        // --- Shield Bonus & Properties ---
        OwnerStats.ShieldBonus = 0;
        if (LeftHandSlot != null && LeftHandSlot.IsShield)
            OwnerStats.ShieldBonus = LeftHandSlot.ShieldBonus;

        // Runtime equipped-item references for proficiency/ACP calculations
        OwnerStats.EquippedArmorItem = ArmorRobeSlot;
        OwnerStats.EquippedShieldItem = (LeftHandSlot != null && LeftHandSlot.IsShield) ? LeftHandSlot : null;

        // Effective Max Dex cap comes from armor only.
        OwnerStats.MaxDexBonus = armorMaxDex >= 0 ? armorMaxDex : -1;

        // --- Armor Check Penalty (sum of armor + shield) ---
        int totalACP = 0;
        if (ArmorRobeSlot != null)
            totalACP += ArmorRobeSlot.ArmorCheckPenalty;
        if (LeftHandSlot != null && LeftHandSlot.IsShield)
            totalACP += LeftHandSlot.ArmorCheckPenalty;
        OwnerStats.ArmorCheckPenalty = totalACP;

        // --- Arcane Spell Failure (sum of armor + shield) ---
        int totalASF = 0;
        if (ArmorRobeSlot != null)
            totalASF += ArmorRobeSlot.ArcaneSpellFailure;
        if (LeftHandSlot != null && LeftHandSlot.IsShield)
            totalASF += LeftHandSlot.ArcaneSpellFailure;
        OwnerStats.ArcaneSpellFailure = totalASF;

        // --- Weapon Stats ---
        // Primary weapon from right hand
        if (RightHandSlot != null && RightHandSlot.IsWeapon)
        {
            OwnerStats.EquippedMainWeaponItem = RightHandSlot;
            ApplyWeaponStats(RightHandSlot);
        }
        else if (LeftHandSlot != null && LeftHandSlot.IsWeapon)
        {
            // Fallback: weapon in left hand only
            OwnerStats.EquippedMainWeaponItem = LeftHandSlot;
            ApplyWeaponStats(LeftHandSlot);
        }
        else
        {
            OwnerStats.EquippedMainWeaponItem = null;
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

        if (weapon.WeaponCat == WeaponCategory.Melee)
            OwnerStats.AttackRange = Mathf.Max(1, weapon.ReachSquares > 0 ? weapon.ReachSquares : weapon.AttackRange);
        else
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
