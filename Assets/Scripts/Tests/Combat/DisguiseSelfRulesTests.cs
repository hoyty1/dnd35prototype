using System.Collections.Generic;
using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Disguise Self spell implementation.
/// </summary>
public static class DisguiseSelfRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== DISGUISE SELF RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestDisguiseSelfSpellDefinition();
        TestRaceFilteringBySizeCategory();
        TestDisplayedRaceVsActualRaceTracking();
        TestDisguiseSkillBonusAppliedAndRemoved();
        TestDurationExpirationRevertsDisplayedRace();

        Debug.Log($"====== Disguise Self Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController CreateController(string name, string raceName)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: 3,
            characterClass: "Wizard",
            str: 10,
            dex: 12,
            con: 10,
            wis: 10,
            intelligence: 16,
            cha: 12,
            bab: 1,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 18,
            raceName: raceName);

        stats.InitializeSkills("Wizard", 3);

        GameObject go = new GameObject($"DisguiseSelf_{name}");
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

    private static void TestDisguiseSelfSpellDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell("disguise_self");
        Assert(spell != null, "Disguise Self definition exists");
        if (spell == null)
            return;

        Assert(!spell.IsPlaceholder, "Disguise Self is implemented");
        Assert(spell.School == "Illusion", "School is Illusion");
        Assert(spell.TargetType == SpellTargetType.Self, "Target is self");
        Assert(spell.RangeCategory == SpellRangeCategory.Personal, "Range is personal");
        Assert(spell.DurationType == DurationType.Minutes && spell.DurationValue == 10 && spell.DurationScalesWithLevel,
            "Duration is 10 min/level");
        Assert(spell.IsDismissible, "Spell is dismissible");
        Assert(spell.ClassList != null
            && System.Array.Exists(spell.ClassList, c => c == "Wizard")
            && System.Array.Exists(spell.ClassList, c => c == "Sorcerer")
            && System.Array.Exists(spell.ClassList, c => c == "Bard"),
            "Class list includes Wizard/Sorcerer/Bard");
    }

    private static void TestRaceFilteringBySizeCategory()
    {
        List<string> mediumRaces = RaceDatabase.GetRaceNamesBySizeCategory(SizeCategory.Medium);
        Assert(mediumRaces.Contains("Human"), "Medium race list contains Human");
        Assert(mediumRaces.Contains("Elf"), "Medium race list contains Elf");
        Assert(mediumRaces.Contains("Dwarf"), "Medium race list contains Dwarf");
        Assert(!mediumRaces.Contains("Halfling"), "Medium race list excludes Halfling");

        List<string> smallRaces = RaceDatabase.GetRaceNamesBySizeCategory(SizeCategory.Small);
        Assert(smallRaces.Contains("Halfling"), "Small race list contains Halfling");
        Assert(smallRaces.Contains("Gnome"), "Small race list contains Gnome");
        Assert(!smallRaces.Contains("Human"), "Small race list excludes Human");
    }

    private static void TestDisplayedRaceVsActualRaceTracking()
    {
        CharacterController controller = null;

        try
        {
            controller = CreateController("DisplayRaceTest", "Human");
            Assert(controller.ActualRace == "Human", "Actual race starts as Human", $"actual={controller.ActualRace}");
            Assert(controller.DisplayedRace == "Human", "Displayed race starts as Human", $"displayed={controller.DisplayedRace}");

            controller.ApplyDisguiseSelfEffect("Elf", 100, controller);
            Assert(controller.ActualRace == "Human", "Actual race remains unchanged while disguised", $"actual={controller.ActualRace}");
            Assert(controller.DisplayedRace == "Elf", "Displayed race changes to selected disguise", $"displayed={controller.DisplayedRace}");

            controller.ClearDisguiseSelfEffect();
            Assert(controller.DisplayedRace == "Human", "Displayed race reverts on clear", $"displayed={controller.DisplayedRace}");
        }
        finally
        {
            DestroyController(controller);
        }
    }

    private static void TestDisguiseSkillBonusAppliedAndRemoved()
    {
        CharacterController controller = null;

        try
        {
            controller = CreateController("SkillBonusTest", "Human");
            StatusEffectManager statusMgr = controller.GetComponent<StatusEffectManager>();
            SpellData spell = SpellDatabase.GetSpell("disguise_self");

            int baseline = controller.Stats.GetSkillBonus("Disguise");
            ActiveSpellEffect effect = statusMgr.AddEffect(spell, "Tester", 3);
            controller.ApplyDisguiseSelfEffect("Elf", effect != null ? effect.RemainingRounds : 0, controller);

            int boosted = controller.Stats.GetSkillBonus("Disguise");
            Assert(boosted - baseline == 10, "Disguise Self grants +10 competence to Disguise", $"baseline={baseline}, boosted={boosted}");

            statusMgr.RemoveEffectsBySpellId("disguise_self");
            int reverted = controller.Stats.GetSkillBonus("Disguise");
            Assert(reverted == baseline, "Disguise bonus removed when effect removed", $"baseline={baseline}, reverted={reverted}");
        }
        finally
        {
            DestroyController(controller);
        }
    }

    private static void TestDurationExpirationRevertsDisplayedRace()
    {
        CharacterController controller = null;

        try
        {
            controller = CreateController("DurationTest", "Human");
            StatusEffectManager statusMgr = controller.GetComponent<StatusEffectManager>();
            SpellData spell = SpellDatabase.GetSpell("disguise_self");

            ActiveSpellEffect effect = statusMgr.AddEffect(spell, "Tester", 1);
            controller.ApplyDisguiseSelfEffect("Elf", effect != null ? effect.RemainingRounds : 0, controller);

            Assert(controller.DisplayedRace == "Elf", "Precondition: race is disguised to Elf");

            int safety = 0;
            while (statusMgr.HasEffect("disguise_self") && safety < 120)
            {
                safety++;
                statusMgr.TickAllEffects();
            }

            Assert(!statusMgr.HasEffect("disguise_self"), "Disguise Self expires after duration ticks");
            Assert(controller.DisplayedRace == "Human", "Displayed race reverted after expiration", $"displayed={controller.DisplayedRace}");
        }
        finally
        {
            DestroyController(controller);
        }
    }
}
}
