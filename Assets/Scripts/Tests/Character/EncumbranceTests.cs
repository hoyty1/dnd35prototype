using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// Tests for carrying capacity, encumbrance tiers, and derived penalties.
/// Run by calling EncumbranceTests.RunAll() from any MonoBehaviour.
/// </summary>
public static class EncumbranceTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== ENCUMBRANCE TESTS ======");

        RaceDatabase.Init();
        ItemDatabase.Init();

        TestCarryingCapacityTable();
        TestEncumbranceTierBoundaries();
        TestMediumLoadPenalties();
        TestHeavyLoadPenaltiesAndSpeed();
        TestOverloadedCannotMove();
        TestMostRestrictiveArmorVsLoadCaps();

        Debug.Log($"====== Encumbrance Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats MakeChar(string name, int str, int dex = 14)
    {
        return new CharacterStats(name, 3, "Fighter",
            str, dex, 14, 10, 10, 10,
            3, 0, 0,
            8, 1, 0,
            6, 1, 24,
            "Human");
    }

    private static void TestCarryingCapacityTable()
    {
        Assert(Mathf.Approximately(CharacterStats.GetHeavyLoadForStrength(14), 175f),
            "STR 14 heavy load is 175 lbs");

        Assert(Mathf.Approximately(CharacterStats.GetHeavyLoadForStrength(18), 300f),
            "STR 18 heavy load is 300 lbs");

        Assert(Mathf.Approximately(CharacterStats.GetHeavyLoadForStrength(30), 1600f),
            "STR 30 heavy load scales by x4 from STR 20 (1600 lbs)");
    }

    private static void TestEncumbranceTierBoundaries()
    {
        float max = 90f;
        Assert(CharacterStats.GetEncumbranceLevel(30f, max) == EncumbranceLevel.Light,
            "Light load includes up to one-third max");
        Assert(CharacterStats.GetEncumbranceLevel(60f, max) == EncumbranceLevel.Medium,
            "Medium load includes up to two-thirds max");
        Assert(CharacterStats.GetEncumbranceLevel(89f, max) == EncumbranceLevel.Heavy,
            "Heavy load covers above two-thirds up to max");
        Assert(CharacterStats.GetEncumbranceLevel(91f, max) == EncumbranceLevel.Overloaded,
            "Overloaded applies above max");
    }

    private static void TestMediumLoadPenalties()
    {
        var stats = MakeChar("MediumLoad", str: 10, dex: 18);
        var inv = new Inventory { OwnerStats = stats };

        inv.AddItem(ItemDatabase.CloneItem("full_plate")); // 50 lbs -> medium for STR 10 (max 100)

        Assert(stats.CurrentEncumbrance == EncumbranceLevel.Medium,
            "STR 10 with 50 lbs is medium load",
            $"got {stats.CurrentEncumbrance}");
        Assert(stats.MaxDexBonus == 3,
            "Medium load applies Max Dex +3 cap",
            $"got {stats.MaxDexBonus}");
        Assert(stats.ArmorCheckPenalty == 3,
            "Medium load applies ACP 3",
            $"got {stats.ArmorCheckPenalty}");
    }

    private static void TestHeavyLoadPenaltiesAndSpeed()
    {
        var stats = MakeChar("HeavyLoad", str: 10, dex: 18);
        var inv = new Inventory { OwnerStats = stats };

        inv.AddItem(ItemDatabase.CloneItem("full_plate"));
        inv.AddItem(ItemDatabase.CloneItem("full_plate")); // 100 lbs total => heavy for STR 10

        Assert(stats.CurrentEncumbrance == EncumbranceLevel.Heavy,
            "STR 10 with 100 lbs is heavy load",
            $"got {stats.CurrentEncumbrance}");
        Assert(stats.MaxDexBonus == 1,
            "Heavy load applies Max Dex +1 cap",
            $"got {stats.MaxDexBonus}");
        Assert(stats.ArmorCheckPenalty == 6,
            "Heavy load applies ACP 6",
            $"got {stats.ArmorCheckPenalty}");
        Assert(stats.SpeedInFeet == 15 && stats.MoveRange == 3,
            "Heavy load reduces 30 ft speed to 15 ft (3 squares)",
            $"got {stats.SpeedInFeet} ft / {stats.MoveRange} sq");
    }

    private static void TestOverloadedCannotMove()
    {
        var stats = MakeChar("Overloaded", str: 10, dex: 18);
        var inv = new Inventory { OwnerStats = stats };

        inv.AddItem(ItemDatabase.CloneItem("full_plate"));
        inv.AddItem(ItemDatabase.CloneItem("full_plate"));
        inv.AddItem(ItemDatabase.CloneItem("full_plate")); // 150 lbs > STR 10 max 100

        Assert(stats.CurrentEncumbrance == EncumbranceLevel.Overloaded,
            "Carrying above heavy max is overloaded",
            $"got {stats.CurrentEncumbrance}");
        Assert(stats.MoveRange == 0 && stats.SpeedInFeet == 0,
            "Overloaded characters cannot move",
            $"got {stats.SpeedInFeet} ft / {stats.MoveRange} sq");
    }

    private static void TestMostRestrictiveArmorVsLoadCaps()
    {
        var stats = MakeChar("MixedCaps", str: 18, dex: 20);
        var inv = new Inventory { OwnerStats = stats };

        inv.DirectEquip(ItemDatabase.CloneItem("full_plate"), EquipSlot.Armor); // Armor cap +1, ACP 6
        inv.AddItem(ItemDatabase.CloneItem("full_plate"));
        inv.AddItem(ItemDatabase.CloneItem("half_plate")); // 50 + 50 + 50 = 150 lbs => medium for STR 18 max 300

        Assert(stats.CurrentEncumbrance == EncumbranceLevel.Medium,
            "150 lbs at STR 18 is medium load",
            $"got {stats.CurrentEncumbrance}");
        Assert(stats.MaxDexBonus == 1,
            "Most restrictive Max Dex cap wins (armor +1 vs load +3)",
            $"got {stats.MaxDexBonus}");
        Assert(stats.ArmorCheckPenalty == 6,
            "Most restrictive ACP wins (armor 6 vs load 3)",
            $"got {stats.ArmorCheckPenalty}");
    }
}

}
