using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Test suite for D&D 3.5 damage modifier calculations.
/// Run by calling DamageModifierTests.RunAll() from any MonoBehaviour.
/// </summary>
public static class DamageModifierTests
{
    private static int _passed = 0;
    private static int _failed = 0;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== D&D 3.5 Damage Modifier Tests ======");

        // Initialize databases
        RaceDatabase.Init();
        ItemDatabase.Init();

        TestGreatswordDamageModifier();
        TestLongswordDamageModifier();
        TestShortbowNoStrDamage();
        TestCompositeLongbow2Capped();
        TestCompositeLongbow2LowStr();
        TestJavelinThrownStr();
        TestOffHandHalfStr();
        TestQuarterstaffTwoHanded();
        TestUnarmedStrike();
        TestSlingNoStr();
        TestCrossbowNoStr();
        TestDartThrown();
        TestCompositeRating0();
        TestNegativeStrComposite();
        TestDamageModifierDescriptions();
        TestWeaponPropertiesSet();

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

    // Create a test character with specific STR
    private static CharacterStats MakeChar(int str, string raceName = "Human")
    {
        return new CharacterStats("TestChar", 3, "Fighter",
            str, 14, 14, 10, 10, 10,
            3, 0, 0,
            8, 1, 0,
            6, 1, 24,
            raceName);
    }

    // ======== Test Cases ========

    static void TestGreatswordDamageModifier()
    {
        // Fighter with STR 18 (+4) using Greatsword: 1.5× STR = +6
        var stats = MakeChar(18);
        var weapon = ItemDatabase.Get("greatsword");
        int mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == 6, "Greatsword 1.5x STR (STR 18, +4 mod, 1.5x = +6)", $"got {mod}");
        Assert(weapon.DmgModType == DamageModifierType.StrengthOneAndHalf, "Greatsword DmgModType is StrengthOneAndHalf");
    }

    static void TestLongswordDamageModifier()
    {
        // Fighter with STR 18 (+4) using Longsword: full STR = +4
        var stats = MakeChar(18);
        var weapon = ItemDatabase.Get("longsword");
        int mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == 4, "Longsword full STR (STR 18, +4)", $"got {mod}");
        Assert(weapon.DmgModType == DamageModifierType.Strength, "Longsword DmgModType is Strength");
    }

    static void TestShortbowNoStrDamage()
    {
        // Rogue with STR 12 (+1) using Shortbow: no STR = 0
        var stats = MakeChar(12);
        var weapon = ItemDatabase.Get("shortbow");
        int mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == 0, "Shortbow no STR modifier (STR 12)", $"got {mod}");
        Assert(weapon.DmgModType == DamageModifierType.None, "Shortbow DmgModType is None");
    }

    static void TestCompositeLongbow2Capped()
    {
        // Fighter with STR 18 (+4) using Composite Longbow +2: capped at +2
        var stats = MakeChar(18);
        var weapon = ItemDatabase.Get("composite_longbow_2");
        Assert(weapon != null, "Composite Longbow +2 exists in database");
        int mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == 2, "Composite Longbow +2 caps STR +4 to +2", $"got {mod}");
        Assert(weapon.CompositeRating == 2, "Composite Longbow +2 has rating 2", $"got {weapon.CompositeRating}");
    }

    static void TestCompositeLongbow2LowStr()
    {
        // Fighter with STR 12 (+1) using Composite Longbow +2: only +1 (lower than rating)
        var stats = MakeChar(12);
        var weapon = ItemDatabase.Get("composite_longbow_2");
        int mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == 1, "Composite Longbow +2 with STR +1 gives +1", $"got {mod}");
    }

    static void TestJavelinThrownStr()
    {
        // Fighter with STR 18 (+4) throwing Javelin: full STR = +4
        var stats = MakeChar(18);
        var weapon = ItemDatabase.Get("javelin");
        int mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == 4, "Javelin thrown full STR (STR 18, +4)", $"got {mod}");
        Assert(weapon.IsThrown, "Javelin is marked as thrown");
        Assert(weapon.DmgModType == DamageModifierType.Strength, "Javelin DmgModType is Strength");
    }

    static void TestOffHandHalfStr()
    {
        // Rogue with STR 12 (+1) dual wielding, off-hand: 0.5× STR +1 = +0
        var stats = MakeChar(12);
        var weapon = ItemDatabase.Get("dagger");
        int mod = stats.GetWeaponDamageModifier(weapon, isOffHand: true);
        Assert(mod == 0, "Off-hand dagger 0.5× STR +1 = +0 (rounded down)", $"got {mod}");
    }

    static void TestQuarterstaffTwoHanded()
    {
        // Quarterstaff is two-handed: 1.5× STR
        var stats = MakeChar(16); // +3 STR mod, 1.5x = +4
        var weapon = ItemDatabase.Get("quarterstaff");
        int mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == 4, "Quarterstaff 1.5x STR (STR 16, +3 mod, 1.5x = +4)", $"got {mod}");
        Assert(weapon.IsTwoHanded, "Quarterstaff is two-handed");
        Assert(weapon.DmgModType == DamageModifierType.StrengthOneAndHalf, "Quarterstaff DmgModType is StrengthOneAndHalf");
    }

    static void TestUnarmedStrike()
    {
        // Unarmed strike: full STR
        var stats = MakeChar(14); // +2
        var weapon = ItemDatabase.Get("unarmed_strike");
        int mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == 2, "Unarmed Strike full STR (STR 14, +2)", $"got {mod}");
        Assert(weapon.DmgModType == DamageModifierType.Strength, "Unarmed Strike DmgModType is Strength");
    }

    static void TestSlingNoStr()
    {
        var stats = MakeChar(18);
        var weapon = ItemDatabase.Get("sling");
        int mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == 0, "Sling no STR modifier", $"got {mod}");
        Assert(weapon.DmgModType == DamageModifierType.None, "Sling DmgModType is None");
    }

    static void TestCrossbowNoStr()
    {
        var stats = MakeChar(18);
        var weapon = ItemDatabase.Get("crossbow_light");
        int mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == 0, "Light Crossbow no STR modifier", $"got {mod}");

        weapon = ItemDatabase.Get("crossbow_heavy");
        mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == 0, "Heavy Crossbow no STR modifier", $"got {mod}");
    }

    static void TestDartThrown()
    {
        var stats = MakeChar(16); // +3
        var weapon = ItemDatabase.Get("dart");
        int mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == 3, "Dart thrown full STR (STR 16, +3)", $"got {mod}");
        Assert(weapon.IsThrown, "Dart is marked as thrown");
    }

    static void TestCompositeRating0()
    {
        // Composite longbow +0: no STR bonus even with high STR
        var stats = MakeChar(18); // +4
        var weapon = ItemDatabase.Get("composite_longbow");
        int mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == 0, "Composite Longbow +0 gives +0 even with STR +4", $"got {mod}");
        Assert(weapon.CompositeRating == 0, "Base composite longbow has rating 0");
    }

    static void TestNegativeStrComposite()
    {
        // Negative STR with composite bow: negative still applies
        var stats = MakeChar(8); // -1 STR mod
        var weapon = ItemDatabase.Get("composite_longbow_2");
        int mod = stats.GetWeaponDamageModifier(weapon);
        Assert(mod == -1, "Composite Longbow +2 with STR -1 gives -1 (negative still applies)", $"got {mod}");
    }

    static void TestDamageModifierDescriptions()
    {
        var stats = MakeChar(14);

        // Greatsword → "1.5× STR"
        var gs = ItemDatabase.Get("greatsword");
        Assert(stats.GetDamageModifierDescription(gs) == "1.5× STR", "Greatsword desc is '1.5× STR'");

        // Longsword → "STR"
        var ls = ItemDatabase.Get("longsword");
        Assert(stats.GetDamageModifierDescription(ls) == "STR", "Longsword desc is 'STR'");

        // Shortbow → ""
        var sb = ItemDatabase.Get("shortbow");
        Assert(stats.GetDamageModifierDescription(sb) == "", "Shortbow desc is empty");

        // Javelin → "thrown, STR"
        var jav = ItemDatabase.Get("javelin");
        Assert(stats.GetDamageModifierDescription(jav) == "thrown, STR", "Javelin desc is 'thrown, STR'");

        // Composite longbow +2 → "composite +2"
        var cl2 = ItemDatabase.Get("composite_longbow_2");
        Assert(stats.GetDamageModifierDescription(cl2) == "composite +2", "Composite Longbow +2 desc is 'composite +2'");

        // Off-hand → "0.5× STR"
        Assert(stats.GetDamageModifierDescription(ls, isOffHand: true) == "0.5× STR", "Off-hand desc is '0.5× STR'");
    }

    static void TestWeaponPropertiesSet()
    {
        // Verify all weapon types have DmgModType set (not default for melee)
        string[] meleeWeapons = { "longsword", "rapier", "scimitar", "battleaxe", "warhammer",
            "mace_heavy", "morningstar", "shortspear", "club", "sickle", "mace_light" };
        foreach (var id in meleeWeapons)
        {
            var w = ItemDatabase.Get(id);
            Assert(w != null && w.DmgModType == DamageModifierType.Strength,
                $"{id} has Strength DmgModType", w == null ? "null" : w.DmgModType.ToString());
        }

        string[] twoHandedWeapons = { "greatsword", "greataxe", "greatclub", "falchion",
            "flail_heavy", "glaive", "quarterstaff", "spear" };
        foreach (var id in twoHandedWeapons)
        {
            var w = ItemDatabase.Get(id);
            Assert(w != null && w.DmgModType == DamageModifierType.StrengthOneAndHalf,
                $"{id} has StrengthOneAndHalf DmgModType", w == null ? "null" : w.DmgModType.ToString());
        }

        string[] rangedNone = { "longbow", "shortbow", "crossbow_light", "crossbow_heavy", "sling" };
        foreach (var id in rangedNone)
        {
            var w = ItemDatabase.Get(id);
            Assert(w != null && w.DmgModType == DamageModifierType.None,
                $"{id} has None DmgModType", w == null ? "null" : w.DmgModType.ToString());
        }

        string[] thrownWeapons = { "javelin", "dart", "dagger", "handaxe", "shortspear", "trident", "spear" };
        foreach (var id in thrownWeapons)
        {
            var w = ItemDatabase.Get(id);
            Assert(w != null && w.IsThrown,
                $"{id} is marked as thrown", w == null ? "null" : w.IsThrown.ToString());
            Assert(w != null && w.IsThrowable,
                $"{id} IsThrowable alias mirrors IsThrown", w == null ? "null" : w.IsThrowable.ToString());
            Assert(w != null && w.ThrowRangeIncrement == w.RangeIncrement,
                $"{id} ThrowRangeIncrement alias mirrors RangeIncrement", w == null ? "null" : $"{w.ThrowRangeIncrement} vs {w.RangeIncrement}");
        }

        // Composite bow variants exist
        for (int r = 1; r <= 4; r++)
        {
            var cl = ItemDatabase.Get($"composite_longbow_{r}");
            Assert(cl != null, $"composite_longbow_{r} exists");
            Assert(cl != null && cl.CompositeRating == r, $"composite_longbow_{r} has rating {r}");

            var cs = ItemDatabase.Get($"composite_shortbow_{r}");
            Assert(cs != null, $"composite_shortbow_{r} exists");
            Assert(cs != null && cs.CompositeRating == r, $"composite_shortbow_{r} has rating {r}");
        }
    }
}

}
