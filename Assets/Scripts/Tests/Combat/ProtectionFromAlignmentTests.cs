using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Focused regression checks for Protection from Alignment behavior.
/// Run with ProtectionFromAlignmentTests.RunAll().
/// </summary>
public static class ProtectionFromAlignmentTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== PROTECTION FROM ALIGNMENT TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestSpellDefinitionsExistAndDurationScalePerMinutePerLevel();
        TestProtectionBenefitsOnlyApplyAgainstMatchingAlignment();
        TestProtectionAddsPlusTwoSaveVsMatchingAlignedSpells();
        TestProtectionBlocksMindAffectingFromMatchingAlignment();
        TestProtectionAddsPlusTwoDeflectionAcVsMatchingAlignedAttackers();
        TestProtectionBlocksSummonedMeleeBodilyContact();

        Debug.Log($"====== Protection From Alignment Results: {_passed} passed, {_failed} failed ======");
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
        string className,
        int level,
        Alignment alignment,
        int str,
        int dex,
        int con,
        int wis,
        int intelligence,
        int cha,
        int bab)
    {
        var stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: str, dex: dex, con: con, wis: wis, intelligence: intelligence, cha: cha,
            bab: bab,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 30,
            raceName: "Human");

        stats.CharacterAlignment = alignment;
        return stats;
    }

    private static CharacterController CreateController(CharacterStats stats)
    {
        var go = new GameObject($"ProtectionTest_{stats.CharacterName}");
        var controller = go.AddComponent<CharacterController>();
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
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
    }

    private static void TestSpellDefinitionsExistAndDurationScalePerMinutePerLevel()
    {
        string[] ids = { "protection_from_evil", "protection_from_good", "protection_from_law", "protection_from_chaos" };

        for (int i = 0; i < ids.Length; i++)
        {
            SpellData spell = SpellDatabase.GetSpell(ids[i]);
            Assert(spell != null, $"{ids[i]} definition exists");
            if (spell == null)
                continue;

            Assert(!spell.IsPlaceholder, $"{ids[i]} is not placeholder");
            Assert(spell.DurationType == DurationType.Minutes && spell.DurationValue == 1 && spell.DurationScalesWithLevel,
                $"{ids[i]} duration is 1 minute/level",
                $"durationType={spell.DurationType}, durationValue={spell.DurationValue}, scales={spell.DurationScalesWithLevel}");

            int roundsAt5 = ActiveSpellEffect.CalculateDurationRounds(spell, 5);
            Assert(roundsAt5 == 50,
                $"{ids[i]} scales to 50 rounds at CL 5",
                $"expected 50, got {roundsAt5}");
        }
    }

    private static void TestProtectionBenefitsOnlyApplyAgainstMatchingAlignment()
    {
        CharacterController target = null;
        try
        {
            target = CreateController(BuildStats("WardTarget", "Wizard", 5, Alignment.TrueNeutral, 10, 12, 12, 14, 16, 10, 2));
            SpellData protection = SpellDatabase.GetSpell("protection_from_evil");
            ActiveSpellEffect effect = target.GetComponent<StatusEffectManager>().AddEffect(protection, "Tester", 5);
            Assert(effect != null, "Protection from Evil effect applied");

            AlignmentProtectionBenefits vsEvil = AlignmentProtectionRules.GetBenefitsAgainst(target, Alignment.NeutralEvil);
            AlignmentProtectionBenefits vsNeutral = AlignmentProtectionRules.GetBenefitsAgainst(target, Alignment.TrueNeutral);

            Assert(vsEvil.HasMatch, "Protection matches evil source alignment");
            Assert(vsEvil.DeflectionAcBonus == 2, "Protection grants +2 AC against matching alignment", $"expected 2, got {vsEvil.DeflectionAcBonus}");
            Assert(vsEvil.ResistanceSaveBonus == 2, "Protection grants +2 saves against matching alignment", $"expected 2, got {vsEvil.ResistanceSaveBonus}");

            Assert(!vsNeutral.HasMatch, "Protection does not match neutral source alignment");
            Assert(vsNeutral.DeflectionAcBonus == 0 && vsNeutral.ResistanceSaveBonus == 0,
                "Protection gives no conditional bonuses when alignment does not match",
                $"ac={vsNeutral.DeflectionAcBonus}, save={vsNeutral.ResistanceSaveBonus}");
        }
        finally
        {
            DestroyController(target);
        }
    }

    private static void TestProtectionAddsPlusTwoSaveVsMatchingAlignedSpells()
    {
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("EvilCleric", "Cleric", 5, Alignment.NeutralEvil, 10, 10, 12, 16, 12, 10, 3));
            target = CreateController(BuildStats("ProtectedAlly", "Wizard", 5, Alignment.TrueNeutral, 10, 12, 12, 14, 16, 10, 2));

            SpellData protection = SpellDatabase.GetSpell("protection_from_evil");
            SpellData command = SpellDatabase.GetSpell("command");

            target.GetComponent<StatusEffectManager>().AddEffect(protection, "AllyCaster", 5);

            SpellResult result = SpellCaster.Cast(
                command,
                caster.Stats,
                target.Stats,
                metamagic: null,
                forceFriendlyTouchNoRoll: false,
                forceTargetToFailSave: false,
                casterController: caster,
                targetController: target);

            Assert(result.RequiredSave, "Command required a save for protected target");
            Assert(result.ProtectionSaveBonus == 2, "Protection contributes +2 save bonus in spell result", $"expected 2, got {result.ProtectionSaveBonus}");
            Assert(result.SaveMod >= target.Stats.WillSave + 2,
                "Spell save modifier includes protection bonus",
                $"expected at least {target.Stats.WillSave + 2}, got {result.SaveMod}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }

    private static void TestProtectionBlocksMindAffectingFromMatchingAlignment()
    {
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("EvilEnchanter", "Wizard", 5, Alignment.NeutralEvil, 8, 14, 12, 12, 18, 10, 2));
            target = CreateController(BuildStats("WardedTarget", "Wizard", 5, Alignment.TrueNeutral, 10, 12, 12, 14, 16, 10, 2));

            SpellData protection = SpellDatabase.GetSpell("protection_from_evil");
            SpellData charmPerson = SpellDatabase.GetSpell("charm_person");
            target.GetComponent<StatusEffectManager>().AddEffect(protection, "AllyCaster", 5);

            SpellResult result = SpellCaster.Cast(
                charmPerson,
                caster.Stats,
                target.Stats,
                metamagic: null,
                forceFriendlyTouchNoRoll: false,
                forceTargetToFailSave: false,
                casterController: caster,
                targetController: target);

            Assert(result.MindAffectingBlockedByProtection,
                "Protection blocks mind-affecting spell from matching aligned source");
            Assert(!result.Success, "Mind-affecting spell marked unsuccessful when blocked by protection");
            Assert(!string.IsNullOrEmpty(result.NoEffectReason) && result.NoEffectReason.ToLowerInvariant().Contains("blocks mental control"),
                "Protection block provides no-effect reason",
                $"reason={result.NoEffectReason}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }

    private static void TestProtectionAddsPlusTwoDeflectionAcVsMatchingAlignedAttackers()
    {
        CharacterController evilAttacker = null;
        CharacterController neutralAttacker = null;
        CharacterController target = null;

        try
        {
            target = CreateController(BuildStats("WardedDefender", "Wizard", 5, Alignment.TrueNeutral, 10, 12, 12, 14, 16, 10, 2));
            evilAttacker = CreateController(BuildStats("EvilMelee", "Fighter", 5, Alignment.NeutralEvil, 16, 12, 14, 10, 10, 8, 5));
            neutralAttacker = CreateController(BuildStats("NeutralMelee", "Fighter", 5, Alignment.TrueNeutral, 16, 12, 14, 10, 10, 8, 5));

            SpellData protection = SpellDatabase.GetSpell("protection_from_evil");
            target.GetComponent<StatusEffectManager>().AddEffect(protection, "AllyCaster", 5);

            CombatResult evilAttack = evilAttacker.Attack(target);
            CombatResult neutralAttack = neutralAttacker.Attack(target);

            Assert(evilAttack.ProtectionDeflectionBonusToAc == 2,
                "Matching aligned attacker sees +2 deflection AC from protection",
                $"expected 2, got {evilAttack.ProtectionDeflectionBonusToAc}");
            Assert(evilAttack.TargetAC == neutralAttack.TargetAC + 2,
                "Matching aligned attack target AC is 2 higher than non-matching aligned attack",
                $"evilAC={evilAttack.TargetAC}, neutralAC={neutralAttack.TargetAC}");
        }
        finally
        {
            DestroyController(evilAttacker);
            DestroyController(neutralAttacker);
            DestroyController(target);
        }
    }

    private static void TestProtectionBlocksSummonedMeleeBodilyContact()
    {
        GameManager gm = null;
        CharacterController summonAttacker = null;
        CharacterController summonCaster = null;
        CharacterController target = null;

        try
        {
            var gmGo = new GameObject("ProtectionTest_GameManager");
            gm = gmGo.AddComponent<GameManager>();

            target = CreateController(BuildStats("ProtectedDefender", "Wizard", 5, Alignment.TrueNeutral, 10, 12, 12, 14, 16, 10, 2));
            summonAttacker = CreateController(BuildStats("SummonedFiend", "Fighter", 5, Alignment.NeutralEvil, 16, 12, 14, 10, 10, 8, 5));
            summonCaster = CreateController(BuildStats("Summoner", "Wizard", 5, Alignment.NeutralEvil, 8, 14, 12, 12, 18, 10, 2));

            SpellData protection = SpellDatabase.GetSpell("protection_from_evil");
            target.GetComponent<StatusEffectManager>().AddEffect(protection, "AllyCaster", 5);

            RegisterAsActiveSummon(gm, summonAttacker, summonCaster);

            CombatResult attack = summonAttacker.Attack(target);
            Assert(attack.ProtectionSummonedBarrierBlocked,
                "Protection barrier blocks bodily contact from summoned matching-aligned melee attacker");
            Assert(!attack.Hit, "Summoned contact-blocked melee attack is treated as miss/no contact");
            Assert(!string.IsNullOrEmpty(attack.ProtectionBarrierNote), "Summoned contact block provides barrier note");
        }
        finally
        {
            DestroyController(summonAttacker);
            DestroyController(summonCaster);
            DestroyController(target);
            if (gm != null)
                UnityEngine.Object.DestroyImmediate(gm.gameObject);
        }
    }

    private static void RegisterAsActiveSummon(GameManager gameManager, CharacterController summon, CharacterController caster)
    {
        Type gmType = typeof(GameManager);
        Type summonType = gmType.GetNestedType("ActiveSummonInstance", BindingFlags.NonPublic);
        Assert(summonType != null, "Reflection found ActiveSummonInstance nested type");
        if (summonType == null)
            return;

        object summonEntry = Activator.CreateInstance(summonType);

        FieldInfo controllerField = summonType.GetField("Controller", BindingFlags.Instance | BindingFlags.Public);
        FieldInfo casterField = summonType.GetField("Caster", BindingFlags.Instance | BindingFlags.Public);
        FieldInfo remainingField = summonType.GetField("RemainingRounds", BindingFlags.Instance | BindingFlags.Public);
        FieldInfo totalField = summonType.GetField("TotalDurationRounds", BindingFlags.Instance | BindingFlags.Public);
        FieldInfo alliedField = summonType.GetField("IsAlliedToPCs", BindingFlags.Instance | BindingFlags.Public);

        controllerField?.SetValue(summonEntry, summon);
        casterField?.SetValue(summonEntry, caster);
        remainingField?.SetValue(summonEntry, 5);
        totalField?.SetValue(summonEntry, 5);
        alliedField?.SetValue(summonEntry, true);

        FieldInfo activeListField = gmType.GetField("_activeSummons", BindingFlags.Instance | BindingFlags.NonPublic);
        object activeList = activeListField?.GetValue(gameManager);
        MethodInfo addMethod = activeList?.GetType().GetMethod("Add");
        addMethod?.Invoke(activeList, new[] { summonEntry });

        bool registered = gameManager.IsSummonedCreature(summon);
        Assert(registered, "Reflection successfully registered summoned creature in GameManager active summons list");
    }
}
}
