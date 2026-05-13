using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Deep Slumber and Heroism spell definitions.
/// Run with DeepSlumberAndHeroismTests.RunAll().
/// </summary>
public static class DeepSlumberAndHeroismTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== DEEP SLUMBER & HEROISM SPELL TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        // Deep Slumber tests
        TestDeepSlumberDefinitionExists();
        TestDeepSlumberCoreRules();
        TestDeepSlumberClassAvailability();
        TestDeepSlumberRangeIsClose();

        // Heroism tests
        TestHeroismDefinitionExists();
        TestHeroismCoreRules();
        TestHeroismClassAvailability();
        TestHeroismBonusType();
        TestHeroismRangeIsTouch();

        Debug.Log($"====== Deep Slumber & Heroism Results: {_passed} passed, {_failed} failed ======");
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

    // ═══════════════════════════════════════════════════════
    //  DEEP SLUMBER TESTS
    // ═══════════════════════════════════════════════════════

    private static void TestDeepSlumberDefinitionExists()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.DEEP_SLUMBER);
        Assert(spell != null, "Deep Slumber spell exists in SpellDatabase");
    }

    private static void TestDeepSlumberCoreRules()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.DEEP_SLUMBER);
        if (spell == null) { Assert(false, "Deep Slumber core rules (spell missing)"); return; }

        Assert(spell.SpellLevel == 3, "Deep Slumber is level 3", $"got {spell.SpellLevel}");
        Assert(spell.School == "Enchantment", "Deep Slumber school is Enchantment", $"got {spell.School}");
        Assert(spell.IsMindAffecting, "Deep Slumber is Mind-Affecting");
        Assert(spell.AllowsSavingThrow, "Deep Slumber allows Will save");
        Assert(spell.SavingThrowType == "Will", "Deep Slumber save type is Will", $"got {spell.SavingThrowType}");
        Assert(spell.SpellResistanceApplies, "Deep Slumber SR applies");
        Assert(spell.TargetType == SpellTargetType.Area, "Deep Slumber is Area target type");
        Assert(spell.AoEShapeType == AoEShape.Burst, "Deep Slumber AoE is Burst");
        Assert(spell.AoESizeSquares == 2, "Deep Slumber AoE is 10-ft (2 squares) radius", $"got {spell.AoESizeSquares}");
        Assert(spell.DurationType == DurationType.Minutes, "Deep Slumber duration type is Minutes");
        Assert(spell.DurationValue == 1, "Deep Slumber duration value is 1 min/level", $"got {spell.DurationValue}");
        Assert(spell.DurationScalesWithLevel, "Deep Slumber duration scales with level");
        Assert(!spell.IsPlaceholder, "Deep Slumber is not a placeholder");
        Assert(spell.EffectType == SpellEffectType.Debuff, "Deep Slumber effect type is Debuff");
        Assert(spell.BlockedByProtectionFromAlignment, "Deep Slumber blocked by Protection from Alignment");
    }

    private static void TestDeepSlumberClassAvailability()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.DEEP_SLUMBER);
        if (spell == null) { Assert(false, "Deep Slumber class availability (spell missing)"); return; }

        Assert(spell.IsAvailableFor("Bard", 3), "Deep Slumber available for Bard 3");
        Assert(spell.IsAvailableFor("Sorcerer", 3), "Deep Slumber available for Sorcerer 3");
        Assert(spell.IsAvailableFor("Wizard", 3), "Deep Slumber available for Wizard 3");
        Assert(!spell.IsAvailableFor("Cleric", 3), "Deep Slumber NOT available for Cleric");
    }

    private static void TestDeepSlumberRangeIsClose()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.DEEP_SLUMBER);
        if (spell == null) { Assert(false, "Deep Slumber range (spell missing)"); return; }

        SpellRangeCategory range = spell.GetEffectiveRangeCategory();
        Assert(range == SpellRangeCategory.Close, "Deep Slumber range is Close", $"got {range}");

        // CL 5: Close = 25 + 5*(5/2) = 25 + 10 = 35 ft = 7 squares
        int rangeAtCL5 = spell.GetRangeSquaresForCasterLevel(5);
        Assert(rangeAtCL5 == 7, "Deep Slumber range at CL5 = 7 squares (35 ft)", $"got {rangeAtCL5}");
    }

    // ═══════════════════════════════════════════════════════
    //  HEROISM TESTS
    // ═══════════════════════════════════════════════════════

    private static void TestHeroismDefinitionExists()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.HEROISM);
        Assert(spell != null, "Heroism spell exists in SpellDatabase");
    }

    private static void TestHeroismCoreRules()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.HEROISM);
        if (spell == null) { Assert(false, "Heroism core rules (spell missing)"); return; }

        Assert(spell.SpellLevel == 3, "Heroism is level 3 (Sor/Wiz)", $"got {spell.SpellLevel}");
        Assert(spell.School == "Enchantment", "Heroism school is Enchantment", $"got {spell.School}");
        Assert(spell.IsMindAffecting, "Heroism is Mind-Affecting");
        Assert(spell.AllowsSavingThrow, "Heroism allows Will save (harmless)");
        Assert(spell.SavingThrowType == "Will", "Heroism save type is Will", $"got {spell.SavingThrowType}");
        Assert(spell.SpellResistanceApplies, "Heroism SR applies (harmless)");
        Assert(spell.EffectType == SpellEffectType.Buff, "Heroism effect type is Buff");
        Assert(spell.BuffAttackBonus == 2, "Heroism +2 attack bonus", $"got {spell.BuffAttackBonus}");
        Assert(spell.BuffSaveBonus == 2, "Heroism +2 save bonus", $"got {spell.BuffSaveBonus}");
        Assert(spell.DurationType == DurationType.Minutes, "Heroism duration type is Minutes");
        Assert(spell.DurationValue == 10, "Heroism duration value is 10 min/level", $"got {spell.DurationValue}");
        Assert(spell.DurationScalesWithLevel, "Heroism duration scales with level");
        Assert(!spell.IsPlaceholder, "Heroism is not a placeholder");
    }

    private static void TestHeroismClassAvailability()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.HEROISM);
        if (spell == null) { Assert(false, "Heroism class availability (spell missing)"); return; }

        Assert(spell.IsAvailableFor("Bard", 2), "Heroism available for Bard 2");
        Assert(spell.IsAvailableFor("Sorcerer", 3), "Heroism available for Sorcerer 3");
        Assert(spell.IsAvailableFor("Wizard", 3), "Heroism available for Wizard 3");
        Assert(!spell.IsAvailableFor("Cleric", 3), "Heroism NOT available for Cleric");
    }

    private static void TestHeroismBonusType()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.HEROISM);
        if (spell == null) { Assert(false, "Heroism bonus type (spell missing)"); return; }

        Assert(spell.BonusTypeExplicitlySet, "Heroism bonus type explicitly set");
        Assert(spell.BuffBonusType == BonusType.Morale, "Heroism bonus type is Morale", $"got {spell.BuffBonusType}");
        Assert(spell.GetEffectiveBonusType() == BonusType.Morale, "Heroism effective bonus type is Morale");

        // Morale bonuses should NOT stack
        Assert(!BonusTypeHelper.DoesStack(BonusType.Morale), "Morale bonuses do not stack (D&D 3.5e rule)");
    }

    private static void TestHeroismRangeIsTouch()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.HEROISM);
        if (spell == null) { Assert(false, "Heroism range (spell missing)"); return; }

        Assert(spell.IsTouchSpell(), "Heroism is a touch spell");
        Assert(spell.IsTouch, "Heroism IsTouch flag is set");
        Assert(spell.TargetType == SpellTargetType.SingleAlly, "Heroism targets SingleAlly");
    }
}
}
