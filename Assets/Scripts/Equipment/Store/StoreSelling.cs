using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// One place a sellable item sits: the party stash or a character's inventory.
/// <see cref="Units"/> counts the item's stack (potions and scrolls merge into one ItemData with
/// <see cref="ItemData.StackCount"/> up to 20), so a stack of five is five units, not one (ITM-003).
/// </summary>
public sealed class SellSource
{
    public ItemData Item;
    /// <summary>Set when the item sits in the party stash.</summary>
    public PartyStash Stash;
    /// <summary>Set when the item sits in a character's inventory.</summary>
    public Inventory Inventory;
    public string OwnerName;

    public bool FromStash => Stash != null;
    public int Units => StoreSelling.UnitsIn(Item);
}

/// <summary>
/// A sell-list row: items that share a name and an unrounded unit sell value. Every unit in the row is worth
/// <see cref="UnitSellValue"/>; items whose value differs (for example wands with different charges) get their own row.
/// </summary>
public sealed class SellStack
{
    public string StackKey;
    public string ItemName;
    public ItemData RepresentativeItem;
    /// <summary>Half the listed price, prorated by charges for a wand, before rounding (for example 12.5 gp).</summary>
    public decimal UnitSellValue;
    public readonly List<SellSource> Sources = new List<SellSource>();

    /// <summary>One unit's sell price in whole gp (rounded down), as shown on the row.</summary>
    public int UnitSellPrice => StoreSelling.FloorGp(UnitSellValue);

    /// <summary>Gold paid for <paramref name="units"/> units sold together: the lot's value, rounded down once.</summary>
    public int LotPrice(int units) => StoreSelling.LotPrice(UnitSellValue, units);

    /// <summary>Units available to sell: the sum of the sources' stack counts.</summary>
    public int Quantity
    {
        get
        {
            int total = 0;
            for (int i = 0; i < Sources.Count; i++)
                total += Sources[i] != null ? Sources[i].Units : 0;
            return total;
        }
    }
}

/// <summary>
/// Store selling, independent of the UI (ITM-003). PHB p.112 (Selling Loot): an item sells for half its listed price,
/// per item, so a stack of N potions is worth N times half the unit price. The party's gold is kept in whole gp, so a
/// sale pays the lot's value rounded down once (three 25 gp scrolls pay 37 gp, not 3 x 12). DMG p.214: a partially used
/// charged item is worth its price in proportion to its remaining charges, so wands are priced per wand
/// (<see cref="StoreInventory.GetSellValue"/>) and only wands with the same value and charges share a row.
/// </summary>
public static class StoreSelling
{
    /// <summary>
    /// Unrounded sell value of <paramref name="units"/> units of <paramref name="item"/> listed at
    /// <paramref name="listedPriceGp"/> gp each: half the listed price per unit (PHB p.112), and for a wand times
    /// current / max charges (DMG p.214; a wand with no maximum is worth nothing).
    /// </summary>
    public static decimal SellValue(int listedPriceGp, ItemData item, int units = 1)
    {
        if (item == null || listedPriceGp <= 0 || units <= 0)
            return 0m;

        decimal value = (decimal)listedPriceGp * units;
        if (item.IsWand)
        {
            int maxCharges = item.Wand != null ? item.Wand.MaxCharges : item.MaxCharges;
            int currentCharges = item.Wand != null ? item.Wand.CurrentCharges : item.CurrentCharges;
            if (maxCharges <= 0 || currentCharges <= 0)
                return 0m;
            return value * Mathf.Min(currentCharges, maxCharges) / (2m * maxCharges);
        }

        return value / 2m;
    }

    /// <summary>A gp value rounded down to whole gp (never below 0).</summary>
    public static int FloorGp(decimal value)
    {
        return value <= 0m ? 0 : (int)Math.Floor(value);
    }

    /// <summary>A gp value for display, with up to two decimals (12.5 gp is 12 gp 5 sp).</summary>
    public static string FormatGp(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Gold for <paramref name="units"/> units of value <paramref name="unitSellValue"/> sold together: rounded down once.</summary>
    public static int LotPrice(decimal unitSellValue, int units)
    {
        return units <= 0 ? 0 : FloorGp(unitSellValue * units);
    }

    /// <summary>Units an item stands for: its stack count for a stackable item, otherwise 1.</summary>
    public static int UnitsIn(ItemData item)
    {
        if (item == null)
            return 0;

        return item.IsStackable ? Mathf.Max(1, item.StackCount) : 1;
    }

    /// <summary>Row key: display name and unrounded unit sell value, plus the charges for a wand.</summary>
    public static string BuildStackKey(ItemData item, decimal unitSellValue)
    {
        if (item == null)
            return string.Empty;

        string key = (item.FullNameWithEnhancement ?? string.Empty).Trim() + "|"
            + unitSellValue.ToString("0.##########", CultureInfo.InvariantCulture);
        if (item.IsWand)
        {
            int current = item.Wand != null ? item.Wand.CurrentCharges : item.CurrentCharges;
            int max = item.Wand != null ? item.Wand.MaxCharges : item.MaxCharges;
            key += "|" + current + "/" + max;
        }

        return key;
    }

    /// <summary>Adds an item to the row with the same key, creating the row if needed. Returns the row.</summary>
    public static SellStack AddToStacks(
        Dictionary<string, SellStack> stacks,
        ItemData item,
        decimal unitSellValue,
        PartyStash stash,
        Inventory inventory,
        string ownerName)
    {
        if (stacks == null || item == null)
            return null;

        string key = BuildStackKey(item, unitSellValue);
        if (!stacks.TryGetValue(key, out SellStack stack))
        {
            stack = new SellStack
            {
                StackKey = key,
                ItemName = item.FullNameWithEnhancement,
                RepresentativeItem = item,
                UnitSellValue = unitSellValue
            };
            stacks[key] = stack;
        }

        stack.Sources.Add(new SellSource
        {
            Item = item,
            Stash = stash,
            Inventory = inventory,
            OwnerName = ownerName
        });

        return stack;
    }

    /// <summary>
    /// Removes up to <paramref name="quantity"/> units from the row's sources, in order, taking part of a stack
    /// when fewer units are wanted than it holds. Returns the units removed; the seller is paid
    /// <see cref="SellStack.LotPrice"/> of that count. <paramref name="onRemoved"/> reports each source and its units.
    /// </summary>
    public static int RemoveUnits(SellStack stack, int quantity, Action<SellSource, int> onRemoved = null)
    {
        if (stack == null || quantity <= 0)
            return 0;

        int removedTotal = 0;
        for (int i = 0; i < stack.Sources.Count && removedTotal < quantity; i++)
        {
            SellSource source = stack.Sources[i];
            if (source == null || source.Item == null)
                continue;

            int want = Mathf.Min(quantity - removedTotal, source.Units);
            if (want <= 0)
                continue;

            int removed = source.Stash != null
                ? source.Stash.RemoveUnits(source.Item, want)
                : source.Inventory != null ? source.Inventory.RemoveUnits(source.Item, want) : 0;

            if (removed <= 0)
                continue;

            removedTotal += removed;
            onRemoved?.Invoke(source, removed);
        }

        return removedTotal;
    }
}
