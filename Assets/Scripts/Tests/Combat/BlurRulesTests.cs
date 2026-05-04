using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Blur mechanics (D&D 3.5e PHB).
/// Run with BlurRulesTests.RunAll().
/// </summary>
public static class BlurRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void blur_rules_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== BLUR RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestSpellDefinitionMatchesCoreRules();
        TestDurationScalingInRounds();
        TestBlurAppliesTwentyPercentConcealment();
        TestConcealmentUsesHighestSourceOnly();
        TestSeeInvisibleDoesNotNegateBlur();
        TestBlindedAttackerIgnoresBlurConcealment();
        TestTouchFriendlyDeliveryAndDismissalFlow();

        Debug.Log($"====== Blur Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name, string className = "Wizard", int level = 5, int str = 12, int dex = 12, int wis = 10, int intelligence = 14)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: str,
            dex: dex,
            con: 12,
            wis: wis,
            intelligence: intelligence,
            cha: 10,
            bab: Mathf.Max(1, level / 2),
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 30,
            raceName: "Human");

        stats.InitializeSkills(className, level);
        return stats;
    }

    private static CharacterController CreateController(string name, string className = "Wizard", int level = 5)
    {
        CharacterStats stats = BuildStats(name, className, level);

        GameObject go = new GameObject($"BlurRules_{name}");
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

    private static ActiveSpellEffect ApplySpell(CharacterController target, CharacterController caster, string spellId, int casterLevel = 5)
    {
        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        SpellData spell = SpellDatabase.GetSpell(spellId);
        if (statusMgr == null || spell == null)
            return null;

        return statusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster", casterLevel);
    }

    private static void TestSpellDefinitionMatchesCoreRules()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.BLUR);
        Assert(spell != null, "Blur definition exists");
        if (spell == null)
            return;

        Assert(string.Equals(spell.SpellId, SpellNames.BLUR, System.StringComparison.Ordinal), "Spell ID is blur");
        Assert(string.Equals(spell.Name, "Blur", System.StringComparison.Ordinal), "Spell name is Blur");
        Assert(string.Equals(spell.School, "Illusion (Glamer)", System.StringComparison.Ordinal), "School is Illusion (Glamer)");
        Assert(spell.GetSpellLevelFor("Wizard") == 2, "Wizard level is 2");
        Assert(spell.GetSpellLevelFor("Sorcerer") == 2, "Sorcerer level is 2");
        Assert(spell.GetSpellLevelFor("Bard") == 2, "Bard level is 2");
        Assert(spell.TargetType == SpellTargetType.Touch, "Blur target type is Touch");
        Assert(spell.RangeCategory == SpellRangeCategory.Touch, "Blur range is Touch");
        Assert(spell.IsMeleeTouchSpell(), "Blur uses melee touch delivery");
        Assert(spell.DurationType == DurationType.Minutes && spell.DurationValue == 1 && spell.DurationScalesWithLevel,
            "Blur duration is 1 minute/level");
        Assert(spell.IsDismissible, "Blur is dismissible");
        Assert(spell.AllowsSavingThrow && spell.SavingThrowType == "Will", "Blur allows Will save");
        Assert(spell.SpellResistanceApplies, "Blur is subject to spell resistance");
        Assert(spell.HasVerbalComponent && !spell.HasSomaticComponent, "Blur has verbal-only components (no somatic)");
    }

    private static void TestDurationScalingInRounds()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.BLUR);
        Assert(spell != null, "Blur exists for duration checks");
        if (spell == null)
            return;

        int cl3 = ActiveSpellEffect.CalculateDurationRounds(spell, 3);
        int cl5 = ActiveSpellEffect.CalculateDurationRounds(spell, 5);
        int cl10 = ActiveSpellEffect.CalculateDurationRounds(spell, 10);

        Assert(cl3 == 30, "CL3 Blur duration is 30 rounds", $"observed={cl3}");
        Assert(cl5 == 50, "CL5 Blur duration is 50 rounds", $"observed={cl5}");
        Assert(cl10 == 100, "CL10 Blur duration is 100 rounds", $"observed={cl10}");
    }

    private static void TestBlurAppliesTwentyPercentConcealment()
    {
        CharacterController caster = null;
        CharacterController target = null;
        CharacterController attacker = null;

        try
        {
            caster = CreateController("BlurCaster", "Wizard", 5);
            target = CreateController("BlurTarget", "Fighter", 5);
            attacker = CreateController("BlurAttacker", "Fighter", 5);

            ActiveSpellEffect blur = ApplySpell(target, caster, SpellNames.BLUR, 5);
            Assert(blur != null, "Blur status effect is applied");
            Assert(target.HasActiveBlurEffect, "Target tracks active Blur effect");
            Assert(target.GetMissChance(attacker, incomingIsRangedAttack: false) == 20,
                "Blur grants 20% miss chance");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            DestroyController(attacker);
        }
    }

    private static void TestConcealmentUsesHighestSourceOnly()
    {
        CharacterController caster = null;
        CharacterController target = null;
        CharacterController attacker = null;

        try
        {
            caster = CreateController("StackCaster", "Wizard", 5);
            target = CreateController("StackTarget", "Rogue", 5);
            attacker = CreateController("StackAttacker", "Fighter", 5);

            ActiveSpellEffect blur = ApplySpell(target, caster, SpellNames.BLUR, 5);
            Assert(blur != null, "Blur applied for stacking test");
            Assert(target.GetMissChance(attacker, false) == 20, "Blur alone gives 20%");

            ActiveSpellEffect invis = ApplySpell(target, caster, SpellNames.INVISIBILITY, 5);
            if (invis != null)
                target.ApplyInvisibilityEffect(invis.RemainingRounds, caster, isMoving: false);

            Assert(target.GetMissChance(attacker, false) == 50,
                "Blur + Invisibility uses highest concealment (50%)");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            DestroyController(attacker);
        }
    }

    private static void TestSeeInvisibleDoesNotNegateBlur()
    {
        CharacterController caster = null;
        CharacterController target = null;
        CharacterController seer = null;

        try
        {
            caster = CreateController("SeeBlurCaster", "Wizard", 5);
            target = CreateController("SeeBlurTarget", "Rogue", 5);
            seer = CreateController("SeeBlurSeer", "Wizard", 5);

            ActiveSpellEffect blur = ApplySpell(target, caster, SpellNames.BLUR, 5);
            Assert(blur != null, "Blur applied for See Invisible test");

            ActiveSpellEffect seeInvisible = ApplySpell(seer, seer, SpellNames.SEE_INVISIBLE, 5);
            if (seeInvisible != null)
                seer.ApplySeeInvisibilityEffect(seeInvisible.RemainingRounds, seer);

            Assert(seer.CanSeeInvisible(target), "Seer can see invisible creatures");
            Assert(target.GetMissChance(seer, false) == 20,
                "See Invisible does not negate Blur miss chance");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            DestroyController(seer);
        }
    }

    private static void TestBlindedAttackerIgnoresBlurConcealment()
    {
        CharacterController caster = null;
        CharacterController target = null;
        CharacterController blindedAttacker = null;

        try
        {
            caster = CreateController("BlindCaster", "Wizard", 5);
            target = CreateController("BlindTarget", "Rogue", 5);
            blindedAttacker = CreateController("BlindAttacker", "Fighter", 5);

            ApplySpell(target, caster, SpellNames.BLUR, 5);
            blindedAttacker.ApplyCondition(CombatConditionType.Blinded, 3, "Unit Test");

            Assert(target.GetMissChance(blindedAttacker, false) == 0,
                "Blinded attacker ignores target Blur concealment source");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            DestroyController(blindedAttacker);
        }
    }

    private static void TestTouchFriendlyDeliveryAndDismissalFlow()
    {
        CharacterController caster = null;
        CharacterController ally = null;

        try
        {
            caster = CreateController("TouchCaster", "Wizard", 5);
            ally = CreateController("TouchAlly", "Fighter", 5);

            SpellData blur = SpellDatabase.GetSpell(SpellNames.BLUR);
            Assert(blur != null, "Blur exists for touch delivery test");
            if (blur == null)
                return;

            SpellResult touchCast = SpellCaster.Cast(
                blur,
                caster.Stats,
                ally.Stats,
                null,
                forceFriendlyTouchNoRoll: true,
                forceTargetToFailSave: true,
                casterController: caster,
                targetController: ally);

            Assert(!touchCast.RequiredAttackRoll, "Willing touch delivery skips touch attack roll");
            Assert(touchCast.AttackHit, "Friendly touch delivery succeeds");
            Assert(touchCast.RequiredSave && !touchCast.SaveSucceeded,
                "Willing ally can elect to fail harmless save");

            StatusEffectManager statusMgr = ally.GetComponent<StatusEffectManager>();
            ActiveSpellEffect effect = statusMgr != null ? statusMgr.AddEffect(blur, caster.Stats.CharacterName, 5) : null;
            Assert(effect != null, "Blur effect tracked on ally status manager");
            Assert(statusMgr != null && statusMgr.HasEffect(SpellNames.BLUR), "Blur is active before dismissal");

            if (statusMgr != null)
                statusMgr.RemoveEffectsBySpellId(SpellNames.BLUR);

            Assert(statusMgr == null || !statusMgr.HasEffect(SpellNames.BLUR), "Blur can be dismissed/removed by caster flow");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(ally);
        }
    }
}
}
