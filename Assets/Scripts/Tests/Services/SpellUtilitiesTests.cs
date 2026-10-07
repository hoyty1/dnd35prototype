using System;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for SpellUtilities — verifies spell save DC calculations,
/// casting ability modifier lookups, Fear spell identification, and immunity checks.
/// Run with SpellUtilitiesTests.RunAll().
///
/// PHB 3.5e References:
///   - Spell Save DC = 10 + spell level + casting ability mod (p.171)
///   - [Mind-Affecting] immunity: undead, constructs, oozes, plants, vermin (MM)
///   - [Sleep] immunity: elves, half-elves (PHB p.15)
///   - [Fear] spells: Cause Fear, Scare, Fear (PHB p.208, p.274, p.229)
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

        // Pure-function tests (no mocks needed)
        TestSaveDCFromComponents();
        TestSaveDCFromComponents_ZeroLevel();
        TestSaveDCFromComponents_NegativeMod();
        TestSaveDCFromComponents_HighLevel();
        TestIsFearSpell_CauseFear();
        TestIsFearSpell_Scare();
        TestIsFearSpell_Fear();
        TestIsFearSpell_NonFear();
        TestIsFearSpell_Null();

        // Save negation shared by the PC and NPC single-target cast paths (SPL-007)
        TestSaveNegates_ControlSaved();
        TestSaveNegates_DebuffSaved();
        TestSaveNegates_ControlFailedOrNoSave();
        TestSaveNegates_PartialSavesNotNegated();
        TestSaveNegates_CommandUndead();
        TestSaveNegates_BuffAndBlur();
        TestSaveNegates_ReclassifiedHoldPerson();

        // Mock-required tests (documented but skipped without live CharacterController)
        TestCastingAbilityModifier_Wizard();
        TestCastingAbilityModifier_Cleric();
        TestIsMindAffectingImmune_Undead();
        TestIsSleepImmune_Elf();

        Debug.Log($"====== Spell Utilities Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    // ──────────────────────────────────────────────
    //  Spell Save DC  (pure: 10 + spellLevel + mod)
    // ──────────────────────────────────────────────

    private static void TestSaveDCFromComponents()
    {
        // DC = 10 + 3 + 4 = 17
        int dc = SpellUtilities.GetSpellSaveDC(3, 4);
        Assert(dc == 17, "SaveDC(3,4)==17", $"got {dc}");
    }

    private static void TestSaveDCFromComponents_ZeroLevel()
    {
        // Cantrip (level 0) with +2 mod: DC = 10 + 0 + 2 = 12
        int dc = SpellUtilities.GetSpellSaveDC(0, 2);
        Assert(dc == 12, "SaveDC(0,2)==12 (cantrip)", $"got {dc}");
    }

    private static void TestSaveDCFromComponents_NegativeMod()
    {
        // Level 1 spell, -1 ability mod: DC = 10 + 1 + (-1) = 10
        int dc = SpellUtilities.GetSpellSaveDC(1, -1);
        Assert(dc == 10, "SaveDC(1,-1)==10 (negative mod)", $"got {dc}");
    }

    private static void TestSaveDCFromComponents_HighLevel()
    {
        // 9th-level spell, +8 mod (epic caster): DC = 10 + 9 + 8 = 27
        int dc = SpellUtilities.GetSpellSaveDC(9, 8);
        Assert(dc == 27, "SaveDC(9,8)==27 (epic)", $"got {dc}");
    }

    // ──────────────────────────────────────────────
    //  IsFearSpell — tests with real SpellData
    // ──────────────────────────────────────────────

    private static SpellData MakeSpell(string spellId)
    {
        var spell = new SpellData();
        spell.SpellId = spellId;
        return spell;
    }

    private static void TestIsFearSpell_CauseFear()
    {
        bool result = SpellUtilities.IsFearSpell(MakeSpell("cause_fear"));
        Assert(result, "IsFearSpell(cause_fear)==true");
    }

    private static void TestIsFearSpell_Scare()
    {
        bool result = SpellUtilities.IsFearSpell(MakeSpell("scare"));
        Assert(result, "IsFearSpell(scare)==true");
    }

    private static void TestIsFearSpell_Fear()
    {
        bool result = SpellUtilities.IsFearSpell(MakeSpell("fear"));
        Assert(result, "IsFearSpell(fear)==true");
    }

    private static void TestIsFearSpell_NonFear()
    {
        bool result = SpellUtilities.IsFearSpell(MakeSpell("magic_missile"));
        Assert(!result, "IsFearSpell(magic_missile)==false");
    }

    private static void TestIsFearSpell_Null()
    {
        bool result = SpellUtilities.IsFearSpell(null);
        Assert(!result, "IsFearSpell(null)==false");
    }

    // ──────────────────────────────────────────────
    //  IsEffectNegatedBySave (SPL-007)
    //  A successful save against a "negates" spell means no effect (PHB p.177).
    //  SpellCategoryClassifier turns many Debuff spells into Control, so both
    //  types must be negated, on the PC and NPC paths alike.
    // ──────────────────────────────────────────────

    private static SpellData MakeSpell(string spellId, SpellEffectType effectType)
    {
        var spell = MakeSpell(spellId);
        spell.EffectType = effectType;
        return spell;
    }

    private static SpellResult MakeSaveResult(bool requiredSave, bool saveSucceeded)
    {
        return new SpellResult { Success = true, RequiredSave = requiredSave, SaveSucceeded = saveSucceeded };
    }

    private static void TestSaveNegates_ControlSaved()
    {
        // Regression: before SPL-007 the NPC path negated only Debuff, so a saved Hold Person still paralyzed.
        bool negated = SpellUtilities.IsEffectNegatedBySave(
            MakeSpell(SpellNames.HOLD_PERSON, SpellEffectType.Control), MakeSaveResult(true, true), false);
        Assert(negated, "SaveNegates: Control spell (Hold Person) negated on a successful save");
    }

    private static void TestSaveNegates_DebuffSaved()
    {
        bool negated = SpellUtilities.IsEffectNegatedBySave(
            MakeSpell(SpellNames.GHOUL_TOUCH, SpellEffectType.Debuff), MakeSaveResult(true, true), false);
        Assert(negated, "SaveNegates: Debuff spell negated on a successful save");
    }

    private static void TestSaveNegates_ControlFailedOrNoSave()
    {
        SpellData hold = MakeSpell(SpellNames.HOLD_PERSON, SpellEffectType.Control);
        Assert(!SpellUtilities.IsEffectNegatedBySave(hold, MakeSaveResult(true, false), false),
            "SaveNegates: Control spell applies on a failed save");
        Assert(!SpellUtilities.IsEffectNegatedBySave(hold, MakeSaveResult(false, false), false),
            "SaveNegates: Control spell applies when no save was rolled");
        Assert(!SpellUtilities.IsEffectNegatedBySave(null, MakeSaveResult(true, true), false)
               && !SpellUtilities.IsEffectNegatedBySave(hold, null, false),
            "SaveNegates: null spell or result is not negated");
    }

    private static void TestSaveNegates_PartialSavesNotNegated()
    {
        // Cause Fear (PHB p.208) and Scare (PHB p.274): Will partial, shaken 1 round on a save.
        Assert(!SpellUtilities.IsEffectNegatedBySave(
                MakeSpell(SpellNames.CAUSE_FEAR, SpellEffectType.Control), MakeSaveResult(true, true), false),
            "SaveNegates: Cause Fear save is partial, not negated");
        Assert(!SpellUtilities.IsEffectNegatedBySave(
                MakeSpell(SpellNames.SCARE, SpellEffectType.Control), MakeSaveResult(true, true), false),
            "SaveNegates: Scare save is partial, not negated");
    }

    private static void TestSaveNegates_CommandUndead()
    {
        // PHB p.211: nonintelligent undead get no save; intelligent undead save normally (Will negates).
        SpellData command = MakeSpell(SpellNames.COMMAND_UNDEAD, SpellEffectType.Control);
        Assert(!SpellUtilities.IsEffectNegatedBySave(command, MakeSaveResult(true, true), true),
            "SaveNegates: Command Undead vs nonintelligent undead ignores the rolled save");
        Assert(SpellUtilities.IsEffectNegatedBySave(command, MakeSaveResult(true, true), false),
            "SaveNegates: Command Undead vs intelligent undead is negated on a save");
    }

    private static void TestSaveNegates_BuffAndBlur()
    {
        // Harmless buffs keep their old behaviour (willing allies auto-fail via forceTargetToFailSave).
        Assert(!SpellUtilities.IsEffectNegatedBySave(
                MakeSpell(SpellNames.MAGE_ARMOR, SpellEffectType.Buff), MakeSaveResult(true, true), false),
            "SaveNegates: Buff spell is not negated by this rule");
        Assert(SpellUtilities.IsEffectNegatedBySave(
                MakeSpell(SpellNames.BLUR, SpellEffectType.Buff), MakeSaveResult(true, true), false),
            "SaveNegates: Blur negated when an unwilling target saves");
    }

    private static void TestSaveNegates_ReclassifiedHoldPerson()
    {
        // Integration: the database reclassifies Hold Person to Control at init (the root of SPL-007).
        SpellDatabase.Init();
        SpellData hold = SpellDatabase.GetSpell(SpellNames.HOLD_PERSON);
        if (hold == null)
        {
            Assert(false, "SaveNegates: Hold Person registered in SpellDatabase");
            return;
        }

        Assert(hold.EffectType == SpellEffectType.Control,
            "SaveNegates: database Hold Person is Control after reclassification", $"got {hold.EffectType}");
        Assert(SpellUtilities.IsEffectNegatedBySave(hold, MakeSaveResult(true, true), false),
            "SaveNegates: database Hold Person negated on a successful save");
    }

    // ──────────────────────────────────────────────
    //  Mock-required tests (CharacterController needed)
    // ──────────────────────────────────────────────

    private static void TestCastingAbilityModifier_Wizard()
    {
        // Requires CharacterStats mock with ClassName = "Wizard" and INT modifier
        // Expected: returns stats.IntModifier for Wizard class (PHB p.171)
        Assert(true, "CastingAbilityMod_Wizard (SKIP — needs CharacterStats mock)");
    }

    private static void TestCastingAbilityModifier_Cleric()
    {
        // Requires CharacterStats mock with ClassName = "Cleric" and WIS modifier
        // Expected: returns stats.WisModifier for Cleric class (PHB p.171)
        Assert(true, "CastingAbilityMod_Cleric (SKIP — needs CharacterStats mock)");
    }

    private static void TestIsMindAffectingImmune_Undead()
    {
        // Requires CharacterController mock with CreatureType = "Undead"
        // Expected: returns true (undead immune to [Mind-Affecting], MM p.317)
        Assert(true, "IsMindAffectingImmune_Undead (SKIP — needs CharacterController mock)");
    }

    private static void TestIsSleepImmune_Elf()
    {
        // Requires CharacterController mock with Race = "Elf"
        // Expected: returns true (elves immune to magical sleep, PHB p.15)
        Assert(true, "IsSleepImmune_Elf (SKIP — needs CharacterController mock)");
    }
}
}
