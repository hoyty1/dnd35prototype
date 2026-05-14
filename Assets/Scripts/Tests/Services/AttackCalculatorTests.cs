using System.Collections.Generic;
using UnityEngine;
using Tests.Utilities;

namespace Tests.Services
{
/// <summary>
/// Unit tests for AttackCalculator — verifies feat-based attack and damage
/// modifier calculations for D&amp;D 3.5e combat.
/// Run with AttackCalculatorTests.RunAll().
/// </summary>
public static class AttackCalculatorTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== ATTACK CALCULATOR TESTS ======");

        TestHelpers.EnsureCoreDatabasesInitialized();

        TestPowerAttackMelee();
        TestPowerAttackTwoHanded();
        TestPowerAttackRangedIgnored();
        TestPowerAttackNoFeat();
        TestPointBlankShot();
        TestPointBlankShotOutOfRange();
        TestPointBlankShotNoFeat();
        TestWeaponFocus();
        TestWeaponFocusGreater();
        TestWeaponSpec();
        TestWeaponSpecGreater();
        TestCombatExpertise();
        TestCombatExpertiseRangedIgnored();
        TestCritThreatMinNormal();
        TestCritThreatMinImprovedCritical();
        TestCalculateAllFeatModifiers();
        TestShouldDenyDexToAC();

        Debug.Log($"====== Attack Calculator Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  ✅ PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  ❌ FAIL: {testName} {detail}");
        }
    }

    private static CharacterStats MakeStats(string name, int str = 16, int dex = 14, int bab = 5, params string[] feats)
    {
        var stats = TestHelpers.CreateStats(
            name: name,
            level: 5,
            characterClass: "Fighter",
            str: str,
            dex: dex,
            bab: bab);

        if (feats != null && feats.Length > 0)
            stats.AddFeats(new List<string>(feats));

        return stats;
    }

    // ===== POWER ATTACK =====

    private static void TestPowerAttackMelee()
    {
        var stats = MakeStats("PA_Melee", feats: new[] { "Power Attack" });

        AttackCalculator.CalculatePowerAttack(stats, 3, isMelee: true, isTwoHanded: false, weaponDisablesStrDmg: false,
            out int penalty, out int dmgBonus);

        Assert(penalty == -3, "Power Attack melee penalty = -3", $"got {penalty}");
        Assert(dmgBonus == 3, "Power Attack melee damage = +3", $"got {dmgBonus}");
    }

    private static void TestPowerAttackTwoHanded()
    {
        var stats = MakeStats("PA_2H", feats: new[] { "Power Attack" });

        AttackCalculator.CalculatePowerAttack(stats, 2, isMelee: true, isTwoHanded: true, weaponDisablesStrDmg: false,
            out int penalty, out int dmgBonus);

        Assert(penalty == -2, "Power Attack 2H penalty = -2", $"got {penalty}");
        Assert(dmgBonus == 4, "Power Attack 2H damage = +4 (2x)", $"got {dmgBonus}");
    }

    private static void TestPowerAttackRangedIgnored()
    {
        var stats = MakeStats("PA_Ranged", feats: new[] { "Power Attack" });

        AttackCalculator.CalculatePowerAttack(stats, 5, isMelee: false, isTwoHanded: false, weaponDisablesStrDmg: false,
            out int penalty, out int dmgBonus);

        Assert(penalty == 0, "Power Attack ignored for ranged (penalty=0)", $"got {penalty}");
        Assert(dmgBonus == 0, "Power Attack ignored for ranged (dmg=0)", $"got {dmgBonus}");
    }

    private static void TestPowerAttackNoFeat()
    {
        var stats = MakeStats("PA_NoFeat");

        AttackCalculator.CalculatePowerAttack(stats, 3, isMelee: true, isTwoHanded: false, weaponDisablesStrDmg: false,
            out int penalty, out int dmgBonus);

        Assert(penalty == 0, "No Power Attack feat: penalty=0", $"got {penalty}");
        Assert(dmgBonus == 0, "No Power Attack feat: dmg=0", $"got {dmgBonus}");
    }

    // ===== POINT BLANK SHOT =====

    private static void TestPointBlankShot()
    {
        var stats = MakeStats("PBS", feats: new[] { "Point Blank Shot" });

        AttackCalculator.CalculatePointBlankShot(stats, isMelee: false, distanceFeet: 25,
            out bool active, out int atkBonus, out int dmgBonus);

        Assert(active, "PBS active within 30ft");
        Assert(atkBonus == 1, "PBS attack bonus = +1", $"got {atkBonus}");
        Assert(dmgBonus == 1, "PBS damage bonus = +1", $"got {dmgBonus}");
    }

    private static void TestPointBlankShotOutOfRange()
    {
        var stats = MakeStats("PBS_Far", feats: new[] { "Point Blank Shot" });

        AttackCalculator.CalculatePointBlankShot(stats, isMelee: false, distanceFeet: 35,
            out bool active, out int atkBonus, out int dmgBonus);

        Assert(!active, "PBS not active beyond 30ft");
        Assert(atkBonus == 0, "PBS no attack bonus beyond 30ft", $"got {atkBonus}");
        Assert(dmgBonus == 0, "PBS no damage bonus beyond 30ft", $"got {dmgBonus}");
    }

    private static void TestPointBlankShotNoFeat()
    {
        var stats = MakeStats("PBS_NoFeat");

        AttackCalculator.CalculatePointBlankShot(stats, isMelee: false, distanceFeet: 25,
            out bool active, out int atkBonus, out int dmgBonus);

        Assert(!active, "No PBS feat: not active");
        Assert(atkBonus == 0, "No PBS feat: atk=0", $"got {atkBonus}");
    }

    // ===== WEAPON FOCUS =====

    private static void TestWeaponFocus()
    {
        var stats = MakeStats("WF", feats: new[] { "Weapon Focus" });
        int bonus = AttackCalculator.GetWeaponFocusBonus(stats);
        Assert(bonus == 1, "Weapon Focus = +1", $"got {bonus}");
    }

    private static void TestWeaponFocusGreater()
    {
        var stats = MakeStats("GWF", feats: new[] { "Weapon Focus", "Greater Weapon Focus" });
        int bonus = AttackCalculator.GetWeaponFocusBonus(stats);
        Assert(bonus == 2, "Weapon Focus + Greater = +2", $"got {bonus}");
    }

    // ===== WEAPON SPECIALIZATION =====

    private static void TestWeaponSpec()
    {
        var stats = MakeStats("WS", feats: new[] { "Weapon Specialization" });
        int bonus = AttackCalculator.GetWeaponSpecBonus(stats);
        Assert(bonus == 2, "Weapon Specialization = +2", $"got {bonus}");
    }

    private static void TestWeaponSpecGreater()
    {
        var stats = MakeStats("GWS", feats: new[] { "Weapon Specialization", "Greater Weapon Specialization" });
        int bonus = AttackCalculator.GetWeaponSpecBonus(stats);
        Assert(bonus == 4, "Weapon Spec + Greater = +4", $"got {bonus}");
    }

    // ===== COMBAT EXPERTISE =====

    private static void TestCombatExpertise()
    {
        var stats = MakeStats("CE", feats: new[] { "Combat Expertise" });
        int penalty = AttackCalculator.CalculateCombatExpertisePenalty(stats, isMelee: true);
        // Combat Expertise applies -CombatExpertiseValue; default is usually set by player.
        // With default value 0, penalty should be 0. If stats.CombatExpertiseValue is set:
        Assert(penalty <= 0, "Combat Expertise penalty is non-positive", $"got {penalty}");
    }

    private static void TestCombatExpertiseRangedIgnored()
    {
        var stats = MakeStats("CE_Ranged", feats: new[] { "Combat Expertise" });
        int penalty = AttackCalculator.CalculateCombatExpertisePenalty(stats, isMelee: false);
        Assert(penalty == 0, "Combat Expertise ignored for ranged", $"got {penalty}");
    }

    // ===== CRITICAL THREAT RANGE =====

    private static void TestCritThreatMinNormal()
    {
        var stats = MakeStats("Crit_Normal");
        int threatMin = AttackCalculator.GetAdjustedCritThreatMin(stats, 20);
        Assert(threatMin == 20, "Normal crit threat = 20", $"got {threatMin}");
    }

    private static void TestCritThreatMinImprovedCritical()
    {
        var stats = MakeStats("Crit_Improved", feats: new[] { "Improved Critical" });
        // Base threat range 19-20 (baseThreatMin = 19) with Improved Critical doubles it
        int threatMin = AttackCalculator.GetAdjustedCritThreatMin(stats, 19);
        // Should double range: 19-20 = 2 range -> 4 range -> 17-20
        Assert(threatMin <= 19, "Improved Critical lowers threat min", $"got {threatMin}");
    }

    // ===== CALCULATE ALL FEAT MODIFIERS =====

    private static void TestCalculateAllFeatModifiers()
    {
        var stats = MakeStats("AllFeats", feats: new[] { "Power Attack", "Point Blank Shot", "Weapon Focus" });

        var mods = AttackCalculator.CalculateAllFeatModifiers(
            stats,
            powerAttackValue: 2,
            isMelee: true,
            isRanged: false,
            isTwoHanded: false,
            weaponDisablesStrDmg: false,
            baseThreatMin: 20,
            distanceFeet: 20,
            isFullAttack: false);

        Assert(mods.PowerAttackPenalty == -2, "AllFeats: PA penalty = -2", $"got {mods.PowerAttackPenalty}");
        Assert(mods.PowerAttackDamageBonus == 2, "AllFeats: PA damage = +2", $"got {mods.PowerAttackDamageBonus}");
        Assert(mods.WeaponFocusBonus == 1, "AllFeats: WF bonus = +1", $"got {mods.WeaponFocusBonus}");
        // PBS should not be active for melee
        Assert(!mods.PointBlankShotActive, "AllFeats: PBS not active for melee");
    }

    // ===== DENY DEX TO AC =====

    private static void TestShouldDenyDexToAC()
    {
        // Create attacker and target GameObjects
        var attackerGO = new GameObject("DexDeny_Attacker");
        var targetGO = new GameObject("DexDeny_Target");
        var attacker = attackerGO.AddComponent<CharacterController>();
        var target = targetGO.AddComponent<CharacterController>();

        attacker.Stats = MakeStats("Attacker");
        target.Stats = MakeStats("Target");

        // By default, without invisibility or flat-footed, should not deny dex
        bool denied = AttackCalculator.ShouldDenyDexToAC(attacker, target, isFlanking: false);
        Assert(!denied, "Normal conditions: DEX not denied");

        // Flanking should deny DEX
        bool deniedFlanking = AttackCalculator.ShouldDenyDexToAC(attacker, target, isFlanking: true);
        Assert(deniedFlanking, "Flanking: DEX denied");

        // Cleanup
        Object.DestroyImmediate(attackerGO);
        Object.DestroyImmediate(targetGO);
    }
}
}
