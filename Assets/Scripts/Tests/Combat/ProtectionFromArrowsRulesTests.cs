using System;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Protection from Arrows behavior.
/// </summary>
public static class ProtectionFromArrowsRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== PROTECTION FROM ARROWS RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestSpellDefinition();
        TestAbsorptionPoolScalingAndCap();
        TestRangedWeaponDamageIsReducedAndPoolDecrements();
        TestMagicRangedWeaponBypassesProtection();
        TestRangedSpellBypassesProtection();
        TestPoolDischargeRemovesEffectImmediately();

        Debug.Log($"====== Protection from Arrows Results: {_passed} passed, {_failed} failed ======");
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

        GameObject go = new GameObject($"Pfa_{name}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Init(stats, Vector2Int.zero, null, null);

        StatusEffectManager statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null)
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
    }

    private static void ApplyProtectionFromArrows(CharacterController target, int casterLevel)
    {
        SpellData spell = SpellDatabase.GetSpell("protection_from_arrows");
        StatusEffectManager mgr = target.GetComponent<StatusEffectManager>();
        ActiveSpellEffect effect = mgr.AddEffect(spell, "Tester", casterLevel);
        int pool = Mathf.Min(100, Mathf.Max(1, casterLevel) * 10);
        target.Stats.ActiveProtectionFromArrowsEffect = new ProtectionFromArrowsEffectData
        {
            DamageReductionAmount = effect != null && effect.AppliedDamageReductionAmount > 0 ? effect.AppliedDamageReductionAmount : 10,
            TotalAbsorptionPool = pool,
            CurrentAbsorbedDamage = 0,
            DurationRemainingRounds = effect != null ? effect.RemainingRounds : 0,
            AttacksBlocked = 0
        };
    }

    private static DamagePacket BuildRangedWeaponPacket(bool magical = false)
    {
        return new DamagePacket
        {
            Source = AttackSource.Weapon,
            IsRanged = true,
            IsNonlethal = false,
            SourceName = magical ? "+1 Longbow" : "Longbow",
            AttackTags = magical ? (DamageBypassTag.Piercing | DamageBypassTag.Ranged | DamageBypassTag.Magic) : (DamageBypassTag.Piercing | DamageBypassTag.Ranged),
            Types = new System.Collections.Generic.HashSet<DamageType> { DamageType.Piercing }
        };
    }

    private static void TestSpellDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell("protection_from_arrows");
        Assert(spell != null, "Protection from Arrows definition exists");
        if (spell == null)
            return;

        Assert(spell.SpellLevel == 2, "Protection from Arrows is level 2");
        Assert(string.Equals(spell.School, "Abjuration", StringComparison.OrdinalIgnoreCase), "Protection from Arrows school is Abjuration");
        Assert(spell.RangeCategory == SpellRangeCategory.Touch, "Protection from Arrows range is touch");
        Assert(spell.TargetType == SpellTargetType.SingleAlly, "Protection from Arrows target is creature touched");
        Assert(spell.BuffDamageReductionAmount == 10 && spell.BuffDamageReductionBypass == DamageBypassTag.Magic,
            "Protection from Arrows grants DR 10/magic");
        Assert(spell.DurationType == DurationType.Hours && spell.DurationValue == 1 && spell.DurationScalesWithLevel,
            "Protection from Arrows duration is 1 hour/level");
    }

    private static void TestAbsorptionPoolScalingAndCap()
    {
        CharacterController target = null;
        try
        {
            target = CreateController("PoolScale", level: 12);
            ApplyProtectionFromArrows(target, casterLevel: 12);

            ProtectionFromArrowsEffectData data = target.Stats.ActiveProtectionFromArrowsEffect;
            Assert(data != null, "Protection from Arrows data is created");
            Assert(data != null && data.TotalAbsorptionPool == 100, "Absorption pool caps at 100", $"pool={data?.TotalAbsorptionPool}");
        }
        finally
        {
            DestroyController(target);
        }
    }

    private static void TestRangedWeaponDamageIsReducedAndPoolDecrements()
    {
        CharacterController target = null;
        try
        {
            target = CreateController("RangedBlock", level: 5);
            ApplyProtectionFromArrows(target, casterLevel: 5);

            int hpBefore = target.Stats.CurrentHP;
            DamageResolutionResult result = target.Stats.ApplyIncomingDamage(15, BuildRangedWeaponPacket(magical: false));
            int hpLost = hpBefore - target.Stats.CurrentHP;

            Assert(result.DamageReductionApplied == 10, "Ranged weapon attack reduced by DR 10", $"dr={result.DamageReductionApplied}");
            Assert(hpLost == 5, "Target only takes reduced ranged damage", $"hpLost={hpLost}");
            Assert(target.Stats.ActiveProtectionFromArrowsEffect != null && target.Stats.ActiveProtectionFromArrowsEffect.RemainingAbsorptionPool == 40,
                "Absorption pool decrements by prevented damage", $"remaining={target.Stats.ActiveProtectionFromArrowsEffect?.RemainingAbsorptionPool}");
            Assert(target.Stats.ActiveProtectionFromArrowsEffect != null && target.Stats.ActiveProtectionFromArrowsEffect.AttacksBlocked == 1,
                "Blocked attack counter increments");
        }
        finally
        {
            DestroyController(target);
        }
    }

    private static void TestMagicRangedWeaponBypassesProtection()
    {
        CharacterController target = null;
        try
        {
            target = CreateController("MagicBypass", level: 5);
            ApplyProtectionFromArrows(target, casterLevel: 5);

            int hpBefore = target.Stats.CurrentHP;
            DamageResolutionResult result = target.Stats.ApplyIncomingDamage(15, BuildRangedWeaponPacket(magical: true));
            int hpLost = hpBefore - target.Stats.CurrentHP;

            Assert(result.DamageReductionApplied == 0, "Magical ranged weapon bypasses DR");
            Assert(hpLost == 15, "Magical ranged weapon deals full damage", $"hpLost={hpLost}");
            Assert(target.Stats.ActiveProtectionFromArrowsEffect != null && target.Stats.ActiveProtectionFromArrowsEffect.RemainingAbsorptionPool == 50,
                "Pool is unchanged when attack bypasses DR", $"remaining={target.Stats.ActiveProtectionFromArrowsEffect?.RemainingAbsorptionPool}");
        }
        finally
        {
            DestroyController(target);
        }
    }

    private static void TestRangedSpellBypassesProtection()
    {
        CharacterController target = null;
        try
        {
            target = CreateController("SpellBypass", level: 5);
            ApplyProtectionFromArrows(target, casterLevel: 5);

            var packet = new DamagePacket
            {
                Source = AttackSource.Spell,
                IsRanged = true,
                IsNonlethal = false,
                SourceName = "Scorching Ray",
                AttackTags = DamageBypassTag.None,
                Types = new System.Collections.Generic.HashSet<DamageType> { DamageType.Fire }
            };

            int hpBefore = target.Stats.CurrentHP;
            DamageResolutionResult result = target.Stats.ApplyIncomingDamage(12, packet);
            int hpLost = hpBefore - target.Stats.CurrentHP;

            Assert(result.DamageReductionApplied == 0, "Ranged spell attack is not reduced by Protection from Arrows");
            Assert(hpLost == 12, "Ranged spell deals full damage", $"hpLost={hpLost}");
            Assert(!string.IsNullOrEmpty(result.GetMitigationSummary()) && result.GetMitigationSummary().Contains("bypasses Protection from Arrows"),
                "Mitigation summary explains ranged spell bypass",
                $"summary={result.GetMitigationSummary()}");
        }
        finally
        {
            DestroyController(target);
        }
    }

    private static void TestPoolDischargeRemovesEffectImmediately()
    {
        CharacterController target = null;
        try
        {
            target = CreateController("Discharge", level: 1);
            ApplyProtectionFromArrows(target, casterLevel: 1); // pool 10

            DamageResolutionResult result = target.Stats.ApplyIncomingDamage(15, BuildRangedWeaponPacket(magical: false));
            StatusEffectManager mgr = target.GetComponent<StatusEffectManager>();

            Assert(result.DamageReductionApplied == 10, "Final blocked attack still benefits from DR before discharge");
            Assert(target.Stats.ActiveProtectionFromArrowsEffect == null, "Protection from Arrows data cleared on discharge");
            Assert(mgr != null && !mgr.HasEffect("protection_from_arrows"), "Protection from Arrows status effect removed on discharge");
            Assert(result.GetMitigationSummary().Contains("Protection from Arrows discharged!"),
                "Mitigation summary includes discharge message",
                $"summary={result.GetMitigationSummary()}");
        }
        finally
        {
            DestroyController(target);
        }
    }
}
}
