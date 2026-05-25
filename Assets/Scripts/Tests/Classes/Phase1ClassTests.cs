using System.Collections.Generic;
using UnityEngine;

namespace Tests.Classes
{
/// <summary>
/// Phase 1 class implementation tests:
/// 1. Sorcerer class + spontaneous casting framework
/// 2. Fighter bonus feat system
/// 3. Barbarian rage scaling (Normal/Greater/Mighty), DR, Tireless Rage
///
/// D&D 3.5e PHB accuracy verified against tables on p.25 (Barbarian), p.37 (Fighter), p.51 (Sorcerer).
/// </summary>
public static class Phase1ClassTests
{
    private static int _passed;
    private static int _failed;

    public static void phase1_class_tests() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== PHASE 1 CLASS TESTS ======");
        RaceDatabase.Init();
        ClassRegistry.Init();
        SpellDatabase.Init();

        // Sorcerer Tests
        TestSorcererClassRegistered();
        TestSorcererClassProperties();
        TestSorcererQuickStart();
        TestSpontaneousCastingDataInit();
        TestSpontaneousCastingSpellsPerDay();
        TestSpontaneousCastingSpellsKnown();
        TestSpontaneousCastingCanCastAndSpend();
        TestSpontaneousCastingBonusSlotsFromCHA();
        TestSpontaneousCastingLearnForgetSwap();

        // Fighter Tests
        TestFighterBonusFeatCount();
        TestFighterBonusFeatLevels();

        // Barbarian Tests
        TestMaxRagesPerDay();
        TestRageTierScaling();
        TestActivateRageScaling();
        TestDeactivateRageScaling();
        TestTirelessRage();
        TestBarbarianDamageReduction();
        TestImprovedUncannyDodge();
        TestIndomitableWill();
        TestRageWillBonusScaling();

        Debug.Log($"====== Phase 1 Class Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name, string className, int level,
        int str = 14, int dex = 12, int con = 14, int wis = 12, int intel = 10, int cha = 10)
    {
        ICharacterClass classDef = ClassRegistry.GetClass(className);
        int bab = classDef != null ? (level * classDef.BABAtLevel3 / 3) : 0;
        var stats = new CharacterStats(
            name: name, level: level, characterClass: className,
            str: str, dex: dex, con: con, wis: wis, intelligence: intel, cha: cha,
            bab: bab, armorBonus: classDef != null ? classDef.DefaultArmorBonus : 0,
            shieldBonus: classDef != null ? classDef.DefaultShieldBonus : 0,
            damageDice: classDef != null ? classDef.DefaultDamageDice : 8,
            damageCount: 1, bonusDamage: 0, baseSpeed: 6, atkRange: 1,
            baseHitDieHP: classDef != null ? classDef.HitDie : 8, raceName: "Human");
        return stats;
    }

    // ==================== SORCERER TESTS ====================

    private static void TestSorcererClassRegistered()
    {
        var sorcClass = ClassRegistry.GetClass("Sorcerer");
        Assert(sorcClass != null, "Sorcerer class registered in ClassRegistry");
        Assert(sorcClass != null && sorcClass.ClassName == "Sorcerer", "Sorcerer class name is 'Sorcerer'");
    }

    private static void TestSorcererClassProperties()
    {
        var sorcClass = ClassRegistry.GetClass("Sorcerer");
        if (sorcClass == null) { Assert(false, "SorcererClass lookup"); return; }

        Assert(sorcClass.HitDie == 4, "Sorcerer d4 hit die");
        Assert(sorcClass.BABAtLevel3 == 1, "Sorcerer poor BAB (1/2)");
        Assert(sorcClass.GoodWill == true, "Sorcerer good Will save");
        Assert(sorcClass.GoodFortitude == false, "Sorcerer poor Fort save");
        Assert(sorcClass.GoodReflex == false, "Sorcerer poor Ref save");
        Assert(sorcClass.SkillPointsPerLevel == 2, "Sorcerer 2 skill points/level");
        Assert(sorcClass.IsSpellcaster == true, "Sorcerer is spellcaster");
    }

    private static void TestSorcererQuickStart()
    {
        var data = SorcererClass.GetQuickStartCharacter();
        Assert(data != null, "Sorcerer QuickStart not null");
        Assert(data.ClassName == "Sorcerer", "QuickStart class is Sorcerer");
        Assert(data.CHA == 17, "QuickStart CHA is 17 (primary stat)");
        Assert(data.SelectedSpellIds != null && data.SelectedSpellIds.Count == 6,
            "QuickStart has 6 known spells (4 cantrips + 2 first-level)",
            $"Actual: {(data.SelectedSpellIds != null ? data.SelectedSpellIds.Count : 0)}");
        Assert(data.PreparedSpellSlotIds != null && data.PreparedSpellSlotIds.Count == 0,
            "QuickStart has no prepared slots (spontaneous caster)");
    }

    private static void TestSpontaneousCastingDataInit()
    {
        var sc = new SpontaneousCastingData();
        Assert(!sc.IsInitialized, "SpontaneousCastingData not initialized before Init()");

        sc.Initialize("Sorcerer", 1, 3); // Level 1, CHA mod +3
        Assert(sc.IsInitialized, "SpontaneousCastingData initialized after Init()");
        Assert(sc.ClassName == "Sorcerer", "ClassName is Sorcerer");
        Assert(sc.ClassLevel == 1, "ClassLevel is 1");
    }

    private static void TestSpontaneousCastingSpellsPerDay()
    {
        // PHB p.52: Sorcerer spells per day (base, before bonus)
        // Level 1: 5/3
        var sc1 = new SpontaneousCastingData();
        sc1.Initialize("Sorcerer", 1, 0); // No CHA bonus
        Assert(sc1.GetSlotsMax(0) == 5, "L1 Sorcerer: 5 cantrip slots", $"Actual: {sc1.GetSlotsMax(0)}");
        Assert(sc1.GetSlotsMax(1) == 3, "L1 Sorcerer: 3 first-level slots", $"Actual: {sc1.GetSlotsMax(1)}");
        Assert(sc1.GetSlotsMax(2) == 0, "L1 Sorcerer: 0 second-level slots", $"Actual: {sc1.GetSlotsMax(2)}");

        // Level 4: 6/6/3
        var sc4 = new SpontaneousCastingData();
        sc4.Initialize("Sorcerer", 4, 0);
        Assert(sc4.GetSlotsMax(0) == 6, "L4 Sorcerer: 6 cantrip slots", $"Actual: {sc4.GetSlotsMax(0)}");
        Assert(sc4.GetSlotsMax(1) == 6, "L4 Sorcerer: 6 first-level slots", $"Actual: {sc4.GetSlotsMax(1)}");
        Assert(sc4.GetSlotsMax(2) == 3, "L4 Sorcerer: 3 second-level slots", $"Actual: {sc4.GetSlotsMax(2)}");

        // Level 20: 6/6/6/6/6/6/6/6/6/6
        var sc20 = new SpontaneousCastingData();
        sc20.Initialize("Sorcerer", 20, 0);
        for (int i = 0; i <= 9; i++)
        {
            Assert(sc20.GetSlotsMax(i) == 6, $"L20 Sorcerer: 6 slots at level {i}", $"Actual: {sc20.GetSlotsMax(i)}");
        }
    }

    private static void TestSpontaneousCastingSpellsKnown()
    {
        // PHB p.52: Sorcerer spells known
        // Level 1: 4 cantrips, 2 first-level
        var sc1 = new SpontaneousCastingData();
        sc1.Initialize("Sorcerer", 1, 0);
        Assert(sc1.MaxSpellsKnownByLevel[0] == 4, "L1 known: 4 cantrips", $"Actual: {sc1.MaxSpellsKnownByLevel[0]}");
        Assert(sc1.MaxSpellsKnownByLevel[1] == 2, "L1 known: 2 first-level", $"Actual: {sc1.MaxSpellsKnownByLevel[1]}");

        // Level 20: 9 cantrips, 3 ninth-level
        var sc20 = new SpontaneousCastingData();
        sc20.Initialize("Sorcerer", 20, 0);
        Assert(sc20.MaxSpellsKnownByLevel[0] == 9, "L20 known: 9 cantrips", $"Actual: {sc20.MaxSpellsKnownByLevel[0]}");
        Assert(sc20.MaxSpellsKnownByLevel[9] == 3, "L20 known: 3 ninth-level", $"Actual: {sc20.MaxSpellsKnownByLevel[9]}");
    }

    private static void TestSpontaneousCastingCanCastAndSpend()
    {
        var sc = new SpontaneousCastingData();
        sc.Initialize("Sorcerer", 1, 3); // CHA +3
        sc.LearnSpell("magic_missile", 1);

        Assert(sc.CanCast("magic_missile", 1), "Can cast known spell with slots");

        // Spend all first-level slots
        int maxSlots = sc.GetSlotsMax(1);
        for (int i = 0; i < maxSlots; i++)
        {
            Assert(sc.SpendSlot(1), $"Spend slot {i + 1}/{maxSlots}");
        }
        Assert(!sc.CanCast("magic_missile", 1), "Cannot cast when all slots spent",
            $"Remaining: {sc.GetSlotsRemaining(1)}");

        // Refresh
        sc.RefreshAllSlots();
        Assert(sc.CanCast("magic_missile", 1), "Can cast after refresh");
    }

    private static void TestSpontaneousCastingBonusSlotsFromCHA()
    {
        // CHA 17 = +3 mod: bonus slots at levels 1, 2, 3
        var sc = new SpontaneousCastingData();
        sc.Initialize("Sorcerer", 4, 3); // Level 4, CHA mod +3

        // Base level 1: 6, +1 bonus from CHA = 7
        Assert(sc.GetSlotsMax(1) == 7, "L4 CHA+3: 7 first-level slots (6 base + 1 bonus)",
            $"Actual: {sc.GetSlotsMax(1)}");

        // Base level 2: 3, +1 bonus from CHA = 4
        Assert(sc.GetSlotsMax(2) == 4, "L4 CHA+3: 4 second-level slots (3 base + 1 bonus)",
            $"Actual: {sc.GetSlotsMax(2)}");
    }

    private static void TestSpontaneousCastingLearnForgetSwap()
    {
        var sc = new SpontaneousCastingData();
        sc.Initialize("Sorcerer", 1, 3);

        Assert(sc.LearnSpell("magic_missile", 1), "Learn Magic Missile");
        Assert(sc.LearnSpell("mage_armor", 1), "Learn Mage Armor");
        Assert(!sc.LearnSpell("magic_missile", 1), "Cannot learn duplicate");

        var known = sc.GetAllKnownSpellIds();
        Assert(known.Count == 2, "2 spells known", $"Actual: {known.Count}");

        Assert(sc.ForgetSpell("magic_missile", 1), "Forget Magic Missile");
        known = sc.GetAllKnownSpellIds();
        Assert(known.Count == 1, "1 spell known after forget", $"Actual: {known.Count}");

        Assert(sc.SwapSpell("mage_armor", 1, "shield", 1), "Swap Mage Armor for Shield");
        known = sc.GetAllKnownSpellIds();
        Assert(known.Contains("shield"), "Shield in known after swap");
        Assert(!known.Contains("mage_armor"), "Mage Armor not in known after swap");
    }

    // ==================== FIGHTER TESTS ====================

    private static void TestFighterBonusFeatCount()
    {
        // PHB p.37: Bonus feats at 1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 = 11 total
        var stats1 = BuildStats("Fighter1", "Fighter", 1);
        Assert(stats1.FighterBonusFeatCount == 1, "L1 Fighter: 1 bonus feat", $"Actual: {stats1.FighterBonusFeatCount}");

        var stats2 = BuildStats("Fighter2", "Fighter", 2);
        Assert(stats2.FighterBonusFeatCount == 2, "L2 Fighter: 2 bonus feats", $"Actual: {stats2.FighterBonusFeatCount}");

        var stats3 = BuildStats("Fighter3", "Fighter", 3);
        Assert(stats3.FighterBonusFeatCount == 2, "L3 Fighter: 2 bonus feats (no bonus at L3)", $"Actual: {stats3.FighterBonusFeatCount}");

        var stats4 = BuildStats("Fighter4", "Fighter", 4);
        Assert(stats4.FighterBonusFeatCount == 3, "L4 Fighter: 3 bonus feats", $"Actual: {stats4.FighterBonusFeatCount}");

        var stats20 = BuildStats("Fighter20", "Fighter", 20);
        Assert(stats20.FighterBonusFeatCount == 11, "L20 Fighter: 11 bonus feats", $"Actual: {stats20.FighterBonusFeatCount}");

        // Non-fighter gets 0
        var wizard = BuildStats("Wizard1", "Wizard", 5);
        Assert(wizard.FighterBonusFeatCount == 0, "L5 Wizard: 0 fighter bonus feats");
    }

    private static void TestFighterBonusFeatLevels()
    {
        // Verify every bonus feat level
        int[] bonusLevels = { 1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 };
        foreach (int lvl in bonusLevels)
        {
            Assert(CharacterStats.IsFighterBonusFeatLevel(lvl),
                $"Fighter L{lvl} is bonus feat level");
        }

        // Verify non-bonus levels
        int[] nonBonusLevels = { 3, 5, 7, 9, 11, 13, 15, 17, 19 };
        foreach (int lvl in nonBonusLevels)
        {
            Assert(!CharacterStats.IsFighterBonusFeatLevel(lvl),
                $"Fighter L{lvl} is NOT bonus feat level");
        }

        Assert(!CharacterStats.IsFighterBonusFeatLevel(0), "Fighter L0 is not bonus feat level");
        Assert(!CharacterStats.IsFighterBonusFeatLevel(-1), "Fighter L-1 is not bonus feat level");
    }

    // ==================== BARBARIAN TESTS ====================

    private static void TestMaxRagesPerDay()
    {
        // PHB p.25, Table 3-3: 1/day at L1, +1 at L4, L8, L12, L16, L20
        // Formula: 1 + floor(level / 4)
        var b1 = BuildStats("Barb1", "Barbarian", 1, str: 16, con: 16);
        var b3 = BuildStats("Barb3", "Barbarian", 3, str: 16, con: 16);
        var b4 = BuildStats("Barb4", "Barbarian", 4, str: 16, con: 16);
        var b8 = BuildStats("Barb8", "Barbarian", 8, str: 16, con: 16);
        var b12 = BuildStats("Barb12", "Barbarian", 12, str: 16, con: 16);
        var b16 = BuildStats("Barb16", "Barbarian", 16, str: 16, con: 16);
        var b20 = BuildStats("Barb20", "Barbarian", 20, str: 16, con: 16);

        Assert(b1.MaxRagesPerDay == 1, "L1: 1 rage/day", $"Actual: {b1.MaxRagesPerDay}");
        Assert(b3.MaxRagesPerDay == 1, "L3: 1 rage/day", $"Actual: {b3.MaxRagesPerDay}");
        Assert(b4.MaxRagesPerDay == 2, "L4: 2 rages/day", $"Actual: {b4.MaxRagesPerDay}");
        Assert(b8.MaxRagesPerDay == 3, "L8: 3 rages/day", $"Actual: {b8.MaxRagesPerDay}");
        Assert(b12.MaxRagesPerDay == 4, "L12: 4 rages/day", $"Actual: {b12.MaxRagesPerDay}");
        Assert(b16.MaxRagesPerDay == 5, "L16: 5 rages/day", $"Actual: {b16.MaxRagesPerDay}");
        Assert(b20.MaxRagesPerDay == 6, "L20: 6 rages/day", $"Actual: {b20.MaxRagesPerDay}");

        // Non-barbarian
        var wiz = BuildStats("WizNoRage", "Wizard", 5);
        Assert(wiz.MaxRagesPerDay == 0, "Wizard: 0 rages/day");
    }

    private static void TestRageTierScaling()
    {
        var b1 = BuildStats("Barb1", "Barbarian", 1, str: 16, con: 16);
        Assert(b1.RageTier == 0, "L1 Barbarian: Normal Rage (tier 0)");
        Assert(b1.RageAbilityBonus == 4, "L1: +4 STR/CON from rage");

        var b10 = BuildStats("Barb10", "Barbarian", 10, str: 16, con: 16);
        Assert(b10.RageTier == 0, "L10 Barbarian: Normal Rage (tier 0)");
        Assert(b10.RageAbilityBonus == 4, "L10: +4 STR/CON from rage");

        var b11 = BuildStats("Barb11", "Barbarian", 11, str: 16, con: 16);
        Assert(b11.RageTier == 1, "L11 Barbarian: Greater Rage (tier 1)");
        Assert(b11.RageAbilityBonus == 6, "L11: +6 STR/CON from rage");

        var b19 = BuildStats("Barb19", "Barbarian", 19, str: 16, con: 16);
        Assert(b19.RageTier == 1, "L19 Barbarian: Greater Rage (tier 1)");

        var b20 = BuildStats("Barb20", "Barbarian", 20, str: 16, con: 16);
        Assert(b20.RageTier == 2, "L20 Barbarian: Mighty Rage (tier 2)");
        Assert(b20.RageAbilityBonus == 8, "L20: +8 STR/CON from rage");
    }

    private static void TestActivateRageScaling()
    {
        // Test Greater Rage at L11: +6 STR/CON
        var b11 = BuildStats("Barb11", "Barbarian", 11, str: 16, con: 16);
        int origStr = b11.STR;
        int origCon = b11.CON;
        int origMaxHP = b11.MaxHP;

        bool activated = b11.ActivateRage();
        Assert(activated, "L11 Barbarian can activate rage");
        Assert(b11.IsRaging, "L11 Barbarian is raging");
        Assert(b11.STR == origStr + 6, "Greater Rage: STR +6", $"Expected {origStr + 6}, got {b11.STR}");
        Assert(b11.CON == origCon + 6, "Greater Rage: CON +6", $"Expected {origCon + 6}, got {b11.CON}");

        // HP gain = level * (bonus/2) = 11 * 3 = 33
        Assert(b11.MaxHP == origMaxHP + 33, "Greater Rage: +33 HP (11 * 3)",
            $"Expected {origMaxHP + 33}, got {b11.MaxHP}");
    }

    private static void TestDeactivateRageScaling()
    {
        var b11 = BuildStats("Barb11", "Barbarian", 11, str: 16, con: 16);
        int origStr = b11.STR;
        int origCon = b11.CON;
        int origMaxHP = b11.MaxHP;

        b11.ActivateRage();
        b11.DeactivateRage();

        Assert(!b11.IsRaging, "No longer raging after deactivation");
        Assert(b11.STR == origStr, "STR restored after rage", $"Expected {origStr}, got {b11.STR}");
        Assert(b11.CON == origCon, "CON restored after rage", $"Expected {origCon}, got {b11.CON}");
        Assert(b11.MaxHP == origMaxHP, "MaxHP restored after rage", $"Expected {origMaxHP}, got {b11.MaxHP}");
        Assert(b11.IsFatigued, "Fatigued after rage (L11, no Tireless Rage)");
    }

    private static void TestTirelessRage()
    {
        // L17+ should NOT be fatigued after rage
        var b17 = BuildStats("Barb17", "Barbarian", 17, str: 16, con: 16);
        Assert(b17.HasTirelessRage, "L17 has Tireless Rage");

        b17.ActivateRage();
        b17.DeactivateRage();
        Assert(!b17.IsFatigued, "L17 NOT fatigued after rage (Tireless Rage)");

        // L16 should be fatigued
        var b16 = BuildStats("Barb16", "Barbarian", 16, str: 16, con: 16);
        Assert(!b16.HasTirelessRage, "L16 does NOT have Tireless Rage");
        b16.ActivateRage();
        b16.DeactivateRage();
        Assert(b16.IsFatigued, "L16 IS fatigued after rage");
    }

    private static void TestBarbarianDamageReduction()
    {
        // PHB p.26: DR 1/- at L7, +1 per 3 levels
        var b6 = BuildStats("Barb6", "Barbarian", 6, str: 16, con: 16);
        Assert(b6.BarbarianDamageReduction == 0, "L6: no DR yet", $"Actual: {b6.BarbarianDamageReduction}");

        var b7 = BuildStats("Barb7", "Barbarian", 7, str: 16, con: 16);
        Assert(b7.BarbarianDamageReduction == 1, "L7: DR 1/-", $"Actual: {b7.BarbarianDamageReduction}");

        var b10 = BuildStats("Barb10", "Barbarian", 10, str: 16, con: 16);
        Assert(b10.BarbarianDamageReduction == 2, "L10: DR 2/-", $"Actual: {b10.BarbarianDamageReduction}");

        var b13 = BuildStats("Barb13", "Barbarian", 13, str: 16, con: 16);
        Assert(b13.BarbarianDamageReduction == 3, "L13: DR 3/-", $"Actual: {b13.BarbarianDamageReduction}");

        var b16 = BuildStats("Barb16", "Barbarian", 16, str: 16, con: 16);
        Assert(b16.BarbarianDamageReduction == 4, "L16: DR 4/-", $"Actual: {b16.BarbarianDamageReduction}");

        var b19 = BuildStats("Barb19", "Barbarian", 19, str: 16, con: 16);
        Assert(b19.BarbarianDamageReduction == 5, "L19: DR 5/-", $"Actual: {b19.BarbarianDamageReduction}");
    }

    private static void TestImprovedUncannyDodge()
    {
        var b4 = BuildStats("Barb4", "Barbarian", 4, str: 16, con: 16);
        Assert(!b4.HasImprovedUncannyDodge, "L4 Barbarian: no Improved Uncanny Dodge");

        var b5 = BuildStats("Barb5", "Barbarian", 5, str: 16, con: 16);
        Assert(b5.HasImprovedUncannyDodge, "L5 Barbarian: has Improved Uncanny Dodge");

        // Non-barbarian
        var wiz = BuildStats("Wiz5", "Wizard", 5);
        Assert(!wiz.HasImprovedUncannyDodge, "Wizard: no Improved Uncanny Dodge");
    }

    private static void TestIndomitableWill()
    {
        var b13 = BuildStats("Barb13", "Barbarian", 13, str: 16, con: 16);
        b13.ActivateRage();
        Assert(b13.IndomitableWillBonus == 0, "L13 raging: no Indomitable Will");
        b13.DeactivateRage();

        var b14 = BuildStats("Barb14", "Barbarian", 14, str: 16, con: 16);
        Assert(b14.IndomitableWillBonus == 0, "L14 NOT raging: 0 Indomitable Will");
        b14.ActivateRage();
        Assert(b14.IndomitableWillBonus == 4, "L14 raging: +4 Indomitable Will", $"Actual: {b14.IndomitableWillBonus}");
    }

    private static void TestRageWillBonusScaling()
    {
        var b1 = BuildStats("Barb1", "Barbarian", 1, str: 16, con: 16);
        b1.ActivateRage();
        Assert(b1.RageWillBonus == 2, "L1 rage Will bonus: +2 (Normal)", $"Actual: {b1.RageWillBonus}");
        b1.DeactivateRage();

        var b11 = BuildStats("Barb11", "Barbarian", 11, str: 16, con: 16);
        b11.ActivateRage();
        Assert(b11.RageWillBonus == 3, "L11 rage Will bonus: +3 (Greater)", $"Actual: {b11.RageWillBonus}");
        b11.DeactivateRage();

        var b20 = BuildStats("Barb20", "Barbarian", 20, str: 16, con: 16);
        b20.ActivateRage();
        Assert(b20.RageWillBonus == 4, "L20 rage Will bonus: +4 (Mighty)", $"Actual: {b20.RageWillBonus}");
        b20.DeactivateRage();

        // Not raging = 0
        Assert(b20.RageWillBonus == 0, "Not raging: Will bonus is 0");
    }
}
}
