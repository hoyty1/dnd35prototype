using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Touch of Idiocy mechanics.
/// Run with TouchOfIdiocyRulesTests.RunAll().
/// </summary>
public static class TouchOfIdiocyRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== TOUCH OF IDIOCY RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestDefinitionMatchesCoreRules();
        TestDurationScalingInRounds();
        TestMeleeTouchAttackUsesTouchAc();
        TestNoSavingThrowAndSpellResistance();
        TestAbilityDamageApplicationAndExpirationRecovery();
        TestZeroMentalScoresDoNotCauseUnconsciousness();

        Debug.Log($"====== Touch of Idiocy Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(
        string name,
        int level,
        int str = 10,
        int dex = 10,
        int con = 10,
        int intelligence = 10,
        int wisdom = 10,
        int charisma = 10,
        int bab = -999)
    {
        if (bab == -999)
            bab = Mathf.Max(0, level / 2);

        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: "Wizard",
            str: str,
            dex: dex,
            con: con,
            wis: wisdom,
            intelligence: intelligence,
            cha: charisma,
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
        GameObject go = new GameObject($"TouchIdiocyRules_{stats.CharacterName}");
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

    private static void TestDefinitionMatchesCoreRules()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.TOUCH_OF_IDIOCY);
        Assert(spell != null, "Touch of Idiocy spell definition exists");
        if (spell == null)
            return;

        Assert(spell.SpellLevel == 2, "Touch of Idiocy is level 2");
        Assert(spell.TargetType == SpellTargetType.SingleEnemy, "Touch of Idiocy targets one enemy");
        Assert(spell.IsMeleeTouchSpell(), "Touch of Idiocy uses melee touch attack");
        Assert(!spell.AllowsSavingThrow, "Touch of Idiocy has no saving throw");
        Assert(spell.SpellResistanceApplies, "Touch of Idiocy is subject to spell resistance");
        Assert(spell.DurationType == DurationType.Minutes && spell.DurationValue == 10 && spell.DurationScalesWithLevel,
            "Touch of Idiocy duration is 10 min/level");
    }

    private static void TestDurationScalingInRounds()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.TOUCH_OF_IDIOCY);
        Assert(spell != null, "Precondition: Touch of Idiocy exists for duration test");
        if (spell == null)
            return;

        Assert(ActiveSpellEffect.CalculateDurationRounds(spell, 3) == 300, "Duration at CL3 is 300 rounds");
        Assert(ActiveSpellEffect.CalculateDurationRounds(spell, 5) == 500, "Duration at CL5 is 500 rounds");
        Assert(ActiveSpellEffect.CalculateDurationRounds(spell, 10) == 1000, "Duration at CL10 is 1000 rounds");
    }

    private static void TestMeleeTouchAttackUsesTouchAc()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.TOUCH_OF_IDIOCY);
        CharacterStats caster = BuildStats("Caster", level: 5, str: 14, bab: 5);
        CharacterStats target = BuildStats("Target", level: 3, dex: 14, bab: 2);
        target.ArmorBonus = 8;
        target.ShieldBonus = 4;
        target.NaturalArmorBonus = 6;

        Random.InitState(9001);
        int predictedRoll = Random.Range(1, 21);
        Random.InitState(9001);
        SpellResult result = SpellCaster.Cast(spell, caster, target, null, false, false, null, null);

        int expectedTouchAc = SpellcastingComponent.GetTouchAC(target);
        int expectedAttackBonus = caster.BaseAttackBonus + caster.STRMod + caster.SizeModifier;
        bool expectedHit = predictedRoll == 20 || (predictedRoll != 1 && predictedRoll + expectedAttackBonus >= expectedTouchAc);

        Assert(result.RequiredAttackRoll, "Touch of Idiocy requires attack roll");
        Assert(!result.IsRangedTouch, "Touch of Idiocy uses melee (not ranged) touch attack");
        Assert(result.TouchAC == expectedTouchAc, "Touch of Idiocy attack checks against touch AC", $"expected={expectedTouchAc}, actual={result.TouchAC}");
        Assert(result.AttackHit == expectedHit, "Touch of Idiocy hit/miss resolves against touch AC");
    }

    private static void TestNoSavingThrowAndSpellResistance()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.TOUCH_OF_IDIOCY);
        CharacterStats caster = BuildStats("Caster", level: 5, str: 14, bab: 5);
        CharacterStats target = BuildStats("Target", level: 5, dex: 10, bab: 2);
        target.SpellResistance = 999;

        Random.InitState(42);
        SpellResult result = SpellCaster.Cast(spell, caster, target, null, false, false, null, null);

        Assert(!result.RequiredSave, "Touch of Idiocy requires no saving throw");
        Assert(result.SpellResistanceChecked, "Touch of Idiocy checks spell resistance");
        Assert(!result.SpellResistancePassed, "Very high SR blocks Touch of Idiocy");
        Assert(!result.Success, "Touch of Idiocy fails when SR blocks");
    }

    private static void TestAbilityDamageApplicationAndExpirationRecovery()
    {
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Caster", level: 5), CharacterTeam.Player, new Vector2Int(0, 0));
            target = CreateController(BuildStats("Target", level: 4, intelligence: 12, wisdom: 11, charisma: 10), CharacterTeam.Enemy, new Vector2Int(1, 0));

            int baseInt = target.Stats.EffectiveINTScore;
            int baseWis = target.Stats.EffectiveWISScore;
            int baseCha = target.Stats.EffectiveCHAScore;
            int baseWill = target.Stats.WillSave;

            TouchOfIdiocyConditionData effect = target.ApplyTouchOfIdiocyEffect(4, 2, 5, 3, caster);
            Assert(effect != null, "Touch of Idiocy effect applies to target");
            Assert(target.ActiveTouchOfIdiocyEffect != null, "Target tracks active Touch of Idiocy effect");
            Assert(target.Stats.EffectiveINTScore == baseInt - 4, "INT reduced by rolled damage");
            Assert(target.Stats.EffectiveWISScore == baseWis - 2, "WIS reduced by rolled damage");
            Assert(target.Stats.EffectiveCHAScore == baseCha - 5, "CHA reduced by rolled damage");
            Assert(target.Stats.WillSave < baseWill, "Will save is recalculated after Wisdom damage");

            target.TickTouchOfIdiocyEffect();
            target.TickTouchOfIdiocyEffect();
            TouchOfIdiocyConditionData expired = target.TickTouchOfIdiocyEffect();
            Assert(expired != null, "Touch of Idiocy expires after duration rounds");
            Assert(target.ActiveTouchOfIdiocyEffect == null, "No active Touch of Idiocy after expiration");
            Assert(target.Stats.EffectiveINTScore == baseInt, "INT restored after Touch of Idiocy expires");
            Assert(target.Stats.EffectiveWISScore == baseWis, "WIS restored after Touch of Idiocy expires");
            Assert(target.Stats.EffectiveCHAScore == baseCha, "CHA restored after Touch of Idiocy expires");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }

    private static void TestZeroMentalScoresDoNotCauseUnconsciousness()
    {
        CharacterController target = null;

        try
        {
            target = CreateController(BuildStats("MindTarget", level: 3, intelligence: 1, wisdom: 1, charisma: 1), CharacterTeam.Enemy, new Vector2Int(2, 2));
            target.ApplyTouchOfIdiocyEffect(2, 2, 2, 2, null);

            Assert(target.Stats.IsAbilityReducedToZero(AbilityType.INT)
                   && target.Stats.IsAbilityReducedToZero(AbilityType.WIS)
                   && target.Stats.IsAbilityReducedToZero(AbilityType.CHA),
                "Touch of Idiocy can reduce mental scores to 0");
            Assert(!target.HasCondition(CombatConditionType.Unconscious),
                "Touch of Idiocy does not cause unconsciousness at 0 mental scores");
            Assert(!target.HasCondition(CombatConditionType.Helpless),
                "Touch of Idiocy does not force helplessness when only mental 0 is from this spell");
            Assert(target.IsComatoseOnlyFromTouchOfIdiocyEffect(),
                "Comatose-prevention helper detects Touch of Idiocy special-case");
        }
        finally
        {
            DestroyController(target);
        }
    }
}
}
