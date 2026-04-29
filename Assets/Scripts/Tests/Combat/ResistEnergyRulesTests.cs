using System;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Resist Energy behavior.
/// </summary>
public static class ResistEnergyRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void resist_energy_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== RESIST ENERGY RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        SpellDatabase.Init();

        TestSpellDefinition();
        TestCasterLevelScaling();
        TestMatchingTypeReducesDamage();
        TestMismatchedTypeDoesNotReduceDamage();
        TestCannotGoBelowZero();
        TestStackingRules();

        Debug.Log($"====== Resist Energy Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController CreateController(string name, int level = 5)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: "Wizard",
            str: 10,
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
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 30,
            raceName: "Human");

        GameObject go = new GameObject($"ResistEnergy_{name}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Init(stats, Vector2Int.zero, null, null);
        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
    }

    private static DamagePacket BuildPacket(DamageType type)
    {
        return new DamagePacket
        {
            Source = AttackSource.Spell,
            IsRanged = true,
            IsNonlethal = false,
            SourceName = "Unit Test Energy Damage",
            AttackTags = DamageBypassTag.None,
            Types = new System.Collections.Generic.HashSet<DamageType> { type }
        };
    }

    private static void ApplyResistEnergy(CharacterController target, ResistEnergyType energy, int amount, int rounds = 100)
    {
        target.Stats.SetResistEnergyEffect(new ResistEnergyEffectData
        {
            EnergyType = energy,
            ResistanceAmount = amount,
            DurationRemainingRounds = rounds,
            Caster = target
        });
    }

    private static void TestSpellDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell("resist_energy");
        Assert(spell != null, "Resist Energy definition exists");
        if (spell == null)
            return;

        Assert(spell.SpellLevel == 2, "Resist Energy base level is 2");
        Assert(string.Equals(spell.School, "Abjuration", StringComparison.OrdinalIgnoreCase), "Resist Energy school is Abjuration");
        Assert(spell.RangeCategory == SpellRangeCategory.Touch, "Resist Energy range is touch");
        Assert(spell.TargetType == SpellTargetType.SingleAlly, "Resist Energy target is creature touched");
        Assert(spell.DurationType == DurationType.Minutes && spell.DurationValue == 10 && spell.DurationScalesWithLevel,
            "Resist Energy duration is 10 min/level");

        Assert(spell.GetSpellLevelFor("Cleric") == 2, "Resist Energy is Cleric 2");
        Assert(spell.GetSpellLevelFor("Druid") == 2, "Resist Energy is Druid 2");
        Assert(spell.GetSpellLevelFor("Paladin") == 2, "Resist Energy is Paladin 2");
        Assert(spell.GetSpellLevelFor("Ranger") == 1, "Resist Energy is Ranger 1");
        Assert(spell.GetSpellLevelFor("Wizard") == 2, "Resist Energy is Wizard 2");
        Assert(spell.GetSpellLevelFor("Sorcerer") == 2, "Resist Energy is Sorcerer 2");
    }

    private static void TestCasterLevelScaling()
    {
        Assert(GetResistanceForCasterLevel(1) == 10, "CL 1 grants Resist 10");
        Assert(GetResistanceForCasterLevel(6) == 10, "CL 6 grants Resist 10");
        Assert(GetResistanceForCasterLevel(7) == 20, "CL 7 grants Resist 20");
        Assert(GetResistanceForCasterLevel(10) == 20, "CL 10 grants Resist 20");
        Assert(GetResistanceForCasterLevel(11) == 30, "CL 11 grants Resist 30");
        Assert(GetResistanceForCasterLevel(18) == 30, "CL 18 grants Resist 30");
    }

    private static int GetResistanceForCasterLevel(int casterLevel)
    {
        int cl = Mathf.Max(1, casterLevel);
        return cl >= 11 ? 30 : (cl >= 7 ? 20 : 10);
    }

    private static void TestMatchingTypeReducesDamage()
    {
        CharacterController target = null;
        try
        {
            target = CreateController("MatchType", level: 5);
            ApplyResistEnergy(target, ResistEnergyType.Fire, 10);

            int hpBefore = target.Stats.CurrentHP;
            DamageResolutionResult result = target.Stats.ApplyIncomingDamage(15, BuildPacket(DamageType.Fire));
            int hpLost = hpBefore - target.Stats.CurrentHP;

            Assert(result.ResistanceApplied == 10, "Matching energy type applies resistance", $"resisted={result.ResistanceApplied}");
            Assert(hpLost == 5, "Fire 15 reduced to 5 with Resist Fire 10", $"hpLost={hpLost}");
            Assert(result.GetMitigationSummary().Contains("reduced to 5"), "Combat mitigation summary includes reduced damage details", result.GetMitigationSummary());
        }
        finally
        {
            DestroyController(target);
        }
    }

    private static void TestMismatchedTypeDoesNotReduceDamage()
    {
        CharacterController target = null;
        try
        {
            target = CreateController("MismatchType", level: 5);
            ApplyResistEnergy(target, ResistEnergyType.Fire, 10);

            int hpBefore = target.Stats.CurrentHP;
            DamageResolutionResult result = target.Stats.ApplyIncomingDamage(15, BuildPacket(DamageType.Cold));
            int hpLost = hpBefore - target.Stats.CurrentHP;

            Assert(result.ResistanceApplied == 0, "Different energy type does not apply resistance", $"resisted={result.ResistanceApplied}");
            Assert(hpLost == 15, "Cold damage is unchanged by Resist Fire", $"hpLost={hpLost}");
        }
        finally
        {
            DestroyController(target);
        }
    }

    private static void TestCannotGoBelowZero()
    {
        CharacterController target = null;
        try
        {
            target = CreateController("FloorZero", level: 8);
            ApplyResistEnergy(target, ResistEnergyType.Cold, 20);

            int hpBefore = target.Stats.CurrentHP;
            DamageResolutionResult result = target.Stats.ApplyIncomingDamage(15, BuildPacket(DamageType.Cold));
            int hpLost = hpBefore - target.Stats.CurrentHP;

            Assert(result.FinalDamage == 0, "Damage floors at zero when resistance exceeds damage", $"final={result.FinalDamage}");
            Assert(hpLost == 0, "No HP lost when cold 15 hits Resist Cold 20", $"hpLost={hpLost}");
        }
        finally
        {
            DestroyController(target);
        }
    }

    private static void TestStackingRules()
    {
        CharacterController target = null;
        try
        {
            target = CreateController("Stacking", level: 10);

            ApplyResistEnergy(target, ResistEnergyType.Fire, 10, rounds: 20);
            ApplyResistEnergy(target, ResistEnergyType.Fire, 20, rounds: 15);
            ApplyResistEnergy(target, ResistEnergyType.Cold, 20, rounds: 30);

            int fireEntries = 0;
            int coldEntries = 0;
            int fireAmount = 0;
            for (int i = 0; i < target.Stats.ActiveResistEnergyEffects.Count; i++)
            {
                ResistEnergyEffectData e = target.Stats.ActiveResistEnergyEffects[i];
                if (e == null) continue;
                if (e.EnergyType == ResistEnergyType.Fire)
                {
                    fireEntries++;
                    fireAmount = Mathf.Max(fireAmount, e.ResistanceAmount);
                }
                if (e.EnergyType == ResistEnergyType.Cold)
                    coldEntries++;
            }

            Assert(fireEntries == 1, "Same-type Resist Energy effects consolidate to one entry", $"fireEntries={fireEntries}");
            Assert(fireAmount == 20, "Same-type stacking keeps highest resistance", $"fireAmount={fireAmount}");
            Assert(coldEntries == 1, "Different energy type remains alongside fire", $"coldEntries={coldEntries}");

            int hpBeforeFire = target.Stats.CurrentHP;
            DamageResolutionResult fireResult = target.Stats.ApplyIncomingDamage(25, BuildPacket(DamageType.Fire));
            int fireHpLost = hpBeforeFire - target.Stats.CurrentHP;
            Assert(fireResult.ResistanceApplied == 20 && fireHpLost == 5, "Highest fire resistance is used for fire damage", $"res={fireResult.ResistanceApplied}, lost={fireHpLost}");

            int hpBeforeCold = target.Stats.CurrentHP;
            DamageResolutionResult coldResult = target.Stats.ApplyIncomingDamage(25, BuildPacket(DamageType.Cold));
            int coldHpLost = hpBeforeCold - target.Stats.CurrentHP;
            Assert(coldResult.ResistanceApplied == 20 && coldHpLost == 5, "Cold resistance applies independently", $"res={coldResult.ResistanceApplied}, lost={coldHpLost}");
        }
        finally
        {
            DestroyController(target);
        }
    }
}
}
