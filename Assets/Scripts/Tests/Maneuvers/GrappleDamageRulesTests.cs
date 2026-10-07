using System.Reflection;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Maneuvers
{
/// <summary>
/// Tests for D&D 3.5 grapple damage behavior:
/// opposed grapple check, unarmed damage, lethal/nonlethal defaults, and monk exception;
/// plus the shared opposed-maneuver math for trip, bull rush, overrun, disarm and the grapple
/// touch attack (PHB p.154-158, CMB-014).
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
        SpellDatabase.Init();

        Debug.Log("========== GRAPPLE DAMAGE RULES TESTS ==========");

        TestNonMonkDefaultGrappleDamageIsNonlethal();
        TestNonMonkLethalGrappleAppliesMinus4ToGrappleCheck();
        TestImprovedUnarmedStrikeDefaultGrappleDamageIsLethalWithoutPenalty();
        TestImprovedUnarmedStrikeNonlethalChoiceHasNoPenalty();
        TestMonkDefaultGrappleDamageIsLethalWithoutPenalty();
        TestMonkNonlethalChoiceHasNoPenalty();
        TestMoveWhileGrapplingWithoutPinnedBonusByDefault();
        TestMoveWhileGrapplingPinnedBonusAppliedInOneVsOne();
        TestGrappledConditionDoesNotZeroBaseSpeed();
        TestPinOpponentAppliesPinnedCondition();
        TestPinnedIsNotHelpless();
        TestPinnedAcPenaltyAppliesOnlyVsNonGrappler();
        TestPinExpiresAtMaintainerEndOfNextTurnWithoutMaintenance();
        TestMaintainPinExtendsDurationAcrossTurns();
        TestGrappleDamageUsesUnarmedStrikeDamageEvenWithWeaponEquipped();
        TestUseOpponentWeaponFailsWhenOpponentHasNoLightWeapon();
        TestUseOpponentWeaponUsesSelectedRightHandLightWeaponWithoutTransfer();
        TestUseOpponentWeaponCanSelectLeftHandLightWeapon();
        TestIsPinningOpponentHelperTracksMaintainer();
        TestPinnerBlockedActionsReturnExpectedMessages();
        TestEscapeFromPinMaintainsGrapple_EscapeArtist();
        TestEscapeFromPinMaintainsGrapple_OpposedEscape();
        TestReleasePinnedOpponentEndsEntireGrapple();
        TestReleasePinnedOpponentIsFreeAndGrantsNoStep();
        TestSilentAndStillMetamagicRemoveVerbalAndSomaticComponents();
        TestIterativeGrappleAttackBonusesConsumeInOrder();
        TestOpposedEscapeCountsAsIterativeGrappleAttackAction();
        TestStandardOnlyAllowsSingleIterativeGrappleAttack();
        TestImprovedGrabCreatureCanUseStandardGrappleAction();
        TestImprovedGrabCreatureCanStillUseIterativeGrappleActionsWhenAlreadyGrappling();
        TestIterativeDisarmAttackBonusesConsumeInOrder();
        TestStandardOnlyAllowsSingleIterativeDisarmAttack();
        TestAttackSequenceFirstStepSpendsOnlyStandardAction();
        TestAttackSequenceSecondStepRefusedAfterMoveAction();
        TestAttackSequenceFiveFootStepAllowsFullAttack();
        TestAttackSequenceOffHandThenMainHandEntersFullAttack();
        TestAttackSequenceIsPerCreature();
        TestAttackSequencePayingTwiceSpendsOnlyStandardAction();
        TestAttackSequenceClearedByStartNewTurn();
        TestAttackSequenceSingleActionOnlyAllowsOneStep();
        TestAttackSequenceSlowedCreatureGetsOneStep();
        TestAttackSequenceSingleAttackThenAttackUsesNextStep();
        TestAttackSequenceNaturalAttackThenTripRefusedWhenNaturalBudgetSpent();
        TestAttackSequenceTripReplacesOneNaturalAttack();
        TestAttackSequenceStepResolverUsesStepBab();
        TestManeuverActionCostTable();
        TestBullRushIsAStandardAction();
        TestBullRushTargetLimits();
        TestNpcManeuverCostsOneAttackStep();
        TestNpcMeleeSequenceTripThenAttacks();
        TestBullRushChargeAppliesPlus2ToAttackerCheck();
        TestBullRushImprovedFeatAddsPlus4();
        TestBullRushDefenderUsesStrengthAndDwarfStability();
        TestBullRushCheckIgnoresBaseAttackBonus();
        TestSpecialSizeModifierScale();
        TestTripAttackerModifierIsStrengthCheck();
        TestTripDefenderUsesBestOfStrDexWithoutImprovedTrip();
        TestOverrunCheckModifiers();
        TestManeuverTouchAttackNaturalTwentyAndOne();
        TestOpposedCheckTieBreaks();
        TestFreeTripSkipsTouchAttack();
        TestTripAttemptRollsTouchAttack();
        TestDefenderImprovedDisarmGivesNoBonus();
        TestImprovedDisarmDeniesCounterDisarm();
        TestGrappleHoldFailsAgainstMuchLargerTarget();
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
    private static void TestGrappledConditionDoesNotZeroBaseSpeed()
    {
        var attacker = CreateTestCharacter("GrappleSpeedSource", "Fighter");
        var defender = CreateWeakDefender("GrappleSpeedTarget");

        int expectedSpeedFeet = attacker.Stats.EffectiveSpeedFeet;
        int expectedMoveRange = attacker.Stats.MoveRange;

        ForceGrappleState(attacker, defender);

        Assert(attacker.HasCondition(CombatConditionType.Grappled), "Attacker has grappled condition after grapple is established");
        Assert(attacker.Stats.EffectiveSpeedFeet == expectedSpeedFeet,
            "Grappled condition does not reduce base speed to 0");
        Assert(attacker.Stats.MoveRange == expectedMoveRange,
            "Grappled condition preserves normal move range for half-speed grapple move math");

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
        inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.GREATSWORD), EquipSlot.RightHand);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.DamageOpponent, AttackDamageMode.Nonlethal);

        Assert(result != null && result.Success, "Grapple damage succeeds even while a regular weapon is equipped");
        Assert(result != null && result.Log.Contains("unarmed grapple damage"), "Grapple damage log identifies unarmed grapple damage");
        Assert(result != null && result.Log.Contains("rolled 1d3"), "Grapple damage roll uses unarmed damage dice (1d3 for Medium) instead of weapon dice");

        Cleanup(attacker, defender);
    }

    private static void TestUseOpponentWeaponFailsWhenOpponentHasNoLightWeapon()
    {
        var attacker = CreateTestCharacter("GrappleUseOppWeaponNoLight", "Fighter");
        var defender = CreateWeakDefender("GrappleUseOppWeaponNoLightTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        var defenderInv = defender.GetComponent<InventoryComponent>();
        defenderInv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.GREATSWORD), EquipSlot.RightHand);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.UseOpponentWeapon);

        Assert(result != null && !result.Success, "Use Opponent's Weapon fails when defender has no equipped light weapon");
        Assert(result != null && result.Log.Contains("no equipped light weapon"), "Use Opponent's Weapon failure log explains missing light weapon requirement");

        Cleanup(attacker, defender);
    }

    private static void TestUseOpponentWeaponUsesSelectedRightHandLightWeaponWithoutTransfer()
    {
        var attacker = CreateTestCharacter("GrappleUseOppWeaponRight", "Fighter");
        var defender = CreateWeakDefender("GrappleUseOppWeaponRightTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        var defenderInv = defender.GetComponent<InventoryComponent>();
        ItemData defenderDagger = ItemDatabase.CloneItem(ItemIDs.DAGGER);
        defenderInv.CharacterInventory.DirectEquip(defenderDagger, EquipSlot.RightHand);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.UseOpponentWeapon, null, EquipSlot.RightHand);

        Assert(result != null && result.Success, "Use Opponent's Weapon succeeds with favorable opposed grapple modifier gap");
        Assert(result != null && result.Log.Contains("-4 penalty"), "Use Opponent's Weapon combat log includes the fixed -4 attack penalty");
        Assert(result != null && result.Log.Contains(ItemIDs.DAGGER), "Use Opponent's Weapon log names the selected opponent weapon");

        ItemData equippedAfter = defenderInv.CharacterInventory.RightHandSlot;
        Assert(equippedAfter != null && equippedAfter.Name == defenderDagger.Name, "Use Opponent's Weapon does not transfer or remove defender's weapon");

        Cleanup(attacker, defender);
    }

    private static void TestUseOpponentWeaponCanSelectLeftHandLightWeapon()
    {
        var attacker = CreateTestCharacter("GrappleUseOppWeaponLeft", "Fighter");
        var defender = CreateWeakDefender("GrappleUseOppWeaponLeftTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        var defenderInv = defender.GetComponent<InventoryComponent>();
        defenderInv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.DAGGER), EquipSlot.RightHand);
        ItemData leftWeapon = ItemDatabase.CloneItem(ItemIDs.SICKLE);
        defenderInv.CharacterInventory.DirectEquip(leftWeapon, EquipSlot.LeftHand);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult result = attacker.ResolveGrappleAction(GrappleActionType.UseOpponentWeapon, null, EquipSlot.LeftHand);

        Assert(result != null && result.Success, "Use Opponent's Weapon supports selecting the left-hand light weapon");
        Assert(result != null && result.Log.Contains("LeftHand") && result.Log.Contains(leftWeapon.Name), "Use Opponent's Weapon log confirms left-hand weapon selection");

        Cleanup(attacker, defender);
    }

    private static void TestIsPinningOpponentHelperTracksMaintainer()
    {
        var attacker = CreateTestCharacter("GrappleIsPinningMaintainer", "Fighter");
        var defender = CreateWeakDefender("GrappleIsPinningMaintainerTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult pinResult = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);

        Assert(pinResult != null && pinResult.Success, "Pin succeeds before helper validation");
        Assert(attacker.IsPinningOpponent(), "Pin maintainer is reported as actively pinning an opponent");
        Assert(!defender.IsPinningOpponent(), "Pinned defender is not reported as pinning an opponent");

        Cleanup(attacker, defender);
    }

    private static void TestPinnerBlockedActionsReturnExpectedMessages()
    {
        var attacker = CreateTestCharacter("GrapplePinnerBlockedActions", "Fighter");
        var defender = CreateWeakDefender("GrapplePinnerBlockedActionsTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult pinResult = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(pinResult != null && pinResult.Success, "Pin succeeds before blocked-action validation");

        SpecialAttackResult drawResult = attacker.ResolveGrappleAction(GrappleActionType.DrawLightWeapon);
        Assert(drawResult != null && !drawResult.Success, "Pinner cannot draw a weapon while maintaining a pin");
        Assert(drawResult != null && drawResult.Log.Contains("Cannot draw weapon while pinning"), "Draw weapon block message is explicit while pinning");

        SpecialAttackResult retrieveResult = attacker.ResolveGrappleAction(GrappleActionType.RetrieveSpellComponent);
        Assert(retrieveResult != null && !retrieveResult.Success, "Pinner cannot retrieve a spell component while maintaining a pin");
        Assert(retrieveResult != null && retrieveResult.Log.Contains("Cannot retrieve component while pinning"), "Retrieve component block message is explicit while pinning");

        SpecialAttackResult breakPinResult = attacker.ResolveGrappleAction(GrappleActionType.BreakPin);
        Assert(breakPinResult != null && !breakPinResult.Success, "Pinner cannot break another person's pin while pinning");
        Assert(breakPinResult != null && breakPinResult.Log.Contains("Cannot break pin while pinning"), "Break pin block message is explicit while pinning");

        SpecialAttackResult escapeResult = attacker.ResolveGrappleAction(GrappleActionType.EscapeArtist);
        Assert(escapeResult != null && !escapeResult.Success, "Pinner cannot escape while maintaining a pin");
        Assert(escapeResult != null && escapeResult.Log.Contains("Cannot escape while pinning"), "Escape block message is explicit while pinning");

        Cleanup(attacker, defender);
    }

    private static void TestEscapeFromPinMaintainsGrapple_EscapeArtist()
    {
        var pinner = CreateTestCharacter("GrappleEscapePinEA_Pinner", "Fighter");
        var pinned = CreateWeakDefender("GrappleEscapePinEA_Pinned");
        ConfigureVeryStrongGrappler(pinner);
        ConfigureVeryWeakGrappler(pinned);

        ForceGrappleState(pinner, pinned);
        SpecialAttackResult pinResult = pinner.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(pinResult != null && pinResult.Success, "Pin succeeds before Escape Artist pin-break validation");

        // Ensure deterministic success for the pinned character's Escape Artist check.
        pinned.Stats.InitializeSkills(pinned.Stats.CharacterClass, pinned.Stats.Level);
        pinned.Stats.Skills["Escape Artist"].Ranks = 40;
        SpecialAttackResult escapeResult = pinned.ResolveGrappleAction(GrappleActionType.EscapeArtist);

        Assert(escapeResult != null && escapeResult.Success, "Pinned character succeeds Escape Artist check");
        Assert(!pinned.HasCondition(CombatConditionType.Pinned), "Pinned condition is removed after successful Escape Artist from pin");
        Assert(!pinner.IsPinningOpponent(), "Pinner no longer holds the pin after successful Escape Artist from pin");
        Assert(pinned.IsGrappling(), "Escaping a pin with Escape Artist maintains grapple state for the pinned character");
        Assert(pinner.IsGrappling(), "Escaping a pin with Escape Artist maintains grapple state for the former pinner");

        Cleanup(pinner, pinned);
    }

    private static void TestEscapeFromPinMaintainsGrapple_OpposedEscape()
    {
        var pinner = CreateTestCharacter("GrappleEscapePinOpposed_Pinner", "Fighter");
        var pinned = CreateWeakDefender("GrappleEscapePinOpposed_Pinned");
        ConfigureVeryStrongGrappler(pinner);
        ConfigureVeryWeakGrappler(pinned);

        ForceGrappleState(pinner, pinned);
        SpecialAttackResult pinResult = pinner.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(pinResult != null && pinResult.Success, "Pin succeeds before opposed pin-break validation");

        // Guarantee the pinned character wins the opposed grapple escape.
        ConfigureVeryStrongGrappler(pinned);
        ConfigureVeryWeakGrappler(pinner);
        SpecialAttackResult escapeResult = pinned.ResolveGrappleAction(GrappleActionType.OpposedGrappleEscape);

        Assert(escapeResult != null && escapeResult.Success, "Pinned character succeeds opposed grapple escape check");
        Assert(!pinned.HasCondition(CombatConditionType.Pinned), "Pinned condition is removed after successful opposed escape from pin");
        Assert(!pinner.IsPinningOpponent(), "Pinner no longer holds the pin after successful opposed escape from pin");
        Assert(pinned.IsGrappling(), "Escaping a pin with opposed grapple check maintains grapple state for the pinned character");
        Assert(pinner.IsGrappling(), "Escaping a pin with opposed grapple check maintains grapple state for the former pinner");

        Cleanup(pinner, pinned);
    }
    private static void TestReleasePinnedOpponentEndsEntireGrapple()
    {
        var attacker = CreateTestCharacter("GrappleReleasePin", "Fighter");
        var defender = CreateWeakDefender("GrappleReleasePinTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);
        // Stats.BaseAttackBonus is ignored for classed characters (CHR-068); pin the gap so the pin cannot fail.
        attacker.Stats.BaseAttackBonusOverride = 20;
        defender.Stats.BaseAttackBonusOverride = 0;

        ForceGrappleState(attacker, defender);
        SpecialAttackResult pinResult = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(pinResult != null && pinResult.Success, "Pin succeeds before release validation");

        SpecialAttackResult releaseResult = attacker.ResolveGrappleAction(GrappleActionType.ReleasePinnedOpponent);
        Assert(releaseResult != null && releaseResult.Success, "Release pinned opponent action succeeds for pin maintainer");
        Assert(releaseResult != null && releaseResult.Log.Contains("releases") && releaseResult.Log.Contains("ending the grapple"), "Release action combat log describes releasing the pin and ending the grapple");
        Assert(!attacker.HasCondition(CombatConditionType.Grappled), "Pin maintainer is no longer grappled after release action");
        Assert(!defender.HasCondition(CombatConditionType.Grappled), "Released opponent is no longer grappled after release action");
        Assert(!defender.HasCondition(CombatConditionType.Pinned), "Released opponent is no longer pinned after release action");
        Assert(!attacker.IsGrappling() && !defender.IsGrappling(), "Neither creature is in a grapple link after the pin is released (PHB p.157, CMB-089)");
        Assert(!attacker.IsPinningOpponent() && defender.GetPinnedBy() == null, "Release clears the pinner and pinned-by state on both creatures");

        Cleanup(attacker, defender);
    }

    private static void TestReleasePinnedOpponentIsFreeAndGrantsNoStep()
    {
        var attacker = CreateTestCharacter("GrappleReleasePinFree", "Fighter");
        var defender = CreateWeakDefender("GrappleReleasePinFreeTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);
        // Stats.BaseAttackBonus is ignored for classed characters (CHR-068); pin the gap so the pin cannot fail.
        attacker.Stats.BaseAttackBonusOverride = 20;
        defender.Stats.BaseAttackBonusOverride = 0;

        ForceGrappleState(attacker, defender);
        SpecialAttackResult pinResult = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(pinResult != null && pinResult.Success, "Pin succeeds before free-release validation");

        attacker.StartNewTurn();
        SpecialAttackResult releaseResult = attacker.ResolveGrappleAction(GrappleActionType.ReleasePinnedOpponent);
        Assert(releaseResult != null && releaseResult.Success, "Release succeeds at the start of the pinner's turn");
        Assert(CharacterController.IsFreeGrappleAction(GrappleActionType.ReleasePinnedOpponent),
            "Releasing a pin is a free action in the shared PC/AI action-cost helper (PHB p.157)");
        Assert(!CharacterController.IsFreeGrappleAction(GrappleActionType.PinOpponent)
            && !CharacterController.IsFreeGrappleAction(GrappleActionType.MoveHalfSpeed),
            "Other grapple actions are not free actions");

        // The shared PC/AI post-action step (FinalizeGrappleActionResolution and AI_GrappleRestrictedTurn)
        // grants the free adjacent move only to an escaper; a releaser stays put (PHB p.157).
        GameManager gm = GameManager.Instance;
        MethodInfo endedGrapple = typeof(GameManager).GetMethod("DidActorEndGrappleAndGainFreeAdjacentMove", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(endedGrapple != null, "GameManager.DidActorEndGrappleAndGainFreeAdjacentMove exists for the release follow-up test");
        if (gm != null && endedGrapple != null)
        {
            bool releaseGrantsStep = (bool)endedGrapple.Invoke(gm, new object[] { attacker, GrappleActionType.ReleasePinnedOpponent, releaseResult });
            Assert(!releaseGrantsStep, "Releasing a pin grants the releaser no free adjacent move (only an escape does, PHB p.157)");
        }
        else if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; release follow-up check needs Play mode");
        }

        Cleanup(attacker, defender);
    }

    private static void TestSilentAndStillMetamagicRemoveVerbalAndSomaticComponents()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
        Assert(spell != null, "Magic Missile exists for metamagic component suppression test");
        if (spell == null)
            return;

        SpellData spellClone = spell.Clone();
        spellClone.HasVerbalComponent = true;
        spellClone.HasSomaticComponent = true;

        var metamagic = new MetamagicData();
        metamagic.AppliedMetamagic.Add(MetamagicFeatId.SilentSpell);
        metamagic.AppliedMetamagic.Add(MetamagicFeatId.StillSpell);

        SpellCaster.ApplyMetamagicToSpellData(spellClone, metamagic);

        Assert(!spellClone.HasVerbalComponent, "Silent Spell metamagic removes verbal component from cloned spell data");
        Assert(!spellClone.HasSomaticComponent, "Still Spell metamagic removes somatic component from cloned spell data");

        Assert(spell.HasVerbalComponent, "Original spell data verbal component remains unchanged after metamagic clone mutation");
        Assert(spell.HasSomaticComponent, "Original spell data somatic component remains unchanged after metamagic clone mutation");
    }

    private static void TestIterativeGrappleAttackBonusesConsumeInOrder()
    {
        var attacker = CreateTestCharacter("IterativeGrappleBonuses", "Fighter");
        attacker.Stats.BaseAttackBonusOverride = 11; // CHR-068: the plain setter is ignored for classed characters
        attacker.StartNewTurn();

        bool first = attacker.TryConsumeIterativeGrappleAttackAction(out int bab1, out int remaining1, out string reason1);
        bool second = attacker.TryConsumeIterativeGrappleAttackAction(out int bab2, out int remaining2, out string reason2);
        bool third = attacker.TryConsumeIterativeGrappleAttackAction(out int bab3, out int remaining3, out string reason3);
        bool fourth = attacker.TryConsumeIterativeGrappleAttackAction(out int bab4, out int remaining4, out string reason4);

        Assert(first && second && third, "BAB +11 character can consume 3 iterative grapple attacks in one sequence");
        Assert(bab1 == 11 && bab2 == 6 && bab3 == 1, "Iterative grapple attacks use BAB progression +11/+6/+1");
        Assert(remaining1 == 2 && remaining2 == 1 && remaining3 == 0, "Iterative grapple attack remaining counter decreases each use");
        Assert(!fourth && !string.IsNullOrEmpty(reason4), "No additional iterative grapple attack is available after budget is exhausted");

        Cleanup(attacker);
    }

    private static void TestOpposedEscapeCountsAsIterativeGrappleAttackAction()
    {
        bool opposedEscapeIsIterative = CharacterController.IsIterativeGrappleAttackAction(GrappleActionType.OpposedGrappleEscape);
        bool escapeArtistIsIterative = CharacterController.IsIterativeGrappleAttackAction(GrappleActionType.EscapeArtist);

        Assert(opposedEscapeIsIterative, "Opposed grapple escape is classified as an iterative grapple attack action");
        Assert(!escapeArtistIsIterative, "Escape Artist remains a non-iterative standard-action grapple option");
    }

    private static void TestStandardOnlyAllowsSingleIterativeGrappleAttack()
    {
        var attacker = CreateTestCharacter("StandardSingleIterative", "Fighter");
        attacker.Stats.BaseAttackBonusOverride = 11; // CHR-068: the plain setter is ignored for classed characters
        attacker.StartNewTurn();

        attacker.Actions.UseMoveAction(); // Spend move first: only standard action remains.

        bool first = attacker.TryConsumeIterativeGrappleAttackAction(out int bab1, out int remaining1, out _);
        bool second = attacker.TryConsumeIterativeGrappleAttackAction(out int bab2, out int remaining2, out string reason2);

        Assert(first, "Character can consume one iterative grapple attack with standard action only");
        Assert(bab1 == 11 && remaining1 == 0, "Standard-action grapple uses first BAB only and leaves no iterative attacks");
        Assert(!second && !string.IsNullOrEmpty(reason2), "Additional iterative grapple attacks are unavailable after standard-only use");

        Cleanup(attacker);
    }

    private static void TestImprovedGrabCreatureCanUseStandardGrappleAction()
    {
        var attacker = CreateTestCharacter("ImprovedGrabStandardGrapple", "Fighter");
        var defender = CreateWeakDefender("ImprovedGrabStandardGrappleTarget");

        attacker.Stats.HasImprovedGrab = true;
        attacker.StartNewTurn();

        bool canUseStandardGrapple = attacker.CanUseStandardGrapple();
        SpecialAttackResult result = attacker.ExecuteSpecialAttack(SpecialAttackType.Grapple, defender);

        // CMB-014: Improved Grab adds a free grapple on a hit; it does not remove the normal one.
        Assert(canUseStandardGrapple, "Improved Grab creature can still use the standard grapple action");
        Assert(result != null && result.Log.Contains("Touch attack"), "Improved Grab creature's standard grapple rolls the grab touch attack");
        Assert(result != null && !result.Log.Contains("cannot initiate"), "Standard grapple is not refused for an Improved Grab creature");

        Cleanup(attacker, defender);
    }

    private static void TestImprovedGrabCreatureCanStillUseIterativeGrappleActionsWhenAlreadyGrappling()
    {
        var attacker = CreateTestCharacter("ImprovedGrabIterativeGrapple", "Fighter");
        var defender = CreateWeakDefender("ImprovedGrabIterativeGrappleTarget");

        attacker.Stats.BaseAttackBonusOverride = 11; // CHR-068: the plain setter is ignored for classed characters
        attacker.Stats.HasImprovedGrab = true;
        attacker.StartNewTurn();

        ForceGrappleState(attacker, defender);

        bool consumed = attacker.TryConsumeIterativeGrappleAttackAction(out int babUsed, out int remaining, out string reason);

        Assert(consumed, "Improved Grab creature can still consume iterative grapple attacks after grapple is established");
        Assert(babUsed == 11, "First iterative grapple attack for Improved Grab creature uses top BAB");
        Assert(remaining == 2, "Improved Grab creature retains full iterative grapple budget after first consume");
        Assert(string.IsNullOrEmpty(reason), "No failure reason is returned when iterative grapple consume succeeds");

        Cleanup(attacker, defender);
    }

    private static void TestIterativeDisarmAttackBonusesConsumeInOrder()
    {
        var attacker = CreateTestCharacter("IterativeDisarmBonuses", "Fighter");
        attacker.Stats.BaseAttackBonusOverride = 11; // CHR-068: the plain setter is ignored for classed characters
        attacker.StartNewTurn();

        bool first = attacker.TryConsumeIterativeDisarmAttackAction(out int bab1, out int remaining1, out string reason1);
        bool second = attacker.TryConsumeIterativeDisarmAttackAction(out int bab2, out int remaining2, out string reason2);
        bool third = attacker.TryConsumeIterativeDisarmAttackAction(out int bab3, out int remaining3, out string reason3);
        bool fourth = attacker.TryConsumeIterativeDisarmAttackAction(out int bab4, out int remaining4, out string reason4);

        Assert(first && second && third, "BAB +11 character can consume 3 iterative disarm attacks in one sequence");
        Assert(bab1 == 11 && bab2 == 6 && bab3 == 1, "Iterative disarm attacks use BAB progression +11/+6/+1");
        Assert(remaining1 == 2 && remaining2 == 1 && remaining3 == 0, "Iterative disarm attack remaining counter decreases each use");
        Assert(!fourth && !string.IsNullOrEmpty(reason4), "No additional iterative disarm attack is available after budget is exhausted");

        Cleanup(attacker);
    }

    private static void TestStandardOnlyAllowsSingleIterativeDisarmAttack()
    {
        var attacker = CreateTestCharacter("StandardSingleDisarmIterative", "Fighter");
        attacker.Stats.BaseAttackBonusOverride = 11; // CHR-068: the plain setter is ignored for classed characters
        attacker.StartNewTurn();

        attacker.Actions.UseMoveAction(); // Spend move first: only standard action remains.

        bool first = attacker.TryConsumeIterativeDisarmAttackAction(out int bab1, out int remaining1, out _);
        bool second = attacker.TryConsumeIterativeDisarmAttackAction(out int bab2, out int remaining2, out string reason2);

        Assert(first, "Character can consume one iterative disarm attack with standard action only");
        Assert(bab1 == 11 && remaining1 == 0, "Standard-action disarm uses first BAB only and leaves no iterative attacks");
        Assert(!second && !string.IsNullOrEmpty(reason2), "Additional iterative disarm attacks are unavailable after standard-only use");

        Cleanup(attacker);
    }

    // ===== Per-creature attack sequence (PHB p.143; CMB-102) =====

    private static CharacterController CreateIterativeAttacker(string name)
    {
        var attacker = CreateTestCharacter(name, "Fighter");
        attacker.Stats.BaseAttackBonusOverride = 11;
        attacker.StartNewTurn();
        return attacker;
    }

    private static void TestAttackSequenceFirstStepSpendsOnlyStandardAction()
    {
        var attacker = CreateIterativeAttacker("SequenceStandardThenFull");

        bool first = attacker.TryCommitAttack(AttackStepKind.MainHand, out int step0, out _);
        bool standardSpentAfterFirst = !attacker.Actions.HasStandardAction;
        bool moveLeftAfterFirst = attacker.Actions.HasMoveAction;
        ProgressiveAttackMode modeAfterFirst = attacker.ProgressiveAttackPool.Mode;

        bool second = attacker.TryCommitAttack(AttackStepKind.MainHand, out int step1, out _);
        bool moveSpentAfterSecond = !attacker.Actions.HasMoveAction;
        ProgressiveAttackMode modeAfterSecond = attacker.ProgressiveAttackPool.Mode;

        bool moveUsedBefore = attacker.Actions.MoveActionUsed;
        bool standardUsedBefore = attacker.Actions.StandardActionUsed;
        bool fullRoundUsedBefore = attacker.Actions.FullRoundActionUsed;
        bool third = attacker.TryCommitAttack(AttackStepKind.MainHand, out int step2, out _);
        bool thirdSpentNothing = attacker.Actions.MoveActionUsed == moveUsedBefore
            && attacker.Actions.StandardActionUsed == standardUsedBefore
            && attacker.Actions.FullRoundActionUsed == fullRoundUsedBefore;

        Assert(first && step0 == 0 && standardSpentAfterFirst && moveLeftAfterFirst && modeAfterFirst == ProgressiveAttackMode.StandardAttackCommitted,
            "First attack step spends only the standard action and leaves the move action (PHB p.143)");
        Assert(second && step1 == 1 && moveSpentAfterSecond && modeAfterSecond == ProgressiveAttackMode.FullAttackCommitted,
            "Second attack step spends the move action and makes the turn a full attack");
        Assert(third && step2 == 2 && thirdSpentNothing, "Third attack step of a full attack spends no further action");

        Cleanup(attacker);
    }

    private static void TestAttackSequenceSecondStepRefusedAfterMoveAction()
    {
        var attacker = CreateIterativeAttacker("SequenceMoveThenAttack");
        attacker.Actions.UseMoveAction();

        bool first = attacker.TryCommitAttack(AttackStepKind.MainHand, out _, out _);
        bool second = attacker.TryCommitAttack(AttackStepKind.MainHand, out _, out string reason);

        Assert(first, "After a move action the creature can still make one attack (standard action)");
        Assert(!second && !string.IsNullOrEmpty(reason), "After a move action a second attack is refused with a reason (no full attack after moving)");

        Cleanup(attacker);
    }

    private static void TestAttackSequenceFiveFootStepAllowsFullAttack()
    {
        var attacker = CreateIterativeAttacker("SequenceFiveFootStep");
        attacker.Actions.HasMoved5Ft = true;
        attacker.HasTakenFiveFootStep = true;

        bool first = attacker.TryCommitAttack(AttackStepKind.MainHand, out _, out _);
        bool second = attacker.TryCommitAttack(AttackStepKind.MainHand, out _, out string reason);

        Assert(first && second, "A 5-foot step does not use the move action, so a full attack is still allowed (PHB p.144)");

        Cleanup(attacker);
    }

    private static void TestAttackSequenceOffHandThenMainHandEntersFullAttack()
    {
        var attacker = CreateIterativeAttacker("SequenceOffHandFirst");

        bool offHand = attacker.TryCommitAttack(AttackStepKind.OffHand, out int offHandStep, out _);
        bool mainHand = attacker.TryCommitAttack(AttackStepKind.MainHand, out int mainHandStep, out _);

        Assert(offHand && offHandStep == -1, "An off-hand attack is committed without moving the main-hand cursor");
        Assert(mainHand && mainHandStep == 0 && !attacker.Actions.HasMoveAction && attacker.ProgressiveAttackPool.IsFullAttack,
            "A main-hand attack after an off-hand attack spends the move action and starts at the first iterative step");

        Cleanup(attacker);
    }

    private static void TestAttackSequenceIsPerCreature()
    {
        var attackerA = CreateIterativeAttacker("SequenceOwnerA");
        var attackerB = CreateIterativeAttacker("SequenceOwnerB");

        bool committedA = attackerA.TryCommitAttack(AttackStepKind.MainHand, out _, out _);
        bool committedB = attackerB.TryCommitAttack(AttackStepKind.MainHand, out int stepB, out string reasonB);

        Assert(committedA, "Creature A commits its first attack step");
        Assert(committedB && stepB == 0 && attackerA.ProgressiveAttackPool.MainHandStepsUsed == 1,
            "Creature B can still commit its own first attack step while A has an attack sequence");

        Cleanup(attackerA, attackerB);
    }

    private static void TestAttackSequencePayingTwiceSpendsOnlyStandardAction()
    {
        var attacker = CreateIterativeAttacker("SequencePayTwice");

        bool paid1 = attacker.TryPayForNextAttack(out _);
        bool paid2 = attacker.TryPayForNextAttack(out _);
        bool moveStillAvailable = attacker.Actions.HasMoveAction;
        int step = attacker.RegisterAttackMade(AttackStepKind.MainHand);

        Assert(paid1 && paid2 && moveStillAvailable && !attacker.Actions.HasStandardAction,
            "Paying twice for the same attack (cancelled targeting, retry) spends only the standard action");
        Assert(step == 0 && attacker.ProgressiveAttackPool.Mode == ProgressiveAttackMode.StandardAttackCommitted && !attacker.ProgressiveAttackPool.PendingStepPaid,
            "Registering the paid attack records step 0 and clears the pending payment");

        Cleanup(attacker);
    }

    private static void TestAttackSequenceClearedByStartNewTurn()
    {
        var attacker = CreateIterativeAttacker("SequenceTurnReset");

        attacker.TryCommitAttack(AttackStepKind.MainHand, out _, out _);
        attacker.TryCommitAttack(AttackStepKind.MainHand, out _, out _);
        attacker.TryPayForNextAttack(out _);
        attacker.StartNewTurn();

        AttackPool pool = attacker.ProgressiveAttackPool;
        Assert(pool.Mode == ProgressiveAttackMode.None && pool.AttacksCommitted == 0 && pool.MainHandStepsUsed == 0
            && pool.MainHandBudget == 0 && !pool.PendingStepPaid,
            "StartNewTurn clears the attack-sequence mode, counters and pending payment");

        Cleanup(attacker);
    }

    private static void TestAttackSequenceSingleActionOnlyAllowsOneStep()
    {
        var attacker = CreateIterativeAttacker("SequenceSingleActionOnly");
        attacker.Actions.SingleActionOnly = true;

        int remainingBefore = attacker.GetRemainingMainHandAttackSteps();
        bool first = attacker.TryCommitAttack(AttackStepKind.MainHand, out _, out _);
        bool second = attacker.TryCommitAttack(AttackStepKind.MainHand, out _, out string reason);

        Assert(remainingBefore == 1, "A creature limited to a single action shows one attack step remaining");
        Assert(first && !second && !string.IsNullOrEmpty(reason), "A creature limited to a single action gets one attack step only");

        Cleanup(attacker);
    }

    private static void TestAttackSequenceSlowedCreatureGetsOneStep()
    {
        var attacker = CreateIterativeAttacker("SequenceSlowed");
        attacker.ApplySlowEffect(2, null);

        int remainingBefore = attacker.GetRemainingMainHandAttackSteps();
        bool first = attacker.TryCommitAttack(AttackStepKind.MainHand, out _, out _);
        bool second = attacker.TryCommitAttack(AttackStepKind.MainHand, out _, out string reason);

        Assert(remainingBefore == 1, "A slowed creature shows one attack step remaining (PHB p.280)");
        // Only the refused second attack is asserted. RAW a slowed creature takes a move or a standard
        // action, not both (PHB p.280); its move action is still available here (CMB-095).
        Assert(first && !second && !string.IsNullOrEmpty(reason),
            "A slowed creature cannot turn its attack into a full attack (PHB p.280: no full-round actions)");

        Cleanup(attacker);
    }

    private static void TestAttackSequenceSingleAttackThenAttackUsesNextStep()
    {
        // A single standard attack (e.g. Fighting Defensively (Std)) is step 0 of the ladder, so a later
        // Attack continues at the second iterative step instead of starting over (PHB p.143).
        var attacker = CreateIterativeAttacker("SequenceSingleThenAttack");

        bool paid = attacker.TryPayForNextAttack(out _);
        int singleStep = attacker.RegisterAttackMade(AttackStepKind.MainHand);
        bool second = attacker.TryCommitAttack(AttackStepKind.MainHand, out int step1, out _);
        bool third = attacker.TryCommitAttack(AttackStepKind.MainHand, out int step2, out _);
        bool fourth = attacker.TryCommitAttack(AttackStepKind.MainHand, out _, out string reason);

        Assert(paid && singleStep == 0 && second && step1 == 1 && attacker.GetMainHandAttackStepBAB(step1) == 6,
            "After a single standard attack, the next Attack is the second iterative step (+6)");
        Assert(third && step2 == 2 && !fourth && !string.IsNullOrEmpty(reason),
            "A single attack plus a later Attack never exceeds the iterative attacks (+11/+6/+1)");

        Cleanup(attacker);
    }

    private static CharacterController CreateNaturalAttacker(string name, params (string label, int count)[] naturalAttacks)
    {
        var attacker = CreateIterativeAttacker(name);
        attacker.Stats.NaturalAttacks.Clear();
        foreach (var natural in naturalAttacks)
        {
            attacker.Stats.NaturalAttacks.Add(new NaturalAttackDefinition
            {
                Name = natural.label,
                DamageDice = 6,
                DamageCount = 1,
                Count = natural.count
            });
        }

        return attacker;
    }

    private static void TestAttackSequenceNaturalAttackThenTripRefusedWhenNaturalBudgetSpent()
    {
        // One bite: a trip replaces a melee attack (PHB p.141 Table 8-2 note 7), it is not added to it.
        var wolf = CreateNaturalAttacker("SequenceNaturalBite", ("Bite", 1));

        bool paid = wolf.TryPayForNextAttack(out _);
        int biteStep = wolf.RegisterAttackMade(AttackStepKind.NaturalSequence);
        bool canTrip = wolf.CanCommitAttack(AttackStepKind.MainHand, out string reason);

        Assert(paid && biteStep == 0 && !canTrip && !string.IsNullOrEmpty(reason) && wolf.GetRemainingMainHandAttackSteps() == 0,
            "A creature with one natural attack cannot trip after using it");

        Cleanup(wolf);
    }

    private static void TestAttackSequenceTripReplacesOneNaturalAttack()
    {
        var bear = CreateNaturalAttacker("SequenceNaturalClawClawBite", ("Claw", 2), ("Bite", 1));

        bool trip = bear.TryCommitAttack(AttackStepKind.MainHand, out int tripStep, out _);
        bool natural1 = bear.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
        bool natural2 = bear.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
        bool natural3 = bear.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out string reason);

        Assert(trip && tripStep == 0 && bear.ProgressiveAttackPool.MainHandBudget == 3,
            "A trip by a natural-weapon creature uses one step of its natural-attack budget");
        Assert(natural1 && natural2 && !natural3 && !string.IsNullOrEmpty(reason),
            "After a trip replaces one of three natural attacks, only two natural attacks remain");

        Cleanup(bear);
    }

    private static void TestAttackSequenceStepResolverUsesStepBab()
    {
        // CMB-102 / PC_NPC_PARITY step 6: the shared step resolver used by the PC iterative flow and
        // the NPC melee sequence rolls a weapon step at that step's iterative BAB plus the caller's
        // adjustment, and a natural step is that natural attack of the innate sequence.
        var attacker = CreateIterativeAttacker("StepResolverWeapon");
        var target = CreateWeakDefender("StepResolverTarget");
        var bear = CreateNaturalAttacker("StepResolverNatural", ("Claw", 2), ("Bite", 1));
        target.GridPosition = new Vector2Int(1, 0);
        // Enough hit points that the earlier steps cannot drop it before the natural step.
        target.Stats.AdjustMaxHP(500);
        target.Stats.CurrentHP += 500;
        try
        {
            CombatResult second = attacker.ResolveAttackSequenceStep(target, AttackStepKind.MainHand, 1,
                false, 0, null, null, attacker.GetEquippedMainWeapon(), 0, out string secondLabel);
            Assert(second != null && second.BreakdownBAB == 6 && secondLabel == "Attack 2 (BAB +6)",
                "Shared step resolver: the second weapon step rolls at the second iterative BAB (+6)");

            CombatResult adjusted = attacker.ResolveAttackSequenceStep(target, AttackStepKind.MainHand, 0,
                false, 0, null, null, attacker.GetEquippedMainWeapon(), -2, out _);
            Assert(adjusted != null && adjusted.BreakdownBAB == 9,
                "Shared step resolver: the BAB adjustment (dual-wield main-hand penalty) is applied to the step BAB");

            CombatResult bite = bear.ResolveAttackSequenceStep(target, AttackStepKind.NaturalSequence, 2,
                false, 0, null, null, null, 0, out string biteLabel);
            Assert(bite != null && bite.WeaponName == "Bite" && biteLabel != null && biteLabel.StartsWith("Bite"),
                "Shared step resolver: natural step 2 of claw/claw/bite is the bite");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Shared step resolver check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Cleanup(attacker, target, bear);
        }
    }

    private static void TestManeuverActionCostTable()
    {
        // PHB p.141 Table 8-2 note 7: disarm, grapple and trip replace a melee attack; sunder is a
        // melee attack (PHB p.158, owner decision 2026-10-07). Bull rush (p.154) and overrun
        // (p.157) are standard actions or part of a charge, feint (p.155) a standard action, and a
        // coup de grace a full-round action (CMB-102).
        Assert(ManeuverActionCost.ReplacesMeleeAttack(SpecialAttackType.Trip)
            && ManeuverActionCost.ReplacesMeleeAttack(SpecialAttackType.Disarm)
            && ManeuverActionCost.ReplacesMeleeAttack(SpecialAttackType.Sunder)
            && ManeuverActionCost.ReplacesMeleeAttack(SpecialAttackType.Grapple),
            "Trip, disarm, sunder and grapple replace one melee attack");
        Assert(!ManeuverActionCost.ReplacesMeleeAttack(SpecialAttackType.BullRushAttack)
            && !ManeuverActionCost.ReplacesMeleeAttack(SpecialAttackType.BullRushCharge)
            && !ManeuverActionCost.ReplacesMeleeAttack(SpecialAttackType.Overrun)
            && !ManeuverActionCost.ReplacesMeleeAttack(SpecialAttackType.Feint)
            && !ManeuverActionCost.ReplacesMeleeAttack(SpecialAttackType.CoupDeGrace),
            "Bull rush, overrun, feint and coup de grace do not replace a melee attack");
    }

    private static void TestBullRushIsAStandardAction()
    {
        // PHB p.141 Table 8-2 and p.154: bull rush is a standard action (or part of a charge) for
        // every creature; it never replaces one attack of a full attack (CMB-102).
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; bull rush action cost check needs Play mode");
            return;
        }

        var attacker = CreateIterativeAttacker("BullRushStandardAction");
        var moved = CreateIterativeAttacker("BullRushStandardActionMoved");
        CharacterController farTarget = null;
        try
        {
            Assert(gm.CanUseBullRushAttackOption(attacker),
                "A fresh creature can bull rush (standard action available)");

            bool committed = attacker.TryCommitAttack(AttackStepKind.MainHand, out _, out string why);
            Assert(committed && !gm.CanUseBullRushAttackOption(attacker),
                $"After one attack spends the standard action the creature cannot bull rush (PHB p.154) {why}");

            moved.Actions.UseMoveAction();
            Assert(gm.CanUseBullRushAttackOption(moved),
                "A creature that spent only its move action can still bull rush as its standard action");

            // The NPC executor runs the shared legality check before any cost or AoO.
            farTarget = CreateWeakDefender("BullRushStandardActionFarTarget");
            moved.GridPosition = new Vector2Int(0, 0);
            farTarget.GridPosition = new Vector2Int(2, 0);
            bool farBullRush = gm.TryNPCSpecialAttackByTypeForAI(moved, farTarget, SpecialAttackType.BullRushAttack);
            bool farRefusedForAdjacency = !moved.CanBullRush(farTarget, false, out string farReason)
                && farReason != null && farReason.Contains("not adjacent");
            Assert(!farBullRush && moved.Actions.HasStandardAction && farRefusedForAdjacency,
                $"NPC bull rush at a target two squares away is refused by CanBullRush's adjacency rule and spends nothing ({farReason})");

            // The same creature and target once adjacent: the executor now bull rushes and spends the
            // standard action, so the refusal above came from adjacency. AI-run so the push and
            // follow choices resolve without the PC prompts.
            moved.IsControllable = false;
            farTarget.GridPosition = new Vector2Int(1, 0);
            bool nearBullRush = gm.TryNPCSpecialAttackByTypeForAI(moved, farTarget, SpecialAttackType.BullRushAttack);
            Assert(nearBullRush && !moved.Actions.HasStandardAction,
                "NPC bull rush at an adjacent target is attempted and spends the standard action (PHB p.154)");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Bull rush action cost check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            // A successful push or follow moves the actors on the live grid; clear their occupancy.
            if (gm.Grid != null)
            {
                gm.Grid.ClearCreatureOccupancy(moved);
                if (farTarget != null)
                    gm.Grid.ClearCreatureOccupancy(farTarget);
            }

            Cleanup(attacker, moved, farTarget);
        }
    }

    private static void TestBullRushTargetLimits()
    {
        // Shared legality for PC and NPC (CharacterController.CanBullRush): at most one size category
        // larger (PHB p.154), not a swarm (MM p.316), not incorporeal (MM p.311), not while grappling
        // (PHB p.156), and adjacent (the bull rusher enters the defender's space). Positions are set
        // directly; the check reads footprints from GridPosition and size.
        var attacker = CreateTestCharacter("BullRushLimitsAttacker", "Fighter");
        var target = CreateWeakDefender("BullRushLimitsTarget");
        CharacterController grappler = null;
        CharacterController grappled = null;
        CharacterController grappleVictim = null;
        try
        {
            attacker.GridPosition = new Vector2Int(0, 0);
            target.GridPosition = new Vector2Int(1, 0);

            Assert(attacker.CanBullRush(target, false, out string mediumReason),
                $"Medium vs adjacent Medium is allowed {mediumReason}");

            target.Stats.SetBaseSizeCategory(SizeCategory.Large);
            Assert(attacker.CanBullRush(target, false, out string largeReason),
                $"Medium vs adjacent Large (one category larger) is allowed (PHB p.154) {largeReason}");

            target.Stats.SetBaseSizeCategory(SizeCategory.Huge);
            Assert(!attacker.CanBullRush(target, false, out _),
                "Medium vs Huge (two categories larger) is refused (PHB p.154)");

            attacker.Stats.SetBaseSizeCategory(SizeCategory.Small);
            target.Stats.SetBaseSizeCategory(SizeCategory.Large);
            Assert(!attacker.CanBullRush(target, false, out _),
                "Small vs Large (two categories larger) is refused (PHB p.154)");

            attacker.Stats.SetBaseSizeCategory(SizeCategory.Medium);
            target.Stats.SetBaseSizeCategory(SizeCategory.Medium);

            target.Stats.IsSwarm = true;
            Assert(!attacker.CanBullRush(target, false, out _), "A swarm cannot be bull rushed (MM p.316)");
            target.Stats.IsSwarm = false;

            target.ConfigureIncorporeal(true);
            Assert(!attacker.CanBullRush(target, false, out _), "An incorporeal creature cannot be bull rushed (MM p.311)");
            target.ConfigureIncorporeal(false);

            target.GridPosition = new Vector2Int(2, 0);
            Assert(!attacker.CanBullRush(target, false, out _), "A target two squares away is refused (must enter its space)");
            Assert(attacker.CanBullRush(target, true, out string chargeReason),
                $"The charge planner leaves adjacency to the charge path {chargeReason}");
            target.GridPosition = new Vector2Int(1, 0);

            grappler = CreateTestCharacter("BullRushLimitsGrappler", "Fighter");
            grappleVictim = CreateWeakDefender("BullRushLimitsGrappleVictim");
            grappled = CreateWeakDefender("BullRushLimitsOther");
            grappler.GridPosition = new Vector2Int(5, 5);
            grappleVictim.GridPosition = new Vector2Int(6, 5);
            grappled.GridPosition = new Vector2Int(5, 6);
            ForceGrappleState(grappler, grappleVictim);
            if (grappler.IsGrappling())
                Assert(!grappler.CanBullRush(grappled, false, out _), "A grappling creature cannot bull rush (PHB p.156)");
            else
                Debug.Log("  [SKIP] Could not set up a grapple; grappling bull rush refusal not checked");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Bull rush target limits check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            Cleanup(attacker, target, grappler, grappled, grappleVictim);
        }
    }

    private static void TestNpcManeuverCostsOneAttackStep()
    {
        // CMB-102: an NPC trip goes through the shared attack sequence (TryNPCSpecialAttackIfBeneficial):
        // the first one spends only the standard action, a second one turns the turn into a full attack
        // (PHB p.143), and a bull rush is a standard action, never an attack step (PHB p.154).
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; NPC maneuver cost check needs Play mode");
            return;
        }

        var npc = CreateIterativeAttacker("NpcManeuverStepCost");
        var target = CreateWeakDefender("NpcManeuverStepCostTarget");
        // Adjacent, so the bull rush below is refused for its action cost, not by CanBullRush's adjacency rule.
        target.GridPosition = new Vector2Int(1, 0);
        try
        {
            bool firstTrip = gm.TryNPCSpecialAttackByTypeForAI(npc, target, SpecialAttackType.Trip);
            Assert(firstTrip
                && npc.ProgressiveAttackPool.MainHandStepsUsed == 1
                && npc.ProgressiveAttackPool.Mode == ProgressiveAttackMode.StandardAttackCommitted
                && !npc.Actions.HasStandardAction
                && npc.Actions.HasMoveAction,
                "NPC trip costs one attack step and only the standard action; the move action stays (CMB-102)");

            bool bullRush = gm.TryNPCSpecialAttackByTypeForAI(npc, target, SpecialAttackType.BullRushAttack);
            Assert(!bullRush
                && npc.ProgressiveAttackPool.MainHandStepsUsed == 1
                && npc.Actions.HasMoveAction,
                "NPC bull rush after an attack is refused and spends nothing: it needs a standard action (PHB p.154)");

            bool secondTrip = gm.TryNPCSpecialAttackByTypeForAI(npc, target, SpecialAttackType.Trip);
            Assert(secondTrip
                && npc.ProgressiveAttackPool.MainHandStepsUsed == 2
                && npc.ProgressiveAttackPool.IsFullAttack
                && !npc.Actions.HasMoveAction,
                "A second NPC trip is the next step and turns the turn into a full attack (PHB p.143)");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"NPC maneuver cost check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Cleanup(npc, target);
        }
    }

    private static FullAttackResult RunNpcMeleeSequence(GameManager gm, CharacterController npc, CharacterController target,
        System.Func<CharacterController, CharacterController, bool> tryStepManeuver, out int maneuversUsed)
    {
        MethodInfo method = typeof(GameManager).GetMethod("PerformNPCMeleeAttackSequence", BindingFlags.Instance | BindingFlags.NonPublic);
        object[] args = { npc, target, null, tryStepManeuver, false, 0, 0, null };
        var result = (FullAttackResult)method.Invoke(gm, args);
        maneuversUsed = (int)args[5];
        return result;
    }

    private static void TestNpcMeleeSequenceTripThenAttacks()
    {
        // CMB-102: the NPC melee attack runs step by step, so a trip can replace the first attack and
        // the remaining iteratives attack (+6, +1; PHB p.143, p.158). A trip-only evaluation is used
        // so the test does not depend on the AI profile.
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; NPC melee sequence check needs Play mode");
            return;
        }

        var npc = CreateIterativeAttacker("NpcMeleeSequence");
        var target = CreateWeakDefender("NpcMeleeSequenceTarget");
        var movedNpc = CreateIterativeAttacker("NpcMeleeSequenceMoved");
        target.GridPosition = new Vector2Int(1, 0);
        movedNpc.GridPosition = new Vector2Int(1, 1);
        target.Stats.AdjustMaxHP(500);
        target.Stats.CurrentHP += 500;
        CharacterController bear = null;
        CharacterController bearTarget = null;
        try
        {
            int tripsTried = 0;
            FullAttackResult full = RunNpcMeleeSequence(gm, npc, target,
                (actor, stepTarget) => tripsTried++ == 0 && gm.TryNPCSpecialAttackByTypeForAI(actor, stepTarget, SpecialAttackType.Trip),
                out int maneuvers);

            Assert(maneuvers == 1 && full.Attacks.Count == 2
                && full.Attacks[0].BreakdownBAB == 6 && full.Attacks[1].BreakdownBAB == 1
                && npc.ProgressiveAttackPool.MainHandStepsUsed == 3 && npc.ProgressiveAttackPool.IsFullAttack,
                "NPC melee sequence: a trip replaces the first attack and the next iteratives attack at +6 and +1 (CMB-102)");

            movedNpc.Actions.UseMoveAction();
            FullAttackResult afterMove = RunNpcMeleeSequence(gm, movedNpc, target, null, out _);
            Assert(afterMove.Attacks.Count == 1 && afterMove.Attacks[0].BreakdownBAB == 11 && !movedNpc.ProgressiveAttackPool.IsFullAttack,
                "NPC melee sequence: an NPC that used its move action makes one attack (PHB p.143)");

            // A natural-weapon creature with one iterative (BAB +4): the trip replaces the first claw
            // and the remaining claw and bite follow, as in the PC iterative flow.
            bear = CreateNaturalAttacker("NpcMeleeSequenceNatural", ("Claw", 2), ("Bite", 1));
            bear.Stats.BaseAttackBonusOverride = 4;
            bearTarget = CreateWeakDefender("NpcMeleeSequenceNaturalTarget");
            bear.GridPosition = new Vector2Int(4, 4);
            bearTarget.GridPosition = new Vector2Int(5, 4);
            bearTarget.Stats.AdjustMaxHP(500);
            bearTarget.Stats.CurrentHP += 500;
            int bearTripsTried = 0;
            FullAttackResult natural = RunNpcMeleeSequence(gm, bear, bearTarget,
                (actor, stepTarget) => bearTripsTried++ == 0 && gm.TryNPCSpecialAttackByTypeForAI(actor, stepTarget, SpecialAttackType.Trip),
                out int bearManeuvers);
            Assert(bearManeuvers == 1 && natural.Attacks.Count == 2
                && natural.Attacks[0].WeaponName == "Claw" && natural.Attacks[1].WeaponName == "Bite"
                && bear.ProgressiveAttackPool.MainHandStepsUsed == 3,
                "NPC melee sequence: after a trip replaces the first of claw/claw/bite, the claw and bite follow (CMB-102)");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"NPC melee sequence check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            Cleanup(npc, target, movedNpc);
            if (bear != null)
                Cleanup(bear);
            if (bearTarget != null)
                Cleanup(bearTarget);
        }
    }

    private static void TestBullRushChargeAppliesPlus2ToAttackerCheck()
    {
        var attacker = CreateTestCharacter("BullRushChargeBonus", "Fighter");
        attacker.Stats.BaseAttackBonus = 11;
        attacker.Stats.STR = 18;
        attacker.StartNewTurn();

        BullRushCheckResult noCharge = attacker.RollBullRushAttackerCheck(chargeBonus: 0, fixedRoll: 10);
        BullRushCheckResult withCharge = attacker.RollBullRushAttackerCheck(chargeBonus: 2, fixedRoll: 10);

        Assert(withCharge.Total == noCharge.Total + 2, "Bull Rush (Charge) applies +2 bonus to attacker opposed check");

        Cleanup(attacker);
    }

    private static void TestBullRushImprovedFeatAddsPlus4()
    {
        var attacker = CreateTestCharacter("BullRushFeatBonus", "Fighter");
        attacker.Stats.BaseAttackBonus = 11;
        attacker.Stats.STR = 18;
        attacker.StartNewTurn();

        BullRushCheckResult baseline = attacker.RollBullRushAttackerCheck(chargeBonus: 0, fixedRoll: 10);
        attacker.Stats.Feats.Add("Improved Bull Rush");
        BullRushCheckResult withFeat = attacker.RollBullRushAttackerCheck(chargeBonus: 0, fixedRoll: 10);

        Assert(withFeat.Total == baseline.Total + 4, "Improved Bull Rush adds +4 to attacker opposed check");

        Cleanup(attacker);
    }

    private static void TestBullRushDefenderUsesStrengthAndDwarfStability()
    {
        var defender = CreateTestCharacter("BullRushDefender", "Fighter");
        defender.Stats.BaseAttackBonus = 6;
        defender.Stats.STR = 8;  // -1
        defender.Stats.DEX = 18; // +4
        defender.Stats.Race = RaceDatabase.GetRace("Dwarf");
        defender.StartNewTurn();

        BullRushCheckResult check = defender.RollBullRushDefenderCheck(fixedRoll: 10);

        // PHB p.154: opposed Strength checks; Dexterity and BAB do not apply (CMB-014).
        int expected = 10 + defender.Stats.STRMod + defender.GetSpecialSizeModifier() + 4;
        Assert(!check.UsesBestStrengthOrDexterity, "Bull rush defender check uses Strength, not the better of STR/DEX");
        Assert(check.StrengthModifier == defender.Stats.STRMod, "Bull rush defender uses its STR modifier");
        Assert(check.StabilityBonus == 4, "Dwarf defender gains +4 stability bonus against bull rush");
        Assert(check.Total == expected, "Bull rush defender total is roll + STR + special size + stability (no BAB, no DEX)");

        Cleanup(defender);
    }

    private static void TestBullRushCheckIgnoresBaseAttackBonus()
    {
        var lowBab = CreateTestCharacter("BullRushLowBab", "Fighter");
        var highBab = CreateTestCharacter("BullRushHighBab", "Fighter");
        lowBab.Stats.BaseAttackBonus = 0;
        highBab.Stats.BaseAttackBonus = 15;

        BullRushCheckResult low = lowBab.RollBullRushAttackerCheck(chargeBonus: 0, fixedRoll: 10);
        BullRushCheckResult high = highBab.RollBullRushAttackerCheck(chargeBonus: 0, fixedRoll: 10);

        Assert(low.Total == high.Total, "Bull rush attacker check ignores BAB (it is a Strength check)");
        Assert(low.Total == 10 + lowBab.Stats.STRMod + lowBab.GetSpecialSizeModifier(), "Bull rush attacker total is roll + STR + special size");

        Cleanup(lowBab, highBab);
    }

    private static void TestSpecialSizeModifierScale()
    {
        var creature = CreateTestCharacter("SpecialSizeScale", "Fighter");

        creature.Stats.CurrentSizeCategory = SizeCategory.Small;
        bool small = creature.GetSpecialSizeModifier() == -4 && creature.Stats.SizeModifier == 1;
        creature.Stats.CurrentSizeCategory = SizeCategory.Large;
        bool large = creature.GetSpecialSizeModifier() == 4 && creature.Stats.SizeModifier == -1;
        creature.Stats.CurrentSizeCategory = SizeCategory.Huge;
        bool huge = creature.GetSpecialSizeModifier() == 8;
        creature.Stats.CurrentSizeCategory = SizeCategory.Medium;

        Assert(small && large && huge, "Special size modifier is -4 Small, +4 Large, +8 Huge (not the attack size modifier)");

        Cleanup(creature);
    }

    private static void TestTripAttackerModifierIsStrengthCheck()
    {
        var attacker = CreateTestCharacter("TripAttackerMods", "Fighter");
        attacker.Stats.STR = 18; // +4
        attacker.Stats.BaseAttackBonus = 10;
        attacker.Stats.CurrentSizeCategory = SizeCategory.Large;
        attacker.Stats.TripAttackCheckBonus = 11; // MM stat-block value: not added on top (it already counts STR and size)

        int baseline = attacker.GetTripAttackerCheckModifier();
        Assert(baseline == 4 + 4, "Trip Strength check = STR + special size; no BAB and no double-counted TripAttackCheckBonus");

        attacker.Stats.Feats.Add("Improved Trip");
        Assert(attacker.GetTripAttackerCheckModifier() == baseline + 4, "Improved Trip adds +4 to the tripper's Strength check");

        Cleanup(attacker);
    }

    private static void TestTripDefenderUsesBestOfStrDexWithoutImprovedTrip()
    {
        var defender = CreateTestCharacter("TripDefenderMods", "Fighter");
        defender.Stats.STR = 8;   // -1
        defender.Stats.DEX = 16;  // +3
        defender.Stats.BaseAttackBonus = 12;
        defender.Stats.CurrentSizeCategory = SizeCategory.Small;

        int baseline = defender.GetTripOrOverrunDefenderCheckModifier();
        Assert(baseline == 3 - 4, "Trip defender uses the better of STR/DEX plus special size, without BAB");

        defender.Stats.Feats.Add("Improved Trip");
        Assert(defender.GetTripOrOverrunDefenderCheckModifier() == baseline, "Regression: the defender's own Improved Trip gives no bonus to resist a trip");

        defender.Stats.Race = RaceDatabase.GetRace("Dwarf");
        Assert(defender.GetTripOrOverrunDefenderCheckModifier() == baseline + 4, "Dwarf stability adds +4 to resist a trip or overrun");

        Cleanup(defender);
    }

    private static void TestOverrunCheckModifiers()
    {
        var attacker = CreateTestCharacter("OverrunAttackerMods", "Fighter");
        attacker.Stats.STR = 16; // +3
        int attackerBase = attacker.GetOverrunAttackerCheckModifier();
        attacker.Stats.Feats.Add("Improved Overrun");
        Assert(attackerBase == 3 && attacker.GetOverrunAttackerCheckModifier() == 7, "Overrun attacker: STR + special size, +4 with Improved Overrun");

        var defender = CreateTestCharacter("OverrunDefenderMods", "Fighter");
        defender.Stats.STR = 10; // +0
        defender.Stats.DEX = 18; // +4
        Assert(defender.GetTripOrOverrunDefenderCheckModifier() == 4, "Overrun defender uses DEX when it is better than STR (PHB p.157)");

        Cleanup(attacker, defender);
    }

    private static void TestManeuverTouchAttackNaturalTwentyAndOne()
    {
        Assert(CharacterController.IsManeuverTouchAttackHit(20, 21, 40), "Natural 20 hits on a maneuver touch attack whatever the touch AC");
        Assert(!CharacterController.IsManeuverTouchAttackHit(1, 30, 10), "Natural 1 misses on a maneuver touch attack whatever the total");
        Assert(CharacterController.IsManeuverTouchAttackHit(10, 15, 15) && !CharacterController.IsManeuverTouchAttackHit(10, 14, 15),
            "Otherwise the touch attack hits when the total meets the touch AC");
    }

    private static void TestOpposedCheckTieBreaks()
    {
        Assert(CharacterController.DoesAttackerWinOpposedCheck(15, 2, 14, 9, null), "Higher opposed total wins");
        Assert(!CharacterController.DoesAttackerWinOpposedCheck(14, 9, 15, 2, null), "Lower opposed total loses");
        Assert(CharacterController.DoesAttackerWinOpposedCheck(15, 5, 15, 3, null), "Tie goes to the higher modifier (attacker)");
        Assert(!CharacterController.DoesAttackerWinOpposedCheck(15, 3, 15, 5, null), "Tie goes to the higher modifier (defender)");

        int[] attackerWins = { 7, 7, 12, 9 };
        int i = 0;
        Assert(CharacterController.DoesAttackerWinOpposedCheck(15, 4, 15, 4, () => attackerWins[i++]),
            "Equal totals and modifiers: both roll again until the tie breaks (attacker wins the reroll)");
        int[] defenderWins = { 3, 18 };
        int j = 0;
        Assert(!CharacterController.DoesAttackerWinOpposedCheck(15, 4, 15, 4, () => defenderWins[j++]),
            "Equal totals and modifiers: the defender can win the reroll");
    }

    private static void TestFreeTripSkipsTouchAttack()
    {
        var attacker = CreateTestCharacter("FreeTripAttacker", "Fighter");
        var defender = CreateWeakDefender("FreeTripTarget");
        attacker.Stats.STR = 30;                                 // +10
        attacker.Stats.CurrentSizeCategory = SizeCategory.Large; // +4 special size
        defender.Stats.STR = 1;
        defender.Stats.DEX = 1;                                  // -5
        defender.Stats.DeflectionBonus = 60;                     // touch AC out of reach

        SpecialAttackResult result = attacker.ResolveFreeTripAttempt(defender);

        // Attacker minimum 1 + 14 = 15 meets the defender maximum 20 - 5 = 15 and wins the tie on modifier.
        Assert(result != null && result.Success, "Free trip after a hit needs no touch attack and wins the Strength check");
        Assert(result != null && !result.Log.Contains("Touch attack"), "Free trip after a hit does not roll a touch attack");

        Cleanup(attacker, defender);
    }

    private static void TestTripAttemptRollsTouchAttack()
    {
        var attacker = CreateTestCharacter("TripTouchAttacker", "Fighter");
        var defender = CreateWeakDefender("TripTouchTarget");
        defender.Stats.DeflectionBonus = 60; // touch AC includes deflection, so only a natural 20 hits

        SpecialAttackResult result = attacker.ExecuteSpecialAttack(SpecialAttackType.Trip, defender);

        Assert(result != null && result.Log.Contains("Touch attack") && result.Log.Contains($"touch AC {defender.Stats.TouchArmorClass}"),
            "A trip attempt starts with a melee touch attack against the full touch AC");
        Assert(result != null && (!result.Success || result.Log.Contains("natural 20")),
            "A trip attempt against an unreachable touch AC succeeds only after a natural 20 touch attack");

        Cleanup(attacker, defender);
    }

    private static void TestDefenderImprovedDisarmGivesNoBonus()
    {
        var attacker = CreateTestCharacter("DisarmFeatAttacker", "Fighter");
        var defender = CreateTestCharacter("DisarmFeatDefender", "Fighter");
        ItemData attackerSword = ItemDatabase.CloneItem(ItemID.WeaponLongsword);
        ItemData defenderSword = ItemDatabase.CloneItem(ItemID.WeaponLongsword);

        MethodInfo rollDisarm = typeof(CharacterController).GetMethod("RollDisarmCheck", BindingFlags.Static | BindingFlags.NonPublic);
        Assert(rollDisarm != null, "RollDisarmCheck is available for the disarm feat test");
        if (rollDisarm == null)
        {
            Cleanup(attacker, defender);
            return;
        }

        object[] args = { attacker, defender, attackerSword, defenderSword, EquipSlot.RightHand, 0, string.Empty, null, 0 };
        int withoutFeat = GetDisarmModifier(rollDisarm.Invoke(null, args), "Defender");
        defender.Stats.Feats.Add("Improved Disarm");
        int withFeat = GetDisarmModifier(rollDisarm.Invoke(null, args), "Defender");
        int attackerWithoutFeat = GetDisarmModifier(rollDisarm.Invoke(null, args), "Attacker");
        attacker.Stats.Feats.Add("Improved Disarm");
        int attackerWithFeat = GetDisarmModifier(rollDisarm.Invoke(null, args), "Attacker");

        Assert(withFeat == withoutFeat, "Regression: the defender's own Improved Disarm adds nothing when resisting a disarm");
        Assert(attackerWithFeat == attackerWithoutFeat + 4, "Improved Disarm adds +4 to the disarming creature's roll");

        Cleanup(attacker, defender);
    }

    private static int GetDisarmModifier(object disarmCheck, string side)
    {
        System.Type type = disarmCheck.GetType();
        int total = (int)type.GetField(side + "Total").GetValue(disarmCheck);
        int roll = (int)type.GetField(side + "Roll").GetValue(disarmCheck);
        return total - roll;
    }

    private static void TestImprovedDisarmDeniesCounterDisarm()
    {
        var attacker = CreateWeakDefender("CounterDisarmAttacker");
        var defender = CreateTestCharacter("CounterDisarmDefender", "Fighter");
        ConfigureVeryWeakGrappler(attacker);
        attacker.Stats.STR = 1;
        ConfigureVeryStrongGrappler(defender);
        attacker.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponDagger), EquipSlot.RightHand);
        defender.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
        attacker.Stats.Feats.Add("Improved Disarm");

        // The weak attacker cannot win (its best total is below the defender's worst), so the
        // attempt fails; with Improved Disarm the defender gets no counter-disarm (PHB p.95).
        SpecialAttackResult result = attacker.ExecuteSpecialAttack(SpecialAttackType.Disarm, defender);

        Assert(result != null && !result.Success, "Weak disarm attempt fails against a much stronger defender");
        Assert(result != null && result.Log.Contains("gets no counter-disarm"), "Improved Disarm denies the defender's counter-disarm");
        Assert(attacker.GetEquippedMainWeapon() != null, "The Improved Disarm attacker keeps its weapon after the failed attempt");

        Cleanup(attacker, defender);
    }

    private static void TestGrappleHoldFailsAgainstMuchLargerTarget()
    {
        var attacker = CreateTestCharacter("GrappleTooLargeAttacker", "Fighter");
        var defender = CreateWeakDefender("GrappleTooLargeTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);
        defender.Stats.CurrentSizeCategory = SizeCategory.Huge; // two categories above Medium
        attacker.StartNewTurn();

        SpecialAttackResult result = attacker.ExecuteSpecialAttack(SpecialAttackType.Grapple, defender);

        Assert(result != null && !result.Success, "Grapple hold automatically fails against a target two or more sizes larger");
        Assert(!attacker.IsGrappling(), "No grapple is established against a much larger target");

        Cleanup(attacker, defender);
    }

}

}
