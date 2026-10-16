using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Magic
{
/// <summary>
/// Comprehensive tests for the D&D 3.5e Metamagic System (Phases 1-8).
/// Tests all 9 PHB metamagic feats, stacking, validation, spontaneous/prepared
/// caster differences, rod integration, and the live MetamagicSystem rules (batch 11 step 21, SPL-055:
/// ValidateMetamagicApplication, the cast core's Legality rule, and GetMetamagicCastingAction, its Cost rule).
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
        TestSpontaneousCasterFullRoundAction();
        TestPreparedCasterStandardAction();
        TestQuickenRefusedOnSpontaneousCast();
        TestCastingTimeFromSpellData();

        // Phase 7: Stacking & Validation
        TestMultipleMetamagicStackAdditively();
        TestSpellCompatibilityValidation();
        TestFeatOwnershipValidation();
        TestLevelCapValidation();
        TestFireballEmpoweredWidenedSlotLevel();

        // Phase 8: Rod Integration
        TestRodMetamagicValidation();
        TestRodTrackingInMetamagicData();
        TestMetamagicDataClone();

        // SpellData extensions
        TestSpellDataClonePreservesMetamagic();

        // Feat definitions
        TestAllMetamagicFeatsRegistered();

        // Batch 11 step 19: metamagic on the spell clone (SPL-038), Empower with Maximize (SPL-097), the Extend offer (SPL-095)
        TestApplyMetamagicStampsTheClone();
        TestEmpowerPlusMaximizeIsMaximumPlusHalfANormalRoll();
        TestExtendOfferMatchesItsEffect();

        // Batch 11 step 21: MetamagicSystem is the live legality and casting-action rule (SPL-055)
        TestValidationExemptions();
        TestHeightenMustRaiseTheLevel();
        TestMetamagicCastingActionIsTheCostRule();

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

    // The live action-cost rule: CastActionRules.CastingAction, which since batch 11 step 21 asks
    // MetamagicSystem.GetMetamagicCastingAction for every source but an item or a delivery; the cast's source decides
    // the spontaneous full round (PHB p.88; delegated RAW reading 2026-10-14), not the caster's classes.

    private static CastSource Source(CastSourceKind kind) => new CastSource { Kind = kind };

    private static void TestSpontaneousCasterFullRoundAction()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.EmpowerSpell);

        Assert(CastActionRules.CastingAction(spell, mm, Source(CastSourceKind.SpontaneousSlot)) == CastActionCost.FullRound,
            "A spontaneous caster's metamagic cast takes a full-round action (PHB p.88)");
        Assert(CastActionRules.CastingAction(spell, mm, Source(CastSourceKind.SpontaneousConversion)) == CastActionCost.FullRound,
            "A cleric's spontaneous metamagic conversion takes a full-round action (PHB p.88)");
        Assert(CastActionRules.CastingAction(spell, mm, new CastSource { Kind = CastSourceKind.HeldChargeHold, HeldFromSpontaneousSlot = true }) == CastActionCost.FullRound,
            "A held charge of a spontaneous metamagic spell is still cast spontaneously (full round)");
        Assert(CastActionRules.CastingAction(spell, null, Source(CastSourceKind.SpontaneousSlot)) == CastActionCost.Standard,
            "A spontaneous cast without metamagic keeps the standard action");

        // Delegated RAW reading 2026-10-14 (PHB p.88, p.98): spontaneous metamagic on a free-action spell also makes it a
        // full-round action, so it is no longer the round's free-action spell and provokes like any other casting.
        SpellData featherFall = SpellDatabase.GetSpell(SpellNames.FEATHER_FALL);
        var extend = new MetamagicData();
        extend.AppliedMetamagic.Add(MetamagicFeatId.ExtendSpell);
        Assert(featherFall != null
               && CastActionRules.CastingAction(featherFall, extend, Source(CastSourceKind.SpontaneousSlot)) == CastActionCost.FullRound
               && !CastActionRules.CountsAsQuickenedSpell(featherFall, extend, Source(CastSourceKind.SpontaneousSlot)),
            "A spontaneous metamagic Feather Fall is a full-round action, not the round's free-action spell (PHB p.88, p.98)");
        Assert(featherFall != null
               && CastActionRules.CastingAction(featherFall, null, Source(CastSourceKind.SpontaneousSlot)) == CastActionCost.Free
               && CastActionRules.CountsAsQuickenedSpell(featherFall, null, Source(CastSourceKind.SpontaneousSlot)),
            "A spontaneous Feather Fall without metamagic stays a free action (PHB p.174)");
    }

    private static void TestPreparedCasterStandardAction()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.EmpowerSpell);

        Assert(CastActionRules.CastingAction(spell, mm, Source(CastSourceKind.PreparedSlot)) == CastActionCost.Standard,
            "A prepared metamagic spell keeps its standard action, whatever other classes the caster has (SPL-159)");
        Assert(CastActionRules.CastingAction(spell, mm, Source(CastSourceKind.HeldChargeHold)) == CastActionCost.Standard,
            "A held charge of a prepared metamagic spell keeps its standard action");
        Assert(CastActionRules.CastingAction(spell, mm, Source(CastSourceKind.Scroll)) == CastActionCost.ItemOrDelivery
               && CastActionRules.CastingAction(spell, mm, Source(CastSourceKind.Wand)) == CastActionCost.ItemOrDelivery,
            "A scroll or wand charges the item's own action, not a casting action of its own (SPL-163; the spell's casting time is ITM-086)");
        Assert(CastSource.SlotKindFor(null, spell) == CastSourceKind.PreparedSlot,
            "A caster with no spellcasting component casts from a prepared slot (CastSource.SlotKindFor)");
    }

    private static void TestQuickenRefusedOnSpontaneousCast()
    {
        var spell = CreateTestDamageSpell("Magic Missile", 1, 1, 4);
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.QuickenSpell);

        Assert(CastActionRules.Refusal(null, spell, mm, Source(CastSourceKind.SpontaneousSlot)) == CastActionRules.RefusalQuickenSpontaneous,
            "Quicken Spell is refused on a spontaneous caster's spell (PHB p.98 Special)");
        Assert(CastActionRules.Refusal(null, spell, mm, Source(CastSourceKind.SpontaneousConversion)) == CastActionRules.RefusalQuickenSpontaneous,
            "Quicken Spell is refused on a cleric's spontaneous conversion (PHB p.98 Special)");
        Assert(CastActionRules.Refusal(null, spell, mm, Source(CastSourceKind.PreparedSlot)) == null,
            "Quicken Spell is allowed on a prepared spell");
        Assert(CastActionRules.CastingAction(spell, mm, Source(CastSourceKind.PreparedSlot)) == CastActionCost.Free
               && CastActionRules.CountsAsQuickenedSpell(spell, mm, Source(CastSourceKind.PreparedSlot)),
            "A quickened prepared spell is a free action and the round's quickened spell (PHB p.98)");
        Assert(!CastActionRules.CountsAsQuickenedSpell(spell, mm, Source(CastSourceKind.HeldChargeDelivery)),
            "Delivering a held charge is no casting, so it is not the round's quickened spell (PHB p.141)");
    }

    private static void TestCastingTimeFromSpellData()
    {
        foreach (string id in new[] { SpellNames.SLEEP, SpellNames.DEEP_SLUMBER, SpellNames.HYPNOTISM, SpellNames.CALL_LIGHTNING,
                     SpellNames.SUMMON_MONSTER_1, SpellNames.SUMMON_NATURES_ALLY_1, SpellNames.ENLARGE_PERSON })
        {
            SpellData spell = SpellDatabase.GetSpell(id);
            Assert(spell != null && spell.ActionType == SpellActionType.FullRound
                   && CastActionRules.CastingAction(spell, null, Source(CastSourceKind.PreparedSlot)) == CastActionCost.FullRound,
                id + ": a 1-round casting time is a full-round action (PHB p.174; SPL-162)");
        }

        SpellData featherFall = SpellDatabase.GetSpell(SpellNames.FEATHER_FALL);
        Assert(featherFall != null && CastActionRules.CastingAction(featherFall, null, Source(CastSourceKind.PreparedSlot)) == CastActionCost.Free
               && CastActionRules.CountsAsQuickenedSpell(featherFall, null, Source(CastSourceKind.PreparedSlot)),
            "Feather Fall is a free action that counts toward the one quickened spell per round (PHB p.174, p.229)");
        Assert(featherFall != null && !CastActionRules.CountsAsQuickenedSpell(featherFall, null, Source(CastSourceKind.Scroll)),
            "A scroll of Feather Fall costs the item's action and provokes today (ITM-086)");

        SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
        Assert(missile != null && CastActionRules.CastingAction(missile, null, Source(CastSourceKind.PreparedSlot)) == CastActionCost.Standard
               && !CastActionRules.CountsAsQuickenedSpell(missile, null, Source(CastSourceKind.PreparedSlot)),
            "Magic Missile is a standard action (PHB p.251)");
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

    private static MetamagicData Mm(params MetamagicFeatId[] feats)
    {
        var mm = new MetamagicData();
        foreach (MetamagicFeatId feat in feats)
            mm.AppliedMetamagic.Add(feat);
        return mm;
    }

    private static void TestSpellCompatibilityValidation()
    {
        var buffSpell = CreateTestBuffSpell("Shield", 1);
        string error = MetamagicSystem.ValidateMetamagicApplication(buffSpell, Mm(MetamagicFeatId.EmpowerSpell));
        Assert(error == MetamagicSystem.RefusalNotApplicable && !MetamagicData.IsApplicable(MetamagicFeatId.EmpowerSpell, buffSpell),
            $"A feat that cannot modify the spell is refused, as it is never offered (MetamagicData.IsApplicable; PHB p.88, p.93; delegated RAW reading 2026-10-14): {error}");
        SpellData cure = SpellDatabase.GetSpell(SpellNames.CURE_LIGHT_WOUNDS);
        Assert(cure != null && MetamagicSystem.ValidateMetamagicApplication(cure, Mm(MetamagicFeatId.ExtendSpell), null, Source(CastSourceKind.SpontaneousConversion)) == MetamagicSystem.RefusalNotApplicable
               && MetamagicSystem.ValidateMetamagicApplication(cure, Mm(MetamagicFeatId.EmpowerSpell), null, Source(CastSourceKind.SpontaneousConversion)) == null,
            "A cleric's conversion: Extend cannot modify an instantaneous Cure Light Wounds (PHB p.94), Empower can (PHB p.88, p.93)");
        Assert(MetamagicSystem.ValidateMetamagicApplication(buffSpell, null) == null
               && MetamagicSystem.ValidateMetamagicApplication(buffSpell, new MetamagicData()) == null,
            "No metamagic is always valid");
    }

    private static void TestFeatOwnershipValidation()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var stats = CreateTestCasterStats("NoFeats", "Wizard"); // No metamagic feats
        MetamagicData empower = Mm(MetamagicFeatId.EmpowerSpell);

        string error = MetamagicSystem.ValidateMetamagicApplication(spell, empower, stats, Source(CastSourceKind.PreparedSlot));
        Assert(error == MetamagicSystem.RefusalMissingFeat,
            $"Validation rejects a feat the caster lacks (PHB p.88): {error}");

        // With feat it should pass
        var statsWithFeat = CreateTestCasterStats("HasFeat", "Wizard", "Empower Spell");
        string errorWithFeat = MetamagicSystem.ValidateMetamagicApplication(spell, empower, statsWithFeat, Source(CastSourceKind.PreparedSlot));
        Assert(errorWithFeat == null, "Validation passes when character has the feat");
        Assert(MetamagicSystem.ValidateMetamagicApplication(spell, empower) == null,
            "Without the caster's stats the feat is not tested");
    }

    private static void TestLevelCapValidation()
    {
        var spell = CreateTestDamageSpell("TestSpell", 4, 10, 6);
        // +3 +4 = 11th level
        string error = MetamagicSystem.ValidateMetamagicApplication(spell, Mm(MetamagicFeatId.MaximizeSpell, MetamagicFeatId.QuickenSpell));
        Assert(error == MetamagicSystem.RefusalAboveNinth,
            $"Validation rejects an adjusted level above 9th: {error}");
        Assert(MetamagicSystem.ValidateMetamagicApplication(CreateTestDamageSpell("TestSpell", 2, 10, 6),
                   Mm(MetamagicFeatId.MaximizeSpell, MetamagicFeatId.QuickenSpell)) == null,
            "A 2nd-level spell maximized and quickened (9th) is valid");
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

    /// <summary>
    /// DMG p.233 (metamagic rods): a rod's feat needs no feat of the caster's and raises no level, so the feat test and the
    /// level cap skip it (MetamagicSystem.ValidateMetamagicApplication; rods are data only, ITM-018).
    /// </summary>
    private static void TestRodMetamagicValidation()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var noFeats = CreateTestCasterStats("NoFeats", "Wizard");
        var rod = new MetamagicData();
        rod.ApplyFromRod(MetamagicFeatId.EmpowerSpell);
        Assert(MetamagicSystem.ValidateMetamagicApplication(spell, rod, noFeats, Source(CastSourceKind.PreparedSlot)) == null,
            "A rod's Empower needs no Empower Spell feat");
        Assert(rod.GetEffectiveSpellLevelWithRods(3) == 3, "A rod's Empower does not raise the slot level");

        var rodPlusFeat = new MetamagicData();
        rodPlusFeat.ApplyFromRod(MetamagicFeatId.EmpowerSpell);
        rodPlusFeat.AppliedMetamagic.Add(MetamagicFeatId.MaximizeSpell);
        Assert(MetamagicSystem.ValidateMetamagicApplication(spell, rodPlusFeat, noFeats, Source(CastSourceKind.PreparedSlot)) == MetamagicSystem.RefusalMissingFeat,
            "A feat applied with a rod's still needs the caster's own feat");
        Assert(rodPlusFeat.GetEffectiveSpellLevelWithRods(3) == 6, "Fireball + rod Empower (+0) + feat Maximize (+3) = 6th");

        var high = CreateTestDamageSpell("High", 5, 10, 6);
        var rodHigh = new MetamagicData();
        rodHigh.ApplyFromRod(MetamagicFeatId.QuickenSpell);
        rodHigh.AppliedMetamagic.Add(MetamagicFeatId.SilentSpell);
        Assert(MetamagicSystem.ValidateMetamagicApplication(high, Mm(MetamagicFeatId.QuickenSpell, MetamagicFeatId.SilentSpell)) == MetamagicSystem.RefusalAboveNinth
               && MetamagicSystem.ValidateMetamagicApplication(high, rodHigh) == null,
            "The 9th-level cap counts only the feats' levels, not a rod's (5 + 4 + 1 refused; 5 + rod + 1 allowed)");
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

    // ========================================================================
    // BATCH 11 STEP 19 (SPL-038, SPL-097, SPL-095)
    // ========================================================================

    /// <summary>
    /// SpellCaster.ApplyMetamagicToSpellData stamps the metamagic on the clone it changes (SPL-038), which is not a
    /// prepared metamagic slot's spell; SpellData.Clone drops both, SpellCastRequest.CloneForCast keeps both.
    /// </summary>
    private static void TestApplyMetamagicStampsTheClone()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.EmpowerSpell);
        var clone = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(clone, mm);
        Assert(ReferenceEquals(clone.MetamagicDataRef, mm) && !clone.IsPreparedMetamagicSlotSpell,
            "SPL-038: ApplyMetamagicToSpellData stamps the metamagic on the clone (not a prepared-slot spell)");
        Assert(spell.MetamagicDataRef == null, "SPL-038: the source spell keeps no metamagic");
        Assert(clone.Clone().MetamagicDataRef == null, "SpellData.Clone drops the stamp");

        clone.IsPreparedMetamagicSlotSpell = true;
        SpellData forCast = SpellCastRequest.CloneForCast(clone);
        Assert(ReferenceEquals(forCast.MetamagicDataRef, mm) && forCast.IsPreparedMetamagicSlotSpell,
            "SpellCastRequest.CloneForCast keeps the stamp and the prepared-slot flag");

        var none = spell.Clone();
        SpellCaster.ApplyMetamagicToSpellData(none, new MetamagicData());
        Assert(none.MetamagicDataRef == null, "No metamagic, no stamp");
    }

    /// <summary>
    /// PHB p.97-98 (Maximize Spell): an empowered, maximized spell deals the maximum result plus one-half of the normally
    /// rolled result (SPL-097), through the one dice rule SpellDiceRules.Roll; before, the maximum x1.5.
    /// </summary>
    private static void TestEmpowerPlusMaximizeIsMaximumPlusHalfANormalRoll()
    {
        var mm = new MetamagicData();
        mm.AppliedMetamagic.Add(MetamagicFeatId.EmpowerSpell);
        mm.AppliedMetamagic.Add(MetamagicFeatId.MaximizeSpell);
        SpellDiceRoll roll = SpellDiceRules.RollWith(SpellDice.Of(10, 6), mm, sides => 2);
        Assert(roll.Total == 70 && roll.Base == 60 && roll.EmpowerBonus == 10,
            $"SPL-097: Empowered Maximized 10d6 rolling 2s = 60 + half of 20 = 70, not 90 (got {roll.Total})");
        SpellDiceRoll odd = SpellDiceRules.RollWith(SpellDice.Of(3, 6), mm, sides => 1);
        Assert(odd.Total == 19, $"SPL-097: Empowered Maximized 3d6 rolling 1s = 18 + half of 3, rounded down = 19 (got {odd.Total})");
    }

    /// <summary>
    /// SPL-095 (PHB p.94): the Extend Spell offer (MetamagicData.CanExtend, used by preparation, crafting and the combat
    /// panel) is the same test as its effect (SpellDurationRules.IsExtendable in ApplyMetamagicToSpellData) for every
    /// spell: Blur and Divine Power are offered, the permanent, instantaneous and concentration spells are not, and a
    /// rolled duration (Hypnotism's 2d4 rounds) and Death Knell's timed part are.
    /// </summary>
    private static void TestExtendOfferMatchesItsEffect()
    {
        SpellDatabase.Init();
        int mismatches = 0;
        string first = null;
        foreach (SpellData spell in SpellDatabase.GetAllSpells())
        {
            if (spell == null)
                continue;
            if (MetamagicData.CanExtend(spell) != SpellDurationRules.IsExtendable(spell))
            {
                mismatches++;
                first = first ?? spell.SpellId;
            }
        }
        Assert(mismatches == 0, $"SPL-095: the Extend offer equals its effect test for every spell ({mismatches} differ, first {first})");

        void Offered(string id, bool expected, string why)
        {
            SpellData spell = SpellDatabase.GetSpell(id);
            Assert(spell != null && MetamagicData.CanExtend(spell) == expected,
                $"SPL-095: Extend {(expected ? "is" : "is not")} offered for {spell?.Name ?? id} ({why})");
        }
        Offered(SpellNames.BLUR, true, "1 min./level");
        Offered(SpellNames.DIVINE_POWER, true, "1 round/level");
        Offered(SpellNames.HYPNOTISM, true, "2d4 rounds");
        Offered(SpellNames.DEATH_KNELL, true, "Instantaneous/10 minutes per HD: the timed part");
        Offered(SpellNames.BLINDNESS_DEAFNESS, false, "permanent");
        Offered(SpellNames.MAGIC_MISSILE, false, "instantaneous");
        Offered(SpellNames.MAGE_HAND, false, "concentration");
        Offered(SpellNames.RAINBOW_PATTERN, true, "concentration + 1 round/level: the rounds after concentration");
        Offered(SpellNames.SUMMON_SWARM, true, "concentration + 2 rounds: the rounds after concentration");

        // The effect follows the offer: an extended Death Knell clone doubles a duration its handler computes.
        SpellData knell = SpellDatabase.GetSpell(SpellNames.DEATH_KNELL).Clone();
        var extend = new MetamagicData();
        extend.AppliedMetamagic.Add(MetamagicFeatId.ExtendSpell);
        SpellCaster.ApplyMetamagicToSpellData(knell, extend);
        Assert(SpellDurationRules.Extended(knell, 300) == 600, "Extend doubles a handler's own rounds on an extended clone (Death Knell 300 to 600)");
        SpellData plainKnell = SpellDatabase.GetSpell(SpellNames.DEATH_KNELL).Clone();
        Assert(SpellDurationRules.Extended(plainKnell, 300) == 300, "A plain clone keeps the handler's rounds");
        // "Concentration + N rounds": Extend doubles only the rounds after the concentration (PHB p.94, p.176; delegated
        // RAW reading 2026-10-14).
        SpellData pattern = SpellDatabase.GetSpell(SpellNames.RAINBOW_PATTERN).Clone();
        SpellCaster.ApplyMetamagicToSpellData(pattern, extend);
        Assert(ConcentrationRules.TailRounds(pattern, 5) == 10, "Extend doubles the rounds after concentration on an extended clone (Rainbow Pattern CL 5: 5 to 10)");
        Assert(SpellDurationRules.Rounds(pattern, 5) == SpellDurationRules.ConcentrationRounds, "An extended Rainbow Pattern still lasts while its caster concentrates");
        Assert(ConcentrationRules.TailRounds(SpellDatabase.GetSpell(SpellNames.RAINBOW_PATTERN), 5) == 5, "Extend leaves the Rainbow Pattern template's tail at 1 round/level");

        // A handler's factory duration follows Extend: Command Undead lasts 2 days/level when extended (PHB p.211).
        SpellData command = SpellDatabase.GetSpell(SpellNames.COMMAND_UNDEAD).Clone();
        SpellCaster.ApplyMetamagicToSpellData(command, extend);
        CommandUndeadEffectData commanded = CommandUndeadEffectData.CreateForNonintelligent(null, null, 4, SpellDurationRules.Rounds(command, 4));
        Assert(commanded.DurationRemainingRounds == 8 * SpellDurationRules.RoundsPerDay, "An extended Command Undead at CL 4 lasts 8 days");
        CommandUndeadEffectData plainCommand = CommandUndeadEffectData.CreateForIntelligent(null, null, 4, SpellDurationRules.Rounds(SpellDatabase.GetSpell(SpellNames.COMMAND_UNDEAD), 4));
        Assert(plainCommand.DurationRemainingRounds == 4 * SpellDurationRules.RoundsPerDay, "A plain Command Undead at CL 4 lasts 4 days");
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

    // ========================================================================
    // BATCH 11 STEP 21 (SPL-055): MetamagicSystem is the live rule
    // ========================================================================

    /// <summary>
    /// The sources whose metamagic is not tested at the cast: a scroll or wand carries what its maker applied (DMG p.238,
    /// p.245), a held charge's delivery was tested at the hold (PHB p.141), the F12 panel's toggles stand in for the feats;
    /// Quicken on a spontaneous cast is refused whatever the caster's feats (PHB p.98 Special), a hold of a spontaneous
    /// spell included.
    /// </summary>
    private static void TestValidationExemptions()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        var noFeats = CreateTestCasterStats("NoFeats", "Wizard");
        MetamagicData empower = Mm(MetamagicFeatId.EmpowerSpell);
        Assert(MetamagicSystem.ValidateMetamagicApplication(spell, empower, noFeats, Source(CastSourceKind.Scroll)) == null
               && MetamagicSystem.ValidateMetamagicApplication(spell, empower, noFeats, Source(CastSourceKind.Wand)) == null,
            "A scroll's or wand's metamagic needs no feat of the reader's (DMG p.238, p.245)");
        Assert(MetamagicSystem.ValidateMetamagicApplication(spell, empower, noFeats, Source(CastSourceKind.HeldChargeDelivery)) == null,
            "A held charge's delivery is not tested again (PHB p.141)");
        Assert(MetamagicSystem.ValidateMetamagicApplication(spell, empower, noFeats, Source(CastSourceKind.TestPanel)) == null
               && MetamagicSystem.ValidateMetamagicApplication(CreateTestDamageSpell("High", 7, 10, 6), Mm(MetamagicFeatId.MaximizeSpell), noFeats, Source(CastSourceKind.TestPanel)) == MetamagicSystem.RefusalAboveNinth,
            "The F12 panel's toggles stand in for the feats; the level cap still binds it");

        var quickenFeat = CreateTestCasterStats("Quickener", "Sorcerer", "Quicken Spell");
        MetamagicData quicken = Mm(MetamagicFeatId.QuickenSpell);
        Assert(MetamagicSystem.ValidateMetamagicApplication(spell, quicken, quickenFeat, Source(CastSourceKind.SpontaneousSlot)) == MetamagicSystem.RefusalQuickenSpontaneous
               && MetamagicSystem.ValidateMetamagicApplication(spell, quicken, quickenFeat, new CastSource { Kind = CastSourceKind.HeldChargeHold, HeldFromSpontaneousSlot = true }) == MetamagicSystem.RefusalQuickenSpontaneous
               && MetamagicSystem.ValidateMetamagicApplication(spell, quicken, quickenFeat, Source(CastSourceKind.PreparedSlot)) == null,
            "Quicken Spell is refused on a spontaneous cast and a hold of one even with the feat, allowed on a prepared spell (PHB p.98)");
        Assert(CastActionRules.DescribeRefusal(MetamagicSystem.RefusalMissingFeat, "Ann", "Fireball", empower).Contains("Empower")
               && CastActionRules.DescribeRefusal(CastActionRules.RefusalNoFullRoundAction, "Ann", "Sleep").Contains("full-round"),
            "CastActionRules.DescribeRefusal gives the metamagic refusals' text and its own");
    }

    /// <summary>PHB p.95 (Heighten Spell): a heightened spell has a higher spell level than normal; no raise, no Heighten.</summary>
    private static void TestHeightenMustRaiseTheLevel()
    {
        var missile = CreateTestDamageSpell("Missile", 1, 1, 4);
        var heighten = Mm(MetamagicFeatId.HeightenSpell);
        heighten.HeightenToLevel = 1;
        Assert(MetamagicSystem.ValidateMetamagicApplication(missile, heighten) == MetamagicSystem.RefusalHeightenNoRaise,
            "Heighten to the spell's own level is refused (PHB p.95)");
        heighten.HeightenToLevel = -1;
        Assert(MetamagicSystem.ValidateMetamagicApplication(missile, heighten) == MetamagicSystem.RefusalHeightenNoRaise,
            "Heighten with no level chosen is refused");
        heighten.HeightenToLevel = 4;
        var withFeat = CreateTestCasterStats("Heightener", "Wizard", "Heighten Spell");
        Assert(MetamagicSystem.ValidateMetamagicApplication(missile, heighten, withFeat, Source(CastSourceKind.PreparedSlot)) == null
               && heighten.GetEffectiveSpellLevel(1) == 4,
            "Heighten to 4th is valid and takes a 4th-level slot");
    }

    /// <summary>
    /// The cast core's Cost stage charges MetamagicSystem.GetMetamagicCastingAction (batch 11 step 21), the rule
    /// CastActionRules.CastingAction gives the PC menu and the AI: the same answer for every slot source.
    /// </summary>
    private static void TestMetamagicCastingActionIsTheCostRule()
    {
        var spell = CreateTestDamageSpell("Fireball", 3, 10, 6);
        MetamagicData empower = Mm(MetamagicFeatId.EmpowerSpell);
        MetamagicData quicken = Mm(MetamagicFeatId.QuickenSpell);
        Assert(MetamagicSystem.GetMetamagicCastingAction(spell, empower, Source(CastSourceKind.SpontaneousSlot)) == CastActionCost.FullRound
               && MetamagicSystem.GetMetamagicCastingAction(spell, empower, Source(CastSourceKind.SpontaneousConversion)) == CastActionCost.FullRound
               && MetamagicSystem.GetMetamagicCastingAction(spell, empower, Source(CastSourceKind.PreparedSlot)) == CastActionCost.Standard
               && MetamagicSystem.GetMetamagicCastingAction(spell, quicken, Source(CastSourceKind.PreparedSlot)) == CastActionCost.Free
               && MetamagicSystem.GetMetamagicCastingAction(spell, null, Source(CastSourceKind.SpontaneousSlot)) == CastActionCost.Standard,
            "GetMetamagicCastingAction: spontaneous metamagic a full round, prepared metamagic its casting time, quickened a free action (PHB p.88, p.98)");
        bool same = true;
        foreach (CastSourceKind kind in new[] { CastSourceKind.PreparedSlot, CastSourceKind.SpontaneousSlot, CastSourceKind.SpontaneousConversion,
                     CastSourceKind.HeldChargeHold, CastSourceKind.TestPanel })
            foreach (MetamagicData mm in new[] { null, empower, quicken })
                same &= MetamagicSystem.GetMetamagicCastingAction(spell, mm, Source(kind)) == CastActionRules.CastingAction(spell, mm, Source(kind));
        Assert(same, "CastActionRules.CastingAction is GetMetamagicCastingAction for every slot, hold and F12 source");
    }
}
}
