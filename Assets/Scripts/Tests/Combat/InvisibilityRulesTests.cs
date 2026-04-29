using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Invisibility mechanics.
/// </summary>
public static class InvisibilityRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void invisibility_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== INVISIBILITY RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestSpellDefinition();
        TestConcealmentAndHideBonuses();
        TestBreaksOnAttackRoll();
        TestDirectVisibilityBlockedWhileInvisible();

        Debug.Log($"====== Invisibility Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController CreateController(string name, string className = "Wizard", int level = 5, int baseSpeedSquares = 6)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: 12,
            dex: 14,
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
            baseHitDieHP: 30,
            raceName: "Human");

        stats.InitializeSkills(className, level);

        GameObject go = new GameObject($"Invisibility_{name}");
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

    private static void ApplyInvisibility(CharacterController target, CharacterController caster, int casterLevel = 5)
    {
        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        SpellData spell = SpellDatabase.GetSpell("invisibility");
        ActiveSpellEffect effect = statusMgr.AddEffect(spell, caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster", casterLevel);
        if (effect != null)
            target.ApplyInvisibilityEffect(effect.RemainingRounds, caster, isMoving: false);
    }

    private static void TestSpellDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell("invisibility");
        Assert(spell != null, "Invisibility definition exists");
        if (spell == null)
            return;

        Assert(spell.SpellLevel == 2, "Spell level is 2");
        Assert(spell.School == "Illusion", "School is Illusion");
        Assert(spell.TargetType == SpellTargetType.SingleAlly, "Target type supports ally/self touch cast flow");
        Assert(spell.RangeCategory == SpellRangeCategory.Touch, "Range is touch");
        Assert(spell.DurationType == DurationType.Minutes && spell.DurationValue == 1 && spell.DurationScalesWithLevel,
            "Duration is 1 minute/level");
        Assert(spell.IsDismissible, "Spell is dismissible");
        Assert(spell.ClassList != null
               && System.Array.Exists(spell.ClassList, c => c == "Wizard")
               && System.Array.Exists(spell.ClassList, c => c == "Sorcerer")
               && System.Array.Exists(spell.ClassList, c => c == "Bard"),
            "Class list includes Wizard/Sorcerer/Bard");
    }

    private static void TestConcealmentAndHideBonuses()
    {
        CharacterController caster = null;
        CharacterController target = null;
        CharacterController attacker = null;

        try
        {
            caster = CreateController("Caster", "Wizard", 5);
            target = CreateController("Target", "Rogue", 5);
            attacker = CreateController("Attacker", "Fighter", 5);

            int baseHide = target.Stats.GetSkillBonus("Hide");
            ApplyInvisibility(target, caster, 5);

            Assert(target.HasActiveInvisibilityEffect, "Target tracks active invisibility effect data");
            Assert(target.GetMissChance(attacker, incomingIsRangedAttack: false) == 50,
                "Invisibility grants 50% miss chance vs melee");
            Assert(target.GetMissChance(attacker, incomingIsRangedAttack: true) == 50,
                "Invisibility grants 50% miss chance vs ranged");

            int stationaryHide = target.Stats.GetSkillBonus("Hide");
            Assert(stationaryHide - baseHide == 40,
                "Hide bonus is +40 while stationary",
                $"base={baseHide}, actual={stationaryHide}");

            target.UpdateInvisibilityMovementState(true);
            int movingHide = target.Stats.GetSkillBonus("Hide");
            Assert(movingHide - baseHide == 20,
                "Hide bonus is +20 while moving",
                $"base={baseHide}, actual={movingHide}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            DestroyController(attacker);
        }
    }

    private static void TestBreaksOnAttackRoll()
    {
        CharacterController attacker = null;
        CharacterController defender = null;

        try
        {
            attacker = CreateController("InvisibleAttacker", "Rogue", 5);
            defender = CreateController("Defender", "Fighter", 5);

            ApplyInvisibility(attacker, attacker, 5);
            StatusEffectManager statusMgr = attacker.GetComponent<StatusEffectManager>();

            CombatResult result = attacker.Attack(defender, false, 0, null, null, null, null);
            Assert(result != null, "Attack resolves while invisible");
            Assert(!attacker.HasActiveInvisibilityEffect, "Invisibility effect data clears on attack action");
            Assert(statusMgr != null && !statusMgr.HasEffect("invisibility"), "Invisibility spell effect removed on attack action");
        }
        finally
        {
            DestroyController(attacker);
            DestroyController(defender);
        }
    }

    private static void TestDirectVisibilityBlockedWhileInvisible()
    {
        CharacterController caster = null;
        CharacterController invisibleTarget = null;

        try
        {
            caster = CreateController("Spellcaster", "Wizard", 5);
            invisibleTarget = CreateController("InvisibleTarget", "Wizard", 5);

            ApplyInvisibility(invisibleTarget, invisibleTarget, 5);
            Assert(!caster.CanSee(invisibleTarget, incomingIsRangedAttack: false),
                "Direct visibility check fails while target is invisible");

            StatusEffectManager statusMgr = invisibleTarget.GetComponent<StatusEffectManager>();
            statusMgr.RemoveEffectsBySpellId("invisibility");
            invisibleTarget.ClearInvisibilityEffect();

            Assert(caster.CanSee(invisibleTarget, incomingIsRangedAttack: false),
                "Visibility check succeeds after invisibility ends");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(invisibleTarget);
        }
    }
}
}
