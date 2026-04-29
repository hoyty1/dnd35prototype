using System.Reflection;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Melf's Acid Arrow mechanics.
/// Run with MelfsAcidArrowRulesTests.RunAll().
/// </summary>
public static class MelfsAcidArrowRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== MELF'S ACID ARROW RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        SpellDatabase.Init();

        TestSpellDefinitionMatchesRules();
        TestDurationScalingAndCap();
        TestRangedTouchAttackUsesTouchAc();
        TestAcidDamageIsMitigatedByResistEnergy();

        Debug.Log($"====== Melf's Acid Arrow Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name, int level, int dex = 12, int bab = -999)
    {
        if (bab == -999)
            bab = Mathf.Max(0, level / 2);

        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: "Wizard",
            str: 10,
            dex: dex,
            con: 12,
            wis: 12,
            intelligence: 16,
            cha: 10,
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
        GameObject go = new GameObject($"MelfRules_{stats.CharacterName}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Stats = stats;
        controller.SetTeam(team);
        controller.GridPosition = gridPos;

        StatusEffectManager statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static void TestSpellDefinitionMatchesRules()
    {
        SpellData spell = SpellDatabase.GetSpell("melfs_acid_arrow");
        Assert(spell != null, "Melf's Acid Arrow definition exists");
        if (spell == null)
            return;

        Assert(spell.SpellLevel == 2, "Melf's Acid Arrow is level 2");
        Assert(spell.GetSpellLevelFor("Wizard") == 2, "Melf's Acid Arrow is Wizard 2");
        Assert(spell.GetSpellLevelFor("Sorcerer") == 2, "Melf's Acid Arrow is Sorcerer 2");
        Assert(spell.RangeCategory == SpellRangeCategory.Long, "Melf's Acid Arrow uses Long range");
        Assert(spell.TargetType == SpellTargetType.SingleEnemy, "Melf's Acid Arrow targets one enemy");
        Assert(spell.IsRangedTouchSpell(), "Melf's Acid Arrow uses ranged touch attack");
        Assert(spell.DamageCount == 2 && spell.DamageDice == 4, "Melf's Acid Arrow initial damage is 2d4");
        Assert(string.Equals(spell.DamageType, "acid", System.StringComparison.OrdinalIgnoreCase), "Melf's Acid Arrow uses acid damage");
        Assert(!spell.AllowsSavingThrow, "Melf's Acid Arrow allows no save");
        Assert(!spell.SpellResistanceApplies, "Melf's Acid Arrow ignores spell resistance");
    }

    private static void TestDurationScalingAndCap()
    {
        MethodInfo method = typeof(GameManager).GetMethod("CalculateMelfsAcidArrowAdditionalRounds", BindingFlags.NonPublic | BindingFlags.Static);
        Assert(method != null, "Can access Melf's Acid Arrow duration helper");
        if (method == null)
            return;

        ValidateDuration(method, 1, expectedAdditionalRounds: 0);
        ValidateDuration(method, 2, expectedAdditionalRounds: 0);
        ValidateDuration(method, 3, expectedAdditionalRounds: 1);
        ValidateDuration(method, 6, expectedAdditionalRounds: 2);
        ValidateDuration(method, 9, expectedAdditionalRounds: 3);
        ValidateDuration(method, 12, expectedAdditionalRounds: 4);
        ValidateDuration(method, 15, expectedAdditionalRounds: 5);
        ValidateDuration(method, 18, expectedAdditionalRounds: 6);
        ValidateDuration(method, 24, expectedAdditionalRounds: 6);
    }

    private static void ValidateDuration(MethodInfo method, int casterLevel, int expectedAdditionalRounds)
    {
        CharacterController caster = null;
        try
        {
            caster = CreateController(BuildStats($"CL{casterLevel}", casterLevel), CharacterTeam.Player, Vector2Int.zero);
            int additional = (int)method.Invoke(null, new object[] { caster });
            int total = 1 + additional;
            int expectedTotal = 1 + expectedAdditionalRounds;
            Assert(additional == expectedAdditionalRounds,
                $"Duration additional rounds at CL {casterLevel}",
                $"expected={expectedAdditionalRounds}, actual={additional}");
            Assert(total == expectedTotal,
                $"Duration total rounds at CL {casterLevel}",
                $"expected={expectedTotal}, actual={total}");
        }
        finally
        {
            DestroyController(caster);
        }
    }

    private static void TestRangedTouchAttackUsesTouchAc()
    {
        SpellData spell = SpellDatabase.GetSpell("melfs_acid_arrow");
        CharacterStats caster = BuildStats("Caster", level: 7, dex: 16, bab: 5);
        CharacterStats target = BuildStats("Target", level: 5, dex: 14, bab: 3);

        Random.InitState(4242);
        SpellResult result = SpellCaster.Cast(spell, caster, target, null, false, false, null, null);

        Assert(result.RequiredAttackRoll, "Melf's Acid Arrow requires attack roll");
        Assert(result.IsRangedTouch, "Melf's Acid Arrow is ranged touch");
        Assert(result.TouchAC == SpellcastingComponent.GetTouchAC(target),
            "Melf's Acid Arrow attack compares against touch AC",
            $"expected={SpellcastingComponent.GetTouchAC(target)}, actual={result.TouchAC}");
    }

    private static void TestAcidDamageIsMitigatedByResistEnergy()
    {
        CharacterController target = null;
        try
        {
            target = CreateController(BuildStats("ResistTarget", level: 6), CharacterTeam.Enemy, new Vector2Int(1, 1));
            target.Stats.SetResistEnergyEffect(new ResistEnergyEffectData
            {
                EnergyType = ResistEnergyType.Acid,
                ResistanceAmount = 10,
                DurationRemainingRounds = 10,
                Caster = target
            });

            var packet = new DamagePacket
            {
                RawDamage = 14,
                Types = new System.Collections.Generic.HashSet<DamageType> { DamageType.Acid },
                AttackTags = DamageBypassTag.None,
                IsRanged = true,
                IsNonlethal = false,
                Source = AttackSource.Spell,
                SourceName = "Melf's Acid Arrow Test Tick"
            };

            int hpBefore = target.Stats.CurrentHP;
            DamageResolutionResult resolved = target.Stats.ApplyIncomingDamage(14, packet);
            int hpLost = hpBefore - target.Stats.CurrentHP;

            Assert(resolved.ResistanceApplied == 10, "Resist Energy (Acid) applies to acid arrow tick", $"resisted={resolved.ResistanceApplied}");
            Assert(resolved.FinalDamage == 4 && hpLost == 4, "Acid arrow damage reduced by Resist Energy", $"final={resolved.FinalDamage}, hpLost={hpLost}");
        }
        finally
        {
            DestroyController(target);
        }
    }
}
}
