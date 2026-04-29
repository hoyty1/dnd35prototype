using System.Reflection;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Ray of Enfeeblement mechanics.
/// Run with RayOfEnfeeblementRulesTests.RunAll().
/// </summary>
public static class RayOfEnfeeblementRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== RAY OF ENFEEBLEMENT RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestRayDefinitionMatchesCoreRules();
        TestRangedTouchAttackUsesTouchAcAndCanMiss();
        TestPenaltyFormulaScalingAndCapAcrossLevels();
        TestEnfeeblementStacksAndExpiresIndependently();
        TestStrengthFloorAndCombatStatImpact();

        Debug.Log($"====== Ray of Enfeeblement Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name, int level, int str = 12, int dex = 12, int bab = -999)
    {
        if (bab == -999)
            bab = Mathf.Max(0, level / 2);

        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: "Wizard",
            str: str,
            dex: dex,
            con: 12,
            wis: 12,
            intelligence: 16,
            cha: 12,
            bab: bab,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 24,
            raceName: "Human");

        stats.CreatureType = "Humanoid";
        stats.InitializeSkills("Wizard", level);
        return stats;
    }

    private static CharacterController CreateController(CharacterStats stats, CharacterTeam team, Vector2Int gridPos)
    {
        GameObject go = new GameObject($"RayRules_{stats.CharacterName}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Stats = stats;
        controller.SetTeam(team);
        controller.GridPosition = gridPos;

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

    private static void TestRayDefinitionMatchesCoreRules()
    {
        SpellData ray = SpellDatabase.GetSpell("ray_of_enfeeblement");
        Assert(ray != null, "Ray of Enfeeblement spell definition exists");
        if (ray == null)
            return;

        Assert(ray.School == "Necromancy", "Ray school is Necromancy");
        Assert(ray.SpellLevel == 1, "Ray is level 1");
        Assert(ray.TargetType == SpellTargetType.SingleEnemy, "Ray targets one enemy");
        Assert(ray.RangeCategory == SpellRangeCategory.Close, "Ray uses Close range");
        Assert(ray.IsRangedTouchSpell(), "Ray uses ranged touch attack");
        Assert(!ray.AllowsSavingThrow, "Ray allows no saving throw");
        Assert(ray.SpellResistanceApplies, "Ray is subject to spell resistance");
        Assert(ray.DurationType == DurationType.Minutes && ray.DurationValue == 1 && ray.DurationScalesWithLevel,
            "Ray duration is 1 minute per level");
    }

    private static void TestRangedTouchAttackUsesTouchAcAndCanMiss()
    {
        SpellData ray = SpellDatabase.GetSpell("ray_of_enfeeblement");
        CharacterStats caster = BuildStats("Caster", level: 5, dex: 14, bab: 5);

        CharacterStats lowTouchTarget = BuildStats("LowTouch", level: 3, dex: 8, bab: 2);
        lowTouchTarget.DeflectionBonus = 0;

        Random.InitState(1337);
        int predictedRoll = Random.Range(1, 21);
        Random.InitState(1337);
        SpellResult hitCheck = SpellCaster.Cast(ray, caster, lowTouchTarget, null, false, false, null, null);

        int expectedBonus = caster.BaseAttackBonus + caster.DEXMod + caster.SizeModifier;
        int expectedTouchAc = SpellcastingComponent.GetTouchAC(lowTouchTarget);
        bool expectedHit = predictedRoll == 20 || (predictedRoll != 1 && predictedRoll + expectedBonus >= expectedTouchAc);

        Assert(hitCheck.RequiredAttackRoll, "Ray requires an attack roll");
        Assert(hitCheck.IsRangedTouch, "Ray attack is ranged touch");
        Assert(hitCheck.TouchAC == expectedTouchAc, "Ray uses target touch AC", $"expected={expectedTouchAc}, actual={hitCheck.TouchAC}");
        Assert(hitCheck.AttackRoll == predictedRoll, "Ray attack roll matches deterministic random seed",
            $"expected={predictedRoll}, actual={hitCheck.AttackRoll}");
        Assert(hitCheck.AttackHit == expectedHit, "Ray hit/miss is resolved against touch AC");

        CharacterStats highTouchTarget = BuildStats("HighTouch", level: 3, dex: 30, bab: 2);
        highTouchTarget.DeflectionBonus = 8;
        SpellResult missCheck = SpellCaster.Cast(ray, caster, highTouchTarget, null, false, false, null, null);
        Assert(!missCheck.AttackHit || missCheck.AttackRoll == 20,
            "Very high touch AC causes miss unless natural 20",
            $"roll={missCheck.AttackRoll}, total={missCheck.AttackTotal}, touchAC={missCheck.TouchAC}");
    }

    private static void TestPenaltyFormulaScalingAndCapAcrossLevels()
    {
        MethodInfo method = typeof(GameManager).GetMethod("CalculateRayOfEnfeeblementPenalty", BindingFlags.NonPublic | BindingFlags.Static);
        Assert(method != null, "Can access Ray of Enfeeblement penalty calculator");
        if (method == null)
            return;

        ValidateLevelPenaltyBounds(method, 1, expectedMin: 2, expectedMax: 7);
        ValidateLevelPenaltyBounds(method, 3, expectedMin: 3, expectedMax: 8);
        ValidateLevelPenaltyBounds(method, 5, expectedMin: 4, expectedMax: 9);
        ValidateLevelPenaltyBounds(method, 7, expectedMin: 5, expectedMax: 10);
        ValidateLevelPenaltyBounds(method, 9, expectedMin: 6, expectedMax: 11);
        ValidateLevelPenaltyBounds(method, 10, expectedMin: 6, expectedMax: 11);
        ValidateLevelPenaltyBounds(method, 15, expectedMin: 6, expectedMax: 11);
    }

    private static void ValidateLevelPenaltyBounds(MethodInfo method, int casterLevel, int expectedMin, int expectedMax)
    {
        CharacterController caster = null;
        try
        {
            caster = CreateController(BuildStats($"L{casterLevel}Caster", level: casterLevel), CharacterTeam.Player, new Vector2Int(0, 0));
            int observed = (int)method.Invoke(null, new object[] { caster });
            Assert(observed >= expectedMin && observed <= expectedMax,
                $"Penalty bounds at caster level {casterLevel}",
                $"observed={observed}, expected=[{expectedMin}..{expectedMax}]");
        }
        finally
        {
            DestroyController(caster);
        }
    }

    private static void TestEnfeeblementStacksAndExpiresIndependently()
    {
        CharacterController target = null;
        CharacterController caster = null;
        try
        {
            target = CreateController(BuildStats("Target", level: 4, str: 14), CharacterTeam.Enemy, new Vector2Int(3, 3));
            caster = CreateController(BuildStats("Caster", level: 6), CharacterTeam.Player, new Vector2Int(1, 1));

            target.ApplyEnfeeblementEffect(strengthPenaltyAmount: 4, durationRemainingRounds: 2, caster: caster);
            target.ApplyEnfeeblementEffect(strengthPenaltyAmount: 3, durationRemainingRounds: 4, caster: caster);

            Assert(target.ActiveEnfeeblementEffects.Count == 2, "Multiple Ray effects are tracked separately");
            Assert(target.TotalEnfeeblementStrengthPenalty == 7, "Ray penalties stack by summation", $"total={target.TotalEnfeeblementStrengthPenalty}");

            var expiredAtRound2 = target.TickEnfeeblementEffects();
            Assert(expiredAtRound2.Count == 0, "No Ray effect expires after first tick");
            expiredAtRound2 = target.TickEnfeeblementEffects();
            Assert(expiredAtRound2.Count == 1, "One Ray effect expires on second tick");
            Assert(target.TotalEnfeeblementStrengthPenalty == 3, "Remaining Ray penalty persists after first expiry", $"total={target.TotalEnfeeblementStrengthPenalty}");

            target.TickEnfeeblementEffects();
            var expiredAtRound4 = target.TickEnfeeblementEffects();
            Assert(expiredAtRound4.Count == 1, "Second Ray effect expires on fourth tick");
            Assert(target.TotalEnfeeblementStrengthPenalty == 0, "Total Ray penalty returns to zero after all effects expire");
        }
        finally
        {
            DestroyController(target);
            DestroyController(caster);
        }
    }

    private static void TestStrengthFloorAndCombatStatImpact()
    {
        CharacterController target = null;
        try
        {
            target = CreateController(BuildStats("Bruiser", level: 5, str: 8), CharacterTeam.Enemy, new Vector2Int(4, 4));

            int baseEffectiveStr = target.Stats.EffectiveStrengthScore;
            int baseStrMod = target.Stats.STRMod;
            int baseMeleeAttackBonus = target.Stats.GetMeleeAttackBonus();
            int baseUnarmedDamageBonus = target.Stats.GetWeaponDamageModifier(null, false);
            float baseMaxCarry = target.Stats.MaxCarryWeightLbs;
            int baseClimbBonus = target.Stats.GetSkillBonus("Climb");

            target.ApplyEnfeeblementEffect(strengthPenaltyAmount: 20, durationRemainingRounds: 3, caster: null);

            Assert(target.Stats.EffectiveStrengthScore == 1, "Ray enforces minimum Strength score of 1", $"str={target.Stats.EffectiveStrengthScore}");
            Assert(target.Stats.STRMod < baseStrMod, "Ray reduces Strength modifier", $"before={baseStrMod}, after={target.Stats.STRMod}");
            Assert(target.Stats.GetMeleeAttackBonus() < baseMeleeAttackBonus,
                "Ray lowers melee attack bonus",
                $"before={baseMeleeAttackBonus}, after={target.Stats.GetMeleeAttackBonus()}");
            Assert(target.Stats.GetWeaponDamageModifier(null, false) < baseUnarmedDamageBonus,
                "Ray lowers STR-based melee damage bonus",
                $"before={baseUnarmedDamageBonus}, after={target.Stats.GetWeaponDamageModifier(null, false)}");
            Assert(target.Stats.MaxCarryWeightLbs < baseMaxCarry,
                "Ray lowers effective carrying capacity threshold",
                $"before={baseMaxCarry}, after={target.Stats.MaxCarryWeightLbs}, baseSTR={baseEffectiveStr}");
            Assert(target.Stats.GetSkillBonus("Climb") < baseClimbBonus,
                "Ray lowers STR-based skill checks (example: Climb)",
                $"before={baseClimbBonus}, after={target.Stats.GetSkillBonus("Climb")}");
        }
        finally
        {
            DestroyController(target);
        }
    }
}
}
