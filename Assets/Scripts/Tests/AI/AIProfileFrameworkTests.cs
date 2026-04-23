using System.Collections.Generic;
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
            TestEvokerSchoolPriority();
            TestAbjurerPrefersSingleTarget();
            TestSpellcasterAOEAvoidsUnsafeFriendlyFire();
            TestHealerPrioritizesHealing();
            TestHealerUsesBuffingWhenAlliesHealthy();
            TestHealerChoosesCriticalHealing();
            TestHealerChoosesMeleeWithHighACAndMeleeAB();
            TestHealerChoosesRangedWithLowAC();
            TestHealerPrioritizesMostWoundedAlly();

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
    }
}
