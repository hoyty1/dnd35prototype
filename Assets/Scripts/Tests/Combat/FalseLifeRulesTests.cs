using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Tests for False Life spell (D&D 3.5e PHB p.229).
/// Validates temp HP calculation, damage absorption, non-stacking,
/// duration expiration, discharge, and healing interaction.
/// Run with FalseLifeRulesTests.RunAll().
/// </summary>
public static class FalseLifeRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== FALSE LIFE RULES TESTS ======");

        SpellDatabase.Init();
        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();

        TestSpellDefinition();
        TestTempHPCalculation();
        TestTempHPCalculationCLCap();
        TestTempHPAbsorbsDamageBeforeHP();
        TestTempHPPartialAbsorption();
        TestTempHPFullAbsorption();
        TestNonStackingHigherWins();
        TestNonStackingLowerBlocked();
        TestDurationExpiration();
        TestDischargeOnZeroTempHP();
        TestTempHPCannotBeHealed();
        TestFalseLifeEffectDataFactory();
        TestGenericTempHPFactory();

        Debug.Log($"====== False Life Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats MakeWizardStats(string name, int level = 3)
    {
        return new CharacterStats(name, level, "Wizard",
            8, 14, 12, 16, 18, 10,  // STR, DEX, CON, WIS, INT, CHA
            1, 0, 0,                  // BAB, armorBonus, shieldBonus
            4, 1, 0,                  // damageDice, damageCount, bonusDamage
            6, 1, 20,                 // baseSpeed, atkRange, baseHitDieHP
            "Human");
    }

    // ===== SPELL DEFINITION TESTS =====

    private static void TestSpellDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.FALSE_LIFE);
        Assert(spell != null, "Spell definition exists");
        if (spell == null) return;

        Assert(spell.SpellLevel == 2, "Spell level is 2", $"got {spell.SpellLevel}");
        Assert(spell.School == "Necromancy", "School is Necromancy", $"got {spell.School}");
        Assert(spell.TargetType == SpellTargetType.Self, "Target type is Self", $"got {spell.TargetType}");
        Assert(spell.RangeCategory == SpellRangeCategory.Personal, "Range is Personal", $"got {spell.RangeCategory}");
        Assert(spell.EffectType == SpellEffectType.Buff, "Effect type is Buff", $"got {spell.EffectType}");
        Assert(spell.DurationType == DurationType.Hours, "Duration type is Hours", $"got {spell.DurationType}");
        Assert(spell.DurationValue == 1, "Duration value is 1 (hour/level)", $"got {spell.DurationValue}");
        Assert(spell.DurationScalesWithLevel, "Duration scales with level");
        Assert(spell.BuffTempHP == 0, "BuffTempHP is 0 (calculated at cast time)", $"got {spell.BuffTempHP}");
        Assert(spell.ActionType == SpellActionType.Standard, "Action type is Standard", $"got {spell.ActionType}");
        Assert(spell.ProvokesAoO, "Provokes AoO");
    }

    // ===== TEMP HP CALCULATION TESTS =====

    private static void TestTempHPCalculation()
    {
        // Test deterministic calculation: 1d10 + min(CL, 10)
        // At CL 3 with die roll 5: 5 + 3 = 8
        int result = FalseLifeEffectData.CalculateTempHP(3, 5);
        Assert(result == 8, "Temp HP at CL3 roll 5 = 8", $"got {result}");

        // At CL 1 with die roll 1 (minimum): 1 + 1 = 2
        result = FalseLifeEffectData.CalculateTempHP(1, 1);
        Assert(result == 2, "Temp HP minimum at CL1 roll 1 = 2", $"got {result}");

        // At CL 10 with die roll 10 (maximum): 10 + 10 = 20
        result = FalseLifeEffectData.CalculateTempHP(10, 10);
        Assert(result == 20, "Temp HP maximum at CL10 roll 10 = 20", $"got {result}");

        // At CL 5 with die roll 7: 7 + 5 = 12
        result = FalseLifeEffectData.CalculateTempHP(5, 7);
        Assert(result == 12, "Temp HP at CL5 roll 7 = 12", $"got {result}");
    }

    private static void TestTempHPCalculationCLCap()
    {
        // CL bonus caps at +10, even at higher caster levels
        // At CL 15 with die roll 10: 10 + 10 = 20 (not 10 + 15 = 25)
        int result = FalseLifeEffectData.CalculateTempHP(15, 10);
        Assert(result == 20, "CL 15 caps at +10 bonus (max 20)", $"got {result}");

        // At CL 20 with die roll 5: 5 + 10 = 15 (not 5 + 20 = 25)
        result = FalseLifeEffectData.CalculateTempHP(20, 5);
        Assert(result == 15, "CL 20 caps at +10 bonus", $"got {result}");

        // At CL 10 with die roll 3: 3 + 10 = 13
        result = FalseLifeEffectData.CalculateTempHP(10, 3);
        Assert(result == 13, "CL 10 gives +10 bonus", $"got {result}");
    }

    // ===== DAMAGE ABSORPTION TESTS =====

    private static void TestTempHPAbsorbsDamageBeforeHP()
    {
        // Setup: character with 20 HP and 15 temp HP takes 20 damage
        // Result: 15 temp HP lost, 5 damage to regular HP = 15 HP remaining
        var stats = MakeWizardStats("Test Wizard");
        stats.CurrentHP = 20;
        stats.TempHP = 15;
        stats.TakeDamage(20);

        Assert(stats.TempHP == 0, "Temp HP depleted after 20 damage", $"got {stats.TempHP}");
        Assert(stats.CurrentHP == 15, "Regular HP reduced by overflow (20-15=5 overflow, 20-5=15)", $"got {stats.CurrentHP}");
    }

    private static void TestTempHPPartialAbsorption()
    {
        // Setup: character with 30 HP and 10 temp HP takes 7 damage
        // Result: 3 temp HP remain, regular HP unchanged
        var stats = MakeWizardStats("Test Wizard");
        stats.CurrentHP = 30;
        stats.TempHP = 10;
        stats.TakeDamage(7);

        Assert(stats.TempHP == 3, "Temp HP partially depleted (10-7=3)", $"got {stats.TempHP}");
        Assert(stats.CurrentHP == 30, "Regular HP unchanged when temp HP absorbs all", $"got {stats.CurrentHP}");
    }

    private static void TestTempHPFullAbsorption()
    {
        // Setup: character with 25 HP and 15 temp HP takes 15 damage (exact)
        // Result: 0 temp HP, regular HP unchanged
        var stats = MakeWizardStats("Test Wizard");
        stats.CurrentHP = 25;
        stats.TempHP = 15;
        stats.TakeDamage(15);

        Assert(stats.TempHP == 0, "Temp HP fully depleted (exact match)", $"got {stats.TempHP}");
        Assert(stats.CurrentHP == 25, "Regular HP unchanged (exact temp HP absorption)", $"got {stats.CurrentHP}");
    }

    // ===== NON-STACKING TESTS =====

    private static void TestNonStackingHigherWins()
    {
        // Setup: character has 12 temp HP from False Life, casts again for 15
        // Result: 15 temp HP (higher wins), NOT 27
        var stats = MakeWizardStats("Test Wizard");
        stats.CurrentHP = 20;

        var go = new GameObject("FalseLifeNonStackTest");
        var controller = go.AddComponent<CharacterController>();
        controller.Stats = stats;

        // Apply first False Life: 12 temp HP
        var effect1 = FalseLifeEffectData.CreateFalseLifeWithAmount(12, 3, controller);
        controller.ApplyFalseLifeEffect(effect1);

        Assert(stats.TempHP == 12, "First False Life gives 12 temp HP", $"got {stats.TempHP}");

        // Apply second False Life: 15 temp HP (higher)
        var effect2 = FalseLifeEffectData.CreateFalseLifeWithAmount(15, 3, controller);
        controller.ApplyFalseLifeEffect(effect2);

        Assert(stats.TempHP == 15, "Higher False Life replaces (15, not 27)", $"got {stats.TempHP}");
        Assert(controller.ActiveFalseLifeEffect.CurrentTempHP == 15, "Active effect has 15 temp HP", $"got {controller.ActiveFalseLifeEffect.CurrentTempHP}");

        Object.DestroyImmediate(go);
    }

    private static void TestNonStackingLowerBlocked()
    {
        // Setup: character has 15 temp HP from False Life, casts again for 10
        // Result: still 15 temp HP (higher stays)
        var stats = MakeWizardStats("Test Wizard");
        stats.CurrentHP = 20;

        var go = new GameObject("FalseLifeNonStackTest2");
        var controller = go.AddComponent<CharacterController>();
        controller.Stats = stats;

        // Apply first False Life: 15 temp HP
        var effect1 = FalseLifeEffectData.CreateFalseLifeWithAmount(15, 5, controller);
        controller.ApplyFalseLifeEffect(effect1);

        Assert(stats.TempHP == 15, "First False Life gives 15 temp HP", $"got {stats.TempHP}");

        // Apply second False Life: 10 temp HP (lower — should be blocked)
        var effect2 = FalseLifeEffectData.CreateFalseLifeWithAmount(10, 3, controller);
        controller.ApplyFalseLifeEffect(effect2);

        Assert(stats.TempHP == 15, "Lower False Life blocked (stays 15)", $"got {stats.TempHP}");
        Assert(controller.ActiveFalseLifeEffect.CurrentTempHP == 15, "Active effect still has 15 temp HP", $"got {controller.ActiveFalseLifeEffect.CurrentTempHP}");

        Object.DestroyImmediate(go);
    }

    // ===== DURATION AND DISCHARGE TESTS =====

    private static void TestDurationExpiration()
    {
        // When duration expires, remaining temp HP disappear
        var effect = FalseLifeEffectData.CreateFalseLifeWithAmount(15, 5, null);

        Assert(effect.IsActive, "Effect starts active");
        Assert(effect.CurrentTempHP == 15, "Starts with 15 temp HP", $"got {effect.CurrentTempHP}");

        // Simulate taking 6 damage — 9 temp HP remain
        effect.AbsorbDamage(6);
        Assert(effect.CurrentTempHP == 9, "After 6 damage: 9 temp HP remain", $"got {effect.CurrentTempHP}");
        Assert(effect.IsActive, "Still active after partial damage");

        // Duration expires
        effect.ExpireDuration();
        Assert(!effect.IsActive, "Inactive after duration expires");
        Assert(effect.CurrentTempHP == 0, "Temp HP gone after expiration", $"got {effect.CurrentTempHP}");
    }

    private static void TestDischargeOnZeroTempHP()
    {
        // When all temp HP are lost, effect is discharged immediately
        var effect = FalseLifeEffectData.CreateFalseLifeWithAmount(10, 3, null);

        Assert(effect.IsActive, "Effect starts active");

        // Take exactly 10 damage — discharged
        int overflow = effect.AbsorbDamage(10);
        Assert(overflow == 0, "No overflow when exact temp HP absorbed", $"got {overflow}");
        Assert(!effect.IsActive, "Discharged when temp HP reach 0");
        Assert(effect.CurrentTempHP == 0, "Temp HP is 0 after discharge", $"got {effect.CurrentTempHP}");

        // Test overflow scenario
        var effect2 = FalseLifeEffectData.CreateFalseLifeWithAmount(8, 3, null);
        int overflow2 = effect2.AbsorbDamage(12);
        Assert(overflow2 == 4, "Overflow damage = 4 (12-8)", $"got {overflow2}");
        Assert(!effect2.IsActive, "Discharged on overflow damage");
    }

    // ===== HEALING INTERACTION TEST =====

    private static void TestTempHPCannotBeHealed()
    {
        // Temp HP cannot be restored by healing — they stay at their reduced value
        var stats = MakeWizardStats("Test Wizard");
        stats.CurrentHP = 15;
        stats.TempHP = 10;

        // Take 12 damage: 10 temp HP absorbed, 2 to regular HP = 13 HP, 0 temp HP
        stats.TakeDamage(12);
        Assert(stats.TempHP == 0, "Temp HP depleted after damage", $"got {stats.TempHP}");
        Assert(stats.CurrentHP == 13, "Regular HP reduced by 2 overflow", $"got {stats.CurrentHP}");

        // "Heal" 5 HP — only regular HP should increase, temp HP stays at 0
        stats.CurrentHP = Mathf.Min(stats.TotalMaxHP, stats.CurrentHP + 5);
        Assert(stats.TempHP == 0, "Temp HP stays 0 after healing (cannot be healed)", $"got {stats.TempHP}");
        Assert(stats.CurrentHP == 18, "Regular HP healed to 18", $"got {stats.CurrentHP}");
    }

    // ===== FACTORY METHOD TESTS =====

    private static void TestFalseLifeEffectDataFactory()
    {
        var effect = FalseLifeEffectData.CreateFalseLifeWithAmount(14, 7, null);

        Assert(effect.GrantedTempHP == 14, "GrantedTempHP = 14", $"got {effect.GrantedTempHP}");
        Assert(effect.CurrentTempHP == 14, "CurrentTempHP = 14", $"got {effect.CurrentTempHP}");
        Assert(effect.CasterLevel == 7, "CasterLevel = 7", $"got {effect.CasterLevel}");
        Assert(effect.DurationRemainingRounds == 7 * 600, "Duration = 7 * 600 rounds", $"got {effect.DurationRemainingRounds}");
        Assert(effect.IsActive, "IsActive = true");
        Assert(effect.SourceSpellId == SpellNames.FALSE_LIFE, "SourceSpellId = false_life", $"got {effect.SourceSpellId}");
        Assert(effect.SourceName == "False Life", "SourceName = False Life", $"got {effect.SourceName}");
        Assert(effect.HasTempHP, "HasTempHP = true");
    }

    private static void TestGenericTempHPFactory()
    {
        // Test the generic factory for future temp HP sources (Aid, etc.)
        var effect = FalseLifeEffectData.CreateGenericTempHP("aid", "Aid", 8, 5, 50, null);

        Assert(effect.GrantedTempHP == 8, "Generic: GrantedTempHP = 8", $"got {effect.GrantedTempHP}");
        Assert(effect.SourceSpellId == "aid", "Generic: SourceSpellId = aid", $"got {effect.SourceSpellId}");
        Assert(effect.SourceName == "Aid", "Generic: SourceName = Aid", $"got {effect.SourceName}");
        Assert(effect.DurationRemainingRounds == 50, "Generic: Duration = 50 rounds", $"got {effect.DurationRemainingRounds}");
        Assert(effect.IsActive, "Generic: IsActive = true");
    }
}
}
