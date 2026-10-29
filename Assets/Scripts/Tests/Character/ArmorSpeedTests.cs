using UnityEngine;
using DND35e.Identifiers;

#pragma warning disable CS0618 // the string item ids, as the other inventory suites use them (EncumbranceTests)

namespace Tests.Character
{
/// <summary>
/// ITM-010 (batch 12 step 24): medium and heavy armor slow every creature (PHB p.123 Table 7-6: 30 ft to 20 ft, 20 ft to
/// 15 ft), as a medium or heavy load does (PHB p.162 Table 9-2), the worse of the two only (PHB p.162: no stacking); a
/// dwarf moves at its speed in any armor and under any load (PHB p.14). Mithral counts one category lighter (DMG p.284).
/// Other speeds take two thirds rounded up to 5 ft (delegated RAW reading 2026-10-29, RULES_COVERAGE.md 4.1: the MM's
/// armored speeds 40 to 30, 50 to 35, 60 to 40). The rule is CharacterStats.EffectiveSpeedFeet, which PCs and NPCs share;
/// the NPC entries that had the reduced speed baked in keep their unarmored speed now. Edit mode; no scene needed.
/// </summary>
public static class ArmorSpeedTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== ARMOR SPEED TESTS (ITM-010) ======");

        RaceDatabase.Init();
        ItemDatabase.Init();
        NPCDatabase.Init();

        TestReducedSpeedTable();
        TestHumanByArmorCategory();
        TestDwarfIsNotSlowed();
        TestTwentyFootRace();
        TestMithral();
        TestArmorAndLoadDoNotStack();
        TestBarbarianFastMovementBeforeArmor();
        TestNpcEntriesKeepUnarmoredSpeed();

        Debug.Log($"====== Armor Speed Results: {_passed} passed, {_failed} failed ======");
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

    private static void AssertEq(int got, int want, string testName) => Assert(got == want, testName, $"got {got}, expected {want}");

    private static CharacterStats Make(string name, string race, string cls = "Fighter", int baseSpeedSquares = 6, int str = 14)
    {
        return new CharacterStats(name, 3, cls,
            str, 12, 14, 10, 10, 10,
            3, 0, 0,
            8, 1, 0,
            baseSpeedSquares, 1, 24,
            race);
    }

    /// <summary>A creature wearing <paramref name="armorId"/> (null: none), with an inventory holding nothing else.</summary>
    private static CharacterStats Wearing(CharacterStats stats, string armorId, ItemMaterialType? material = null)
    {
        var inv = new global::Inventory { OwnerStats = stats };
        if (armorId != null)
        {
            ItemData armor = ItemDatabase.CloneItem(armorId);
            if (material.HasValue)
                armor.Material = MaterialProperties.GetArmorMaterial(material.Value, armor);
            inv.DirectEquip(armor, EquipSlot.Armor);
        }
        inv.RecalculateStats();
        return stats;
    }

    private static void TestReducedSpeedTable()
    {
        Debug.Log("--- The reduced speed (PHB Table 7-6 and Table 9-2; MM armored speeds) ---");
        AssertEq(CharacterStats.ReducedSpeedFeet(30), 20, "30 ft becomes 20 ft (PHB p.123)");
        AssertEq(CharacterStats.ReducedSpeedFeet(20), 15, "20 ft becomes 15 ft (PHB p.123)");
        AssertEq(CharacterStats.ReducedSpeedFeet(40), 30, "40 ft becomes 30 ft (MM p.199 ogre in hide armor)");
        AssertEq(CharacterStats.ReducedSpeedFeet(50), 35, "50 ft becomes 35 ft (MM: 35 ft in hide armor, base 50 ft)");
        AssertEq(CharacterStats.ReducedSpeedFeet(60), 40, "60 ft becomes 40 ft (MM titan in half-plate)");
        AssertEq(CharacterStats.ReducedSpeedFeet(15), 10, "15 ft becomes 10 ft");
        AssertEq(CharacterStats.ReducedSpeedFeet(0), 0, "no speed stays none");
    }

    private static void TestHumanByArmorCategory()
    {
        Debug.Log("--- A human (30 ft) in each armor category ---");
        AssertEq(Wearing(Make("Bare", "Human"), null).EffectiveSpeedFeet, 30, "no armor: 30 ft");
        AssertEq(Wearing(Make("Shirt", "Human"), ItemIDs.CHAIN_SHIRT).EffectiveSpeedFeet, 30, "chain shirt (light): 30 ft");
        AssertEq(Wearing(Make("Chain", "Human"), ItemIDs.CHAINMAIL).EffectiveSpeedFeet, 20, "chainmail (medium): 20 ft (PHB p.123)");
        CharacterStats plate = Wearing(Make("Plate", "Human"), ItemIDs.FULL_PLATE);
        AssertEq(plate.EffectiveSpeedFeet, 20, "full plate (heavy): 20 ft (PHB p.123)");
        AssertEq(plate.MoveRange, 4, "full plate: 4 squares");
    }

    private static void TestDwarfIsNotSlowed()
    {
        Debug.Log("--- A dwarf moves 20 ft in any armor and load (PHB p.14) ---");
        CharacterStats dwarf = Wearing(Make("Dwarf", "Dwarf", baseSpeedSquares: 4), ItemIDs.CHAINMAIL);
        Assert(dwarf.SpeedNotReducedByArmor, "the dwarf race has the exemption");
        AssertEq(dwarf.EffectiveSpeedFeet, 20, "a dwarf in chainmail: 20 ft, unchanged");
        AssertEq(Wearing(Make("DwarfPlate", "Dwarf", baseSpeedSquares: 4), ItemIDs.FULL_PLATE).EffectiveSpeedFeet, 20,
            "a dwarf in full plate: 20 ft, unchanged");
    }

    private static void TestTwentyFootRace()
    {
        Debug.Log("--- A 20-ft race in medium or heavy armor moves 15 ft (PHB p.123) ---");
        AssertEq(Wearing(Make("Halfling", "Halfling", baseSpeedSquares: 4), null).EffectiveSpeedFeet, 20, "an unarmored halfling: 20 ft");
        AssertEq(Wearing(Make("HalflingChain", "Halfling", baseSpeedSquares: 4), ItemIDs.CHAINMAIL).EffectiveSpeedFeet, 15,
            "a halfling in chainmail: 15 ft");
        AssertEq(Wearing(Make("GnomeShirt", "Gnome", baseSpeedSquares: 4), ItemIDs.CHAIN_SHIRT).EffectiveSpeedFeet, 20,
            "a gnome in a chain shirt (light): 20 ft");
    }

    private static void TestMithral()
    {
        Debug.Log("--- Mithral armor is one category lighter for movement (DMG p.284) ---");
        AssertEq(Wearing(Make("MithralBreast", "Human"), ItemIDs.BREASTPLATE, ItemMaterialType.Mithral).EffectiveSpeedFeet, 30,
            "a mithral breastplate counts as light armor: 30 ft");
        AssertEq(Wearing(Make("MithralPlate", "Human"), ItemIDs.FULL_PLATE, ItemMaterialType.Mithral).EffectiveSpeedFeet, 20,
            "mithral full plate counts as medium armor: still 20 ft");
    }

    private static void TestArmorAndLoadDoNotStack()
    {
        Debug.Log("--- Armor and load: the worse figure, not both (PHB p.162) ---");
        // STR 10 carries 33 lb light, 66 lb medium, 100 lb heavy (PHB Table 9-1).
        CharacterStats both = Make("ChainAndLoad", "Human", str: 10);
        var inv = new global::Inventory { OwnerStats = both };
        inv.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAINMAIL), EquipSlot.Armor); // 40 lb: medium load
        inv.RecalculateStats();
        Assert(both.CurrentEncumbrance == EncumbranceLevel.Medium, "STR 10 in chainmail (40 lb) carries a medium load", $"got {both.CurrentEncumbrance}");
        AssertEq(both.EffectiveSpeedFeet, 20, "chainmail and a medium load: 20 ft, not reduced twice");

        CharacterStats heavyLoad = Make("HeavyLoad", "Human", str: 10);
        var inv2 = new global::Inventory { OwnerStats = heavyLoad };
        inv2.AddItem(ItemDatabase.CloneItem(ItemIDs.FULL_PLATE));
        inv2.AddItem(ItemDatabase.CloneItem(ItemIDs.FULL_PLATE)); // 100 lb: heavy load, no armor worn
        Assert(heavyLoad.CurrentEncumbrance == EncumbranceLevel.Heavy, "STR 10 with 100 lb carries a heavy load", $"got {heavyLoad.CurrentEncumbrance}");
        AssertEq(heavyLoad.EffectiveSpeedFeet, 20, "a heavy load alone: 20 ft (PHB Table 9-2)");
    }

    private static void TestBarbarianFastMovementBeforeArmor()
    {
        Debug.Log("--- Barbarian fast movement is added before the armor reduction (PHB p.25) ---");
        AssertEq(Wearing(Make("BarbShirt", "Human", "Barbarian"), ItemIDs.CHAIN_SHIRT).EffectiveSpeedFeet, 40,
            "a human barbarian in a chain shirt: 30 + 10 = 40 ft");
        AssertEq(Wearing(Make("BarbHide", "Human", "Barbarian"), ItemIDs.HIDE_ARMOR).EffectiveSpeedFeet, 30,
            "a human barbarian in hide armor (medium): 40 ft becomes 30 ft");
        AssertEq(Wearing(Make("BarbPlate", "Human", "Barbarian"), ItemIDs.FULL_PLATE).EffectiveSpeedFeet, 20,
            "a human barbarian in full plate (heavy): no fast movement, 30 ft becomes 20 ft");
    }

    private static void TestNpcEntriesKeepUnarmoredSpeed()
    {
        Debug.Log("--- NPC entries store their unarmored speed; the armor slows them as it slows a PC ---");
        (string id, int squares)[] entries =
        {
            ("human_paladin_3", 6), ("human_paladin_5", 6), ("human_paladin_7", 6), ("hobgoblin_sergeant", 6),
            ("janni", 6), ("ogre", 8), ("hill_giant", 8), ("stone_giant", 8), ("fire_giant", 8)
        };
        foreach ((string id, int squares) in entries)
        {
            NPCDefinition def = NPCDatabase.Get(id);
            Assert(def != null && def.BaseSpeed == squares, $"{id} stores its unarmored speed ({squares * 5} ft)",
                def == null ? "missing" : $"BaseSpeed {def.BaseSpeed}");
        }
    }
}

}
