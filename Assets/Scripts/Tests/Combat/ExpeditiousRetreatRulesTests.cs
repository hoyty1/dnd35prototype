using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Expeditious Retreat mechanics.
/// </summary>
public static class ExpeditiousRetreatRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void expeditious_retreat_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== EXPEDITIOUS RETREAT RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestSpellDefinition();
        TestSpeedIncreaseAndMovementRange();
        TestJumpIntegrationFromSpeed();
        TestDurationTickAndExpiry();
        TestDismissalRemovesSpeedBonus();
        TestEnhancementStackingUsesHighestSpeedBonus();

        Debug.Log($"====== Expeditious Retreat Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController CreateController(string name, int level = 3, int baseSpeedSquares = 6)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: "Wizard",
            str: 12,
            dex: 12,
            con: 12,
            wis: 10,
            intelligence: 16,
            cha: 10,
            bab: Mathf.Max(1, level / 2),
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: baseSpeedSquares,
            atkRange: 1,
            baseHitDieHP: 18,
            raceName: "Human");

        stats.InitializeSkills("Wizard", level);

        GameObject go = new GameObject($"ExpRet_{name}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Init(stats, Vector2Int.zero, null, null);

        InventoryComponent inv = go.AddComponent<InventoryComponent>();
        inv.Init(stats);

        StatusEffectManager statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static void TestSpellDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell("expeditious_retreat");
        Assert(spell != null, "Spell definition exists");
        if (spell == null)
            return;

        Assert(spell.School == "Transmutation", "School is Transmutation");
        Assert(spell.TargetType == SpellTargetType.Self, "Target is self");
        Assert(spell.RangeCategory == SpellRangeCategory.Personal, "Range is personal");
        Assert(spell.DurationType == DurationType.Minutes && spell.DurationValue == 1 && spell.DurationScalesWithLevel,
            "Duration is 1 minute/level");
        Assert(spell.IsDismissible, "Spell is dismissible");
        Assert(spell.BuffSpeedBonusFeet == 30, "Speed bonus is +30 ft");
        Assert(spell.GetEffectiveBonusType() == BonusType.Enhancement, "Speed bonus type is enhancement");
        Assert(spell.ClassList != null
               && System.Array.Exists(spell.ClassList, c => c == "Wizard")
               && System.Array.Exists(spell.ClassList, c => c == "Sorcerer")
               && System.Array.Exists(spell.ClassList, c => c == "Bard"),
            "Class list includes Wizard/Sorcerer/Bard");
    }

    private static void TestSpeedIncreaseAndMovementRange()
    {
        CharacterController controller = null;
        try
        {
            controller = CreateController("SpeedTest", level: 3);
            StatusEffectManager statusMgr = controller.GetComponent<StatusEffectManager>();
            SpellData spell = SpellDatabase.GetSpell("expeditious_retreat");

            int baseFeet = controller.Stats.SpeedInFeet;
            int baseRange = controller.Stats.MoveRange;

            ActiveSpellEffect effect = statusMgr.AddEffect(spell, "Tester", 3);
            controller.ApplyExpeditiousRetreatEffect(effect != null ? effect.AppliedSpeedBonusFeet : 0, effect != null ? effect.RemainingRounds : 0, controller);

            Assert(controller.Stats.SpeedInFeet == baseFeet + 30,
                "Speed increases by +30 ft",
                $"base={baseFeet}, actual={controller.Stats.SpeedInFeet}");
            Assert(controller.Stats.MoveRange == baseRange + 6,
                "Movement range increases by +6 squares",
                $"base={baseRange}, actual={controller.Stats.MoveRange}");
        }
        finally
        {
            DestroyController(controller);
        }
    }

    private static void TestJumpIntegrationFromSpeed()
    {
        CharacterController controller = null;
        try
        {
            controller = CreateController("JumpTest", level: 3);
            StatusEffectManager statusMgr = controller.GetComponent<StatusEffectManager>();
            SpellData spell = SpellDatabase.GetSpell("expeditious_retreat");

            int baselineJump = controller.Stats.GetSkillBonus("Jump");
            int baselineJumpSpeedModifier = controller.Stats.JumpSpeedModifier;

            ActiveSpellEffect effect = statusMgr.AddEffect(spell, "Tester", 3);
            controller.ApplyExpeditiousRetreatEffect(effect != null ? effect.AppliedSpeedBonusFeet : 0, effect != null ? effect.RemainingRounds : 0, controller);

            int boostedJump = controller.Stats.GetSkillBonus("Jump");
            int boostedSpeedMod = controller.Stats.JumpSpeedModifier;

            Assert(boostedSpeedMod - baselineJumpSpeedModifier == 12,
                "Jump speed modifier increases by +12 from +30 ft",
                $"baseline={baselineJumpSpeedModifier}, boosted={boostedSpeedMod}");
            Assert(boostedJump - baselineJump == 12,
                "Jump skill bonus reflects speed-based increase",
                $"baseline={baselineJump}, boosted={boostedJump}");
            Assert(controller.Stats.JumpDcAdjustmentFromSpeed == boostedSpeedMod,
                "Jump DC adjustment tracks speed modifier");
        }
        finally
        {
            DestroyController(controller);
        }
    }

    private static void TestDurationTickAndExpiry()
    {
        CharacterController controller = null;
        try
        {
            controller = CreateController("DurationTest", level: 2);
            StatusEffectManager statusMgr = controller.GetComponent<StatusEffectManager>();
            SpellData spell = SpellDatabase.GetSpell("expeditious_retreat");

            int baseSpeed = controller.Stats.SpeedInFeet;
            ActiveSpellEffect effect = statusMgr.AddEffect(spell, "Tester", 2);
            controller.ApplyExpeditiousRetreatEffect(effect != null ? effect.AppliedSpeedBonusFeet : 0, effect != null ? effect.RemainingRounds : 0, controller);

            Assert(effect != null && effect.RemainingRounds == 20, "Duration starts at 10 rounds/level", $"rounds={(effect != null ? effect.RemainingRounds : 0)}");

            for (int i = 0; i < 19; i++)
                statusMgr.TickAllEffects();

            Assert(statusMgr.HasEffect("expeditious_retreat"), "Effect remains active before final round");
            Assert(controller.Stats.SpeedInFeet == baseSpeed + 30, "Speed bonus remains while active");

            statusMgr.TickAllEffects();
            controller.ClearExpeditiousRetreatEffect();

            Assert(!statusMgr.HasEffect("expeditious_retreat"), "Effect expires after full duration");
            Assert(controller.Stats.SpeedInFeet == baseSpeed, "Speed returns to baseline after expiry", $"expected={baseSpeed}, actual={controller.Stats.SpeedInFeet}");
        }
        finally
        {
            DestroyController(controller);
        }
    }

    private static void TestDismissalRemovesSpeedBonus()
    {
        CharacterController controller = null;
        try
        {
            controller = CreateController("DismissTest", level: 3);
            StatusEffectManager statusMgr = controller.GetComponent<StatusEffectManager>();
            SpellData spell = SpellDatabase.GetSpell("expeditious_retreat");

            int baseSpeed = controller.Stats.SpeedInFeet;
            ActiveSpellEffect effect = statusMgr.AddEffect(spell, "Tester", 3);
            controller.ApplyExpeditiousRetreatEffect(effect != null ? effect.AppliedSpeedBonusFeet : 0, effect != null ? effect.RemainingRounds : 0, controller);

            statusMgr.RemoveEffectsBySpellId("expeditious_retreat");
            ExpeditiousRetreatEffectData removed = controller.RemoveExpeditiousRetreatEffect();

            Assert(removed != null, "Character tracks removable Expeditious Retreat effect data");
            Assert(controller.Stats.SpeedInFeet == baseSpeed,
                "Dismissal removes speed bonus immediately",
                $"expected={baseSpeed}, actual={controller.Stats.SpeedInFeet}");
        }
        finally
        {
            DestroyController(controller);
        }
    }

    private static void TestEnhancementStackingUsesHighestSpeedBonus()
    {
        CharacterController controller = null;
        try
        {
            controller = CreateController("StackingTest", level: 3);
            StatusEffectManager statusMgr = controller.GetComponent<StatusEffectManager>();

            int baseSpeed = controller.Stats.SpeedInFeet;

            SpellData weakerSpeedEnhancement = new SpellData
            {
                SpellId = "test_speed_weaker",
                Name = "Test Speed Weaker",
                BuffSpeedBonusFeet = 10,
                BuffType = "enhancement",
                BuffBonusType = BonusType.Enhancement,
                BonusTypeExplicitlySet = true,
                DurationType = DurationType.Rounds,
                DurationValue = 5,
                DurationScalesWithLevel = false
            };

            SpellData strongerSpeedEnhancement = new SpellData
            {
                SpellId = "test_speed_stronger",
                Name = "Test Speed Stronger",
                BuffSpeedBonusFeet = 30,
                BuffType = "enhancement",
                BuffBonusType = BonusType.Enhancement,
                BonusTypeExplicitlySet = true,
                DurationType = DurationType.Rounds,
                DurationValue = 5,
                DurationScalesWithLevel = false
            };

            ActiveSpellEffect weaker = statusMgr.AddEffect(weakerSpeedEnhancement, "Tester", 1);
            Assert(weaker != null, "Weaker enhancement bonus applies first");
            Assert(controller.Stats.SpeedInFeet == baseSpeed + 10, "Initial weaker enhancement modifies speed");

            ActiveSpellEffect stronger = statusMgr.AddEffect(strongerSpeedEnhancement, "Tester", 1);
            Assert(stronger != null, "Stronger enhancement replaces weaker one");
            Assert(controller.Stats.SpeedInFeet == baseSpeed + 30,
                "Highest enhancement bonus applies",
                $"expected={baseSpeed + 30}, actual={controller.Stats.SpeedInFeet}");
            Assert(!statusMgr.HasEffect("test_speed_weaker"), "Weaker enhancement removed after stronger replacement");
        }
        finally
        {
            DestroyController(controller);
        }
    }
}
}
