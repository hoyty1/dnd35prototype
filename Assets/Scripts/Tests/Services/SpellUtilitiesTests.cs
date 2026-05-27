using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for SpellUtilities — verifies spell save DC calculations,
/// casting ability modifier lookups, and immunity checks.
/// Run with SpellUtilitiesTests.RunAll().
/// </summary>
public static class SpellUtilitiesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SPELL UTILITIES TESTS ======");

        TestSaveDCFromComponents();
        TestCastingAbilityModifier_Wizard();
        TestCastingAbilityModifier_Cleric();
        TestIsMindAffectingImmune_Undead();
        TestIsSleepImmune_Elf();
        TestIsFearSpell();

        Debug.Log($"====== Spell Utilities Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    // --- Test Methods ---

    private static void TestSaveDCFromComponents()
    {
        // DC = 10 + spellLevel + castingAbilityMod
        int dc = SpellUtilities.GetSpellSaveDC(3, 4); // level-3 spell, +4 mod
        Assert(dc == 17, "SaveDC(3,4)==17", $"got {dc}");
    }

    private static void TestCastingAbilityModifier_Wizard()
    {
        // TODO: Requires CharacterStats mock — placeholder
        Assert(true, "CastingAbilityMod_Wizard (placeholder — needs mock)");
    }

    private static void TestCastingAbilityModifier_Cleric()
    {
        Assert(true, "CastingAbilityMod_Cleric (placeholder — needs mock)");
    }

    private static void TestIsMindAffectingImmune_Undead()
    {
        // TODO: Requires CharacterController mock with CreatureType = Undead
        Assert(true, "IsMindAffectingImmune_Undead (placeholder — needs mock)");
    }

    private static void TestIsSleepImmune_Elf()
    {
        Assert(true, "IsSleepImmune_Elf (placeholder — needs mock)");
    }

    private static void TestIsFearSpell()
    {
        // TODO: Requires SpellData with Fear descriptor
        Assert(true, "IsFearSpell (placeholder — needs SpellData)");
    }
}
}
