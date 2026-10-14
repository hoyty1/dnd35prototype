using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Inventory
{
/// <summary>
/// ITM-003: selling counts stack units, not ItemData objects. Rules: PHB p.112 (Selling Loot: an item sells for half its
/// listed price, so N potions sell for N times the unit price); DMG p.214 (a partially used charged item is worth its
/// price in proportion to its remaining charges, so each wand is priced by its own charges). Gold is kept in whole gp, so a
/// lot sold together is paid its value rounded down once (three 25 gp scrolls pay 37 gp, not 3 x 12). Exercises
/// <see cref="StoreSelling"/> (the sell logic StoreUI calls), <see cref="global::Inventory.RemoveUnits"/>,
/// <see cref="PartyStash.RemoveUnits"/> and <see cref="EconomyService.SellItem"/>. Items are built here or cloned from
/// the databases (templates are never changed); no scene is needed, except the two StoreInventory price checks, which
/// run in Play mode with a scene GameManager (the store is created as the pre-combat hub creates it) and otherwise log a warning.
/// </summary>
public static class StoreSellingRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        ItemDatabase.Init();

        Debug.Log("====== STORE SELLING RULES TESTS (ITM-003) ======");

        TestUnitsIn();
        TestPartialSellFromInventoryStack();
        TestSellAcrossStashSources();
        TestLockedStashSellsNothing();
        TestRemoveUnitsIgnoresItemsNotHeld();
        TestWandsPricedPerWand();
        TestLotRoundedDownOnce();
        TestEconomySellItemSellsOneUnit();
        TestStorePricesPerUnitAndCharges();

        Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  FAIL: {testName} {detail}");
        }
    }

    private static ItemData MakePotionStack(int count)
    {
        ItemData potion = ItemDatabase.CloneItem(ItemID.PotionCureLightWounds);
        if (potion != null)
            potion.StackCount = count;
        return potion;
    }

    private static ItemData MakeScroll(int count)
    {
        return new ItemData
        {
            Id = "test_scroll_itm003",
            Name = "Scroll of Test (ITM-003)",
            Type = ItemType.Consumable,
            IsScroll = true,
            IsStackable = true,
            MaxStackSize = 20,
            StackCount = count,
            BasePriceGp = 25
        };
    }

    private static ItemData MakeWand(int charges)
    {
        return new ItemData
        {
            Id = string.Empty,
            Name = "Wand of Test (ITM-003)",
            Type = ItemType.Consumable,
            IsWand = true,
            IsStackable = false,
            MaxStackSize = 1,
            StackCount = 1,
            CurrentCharges = charges,
            MaxCharges = 50,
            BasePriceGp = 750
        };
    }

    // ===== Units =====

    private static void TestUnitsIn()
    {
        Assert(StoreSelling.UnitsIn(null) == 0, "UnitsIn(null) is 0");
        Assert(StoreSelling.UnitsIn(MakeScroll(5)) == 5, "A stack of five scrolls is five units");
        Assert(StoreSelling.UnitsIn(MakeScroll(0)) == 1, "A stackable item with StackCount 0 still counts as one unit");
        ItemData wand = MakeWand(50);
        wand.StackCount = 7;
        Assert(StoreSelling.UnitsIn(wand) == 1, "A non-stackable item is one unit whatever its StackCount");
    }

    // ===== Inventory sources =====

    private static void TestPartialSellFromInventoryStack()
    {
        ItemData template = MakePotionStack(5);
        if (template == null)
        {
            Assert(false, "Cure Light Wounds potion clone exists");
            return;
        }

        var inv = new global::Inventory();
        Assert(inv.AddItem(template), "A stack of five potions goes into the inventory");
        ItemData held = FindFirst(inv, ItemIDs.POTION_CURE_LIGHT_WOUNDS);
        Assert(held != null && held.StackCount == 5, "The inventory holds one ItemData with StackCount 5",
            $"got {(held != null ? held.StackCount.ToString() : "none")}");

        var stacks = new Dictionary<string, SellStack>();
        SellStack row = StoreSelling.AddToStacks(stacks, held, 25, null, inv, "Tester");
        Assert(row != null && row.Quantity == 5, "The sell row offers five units, not one",
            $"got {(row != null ? row.Quantity : -1)}");

        int sold = StoreSelling.RemoveUnits(row, 2);
        Assert(sold == 2, "Selling two of five removes two units", $"got {sold}");
        Assert(row.LotPrice(sold) == 50, "Two potions at 25 gp each pay 50 gp", $"got {row.LotPrice(sold)}");
        Assert(inv.Holds(held) && held.StackCount == 3, "Three potions stay in the same inventory slot",
            $"held={inv.Holds(held)} count={held.StackCount}");

        stacks.Clear();
        row = StoreSelling.AddToStacks(stacks, held, 25, null, inv, "Tester");
        Assert(row.Quantity == 3, "The rebuilt row offers the three left", $"got {row.Quantity}");
        sold = StoreSelling.RemoveUnits(row, 3);
        Assert(sold == 3 && !inv.Holds(held) && FindFirst(inv, ItemIDs.POTION_CURE_LIGHT_WOUNDS) == null,
            "Selling the rest empties the slot", $"sold={sold}");

        sold = StoreSelling.RemoveUnits(row, 1);
        Assert(sold == 0, "Selling from an emptied row removes nothing", $"got {sold}");
    }

    private static ItemData FindFirst(global::Inventory inv, string id)
    {
        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            ItemData item = inv.GeneralSlots[i];
            if (item != null && item.Id == id)
                return item;
        }

        return null;
    }

    // ===== Stash sources =====

    private static void TestSellAcrossStashSources()
    {
        var stash = new PartyStash();
        ItemData four = MakeScroll(4);
        ItemData single = MakeScroll(1);
        stash.AddItem(four);
        stash.AddItem(single);

        var stacks = new Dictionary<string, SellStack>();
        StoreSelling.AddToStacks(stacks, four, 12.5m, stash, null, "Stash");
        SellStack row = StoreSelling.AddToStacks(stacks, single, 12.5m, stash, null, "Stash");
        Assert(stacks.Count == 1 && row.Quantity == 5, "A stack of four and a single scroll make one row of five",
            $"rows={stacks.Count} qty={row.Quantity}");

        int sold = StoreSelling.RemoveUnits(row, 3);
        Assert(sold == 3 && four.StackCount == 1 && stash.Count == 2,
            "Selling three takes them from the first stack and leaves the rest",
            $"sold={sold} first={four.StackCount} stash={stash.Count}");

        Assert(row.LotPrice(sold) == 37, "Three 25 gp scrolls sold together pay 37 gp (half of 75, rounded down once)",
            $"got {row.LotPrice(sold)}");

        sold = StoreSelling.RemoveUnits(row, 2);
        Assert(sold == 2 && stash.Count == 0, "Selling the last two spans both stash entries and empties the stash",
            $"sold={sold} stash={stash.Count}");
        Assert(row.LotPrice(sold) == 25, "Two scrolls from two stash entries pay 25 gp", $"got {row.LotPrice(sold)}");
    }

    private static void TestLockedStashSellsNothing()
    {
        var stash = new PartyStash();
        ItemData scrolls = MakeScroll(3);
        stash.AddItem(scrolls);
        stash.Lock();

        int removed = stash.RemoveUnits(scrolls, 2);
        Assert(removed == 0 && scrolls.StackCount == 3 && stash.Count == 1, "A locked stash gives up no units",
            $"removed={removed} count={scrolls.StackCount}");
        stash.Unlock();
    }

    private static void TestRemoveUnitsIgnoresItemsNotHeld()
    {
        var inv = new global::Inventory();
        ItemData stray = MakeScroll(4);
        Assert(inv.RemoveUnits(stray, 2) == 0 && stray.StackCount == 4,
            "Inventory.RemoveUnits leaves an item it does not hold alone");

        var stash = new PartyStash();
        Assert(stash.RemoveUnits(stray, 2) == 0 && stray.StackCount == 4,
            "PartyStash.RemoveUnits leaves an item it does not hold alone");
    }

    // ===== Wands (DMG p.214) =====

    private static void TestWandsPricedPerWand()
    {
        var stash = new PartyStash();
        ItemData full = MakeWand(50);
        ItemData fullToo = MakeWand(50);
        ItemData low = MakeWand(10);
        stash.AddItem(full);
        stash.AddItem(fullToo);
        stash.AddItem(low);

        // Half of 750 gp prorated by charges: 375 gp at 50/50, 75 gp at 10/50.
        var stacks = new Dictionary<string, SellStack>();
        SellStack fullRow = StoreSelling.AddToStacks(stacks, full, 375, stash, null, "Stash");
        StoreSelling.AddToStacks(stacks, fullToo, 375, stash, null, "Stash");
        SellStack lowRow = StoreSelling.AddToStacks(stacks, low, 75, stash, null, "Stash");

        Assert(stacks.Count == 2 && !ReferenceEquals(fullRow, lowRow),
            "Same-named wands with different charges get separate rows", $"rows={stacks.Count}");
        Assert(fullRow.Quantity == 2 && lowRow.Quantity == 1, "Two full wands share a row; the 10-charge wand has its own",
            $"full={fullRow.Quantity} low={lowRow.Quantity}");

        int sold = StoreSelling.RemoveUnits(lowRow, 1);
        Assert(sold == 1 && lowRow.LotPrice(sold) == 75 && stash.Count == 2,
            "Selling the 10-charge wand pays its own 75 gp and removes only it",
            $"sold={sold} stash={stash.Count}");

        Assert(StoreSelling.BuildStackKey(full, 375) != StoreSelling.BuildStackKey(MakeWand(49), 375),
            "Wands with equal sell price but different charges still get separate rows");
    }

    // ===== Rounding (PHB p.112: half the listed price per item; gold kept in whole gp) =====

    private static void TestLotRoundedDownOnce()
    {
        ItemData scroll = MakeScroll(1);
        Assert(StoreSelling.SellValue(25, scroll) == 12.5m, "A 25 gp scroll is worth 12.5 gp to sell",
            $"got {StoreSelling.SellValue(25, scroll)}");
        Assert(StoreSelling.SellValue(25, scroll, 3) == 37.5m, "Three 25 gp scrolls are worth 37.5 gp",
            $"got {StoreSelling.SellValue(25, scroll, 3)}");
        Assert(StoreSelling.LotPrice(12.5m, 1) == 12 && StoreSelling.LotPrice(12.5m, 2) == 25
                && StoreSelling.LotPrice(12.5m, 3) == 37 && StoreSelling.LotPrice(12.5m, 20) == 250,
            "A lot pays its value rounded down once: 12, 25, 37 and 250 gp for 1, 2, 3 and 20 scrolls");
        Assert(StoreSelling.BuildStackKey(scroll, 12.5m) != StoreSelling.BuildStackKey(scroll, 12m),
            "Items worth 12.5 gp and 12 gp do not share a row");
        Assert(StoreSelling.BuildStackKey(scroll, 12.5m) == StoreSelling.BuildStackKey(scroll, 12.50m),
            "The row key does not depend on the decimal scale of the value");

        // DMG p.214: 375 gp wand (0-level spell) at 25/50 charges is worth 375 / 2 x 25 / 50 = 93.75 gp.
        ItemData wand = MakeWand(25);
        Assert(StoreSelling.SellValue(375, wand) == 93.75m && StoreSelling.LotPrice(StoreSelling.SellValue(375, wand), 1) == 93,
            "A half-charged 375 gp wand is worth 93.75 gp and sells for 93 gp", $"got {StoreSelling.SellValue(375, wand)}");
        Assert(StoreSelling.SellValue(750, MakeWand(0)) == 0m, "An empty wand is worth nothing");
    }

    // ===== EconomyService =====

    private static void TestEconomySellItemSellsOneUnit()
    {
        var go = new GameObject("StoreSellingTest_Economy");
        try
        {
            var svc = go.AddComponent<EconomyService>();
            svc.Initialize(null, () => null, 0);
            ItemData scrolls = MakeScroll(3);
            svc.PartyStash.AddItem(scrolls);

            int unit = svc.GetSellPrice(scrolls);
            Assert(unit == 12, "EconomyService sell price of a 25 gp scroll is 12 gp per scroll", $"got {unit}");
            Assert(svc.CalculateTotalSellValue(new[] { scrolls }) == 37,
                "A stack of three 25 gp scrolls is worth 37 gp (half of 75, rounded down once)",
                $"got {svc.CalculateTotalSellValue(new[] { scrolls })}");

            bool ok = svc.SellItem(scrolls);
            Assert(ok && svc.PartyGold == 12 && scrolls.StackCount == 2 && svc.PartyStash.Count == 1,
                "EconomyService.SellItem sells one scroll of the stack",
                $"ok={ok} gold={svc.PartyGold} left={scrolls.StackCount} stash={svc.PartyStash.Count}");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // ===== StoreInventory prices (scene only) =====

    private static void TestStorePricesPerUnitAndCharges()
    {
        StoreInventory store = StoreInventory.Instance;
        // In Play mode, create the store the way the pre-combat hub does (EconomyService on the scene GameManager).
        if (store == null && Application.isPlaying && GameManager.Instance != null && GameManager.Instance.Economy != null)
            store = GameManager.Instance.Economy.EnsureStoreInventoryInitialized();

        if (store == null)
        {
            Debug.LogWarning("  [StoreSelling] No scene StoreInventory: per-unit and charge price checks not run.");
            return;
        }

        ItemData one = MakePotionStack(1);
        ItemData five = MakePotionStack(5);
        if (one != null && five != null)
        {
            Assert(store.GetSellPrice(one) == store.GetSellPrice(five) && store.GetSellPrice(one) > 0,
                "StoreInventory.GetSellPrice is a per-unit price, the same for one potion and a stack of five",
                $"one={store.GetSellPrice(one)} five={store.GetSellPrice(five)}");
        }

        int fullPrice = store.GetSellPrice(MakeWand(50));
        int lowPrice = store.GetSellPrice(MakeWand(10));
        Assert(fullPrice == 375 && lowPrice == 75,
            "A wand's sell price is half its price prorated by charges (DMG p.214)",
            $"full={fullPrice} low={lowPrice}");
    }
}
}
