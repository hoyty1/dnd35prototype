using System;
using System.Collections.Generic;
using UnityEngine;

public enum PartyStashState
{
    Unlocked,
    Locked
}

/// <summary>
/// Shared party inventory used between combats.
/// Items in this stash are not available while combat is active.
/// Session-only by default (no persistence yet).
/// </summary>
[Serializable]
public class PartyStash
{
    [SerializeField] private List<ItemData> _items = new List<ItemData>();
    [SerializeField] private PartyStashState _state = PartyStashState.Unlocked;

    public PartyStashState State => _state;
    public bool IsLocked => _state == PartyStashState.Locked;
    public int Count => _items != null ? _items.Count : 0;

    public void Lock() => _state = PartyStashState.Locked;
    public void Unlock() => _state = PartyStashState.Unlocked;

    public bool AddItem(ItemData item)
    {
        if (item == null || IsLocked)
            return false;

        _items ??= new List<ItemData>();
        _items.Add(item);
        return true;
    }

    public bool RemoveItem(ItemData item)
    {
        if (item == null || IsLocked || _items == null)
            return false;

        return _items.Remove(item);
    }

    public bool TryRemoveFirstById(string itemId, out ItemData removed)
    {
        removed = null;
        if (IsLocked || _items == null || string.IsNullOrWhiteSpace(itemId))
            return false;

        for (int i = 0; i < _items.Count; i++)
        {
            ItemData item = _items[i];
            if (item == null)
                continue;

            if (!string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase))
                continue;

            removed = item;
            _items.RemoveAt(i);
            return true;
        }

        return false;
    }

    public IReadOnlyList<ItemData> GetItems()
    {
        _items ??= new List<ItemData>();
        return _items;
    }

    public List<ItemData> GetItemsSnapshot()
    {
        _items ??= new List<ItemData>();
        return new List<ItemData>(_items);
    }

    public void Clear()
    {
        if (IsLocked)
            return;

        _items?.Clear();
    }

    public void SeedDefaultItemsIfEmpty()
    {
        _items ??= new List<ItemData>();
        if (_items.Count > 0)
            return;

        ItemDatabase.Init();

        AddClonedItem("longsword", 1);
        AddClonedItem("shortbow", 1);
        AddClonedItem("dagger", 2);
        AddClonedItem("shield_heavy_steel", 1);
        AddClonedItem("chain_shirt", 1);
        AddClonedItem("breastplate", 1);
        AddClonedItem("potion_cure_light_wounds", 30);
        AddClonedItem("potion_shield_of_faith", 4);
        AddClonedItem("crossbow_bolts_20", 2);
        AddClonedItem("rope_hemp", 1);
        AddClonedItem("torch", 3);
    }

    private void AddClonedItem(string itemId, int count)
    {
        if (string.IsNullOrWhiteSpace(itemId) || count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            ItemData item = ItemDatabase.CloneItem(itemId);
            if (item != null)
                _items.Add(item);
        }
    }
}
