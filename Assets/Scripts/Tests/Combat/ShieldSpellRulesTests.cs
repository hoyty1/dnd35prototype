using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Focused regression checks for Shield vs Magic Missile behavior.
/// Run with ShieldSpellRulesTests.RunAll().
/// </summary>
public static class ShieldSpellRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SHIELD SPELL RULE TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestShieldDefinitionUsesShieldBonusAndMinutePerLevelDuration();
        TestShieldAppliesPlusFourShieldBonusAndDurationScaling();
        TestMagicMissileIsCompletelyBlockedByShield();
        TestMagicMissileStillDealsDamageWithoutShield();

        Debug.Log($"====== Shield Spell Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildWizardStats(string name, int level)
    {
        return new CharacterStats(
            name: name,
            level: level,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 12, intelligence: 18, cha: 10,
            bab: Mathf.Max(1, level / 2),
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 24,
            raceName: "Human");
    }

    private static CharacterController CreateWizardController(string name, int level)
    {
        var go = new GameObject($"ShieldTest_{name}");
        var controller = go.AddComponent<CharacterController>();
        var stats = BuildWizardStats(name, level);
        controller.Stats = stats;

        var spellComp = go.AddComponent<SpellcastingComponent>();
        spellComp.Init(stats);

        var statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static void TestShieldDefinitionUsesShieldBonusAndMinutePerLevelDuration()
    {
        SpellData shield = SpellDatabase.GetSpell("shield");

        Assert(shield != null, "Shield definition exists");
        if (shield == null)
            return;

        Assert(shield.BuffShieldBonus == 4,
            "Shield definition grants +4 shield bonus",
            $"expected 4, got {shield.BuffShieldBonus}");

        Assert(shield.GetEffectiveBonusType() == BonusType.Shield,
            "Shield definition uses Shield bonus type",
            $"got {shield.GetEffectiveBonusType()}");

        Assert(shield.DurationType == DurationType.Minutes && shield.DurationValue == 1 && shield.DurationScalesWithLevel,
            "Shield definition duration is 1 minute/level",
            $"durationType={shield.DurationType}, durationValue={shield.DurationValue}, scales={shield.DurationScalesWithLevel}");
    }

    private static void TestShieldAppliesPlusFourShieldBonusAndDurationScaling()
    {
        CharacterController target = null;
        try
        {
            target = CreateWizardController("ShieldTarget", 5);
            SpellData shield = SpellDatabase.GetSpell("shield");
            StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();

            int acBefore = target.Stats.GetArmorClass();
            int shieldBefore = target.Stats.ShieldBonus;

            ActiveSpellEffect effect = statusMgr.AddEffect(shield, "Tester", casterLevel: 5);

            Assert(effect != null, "Shield effect applied");
            if (effect == null)
                return;

            Assert(target.Stats.ShieldBonus == shieldBefore + 4,
                "Shield adds +4 shield bonus to AC stat",
                $"expected {shieldBefore + 4}, got {target.Stats.ShieldBonus}");

            int acAfter = target.Stats.GetArmorClass();
            Assert(acAfter == acBefore + 4,
                "Shield increases AC by +4",
                $"expected AC {acBefore + 4}, got {acAfter}");

            Assert(effect.RemainingRounds == 50,
                "Shield duration scales to 50 rounds at caster level 5",
                $"expected 50, got {effect.RemainingRounds}");

            effect.Tick();
            Assert(effect.RemainingRounds == 49,
                "Shield duration decrements by one round per tick",
                $"expected 49, got {effect.RemainingRounds}");
        }
        finally
        {
            DestroyController(target);
        }
    }

    private static void TestMagicMissileIsCompletelyBlockedByShield()
    {
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateWizardController("MissileCaster", 5);
            target = CreateWizardController("ShieldedTarget", 5);

            SpellData shield = SpellDatabase.GetSpell("shield");
            SpellData magicMissile = SpellDatabase.GetSpell("magic_missile");
            StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
            statusMgr.AddEffect(shield, "TargetCaster", casterLevel: 5);

            int hpBefore = target.Stats.CurrentHP;
            SpellResult result = SpellCaster.Cast(
                magicMissile,
                caster.Stats,
                target.Stats,
                metamagic: null,
                forceFriendlyTouchNoRoll: false,
                forceTargetToFailSave: false,
                casterController: caster,
                targetController: target);

            Assert(result.MagicMissileBlockedByShield,
                "Shield flags Magic Missile as blocked");

            Assert(result.DamageDealt == 0,
                "Shield causes Magic Missile to deal 0 damage",
                $"expected 0, got {result.DamageDealt}");

            Assert(target.Stats.CurrentHP == hpBefore,
                "Shielded target HP remains unchanged after Magic Missile",
                $"expected {hpBefore}, got {target.Stats.CurrentHP}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }

    private static void TestMagicMissileStillDealsDamageWithoutShield()
    {
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateWizardController("UnblockedCaster", 5);
            target = CreateWizardController("UnshieldedTarget", 5);

            SpellData magicMissile = SpellDatabase.GetSpell("magic_missile");
            int hpBefore = target.Stats.CurrentHP;

            SpellResult result = SpellCaster.Cast(
                magicMissile,
                caster.Stats,
                target.Stats,
                metamagic: null,
                forceFriendlyTouchNoRoll: false,
                forceTargetToFailSave: false,
                casterController: caster,
                targetController: target);

            Assert(!result.MagicMissileBlockedByShield,
                "Magic Missile is not incorrectly marked blocked when Shield is absent");

            Assert(result.DamageDealt > 0,
                "Magic Missile deals positive damage when Shield is absent",
                $"expected > 0, got {result.DamageDealt}");

            Assert(target.Stats.CurrentHP < hpBefore,
                "Unshielded target loses HP from Magic Missile",
                $"expected less than {hpBefore}, got {target.Stats.CurrentHP}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }
}
}
