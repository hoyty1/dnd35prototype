using DND35.AI;
using DND35.AI.Profiles;
using Tests.Utilities;
using UnityEngine;

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
                lightArmor.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem("leather_armor"), EquipSlot.Armor);
                heavyArmor.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem("full_plate"), EquipSlot.Armor);

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
                defender.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem("longsword"), EquipSlot.RightHand);

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

                Inventory inv = defender.GetComponent<InventoryComponent>().CharacterInventory;
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

                Inventory inv = defender.GetComponent<InventoryComponent>().CharacterInventory;
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
                disarmTarget.GetComponent<InventoryComponent>().CharacterInventory.DirectEquip(ItemDatabase.CloneItem("longsword"), EquipSlot.RightHand);

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
    }
}
