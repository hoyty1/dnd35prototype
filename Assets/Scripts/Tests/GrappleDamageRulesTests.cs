using System.Reflection;
using UnityEngine;

/// <summary>
/// Tests for D&D 3.5 grapple damage behavior:
/// opposed grapple check, unarmed damage, lethal/nonlethal defaults, and monk exception.
/// Run via GrappleDamageRulesTests.RunAll() from a runtime test hook.
/// </summary>
public static class GrappleDamageRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        RaceDatabase.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();

        Debug.Log("========== GRAPPLE DAMAGE RULES TESTS ==========");

        TestNonMonkDefaultGrappleDamageIsNonlethal();
        TestNonMonkLethalGrappleAppliesMinus4ToGrappleCheck();
        TestImprovedUnarmedStrikeDefaultGrappleDamageIsLethalWithoutPenalty();
        TestImprovedUnarmedStrikeNonlethalChoiceHasNoPenalty();
        TestMonkDefaultGrappleDamageIsLethalWithoutPenalty();
        TestMonkNonlethalChoiceHasNoPenalty();
        TestMoveWhileGrapplingWithoutPinnedBonusByDefault();
        TestMoveWhileGrapplingPinnedBonusAppliedInOneVsOne();
        TestPinOpponentAppliesPinnedCondition();
        TestPinnedIsNotHelpless();
        TestPinnedAcPenaltyAppliesOnlyVsNonGrappler();
        TestPinExpiresAtMaintainerEndOfNextTurnWithoutMaintenance();
        TestMaintainPinExtendsDurationAcrossTurns();
        TestGrappleDamageUsesUnarmedStrikeDamageEvenWithWeaponEquipped();
        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    private static void Assert(bool condition, string testName)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}");
        }
    }

    private static CharacterController CreateTestCharacter(string name, string className = "Fighter")
    {
        var go = new GameObject($"{name}_GO");
        var controller = go.AddComponent<CharacterController>();
        var inventory = go.AddComponent<InventoryComponent>();
        var stats = new CharacterStats(name, 3, className, 18, 12, 12, 10, 10, 10, 3, 0, 0, 8, 1, 0, 8, 1, 18);

        controller.Init(stats, Vector2Int.zero, null, null);
        inventory.Init(stats);

        // Strong deterministic gap so opposed grapple checks reliably succeed in tests.
        controller.Stats.BaseAttackBonus = 12;
        controller.Stats.STR = 26;

        return controller;
    }

    private static CharacterController CreateWeakDefender(string name)
    {
        var defender = CreateTestCharacter(name);
        defender.Stats.BaseAttackBonus = 0;
        defender.Stats.STR = 6;
        return defender;
    }

    private static void ForceGrappleState(CharacterController attacker, CharacterController defender)
    {
        MethodInfo establishMethod = typeof(CharacterController).GetMethod("EstablishGrappleWith", BindingFlags.Instance | BindingFlags.NonPublic);
        establishMethod.Invoke(attacker, new object[] { defender });
    }

    private static void ConfigureVeryStrongGrappler(CharacterController controller)
    {
        if (controller == null || controller.Stats == null)
            return;

        controller.Stats.BaseAttackBonus = 20;
        controller.Stats.STR = 30;
    }

    private static void ConfigureVeryWeakGrappler(CharacterController controller)
    {
        if (controller == null || controller.Stats == null)
            return;

        controller.Stats.BaseAttackBonus = 0;
        controller.Stats.STR = 6;
    }

    private static void Cleanup(params CharacterController[] controllers)
    {
        if (controllers == null)
            return;

        foreach (var controller in controllers)
        {
            if (controller != null)
                Object.DestroyImmediate(controller.gameObject);
        }
    }

    private static void TestNonMonkDefaultGrappleDamageIsNonlethal()
    {
        var attacker = CreateTestCharacter("GrappleDefaultNonMonk", "Fighter");
        var defender = CreateWeakDefender("GrappleDefaultNonMonkTarget");

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.DamageOpponent);

        Assert(result != null && result.Success, "Non-monk default grapple damage action succeeds with favorable stats");
        Assert(result != null && result.CheckTotal == result.CheckRoll + attacker.GetGrappleModifier(), "Non-monk default grapple damage uses full grapple modifier without -4 penalty");
        Assert(result != null && result.Log.Contains("nonlethal"), "Non-monk default grapple damage is logged as nonlethal");

        Cleanup(attacker, defender);
    }

    private static void TestNonMonkLethalGrappleAppliesMinus4ToGrappleCheck()
    {
        var attacker = CreateTestCharacter("GrappleLethalPenalty", "Fighter");
        var defender = CreateWeakDefender("GrappleLethalPenaltyTarget");

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.DamageOpponent, AttackDamageMode.Lethal);

        Assert(result != null && result.Success, "Non-monk lethal grapple damage action succeeds with favorable stats");
        if (result != null)
        {
            int expectedTotal = result.CheckRoll + attacker.GetGrappleModifier() - 4;
            Assert(result.CheckTotal == expectedTotal, "Non-monk lethal grapple damage applies -4 to opposed grapple check total");
        }
        else
        {
            Assert(false, "Non-monk lethal grapple damage applies -4 to opposed grapple check total");
        }
        Assert(result != null && result.Log.Contains("lethal"), "Non-monk lethal grapple damage is logged as lethal");
        Assert(result != null && result.Log.Contains("-4"), "Combat log includes the lethal grapple check penalty for non-monks");

        Cleanup(attacker, defender);
    }
    private static void TestImprovedUnarmedStrikeDefaultGrappleDamageIsLethalWithoutPenalty()
    {
        var attacker = CreateTestCharacter("GrappleIusDefault", "Fighter");
        var defender = CreateWeakDefender("GrappleIusDefaultTarget");
        attacker.Stats.Feats.Add("Improved Unarmed Strike");

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.DamageOpponent);

        Assert(result != null && result.Success, "Improved Unarmed Strike default grapple damage action succeeds with favorable stats");
        Assert(result != null && result.CheckTotal == result.CheckRoll + attacker.GetGrappleModifier(), "Improved Unarmed Strike default lethal grapple damage has no -4 penalty");
        Assert(result != null && result.Log.Contains("lethal"), "Improved Unarmed Strike default grapple damage is logged as lethal");
        Assert(result != null && result.Log.Contains("Deals lethal damage by default (Improved Unarmed Strike feat)"), "Combat log explains lethal default from Improved Unarmed Strike feat");

        Cleanup(attacker, defender);
    }

    private static void TestImprovedUnarmedStrikeNonlethalChoiceHasNoPenalty()
    {
        var attacker = CreateTestCharacter("GrappleIusNonlethal", "Fighter");
        var defender = CreateWeakDefender("GrappleIusNonlethalTarget");
        attacker.Stats.Feats.Add("Improved Unarmed Strike");

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.DamageOpponent, AttackDamageMode.Nonlethal);

        Assert(result != null && result.Success, "Improved Unarmed Strike nonlethal grapple damage action succeeds with favorable stats");
        Assert(result != null && result.CheckTotal == result.CheckRoll + attacker.GetGrappleModifier(), "Improved Unarmed Strike nonlethal grapple damage has no -4 penalty");
        Assert(result != null && result.Log.Contains("nonlethal"), "Improved Unarmed Strike nonlethal grapple damage is logged as nonlethal");
        Assert(result != null && result.Log.Contains("No penalty (Improved Unarmed Strike feat)"), "Combat log explains no-penalty nonlethal choice from Improved Unarmed Strike feat");

        Cleanup(attacker, defender);
    }

    private static void TestMonkDefaultGrappleDamageIsLethalWithoutPenalty()
    {
        var attacker = CreateTestCharacter("GrappleMonkDefault", "Monk");
        var defender = CreateWeakDefender("GrappleMonkDefaultTarget");

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.DamageOpponent);

        Assert(result != null && result.Success, "Monk default grapple damage action succeeds with favorable stats");
        Assert(result != null && result.CheckTotal == result.CheckRoll + attacker.GetGrappleModifier(), "Monk default lethal grapple damage has no -4 penalty");
        Assert(result != null && result.Log.Contains("lethal"), "Monk default grapple damage is logged as lethal");

        Cleanup(attacker, defender);
    }

    private static void TestMonkNonlethalChoiceHasNoPenalty()
    {
        var attacker = CreateTestCharacter("GrappleMonkNonlethal", "Monk");
        var defender = CreateWeakDefender("GrappleMonkNonlethalTarget");

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.DamageOpponent, AttackDamageMode.Nonlethal);

        Assert(result != null && result.Success, "Monk nonlethal grapple damage action succeeds with favorable stats");
        Assert(result != null && result.CheckTotal == result.CheckRoll + attacker.GetGrappleModifier(), "Monk nonlethal grapple damage has no -4 penalty");
        Assert(result != null && result.Log.Contains("nonlethal"), "Monk nonlethal grapple damage is logged as nonlethal");

        Cleanup(attacker, defender);
    }

    private static void TestMoveWhileGrapplingWithoutPinnedBonusByDefault()
    {
        var attacker = CreateTestCharacter("GrappleMoveNoPinnedBonus", "Fighter");
        var defender = CreateWeakDefender("GrappleMoveNoPinnedBonusTarget");

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.MoveHalfSpeed);

        Assert(result != null, "Move while grappling returns a result");
        Assert(result != null && result.CheckTotal == result.CheckRoll + attacker.GetGrappleModifier(), "Move while grappling uses normal grapple modifier when no pinned opponent bonus applies");
        Assert(result != null && !result.Log.Contains("gains +4"), "Move while grappling log does not report pinned bonus when not moving a pinned opponent");

        Cleanup(attacker, defender);
    }

    private static void TestMoveWhileGrapplingPinnedBonusAppliedInOneVsOne()
    {
        var attacker = CreateTestCharacter("GrappleMovePinnedBonus", "Fighter");
        var defender = CreateWeakDefender("GrappleMovePinnedBonusTarget");

        ForceGrappleState(attacker, defender);
        defender.ApplyCondition(CombatConditionType.Pinned, -1, attacker.Stats.CharacterName);

        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.MoveHalfSpeed);

        Assert(result != null, "Move while grappling with pinned opponent returns a result");
        Assert(result != null && result.CheckTotal == result.CheckRoll + attacker.GetGrappleModifier() + 4, "Move while grappling gains +4 in 1v1 when moving a pinned opponent");
        Assert(result != null && result.Log.Contains("gains +4"), "Move while grappling log reports the pinned-opponent +4 bonus");

        Cleanup(attacker, defender);
    }


    private static void TestPinOpponentAppliesPinnedCondition()
    {
        var attacker = CreateTestCharacter("GrapplePinApplies", "Fighter");
        var defender = CreateWeakDefender("GrapplePinAppliesTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);

        Assert(result != null && result.Success, "Pin opponent succeeds with strong grappler advantage");
        Assert(defender.HasCondition(CombatConditionType.Pinned), "Successful pin applies pinned condition to defender");

        Cleanup(attacker, defender);
    }
    private static void TestPinnedIsNotHelpless()
    {
        var attacker = CreateTestCharacter("GrapplePinNotHelpless", "Fighter");
        var defender = CreateWeakDefender("GrapplePinNotHelplessTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);

        Assert(result != null && result.Success, "Pin succeeds before pinned-vs-helpless validation");
        Assert(defender.HasCondition(CombatConditionType.Pinned), "Pinned condition is present after successful pin");
        Assert(!defender.HasCondition(CombatConditionType.Helpless), "Pinned condition does not apply helpless status");

        Cleanup(attacker, defender);
    }

    private static void TestPinnedAcPenaltyAppliesOnlyVsNonGrappler()
    {
        var attacker = CreateTestCharacter("GrapplePinAcMaintainer", "Fighter");
        var defender = CreateWeakDefender("GrapplePinAcTarget");
        var bystander = CreateTestCharacter("GrapplePinAcBystander", "Fighter");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);

        Assert(result != null && result.Success, "Pin succeeds before AC-penalty split validation");

        MethodInfo getSituationalAc = typeof(CharacterController).GetMethod(
            "GetSituationalTargetArmorClass",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert(getSituationalAc != null, "Internal AC helper is available for pinned AC split test");

        if (getSituationalAc != null)
        {
            int acVsMaintainer = (int)getSituationalAc.Invoke(null, new object[] { defender, attacker, false });
            int acVsBystander = (int)getSituationalAc.Invoke(null, new object[] { defender, bystander, false });
            Assert(acVsBystander == acVsMaintainer - 4, "Pinned defender takes -4 AC only vs non-grappling attackers");
        }

        Cleanup(attacker, defender, bystander);
    }


    private static void TestPinExpiresAtMaintainerEndOfNextTurnWithoutMaintenance()
    {
        var attacker = CreateTestCharacter("GrapplePinExpires", "Fighter");
        var defender = CreateWeakDefender("GrapplePinExpiresTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult pinResult = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(pinResult != null && pinResult.Success, "Initial pin succeeds before expiry test");
        Assert(defender.HasCondition(CombatConditionType.Pinned), "Defender is pinned immediately after successful pin");

        attacker.ProcessPinnedDurationAtTurnEnd();
        Assert(defender.HasCondition(CombatConditionType.Pinned), "Pin does not expire at end of the same turn it was applied");

        attacker.StartNewTurn();
        attacker.ProcessPinnedDurationAtTurnEnd();
        Assert(!defender.HasCondition(CombatConditionType.Pinned), "Pin expires at end of maintainer's next turn if not maintained");

        Cleanup(attacker, defender);
    }

    private static void TestMaintainPinExtendsDurationAcrossTurns()
    {
        var attacker = CreateTestCharacter("GrappleMaintainPin", "Fighter");
        var defender = CreateWeakDefender("GrappleMaintainPinTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult initialPin = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(initialPin != null && initialPin.Success, "Initial pin succeeds before maintenance test");
        Assert(defender.HasCondition(CombatConditionType.Pinned), "Defender is pinned before maintenance attempt");

        attacker.StartNewTurn();
        SpecialAttackResult maintainResult = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(maintainResult != null && maintainResult.Success, "Maintain pin action succeeds on subsequent turn");
        Assert(defender.HasCondition(CombatConditionType.Pinned), "Defender remains pinned after successful maintenance");

        attacker.ProcessPinnedDurationAtTurnEnd();
        Assert(defender.HasCondition(CombatConditionType.Pinned), "Maintained pin persists through current turn end");

        attacker.StartNewTurn();
        attacker.ProcessPinnedDurationAtTurnEnd();
        Assert(!defender.HasCondition(CombatConditionType.Pinned), "Maintained pin eventually expires when not maintained again");

        Cleanup(attacker, defender);
    }
    private static void TestGrappleDamageUsesUnarmedStrikeDamageEvenWithWeaponEquipped()
    {
        var attacker = CreateTestCharacter("GrappleUnarmedDice", "Fighter");
        var defender = CreateWeakDefender("GrappleUnarmedDiceTarget");

        // Equip a larger-die weapon to verify grapple damage still uses unarmed strike damage.
        var inv = attacker.GetComponent<InventoryComponent>();
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("greatsword"), EquipSlot.RightHand);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.DamageOpponent, AttackDamageMode.Nonlethal);

        Assert(result != null && result.Success, "Grapple damage succeeds even while a regular weapon is equipped");
        Assert(result != null && result.Log.Contains("unarmed grapple damage"), "Grapple damage log identifies unarmed grapple damage");
        Assert(result != null && result.Log.Contains("rolled 1d3"), "Grapple damage roll uses unarmed damage dice (1d3 for Medium) instead of weapon dice");

        Cleanup(attacker, defender);
    }
}