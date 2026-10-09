using System.Reflection;
using UnityEngine;
using DND35e.Identifiers;
using DND35.AI.Profiles;

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
        TestPinRenewalOnlyOnPinnersNextTurn();
        TestFailedPinRenewalEndsPinKeepsGrapple();
        TestOtherActionOnRenewalTurnLetsPinLapse();
        TestAiRenewsDuePin();
        TestPinRenewalDefenderResistsWithoutPinnedPenalty();
        TestPinEndsWhenPinnerCouldNotAct();
        TestNoOpActionOnRenewalTurnKeepsPin();
        TestSpellOnRenewalTurnLetsPinLapse();
        TestAiOneAttackPinnerLetsDuePinLapse();
        TestPredatorRenewsDuePinAndReleasesWhenFleeing();
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
        TestPathLeavesSquareSharedAfterPinRelease();
        TestLargePathLeavesSquareSharedAfterPinRelease();
        TestGrapplerTakesNoNormalMovement();
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
        TestNaturalAttackDamageTypes();
        TestNaturalWeaponSunderBySlashingOrBludgeoning();
        TestNaturalWeaponDisarmOneHandedOwnSize();
        TestSunderOpposedRollSizeAndHandedness();
        TestNaturalSunderAtHasteStep();
        TestNaturalSecondaryGrapplePenaltyOnTouchOnly();
        TestNpcNaturalSequenceMakesOneSubstitute();
        TestAiManeuverStopgapTripSucceedsThenAttacks();
        TestAiManeuverStopgapFailedTripNotRetried();
        TestHasteAddsOneNaturalAttackStep();
        TestHasteNaturalStepUsesChosenNaturalAttack();
        TestHasteNaturalFullAttackAddsOneAttack();
        TestGrappleNaturalAttackIsOneAttack();
        TestGrappleNaturalAttackHasteAndOutOfOrderSteps();
        TestGrappleNaturalAttackRakeOncePerTurn();
        TestGrappleNaturalAttackStepsAndAiChoice();
        TestGrappleChecksStayIterativeForNaturalAttackers();
        TestGrappleNaturalAttackRefusesUsedOrMissingAttack();
        TestHasteWeaponAndMixedAttackersGetOneHasteAttack();
        TestHasteNaturalManeuverReplacesHasteStep();
        TestAiHasteNaturalAttackChoice();
        TestNpcHasteNaturalSequence();
        TestImprovedGrabSizeLimit();
        TestImprovedGrabSizeOverrideData();
        TestPcHasteNaturalAttackOptions();
        TestPcHasteNaturalButtonSelection();
        TestPcHasteChooserForFullAttackAndPounce();
        TestBullRushChargeAppliesPlus2ToAttackerCheck();
        TestBullRushImprovedFeatAddsPlus4();
        TestBullRushDefenderUsesStrengthAndDwarfStability();
        TestExceptionallyStableCreatureData();
        TestOwnerStabilityClassification();
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
        TestTripTriggerAttackData();
        TestFreeTripOnlyAfterTriggerAttack();
        TestSummonSmiteBiteStartsFreeTrip();
        TestTripAttemptRollsTouchAttack();
        TestTripSizeLimit();
        TestCounterTripAfterFailedTrip();
        TestCounterTripChoiceOdds();
        TestNpcExecutorCounterTrip();
        TestImprovedTripFollowUpAttack();
        TestImprovedTripFollowUpNaturalAndFreeTrip();
        TestImprovedTripFollowUpAfterTripReactions();
        TestCounterTripTriggersMeleeReactions();
        TestCounterTripByDisabledDefender();
        TestImprovedTripFollowUpWeaponFallback();
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

    // ===== Pin duration (PHB p.156: a pin lasts 1 round; owner decision 2026-10-07, CMB-120) =====

    private static void TestPinRenewalOnlyOnPinnersNextTurn()
    {
        var attacker = CreateTestCharacter("GrapplePinRenewTiming", "Fighter");
        var defender = CreateWeakDefender("GrapplePinRenewTimingTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult pinResult = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(pinResult != null && pinResult.Success, "CMB-120: initial pin succeeds before the renewal-timing test");
        Assert(attacker.IsHoldingPinThisRound() && !attacker.IsPinRenewalDue(), "CMB-120: in the turn of the pin the pinner holds it and no renewal is due");
        Assert(attacker.IsGrappleActionBlockedWhilePinning(GrappleActionType.AttackUnarmed, out _), "CMB-120: the pinner restrictions apply while the pin's round lasts");

        SpecialAttackResult samePin = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(samePin != null && !samePin.Success && defender.HasCondition(CombatConditionType.Pinned), "CMB-120: pinning again in the same turn is refused and the pin stays");

        attacker.StartNewTurn();
        Assert(attacker.IsPinRenewalDue() && !attacker.IsHoldingPinThisRound(), "CMB-120: on the pinner's next turn the renewal is due");
        Assert(defender.HasCondition(CombatConditionType.Pinned), "CMB-120: the opponent is still pinned when the pinner's next turn starts, so the pinner can pin again");
        Assert(!attacker.IsGrappleActionBlockedWhilePinning(GrappleActionType.AttackUnarmed, out _)
            && !attacker.IsGrappleActionBlockedWhilePinning(GrappleActionType.PinOpponent, out _),
            "CMB-120: on the renewal turn the pinner restrictions are lifted and Pin Opponent is allowed");

        SpecialAttackResult renew = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(renew != null && renew.Success && defender.HasCondition(CombatConditionType.Pinned), "CMB-120: a won renewal keeps the opponent pinned");
        Assert(attacker.IsHoldingPinThisRound() && !attacker.IsPinRenewalDue(), "CMB-120: a renewal restarts the 1-round duration");

        Cleanup(attacker, defender);
    }

    private static void TestFailedPinRenewalEndsPinKeepsGrapple()
    {
        var attacker = CreateTestCharacter("GrapplePinRenewFail", "Fighter");
        var defender = CreateWeakDefender("GrapplePinRenewFailTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult pinResult = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(pinResult != null && pinResult.Success, "CMB-120: initial pin succeeds before the failed-renewal test");

        // Swap the strengths so the renewal check is lost whatever the dice.
        ConfigureVeryWeakGrappler(attacker);
        ConfigureVeryStrongGrappler(defender);
        attacker.StartNewTurn();

        SpecialAttackResult renew = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(renew != null && !renew.Success, "CMB-120: a lost renewal check fails");
        Assert(!defender.HasCondition(CombatConditionType.Pinned) && defender.GetPinnedBy() == null && !attacker.IsPinningOpponent(),
            "CMB-120: a failed renewal ends the pin");
        Assert(attacker.IsGrappling() && defender.IsGrappling()
            && attacker.HasCondition(CombatConditionType.Grappled) && defender.HasCondition(CombatConditionType.Grappled),
            "CMB-120: a failed renewal leaves the grapple in place");

        Cleanup(attacker, defender);
    }

    private static void TestOtherActionOnRenewalTurnLetsPinLapse()
    {
        var attacker = CreateTestCharacter("GrapplePinLapse", "Fighter");
        var defender = CreateWeakDefender("GrapplePinLapseTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult pinResult = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(pinResult != null && pinResult.Success, "CMB-120: initial pin succeeds before the lapse test");

        attacker.StartNewTurn();
        SpecialAttackResult damage = attacker.ResolveGrappleAction(GrappleActionType.DamageOpponent, AttackDamageMode.Nonlethal);
        Assert(damage != null && damage.Log != null && damage.Log.Contains("does not pin"), "CMB-120: another grapple action on the renewal turn logs the lapse of the pin");
        Assert(!defender.HasCondition(CombatConditionType.Pinned) && !attacker.IsPinningOpponent(), "CMB-120: another grapple action on the renewal turn lets the pin end first");
        Assert(attacker.IsGrappling() && defender.IsGrappling(), "CMB-120: the lapsed pin leaves the grapple in place");

        Cleanup(attacker, defender);
    }

    private static void TestAiRenewsDuePin()
    {
        GameManager gm = GameManager.Instance;
        MethodInfo choose = typeof(GameManager).GetMethod("ChooseNPCGrappleAction", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(choose != null, "CMB-120: GameManager.ChooseNPCGrappleAction exists for the AI renewal test");
        if (gm == null || choose == null)
        {
            if (gm == null)
                Debug.Log("  [SKIP] GameManager.Instance is null; the AI renewal check needs Play mode");
            return;
        }

        var npc = CreateTestCharacter("GrapplePinAiRenew", "Fighter");
        var pc = CreateWeakDefender("GrapplePinAiRenewTarget");
        ConfigureVeryStrongGrappler(npc);
        ConfigureVeryWeakGrappler(pc);

        ForceGrappleState(npc, pc);
        SpecialAttackResult pinResult = npc.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(pinResult != null && pinResult.Success, "CMB-120: initial AI pin succeeds before the AI renewal test");

        npc.StartNewTurn();
        object choice = choose.Invoke(gm, new object[] { npc, pc, false, true });
        Assert(choice is GrappleActionType chosen && chosen == GrappleActionType.PinOpponent,
            "CMB-120: an AI pinner with a grapple attack left after renewing pins again on its renewal turn (got " + (choice ?? "null") + ")");

        Cleanup(npc, pc);
    }

    private static void TestPinRenewalDefenderResistsWithoutPinnedPenalty()
    {
        var attacker = CreateTestCharacter("GrapplePinRenewDefTotal", "Fighter");
        var defender = CreateWeakDefender("GrapplePinRenewDefTotalTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        try
        {
            // Every grapple check d20 is a 10, so the totals differ only by their modifiers.
            ScenarioHooks.RollFilter = (sides, ctx, natural) => ctx == "Grapple check" ? 10 : natural;

            ForceGrappleState(attacker, defender);
            SpecialAttackResult freshPin = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
            Assert(freshPin != null && freshPin.Success && freshPin.OpposedRoll == 10, "CMB-120: the fresh pin succeeds with the forced d20 before the renewal-total test");

            attacker.StartNewTurn();
            SpecialAttackResult renewal = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
            Assert(renewal != null && renewal.Success && renewal.OpposedRoll == 10, "CMB-120: the renewal succeeds with the forced d20");
            Assert(freshPin != null && renewal != null && renewal.OpposedTotal == freshPin.OpposedTotal,
                "CMB-120: the defender resists a renewal with the same total as a fresh pin, without the Pinned modifiers (fresh "
                + (freshPin != null ? freshPin.OpposedTotal.ToString() : "?") + ", renewal " + (renewal != null ? renewal.OpposedTotal.ToString() : "?") + ")");
            Assert(defender.HasCondition(CombatConditionType.Pinned) && attacker.IsHoldingPinThisRound(), "CMB-120: the won renewal pins the defender again");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            Cleanup(attacker, defender);
        }
    }

    private static void TestPinEndsWhenPinnerCouldNotAct()
    {
        var attacker = CreateTestCharacter("GrapplePinNoAct", "Fighter");
        var defender = CreateWeakDefender("GrapplePinNoActTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult pinResult = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(pinResult != null && pinResult.Success, "CMB-120: initial pin succeeds before the could-not-act test");

        // A turn skipped because the pinner cannot act never calls StartNewTurn; only its turn end runs.
        attacker.ApplyCondition(CombatConditionType.Stunned, 2, "CMB-120 test");
        Assert(!attacker.CanTakeActions(), "CMB-120: the stunned pinner cannot act (precondition)");
        attacker.ProcessPinnedDurationAtTurnEnd();
        Assert(!defender.HasCondition(CombatConditionType.Pinned) && !attacker.IsPinningOpponent() && defender.GetPinnedBy() == null,
            "CMB-120: a pin ends at the end of a pinner turn in which the pinner could not act");
        Assert(attacker.IsGrappling() && defender.IsGrappling(), "CMB-120: the grapple goes on after the pin ends for a pinner that could not act");

        Cleanup(attacker, defender);
    }

    private static void TestNoOpActionOnRenewalTurnKeepsPin()
    {
        var attacker = CreateTestCharacter("GrapplePinNoOp", "Fighter");
        var defender = CreateWeakDefender("GrapplePinNoOpTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);

        ForceGrappleState(attacker, defender);
        SpecialAttackResult pinResult = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(pinResult != null && pinResult.Success, "CMB-120: initial pin succeeds before the no-op action test");

        attacker.StartNewTurn();
        SpecialAttackResult breakPin = attacker.ResolveGrappleAction(GrappleActionType.BreakPin);
        Assert(breakPin != null && !breakPin.Success && breakPin.Log != null && !breakPin.Log.Contains("does not pin"),
            "CMB-120: Break Pin by a pinner resolves to nothing and does not let the due pin lapse");
        SpecialAttackResult draw = attacker.ResolveGrappleAction(GrappleActionType.DrawLightWeapon);
        Assert(draw != null && draw.Log != null && !draw.Log.Contains("does not pin"), "CMB-120: a not-implemented stub does not let the due pin lapse");
        Assert(defender.HasCondition(CombatConditionType.Pinned) && attacker.IsPinRenewalDue(), "CMB-120: after no-op actions the pin is still due for renewal");

        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            Assert(!gm.CanUseGrappleAction(attacker, GrappleActionType.BreakPin)
                && !gm.CanUseGrappleAction(attacker, GrappleActionType.DrawLightWeapon)
                && !gm.CanUseGrappleAction(attacker, GrappleActionType.RetrieveSpellComponent),
                "CMB-120: the renewal turn does not offer Break Pin or the not-implemented stubs");
            Assert(gm.CanUseGrappleAction(attacker, GrappleActionType.PinOpponent)
                && gm.CanUseGrappleAction(attacker, GrappleActionType.ReleasePinnedOpponent),
                "CMB-120: the renewal turn offers Pin again and Release");
        }
        else
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; the renewal-turn menu check needs Play mode");
        }

        Cleanup(attacker, defender);
    }

    private static void TestSpellOnRenewalTurnLetsPinLapse()
    {
        GameManager gm = GameManager.Instance;
        MethodInfo concentration = typeof(GameManager).GetMethod("ResolveGrappledOrPinnedCastingConcentration", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(concentration != null, "CMB-120: GameManager.ResolveGrappledOrPinnedCastingConcentration exists for the spell-lapse test");
        if (gm == null || concentration == null)
        {
            if (gm == null)
                Debug.Log("  [SKIP] GameManager.Instance is null; the spell-lapse check needs Play mode");
            return;
        }

        SpellData spell = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
        Assert(spell != null, "CMB-120: Magic Missile exists for the spell-lapse test");
        if (spell == null)
            return;

        var attacker = CreateTestCharacter("GrapplePinSpellLapse", "Fighter");
        var defender = CreateWeakDefender("GrapplePinSpellLapseTarget");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);
        attacker.Stats.CON = 30; // the forced 20 then passes the grappled Concentration check, so no slot is spent

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        try
        {
            ScenarioHooks.RollFilter = (sides, ctx, natural) => sides == 20 ? 20 : natural;

            ForceGrappleState(attacker, defender);
            SpecialAttackResult pinResult = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
            Assert(pinResult != null && pinResult.Success, "CMB-120: initial pin succeeds before the spell-lapse test");

            // In the turn of the pin a cast does not touch the pin.
            concentration.Invoke(gm, new object[] { attacker, null, spell, null, false, spell.SpellLevel, false, -1, null });
            Assert(defender.HasCondition(CombatConditionType.Pinned) && attacker.IsHoldingPinThisRound(), "CMB-120: a cast in the turn of the pin leaves the pin in place");

            attacker.StartNewTurn();
            concentration.Invoke(gm, new object[] { attacker, null, spell, null, false, spell.SpellLevel, false, -1, null });
            Assert(!defender.HasCondition(CombatConditionType.Pinned) && !attacker.IsPinningOpponent(),
                "CMB-120: casting a spell on the renewal turn lets the pin lapse before the spell resolves (shared PC/NPC cast step)");
            Assert(attacker.IsGrappling() && defender.IsGrappling(), "CMB-120: the grapple goes on after a cast lets the pin lapse");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            Cleanup(attacker, defender);
        }
    }

    private static void TestAiOneAttackPinnerLetsDuePinLapse()
    {
        GameManager gm = GameManager.Instance;
        MethodInfo choose = typeof(GameManager).GetMethod("ChooseNPCGrappleAction", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(choose != null, "CMB-120: GameManager.ChooseNPCGrappleAction exists for the one-attack AI test");
        if (gm == null || choose == null)
        {
            if (gm == null)
                Debug.Log("  [SKIP] GameManager.Instance is null; the one-attack AI check needs Play mode");
            return;
        }

        var npc = CreateTestCharacter("GrapplePinAiOneAttack", "Fighter");
        var pc = CreateWeakDefender("GrapplePinAiOneAttackTarget");
        ConfigureVeryStrongGrappler(npc);
        ConfigureVeryWeakGrappler(pc);

        ForceGrappleState(npc, pc);
        SpecialAttackResult pinResult = npc.ResolveGrappleAction(GrappleActionType.PinOpponent);
        Assert(pinResult != null && pinResult.Success, "CMB-120: initial AI pin succeeds before the one-attack test");

        npc.Stats.BaseAttackBonusOverride = 5; // one attack a round (PHB p.141), no ally, a non-caster target
        npc.StartNewTurn();
        Assert(gm.GetRemainingGrappleAttackActions(npc) == 1, "CMB-120: the one-attack pinner has a single grapple attack (precondition, got " + gm.GetRemainingGrappleAttackActions(npc) + ")");
        object choice = choose.Invoke(gm, new object[] { npc, pc, false, true });
        Assert(choice is GrappleActionType chosen && chosen == GrappleActionType.DamageOpponent,
            "CMB-120: a lone one-attack AI pinner deals grapple damage instead of renewing, so the pin lapses (got " + (choice ?? "null") + ")");

        Cleanup(npc, pc);
    }

    private static void TestPredatorRenewsDuePinAndReleasesWhenFleeing()
    {
        GameManager gm = GameManager.Instance;
        MethodInfo choose = typeof(GameManager).GetMethod("ChooseNPCGrappleAction", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(choose != null, "CMB-120: GameManager.ChooseNPCGrappleAction exists for the predator test");
        if (gm == null || choose == null)
        {
            if (gm == null)
                Debug.Log("  [SKIP] GameManager.Instance is null; the predator renewal check needs Play mode");
            return;
        }

        var npc = CreateTestCharacter("GrapplePinPredator", "Fighter");
        var pc = CreateWeakDefender("GrapplePinPredatorTarget");
        ConfigureVeryStrongGrappler(npc);
        ConfigureVeryWeakGrappler(pc);
        npc.Stats.HasPounce = true;
        npc.Stats.HasImprovedGrab = true;
        npc.Stats.HasRake = true;
        AnimalAIProfile profile = ScriptableObject.CreateInstance<AnimalAIProfile>();
        npc.aiProfile = profile;

        try
        {
            ForceGrappleState(npc, pc);
            SpecialAttackResult pinResult = npc.ResolveGrappleAction(GrappleActionType.PinOpponent);
            Assert(pinResult != null && pinResult.Success, "CMB-120: initial predator pin succeeds before the predator test");

            npc.StartNewTurn();
            Assert(profile.ShouldPrioritizeLethalNaturalGrappleAttacks(npc), "CMB-120: the test creature uses the predatory grapple routine (precondition)");
            object choice = choose.Invoke(gm, new object[] { npc, pc, false, true });
            Assert(choice is GrappleActionType renew && renew == GrappleActionType.PinOpponent,
                "CMB-120: a predator with attacks to spare pins again on its renewal turn (got " + (choice ?? "null") + ")");

            npc.Stats.CurrentHP = 1; // below 25%: the emergency escape branch
            object fleeing = choose.Invoke(gm, new object[] { npc, pc, false, true });
            Assert(fleeing is GrappleActionType flee && flee == GrappleActionType.ReleasePinnedOpponent,
                "CMB-120: a badly wounded predator that pins releases its pin (free, ends the grapple) instead of an escape check (got " + (fleeing ?? "null") + ")");
        }
        finally
        {
            Cleanup(npc, pc);
            Object.DestroyImmediate(profile);
        }
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
        Vector2Int attackerPosBefore = attacker.GridPosition;
        Vector2Int defenderPosBefore = defender.GridPosition;
        Assert(attacker.IsPinRenewalDue(), "CMB-120: the release below happens on the pinner's renewal turn");
        if (GameManager.Instance != null)
            Assert(GameManager.Instance.CanUseGrappleAction(attacker, GrappleActionType.ReleasePinnedOpponent), "CMB-120: Release Pinned Opponent is offered on the renewal turn");
        SpecialAttackResult releaseResult = attacker.ResolveGrappleAction(GrappleActionType.ReleasePinnedOpponent);
        Assert(releaseResult != null && releaseResult.Success, "Release succeeds at the start of the pinner's turn");
        Assert(releaseResult != null && releaseResult.Log != null && !releaseResult.Log.Contains("does not pin"),
            "CMB-120: releasing on the renewal turn is not preceded by a pin lapse");
        Assert(!attacker.IsGrappling() && !defender.IsGrappling()
            && !attacker.HasCondition(CombatConditionType.Grappled) && !defender.HasCondition(CombatConditionType.Grappled)
            && !defender.HasCondition(CombatConditionType.Pinned),
            "CMB-089/CMB-120: a release on the renewal turn ends the grapple for both creatures");
        Assert(attacker.GridPosition == attackerPosBefore && defender.GridPosition == defenderPosBefore,
            "CMB-089/CMB-120: a release on the renewal turn moves neither creature");
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

    // ===== Leaving a square shared after a pin release (CMB-122; PHB p.148, p.157) =====

    private static void TestPathLeavesSquareSharedAfterPinRelease()
    {
        var attacker = CreateTestCharacter("GrappleReleaseLeave", "Fighter");
        var defender = CreateWeakDefender("GrappleReleaseLeaveTarget");
        var blocker = CreateTestCharacter("GrappleReleaseLeaveBlocker", "Fighter");
        ConfigureVeryStrongGrappler(attacker);
        ConfigureVeryWeakGrappler(defender);
        defender.SetTeam(CharacterTeam.Enemy);
        blocker.SetTeam(CharacterTeam.Enemy);

        SquareGrid previousGrid = SquareGrid.Instance;
        var gridGo = new GameObject("GrappleReleaseLeave_Grid");
        SquareGrid grid = gridGo.AddComponent<SquareGrid>();
        try
        {
            grid.Width = 8;
            grid.Height = 8;
            grid.GenerateGrid();

            ForceGrappleState(attacker, defender);
            SpecialAttackResult pinResult = attacker.ResolveGrappleAction(GrappleActionType.PinOpponent);
            SpecialAttackResult releaseResult = attacker.ResolveGrappleAction(GrappleActionType.ReleasePinnedOpponent);
            Assert(pinResult != null && pinResult.Success && releaseResult != null && releaseResult.Success
                && !attacker.IsGrappling() && !defender.IsGrappling(),
                "CMB-122: pin and release succeed before the shared-square path test");

            // Both stay in one square after the release (CMB-089); an enemy stands two squares east.
            var shared = new Vector2Int(3, 3);
            var blockerSquare = new Vector2Int(5, 3);
            grid.SetCreatureOccupancy(attacker, shared, 1);
            grid.SetCreatureOccupancy(defender, shared, 1);
            grid.SetCreatureOccupancy(blocker, blockerSquare, 1);

            foreach (CharacterController mover in new[] { attacker, defender })
            {
                string who = mover == attacker ? "the releaser (PC side)" : "the released creature (NPC side)";
                var destination = new Vector2Int(3, 6);
                AoOPathResult path = grid.FindPathAoOAware(shared, destination, null, 6, 1, mover);
                bool reaches = path != null && path.Path != null && path.Path.Count > 0 && path.Path[path.Path.Count - 1] == destination;
                Assert(reaches && !path.Path.Contains(shared) && SquareGridUtils.IsAdjacent(shared, path.Path[0]),
                    "CMB-122: " + who + " finds a path out of the square shared after a pin release");
            }

            // Leaving is allowed; ending a move in an occupied square still is not (PHB p.148).
            AoOPathResult ontoEnemy = grid.FindPathAoOAware(shared, blockerSquare, null, 6, 1, attacker);
            Assert(ontoEnemy == null || ontoEnemy.Path == null || ontoEnemy.Path.Count == 0 || ontoEnemy.Path[ontoEnemy.Path.Count - 1] != blockerSquare,
                "CMB-122: a move from the shared square still cannot end in an enemy's square");
            AoOPathResult intoShared = grid.FindPathAoOAware(blockerSquare, shared, null, 6, 1, blocker);
            Assert(intoShared == null || intoShared.Path == null || intoShared.Path.Count == 0 || intoShared.Path[intoShared.Path.Count - 1] != shared,
                "CMB-122: no other creature can end its move in the shared square");
            Assert(!grid.CanPlaceCreature(shared, 1, attacker) && !grid.CanPlaceCreature(shared, 1, defender),
                "CMB-122: the shared square stays occupied by the other creature, so neither can re-enter it once it leaves");
        }
        finally
        {
            Object.DestroyImmediate(gridGo);
            // SquareGrid.Awake replaced the static Instance; put the previous grid back (GRID-010).
            typeof(SquareGrid).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, previousGrid);
            Cleanup(attacker, defender, blocker);
        }
    }

    private static void TestLargePathLeavesSquareSharedAfterPinRelease()
    {
        var large = CreateTestCharacter("GrappleReleaseLeaveLarge", "Fighter");
        var medium = CreateWeakDefender("GrappleReleaseLeaveMedium");
        medium.SetTeam(CharacterTeam.Enemy);
        large.Stats.CurrentSizeCategory = SizeCategory.Large;

        SquareGrid previousGrid = SquareGrid.Instance;
        var gridGo = new GameObject("GrappleReleaseLeaveLarge_Grid");
        SquareGrid grid = gridGo.AddComponent<SquareGrid>();
        try
        {
            grid.Width = 10;
            grid.Height = 10;
            grid.GenerateGrid();

            // A Large grappler at base (3,3) covers (3..4, 3..4); the Medium creature was left in the corner square
            // (4,4) of that footprint when the pin release ended the grapple (CMB-089).
            var largeBase = new Vector2Int(3, 3);
            var mediumSquare = new Vector2Int(4, 4);
            grid.SetCreatureOccupancy(large, largeBase, 2);
            grid.SetCreatureOccupancy(medium, mediumSquare, 1);
            System.Collections.Generic.List<Vector2Int> largeFootprint = grid.GetOccupiedSquares(largeBase, 2);

            // The Large creature leaves away from the Medium one, and also around it to the far side.
            foreach (Vector2Int destination in new[] { new Vector2Int(0, 0), new Vector2Int(7, 3) })
            {
                AoOPathResult path = grid.FindPathAoOAware(largeBase, destination, null, 8, 2, large);
                bool reaches = path != null && path.Path != null && path.Path.Count > 0 && path.Path[path.Path.Count - 1] == destination;
                bool endClear = reaches && !grid.GetOccupiedSquares(destination, 2).Contains(mediumSquare);
                Assert(reaches && endClear,
                    "CMB-122: a Large creature finds a path from its footprint, shared with a Medium creature, to " + destination + " and ends clear of it");
            }

            // The Medium creature leaves the Large footprint without ending in it.
            var mediumDestination = new Vector2Int(4, 7);
            AoOPathResult mediumPath = grid.FindPathAoOAware(mediumSquare, mediumDestination, null, 6, 1, medium);
            bool mediumReaches = mediumPath != null && mediumPath.Path != null && mediumPath.Path.Count > 0
                && mediumPath.Path[mediumPath.Path.Count - 1] == mediumDestination;
            Assert(mediumReaches && !largeFootprint.Contains(mediumDestination),
                "CMB-122: a Medium creature finds a path out of the Large footprint it shared after a pin release");

            AoOPathResult ontoLarge = grid.FindPathAoOAware(mediumSquare, new Vector2Int(3, 3), null, 6, 1, medium);
            Assert(ontoLarge == null || ontoLarge.Path == null || ontoLarge.Path.Count == 0 || !largeFootprint.Contains(ontoLarge.Path[ontoLarge.Path.Count - 1]),
                "CMB-122: the Medium creature's move cannot end in another square of the Large footprint (PHB p.148)");
        }
        finally
        {
            Object.DestroyImmediate(gridGo);
            typeof(SquareGrid).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, previousGrid);
            Cleanup(large, medium);
        }
    }

    // ===== No normal movement while grappling (PHB p.156) =====

    private static void TestGrapplerTakesNoNormalMovement()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; the grappler movement-budget check needs Play mode");
            return;
        }

        var attacker = CreateTestCharacter("GrappleNoMove", "Fighter");
        var defender = CreateWeakDefender("GrappleNoMoveTarget");
        defender.SetTeam(CharacterTeam.Enemy);
        try
        {
            int speedBefore = gm.GetCurrentMoveRangeSquares(defender);
            ForceGrappleState(attacker, defender);
            Assert(attacker.IsGrappling() && defender.IsGrappling(), "PHB p.156: the grapple is set up for the movement-budget test");

            // Grapplers share a square and path-finding accepts a shared start (CMB-122), so the movement budget is
            // what keeps both from leaving by ordinary movement (move, run, withdraw, charge, forced flight).
            Assert(speedBefore > 0 && gm.GetCurrentMoveRangeSquares(attacker) == 0 && gm.GetCurrentMoveRangeSquares(defender) == 0,
                "PHB p.156: a grappling creature (grappler and grappled) has no ordinary movement budget");
            Assert(gm.GetMoveRangeSquaresIgnoringGrapple(attacker) > 0,
                "PHB p.157: the grapple's own move action still reads the creature's speed");
            Assert(!gm.CanChargeTargetForAI(attacker, defender) && !gm.CanChargeTargetForAI(defender, attacker),
                "PHB p.156: a grappling creature cannot charge");

            // A frightened grappler cannot flee; the controller falls to its cornered branch (cower or fight defensively).
            MethodInfo flee = typeof(FrightenedBehaviorController).GetMethod("FindBestFleeCell", BindingFlags.NonPublic | BindingFlags.Static);
            Assert(flee != null, "FrightenedBehaviorController.FindBestFleeCell exists for the grappler flee test");
            if (flee != null)
            {
                foreach (bool withdraw in new[] { true, false })
                {
                    object cell = flee.Invoke(null, new object[] { gm, defender, attacker.GridPosition, withdraw, false, -1 });
                    Assert(cell == null, "PHB p.156: a frightened grappler finds no " + (withdraw ? "withdraw" : "flee") + " square");
                }
            }

            // A confused grappler told to flee (also the confused PC's forced turn) finds no square to move to.
            MethodInfo confusedMove = typeof(ConfusedBehaviorController).GetMethod("FindBestMovementCell", BindingFlags.NonPublic | BindingFlags.Static);
            Assert(confusedMove != null, "ConfusedBehaviorController.FindBestMovementCell exists for the grappler flee test");
            if (confusedMove != null)
            {
                var cell = confusedMove.Invoke(null, new object[] { gm, defender, attacker.GridPosition, true }) as SquareCell;
                Assert(cell == null || cell.Coords == defender.GridPosition, "PHB p.156: a confused grappler finds no square to flee to");
            }

            defender.ReleaseGrappleState("test cleanup");
            Assert(!defender.IsGrappling() && gm.GetCurrentMoveRangeSquares(defender) == speedBefore,
                "The movement budget returns once the grapple ends");
        }
        finally
        {
            attacker.ReleaseGrappleState("test cleanup");
            Cleanup(attacker, defender);
        }
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

    /// <summary>
    /// MM p.310 (CMB-126): unless the entry says otherwise, Improved Grab works only against an opponent at least one
    /// size category smaller than the creature (its current size); an entry's own maximum replaces that limit. A refused
    /// free attempt rolls nothing and starts no grapple.
    /// </summary>
    private static void TestImprovedGrabSizeLimit()
    {
        var grabber = CreateTestCharacter("ImprovedGrabSizeGrabber", "Fighter");
        var medium = CreateWeakDefender("ImprovedGrabSizeMedium");
        var small = CreateWeakDefender("ImprovedGrabSizeSmall");
        var huge = CreateWeakDefender("ImprovedGrabSizeHuge");
        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        try
        {
            grabber.Stats.HasImprovedGrab = true;
            grabber.Stats.ImprovedGrabTriggerAttackName = "Claw";
            small.Stats.SetBaseSizeCategory(SizeCategory.Small);
            huge.Stats.SetBaseSizeCategory(SizeCategory.Huge);

            Assert(!grabber.CanImprovedGrabTargetBySize(medium, out string mediumReason) && !string.IsNullOrEmpty(mediumReason)
                && grabber.CanImprovedGrabTargetBySize(small, out _),
                "Improved Grab size: a Medium grabber cannot grab a Medium target but can grab a Small one (MM p.310)");

            int d20Rolls = 0;
            ScenarioHooks.RollFilter = (sides, ctx, natural) => { if (sides == 20) d20Rolls++; return natural; };
            SpecialAttackResult refused = grabber.ResolveImprovedGrabFreeAttempt(medium);
            ScenarioHooks.RollFilter = savedFilter;
            Assert(refused != null && !refused.Success && d20Rolls == 0 && !grabber.IsGrappling() && !medium.IsGrappling()
                && refused.Log.Contains("too large"),
                "Improved Grab size: the free attempt on a target too large rolls no grapple check and starts no grapple");

            grabber.Stats.TryShiftCurrentSize(1);
            Assert(grabber.GetCurrentSizeCategory() == SizeCategory.Large && grabber.CanImprovedGrabTargetBySize(medium, out _)
                && !grabber.CanImprovedGrabTargetBySize(huge, out _),
                "Improved Grab size: the limit follows the grabber's current size (Large after a size increase grabs Medium, not Huge)");
            grabber.Stats.TryShiftCurrentSize(-1);

            grabber.Stats.ImprovedGrabMaxTargetSize = SizeCategory.Large;
            Assert(grabber.CanImprovedGrabTargetBySize(medium, out _) && !grabber.CanImprovedGrabTargetBySize(huge, out _),
                "Improved Grab size: an entry's own maximum (Large or smaller) replaces the default limit");

            grabber.Stats.ImprovedGrabMaxTargetSize = null;
            grabber.Stats.SetBaseSizeCategory(SizeCategory.Fine);
            var fine = CreateWeakDefender("ImprovedGrabSizeFine");
            fine.Stats.SetBaseSizeCategory(SizeCategory.Fine);
            Assert(!grabber.CanImprovedGrabTargetBySize(fine, out _),
                "Improved Grab size: a Fine grabber without an entry maximum can grab nothing (no smaller size exists)");
            Cleanup(fine);
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Improved Grab size check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            Cleanup(grabber, medium, small, huge);
        }
    }

    /// <summary>
    /// The per-creature Improved Grab maxima from the MM (CMB-126) are on the database entries, survive NPCDefinition.Clone,
    /// and entries without a size clause keep the default (null).
    /// </summary>
    private static void TestImprovedGrabSizeOverrideData()
    {
        NPCDatabase.Init();
        NPCDefinition choker = NPCDatabase.Get("choker");
        NPCDefinition behir = NPCDatabase.Get("behir");
        NPCDefinition mohrg = NPCDatabase.Get("mohrg");
        NPCDefinition crocodile = NPCDatabase.Get("crocodile");
        NPCDefinition lion = NPCDatabase.Get("lion");
        Assert(choker != null && choker.ImprovedGrabMaxTargetSize == SizeCategory.Large
            && behir != null && behir.ImprovedGrabMaxTargetSize == SizeCategory.Colossal
            && mohrg != null && mohrg.ImprovedGrabMaxTargetSize == SizeCategory.Medium,
            "Improved Grab data: choker Large or smaller (MM p.35), behir any size (MM p.25), mohrg its own size (MM p.190)");
        Assert(crocodile != null && crocodile.HasImprovedGrab && crocodile.ImprovedGrabMaxTargetSize == null
            && lion != null && lion.HasImprovedGrab && lion.ImprovedGrabMaxTargetSize == null,
            "Improved Grab data: the crocodile and lion entries name no size, so the MM p.310 default applies");
        NPCDefinition chokerClone = choker != null ? choker.Clone() : null;
        Assert(chokerClone != null && chokerClone.ImprovedGrabMaxTargetSize == SizeCategory.Large,
            "Improved Grab data: NPCDefinition.Clone keeps the size maximum (CRE-023)");
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
        object[] args = { npc, target, null, tryStepManeuver, false, 0, 0, null, null }; // last: no resume state (CMB-079)
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

    /// <summary>A creature with one natural attack per entry (name, primary, count), BAB through BaseAttackBonusOverride, no weapon.</summary>
    private static CharacterController CreateNaturalCreature(string name, int bab, params (string label, bool primary, int count)[] attacks)
    {
        var creature = CreateIterativeAttacker(name);
        creature.Stats.BaseAttackBonusOverride = bab;
        creature.Stats.NaturalAttacks.Clear();
        foreach (var attack in attacks)
        {
            creature.Stats.NaturalAttacks.Add(new NaturalAttackDefinition
            {
                Name = attack.label, DamageDice = attack.primary ? 6 : 4, DamageCount = 1, Count = attack.count, IsPrimary = attack.primary,
                BonusDamageSource = attack.primary ? DamageBonusSource.Strength : DamageBonusSource.StrengthHalf
            });
        }
        return creature;
    }

    private static bool NaturalAttackNamedCanSunder(string attackName)
        => new NaturalAttackDefinition { Name = attackName, DamageDice = 4 }.CanSunder;

    private static void TestNaturalAttackDamageTypes()
    {
        // MM p.312 natural weapon types, by name: bite B/P/S, claw and talon P/S, gore P, slap and slam B,
        // sting P, tentacle B; the MM dragon wing is a slam (MM p.68); an unarmed strike is B (PHB p.116).
        // Sunder needs slashing or bludgeoning (PHB p.158; owner ruling 2026-10-08, CMB-102).
        const DamageBypassTag B = DamageBypassTag.Bludgeoning, P = DamageBypassTag.Piercing, S = DamageBypassTag.Slashing;
        var expected = new (string name, DamageBypassTag types)[]
        {
            ("Bite", B | P | S), ("Bite (lion)", B | P | S), ("Claw", P | S), ("Claws", P | S), ("Foreclaw", P | S), ("Talons", P | S),
            ("Rake", P | S), ("Gore", P), ("Gore (goat)", P), ("Horn", P), ("Sting", P), ("Slam", B), ("Tail Slap", B),
            ("Tentacle", B), ("Tentacles", B), ("Wing", B), ("Unarmed Strike", B),
            ("Hoof", DamageBypassTag.None), ("Incorporeal Touch", DamageBypassTag.None), (null, DamageBypassTag.None)
        };
        var wrong = new System.Collections.Generic.List<string>();
        foreach (var e in expected)
        {
            DamageBypassTag actual = NaturalAttackDefinition.GetDefaultPhysicalDamageTypes(e.name);
            if (actual != e.types)
                wrong.Add($"{e.name ?? "<null>"}={actual}");
        }
        Assert(wrong.Count == 0, "Natural attack damage types by name follow MM p.312" + (wrong.Count > 0 ? ": wrong " + string.Join(", ", wrong) : string.Empty));

        Assert(NaturalAttackNamedCanSunder("Bite") && NaturalAttackNamedCanSunder("Claw") && NaturalAttackNamedCanSunder("Slam")
            && NaturalAttackNamedCanSunder("Tentacle") && NaturalAttackNamedCanSunder("Tail Slap")
            && !NaturalAttackNamedCanSunder("Gore") && !NaturalAttackNamedCanSunder("Sting") && !NaturalAttackNamedCanSunder("Horn")
            && !NaturalAttackNamedCanSunder("Hoof"),
            "Bites, claws, slams and tentacles can sunder; gores, stings and other piercing-only attacks cannot (PHB p.158)");
        Assert(new NaturalAttackDefinition { Name = "Unarmed Strike" }.IsUnarmedStrike && !NaturalAttackNamedCanSunder("Unarmed Strike")
            && !new NaturalAttackDefinition { Name = "Slam" }.IsUnarmedStrike,
            "An 'Unarmed Strike' natural attack (the NPC monks) is bludgeoning but cannot sunder, as a PC's unarmed strike cannot (CMB-141)");

        var explicitPiercing = new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, PhysicalDamageTypes = P };
        NaturalAttackDefinition clone = explicitPiercing.Clone();
        Assert(explicitPiercing.GetPhysicalDamageTypes() == P && !explicitPiercing.CanSunder && clone.PhysicalDamageTypes == P,
            "An explicit PhysicalDamageTypes overrides the name and survives Clone");

        NPCDatabase.Init();
        NPCDefinition bugbear = NPCDatabase.Get("bugbear");
        NPCDefinition gnoll = NPCDatabase.Get("gnoll");
        Assert(bugbear != null && bugbear.NaturalAttacks.Count > 0 && bugbear.NaturalAttacks[0].GetPhysicalDamageTypes() == (B | P)
            && gnoll != null && gnoll.NaturalAttacks.Count > 0 && gnoll.NaturalAttacks[0].GetPhysicalDamageTypes() == S,
            "The weapon stand-ins in the data carry their weapon's types (bugbear morningstar B/P, gnoll battleaxe S; PHB p.116)");

        // Every damaging natural attack in the creature data is classified, except the names filed under
        // CMB-138 (no MM p.312 type: the owner classifies them) and the touches and rays that deal no
        // physical damage.
        var allowedUnclassified = new System.Collections.Generic.HashSet<string>
        {
            "Hoof", "Head butt", "Quills", "Snakes", "Chain",
            "Incorporeal Touch", "Paralyzing Touch", "Light Ray", "Shock"
        };
        var unexpected = new System.Collections.Generic.SortedSet<string>();
        foreach (NPCDefinition def in NPCDatabase.AllNPCs)
        {
            if (def == null || def.NaturalAttacks == null)
                continue;
            foreach (NaturalAttackDefinition natural in def.NaturalAttacks)
            {
                if (natural == null || natural.DamageDice <= 0 || natural.DamageCount <= 0)
                    continue;
                if (natural.GetPhysicalDamageTypes() == DamageBypassTag.None && !allowedUnclassified.Contains(natural.Name ?? string.Empty))
                    unexpected.Add($"{def.Id}:{natural.Name}");
            }
        }
        Assert(unexpected.Count == 0, "Every damaging natural attack in the data has a damage type or is filed under CMB-138"
            + (unexpected.Count > 0 ? ": " + string.Join(", ", unexpected) : string.Empty));
    }

    private static void TestNaturalWeaponSunderBySlashingOrBludgeoning()
    {
        // Owner ruling 2026-10-08 (CMB-102): a natural attack may sunder when it deals slashing or
        // bludgeoning damage (PHB p.158). One check, CanSunderWithAttack, for the PC buttons, the AI and
        // the NPC executor; the sunder rolls at the replaced natural attack's bonus and deals its damage.
        var bear = CreateBiteClawsCreature("NaturalSunderBear", 6, false);
        var goring = CreateNaturalCreature("NaturalSunderGore", 6, ("Gore", true, 1));
        var goreClaws = CreateNaturalCreature("NaturalSunderGoreClaws", 6, ("Gore", true, 1), ("Claw", false, 2));
        var unarmed = CreateTestCharacter("NaturalSunderUnarmed", "Fighter");
        var monk = CreateNaturalCreature("NaturalSunderMonk", 6, ("Unarmed Strike", true, 1));
        var fighter = CreateIterativeAttacker("NaturalSunderFighter");
        var defender = CreateWeakDefender("NaturalSunderDefender");
        bear.GridPosition = new Vector2Int(16, 12);
        defender.GridPosition = new Vector2Int(17, 12);
        bear.IsControllable = false;
        goring.IsControllable = false;
        unarmed.Stats.NaturalAttacks.Clear();
        ItemData sword = ItemDatabase.CloneItem(ItemID.WeaponLongsword);
        defender.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(sword, EquipSlot.RightHand);
        fighter.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        ActivePcScope scope = null;
        try
        {
            Assert(bear.CanSunderWithAttack(-1, out _) && fighter.CanSunderWithAttack(-1, out _)
                && !goring.CanSunderWithAttack(-1, out string goreReason) && goreReason.Contains("Gore")
                && !unarmed.CanSunderWithAttack(-1, out string unarmedReason) && !string.IsNullOrEmpty(unarmedReason)
                && !monk.CanSunderWithAttack(-1, out string monkReason) && monkReason == unarmedReason.Replace("NaturalSunderUnarmed", "NaturalSunderMonk"),
                "Sunder legality: a bite or a sword may sunder; a gore (piercing only), an empty hand without natural attacks or an 'Unarmed Strike' natural attack may not");
            Assert(!goreClaws.CanSunderWithAttack(-1, out _) && goreClaws.CanSunderWithAttack(1, out _)
                && goreClaws.GetFirstSunderCapableNaturalAttackIndex() == 1 && goreClaws.GetSunderNaturalAttackIndexForStep(0) == 0,
                "A gore/claw/claw creature: not with the gore at its first step, but with a claw");

            // Bite then claw: both sunders fail (forced rolls), so their modifiers can be compared: the claw's
            // is 5 lower (secondary attack, MM p.312), and neither gets a handedness modifier.
            ScenarioHooks.RollFilter = (sides, ctx, natural) =>
                ctx == "Sunder attack roll" ? 1 : ctx == "Sunder defense roll" ? 20 : natural;
            bool biteCommitted = bear.TryCommitManeuverSubstituteStep(-1, out int biteBab, out _, out _);
            SpecialAttackResult biteSunder = bear.ExecuteSpecialAttack(SpecialAttackType.Sunder, defender, sunderAttackBonusOverride: biteBab);
            bool clawCommitted = bear.TryCommitManeuverSubstituteStep(-1, out int clawBab, out _, out _);
            SpecialAttackResult clawSunder = bear.ExecuteSpecialAttack(SpecialAttackType.Sunder, defender, sunderAttackBonusOverride: clawBab);
            int biteMod = biteSunder.CheckTotal - biteSunder.CheckRoll;
            int clawMod = clawSunder.CheckTotal - clawSunder.CheckRoll;
            int expectedBiteMod = 6 + bear.Stats.STRMod + bear.Stats.SizeModifier + bear.Stats.ConditionAttackPenalty;
            Assert(biteCommitted && clawCommitted && biteBab == 6 && clawBab == 1 && !biteSunder.Success && !clawSunder.Success
                && biteSunder.Log.Contains("(Bite)") && clawSunder.Log.Contains("(Claw)") && biteMod == expectedBiteMod && biteMod - clawMod == 5,
                $"A natural sunder rolls at the replaced attack's bonus with no handedness modifier: bite {biteMod}, claw {clawMod} (expected {expectedBiteMod}, 5 apart)");

            // The second claw lands: damage is the claw's 1d4 plus half Strength (secondary, MM p.312).
            ScenarioHooks.RollFilter = (sides, ctx, natural) =>
                ctx == "Sunder attack roll" ? 20 : ctx == "Sunder defense roll" ? 1 : natural;
            bear.TryCommitManeuverSubstituteStep(-1, out int claw2Bab, out _, out _);
            SpecialAttackResult hit = bear.ExecuteSpecialAttack(SpecialAttackType.Sunder, defender, sunderAttackBonusOverride: claw2Bab);
            int halfStr = Mathf.FloorToInt(bear.Stats.STRMod * 0.5f);
            // The shared damage modifier lists the Strength share as "0.5× STR +N" (WeaponDamageBreakdown.Describe, CMB-003).
            bool halfStrListed = halfStr != 0
                ? hit.Log.Contains("0.5× STR " + CharacterStats.FormatMod(halfStr))
                : !hit.Log.Contains("STR");
            Assert(hit.Success && hit.Log.Contains("Damage: 1d4") && halfStrListed,
                $"A natural sunder deals the natural attack's damage (claw 1d4, half STR {CharacterStats.FormatMod(halfStr)})");
            ScenarioHooks.RollFilter = savedFilter;

            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.Log("  [SKIP] GameManager.Instance is null; PC/NPC sunder gates need Play mode");
                return;
            }

            Assert(!gm.CanUseSunderAttackOption(goring) && gm.GetRemainingSunderAttackActions(goring) == 0 && gm.CanUseSunderAttackOption(fighter),
                "PC Sunder button: not offered to a creature whose only natural attack is a gore");

            ItemData sword2 = ItemDatabase.CloneItem(ItemID.WeaponLongsword);
            defender.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(sword2, EquipSlot.RightHand);
            goring.GridPosition = new Vector2Int(17, 13);
            bool npcGoreSunder = gm.TryNPCSpecialAttackByTypeForAI(goring, defender, SpecialAttackType.Sunder);
            Assert(!npcGoreSunder && goring.ProgressiveAttackPool.MainHandStepsUsed == 0 && goring.Actions.HasStandardAction,
                "NPC sunder with a gore is refused and spends no attack step");

            MethodInfo consumeSunder = typeof(GameManager).GetMethod("TryConsumeSunderAttackAction", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(consumeSunder != null, "GameManager.TryConsumeSunderAttackAction is available for the PC natural sunder test");
            scope = ActivePcScope.Enter(gm, goreClaws);
            if (scope != null && consumeSunder != null)
            {
                // The PC gives up the next unused natural attack, as the NPC gives up the one at its step
                // (PC_NPC_PARITY; CMB-102 open item 8): with the gore next, the sunder is refused.
                object[] refusedArgs = { goreClaws, false, 0, 0, null, false, null };
                bool refused = !(bool)consumeSunder.Invoke(gm, refusedArgs);
                Assert(!gm.CanUseSunderAttackOption(goreClaws) && gm.GetRemainingSunderAttackActions(goreClaws) == 0 && refused
                    && goreClaws.ProgressiveAttackPool.MainHandStepsUsed == 0 && !gm.IsNaturalAttackSequenceIndexUsed(goreClaws, 0),
                    "PC Sunder for a gore/claw/claw creature while the gore is next: not offered, refused, no step spent (the NPC rule)");

                // The gore attacks first; then the Sunder button gives up the first claw at the claw's +1.
                bool goreCommitted = goreClaws.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
                gm.Combat_MarkNaturalAttackSequenceIndexUsed(0);
                Assert(goreCommitted && gm.CanUseSunderAttackOption(goreClaws) && gm.GetRemainingSunderAttackActions(goreClaws) == 2
                    && gm.GetCurrentSunderAttackBonus(goreClaws) == 1,
                    "PC Sunder after the gore attacked: 2 sunders (the claws) at the claw's BAB +1");
                object[] args = { goreClaws, false, 0, 0, null, false, null };
                bool consumed = (bool)consumeSunder.Invoke(gm, args);
                Assert(consumed && (int)args[2] == 1 && gm.IsNaturalAttackSequenceIndexUsed(goreClaws, 1) && !gm.IsNaturalAttackSequenceIndexUsed(goreClaws, 2)
                    && goreClaws.ProgressiveAttackPool.LastSubstituteNaturalAttackIndex == 1 && gm.GetRemainingSunderAttackActions(goreClaws) == 1,
                    "The PC sunder gives up the first claw at +1, and one claw sunder is left");
            }
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Natural sunder check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            scope?.Dispose();
            Cleanup(bear, goring, goreClaws, unarmed, monk, fighter, defender);
        }
    }

    private static void TestNaturalWeaponDisarmOneHandedOwnSize()
    {
        // Owner ruling 2026-10-08 (CMB-102): on a disarm roll a natural weapon counts as a one-handed weapon
        // of the creature's own size: no +4 two-handed or -4 light modifier, and the normal +4 per size
        // category of difference for the larger combatant only (PHB p.155).
        var bear = CreateBiteClawsCreature("NaturalDisarmSizeBear", 6, false);
        var defender = CreateWeakDefender("NaturalDisarmSizeDefender");
        var created = new System.Collections.Generic.List<CharacterController> { bear, defender };
        ItemData sword = ItemDatabase.CloneItem(ItemID.WeaponLongsword);
        defender.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(sword, EquipSlot.RightHand);
        try
        {
            MethodInfo rollDisarm = typeof(CharacterController).GetMethod("RollDisarmCheck", BindingFlags.Static | BindingFlags.NonPublic);
            Assert(rollDisarm != null, "RollDisarmCheck is available for the natural disarm size test");
            if (rollDisarm == null)
                return;

            int baseMod = bear.Stats.BaseAttackBonus + bear.Stats.STRMod + bear.Stats.SizeModifier + bear.Stats.ConditionAttackPenalty;
            int natural = RollNaturalDisarmModifier(rollDisarm, bear, defender, null, sword, "Attacker");
            int oneHanded = RollNaturalDisarmModifier(rollDisarm, bear, defender, ItemDatabase.CloneItem(ItemID.WeaponLongsword), sword, "Attacker");
            int twoHanded = RollNaturalDisarmModifier(rollDisarm, bear, defender, ItemDatabase.CloneItem(ItemID.WeaponGreatsword), sword, "Attacker");
            int light = RollNaturalDisarmModifier(rollDisarm, bear, defender, ItemDatabase.CloneItem(ItemID.WeaponDagger), sword, "Attacker");
            Assert(CharacterController.NaturalWeaponDisarmHandednessModifier == 0 && natural == baseMod && natural == oneHanded
                && twoHanded == natural + 4 && light == natural - 4,
                $"Medium natural weapon disarms as a one-handed weapon: natural {natural}, longsword {oneHanded}, greatsword {twoHanded}, dagger {light}");

            int defenderVsMedium = RollNaturalDisarmModifier(rollDisarm, bear, defender, null, sword, "Defender");
            bear.Stats.CurrentSizeCategory = SizeCategory.Large;
            int largeBase = bear.Stats.BaseAttackBonus + bear.Stats.STRMod + bear.Stats.SizeModifier + bear.Stats.ConditionAttackPenalty;
            int largeNatural = RollNaturalDisarmModifier(rollDisarm, bear, defender, null, sword, "Attacker");
            int largeOneHanded = RollNaturalDisarmModifier(rollDisarm, bear, defender, ItemDatabase.CloneItem(ItemID.WeaponLongsword), sword, "Attacker");
            int defenderVsLarge = RollNaturalDisarmModifier(rollDisarm, bear, defender, null, sword, "Defender");
            bear.Stats.CurrentSizeCategory = SizeCategory.Medium;
            // PHB p.155: only the larger combatant gets the +4 per size category; the smaller one takes no penalty.
            Assert(largeNatural == largeBase + 4 && largeNatural == largeOneHanded && defenderVsLarge == defenderVsMedium,
                $"A Large natural-weapon creature gets +4 for one size category over a Medium defender, who takes no penalty (natural {largeNatural}, base {largeBase}; defender {defenderVsMedium} -> {defenderVsLarge})");

            // The other way round: a Large defender gets the +4 and the Medium natural attacker takes nothing.
            int attackerVsMedium = RollNaturalDisarmModifier(rollDisarm, bear, defender, null, sword, "Attacker");
            defender.Stats.CurrentSizeCategory = SizeCategory.Large;
            int largeDefenderBase = defender.Stats.BaseAttackBonus + defender.Stats.STRMod + defender.Stats.SizeModifier + defender.Stats.ConditionAttackPenalty;
            int defenderLarge = RollNaturalDisarmModifier(rollDisarm, bear, defender, null, sword, "Defender");
            int attackerVsLarge = RollNaturalDisarmModifier(rollDisarm, bear, defender, null, sword, "Attacker");
            defender.Stats.CurrentSizeCategory = SizeCategory.Medium;
            Assert(defenderLarge == largeDefenderBase + 4 && attackerVsLarge == attackerVsMedium,
                $"A Large defender gets +4 against a Medium natural-weapon disarmer, who takes no penalty (defender {defenderLarge}, base {largeDefenderBase}; attacker {attackerVsMedium} -> {attackerVsLarge})");

            // An unarmed strike an NPC lists as a natural attack (the monks) is unarmed: the light -4, and it
            // catches the weapon (PHB p.155), as a PC's unarmed strike does.
            var monk = CreateNaturalCreature("NaturalDisarmUnarmedMonk", 6, ("Unarmed Strike", true, 1));
            created.Add(monk);
            int monkBase = monk.Stats.BaseAttackBonus + monk.Stats.STRMod + monk.Stats.SizeModifier + monk.Stats.ConditionAttackPenalty;
            int monkMod = RollNaturalDisarmModifier(rollDisarm, monk, defender, null, sword, "Attacker");
            MethodInfo catches = typeof(CharacterController).GetMethod("DisarmerCatchesWeapon", BindingFlags.Static | BindingFlags.NonPublic);
            Assert(catches != null && monk.UsesInnateNaturalAttackSequence() && !monk.FightsWithNaturalWeapons() && monkMod == monkBase - 4
                && (bool)catches.Invoke(null, new object[] { monk, null }) && !(bool)catches.Invoke(null, new object[] { bear, null }),
                $"An 'Unarmed Strike' natural attack disarms as an unarmed strike: -4 ({monkMod} vs base {monkBase}) and catches the weapon; a bite does not");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Natural disarm size check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            Cleanup(created.ToArray());
        }
    }

    private static int RollNaturalDisarmModifier(MethodInfo rollDisarm, CharacterController attacker, CharacterController defender,
        ItemData attackerItem, ItemData defenderItem, string side)
        => GetDisarmModifier(rollDisarm.Invoke(null, new object[] { attacker, defender, attackerItem, defenderItem, EquipSlot.RightHand, 0, string.Empty, null, 0 }), side);

    /// <summary>A forced failing sunder by <paramref name="attacker"/>: (attacker modifier, defender modifier) of its opposed roll.</summary>
    private static (int attack, int defense, SpecialAttackResult result) ForcedSunderModifiers(CharacterController attacker, CharacterController defender, int? bab = null)
    {
        SpecialAttackResult r = attacker.ExecuteSpecialAttack(SpecialAttackType.Sunder, defender, sunderAttackBonusOverride: bab);
        return r == null ? (int.MinValue, int.MinValue, null) : (r.CheckTotal - r.CheckRoll, r.OpposedTotal - r.OpposedRoll, r);
    }

    private static int PlainOpposedMod(CharacterController c)
        => c.Stats.BaseAttackBonus + c.Stats.STRMod + c.Stats.SizeModifier + c.Stats.ConditionAttackPenalty;

    private static void TestSunderOpposedRollSizeAndHandedness()
    {
        // PHB p.158 Step 2: both sides roll with their own weapon (+4 two-handed, -4 light) and the larger
        // combatant gets +4 per size category of difference; the smaller one takes no penalty. A natural weapon
        // gets no handedness modifier (as on a disarm roll; pending owner confirmation for sunder, CMB-141).
        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        var bear = CreateBiteClawsCreature("SunderRollBear", 6, false);
        var fighter = CreateIterativeAttacker("SunderRollFighter");
        var defender = CreateWeakDefender("SunderRollDefender");
        bear.GridPosition = new Vector2Int(16, 12);
        fighter.GridPosition = new Vector2Int(18, 12);
        defender.GridPosition = new Vector2Int(17, 12);
        var defenderInventory = defender.GetComponent<InventoryComponent>().CharacterInventory;
        var fighterInventory = fighter.GetComponent<InventoryComponent>().CharacterInventory;
        try
        {
            ScenarioHooks.RollFilter = (sides, ctx, natural) =>
                ctx == "Sunder attack roll" ? 1 : ctx == "Sunder defense roll" ? 20 : natural;
            int bearBase = PlainOpposedMod(bear) + 0; // the bite at full BAB, no handedness, no size
            int defenderBase = PlainOpposedMod(defender);

            defenderInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
            var vsLongsword = ForcedSunderModifiers(bear, defender, 6);
            defenderInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponGreataxe), EquipSlot.RightHand);
            var vsGreataxe = ForcedSunderModifiers(bear, defender, 6);
            defenderInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponDagger), EquipSlot.RightHand);
            var vsDagger = ForcedSunderModifiers(bear, defender, 6);
            Assert(vsLongsword.attack == bearBase && vsGreataxe.attack == bearBase && vsDagger.attack == bearBase
                && vsLongsword.defense == defenderBase && vsGreataxe.defense == defenderBase + 4 && vsDagger.defense == defenderBase - 4,
                $"The defender rolls with its own weapon: longsword {vsLongsword.defense}, greataxe {vsGreataxe.defense}, dagger {vsDagger.defense} (base {defenderBase}); the bite {vsLongsword.attack} gets no handedness modifier");

            defenderInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
            bear.Stats.CurrentSizeCategory = SizeCategory.Large;
            int largeBearBase = PlainOpposedMod(bear);
            var largeBear = ForcedSunderModifiers(bear, defender, 6);
            bear.Stats.CurrentSizeCategory = SizeCategory.Medium;
            defender.Stats.CurrentSizeCategory = SizeCategory.Large;
            int largeDefenderBase = PlainOpposedMod(defender);
            var largeDefender = ForcedSunderModifiers(bear, defender, 6);
            defender.Stats.CurrentSizeCategory = SizeCategory.Medium;
            Assert(largeBear.attack == largeBearBase + 4 && largeBear.defense == defenderBase
                && largeDefender.attack == bearBase && largeDefender.defense == largeDefenderBase + 4,
                $"The larger combatant gets +4 per size category and the smaller none: Large bite {largeBear.attack} (base {largeBearBase}) vs {largeBear.defense}; bite {largeDefender.attack} vs Large defender {largeDefender.defense} (base {largeDefenderBase})");

            // BAB +1, so the forced rolls (1 against 20) still lose and the defender's weapon survives.
            fighter.Stats.BaseAttackBonusOverride = 1;
            fighterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponGreatsword), EquipSlot.RightHand);
            int fighterBase = PlainOpposedMod(fighter);
            var greatsword = ForcedSunderModifiers(fighter, defender);
            fighterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponDagger), EquipSlot.RightHand);
            defenderInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponGreataxe), EquipSlot.RightHand);
            var daggerVsGreataxe = ForcedSunderModifiers(fighter, defender);
            Assert(greatsword.result != null && !greatsword.result.Success && daggerVsGreataxe.result != null && !daggerVsGreataxe.result.Success
                && greatsword.attack == fighterBase + 4 && greatsword.defense == defenderBase
                && daggerVsGreataxe.attack == fighterBase - 4 && daggerVsGreataxe.defense == defenderBase + 4,
                $"Weapon sunders: greatsword {greatsword.attack} vs longsword {greatsword.defense}; dagger {daggerVsGreataxe.attack} vs greataxe {daggerVsGreataxe.defense} (bases {fighterBase}, {defenderBase})");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Sunder opposed roll check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            Cleanup(bear, fighter, defender);
        }
    }

    private static void TestNaturalSunderAtHasteStep()
    {
        // Haste's extra natural attack is made with a natural attack of the attacker's choice (CMB-106). A
        // sunder in its place uses the default Haste attack when that one can sunder, else the first natural
        // attack that can: for a hasted gore/claw/claw creature (the gore has the highest bonus), a claw.
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; Haste natural sunder check needs Play mode");
            return;
        }

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        System.Action<CharacterController, CharacterController, SpecialAttackType, SpecialAttackResult> savedManeuver = ScenarioHooks.ManeuverResolved;
        var npc = CreateNaturalCreature("HasteSunderNpc", 6, ("Gore", true, 1), ("Claw", false, 2));
        var pc = CreateNaturalCreature("HasteSunderPc", 6, ("Gore", true, 1), ("Claw", false, 2));
        var defender = CreateWeakDefender("HasteSunderDefender");
        npc.ApplyHasteEffect(10, null);
        pc.ApplyHasteEffect(10, null);
        npc.IsControllable = false;
        npc.GridPosition = new Vector2Int(16, 12);
        defender.GridPosition = new Vector2Int(17, 12);
        pc.GridPosition = new Vector2Int(18, 12);
        defender.Stats.CanMakeAttacksOfOpportunity = false;
        defender.Stats.AdjustMaxHP(500);
        defender.Stats.CurrentHP += 500;
        defender.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
        ActivePcScope scope = null;
        SpecialAttackResult npcSunder = null;
        try
        {
            ScenarioHooks.RollFilter = (sides, ctx, natural) =>
                ctx == "Sunder attack roll" ? 1 : ctx == "Sunder defense roll" ? 20 : natural;
            ScenarioHooks.ManeuverResolved = (attacker, target, type, result) =>
            {
                if (attacker == npc && type == SpecialAttackType.Sunder)
                    npcSunder = result;
            };

            Assert(npc.HasHasteExtraNaturalAttack() && npc.GetDefaultHasteNaturalAttackIndex() == 0
                && npc.GetSunderNaturalAttackIndexForStep(npc.GetHasteExtraNaturalStepIndex()) == 1,
                "Hasted gore/claw/claw: the default Haste attack is the gore; a sunder at the Haste step uses the first claw");

            // NPC: the gore and both claws attack (three steps), then the sunder replaces Haste's extra attack.
            bool stepsCommitted = true;
            for (int i = 0; i < 3; i++)
                stepsCommitted &= npc.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
            bool legal = npc.CanSunderWithAttack(-1, out string why);
            gm.TryNPCSpecialAttackByTypeForAI(npc, defender, SpecialAttackType.Sunder);
            int expectedClawMod = 1 + npc.Stats.STRMod + npc.Stats.SizeModifier + npc.Stats.ConditionAttackPenalty;
            Assert(stepsCommitted && legal && npcSunder != null && npcSunder.Log.Contains("(Claw)")
                && npcSunder.CheckTotal - npcSunder.CheckRoll == expectedClawMod
                && npc.ProgressiveAttackPool.HasteExtraNaturalAttackUsed && npc.ProgressiveAttackPool.LastSubstituteNaturalAttackIndex == 1
                && npc.ProgressiveAttackPool.MainHandStepsUsed == 4,
                $"NPC sunder at the Haste step: made with a claw at {expectedClawMod} and Haste's extra attack marked used"
                + (npcSunder != null ? $" (got {npcSunder.CheckTotal - npcSunder.CheckRoll}, last substitute {npc.ProgressiveAttackPool.LastSubstituteNaturalAttackIndex})" : $" (no sunder; {why})"));

            // PC: with every natural attack used, the Sunder button gives up Haste's extra attack, made with a claw.
            MethodInfo consumeSunder = typeof(GameManager).GetMethod("TryConsumeSunderAttackAction", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(consumeSunder != null, "GameManager.TryConsumeSunderAttackAction is available for the Haste natural sunder test");
            scope = ActivePcScope.Enter(gm, pc);
            if (scope != null && consumeSunder != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    pc.TryCommitAttack(AttackStepKind.NaturalSequence, out _, out _);
                    gm.Combat_MarkNaturalAttackSequenceIndexUsed(i);
                }

                bool offered = gm.CanUseSunderAttackOption(pc) && gm.GetRemainingSunderAttackActions(pc) == 1 && gm.GetCurrentSunderAttackBonus(pc) == 1;
                object[] args = { pc, false, 0, 0, null, false, null };
                bool consumed = (bool)consumeSunder.Invoke(gm, args);
                Assert(offered && consumed && (int)args[2] == 1 && pc.ProgressiveAttackPool.HasteExtraNaturalAttackUsed
                    && pc.ProgressiveAttackPool.LastSubstituteNaturalAttackIndex == 1 && gm.GetRemainingSunderAttackActions(pc) == 0,
                    $"PC sunder in place of Haste's extra attack: offered once at the claw's +1, made with a claw (offered {offered}, BAB {(int)args[2]})");
            }
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Haste natural sunder check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            ScenarioHooks.ManeuverResolved = savedManeuver;
            scope?.Dispose();
            Cleanup(npc, pc, defender);
        }
    }

    private static void TestNaturalSecondaryGrapplePenaltyOnTouchOnly()
    {
        // Owner ruling 2026-10-08 (CMB-102): a grapple that replaces a secondary natural attack takes the
        // -5 (-2 with Multiattack) on its touch attack (an attack roll, MM p.312) but not on the opposed
        // grapple check. An iterative grapple keeps its step BAB on both (PHB p.156).
        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        var created = new System.Collections.Generic.List<CharacterController>();
        try
        {
            ScenarioHooks.RollFilter = (sides, ctx, natural) =>
                ctx == "Touch attack" ? 20 : ctx == "Grapple check" ? 10 : natural;

            foreach (bool multiattack in new[] { false, true })
            {
                string tag = multiattack ? " (Multiattack)" : string.Empty;
                int expectedTouchBab = multiattack ? 4 : 1;
                var primary = CreateBiteClawsCreature("SecondaryGrapplePrimary" + tag, 6, multiattack);
                var secondary = CreateBiteClawsCreature("SecondaryGrappleClaw" + tag, 6, multiattack);
                var target1 = CreateWeakDefender("SecondaryGrappleTarget1" + tag);
                var target2 = CreateWeakDefender("SecondaryGrappleTarget2" + tag);
                created.AddRange(new[] { primary, secondary, target1, target2 });
                primary.GridPosition = new Vector2Int(2, 2);
                target1.GridPosition = new Vector2Int(3, 2);
                secondary.GridPosition = new Vector2Int(2, 6);
                target2.GridPosition = new Vector2Int(3, 6);

                primary.TryCommitManeuverSubstituteStep(-1, out int biteBab, out _, out _);
                SpecialAttackResult biteGrapple = primary.ExecuteSpecialAttack(SpecialAttackType.Grapple, target1, grappleAttackBonusOverride: biteBab);

                secondary.TryCommitManeuverSubstituteStep(-1, out _, out _, out _); // the bite, given up for something else
                secondary.TryCommitManeuverSubstituteStep(-1, out int clawBab, out _, out _);
                SpecialAttackResult clawGrapple = secondary.ExecuteSpecialAttack(SpecialAttackType.Grapple, target2, grappleAttackBonusOverride: clawBab);

                int biteCheckMod = biteGrapple.CheckTotal - biteGrapple.CheckRoll;
                int clawCheckMod = clawGrapple.CheckTotal - clawGrapple.CheckRoll;
                Assert(biteBab == 6 && clawBab == expectedTouchBab && clawGrapple.Log.Contains($"  BAB: {CharacterStats.FormatMod(expectedTouchBab)}")
                    && secondary.GetGrappleCheckBabAfterTouchAttack(clawBab, true) == 6,
                    $"A grapple replacing a secondary claw makes its touch attack at BAB {CharacterStats.FormatMod(expectedTouchBab)} (MM p.312)" + tag);
                Assert(biteCheckMod == clawCheckMod && clawCheckMod == 6 + secondary.Stats.STRMod + secondary.GetGrappleSizeModifier(),
                    $"Its opposed grapple check uses the full BAB, as the bite's does: check modifiers bite {biteCheckMod}, claw {clawCheckMod}" + tag);
            }

            var fighter = CreateIterativeAttacker("IterativeGrappleCheckBab");
            created.Add(fighter);
            Assert(fighter.GetGrappleCheckBabAfterTouchAttack(6, true) == 6 && fighter.GetGrappleCheckBabAfterTouchAttack(1, true) == 1,
                "An iterative grapple keeps its step BAB on the grapple check (PHB p.156)");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Secondary natural grapple check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            Cleanup(created.ToArray());
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
    /// purpose: its attack after a trip that lands (PHB p.96, CMB-079) would add an attack outside the
    /// sequence, and these tests count only the iterative steps the AI chooses. The trip's attack of
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

    // ── Grapple attacks with natural weapons (PHB p.156; MM p.314; CMB-127) ──
    // Each grapple attack action of a creature fighting with its natural attacks is ONE natural attack of its choice,
    // at that attack's normal bonus with the -4 for attacking in a grapple (the attacker's Grappled condition), in
    // place of one natural attack of its sequence. Rake adds its claws once a turn.

    /// <summary>Commits the grapple step that gives up natural attack <paramref name="index"/>, then makes that attack (the PC and AI order).</summary>
    private static SpecialAttackResult GrappleNaturalAttack(CharacterController attacker, int index, bool haste, out bool committed)
    {
        committed = attacker.TryCommitManeuverSubstituteStep(index, out int bab, out _, out _, haste);
        return committed
            ? attacker.ResolveGrappleAction(GrappleActionType.AttackUnarmed, null, null, bab, index, haste)
            : null;
    }

    private static int CheckMod(SpecialAttackResult r) => r != null ? r.CheckTotal - r.CheckRoll : int.MinValue;

    private static void TestGrappleNaturalAttackIsOneAttack()
    {
        var bear = CreateBiteClawsCreature("GrappleNaturalOne", 4, false);
        var control = CreateBiteClawsCreature("GrappleNaturalOneControl", 4, false);
        var target = CreateHasteTarget("GrappleNaturalOneTarget");
        try
        {
            bear.StartNewTurn();
            ForceGrappleState(bear, target);
            var options = bear.GetGrappleNaturalAttackOptions();
            Assert(bear.UsesNaturalAttacksForGrappleAttack() && options.Count == 3
                && options[0].NaturalAttackIndex == 0 && options[2].NaturalAttackIndex == 2 && !options.Exists(o => o.IsHasteExtraAttack),
                "CMB-127: a grappling bite/claw/claw creature may make any of its 3 natural attacks with a grapple attack");

            int hpBefore = target.Stats.CurrentHP;
            SpecialAttackResult claw = GrappleNaturalAttack(bear, 1, false, out bool clawCommitted);
            FullAttackResult controlClaw = control.FullAttack(target, false, 0, null, null, startAttackIndex: 1, maxAttacks: 1);
            FullAttackResult controlBite = control.FullAttack(target, false, 0, null, null, startAttackIndex: 0, maxAttacks: 1);
            Assert(clawCommitted && claw != null && claw.Log.Contains("with its Claw while grappling") && !claw.Log.Contains("Bite")
                && bear.ProgressiveAttackPool.IsNaturalAttackUsed(1) && !bear.ProgressiveAttackPool.IsNaturalAttackUsed(0)
                && bear.ProgressiveAttackPool.MainHandStepsUsed == 1,
                "CMB-127: one grapple attack is one natural attack (the claw picked), and only that natural attack is used");
            Assert(controlClaw.Attacks.Count == 1 && CheckMod(claw) == AttackModifier(controlClaw.Attacks[0]) - 4,
                $"CMB-127: the grapple claw rolls at the claw's normal bonus -4 for grappling (PHB p.156; got {CheckMod(claw)}, ungrappled {AttackModifier(controlClaw.Attacks[0])})");

            SpecialAttackResult bite = GrappleNaturalAttack(bear, 0, false, out bool biteCommitted);
            Assert(biteCommitted && bite != null && bite.Log.Contains("with its Bite") && CheckMod(bite) - CheckMod(claw) == 5
                && CheckMod(bite) == AttackModifier(controlBite.Attacks[0]) - 4 && bear.ProgressiveAttackPool.IsFullAttack,
                "CMB-127: the second grapple attack (the primary bite) is 5 above the secondary claw (MM p.312) and makes it a full attack");

            var left = bear.GetGrappleNaturalAttackOptions();
            SpecialAttackResult claw2 = GrappleNaturalAttack(bear, 2, false, out bool claw2Committed);
            bool fourth = bear.TryCommitManeuverSubstituteStep(-1, out _, out _, out string fourthReason);
            Assert(left.Count == 1 && left[0].NaturalAttackIndex == 2 && claw2Committed && claw2 != null
                && !fourth && !string.IsNullOrEmpty(fourthReason) && bear.GetGrappleNaturalAttackOptions().Count == 0,
                "CMB-127: a full attack of grapple attacks makes each natural attack once (3 with BAB +4), then nothing is left");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Grapple natural attack check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Cleanup(bear, control, target);
        }
    }

    private static void TestGrappleNaturalAttackHasteAndOutOfOrderSteps()
    {
        var bear = CreateHastedBiteClaws("GrappleNaturalHaste", 4);
        var target = CreateHasteTarget("GrappleNaturalHasteTarget");
        try
        {
            bear.StartNewTurn();
            ForceGrappleState(bear, target);
            SpecialAttackResult bite = GrappleNaturalAttack(bear, 0, false, out bool biteOk);
            var options = bear.GetGrappleNaturalAttackOptions();
            Assert(biteOk && bite != null && options.Count == 3 && options[2].NaturalAttackIndex == 0 && options[2].IsHasteExtraAttack
                && !options[0].IsHasteExtraAttack && !options[1].IsHasteExtraAttack,
                "CMB-127/CMB-106: after the bite, a hasted grappler may attack with either claw, or with the bite again as Haste's extra attack");

            SpecialAttackResult hasteBite = GrappleNaturalAttack(bear, 0, true, out bool hasteOk);
            Assert(hasteOk && hasteBite != null && hasteBite.Log.Contains("Haste") && CheckMod(hasteBite) == CheckMod(bite)
                && bear.ProgressiveAttackPool.HasteExtraNaturalAttackUsed && !bear.CanUseHasteExtraNaturalAttack(),
                "CMB-127/CMB-106: the Haste grapple attack is the bite at the bite's bonus, and uses Haste's extra attack");

            SpecialAttackResult c1 = GrappleNaturalAttack(bear, 1, false, out bool c1Ok);
            SpecialAttackResult c2 = GrappleNaturalAttack(bear, 2, false, out bool c2Ok);
            bool fifth = bear.TryCommitManeuverSubstituteStep(-1, out _, out _, out _);
            Assert(c1Ok && c2Ok && c1 != null && c2 != null && !fifth,
                "CMB-127: a hasted bite/claw/claw grappler makes 4 grapple attacks (bite, Haste bite, claw, claw), and no fifth");

            // Out of sequence order: a grapple claw at step 0, then a maneuver substitute with no natural attack named
            // (the NPC executor's call) gives up the bite, the first natural attack not used, at the bite's BAB
            // (CMB-127; MM p.312). Grapple checks do not come here: they take iterative steps.
            bear.StartNewTurn();
            GrappleNaturalAttack(bear, 1, false, out bool firstClawOk);
            bool pinStep = bear.TryCommitManeuverSubstituteStep(-1, out int pinBab, out int pinStepIndex, out _);
            int resolvedThird = bear.ResolveSubstituteNaturalAttackIndex(2, out bool thirdIsHaste);
            Assert(firstClawOk && pinStep && pinStepIndex == 1 && pinBab == 4 && bear.ProgressiveAttackPool.IsNaturalAttackUsed(0)
                && resolvedThird == 2 && !thirdIsHaste,
                "CMB-127: after a grapple claw at step 0, a maneuver at step 1 gives up the unused bite (BAB +4), and step 2 the other claw");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Grapple natural Haste check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Cleanup(bear, target);
        }
    }

    private static void TestGrappleNaturalAttackRakeOncePerTurn()
    {
        var cat = CreateBiteClawsCreature("GrappleNaturalRake", 4, false);
        var target = CreateHasteTarget("GrappleNaturalRakeTarget");
        try
        {
            cat.Stats.HasRake = true;
            cat.Stats.SetRakeAttack(new NaturalAttackDefinition { Name = "Rake", DamageDice = 4, DamageCount = 1, Count = 2, IsPrimary = true, BonusDamageSource = DamageBonusSource.StrengthHalf });
            cat.StartNewTurn();
            ForceGrappleState(cat, target);
            SpecialAttackResult first = GrappleNaturalAttack(cat, 0, false, out _);
            SpecialAttackResult second = GrappleNaturalAttack(cat, 1, false, out _);
            Assert(first != null && first.Log.Contains("Rake: 2 extra claw attack(s)") && second != null && !second.Log.Contains("Rake:")
                && cat.ProgressiveAttackPool.RakeUsedThisTurn,
                "CMB-127: the rake's 2 claws come with the first grapple natural attack of the turn, not with every one (MM p.314)");

            cat.StartNewTurn();
            SpecialAttackResult nextTurn = GrappleNaturalAttack(cat, 2, false, out _);
            Assert(nextTurn != null && nextTurn.Log.Contains("Rake: 2 extra claw attack(s)"),
                "CMB-127: the next turn's first grapple natural attack rakes again");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Grapple natural rake check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Cleanup(cat, target);
        }
    }

    private static void TestGrappleNaturalAttackStepsAndAiChoice()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; the grapple step and AI choice checks need Play mode");
            return;
        }

        var bear = CreateBiteClawsCreature("GrappleNaturalSteps", 4, false);
        var fighter = CreateTestCharacter("GrappleNaturalStepsFighter", "Fighter"); // BAB +12: +12/+7/+2
        var stinger = CreateBiteClawsCreature("GrappleNaturalStepsSting", 4, false);
        var target = CreateHasteTarget("GrappleNaturalStepsTarget");
        var target2 = CreateHasteTarget("GrappleNaturalStepsTarget2");
        var target3 = CreateHasteTarget("GrappleNaturalStepsTarget3");
        try
        {
            bear.StartNewTurn();
            fighter.StartNewTurn();
            ForceGrappleState(bear, target);
            ForceGrappleState(fighter, target2);
            Assert(gm.GetRemainingGrappleAttackActions(bear) == 3 && gm.GetCurrentGrappleAttackBonus(bear) == 4
                && gm.GetRemainingGrappleAttackActions(bear, GrappleActionType.AttackUnarmed) == 3
                && gm.GetRemainingGrappleAttackActions(bear, GrappleActionType.PinOpponent) == 1
                && gm.GetRemainingGrappleAttackActions(fighter) == 3 && gm.GetRemainingGrappleAttackActions(fighter, GrappleActionType.AttackUnarmed) == 3,
                $"CMB-127/CMB-146: a grappling BAB +4 bite/claw/claw creature has 3 grapple natural attacks (one per natural attack) but 1 grapple check (its iterative ladder, PHB p.156); a BAB +12 fighter keeps its 3 iteratives for both (got {gm.GetRemainingGrappleAttackActions(bear)}, {gm.GetRemainingGrappleAttackActions(bear, GrappleActionType.PinOpponent)}, {gm.GetRemainingGrappleAttackActions(fighter)})");

            CharacterController.GrappleNaturalAttackOption pick = gm.ChooseGrappleNaturalAttackForAI(bear, target);
            Assert(pick.NaturalAttackIndex == 0 && !pick.IsHasteExtraAttack,
                "CMB-127: with no riders the AI's grapple attack is the highest-bonus natural attack (the bite)");
            GrappleNaturalAttack(bear, 0, false, out _);
            CharacterController.GrappleNaturalAttackOption next = gm.ChooseGrappleNaturalAttackForAI(bear, target);
            Assert(next.NaturalAttackIndex == 1 && gm.GetRemainingGrappleAttackActions(bear) == 2,
                "CMB-127: once the bite is used the AI attacks with a claw, and 2 grapple actions are left");

            stinger.Stats.NaturalAttacks.Clear();
            stinger.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, IsPrimary = true });
            stinger.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Sting", DamageDice = 4, DamageCount = 1, Count = 1, IsPrimary = false, PoisonOnHitId = "test_poison" });
            stinger.StartNewTurn();
            ForceGrappleState(stinger, target3);
            CharacterController.GrappleNaturalAttackOption sting = gm.ChooseGrappleNaturalAttackForAI(stinger, target3);
            Assert(sting.NaturalAttackIndex == 1,
                "CMB-127: the AI's grapple attack takes the poison sting over the higher-bonus bite against a living foe (NaturalAttackChoice)");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Grapple natural step check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Cleanup(bear, fighter, stinger, target, target2, target3);
        }
    }

    /// <summary>Commits one grapple action's step through GameManager.TryConsumeGrappleAttackAction (the PC and AI path).</summary>
    private static bool ConsumeGrappleStep(GameManager gm, CharacterController attacker, GrappleActionType action, int naturalIndex, out int bab, out string reason)
    {
        MethodInfo consume = typeof(GameManager).GetMethod("TryConsumeGrappleAttackAction", BindingFlags.Instance | BindingFlags.NonPublic);
        object[] args = { attacker, 0, 0, null, (GrappleActionType?)action, naturalIndex, false };
        bool ok = (bool)consume.Invoke(gm, args);
        bab = (int)args[1];
        reason = args[3] as string;
        return ok;
    }

    private static void TestGrappleChecksStayIterativeForNaturalAttackers()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; the grapple check step checks need Play mode");
            return;
        }

        var bear = CreateBiteClawsCreature("GrappleCheckIterative", 4, false);
        var bear2 = CreateBiteClawsCreature("GrappleCheckIterative2", 4, false);
        var target = CreateHasteTarget("GrappleCheckIterativeTarget");
        var target2 = CreateHasteTarget("GrappleCheckIterativeTarget2");
        try
        {
            // PHB p.156: grapple checks in place of attacks follow the BAB ladder. Only the grapple natural attack takes
            // a natural step (CMB-127), so a pin is an iterative step at full BAB and gives up no natural attack.
            bear.StartNewTurn();
            ForceGrappleState(bear, target);
            bool pinOk = ConsumeGrappleStep(gm, bear, GrappleActionType.PinOpponent, -1, out int pinBab, out string pinReason);
            Assert(pinOk && pinBab == 4 && bear.ProgressiveAttackPool.LastSubstituteNaturalAttackIndex == -1
                && bear.GetGrappleNaturalAttackOptions().Count == 3
                && !gm.CanUseGrappleAttackOption(bear, GrappleActionType.PinOpponent)
                && !gm.CanUseGrappleAttackOption(bear, GrappleActionType.OpposedGrappleEscape)
                && gm.GetRemainingGrappleAttackActions(bear, GrappleActionType.AttackUnarmed) == 2,
                $"CMB-127: a BAB +4 bite/claw/claw grappler's pin is an iterative step at its full BAB +4 that gives up no natural attack; no second grapple check follows, 2 grapple natural attacks do (got ok={pinOk} bab={pinBab} reason={pinReason})");

            // The lion case: after one grapple claw attack the one iterative step is used, so a pin or escape check is refused.
            bear2.StartNewTurn();
            ForceGrappleState(bear2, target2);
            bool clawOk = ConsumeGrappleStep(gm, bear2, GrappleActionType.AttackUnarmed, 1, out int clawBab, out _);
            bool pinAfter = ConsumeGrappleStep(gm, bear2, GrappleActionType.PinOpponent, -1, out _, out string refusal);
            Assert(clawOk && clawBab == -1 && bear2.ProgressiveAttackPool.IsNaturalAttackUsed(1)
                && !pinAfter && !string.IsNullOrEmpty(refusal) && bear2.ProgressiveAttackPool.MainHandStepsUsed == 1
                && !gm.CanUseGrappleAttackOption(bear2, GrappleActionType.PinOpponent)
                && !gm.CanUseGrappleAttackOption(bear2, GrappleActionType.OpposedGrappleEscape)
                && gm.CanUseGrappleAttackOption(bear2, GrappleActionType.AttackUnarmed),
                $"CMB-127: after one grapple claw attack (at the claw's -1) a BAB +4 natural attacker is refused a pin or escape check and keeps its other natural attacks (got claw={clawOk}/{clawBab}, pin={pinAfter})");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Grapple check step check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Cleanup(bear, bear2, target, target2);
        }
    }

    private static void TestGrappleNaturalAttackRefusesUsedOrMissingAttack()
    {
        var beast = CreateBiteClawsCreature("GrappleNaturalRefuse", 4, false);
        var clawBite = CreateBiteClawsCreature("GrappleNaturalFallback", 4, false);
        var target = CreateHasteTarget("GrappleNaturalRefuseTarget");
        var target2 = CreateHasteTarget("GrappleNaturalFallbackTarget");
        try
        {
            beast.StartNewTurn();
            ForceGrappleState(beast, target);
            GrappleNaturalAttack(beast, 1, false, out bool firstOk);
            int stepsBefore = beast.ProgressiveAttackPool.MainHandStepsUsed;
            bool again = beast.TryCommitManeuverSubstituteStep(1, out _, out _, out string againReason);
            bool fakeHaste = beast.TryCommitManeuverSubstituteStep(0, out _, out _, out _, true);
            bool outOfRange = beast.TryCommitManeuverSubstituteStep(7, out _, out _, out _);
            Assert(firstOk && !again && !fakeHaste && !outOfRange && againReason != null && againReason.Contains("already used")
                && beast.ProgressiveAttackPool.MainHandStepsUsed == stepsBefore,
                "CMB-127: a named natural attack already used this turn, a Haste attack without Haste, or a missing one is refused before any step is spent");

            // Claw (secondary) first, bite (primary) second: a step committed with no natural attack named gives up the claw.
            clawBite.Stats.NaturalAttacks.Clear();
            clawBite.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 1, IsPrimary = false });
            clawBite.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, IsPrimary = true });
            clawBite.StartNewTurn();
            ForceGrappleState(clawBite, target2);
            bool stepOk = clawBite.TryCommitManeuverSubstituteStep(-1, out int stepBab, out _, out _);
            SpecialAttackResult fallback = stepOk ? clawBite.ResolveGrappleAction(GrappleActionType.AttackUnarmed, null, null, stepBab) : null;
            Assert(stepOk && fallback != null && fallback.Log.Contains("with its Claw") && !clawBite.ProgressiveAttackPool.IsNaturalAttackUsed(1),
                "CMB-127: a grapple attack whose step was committed without naming a natural attack makes the one that step gave up (the claw), not the higher-bonus bite, so one step spends one natural attack");

            GrappleNaturalAttack(clawBite, 1, false, out bool biteOk);
            SpecialAttackResult none = clawBite.ResolveGrappleAction(GrappleActionType.AttackUnarmed);
            Assert(biteOk && none != null && !none.Success && none.Log.Contains("no natural attack left"),
                "CMB-127: with every natural attack used, a natural-weapon grappler's grapple attack is refused, never turned into an unarmed strike");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Grapple natural refusal check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Cleanup(beast, clawBite, target, target2);
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
        var smallLiving = CreateHasteTarget("AiHasteChoiceSmallLiving");
        DND35.AI.AIProfile profile = null;
        try
        {
            undead.Stats.CreatureType = "Undead";
            // MM p.310 (CMB-126): the Medium grabber's claw can seize only a Small or smaller target.
            smallLiving.Stats.SetBaseSizeCategory(SizeCategory.Small);
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
            Assert(DND35.AI.NaturalAttackChoice.ChooseHasteExtraAttackIndex(grabber, smallLiving) == 1
                && grabber.GetDefaultHasteNaturalAttackIndex() == 0,
                "AI Haste pick: the Improved Grab claw beats the higher-bonus bite; the rules default stays the bite");
            Assert(DND35.AI.NaturalAttackChoice.ChooseHasteExtraAttackIndex(grabber, living) == 0
                && DND35.AI.NaturalAttackChoice.CountRidersThatMatter(grabber, living, grabber.Stats.GetNaturalAttackAtSequenceIndex(1)) == 0,
                "AI Haste pick: against a Medium target, too large for a Medium grabber (MM p.310), the grab claw adds nothing and the bite wins (CMB-126)");
            Assert(DND35.AI.NaturalAttackChoice.ChooseHasteExtraAttackIndex(stinger, living) == 1
                && DND35.AI.NaturalAttackChoice.ChooseHasteExtraAttackIndex(stinger, undead) == 0,
                "AI Haste pick: a poison sting against a living target, the bite against an undead one (immune to poison)");

            profile = ScriptableObject.CreateInstance<DND35.AI.AIProfile>();
            Assert(profile.ChooseHasteNaturalAttackIndex(grabber, smallLiving) == 1
                && profile.ScoreHasteNaturalAttack(grabber, smallLiving, 1) > profile.ScoreHasteNaturalAttack(grabber, smallLiving, 0),
                "AIProfile.ChooseHasteNaturalAttackIndex and ScoreHasteNaturalAttack default to the shared scoring (the override points)");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"AI Haste natural choice check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Cleanup(plain, grabber, stinger, clawBite, living, undead, smallLiving);
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
            // Large, so the Medium target is small enough to grab (MM p.310, CMB-126).
            grabber.Stats.SetBaseSizeCategory(SizeCategory.Large);
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

    private static void TestPcHasteChooserForFullAttackAndPounce()
    {
        // The Full Attack button and the PC pounce ask which natural weapon makes Haste's extra attack
        // (owner decision CMB-106: the attacker chooses; CMB-124) through GameManager.PromptHasteNaturalAttackChoice:
        // one option per natural-attack type, the natural-attack chooser's labels; Cancel gets the default (highest
        // bonus: the bite); a creature not under player control gets the AI's own pick (ChooseHasteNaturalAttackIndexForAI,
        // the bite here: no riders). The full Ui flow is covered by the
        // rules/haste-natural-full-attack-ui, -ui-default and rules/haste-natural-pounce-ui scenarios.
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; PC Haste chooser check needs Play mode");
            return;
        }

        var pc = CreateHastedBiteClaws("PcHasteChooser", 4);
        var foe = CreateHasteTarget("PcHasteChooserTarget");
        PropertyInfo subPhaseProperty = typeof(GameManager).GetProperty("CurrentSubPhase");
        object previousSubPhase = subPhaseProperty != null ? subPhaseProperty.GetValue(gm) : null;
        try
        {
            System.Collections.Generic.List<int> choices = gm.GetHasteNaturalAttackChoiceIndices(pc);
            Assert(choices.Count == 2 && choices[0] == 0 && choices[1] == 1,
                "Haste chooser options: one per natural-attack type, bite (index 0) and claw (index 1)");
            Assert(ActionButtonPanel.BuildNaturalAttackChooserLabel("Claw", false, -1, true) == "Claw (Haste, Secondary -1)"
                && ActionButtonPanel.BuildNaturalAttackChooserLabel("Bite", true, 4, false) == "Bite (Primary +4)",
                "The Haste chooser uses the natural-attack chooser's label format");

            int resolved = -99;
            pc.IsControllable = false;
            System.Collections.IEnumerator npcPrompt = gm.PromptHasteNaturalAttackChoice(pc, foe, choice => resolved = choice);
            bool npcWaits = npcPrompt.MoveNext();
            Assert(!npcWaits && resolved == gm.ChooseHasteNaturalAttackIndexForAI(pc, foe) && resolved == 0,
                "A creature not under player control opens no chooser and gets the AI's pick (the bite)");

#if UNITY_EDITOR
            pc.IsControllable = true;
            resolved = -99;
            System.Collections.IEnumerator prompt = gm.PromptHasteNaturalAttackChoice(pc, foe, choice => resolved = choice);
            bool waits = prompt.MoveNext();
            if (!waits || gm.CombatUI == null || gm.CombatUI.Harness_OpenSpecialStyleMenuName() != GameManager.HasteNaturalAttackMenuName)
            {
                Debug.Log("  [SKIP] the Haste chooser did not open (no CombatUI action panel); option click not checked");
            }
            else
            {
                UnityEngine.UI.Button claw = gm.CombatUI.Harness_FindSpecialStyleOption("Claw (Haste, Secondary");
                Assert(claw != null, "The open Haste chooser offers 'Claw (Haste, Secondary ...)'");
                if (claw != null)
                    claw.onClick.Invoke();
                bool stillWaiting = prompt.MoveNext();
                Assert(!stillWaiting && resolved == 1, "Clicking the claw option resolves the Haste attack to the claw (index 1)");

                resolved = -99;
                System.Collections.IEnumerator cancelPrompt = gm.PromptHasteNaturalAttackChoice(pc, foe, choice => resolved = choice);
                cancelPrompt.MoveNext();
                UnityEngine.UI.Button cancel = gm.CombatUI.Harness_FindSpecialStyleCancel();
                Assert(cancel != null, "The open Haste chooser has a Cancel button");
                if (cancel != null)
                    cancel.onClick.Invoke();
                bool cancelWaiting = cancelPrompt.MoveNext();
                Assert(!cancelWaiting && resolved == 0, "Cancel resolves the Haste attack to the default (the bite)");

                // The pounce passes the charge's +2 (PHB p.154) as a label offset, so the chooser shows the bonus the
                // attack rolls; the turn indicator the prompt replaced comes back once it is answered.
                NaturalAttackDefinition clawAttack = pc.Stats.GetNaturalAttackAtSequenceIndex(1);
                string chargeClawLabel = ActionButtonPanel.BuildNaturalAttackChooserLabel(clawAttack.Name, clawAttack.IsPrimary,
                    pc.Stats.GetNaturalAttackBonus(clawAttack) + 2, true);
                UnityEngine.UI.Text indicator = gm.CombatUI.TurnIndicatorText;
                string previousIndicator = indicator != null ? indicator.text : null;
                if (indicator != null)
                    gm.CombatUI.SetTurnIndicator("Harness indicator before the Haste chooser");
                resolved = -99;
                System.Collections.IEnumerator pouncePrompt = gm.PromptHasteNaturalAttackChoice(pc, foe, choice => resolved = choice, labelBonusOffset: 2);
                pouncePrompt.MoveNext();
                UnityEngine.UI.Button chargeClaw = gm.CombatUI.Harness_FindSpecialStyleOption(chargeClawLabel);
                Assert(chargeClaw != null, "With the charge's +2 offset the chooser labels the claw '" + chargeClawLabel + "'");
                if (chargeClaw != null)
                    chargeClaw.onClick.Invoke();
                bool pounceWaiting = pouncePrompt.MoveNext();
                Assert(!pounceWaiting && resolved == 1
                    && (indicator == null || indicator.text == "Harness indicator before the Haste chooser"),
                    "Clicking the offset claw resolves to the claw and restores the turn indicator");
                if (indicator != null && previousIndicator != null)
                    gm.CombatUI.SetTurnIndicator(previousIndicator);
            }
#endif
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"PC Haste chooser check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
#if UNITY_EDITOR
            gm.CombatUI?.HideSpecialStyleSelectionMenu();
#endif
            if (subPhaseProperty != null && previousSubPhase != null)
                subPhaseProperty.SetValue(gm, previousSubPhase);
            Cleanup(pc);
            Cleanup(foe);
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

    // CMB-085 owner decision 2026-10-07: the creatures whose legs the MM text left unclear.
    // Reads the shared templates only.
    private static void TestOwnerStabilityClassification()
    {
        string[] stable =
        {
            "chuul", "formian_worker", "formian_taskmaster",
            "hydra_5head", "hydra_7head", "hydra_9head", "howler"
        };
        foreach (string id in stable)
        {
            NPCDefinition def = NPCDatabase.Get(id);
            Assert(def != null && def.IsExceptionallyStable, $"{id} is exceptionally stable (owner decision, CMB-085)");
        }

        // Two legs plus arms, no legs, or aquatic with no footing. The barghests are stable only
        // in wolf form; the data is their natural goblin-wolf hybrid form and Change Shape is text only.
        // The ethereal filcher balances on a single leg (MM p.104): the 2026-10-07 "three legs" classification
        // was wrong and is corrected (owner informed 2026-10-08, CRE-045).
        string[] notStable =
        {
            "ape", "dire_ape", "monkey", "girallon", "arrowhawk_juvenile", "octopus", "owlbear",
            "skeleton_owlbear", "zombie_owlbear", "barghest", "greater_barghest", "ethereal_filcher"
        };
        foreach (string id in notStable)
        {
            NPCDefinition def = NPCDatabase.Get(id);
            Assert(def != null && !def.IsExceptionallyStable, $"{id} is not exceptionally stable (owner decision, CMB-085)");
        }

        NPCDefinition chuul = NPCDatabase.Get("chuul");
        if (chuul != null)
            Assert(chuul.Clone().IsExceptionallyStable, "A chuul clone keeps the flag");
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

    /// <summary>
    /// CMB-125 data and the shared trigger test: every MM Trip (Ex) entry names the attack that trips (the bite; the
    /// cheetah's claw or bite, MM p.271), and only that attack counts as a trip rider for the AI's Haste pick.
    /// </summary>
    private static void TestTripTriggerAttackData()
    {
        var plain = new CharacterStats();
        plain.HasTripAttack = true;
        Assert(plain.IsTripTriggerAttack("Bite") && plain.IsTripTriggerAttack("bite") && !plain.IsTripTriggerAttack("Claw")
            && !plain.IsTripTriggerAttack("Longsword") && !plain.IsTripTriggerAttack("Unarmed strike") && !plain.IsTripTriggerAttack(null),
            "Trip (Ex) trigger: with no name set only a bite hit trips, not a claw, a weapon or an unarmed strike (CMB-125)");
        plain.TripTriggerAttackName = "Claw, Bite";
        Assert(plain.IsTripTriggerAttack("Claw") && plain.IsTripTriggerAttack("Bite") && !plain.IsTripTriggerAttack("Gore"),
            "Trip (Ex) trigger: a comma-separated list names several attacks (the cheetah's claw or bite)");
        plain.HasTripAttack = false;
        Assert(!plain.IsTripTriggerAttack("Bite"), "Trip (Ex) trigger: no Trip (Ex), no trigger");

        NPCDatabase.Init();
        var problems = new System.Collections.Generic.List<string>();
        int tripCreatures = 0;
        foreach (NPCDefinition def in NPCDatabase.AllNPCs)
        {
            if (def == null || !def.HasTripAttack)
                continue;
            tripCreatures++;
            if (string.IsNullOrWhiteSpace(def.TripTriggerAttackName))
            {
                problems.Add(def.Id + ": no trigger");
                continue;
            }

            foreach (string raw in def.TripTriggerAttackName.Split(','))
            {
                string trigger = raw.Trim();
                bool found = def.NaturalAttacks != null && def.NaturalAttacks.Exists(n => n != null && n.Name != null
                    && n.Name.IndexOf(trigger, System.StringComparison.OrdinalIgnoreCase) >= 0);
                if (!found)
                    problems.Add(def.Id + ": no natural attack named " + trigger);
            }
        }
        Assert(tripCreatures >= 7 && problems.Count == 0,
            $"Trip (Ex) data: every trip creature names a trigger attack it has ({tripCreatures} creatures; {string.Join("; ", problems)})");

        NPCDefinition cheetah = NPCDatabase.Get("cheetah");
        NPCDefinition wolf = NPCDatabase.Get("wolf");
        NPCDefinition werewolf = NPCDatabase.Get("werewolf");
        Assert(cheetah != null && cheetah.TripTriggerAttackName == "Claw, Bite"
            && wolf != null && wolf.TripTriggerAttackName == "Bite" && wolf.Clone().TripTriggerAttackName == "Bite"
            && werewolf != null && werewolf.HasTripAttack && werewolf.TripTriggerAttackName == "Bite",
            "Trip (Ex) data: cheetah claw or bite (MM p.271), wolf bite (MM p.283) kept by Clone and the summon alias, werewolf bite (MM p.174)");

        var tripper = CreateBiteClawsCreature("TripTriggerRiders", 4, false);
        var target = CreateHasteTarget("TripTriggerRidersTarget");
        try
        {
            tripper.Stats.HasTripAttack = true;
            Assert(DND35.AI.NaturalAttackChoice.CountRidersThatMatter(tripper, target, tripper.Stats.GetNaturalAttackAtSequenceIndex(0)) == 1
                && DND35.AI.NaturalAttackChoice.CountRidersThatMatter(tripper, target, tripper.Stats.GetNaturalAttackAtSequenceIndex(1)) == 0,
                "AI riders: Trip (Ex) counts on the bite only, not on the claws (CMB-125)");
        }
        finally
        {
            Cleanup(tripper, target);
        }
    }

    /// <summary>
    /// CMB-125 through the one shared free-trip path (GameManager.TryResolveFreeTripOnHit, used by the PC buttons, the NPC
    /// executor and charges): with the trip checks forced to land, a bite hit trips, a claw or weapon hit does not.
    /// </summary>
    private static void TestFreeTripOnlyAfterTriggerAttack()
    {
        GameManager gm = GameManager.Instance;
        MethodInfo freeTrip = typeof(GameManager).GetMethod("TryResolveFreeTripOnHit", BindingFlags.Instance | BindingFlags.NonPublic);
        if (gm == null || freeTrip == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance or TryResolveFreeTripOnHit is missing; the free-trip trigger check needs Play mode");
            return;
        }

        CharacterController wolf = null, clawTarget = null, weaponTarget = null, biteTarget = null;
        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        try
        {
            ScenarioHooks.RollFilter = (sides, ctx, natural) =>
                ctx == "Trip Strength check" ? 20 : ctx == "Trip defense check" ? 1 : natural;
            wolf = CreateBiteClawsCreature("FreeTripTrigger", 4, false);
            wolf.Stats.HasTripAttack = true;
            clawTarget = CreateWeakDefender("FreeTripTriggerClaw");
            weaponTarget = CreateWeakDefender("FreeTripTriggerWeapon");
            biteTarget = CreateWeakDefender("FreeTripTriggerBite");

            freeTrip.Invoke(gm, new object[] { wolf, clawTarget, new CombatResult { WeaponName = "Claw", Hit = true, BreakdownBAB = 4 }, null });
            freeTrip.Invoke(gm, new object[] { wolf, weaponTarget, new CombatResult { WeaponName = "Longsword", Hit = true, BreakdownBAB = 4 }, null });
            freeTrip.Invoke(gm, new object[] { wolf, biteTarget, new CombatResult { WeaponName = "Bite", Hit = true, BreakdownBAB = 4 }, null });
            Assert(!clawTarget.HasCondition(CombatConditionType.Prone) && !weaponTarget.HasCondition(CombatConditionType.Prone)
                && biteTarget.HasCondition(CombatConditionType.Prone),
                "Free trip: a bite hit trips; a claw or a longsword hit of the same creature does not (MM p.283, CMB-125)");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Free-trip trigger check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            Cleanup(wolf, clawTarget, weaponTarget, biteTarget);
        }
    }

    /// <summary>
    /// A summon's Smite is an ordinary melee attack with its bite (a fiendish wolf, Summon Monster II), so a smite hit
    /// starts the Trip (Ex) free trip through the shared path (GameManager.TryExecuteSummonSmiteAttack, MM p.283, CMB-125).
    /// Attack d20s forced to 19 (a hit, no natural-weapon threat), the trip check forced to land.
    /// </summary>
    private static void TestSummonSmiteBiteStartsFreeTrip()
    {
        GameManager gm = GameManager.Instance;
        MethodInfo smite = typeof(GameManager).GetMethod("TryExecuteSummonSmiteAttack", BindingFlags.Instance | BindingFlags.NonPublic);
        System.Type summonType = typeof(GameManager).GetNestedType("ActiveSummonInstance", BindingFlags.NonPublic);
        if (gm == null || gm.CombatUI == null || smite == null || summonType == null)
        {
            Debug.Log("  [SKIP] GameManager, CombatUI, TryExecuteSummonSmiteAttack or ActiveSummonInstance is missing; the smite free-trip check needs Play mode");
            return;
        }

        CharacterController wolf = null, target = null;
        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        try
        {
            ScenarioHooks.RollFilter = (sides, ctx, natural) =>
                ctx == "Trip Strength check" ? 20 : ctx == "Trip defense check" ? 1 : sides == 20 ? 19 : natural;
            wolf = CreateBiteClawsCreature("SmiteTripWolf", 4, false);
            wolf.Stats.HasTripAttack = true;
            wolf.Stats.TripTriggerAttackName = "Bite";
            wolf.Stats.HasTemplateSmiteGood = true;
            target = CreateWeakDefender("SmiteTripTarget");
            wolf.GridPosition = new Vector2Int(0, 0);
            target.GridPosition = new Vector2Int(1, 0); // adjacent: in the bite's reach
            target.Stats.CharacterAlignment = Alignment.LawfulGood;
            target.Stats.AdjustMaxHP(200);
            target.Stats.CurrentHP += 200;

            object summonData = System.Activator.CreateInstance(summonType, true);
            bool used = (bool)smite.Invoke(gm, new object[] { wolf, target, summonData });
            Assert(used && target.HasCondition(CombatConditionType.Prone),
                $"Summon smite: the fiendish wolf's Smite Good bite hit trips (MM p.283, CMB-125) (used {used}, prone {target.HasCondition(CombatConditionType.Prone)})");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Summon smite free-trip check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            Cleanup(wolf, target);
        }
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

    // ── Trip size limit, counter-trip and Improved Trip (PHB p.158, p.96; CMB-079) ──

    /// <summary>Forces the trip dice by context: touch attack, the trip's opposed check, the counter-trip's opposed check.</summary>
    private static System.Func<int, string, int, int> TripDice(int touch, int tripCheck, int tripDefense, int counterCheck = 10, int counterResist = 10)
        => (sides, ctx, natural) =>
            ctx == "Trip touch attack" ? touch
            : ctx == "Trip Strength check" ? tripCheck
            : ctx == "Trip defense check" ? tripDefense
            : ctx == "Counter-trip check" ? counterCheck
            : ctx == "Counter-trip resist check" ? counterResist
            : natural;

    private static void TestTripSizeLimit()
    {
        var tripper = CreateTestCharacter("TripSizeTripper", "Fighter");
        var large = CreateWeakDefender("TripSizeLarge");
        var huge = CreateWeakDefender("TripSizeHuge");
        var tiny = CreateWeakDefender("TripSizeTiny");
        large.Stats.CurrentSizeCategory = SizeCategory.Large;
        huge.Stats.CurrentSizeCategory = SizeCategory.Huge;
        tiny.Stats.CurrentSizeCategory = SizeCategory.Tiny;
        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        try
        {
            Assert(tripper.CanTrip(large, out _) && tripper.CanTrip(tiny, out _),
                "A Medium creature may trip a creature one size larger or any smaller one (PHB p.158)");
            Assert(!tripper.CanTrip(huge, out string hugeReason) && hugeReason != null && hugeReason.Contains("size category larger"),
                "A Medium creature cannot trip a Huge one, two categories larger (PHB p.158)");

            tripper.Stats.CurrentSizeCategory = SizeCategory.Small;
            Assert(!tripper.CanTrip(large, out _), "A Small creature cannot trip a Large one (PHB p.158)");
            tripper.Stats.CurrentSizeCategory = SizeCategory.Medium;

            // The shared resolver refuses too, before any die: a forced success would otherwise trip the Huge target.
            ScenarioHooks.RollFilter = TripDice(20, 20, 1);
            SpecialAttackResult refused = tripper.ExecuteSpecialAttack(SpecialAttackType.Trip, huge);
            SpecialAttackResult freeRefused = tripper.ResolveFreeTripAttempt(huge);
            Assert(refused != null && !refused.Success && !refused.Log.Contains("Touch attack") && !refused.CounterTripAllowed
                && freeRefused != null && !freeRefused.Success && !huge.HasCondition(CombatConditionType.Prone),
                "The trip resolver refuses a target two sizes larger, free trips included, with no roll and no counter-trip");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Trip size limit check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            Cleanup(tripper, large, huge, tiny);
        }
    }

    private static void TestCounterTripAfterFailedTrip()
    {
        // Tripper and defender both STR 26 (+8): the trip check forced to 1 (9) loses to the defense 20 (28);
        // the counter-trip forced to 20 (28) beats the tripper's resist 1 (9).
        var tripper = CreateTestCharacter("CounterTripTripper", "Fighter");
        var defender = CreateTestCharacter("CounterTripDefender", "Fighter");
        var tripper2 = CreateTestCharacter("CounterTripTripperTouchMiss", "Fighter");
        var hugeTripper = CreateTestCharacter("CounterTripHugeTripper", "Fighter");
        var smallDefender = CreateWeakDefender("CounterTripSmallDefender");
        defender.GridPosition = new Vector2Int(1, 0);
        hugeTripper.Stats.CurrentSizeCategory = SizeCategory.Huge;
        smallDefender.Stats.CurrentSizeCategory = SizeCategory.Small;
        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        System.Action<CharacterController, CharacterController, SpecialAttackType, SpecialAttackResult> savedManeuver = ScenarioHooks.ManeuverResolved;
        var reported = new System.Collections.Generic.List<SpecialAttackResult>();
        CharacterController reportedBy = null;
        try
        {
            ScenarioHooks.RollFilter = TripDice(20, 1, 20, 20, 1);
            SpecialAttackResult trip = tripper.ExecuteSpecialAttack(SpecialAttackType.Trip, defender);
            Assert(trip != null && !trip.Success && trip.CounterTripAllowed && !defender.HasCondition(CombatConditionType.Prone),
                "A trip lost at the opposed check lets the defender try to trip back (PHB p.158)");

            ScenarioHooks.ManeuverResolved = (by, target, type, r) => { reported.Add(r); reportedBy = by; };
            SpecialAttackResult counter = defender.ResolveCounterTrip(tripper);
            Assert(counter != null && counter.IsCounterTrip && counter.Success && !counter.AttackerActionConsumed
                && tripper.HasCondition(CombatConditionType.Prone) && !counter.Log.Contains("Touch attack"),
                "Counter-trip: an opposed check with no touch attack and no action spent; the tripper is prone (PHB p.158)");
            Assert(counter != null
                && counter.CheckTotal - counter.CheckRoll == defender.GetTripAttackerCheckModifier()
                && counter.OpposedTotal - counter.OpposedRoll == tripper.GetTripOrOverrunDefenderCheckModifier(),
                "Counter-trip: the defender's Strength check against the tripper's better of Strength and Dexterity, roles swapped");
            Assert(reported.Count == 1 && reportedBy == defender && reported[0].IsCounterTrip,
                "Counter-trip is reported to the maneuver hook as a trip by the defender, marked as a counter-trip");
            Assert(!defender.CanCounterTrip(tripper, out _), "A tripper already prone cannot be tripped back again");

            ScenarioHooks.RollFilter = TripDice(1, 1, 20);
            tripper2.GridPosition = new Vector2Int(0, 1);
            SpecialAttackResult touchMiss = tripper2.ExecuteSpecialAttack(SpecialAttackType.Trip, defender);
            Assert(touchMiss != null && !touchMiss.Success && !touchMiss.CounterTripAllowed,
                "A trip that misses its touch attack gives no counter-trip (only a lost opposed check does)");

            ScenarioHooks.RollFilter = TripDice(20, 1, 20);
            SpecialAttackResult freeLost = tripper2.ResolveFreeTripAttempt(defender);
            Assert(freeLost != null && !freeLost.Success && !freeLost.CounterTripAllowed,
                "A free trip after a hit gives no counter-trip (MM trip: the opponent cannot react to trip)");

            Assert(!smallDefender.CanCounterTrip(hugeTripper, out string sizeReason) && sizeReason != null && sizeReason.Contains("size category larger"),
                "A Small defender cannot trip back a Huge tripper (the trip size limit, PHB p.158)");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Counter-trip check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            ScenarioHooks.ManeuverResolved = savedManeuver;
            Cleanup(tripper, defender, tripper2, hugeTripper, smallDefender);
        }
    }

    private static void TestCounterTripChoiceOdds()
    {
        Assert(Mathf.Approximately(CharacterController.EstimateOpposedCheckWinChance(0, 0), 0.5f)
            && CharacterController.EstimateOpposedCheckWinChance(0, 20) == 0f
            && CharacterController.EstimateOpposedCheckWinChance(20, 0) == 1f
            && CharacterController.EstimateOpposedCheckWinChance(5, 0) > 0.7f,
            "Opposed-check odds: even at equal modifiers, 0 and 1 at a 20-point gap (PHB p.64 ties)");

        var tripper = CreateTestCharacter("CounterTripChoiceTripper", "Fighter");     // STR 26: resists at +8
        var even = CreateTestCharacter("CounterTripChoiceEven", "Fighter");          // STR 26: +8
        var weak = CreateWeakDefender("CounterTripChoiceWeak");
        weak.Stats.STR = 1;                                                            // -5: about 5% to win
        var allyOfTripper = CreateTestCharacter("CounterTripChoiceAlly", "Fighter");  // STR 26, the tripper's own side
        // The test characters start on the Enemy team; the defenders face an enemy tripper.
        even.SetTeam(CharacterTeam.Player);
        weak.SetTeam(CharacterTeam.Player);
        try
        {
            Assert(DND35.AI.AIProfile.DefaultShouldCounterTrip(even, tripper),
                "AI counter-trip choice: an even check trips back (owner direction: unless clearly bad)");
            Assert(!DND35.AI.AIProfile.DefaultShouldCounterTrip(weak, tripper),
                "AI counter-trip choice: a much weaker check (13 points, about 5%) declines");
            Assert(!DND35.AI.AIProfile.DefaultShouldCounterTrip(allyOfTripper, tripper),
                "AI counter-trip choice: an even check against an ally declines (owner ruling 2026-10-08, CMB-136)");
        }
        finally
        {
            Cleanup(tripper, even, weak, allyOfTripper);
        }
    }

    private static void TestNpcExecutorCounterTrip()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; NPC executor counter-trip check needs Play mode");
            return;
        }

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        System.Action<CharacterController, CharacterController, SpecialAttackType, SpecialAttackResult> savedManeuver = ScenarioHooks.ManeuverResolved;
        System.Action<string> savedLog = ScenarioHooks.CombatLog;
        var counters = new System.Collections.Generic.List<CharacterController>();
        var logLines = new System.Collections.Generic.List<string>();
        CharacterController npc = null, enemy = null, npc2 = null, ally = null;
        try
        {
            ScenarioHooks.RollFilter = TripDice(20, 1, 20, 20, 1);
            ScenarioHooks.ManeuverResolved = (by, target, type, r) => { if (r != null && r.IsCounterTrip) counters.Add(by); };
            ScenarioHooks.CombatLog = line => { if (line != null) logLines.Add(line); };

            npc = CreateIterativeAttacker("ExecutorCounterTripNpc");
            npc.IsControllable = false;
            npc.GridPosition = new Vector2Int(30, 30);
            enemy = CreateTestCharacter("ExecutorCounterTripEnemy", "Fighter");
            enemy.SetTeam(CharacterTeam.Player);
            enemy.IsControllable = false; // AI-run: the AI layer decides
            enemy.Stats.CanMakeAttacksOfOpportunity = false;
            enemy.GridPosition = new Vector2Int(31, 30);

            bool acted = gm.TryNPCSpecialAttackByTypeForAI(npc, enemy, SpecialAttackType.Trip);
            Assert(acted && !gm.IsAwaitingCounterTripChoice && counters.Count == 1 && counters[0] == enemy
                && npc.HasCondition(CombatConditionType.Prone) && npc.ProgressiveAttackPool.MainHandStepsUsed == 1,
                "NPC executor: after a failed trip the AI-run enemy trips back at once; the trip cost one step (PHB p.158, CMB-079)");

            counters.Clear();
            npc2 = CreateIterativeAttacker("ExecutorCounterTripNpcAllyCase");
            npc2.IsControllable = false;
            npc2.GridPosition = new Vector2Int(30, 33);
            ally = CreateTestCharacter("ExecutorCounterTripAlly", "Fighter");
            ally.IsControllable = false;
            ally.Stats.CanMakeAttacksOfOpportunity = false;
            ally.GridPosition = new Vector2Int(31, 33);
            logLines.Clear();
            bool actedOnAlly = gm.TryNPCSpecialAttackByTypeForAI(npc2, ally, SpecialAttackType.Trip);
            // The decline line is written only when HandleTripAftermath asks the AI-run ally (AIService.ShouldCounterTrip),
            // so it proves the offer reached the decision layer; a side gate in the rule would skip it.
            string declineLine = $"{ally.Stats.CharacterName} does not try to trip {npc2.Stats.CharacterName} back.";
            int declines = logLines.FindAll(l => l.Contains(declineLine)).Count;
            Assert(actedOnAlly && counters.Count == 0 && !npc2.HasCondition(CombatConditionType.Prone)
                && ally.CanCounterTrip(npc2, out _) && declines == 1,
                $"A failed trip by the defender's ally: the counter-trip is offered to the AI-run ally, which declines once (owner ruling 2026-10-08, CMB-136; {declines} decline lines)");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"NPC executor counter-trip check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            ScenarioHooks.ManeuverResolved = savedManeuver;
            ScenarioHooks.CombatLog = savedLog;
            Cleanup(npc, enemy, npc2, ally);
        }
    }

    private static void TestImprovedTripFollowUpAttack()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; Improved Trip follow-up check needs Play mode");
            return;
        }

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        System.Action<CharacterController, CombatResult> savedAttack = ScenarioHooks.AttackResolved;
        var attacks = new System.Collections.Generic.List<CombatResult>();
        CharacterController npc = null, target = null, plain = null, plainTarget = null;
        try
        {
            ScenarioHooks.RollFilter = TripDice(20, 20, 1);

            // BAB +11 (+11/+6/+1) with a longsword and Improved Trip.
            npc = CreateIterativeAttacker("ImprovedTripFollowUp");
            npc.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
            npc.Stats.Feats.Add("Improved Trip");
            npc.IsControllable = false;
            npc.GridPosition = new Vector2Int(34, 30);
            target = CreateWeakDefender("ImprovedTripFollowUpTarget");
            target.GridPosition = new Vector2Int(35, 30);
            target.Stats.AdjustMaxHP(500);
            target.Stats.CurrentHP += 500;
            ScenarioHooks.AttackResolved = (by, r) => { if (by == npc) attacks.Add(r); };

            bool first = gm.TryNPCSpecialAttackByTypeForAI(npc, target, SpecialAttackType.Trip);
            Assert(first && target.HasCondition(CombatConditionType.Prone) && attacks.Count == 1 && attacks[0].BreakdownBAB == 11
                && attacks[0].WeaponName == "Longsword"
                && npc.ProgressiveAttackPool.MainHandStepsUsed == 1 && !npc.ProgressiveAttackPool.IsFullAttack,
                $"Improved Trip: one longsword attack at the trip's BAB (+11) right after the trip; the sequence spent only the trip's step (attacks {attacks.Count}, BAB {(attacks.Count > 0 ? attacks[0].BreakdownBAB : 0)}; PHB p.96)");

            attacks.Clear();
            bool second = gm.TryNPCSpecialAttackByTypeForAI(npc, target, SpecialAttackType.Trip);
            Assert(second && attacks.Count == 1 && attacks[0].BreakdownBAB == 6 && npc.ProgressiveAttackPool.MainHandStepsUsed == 2,
                "Improved Trip: a trip in place of the second iterative is followed by an attack at +6, that step's bonus (PHB p.96 example)");

            // Without the feat no attack follows.
            plain = CreateIterativeAttacker("ImprovedTripControl");
            plain.IsControllable = false;
            plain.GridPosition = new Vector2Int(34, 33);
            plainTarget = CreateWeakDefender("ImprovedTripControlTarget");
            plainTarget.GridPosition = new Vector2Int(35, 33);
            plainTarget.Stats.CanMakeAttacksOfOpportunity = false;
            attacks.Clear();
            ScenarioHooks.AttackResolved = (by, r) => { if (by == plain) attacks.Add(r); };
            bool control = gm.TryNPCSpecialAttackByTypeForAI(plain, plainTarget, SpecialAttackType.Trip);
            Assert(control && plainTarget.HasCondition(CombatConditionType.Prone) && attacks.Count == 0,
                "Without Improved Trip a trip that lands is followed by no attack");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Improved Trip follow-up check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            ScenarioHooks.AttackResolved = savedAttack;
            Cleanup(npc, target, plain, plainTarget);
        }
    }

    private static void TestImprovedTripFollowUpNaturalAndFreeTrip()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; Improved Trip natural follow-up check needs Play mode");
            return;
        }

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        System.Action<CharacterController, CombatResult> savedAttack = ScenarioHooks.AttackResolved;
        var attacks = new System.Collections.Generic.List<CombatResult>();
        CharacterController beast = null, target = null, wolf = null, wolfTarget = null;
        try
        {
            ScenarioHooks.RollFilter = TripDice(20, 20, 1);

            // Bite/claw/claw at BAB +4 with Improved Trip: the trip replaces the bite (step 1); the follow-up
            // is the bite itself; then the two claws (MM p.312; PHB p.96).
            beast = CreateBiteClawsCreature("ImprovedTripNatural", 4, false);
            beast.Stats.Feats.Add("Improved Trip");
            beast.IsControllable = false;
            beast.GridPosition = new Vector2Int(38, 30);
            target = CreateWeakDefender("ImprovedTripNaturalTarget");
            target.GridPosition = new Vector2Int(39, 30);
            target.Stats.AdjustMaxHP(500);
            target.Stats.CurrentHP += 500;
            ScenarioHooks.AttackResolved = (by, r) => { if (by == beast) attacks.Add(r); };

            int offers = 0;
            FullAttackResult sequence = RunNpcMeleeSequence(gm, beast, target,
                (actor, stepTarget) => offers++ == 0 && gm.TryNPCSpecialAttackByTypeForAI(actor, stepTarget, SpecialAttackType.Trip),
                out int maneuvers);
            Assert(maneuvers == 1 && target.HasCondition(CombatConditionType.Prone)
                && attacks.Count == 3 && attacks[0].WeaponName == "Bite" && attacks[1].WeaponName == "Claw" && attacks[2].WeaponName == "Claw"
                && sequence.Attacks.Count == 2 && beast.ProgressiveAttackPool.MainHandStepsUsed == 3,
                $"Improved Trip, natural sequence: the trip replaces the bite, the bite follows at once, then both claws; 3 steps (attacks {string.Join(",", attacks.ConvertAll(a => a.WeaponName))})");

            // Free trip after a bite hit, with Improved Trip: the bite that hit is made again.
            wolf = CreateBiteClawsCreature("ImprovedTripFreeTrip", 4, false);
            wolf.Stats.Feats.Add("Improved Trip");
            wolf.Stats.HasTripAttack = true;
            wolf.GridPosition = new Vector2Int(38, 33);
            wolfTarget = CreateWeakDefender("ImprovedTripFreeTripTarget");
            wolfTarget.GridPosition = new Vector2Int(39, 33);
            wolfTarget.Stats.AdjustMaxHP(500);
            wolfTarget.Stats.CurrentHP += 500;
            attacks.Clear();
            ScenarioHooks.AttackResolved = (by, r) => { if (by == wolf) attacks.Add(r); };
            SpecialAttackResult free = wolf.ResolveFreeTripAttempt(wolfTarget, new CombatResult { WeaponName = "Bite", BreakdownBAB = 4, Hit = true });
            bool pendingOnly = free != null && free.ImprovedTripFollowUpPending && free.FollowUpAttack == null && attacks.Count == 0;
            gm.HandleTripAftermath(wolf, wolfTarget, free, null); // the callers' step after the trip's log and reactions
            Assert(pendingOnly && free.Success && !free.CounterTripAllowed && free.FollowUpAttack != null
                && attacks.Count == 1 && attacks[0].WeaponName == "Bite",
                "Improved Trip after a free trip on a bite hit: the bite is made again in the aftermath, and no counter-trip is offered");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Improved Trip natural follow-up check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            ScenarioHooks.AttackResolved = savedAttack;
            Cleanup(beast, target, wolf, wolfTarget);
        }
    }

    /// <summary>Records every melee reaction call made against the creatures it is set on (a stand-in for Fire Shield).</summary>
    private sealed class ReactionSpy : IMeleeReactionEffect
    {
        public readonly System.Collections.Generic.HashSet<CharacterController> On = new System.Collections.Generic.HashSet<CharacterController>();
        public readonly System.Collections.Generic.List<(CharacterController by, CharacterController on, bool withAttack, int attacksSoFar)> Calls
            = new System.Collections.Generic.List<(CharacterController, CharacterController, bool, int)>();
        public System.Func<int> AttacksSoFar = () => 0;
        public string EffectName => "Test reaction spy";
        public bool IsActiveOn(CharacterController character) => character != null && On.Contains(character);
        public void OnMeleeAttackHit(CharacterController attacker, CharacterController defender, CombatResult attackResult)
            => Calls.Add((attacker, defender, attackResult != null, AttacksSoFar()));
    }

    /// <summary>
    /// The Improved Trip attack comes after the trip's own melee reactions (CMB-079 review): the resolver only
    /// records it and GameManager.HandleTripAftermath makes it, at the bonus the trip was given (for a PC in a
    /// two-weapon round that bonus already includes the main-hand penalty, TryCommitMainHandManeuverStep).
    /// </summary>
    private static void TestImprovedTripFollowUpAfterTripReactions()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; Improved Trip order check needs Play mode");
            return;
        }

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        System.Action<CharacterController, CombatResult> savedAttack = ScenarioHooks.AttackResolved;
        var attacks = new System.Collections.Generic.List<CombatResult>();
        var spy = new ReactionSpy { AttacksSoFar = () => attacks.Count };
        CharacterController npc = null, target = null, pc = null, pcTarget = null;
        try
        {
            ScenarioHooks.RollFilter = TripDice(20, 20, 1);
            MeleeReactionService.Register(spy);

            // NPC executor: the trip's reaction runs before the Improved Trip attack is rolled.
            npc = CreateIterativeAttacker("ImprovedTripOrderNpc");
            npc.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
            npc.Stats.Feats.Add("Improved Trip");
            npc.IsControllable = false;
            npc.GridPosition = new Vector2Int(42, 30);
            target = CreateWeakDefender("ImprovedTripOrderTarget");
            target.GridPosition = new Vector2Int(43, 30);
            target.Stats.AdjustMaxHP(500);
            target.Stats.CurrentHP += 500;
            spy.On.Add(target);
            ScenarioHooks.AttackResolved = (by, r) => { if (by == npc) attacks.Add(r); };

            bool acted = gm.TryNPCSpecialAttackByTypeForAI(npc, target, SpecialAttackType.Trip);
            int tripReaction = spy.Calls.FindIndex(c => c.by == npc && c.on == target && !c.withAttack);
            Assert(acted && attacks.Count == 1 && tripReaction >= 0 && spy.Calls[tripReaction].attacksSoFar == 0,
                $"Improved Trip: the trip's melee reaction runs before the follow-up attack (reaction at attack count {(tripReaction >= 0 ? spy.Calls[tripReaction].attacksSoFar : -1)}, attacks {attacks.Count})");

            // Shared resolver: a landed trip only records the attack; HandleTripAftermath makes it at the trip's
            // bonus (+9: a +11 step with a -2 two-weapon main-hand penalty folded in, as the PC wrapper passes it).
            pc = CreateIterativeAttacker("ImprovedTripRecordPc");
            pc.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
            pc.Stats.Feats.Add("Improved Trip");
            pc.GridPosition = new Vector2Int(42, 33);
            pcTarget = CreateWeakDefender("ImprovedTripRecordTarget");
            pcTarget.GridPosition = new Vector2Int(43, 33);
            pcTarget.Stats.AdjustMaxHP(500);
            pcTarget.Stats.CurrentHP += 500;
            attacks.Clear();
            ScenarioHooks.AttackResolved = (by, r) => attacks.Add(r);
            SpecialAttackResult trip = pc.ExecuteSpecialAttack(SpecialAttackType.Trip, pcTarget, tripAttackBonusOverride: 9);
            bool recordedOnly = trip != null && trip.Success && trip.ImprovedTripFollowUpPending && trip.FollowUpAttack == null && attacks.Count == 0;
            gm.HandleTripAftermath(pc, pcTarget, trip, null);
            Assert(recordedOnly, "Improved Trip: the trip resolver records the attack and makes none itself");
            CombatResult followUp = trip != null ? trip.FollowUpAttack : null;
            Assert(followUp != null && !trip.ImprovedTripFollowUpPending && attacks.Count == 1 && followUp.BreakdownBAB == 9,
                $"Improved Trip: the aftermath makes the one attack at the trip's bonus, +9 (BAB {(followUp != null ? followUp.BreakdownBAB : 0)}, attacks {attacks.Count})");
            gm.HandleTripAftermath(pc, pcTarget, trip, null);
            Assert(attacks.Count == 1, "Improved Trip: a second aftermath call makes no second attack");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Improved Trip order check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            MeleeReactionService.Unregister(spy);
            ScenarioHooks.RollFilter = savedFilter;
            ScenarioHooks.AttackResolved = savedAttack;
            Cleanup(npc, target, pc, pcTarget);
        }
    }

    /// <summary>
    /// A counter-trip is melee contact with the tripper (CMB-079 review): it triggers the tripper's melee reactions
    /// (Fire Shield, Thorns), as every other trip does.
    /// </summary>
    private static void TestCounterTripTriggersMeleeReactions()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; counter-trip reaction check needs Play mode");
            return;
        }

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        var spy = new ReactionSpy();
        CharacterController npc = null, enemy = null;
        try
        {
            ScenarioHooks.RollFilter = TripDice(20, 1, 20, 20, 1);
            MeleeReactionService.Register(spy);

            npc = CreateIterativeAttacker("CounterTripReactionNpc");
            npc.IsControllable = false;
            npc.GridPosition = new Vector2Int(46, 30);
            enemy = CreateTestCharacter("CounterTripReactionEnemy", "Fighter");
            enemy.SetTeam(CharacterTeam.Player);
            enemy.IsControllable = false;
            enemy.Stats.CanMakeAttacksOfOpportunity = false;
            enemy.GridPosition = new Vector2Int(47, 30);
            spy.On.Add(npc); // the tripper carries the reaction

            bool acted = gm.TryNPCSpecialAttackByTypeForAI(npc, enemy, SpecialAttackType.Trip);
            Assert(acted && npc.HasCondition(CombatConditionType.Prone)
                && spy.Calls.Exists(c => c.by == enemy && c.on == npc && !c.withAttack),
                "Counter-trip: the tripper's melee reaction is triggered by the defender's counter-trip (contact, as every trip)");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Counter-trip reaction check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            MeleeReactionService.Unregister(spy);
            ScenarioHooks.RollFilter = savedFilter;
            Cleanup(npc, enemy);
        }
    }

    /// <summary>
    /// A conscious defender at 0 HP or below may trip back (owner ruling 2026-10-08, CMB-136): a disabled creature
    /// (PHB p.145) and a Diehard creature below 0 HP react, since the counter-trip is a reaction, not an action, and it
    /// costs them no hit point; a dying (unconscious) creature does not.
    /// </summary>
    private static void TestCounterTripByDisabledDefender()
    {
        var tripper = CreateTestCharacter("CounterTripDisabledTripper", "Fighter");
        var disabled = CreateTestCharacter("CounterTripDisabledDefender", "Fighter");
        var diehard = CreateTestCharacter("CounterTripDiehardDefender", "Fighter");
        var dying = CreateTestCharacter("CounterTripDyingDefender", "Fighter");
        tripper.GridPosition = new Vector2Int(60, 30);
        disabled.GridPosition = new Vector2Int(61, 30);
        diehard.GridPosition = new Vector2Int(60, 31);
        dying.GridPosition = new Vector2Int(61, 31);
        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        try
        {
            disabled.Stats.CurrentHP = 0;
            diehard.Stats.Feats.Add("Diehard");
            diehard.Stats.CurrentHP = -3;
            dying.Stats.CurrentHP = -3;

            bool disabledMay = disabled.CanCounterTrip(tripper, out string disabledReason);
            Assert(disabled.CurrentHPState == HPState.Disabled && disabledMay,
                $"A disabled defender (0 HP, PHB p.145) may trip back: a reaction, not an action (state {disabled.CurrentHPState}, reason {disabledReason})");
            Assert(diehard.CurrentHPState == HPState.Disabled && diehard.CanCounterTrip(tripper, out _),
                $"A Diehard defender below 0 HP (conscious, disabled) may trip back (state {diehard.CurrentHPState})");
            Assert(dying.CurrentHPState == HPState.Dying && !dying.CanCounterTrip(tripper, out string dyingReason) && dyingReason != null,
                $"A dying, unconscious defender cannot trip back (state {dying.CurrentHPState})");

            ScenarioHooks.RollFilter = TripDice(20, 1, 20, 20, 1);
            SpecialAttackResult trip = tripper.ExecuteSpecialAttack(SpecialAttackType.Trip, disabled);
            Assert(trip != null && !trip.Success && trip.CounterTripAllowed,
                "A trip lost at the opposed check against a disabled defender offers the counter-trip (PHB p.158)");
            SpecialAttackResult counter = disabled.ResolveCounterTrip(tripper);
            Assert(counter != null && counter.Success && tripper.HasCondition(CombatConditionType.Prone)
                && disabled.Stats.CurrentHP == 0 && disabled.CurrentHPState == HPState.Disabled,
                $"The disabled defender's counter-trip lands and costs it no hit point (HP {disabled.Stats.CurrentHP}, state {disabled.CurrentHPState})");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"Disabled counter-trip check threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            Cleanup(tripper, disabled, diehard, dying);
        }
    }

    /// <summary>A copy of a weapon that has only a string id (the longspear and the whip have no ItemID value).</summary>
    private static ItemData CloneWeaponByStringId(string id)
    {
#pragma warning disable CS0618 // CloneItem(string): these weapons have no ItemID enum value
        return ItemDatabase.CloneItem(id);
#pragma warning restore CS0618
    }

    private static CharacterController CreateImprovedTripper(string name, Vector2Int position, ItemData mainHand, ItemData offHand = null)
    {
        CharacterController tripper = CreateIterativeAttacker(name);
        global::Inventory inventory = tripper.GetComponent<InventoryComponent>().CharacterInventory;
        inventory.DirectEquip(mainHand, EquipSlot.RightHand);
        if (offHand != null)
            inventory.DirectEquip(offHand, EquipSlot.LeftHand);
        inventory.RecalculateStats();
        tripper.Stats.Feats.Add("Improved Trip");
        tripper.IsControllable = false;
        tripper.GridPosition = position;
        return tripper;
    }

    private static CharacterController CreateSturdyTripTarget(string name, Vector2Int position, bool armed)
    {
        CharacterController target = CreateWeakDefender(name);
        if (armed)
        {
            global::Inventory inventory = target.GetComponent<InventoryComponent>().CharacterInventory;
            inventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
            inventory.RecalculateStats();
        }
        target.GridPosition = position;
        target.Stats.AdjustMaxHP(500);
        target.Stats.CurrentHP += 500;
        // The trippers stay on the Enemy team the test characters start on; the target is their foe.
        target.SetTeam(CharacterTeam.Player);
        return target;
    }

    /// <summary>
    /// The Improved Trip attack's weapon (owner ruling 2026-10-08, CMB-136; PHB p.96 "a melee attack against that
    /// opponent", p.139): the main weapon when it can attack the tripped opponent; else the off-hand weapon, as an
    /// off-hand attack (a shield bash drops the shield's AC, PHB p.125); else an unarmed strike, which provokes the
    /// armed target's attack of opportunity first unless the tripper's unarmed attack is armed (Improved Unarmed
    /// Strike, natural weapons) or the target is its teammate. Run through the NPC executor and through the PC
    /// wrapper's shared resolver and aftermath.
    /// </summary>
    private static void TestImprovedTripFollowUpWeaponFallback()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; Improved Trip weapon fallback check needs Play mode");
            return;
        }

        System.Func<int, string, int, int> savedFilter = ScenarioHooks.RollFilter;
        System.Action<CharacterController, CombatResult> savedAttack = ScenarioHooks.AttackResolved;
        System.Action<CharacterController, CharacterController, string, CombatResult> savedAoO = ScenarioHooks.AoOResolved;
        var events = new System.Collections.Generic.List<string>();
        var attacks = new System.Collections.Generic.List<(CharacterController by, CombatResult result)>();
        CharacterController spear = null, spearTarget = null, ius = null, iusTarget = null, sling = null, slingTarget = null,
            basher = null, basherTarget = null, bare = null, bareTarget = null, sword = null, swordTarget = null,
            mate = null, mateTarget = null, clawed = null, clawedTarget = null, archer = null, monkArcher = null, diehard = null, dying = null;
        try
        {
            ScenarioHooks.RollFilter = TripDice(20, 20, 1);
            ScenarioHooks.AttackResolved = (by, r) => { attacks.Add((by, r)); events.Add("attack " + by.Stats.CharacterName + " " + r.WeaponName); };
            ScenarioHooks.AoOResolved = (by, t, trigger, r) => events.Add("aoo " + by.Stats.CharacterName + " " + trigger);

            // Longspear (reach, cannot attack adjacent) against an adjacent armed target: an unarmed strike at the
            // trip's bonus, after the target's AoO (no Improved Unarmed Strike).
            spear = CreateImprovedTripper("TripFallbackSpear", new Vector2Int(60, 34), CloneWeaponByStringId(ItemIDs.LONGSPEAR));
            spearTarget = CreateSturdyTripTarget("TripFallbackSpearTarget", new Vector2Int(61, 34), armed: true);
            Assert(spear.SelectImprovedTripAttack(spearTarget, out _, out _) == CharacterController.ImprovedTripAttackSource.UnarmedStrike
                && spear.DoesUnarmedAttackProvoke(spearTarget),
                "Improved Trip weapon: a longspear cannot attack an adjacent foe, so an unarmed strike, which provokes from an armed target (PHB p.139)");
            bool spearActed = gm.TryNPCSpecialAttackByTypeForAI(spear, spearTarget, SpecialAttackType.Trip);
            int aooAt = events.FindIndex(e => e == "aoo " + spearTarget.Stats.CharacterName + " unarmed");
            int strikeAt = events.FindIndex(e => e == "attack " + spear.Stats.CharacterName + " Unarmed strike");
            CombatResult strike = attacks.Find(a => a.by == spear).result;
            Assert(spearActed && spearTarget.HasCondition(CombatConditionType.Prone) && aooAt >= 0 && strikeAt > aooAt
                && strike != null && strike.BreakdownBAB == 11 && attacks.FindAll(a => a.by == spear).Count == 1,
                $"Improved Trip with a longspear, adjacent: the target's AoO, then one unarmed strike at the trip's +11 (events: {string.Join(" | ", events)})");

            // The same with Improved Unarmed Strike: no AoO.
            events.Clear();
            attacks.Clear();
            ius = CreateImprovedTripper("TripFallbackIus", new Vector2Int(60, 37), CloneWeaponByStringId(ItemIDs.LONGSPEAR));
            ius.Stats.Feats.Add("Improved Unarmed Strike");
            iusTarget = CreateSturdyTripTarget("TripFallbackIusTarget", new Vector2Int(61, 37), armed: true);
            bool iusActed = gm.TryNPCSpecialAttackByTypeForAI(ius, iusTarget, SpecialAttackType.Trip);
            Assert(iusActed && !events.Exists(e => e.StartsWith("aoo ", System.StringComparison.Ordinal))
                && events.Contains("attack " + ius.Stats.CharacterName + " Unarmed strike"),
                $"Improved Trip unarmed strike with Improved Unarmed Strike provokes nothing (events: {string.Join(" | ", events)})");

            // A sling (ranged, cannot make a melee attack) with a dagger in the off hand: the dagger, as an off-hand
            // attack, no AoO. Through the PC wrapper's shared resolver and aftermath.
            events.Clear();
            attacks.Clear();
            sling = CreateImprovedTripper("TripFallbackSling", new Vector2Int(60, 40), ItemDatabase.CloneItem(ItemID.WeaponSling), ItemDatabase.CloneItem(ItemID.WeaponDagger));
            slingTarget = CreateSturdyTripTarget("TripFallbackSlingTarget", new Vector2Int(61, 40), armed: true);
            SpecialAttackResult slingTrip = sling.ExecuteSpecialAttack(SpecialAttackType.Trip, slingTarget, tripAttackBonusOverride: 11);
            gm.HandleTripAftermath(sling, slingTarget, slingTrip, null);
            Assert(slingTrip != null && slingTrip.Success && slingTrip.FollowUpAttack != null && slingTrip.FollowUpAttack.WeaponName == "Dagger"
                && slingTrip.FollowUpAttack.IsOffHandAttack && slingTrip.FollowUpAttack.BreakdownBAB == 11
                && !events.Exists(e => e.StartsWith("aoo ", System.StringComparison.Ordinal)),
                $"Improved Trip with a sling, adjacent: the off-hand dagger attacks as an off-hand attack at the trip's bonus, no AoO (events: {string.Join(" | ", events)})");

            // A sling with a heavy steel shield: the shield bash, which drops the shield's AC bonus without Improved
            // Shield Bash (PHB p.125). The sling alone is a ranged-only loadout that cannot start a trip in this game,
            // so the follow-up is resolved from a landed trip's record.
            basher = CreateImprovedTripper("TripFallbackBasher", new Vector2Int(60, 49), ItemDatabase.CloneItem(ItemID.WeaponSling), ItemDatabase.CloneItem(ItemID.ShieldHeavySteel));
            basherTarget = CreateSturdyTripTarget("TripFallbackBasherTarget", new Vector2Int(61, 49), armed: true);
            int shieldBefore = basher.Stats.ShieldBonus;
            CharacterController.ImprovedTripAttackSource bashSource = basher.SelectImprovedTripAttack(basherTarget, out ItemData bashWeapon, out _);
            var bashRecord = new SpecialAttackResult { ManeuverName = "Trip", Success = true, ImprovedTripFollowUpPending = true, FollowUpAttackBonus = 11 };
            basher.ResolveImprovedTripFollowUp(basherTarget, bashRecord);
            Assert(bashSource == CharacterController.ImprovedTripAttackSource.OffHandWeapon && bashWeapon != null && bashWeapon.IsShield
                && bashRecord.FollowUpAttack != null && bashRecord.FollowUpAttack.IsOffHandAttack
                && shieldBefore > 0 && basher.Stats.ShieldBonus == 0 && basher.HasCondition(CombatConditionType.LostShieldAC),
                $"Improved Trip with a sling and a shield: a shield bash as an off-hand attack, shield AC lost (PHB p.125; source {bashSource}, shield {shieldBefore} -> {basher.Stats.ShieldBonus})");

            // An unarmed target without Improved Unarmed Strike is not armed: the unarmed strike provokes nothing.
            events.Clear();
            attacks.Clear();
            bare = CreateImprovedTripper("TripFallbackBare", new Vector2Int(60, 43), CloneWeaponByStringId(ItemIDs.LONGSPEAR));
            bareTarget = CreateSturdyTripTarget("TripFallbackBareTarget", new Vector2Int(61, 43), armed: false);
            bool bareActed = gm.TryNPCSpecialAttackByTypeForAI(bare, bareTarget, SpecialAttackType.Trip);
            Assert(bareActed && !bare.DoesUnarmedAttackProvoke(bareTarget) && !events.Exists(e => e.StartsWith("aoo ", System.StringComparison.Ordinal))
                && events.Contains("attack " + bare.Stats.CharacterName + " Unarmed strike"),
                $"Improved Trip unarmed strike against an unarmed target provokes nothing (PHB p.139; events: {string.Join(" | ", events)})");

            // A main weapon that reaches is still the one used.
            events.Clear();
            attacks.Clear();
            sword = CreateImprovedTripper("TripFallbackSword", new Vector2Int(60, 46), ItemDatabase.CloneItem(ItemID.WeaponLongsword), ItemDatabase.CloneItem(ItemID.WeaponDagger));
            swordTarget = CreateSturdyTripTarget("TripFallbackSwordTarget", new Vector2Int(61, 46), armed: true);
            bool swordActed = gm.TryNPCSpecialAttackByTypeForAI(sword, swordTarget, SpecialAttackType.Trip);
            Assert(swordActed && events.Contains("attack " + sword.Stats.CharacterName + " Longsword")
                && !events.Exists(e => e.StartsWith("aoo ", System.StringComparison.Ordinal)),
                $"Improved Trip with a longsword in reach: the longsword, not the off-hand dagger (events: {string.Join(" | ", events)})");

            // A teammate takes no AoO against the unarmed strike (ThreatSystem.GetThreateningEnemies skips teammates).
            events.Clear();
            attacks.Clear();
            mate = CreateImprovedTripper("TripFallbackMate", new Vector2Int(60, 52), CloneWeaponByStringId(ItemIDs.LONGSPEAR));
            mateTarget = CreateSturdyTripTarget("TripFallbackMateTarget", new Vector2Int(61, 52), armed: true);
            mateTarget.SetTeam(mate.Team);
            bool mateActed = gm.TryNPCSpecialAttackByTypeForAI(mate, mateTarget, SpecialAttackType.Trip);
            Assert(mateActed && !events.Exists(e => e.StartsWith("aoo ", System.StringComparison.Ordinal))
                && events.Contains("attack " + mate.Stats.CharacterName + " Unarmed strike"),
                $"Improved Trip unarmed strike against a teammate: no AoO (events: {string.Join(" | ", events)})");

            // PHB p.139 "armed" unarmed attacks: claws make the tripper's unarmed attack armed even while it holds a
            // weapon; a target holding only a bow is armed when it has Improved Unarmed Strike, not otherwise.
            clawed = CreateImprovedTripper("TripFallbackClawed", new Vector2Int(60, 55), CloneWeaponByStringId(ItemIDs.LONGSPEAR));
            clawed.Stats.NaturalAttacks.Add(new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2 });
            clawedTarget = CreateSturdyTripTarget("TripFallbackClawedTarget", new Vector2Int(61, 55), armed: true);
            archer = CreateWeakDefender("TripFallbackArcher");
            archer.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponShortbow), EquipSlot.RightHand);
            archer.GetComponent<InventoryComponent>().CharacterInventory.RecalculateStats();
            monkArcher = CreateWeakDefender("TripFallbackMonkArcher");
            monkArcher.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemID.WeaponShortbow), EquipSlot.RightHand);
            monkArcher.GetComponent<InventoryComponent>().CharacterInventory.RecalculateStats();
            monkArcher.Stats.Feats.Add("Improved Unarmed Strike");
            CharacterController.ImprovedTripAttackSource clawSource = clawed.SelectImprovedTripAttack(clawedTarget, out _, out _);
            Assert(clawSource == CharacterController.ImprovedTripAttackSource.UnarmedStrike
                && !clawed.DoesUnarmedAttackProvoke(clawedTarget) && clawed.HasNaturalPhysicalWeapons(),
                $"PHB p.139: a creature with claws holding a longspear makes an armed unarmed attack, which provokes nothing (source {clawSource})");
            Assert(!archer.IsArmedAgainstUnarmedAttacks() && monkArcher.IsArmedAgainstUnarmedAttacks(),
                "PHB p.139: a target holding only a bow is unarmed, but armed with Improved Unarmed Strike");

            // A conscious tripper at 0 HP or below still makes the attack; an unconscious one does not.
            diehard = CreateImprovedTripper("TripFallbackDiehard", new Vector2Int(60, 58), ItemDatabase.CloneItem(ItemID.WeaponLongsword));
            diehard.Stats.Feats.Add("Diehard");
            diehard.Stats.CurrentHP = -3;
            dying = CreateImprovedTripper("TripFallbackDying", new Vector2Int(62, 58), ItemDatabase.CloneItem(ItemID.WeaponLongsword));
            dying.Stats.CurrentHP = -3;
            CharacterController dieTarget = swordTarget;
            dieTarget.GridPosition = new Vector2Int(61, 58);
            Assert(diehard.CurrentHPState == HPState.Disabled
                && diehard.SelectImprovedTripAttack(dieTarget, out _, out _) == CharacterController.ImprovedTripAttackSource.MainWeapon,
                $"A disabled (Diehard, -3 HP) tripper still makes the Improved Trip attack (state {diehard.CurrentHPState})");
            Assert(dying.CurrentHPState == HPState.Dying
                && dying.SelectImprovedTripAttack(dieTarget, out _, out string dyingNote) == CharacterController.ImprovedTripAttackSource.None
                && dyingNote != null,
                $"A dying tripper makes no Improved Trip attack (state {dying.CurrentHPState})");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex.InnerException ?? ex;
            Assert(false, $"Improved Trip weapon fallback check threw {inner.GetType().Name}: {inner.Message}");
        }
        finally
        {
            ScenarioHooks.RollFilter = savedFilter;
            ScenarioHooks.AttackResolved = savedAttack;
            ScenarioHooks.AoOResolved = savedAoO;
            Cleanup(spear, spearTarget, ius, iusTarget, sling, slingTarget, basher, basherTarget, bare, bareTarget, sword, swordTarget,
                mate, mateTarget, clawed, clawedTarget, archer, monkArcher, diehard, dying);
        }
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
