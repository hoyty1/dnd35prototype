using System;
using System.Collections.Generic;
using DND35.AI;
using DND35.AI.Profiles;
using Tests.Utilities;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.AI
{
    /// <summary>
    /// Runtime tests for AI profile scoring and maneuver preference hooks.
    /// Run via AIProfileFrameworkTests.RunAll().
    /// </summary>
    public static class AIProfileFrameworkTests
    {
        private static int _passed;
        private static int _failed;

        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            Debug.Log("====== AI PROFILE FRAMEWORK TESTS ======");
            TestHelpers.EnsureCoreDatabasesInitialized();

            TestBerserkPrioritizesWounded();
            TestRangedPenalizesHeavyArmor();
            TestProfileCanDriveManeuverDecision();
            TestProfileSkipsTripAgainstProneTarget();
            TestProfileSkipsDisarmAgainstUnarmedTarget();
            TestProfileSkipsSunderWhenNoEquipment();
            TestProfileCanSelectValidTripAndDisarm();
            TestAnimalProfilePrefersNearestTarget();
            TestAnimalProfileUsesNaturalTripFollowUpInsteadOfManeuver();
            TestAnimalProfileEnablesAdaptiveFullAttackRetargeting();
            TestDefaultProfileLeavesAdaptiveFullAttackRetargetingDisabled();
            TestProfileCoupDeGraceDefaults();
            TestNPCDefinitionCoupDeGraceOverrideFieldDefaultsToNull();
            TestNPCDefinitionTeamControlFlagsDefaultToEnemyAI();
            TestNPCDefinitionsAssignProfileArchetypes();
            TestWolfDefinitionMatchesBaselineStats();
            TestTigerDefinitionUsesAnimalProfile();
            TestScenarioEnemyProfileAssignments();
            TestEvokerSchoolPriority();
            TestAbjurerPrefersSingleTarget();
            TestSpellcasterAOEAvoidsUnsafeFriendlyFire();
            TestHealerPrioritizesHealing();
            TestHealerUsesBuffingWhenAlliesHealthy();
            TestHealerChoosesCriticalHealing();
            TestHealerChoosesMeleeWithHighACAndMeleeAB();
            TestHealerChoosesRangedWithLowAC();
            TestHealerPrioritizesMostWoundedAlly();
            TestHealerIgnoresWoundedUndeadAlly();
            TestAiEnergyChoiceReadsVisibleEnemyAttacks();
            TestAiWeaponAndDeciderChoices();
            TestAiSpellCandidatesTryInOrder();
            TestAiSpellTargetingUsesTeamUtility();
            TestAiWeaponChoiceSkipsCastsThatAddNothing();
            TestAiEnemySpellTargetNeedsSight();
            TestAiOffensiveSpellNeverFallsBackToAlly();
            TestRangedRiskMultiplierDefaults();
            TestConcealmentTargetingAdjustmentUsesPriorityTiers();
            TestConcealmentTargetingAdjustmentSupportsProfileMultiplier();
            TestThreatSystemEstimatesRangedProvocationDamage();
            TestThreatSystemCountsOffHandMeleeThreateners();
            TestSwarmAISticksToCurrentTargetUntilDead();
            TestIndiscriminateSwarmAITargetsNearestCreature();
            TestUseMoveActionReportsFailureOnceSpent();
            TestUseMoveActionFailsAfterFullRoundAction();
            TestUseMoveActionRespectsDisabledSingleAction();
            TestSecondMoveAfterSearchNeedsExplicitDoubleMove();
            TestAiTwoWeaponStopgap();
            TestAiRapidShotStopgap();

            Debug.Log($"====== AI PROFILE RESULTS: {_passed} passed, {_failed} failed ======");
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

        // ---- Batch 12 step 18 (AI-055 closed; owner decision Q2, 2026-10-23): the AI attack-mode stopgaps ----

        private static CharacterController CreateArmedFighter(string name, int bab, Vector2Int position, ItemID? rightHand,
            ItemID? leftHand, params string[] feats)
        {
            var go = new GameObject("AIModes_" + name);
            var controller = go.AddComponent<CharacterController>();
            var inventoryComp = go.AddComponent<InventoryComponent>();
            var stats = new CharacterStats(name, bab, "Fighter",
                14, 14, 14, 10, 10, 10,
                bab, 0, 0,
                8, 1, 0,
                6, 1, 200,
                "Human");
            if (feats != null && feats.Length > 0)
                stats.AddFeats(new List<string>(feats));
            controller.Init(stats, position, null, null);
            inventoryComp.Init(stats);
            if (rightHand.HasValue)
                inventoryComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(rightHand.Value), EquipSlot.RightHand);
            if (leftHand.HasValue)
                inventoryComp.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(leftHand.Value), EquipSlot.LeftHand);
            inventoryComp.CharacterInventory.RecalculateStats();
            return controller;
        }

        /// <summary>
        /// AIProfile.DefaultShouldUseTwoWeaponFighting (AI stopgap, batch 13 catalog): a fighter 6 with Two-Weapon Fighting,
        /// Improved Two-Weapon Fighting and a light off-hand weapon fights with both (four attacks at -2 beat two); a fighter 6
        /// without the feats and with a one-handed off-hand weapon (-6 and -10, PHB p.160 Table 8-10) does not; once a flurry
        /// or an attack has started, or the turn cannot become a full attack, it does not.
        /// </summary>
        private static void TestAiTwoWeaponStopgap()
        {
            CharacterController twf = null, noFeat = null, target = null;
            try
            {
                twf = CreateArmedFighter("TwfFighter", 6, Vector2Int.zero, ItemID.WeaponLongsword, ItemID.WeaponShortsword,
                    "Two-Weapon Fighting", "Improved Two-Weapon Fighting");
                noFeat = CreateArmedFighter("HeavyOffHand", 6, new Vector2Int(0, 2), ItemID.WeaponLongsword, ItemID.WeaponLongsword);
                target = CreateArmedFighter("TwfTarget", 1, Vector2Int.right, null, null);

                Assert(twf.GetOffHandAttackCount() == 2 && AIProfile.DefaultShouldUseTwoWeaponFighting(twf, target),
                    "AI stopgap: a TWF + ITWF fighter with a light off-hand weapon fights with two weapons (PHB p.160, p.96)",
                    "off-hand attacks " + twf.GetOffHandAttackCount());
                Assert(noFeat.GetOffHandAttackCount() == 1 && !AIProfile.DefaultShouldUseTwoWeaponFighting(noFeat, target),
                    "AI stopgap: without the feat and with a one-handed off-hand weapon (-6/-10) it attacks with the main hand only; it would still get one off-hand attack (PHB p.160)",
                    "off-hand attacks " + noFeat.GetOffHandAttackCount());

                twf.Actions.UseMoveAction();
                Assert(!AIProfile.DefaultShouldUseTwoWeaponFighting(twf, target),
                    "AI stopgap: no two-weapon choice when the turn can no longer become a full attack (PHB p.143)");
                twf.Actions.Reset();
            }
            finally
            {
                TestHelpers.Cleanup(twf != null ? twf.gameObject : null, noFeat != null ? noFeat.gameObject : null,
                    target != null ? target.gameObject : null);
            }
        }

        /// <summary>
        /// AIProfile.DefaultShouldUseRapidShot (AI stopgap, batch 13 catalog): a fighter 6 with Rapid Shot and a longbow
        /// declares it (three shots at -2 beat two, PHB p.99); without the feat it does not; with a single dagger to throw
        /// there is no extra throw to make, so it does not, and with a second dagger carried it does (PHB p.113).
        /// </summary>
        private static void TestAiRapidShotStopgap()
        {
            CharacterController archer = null, noFeat = null, thrower = null, target = null;
            try
            {
                archer = CreateArmedFighter("RapidArcher", 6, Vector2Int.zero, ItemID.WeaponLongbow, null, "Rapid Shot");
                noFeat = CreateArmedFighter("PlainArcher", 6, new Vector2Int(0, 2), ItemID.WeaponLongbow, null);
                thrower = CreateArmedFighter("DaggerThrower", 6, new Vector2Int(0, 4), ItemID.WeaponDagger, null, "Rapid Shot");
                target = CreateArmedFighter("RapidTarget", 1, new Vector2Int(4, 0), null, null);

                Assert(AIProfile.DefaultShouldUseRapidShot(archer, target, archer.GetEquippedMainWeapon()),
                    "AI stopgap: a Rapid Shot archer with a bow declares it (PHB p.99)");
                Assert(!AIProfile.DefaultShouldUseRapidShot(noFeat, target, noFeat.GetEquippedMainWeapon()),
                    "AI stopgap: no Rapid Shot without the feat");
                Assert(!AIProfile.DefaultShouldUseRapidShot(thrower, target, thrower.GetEquippedMainWeapon()),
                    "AI stopgap: one dagger to throw gives no extra throw, so no Rapid Shot");
                thrower.GetComponent<InventoryComponent>().CharacterInventory.AddItem(ItemDatabase.CloneItem(ItemID.WeaponDagger));
                Assert(AIProfile.DefaultShouldUseRapidShot(thrower, target, thrower.GetEquippedMainWeapon()),
                    "AI stopgap: with a second dagger carried the thrower declares Rapid Shot (PHB p.99, p.113)");
            }
            finally
            {
                TestHelpers.Cleanup(archer != null ? archer.gameObject : null, noFeat != null ? noFeat.gameObject : null,
                    thrower != null ? thrower.gameObject : null, target != null ? target.gameObject : null);
            }
        }

        private static void TestBerserkPrioritizesWounded()
        {
            CharacterController ai = TestHelpers.CreateWarrior("AI_Berserk", level: 5);
            CharacterController healthy = TestHelpers.CreateWarrior("Target_Healthy", level: 5);
            CharacterController wounded = TestHelpers.CreateWarrior("Target_Wounded", level: 5);
            BerserkAIProfile profile = ScriptableObject.CreateInstance<BerserkAIProfile>();

            try
            {
                wounded.Stats.CurrentHP = Mathf.Max(1, wounded.Stats.TotalMaxHP / 4);

                float healthyScore = profile.ScoreTarget(healthy, ai);
                float woundedScore = profile.ScoreTarget(wounded, ai);

                Assert(woundedScore > healthyScore,
                    "Berserk profile prioritizes wounded target",
                    $"(healthy={healthyScore:F2}, wounded={woundedScore:F2})");
            }
            finally
            {
                TestHelpers.Cleanup(ai != null ? ai.gameObject : null,
                    healthy != null ? healthy.gameObject : null,
                    wounded != null ? wounded.gameObject : null,
                    profile);
            }
        }

        private static void TestRangedPenalizesHeavyArmor()
        {
            CharacterController ai = TestHelpers.CreateRogue("AI_Ranged", level: 5);
            CharacterController lightArmor = TestHelpers.CreateWarrior("Target_Light", level: 5);
            CharacterController heavyArmor = TestHelpers.CreateWarrior("Target_Heavy", level: 5);
            RangedAIProfile profile = ScriptableObject.CreateInstance<RangedAIProfile>();

            try
            {
                lightArmor.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LEATHER_ARMOR), EquipSlot.Armor);
                heavyArmor.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.FULL_PLATE), EquipSlot.Armor);

                float lightScore = profile.ScoreTarget(lightArmor, ai);
                float heavyScore = profile.ScoreTarget(heavyArmor, ai);

                Assert(lightScore > heavyScore,
                    "Ranged profile prefers lighter armor targets",
                    $"(light={lightScore:F2}, heavy={heavyScore:F2})");
            }
            finally
            {
                TestHelpers.Cleanup(ai != null ? ai.gameObject : null,
                    lightArmor != null ? lightArmor.gameObject : null,
                    heavyArmor != null ? heavyArmor.gameObject : null,
                    profile);
            }
        }

        private static void TestProfileCanDriveManeuverDecision()
        {
            var aiServiceGo = new GameObject("AIService_Test");
            AIService aiService = aiServiceGo.AddComponent<AIService>();
            CharacterController attacker = TestHelpers.CreateWarrior("AI_Grappler", level: 6);
            CharacterController defender = TestHelpers.CreateWarrior("Target_Armed", level: 6);
            GrapplerAIProfile profile = ScriptableObject.CreateInstance<GrapplerAIProfile>();

            try
            {
                attacker.aiProfile = profile;
                defender.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);

                bool shouldUseManeuver = aiService.ShouldUseManeuver(attacker, defender);
                Assert(shouldUseManeuver, "AIService reads profile and opts into maneuver usage");
            }
            finally
            {
                TestHelpers.Cleanup(aiServiceGo,
                    attacker != null ? attacker.gameObject : null,
                    defender != null ? defender.gameObject : null,
                    profile);
            }
        }

        private static void TestProfileSkipsTripAgainstProneTarget()
        {
            CharacterController attacker = TestHelpers.CreateWarrior("AI_Tripper", level: 5);
            CharacterController defender = TestHelpers.CreateWarrior("Target_Prone", level: 5);
            AIProfile profile = ScriptableObject.CreateInstance<AIProfile>();

            try
            {
                profile.Maneuvers.AttemptTrip = true;
                profile.Maneuvers.AttemptDisarm = false;
                profile.Maneuvers.AttemptSunder = false;
                profile.GrappleBehavior = GrappleBehavior.Avoid;

                defender.Conditions.ApplyCondition(CombatConditionType.Prone, 1, "Test");

                SpecialAttackType? preferred = profile.GetPreferredManeuver(attacker, defender);
                Assert(preferred != SpecialAttackType.Trip,
                    "Profile does not choose Trip against prone target",
                    $"(preferred={preferred?.ToString() ?? "none"})");
            }
            finally
            {
                TestHelpers.Cleanup(attacker != null ? attacker.gameObject : null,
                    defender != null ? defender.gameObject : null,
                    profile);
            }
        }

        private static void TestProfileSkipsDisarmAgainstUnarmedTarget()
        {
            CharacterController attacker = TestHelpers.CreateWarrior("AI_Disarmer", level: 5);
            CharacterController defender = TestHelpers.CreateWarrior("Target_Unarmed", level: 5);
            AIProfile profile = ScriptableObject.CreateInstance<AIProfile>();

            try
            {
                profile.Maneuvers.AttemptTrip = false;
                profile.Maneuvers.AttemptDisarm = true;
                profile.Maneuvers.AttemptSunder = false;
                profile.GrappleBehavior = GrappleBehavior.Avoid;

                global::Inventory inv = defender.GetComponent<InventoryComponent>().CharacterInventory;
                inv.RightHandSlot = null;
                inv.LeftHandSlot = null;
                inv.RecalculateStats();

                SpecialAttackType? preferred = profile.GetPreferredManeuver(attacker, defender);
                Assert(preferred != SpecialAttackType.Disarm,
                    "Profile does not choose Disarm against unarmed target",
                    $"(preferred={preferred?.ToString() ?? "none"})");
            }
            finally
            {
                TestHelpers.Cleanup(attacker != null ? attacker.gameObject : null,
                    defender != null ? defender.gameObject : null,
                    profile);
            }
        }

        private static void TestProfileSkipsSunderWhenNoEquipment()
        {
            CharacterController attacker = TestHelpers.CreateWarrior("AI_Sunderer", level: 5);
            CharacterController defender = TestHelpers.CreateWarrior("Target_NoGear", level: 5);
            AIProfile profile = ScriptableObject.CreateInstance<AIProfile>();

            try
            {
                profile.Maneuvers.AttemptTrip = false;
                profile.Maneuvers.AttemptDisarm = false;
                profile.Maneuvers.AttemptSunder = true;
                profile.GrappleBehavior = GrappleBehavior.Avoid;

                global::Inventory inv = defender.GetComponent<InventoryComponent>().CharacterInventory;
                inv.RightHandSlot = null;
                inv.LeftHandSlot = null;
                inv.ArmorRobeSlot = null;
                inv.RecalculateStats();

                SpecialAttackType? preferred = profile.GetPreferredManeuver(attacker, defender);
                Assert(preferred != SpecialAttackType.Sunder,
                    "Profile does not choose Sunder without sunderable equipment",
                    $"(preferred={preferred?.ToString() ?? "none"})");
            }
            finally
            {
                TestHelpers.Cleanup(attacker != null ? attacker.gameObject : null,
                    defender != null ? defender.gameObject : null,
                    profile);
            }
        }

        private static void TestProfileCanSelectValidTripAndDisarm()
        {
            CharacterController attacker = TestHelpers.CreateWarrior("AI_ValidManeuvers", level: 6);
            CharacterController tripTarget = TestHelpers.CreateWarrior("Target_Standing", level: 5);
            CharacterController disarmTarget = TestHelpers.CreateWarrior("Target_Armed_2", level: 5);
            AIProfile profile = ScriptableObject.CreateInstance<AIProfile>();

            try
            {
                profile.Maneuvers.AttemptTrip = true;
                profile.Maneuvers.AttemptDisarm = true;
                profile.Maneuvers.AttemptSunder = false;
                profile.GrappleBehavior = GrappleBehavior.Avoid;

                SpecialAttackType? tripChoice = profile.GetPreferredManeuver(attacker, tripTarget);
                Assert(tripChoice == SpecialAttackType.Trip,
                    "Profile chooses Trip for valid standing target",
                    $"(preferred={tripChoice?.ToString() ?? "none"})");

                profile.Maneuvers.AttemptTrip = false;
                disarmTarget.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);

                SpecialAttackType? disarmChoice = profile.GetPreferredManeuver(attacker, disarmTarget);
                Assert(disarmChoice == SpecialAttackType.Disarm,
                    "Profile chooses Disarm for armed target",
                    $"(preferred={disarmChoice?.ToString() ?? "none"})");
            }
            finally
            {
                TestHelpers.Cleanup(attacker != null ? attacker.gameObject : null,
                    tripTarget != null ? tripTarget.gameObject : null,
                    disarmTarget != null ? disarmTarget.gameObject : null,
                    profile);
            }
        }

        private static void TestAnimalProfilePrefersNearestTarget()
        {
            CharacterController wolf = TestHelpers.CreateWarrior("AI_Wolf", level: 2);
            CharacterController nearTarget = TestHelpers.CreateWarrior("Near_Target", level: 3);
            CharacterController farTarget = TestHelpers.CreateWarrior("Far_Target", level: 3);
            AnimalAIProfile profile = ScriptableObject.CreateInstance<AnimalAIProfile>();

            try
            {
                TestHelpers.SetGridPosition(wolf, 0, 0);
                TestHelpers.SetGridPosition(nearTarget, 1, 0);
                TestHelpers.SetGridPosition(farTarget, 6, 0);

                float nearScore = profile.ScoreTarget(nearTarget, wolf);
                float farScore = profile.ScoreTarget(farTarget, wolf);

                Assert(nearScore > farScore,
                    "Animal profile prefers nearest target",
                    $"(near={nearScore:F2}, far={farScore:F2})");
            }
            finally
            {
                TestHelpers.Cleanup(wolf != null ? wolf.gameObject : null,
                    nearTarget != null ? nearTarget.gameObject : null,
                    farTarget != null ? farTarget.gameObject : null,
                    profile);
            }
        }

        private static void TestAnimalProfileUsesNaturalTripFollowUpInsteadOfManeuver()
        {
            CharacterController wolf = TestHelpers.CreateWarrior("AI_Wolf_Trip", level: 2);
            CharacterController target = TestHelpers.CreateWarrior("Trip_Target", level: 3);
            AnimalAIProfile profile = ScriptableObject.CreateInstance<AnimalAIProfile>();

            try
            {
                wolf.Stats.HasTripAttack = true;
                SpecialAttackType? preferred = profile.GetPreferredManeuver(wolf, target);

                Assert(!preferred.HasValue,
                    "Animal profile defers innate trip creatures to normal attacks",
                    $"(preferred={preferred?.ToString() ?? "none"})");
            }
            finally
            {
                TestHelpers.Cleanup(wolf != null ? wolf.gameObject : null,
                    target != null ? target.gameObject : null,
                    profile);
            }
        }

        private static void TestAnimalProfileEnablesAdaptiveFullAttackRetargeting()
        {
            CharacterController tiger = TestHelpers.CreateWarrior("AI_Tiger_Profile", level: 8);
            AnimalAIProfile profile = ScriptableObject.CreateInstance<AnimalAIProfile>();

            try
            {
                bool ignoreUnconscious = profile.ShouldIgnoreUnconsciousTargets(tiger);
                bool switchMidAttack = profile.ShouldSwitchTargetsMidFullAttack(tiger);
                bool allowStepRetarget = profile.ShouldTakeFiveFootStepToContinueFullAttack(tiger);

                Assert(ignoreUnconscious && switchMidAttack && allowStepRetarget,
                    "Animal profile enables adaptive full-attack retargeting behavior",
                    $"(ignoreUnconscious={ignoreUnconscious}, switchMidAttack={switchMidAttack}, allowStepRetarget={allowStepRetarget})");
            }
            finally
            {
                TestHelpers.Cleanup(tiger != null ? tiger.gameObject : null, profile);
            }
        }

        private static void TestDefaultProfileLeavesAdaptiveFullAttackRetargetingDisabled()
        {
            CharacterController attacker = TestHelpers.CreateWarrior("AI_Default_Profile", level: 5);
            AIProfile profile = ScriptableObject.CreateInstance<AIProfile>();

            try
            {
                bool ignoreUnconscious = profile.ShouldIgnoreUnconsciousTargets(attacker);
                bool switchMidAttack = profile.ShouldSwitchTargetsMidFullAttack(attacker);
                bool allowStepRetarget = profile.ShouldTakeFiveFootStepToContinueFullAttack(attacker);

                Assert(!ignoreUnconscious && !switchMidAttack && !allowStepRetarget,
                    "Base profile keeps adaptive full-attack retargeting disabled by default",
                    $"(ignoreUnconscious={ignoreUnconscious}, switchMidAttack={switchMidAttack}, allowStepRetarget={allowStepRetarget})");
            }
            finally
            {
                TestHelpers.Cleanup(attacker != null ? attacker.gameObject : null, profile);
            }
        }

        private static void TestProfileCoupDeGraceDefaults()
        {
            CharacterController attacker = TestHelpers.CreateWarrior("AI_Coup_Defaults", level: 5);
            AnimalAIProfile animalProfile = ScriptableObject.CreateInstance<AnimalAIProfile>();
            BerserkAIProfile berserkProfile = ScriptableObject.CreateInstance<BerserkAIProfile>();
            SpellcasterAIProfile spellcasterProfile = ScriptableObject.CreateInstance<SpellcasterAIProfile>();
            AIProfile baseProfile = ScriptableObject.CreateInstance<AIProfile>();

            try
            {
                bool animalUsesCoup = animalProfile.ShouldUseCoupDeGrace(attacker);
                bool berserkUsesCoup = berserkProfile.ShouldUseCoupDeGrace(attacker);
                bool spellcasterUsesCoup = spellcasterProfile.ShouldUseCoupDeGrace(attacker);
                bool baseUsesCoup = baseProfile.ShouldUseCoupDeGrace(attacker);

                Assert(animalUsesCoup && berserkUsesCoup && !spellcasterUsesCoup && !baseUsesCoup,
                    "AI profile coup de grace defaults match archetype intent",
                    $"(animal={animalUsesCoup}, berserk={berserkUsesCoup}, spellcaster={spellcasterUsesCoup}, base={baseUsesCoup})");
            }
            finally
            {
                TestHelpers.Cleanup(attacker != null ? attacker.gameObject : null, animalProfile, berserkProfile, spellcasterProfile, baseProfile);
            }
        }

        private static void TestNPCDefinitionCoupDeGraceOverrideFieldDefaultsToNull()
        {
            NPCDatabase.Init();
            NPCDefinition wolf = NPCDatabase.Get("wolf_pack_hunter");
            NPCDefinition tiger = NPCDatabase.Get("tiger");

            bool defaultsToNull = wolf != null
                                  && tiger != null
                                  && !wolf.UseCoupDeGrace.HasValue
                                  && !tiger.UseCoupDeGrace.HasValue;

            Assert(defaultsToNull,
                "Enemy definitions default coup de grace override to null (profile-driven)",
                wolf == null || tiger == null
                    ? "(enemy definition missing)"
                    : $"(wolfOverride={(wolf.UseCoupDeGrace.HasValue ? wolf.UseCoupDeGrace.Value.ToString() : "null")}, tigerOverride={(tiger.UseCoupDeGrace.HasValue ? tiger.UseCoupDeGrace.Value.ToString() : "null")})");
        }

        private static void TestNPCDefinitionTeamControlFlagsDefaultToEnemyAI()
        {
            NPCDatabase.Init();
            NPCDefinition wolf = NPCDatabase.Get("wolf_pack_hunter");
            NPCDefinition goblin = NPCDatabase.Get("goblin_warchief");

            bool defaultsAreEnemyAI = wolf != null
                                     && goblin != null
                                     && !wolf.IsAlly && !wolf.IsControllable
                                     && !goblin.IsAlly && !goblin.IsControllable;

            Assert(defaultsAreEnemyAI,
                "NPC definitions default to enemy AI team/control flags",
                wolf == null || goblin == null
                    ? "(npc definition missing)"
                    : $"(wolf ally={wolf.IsAlly}, ctrl={wolf.IsControllable}; goblin ally={goblin.IsAlly}, ctrl={goblin.IsControllable})");
        }

        private static void TestNPCDefinitionsAssignProfileArchetypes()
        {
            NPCDatabase.Init();

            int total = 0;
            List<string> missing = new List<string>();

            foreach (NPCDefinition def in NPCDatabase.AllNPCs)
            {
                if (def == null)
                    continue;

                total++;
                if (def.AIProfileArchetype == NPCAIProfileArchetype.None)
                    missing.Add(def.Id);
            }

            bool allAssigned = total > 0 && missing.Count == 0;
            Assert(allAssigned,
                "Enemy database assigns AI profile archetypes for all enemies",
                $"(total={total}, missing={string.Join(",", missing)})");
        }

        private static void TestWolfDefinitionMatchesBaselineStats()
        {
            NPCDatabase.Init();
            NPCDefinition wolf = NPCDatabase.Get("wolf_pack_hunter");

            NaturalAttackDefinition wolfPrimaryAttack = wolf != null && wolf.NaturalAttacks != null && wolf.NaturalAttacks.Count > 0
                ? wolf.NaturalAttacks[0]
                : null;

            bool statsMatch = wolf != null
                              && wolf.BAB == 1
                              && wolf.BaseHitDieHP == 13
                              && wolf.BaseSpeed == 10
                              && wolf.NaturalArmorBonus == 2
                              && wolfPrimaryAttack != null
                              && wolfPrimaryAttack.Count == 1
                              && wolfPrimaryAttack.IsPrimary
                              && wolf.HasTripAttack
                              && wolf.TripAttackCheckBonus == 1;

            Assert(statsMatch,
                "Wolf enemy definition uses Monster Manual baseline core stats",
                wolf == null
                    ? "(wolf definition missing)"
                    : $"(BAB={wolf.BAB}, HP={wolf.BaseHitDieHP}, Speed={wolf.BaseSpeed}, NA={wolf.NaturalArmorBonus}, AttackCount={wolfPrimaryAttack?.Count ?? 0}, TripBonus={wolf.TripAttackCheckBonus})");
        }

        private static void TestTigerDefinitionUsesAnimalProfile()
        {
            NPCDatabase.Init();
            NPCDefinition tiger = NPCDatabase.Get("tiger");

            NaturalAttackDefinition tigerClaw = tiger != null && tiger.NaturalAttacks != null
                ? tiger.NaturalAttacks.Find(a => a != null && string.Equals(a.Name, "Claw", StringComparison.OrdinalIgnoreCase))
                : null;
            NaturalAttackDefinition tigerBite = tiger != null && tiger.NaturalAttacks != null
                ? tiger.NaturalAttacks.Find(a => a != null && string.Equals(a.Name, "Bite", StringComparison.OrdinalIgnoreCase))
                : null;
            int tigerNaturalAttackCount = 0;
            if (tiger != null && tiger.NaturalAttacks != null)
            {
                for (int i = 0; i < tiger.NaturalAttacks.Count; i++)
                {
                    NaturalAttackDefinition attack = tiger.NaturalAttacks[i];
                    if (attack != null && attack.DamageDice > 0 && attack.DamageCount > 0)
                        tigerNaturalAttackCount += Mathf.Max(1, attack.Count);
                }
            }

            bool usesAnimalProfile = tiger != null
                                     && tiger.AIProfileArchetype == NPCAIProfileArchetype.Animal
                                     && tiger.HasScent
                                     && tiger.HasPounce
                                     && tiger.HasImprovedGrab
                                     && tiger.HasRake
                                     && string.Equals(tiger.ImprovedGrabTriggerAttackName, "Claw,Bite", StringComparison.OrdinalIgnoreCase) // MM p.281: claw or bite
                                     && tigerNaturalAttackCount == 3
                                     && tigerClaw != null
                                     && tigerClaw.IsPrimary
                                     && tigerClaw.Count == 2
                                     && tigerBite != null
                                     && !tigerBite.IsPrimary
                                     && tigerBite.Count == 1;

            Assert(usesAnimalProfile,
                "Tiger enemy definition uses Animal AI profile archetype",
                tiger == null
                    ? "(tiger definition missing)"
                    : $"(archetype={tiger.AIProfileArchetype}, scent={tiger.HasScent}, pounce={tiger.HasPounce}, grab={tiger.HasImprovedGrab}, trigger={tiger.ImprovedGrabTriggerAttackName}, rake={tiger.HasRake}, naturalAttackCount={tigerNaturalAttackCount}, clawCount={tigerClaw?.Count ?? 0}, biteCount={tigerBite?.Count ?? 0})");
        }

        private static void TestScenarioEnemyProfileAssignments()
        {
            NPCDatabase.Init();

            var expected = new Dictionary<string, NPCAIProfileArchetype>
            {
                { "goblin_warchief", NPCAIProfileArchetype.Humanoid },
                { "goblin_feint_drill", NPCAIProfileArchetype.Humanoid },
                { "skeleton_archer", NPCAIProfileArchetype.UndeadMindless },
                { "skeleton_warrior", NPCAIProfileArchetype.UndeadMindless },
                { "wight_dreadwalker", NPCAIProfileArchetype.Humanoid },
                { "orc_berserker", NPCAIProfileArchetype.Berserk },
                { "orc_grapple_drill", NPCAIProfileArchetype.Grappler },
                { "hobgoblin_sergeant", NPCAIProfileArchetype.Humanoid },
                { "ogre_brute", NPCAIProfileArchetype.Berserk },
                { "dire_wolf", NPCAIProfileArchetype.Animal },
                { "tiger", NPCAIProfileArchetype.Animal },
                { "brown_bear", NPCAIProfileArchetype.Animal },
                { "wolf_pack_hunter", NPCAIProfileArchetype.Animal },
            };

            List<string> mismatches = new List<string>();
            foreach (var pair in expected)
            {
                NPCDefinition def = NPCDatabase.Get(pair.Key);
                if (def == null)
                {
                    mismatches.Add($"{pair.Key}=missing");
                    continue;
                }

                if (def.AIProfileArchetype != pair.Value)
                    mismatches.Add($"{pair.Key}={def.AIProfileArchetype} (expected {pair.Value})");
            }

            Assert(mismatches.Count == 0,
                "Scenario enemy definitions use expected AI profile archetypes",
                mismatches.Count > 0 ? $"(mismatches: {string.Join(", ", mismatches)})" : string.Empty);
        }

        private static void TestEvokerSchoolPriority()
        {
            EvokerAIProfile profile = ScriptableObject.CreateInstance<EvokerAIProfile>();
            try
            {
                float evocation = profile.GetSchoolPriority(SpellSchool.Evocation);
                float abjuration = profile.GetSchoolPriority(SpellSchool.Abjuration);
                Assert(evocation > abjuration,
                    "Evoker profile prioritizes Evocation school",
                    $"(evocation={evocation:F1}, abjuration={abjuration:F1})");
            }
            finally
            {
                TestHelpers.Cleanup(profile);
            }
        }

        private static void TestAbjurerPrefersSingleTarget()
        {
            AbjurerAIProfile profile = ScriptableObject.CreateInstance<AbjurerAIProfile>();
            try
            {
                Assert(profile.AOECasting.PreferSingleTarget,
                    "Abjurer profile prefers single-target casting");
            }
            finally
            {
                TestHelpers.Cleanup(profile);
            }
        }

        private static void TestSpellcasterAOEAvoidsUnsafeFriendlyFire()
        {
            SpellcasterAIProfile profile = ScriptableObject.CreateInstance<SpellcasterAIProfile>();
            var gridGo = new GameObject("Grid_Test");
            var gmGo = new GameObject("GM_Test");
            SquareGrid grid = gridGo.AddComponent<SquareGrid>();
            GameManager gm = gmGo.AddComponent<GameManager>();

            CharacterController caster = null;
            CharacterController enemy = null;
            CharacterController ally1 = null;
            CharacterController ally2 = null;

            try
            {
                gm.Grid = grid;
                grid.GenerateGrid();

                profile.AOECasting.AvoidHittingAllies = true;
                profile.AOECasting.MaxAcceptableAllyCasualties = 0f;
                profile.AOECasting.AcceptableAllyDamagePercent = 0.05f;

                caster = TestHelpers.CreateCharacter("Caster", "Wizard", level: 5, intelligence: 18, bab: 2, gridPosition: new Vector2Int(0, 0));
                enemy = TestHelpers.CreateWarrior("Enemy", level: 5);
                ally1 = TestHelpers.CreateWarrior("Ally1", level: 5);
                ally2 = TestHelpers.CreateWarrior("Ally2", level: 5);

                caster.IsPlayerControlled = true;
                ally1.IsPlayerControlled = true;
                ally2.IsPlayerControlled = true;
                enemy.IsPlayerControlled = false;

                TestHelpers.SetGridPosition(enemy, 2, 2);
                TestHelpers.SetGridPosition(ally1, 2, 2);
                TestHelpers.SetGridPosition(ally2, 2, 3);

                var all = new List<CharacterController> { caster, enemy, ally1, ally2 };

                SpellData fireballLike = new SpellData
                {
                    Name = "Test Fireball",
                    School = "Evocation",
                    TargetType = SpellTargetType.Area,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 2,
                    AreaRadius = 2,
                    AoERangeSquares = 20, // placed within range (a range of 0 is a burst centred on the caster)
                    DamageCount = 6,
                    DamageDice = 6,
                    BonusDamage = 0,
                    DamageType = "fire",
                    EffectType = SpellEffectType.Damage
                };

                SpellAOEEvaluation eval = profile.EvaluateAOECast(fireballLike, caster, enemy, all, gm);
                Assert(!eval.IsSafe,
                    "Spellcaster AOE evaluation blocks unsafe ally damage",
                    $"(alliesHit={eval.AlliesHit}, casualties={eval.EffectiveAllyCasualties:F1})");
            }
            finally
            {
                TestHelpers.Cleanup(caster != null ? caster.gameObject : null,
                    enemy != null ? enemy.gameObject : null,
                    ally1 != null ? ally1.gameObject : null,
                    ally2 != null ? ally2.gameObject : null,
                    profile,
                    gmGo,
                    gridGo);
            }
        }

        private static void TestHealerPrioritizesHealing()
        {
            HealerAIProfile profile = ScriptableObject.CreateInstance<HealerAIProfile>();
            CharacterController healer = TestHelpers.CreateCleric("Healer_Cleric", level: 5);
            CharacterController woundedAlly = TestHelpers.CreateWarrior("Ally_Wounded", level: 5);

            try
            {
                healer.IsPlayerControlled = true;
                woundedAlly.IsPlayerControlled = true;
                woundedAlly.Stats.CurrentHP = Mathf.CeilToInt(woundedAlly.Stats.TotalMaxHP * 0.30f);

                var all = new List<CharacterController> { healer, woundedAlly };
                HealerActionType action = profile.DetermineActionPriority(healer, all, hasCastableSpells: true);

                Assert(action == HealerActionType.Healing,
                    "Healer prioritizes healing for wounded ally",
                    $"(action={action})");
            }
            finally
            {
                TestHelpers.Cleanup(healer != null ? healer.gameObject : null,
                    woundedAlly != null ? woundedAlly.gameObject : null,
                    profile);
            }
        }

        private static void TestHealerUsesBuffingWhenAlliesHealthy()
        {
            HealerAIProfile profile = ScriptableObject.CreateInstance<HealerAIProfile>();
            CharacterController healer = TestHelpers.CreateCleric("Healer_Buffer", level: 5);
            CharacterController healthyAlly = TestHelpers.CreateWarrior("Ally_Healthy", level: 5);

            try
            {
                healer.IsPlayerControlled = true;
                healthyAlly.IsPlayerControlled = true;

                var all = new List<CharacterController> { healer, healthyAlly };
                HealerActionType action = profile.DetermineActionPriority(healer, all, hasCastableSpells: true);

                Assert(action == HealerActionType.Buffing,
                    "Healer chooses buffing when party is healthy",
                    $"(action={action})");
            }
            finally
            {
                TestHelpers.Cleanup(healer != null ? healer.gameObject : null,
                    healthyAlly != null ? healthyAlly.gameObject : null,
                    profile);
            }
        }

        private static void TestHealerChoosesCriticalHealing()
        {
            HealerAIProfile profile = ScriptableObject.CreateInstance<HealerAIProfile>();
            CharacterController healer = TestHelpers.CreateCleric("Healer_Critical", level: 5);
            CharacterController criticalAlly = TestHelpers.CreateWarrior("Ally_Critical", level: 5);

            try
            {
                healer.IsPlayerControlled = true;
                criticalAlly.IsPlayerControlled = true;
                criticalAlly.Stats.CurrentHP = Mathf.CeilToInt(criticalAlly.Stats.TotalMaxHP * 0.15f);

                var all = new List<CharacterController> { healer, criticalAlly };
                HealerActionType action = profile.DetermineActionPriority(healer, all, hasCastableSpells: true);

                Assert(action == HealerActionType.CriticalHealing,
                    "Healer chooses critical healing for emergency HP",
                    $"(action={action})");
            }
            finally
            {
                TestHelpers.Cleanup(healer != null ? healer.gameObject : null,
                    criticalAlly != null ? criticalAlly.gameObject : null,
                    profile);
            }
        }

        private static void TestHealerChoosesMeleeWithHighACAndMeleeAB()
        {
            HealerAIProfile profile = ScriptableObject.CreateInstance<HealerAIProfile>();
            CharacterController healer = TestHelpers.CreateCleric("Healer_MeleeMode", level: 6);

            try
            {
                healer.Stats.DEX = 12;
                healer.Stats.STR = 18;
                healer.Stats.ArmorBonus = 8;
                healer.Stats.ShieldBonus = 2;

                CombatStyle mode = profile.DetermineCombatMode(healer);
                Assert(mode == CombatStyle.Melee,
                    "Healer chooses melee with high AC and stronger melee bonus",
                    $"(mode={mode}, AC={healer.Stats.GetArmorClass()}, melee={healer.Stats.GetMeleeAttackBonus()}, ranged={healer.Stats.GetRangedAttackBonus()})");
            }
            finally
            {
                TestHelpers.Cleanup(healer != null ? healer.gameObject : null, profile);
            }
        }

        private static void TestHealerChoosesRangedWithLowAC()
        {
            HealerAIProfile profile = ScriptableObject.CreateInstance<HealerAIProfile>();
            CharacterController healer = TestHelpers.CreateCleric("Healer_RangedMode", level: 6);

            try
            {
                healer.Stats.STR = 18;
                healer.Stats.DEX = 12;
                healer.Stats.ArmorBonus = 1;
                healer.Stats.ShieldBonus = 0;

                CombatStyle mode = profile.DetermineCombatMode(healer);
                Assert(mode == CombatStyle.Ranged,
                    "Healer chooses ranged when AC is low",
                    $"(mode={mode}, AC={healer.Stats.GetArmorClass()})");
            }
            finally
            {
                TestHelpers.Cleanup(healer != null ? healer.gameObject : null, profile);
            }
        }

        // ── Batch 11 step 22: the AI's caster choices, its retry walk and its side tests ──

        /// <summary>
        /// SPL-112: the AI's energy for Resist Energy and Protection from Energy is the energy of the attacks of the enemies
        /// it can see (a breath weapon, a natural attack's energy dice), else the energy the recipient last took, else fire;
        /// an ally's breath does not count. Fire Shield is chill against fire and warm against cold (PHB p.230).
        /// </summary>
        private static void TestAiEnergyChoiceReadsVisibleEnemyAttacks()
        {
            CharacterController caster = TestHelpers.CreateCleric("Energy_Caster", level: 5);
            CharacterController enemy = TestHelpers.CreateWarrior("Energy_Enemy", level: 5);
            CharacterController ally = TestHelpers.CreateWarrior("Energy_Ally", level: 5);
            try
            {
                caster.IsPlayerControlled = true;
                ally.IsPlayerControlled = true;
                TestHelpers.SetGridPosition(caster, 0, 0);
                TestHelpers.SetGridPosition(ally, 1, 0);
                TestHelpers.SetGridPosition(enemy, 5, 0);
                var all = new List<CharacterController> { caster, ally, enemy };

                Assert(AiCastChoiceHeuristics.ChooseProtectionEnergy(caster, caster, all) == ResistEnergyType.Fire,
                    "Energy choice: no energy attack seen and none taken gives fire (the old default)");

                ally.ConfigureBreathWeapon(new BreathWeaponDefinition { DamageType = global::DamageType.Cold, DamageCount = 6, DamageDice = 6 });
                Assert(AiCastChoiceHeuristics.ChooseProtectionEnergy(caster, caster, all) == ResistEnergyType.Fire,
                    "Energy choice: an ally's cold breath is no threat");

                enemy.ConfigureBreathWeapon(new BreathWeaponDefinition { DamageType = global::DamageType.Cold, DamageCount = 2, DamageDice = 6 });
                Assert(AiCastChoiceHeuristics.ChooseProtectionEnergy(caster, caster, all) == ResistEnergyType.Cold,
                    "Energy choice: a visible enemy's cold breath gives cold (white dragon)");
                Assert(AiCastChoiceHeuristics.ChooseFireShieldWarm(caster, all),
                    "Fire Shield: warm against a cold threat (PHB p.230)");

                enemy.ConfigureBreathWeapon(new BreathWeaponDefinition { DamageType = global::DamageType.Fire, DamageCount = 2, DamageDice = 10 });
                Assert(AiCastChoiceHeuristics.ChooseProtectionEnergy(caster, caster, all) == ResistEnergyType.Fire,
                    "Energy choice: a visible enemy's fire breath gives fire (red dragon)");
                Assert(!AiCastChoiceHeuristics.ChooseFireShieldWarm(caster, all),
                    "Fire Shield: chill against a fire threat (PHB p.230)");

                enemy.ConfigureBreathWeapon(null);
                enemy.Stats.NaturalAttacks.Add(new NaturalAttackDefinition
                {
                    Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1,
                    BonusElementalDamageType = global::DamageType.Acid, BonusElementalDamageCount = 1, BonusElementalDamageDice = 6
                });
                Assert(AiCastChoiceHeuristics.ChooseProtectionEnergy(caster, caster, all) == ResistEnergyType.Acid,
                    "Energy choice: a natural attack's acid dice give acid");

                enemy.Stats.NaturalAttacks.Clear();
                caster.Stats.ApplyIncomingDamage(4, new DamagePacket { RawDamage = 4, Types = new HashSet<global::DamageType> { global::DamageType.Sonic }, SourceName = "test" });
                Assert(caster.Stats.LastEnergyDamageTaken == global::DamageType.Sonic,
                    "Damage taken records its energy type (CharacterStats.LastEnergyDamageTaken)", "" + caster.Stats.LastEnergyDamageTaken);
                Assert(AiCastChoiceHeuristics.ChooseProtectionEnergy(caster, caster, all) == ResistEnergyType.Sonic,
                    "Energy choice: with no visible energy attack, the energy the recipient last took");

                enemy.Stats.CurrentHP = -20;
                enemy.ConfigureBreathWeapon(new BreathWeaponDefinition { DamageType = global::DamageType.Electricity, DamageCount = 2, DamageDice = 6 });
                Assert(AiCastChoiceHeuristics.ChooseProtectionEnergy(caster, caster, all) == ResistEnergyType.Sonic,
                    "Energy choice: a dead enemy's breath does not count");
            }
            finally
            {
                TestHelpers.Cleanup(caster != null ? caster.gameObject : null, enemy != null ? enemy.gameObject : null, ally != null ? ally.gameObject : null);
            }
        }

        /// <summary>
        /// SPL-113: the AI enchants the recipient's wielded primary weapon (Keen Edge only a piercing or slashing one,
        /// PHB p.246), and the decider refuses the cast (nothing spent) when there is none; Disguise Self keeps the
        /// caster's race; an energy choice already on the request is kept.
        /// </summary>
        private static void TestAiWeaponAndDeciderChoices()
        {
            CharacterController caster = TestHelpers.CreateCleric("Weapon_Caster", level: 5);
            CharacterController fighter = TestHelpers.CreateWarrior("Weapon_Fighter", level: 5);
            try
            {
                caster.IsPlayerControlled = true;
                fighter.IsPlayerControlled = true;
                global::Inventory inv = fighter.GetComponent<InventoryComponent>().CharacterInventory;
                inv.RightHandSlot = null;
                SpellData magicWeapon = SpellDatabase.GetSpell(SpellNames.MAGIC_WEAPON);
                SpellData keenEdge = SpellDatabase.GetSpell(SpellNames.KEEN_EDGE);

                Assert(AiCastChoiceHeuristics.ChooseWeapon(fighter, magicWeapon) == null, "Weapon choice: no wielded weapon, no pick");
                SpellCastRequest none = SpellCastRequest.ForAI(caster, fighter, magicWeapon);
                Assert(AiCastDecider.Instance.FillAiChoices(none, null) == CastChoiceFill.Refused,
                    "AiCastDecider: Magic Weapon on a creature with no wielded weapon is refused before anything is spent");

                ItemData sword = ItemDatabase.CloneItem(ItemID.WeaponLongsword);
                inv.DirectEquip(sword, EquipSlot.RightHand);
                ItemData wielded = fighter.GetEquippedMainWeapon();
                Assert(wielded != null && AiCastChoiceHeuristics.ChooseWeapon(fighter, magicWeapon) == wielded,
                    "Weapon choice: Magic Weapon takes the wielded longsword");
                Assert(AiCastChoiceHeuristics.ChooseWeapon(fighter, keenEdge) == wielded,
                    "Weapon choice: Keen Edge takes a slashing weapon");
                SpellCastRequest mw = SpellCastRequest.ForAI(caster, fighter, magicWeapon);
                Assert(AiCastDecider.Instance.FillAiChoices(mw, null) == CastChoiceFill.Filled && mw.Choices.MagicWeaponItem == wielded,
                    "AiCastDecider: Magic Weapon's request carries the recipient's wielded weapon (SPL-113)");

                wielded.AddOrReplaceItemSpellEffect(new ItemSpellEffect(SpellNames.MAGIC_WEAPON, "Magic Weapon", "test", 5, 50));
                Assert(AiCastChoiceHeuristics.ChooseWeapon(fighter, magicWeapon) == null,
                    "Weapon choice: a weapon Magic Weapon already enchants is no pick");

                inv.RightHandSlot = null;
                ItemData hammer = ItemDatabase.CloneItem(ItemID.WeaponWarhammer);
                inv.DirectEquip(hammer, EquipSlot.RightHand);
                Assert(fighter.GetEquippedMainWeapon() != null && AiCastChoiceHeuristics.ChooseWeapon(fighter, keenEdge) == null,
                    "Weapon choice: Keen Edge takes no bludgeoning weapon (PHB p.246)");
                Assert(AiCastChoiceHeuristics.ChooseWeapon(fighter, magicWeapon) == fighter.GetEquippedMainWeapon(),
                    "Weapon choice: Magic Weapon takes a bludgeoning weapon");

                SpellCastRequest disguise = SpellCastRequest.ForAI(caster, caster, SpellDatabase.GetSpell(SpellNames.DISGUISE_SELF));
                Assert(AiCastDecider.Instance.FillAiChoices(disguise, null) == CastChoiceFill.Filled && disguise.Choices.DisguiseRace == caster.ActualRace,
                    "AiCastDecider: Disguise Self keeps the caster's own race", disguise.Choices.DisguiseRace);

                SpellCastRequest kept = SpellCastRequest.ForAI(caster, caster, SpellDatabase.GetSpell(SpellNames.RESIST_ENERGY));
                kept.Choices.Energy = ResistEnergyType.Electricity;
                Assert(AiCastDecider.Instance.FillAiChoices(kept, null) == CastChoiceFill.Filled && kept.Choices.Energy == ResistEnergyType.Electricity,
                    "AiCastDecider keeps an energy the request already carries");
            }
            finally
            {
                TestHelpers.Cleanup(caster != null ? caster.gameObject : null, fighter != null ? fighter.gameObject : null);
            }
        }

        /// <summary>
        /// AI-063 (single-target part): the walk tries the ranked candidates in order, moves on after a refusal, stops at the
        /// first cast and gives up after five tries.
        /// </summary>
        private static void TestAiSpellCandidatesTryInOrder()
        {
            var order = new List<int>();
            bool cast = AiSpellCandidates.TryInOrder(new[] { 1, 2, 3 }, AiSpellCandidates.MaxTries, c => { order.Add(c); return c == 2; }, out int tries);
            Assert(cast && tries == 2 && string.Join(",", order) == "1,2",
                "Retry walk: a refused first pick passes to the second, which is cast", string.Join(",", order));

            order.Clear();
            cast = AiSpellCandidates.TryInOrder(new[] { 1, 2, 3, 4, 5, 6, 7 }, AiSpellCandidates.MaxTries, c => { order.Add(c); return false; }, out tries);
            Assert(!cast && tries == 5 && AiSpellCandidates.MaxTries == 5 && string.Join(",", order) == "1,2,3,4,5",
                "Retry walk: at most five tries", string.Join(",", order));

            cast = AiSpellCandidates.TryInOrder(new int[0], AiSpellCandidates.MaxTries, c => true, out tries);
            Assert(!cast && tries == 0, "Retry walk: no candidate, no try");
        }

        /// <summary>
        /// The AI's weapon choice skips a cast that adds nothing (step 22 review): Magic Weapon (+1) or Greater Magic Weapon
        /// at CL 5 (+1) on a +1 weapon (enhancement bonuses do not stack, PHB p.171), Magic Weapon under a running Greater
        /// Magic Weapon +2, Keen Edge on a keen weapon (PHB p.246: threat-range effects do not stack); Greater Magic Weapon
        /// at CL 8 (+2) still improves a +1 weapon.
        /// </summary>
        private static void TestAiWeaponChoiceSkipsCastsThatAddNothing()
        {
            CharacterController caster = TestHelpers.CreateCleric("WeaponNothing_Caster", level: 5);
            CharacterController highCaster = TestHelpers.CreateCleric("WeaponNothing_HighCaster", level: 8);
            CharacterController fighter = TestHelpers.CreateWarrior("WeaponNothing_Fighter", level: 5);
            try
            {
                global::Inventory inv = fighter.GetComponent<InventoryComponent>().CharacterInventory;
                inv.RightHandSlot = null;
                ItemData sword = ItemDatabase.CloneItem(ItemID.WeaponLongsword);
                inv.DirectEquip(sword, EquipSlot.RightHand);
                ItemData wielded = fighter.GetEquippedMainWeapon();
                SpellData magicWeapon = SpellDatabase.GetSpell(SpellNames.MAGIC_WEAPON);
                SpellData greater = SpellDatabase.GetSpell(SpellNames.GREATER_MAGIC_WEAPON);
                SpellData keenEdge = SpellDatabase.GetSpell(SpellNames.KEEN_EDGE);

                Assert(wielded != null && AiCastChoiceHeuristics.ChooseWeapon(fighter, greater, caster) == wielded,
                    "Weapon choice: Greater Magic Weapon takes a +0 weapon");

                wielded.EnhancementBonus = 1;
                Assert(AiCastChoiceHeuristics.ChooseWeapon(fighter, magicWeapon, caster) == null,
                    "Weapon choice: Magic Weapon adds nothing to a +1 weapon (PHB p.171), no pick");
                Assert(AiCastChoiceHeuristics.ChooseWeapon(fighter, greater, caster) == null,
                    "Weapon choice: Greater Magic Weapon at CL 5 (+1) adds nothing to a +1 weapon, no pick");
                Assert(AiCastChoiceHeuristics.ChooseWeapon(fighter, greater, highCaster) == wielded,
                    "Weapon choice: Greater Magic Weapon at CL 8 (+2) improves a +1 weapon");
                SpellCastRequest refused = SpellCastRequest.ForAI(caster, fighter, magicWeapon);
                Assert(AiCastDecider.Instance.FillAiChoices(refused, null) == CastChoiceFill.Refused,
                    "AiCastDecider: Magic Weapon on a +1 weapon is refused before anything is spent");

                wielded.EnhancementBonus = 0;
                wielded.AddOrReplaceItemSpellEffect(new ItemSpellEffect(SpellNames.GREATER_MAGIC_WEAPON, "Greater Magic Weapon", "test", 8, 80)
                {
                    BonusType = BonusType.Enhancement, EnhancementBonusAttack = 2, EnhancementBonusDamage = 2, CountsAsMagicForBypass = true
                });
                Assert(AiCastChoiceHeuristics.ChooseWeapon(fighter, magicWeapon, caster) == null,
                    "Weapon choice: Magic Weapon adds nothing under a running Greater Magic Weapon +2, no pick");

                if (wielded.Enchantment == null)
                    wielded.Enchantment = new ItemEnchantmentData();
                Assert(AiCastChoiceHeuristics.ChooseWeapon(fighter, keenEdge, caster) == wielded,
                    "Weapon choice: Keen Edge takes a slashing weapon that is not keen");
                wielded.Enchantment.AddAbility(EnchantmentType.Keen);
                Assert(AiCastChoiceHeuristics.ChooseWeapon(fighter, keenEdge, caster) == null,
                    "Weapon choice: Keen Edge adds nothing to a keen weapon (PHB p.246), no pick");
            }
            finally
            {
                TestHelpers.Cleanup(caster != null ? caster.gameObject : null, highCaster != null ? highCaster.gameObject : null,
                    fighter != null ? fighter.gameObject : null);
            }
        }

        /// <summary>
        /// PHB p.175 (the caster must see or touch the target): the AI never aims Charm Person, one of the spells
        /// GameManager.IsValidTargetForSpell returns for before its sight test (SPL-185), at an invisible enemy, with or
        /// without a GameManager (the scene's in the Play pass); a visible one is chosen, and See Invisibility reveals the
        /// invisible one, as in the cast core's test.
        /// </summary>
        private static void TestAiEnemySpellTargetNeedsSight()
        {
            CharacterController caster = TestHelpers.CreateCharacter("Sight_Caster", "Wizard", level: 5, intelligence: 18);
            CharacterController enemy = TestHelpers.CreateWarrior("Sight_Enemy", level: 3);
            try
            {
                caster.IsPlayerControlled = true;
                TestHelpers.SetGridPosition(caster, 0, 0);
                TestHelpers.SetGridPosition(enemy, 3, 0);
                TestHelpers.ResetActions(caster);
                // TestHelpers builds no spellcasting; the NPC caster tests of CheckLegality need one with a slot.
                SpellcastingComponent sc = caster.GetComponent<SpellcastingComponent>();
                if (sc == null)
                {
                    sc = caster.gameObject.AddComponent<SpellcastingComponent>();
                    sc.Init(caster.Stats);
                }
                SpellData charm = SpellDatabase.GetSpell(SpellNames.CHARM_PERSON).Clone();
                if (sc != null && sc.SpellSlots != null)
                {
                    var slot = new SpellSlot(1, false, "Wizard");
                    slot.PrepareWithMetamagic(charm, null);
                    sc.SpellSlots.Add(slot);
                    sc.SyncPreparedSpellsFromSlots();
                }
                var all = new List<CharacterController> { caster, enemy };
                GameManager gm = GameManager.Instance;
                string withGm = gm != null ? " (GameManager present)" : "";

                CharacterController visible = AISpellcastingStrategist.SelectBestSpellTarget(caster, charm, null, all, gm);
                Assert(visible == enemy, "Spell sight: a visible humanoid enemy is a Charm Person target" + withGm,
                    (visible != null && visible.Stats != null ? visible.Stats.CharacterName : "none")
                    + (gm != null ? " legality=" + gm.CheckLegalityForAI(caster, enemy, charm).Reason : ""));

                // Invisibility as the spell gives it: the tracked effect's 50% miss chance (what CanSee reads) and the
                // invisibility state (InvisibilityRulesTests' setup).
                StatusEffectManager statusMgr = enemy.GetComponent<StatusEffectManager>();
                if (statusMgr == null)
                {
                    statusMgr = enemy.gameObject.AddComponent<StatusEffectManager>();
                    statusMgr.Init(enemy.Stats);
                }
                ActiveSpellEffect invisibility = statusMgr.AddEffect(SpellDatabase.GetSpell(SpellNames.INVISIBILITY), "Sight_Enemy", 5);
                enemy.ApplyInvisibilityEffect(invisibility != null ? invisibility.RemainingRounds : 10, enemy, isMoving: false);
                Assert(!caster.CanSee(enemy), "Spell sight setup: the invisible enemy has total concealment from the caster");
                CharacterController hidden = AISpellcastingStrategist.SelectBestSpellTarget(caster, charm, null, all, gm);
                Assert(hidden == null, "Spell sight: an invisible enemy is no Charm Person target (PHB p.175; SPL-185)" + withGm,
                    hidden != null && hidden.Stats != null ? hidden.Stats.CharacterName : "none");
                CharacterController fallback = AISpellcastingStrategist.SelectBestSpellTarget(caster, charm, enemy, all, gm);
                Assert(fallback == null, "Spell sight: the caller's fallback does not bring back an invisible enemy" + withGm,
                    fallback != null && fallback.Stats != null ? fallback.Stats.CharacterName : "none");

                caster.ApplySeeInvisibilityEffect(10, caster);
                CharacterController seen = AISpellcastingStrategist.SelectBestSpellTarget(caster, charm, null, all, gm);
                Assert(seen == enemy, "Spell sight: See Invisibility reveals the invisible enemy as a target" + withGm,
                    seen != null && seen.Stats != null ? seen.Stats.CharacterName : "none");
            }
            finally
            {
                TestHelpers.Cleanup(caster != null ? caster.gameObject : null, enemy != null ? enemy.gameObject : null);
            }
        }

        /// <summary>
        /// AI-016 (step 22 review): an offensive spell with no enemy in reach never falls back to the caller's target when
        /// that is an ally (the Healer turn passes its wounded ally, and the cast walk goes on to offensive spells).
        /// </summary>
        private static void TestAiOffensiveSpellNeverFallsBackToAlly()
        {
            CharacterController caster = TestHelpers.CreateCleric("Fallback_Caster", level: 5);
            CharacterController ally = TestHelpers.CreateWarrior("Fallback_Ally", level: 5);
            CharacterController enemy = TestHelpers.CreateWarrior("Fallback_Enemy", level: 5);
            try
            {
                caster.IsPlayerControlled = true;
                ally.IsPlayerControlled = true;
                TestHelpers.SetGridPosition(caster, 0, 0);
                TestHelpers.SetGridPosition(ally, 1, 0);
                TestHelpers.SetGridPosition(enemy, 40, 0);
                var all = new List<CharacterController> { caster, ally, enemy };
                SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);

                CharacterController target = AISpellcastingStrategist.SelectBestSpellTarget(caster, missile, ally, all, null);
                Assert(target == null, "Fallback: an offensive spell with no enemy in range does not fall back to an ally (AI-016)",
                    target != null && target.Stats != null ? target.Stats.CharacterName : "none");
                target = AISpellcastingStrategist.SelectBestSpellTarget(caster, missile, enemy, all, null);
                Assert(target == enemy, "Fallback: without a GameManager an enemy fallback is still returned when none is found in range",
                    target != null && target.Stats != null ? target.Stats.CharacterName : "none");
            }
            finally
            {
                TestHelpers.Cleanup(caster != null ? caster.gameObject : null, ally != null ? ally.gameObject : null, enemy != null ? enemy.gameObject : null);
            }
        }

        /// <summary>
        /// AI-016: the AI's spell targeting tells allies and enemies apart by TeamUtility, so a Neutral creature is neither
        /// an enemy target of an offensive spell nor an ally target of a buff.
        /// </summary>
        private static void TestAiSpellTargetingUsesTeamUtility()
        {
            CharacterController caster = TestHelpers.CreateCleric("Team_Caster", level: 5);
            CharacterController neutral = TestHelpers.CreateWarrior("Team_Neutral", level: 5);
            try
            {
                caster.IsPlayerControlled = true;
                neutral.SetTeam(CharacterTeam.Neutral);
                TestHelpers.SetGridPosition(caster, 0, 0);
                TestHelpers.SetGridPosition(neutral, 1, 0);
                var all = new List<CharacterController> { caster, neutral };

                CharacterController missileTarget = AISpellcastingStrategist.SelectBestSpellTarget(caster, SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE), null, all, null);
                Assert(missileTarget == null, "A Neutral creature is no enemy target (TeamUtility.IsEnemy, AI-016)", missileTarget?.Stats?.CharacterName);

                neutral.Stats.CurrentHP = 1;
                CharacterController cureTarget = AISpellcastingStrategist.SelectBestSpellTarget(caster, SpellDatabase.GetSpell(SpellNames.CURE_LIGHT_WOUNDS), null, all, null);
                Assert(cureTarget != neutral, "A hurt Neutral creature is no ally target (TeamUtility.IsAlly, AI-016)", cureTarget?.Stats?.CharacterName);
            }
            finally
            {
                TestHelpers.Cleanup(caster != null ? caster.gameObject : null, neutral != null ? neutral.gameObject : null);
            }
        }

        /// <summary>
        /// SPL-005: a cure harms an undead (PHB p.215), so a wounded undead ally does not make a healer heal, and a
        /// cure finds no target (not a wasted self-cure at full HP) when nobody it can cure is hurt.
        /// </summary>
        private static void TestHealerIgnoresWoundedUndeadAlly()
        {
            HealerAIProfile profile = ScriptableObject.CreateInstance<HealerAIProfile>();
            CharacterController healer = TestHelpers.CreateCleric("Healer_UndeadEscort", level: 5);
            CharacterController undeadAlly = TestHelpers.CreateWarrior("Ally_Zombie", level: 5);

            try
            {
                healer.IsPlayerControlled = true;
                undeadAlly.IsPlayerControlled = true;
                undeadAlly.Stats.CreatureType = "Undead";
                undeadAlly.Stats.CurrentHP = Mathf.CeilToInt(undeadAlly.Stats.TotalMaxHP * 0.15f);
                TestHelpers.SetGridPosition(healer, 0, 0);
                TestHelpers.SetGridPosition(undeadAlly, 1, 0);

                var all = new List<CharacterController> { healer, undeadAlly };
                HealerActionType action = profile.DetermineActionPriority(healer, all, hasCastableSpells: true);
                Assert(action != HealerActionType.Healing && action != HealerActionType.CriticalHealing,
                    "Healer does not choose healing for a wounded undead ally",
                    $"(action={action})");

                SpellData cure = SpellDatabase.GetSpell(SpellNames.CURE_LIGHT_WOUNDS);
                CharacterController target = AISpellcastingStrategist.SelectBestSpellTarget(healer, cure, null, all, null);
                Assert(target == null,
                    "A cure finds no target when only an undead ally is hurt and the caster is unhurt",
                    $"(selected={target?.Stats?.CharacterName ?? "null"})");

                healer.Stats.CurrentHP = healer.Stats.TotalMaxHP - 3;
                target = AISpellcastingStrategist.SelectBestSpellTarget(healer, cure, null, all, null);
                Assert(target == healer,
                    "A wounded caster still cures itself",
                    $"(selected={target?.Stats?.CharacterName ?? "null"})");
            }
            finally
            {
                TestHelpers.Cleanup(healer != null ? healer.gameObject : null,
                    undeadAlly != null ? undeadAlly.gameObject : null,
                    profile);
            }
        }

        private static void TestHealerPrioritizesMostWoundedAlly()
        {
            HealerAIProfile profile = ScriptableObject.CreateInstance<HealerAIProfile>();
            CharacterController healer = TestHelpers.CreateCleric("Healer_Targeting", level: 5);
            CharacterController allyA = TestHelpers.CreateWarrior("Ally_A", level: 5);
            CharacterController allyB = TestHelpers.CreateWarrior("Ally_B", level: 5);
            CharacterController allyC = TestHelpers.CreateWarrior("Ally_C", level: 5);

            try
            {
                healer.IsPlayerControlled = true;
                allyA.IsPlayerControlled = true;
                allyB.IsPlayerControlled = true;
                allyC.IsPlayerControlled = true;

                allyA.Stats.CurrentHP = Mathf.CeilToInt(allyA.Stats.TotalMaxHP * 0.62f);
                allyB.Stats.CurrentHP = Mathf.CeilToInt(allyB.Stats.TotalMaxHP * 0.20f);
                allyC.Stats.CurrentHP = Mathf.CeilToInt(allyC.Stats.TotalMaxHP * 0.68f);

                var all = new List<CharacterController> { healer, allyA, allyB, allyC };
                CharacterController target = profile.GetPriorityHealTarget(healer, all);

                Assert(target == allyB,
                    "Healer prioritizes most wounded ally",
                    $"(selected={target?.Stats?.CharacterName ?? "null"})");
            }
            finally
            {
                TestHelpers.Cleanup(healer != null ? healer.gameObject : null,
                    allyA != null ? allyA.gameObject : null,
                    allyB != null ? allyB.gameObject : null,
                    allyC != null ? allyC.gameObject : null,
                    profile);
            }
        }

        private static void TestRangedRiskMultiplierDefaults()
        {
            RangedAIProfile rangedProfile = ScriptableObject.CreateInstance<RangedAIProfile>();
            SpellcasterAIProfile spellcasterProfile = ScriptableObject.CreateInstance<SpellcasterAIProfile>();

            try
            {
                float ranged = rangedProfile.GetRangedAoORiskToleranceMultiplier();
                float caster = spellcasterProfile.GetRangedAoORiskToleranceMultiplier();

                Assert(caster < ranged,
                    "Spellcaster profile is more conservative than ranged profile for AoO risk",
                    $"(ranged={ranged:F2}, caster={caster:F2})");
            }
            finally
            {
                TestHelpers.Cleanup(rangedProfile, spellcasterProfile);
            }
        }

        private static void TestConcealmentTargetingAdjustmentUsesPriorityTiers()
        {
            float visible = AIService.GetConcealmentTargetingAdjustment(0, canSeeTarget: true, hasLastKnownPosition: true);
            float partial = AIService.GetConcealmentTargetingAdjustment(20, canSeeTarget: true, hasLastKnownPosition: true);
            float totalVisible = AIService.GetConcealmentTargetingAdjustment(50, canSeeTarget: true, hasLastKnownPosition: true);
            float unknown = AIService.GetConcealmentTargetingAdjustment(50, canSeeTarget: false, hasLastKnownPosition: false);

            bool ordered = visible > partial && partial > totalVisible && totalVisible > unknown;
            Assert(ordered,
                "Concealment targeting adjustment follows visible > partial > total > unknown order",
                $"(visible={visible:F1}, partial={partial:F1}, totalVisible={totalVisible:F1}, unknown={unknown:F1})");
        }

        private static void TestConcealmentTargetingAdjustmentSupportsProfileMultiplier()
        {
            float baseline = AIService.GetConcealmentTargetingAdjustment(50, canSeeTarget: false, hasLastKnownPosition: true, penaltyMultiplier: 1f);
            float reduced = AIService.GetConcealmentTargetingAdjustment(50, canSeeTarget: false, hasLastKnownPosition: true, penaltyMultiplier: 0.5f);
            float disabled = AIService.GetConcealmentTargetingAdjustment(50, canSeeTarget: false, hasLastKnownPosition: true, penaltyMultiplier: 0f);

            bool scalingWorks = Mathf.Abs(reduced) < Mathf.Abs(baseline) && Mathf.Approximately(disabled, 0f);
            Assert(scalingWorks,
                "Concealment targeting multiplier scales penalties and supports disabling",
                $"(baseline={baseline:F1}, reduced={reduced:F1}, disabled={disabled:F1})");
        }

        private static void TestThreatSystemEstimatesRangedProvocationDamage()
        {
            CharacterController archer = TestHelpers.CreateRogue("AI_Archer", level: 4);
            CharacterController fighter = TestHelpers.CreateWarrior("Enemy_Fighter", level: 4);

            try
            {
                archer.IsPlayerControlled = false;
                fighter.IsPlayerControlled = true;

                TestHelpers.SetGridPosition(archer, 0, 0);
                TestHelpers.SetGridPosition(fighter, 1, 0);

                fighter.Stats.MaxAttacksOfOpportunity = 1;
                fighter.Stats.AttacksOfOpportunityUsed = 0;

                List<CharacterController> all = new List<CharacterController> { archer, fighter };
                List<CharacterController> threats = ThreatSystem.GetThreateningEnemies(archer.GridPosition, archer, all);
                float expectedDamage = ThreatSystem.CalculateExpectedAoODamageForRangedAttack(archer, threats);

                Assert(expectedDamage > 0f,
                    "Threat system estimates positive expected AoO damage for threatened ranged attacker",
                    $"(expectedDamage={expectedDamage:F2}, threats={threats.Count})");
            }
            finally
            {
                TestHelpers.Cleanup(archer != null ? archer.gameObject : null,
                    fighter != null ? fighter.gameObject : null);
            }
        }

        private static void TestThreatSystemCountsOffHandMeleeThreateners()
        {
            CharacterController archer = TestHelpers.CreateRogue("AI_Archer_OffhandThreat", level: 4);
            CharacterController offHandThreatener = TestHelpers.CreateWarrior("Enemy_OffhandThreat", level: 4);
            CharacterController adjacentThreatener = TestHelpers.CreateWarrior("Enemy_AdjacentThreat", level: 4);

            try
            {
                archer.IsPlayerControlled = false;
                offHandThreatener.IsPlayerControlled = true;
                adjacentThreatener.IsPlayerControlled = true;

                global::Inventory offHandInventory = offHandThreatener.GetComponent<InventoryComponent>().CharacterInventory;
                offHandInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHORTBOW), EquipSlot.RightHand);
                offHandInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.DAGGER), EquipSlot.LeftHand);

                TestHelpers.SetGridPosition(archer, 0, 0);
                TestHelpers.SetGridPosition(offHandThreatener, 1, 0);
                TestHelpers.SetGridPosition(adjacentThreatener, -1, 0);

                List<CharacterController> all = new List<CharacterController> { archer, offHandThreatener, adjacentThreatener };
                List<CharacterController> threats = ThreatSystem.GetThreateningEnemies(archer.GridPosition, archer, all);

                bool includesOffHandThreatener = threats.Contains(offHandThreatener);
                bool includesAdjacentThreatener = threats.Contains(adjacentThreatener);

                Assert(includesOffHandThreatener && includesAdjacentThreatener && threats.Count == 2,
                    "Threat system includes off-hand melee threatener when main hand is ranged",
                    $"(threatCount={threats.Count}, includesOffHand={includesOffHandThreatener}, includesAdjacent={includesAdjacentThreatener})");
            }
            finally
            {
                TestHelpers.Cleanup(archer != null ? archer.gameObject : null,
                    offHandThreatener != null ? offHandThreatener.gameObject : null,
                    adjacentThreatener != null ? adjacentThreatener.gameObject : null);
            }
        }

        private static void TestSwarmAISticksToCurrentTargetUntilDead()
        {
            CharacterController swarm = TestHelpers.CreateWarrior("Swarm_AI", level: 3);
            CharacterController nearEnemy = TestHelpers.CreateWarrior("Enemy_Near", level: 3);
            CharacterController fartherEnemy = TestHelpers.CreateWarrior("Enemy_Far", level: 3);
            SwarmAI profile = ScriptableObject.CreateInstance<SwarmAI>();

            try
            {
                TestHelpers.SetGridPosition(swarm, 0, 0);
                TestHelpers.SetGridPosition(nearEnemy, 1, 0);
                TestHelpers.SetGridPosition(fartherEnemy, 4, 0);

                List<CharacterController> enemies = new List<CharacterController> { nearEnemy, fartherEnemy };
                CharacterController first = profile.ResolveTarget(swarm, enemies);
                Assert(first == nearEnemy,
                    "Swarm AI initially selects nearest enemy",
                    $"(selected={first?.Stats?.CharacterName ?? "null"})");

                TestHelpers.SetGridPosition(fartherEnemy, 0, 1);
                CharacterController second = profile.ResolveTarget(swarm, enemies);
                Assert(second == nearEnemy,
                    "Swarm AI keeps current target while it is alive",
                    $"(selected={second?.Stats?.CharacterName ?? "null"})");

                nearEnemy.Stats.CurrentHP = -10;
                CharacterController third = profile.ResolveTarget(swarm, enemies);
                Assert(third == fartherEnemy,
                    "Swarm AI switches only after current target dies",
                    $"(selected={third?.Stats?.CharacterName ?? "null"})");
            }
            finally
            {
                TestHelpers.Cleanup(swarm != null ? swarm.gameObject : null,
                    nearEnemy != null ? nearEnemy.gameObject : null,
                    fartherEnemy != null ? fartherEnemy.gameObject : null,
                    profile);
            }
        }

        private static void TestIndiscriminateSwarmAITargetsNearestCreature()
        {
            CharacterController swarm = TestHelpers.CreateWarrior("Summoned_Swarm", level: 3);
            CharacterController ally = TestHelpers.CreateWarrior("Ally_Close", level: 3);
            CharacterController enemy = TestHelpers.CreateWarrior("Enemy_Far", level: 3);
            IndiscriminateSwarmAI profile = ScriptableObject.CreateInstance<IndiscriminateSwarmAI>();

            try
            {
                TestHelpers.SetGridPosition(swarm, 0, 0);
                TestHelpers.SetGridPosition(ally, 1, 0);
                TestHelpers.SetGridPosition(enemy, 4, 0);

                List<CharacterController> everyone = new List<CharacterController> { ally, enemy };
                CharacterController selected = profile.ResolveTarget(swarm, everyone);

                Assert(selected == ally,
                    "Indiscriminate swarm AI can target nearest creature regardless of team",
                    $"(selected={selected?.Stats?.CharacterName ?? "null"})");
            }
            finally
            {
                TestHelpers.Cleanup(swarm != null ? swarm.gameObject : null,
                    ally != null ? ally.gameObject : null,
                    enemy != null ? enemy.gameObject : null,
                    profile);
            }
        }

        // ── Action economy (AI-034): UseMoveAction reports failure; AI moves gate on HasMoveAction ──

        private static void TestUseMoveActionReportsFailureOnceSpent()
        {
            var actions = new ActionEconomy();
            bool first = actions.UseMoveAction();
            bool second = actions.UseMoveAction();

            Assert(first && !second,
                "UseMoveAction succeeds once per turn and then reports failure",
                $"(first={first}, second={second})");
            Assert(actions.HasStandardAction && !actions.StandardConvertedToMove,
                "A failed UseMoveAction does not silently convert or spend the standard action",
                $"(hasStandard={actions.HasStandardAction}, converted={actions.StandardConvertedToMove})");
        }

        private static void TestUseMoveActionFailsAfterFullRoundAction()
        {
            var actions = new ActionEconomy();
            actions.UseFullRoundAction();
            bool used = actions.UseMoveAction();

            Assert(!used && !actions.MoveActionUsed,
                "UseMoveAction fails after a full-round action and leaves state unchanged",
                $"(used={used}, moveUsed={actions.MoveActionUsed})");
        }

        private static void TestUseMoveActionRespectsDisabledSingleAction()
        {
            var moveFirst = new ActionEconomy { SingleActionOnly = true };
            bool moved = moveFirst.UseMoveAction();
            Assert(moved && !moveFirst.HasStandardAction,
                "Disabled: a move action uses the turn's single action",
                $"(moved={moved}, hasStandard={moveFirst.HasStandardAction})");

            var standardFirst = new ActionEconomy { SingleActionOnly = true };
            standardFirst.UseStandardAction();
            bool movedAfterStandard = standardFirst.UseMoveAction();
            Assert(!movedAfterStandard,
                "Disabled: UseMoveAction fails after the single standard action",
                $"(movedAfterStandard={movedAfterStandard})");
        }

        private static void TestSecondMoveAfterSearchNeedsExplicitDoubleMove()
        {
            // Regression for AI-034: a search (or breath-positioning) move spends the move action;
            // the melee routine's approach is gated on HasMoveAction, so it must not move again
            // and the standard action stays available for the attack. A second move is only the
            // explicit double-move conversion, which then costs the standard action.
            var actions = new ActionEconomy();
            actions.UseMoveAction(); // search move

            Assert(!actions.HasMoveAction && actions.HasStandardAction,
                "After a search move the AI move gate is closed and the attack remains",
                $"(hasMove={actions.HasMoveAction}, hasStandard={actions.HasStandardAction})");

            bool secondMove = actions.UseMoveAction();
            Assert(!secondMove && actions.HasStandardAction,
                "A second UseMoveAction cannot buy a free move",
                $"(secondMove={secondMove}, hasStandard={actions.HasStandardAction})");

            Assert(actions.CanConvertStandardToMove,
                "Double move stays available as an explicit conversion",
                $"(canConvert={actions.CanConvertStandardToMove})");
            actions.ConvertStandardToMove();
            Assert(!actions.HasStandardAction && actions.AllMainActionsSpent,
                "Double move spends the standard action, leaving no attack",
                $"(hasStandard={actions.HasStandardAction}, allSpent={actions.AllMainActionsSpent})");
        }
    }
}
