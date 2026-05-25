using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Magic
{
/// <summary>
/// Comprehensive tests for the D&D 3.5e Metamagic System (Phases 1-8).
/// Tests all 9 PHB metamagic feats, stacking, validation, spontaneous/prepared
/// caster differences, rod integration, and the MetamagicSystem pipeline.
/// Run via MetamagicSystemTests.RunAll() from a runtime test hook.
/// </summary>
public static class MetamagicSystemTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        // Init required systems
        RaceDatabase.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();
        SpellDatabase.Init();

        Debug.Log("========== METAMAGIC SYSTEM TESTS ==========");

        // Phase 1: Core System
        TestMetamagicFeatIdEnumHasAll9Types();
        TestMetamagicDataLevelAdjustments();
        TestMetamagicData9thLevelCap();
        TestMetamagicDataExceedsMaxSpellLevel();
        TestMetamagicModifierCreation();
        TestMetamagicSystemPipeline();
        TestMetamagicSystemPipelineLevelCap();

        // Phase 2: Numeric Modifiers
        TestEmpowerSpellDamageIncrease();
        TestMaximizeSpellDamageMaximized();
        TestEmpowerPlusMaximizeStacking();
        TestEmpowerApplicabilityCheck();
        TestMaximizeApplicabilityCheck();

        // Phase 3: Range/Duration/Area
        TestEnlargeSpellDoubleRange();
        TestExtendSpellDoubleDuration();
        TestExtendSpellSkipsInstantaneous();
        TestExtendSpellSkipsPermanent();
        TestExtendSpellSkipsConcentration();
        TestWidenSpellDoubleArea();
        TestWidenSpellDoubleAoESize();

        // Phase 4: Component Modifiers
        TestSilentSpellRemovesVerbal();
        TestStillSpellRemovesSomatic();
        TestSilentPlusStillRemovesBoth();

        // Phase 5: Advanced Metamagics
        TestQuickenSpellFreeAction();
        TestHeightenSpellVariableLevel();
        TestHeightenSpellIncreaseDC();

        // Phase 6: Spontaneous vs Prepared
        TestSpontaneousCasterDetection();
        TestPreparedCasterDetection();
        TestSpontaneousCasterFullRoundAction();
        TestPreparedCasterStandardAction();
        TestQuickenOverridesSpontaneousPenalty();

        // Phase 7: Stacking & Validation
        TestMultipleMetamagicStackAdditively();
        TestNoDuplicateMetamagicValidation();
        TestSpellCompatibilityValidation();
        TestFeatOwnershipValidation();
        TestLevelCapValidation();
        TestFireballEmpoweredWidenedSlotLevel();

        // Phase 8: Rod Integration
        TestRodAppliedMetamagicNoSlotIncrease();
        TestRodPlusFeatStacking();
        TestRodTrackingInMetamagicData();
        TestMetamagicDataClone();

        // SpellData extensions
        TestSpellDataMetamagicTracking();
        TestSpellDataClonePreservesMetamagic();

        // Feat definitions
        TestAllMetamagicFeatsRegistered();

        Debug.Log($"========== METAMAGIC TESTS: {_passed} passed, {_failed} failed ==========");
    }

    // ========================================================================
    // HELPERS
    // ========================================================================

    private static void Assert(bool condition, string testName)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  ✓ {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  ✗ FAIL: {testName}");
        }
    }

    private static SpellData CreateTestDamageSpell(string name, int level, int diceCount, int diceSides, int range = 20, int areaRadius = 4)
    {
        return new SpellData
        {
            SpellId = $"test_{name.ToLower()}",
            Name = name,
            SpellLevel = level,
            School = "Evocation",
            EffectType = SpellEffectType.Damage,
            DamageCount = diceCount,
            DamageDice = diceSides,
            DamageType = "fire",
            RangeSquares = range,
            AreaRadius = areaRadius,
            AoEShapeType = AoEShape.Burst,
            AoESizeSquares = areaRadius,
            HasVerbalComponent = true,
            HasSomaticComponent = true,
            AllowsSavingThrow = true,
            SavingThrowType = "Reflex",
            SaveHalves = true,
            DurationType = DurationType.Instantaneous,
            ActionType = SpellActionType.Standard,
            TargetType = SpellTargetType.Area,
            AvailableFor = new List<SpellAvailability>
            {
                new SpellAvailability("Wizard", level),
                new SpellAvailability("Sorcerer", level)
            }
        };
    }

    private static SpellData CreateTestBuffSpell(string name, int level, int durationRounds = 10)
    {
        return new SpellData
        {
            SpellId = $"test_{name.ToLower()}",
            Name = name,
            SpellLevel = level,
            School = "Abjuration",
            EffectType = SpellEffectType.Buff,
            BuffACBonus = 4,
            BuffDurationRounds = durationRounds,
            DurationType = DurationType.Rounds,
            DurationValue = durationRounds,
            DurationScalesWithLevel = false,
            RangeSquares = 1, // Touch
            HasVerbalComponent = true,
            HasSomaticComponent = true,
            ActionType = SpellActionType.Standard,
            TargetType = SpellTargetType.SingleAlly,
            AvailableFor = new List<SpellAvailability>
            {
                new SpellAvailability("Wizard", level)
            }
        };
    }

    private static SpellData CreateTestHealSpell(string name, int level, int healCount, int healDice)
    {
        return new SpellData
        {
            SpellId = $"test_{name.ToLower()}",
            Name = name,
            SpellLevel = level,
            School = "Conjuration",
            EffectType = SpellEffectType.Healing,
            HealCount = healCount,
            HealDice = healDice,
            BonusHealing = 5,
            RangeSquares = 1,
            HasVerbalComponent = true,
            HasSomaticComponent = true,
            DurationType = DurationType.Instantaneous,
            ActionType = SpellActionType.Standard,
            TargetType = SpellTargetType.SingleAlly,
            AvailableFor = new List<SpellAvailability>
            {
                new SpellAvailability("Cleric", level)
            }
        };
    }

    private static CharacterStats CreateTestCasterStats(string name, string className, params string[] feats)
    {
        var stats = new CharacterStats();
        stats.CharacterName = name;
        stats.CharacterClass = className;
        stats.ClassLevels = new List<ClassLevelEntry>
        {
            new ClassLevelEntry { ClassName = className, Level = 10 }
        };
        stats.INT = 18;
        stats.WIS = 16;
        stats.CHA = 16;
        foreach (var feat in feats)
            stats.Feats.Add(feat);
        return stats;
    }

    // ========================================================================
    // PHASE 1: CORE SYSTEM
    // ========================================================================

    private static void TestMetamagicFeatIdEnumHasAll9Types()
    {
        Assert(MetamagicData.AllMetamagicFeats.Length == 9,
            "MetamagicFeatId enum has all 9 PHB metamagic types");
    }

    private static void TestMetamagicDataLevelAdjustments()
    {
        var mm = new MetamagicData();

        // Test standard adjustments
        Assert(mm.GetLevelAdjustment(MetamagicFeatId.EmpowerSpell, 3) == 2,
            "Empower Spell level adjustment is +2");
        Assert(mm.GetLevelAdjustment(MetamagicFeatId.EnlargeSpell, 3) == 1,
            "Enlarge Spell level adjustment is +1");
        Assert(mm.GetLevelAdjustment(MetamagicFeatId.ExtendSpell, 3) == 1,
            "Extend Spell level adjustment is +1");
        Assert(mm.GetLevelAdjustment(MetamagicFeatId.MaximizeSpell, 3) == 3,
            "Maximize Spell level adjustment is +3");
        Assert(mm.GetLevelAdjustment(MetamagicFeatId.QuickenSpell, 3) == 4,
            "Quicken Spell level adjustment is +4");
        Assert(mm.GetLevelAdjustment(MetamagicFeatId.SilentSpell, 3) == 1,
            "Silent Spell level adjustment is +1");
        Assert(mm.GetLevelAdjustment(MetamagicFeatId.StillSpell, 3) == 1,
            "Still Spell level adjustment is +1");
        Assert(mm.GetLevelAdjustment(MetamagicFeatId.WidenSpell, 3) == 3,
            "Widen Spell level adjustment is +3");
    }

    private static void TestMetamagicData9thLevelCap()
    {
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.MaximizeSpell); // +3
        mm.AppliedMetamagic.Add(MetamagicFeatId.QuickenSpell);  // +4
        // Total: base 3 + 3 + 4 = 10 → capped to 9
        int effective = mm.GetEffectiveSpellLevel(3);
        Assert(effective == 9,
            $"Effective spell level capped at 9 (got {effective} for base 3 + Maximize + Quicken)");
    }

    private static void TestMetamagicDataExceedsMaxSpellLevel()
    {
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.MaximizeSpell); // +3
        mm.AppliedMetamagic.Add(MetamagicFeatId.QuickenSpell);  // +4
        Assert(mm.ExceedsMaxSpellLevel(3),
            "ExceedsMaxSpellLevel returns true for base 3 + Maximize + Quicken = 10");
        Assert(!mm.ExceedsMaxSpellLevel(1),
            "ExceedsMaxSpellLevel returns false for base 1 + Maximize + Quicken = 8");
    }

    private static void TestMetamagicModifierCreation()
    {
        var mod = new MetamagicModifier(MetamagicFeatId.EmpowerSpell, 3);
        Assert(mod.Type == MetamagicFeatId.EmpowerSpell, "MetamagicModifier type is EmpowerSpell");
        Assert(mod.SlotIncrease == 2, "MetamagicModifier slot increase is 2 for Empower");
        Assert(!mod.AppliedByRod, "MetamagicModifier default is not from rod");

        var rodMod = new MetamagicModifier(MetamagicFeatId.EnlargeSpell, 3, true);
        Assert(rodMod.AppliedByRod, "MetamagicModifier AppliedByRod is true when specified");

        var heighten = MetamagicModifier.CreateHeighten(1, 5);
        Assert(heighten.Type == MetamagicFeatId.HeightenSpell, "Heighten modifier type correct");
        Assert(heighten.SlotIncrease == 4, "Heighten 1→5 slot increase is 4");
        Assert(heighten.HeightenToLevel == 5, "Heighten target level is 5");
    }

    private static void TestMetamagicSystemPipeline()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6, 40, 4);
        var mods = new List<MetamagicModifier>
        {
            new MetamagicModifier(MetamagicFeatId.EmpowerSpell, 3)
        };

        var result = MetamagicSystem.PrepareMetamagicSpell(spell, mods);
        Assert(result.IsSuccess, "MetamagicSystem pipeline succeeds for Empowered Fireball");
        Assert(result.EffectiveSpellLevel == 5, $"Empowered Fireball effective level is 5 (got {result.EffectiveSpellLevel})");
        Assert(result.ModifiedSpell.BaseSpellLevel == 3, "Modified spell tracks base level 3");
        Assert(result.ModifiedSpell.EffectiveSpellLevel == 5, "Modified spell tracks effective level 5");
        Assert(result.AppliedMetamagics.Contains(MetamagicFeatId.EmpowerSpell), "Applied metamagics includes Empower");
    }

    private static void TestMetamagicSystemPipelineLevelCap()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var mods = new List<MetamagicModifier>
        {
            new MetamagicModifier(MetamagicFeatId.MaximizeSpell, 3),  // +3 → 6
            new MetamagicModifier(MetamagicFeatId.QuickenSpell, 3),   // +4 → 10
        };

        var result = MetamagicSystem.PrepareMetamagicSpell(spell, mods);
        Assert(!result.IsSuccess, "Pipeline rejects Maximized+Quickened Fireball (level 10 > 9)");
        Assert(result.ErrorMessage.Contains("exceeds maximum"),
            $"Error message mentions level cap: {result.ErrorMessage}");
    }

    // ========================================================================
    // PHASE 2: NUMERIC MODIFIERS
    // ========================================================================

    private static void TestEmpowerSpellDamageIncrease()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        Assert(MetamagicData.CanEmpower(spell), "Empower can be applied to damage spell");

        // Empower is resolved in SpellCaster.Cast (not in ApplyMetamagicToSpellData)
        // We test the applicability and level adjustment
        Assert(MetamagicData.GetStandardLevelAdjustment(MetamagicFeatId.EmpowerSpell) == 2,
            "Empower standard level adjustment is +2");
    }

    private static void TestMaximizeSpellDamageMaximized()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        Assert(MetamagicData.CanMaximize(spell), "Maximize can be applied to damage spell");
        Assert(MetamagicData.GetStandardLevelAdjustment(MetamagicFeatId.MaximizeSpell) == 3,
            "Maximize standard level adjustment is +3");
    }

    private static void TestEmpowerPlusMaximizeStacking()
    {
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.EmpowerSpell);  // +2
        mm.AppliedMetamagic.Add(MetamagicFeatId.MaximizeSpell); // +3
        int totalAdj = mm.GetTotalLevelAdjustment(3);
        Assert(totalAdj == 5, $"Empower+Maximize total adjustment is +5 (got {totalAdj})");
        int effective = mm.GetEffectiveSpellLevel(3);
        Assert(effective == 8, $"Empowered Maximized Fireball (Lv3) uses Lv8 slot (got {effective})");
    }

    private static void TestEmpowerApplicabilityCheck()
    {
        var buffSpell = CreateTestBuffSpell("Shield", 1);
        Assert(!MetamagicData.CanEmpower(buffSpell),
            "Empower cannot be applied to non-damage/healing buff spell");

        var healSpell = CreateTestHealSpell("Cure Light", 1, 1, 8);
        Assert(MetamagicData.CanEmpower(healSpell),
            "Empower can be applied to healing spell");
    }

    private static void TestMaximizeApplicabilityCheck()
    {
        var buffSpell = CreateTestBuffSpell("Shield", 1);
        Assert(!MetamagicData.CanMaximize(buffSpell),
            "Maximize cannot be applied to non-damage/healing buff spell");
    }

    // ========================================================================
    // PHASE 3: RANGE/DURATION/AREA
    // ========================================================================

    private static void TestEnlargeSpellDoubleRange()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6, 40, 4);
        spell.RangeIncreaseSquares = 2;
        spell.RangeIncreasePerLevels = 1;

        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.EnlargeSpell);

        var clone = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(clone, mm);

        Assert(clone.RangeSquares == 80, $"Enlarge doubles range: 40 → {clone.RangeSquares} (expected 80)");
        Assert(clone.RangeIncreaseSquares == 4, $"Enlarge doubles scaling: 2 → {clone.RangeIncreaseSquares} (expected 4)");
        Assert(spell.RangeSquares == 40, "Original spell range unchanged after Enlarge");
    }

    private static void TestExtendSpellDoubleDuration()
    {
        var spell = CreateTestBuffSpell("Mage Armor", 1, 10);
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.ExtendSpell);

        var clone = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(clone, mm);

        Assert(clone.DurationValue == 20, $"Extend doubles duration: 10 → {clone.DurationValue} (expected 20)");
        Assert(clone.BuffDurationRounds == 20, $"Extend doubles legacy duration: 10 → {clone.BuffDurationRounds} (expected 20)");
        Assert(spell.DurationValue == 10, "Original spell duration unchanged after Extend");
    }

    private static void TestExtendSpellSkipsInstantaneous()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        spell.DurationType = DurationType.Instantaneous;
        spell.DurationValue = 0;

        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.ExtendSpell);

        var clone = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(clone, mm);

        Assert(clone.DurationValue == 0, "Extend skips instantaneous duration spells");
    }

    private static void TestExtendSpellSkipsPermanent()
    {
        var spell = CreateTestBuffSpell("Test Perm", 5, 0);
        spell.DurationType = DurationType.Permanent;
        spell.DurationValue = 0;

        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.ExtendSpell);

        var clone = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(clone, mm);

        Assert(clone.DurationValue == 0, "Extend skips permanent duration spells");
    }

    private static void TestExtendSpellSkipsConcentration()
    {
        var spell = CreateTestBuffSpell("Test Conc", 3, 0);
        spell.DurationType = DurationType.Concentration;
        spell.DurationValue = 0;

        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.ExtendSpell);

        var clone = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(clone, mm);

        Assert(clone.DurationValue == 0, "Extend skips concentration duration spells");
    }

    private static void TestWidenSpellDoubleArea()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6, 40, 4);
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.WidenSpell);

        var clone = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(clone, mm);

        Assert(clone.AreaRadius == 8, $"Widen doubles AreaRadius: 4 → {clone.AreaRadius} (expected 8)");
        Assert(clone.AoESizeSquares == 8, $"Widen doubles AoESizeSquares: 4 → {clone.AoESizeSquares} (expected 8)");
        Assert(spell.AreaRadius == 4, "Original spell area unchanged after Widen");
    }

    private static void TestWidenSpellDoubleAoESize()
    {
        // Test cone spell (no AreaRadius, has AoESizeSquares)
        var coneSpell = new SpellData
        {
            SpellId = "test_cone",
            Name = "Test Cone",
            SpellLevel = 3,
            EffectType = SpellEffectType.Damage,
            AoEShapeType = AoEShape.Cone,
            AoESizeSquares = 6,
            AreaRadius = 0,
            TargetType = SpellTargetType.Area
        };

        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.WidenSpell);

        var clone = coneSpell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(clone, mm);

        Assert(clone.AoESizeSquares == 12, $"Widen doubles cone AoESizeSquares: 6 → {clone.AoESizeSquares} (expected 12)");
    }

    // ========================================================================
    // PHASE 4: COMPONENT MODIFIERS
    // ========================================================================

    private static void TestSilentSpellRemovesVerbal()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        Assert(spell.HasVerbalComponent, "Spell starts with verbal component");

        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.SilentSpell);

        var clone = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(clone, mm);

        Assert(!clone.HasVerbalComponent, "Silent Spell removes verbal component");
        Assert(clone.HasSomaticComponent, "Silent Spell preserves somatic component");
        Assert(spell.HasVerbalComponent, "Original spell verbal component unchanged");
    }

    private static void TestStillSpellRemovesSomatic()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        Assert(spell.HasSomaticComponent, "Spell starts with somatic component");

        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.StillSpell);

        var clone = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(clone, mm);

        Assert(!clone.HasSomaticComponent, "Still Spell removes somatic component");
        Assert(clone.HasVerbalComponent, "Still Spell preserves verbal component");
    }

    private static void TestSilentPlusStillRemovesBoth()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);

        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.SilentSpell);
        mm.AppliedMetamagic.Add(MetamagicFeatId.StillSpell);

        var clone = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(clone, mm);

        Assert(!clone.HasVerbalComponent && !clone.HasSomaticComponent,
            "Silent+Still removes both verbal and somatic components");

        int totalAdj = mm.GetTotalLevelAdjustment(3);
        Assert(totalAdj == 2, $"Silent+Still total adjustment is +2 (got {totalAdj})");
    }

    // ========================================================================
    // PHASE 5: ADVANCED METAMAGICS
    // ========================================================================

    private static void TestQuickenSpellFreeAction()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        Assert(spell.ActionType == SpellActionType.Standard, "Spell starts as standard action");

        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.QuickenSpell);

        var clone = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(clone, mm);

        Assert(clone.ActionType == SpellActionType.Free, "Quicken changes action type to Free");

        int adj = mm.GetTotalLevelAdjustment(3);
        Assert(adj == 4, $"Quicken level adjustment is +4 (got {adj})");
    }

    private static void TestHeightenSpellVariableLevel()
    {
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.HeightenSpell);
        mm.HeightenToLevel = 7;

        int adj = mm.GetTotalLevelAdjustment(3); // Fireball base 3, heighten to 7
        Assert(adj == 4, $"Heighten Spell 3→7 adjustment is +4 (got {adj})");

        int effective = mm.GetEffectiveSpellLevel(3);
        Assert(effective == 7, $"Heightened Fireball effective level is 7 (got {effective})");
    }

    private static void TestHeightenSpellIncreaseDC()
    {
        // Heighten Spell increases effective level, which increases save DC
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.HeightenSpell);
        mm.HeightenToLevel = 5;

        // Magic Missile (1st level) heightened to 5th
        int adj = mm.GetTotalLevelAdjustment(1);
        Assert(adj == 4, $"Heighten 1→5 adjustment is +4 (got {adj})");

        int effective = mm.GetEffectiveSpellLevel(1);
        Assert(effective == 5, $"Heightened Magic Missile effective level is 5 (got {effective})");

        var heightenMod = MetamagicModifier.CreateHeighten(1, 5);
        Assert(heightenMod.SlotIncrease == 4, $"Heighten modifier slot increase is 4 (got {heightenMod.SlotIncrease})");
    }

    // ========================================================================
    // PHASE 6: SPONTANEOUS VS PREPARED
    // ========================================================================

    private static void TestSpontaneousCasterDetection()
    {
        var sorcerer = CreateTestCasterStats("TestSorcerer", "Sorcerer");
        var bard = CreateTestCasterStats("TestBard", "Bard");
        var wizard = CreateTestCasterStats("TestWizard", "Wizard");

        Assert(MetamagicSystem.IsSpontaneousCaster(sorcerer), "Sorcerer is spontaneous caster");
        Assert(MetamagicSystem.IsSpontaneousCaster(bard), "Bard is spontaneous caster");
        Assert(!MetamagicSystem.IsSpontaneousCaster(wizard), "Wizard is NOT spontaneous caster");
    }

    private static void TestPreparedCasterDetection()
    {
        var wizard = CreateTestCasterStats("TestWizard", "Wizard");
        var cleric = CreateTestCasterStats("TestCleric", "Cleric");
        var sorcerer = CreateTestCasterStats("TestSorcerer", "Sorcerer");

        Assert(MetamagicSystem.IsPreparedCaster(wizard), "Wizard is prepared caster");
        Assert(MetamagicSystem.IsPreparedCaster(cleric), "Cleric is prepared caster");
        Assert(!MetamagicSystem.IsPreparedCaster(sorcerer), "Sorcerer is NOT prepared caster");
    }

    private static void TestSpontaneousCasterFullRoundAction()
    {
        var sorcerer = CreateTestCasterStats("TestSorcerer", "Sorcerer");
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.EmpowerSpell);

        var action = MetamagicSystem.GetMetamagicCastingAction(spell, mm, sorcerer);
        Assert(action == SpellActionType.FullRound,
            "Spontaneous caster with metamagic takes full-round action");
    }

    private static void TestPreparedCasterStandardAction()
    {
        var wizard = CreateTestCasterStats("TestWizard", "Wizard");
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.EmpowerSpell);

        var action = MetamagicSystem.GetMetamagicCastingAction(spell, mm, wizard);
        Assert(action == SpellActionType.Standard,
            "Prepared caster with metamagic keeps standard action");
    }

    private static void TestQuickenOverridesSpontaneousPenalty()
    {
        var sorcerer = CreateTestCasterStats("TestSorcerer", "Sorcerer");
        var spell = CreateTestDamageSpell("Magic Missile", 1, 1, 4);
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.QuickenSpell);

        var action = MetamagicSystem.GetMetamagicCastingAction(spell, mm, sorcerer);
        Assert(action == SpellActionType.Free,
            "Quicken overrides spontaneous caster full-round penalty (Free action)");
    }

    // ========================================================================
    // PHASE 7: STACKING & VALIDATION
    // ========================================================================

    private static void TestMultipleMetamagicStackAdditively()
    {
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.EmpowerSpell);  // +2
        mm.AppliedMetamagic.Add(MetamagicFeatId.WidenSpell);    // +3
        int totalAdj = mm.GetTotalLevelAdjustment(3);
        Assert(totalAdj == 5, $"Empower+Widen stack additively: +2+3 = +5 (got {totalAdj})");
        int effective = mm.GetEffectiveSpellLevel(3);
        Assert(effective == 8, $"Fireball Lv3 + Empower + Widen = Lv8 slot (got {effective})");
    }

    private static void TestNoDuplicateMetamagicValidation()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var mods = new List<MetamagicModifier>
        {
            new MetamagicModifier(MetamagicFeatId.EmpowerSpell, 3),
            new MetamagicModifier(MetamagicFeatId.EmpowerSpell, 3) // Duplicate!
        };

        string error = MetamagicSystem.ValidateMetamagicApplication(spell, mods);
        Assert(error != null && error.Contains("twice"),
            $"Validation rejects duplicate metamagic: {error}");
    }

    private static void TestSpellCompatibilityValidation()
    {
        var buffSpell = CreateTestBuffSpell("Shield", 1);
        var mods = new List<MetamagicModifier>
        {
            new MetamagicModifier(MetamagicFeatId.EmpowerSpell, 1) // Can't empower a buff
        };

        string error = MetamagicSystem.ValidateMetamagicApplication(buffSpell, mods);
        Assert(error != null && error.Contains("cannot be applied"),
            $"Validation rejects incompatible metamagic: {error}");
    }

    private static void TestFeatOwnershipValidation()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var stats = CreateTestCasterStats("NoFeats", "Wizard"); // No metamagic feats
        var mods = new List<MetamagicModifier>
        {
            new MetamagicModifier(MetamagicFeatId.EmpowerSpell, 3)
        };

        string error = MetamagicSystem.ValidateMetamagicApplication(spell, mods, stats);
        Assert(error != null && error.Contains("does not have"),
            $"Validation rejects missing feat: {error}");

        // With feat it should pass
        var statsWithFeat = CreateTestCasterStats("HasFeat", "Wizard", "Empower Spell");
        string errorWithFeat = MetamagicSystem.ValidateMetamagicApplication(spell, mods, statsWithFeat);
        Assert(errorWithFeat == null, "Validation passes when character has the feat");
    }

    private static void TestLevelCapValidation()
    {
        var spell = CreateTestDamageSpell("TestSpell", 4, 10, 6);
        var mods = new List<MetamagicModifier>
        {
            new MetamagicModifier(MetamagicFeatId.MaximizeSpell, 4),  // +3 → 7
            new MetamagicModifier(MetamagicFeatId.QuickenSpell, 4),   // +4 → 11
        };

        string error = MetamagicSystem.ValidateMetamagicApplication(spell, mods);
        Assert(error != null && error.Contains("exceeds maximum"),
            $"Validation rejects level cap violation: {error}");
    }

    private static void TestFireballEmpoweredWidenedSlotLevel()
    {
        // Classic example: Fireball (3rd) + Empower (+2) + Widen (+3) = 8th level slot
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.EmpowerSpell);
        mm.AppliedMetamagic.Add(MetamagicFeatId.WidenSpell);

        int effective = mm.GetEffectiveSpellLevel(3);
        Assert(effective == 8,
            $"Empowered Widened Fireball: Lv3 + 2 + 3 = Lv8 (got {effective})");

        // Now add Maximize (+3) → 11, exceeds cap
        mm.AppliedMetamagic.Add(MetamagicFeatId.MaximizeSpell);
        Assert(mm.ExceedsMaxSpellLevel(3),
            "Empowered Widened Maximized Fireball: 3+2+3+3 = 11 exceeds cap");
    }

    // ========================================================================
    // PHASE 8: ROD INTEGRATION
    // ========================================================================

    private static void TestRodAppliedMetamagicNoSlotIncrease()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var mods = new List<MetamagicModifier>
        {
            new MetamagicModifier(MetamagicFeatId.EmpowerSpell, 3, true) // Rod-applied
        };

        var result = MetamagicSystem.PrepareMetamagicSpell(spell, mods);
        Assert(result.IsSuccess, "Rod-applied metamagic succeeds");
        Assert(result.EffectiveSpellLevel == 3,
            $"Rod-applied Empower does not increase slot level (got {result.EffectiveSpellLevel}, expected 3)");
        Assert(result.HasRodMetamagic, "Result tracks rod metamagic");
    }

    private static void TestRodPlusFeatStacking()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var mods = new List<MetamagicModifier>
        {
            new MetamagicModifier(MetamagicFeatId.EmpowerSpell, 3, true),  // Rod: +0
            new MetamagicModifier(MetamagicFeatId.MaximizeSpell, 3, false), // Feat: +3
        };

        var result = MetamagicSystem.PrepareMetamagicSpell(spell, mods);
        Assert(result.IsSuccess, "Rod+feat combination succeeds");
        Assert(result.EffectiveSpellLevel == 6,
            $"Fireball + Rod Empower (+0) + Feat Maximize (+3) = Lv6 (got {result.EffectiveSpellLevel})");
        Assert(result.HasRodMetamagic, "Result tracks rod metamagic");
    }

    private static void TestRodTrackingInMetamagicData()
    {
        var mm = new MetamagicData();
        mm.ApplyFromRod(MetamagicFeatId.EnlargeSpell);
        mm.AppliedMetamagic.Add(MetamagicFeatId.EmpowerSpell); // Feat

        Assert(mm.HasAnyMetamagic, "MetamagicData has metamagic after rod + feat");
        Assert(mm.HasRodMetamagic, "MetamagicData tracks rod metamagic");
        Assert(mm.IsFromRod(MetamagicFeatId.EnlargeSpell), "Enlarge identified as from rod");
        Assert(!mm.IsFromRod(MetamagicFeatId.EmpowerSpell), "Empower identified as from feat");

        int featOnly = mm.GetFeatOnlyLevelAdjustment(3);
        Assert(featOnly == 2, $"Feat-only level adjustment is 2 (Empower), got {featOnly}");

        int withRods = mm.GetEffectiveSpellLevelWithRods(3);
        Assert(withRods == 5, $"Effective level with rods: 3 + 2 (Empower feat only) = 5, got {withRods}");
    }

    private static void TestMetamagicDataClone()
    {
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.EmpowerSpell);
        mm.ApplyFromRod(MetamagicFeatId.EnlargeSpell);
        mm.HeightenToLevel = 5;

        var clone = mm.Clone();
        Assert(clone.Has(MetamagicFeatId.EmpowerSpell), "Clone has Empower");
        Assert(clone.IsFromRod(MetamagicFeatId.EnlargeSpell), "Clone tracks rod Enlarge");
        Assert(clone.HeightenToLevel == 5, "Clone preserves heighten level");

        // Modify original, clone should be unaffected
        mm.AppliedMetamagic.Add(MetamagicFeatId.MaximizeSpell);
        Assert(!clone.Has(MetamagicFeatId.MaximizeSpell), "Clone is independent of original");
    }

    // ========================================================================
    // SPELLDATA EXTENSIONS
    // ========================================================================

    private static void TestSpellDataMetamagicTracking()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var mods = new List<MetamagicModifier>
        {
            new MetamagicModifier(MetamagicFeatId.EmpowerSpell, 3),
            new MetamagicModifier(MetamagicFeatId.WidenSpell, 3)
        };

        var result = MetamagicSystem.PrepareMetamagicSpell(spell, mods);
        Assert(result.IsSuccess, "Pipeline succeeds");
        Assert(result.ModifiedSpell.HasMetamagic, "Modified spell has metamagic flag");
        Assert(result.ModifiedSpell.HasMetamagicType(MetamagicFeatId.EmpowerSpell), "Tracks Empower");
        Assert(result.ModifiedSpell.HasMetamagicType(MetamagicFeatId.WidenSpell), "Tracks Widen");
        Assert(!result.ModifiedSpell.HasMetamagicType(MetamagicFeatId.QuickenSpell), "Does not track unapplied Quicken");
        Assert(result.ModifiedSpell.BaseSpellLevel == 3, "BaseSpellLevel is 3");
        Assert(result.ModifiedSpell.EffectiveSpellLevel == 8, "EffectiveSpellLevel is 8");
    }

    private static void TestSpellDataClonePreservesMetamagic()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        spell.AppliedMetamagics = new List<MetamagicFeatId> { MetamagicFeatId.EmpowerSpell };
        spell.BaseSpellLevel = 3;
        spell.EffectiveSpellLevel = 5;
        spell.HasRodMetamagic = true;

        var clone = spell.Clone();
        Assert(clone.HasMetamagic, "Clone preserves metamagic flag");
        Assert(clone.HasMetamagicType(MetamagicFeatId.EmpowerSpell), "Clone preserves Empower");
        Assert(clone.BaseSpellLevel == 3, "Clone preserves BaseSpellLevel");
        Assert(clone.EffectiveSpellLevel == 5, "Clone preserves EffectiveSpellLevel");
        Assert(clone.HasRodMetamagic, "Clone preserves HasRodMetamagic");

        // Modify clone, original should be unaffected
        clone.AppliedMetamagics.Add(MetamagicFeatId.MaximizeSpell);
        Assert(!spell.HasMetamagicType(MetamagicFeatId.MaximizeSpell),
            "Original spell unaffected by clone modification");
    }

    // ========================================================================
    // FEAT DEFINITIONS
    // ========================================================================

    private static void TestAllMetamagicFeatsRegistered()
    {
        string[] expectedFeats = new string[]
        {
            "Empower Spell", "Enlarge Spell", "Extend Spell",
            "Heighten Spell", "Maximize Spell", "Quicken Spell",
            "Silent Spell", "Still Spell", "Widen Spell"
        };

        int found = 0;
        foreach (var expected in expectedFeats)
        {
            var def = FeatDefinitions.GetFeat(expected);
            if (def != null && def.Type == FeatType.Metamagic)
            {
                found++;
                Assert(def.Benefit != null && def.Benefit.IsMetamagic,
                    $"Feat '{expected}' has IsMetamagic benefit flag");
                Assert(def.IsWizardBonus,
                    $"Feat '{expected}' is marked as Wizard bonus feat");
            }
            else
            {
                Assert(false, $"Missing metamagic feat definition: {expected}");
            }
        }
        Assert(found == 9, $"All 9 metamagic feats registered in FeatDefinitions ({found}/9)");
    }
}
}
