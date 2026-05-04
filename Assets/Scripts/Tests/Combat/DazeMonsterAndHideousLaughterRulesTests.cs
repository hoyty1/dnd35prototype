using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Regression checks for Daze Monster and Tasha's Hideous Laughter.
/// Run with DazeMonsterAndHideousLaughterRulesTests.RunAll().
/// </summary>
public static class DazeMonsterAndHideousLaughterRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== DAZE MONSTER + TASHA'S HIDEOUS LAUGHTER RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        TestDazeMonsterDefinition();
        TestDazeMonsterHighHdImmunity();
        TestDazeMonsterNonLivingImmunity();

        TestTashasDefinition();
        TestTashasIntImmunity();
        TestTashasDifferentCreatureTypeSaveBonus();
        TestTashasDurationScalesWithLevel();
        TestTashasSpellResistanceCheck();
        TestHideousLaughterConditionDefinition();
        TestHideousLaughterApplysConditionAndProne();

        Debug.Log($"====== Daze Monster + Tasha's Hideous Laughter Results: {_passed} passed, {_failed} failed ======");
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
        int hitDice,
        string creatureType = "Humanoid",
        int intelligence = 12)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: 12,
            dex: 12,
            con: 12,
            wis: 12,
            intelligence: intelligence,
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
        GameObject go = new GameObject($"DazeLaughRules_{stats.CharacterName}");
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
        if (controller != null)
            Object.DestroyImmediate(controller.gameObject);
    }

    private static void TestDazeMonsterDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.DAZE_MONSTER);
        Assert(spell != null, "Daze Monster spell definition exists");
        if (spell == null)
            return;

        Assert(spell.TargetType == SpellTargetType.SingleEnemy, "Daze Monster targets one creature");
        Assert(spell.RangeCategory == SpellRangeCategory.Medium, "Daze Monster uses Medium range");
        Assert(spell.AllowsSavingThrow && spell.SavingThrowType == "Will", "Daze Monster uses Will negates");
        Assert(spell.SpellResistanceApplies, "Daze Monster allows spell resistance");
        Assert(spell.IsMindAffecting, "Daze Monster is mind-affecting");
        Assert(spell.DurationType == DurationType.Rounds && spell.DurationValue == 1 && !spell.DurationScalesWithLevel,
            "Daze Monster duration is fixed at 1 round");
    }

    private static void TestDazeMonsterHighHdImmunity()
    {
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Caster", "Wizard", 5, 5), CharacterTeam.Player, new Vector2Int(1, 1));
            target = CreateController(BuildStats("Ogre", "Fighter", 7, 7), CharacterTeam.Enemy, new Vector2Int(2, 1));

            SpellData spell = SpellDatabase.GetSpell(SpellNames.DAZE_MONSTER);
            SpellResult result = SpellCaster.Cast(spell, caster.Stats, target.Stats, null, false, false, caster, target);

            Assert(!result.Success, "Daze Monster fails against 7+ HD targets");
            Assert(!string.IsNullOrWhiteSpace(result.NoEffectReason) && result.NoEffectReason.Contains("Immune (7+ HD)"),
                "Daze Monster logs HD immunity message",
                $"reason={result.NoEffectReason}");
            Assert(!result.RequiredSave, "Daze Monster HD immunity bypasses saving throw");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }

    private static void TestDazeMonsterNonLivingImmunity()
    {
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Caster", "Wizard", 5, 5), CharacterTeam.Player, new Vector2Int(1, 1));
            target = CreateController(BuildStats("Skeleton", "Warrior", 3, 3, creatureType: "Undead"), CharacterTeam.Enemy, new Vector2Int(2, 1));

            SpellData spell = SpellDatabase.GetSpell(SpellNames.DAZE_MONSTER);
            SpellResult result = SpellCaster.Cast(spell, caster.Stats, target.Stats, null, false, false, caster, target);

            Assert(!result.Success, "Daze Monster fails against non-living creatures");
            Assert(!string.IsNullOrWhiteSpace(result.NoEffectReason) && result.NoEffectReason.Contains("not a living creature"),
                "Daze Monster logs living-creature immunity",
                $"reason={result.NoEffectReason}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }

    private static void TestTashasDefinition()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.HIDEOUS_LAUGHTER);
        Assert(spell != null, "Tasha's Hideous Laughter spell definition exists");
        if (spell == null)
            return;

        Assert(string.Equals(spell.SpellId, "tashas_hideous_laughter", System.StringComparison.Ordinal),
            "Tasha's Hideous Laughter uses canonical spell id");
        Assert(spell.TargetType == SpellTargetType.SingleEnemy, "Tasha's targets one creature");
        Assert(spell.RangeCategory == SpellRangeCategory.Close, "Tasha's uses Close range");
        Assert(spell.AllowsSavingThrow && spell.SavingThrowType == "Will", "Tasha's uses Will negates");
        Assert(spell.SpellResistanceApplies, "Tasha's allows spell resistance");
        Assert(spell.IsMindAffecting, "Tasha's is mind-affecting");
        Assert(spell.DurationType == DurationType.Rounds && spell.DurationValue == 1 && spell.DurationScalesWithLevel,
            "Tasha's duration is 1 round/level");
    }

    private static void TestTashasIntImmunity()
    {
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Caster", "Wizard", 5, 5), CharacterTeam.Player, new Vector2Int(1, 1));
            target = CreateController(BuildStats("Lizard", "Animal", 3, 3, creatureType: "Animal", intelligence: 2), CharacterTeam.Enemy, new Vector2Int(2, 1));

            SpellData spell = SpellDatabase.GetSpell(SpellNames.HIDEOUS_LAUGHTER);
            SpellResult result = SpellCaster.Cast(spell, caster.Stats, target.Stats, null, false, false, caster, target);

            Assert(!result.Success, "Tasha's fails against Int 2 or less targets");
            Assert(!string.IsNullOrWhiteSpace(result.NoEffectReason) && result.NoEffectReason.Contains("Immune (Int 2 or less)"),
                "Tasha's logs Int-based immunity",
                $"reason={result.NoEffectReason}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }

    private static void TestTashasDifferentCreatureTypeSaveBonus()
    {
        CharacterController caster = null;
        CharacterController sameTypeTarget = null;
        CharacterController differentTypeTarget = null;

        try
        {
            caster = CreateController(BuildStats("Caster", "Wizard", 5, 5, creatureType: "Humanoid", intelligence: 16), CharacterTeam.Player, new Vector2Int(1, 1));
            sameTypeTarget = CreateController(BuildStats("Guard", "Fighter", 4, 4, creatureType: "Humanoid", intelligence: 12), CharacterTeam.Enemy, new Vector2Int(2, 1));
            differentTypeTarget = CreateController(BuildStats("Aberrant", "Monster", 4, 4, creatureType: "Aberration", intelligence: 12), CharacterTeam.Enemy, new Vector2Int(3, 1));

            SpellData spell = SpellDatabase.GetSpell(SpellNames.HIDEOUS_LAUGHTER);
            SpellResult sameTypeResult = SpellCaster.Cast(spell, caster.Stats, sameTypeTarget.Stats, null, false, false, caster, sameTypeTarget);
            SpellResult differentTypeResult = SpellCaster.Cast(spell, caster.Stats, differentTypeTarget.Stats, null, false, false, caster, differentTypeTarget);

            Assert(sameTypeResult.RequiredSave && differentTypeResult.RequiredSave, "Tasha's requires save in both comparison scenarios");
            Assert(differentTypeResult.SituationalSaveBonus == 4, "Different creature type gets +4 save bonus",
                $"bonus={differentTypeResult.SituationalSaveBonus}");
            Assert(sameTypeResult.SituationalSaveBonus == 0, "Same creature type gets no save bonus",
                $"bonus={sameTypeResult.SituationalSaveBonus}");
            Assert(differentTypeResult.SaveMod - sameTypeResult.SaveMod == 4,
                "Save modifier increases by 4 for different creature type",
                $"same={sameTypeResult.SaveMod}, different={differentTypeResult.SaveMod}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(sameTypeTarget);
            DestroyController(differentTypeTarget);
        }
    }

    private static void TestTashasDurationScalesWithLevel()
    {
        SpellData spell = SpellDatabase.GetSpell(SpellNames.HIDEOUS_LAUGHTER);
        Assert(spell != null, "Precondition: Tasha's spell exists for duration test");
        if (spell == null)
            return;

        Assert(ActiveSpellEffect.CalculateDurationRounds(spell, 3) == 3,
            "Tasha's duration = 3 rounds at caster level 3",
            $"actual={ActiveSpellEffect.CalculateDurationRounds(spell, 3)}");
        Assert(ActiveSpellEffect.CalculateDurationRounds(spell, 10) == 10,
            "Tasha's duration = 10 rounds at caster level 10",
            $"actual={ActiveSpellEffect.CalculateDurationRounds(spell, 10)}");
    }

    private static void TestTashasSpellResistanceCheck()
    {
        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Caster", "Wizard", 5, 5), CharacterTeam.Player, new Vector2Int(1, 1));
            target = CreateController(BuildStats("SR Target", "Monster", 4, 4, creatureType: "Humanoid", intelligence: 12), CharacterTeam.Enemy, new Vector2Int(2, 1));
            target.Stats.SpellResistance = 999;

            SpellData spell = SpellDatabase.GetSpell(SpellNames.HIDEOUS_LAUGHTER);
            SpellResult result = SpellCaster.Cast(spell, caster.Stats, target.Stats, null, false, false, caster, target);

            Assert(result.SpellResistanceChecked, "Tasha's performs spell resistance check");
            Assert(!result.SpellResistancePassed, "Very high SR blocks Tasha's");
            Assert(!result.Success, "Tasha's fails when SR blocks");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
        }
    }

    private static void TestHideousLaughterConditionDefinition()
    {
        ConditionDefinition def = ConditionRules.GetDefinition(CombatConditionType.HideousLaughter);
        Assert(def != null, "Hideous Laughter condition definition exists");
        if (def == null)
            return;

        Assert(def.PreventsMovement && def.PreventsStandardActions && def.PreventsFullRoundActions,
            "Hideous Laughter blocks movement and actions");
        Assert(!def.DeniesDexToAc && !def.CoupDeGraceVulnerable,
            "Hideous Laughter does not make target helpless",
            $"deniesDex={def.DeniesDexToAc}, cdg={def.CoupDeGraceVulnerable}");
    }

    private static void TestHideousLaughterApplysConditionAndProne()
    {
        GameObject gmObject = new GameObject("DazeLaughRules_GM");
        GameManager gm = gmObject.AddComponent<GameManager>();
        ConditionService conditionService = gmObject.AddComponent<ConditionService>();

        CharacterController caster = null;
        CharacterController target = null;

        try
        {
            caster = CreateController(BuildStats("Caster", "Wizard", 4, 4), CharacterTeam.Player, new Vector2Int(1, 1));
            target = CreateController(BuildStats("Target", "Fighter", 4, 4), CharacterTeam.Enemy, new Vector2Int(2, 1));
            gm.PCs.Add(caster);
            gm.NPCs.Add(target);

            conditionService.Initialize(() =>
            {
                var all = new List<CharacterController>();
                all.AddRange(gm.PCs);
                all.AddRange(gm.NPCs);
                return all;
            });

            FieldInfo conditionField = typeof(GameManager).GetField("_conditionService", BindingFlags.NonPublic | BindingFlags.Instance);
            conditionField?.SetValue(gm, conditionService);

            MethodInfo applyBuff = typeof(GameManager).GetMethod("ApplySpellBuff", BindingFlags.NonPublic | BindingFlags.Instance);
            SpellData spell = SpellDatabase.GetSpell(SpellNames.HIDEOUS_LAUGHTER);
            applyBuff?.Invoke(gm, new object[] { caster, target, spell, null });

            Assert(conditionService.HasCondition(target, CombatConditionType.HideousLaughter),
                "ApplySpellBuff applies Hideous Laughter condition");
            Assert(conditionService.HasCondition(target, CombatConditionType.Prone),
                "ApplySpellBuff applies Prone while laughing");
            Assert(conditionService.GetConditionDuration(target, CombatConditionType.HideousLaughter) == 4,
                "Hideous Laughter duration tracks caster level",
                $"duration={conditionService.GetConditionDuration(target, CombatConditionType.HideousLaughter)}");
        }
        finally
        {
            DestroyController(caster);
            DestroyController(target);
            Object.DestroyImmediate(gmObject);
        }
    }
}
}
