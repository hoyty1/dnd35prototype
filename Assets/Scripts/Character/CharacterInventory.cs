using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Encapsulates character inventory access and inventory-related item operations used by <see cref="CharacterController"/>.
/// </summary>
public class CharacterInventory : MonoBehaviour
{
    private CharacterController _character;
    private InventoryComponent _inventoryComponent;

    public void Initialize(CharacterController character)
    {
        _character = character;
        _inventoryComponent = _character != null ? _character.GetComponent<InventoryComponent>() : null;

        if (_inventoryComponent == null)
        {
            string ownerName = _character != null && _character.Stats != null ? _character.Stats.CharacterName : name;
            Debug.LogWarning($"[Inventory] No InventoryComponent found on {ownerName}.");
        }
    }

    private InventoryComponent GetInventoryComponent()
    {
        if (_character == null)
            return null;

        if (_inventoryComponent == null)
            _inventoryComponent = _character.GetComponent<InventoryComponent>();

        return _inventoryComponent;
    }

    public Inventory GetInventory()
    {
        InventoryComponent invComp = GetInventoryComponent();
        return invComp != null ? invComp.CharacterInventory : null;
    }

    public ItemData GetRightHandEquippedWeapon()
    {
        Inventory inv = GetInventory();
        return inv != null ? inv.RightHandSlot : null;
    }

    public ItemData GetEquippedHandsItem()
    {
        Inventory inv = GetInventory();
        return inv != null ? inv.HandsSlot : null;
    }

    public bool TryEquipDisarmedItem(ItemData disarmedItem, out EquipSlot equippedSlot)
    {
        equippedSlot = EquipSlot.None;
        if (disarmedItem == null)
            return false;

        Inventory inv = GetInventory();
        if (inv == null)
            return false;

        if (inv.RightHandSlot == null && disarmedItem.CanEquipIn(EquipSlot.RightHand))
        {
            inv.RightHandSlot = disarmedItem;
            inv.RecalculateStats();
            equippedSlot = EquipSlot.RightHand;
            return true;
        }

        if (inv.LeftHandSlot == null && disarmedItem.CanEquipIn(EquipSlot.LeftHand))
        {
            inv.LeftHandSlot = disarmedItem;
            inv.RecalculateStats();
            equippedSlot = EquipSlot.LeftHand;
            return true;
        }

        return false;
    }

    public ItemData RemoveEquippedHeldItem(EquipSlot? handSlot)
    {
        Inventory inv = GetInventory();
        if (inv == null)
            return null;

        ItemData removedItem = null;

        if (handSlot == EquipSlot.RightHand)
        {
            removedItem = inv.RightHandSlot;
            inv.RightHandSlot = null;
        }
        else if (handSlot == EquipSlot.LeftHand)
        {
            removedItem = inv.LeftHandSlot;
            inv.LeftHandSlot = null;
        }
        else
        {
            if (inv.RightHandSlot != null)
            {
                removedItem = inv.RightHandSlot;
                inv.RightHandSlot = null;
            }
            else if (inv.LeftHandSlot != null)
            {
                removedItem = inv.LeftHandSlot;
                inv.LeftHandSlot = null;
            }
        }

        inv.RecalculateStats();
        return removedItem;
    }

    public bool DestroyEquippedItem(EquipSlot slot, out ItemData destroyedItem)
    {
        destroyedItem = null;

        Inventory inv = GetInventory();
        if (inv == null)
            return false;

        destroyedItem = inv.GetEquipped(slot);
        if (destroyedItem == null)
            return false;

        return inv.RemoveItem(destroyedItem);
    }

    public bool AddItem(ItemData item)
    {
        Inventory inv = GetInventory();
        return inv != null && inv.AddItem(item);
    }

    public bool RemoveItem(ItemData item)
    {
        Inventory inv = GetInventory();
        return inv != null && inv.RemoveItem(item);
    }

    public int GetGeneralInventoryItemCount()
    {
        Inventory inv = GetInventory();
        return inv != null ? inv.ItemCount : 0;
    }

    public float GetTotalCarriedWeightLbs()
    {
        Inventory inv = GetInventory();
        return inv != null ? inv.GetTotalCarriedWeightLbs() : 0f;
    }

    public int GetConsumableCount()
    {
        Inventory inv = GetInventory();
        if (inv == null)
            return 0;

        int count = 0;

        foreach (EquipSlot slot in Inventory.AllEquipmentSlots)
        {
            ItemData equipped = inv.GetEquipped(slot);
            if (equipped != null && equipped.IsConsumable)
                count++;
        }

        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            ItemData item = inv.GeneralSlots[i];
            if (item != null && item.IsConsumable)
                count++;
        }

        return count;
    }

    public List<ItemData> GetAllItems()
    {
        var items = new List<ItemData>();
        Inventory inv = GetInventory();
        if (inv == null)
            return items;

        foreach (EquipSlot slot in Inventory.AllEquipmentSlots)
        {
            ItemData equipped = inv.GetEquipped(slot);
            if (equipped != null)
                items.Add(equipped);
        }

        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            if (inv.GeneralSlots[i] != null)
                items.Add(inv.GeneralSlots[i]);
        }

        return items;
    }
}
