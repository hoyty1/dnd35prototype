using UnityEngine;
using Tests.Utilities;

namespace Tests.Services
{
/// <summary>
/// Unit tests for EconomyService — verifies gold transactions, shop pricing,
/// CanAfford logic, and buy/sell operations per D&amp;D 3.5e rules.
/// Run with EconomyServiceTests.RunAll().
/// </summary>
public static class EconomyServiceTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== ECONOMY SERVICE TESTS ======");

        TestHelpers.EnsureCoreDatabasesInitialized();

        TestInitialGold();
        TestAddGold();
        TestAddGoldZero();
        TestAddGoldNegative();
        TestSpendGoldSuccess();
        TestSpendGoldFailure();
        TestSpendGoldExact();
        TestSpendGoldZero();
        TestCanAfford();
        TestCanAffordExact();
        TestCanAffordZero();
        TestCanAffordNegative();
        TestGoldClampedToZero();
        TestBuyPrice();
        TestSellPrice();
        TestSellPriceHalfRounding();
        TestBuyPriceNull();
        TestSellPriceNull();
        TestOnGoldChangedEvent();

        Debug.Log($"====== Economy Service Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  ✅ PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  ❌ FAIL: {testName} {detail}");
        }
    }

    private static EconomyService CreateService(int startingGold = 1000)
    {
        var go = new GameObject("EconomyTest");
        var service = go.AddComponent<EconomyService>();
        // Use null GameManager and CombatUI for unit tests
        service.Initialize(null, () => null, startingGold);
        return service;
    }

    private static void DestroyService(EconomyService service)
    {
        if (service != null && service.gameObject != null)
            Object.DestroyImmediate(service.gameObject);
    }

    // ===== INITIAL STATE =====

    private static void TestInitialGold()
    {
        var svc = CreateService(500);
        Assert(svc.PartyGold == 500, "Initial gold = 500", $"got {svc.PartyGold}");
        DestroyService(svc);
    }

    // ===== ADD GOLD =====

    private static void TestAddGold()
    {
        var svc = CreateService(100);
        svc.AddGold(250);
        Assert(svc.PartyGold == 350, "Add 250 gold: 100 + 250 = 350", $"got {svc.PartyGold}");
        DestroyService(svc);
    }

    private static void TestAddGoldZero()
    {
        var svc = CreateService(100);
        svc.AddGold(0);
        Assert(svc.PartyGold == 100, "Add 0 gold: no change", $"got {svc.PartyGold}");
        DestroyService(svc);
    }

    private static void TestAddGoldNegative()
    {
        var svc = CreateService(100);
        svc.AddGold(-50);
        Assert(svc.PartyGold == 100, "Add negative gold: no change", $"got {svc.PartyGold}");
        DestroyService(svc);
    }

    // ===== SPEND GOLD =====

    private static void TestSpendGoldSuccess()
    {
        var svc = CreateService(500);
        bool success = svc.SpendGold(200);
        Assert(success, "Spend 200 from 500: success");
        Assert(svc.PartyGold == 300, "After spend: 500 - 200 = 300", $"got {svc.PartyGold}");
        DestroyService(svc);
    }

    private static void TestSpendGoldFailure()
    {
        var svc = CreateService(100);
        bool success = svc.SpendGold(200);
        Assert(!success, "Spend 200 from 100: fails");
        Assert(svc.PartyGold == 100, "After failed spend: gold unchanged", $"got {svc.PartyGold}");
        DestroyService(svc);
    }

    private static void TestSpendGoldExact()
    {
        var svc = CreateService(100);
        bool success = svc.SpendGold(100);
        Assert(success, "Spend exact amount: success");
        Assert(svc.PartyGold == 0, "After exact spend: gold = 0", $"got {svc.PartyGold}");
        DestroyService(svc);
    }

    private static void TestSpendGoldZero()
    {
        var svc = CreateService(100);
        bool success = svc.SpendGold(0);
        Assert(success, "Spend 0: success");
        Assert(svc.PartyGold == 100, "Spend 0: gold unchanged", $"got {svc.PartyGold}");
        DestroyService(svc);
    }

    // ===== CAN AFFORD =====

    private static void TestCanAfford()
    {
        var svc = CreateService(500);
        Assert(svc.CanAfford(200), "Can afford 200 with 500 gold");
        Assert(!svc.CanAfford(600), "Cannot afford 600 with 500 gold");
        DestroyService(svc);
    }

    private static void TestCanAffordExact()
    {
        var svc = CreateService(100);
        Assert(svc.CanAfford(100), "Can afford exact amount");
        DestroyService(svc);
    }

    private static void TestCanAffordZero()
    {
        var svc = CreateService(0);
        Assert(svc.CanAfford(0), "Can afford 0 with 0 gold");
        DestroyService(svc);
    }

    private static void TestCanAffordNegative()
    {
        var svc = CreateService(0);
        Assert(svc.CanAfford(-1), "Can afford negative amount (trivially true)");
        DestroyService(svc);
    }

    // ===== GOLD CLAMPING =====

    private static void TestGoldClampedToZero()
    {
        var svc = CreateService(0);
        svc.PartyGold = -100;
        Assert(svc.PartyGold == 0, "Negative gold clamped to 0", $"got {svc.PartyGold}");
        DestroyService(svc);
    }

    // ===== PRICING =====

    private static void TestBuyPrice()
    {
        var svc = CreateService();
        var item = ItemDatabase.GetItem("longsword");
        if (item != null)
        {
            int price = svc.GetBuyPrice(item);
            Assert(price == item.BasePriceGp, $"Buy price = base price ({item.BasePriceGp} gp)", $"got {price}");
        }
        else
        {
            Assert(true, "Buy price: longsword not in DB (skip)");
        }
        DestroyService(svc);
    }

    private static void TestSellPrice()
    {
        var svc = CreateService();
        var item = ItemDatabase.GetItem("longsword");
        if (item != null)
        {
            int sell = svc.GetSellPrice(item);
            int expected = item.BasePriceGp / 2;
            Assert(sell == expected, $"Sell price = half base ({expected} gp)", $"got {sell}");
        }
        else
        {
            Assert(true, "Sell price: longsword not in DB (skip)");
        }
        DestroyService(svc);
    }

    private static void TestSellPriceHalfRounding()
    {
        var svc = CreateService();
        // Create an item with odd price to test floor division
        var item = new ItemData();
        item.Name = "TestOddItem";
        item.BasePriceGp = 15; // 15 / 2 = 7 (integer division)
        int sell = svc.GetSellPrice(item);
        Assert(sell == 7, "Sell price floors: 15/2 = 7", $"got {sell}");
        DestroyService(svc);
    }

    private static void TestBuyPriceNull()
    {
        var svc = CreateService();
        int price = svc.GetBuyPrice(null);
        Assert(price == 0, "Null item buy price = 0", $"got {price}");
        DestroyService(svc);
    }

    private static void TestSellPriceNull()
    {
        var svc = CreateService();
        int price = svc.GetSellPrice(null);
        Assert(price == 0, "Null item sell price = 0", $"got {price}");
        DestroyService(svc);
    }

    // ===== EVENTS =====

    private static void TestOnGoldChangedEvent()
    {
        var svc = CreateService(100);
        int eventFiredCount = 0;
        int lastGoldValue = -1;

        svc.OnGoldChanged += gold =>
        {
            eventFiredCount++;
            lastGoldValue = gold;
        };

        svc.AddGold(50);
        Assert(eventFiredCount == 1, "OnGoldChanged fired on AddGold", $"count={eventFiredCount}");
        Assert(lastGoldValue == 150, "OnGoldChanged reports new total", $"got {lastGoldValue}");

        svc.SpendGold(30);
        Assert(eventFiredCount == 2, "OnGoldChanged fired on SpendGold", $"count={eventFiredCount}");
        Assert(lastGoldValue == 120, "OnGoldChanged reports after spend", $"got {lastGoldValue}");

        DestroyService(svc);
    }
}
}
