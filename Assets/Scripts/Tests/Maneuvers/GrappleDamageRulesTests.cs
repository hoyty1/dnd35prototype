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
        TestNaturalAttackStepBabPrimarySecondaryMultiattack();
        TestManeuverReplacesAnyNaturalAttackStep();
        TestPcManeuverReplacesNaturalAttackAtItsBonus();
        TestNpcManeuverReplacesLaterNaturalAttack();
        TestNaturalWeaponDisarmIsArmed();
        TestNaturalWeaponCreatureCannotSunder();
        TestNpcNaturalSequenceMakesOneSubstitute();
        TestAiManeuverStopgapTripSucceedsThenAttacks();
        TestAiManeuverStopgapFailedTripNotRetried();
        TestHasteAddsOneNaturalAttackStep();
        TestHasteNaturalStepUsesChosenNaturalAttack();
        TestHasteNaturalFullAttackAddsOneAttack();
        TestHasteNaturalNotInGrappleRoutine();
        TestHasteWeaponAndMixedAttackersGetOneHasteAttack();
        TestHasteNaturalManeuverReplacesHasteStep();
        TestAiHasteNaturalAttackChoice();
        TestNpcHasteNaturalSequence();
        TestPcHasteNaturalAttackOptions();
        TestPcHasteNaturalButtonSelection();
        TestBullRushChargeAppliesPlus2ToAttackerCheck();
        TestBullRushImprovedFeatAddsPlus4();
        TestBullRushDefenderUsesStrengthAndDwarfStability();
        TestExceptionallyStableCreatureData();
        TestExceptionallyStableDefenderGetsPlus4();
        TestStableDwarfGetsSinglePlus4AndNoneWhileMounted();
        TestNpcSetupCopiesExceptionallyStable();
        TestMoveThroughOverrunUsesSharedCheck();
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
        // Stats.BaseAttackBonus is ignored for classed characters (CHR-068), so set the override.
        controller.Stats.BaseAttackBonusOverride = 12;
        controller.Stats.STR = 26;

        return controller;
    }

    private static CharacterController CreateWeakDefender(string name)
    {
        var defender = CreateTestCharacter(name);
        defender.Stats.BaseAttackBonusOverride = 0;
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

        // Stats.BaseAttackBonus is ignored for classed characters (CHR-068); the override makes the gap deterministic.
        controller.Stats.BaseAttackBonusOverride = 20;
        controller.Stats.STR = 30;
    }

    private static void ConfigureVeryWeakGrappler(CharacterController controller)
    {
        if (controller == null || controller.Stats == null)
            return;

        controller.Stats.BaseAttackBonusOverride = 0; // CHR-068: the plain setter is ignored for classed characters
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

        // Ensure deterministic success for the pinned character's Escape Artist check
        // (opposed by the pinner's grapple check of +30: BAB override 20, Str 30).
        pinned.Stats.InitializeSkills(pinned.Stats.CharacterClass, pinned.Stats.Level);
        pinned.Stats.Skills["Escape Artist"].Ranks = 60;
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
        AttackStepKind tripKind = wolf.GetManeuverSubstituteStepKind();
        bool canTrip = wolf.CanCommitAttack(tripKind, out string reason);
        bool tripCommitted = wolf.TryCommitManeuverSubstituteStep(-1, out _, out _, out _);

        Assert(paid && biteStep == 0 && tripKind == AttackStepKind.NaturalSequence && !canTrip && !tripCommitted
            && !string.IsNullOrEmpty(reason) && wolf.GetRemainingMainHandAttackSteps(tripKind) == 0,
            "A creature with one natural attack cannot trip after using it (the trip would replace that bite)");

        Cleanup(wolf);
    }

    private static void TestAttackSequenceTripReplacesOneNaturalAttack()
    {
        var bear = CreateNaturalAttacker("SequenceNaturalClawClawBite", ("Claw", 2), ("Bite", 1));

        // The live path of the PC wrapper and the NPC executor (CMB-102): a NaturalSequence step at the
        // BAB of the natural attack it replaces (the first claw, primary here: full BAB).
        bool trip = bear.TryCommitManeuverSubstituteStep(-1, out int tripBab, out int tripStep, out _);
        bool natural1 = bear.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
        bool natural2 = bear.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
        bool natural3 = bear.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out string reason);

        Assert(trip && tripStep == 0 && bear.GetManeuverSubstituteStepKind() == AttackStepKind.NaturalSequence
            && tripBab == bear.GetNaturalAttackStepBAB(0) && tripBab == bear.Stats.BaseAttackBonus
            && bear.ProgressiveAttackPool.MainHandBudget == 3,
            "A trip by a natural-weapon creature uses one step of its natural-attack budget, at the replaced claw's BAB");
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

    // ── Maneuvers replace any natural attack at that attack's bonus (CMB-102, owner decision 2026-10-07) ──
    // MM p.312: a primary natural attack uses the full attack bonus, a secondary one takes -5, or -2
    // with Multiattack (MM p.304). A trip, disarm, sunder or grapple that replaces a natural attack
    // rolls at that natural attack's BAB, for PCs and NPCs alike, and the other natural attacks stay.

    /// <summary>Bite (primary) and two claws (secondary), BAB through BaseAttackBonusOverride, no weapon.</summary>
    private static CharacterController CreateBiteClawsCreature(string name, int bab, bool multiattack)
    {
        var creature = CreateIterativeAttacker(name);
        creature.Stats.BaseAttackBonusOverride = bab;
        creature.Stats.NaturalAttacks.Clear();
        creature.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, IsPrimary = true });
        creature.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, IsPrimary = false, BonusDamageSource = DamageBonusSource.StrengthHalf });
        if (multiattack)
            creature.Stats.Feats.Add("Multiattack");
        return creature;
    }

    private static void TestNaturalAttackStepBabPrimarySecondaryMultiattack()
    {
        var plain = CreateBiteClawsCreature("NaturalStepBabPlain", 4, false);
        var multi = CreateBiteClawsCreature("NaturalStepBabMultiattack", 4, true);
        try
        {
            Assert(plain.GetNaturalAttackStepBAB(0) == 4 && plain.GetNaturalAttackStepBAB(1) == -1 && plain.GetNaturalAttackStepBAB(2) == -1,
                "Natural step BAB: primary bite +4, secondary claws +4-5 = -1 (MM p.312)");
            Assert(multi.GetNaturalAttackStepBAB(0) == 4 && multi.GetNaturalAttackStepBAB(1) == 2 && multi.GetNaturalAttackStepBAB(2) == 2,
                "Natural step BAB with Multiattack: primary +4, secondary claws +4-2 = +2 (MM p.304)");

            NaturalAttackDefinition claw = multi.Stats.GetNaturalAttackAtSequenceIndex(2);
            Assert(claw != null && claw.Name == "Claw" && multi.Stats.GetNaturalAttackAtSequenceIndex(3) == null
                && multi.Stats.GetNaturalAttackAtSequenceIndex(-1) == null,
                "Natural sequence index: bite, claw, claw; out of range is null");

            int multiBite = multi.Stats.GetNaturalAttackBonus(multi.Stats.GetNaturalAttackAtSequenceIndex(0));
            int plainBite = plain.Stats.GetNaturalAttackBonus(plain.Stats.GetNaturalAttackAtSequenceIndex(0));
            int plainClaw = plain.Stats.GetNaturalAttackBonus(plain.Stats.GetNaturalAttackAtSequenceIndex(1));
            Assert(multi.Stats.GetNaturalAttackBonus(claw) - multiBite == -2 && plainClaw - plainBite == -5,
                "The natural attack rolls use the same secondary penalty: -5, or -2 with Multiattack");
        }
        finally
        {
            Cleanup(plain, multi);
        }
    }

    private static void TestManeuverReplacesAnyNaturalAttackStep()
    {
        // BAB +4 is one iterative, but each of the three natural attacks can be replaced (shared core,
        // CharacterController.TryCommitManeuverSubstituteStep, used by the PC wrapper and the NPC executor).
        var plain = CreateBiteClawsCreature("NaturalSubstitutePlain", 4, false);
        var multi = CreateBiteClawsCreature("NaturalSubstituteMultiattack", 4, true);
        var mixed = CreateBiteClawsCreature("NaturalSubstituteMixed", 4, false);
        var fighter = CreateIterativeAttacker("NaturalSubstituteWeaponControl");
        try
        {
            Assert(plain.GetManeuverSubstituteStepKind() == AttackStepKind.NaturalSequence
                && fighter.GetManeuverSubstituteStepKind() == AttackStepKind.MainHand,
                "A natural-weapon creature's maneuver replaces a natural attack; a weapon user's an iterative");

            bool p0 = plain.TryCommitManeuverSubstituteStep(-1, out int pBab0, out int pStep0, out _);
            bool p1 = plain.TryCommitManeuverSubstituteStep(-1, out int pBab1, out int pStep1, out _);
            bool p2 = plain.TryCommitManeuverSubstituteStep(-1, out int pBab2, out int pStep2, out _);
            bool p3 = plain.TryCommitManeuverSubstituteStep(-1, out _, out _, out string pReason3);
            Assert(p0 && p1 && p2 && pStep0 == 0 && pStep1 == 1 && pStep2 == 2 && pBab0 == 4 && pBab1 == -1 && pBab2 == -1,
                $"Three maneuvers replace bite (+4), claw (-1) and claw (-1) past the single iterative (got {pBab0}, {pBab1}, {pBab2})");
            Assert(!p3 && !string.IsNullOrEmpty(pReason3) && plain.ProgressiveAttackPool.IsFullAttack,
                "A fourth maneuver is refused: the three natural attacks are spent (PHB p.141 Table 8-2 note 7)");

            multi.TryCommitManeuverSubstituteStep(-1, out int mBab0, out _, out _);
            bool m1 = multi.TryCommitManeuverSubstituteStep(-1, out int mBab1, out _, out _);
            Assert(mBab0 == 4 && m1 && mBab1 == 2,
                $"With Multiattack a maneuver replacing a secondary claw rolls at +2, not -1 (MM p.304; got {mBab1})");

            // The bite is made as an attack, then a maneuver replaces the next claw; one claw remains.
            bool bite = mixed.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
            bool clawTrip = mixed.TryCommitManeuverSubstituteStep(-1, out int mixedBab, out _, out _);
            bool lastClaw = mixed.CanCommitAttack(AttackStepKind.NaturalSequence, out _);
            bool lastClawBab = mixed.GetManeuverSubstituteBAB(2) == -1;
            bool namedPrimary = mixed.GetManeuverSubstituteBAB(2, naturalAttackIndex: 0) == 4;
            Assert(bite && clawTrip && mixedBab == -1 && lastClaw && lastClawBab && namedPrimary,
                "After the bite, a maneuver replaces a claw at -1 and the last claw is still available; a named natural attack sets the bonus");

            bool f1 = fighter.TryCommitManeuverSubstituteStep(-1, out int fBab0, out _, out _);
            bool f2 = fighter.TryCommitManeuverSubstituteStep(-1, out int fBab1, out _, out _);
            Assert(f1 && f2 && fBab0 == 11 && fBab1 == 6,
                "A weapon user's maneuvers still roll at the iterative BABs (+11, +6)");
        }
        finally
        {
            Cleanup(plain, multi, mixed, fighter);
        }
    }

    /// <summary>
    /// Makes <paramref name="pc"/> the GameManager's ActivePC (PC turn, current character, controllable)
    /// with an empty used-natural-attack set, by reflection on private fields (TST-006); Dispose restores
    /// everything. Enter returns null when a field is missing.
    /// </summary>
    private sealed class ActivePcScope : System.IDisposable
    {
        private readonly GameManager _gm;
        private readonly CharacterController _pc;
        private readonly object _turnService;
        private readonly FieldInfo _phaseField;
        private readonly FieldInfo _currentField;
        private readonly object _savedPhase;
        private readonly object _savedCurrent;
        private readonly bool _savedControllable;
        private readonly System.Collections.Generic.HashSet<int> _used;
        private readonly int[] _savedUsed;

        private ActivePcScope(GameManager gm, CharacterController pc, object turnService, FieldInfo phaseField, FieldInfo currentField,
            System.Collections.Generic.HashSet<int> used)
        {
            _gm = gm;
            _pc = pc;
            _turnService = turnService;
            _phaseField = phaseField;
            _currentField = currentField;
            _used = used;
            _savedPhase = phaseField.GetValue(gm);
            _savedCurrent = currentField.GetValue(turnService);
            _savedControllable = pc.IsControllable;
            _savedUsed = new int[used.Count];
            used.CopyTo(_savedUsed);

            pc.IsControllable = true;
            phaseField.SetValue(gm, GameManager.TurnPhase.PCTurn);
            currentField.SetValue(turnService, pc);
            used.Clear();
        }

        public static ActivePcScope Enter(GameManager gm, CharacterController pc)
        {
            if (gm == null || pc == null)
                return null;

            FieldInfo phaseField = typeof(GameManager).GetField("_currentPhase", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo turnServiceField = typeof(GameManager).GetField("_turnService", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo usedField = typeof(GameManager).GetField("_usedNaturalAttackSequenceIndices", BindingFlags.Instance | BindingFlags.NonPublic);
            object turnService = turnServiceField != null ? turnServiceField.GetValue(gm) : null;
            FieldInfo currentField = turnService != null
                ? turnService.GetType().GetField("_currentCharacter", BindingFlags.Instance | BindingFlags.NonPublic)
                : null;
            var used = usedField != null ? usedField.GetValue(gm) as System.Collections.Generic.HashSet<int> : null;
            if (phaseField == null || currentField == null || used == null)
                return null;

            return new ActivePcScope(gm, pc, turnService, phaseField, currentField, used);
        }

        public void Dispose()
        {
            _used.Clear();
            foreach (int index in _savedUsed)
                _used.Add(index);
            _currentField.SetValue(_turnService, _savedCurrent);
            _phaseField.SetValue(_gm, _savedPhase);
            if (_pc != null)
                _pc.IsControllable = _savedControllable;
        }
    }

    private static void TestPcManeuverReplacesNaturalAttackAtItsBonus()
    {
        // The PC maneuver wrapper (GameManager.TryConsumeTripAttackAction and the Trip button's bonus
        // and count) goes through the same core. As the active PC the creature gives up the first
        // natural attack not used this turn, and that attack is marked used for the natural buttons.
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; PC natural maneuver check needs Play mode");
            return;
        }

        MethodInfo consumeTrip = typeof(GameManager).GetMethod("TryConsumeTripAttackAction", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(consumeTrip != null, "GameManager.TryConsumeTripAttackAction is available for the PC natural maneuver test");
        if (consumeTrip == null)
            return;

        foreach (bool multiattack in new[] { false, true })
        {
            int secondary = multiattack ? 2 : -1;
            string tag = multiattack ? " (Multiattack)" : string.Empty;
            var pc = CreateBiteClawsCreature(multiattack ? "PcNaturalTripMultiattack" : "PcNaturalTrip", 4, multiattack);
            ActivePcScope scope = null;
            try
            {
                scope = ActivePcScope.Enter(gm, pc);
                Assert(scope != null && gm.ActivePC == pc, "The test creature is the active PC (reflection on GameManager/TurnService fields)" + tag);
                if (scope == null)
                    continue;

                Assert(gm.CanUseTripAttackOption(pc) && gm.GetRemainingTripAttackActions(pc) == 3 && gm.GetCurrentTripAttackBonus(pc) == 4,
                    "PC Trip button: 3 trips available at BAB +4 (the primary bite) for a BAB +4 bite/claw/claw creature" + tag);

                var babs = new System.Collections.Generic.List<int>();
                for (int i = 0; i < 3; i++)
                {
                    object[] args = { pc, 0, 0, null };
                    if ((bool)consumeTrip.Invoke(gm, args))
                        babs.Add((int)args[1]);
                    if (i == 0)
                        Assert(gm.IsNaturalAttackSequenceIndexUsed(pc, 0) && !gm.IsNaturalAttackSequenceIndexUsed(pc, 1)
                            && gm.GetCurrentTripAttackBonus(pc) == secondary && gm.GetRemainingTripAttackActions(pc) == 2,
                            $"PC Trip after one trip: the bite is marked used, next trip at the claw's BAB {CharacterStats.FormatMod(secondary)}, 2 left" + tag);
                }

                object[] fourth = { pc, 0, 0, null };
                bool fourthOk = (bool)consumeTrip.Invoke(gm, fourth);
                Assert(babs.Count == 3 && babs[0] == 4 && babs[1] == secondary && babs[2] == secondary && !fourthOk && !gm.CanUseTripAttackOption(pc)
                    && gm.IsNaturalAttackSequenceIndexUsed(pc, 1) && gm.IsNaturalAttackSequenceIndexUsed(pc, 2) && !gm.CanUseNaturalAttackOption(pc),
                    $"PC trips replace bite, claw, claw at +4, {CharacterStats.FormatMod(secondary)}, {CharacterStats.FormatMod(secondary)}, all marked used; a fourth is refused" + tag);
            }
            catch (System.Exception ex)
            {
                System.Exception inner = ex.InnerException ?? ex;
                Assert(false, $"PC natural maneuver check threw {inner.GetType().Name}: {inner.Message}" + tag);
            }
            finally
            {
                scope?.Dispose();
                Cleanup(pc);
            }
        }

        // Out of order: the first claw (index 1) was used through its natural-attack button first.
        // The Trip button then offers the first unused natural attack, the bite at +4, marks it, and
        // one natural attack is left.
        var outOfOrder = CreateBiteClawsCreature("PcNaturalTripOutOfOrder", 4, false);
        ActivePcScope orderScope = null;
        try
        {
            orderScope = ActivePcScope.Enter(gm, outOfOrder);
            if (orderScope != null)
            {
                bool clawCommitted = outOfOrder.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
                gm.Combat_MarkNaturalAttackSequenceIndexUsed(1);
                bool offeredAtBite = gm.GetCurrentTripAttackBonus(outOfOrder) == 4 && gm.GetRemainingTripAttackActions(outOfOrder) == 2;

                object[] args = { outOfOrder, 0, 0, null };
                bool tripped = (bool)consumeTrip.Invoke(gm, args);
                Assert(clawCommitted && offeredAtBite && tripped && (int)args[1] == 4
                    && gm.IsNaturalAttackSequenceIndexUsed(outOfOrder, 0) && !gm.IsNaturalAttackSequenceIndexUsed(outOfOrder, 2)
                    && gm.GetRemainingTripAttackActions(outOfOrder) == 1 && gm.CanUseNaturalAttackOption(outOfOrder),
                    "PC Trip after the first claw was used: the trip replaces the bite at +4, marks it, and one claw is left");
            }
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"PC out-of-order natural maneuver check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            orderScope?.Dispose();
            Cleanup(outOfOrder);
        }
    }

    private static void TestNaturalWeaponDisarmIsArmed()
    {
        // MM p.312: a creature attacking with natural weapons is armed. PHB p.155: an armed disarmer
        // knocks the weapon to the defender's square and only an unarmed one takes it in hand; the
        // unarmed-strike light-weapon -4 does not apply to a natural weapon. The creature keeps its
        // other natural attacks after disarming with one (CMB-102).
        var bear = CreateBiteClawsCreature("NaturalDisarmBear", 20, false);
        var unarmed = CreateTestCharacter("NaturalDisarmUnarmedControl", "Fighter");
        var defender = CreateWeakDefender("NaturalDisarmDefender");
        unarmed.Stats.BaseAttackBonusOverride = 20;
        defender.Stats.STR = 1;
        bear.GridPosition = new Vector2Int(12, 12);
        defender.GridPosition = new Vector2Int(13, 12);
        ItemData sword = ItemDatabase.CloneItem(ItemID.WeaponLongsword);
        defender.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(sword, EquipSlot.RightHand);
        ActivePcScope scope = null;
        try
        {
            MethodInfo rollDisarm = typeof(CharacterController).GetMethod("RollDisarmCheck", BindingFlags.Static | BindingFlags.NonPublic);
            Assert(rollDisarm != null, "RollDisarmCheck is available for the natural disarm test");
            if (rollDisarm != null)
            {
                int bearMod = GetDisarmModifier(rollDisarm.Invoke(null, new object[] { bear, defender, null, sword, EquipSlot.RightHand, 0, string.Empty, null, 0 }), "Attacker");
                int unarmedMod = GetDisarmModifier(rollDisarm.Invoke(null, new object[] { unarmed, defender, null, sword, EquipSlot.RightHand, 0, string.Empty, null, 0 }), "Attacker");
                Assert(bearMod - unarmedMod == 4,
                    $"A natural-weapon disarmer takes no unarmed-strike -4 (natural {bearMod}, unarmed {unarmedMod}; PHB p.155, MM p.312)");
            }

            bool committed = bear.TryCommitManeuverSubstituteStep(-1, out int disarmBab, out _, out _);
            SpecialAttackResult result = bear.ExecuteSpecialAttack(SpecialAttackType.Disarm, defender, disarmAttackBonusOverride: disarmBab);
            SquareGrid grid = GameManager.Instance != null ? GameManager.Instance.Grid : SquareGrid.Instance;
            SquareCell cell = grid != null ? grid.GetCell(defender.GridPosition) : null;
            bool onGround = false;
            if (cell != null)
            {
                foreach (ItemData item in cell.GroundItems)
                    onGround |= item == sword;
                cell.RemoveGroundItem(sword);
            }

            Assert(committed && result != null && result.Success && defender.GetEquippedMainWeapon() == null
                && bear.GetEquippedMainWeapon() == null && result.Log.Contains("drops to the ground"),
                "A natural-weapon disarm knocks the weapon down; the creature does not take it in hand (PHB p.155)");
            Assert(cell == null || onGround, "The disarmed weapon lies in the defender's square");
            Assert(bear.UsesInnateNaturalAttackSequence() && bear.GetMeleeAttackStepKind() == AttackStepKind.NaturalSequence
                && bear.CanCommitAttack(AttackStepKind.NaturalSequence, out _) && bear.GetRemainingMainHandAttackSteps(AttackStepKind.NaturalSequence) == 2,
                "After disarming with one natural attack, the two other natural attacks remain (NPC side)");

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                scope = ActivePcScope.Enter(gm, bear);
                if (scope != null)
                {
                    gm.Combat_MarkNaturalAttackSequenceIndexUsed(0);
                    Assert(gm.CanUseNaturalAttackOption(bear) && gm.GetRemainingTripAttackActions(bear) == 2,
                        "After disarming with one natural attack, the PC natural-attack and maneuver buttons still offer the other two");
                }
            }
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Natural disarm check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            scope?.Dispose();
            Cleanup(bear, unarmed, defender);
        }
    }

    private static void TestNaturalWeaponCreatureCannotSunder()
    {
        // ResolveSunder needs a manufactured weapon, so the PC button, the AI and the NPC executor
        // refuse sunder for a natural-weapon creature before any attack step is spent (CMB-102;
        // whether natural weapons may sunder is an open owner question).
        var bear = CreateBiteClawsCreature("NaturalSunderBear", 6, false);
        var defender = CreateWeakDefender("NaturalSunderDefender");
        var fighter = CreateIterativeAttacker("NaturalSunderFighter");
        bear.GridPosition = new Vector2Int(16, 12);
        defender.GridPosition = new Vector2Int(17, 12);
        bear.IsControllable = false;
        defender.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
        fighter.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
        try
        {
            Assert(!bear.CanSunderWithMainWeapon(out string reason) && !string.IsNullOrEmpty(reason) && fighter.CanSunderWithMainWeapon(out _),
                "Sunder needs a weapon: refused for a natural-weapon creature, allowed for a sword fighter");

            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.Log("  [SKIP] GameManager.Instance is null; PC/NPC sunder gates need Play mode");
                return;
            }

            Assert(!gm.CanUseSunderAttackOption(bear) && gm.GetRemainingSunderAttackActions(bear) == 0 && gm.CanUseSunderAttackOption(fighter),
                "PC Sunder button is not offered to a natural-weapon creature");

            bool npcSunder = gm.TryNPCSpecialAttackByTypeForAI(bear, defender, SpecialAttackType.Sunder);
            Assert(!npcSunder && bear.ProgressiveAttackPool.MainHandStepsUsed == 0 && bear.Actions.HasStandardAction
                && defender.GetEquippedMainWeapon() != null,
                "NPC sunder by a natural-weapon creature is refused and spends no attack step");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Natural sunder check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            Cleanup(bear, defender, fighter);
        }
    }

    private static void TestNpcNaturalSequenceMakesOneSubstitute()
    {
        // AI limit pending AI-035: a Grappler-profile claw/claw/bite creature (the owlbear's profile)
        // against a standing armed target prefers a trip before every step. With the trip's touch
        // attack forced to a natural 1 the trip fails and the target stays standing; the NPC still
        // makes only one substitute and rolls its other two natural attacks. Runs the real AIService
        // evaluation through the NPC melee loop.
        GameManager gm = GameManager.Instance;
        AIService ai = gm != null ? gm.GetComponent<AIService>() : null;
        if (gm == null || ai == null)
        {
            Debug.Log("  [SKIP] GameManager/AIService is null; NPC natural substitute limit needs Play mode");
            return;
        }

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        CharacterController npc = null;
        CharacterController target = null;
        DND35.AI.Profiles.GrapplerAIProfile profile = null;
        try
        {
            ScenarioHooks.RollFilter = (sides, ctx, natural) => ctx == "Trip touch attack" ? 1 : natural;
            npc = CreateNaturalAttacker("NpcNaturalGrapplerOwlbear", ("Claw", 2), ("Bite", 1));
            npc.Stats.BaseAttackBonusOverride = 4;
            npc.Stats.Feats.Add("Improved Trip"); // no AoO from the target (PHB p.96)
            npc.IsControllable = false;
            profile = ScriptableObject.CreateInstance<DND35.AI.Profiles.GrapplerAIProfile>();
            npc.aiProfile = profile;
            target = CreateWeakDefender("NpcNaturalGrapplerTarget");
            target.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
            npc.GridPosition = new Vector2Int(20, 12);
            target.GridPosition = new Vector2Int(21, 12);
            target.Stats.AdjustMaxHP(500);
            target.Stats.CurrentHP += 500;

            Assert(profile.GetPreferredManeuver(npc, target) == SpecialAttackType.Trip,
                "Grappler profile prefers a trip against a standing target");

            FullAttackResult sequence = RunNpcMeleeSequence(gm, npc, target, ai.CreateMeleeStepManeuverEvaluator(profile), out int maneuvers);
            Assert(maneuvers == 1 && sequence.Attacks.Count == 2 && npc.ProgressiveAttackPool.MainHandStepsUsed == 3,
                $"NPC natural sequence: one failed trip, then the other two natural attacks are rolled, not more trips (maneuvers {maneuvers}, attacks {sequence.Attacks.Count}; AI-035)");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"NPC natural substitute limit check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            Cleanup(npc, target);
            if (profile != null)
                Object.DestroyImmediate(profile);
        }
    }

    // ── AI maneuver stopgap (owner decision 2026-10-07, CMB-102 open item 7, AI-060) ──
    // An AI decision limit, not a rule: once a maneuver succeeds the AI attacks with the remaining
    // steps, and a failed maneuver type is not retried against the same target that turn. Both tests
    // run the real AIService evaluation (CreateMeleeStepManeuverEvaluator) through the NPC melee loop,
    // with a Humanoid profile (trip, then disarm an armed target) and with no profile (the legacy
    // chooser: trip, then disarm at STR mod 3+, then grapple at STR mod 4+).

    /// <summary>
    /// A BAB +11 fighter (+11/+6/+1, STR 26) with a longsword, AI-run, at (x, y). No Improved Trip on
    /// purpose: its free attack after a trip that lands (PHB p.96) is not built yet (CMB-079) and would add
    /// an attack, so these tests count only the iterative steps the AI chooses. The trip's attack of
    /// opportunity (PHB p.158) is avoided on the target's side instead (<see cref="CreateArmedStopgapTarget"/>).
    /// </summary>
    private static CharacterController CreateAiTripper(string name, DND35.AI.AIProfile profile, int x, int y)
    {
        CharacterController npc = CreateIterativeAttacker(name);
        npc.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
        npc.IsControllable = false;
        npc.aiProfile = profile;
        npc.GridPosition = new Vector2Int(x, y);
        return npc;
    }

    /// <summary>
    /// A weak defender holding a longsword (so a disarm is on the table), 500 extra HP, at (x, y). It makes
    /// no attacks of opportunity, so the AI tripper's trip provokes nothing without Improved Trip.
    /// </summary>
    private static CharacterController CreateArmedStopgapTarget(string name, int x, int y)
    {
        CharacterController target = CreateWeakDefender(name);
        target.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
        target.GridPosition = new Vector2Int(x, y);
        target.Stats.AdjustMaxHP(500);
        target.Stats.CurrentHP += 500;
        target.Stats.CanMakeAttacksOfOpportunity = false;
        return target;
    }

    private static void TestAiManeuverStopgapTripSucceedsThenAttacks()
    {
        GameManager gm = GameManager.Instance;
        AIService ai = gm != null ? gm.GetComponent<AIService>() : null;
        if (gm == null || ai == null)
        {
            Debug.Log("  [SKIP] GameManager/AIService is null; AI maneuver stopgap check needs Play mode");
            return;
        }

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        System.Action<CharacterController, CharacterController, SpecialAttackType, SpecialAttackResult> savedManeuver = ScenarioHooks.ManeuverResolved;
        var maneuverTypes = new System.Collections.Generic.List<SpecialAttackType>();
        try
        {
            // The trip lands: touch attack natural 20, Strength check 20 against a defense roll of 1.
            ScenarioHooks.RollFilter = (sides, ctx, natural) =>
                ctx == "Trip touch attack" || ctx == "Trip Strength check" ? 20
                : ctx == "Trip defense check" ? 1
                : natural;
            ScenarioHooks.ManeuverResolved = (attacker, target, type, result) => maneuverTypes.Add(type);

            int row = 0;
            foreach (bool withProfile in new[] { true, false })
            {
                CharacterController npc = null;
                CharacterController target = null;
                DND35.AI.Profiles.HumanoidAIProfile profile = null;
                string tag = withProfile ? " (Humanoid profile)" : " (no profile, legacy chooser)";
                try
                {
                    if (withProfile)
                        profile = ScriptableObject.CreateInstance<DND35.AI.Profiles.HumanoidAIProfile>();
                    npc = CreateAiTripper(withProfile ? "StopgapTripHumanoid" : "StopgapTripLegacy", profile, 24, 4 + row);
                    target = CreateArmedStopgapTarget(withProfile ? "StopgapTripHumanoidTarget" : "StopgapTripLegacyTarget", 25, 4 + row);
                    row += 3;
                    maneuverTypes.Clear();

                    FullAttackResult sequence = RunNpcMeleeSequence(gm, npc, target, ai.CreateMeleeStepManeuverEvaluator(profile), out int maneuvers);

                    Assert(maneuvers == 1 && maneuverTypes.Count == 1 && maneuverTypes[0] == SpecialAttackType.Trip
                        && target.HasCondition(CombatConditionType.Prone),
                        $"AI stopgap: the first step is a trip that lands and no further maneuver follows (maneuvers {maneuvers}: {string.Join(", ", maneuverTypes)}; AI-060)" + tag);
                    Assert(sequence.Attacks.Count == 2 && sequence.Attacks[0].BreakdownBAB == 6 && sequence.Attacks[1].BreakdownBAB == 1
                        && npc.ProgressiveAttackPool.MainHandStepsUsed == 3,
                        $"AI stopgap: after the trip succeeds the remaining steps are attacks at +6 and +1 on the prone target (attacks {sequence.Attacks.Count}; AI-060)" + tag);
                    Assert(target.GetEquippedMainWeapon() != null,
                        "AI stopgap: the armed target is not disarmed after the successful trip" + tag);

                    // Control: without the stopgap the evaluation would pick another maneuver now.
                    SpecialAttackType? wouldPick = withProfile ? profile.GetPreferredManeuver(npc, target) : gm.PeekNPCFallbackManeuverForAI(npc, target);
                    Assert(wouldPick == SpecialAttackType.Disarm && npc.AIManeuverMemory.ManeuverSucceededThisTurn,
                        $"AI stopgap control: the evaluation alone would now pick a disarm ({(wouldPick.HasValue ? wouldPick.Value.ToString() : "none")}); the turn memory holds the success" + tag);

                    npc.StartNewTurn();
                    Assert(!npc.AIManeuverMemory.ManeuverSucceededThisTurn,
                        "AI stopgap: the turn memory is cleared at the creature's next turn start" + tag);
                }
                catch (System.Exception ex)
                {
                    System.Exception inner = ex.InnerException ?? ex;
                    Assert(false, $"AI maneuver stopgap (trip succeeds) check threw {inner.GetType().Name}: {inner.Message}" + tag);
                }
                finally
                {
                    Cleanup(npc, target);
                    if (profile != null)
                        Object.DestroyImmediate(profile);
                }
            }
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            ScenarioHooks.ManeuverResolved = savedManeuver;
        }
    }

    private static void TestAiManeuverStopgapFailedTripNotRetried()
    {
        GameManager gm = GameManager.Instance;
        AIService ai = gm != null ? gm.GetComponent<AIService>() : null;
        if (gm == null || ai == null)
        {
            Debug.Log("  [SKIP] GameManager/AIService is null; AI maneuver stopgap check needs Play mode");
            return;
        }

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        System.Action<CharacterController, CharacterController, SpecialAttackType, SpecialAttackResult> savedManeuver = ScenarioHooks.ManeuverResolved;
        var maneuverTypes = new System.Collections.Generic.List<SpecialAttackType>();
        try
        {
            // Every trip touch attack is a natural 1, so every trip fails and the target stays standing.
            ScenarioHooks.RollFilter = (sides, ctx, natural) => ctx == "Trip touch attack" ? 1 : natural;
            ScenarioHooks.ManeuverResolved = (attacker, target, type, result) => maneuverTypes.Add(type);

            int row = 0;
            foreach (bool withProfile in new[] { true, false })
            {
                CharacterController npc = null;
                CharacterController target = null;
                CharacterController other = null;
                DND35.AI.Profiles.HumanoidAIProfile profile = null;
                string tag = withProfile ? " (Humanoid profile)" : " (no profile, legacy chooser)";
                try
                {
                    if (withProfile)
                        profile = ScriptableObject.CreateInstance<DND35.AI.Profiles.HumanoidAIProfile>();
                    npc = CreateAiTripper(withProfile ? "StopgapFailHumanoid" : "StopgapFailLegacy", profile, 28, 4 + row);
                    target = CreateArmedStopgapTarget(withProfile ? "StopgapFailHumanoidTarget" : "StopgapFailLegacyTarget", 29, 4 + row);
                    other = CreateArmedStopgapTarget(withProfile ? "StopgapFailHumanoidOther" : "StopgapFailLegacyOther", 29, 5 + row);
                    row += 3;
                    maneuverTypes.Clear();

                    FullAttackResult sequence = RunNpcMeleeSequence(gm, npc, target, ai.CreateMeleeStepManeuverEvaluator(profile), out int maneuvers);

                    int trips = maneuverTypes.FindAll(t => t == SpecialAttackType.Trip).Count;
                    Assert(trips == 1 && maneuvers == 1 && !target.HasCondition(CombatConditionType.Prone),
                        $"AI stopgap: a failed trip is not retried against the same target that turn (trips {trips}, maneuvers {maneuvers}: {string.Join(", ", maneuverTypes)}; AI-060)" + tag);
                    Assert(sequence.Attacks.Count == 2 && sequence.Attacks[0].BreakdownBAB == 6 && sequence.Attacks[1].BreakdownBAB == 1,
                        $"AI stopgap: after the failed trip the remaining steps are attacks at +6 and +1 (attacks {sequence.Attacks.Count})" + tag);

                    DND35.AI.AIManeuverTurnMemory memory = npc.AIManeuverMemory;
                    Assert(memory.HasFailed(target, SpecialAttackType.Trip)
                        && !memory.ManeuverSucceededThisTurn
                        && memory.Forbids(target, SpecialAttackType.Trip, out _)
                        && !memory.Forbids(target, SpecialAttackType.Disarm, out _)
                        && !memory.Forbids(other, SpecialAttackType.Trip, out _)
                        && !memory.Forbids(target, SpecialAttackType.CoupDeGrace, out _),
                        "AI stopgap: the failure forbids only a trip against that target; a disarm of it, a trip of another target and a coup de grace stay open" + tag);

                    npc.StartNewTurn();
                    Assert(!npc.AIManeuverMemory.HasFailed(target, SpecialAttackType.Trip),
                        "AI stopgap: the failed trip is forgotten at the creature's next turn start" + tag);
                }
                catch (System.Exception ex)
                {
                    System.Exception inner = ex.InnerException ?? ex;
                    Assert(false, $"AI maneuver stopgap (trip fails) check threw {inner.GetType().Name}: {inner.Message}" + tag);
                }
                finally
                {
                    Cleanup(npc, target, other);
                    if (profile != null)
                        Object.DestroyImmediate(profile);
                }
            }
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            ScenarioHooks.ManeuverResolved = savedManeuver;
        }
    }

    // ── Haste's extra attack with natural weapons (PHB p.239; owner decision 2026-10-07, CMB-106) ──
    // A hasted creature fighting with its natural attacks makes ONE extra attack, on a full attack,
    // with ONE natural weapon of its choice at that weapon's normal bonus. It is one more step of the
    // shared natural sequence (CharacterController.GetNaturalAttackStepBudget), after the natural
    // attacks. A weapon user keeps the iterative Haste step, and nobody gets both.

    private static CharacterController CreateHastedBiteClaws(string name, int bab)
    {
        var creature = CreateBiteClawsCreature(name, bab, false);
        creature.ApplyHasteEffect(10, null);
        return creature;
    }

    private static CharacterController CreateHasteTarget(string name)
    {
        var target = CreateWeakDefender(name);
        target.GridPosition = new Vector2Int(1, 0);
        target.Stats.AdjustMaxHP(500);
        target.Stats.CurrentHP += 500;
        return target;
    }

    private static int AttackModifier(CombatResult attack) => attack != null ? attack.TotalRoll - attack.DieRoll : int.MinValue;

    private static void TestHasteAddsOneNaturalAttackStep()
    {
        var hasted = CreateHastedBiteClaws("HasteNaturalBudget", 4);
        var plain = CreateBiteClawsCreature("HasteNaturalBudgetControl", 4, false);
        try
        {
            Assert(hasted.HasHasteExtraNaturalAttack() && hasted.GetNaturalAttackStepBudget() == 4
                && hasted.GetMainHandAttackBudget(AttackStepKind.NaturalSequence) == 4
                && hasted.GetHasteExtraNaturalStepIndex() == 3
                && hasted.IsHasteExtraNaturalStep(3) && !hasted.IsHasteExtraNaturalStep(2)
                && !plain.HasHasteExtraNaturalAttack() && plain.GetNaturalAttackStepBudget() == 3 && !plain.IsHasteExtraNaturalStep(3),
                "Haste adds one step to a bite/claw/claw natural sequence (4 steps, the 4th is the Haste one); without Haste 3 (CMB-106)");

            bool all4 = true;
            for (int i = 0; i < 4; i++)
                all4 &= hasted.TryCommitAttack(AttackStepKind.NaturalSequence, out int step, out _) && step == i;
            bool fifth = hasted.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out string reason);
            Assert(all4 && !fifth && !string.IsNullOrEmpty(reason) && hasted.ProgressiveAttackPool.IsFullAttack,
                "A hasted natural creature commits four natural steps on a full attack, and a fifth is refused");

            hasted.StartNewTurn();
            hasted.Actions.UseMoveAction();
            bool first = hasted.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
            bool second = hasted.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
            Assert(first && !second && hasted.GetRemainingMainHandAttackSteps(AttackStepKind.NaturalSequence) == 0,
                "A hasted natural creature that moved makes one attack: Haste's extra attack needs a full attack (PHB p.143, p.239)");
        }
        finally
        {
            Cleanup(hasted, plain);
        }
    }

    private static void TestHasteNaturalStepUsesChosenNaturalAttack()
    {
        var bear = CreateHastedBiteClaws("HasteNaturalStepChoice", 4);
        var target = CreateHasteTarget("HasteNaturalStepChoiceTarget");
        try
        {
            CombatResult bite = bear.ResolveAttackSequenceStep(target, AttackStepKind.NaturalSequence, 0,
                false, 0, null, null, null, 0, out _);
            CombatResult claw = bear.ResolveAttackSequenceStep(target, AttackStepKind.NaturalSequence, 1,
                false, 0, null, null, null, 0, out _);
            Assert(bite != null && claw != null && bite.WeaponName == "Bite" && claw.WeaponName == "Claw"
                && AttackModifier(bite) - AttackModifier(claw) == 5 && !bear.ProgressiveAttackPool.HasteExtraNaturalAttackUsed,
                "Natural steps 0 and 1 are the bite and a claw, 5 apart (secondary -5, MM p.312); no Haste attack used yet");

            CombatResult hasteClaw = bear.ResolveAttackSequenceStep(target, AttackStepKind.NaturalSequence, 3,
                false, 0, null, null, null, 0, out string hasteClawLabel, 1);
            Assert(hasteClaw != null && hasteClaw.WeaponName == "Claw" && AttackModifier(hasteClaw) == AttackModifier(claw)
                && hasteClawLabel != null && hasteClawLabel.Contains("Haste") && bear.ProgressiveAttackPool.HasteExtraNaturalAttackUsed
                && !bear.CanUseHasteExtraNaturalAttack(),
                "The Haste step with the claw chosen is a claw at the claw's normal bonus, labelled Haste, and marks the Haste attack used");

            bear.StartNewTurn();
            CombatResult hasteDefault = bear.ResolveAttackSequenceStep(target, AttackStepKind.NaturalSequence, 3,
                false, 0, null, null, null, 0, out _);
            Assert(hasteDefault != null && hasteDefault.WeaponName == "Bite" && AttackModifier(hasteDefault) == AttackModifier(bite)
                && bear.GetDefaultHasteNaturalAttackIndex() == 0,
                "With no choice the Haste step uses the natural attack with the highest bonus (the bite) at its normal bonus");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Haste natural step check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Cleanup(bear, target);
        }
    }

    private static void TestHasteNaturalFullAttackAddsOneAttack()
    {
        var bear = CreateHastedBiteClaws("HasteNaturalFullAttack", 4);
        var plain = CreateBiteClawsCreature("HasteNaturalFullAttackControl", 4, false);
        var target = CreateHasteTarget("HasteNaturalFullAttackTarget");
        try
        {
            int plannedBefore = bear.GetPlannedFullAttackCount();
            FullAttackResult full = bear.FullAttack(target, false, 0, null);
            bool hasteUsedAfterFull = bear.ProgressiveAttackPool.HasteExtraNaturalAttackUsed;
            FullAttackResult sameTurn = bear.FullAttack(target, false, 0, null, hasteNaturalAttackIndex: 2);
            bear.StartNewTurn();
            FullAttackResult chosen = bear.FullAttack(target, false, 0, null, hasteNaturalAttackIndex: 2);
            FullAttackResult control = plain.FullAttack(target, false, 0, null);
            Assert(full.Attacks.Count == 4 && full.Attacks[3].WeaponName == "Bite" && full.AttackLabels[3].Contains("Haste")
                && AttackModifier(full.Attacks[3]) == AttackModifier(full.Attacks[0])
                && chosen.Attacks.Count == 4 && chosen.Attacks[3].WeaponName == "Claw"
                && control.Attacks.Count == 3
                && plannedBefore == 4 && plain.GetPlannedFullAttackCount() == 3,
                "A hasted natural full attack (PC Full Attack button, pounce) is bite, claw, claw plus one Haste attack (the bite by default, or the chosen claw); unhasted 3");
            Assert(hasteUsedAfterFull && sameTurn.Attacks.Count == 3 && !sameTurn.AttackLabels.Exists(l => l.Contains("Haste")),
                "FullAttack marks the Haste natural attack used, so a second natural routine in the same turn has no Haste attack");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Haste natural full attack check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Cleanup(bear, plain, target);
        }
    }

    private static void TestHasteNaturalNotInGrappleRoutine()
    {
        // Each grapple attack action of a natural-weapon creature runs its natural routine
        // (ResolveNaturalAttackRoutineWhileGrappling). Haste's extra natural attack is one attack of a full
        // attack (PHB p.239), never part of each routine, and a used Haste attack is not made again.
        MethodInfo routine = typeof(CharacterController).GetMethod("ResolveNaturalAttackRoutineWhileGrappling", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(routine != null, "CharacterController.ResolveNaturalAttackRoutineWhileGrappling is available for the Haste grapple test");
        if (routine == null)
            return;

        var bear = CreateHastedBiteClaws("HasteNaturalGrappleRoutine", 4);
        var target = CreateHasteTarget("HasteNaturalGrappleRoutineTarget");
        try
        {
            var first = (SpecialAttackResult)routine.Invoke(bear, new object[] { target, "Attack" });
            var second = (SpecialAttackResult)routine.Invoke(bear, new object[] { target, "Attack" });
            Assert(first != null && second != null && first.Log.Contains("Natural attacks: 3 (") && second.Log.Contains("Natural attacks: 3 (")
                && !first.Log.Contains("(Haste,") && !second.Log.Contains("(Haste,") && !bear.ProgressiveAttackPool.HasteExtraNaturalAttackUsed,
                "A hasted bite/claw/claw creature makes 3 natural attacks per grapple routine, never the Haste attack (CMB-106)");

            CombatResult hasteStep = bear.ResolveAttackSequenceStep(target, AttackStepKind.NaturalSequence, 3,
                false, 0, null, null, null, 0, out _);
            CombatResult hasteAgain = bear.ResolveAttackSequenceStep(target, AttackStepKind.NaturalSequence, 3,
                false, 0, null, null, null, 0, out _);
            FullAttackResult afterUsed = bear.FullAttack(target, false, 0, null);
            Assert(hasteStep != null && hasteAgain == null && afterUsed.Attacks.Count == 3,
                "Once the Haste natural attack is made, the Haste step resolves nothing and a natural routine has 3 attacks");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Haste natural grapple routine check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            Cleanup(bear, target);
        }
    }

    private static void TestHasteWeaponAndMixedAttackersGetOneHasteAttack()
    {
        // Manufactured-weapon Haste keeps working, and a creature with natural attacks that fights with a
        // weapon gets the iterative Haste step only, never a second Haste attack with a natural weapon.
        var fighter = CreateIterativeAttacker("HasteWeaponFighter"); // BAB +11: +11/+6/+1
        var mixed = CreateBiteClawsCreature("HasteMixedWeaponNatural", 6, false); // BAB +6: +6/+1
        var target = CreateHasteTarget("HasteWeaponTarget");
        try
        {
            fighter.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
            mixed.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
            int fighterBefore = fighter.GetIterativeAttackCount();
            fighter.ApplyHasteEffect(10, null);
            mixed.ApplyHasteEffect(10, null);

            FullAttackResult fighterFull = fighter.FullAttack(target, false, 0, null);
            Assert(fighterBefore == 3 && fighter.GetIterativeAttackCount() == 4 && fighter.GetMainHandAttackBudget(AttackStepKind.MainHand) == 4
                && !fighter.HasHasteExtraNaturalAttack() && fighterFull.Attacks.Count == 4 && fighterFull.Attacks[3].BreakdownBAB == 11,
                "Weapon Haste is unchanged: BAB +11 gets +11/+6/+1 plus one attack at +11 (PHB p.239)");

            FullAttackResult mixedFull = mixed.FullAttack(target, false, 0, null);
            Assert(mixed.GetEquippedMainWeapon() != null && !mixed.UsesInnateNaturalAttackSequence() && !mixed.HasHasteExtraNaturalAttack()
                && mixed.GetMainHandAttackBudget(AttackStepKind.MainHand) == 3 && mixedFull.Attacks.Count == 3
                && mixedFull.Attacks.TrueForAll(a => a.WeaponName != "Bite" && a.WeaponName != "Claw"),
                "A hasted creature with natural attacks that fights with a weapon gets +6/+1 plus one Haste weapon attack, and no Haste natural attack");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Haste weapon and mixed attacker check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Cleanup(fighter, mixed, target);
        }
    }

    private static void TestHasteNaturalManeuverReplacesHasteStep()
    {
        // A trip may replace Haste's extra natural attack too (PHB p.141 Table 8-2 note 7), at the bonus
        // of the natural attack it would use (the bite here), and that uses up the Haste attack.
        var bear = CreateHastedBiteClaws("HasteNaturalTripStep", 4);
        try
        {
            bool natural3 = true;
            for (int i = 0; i < 3; i++)
                natural3 &= bear.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
            bool trip = bear.TryCommitManeuverSubstituteStep(-1, out int tripBab, out int tripStep, out _);
            bool again = bear.TryCommitManeuverSubstituteStep(-1, out _, out _, out _);
            Assert(natural3 && trip && tripStep == 3 && tripBab == 4 && bear.ProgressiveAttackPool.HasteExtraNaturalAttackUsed && !again
                && bear.GetNaturalAttackStepBAB(3) == 4,
                "After bite, claw, claw a trip takes the Haste step at the bite's BAB +4, uses up the Haste attack, and nothing is left");
        }
        finally
        {
            Cleanup(bear);
        }
    }

    private static void TestAiHasteNaturalAttackChoice()
    {
        var plain = CreateHastedBiteClaws("AiHasteChoicePlain", 4);
        var grabber = CreateHastedBiteClaws("AiHasteChoiceGrab", 4);
        var stinger = CreateIterativeAttacker("AiHasteChoiceSting");
        var clawBite = CreateIterativeAttacker("AiHasteChoiceEqualBonus");
        var living = CreateHasteTarget("AiHasteChoiceLiving");
        var undead = CreateHasteTarget("AiHasteChoiceUndead");
        DND35.AI.AIProfile profile = null;
        try
        {
            undead.Stats.CreatureType = "Undead";
            grabber.Stats.HasImprovedGrab = true;
            grabber.Stats.ImprovedGrabTriggerAttackName = "Claw";

            stinger.Stats.BaseAttackBonusOverride = 4;
            stinger.Stats.NaturalAttacks.Clear();
            stinger.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, IsPrimary = true });
            stinger.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Sting", DamageDice = 4, DamageCount = 1, Count = 1, IsPrimary = false, PoisonOnHitId = "test_poison" });
            stinger.ApplyHasteEffect(10, null);

            clawBite.Stats.BaseAttackBonusOverride = 4;
            clawBite.Stats.NaturalAttacks.Clear();
            clawBite.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, IsPrimary = true });
            clawBite.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, IsPrimary = true });
            clawBite.ApplyHasteEffect(10, null);

            Assert(DND35.AI.NaturalAttackChoice.ChooseHasteExtraAttackIndex(plain, living) == 0,
                "AI Haste pick: no riders, the highest attack bonus wins (the primary bite over the secondary claws)");
            Assert(DND35.AI.NaturalAttackChoice.ChooseHasteExtraAttackIndex(clawBite, living) == 2
                && clawBite.GetDefaultHasteNaturalAttackIndex() == 2,
                "AI Haste pick: at an equal bonus the higher expected damage wins (bite 1d8 over claw 1d4)");
            Assert(DND35.AI.NaturalAttackChoice.ChooseHasteExtraAttackIndex(grabber, living) == 1
                && grabber.GetDefaultHasteNaturalAttackIndex() == 0,
                "AI Haste pick: the Improved Grab claw beats the higher-bonus bite; the rules default stays the bite");
            Assert(DND35.AI.NaturalAttackChoice.ChooseHasteExtraAttackIndex(stinger, living) == 1
                && DND35.AI.NaturalAttackChoice.ChooseHasteExtraAttackIndex(stinger, undead) == 0,
                "AI Haste pick: a poison sting against a living target, the bite against an undead one (immune to poison)");

            profile = ScriptableObject.CreateInstance<DND35.AI.AIProfile>();
            Assert(profile.ChooseHasteNaturalAttackIndex(grabber, living) == 1
                && profile.ScoreHasteNaturalAttack(grabber, living, 1) > profile.ScoreHasteNaturalAttack(grabber, living, 0),
                "AIProfile.ChooseHasteNaturalAttackIndex and ScoreHasteNaturalAttack default to the shared scoring (the override points)");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"AI Haste natural choice check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Cleanup(plain, grabber, stinger, clawBite, living, undead);
            if (profile != null)
                Object.DestroyImmediate(profile);
        }
    }

    private static void TestNpcHasteNaturalSequence()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; NPC Haste natural sequence check needs Play mode");
            return;
        }

        CharacterController bear = null, grabber = null, mixed = null, tripper = null;
        CharacterController target = null;
        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        try
        {
            target = CreateWeakDefender("NpcHasteNaturalTarget");
            target.GridPosition = new Vector2Int(21, 14);
            target.Stats.AdjustMaxHP(500);
            target.Stats.CurrentHP += 500;

            bear = CreateHastedBiteClaws("NpcHasteNatural", 4);
            bear.IsControllable = false;
            bear.GridPosition = new Vector2Int(20, 14);
            FullAttackResult sequence = RunNpcMeleeSequence(gm, bear, target, null, out _);
            Assert(sequence.Attacks.Count == 4 && sequence.Attacks[0].WeaponName == "Bite" && sequence.Attacks[3].WeaponName == "Bite"
                && sequence.AttackLabels[3].Contains("Haste") && AttackModifier(sequence.Attacks[3]) == AttackModifier(sequence.Attacks[0])
                && bear.ProgressiveAttackPool.MainHandStepsUsed == 4 && bear.ProgressiveAttackPool.HasteExtraNaturalAttackUsed,
                "NPC natural sequence with Haste: bite, claw, claw, then the Haste bite at the bite's bonus (CMB-106)");

            grabber = CreateHastedBiteClaws("NpcHasteNaturalGrabChoice", 4);
            grabber.Stats.HasImprovedGrab = true;
            grabber.Stats.ImprovedGrabTriggerAttackName = "Claw";
            Assert(gm.ChooseHasteNaturalAttackIndexForAI(grabber, target) == 1 && gm.ChooseHasteNaturalAttackIndexForAI(target, bear) == -1,
                "The NPC executor asks the AI scoring for the Haste natural attack (the grab claw); -1 for a creature without one");

            mixed = CreateBiteClawsCreature("NpcHasteMixed", 4, false);
            mixed.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
            mixed.ApplyHasteEffect(10, null);
            mixed.IsControllable = false;
            mixed.GridPosition = new Vector2Int(22, 14);
            FullAttackResult mixedSequence = RunNpcMeleeSequence(gm, mixed, target, null, out _);
            Assert(mixedSequence.Attacks.Count == 2 && mixedSequence.Attacks.TrueForAll(a => a.WeaponName != "Bite" && a.WeaponName != "Claw")
                && mixedSequence.Attacks[1].BreakdownBAB == 4,
                "NPC with a weapon and natural attacks, hasted, BAB +4: one weapon attack plus the Haste weapon attack at +4, no natural attack");

            // A trip replaces the Haste step: the evaluation is offered before every step and trips at the 4th.
            ScenarioHooks.RollFilter = (sides, ctx, natural) => ctx == "Trip touch attack" ? 1 : natural;
            tripper = CreateHastedBiteClaws("NpcHasteNaturalTrip", 4);
            tripper.Stats.Feats.Add("Improved Trip");
            tripper.IsControllable = false;
            tripper.GridPosition = new Vector2Int(21, 13);
            int offers = 0;
            FullAttackResult tripSequence = RunNpcMeleeSequence(gm, tripper, target,
                (actor, stepTarget) => offers++ == 3 && gm.TryNPCSpecialAttackByTypeForAI(actor, stepTarget, SpecialAttackType.Trip),
                out int maneuvers);
            Assert(maneuvers == 1 && tripSequence.Attacks.Count == 3 && tripper.ProgressiveAttackPool.MainHandStepsUsed == 4
                && tripper.ProgressiveAttackPool.HasteExtraNaturalAttackUsed,
                "NPC natural sequence with Haste: a trip may take the Haste step after bite, claw, claw (3 attacks, 1 trip, 4 steps)");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"NPC Haste natural sequence check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            foreach (CharacterController c in new[] { bear, grabber, mixed, tripper, target })
                if (c != null)
                    Cleanup(c);
        }
    }

    private static void TestPcHasteNaturalAttackOptions()
    {
        // PC side: the Attack and natural-attack buttons. Once every natural attack is used, a used one is
        // offered again as Haste's extra attack; the Trip button can take the Haste step at the bite's bonus.
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; PC Haste natural options check needs Play mode");
            return;
        }

        MethodInfo consumeTrip = typeof(GameManager).GetMethod("TryConsumeTripAttackAction", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(consumeTrip != null, "GameManager.TryConsumeTripAttackAction is available for the PC Haste natural test");
        if (consumeTrip == null)
            return;

        var pc = CreateHastedBiteClaws("PcHasteNatural", 4);
        ActivePcScope scope = null;
        try
        {
            scope = ActivePcScope.Enter(gm, pc);
            Assert(scope != null && gm.ActivePC == pc, "The hasted creature is the active PC (reflection on GameManager/TurnService fields)");
            if (scope == null)
                return;

            Assert(gm.GetRemainingTripAttackActions(pc) == 4 && !gm.IsHasteExtraNaturalAttackOption(pc, 0),
                "PC with Haste: 4 natural steps for the Trip button; no Haste option before the bite is used");

            for (int i = 0; i < 3; i++)
            {
                pc.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
                gm.Combat_MarkNaturalAttackSequenceIndexUsed(i);
            }

            Assert(gm.CanUseNaturalAttackOption(pc) && gm.IsHasteExtraNaturalAttackOption(pc, 0) && gm.IsHasteExtraNaturalAttackOption(pc, 2)
                && gm.GetRemainingTripAttackActions(pc) == 1 && gm.GetCurrentTripAttackBonus(pc) == 4,
                "After bite, claw, claw the used natural attacks are offered again for the Haste attack; one trip left, at the bite's +4");

            object[] args = { pc, 0, 0, null };
            bool tripped = (bool)consumeTrip.Invoke(gm, args);
            Assert(tripped && (int)args[1] == 4 && pc.ProgressiveAttackPool.HasteExtraNaturalAttackUsed
                && !gm.CanUseNaturalAttackOption(pc) && !gm.IsHasteExtraNaturalAttackOption(pc, 0) && !gm.CanUseTripAttackOption(pc),
                "A PC trip in place of the Haste attack rolls at +4 and uses it up; no natural attack or trip is left");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"PC Haste natural options check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            scope?.Dispose();
            Cleanup(pc);
        }
    }

    private static void TestPcHasteNaturalButtonSelection()
    {
        // The natural-attack button path (ActionButtonPanel -> GameManager.OnNaturalAttackButtonPressed):
        // pressing a used natural attack while the Haste attack is unused selects Haste's extra attack with
        // that weapon. If the Haste attack is gone by the time the target is clicked, PerformSingleAttack
        // refuses before paying, instead of making the used natural attack again. The resolving click
        // itself is covered by the rules/haste-natural-buttons-ui scenario (it starts the after-attack
        // turn-flow coroutine, which a static test must not leave running).
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; PC Haste natural button check needs Play mode");
            return;
        }

        FieldInfo flowField = typeof(GameManager).GetField("_combatFlowService", BindingFlags.Instance | BindingFlags.NonPublic);
        CombatFlowService flow = flowField != null ? flowField.GetValue(gm) as CombatFlowService : null;
        Assert(flow != null, "GameManager._combatFlowService is available for the PC Haste natural button test");
        if (flow == null)
            return;

        var pc = CreateHastedBiteClaws("PcHasteNaturalButton", 4);
        var target = CreateHasteTarget("PcHasteNaturalButtonTarget");
        ActivePcScope scope = null;
        try
        {
            scope = ActivePcScope.Enter(gm, pc);
            Assert(scope != null && gm.ActivePC == pc, "The hasted creature is the active PC for the button test");
            if (scope == null)
                return;

            pc.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
            gm.Combat_MarkNaturalAttackSequenceIndexUsed(0);

            gm.OnNaturalAttackButtonPressed(0, "Bite");
            Assert(gm.Combat_HasPendingNaturalAttackSelection() && gm.Combat_IsPendingNaturalAttackHasteExtra()
                && gm.Combat_GetPendingNaturalAttackSequenceIndex() == 0,
                "After the bite, pressing the bite again (claws still unused) selects Haste's extra attack with the bite");

            gm.OnNaturalAttackButtonPressed(1, "Claw");
            Assert(gm.Combat_HasPendingNaturalAttackSelection() && !gm.Combat_IsPendingNaturalAttackHasteExtra()
                && gm.Combat_GetPendingNaturalAttackSequenceIndex() == 1,
                "Pressing an unused claw selects that claw as an ordinary natural attack");

            gm.OnNaturalAttackButtonPressed(0, "Bite");
            pc.ClearHasteEffect();
            int hpBefore = target.Stats.CurrentHP;
            flow.PerformSingleAttack(pc, target, false, 0, null, null);
            Assert(pc.ProgressiveAttackPool.MainHandStepsUsed == 1 && target.Stats.CurrentHP == hpBefore
                && !gm.Combat_HasPendingNaturalAttackSelection() && !gm.IsNaturalAttackSequenceIndexUsed(pc, 1),
                "A Haste selection that can no longer be made (Haste ended) is refused before paying; the bite is not made twice");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"PC Haste natural button check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            gm.Combat_ClearPendingNaturalAttackSelection();
            scope?.Dispose();
            Cleanup(pc, target);
        }
    }

    private static void TestNpcManeuverReplacesLaterNaturalAttack()
    {
        // The NPC melee loop offers the maneuver before every natural step until one substitute is
        // made (AI limit, AI-035). A trip-only evaluation trips only on the second step (the first
        // claw), so the third step is not offered; the trip's touch attack is forced to a natural 1
        // (always a miss, so nothing else changes) and its modifier is read from the result.
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; NPC natural maneuver check needs Play mode");
            return;
        }

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        System.Action<CharacterController, CharacterController, SpecialAttackType, SpecialAttackResult> savedManeuver = ScenarioHooks.ManeuverResolved;
        var tripResults = new System.Collections.Generic.List<SpecialAttackResult>();
        try
        {
            ScenarioHooks.RollFilter = (sides, ctx, natural) => ctx == "Trip touch attack" ? 1 : natural;
            ScenarioHooks.ManeuverResolved = (attacker, target, type, result) =>
            {
                if (type == SpecialAttackType.Trip)
                    tripResults.Add(result);
            };

            int row = 0;
            foreach (bool multiattack in new[] { false, true })
            {
                CharacterController npc = null;
                CharacterController target = null;
                string tag = multiattack ? " (Multiattack)" : string.Empty;
                try
                {
                    npc = CreateBiteClawsCreature(multiattack ? "NpcNaturalTripMultiattack" : "NpcNaturalTrip", 4, multiattack);
                    npc.Stats.Feats.Add("Improved Trip"); // no AoO from the target (PHB p.96)
                    npc.IsControllable = false;
                    target = CreateWeakDefender(multiattack ? "NpcNaturalTripTargetMultiattack" : "NpcNaturalTripTarget");
                    npc.GridPosition = new Vector2Int(8, 8 + row);
                    target.GridPosition = new Vector2Int(9, 8 + row);
                    row += 3;
                    target.Stats.AdjustMaxHP(500);
                    target.Stats.CurrentHP += 500;
                    tripResults.Clear();

                    int stepsOffered = 0;
                    int stepOfTrip = -1;
                    FullAttackResult sequence = RunNpcMeleeSequence(gm, npc, target,
                        (actor, stepTarget) =>
                        {
                            if (stepsOffered++ != 1)
                                return false;
                            stepOfTrip = actor.ProgressiveAttackPool.MainHandStepsUsed;
                            return gm.TryNPCSpecialAttackByTypeForAI(actor, stepTarget, SpecialAttackType.Trip);
                        },
                        out int maneuvers);

                    Assert(stepsOffered == 2 && maneuvers == 1 && stepOfTrip == 1,
                        $"NPC melee loop: the maneuver is offered before a later natural step, the trip takes step 2, and no second substitute is offered (offered {stepsOffered}, trip at {stepOfTrip}; CMB-102, AI-035)" + tag);
                    Assert(sequence.Attacks.Count == 2 && sequence.Attacks[0].WeaponName == "Bite" && sequence.Attacks[1].WeaponName == "Claw"
                        && npc.ProgressiveAttackPool.MainHandStepsUsed == 3,
                        "NPC melee loop: bite, a trip in place of the first claw, then the second claw" + tag);

                    int expectedTouch = npc.GetManeuverMeleeTouchAttackModifier(multiattack ? 2 : -1);
                    SpecialAttackResult trip = tripResults.Count == 1 ? tripResults[0] : null;
                    int touchMod = trip != null ? trip.CheckTotal - trip.CheckRoll : int.MinValue;
                    Assert(trip != null && trip.CheckRoll == 1 && touchMod == expectedTouch
                        && touchMod == npc.GetManeuverMeleeTouchAttackModifier(4) - (multiattack ? 2 : 5),
                        $"NPC trip in place of a secondary claw rolls its touch attack at BAB {(multiattack ? "+2" : "-1")} (modifier {touchMod}, expected {expectedTouch}; MM p.312)" + tag);
                }
                catch (System.Exception ex)
                {
                    System.Exception inner = ex.InnerException ?? ex;
                    Assert(false, $"NPC natural maneuver check threw {inner.GetType().Name}: {inner.Message}" + tag);
                }
                finally
                {
                    Cleanup(npc, target);
                }
            }
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            ScenarioHooks.ManeuverResolved = savedManeuver;
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

    // CMB-085: the PHB p.154/157/158 stability bonus comes from creature data. Database
    // templates are shared, so every check below reads them or works on a clone (never mutates).
    private static void TestExceptionallyStableCreatureData()
    {
        NPCDefinition worg = NPCDatabase.Get("worg");
        NPCDefinition dwarf = NPCDatabase.Get("dwarf_warrior");
        NPCDefinition human = NPCDatabase.Get("human_warrior");

        Assert(worg != null && worg.IsExceptionallyStable, "Worg (four legs) is exceptionally stable in the NPC database");
        Assert(dwarf != null && dwarf.IsExceptionallyStable, "NPC dwarf warrior has dwarf stability in the NPC database");
        Assert(human != null && !human.IsExceptionallyStable, "Human warrior is not exceptionally stable");
        Assert(NPCDatabase.Get("wolf") != null && NPCDatabase.Get("wolf").IsExceptionallyStable,
            "Wolf summon alias inherits stability from wolf_pack_hunter");
        Assert(NPCDatabase.Get("fiendish_dire_bear") != null && NPCDatabase.Get("fiendish_dire_bear").IsExceptionallyStable,
            "Fiendish dire bear (a clone of the dire bear) inherits stability");
        Assert(NPCDatabase.Get("skeleton_wolf") != null && NPCDatabase.Get("skeleton_wolf").IsExceptionallyStable,
            "Wolf skeleton keeps the wolf's four legs");

        if (worg == null)
            return;

        NPCDefinition clone = worg.Clone();
        Assert(clone.IsExceptionallyStable, "NPCDefinition.Clone keeps IsExceptionallyStable");

        clone.AppliedTemplateIds = new System.Collections.Generic.List<string> { "skeleton", "fiendish" };
        NPCDefinition templated = CreatureTemplateRegistry.ApplyTemplatesClone(clone);
        Assert(templated != null && templated.IsExceptionallyStable && templated.CreatureType == "Undead",
            "A fiendish worg skeleton built through CreatureTemplateRegistry keeps stability");
        Assert(worg.AppliedTemplateIds == null || worg.AppliedTemplateIds.Count == 0,
            "Applying templates to a clone leaves the worg template untouched");

        // A humanoid's flag comes from a racial trait, which the skeleton and zombie lose
        // (MM p.226, p.266); a quadruped keeps its legs.
        if (dwarf != null)
        {
            foreach (string undeadTemplate in new[] { "skeleton", "zombie" })
            {
                NPCDefinition dwarfClone = dwarf.Clone();
                dwarfClone.AppliedTemplateIds = new System.Collections.Generic.List<string> { undeadTemplate };
                NPCDefinition undeadDwarf = CreatureTemplateRegistry.ApplyTemplatesClone(dwarfClone);
                Assert(undeadDwarf != null && undeadDwarf.CreatureType == "Undead" && !undeadDwarf.IsExceptionallyStable,
                    $"A dwarf {undeadTemplate} loses dwarf stability (a racial trait, not legs)");
            }

            Assert(dwarf.IsExceptionallyStable, "Templating dwarf clones leaves the dwarf_warrior template stable");
        }

        NPCDefinition worgZombieClone = worg.Clone();
        worgZombieClone.AppliedTemplateIds = new System.Collections.Generic.List<string> { "zombie" };
        NPCDefinition worgZombie = CreatureTemplateRegistry.ApplyTemplatesClone(worgZombieClone);
        Assert(worgZombie != null && worgZombie.IsExceptionallyStable, "A worg zombie keeps its four legs' stability");
    }

    private static void TestExceptionallyStableDefenderGetsPlus4()
    {
        var defender = CreateTestCharacter("StableDefender", "Fighter");
        defender.Stats.Race = null;
        defender.Stats.IsExceptionallyStable = false;

        BullRushCheckResult plain = defender.RollBullRushDefenderCheck(fixedRoll: 10);
        int plainTripOverrun = defender.GetTripOrOverrunDefenderCheckModifier();

        defender.Stats.IsExceptionallyStable = true;
        BullRushCheckResult stable = defender.RollBullRushDefenderCheck(fixedRoll: 10);
        int stableTripOverrun = defender.GetTripOrOverrunDefenderCheckModifier();

        Assert(plain.StabilityBonus == 0 && stable.StabilityBonus == 4,
            "Exceptionally stable creature gets +4 on the bull rush defender check");
        Assert(stable.Total == plain.Total + 4, "Bull rush defender total rises by exactly 4 with stability");
        Assert(stableTripOverrun == plainTripOverrun + 4, "Trip and overrun defender modifier rises by exactly 4 with stability");

        Cleanup(defender);
    }

    private static void TestStableDwarfGetsSinglePlus4AndNoneWhileMounted()
    {
        var dwarf = CreateTestCharacter("StableDwarf", "Fighter");
        dwarf.Stats.Race = RaceDatabase.GetRace("Dwarf");
        dwarf.Stats.IsExceptionallyStable = true;

        Assert(dwarf.RollBullRushDefenderCheck(fixedRoll: 10).StabilityBonus == 4,
            "A dwarf that also has the stability flag gets one +4, not +8");

        MountDatabase.Init();
        MountSystem.MountInstance horse = MountSystem.CreateMount(MountType.LightHorse);
        string mountLog = MountSystem.TryMount(dwarf, horse);
        bool mounted = MountSystem.IsMounted(dwarf);
        Assert(mounted, "Test setup: the Medium dwarf mounts a Large light horse" + (mounted ? string.Empty : $" ({mountLog})"));
        if (mounted)
        {
            Assert(dwarf.GetManeuverStabilityBonus() == 0
                    && dwarf.RollBullRushDefenderCheck(fixedRoll: 10).StabilityBonus == 0,
                "No stability bonus while riding (PHB p.15: only while standing on the ground)");
            MountSystem.TryDismount(dwarf);
            if (MountSystem.IsMounted(dwarf))
                MountSystem.ForceDismount(dwarf, allowSoftFall: true);
        }

        Assert(!MountSystem.IsMounted(dwarf) && dwarf.GetManeuverStabilityBonus() == 4,
            "Stability returns after dismounting");

        Cleanup(dwarf);
    }

    // The move-through overrun (OverrunSystem, the PC path; CMB-015) uses the shared PHB p.157
    // terms: stability and Improved Overrun count, and a tie goes to the higher modifier.
    private static void TestMoveThroughOverrunUsesSharedCheck()
    {
        GameManager gm = GameManager.Instance;
        MethodInfo check = typeof(GameManager).GetMethod("ResolveOverrunOpposedCheck", BindingFlags.Instance | BindingFlags.NonPublic);
        if (gm == null || check == null)
        {
            Assert(false, "Move-through overrun check uses the shared helpers (needs Play mode with a GameManager)");
            return;
        }

        var attacker = CreateTestCharacter("MoveThroughOverrunner", "Fighter");
        var defender = CreateTestCharacter("MoveThroughBlocker", "Fighter");
        try
        {
            attacker.Stats.Race = null;
            attacker.Stats.STR = 12; // +1
            attacker.Stats.CurrentSizeCategory = SizeCategory.Medium;
            attacker.Stats.Feats.Remove("Improved Overrun");
            defender.Stats.Race = null;
            defender.Stats.STR = 10; // +0
            defender.Stats.DEX = 10; // +0
            defender.Stats.CurrentSizeCategory = SizeCategory.Medium;
            defender.Stats.IsExceptionallyStable = false;

            bool Run(params int[] rolls)
            {
                int i = 0;
                System.Func<int> d20 = () => i < rolls.Length ? rolls[i++] : 10;
                return (bool)check.Invoke(gm, new object[] { attacker, defender, d20 });
            }

            int atkMod = attacker.GetOverrunAttackerCheckModifier();
            int defMod = defender.GetTripOrOverrunDefenderCheckModifier();
            Assert(Run(10, 10) == CharacterController.DoesAttackerWinOpposedCheck(10 + atkMod, atkMod, 10 + defMod, defMod, () => 10),
                "Move-through overrun result matches the shared helpers");
            Assert(Run(10, 10), "Move-through overrun: attacker +1 beats an unstable defender +0 on equal rolls");
            Assert(Run(9, 10), "Move-through overrun: a tie goes to the higher modifier (the attacker), not the defender");

            defender.Stats.IsExceptionallyStable = true;
            Assert(!Run(10, 10), "Move-through overrun: defender stability (+4) applies");

            attacker.Stats.Feats.Add("Improved Overrun");
            Assert(Run(10, 10), "Move-through overrun: Improved Overrun (+4) applies to the attacker");
        }
        finally
        {
            Cleanup(attacker, defender);
        }
    }

    private static void TestNpcSetupCopiesExceptionallyStable()
    {
        GameManager gm = GameManager.Instance;
        MethodInfo init = typeof(GameManager).GetMethod("InitializeNPCFromDefinition", BindingFlags.Instance | BindingFlags.NonPublic);
        NPCDefinition worg = NPCDatabase.Get("worg");
        if (gm == null || init == null || worg == null)
        {
            Assert(false, "NPC setup copies IsExceptionallyStable (needs Play mode with a GameManager)");
            return;
        }

        var go = new GameObject("StableWorg_GO");
        var npc = go.AddComponent<CharacterController>();
        go.AddComponent<InventoryComponent>();
        try
        {
            init.Invoke(gm, new object[] { npc, worg.Clone(), new Vector2Int(-40, -40), null, null });
            Assert(npc.Stats != null && npc.Stats.IsExceptionallyStable && npc.GetManeuverStabilityBonus() == 4,
                "InitializeNPCFromDefinition copies IsExceptionallyStable, so a spawned worg gets +4");
        }
        finally
        {
            if (gm.Grid != null)
                gm.Grid.ClearCreatureOccupancy(npc);
            Cleanup(npc);
        }
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
