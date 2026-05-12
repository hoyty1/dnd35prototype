using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Tests for Spectral Hand spell (D&D 3.5e PHB p.282).
/// Validates HP loss/recovery mechanics, AC calculation, touch attack bonus,
/// hand destruction, spell delivery, and duration mechanics.
/// Run with SpectralHandRulesTests.RunAll().
/// </summary>
public static class SpectralHandRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SPECTRAL HAND RULES TESTS ======");

        SpellDatabase.Init();
        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();

        TestSpellDefinition();
        TestHPLossOnCasting();
        TestHandHPEqualsHPLost();
        TestHandACCalculation();
        TestHandACMinimum22();
        TestTouchAttackBonus();
        TestHPRecoveryOnNormalEnd();
        TestPermanentHPLossOnDestruction();
        TestHPRecoveryOnDismissal();
        TestTouchSpellDeliveryEligibility();
        TestTouchSpellLevelCap();
        TestHandDamage();
        TestHandStateQueries();
        TestFactoryMethods();
        TestCharacterControllerIntegration();

        Debug.Log($"====== Spectral Hand Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats MakeWizardStats(string name, int level = 3, int intel = 18)
    {
        return new CharacterStats(name, level, "Wizard",
            8, 14, 12, intel, 10, 10,  // STR, DEX, CON, INT, WIS, CHA
            1, 0, 0,                     // BAB, armorBonus, shieldBonus
            4, 1, 0,                     // damageDice, damageCount, bonusDamage
            6, 1, 20,                    // baseSpeed, atkRange, baseHitDieHP
            "Human");
    }

    private static CharacterController MakeTestCharacter(string name, int level = 3, int intel = 18)
    {
        var go = new GameObject(name);
        var cc = go.AddComponent<CharacterController>();
        var stats = MakeWizardStats(name, level, intel);
        stats.CurrentHP = 20; // Give enough HP for testing
        cc.Init(stats, Vector2Int.zero, null, null);
        return cc;
    }

    private static void Cleanup(params CharacterController[] characters)
    {
        foreach (var c in characters)
        {
            if (c != null && c.gameObject != null)
                Object.DestroyImmediate(c.gameObject);
        }
    }

    // ===== SPELL DEFINITION TESTS =====

    private static void TestSpellDefinition()
    {
        Debug.Log("--- Spell Definition ---");
        SpellData spell = SpellDatabase.GetSpell(SpellNames.SPECTRAL_HAND);
        Assert(spell != null, "Spell definition exists");
        if (spell == null) return;

        Assert(spell.SpellLevel == 2, "Spell level is 2", $"got {spell.SpellLevel}");
        Assert(spell.School == "Necromancy", "School is Necromancy", $"got {spell.School}");
        Assert(spell.TargetType == SpellTargetType.Self, "Target type is Self");
        Assert(spell.RangeCategory == SpellRangeCategory.Medium, "Range is Medium");
        Assert(spell.EffectType == SpellEffectType.Buff, "Effect type is Buff");
        Assert(spell.IsDismissible == true, "Is dismissible");
        Assert(spell.AllowsSavingThrow == false, "No saving throw");
        Assert(spell.SpellResistanceApplies == false, "No spell resistance");
        Assert(spell.HasVerbalComponent == true, "Has verbal component");
        Assert(spell.HasSomaticComponent == true, "Has somatic component");
        Assert(spell.ActionType == SpellActionType.Standard, "Standard action");
    }

    // ===== HP LOSS ON CASTING =====

    private static void TestHPLossOnCasting()
    {
        Debug.Log("--- HP Loss on Casting ---");
        var caster = MakeTestCharacter("TestWizard", 3, 18);
        int startHP = caster.Stats.CurrentHP;

        var effect = SpectralHandEffectData.CreateWithHP(3, 3, 4, caster);
        caster.ApplySpectralHandEffect(effect);

        Assert(caster.Stats.CurrentHP == startHP - 3, "Caster loses HP equal to hand HP",
            $"expected {startHP - 3}, got {caster.Stats.CurrentHP}");
        Assert(effect.CasterHPLost == 3, "CasterHPLost tracks amount lost", $"got {effect.CasterHPLost}");

        Cleanup(caster);
    }

    // ===== HAND HP EQUALS HP LOST =====

    private static void TestHandHPEqualsHPLost()
    {
        Debug.Log("--- Hand HP Equals HP Lost ---");
        for (int hp = 1; hp <= 4; hp++)
        {
            var effect = SpectralHandEffectData.CreateWithHP(hp, 3, 4, null);
            Assert(effect.CurrentHandHP == hp, $"Hand HP = {hp} when caster lost {hp}", $"got {effect.CurrentHandHP}");
            Assert(effect.MaxHandHP == hp, $"Max hand HP = {hp}", $"got {effect.MaxHandHP}");
            Assert(effect.CasterHPLost == hp, $"CasterHPLost = {hp}", $"got {effect.CasterHPLost}");
        }
    }

    // ===== HAND AC CALCULATION =====

    private static void TestHandACCalculation()
    {
        Debug.Log("--- Hand AC Calculation ---");
        // Int 18 = +4 modifier → AC = 22 + 4 = 26
        int ac = SpectralHandEffectData.CalculateHandAC(4);
        Assert(ac == 26, "AC with Int 18 (+4) = 26", $"got {ac}");

        // Int 10 = +0 modifier → AC = 22 + 0 = 22
        ac = SpectralHandEffectData.CalculateHandAC(0);
        Assert(ac == 22, "AC with Int 10 (+0) = 22", $"got {ac}");

        // Int 16 = +3 modifier → AC = 22 + 3 = 25
        ac = SpectralHandEffectData.CalculateHandAC(3);
        Assert(ac == 25, "AC with Int 16 (+3) = 25", $"got {ac}");

        // Int 20 = +5 modifier → AC = 22 + 5 = 27
        ac = SpectralHandEffectData.CalculateHandAC(5);
        Assert(ac == 27, "AC with Int 20 (+5) = 27", $"got {ac}");
    }

    // ===== HAND AC MINIMUM 22 =====

    private static void TestHandACMinimum22()
    {
        Debug.Log("--- Hand AC Minimum 22 ---");
        // Int 8 = -1 modifier → AC should be max(22, 22 + (-1)) = 22 (not 21)
        int ac = SpectralHandEffectData.CalculateHandAC(-1);
        Assert(ac == 22, "AC with Int 8 (-1) is minimum 22", $"got {ac}");

        // Int 6 = -2 modifier → AC should be max(22, 22 + (-2)) = 22
        ac = SpectralHandEffectData.CalculateHandAC(-2);
        Assert(ac == 22, "AC with Int 6 (-2) is minimum 22", $"got {ac}");
    }

    // ===== TOUCH ATTACK BONUS =====

    private static void TestTouchAttackBonus()
    {
        Debug.Log("--- Touch Attack Bonus ---");
        var effect = SpectralHandEffectData.CreateWithHP(3, 3, 4, null);

        Assert(effect.GetTouchAttackBonus() == 2, "+2 touch attack bonus when hand active",
            $"got {effect.GetTouchAttackBonus()}");

        // After destruction
        effect.DestroyHand();
        Assert(effect.GetTouchAttackBonus() == 0, "No bonus when hand destroyed",
            $"got {effect.GetTouchAttackBonus()}");
    }

    // ===== HP RECOVERY ON NORMAL END =====

    private static void TestHPRecoveryOnNormalEnd()
    {
        Debug.Log("--- HP Recovery on Normal End ---");
        var caster = MakeTestCharacter("TestWizard", 3, 18);
        int startHP = caster.Stats.CurrentHP;

        var effect = SpectralHandEffectData.CreateWithHP(3, 3, 4, caster);
        caster.ApplySpectralHandEffect(effect);
        Assert(caster.Stats.CurrentHP == startHP - 3, "HP reduced after casting");

        // Remove effect (spell expires normally)
        caster.RemoveSpectralHandEffect();
        Assert(caster.Stats.CurrentHP == startHP, "HP restored when spell ends normally",
            $"expected {startHP}, got {caster.Stats.CurrentHP}");

        Cleanup(caster);
    }

    // ===== PERMANENT HP LOSS ON DESTRUCTION =====

    private static void TestPermanentHPLossOnDestruction()
    {
        Debug.Log("--- Permanent HP Loss on Hand Destruction ---");
        var caster = MakeTestCharacter("TestWizard", 3, 18);
        int startHP = caster.Stats.CurrentHP;

        var effect = SpectralHandEffectData.CreateWithHP(3, 3, 4, caster);
        caster.ApplySpectralHandEffect(effect);
        int hpAfterCast = caster.Stats.CurrentHP;
        Assert(hpAfterCast == startHP - 3, "HP reduced after casting");

        // Destroy the hand
        caster.DestroySpectralHand();
        Assert(caster.Stats.CurrentHP == hpAfterCast, "HP NOT restored when hand destroyed",
            $"expected {hpAfterCast}, got {caster.Stats.CurrentHP}");
        Assert(caster.ActiveSpectralHandEffect == null, "Effect cleared after destruction");

        Cleanup(caster);
    }

    // ===== HP RECOVERY ON DISMISSAL =====

    private static void TestHPRecoveryOnDismissal()
    {
        Debug.Log("--- HP Recovery on Dismissal ---");
        var caster = MakeTestCharacter("TestWizard", 3, 18);
        int startHP = caster.Stats.CurrentHP;

        var effect = SpectralHandEffectData.CreateWithHP(2, 3, 4, caster);
        caster.ApplySpectralHandEffect(effect);
        Assert(caster.Stats.CurrentHP == startHP - 2, "HP reduced after casting");

        // Dismiss = RemoveSpectralHandEffect (same as normal end — HP restored)
        caster.RemoveSpectralHandEffect();
        Assert(caster.Stats.CurrentHP == startHP, "HP restored when spell dismissed",
            $"expected {startHP}, got {caster.Stats.CurrentHP}");

        Cleanup(caster);
    }

    // ===== TOUCH SPELL DELIVERY ELIGIBILITY =====

    private static void TestTouchSpellDeliveryEligibility()
    {
        Debug.Log("--- Touch Spell Delivery Eligibility ---");
        var effect = SpectralHandEffectData.CreateWithHP(3, 3, 4, null);

        // Shocking Grasp — Level 1 touch spell — should be eligible
        SpellData shockingGrasp = SpellDatabase.GetSpell(SpellNames.SHOCKING_GRASP);
        if (shockingGrasp != null)
        {
            Assert(effect.CanDeliverSpell(shockingGrasp), "Can deliver Shocking Grasp (lvl 1 touch)");
        }

        // Ghoul Touch — Level 2 touch spell — should be eligible
        SpellData ghoulTouch = SpellDatabase.GetSpell(SpellNames.GHOUL_TOUCH);
        if (ghoulTouch != null)
        {
            Assert(effect.CanDeliverSpell(ghoulTouch), "Can deliver Ghoul Touch (lvl 2 touch)");
        }

        // Cannot deliver null spell
        Assert(!effect.CanDeliverSpell(null), "Cannot deliver null spell");
    }

    // ===== TOUCH SPELL LEVEL CAP =====

    private static void TestTouchSpellLevelCap()
    {
        Debug.Log("--- Touch Spell Level Cap ---");
        var effect = SpectralHandEffectData.CreateWithHP(3, 3, 4, null);

        // Create a mock 5th level touch spell — should NOT be deliverable
        var highLevelTouch = new SpellData
        {
            SpellId = "test_high_level_touch",
            Name = "Test High Level Touch",
            SpellLevel = 5,
            RangeCategory = SpellRangeCategory.Touch,
            IsMeleeTouch = true
        };
        Assert(!effect.CanDeliverSpell(highLevelTouch), "Cannot deliver 5th level touch spell");

        // Create a 4th level touch spell — SHOULD be deliverable
        var level4Touch = new SpellData
        {
            SpellId = "test_level4_touch",
            Name = "Test Level 4 Touch",
            SpellLevel = 4,
            RangeCategory = SpellRangeCategory.Touch,
            IsMeleeTouch = true
        };
        Assert(effect.CanDeliverSpell(level4Touch), "Can deliver 4th level touch spell");

        // Non-touch spell should NOT be deliverable
        SpellData magicMissile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
        if (magicMissile != null)
        {
            Assert(!effect.CanDeliverSpell(magicMissile), "Cannot deliver non-touch spell (Magic Missile)");
        }
    }

    // ===== HAND DAMAGE =====

    private static void TestHandDamage()
    {
        Debug.Log("--- Hand Damage ---");
        var effect = SpectralHandEffectData.CreateWithHP(3, 3, 4, null);

        // Take partial damage
        bool destroyed = effect.TakeDamage(1);
        Assert(!destroyed, "Hand not destroyed by 1 damage (3 HP)");
        Assert(effect.CurrentHandHP == 2, "Hand HP reduced to 2", $"got {effect.CurrentHandHP}");
        Assert(effect.IsHandAvailable, "Hand still available");

        // Take enough damage to destroy
        destroyed = effect.TakeDamage(2);
        Assert(destroyed, "Hand destroyed when reduced to 0 HP");
        Assert(effect.IsDestroyed, "IsDestroyed flag set");
        Assert(!effect.IsHandAvailable, "Hand no longer available");
        Assert(effect.CurrentHandHP == 0, "Hand HP is 0", $"got {effect.CurrentHandHP}");
    }

    // ===== HAND STATE QUERIES =====

    private static void TestHandStateQueries()
    {
        Debug.Log("--- Hand State Queries ---");
        var effect = SpectralHandEffectData.CreateWithHP(3, 3, 4, null);

        Assert(effect.IsActive, "IsActive true after creation");
        Assert(!effect.IsDestroyed, "Not destroyed after creation");
        Assert(effect.IsHandAvailable, "Hand available after creation");
        Assert(!effect.IsDeliveringSpell, "Not delivering spell initially");

        effect.BeginDelivery();
        Assert(effect.IsDeliveringSpell, "IsDeliveringSpell true during delivery");

        effect.EndDelivery();
        Assert(!effect.IsDeliveringSpell, "IsDeliveringSpell false after delivery");

        // End spell — HP should be returned
        int hpToRestore = effect.EndSpell("test expiry");
        Assert(hpToRestore == 3, "EndSpell returns HP to restore", $"got {hpToRestore}");
        Assert(!effect.IsActive, "Not active after EndSpell");
    }

    // ===== FACTORY METHODS =====

    private static void TestFactoryMethods()
    {
        Debug.Log("--- Factory Methods ---");

        // Test CreateWithHP
        var effect = SpectralHandEffectData.CreateWithHP(4, 5, 3, null);
        Assert(effect.CasterHPLost == 4, "CreateWithHP: CasterHPLost = 4");
        Assert(effect.CurrentHandHP == 4, "CreateWithHP: HandHP = 4");
        Assert(effect.HandAC == 25, "CreateWithHP: AC = 22+3 = 25", $"got {effect.HandAC}");
        Assert(effect.DurationRemainingRounds == 50, "CreateWithHP: Duration = 5*10 = 50 rounds",
            $"got {effect.DurationRemainingRounds}");
        Assert(effect.CasterLevel == 5, "CreateWithHP: CasterLevel = 5");
        Assert(effect.IsActive, "CreateWithHP: IsActive");

        // Test HP clamping
        var clampedEffect = SpectralHandEffectData.CreateWithHP(10, 3, 0, null);
        Assert(clampedEffect.CasterHPLost == 4, "HP clamped to max 4", $"got {clampedEffect.CasterHPLost}");

        var clampedLow = SpectralHandEffectData.CreateWithHP(0, 3, 0, null);
        Assert(clampedLow.CasterHPLost == 1, "HP clamped to min 1", $"got {clampedLow.CasterHPLost}");
    }

    // ===== CHARACTER CONTROLLER INTEGRATION =====

    private static void TestCharacterControllerIntegration()
    {
        Debug.Log("--- CharacterController Integration ---");
        var caster = MakeTestCharacter("IntegrationWizard", 5, 16);

        Assert(!caster.HasActiveSpectralHandEffect, "No spectral hand initially");
        Assert(caster.GetSpectralHandTouchAttackBonus() == 0, "No bonus without hand");

        // Apply effect
        var effect = SpectralHandEffectData.CreateWithHP(3, 5, 3, caster);
        caster.ApplySpectralHandEffect(effect);

        Assert(caster.HasActiveSpectralHandEffect, "Has spectral hand after apply");
        Assert(caster.GetSpectralHandTouchAttackBonus() == 2, "+2 bonus with active hand",
            $"got {caster.GetSpectralHandTouchAttackBonus()}");

        // Check spell delivery
        SpellData shockingGrasp = SpellDatabase.GetSpell(SpellNames.SHOCKING_GRASP);
        if (shockingGrasp != null)
        {
            Assert(caster.CanDeliverSpellThroughSpectralHand(shockingGrasp),
                "Can deliver Shocking Grasp through hand");
        }

        // Remove and verify cleanup
        caster.RemoveSpectralHandEffect();
        Assert(!caster.HasActiveSpectralHandEffect, "No spectral hand after removal");
        Assert(caster.GetSpectralHandTouchAttackBonus() == 0, "No bonus after removal");

        Cleanup(caster);
    }
}
} // namespace Tests.Combat
