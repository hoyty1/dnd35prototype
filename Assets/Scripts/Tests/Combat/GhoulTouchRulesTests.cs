using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Ghoul Touch spell mechanics.
/// D&D 3.5e PHB p.235: Necromancy, Sor/Wiz 2
/// Run with GhoulTouchRulesTests.RunAll().
/// </summary>
public static class GhoulTouchRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== GHOUL TOUCH RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestGhoulTouchDefinitionMatchesPHB();
        TestParalysisDurationRange();
        TestParalysisEffectMechanics();
        TestStenchAuraValidation();
        TestSickenedPenalties();
        TestTargetValidation_LivingHumanoidOnly();
        TestPoisonImmunityBlocksStench();

        Debug.Log($"====== Ghoul Touch Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats BuildStats(string name, string className, int level, int hitDice, string creatureType = "Humanoid")
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: 12,
            dex: 12,
            con: 12,
            wis: 12,
            intelligence: 12,
            cha: 12,
            bab: Mathf.Max(1, level / 2),
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 24,
            raceName: "Human");

        stats.CreatureType = creatureType;
        stats.HitDice = hitDice;
        return stats;
    }

    private static CharacterController CreateController(CharacterStats stats, CharacterTeam team, Vector2Int gridPos)
    {
        GameObject go = new GameObject($"GhoulTouchTest_{stats.CharacterName}");
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
        if (controller != null && controller.gameObject != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    // ======================== SPELL DEFINITION TESTS ========================

    private static void TestGhoulTouchDefinitionMatchesPHB()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.GHOUL_TOUCH);
        Assert(spell != null, "Ghoul Touch spell exists in database");
        if (spell == null) return;

        Assert(spell.Name == "Ghoul Touch", "Ghoul Touch name correct");
        Assert(spell.SpellLevel == 2, "Ghoul Touch is level 2");
        Assert(spell.School == "Necromancy", "Ghoul Touch school is Necromancy");
        Assert(spell.IsTouch, "Ghoul Touch is a touch spell");
        Assert(spell.IsMeleeTouch, "Ghoul Touch is a melee touch spell");
        Assert(spell.AllowsSavingThrow, "Ghoul Touch allows saving throw");
        Assert(spell.SavingThrowType == "Fortitude", "Ghoul Touch save is Fortitude");
        Assert(spell.EffectType == SpellEffectType.Debuff, "Ghoul Touch is a debuff");
        Assert(spell.TargetType == SpellTargetType.SingleEnemy, "Ghoul Touch targets single enemy");
        Assert(!spell.IsPlaceholder, "Ghoul Touch is not a placeholder",
            spell.IsPlaceholder ? $"PlaceholderReason: {spell.PlaceholderReason}" : "");

        // Check Sor/Wiz availability
        bool wizAvail = spell.IsAvailableFor("Wizard", 2);
        bool sorAvail = spell.IsAvailableFor("Sorcerer", 2);
        Assert(wizAvail, "Ghoul Touch available for Wizard at level 2");
        Assert(sorAvail, "Ghoul Touch available for Sorcerer at level 2");
    }

    // ======================== PARALYSIS DURATION TESTS ========================

    private static void TestParalysisDurationRange()
    {
        // 1d6+2 should always be in range 3-8
        for (int i = 0; i < 20; i++)
        {
            int duration = GhoulTouchEffectData.RollParalysisDuration();
            if (duration < 3 || duration > 8)
            {
                Assert(false, $"Paralysis duration in range 3-8", $"Got {duration}");
                return;
            }
        }
        Assert(true, "Paralysis duration always in range 3-8 (1d6+2)");

        // Test deterministic roll
        Assert(GhoulTouchEffectData.RollParalysisDuration(1) == 3, "1d6(1)+2 = 3");
        Assert(GhoulTouchEffectData.RollParalysisDuration(6) == 8, "1d6(6)+2 = 8");
        Assert(GhoulTouchEffectData.RollParalysisDuration(3) == 5, "1d6(3)+2 = 5");
    }

    // ======================== PARALYSIS MECHANICS TESTS ========================

    private static void TestParalysisEffectMechanics()
    {
        CharacterStats casterStats = BuildStats("Wizard", "Wizard", 3, 3);
        CharacterStats targetStats = BuildStats("Victim", "Fighter", 2, 2);

        CharacterController caster = CreateController(casterStats, CharacterTeam.Player, new Vector2Int(0, 0));
        CharacterController target = CreateController(targetStats, CharacterTeam.Enemy, new Vector2Int(1, 0));

        try
        {
            // Create effect with known duration
            GhoulTouchEffectData effect = GhoulTouchEffectData.CreateGhoulTouchWithDuration(5, caster, target);

            Assert(effect.IsParalyzed, "Effect starts as paralyzed");
            Assert(effect.IsStenchActive, "Stench aura starts active");
            Assert(effect.ParalysisDurationRounds == 5, "Duration is 5 rounds");
            Assert(effect.ParalysisRemainingRounds == 5, "Remaining is 5 rounds");
            Assert(effect.StenchRadiusFeet == 10, "Stench radius is 10 ft");
            Assert(effect.StenchRadiusSquares == 2, "Stench radius is 2 squares");

            // Test tick
            bool stillActive = effect.TickRound();
            Assert(stillActive, "Still active after 1 tick");
            Assert(effect.ParalysisRemainingRounds == 4, "Remaining is 4 after 1 tick");

            // Tick to expiry
            effect.TickRound(); // 3
            effect.TickRound(); // 2
            effect.TickRound(); // 1
            bool lastTick = effect.TickRound(); // 0 — expires
            Assert(!lastTick, "Expired after 5 ticks");
            Assert(!effect.IsParalyzed, "No longer paralyzed after expiry");
            Assert(!effect.IsStenchActive, "Stench inactive after expiry");

            // Apply to controller
            GhoulTouchEffectData effect2 = GhoulTouchEffectData.CreateGhoulTouchWithDuration(4, caster, target);
            target.ApplyGhoulTouchEffect(effect2);
            Assert(target.HasActiveGhoulTouchEffect, "Controller reports active Ghoul Touch");
            Assert(target.IsParalyzed(), "Controller reports paralyzed");
            Assert(target.HasCondition(CombatConditionType.Paralyzed), "Paralyzed condition applied");

            // Remove
            target.RemoveGhoulTouchEffect();
            Assert(!target.HasActiveGhoulTouchEffect, "Ghoul Touch removed");
            Assert(!target.IsParalyzed(), "No longer paralyzed after removal");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }

    // ======================== STENCH AURA TESTS ========================

    private static void TestStenchAuraValidation()
    {
        CharacterStats casterStats = BuildStats("Wizard", "Wizard", 3, 3);
        CharacterStats targetStats = BuildStats("Victim", "Fighter", 2, 2, "Humanoid");
        CharacterStats allyStats = BuildStats("Ally", "Fighter", 2, 2, "Humanoid");
        CharacterStats undeadStats = BuildStats("Zombie", "Monster", 2, 2, "Undead");

        CharacterController caster = CreateController(casterStats, CharacterTeam.Player, new Vector2Int(0, 0));
        CharacterController target = CreateController(targetStats, CharacterTeam.Enemy, new Vector2Int(1, 0));
        CharacterController ally = CreateController(allyStats, CharacterTeam.Player, new Vector2Int(2, 0));
        CharacterController undead = CreateController(undeadStats, CharacterTeam.Enemy, new Vector2Int(1, 1));

        try
        {
            GhoulTouchEffectData effect = GhoulTouchEffectData.CreateGhoulTouch(caster, target);

            Assert(!effect.IsValidStenchTarget(caster), "Caster is exempt from stench");
            Assert(!effect.IsValidStenchTarget(target), "Paralyzed target is not affected by own stench");
            Assert(effect.IsValidStenchTarget(ally), "Living ally can be affected by stench");
            Assert(!effect.IsValidStenchTarget(undead), "Undead immune to stench (not living)");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            DestroyController(ally);
            DestroyController(undead);
        }
    }

    // ======================== SICKENED PENALTIES TEST ========================

    private static void TestSickenedPenalties()
    {
        // Sickened condition: -2 to attacks, weapon damage, saves, skill checks, ability checks
        // This test validates the effect data expectations
        CharacterStats stats = BuildStats("SickenedGuy", "Fighter", 2, 2);
        CharacterController controller = CreateController(stats, CharacterTeam.Enemy, new Vector2Int(0, 0));

        try
        {
            // Apply sickened condition
            controller.ApplyCondition(CombatConditionType.Sickened, 3, "Ghoul Touch stench");
            Assert(controller.IsSickened(), "IsSickened() returns true after applying Sickened condition");
            Assert(controller.HasCondition(CombatConditionType.Sickened), "HasCondition(Sickened) returns true");
        }
        finally
        {
            DestroyController(controller);
        }
    }

    // ======================== TARGET VALIDATION TESTS ========================

    private static void TestTargetValidation_LivingHumanoidOnly()
    {
        CharacterStats casterStats = BuildStats("Wizard", "Wizard", 3, 3);
        CharacterController caster = CreateController(casterStats, CharacterTeam.Player, new Vector2Int(0, 0));

        try
        {
            // Living humanoid: valid
            GhoulTouchEffectData effectLiving = GhoulTouchEffectData.CreateGhoulTouch(caster, null);
            Assert(effectLiving != null, "Ghoul Touch effect can be created for null target (factory doesn't validate target type)");

            // Test stench target validation
            CharacterStats humanoidStats = BuildStats("Humanoid", "Fighter", 2, 2, "Humanoid");
            CharacterController humanoid = CreateController(humanoidStats, CharacterTeam.Enemy, new Vector2Int(1, 0));

            CharacterStats constructStats = BuildStats("Golem", "Monster", 4, 4, "Construct");
            CharacterController construct = CreateController(constructStats, CharacterTeam.Enemy, new Vector2Int(2, 0));

            CharacterStats animalStats = BuildStats("Wolf", "Monster", 2, 2, "Animal");
            CharacterController animal = CreateController(animalStats, CharacterTeam.Enemy, new Vector2Int(3, 0));

            GhoulTouchEffectData effect = GhoulTouchEffectData.CreateGhoulTouch(caster, humanoid);

            Assert(effect.IsValidStenchTarget(humanoid), "Living humanoid is a valid stench target");
            Assert(!effect.IsValidStenchTarget(construct), "Construct is not a valid stench target");
            Assert(effect.IsValidStenchTarget(animal), "Living animal IS a valid stench target (stench affects all living, not just humanoids)");

            DestroyController(humanoid);
            DestroyController(construct);
            DestroyController(animal);
        }
        finally
        {
            DestroyController(caster);
        }
    }

    // ======================== POISON IMMUNITY TEST ========================

    private static void TestPoisonImmunityBlocksStench()
    {
        CharacterStats casterStats = BuildStats("Wizard", "Wizard", 3, 3);
        CharacterController caster = CreateController(casterStats, CharacterTeam.Player, new Vector2Int(0, 0));

        CharacterStats poisonImmuneStats = BuildStats("PoisonImmune", "Fighter", 2, 2, "Humanoid");
        poisonImmuneStats.SpecialAbilities = new System.Collections.Generic.List<string> { "Poison Immunity" };
        CharacterController poisonImmune = CreateController(poisonImmuneStats, CharacterTeam.Enemy, new Vector2Int(1, 0));

        CharacterStats normalStats = BuildStats("Normal", "Fighter", 2, 2, "Humanoid");
        CharacterController normal = CreateController(normalStats, CharacterTeam.Enemy, new Vector2Int(2, 0));

        try
        {
            GhoulTouchEffectData effect = GhoulTouchEffectData.CreateGhoulTouch(caster, normal);

            Assert(effect.IsCreaturePoisonImmune(poisonImmune), "Creature with Poison Immunity is immune to stench");
            Assert(!effect.IsCreaturePoisonImmune(normal), "Normal creature is not immune to stench");

            // Undead are inherently poison immune
            CharacterStats undeadStats = BuildStats("Skeleton", "Monster", 1, 1, "Undead");
            CharacterController undead = CreateController(undeadStats, CharacterTeam.Enemy, new Vector2Int(3, 0));
            Assert(effect.IsCreaturePoisonImmune(undead), "Undead are immune to poison (stench)");

            DestroyController(undead);
        }
        finally
        {
            DestroyController(caster);
            DestroyController(poisonImmune);
            DestroyController(normal);
        }
    }
}
}
