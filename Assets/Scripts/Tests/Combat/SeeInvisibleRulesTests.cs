using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for See Invisible (PHB 3.5e) and invisibility interaction rules.
/// </summary>
public static class SeeInvisibleRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void see_invisible_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SEE INVISIBLE RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestSpellDefinitionAndAliases();
        TestDurationScaling();
        TestInvisibilityPenaltiesRemovedForSeer();
        TestDismissAndExpirationFlow();
        TestDoesNotBypassOtherConcealment();

        Debug.Log($"====== See Invisible Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController CreateController(string name, string className = "Wizard", int level = 5)
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
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 30,
            raceName: "Human");

        stats.InitializeSkills(className, level);

        GameObject go = new GameObject($"SeeInvisible_{name}");
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

    private static void TestSpellDefinitionAndAliases()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.SEE_INVISIBLE);
        Assert(spell != null, "See Invisible definition exists");
        if (spell == null)
            return;

        Assert(spell.SpellId == SpellNames.SEE_INVISIBLE, "Spell ID is see_invisible", $"id={spell.SpellId}");
        Assert(string.Equals(spell.Name, "See Invisible", System.StringComparison.Ordinal), "Spell name is See Invisible", $"name={spell.Name}");
        Assert(spell.School == "Divination", "School is Divination");
        Assert(spell.TargetType == SpellTargetType.Self, "Target type is Self (personal)");
        Assert(spell.RangeCategory == SpellRangeCategory.Personal, "Range is personal");
        Assert(spell.ActionType == SpellActionType.Standard, "Casting time is standard action");
        Assert(spell.DurationType == DurationType.Minutes && spell.DurationValue == 10 && spell.DurationScalesWithLevel,
            "Duration is 10 min/level");
        Assert(spell.IsDismissible, "Spell is dismissible");

        Assert(spell.GetSpellLevelFor("Wizard") == 2, "Wizard spell level is 2");
        Assert(spell.GetSpellLevelFor("Sorcerer") == 2, "Sorcerer spell level is 2");
        Assert(spell.GetSpellLevelFor("Bard") == 3, "Bard spell level is 3");

        SpellData legacyLookup = SpellDatabase.GetSpell(SpellNames.SEE_INVISIBILITY_LEGACY);
        Assert(legacyLookup != null && legacyLookup.SpellId == SpellNames.SEE_INVISIBLE,
            "Legacy see_invisibility id resolves to canonical see_invisible");
    }

    private static void TestDurationScaling()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.SEE_INVISIBLE);
        Assert(spell != null, "See Invisible spell available for duration check");
        if (spell == null)
            return;

        int cl3 = ActiveSpellEffect.CalculateDurationRounds(spell, 3);
        int cl5 = ActiveSpellEffect.CalculateDurationRounds(spell, 5);
        int cl10 = ActiveSpellEffect.CalculateDurationRounds(spell, 10);

        Assert(cl3 == 300, "CL3 duration is 300 rounds (30 minutes)", $"observed={cl3}");
        Assert(cl5 == 500, "CL5 duration is 500 rounds (50 minutes)", $"observed={cl5}");
        Assert(cl10 == 1000, "CL10 duration is 1000 rounds (100 minutes)", $"observed={cl10}");
    }

    private static void TestInvisibilityPenaltiesRemovedForSeer()
    {
        CharacterController caster = null;
        CharacterController invisibleTarget = null;
        CharacterController normalAttacker = null;
        CharacterController seer = null;

        try
        {
            caster = CreateController("Caster", "Wizard", 5);
            invisibleTarget = CreateController("InvisibleTarget", "Rogue", 5);
            normalAttacker = CreateController("NormalAttacker", "Fighter", 5);
            seer = CreateController("Seer", "Wizard", 5);

            ActiveSpellEffect invis = ApplySpell(invisibleTarget, caster, SpellNames.INVISIBILITY, 5);
            if (invis != null)
                invisibleTarget.ApplyInvisibilityEffect(invis.RemainingRounds, caster, false);

            ActiveSpellEffect seeInvisible = ApplySpell(seer, seer, SpellNames.SEE_INVISIBLE, 5);
            if (seeInvisible != null)
                seer.ApplySeeInvisibilityEffect(seeInvisible.RemainingRounds, seer);

            Assert(invisibleTarget.GetMissChance(normalAttacker, false) == 50,
                "Attacker without See Invisible has 50% miss chance vs invisible target");
            Assert(invisibleTarget.GetInvisibilityArmorClassBonusAgainst(normalAttacker) == 2,
                "Attacker without See Invisible faces +2 AC bonus from invisibility");

            Assert(seer.CanSeeInvisible(invisibleTarget), "Seer can see invisible target");
            Assert(seer.CanSee(invisibleTarget, false), "Seer can directly target invisible creature");
            Assert(invisibleTarget.GetMissChance(seer, false) == 0,
                "Seer ignores invisibility miss chance");
            Assert(invisibleTarget.GetInvisibilityArmorClassBonusAgainst(seer) == 0,
                "Seer ignores invisibility +2 AC bonus");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(invisibleTarget);
            DestroyController(normalAttacker);
            DestroyController(seer);
        }
    }

    private static void TestDismissAndExpirationFlow()
    {
        CharacterController caster = null;

        try
        {
            caster = CreateController("DismissTester", "Wizard", 5);
            ActiveSpellEffect effect = ApplySpell(caster, caster, SpellNames.SEE_INVISIBLE, 5);
            if (effect != null)
                caster.ApplySeeInvisibilityEffect(effect.RemainingRounds, caster);

            StatusEffectManager statusMgr = caster.GetComponent<StatusEffectManager>();
            Assert(caster.HasActiveSeeInvisibilityEffect, "See Invisible effect is active after apply");
            Assert(statusMgr != null && statusMgr.HasEffect(SpellNames.SEE_INVISIBLE), "Status manager tracks See Invisible effect");

            if (statusMgr != null)
                statusMgr.RemoveEffectsBySpellId(SpellNames.SEE_INVISIBLE);
            caster.ClearSeeInvisibilityEffect();

            Assert(!caster.HasActiveSeeInvisibilityEffect, "See Invisible effect clears on dismiss");
            Assert(statusMgr == null || !statusMgr.HasEffect(SpellNames.SEE_INVISIBLE), "Status manager effect removed on dismiss");
        }
        finally
        {
            DestroyController(caster);
        }
    }

    private static void TestDoesNotBypassOtherConcealment()
    {
        CharacterController seer = null;
        CharacterController target = null;

        try
        {
            seer = CreateController("ConcealmentSeer", "Wizard", 5);
            target = CreateController("ConcealmentTarget", "Rogue", 5);

            ActiveSpellEffect seeInvisible = ApplySpell(seer, seer, SpellNames.SEE_INVISIBLE, 5);
            if (seeInvisible != null)
                seer.ApplySeeInvisibilityEffect(seeInvisible.RemainingRounds, seer);

            StatusEffectManager targetStatus = target.GetComponent<StatusEffectManager>();
            var blurLikeConcealment = new ActiveSpellEffect
            {
                Spell = new SpellData { SpellId = "unit_test_blur", Name = "Unit Test Blur" },
                CasterName = "UnitTest",
                CasterLevel = 5,
                RemainingRounds = 5,
                BonusTypeLegacy = "Concealment",
                BonusTypeEnum = BonusType.Concealment,
                IsApplied = true,
                MissChance = 20,
                IsTotalConcealment = false,
                ConcealmentSource = "Unit Test Blur"
            };

            if (targetStatus != null)
                targetStatus.ActiveEffects.Add(blurLikeConcealment);

            Assert(target.GetMissChance(seer, false) == 20,
                "See Invisible does not remove non-invisibility concealment effects");
        }
        finally
        {
            DestroyController(seer);
            DestroyController(target);
        }
    }
}
}
