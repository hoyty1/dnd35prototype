using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Web spell data and area-effect rule constants.
/// Run with WebSpellRulesTests.RunAll().
/// </summary>
public static class WebSpellRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== WEB SPELL RULES TESTS ======");

        SpellDatabase.Init();

        TestWebSpellDefinition();
        TestWebAreaConstants();
        TestEntangledConditionDefinitionStillMatchesWebPenaltyEnvelope();

        Debug.Log($"====== Web Spell Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static void TestWebSpellDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.WEB);
        Assert(spell != null, "Web spell exists");
        if (spell == null)
            return;

        Assert(spell.TargetType == SpellTargetType.Area, "Web is an area spell");
        Assert(spell.AoEShapeType == AoEShape.Burst && spell.AoESizeSquares == 4,
            "Web area is a 20-ft radius spread (4 squares)",
            $"shape={spell.AoEShapeType}, size={spell.AoESizeSquares}");
        Assert(spell.RangeCategory == SpellRangeCategory.Medium,
            "Web uses Medium range");
        Assert(spell.AllowsSavingThrow && spell.SavingThrowType == "Reflex",
            "Web uses Reflex save");

        int cl1 = spell.GetRangeSquaresForCasterLevel(1);
        int cl10 = spell.GetRangeSquaresForCasterLevel(10);
        Assert(cl1 == 22, "Web range at caster level 1 is 110 ft (22 squares)", $"observed={cl1}");
        Assert(cl10 == 40, "Web range scales by +10 ft/level (2 squares/level)", $"observed={cl10}");

        Assert(spell.DurationType == DurationType.Minutes
               && spell.DurationValue == 10
               && spell.DurationScalesWithLevel,
            "Web duration is 10 min/level");
        Assert(spell.IsDismissible, "Web is dismissible");

        int expectedRoundsCl3 = 300;
        int observedRoundsCl3 = ActiveSpellEffect.CalculateDurationRounds(spell, 3);
        Assert(observedRoundsCl3 == expectedRoundsCl3,
            "Web duration converts to 100 rounds/level",
            $"expected={expectedRoundsCl3}, observed={observedRoundsCl3}");
    }

    private static void TestWebAreaConstants()
    {
        Assert(WebAreaEffect.EscapeDc == 20,
            "Web escape DC is 20",
            $"observed={WebAreaEffect.EscapeDc}");
        Assert(WebAreaEffect.SectionHitPoints == 12,
            "Web section HP is 12",
            $"observed={WebAreaEffect.SectionHitPoints}");
    }

    private static void TestEntangledConditionDefinitionStillMatchesWebPenaltyEnvelope()
    {
        ConditionDefinition def = ConditionRules.GetDefinition(CombatConditionType.Entangled);
        Assert(def != null, "Entangled definition exists");
        if (def == null)
            return;

        Assert(def.AttackModifier == -2,
            "Entangled applies -2 attack penalty",
            $"observed={def.AttackModifier}");
        Assert(Mathf.Approximately(def.MovementMultiplier, 0.5f),
            "Base entangled movement multiplier remains 0.5 for non-Web entangle sources",
            $"observed={def.MovementMultiplier}");
    }
}
}
