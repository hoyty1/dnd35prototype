using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Verifies D&D 3.5 size-based damage scaling for manufactured, natural, and resized attacks.
/// Run with SizeDamageScalingTests.RunAll().
///
/// Expected values are transcribed here on their own (not read from WeaponDamageScaler), so a wrong production
/// row fails: DMG Table 2-2 (increasing weapon damage by size) and Table 2-3 (decreasing), p.28, checked in the
/// DMG on 2026-10-08; MM Table 4-3 (increased damage by size, repeated per category), p.291; PHB Table 7-5
/// (Small and Medium weapon damage) for the unarmed strike and the longsword.
/// "-" is a step the DMG leaves blank (less than 1 point, no longer a weapon); the game keeps 1 point (CMB-133).
/// </summary>
public static class SizeDamageScalingTests
{
    private static int _passed;
    private static int _failed;

    // Medium damage -> Fine, Diminutive, Tiny, Small, Medium, Large, Huge, Gargantuan, Colossal (DMG p.28).
    private static readonly Dictionary<string, string[]> DmgTables = new Dictionary<string, string[]>
    {
        { "1d2",  new[] { "-",   "-",    "-",    "1",    "1d2",  "1d3", "1d4", "1d6", "1d8"  } },
        { "1d3",  new[] { "-",   "-",    "1",    "1d2",  "1d3",  "1d4", "1d6", "1d8", "2d6"  } },
        { "1d4",  new[] { "-",   "1",    "1d2",  "1d3",  "1d4",  "1d6", "1d8", "2d6", "3d6"  } },
        { "1d6",  new[] { "1",   "1d2",  "1d3",  "1d4",  "1d6",  "1d8", "2d6", "3d6", "4d6"  } },
        { "1d8",  new[] { "1d2", "1d3",  "1d4",  "1d6",  "1d8",  "2d6", "3d6", "4d6", "6d6"  } },
        { "1d10", new[] { "1d3", "1d4",  "1d6",  "1d8",  "1d10", "2d8", "3d8", "4d8", "6d8"  } },
        { "1d12", new[] { "1d4", "1d6",  "1d8",  "1d10", "1d12", "3d6", "4d6", "6d6", "8d6"  } },
        { "2d4",  new[] { "1d2", "1d3",  "1d4",  "1d6",  "2d4",  "2d6", "3d6", "4d6", "6d6"  } },
        { "2d6",  new[] { "1d4", "1d6",  "1d8",  "1d10", "2d6",  "3d6", "4d6", "6d6", "8d6"  } },
        { "2d8",  new[] { "1d6", "1d8",  "1d10", "2d6",  "2d8",  "3d8", "4d8", "6d8", "8d8"  } },
        { "2d10", new[] { "1d8", "1d10", "2d6",  "2d8",  "2d10", "4d8", "6d8", "8d8", "12d8" } },
    };

    // A representative PHB weapon for each Medium damage value. The PHB has no Medium weapon dealing 1d2, 2d8 or
    // 2d10; those rows use a longsword clone with its dice overwritten (never the database template).
    private static readonly Dictionary<string, string> Representative = new Dictionary<string, string>
    {
        { "1d3", ItemIDs.GAUNTLET },
        { "1d4", ItemIDs.DAGGER },
        { "1d6", ItemIDs.SHORT_SWORD },
        { "1d8", ItemIDs.LONGSWORD },
        { "1d10", ItemIDs.GREATCLUB },
        { "1d12", ItemIDs.GREATAXE },
        { "2d4", ItemIDs.FALCHION },
        { "2d6", ItemIDs.GREATSWORD },
    };

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SIZE DAMAGE SCALING TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();

        TestWeaponProgressionLongsword();
        TestWeaponProgressionGreatsword();
        TestEveryDmgTableRowWithARepresentativeWeapon();
        TestUnarmedStrikeBySize();
        TestNaturalAttackScalingFromLargeToMedium();
        TestNonMediumSourcesStepPerCategory();
        TestSizeChangeRecalculatesEquippedWeaponDamage();
        TestSmallRaceWielderGetsSmallDamage();
        TestThrownWeaponKeepsNormalSizeAfterResize();
        TestUnarmedFallbackStatsBySize();

        Debug.Log($"====== Size Damage Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats MakeFighterStats(string name, string race = "Human")
    {
        return new CharacterStats(name, 3, "Fighter",
            16, 14, 14, 10, 10, 10,
            3, 0, 0,
            8, 1, 0,
            6, 1, 24,
            race);
    }

    private static CharacterController BuildEquippedCharacter(string name, string weaponId, string race = "Human")
    {
        var go = new GameObject($"Size_Test_{name}");
        var controller = go.AddComponent<CharacterController>();
        var inventoryComp = go.AddComponent<InventoryComponent>();

        CharacterStats stats = MakeFighterStats(name, race);
        controller.Stats = stats;

        inventoryComp.Init(stats);
        inventoryComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(weaponId), EquipSlot.RightHand);
        inventoryComp.CharacterInventory.RecalculateStats();

        return controller;
    }

    private static CharacterController BuildUnarmedCharacter(string name, string race = "Human", string characterClass = "Fighter")
    {
        var go = new GameObject($"Size_Test_{name}");
        var controller = go.AddComponent<CharacterController>();
        var inventoryComp = go.AddComponent<InventoryComponent>();

        CharacterStats stats = new CharacterStats(name, 3, characterClass,
            16, 14, 14, 10, 10, 10,
            3, 0, 0,
            8, 1, 0,
            6, 1, 24,
            race);
        controller.Stats = stats;

        inventoryComp.Init(stats);
        inventoryComp.CharacterInventory.RecalculateStats();
        return controller;
    }

    private static void DestroyTestCharacter(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static void AssertScaled(ItemData weapon, SizeCategory targetSize, int expectedCount, int expectedDice, string label)
    {
        weapon.GetScaledDamageDice(targetSize, out int scaledCount, out int scaledDice);
        Assert(scaledCount == expectedCount && scaledDice == expectedDice,
            label,
            $"expected {expectedCount}d{expectedDice}, got {scaledCount}d{scaledDice}");
    }

    /// <summary>A database clone by string id (the ItemIDs constants are strings, like the rest of this suite).</summary>
    private static ItemData CloneById(string id)
    {
#pragma warning disable CS0618
        return ItemDatabase.CloneItem(id);
#pragma warning restore CS0618
    }

    /// <summary>"-" (no damage in the DMG) is kept at 1 point by the game (CMB-133).</summary>
    private static string GameValue(string dmgValue) => dmgValue == "-" ? "1" : dmgValue;

    private static string Expr(int count, int dice) => WeaponDamageScaler.ToExpression(count, dice);

    private static string ScaleExpr(int count, int dice, SizeCategory from, SizeCategory to)
    {
        bool ok = WeaponDamageScaler.TryScaleDamageDice(count, dice, from, to, out int c, out int d);
        return ok ? Expr(c, d) : "unscaled";
    }

    private static void TestWeaponProgressionLongsword()
    {
        ItemData longsword = ItemDatabase.CloneItem(ItemIDs.LONGSWORD);
        AssertScaled(longsword, SizeCategory.Small, 1, 6, "Longsword Medium->Small = 1d6");
        AssertScaled(longsword, SizeCategory.Medium, 1, 8, "Longsword Medium->Medium = 1d8");
        AssertScaled(longsword, SizeCategory.Large, 2, 6, "Longsword Medium->Large = 2d6");
    }

    private static void TestWeaponProgressionGreatsword()
    {
        ItemData greatsword = ItemDatabase.CloneItem(ItemIDs.GREATSWORD);
        AssertScaled(greatsword, SizeCategory.Small, 1, 10, "Greatsword Medium->Small = 1d10");
        AssertScaled(greatsword, SizeCategory.Medium, 2, 6, "Greatsword Medium->Medium = 2d6");
        AssertScaled(greatsword, SizeCategory.Large, 3, 6, "Greatsword Medium->Large = 3d6");
    }

    /// <summary>Every row of DMG Tables 2-2 and 2-3, Fine to Colossal, through ItemData.GetScaledDamageDice.</summary>
    private static void TestEveryDmgTableRowWithARepresentativeWeapon()
    {
        foreach (KeyValuePair<string, string[]> row in DmgTables)
        {
            string medium = row.Key;
            ItemData weapon;
            string weaponName;
            if (Representative.TryGetValue(medium, out string id))
            {
                weapon = CloneById(id);
                weaponName = weapon != null ? weapon.Name : id;
                bool baseOk = weapon != null && Expr(weapon.DamageCount, weapon.DamageDice) == medium
                    && weapon.DesignedForSize == SizeCategory.Medium;
                Assert(baseOk, $"{weaponName} is a Medium {medium} weapon (PHB Table 7-5)",
                    weapon != null ? $"got {Expr(weapon.DamageCount, weapon.DamageDice)} designed for {weapon.DesignedForSize}" : "item missing");
                if (!baseOk)
                    continue;
            }
            else
            {
                weapon = CloneById(ItemIDs.LONGSWORD);
                string[] parts = medium.Split('d');
                weapon.DamageCount = int.Parse(parts[0]);
                weapon.DamageDice = int.Parse(parts[1]);
                weaponName = $"Medium {medium} weapon (longsword clone)";
            }

            var mismatches = new List<string>();
            for (int i = 0; i < row.Value.Length; i++)
            {
                SizeCategory size = (SizeCategory)i;
                weapon.GetScaledDamageDice(size, out int count, out int dice);
                string got = Expr(count, dice);
                string want = GameValue(row.Value[i]);
                if (got != want)
                    mismatches.Add($"{size}: expected {want}{(row.Value[i] == "-" ? " (DMG blank, CMB-133)" : "")}, got {got}");
            }
            Assert(mismatches.Count == 0,
                $"{weaponName} {medium} Fine..Colossal = {string.Join(" ", row.Value)} (DMG Tables 2-2/2-3)",
                string.Join("; ", mismatches));
        }

        foreach (string key in WeaponDamageScaler.MediumTableKeys)
            Assert(DmgTables.ContainsKey(key), $"Scaler row {key} is a DMG Table 2-2/2-3 row");
        Assert(new List<string>(WeaponDamageScaler.MediumTableKeys).Count == DmgTables.Count,
            $"Scaler has all {DmgTables.Count} DMG rows");
    }

    /// <summary>PHB Table 7-5: unarmed strike 1d2 Small, 1d3 Medium; one size up 1d4 (DMG Table 2-2).</summary>
    private static void TestUnarmedStrikeBySize()
    {
        Assert(ScaleExpr(1, 3, SizeCategory.Medium, SizeCategory.Small) == "1d2", "Unarmed strike Medium 1d3 -> Small 1d2 (PHB Table 7-5)",
            "got " + ScaleExpr(1, 3, SizeCategory.Medium, SizeCategory.Small));
        Assert(ScaleExpr(1, 3, SizeCategory.Medium, SizeCategory.Large) == "1d4", "Unarmed strike Medium 1d3 -> Large 1d4 (DMG Table 2-2)",
            "got " + ScaleExpr(1, 3, SizeCategory.Medium, SizeCategory.Large));
    }

    private static void TestNaturalAttackScalingFromLargeToMedium()
    {
        CharacterStats stats = MakeFighterStats("NaturalScaler");
        stats.SetBaseSizeCategory(SizeCategory.Large);
        stats.SetNaturalAttacks(new List<NaturalAttackDefinition>
        {
            new NaturalAttackDefinition
            {
                Name = "Bite",
                DamageCount = 1,
                DamageDice = 8,
                IsPrimary = true,
                Count = 1
            }
        });

        NaturalAttackDefinition bite = stats.GetPrimaryNaturalAttack();
        stats.GetScaledNaturalAttackDamage(bite, out int largeCount, out int largeDice);
        Assert(largeCount == 1 && largeDice == 8,
            "Natural bite at base Large remains 1d8",
            $"expected 1d8, got {largeCount}d{largeDice}");

        stats.TryShiftCurrentSize(-1); // Large -> Medium
        bite = stats.GetPrimaryNaturalAttack();
        stats.GetScaledNaturalAttackDamage(bite, out int mediumCount, out int mediumDice);
        Assert(mediumCount == 1 && mediumDice == 6,
            "Natural bite Large->Medium becomes 1d6",
            $"expected 1d6, got {mediumCount}d{mediumDice}");

        stats.TryShiftCurrentSize(+2); // Medium -> Huge (one above base)
        bite = stats.GetPrimaryNaturalAttack();
        stats.GetScaledNaturalAttackDamage(bite, out int hugeCount, out int hugeDice);
        Assert(hugeCount == 2 && hugeDice == 6,
            "Natural bite Large->Huge becomes 2d6 (MM Table 4-3: 1d8 -> 2d6)",
            $"expected 2d6, got {hugeCount}d{hugeDice}");
    }

    /// <summary>
    /// A source that is not Medium scales one category at a time (MM Table 4-3 p.291 repeats its step; the DMG
    /// tables' "One" columns for a decrease).
    /// </summary>
    private static void TestNonMediumSourcesStepPerCategory()
    {
        void Check(int count, int dice, SizeCategory from, SizeCategory to, string want, string why)
        {
            string got = ScaleExpr(count, dice, from, to);
            Assert(got == want, $"{Expr(count, dice)} {from}->{to} = {want} ({why})", "got " + got);
        }

        Check(1, 4, SizeCategory.Medium, SizeCategory.Huge, "1d8", "MM Table 4-3 example: two steps, 1d4 claw -> 1d8");
        Check(1, 10, SizeCategory.Large, SizeCategory.Huge, "2d8", "MM Table 4-3: 1d10 -> 2d8");
        Check(2, 6, SizeCategory.Large, SizeCategory.Huge, "3d6", "MM Table 4-3: 2d6 -> 3d6");
        Check(2, 8, SizeCategory.Large, SizeCategory.Gargantuan, "4d8", "MM Table 4-3: 2d8 -> 3d8, then the ladder 3d8 -> 4d8");
        Check(1, 6, SizeCategory.Small, SizeCategory.Medium, "1d8", "one step up from 1d6");
        Check(2, 6, SizeCategory.Large, SizeCategory.Medium, "1d10", "DMG Table 2-3 one step down from 2d6");
        Check(1, 8, SizeCategory.Large, SizeCategory.Small, "1d4", "DMG Table 2-3 two steps down from 1d8");
        Check(3, 6, SizeCategory.Huge, SizeCategory.Gargantuan, "4d6", "ladder 3d6 -> 4d6");
        Check(1, 2, SizeCategory.Tiny, SizeCategory.Fine, "1", "1d2 -> 1 -> less than 1 (DMG blank; game keeps 1, CMB-133)");
        Check(1, 20, SizeCategory.Large, SizeCategory.Huge, "unscaled", "1d20 is in no table");
    }

    private static void TestSizeChangeRecalculatesEquippedWeaponDamage()
    {
        CharacterController controller = null;
        try
        {
            controller = BuildEquippedCharacter("ResizeLongsword", ItemIDs.LONGSWORD);

            Assert(controller.Stats.BaseDamageCount == 1 && controller.Stats.BaseDamageDice == 8,
                "Initial equipped longsword damage is 1d8",
                $"got {controller.Stats.BaseDamageCount}d{controller.Stats.BaseDamageDice}");

            bool enlarged = controller.ChangeSize(+1);
            Assert(enlarged, "Enlarge size change succeeded");
            Assert(controller.Stats.BaseDamageCount == 2 && controller.Stats.BaseDamageDice == 6,
                "After enlarge, equipped longsword damage is 2d6",
                $"got {controller.Stats.BaseDamageCount}d{controller.Stats.BaseDamageDice}");

            bool reduced = controller.ChangeSize(-2);
            Assert(reduced, "Reduce size change succeeded");
            Assert(controller.Stats.BaseDamageCount == 1 && controller.Stats.BaseDamageDice == 6,
                "After enlarge then reduce to Small, equipped longsword damage is 1d6",
                $"got {controller.Stats.BaseDamageCount}d{controller.Stats.BaseDamageDice}");
        }
        finally
        {
            DestroyTestCharacter(controller);
        }
    }

    /// <summary>
    /// PHB p.227 (Enlarge Person) and p.269 (Reduce Person): an item that leaves the resized creature returns to its
    /// normal size, so a thrown weapon deals its normal damage; melee damage stays resized; a launcher is not thrown.
    /// </summary>
    private static void TestThrownWeaponKeepsNormalSizeAfterResize()
    {
        var ranged = new RangeInfo { IsMelee = false, IsInRange = true };
        var melee = new RangeInfo { IsMelee = true, IsInRange = true };
        ItemData dagger = CloneById(ItemIDs.DAGGER);
        ItemData longbow = CloneById(ItemIDs.LONGBOW);

        Assert(CharacterController.IsThrownWeaponAttack(dagger, ranged), "A dagger attack at range is a thrown attack");
        Assert(!CharacterController.IsThrownWeaponAttack(dagger, melee), "A dagger attack in melee is not a thrown attack");
        Assert(!CharacterController.IsThrownWeaponAttack(dagger, null), "A dagger attack with no range info is not a thrown attack");
        Assert(longbow != null && !CharacterController.IsThrownWeaponAttack(longbow, ranged), "A longbow shot is not a thrown attack (projectiles use the launcher's size)");

        CharacterController controller = null;
        try
        {
            controller = BuildEquippedCharacter("ThrownDagger", ItemIDs.DAGGER);
            controller.ChangeSize(+1); // Enlarge Person: Medium -> Large

            controller.GetScaledWeaponDamageDice(dagger, out int meleeCount, out int meleeDice, false);
            Assert(Expr(meleeCount, meleeDice) == "1d6", "An enlarged human's dagger in melee deals 1d6 (DMG Table 2-2)", "got " + Expr(meleeCount, meleeDice));
            controller.GetScaledWeaponDamageDice(dagger, out int thrownCount, out int thrownDice, true);
            Assert(Expr(thrownCount, thrownDice) == "1d4", "An enlarged human's thrown dagger deals 1d4 (Enlarge Person, PHB p.227)", "got " + Expr(thrownCount, thrownDice));

            controller.ChangeSize(-2); // Reduce Person from Medium: Small
            controller.GetScaledWeaponDamageDice(dagger, out meleeCount, out meleeDice, false);
            Assert(Expr(meleeCount, meleeDice) == "1d3", "A reduced human's dagger in melee deals 1d3 (DMG Table 2-3)", "got " + Expr(meleeCount, meleeDice));
            controller.GetScaledWeaponDamageDice(dagger, out thrownCount, out thrownDice, true);
            Assert(Expr(thrownCount, thrownDice) == "1d4", "A reduced human's thrown dagger deals 1d4 (Reduce Person, PHB p.269)", "got " + Expr(thrownCount, thrownDice));
        }
        finally
        {
            DestroyTestCharacter(controller);
        }

        try
        {
            controller = BuildEquippedCharacter("HalflingThrownDagger", ItemIDs.DAGGER, "Halfling");
            controller.GetScaledWeaponDamageDice(dagger, out int count, out int dice, true);
            Assert(Expr(count, dice) == "1d3", "A halfling's thrown dagger deals 1d3 (its normal Small size, PHB Table 7-5)", "got " + Expr(count, dice));
        }
        finally
        {
            DestroyTestCharacter(controller);
        }
    }

    /// <summary>
    /// The unarmed stats fallback (Inventory.RecalculateStats, read by the confused self-attack, Mirror Image and the
    /// inventory summary) is resized like the attack code: PHB Table 7-5 (1d2 Small, 1d3 Medium), DMG Table 2-2
    /// (1d4 Large); a monk's 1d6 (PHB Table 3-10) is 1d4 when Small.
    /// </summary>
    private static void TestUnarmedFallbackStatsBySize()
    {
        void CheckStats(string label, string race, string cls, int sizeShift, string want)
        {
            CharacterController controller = null;
            try
            {
                controller = BuildUnarmedCharacter(label, race, cls);
                if (sizeShift != 0)
                    controller.ChangeSize(sizeShift);
                string got = Expr(controller.Stats.BaseDamageCount, controller.Stats.BaseDamageDice);
                Assert(got == want, $"{label}: unarmed stats damage is {want}", "got " + got);
            }
            finally
            {
                DestroyTestCharacter(controller);
            }
        }

        CheckStats("Unarmed human", "Human", "Fighter", 0, "1d3");
        CheckStats("Unarmed halfling", "Halfling", "Fighter", 0, "1d2");
        CheckStats("Unarmed enlarged human", "Human", "Fighter", +1, "1d4");
        CheckStats("Unarmed human monk", "Human", "Monk", 0, "1d6");
        CheckStats("Unarmed halfling monk", "Halfling", "Monk", 0, "1d4");
    }

    /// <summary>A halfling (Small) wielding a longsword deals 1d6 (PHB Table 7-5, Small longsword).</summary>
    private static void TestSmallRaceWielderGetsSmallDamage()
    {
        CharacterController controller = null;
        try
        {
            controller = BuildEquippedCharacter("HalflingLongsword", ItemIDs.LONGSWORD, "Halfling");
            Assert(controller.Stats.CurrentSizeCategory == SizeCategory.Small,
                "A halfling is Small", $"got {controller.Stats.CurrentSizeCategory}");
            Assert(controller.Stats.BaseDamageCount == 1 && controller.Stats.BaseDamageDice == 6,
                "A halfling's longsword deals 1d6 (PHB Table 7-5)",
                $"got {controller.Stats.BaseDamageCount}d{controller.Stats.BaseDamageDice}");
        }
        finally
        {
            DestroyTestCharacter(controller);
        }
    }
}
}
