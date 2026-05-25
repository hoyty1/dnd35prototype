using System;
using UnityEngine;
using UnityEngine.Serialization;
using DND35e.Identifiers;

/// <summary>
/// Per-character inventory with D&D 3.5e equipment slots and dynamically growing general slots.
/// Manages equipping/unequipping and stat recalculation.
/// </summary>
[System.Serializable]
public class Inventory
{
    public const int GeneralSlotCount = 20; // Initial/default visible capacity for legacy UIs.
    private const int GeneralSlotGrowthStep = 20;

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

    // Slotless wondrous items (Ioun Stones, Bags of Holding, etc.)
    // Multiple slotless items can be equipped simultaneously.
    public System.Collections.Generic.List<ItemData> SlotlessItems = new System.Collections.Generic.List<ItemData>();
    public const int MaxSlotlessItems = 10; // Reasonable cap for slotless items

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
    [NonSerialized] public CharacterController OwnerCharacter;
    [NonSerialized] private bool _isRecalculating;

    public Inventory()
    {
        GeneralSlots = new ItemData[GeneralSlotCount];
    }

    /// <summary>Try to add an item to the first empty general slot. Grows capacity when needed.
    /// For stackable items (scrolls, potions), merges into an existing stack of the same item ID first.</summary>
    public bool AddItem(ItemData item)
    {
        if (item == null) return false;

        // Try stacking with existing identical stackable items first
        if (item.IsStackable && !string.IsNullOrWhiteSpace(item.Id))
        {
            int addCount = Mathf.Max(1, item.StackCount);
            int remaining = addCount;

            // First pass: try to merge into existing stacks
            for (int i = 0; i < GeneralSlots.Length && remaining > 0; i++)
            {
                ItemData existing = GeneralSlots[i];
                if (existing == null || !existing.IsStackable) continue;
                if (!string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase)) continue;

                int maxStack = Mathf.Max(1, existing.MaxStackSize);
                int currentCount = Mathf.Max(1, existing.StackCount);
                int space = maxStack - currentCount;
                if (space <= 0) continue;

                int toAdd = Mathf.Min(remaining, space);
                existing.StackCount = currentCount + toAdd;
                remaining -= toAdd;
            }

            if (remaining <= 0)
            {
                if (!_isRecalculating) RecalculateStats();
                return true;
            }

            // Create new stack(s) for the remainder
            while (remaining > 0)
            {
                int emptyIdx = FindFirstEmptyGeneralSlotIndex();
                if (emptyIdx < 0)
                {
                    EnsureGeneralSlotCapacity(Mathf.Max(GeneralSlots.Length + GeneralSlotGrowthStep, GeneralSlots.Length + 1));
                    emptyIdx = FindFirstEmptyGeneralSlotIndex();
                }
                if (emptyIdx < 0) break;

                int maxStack = Mathf.Max(1, item.MaxStackSize);
                int stackSize = Mathf.Min(remaining, maxStack);
                ItemData newStack = ItemDatabase.CloneItem(item.Id);
                if (newStack == null)
                {
                    // Fallback: use the item directly
                    item.StackCount = stackSize;
                    GeneralSlots[emptyIdx] = item;
                    remaining -= stackSize;
                    break;
                }
                newStack.StackCount = stackSize;
                GeneralSlots[emptyIdx] = newStack;
                remaining -= stackSize;
            }

            if (!_isRecalculating) RecalculateStats();
            return remaining <= 0;
        }

        // Non-stackable: original behavior
        int emptyIndex = FindFirstEmptyGeneralSlotIndex();
        if (emptyIndex < 0)
        {
            EnsureGeneralSlotCapacity(Mathf.Max(GeneralSlots.Length + GeneralSlotGrowthStep, GeneralSlots.Length + 1));
            emptyIndex = FindFirstEmptyGeneralSlotIndex();
        }

        if (emptyIndex < 0)
            return false;

        GeneralSlots[emptyIndex] = item;
        if (!_isRecalculating) RecalculateStats();
        return true;
    }

    /// <summary>Remove an item from a general slot index. Returns the item.</summary>
    public ItemData RemoveItemAt(int index)
    {
        if (index < 0 || index >= GeneralSlots.Length) return null;
        var item = GeneralSlots[index];
        GeneralSlots[index] = null;
        if (!_isRecalculating) RecalculateStats();
        return item;
    }

    /// <summary>
    /// Removes a specific item instance from all inventory locations (equipped + general slots).
    /// Returns true when at least one reference to the item was removed.
    /// </summary>
    public bool RemoveItem(ItemData item)
    {
        if (item == null)
            return false;

        bool removed = false;

        if (ReferenceEquals(HeadSlot, item)) { HeadSlot = null; removed = true; }
        if (ReferenceEquals(FaceEyesSlot, item)) { FaceEyesSlot = null; removed = true; }
        if (ReferenceEquals(NeckSlot, item)) { NeckSlot = null; removed = true; }
        if (ReferenceEquals(TorsoSlot, item)) { TorsoSlot = null; removed = true; }
        if (ReferenceEquals(ArmorRobeSlot, item)) { ArmorRobeSlot = null; removed = true; }
        if (ReferenceEquals(WaistSlot, item)) { WaistSlot = null; removed = true; }
        if (ReferenceEquals(BackSlot, item)) { BackSlot = null; removed = true; }
        if (ReferenceEquals(WristsSlot, item)) { WristsSlot = null; removed = true; }
        if (ReferenceEquals(HandsSlot, item)) { HandsSlot = null; removed = true; }
        if (ReferenceEquals(LeftRingSlot, item)) { LeftRingSlot = null; removed = true; }
        if (ReferenceEquals(RightRingSlot, item)) { RightRingSlot = null; removed = true; }
        if (ReferenceEquals(FeetSlot, item)) { FeetSlot = null; removed = true; }
        if (ReferenceEquals(LeftHandSlot, item)) { LeftHandSlot = null; removed = true; }
        if (ReferenceEquals(RightHandSlot, item)) { RightHandSlot = null; removed = true; }

        for (int i = 0; i < GeneralSlots.Length; i++)
        {
            if (ReferenceEquals(GeneralSlots[i], item))
            {
                GeneralSlots[i] = null;
                removed = true;
            }
        }

        if (removed && !_isRecalculating)
            RecalculateStats();

        return removed;
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
    /// Returns true on success.
    /// </summary>
    public bool Unequip(EquipSlot slot)
    {
        ItemData item = GetEquipped(slot);
        if (item == null) return false;

        int emptyIndex = FindFirstEmptyGeneralSlotIndex();
        if (emptyIndex < 0)
        {
            EnsureGeneralSlotCapacity(Mathf.Max(GeneralSlots.Length + GeneralSlotGrowthStep, GeneralSlots.Length + 1));
            emptyIndex = FindFirstEmptyGeneralSlotIndex();
        }

        if (emptyIndex < 0)
            return false;

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
        if (slot == EquipSlot.Slotless)
        {
            EquipSlotless(item);
            return;
        }
        if (!item.CanEquipIn(slot)) return;
        SetEquipSlot(slot, item);
        if (!_isRecalculating) RecalculateStats();
    }

    // ════════════════════════════════════════════════════════════
    //  Slotless Item Management
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Equip a slotless wondrous item. Multiple slotless items can be equipped.
    /// Returns true on success.
    /// </summary>
    public bool EquipSlotless(ItemData item)
    {
        if (item == null) return false;
        if (item.Slot != EquipSlot.Slotless && !item.IsSlotless) return false;
        if (SlotlessItems.Count >= MaxSlotlessItems)
        {
            Debug.LogWarning($"[Inventory] Cannot equip slotless item '{item.Name}' — maximum of {MaxSlotlessItems} reached.");
            return false;
        }

        item.EnsureDurabilityInitialized();
        SlotlessItems.Add(item);

        if (item.IsWondrous && OwnerCharacter != null)
            WondrousItemActivation.OnWondrousEquipped(OwnerCharacter, item);

        if (!_isRecalculating) RecalculateStats();
        return true;
    }

    /// <summary>
    /// Unequip a slotless item by index, moving it to general inventory.
    /// Returns true on success.
    /// </summary>
    public bool UnequipSlotless(int index)
    {
        if (index < 0 || index >= SlotlessItems.Count) return false;

        ItemData item = SlotlessItems[index];
        int emptyIndex = FindFirstEmptyGeneralSlotIndex();
        if (emptyIndex < 0)
        {
            EnsureGeneralSlotCapacity(UnityEngine.Mathf.Max(GeneralSlots.Length + GeneralSlotGrowthStep, GeneralSlots.Length + 1));
            emptyIndex = FindFirstEmptyGeneralSlotIndex();
        }
        if (emptyIndex < 0) return false;

        GeneralSlots[emptyIndex] = item;
        SlotlessItems.RemoveAt(index);

        if (item.IsWondrous && OwnerCharacter != null)
            WondrousItemActivation.OnWondrousUnequipped(OwnerCharacter, item);

        RecalculateStats();
        return true;
    }

    /// <summary>
    /// Equip a slotless item from general inventory.
    /// Returns true on success.
    /// </summary>
    public bool EquipSlotlessFromInventory(int generalIndex)
    {
        if (generalIndex < 0 || generalIndex >= GeneralSlots.Length) return false;
        var item = GeneralSlots[generalIndex];
        if (item == null) return false;
        if (item.Slot != EquipSlot.Slotless && !item.IsSlotless) return false;
        if (SlotlessItems.Count >= MaxSlotlessItems) return false;

        GeneralSlots[generalIndex] = null;
        return EquipSlotless(item);
    }

    /// <summary>Get all currently equipped slotless items.</summary>
    public System.Collections.Generic.List<ItemData> GetEquippedSlotlessItems()
    {
        return SlotlessItems;
    }

    private void SetEquipSlot(EquipSlot slot, ItemData item)
    {
        if (item != null)
            item.EnsureDurabilityInitialized();

        // --- Sprint 2: Ring equip/unequip hooks for active ring abilities ---
        if (slot == EquipSlot.LeftRing || slot == EquipSlot.RightRing)
        {
            ItemData previousRing = (slot == EquipSlot.LeftRing) ? LeftRingSlot : RightRingSlot;
            if (previousRing != null && previousRing.HasActiveRingAbility && OwnerCharacter != null)
            {
                RingActivationManager.OnRingUnequipped(OwnerCharacter, previousRing);
            }
        }

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

        // --- Sprint 2: Notify new ring of equip (e.g. Ring of Spell Turning auto-applies) ---
        if ((slot == EquipSlot.LeftRing || slot == EquipSlot.RightRing) && item != null && item.HasActiveRingAbility && OwnerCharacter != null)
        {
            RingActivationManager.OnRingEquipped(OwnerCharacter, item);
        }

        // --- Wondrous item equip/unequip hooks ---
        if (slot != EquipSlot.LeftRing && slot != EquipSlot.RightRing &&
            slot != EquipSlot.LeftHand && slot != EquipSlot.RightHand)
        {
            // Check if previous item was wondrous
            ItemData previousItem = null;
            switch (slot)
            {
                case EquipSlot.Head: previousItem = (item != HeadSlot) ? null : previousItem; break;
                // Previous was already replaced above; hook fires on the old item from caller context
            }
            // Notify new wondrous item of equip
            if (item != null && item.IsWondrous && OwnerCharacter != null)
            {
                WondrousItemActivation.OnWondrousEquipped(OwnerCharacter, item);
            }
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
        if (_isRecalculating) return;

        _isRecalculating = true;
        try
        {
            // --- Two-Handed Weapon Enforcement ---
            // If a two-handed weapon is equipped in one hand, the opposite hand must be empty.
            if (RightHandSlot != null && RightHandSlot.IsWeapon && RightHandSlot.IsTwoHanded && LeftHandSlot != null)
            {
                ItemData displaced = LeftHandSlot;
                LeftHandSlot = null;
                if (!AddItem(displaced))
                    LeftHandSlot = displaced;
            }

            if (LeftHandSlot != null && LeftHandSlot.IsWeapon && LeftHandSlot.IsTwoHanded && RightHandSlot != null)
            {
                ItemData displaced = RightHandSlot;
                RightHandSlot = null;
                if (!AddItem(displaced))
                    RightHandSlot = displaced;
            }

            // --- Armor Bonus & Properties ---
            OwnerStats.ArmorBonus = ArmorRobeSlot != null ? ArmorRobeSlot.GetTotalArmorBonus() : 0;

            // Max Dex cap from armor only (-1 means no limit).
            // D&D 3.5e: Mithral increases max Dex bonus by +2.
            int armorMaxDex = -1;
            if (ArmorRobeSlot != null)
                armorMaxDex = ArmorRobeSlot.EffectiveMaxDexBonus;

            // --- Shield Bonus & Properties ---
            OwnerStats.ShieldBonus = 0;
            if (LeftHandSlot != null && LeftHandSlot.IsShield)
                OwnerStats.ShieldBonus = LeftHandSlot.GetTotalShieldBonus();

            // Runtime equipped-item references for proficiency/ACP calculations
            OwnerStats.EquippedArmorItem = ArmorRobeSlot;
            OwnerStats.EquippedShieldItem = (LeftHandSlot != null && LeftHandSlot.IsShield) ? LeftHandSlot : null;

            // --- Encumbrance from total carried weight ---
            float totalWeight = GetTotalCarriedWeightLbs();
            float maxCarry = CharacterStats.GetHeavyLoadForStrength(OwnerStats.EffectiveStrengthScore);
            EncumbranceLevel encumbrance = CharacterStats.GetEncumbranceLevel(totalWeight, maxCarry);
            int encDexCap = CharacterStats.GetEncumbranceDexCap(encumbrance);
            int encAcp = CharacterStats.GetEncumbranceCheckPenalty(encumbrance);

            OwnerStats.TotalCarriedWeightLbs = totalWeight;
            OwnerStats.MaxCarryWeightLbs = maxCarry;
            OwnerStats.CurrentEncumbrance = encumbrance;
            OwnerStats.EncumbranceMaxDexBonus = encDexCap;
            OwnerStats.EncumbranceCheckPenalty = encAcp;

            // Effective Max Dex cap is the most restrictive between armor and encumbrance caps.
            OwnerStats.EquipmentMaxDexBonus = armorMaxDex >= 0 ? armorMaxDex : -1;
            OwnerStats.MaxDexBonus = CharacterStats.CombineMostRestrictiveMaxDex(OwnerStats.EquipmentMaxDexBonus, encDexCap);

            // --- Armor Check Penalty ---
            // Effective ACP is the most restrictive between armor/shield ACP and encumbrance ACP.
            // D&D 3.5e: Masterwork reduces ACP by 1, Mithral reduces ACP by 3 (via EffectiveArmorCheckPenalty).
            int totalACP = 0;
            if (ArmorRobeSlot != null)
                totalACP += ArmorRobeSlot.EffectiveArmorCheckPenalty;
            if (LeftHandSlot != null && LeftHandSlot.IsShield)
                totalACP += LeftHandSlot.EffectiveArmorCheckPenalty;
            OwnerStats.EquipmentArmorCheckPenalty = totalACP;
            OwnerStats.ArmorCheckPenalty = Mathf.Max(totalACP, encAcp);

            // --- Arcane Spell Failure (sum of armor + shield) ---
            // D&D 3.5e: Mithral reduces ASF by 10%.
            int totalASF = 0;
            if (ArmorRobeSlot != null)
                totalASF += ArmorRobeSlot.EffectiveArcaneSpellFailure;
            if (LeftHandSlot != null && LeftHandSlot.IsShield)
                totalASF += LeftHandSlot.EffectiveArcaneSpellFailure;
            OwnerStats.ArcaneSpellFailure = totalASF;

            // --- Adamantine Armor DR ---
            // D&D 3.5e DMG p.283: Adamantine armor grants DR 1/— (light), 2/— (medium), 3/— (heavy).
            // Applied as a special DR entry on the character.
            ApplyAdamantineArmorDR(ArmorRobeSlot);

            // --- Ring Bonuses (D&D 3.5e DMG pp. 229–233) ---
            // Reset all ring-derived stats, then re-apply from both ring slots.
            // Same bonus type from two rings does NOT stack (use highest per D&D 3.5e stacking rules).
            ResetRingBonuses();
            ApplyRingBonuses(LeftRingSlot);
            ApplyRingBonuses(RightRingSlot);

            // Apply ring deflection bonus to AC (stacks with highest only — use max with spell deflection)
            if (OwnerStats.RingForceShieldBonus > 0)
            {
                // Ring of Force Shield: shield bonus that does NOT stack with physical shield
                // Use the higher of physical shield or ring shield
                OwnerStats.ShieldBonus = Mathf.Max(OwnerStats.ShieldBonus, OwnerStats.RingForceShieldBonus);
            }

            // --- Wondrous Item Bonuses (D&D 3.5e DMG pp. 248–271) ---
            // Apply bonuses from all equipped wondrous items in body slots and slotless items.
            ApplyAllWondrousItemBonuses();

            // --- Weapon Stats ---
            // Primary weapon from right hand, then left hand.
            // If neither hand has a weapon, allow spiked gauntlet in Hands slot as primary attack option.
            if (RightHandSlot != null && RightHandSlot.IsWeapon)
            {
                OwnerStats.EquippedMainWeaponItem = RightHandSlot;
                ApplyWeaponStats(RightHandSlot);
            }
            else if (LeftHandSlot != null && LeftHandSlot.IsWeapon)
            {
                OwnerStats.EquippedMainWeaponItem = LeftHandSlot;
                ApplyWeaponStats(LeftHandSlot);
            }
            else if (IsSpikedGauntletItem(HandsSlot))
            {
                OwnerStats.EquippedMainWeaponItem = HandsSlot;
                ApplyWeaponStats(HandsSlot);
            }
            else
            {
                OwnerStats.EquippedMainWeaponItem = null;

                NaturalAttackDefinition primaryNaturalAttack = OwnerStats.GetPrimaryNaturalAttack();
                if (primaryNaturalAttack != null)
                {
                    OwnerStats.GetScaledNaturalAttackDamage(primaryNaturalAttack, out int naturalDamageCount, out int naturalDamageDice);
                    OwnerStats.BaseDamageDice = naturalDamageDice;
                    OwnerStats.BaseDamageCount = naturalDamageCount;
                    OwnerStats.BonusDamage = OwnerStats.GetNaturalAttackDamageBonus(primaryNaturalAttack) - OwnerStats.STRMod;
                    OwnerStats.AttackRange = Mathf.Max(1, primaryNaturalAttack.Range);
                }
                else
                {
                    // Unarmed: 1d3, 20/×2, bludgeoning
                    OwnerStats.BaseDamageDice = 3;
                    OwnerStats.BaseDamageCount = 1;
                    OwnerStats.BonusDamage = 0;
                    OwnerStats.AttackRange = 1;
                }

                OwnerStats.CritThreatMin = 20;
                OwnerStats.CritMultiplier = 2;
            }

            OwnerCharacter?.RefreshEquipmentTags();
        }
        finally
        {
            _isRecalculating = false;
        }
    }

    /// <summary>Apply weapon stats from an ItemData to OwnerStats.</summary>
    private void ApplyWeaponStats(ItemData weapon)
    {
        weapon.GetScaledDamageDice(OwnerStats.CurrentSizeCategory, out int scaledDamageCount, out int scaledDamageDice);
        OwnerStats.BaseDamageDice = scaledDamageDice;
        OwnerStats.BaseDamageCount = scaledDamageCount;
        OwnerStats.BonusDamage = weapon.BonusDamage;

        if (weapon.WeaponCat == WeaponCategory.Melee)
            OwnerStats.AttackRange = Mathf.Max(1, weapon.ReachSquares > 0 ? weapon.ReachSquares : weapon.AttackRange);
        else
            OwnerStats.AttackRange = weapon.AttackRange;

        int baseThreatMin = weapon.CritThreatMin > 0 ? weapon.CritThreatMin : 20;
        // Apply CritThreatRangeModifier from active spell effects (e.g. Keen Edge)
        if (weapon.ActiveSpellEffects != null)
        {
            foreach (var eff in weapon.ActiveSpellEffects)
            {
                if (eff != null && eff.CritThreatRangeModifier != 0)
                    baseThreatMin += eff.CritThreatRangeModifier;
            }
        }
        OwnerStats.CritThreatMin = Mathf.Clamp(baseThreatMin, 2, 20);
        OwnerStats.CritMultiplier = weapon.CritMultiplier > 0 ? weapon.CritMultiplier : 2;
    }

    private static bool IsSpikedGauntletItem(ItemData item)
    {
        if (item == null)
            return false;

        string id = (item.Id ?? string.Empty).ToLowerInvariant();
        string name = (item.Name ?? string.Empty).ToLowerInvariant();
        return id == ItemIDs.SPIKED_GAUNTLET || name.Contains("spiked gauntlet");
    }

    /// <summary>Total carried weight from all equipped items and inventory contents.</summary>
    public float GetTotalCarriedWeightLbs()
    {
        float total = 0f;

        foreach (EquipSlot slot in AllEquipmentSlots)
        {
            ItemData equipped = GetEquipped(slot);
            if (equipped != null)
                total += Mathf.Max(0f, equipped.EffectiveWeightLbs); // D&D 3.5e: mithral/darkwood halves weight
        }

        // Slotless wondrous items weight
        if (SlotlessItems != null)
        {
            foreach (var item in SlotlessItems)
            {
                if (item != null)
                {
                    // Extradimensional containers use apparent weight, not actual weight
                    // (Bag of Holding = 15-60 lbs, Handy Haversack = 5 lbs, Efficient Quiver = 2 lbs,
                    //  Portable Hole = 0 lbs when folded)
                    float weight = (item.IsWondrous && item.WondrousIsExtradimensional)
                        ? item.WondrousApparentWeight
                        : Mathf.Max(0f, item.EffectiveWeightLbs);
                    total += weight;
                }
            }
        }

        for (int i = 0; i < GeneralSlots.Length; i++)
        {
            ItemData item = GeneralSlots[i];
            if (item != null)
            {
                // Extradimensional containers in general inventory also use apparent weight
                float weight = (item.IsWondrous && item.WondrousIsExtradimensional)
                    ? item.WondrousApparentWeight
                    : Mathf.Max(0f, item.EffectiveWeightLbs); // D&D 3.5e: material weight modifiers
                total += weight;
            }
        }

        return total;
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

    private int FindFirstEmptyGeneralSlotIndex()
    {
        if (GeneralSlots == null)
            return -1;

        for (int i = 0; i < GeneralSlots.Length; i++)
        {
            if (GeneralSlots[i] == null)
                return i;
        }

        return -1;
    }

    private void EnsureGeneralSlotCapacity(int minLength)
    {
        int currentLength = GeneralSlots != null ? GeneralSlots.Length : 0;
        if (currentLength >= minLength)
            return;

        int targetLength = Mathf.Max(minLength, currentLength + GeneralSlotGrowthStep);
        if (targetLength <= 0)
            targetLength = GeneralSlotCount;

        Array.Resize(ref GeneralSlots, targetLength);
        Debug.Log($"[Inventory] Expanded general slots to {targetLength}.");
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
    public int EmptySlots => Mathf.Max(0, (GeneralSlots != null ? GeneralSlots.Length : 0) - ItemCount);

    // ===== Ammunition Management =====

    /// <summary>
    /// Find the best ammunition stack in inventory matching the required type.
    /// Prioritizes enchanted ammo (with active spell effects) over normal ammo.
    /// Returns null if no matching ammunition is found.
    /// </summary>
    public ItemData FindBestAmmo(AmmunitionType ammoType)
    {
        if (ammoType == AmmunitionType.None || GeneralSlots == null)
            return null;

        ItemData bestEnchanted = null;
        ItemData bestNormal = null;

        for (int i = 0; i < GeneralSlots.Length; i++)
        {
            ItemData item = GeneralSlots[i];
            if (item == null || !item.IsAmmunition || item.AmmoType != ammoType || item.Quantity <= 0)
                continue;

            bool hasEnchantment = item.ActiveSpellEffects != null && item.ActiveSpellEffects.Count > 0;

            if (hasEnchantment)
            {
                // Among enchanted stacks, prefer the one with fewest remaining (use enchanted first)
                if (bestEnchanted == null || item.Quantity < bestEnchanted.Quantity)
                    bestEnchanted = item;
            }
            else
            {
                if (bestNormal == null || item.Quantity > bestNormal.Quantity)
                    bestNormal = item;
            }
        }

        // Enchanted ammo is consumed first per task spec
        return bestEnchanted ?? bestNormal;
    }

    /// <summary>
    /// Get total ammunition count of a given type across all inventory stacks.
    /// </summary>
    public int GetTotalAmmoCount(AmmunitionType ammoType)
    {
        if (ammoType == AmmunitionType.None || GeneralSlots == null)
            return 0;

        int total = 0;
        for (int i = 0; i < GeneralSlots.Length; i++)
        {
            ItemData item = GeneralSlots[i];
            if (item != null && item.IsAmmunition && item.AmmoType == ammoType)
                total += item.Quantity;
        }
        return total;
    }

    /// <summary>
    /// Consume one round of ammunition from the best available stack.
    /// Returns the consumed ammo stack (for enchantment info), or null if no ammo available.
    /// Automatically removes depleted stacks from inventory.
    /// </summary>
    public ItemData ConsumeOneAmmo(AmmunitionType ammoType)
    {
        ItemData ammo = FindBestAmmo(ammoType);
        if (ammo == null)
            return null;

        if (!ammo.ConsumeOneAmmo())
            return null;

        // If enchanted, consume one enchanted charge from any active spell effect
        if (ammo.ActiveSpellEffects != null)
        {
            for (int i = ammo.ActiveSpellEffects.Count - 1; i >= 0; i--)
            {
                ItemSpellEffect effect = ammo.ActiveSpellEffects[i];
                if (effect != null && effect.EnchantedAmmoRemaining > 0)
                {
                    effect.ConsumeOneEnchantedAmmo();
                    // Remove spent enchantments
                    if (effect.EnchantedAmmoRemaining <= 0)
                        ammo.ActiveSpellEffects.RemoveAt(i);
                    break;
                }
            }
        }

        // Remove depleted ammo stacks
        if (ammo.Quantity <= 0)
        {
            RemoveItem(ammo);
        }

        return ammo;
    }

    /// <summary>
    /// Check if the character has any ammunition matching the given type.
    /// </summary>
    public bool HasAmmo(AmmunitionType ammoType)
    {
        return FindBestAmmo(ammoType) != null;
    }

    // ════════════════════════════════════════════════════════════
    //  Adamantine Armor DR
    //  D&D 3.5e DMG p.283: Adamantine armor grants DR X/—
    //  Light = DR 1/—, Medium = DR 2/—, Heavy = DR 3/—.
    // ════════════════════════════════════════════════════════════

    private int _currentAdamantineDR;

    private void ApplyAdamantineArmorDR(ItemData armor)
    {
        // Remove previous adamantine armor DR if any
        if (_currentAdamantineDR > 0 && OwnerStats != null)
        {
            OwnerStats.RemoveDamageReduction(_currentAdamantineDR, DamageBypassTag.None, false);
            _currentAdamantineDR = 0;
        }

        if (armor == null || armor.Material == null) return;
        if (armor.Material.MaterialType != ItemMaterialType.Adamantine) return;
        if (armor.Material.ArmorDRAmount <= 0) return;

        _currentAdamantineDR = armor.Material.ArmorDRAmount;
        if (OwnerStats != null)
        {
            // DR X/— (BypassTag = None means nothing bypasses it except epic)
            OwnerStats.AddDamageReduction(_currentAdamantineDR, DamageBypassTag.None, false);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Ring Bonus System (D&D 3.5e DMG pp. 229–233)
    //  Handles all Tier 1 passive ring effects.
    //  Same bonus type does NOT stack between two rings — highest wins.
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Reset all ring-derived stats on OwnerStats to zero/false.
    /// Called at the start of RecalculateStats() before re-applying.
    /// </summary>
    private void ResetRingBonuses()
    {
        if (OwnerStats == null) return;

        // Remove ring-applied deflection bonus (only the ring portion)
        // DeflectionBonus may also contain spell bonuses; ring adds on top.
        // We track ring deflection separately and add via max in ApplyRingBonuses.
        _ringDeflectionBonus = 0;

        OwnerStats.RingResistanceSaveBonus = 0;
        OwnerStats.RingForceShieldBonus = 0;
        OwnerStats.RingClimbBonus = 0;
        OwnerStats.RingSwimBonus = 0;
        OwnerStats.RingJumpBonus = 0;
        OwnerStats.RingHideBonus = 0;
        OwnerStats.RingGrantsEvasion = false;
        OwnerStats.RingGrantsFreedomOfMovement = false;
        OwnerStats.RingGrantsFeatherFall = false;
        OwnerStats.RingGrantsWaterWalking = false;
        OwnerStats.RingGrantsSustenance = false;
        OwnerStats.RingGrantsMindShielding = false;
        OwnerStats.RingGrantsColdEndurance = false;

        // Remove previous ring energy resistance effects
        RemoveRingEnergyResistances();
    }

    private int _ringDeflectionBonus;
    private readonly System.Collections.Generic.List<ResistEnergyEffectData> _ringEnergyResistEffects
        = new System.Collections.Generic.List<ResistEnergyEffectData>();

    /// <summary>
    /// Apply all bonuses from a single equipped ring to OwnerStats.
    /// Uses "highest wins" stacking for same bonus types.
    /// </summary>
    private void ApplyRingBonuses(ItemData ring)
    {
        if (ring == null || !ring.IsRing || OwnerStats == null) return;

        // --- Deflection bonus to AC ---
        // D&D 3.5e: Deflection bonuses do not stack; use highest.
        // Ring adds to DeflectionBonus field (which also holds spell bonuses like Shield of Faith).
        if (ring.RingDeflectionBonus > 0)
        {
            int newRingDeflection = Mathf.Max(_ringDeflectionBonus, ring.RingDeflectionBonus);
            // Adjust OwnerStats.DeflectionBonus: remove old ring portion, add new
            OwnerStats.DeflectionBonus += (newRingDeflection - _ringDeflectionBonus);
            _ringDeflectionBonus = newRingDeflection;
        }

        // --- Resistance bonus to all saves ---
        // D&D 3.5e: Resistance bonuses do not stack; use highest.
        if (ring.RingResistanceSaveBonus > 0)
            OwnerStats.RingResistanceSaveBonus = Mathf.Max(OwnerStats.RingResistanceSaveBonus, ring.RingResistanceSaveBonus);

        // --- Shield bonus (force) ---
        if (ring.RingShieldBonus > 0)
            OwnerStats.RingForceShieldBonus = Mathf.Max(OwnerStats.RingForceShieldBonus, ring.RingShieldBonus);

        // --- Energy Resistance ---
        // Continuous effect — rings grant permanent energy resistance (not duration-based).
        // Use large duration so it never expires during gameplay.
        if (ring.RingEnergyResistanceAmount > 0 && !string.IsNullOrEmpty(ring.RingEnergyType))
        {
            ResistEnergyType reType = ParseEnergyType(ring.RingEnergyType);
            var effect = new ResistEnergyEffectData
            {
                EnergyType = reType,
                ResistanceAmount = ring.RingEnergyResistanceAmount,
                DurationRemainingRounds = 999999, // Permanent while worn
                Caster = null
            };
            OwnerStats.SetResistEnergyEffect(effect);
            _ringEnergyResistEffects.Add(effect);
        }

        // --- Skill competence bonuses ---
        // D&D 3.5e: Competence bonuses do not stack; use highest.
        if (ring.RingSkillBonus > 0 && !string.IsNullOrEmpty(ring.RingSkillName))
        {
            string skill = ring.RingSkillName;
            if (string.Equals(skill, "Climb", System.StringComparison.OrdinalIgnoreCase))
                OwnerStats.RingClimbBonus = Mathf.Max(OwnerStats.RingClimbBonus, ring.RingSkillBonus);
            else if (string.Equals(skill, "Swim", System.StringComparison.OrdinalIgnoreCase))
                OwnerStats.RingSwimBonus = Mathf.Max(OwnerStats.RingSwimBonus, ring.RingSkillBonus);
            else if (string.Equals(skill, "Jump", System.StringComparison.OrdinalIgnoreCase))
                OwnerStats.RingJumpBonus = Mathf.Max(OwnerStats.RingJumpBonus, ring.RingSkillBonus);
            else if (string.Equals(skill, "Hide", System.StringComparison.OrdinalIgnoreCase))
                OwnerStats.RingHideBonus = Mathf.Max(OwnerStats.RingHideBonus, ring.RingSkillBonus);
        }

        // --- Boolean ability grants (OR logic — any ring grants it) ---
        if (ring.RingGrantsEvasion) OwnerStats.RingGrantsEvasion = true;
        if (ring.RingGrantsFreedomOfMovement)
        {
            OwnerStats.RingGrantsFreedomOfMovement = true;
            OwnerStats.FreedomOfMovementActive = true; // Activate the existing FoM flag
        }
        if (ring.RingGrantsFeatherFall) OwnerStats.RingGrantsFeatherFall = true;
        if (ring.RingGrantsWaterWalking) OwnerStats.RingGrantsWaterWalking = true;
        if (ring.RingGrantsSustenance) OwnerStats.RingGrantsSustenance = true;
        if (ring.RingGrantsMindShielding) OwnerStats.RingGrantsMindShielding = true;
        if (ring.RingGrantsColdEndurance) OwnerStats.RingGrantsColdEndurance = true;
    }

    /// <summary>Remove ring-applied energy resistance effects from OwnerStats.</summary>
    private void RemoveRingEnergyResistances()
    {
        if (OwnerStats == null) return;

        // Remove each ring energy resistance effect that we previously added.
        // We track them in _ringEnergyResistEffects so we can cleanly remove only ring-sourced ones.
        if (_ringEnergyResistEffects.Count > 0 && OwnerStats.ActiveResistEnergyEffects != null)
        {
            foreach (var effect in _ringEnergyResistEffects)
            {
                OwnerStats.ActiveResistEnergyEffects.Remove(effect);
            }
        }
        _ringEnergyResistEffects.Clear();
    }

    /// <summary>Parse energy type string to ResistEnergyType enum.</summary>
    private static ResistEnergyType ParseEnergyType(string type)
    {
        if (string.IsNullOrEmpty(type)) return ResistEnergyType.Fire;

        switch (type.ToLower())
        {
            case "acid": return ResistEnergyType.Acid;
            case "cold": return ResistEnergyType.Cold;
            case "electricity": return ResistEnergyType.Electricity;
            case "fire": return ResistEnergyType.Fire;
            case "sonic": return ResistEnergyType.Sonic;
            default: return ResistEnergyType.Fire;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Wondrous Item Bonus Application (D&D 3.5e DMG pp. 248–271)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Apply bonuses from all equipped wondrous items.
    /// Uses D&D 3.5e bonus stacking rules (same type doesn't stack, use highest).
    /// </summary>
    private void ApplyAllWondrousItemBonuses()
    {
        if (OwnerStats == null) return;

        // Reset wondrous-derived stats
        OwnerStats.WondrousNaturalArmorBonus = 0;
        OwnerStats.WondrousBracersArmorBonus = 0;
        OwnerStats.WondrousSaveAllBonus = 0;
        OwnerStats.WondrousSpeedBonus = 0;

        // Reset wondrous movement modes
        OwnerStats.WondrousHasFlight = false;
        OwnerStats.WondrousFlightSpeed = 0;
        OwnerStats.WondrousFlightManeuverability = null;
        OwnerStats.WondrousHasSpiderClimb = false;
        OwnerStats.WondrousSpiderClimbSpeed = 0;
        OwnerStats.WondrousHasLevitation = false;
        OwnerStats.WondrousLevitationSpeed = 0;
        OwnerStats.WondrousEndureCold = false;
        OwnerStats.DisplacementMissChance = 0;

        // Reset wondrous ability score enhancement bonuses (Big Six)
        OwnerStats.WondrousEnhancementSTR = 0;
        OwnerStats.WondrousEnhancementDEX = 0;
        OwnerStats.WondrousEnhancementCON = 0;
        OwnerStats.WondrousEnhancementINT = 0;
        OwnerStats.WondrousEnhancementWIS = 0;
        OwnerStats.WondrousEnhancementCHA = 0;

        // Reset wondrous darkvision (Phase 5: Stealth/Detection)
        OwnerStats.WondrousHasDarkvision = false;
        OwnerStats.WondrousDarkvisionRange = 0;

        // Reset wondrous skill bonuses (Phase 5: Stealth/Detection)
        if (OwnerStats.WondrousSkillBonuses == null)
            OwnerStats.WondrousSkillBonuses = new System.Collections.Generic.Dictionary<string, int>();
        else
            OwnerStats.WondrousSkillBonuses.Clear();

        // Check all equipment slots for wondrous items
        foreach (EquipSlot slot in AllEquipmentSlots)
        {
            ItemData item = GetEquipped(slot);
            if (item != null && item.IsWondrous)
                ApplyWondrousItemBonuses(item);
        }

        // Check slotless items
        if (SlotlessItems != null)
        {
            foreach (var item in SlotlessItems)
            {
                if (item != null && item.IsWondrous)
                    ApplyWondrousItemBonuses(item);
            }
        }

        // Apply highest-wins stacking for Bracers of Armor vs physical armor
        // Bracers provide an armor bonus that doesn't stack with physical armor
        if (OwnerStats.WondrousBracersArmorBonus > 0)
        {
            // Only apply if it's higher than current armor bonus (doesn't stack)
            if (OwnerStats.WondrousBracersArmorBonus > OwnerStats.ArmorBonus)
                OwnerStats.ArmorBonus = OwnerStats.WondrousBracersArmorBonus;
        }
    }

    /// <summary>Apply bonuses from a single wondrous item to OwnerStats.</summary>
    private void ApplyWondrousItemBonuses(ItemData item)
    {
        if (item == null || !item.IsWondrous) return;

        // --- AC Bonuses ---
        if (item.WondrousACBonus > 0 && !string.IsNullOrEmpty(item.WondrousACBonusType))
        {
            switch (item.WondrousACBonusType.ToLower())
            {
                case "natural":
                    // Enhancement to natural armor (Amulet of Natural Armor) — highest wins
                    OwnerStats.WondrousNaturalArmorBonus = Mathf.Max(OwnerStats.WondrousNaturalArmorBonus, item.WondrousACBonus);
                    break;
                case "armor":
                    // Armor bonus (Bracers of Armor) — doesn't stack with physical armor, handled in ApplyAllWondrousItemBonuses
                    OwnerStats.WondrousBracersArmorBonus = Mathf.Max(OwnerStats.WondrousBracersArmorBonus, item.WondrousACBonus);
                    break;
            }
        }

        // --- Saving Throw Bonuses ---
        if (item.WondrousSaveBonus > 0 && !string.IsNullOrEmpty(item.WondrousSaveType))
        {
            if (item.WondrousSaveType == "all")
                OwnerStats.WondrousSaveAllBonus = Mathf.Max(OwnerStats.WondrousSaveAllBonus, item.WondrousSaveBonus);
        }

        // --- Speed Bonus ---
        if (item.WondrousSpeedBonus > 0)
        {
            // Enhancement bonuses to speed don't stack; use highest
            OwnerStats.WondrousSpeedBonus = Mathf.Max(OwnerStats.WondrousSpeedBonus, item.WondrousSpeedBonus);
        }

        // --- Displacement Miss Chance (Cloak of Displacement) ---
        if (item.WondrousDisplacementMissChance > 0)
        {
            // Displacement miss chances don't stack; use highest
            OwnerStats.DisplacementMissChance = Mathf.Max(OwnerStats.DisplacementMissChance, item.WondrousDisplacementMissChance);
        }

        // --- Movement Mode Bonuses ---
        if (item.WondrousGrantsMovement && !string.IsNullOrEmpty(item.WondrousMovementMode))
        {
            switch (item.WondrousMovementMode)
            {
                case "fly":
                    // Take best flight speed; upgrade maneuverability if tied
                    if (item.WondrousMovementSpeed > OwnerStats.WondrousFlightSpeed)
                    {
                        OwnerStats.WondrousHasFlight = true;
                        OwnerStats.WondrousFlightSpeed = item.WondrousMovementSpeed;
                        OwnerStats.WondrousFlightManeuverability = item.WondrousFlightManeuverability ?? "average";
                    }
                    else if (item.WondrousMovementSpeed == OwnerStats.WondrousFlightSpeed)
                    {
                        OwnerStats.WondrousHasFlight = true;
                        // Keep better maneuverability
                        if (GetManeuverabilityRank(item.WondrousFlightManeuverability) > GetManeuverabilityRank(OwnerStats.WondrousFlightManeuverability))
                            OwnerStats.WondrousFlightManeuverability = item.WondrousFlightManeuverability;
                    }
                    break;
                case "spider_climb":
                    OwnerStats.WondrousHasSpiderClimb = true;
                    OwnerStats.WondrousSpiderClimbSpeed = Mathf.Max(OwnerStats.WondrousSpiderClimbSpeed, item.WondrousMovementSpeed);
                    break;
                case "levitate":
                    OwnerStats.WondrousHasLevitation = true;
                    OwnerStats.WondrousLevitationSpeed = Mathf.Max(OwnerStats.WondrousLevitationSpeed, item.WondrousMovementSpeed);
                    break;
            }
        }

        // --- Cold Endurance ---
        if (item.WondrousGrantsColdEndurance)
            OwnerStats.WondrousEndureCold = true;

        // --- Darkvision (Phase 5: Goggles of Night, etc.) ---
        if (item.WondrousDarkvisionRange > 0)
        {
            OwnerStats.WondrousHasDarkvision = true;
            OwnerStats.WondrousDarkvisionRange = Mathf.Max(OwnerStats.WondrousDarkvisionRange, item.WondrousDarkvisionRange);
        }

        // --- Skill Bonuses (Phase 5: Boots of Elvenkind, Cloak of Elvenkind, Eyes of the Eagle, etc.) ---
        // Same bonus type to same skill doesn't stack; use highest.
        if (item.WondrousSkillBonus > 0 && !string.IsNullOrEmpty(item.WondrousSkillName))
        {
            if (OwnerStats.WondrousSkillBonuses == null)
                OwnerStats.WondrousSkillBonuses = new System.Collections.Generic.Dictionary<string, int>();

            string skill1 = item.WondrousSkillName;
            if (!OwnerStats.WondrousSkillBonuses.ContainsKey(skill1))
                OwnerStats.WondrousSkillBonuses[skill1] = item.WondrousSkillBonus;
            else
                OwnerStats.WondrousSkillBonuses[skill1] = Mathf.Max(OwnerStats.WondrousSkillBonuses[skill1], item.WondrousSkillBonus);
        }
        if (item.WondrousSkillBonus2 > 0 && !string.IsNullOrEmpty(item.WondrousSkillName2))
        {
            if (OwnerStats.WondrousSkillBonuses == null)
                OwnerStats.WondrousSkillBonuses = new System.Collections.Generic.Dictionary<string, int>();

            string skill2 = item.WondrousSkillName2;
            if (!OwnerStats.WondrousSkillBonuses.ContainsKey(skill2))
                OwnerStats.WondrousSkillBonuses[skill2] = item.WondrousSkillBonus2;
            else
                OwnerStats.WondrousSkillBonuses[skill2] = Mathf.Max(OwnerStats.WondrousSkillBonuses[skill2], item.WondrousSkillBonus2);
        }

        // --- Ability Score Enhancement Bonuses (Big Six items) ---
        // Enhancement bonuses to the same ability score don't stack; use highest.
        // D&D 3.5e DMG: Belt of Giant Strength, Gloves of Dexterity, Amulet of Health,
        // Headband of Intellect, Periapt of Wisdom, Cloak of Charisma, Gauntlets of Ogre Power.
        if (item.WondrousAbilityBonus > 0 && !string.IsNullOrEmpty(item.WondrousAbilityType))
        {
            switch (item.WondrousAbilityType)
            {
                case "Str":
                    OwnerStats.WondrousEnhancementSTR = Mathf.Max(OwnerStats.WondrousEnhancementSTR, item.WondrousAbilityBonus);
                    break;
                case "Dex":
                    OwnerStats.WondrousEnhancementDEX = Mathf.Max(OwnerStats.WondrousEnhancementDEX, item.WondrousAbilityBonus);
                    break;
                case "Con":
                    OwnerStats.WondrousEnhancementCON = Mathf.Max(OwnerStats.WondrousEnhancementCON, item.WondrousAbilityBonus);
                    break;
                case "Int":
                    OwnerStats.WondrousEnhancementINT = Mathf.Max(OwnerStats.WondrousEnhancementINT, item.WondrousAbilityBonus);
                    break;
                case "Wis":
                    OwnerStats.WondrousEnhancementWIS = Mathf.Max(OwnerStats.WondrousEnhancementWIS, item.WondrousAbilityBonus);
                    break;
                case "Cha":
                    OwnerStats.WondrousEnhancementCHA = Mathf.Max(OwnerStats.WondrousEnhancementCHA, item.WondrousAbilityBonus);
                    break;
            }
        }
    }

    /// <summary>Get numeric rank for flight maneuverability comparison (higher = better).</summary>
    private static int GetManeuverabilityRank(string maneuverability)
    {
        if (string.IsNullOrEmpty(maneuverability)) return 2; // default average
        switch (maneuverability.ToLower())
        {
            case "clumsy": return 0;
            case "poor": return 1;
            case "average": return 2;
            case "good": return 3;
            case "perfect": return 4;
            default: return 2;
        }
    }
}