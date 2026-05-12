using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Tests for the 6 attribute enhancement spells (D&D 3.5e PHB):
/// Bear's Endurance, Bull's Strength, Cat's Grace,
/// Eagle's Splendor, Fox's Cunning, Owl's Wisdom.
///
/// Validates spell definitions, +4 bonus application, duration (1 min/level),
/// non-stacking (same type), coexistence (different stats), Bear's Endurance
/// HP grant/removal, death on HP loss, and derived stat updates.
///
/// Run with AttributeEnhancementRulesTests.RunAll().
/// </summary>
public static class AttributeEnhancementRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== ATTRIBUTE ENHANCEMENT RULES TESTS ======");

        SpellDatabase.Init();
        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();

        // Spell definition tests
        TestBearsEnduranceDefinition();
        TestBullsStrengthDefinition();
        TestCatsGraceDefinition();
        TestEaglesSplendorDefinition();
        TestFoxsCunningDefinition();
        TestOwlsWisdomDefinition();

        // Effect data tests
        TestEffectDataFactory();
        TestBearsEnduranceHPCalculation();
        TestIsAttributeEnhancementSpell();
        TestGetAbilityForSpell();

        // Bonus application tests
        TestBullsStrengthAppliesSTRBonus();
        TestCatsGraceAppliesDEXBonus();
        TestBearsEnduranceAppliesCONBonus();
        TestFoxsCunningAppliesINTBonus();
        TestOwlsWisdomAppliesWISBonus();
        TestEaglesSplendorAppliesCHABonus();

        // Bear's Endurance HP tests
        TestBearsEnduranceGrantsHP();
        TestBearsEnduranceHPRemovalCanKill();
        TestBearsEnduranceHPBasedOnHD();

        // Non-stacking tests
        TestSameSpellDoesNotStack();
        TestSameSpellRefreshesDuration();
        TestDifferentStatsCoexist();

        // Duration tests
        TestDurationScalesWithLevel();

        // Derived stat update tests
        TestBullsStrengthAffectsAttackBonus();
        TestCatsGraceAffectsAC();
        TestBearsEnduranceAffectsFortSave();
        TestOwlsWisdomAffectsWillSave();
        TestCatsGraceAffectsReflexSave();

        Debug.Log($"====== Attribute Enhancement Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats MakeWizardStats(string name, int level = 5)
    {
        return new CharacterStats(name, level, "Wizard",
            10, 14, 12, 10, 18, 10,   // STR=10, DEX=14, CON=12, WIS=10, INT=18, CHA=10
            2, 0, 0,                    // BAB, armorBonus, shieldBonus
            4, 1, 0,                    // damageDice, damageCount, bonusDamage
            6, 1, 20,                   // baseSpeed, atkRange, baseHitDieHP
            "Human");
    }

    private static CharacterStats MakeFighterStats(string name, int level = 5)
    {
        return new CharacterStats(name, level, "Fighter",
            16, 12, 14, 10, 12, 8,    // STR=16, DEX=12, CON=14, WIS=10, INT=12, CHA=8
            5, 5, 2,                    // BAB, armorBonus, shieldBonus
            8, 1, 0,                    // damageDice, damageCount, bonusDamage
            6, 1, 40,                   // baseSpeed, atkRange, baseHitDieHP
            "Human");
    }

    // ===== SPELL DEFINITION TESTS =====

    private static void TestBearsEnduranceDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.BEARS_ENDURANCE);
        Assert(spell != null, "Bear's Endurance spell exists");
        if (spell == null) return;

        Assert(spell.SpellLevel == 2, "Bear's Endurance is level 2", $"got {spell.SpellLevel}");
        Assert(spell.School == "Transmutation", "School is Transmutation", $"got {spell.School}");
        Assert(spell.BuffStatName == "CON", "Buffs CON", $"got {spell.BuffStatName}");
        Assert(spell.BuffStatBonus == 4, "Bonus is +4", $"got {spell.BuffStatBonus}");
        Assert(spell.DurationType == DurationType.Minutes, "Duration type is Minutes", $"got {spell.DurationType}");
        Assert(spell.DurationValue == 1, "Duration value is 1 (min/level)", $"got {spell.DurationValue}");
        Assert(spell.DurationScalesWithLevel, "Duration scales with level");
        Assert(spell.BuffBonusType == BonusType.Enhancement, "Bonus type is Enhancement", $"got {spell.BuffBonusType}");
        Assert(!spell.IsPlaceholder, "Not a placeholder");
    }

    private static void TestBullsStrengthDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.BULLS_STRENGTH);
        Assert(spell != null, "Bull's Strength spell exists");
        if (spell == null) return;

        Assert(spell.SpellLevel == 2, "Bull's Strength is level 2", $"got {spell.SpellLevel}");
        Assert(spell.School == "Transmutation", "School is Transmutation", $"got {spell.School}");
        Assert(spell.BuffStatName == "STR", "Buffs STR", $"got {spell.BuffStatName}");
        Assert(spell.BuffStatBonus == 4, "Bonus is +4", $"got {spell.BuffStatBonus}");
        Assert(spell.BuffBonusType == BonusType.Enhancement, "Bonus type is Enhancement", $"got {spell.BuffBonusType}");
    }

    private static void TestCatsGraceDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.CATS_GRACE);
        Assert(spell != null, "Cat's Grace spell exists");
        if (spell == null) return;

        Assert(spell.SpellLevel == 2, "Cat's Grace is level 2", $"got {spell.SpellLevel}");
        Assert(spell.School == "Transmutation", "School is Transmutation", $"got {spell.School}");
        Assert(spell.BuffStatName == "DEX", "Buffs DEX", $"got {spell.BuffStatName}");
        Assert(spell.BuffStatBonus == 4, "Bonus is +4", $"got {spell.BuffStatBonus}");
        Assert(spell.BuffBonusType == BonusType.Enhancement, "Bonus type is Enhancement", $"got {spell.BuffBonusType}");
    }

    private static void TestEaglesSplendorDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.EAGLES_SPLENDOR);
        Assert(spell != null, "Eagle's Splendor spell exists");
        if (spell == null) return;

        Assert(spell.SpellLevel == 2, "Eagle's Splendor is level 2", $"got {spell.SpellLevel}");
        Assert(spell.School == "Transmutation", "School is Transmutation", $"got {spell.School}");
        Assert(spell.BuffStatName == "CHA", "Buffs CHA", $"got {spell.BuffStatName}");
        Assert(spell.BuffStatBonus == 4, "Bonus is +4", $"got {spell.BuffStatBonus}");
        Assert(spell.BuffBonusType == BonusType.Enhancement, "Bonus type is Enhancement", $"got {spell.BuffBonusType}");
    }

    private static void TestFoxsCunningDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.FOXS_CUNNING);
        Assert(spell != null, "Fox's Cunning spell exists");
        if (spell == null) return;

        Assert(spell.SpellLevel == 2, "Fox's Cunning is level 2", $"got {spell.SpellLevel}");
        Assert(spell.School == "Transmutation", "School is Transmutation", $"got {spell.School}");
        Assert(spell.BuffStatName == "INT", "Buffs INT", $"got {spell.BuffStatName}");
        Assert(spell.BuffStatBonus == 4, "Bonus is +4", $"got {spell.BuffStatBonus}");
        Assert(spell.BuffBonusType == BonusType.Enhancement, "Bonus type is Enhancement", $"got {spell.BuffBonusType}");
    }

    private static void TestOwlsWisdomDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.OWLS_WISDOM);
        Assert(spell != null, "Owl's Wisdom spell exists");
        if (spell == null) return;

        Assert(spell.SpellLevel == 2, "Owl's Wisdom is level 2", $"got {spell.SpellLevel}");
        Assert(spell.School == "Transmutation", "School is Transmutation", $"got {spell.School}");
        Assert(spell.BuffStatName == "WIS", "Buffs WIS", $"got {spell.BuffStatName}");
        Assert(spell.BuffStatBonus == 4, "Bonus is +4", $"got {spell.BuffStatBonus}");
        Assert(spell.BuffBonusType == BonusType.Enhancement, "Bonus type is Enhancement", $"got {spell.BuffBonusType}");
    }

    // ===== EFFECT DATA TESTS =====

    private static void TestEffectDataFactory()
    {
        var data = AttributeEnhancementEffectData.Create(SpellNames.BULLS_STRENGTH, 5, 5, null);
        Assert(data != null, "Factory creates non-null data");
        Assert(data.EnhancedAbility == AbilityType.STR, "Bull's Strength targets STR", $"got {data.EnhancedAbility}");
        Assert(data.BonusAmount == 4, "Bonus is +4", $"got {data.BonusAmount}");
        Assert(data.CasterLevel == 5, "Caster level is 5", $"got {data.CasterLevel}");
        Assert(data.DurationRemainingRounds == 50, "Duration is 50 rounds (5 CL × 10)", $"got {data.DurationRemainingRounds}");
        Assert(data.IsActive, "Effect is active");
        Assert(data.GrantedBonusHP == 0, "Non-CON spell grants 0 bonus HP", $"got {data.GrantedBonusHP}");
        Assert(!data.IsBearsEndurance, "Bull's Strength is NOT Bear's Endurance");
    }

    private static void TestBearsEnduranceHPCalculation()
    {
        // 5 HD creature should get 10 HP
        int hp5 = AttributeEnhancementEffectData.CalculateBearsEnduranceHP(5);
        Assert(hp5 == 10, "5 HD = 10 HP", $"got {hp5}");

        // 10 HD creature should get 20 HP
        int hp10 = AttributeEnhancementEffectData.CalculateBearsEnduranceHP(10);
        Assert(hp10 == 20, "10 HD = 20 HP", $"got {hp10}");

        // 1 HD creature should get 2 HP
        int hp1 = AttributeEnhancementEffectData.CalculateBearsEnduranceHP(1);
        Assert(hp1 == 2, "1 HD = 2 HP", $"got {hp1}");

        // Bear's Endurance effect should contain HP
        var data = AttributeEnhancementEffectData.Create(SpellNames.BEARS_ENDURANCE, 5, 5, null);
        Assert(data.IsBearsEndurance, "Bear's Endurance correctly identified");
        Assert(data.GrantedBonusHP == 10, "5 HD Bear's Endurance grants 10 HP", $"got {data.GrantedBonusHP}");
        Assert(data.EnhancedAbility == AbilityType.CON, "Targets CON", $"got {data.EnhancedAbility}");
    }

    private static void TestIsAttributeEnhancementSpell()
    {
        Assert(AttributeEnhancementEffectData.IsAttributeEnhancementSpell(SpellNames.BEARS_ENDURANCE), "Bear's Endurance is enhancement");
        Assert(AttributeEnhancementEffectData.IsAttributeEnhancementSpell(SpellNames.BULLS_STRENGTH), "Bull's Strength is enhancement");
        Assert(AttributeEnhancementEffectData.IsAttributeEnhancementSpell(SpellNames.CATS_GRACE), "Cat's Grace is enhancement");
        Assert(AttributeEnhancementEffectData.IsAttributeEnhancementSpell(SpellNames.EAGLES_SPLENDOR), "Eagle's Splendor is enhancement");
        Assert(AttributeEnhancementEffectData.IsAttributeEnhancementSpell(SpellNames.FOXS_CUNNING), "Fox's Cunning is enhancement");
        Assert(AttributeEnhancementEffectData.IsAttributeEnhancementSpell(SpellNames.OWLS_WISDOM), "Owl's Wisdom is enhancement");
        Assert(!AttributeEnhancementEffectData.IsAttributeEnhancementSpell(SpellNames.MAGIC_MISSILE), "Magic Missile is NOT enhancement");
        Assert(!AttributeEnhancementEffectData.IsAttributeEnhancementSpell(SpellNames.FALSE_LIFE), "False Life is NOT enhancement");
    }

    private static void TestGetAbilityForSpell()
    {
        Assert(AttributeEnhancementEffectData.GetAbilityForSpell(SpellNames.BEARS_ENDURANCE) == AbilityType.CON, "Bear's → CON");
        Assert(AttributeEnhancementEffectData.GetAbilityForSpell(SpellNames.BULLS_STRENGTH) == AbilityType.STR, "Bull's → STR");
        Assert(AttributeEnhancementEffectData.GetAbilityForSpell(SpellNames.CATS_GRACE) == AbilityType.DEX, "Cat's → DEX");
        Assert(AttributeEnhancementEffectData.GetAbilityForSpell(SpellNames.EAGLES_SPLENDOR) == AbilityType.CHA, "Eagle's → CHA");
        Assert(AttributeEnhancementEffectData.GetAbilityForSpell(SpellNames.FOXS_CUNNING) == AbilityType.INT, "Fox's → INT");
        Assert(AttributeEnhancementEffectData.GetAbilityForSpell(SpellNames.OWLS_WISDOM) == AbilityType.WIS, "Owl's → WIS");
    }

    // ===== BONUS APPLICATION TESTS =====

    private static void TestBullsStrengthAppliesSTRBonus()
    {
        CharacterStats stats = MakeFighterStats("STR Test Fighter");
        int originalSTR = stats.STR;

        var go = new GameObject("TestBullsStr");
        var cc = go.AddComponent<CharacterController>();
        cc.Stats = stats;

        var effect = AttributeEnhancementEffectData.Create(SpellNames.BULLS_STRENGTH, 5, 5, null);
        cc.ApplyAttributeEnhancement(effect);

        // Note: The stat bonus (+4 to STR) is applied by StatusEffectManager, not by our custom handler.
        // Our handler manages the AttributeEnhancementEffectData tracking and HP for Bear's Endurance.
        // For this test, we verify the tracking works.
        Assert(cc.HasActiveAttributeEnhancement(AbilityType.STR), "Has active STR enhancement");
        Assert(cc.GetActiveAttributeEnhancement(AbilityType.STR) == effect, "Returns correct effect data");

        Object.DestroyImmediate(go);
    }

    private static void TestCatsGraceAppliesDEXBonus()
    {
        CharacterStats stats = MakeFighterStats("DEX Test Fighter");
        var go = new GameObject("TestCatsGrace");
        var cc = go.AddComponent<CharacterController>();
        cc.Stats = stats;

        var effect = AttributeEnhancementEffectData.Create(SpellNames.CATS_GRACE, 5, 5, null);
        cc.ApplyAttributeEnhancement(effect);

        Assert(cc.HasActiveAttributeEnhancement(AbilityType.DEX), "Has active DEX enhancement");
        Object.DestroyImmediate(go);
    }

    private static void TestBearsEnduranceAppliesCONBonus()
    {
        CharacterStats stats = MakeFighterStats("CON Test Fighter");
        var go = new GameObject("TestBearsEnd");
        var cc = go.AddComponent<CharacterController>();
        cc.Stats = stats;

        var effect = AttributeEnhancementEffectData.Create(SpellNames.BEARS_ENDURANCE, 5, 5, null);
        cc.ApplyAttributeEnhancement(effect);

        Assert(cc.HasActiveAttributeEnhancement(AbilityType.CON), "Has active CON enhancement");
        Object.DestroyImmediate(go);
    }

    private static void TestFoxsCunningAppliesINTBonus()
    {
        CharacterStats stats = MakeWizardStats("INT Test Wizard");
        var go = new GameObject("TestFoxsCunning");
        var cc = go.AddComponent<CharacterController>();
        cc.Stats = stats;

        var effect = AttributeEnhancementEffectData.Create(SpellNames.FOXS_CUNNING, 5, 5, null);
        cc.ApplyAttributeEnhancement(effect);

        Assert(cc.HasActiveAttributeEnhancement(AbilityType.INT), "Has active INT enhancement");
        Object.DestroyImmediate(go);
    }

    private static void TestOwlsWisdomAppliesWISBonus()
    {
        CharacterStats stats = MakeWizardStats("WIS Test Wizard");
        var go = new GameObject("TestOwlsWisdom");
        var cc = go.AddComponent<CharacterController>();
        cc.Stats = stats;

        var effect = AttributeEnhancementEffectData.Create(SpellNames.OWLS_WISDOM, 5, 5, null);
        cc.ApplyAttributeEnhancement(effect);

        Assert(cc.HasActiveAttributeEnhancement(AbilityType.WIS), "Has active WIS enhancement");
        Object.DestroyImmediate(go);
    }

    private static void TestEaglesSplendorAppliesCHABonus()
    {
        CharacterStats stats = MakeWizardStats("CHA Test Wizard");
        var go = new GameObject("TestEaglesSplendor");
        var cc = go.AddComponent<CharacterController>();
        cc.Stats = stats;

        var effect = AttributeEnhancementEffectData.Create(SpellNames.EAGLES_SPLENDOR, 5, 5, null);
        cc.ApplyAttributeEnhancement(effect);

        Assert(cc.HasActiveAttributeEnhancement(AbilityType.CHA), "Has active CHA enhancement");
        Object.DestroyImmediate(go);
    }

    // ===== BEAR'S ENDURANCE HP TESTS =====

    private static void TestBearsEnduranceGrantsHP()
    {
        CharacterStats stats = MakeFighterStats("HP Test Fighter", 5);
        int originalHP = stats.CurrentHP;
        int originalMaxHP = stats.TotalMaxHP;

        var go = new GameObject("TestBearsEndHP");
        var cc = go.AddComponent<CharacterController>();
        cc.Stats = stats;

        // 5 HD Fighter → +10 HP from Bear's Endurance
        var effect = AttributeEnhancementEffectData.Create(SpellNames.BEARS_ENDURANCE, 5, 5, null);
        Assert(effect.GrantedBonusHP == 10, "5 HD grants 10 bonus HP", $"got {effect.GrantedBonusHP}");

        cc.ApplyAttributeEnhancement(effect);

        Assert(stats.CurrentHP == originalHP + 10, "Current HP increased by 10",
            $"expected {originalHP + 10}, got {stats.CurrentHP}");
        Assert(stats.BonusMaxHP >= 10, "BonusMaxHP includes Bear's Endurance HP",
            $"got BonusMaxHP={stats.BonusMaxHP}");

        Object.DestroyImmediate(go);
    }

    private static void TestBearsEnduranceHPRemovalCanKill()
    {
        CharacterStats stats = MakeFighterStats("Dying Fighter", 5);
        var go = new GameObject("TestBearsEndDeath");
        var cc = go.AddComponent<CharacterController>();
        cc.Stats = stats;

        // Apply Bear's Endurance (+10 HP for 5 HD)
        var effect = AttributeEnhancementEffectData.Create(SpellNames.BEARS_ENDURANCE, 5, 5, null);
        cc.ApplyAttributeEnhancement(effect);

        int hpAfterBuff = stats.CurrentHP;

        // Simulate taking damage so character is barely alive
        // Set HP to 5 (so removing 10 HP will drop to -5)
        stats.CurrentHP = 5;

        // Remove Bear's Endurance — should drop to -5 HP (dying/dead)
        bool causedDeath = cc.RemoveAttributeEnhancement(AbilityType.CON);

        Assert(causedDeath, "Removing Bear's Endurance caused death (HP dropped below 0)");
        Assert(stats.CurrentHP <= 0, "Current HP is 0 or below after removal",
            $"got {stats.CurrentHP}");

        Object.DestroyImmediate(go);
    }

    private static void TestBearsEnduranceHPBasedOnHD()
    {
        // Test with HD=3 creature
        var data3 = AttributeEnhancementEffectData.Create(SpellNames.BEARS_ENDURANCE, 5, 3, null);
        Assert(data3.GrantedBonusHP == 6, "3 HD = 6 HP", $"got {data3.GrantedBonusHP}");

        // Test with HD=10 creature
        var data10 = AttributeEnhancementEffectData.Create(SpellNames.BEARS_ENDURANCE, 5, 10, null);
        Assert(data10.GrantedBonusHP == 20, "10 HD = 20 HP", $"got {data10.GrantedBonusHP}");

        // Test with HD=1 creature
        var data1 = AttributeEnhancementEffectData.Create(SpellNames.BEARS_ENDURANCE, 5, 1, null);
        Assert(data1.GrantedBonusHP == 2, "1 HD = 2 HP", $"got {data1.GrantedBonusHP}");
    }

    // ===== NON-STACKING TESTS =====

    private static void TestSameSpellDoesNotStack()
    {
        CharacterStats stats = MakeFighterStats("Stack Test Fighter");
        var go = new GameObject("TestNonStack");
        var cc = go.AddComponent<CharacterController>();
        cc.Stats = stats;

        // Apply Bull's Strength twice — should not stack
        var effect1 = AttributeEnhancementEffectData.Create(SpellNames.BULLS_STRENGTH, 5, 5, null);
        var effect2 = AttributeEnhancementEffectData.Create(SpellNames.BULLS_STRENGTH, 5, 5, null);

        cc.ApplyAttributeEnhancement(effect1);
        cc.ApplyAttributeEnhancement(effect2);

        // Should still have only one active enhancement on STR
        Assert(cc.HasActiveAttributeEnhancement(AbilityType.STR), "Still has STR enhancement");
        var active = cc.GetActiveAttributeEnhancement(AbilityType.STR);
        Assert(active != null, "Active enhancement exists");
        // The bonus should be 4, not 8
        Assert(active.BonusAmount == 4, "Bonus is still +4 (not stacked)", $"got {active.BonusAmount}");

        Object.DestroyImmediate(go);
    }

    private static void TestSameSpellRefreshesDuration()
    {
        CharacterStats stats = MakeFighterStats("Duration Test Fighter");
        var go = new GameObject("TestDurationRefresh");
        var cc = go.AddComponent<CharacterController>();
        cc.Stats = stats;

        // Apply Bull's Strength at CL 3 (30 rounds)
        var effect1 = AttributeEnhancementEffectData.Create(SpellNames.BULLS_STRENGTH, 3, 5, null);
        cc.ApplyAttributeEnhancement(effect1);

        // Simulate some time passing
        effect1.DurationRemainingRounds = 10;

        // Apply again at CL 5 (50 rounds) — should refresh duration
        var effect2 = AttributeEnhancementEffectData.Create(SpellNames.BULLS_STRENGTH, 5, 5, null);
        cc.ApplyAttributeEnhancement(effect2);

        var active = cc.GetActiveAttributeEnhancement(AbilityType.STR);
        Assert(active != null, "Active enhancement exists after refresh");
        Assert(active.DurationRemainingRounds >= 50, "Duration refreshed to longer value",
            $"got {active.DurationRemainingRounds}");

        Object.DestroyImmediate(go);
    }

    private static void TestDifferentStatsCoexist()
    {
        CharacterStats stats = MakeFighterStats("Multi-buff Fighter");
        var go = new GameObject("TestCoexist");
        var cc = go.AddComponent<CharacterController>();
        cc.Stats = stats;

        // Apply Bull's Strength (STR) and Cat's Grace (DEX) — should coexist
        var strEffect = AttributeEnhancementEffectData.Create(SpellNames.BULLS_STRENGTH, 5, 5, null);
        var dexEffect = AttributeEnhancementEffectData.Create(SpellNames.CATS_GRACE, 5, 5, null);

        cc.ApplyAttributeEnhancement(strEffect);
        cc.ApplyAttributeEnhancement(dexEffect);

        Assert(cc.HasActiveAttributeEnhancement(AbilityType.STR), "STR enhancement active");
        Assert(cc.HasActiveAttributeEnhancement(AbilityType.DEX), "DEX enhancement active");

        var allActive = cc.GetAllActiveAttributeEnhancements();
        Assert(allActive.Count == 2, "Two active enhancements", $"got {allActive.Count}");

        // Apply all 6 — should all coexist
        cc.ApplyAttributeEnhancement(AttributeEnhancementEffectData.Create(SpellNames.BEARS_ENDURANCE, 5, 5, null));
        cc.ApplyAttributeEnhancement(AttributeEnhancementEffectData.Create(SpellNames.EAGLES_SPLENDOR, 5, 5, null));
        cc.ApplyAttributeEnhancement(AttributeEnhancementEffectData.Create(SpellNames.FOXS_CUNNING, 5, 5, null));
        cc.ApplyAttributeEnhancement(AttributeEnhancementEffectData.Create(SpellNames.OWLS_WISDOM, 5, 5, null));

        allActive = cc.GetAllActiveAttributeEnhancements();
        Assert(allActive.Count == 6, "All 6 enhancements active simultaneously", $"got {allActive.Count}");

        Object.DestroyImmediate(go);
    }

    // ===== DURATION TESTS =====

    private static void TestDurationScalesWithLevel()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.BULLS_STRENGTH);
        Assert(spell != null, "Bull's Strength spell exists for duration test");
        if (spell == null) return;

        // CL 1: 1 min = 10 rounds
        int dur1 = ActiveSpellEffect.CalculateDurationRounds(spell, 1);
        Assert(dur1 == 10, "CL 1: 10 rounds (1 minute)", $"got {dur1}");

        // CL 5: 5 min = 50 rounds
        int dur5 = ActiveSpellEffect.CalculateDurationRounds(spell, 5);
        Assert(dur5 == 50, "CL 5: 50 rounds (5 minutes)", $"got {dur5}");

        // CL 10: 10 min = 100 rounds
        int dur10 = ActiveSpellEffect.CalculateDurationRounds(spell, 10);
        Assert(dur10 == 100, "CL 10: 100 rounds (10 minutes)", $"got {dur10}");
    }

    // ===== DERIVED STAT UPDATE TESTS =====

    private static void TestBullsStrengthAffectsAttackBonus()
    {
        CharacterStats stats = MakeFighterStats("Atk Test Fighter");
        int originalAttackBonus = stats.AttackBonus;
        int originalSTRMod = stats.STRMod;

        // Simulate applying +4 STR (as StatusEffectManager would)
        stats.STR += 4;
        int newSTRMod = stats.STRMod;
        int newAttackBonus = stats.AttackBonus;

        Assert(newSTRMod == originalSTRMod + 2, "STR mod increased by 2 (+4 score = +2 mod)",
            $"expected {originalSTRMod + 2}, got {newSTRMod}");
        Assert(newAttackBonus == originalAttackBonus + 2, "Attack bonus increased by 2",
            $"expected {originalAttackBonus + 2}, got {newAttackBonus}");

        // Reverse
        stats.STR -= 4;
        Assert(stats.AttackBonus == originalAttackBonus, "Attack bonus restored after removal",
            $"expected {originalAttackBonus}, got {stats.AttackBonus}");
    }

    private static void TestCatsGraceAffectsAC()
    {
        CharacterStats stats = MakeFighterStats("AC Test Fighter");
        int originalAC = stats.ArmorClass;
        int originalDEXMod = stats.DEXMod;

        // Simulate applying +4 DEX
        stats.DEX += 4;
        int newDEXMod = stats.DEXMod;
        int newAC = stats.ArmorClass;

        Assert(newDEXMod == originalDEXMod + 2, "DEX mod increased by 2",
            $"expected {originalDEXMod + 2}, got {newDEXMod}");

        // AC should increase (subject to max DEX bonus from armor)
        Assert(newAC >= originalAC, "AC increased or stayed same (max dex limit)",
            $"expected >= {originalAC}, got {newAC}");

        stats.DEX -= 4;
    }

    private static void TestBearsEnduranceAffectsFortSave()
    {
        CharacterStats stats = MakeFighterStats("Fort Test Fighter");
        int originalFortSave = stats.FortitudeSave;
        int originalCONMod = stats.CONMod;

        // Simulate applying +4 CON
        stats.CON += 4;
        int newCONMod = stats.CONMod;
        int newFortSave = stats.FortitudeSave;

        Assert(newCONMod == originalCONMod + 2, "CON mod increased by 2",
            $"expected {originalCONMod + 2}, got {newCONMod}");
        Assert(newFortSave == originalFortSave + 2, "Fortitude save increased by 2",
            $"expected {originalFortSave + 2}, got {newFortSave}");

        stats.CON -= 4;
        Assert(stats.FortitudeSave == originalFortSave, "Fort save restored after removal",
            $"expected {originalFortSave}, got {stats.FortitudeSave}");
    }

    private static void TestOwlsWisdomAffectsWillSave()
    {
        CharacterStats stats = MakeWizardStats("Will Test Wizard");
        int originalWillSave = stats.WillSave;
        int originalWISMod = stats.WISMod;

        // Simulate applying +4 WIS
        stats.WIS += 4;
        int newWISMod = stats.WISMod;
        int newWillSave = stats.WillSave;

        Assert(newWISMod == originalWISMod + 2, "WIS mod increased by 2",
            $"expected {originalWISMod + 2}, got {newWISMod}");
        Assert(newWillSave == originalWillSave + 2, "Will save increased by 2",
            $"expected {originalWillSave + 2}, got {newWillSave}");

        stats.WIS -= 4;
    }

    private static void TestCatsGraceAffectsReflexSave()
    {
        CharacterStats stats = MakeFighterStats("Reflex Test Fighter");
        int originalReflexSave = stats.ReflexSave;
        int originalDEXMod = stats.DEXMod;

        // Simulate applying +4 DEX
        stats.DEX += 4;
        int newDEXMod = stats.DEXMod;
        int newReflexSave = stats.ReflexSave;

        Assert(newDEXMod == originalDEXMod + 2, "DEX mod increased by 2",
            $"expected {originalDEXMod + 2}, got {newDEXMod}");
        Assert(newReflexSave == originalReflexSave + 2, "Reflex save increased by 2",
            $"expected {originalReflexSave + 2}, got {newReflexSave}");

        stats.DEX -= 4;
    }
}
}
